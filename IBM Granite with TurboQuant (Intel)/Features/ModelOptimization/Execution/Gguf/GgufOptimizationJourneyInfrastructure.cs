using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;

internal sealed class GgufOptimizationAttemptContextFactory(
    ModelSourceCustodyRegistry sourceCustody,
    string stagingRoot) : IOptimizationAttemptContextFactory
{
    private readonly ModelSourceCustodyRegistry _sourceCustody = sourceCustody
        ?? throw new ArgumentNullException(nameof(sourceCustody));
    private readonly string _stagingRoot = StoragePathGuard.RequireRoot(
        stagingRoot,
        create: true);

    internal OptimizationExecutionPlan Plan { get; set; } = null!;

    public async Task<OptimizationAttemptContext> CreateAsync(
        long generation,
        CancellationToken cancellationToken)
    {
        OptimizationExecutionPlan plan = Plan
            ?? throw new InvalidOperationException(
                "The current optimization plan was not bound to staging.");
        ModelSourceCustodyKey key = SourceKey(plan);
        if (!_sourceCustody.TryAcquire(key, out ModelSourceLease? custodyLease))
        {
            throw new FileNotFoundException(
                "The inspected source is no longer available.");
        }

        string identity = $"source-{generation}-{Guid.NewGuid():N}";
        string operationRoot = Path.Combine(_stagingRoot, identity);
        string snapshotPath = Path.Combine(operationRoot, "source.gguf");
        try
        {
            OptimizationDiskSpace.Require(_stagingRoot,
                plan.Candidate.Metrics.DiskObligationBytes);
            Directory.CreateDirectory(operationRoot);
            StoragePathGuard.RequireChild(
                _stagingRoot,
                operationRoot,
                mustExist: true);
            await using (FileStream source = new(
                custodyLease!.SourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (FileStream target = new(
                snapshotPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await source.CopyToAsync(target, 1024 * 1024, cancellationToken);
                await target.FlushAsync(cancellationToken);
            }

            var snapshot = new StagedSourceSnapshot(
                plan.Binding.ModelSha256,
                plan.Binding.ModelLengthBytes,
                identity,
                snapshotPath,
                cleanup: () => Cleanup(
                    _stagingRoot,
                    operationRoot,
                    custodyLease!));
            if (!snapshot.RehashMatches())
            {
                snapshot.Dispose();
                throw new InvalidDataException(
                    "The staged source did not match the inspected model.");
            }
            return new OptimizationAttemptContext(
                generation,
                snapshot,
                identity);
        }
        catch
        {
            Cleanup(_stagingRoot, operationRoot, custodyLease!);
            throw;
        }
    }

    internal static ModelSourceCustodyKey SourceKey(
        OptimizationExecutionPlan plan) => new(
            Guid.ParseExact(plan.Binding.ModelInspectionHandoffId, "N"),
            plan.Binding.ModelSha256,
            checked((long)plan.Binding.ModelLengthBytes),
            plan.Route);

    private static void Cleanup(
        string stagingRoot,
        string operationRoot,
        ModelSourceLease lease)
    {
        lease.Dispose();
        try
        {
            string owned = StoragePathGuard.RequireChild(
                stagingRoot,
                operationRoot,
                mustExist: true);
            Directory.Delete(owned, recursive: true);
        }
        catch
        {
            // cleanup remains bounded to this exact operation directory
        }
    }
}

internal sealed class GgufOptimizationRevalidator(
    ModelSourceCustodyRegistry sourceCustody,
    GgufOptimizationProductionAuthority authority)
    : IOptimizationRevalidator
{
    private readonly ModelSourceCustodyRegistry _sourceCustody = sourceCustody
        ?? throw new ArgumentNullException(nameof(sourceCustody));
    private readonly GgufOptimizationProductionAuthority _authority = authority
        ?? throw new ArgumentNullException(nameof(authority));

    public async Task<OptimizationRevalidationResult> RevalidateAsync(
        OptimizationExecutionPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!_authority.MatchesCapability(plan))
        {
            return new(false, OptimizationSupportCode.CapabilityDrift);
        }
        ModelSourceCustodyKey key;
        try
        {
            key = GgufOptimizationAttemptContextFactory.SourceKey(plan);
        }
        catch (Exception exception) when (exception is
            ArgumentException or OverflowException or FormatException)
        {
            return new(false, OptimizationSupportCode.ModelBindingMismatch);
        }
        if (!_sourceCustody.TryAcquire(key, out ModelSourceLease? lease))
        {
            return new(false, OptimizationSupportCode.SourceIdentityMismatch);
        }
        using (lease!)
        await using (FileStream stream = new(
            lease!.SourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken);
            string sha = Convert.ToHexString(digest).ToLowerInvariant();
            return plan.MatchesSource(sha, checked((ulong)stream.Length))
                ? OptimizationRevalidationResult.Current
                : new(false, OptimizationSupportCode.SourceIdentityMismatch);
        }
    }
}
