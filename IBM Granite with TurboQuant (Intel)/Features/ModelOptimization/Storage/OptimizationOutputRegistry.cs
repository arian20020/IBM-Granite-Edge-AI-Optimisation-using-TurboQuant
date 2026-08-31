using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
                && candidate.Key.ExecutionId == result.ExecutionId
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
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.SourceSha256,
                    result.SourceSha256,
                    StringComparison.Ordinal)
                && candidate.SourceLengthBytes == result.SourceLengthBytes
                && string.Equals(
                    candidate.ProductHardwareRunId,
                    result.ProductHardwareRunId,
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.HardwareSnapshotSha256,
                    result.HardwareSnapshotSha256,
                    StringComparison.Ordinal));
        }
        if (receipt is null)
        {
            return false;
        }

        try
        {
            string publication = StoragePathGuard.RequireChild(
                _committedRoot,
                Path.Combine(_committedRoot, receipt.PublicationIdentity),
                mustExist: true);
            if (!TryGetSingleGgufFile(publication, out string publishedFile))
            {
                return false;
            }
            (string manifest, ulong size, int fileCount) =
                OptimizationOutputLease.ComputeManifest(publication);
            if (fileCount != 1
                || size != receipt.OutputSizeBytes
                || size != result.OutputSizeBytes
                || !string.Equals(
                    manifest,
                    receipt.Key.OutputManifestSha256,
                    StringComparison.Ordinal)
                || !string.Equals(
                    manifest,
                    result.OutputManifestSha256,
                    StringComparison.Ordinal)
                || !string.Equals(
                    Path.GetExtension(publishedFile),
                    ".gguf",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            using FileStream stream = File.OpenRead(publishedFile);
            string digest = Convert.ToHexString(SHA256.HashData(stream))
                .ToLowerInvariant();
            string relative = Path.GetRelativePath(publication, publishedFile)
                .Replace('\\', '/');
            byte[] canonicalRow = Encoding.UTF8.GetBytes(
                relative + "\0" + digest + "\0" + stream.Length + "\n");
            string currentManifest = Convert.ToHexString(
                SHA256.HashData(canonicalRow)).ToLowerInvariant();
            if (!string.Equals(
                    currentManifest,
                    result.OutputManifestSha256,
                    StringComparison.Ordinal))
            {
                return false;
            }
            filePath = publishedFile;
            fileSha256 = digest;
            fileLengthBytes = checked((ulong)stream.Length);
            return fileLengthBytes == size;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidOperationException
                                          or ArgumentException
                                          or OverflowException)
        {
            return false;
        }
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

    internal Task<OptimizationCommitReceipt> AdmitAsync(
        OptimizationExecutionPlan plan,
        long generation,
        StagedSourceSnapshot source,
        bool sourceUnchanged,
        SealedOptimizationCandidate candidate,
        CancellationToken cancellationToken) =>
        AdmitAsync(
            plan,
            generation,
            Guid.NewGuid(),
            source,
            sourceUnchanged,
            candidate,
            cancellationToken);

    internal async Task<OptimizationCommitReceipt> AdmitAsync(
        OptimizationExecutionPlan plan,
        long generation,
        Guid executionId,
        StagedSourceSnapshot source,
        bool sourceUnchanged,
        SealedOptimizationCandidate candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidate);
        if (generation < 1
            || executionId == Guid.Empty
            || !plan.ProducesPersistentArtifact
            || !sourceUnchanged
            || !source.RehashMatches()
            || !plan.MatchesSource(source.SourceSha256, source.SourceLengthBytes))
        {
            throw new InvalidOperationException("The output cannot be bound to an unchanged inspected source.");
        }

        string candidateRoot = candidate.CandidateRoot(_ownerToken);
        StoragePathGuard.RequireChild(_stagingRoot, candidateRoot, mustExist: true);
        if (!TryGetSingleGgufFile(candidateRoot, out _))
        {
            throw new InvalidDataException(
                "A persistent GGUF output must contain one direct regular model file.");
        }
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
            executionId,
            plan.ConfigurationSha256,
            candidate.OutputIdentity,
            candidate.OutputManifestSha256).Validate();
        string publicationIdentity = $"publication-{Guid.NewGuid():N}";
        string publicationPath = Path.Combine(_committedRoot, publicationIdentity);
        OptimizationCommitReceipt receipt = new(
            key,
            generation,
            source.SourceSha256,
            source.SourceLengthBytes,
            plan.Binding.ProductHardwareRunId,
            plan.Binding.HardwareSnapshotSha256,
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
            string candidate = StoragePathGuard.RequireChild(
                owned,
                Path.Combine(owned, "candidate"),
                mustExist: true);
            if (Directory.EnumerateDirectories(
                    candidate,
                    "*",
                    SearchOption.TopDirectoryOnly).Any())
            {
                return;
            }
            foreach (string file in Directory.EnumerateFiles(
                         candidate,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                StoragePathGuard.RequireRegularFile(file);
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            Directory.Delete(candidate, recursive: false);
            Directory.Delete(owned, recursive: false);
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
                if (!TryGetSingleGgufFile(path, out _))
                {
                    throw new InvalidDataException();
                }
                (string manifest, ulong size, int files) = OptimizationOutputLease.ComputeManifest(path);
                if (files == 0
                    || size != receipt.OutputSizeBytes
                    || !string.Equals(manifest, receipt.Key.OutputManifestSha256, StringComparison.Ordinal)
                    || _admitted.Keys.Any(existing =>
                        existing.OptimizationPlanId == receipt.Key.OptimizationPlanId)
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
            Directory.CreateDirectory(_quarantineRoot);
            StoragePathGuard.RequireChild(
                _committedRoot,
                _quarantineRoot,
                mustExist: true);
            string destination = Path.Combine(_quarantineRoot, $"rejected-{Guid.NewGuid():N}");
            Directory.Move(owned, destination);
        }
        catch
        {
            // Never broaden cleanup beyond the exact verified app-owned item.
        }
    }

    private static bool TryGetSingleGgufFile(
        string root,
        out string filePath)
    {
        filePath = string.Empty;
        if (Directory.EnumerateDirectories(
                root,
                "*",
                SearchOption.TopDirectoryOnly).Any())
        {
            return false;
        }
        string[] files = Directory.GetFiles(
            root,
            "*",
            SearchOption.TopDirectoryOnly);
        if (files.Length != 1
            || !string.Equals(
                Path.GetExtension(files[0]),
                ".gguf",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        StoragePathGuard.RequireRegularFile(files[0]);
        filePath = files[0];
        return true;
    }
}
