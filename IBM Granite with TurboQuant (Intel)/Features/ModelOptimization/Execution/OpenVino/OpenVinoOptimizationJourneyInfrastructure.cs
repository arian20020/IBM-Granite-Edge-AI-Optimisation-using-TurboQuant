using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Execution.OpenVino;

internal sealed class OpenVinoOptimizationAttemptContextFactory(
    ModelSourceCustodyRegistry sourceCustody,
    string stagingRoot) : IOptimizationAttemptContextFactory
{
    private readonly ModelSourceCustodyRegistry _sourceCustody = sourceCustody
        ?? throw new ArgumentNullException(nameof(sourceCustody));
    private readonly string _stagingRoot = StoragePathGuard.RequireRoot(
        stagingRoot, create: true);

    internal OptimizationExecutionPlan Plan { get; set; } = null!;

    public async Task<OptimizationAttemptContext> CreateAsync(
        long generation, CancellationToken cancellationToken)
    {
        OptimizationExecutionPlan plan = Plan ?? throw new InvalidOperationException(
            "The current OpenVINO plan was not bound to staging.");
        ModelSourceCustodyKey key = SourceKey(plan);
        if (!_sourceCustody.TryAcquire(key, out ModelSourceLease? lease))
        {
            throw new FileNotFoundException("The inspected OpenVINO package is unavailable.");
        }

        string operationRoot = Path.Combine(
            _stagingRoot, $"openvino-{generation}-{Guid.NewGuid():N}");
        string stagedModel = Path.Combine(operationRoot, "openvino_model.bin");
        try
        {
            string sourceModel = Path.Combine(lease!.SourcePath, "openvino_model.bin");
            StoragePathGuard.RequireRegularFile(sourceModel);
            Directory.CreateDirectory(operationRoot);
            await using (FileStream source = new(
                sourceModel, FileMode.Open, FileAccess.Read, FileShare.Read,
                1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (FileStream destination = new(
                stagedModel, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await source.CopyToAsync(destination, 1024 * 1024, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }
            var snapshot = new StagedSourceSnapshot(
                plan.Binding.ModelSha256,
                plan.Binding.ModelLengthBytes,
                $"ovsrc-{generation}-{Guid.NewGuid():N}",
                stagedModel,
                integrityVerifier: () => SourceMatches(sourceModel, stagedModel, plan),
                cleanup: () => Cleanup(operationRoot, lease!));
            if (!snapshot.RehashMatches())
            {
                snapshot.Dispose();
                throw new InvalidDataException("The OpenVINO model changed before staging.");
            }
            return new OptimizationAttemptContext(
                generation, snapshot, $"ovstage-{generation}-{Guid.NewGuid():N}");
        }
        catch
        {
            Cleanup(operationRoot, lease!);
            throw;
        }
    }

    internal static ModelSourceCustodyKey SourceKey(OptimizationExecutionPlan plan) =>
        new(Guid.ParseExact(plan.Binding.ModelInspectionHandoffId, "N"),
            plan.Binding.ModelSha256,
            checked((long)plan.Binding.ModelLengthBytes),
            OptimizationRoute.OpenVino);

    private static bool SourceMatches(
        string source, string staged, OptimizationExecutionPlan plan)
    {
        try
        {
            return FileMatches(source, plan) && FileMatches(staged, plan);
        }
        catch { return false; }
    }

    private static bool FileMatches(string path, OptimizationExecutionPlan plan)
    {
        using FileStream stream = File.OpenRead(path);
        return (ulong)stream.Length == plan.Binding.ModelLengthBytes
            && string.Equals(Convert.ToHexString(SHA256.HashData(stream))
                .ToLowerInvariant(), plan.Binding.ModelSha256, StringComparison.Ordinal);
    }

    private static void Cleanup(string root, ModelSourceLease lease)
    {
        lease.Dispose();
        try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        catch { }
    }
}

internal sealed class OpenVinoOptimizationRevalidator(
    ModelSourceCustodyRegistry sourceCustody,
    OpenVinoOptimizationProductionAuthority authority)
    : IOptimizationRevalidator
{
    public Task<OptimizationRevalidationResult> RevalidateAsync(
        OptimizationExecutionPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!authority.MatchesCapability(plan))
        {
            return Task.FromResult(new OptimizationRevalidationResult(
                false, OptimizationSupportCode.CapabilityDrift));
        }
        try
        {
            ModelSourceCustodyKey key = OpenVinoOptimizationAttemptContextFactory.SourceKey(plan);
            if (!sourceCustody.TryAcquire(key, out ModelSourceLease? lease))
            {
                return Task.FromResult(new OptimizationRevalidationResult(
                    false, OptimizationSupportCode.SourceIdentityMismatch));
            }
            using (lease!)
            {
                OpenVinoStaticPackageInspectionResult result =
                    new OpenVinoStaticPackageInspector().Inspect(lease!.SourcePath);
                bool current = result.Evidence is { } evidence
                    && string.Equals(evidence.ModelSha256,
                        plan.Binding.ModelSha256, StringComparison.Ordinal)
                    && (ulong)evidence.ModelLengthBytes == plan.Binding.ModelLengthBytes;
                return Task.FromResult(current
                    ? OptimizationRevalidationResult.Current
                    : new OptimizationRevalidationResult(false,
                        OptimizationSupportCode.SourceIdentityMismatch));
            }
        }
        catch
        {
            return Task.FromResult(new OptimizationRevalidationResult(
                false, OptimizationSupportCode.SourceIdentityMismatch));
        }
    }
}

internal sealed class OpenVinoCurrentStateProvider(
    OpenVinoOptimizationProductionAuthority authority)
    : IOpenVinoOptimizationCurrentStateProvider
{
    public ValueTask<OpenVinoOptimizationCurrentState> GetCurrentStateAsync(
        OpenVinoOptimizationCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(authority.CurrentState);
    }
}

internal sealed class OpenVinoOptimizationExecutor(
    ModelSourceCustodyRegistry sourceCustody,
    OpenVinoOptimizationProductionAuthority authority,
    OpenVinoOptimizationService service,
    string outputRoot) : IOptimizationExecutor
{
    private readonly string _outputRoot = StoragePathGuard.RequireRoot(
        outputRoot, create: true);

    public OptimizationRoute Route => OptimizationRoute.OpenVino;
    internal string? LastPublishedDirectory { get; private set; }

    public async Task<OptimizationExecutionResult> ExecuteAsync(
        OptimizationExecutionPlan plan,
        OptimizationAttemptContext context,
        IProgress<OptimizationProgress> progress,
        CancellationToken cancellationToken)
    {
        if (plan.Route != Route || !authority.MatchesCapability(plan))
        {
            return OptimizationExecutionResult.ReplanRequired(
                plan, OptimizationSupportCode.CapabilityDrift,
                context.Source.RehashMatches(), DateTimeOffset.UtcNow);
        }
        ModelSourceCustodyKey key = OpenVinoOptimizationAttemptContextFactory.SourceKey(plan);
        if (!sourceCustody.TryAcquire(key, out ModelSourceLease? lease))
        {
            return OptimizationExecutionResult.ReplanRequired(
                plan, OptimizationSupportCode.SourceIdentityMismatch,
                context.Source.RehashMatches(), DateTimeOffset.UtcNow);
        }
        using (lease!)
        {
            string destination = Path.Combine(
                _outputRoot, $"output-{plan.OptimizationPlanId:N}-{context.Generation}");
            var routeProgress = new Progress<OpenVinoOptimizationProgress>(value =>
                progress.Report(new OptimizationProgress(
                    context.Generation,
                    plan.OptimizationPlanId,
                    plan.ConfigurationSha256,
                    Map(value.Stage),
                    Fraction(value.Stage),
                    value.Stage == OpenVinoOptimizationStage.Completed
                        ? OptimizationProgressStatusKey.Completed
                        : OptimizationProgressStatusKey.Active)));
            OptimizationExecutionResult result = await service.ExecuteAsync(
                new OpenVinoOptimizationRequest(
                    lease!.SourcePath,
                    destination,
                    plan,
                    new OpenVinoCurrentStateProvider(authority),
                    Confirmed: true),
                routeProgress,
                cancellationToken);
            LastPublishedDirectory = result.IsSuccessful ? destination : null;
            return result;
        }
    }

    private static OptimizationProgressStage Map(OpenVinoOptimizationStage value) =>
        value switch
        {
            OpenVinoOptimizationStage.Preflight => OptimizationProgressStage.Preflight,
            OpenVinoOptimizationStage.Optimizing => OptimizationProgressStage.Optimise,
            OpenVinoOptimizationStage.ValidatingOutput => OptimizationProgressStage.Validate,
            OpenVinoOptimizationStage.SmokeTesting => OptimizationProgressStage.SmokeTest,
            OpenVinoOptimizationStage.Reinspecting => OptimizationProgressStage.Reinspect,
            OpenVinoOptimizationStage.Publishing or OpenVinoOptimizationStage.Completed =>
                OptimizationProgressStage.Publish,
            _ => OptimizationProgressStage.Preflight,
        };

    private static double Fraction(OpenVinoOptimizationStage value) => value switch
    {
        OpenVinoOptimizationStage.Preflight => 0.05,
        OpenVinoOptimizationStage.Optimizing => 0.35,
        OpenVinoOptimizationStage.ValidatingOutput => 0.60,
        OpenVinoOptimizationStage.SmokeTesting => 0.75,
        OpenVinoOptimizationStage.Publishing => 0.88,
        OpenVinoOptimizationStage.Reinspecting => 0.95,
        OpenVinoOptimizationStage.Completed => 1.0,
        _ => 0.05,
    };
}
