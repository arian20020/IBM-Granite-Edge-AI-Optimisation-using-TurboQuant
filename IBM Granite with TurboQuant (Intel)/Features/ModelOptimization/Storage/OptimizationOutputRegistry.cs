using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

internal sealed class OptimizationOutputRegistry
{
    private readonly object _gate = new();
    private readonly string _stagingRoot;
    private readonly string _committedRoot;
    private readonly string _quarantineRoot;
    private readonly Guid _ownerToken = Guid.NewGuid();
    private readonly OptimizationCommitJournal _journal;
    private readonly Dictionary<OptimizationOutputKey, OptimizationCommitReceipt> _admitted = [];
    private readonly HashSet<(Guid PlanId, long Generation)> _active = [];

    internal OptimizationOutputRegistry(string stagingRoot, string committedRoot)
    {
        _stagingRoot = StoragePathGuard.RequireRoot(stagingRoot, create: true);
        _committedRoot = StoragePathGuard.RequireRoot(committedRoot, create: true);
        if (!StoragePathGuard.SameVolume(_stagingRoot, _committedRoot))
        {
            throw new InvalidOperationException("Output staging and committed roots must share one volume.");
        }
        _quarantineRoot = Path.Combine(_committedRoot, ".quarantine");
        Directory.CreateDirectory(_quarantineRoot);
        StoragePathGuard.RequireChild(_committedRoot, _quarantineRoot, mustExist: true);
        _journal = new OptimizationCommitJournal(_committedRoot);
        Recover();
    }

    internal int AdmittedCount
    {
        get { lock (_gate) { return _admitted.Count; } }
    }

    internal bool TryGetPublishedGgufFile(
        OptimizationExecutionResult result,
        out string? filePath,
        out string? fileSha256,
        out ulong fileLengthBytes)
    {
        ArgumentNullException.ThrowIfNull(result);
        filePath = null;
        fileSha256 = null;
        fileLengthBytes = 0;
        if (result.Status != OptimizationExecutionStatus.SucceededPersistent
            || result.Route != OptimizationRoute.Gguf
            || result.OutputIdentity is null
            || result.OutputManifestSha256 is null)
        {
            return false;
        }

        OptimizationCommitReceipt? receipt;
        lock (_gate)
        {
            receipt = _admitted.Values.SingleOrDefault(candidate =>
                candidate.Key.OptimizationPlanId == result.OptimizationPlanId
                && string.Equals(
                    candidate.Key.ConfigurationSha256,
                    result.ConfigurationSha256,
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.Key.OutputIdentity,
                    result.OutputIdentity,
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.Key.OutputManifestSha256,
                    result.OutputManifestSha256,
                    StringComparison.Ordinal));
        }
        if (receipt is null)
        {
            return false;
        }

        string publication = StoragePathGuard.RequireChild(
            _committedRoot,
            Path.Combine(_committedRoot, receipt.PublicationIdentity),
            mustExist: true);
        string[] files = Directory.GetFiles(
            publication,
            "*",
            SearchOption.AllDirectories);
        if (files.Length != 1
            || !string.Equals(
                Path.GetExtension(files[0]),
                ".gguf",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        StoragePathGuard.RequireRegularFile(files[0]);
        using FileStream stream = File.OpenRead(files[0]);
        string digest = Convert.ToHexString(SHA256.HashData(stream))
            .ToLowerInvariant();
        filePath = files[0];
        fileSha256 = digest;
        fileLengthBytes = checked((ulong)stream.Length);
        return true;
    }

    internal OptimizationOutputLease CreateLease(
        OptimizationExecutionPlan plan,
        long generation)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (generation < 1 || !plan.ProducesPersistentArtifact)
        {
            throw new ArgumentException("Only a persistent, current attempt can lease output staging.");
        }
        lock (_gate)
        {
            if (!_active.Add((plan.OptimizationPlanId, generation)))
            {
                throw new InvalidOperationException("This optimization attempt already has an output lease.");
            }
        }
        string identity = $"stage-{generation}-{Guid.NewGuid():N}";
        string operationRoot = Path.Combine(_stagingRoot, identity);
        string candidateRoot = Path.Combine(operationRoot, "candidate");
        try
        {
            Directory.CreateDirectory(candidateRoot);
            StoragePathGuard.RequireChild(_stagingRoot, candidateRoot, mustExist: true);
            return new OptimizationOutputLease(
                _stagingRoot,
                candidateRoot,
                identity,
                _ownerToken,
                () => ReleaseLease(plan.OptimizationPlanId, generation, operationRoot));
        }
        catch
        {
            lock (_gate) { _active.Remove((plan.OptimizationPlanId, generation)); }
            throw;
        }
    }

    internal async Task<OptimizationCommitReceipt> AdmitAsync(
        OptimizationExecutionPlan plan,
        long generation,
        StagedSourceSnapshot source,
        bool sourceUnchanged,
        SealedOptimizationCandidate candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidate);
        if (generation < 1
            || !plan.ProducesPersistentArtifact
            || !sourceUnchanged
            || !source.RehashMatches()
            || !plan.MatchesSource(source.SourceSha256, source.SourceLengthBytes))
        {
            throw new InvalidOperationException("The output cannot be bound to an unchanged inspected source.");
        }

        string candidateRoot = candidate.CandidateRoot(_ownerToken);
        StoragePathGuard.RequireChild(_stagingRoot, candidateRoot, mustExist: true);
        if (!StoragePathGuard.SameVolume(candidateRoot, _committedRoot))
        {
            throw new InvalidOperationException("Cross-volume output promotion is prohibited.");
        }
        (string manifest, ulong size, int files) = OptimizationOutputLease.ComputeManifest(candidateRoot);
        if (files == 0
            || size != candidate.OutputSizeBytes
            || !string.Equals(manifest, candidate.OutputManifestSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The sealed output changed before admission.");
        }

        var key = new OptimizationOutputKey(
            plan.Route,
            plan.OptimizationPlanId,
            plan.ConfigurationSha256,
            candidate.OutputIdentity,
            candidate.OutputManifestSha256).Validate();
        string publicationIdentity = $"publication-{Guid.NewGuid():N}";
        string publicationPath = Path.Combine(_committedRoot, publicationIdentity);
        OptimizationCommitReceipt receipt = new(
            key,
            generation,
            source.SourceSha256,
            SourceUnchanged: true,
            candidate.OutputSizeBytes,
            candidate.SealedStagingIdentity,
            publicationIdentity);
        receipt.Validate();

        lock (_gate)
        {
            if (!_active.Contains((plan.OptimizationPlanId, generation))
                || _admitted.Keys.Any(existing => existing.OptimizationPlanId == plan.OptimizationPlanId))
            {
                throw new InvalidOperationException("A stale or duplicate terminal output was rejected.");
            }
        }

        Directory.Move(candidateRoot, publicationPath);
        try
        {
            List<OptimizationCommitReceipt> next;
            lock (_gate)
            {
                next = _admitted.Values.Append(receipt).ToList();
            }
            await _journal.ReplaceAsync(next, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                _admitted.Add(key, receipt);
                _active.Remove((plan.OptimizationPlanId, generation));
            }
            return receipt;
        }
        catch
        {
            // The moved directory deliberately remains unadmitted. Recovery will
            // quarantine it because success is never exposed without a receipt.
            lock (_gate) { _active.Remove((plan.OptimizationPlanId, generation)); }
            throw;
        }
    }

    private void ReleaseLease(Guid planId, long generation, string operationRoot)
    {
        lock (_gate)
        {
            _active.Remove((planId, generation));
        }
        try
        {
            string owned = StoragePathGuard.RequireChild(
                _stagingRoot,
                operationRoot,
                mustExist: true);
            foreach (string file in Directory.EnumerateFiles(owned, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(owned, recursive: true);
        }
        catch
        {
            // Cleanup remains bounded to the exact app-created operation root.
        }
    }

    private void Recover()
    {
        IReadOnlyList<OptimizationCommitReceipt> receipts;
        try
        {
            receipts = _journal.Load();
        }
        catch
        {
            receipts = [];
        }
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (OptimizationCommitReceipt receipt in receipts)
        {
            try
            {
                receipt.Validate();
                string path = StoragePathGuard.RequireChild(
                    _committedRoot,
                    Path.Combine(_committedRoot, receipt.PublicationIdentity),
                    mustExist: true);
                (string manifest, ulong size, int files) = OptimizationOutputLease.ComputeManifest(path);
                if (files == 0
                    || size != receipt.OutputSizeBytes
                    || !string.Equals(manifest, receipt.Key.OutputManifestSha256, StringComparison.Ordinal)
                    || !_admitted.TryAdd(receipt.Key, receipt))
                {
                    throw new InvalidDataException();
                }
                referenced.Add(receipt.PublicationIdentity);
            }
            catch
            {
                // Invalid receipts do not reconstruct public state.
            }
        }

        foreach (string path in Directory.EnumerateDirectories(_committedRoot, "publication-*", SearchOption.TopDirectoryOnly))
        {
            string identity = Path.GetFileName(path);
            if (!referenced.Contains(identity))
            {
                QuarantineOwned(path);
            }
        }
    }

    private void QuarantineOwned(string path)
    {
        try
        {
            string owned = StoragePathGuard.RequireChild(_committedRoot, path, mustExist: true);
            string destination = Path.Combine(_quarantineRoot, $"rejected-{Guid.NewGuid():N}");
            Directory.Move(owned, destination);
        }
        catch
        {
            // Never broaden cleanup beyond the exact verified app-owned item.
        }
    }
}
