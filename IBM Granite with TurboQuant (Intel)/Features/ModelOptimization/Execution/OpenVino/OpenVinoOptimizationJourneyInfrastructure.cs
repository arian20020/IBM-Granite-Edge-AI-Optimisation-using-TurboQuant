using System;
using System.IO;
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

internal sealed class OpenVinoOptimizationAttemptContextFactory :
    IOptimizationAttemptContextFactory
{
    private readonly ModelSourceCustodyRegistry _sourceCustody;
    private readonly string _stagingRoot;

    internal OpenVinoOptimizationAttemptContextFactory(
        ModelSourceCustodyRegistry sourceCustody,
        string stagingRoot)
    {
        _sourceCustody = sourceCustody
            ?? throw new ArgumentNullException(nameof(sourceCustody));
        _stagingRoot = StoragePathGuard.RequireRoot(stagingRoot, create: true);
    }

    internal OptimizationExecutionPlan Plan { get; set; } = null!;

    public Task<OptimizationAttemptContext> CreateAsync(
        long generation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OptimizationExecutionPlan plan = Plan ?? throw new InvalidOperationException(
            "The current OpenVINO plan was not bound to staging.");
        ModelSourceCustodyKey key = SourceKey(plan);
        if (!_sourceCustody.TryAcquire(key, out ModelSourceLease? lease))
        {
            throw new FileNotFoundException("The inspected OpenVINO package is unavailable.");
        }

        try
        {
            if (plan.ProducesPersistentArtifact)
            {
                ulong packageBytes = 0;
                foreach (string path in Directory.EnumerateFiles(lease!.SourcePath, "*", SearchOption.AllDirectories))
                {
                    packageBytes = checked(packageBytes + (ulong)new FileInfo(path).Length);
                }
                OptimizationDiskSpace.Require(_stagingRoot,
                    OpenVinoOptimizationService.RequiredFreeDiskBytes(packageBytes));
            }
            string sourceModel = Path.Combine(lease!.SourcePath, "openvino_model.bin");
            StoragePathGuard.RequireRegularFile(sourceModel);
            var snapshot = new StagedSourceSnapshot(
                plan.Binding.ModelSha256,
                plan.Binding.ModelLengthBytes,
                $"ovsrc-{generation}-{Guid.NewGuid():N}",
                sourceModel,
                integrityVerifier: () => FileMatches(sourceModel, plan),
                cleanup: lease.Dispose);
            if (!snapshot.RehashMatches())
            {
                snapshot.Dispose();
                throw new InvalidDataException(
                    "The inspected OpenVINO source changed before execution.");
            }
            return Task.FromResult(new OptimizationAttemptContext(
                generation,
                snapshot,
                $"ovlease-{generation}-{Guid.NewGuid():N}"));
        }
        catch
        {
            lease!.Dispose();
            throw;
        }
    }

    internal static ModelSourceCustodyKey SourceKey(OptimizationExecutionPlan plan) =>
        new(Guid.ParseExact(plan.Binding.ModelInspectionHandoffId, "N"),
            plan.Binding.ModelSha256,
            checked((long)plan.Binding.ModelLengthBytes),
            OptimizationRoute.OpenVino);

    private static bool FileMatches(string path, OptimizationExecutionPlan plan)
    {
        try
        {
            StoragePathGuard.RequireRegularFile(path);
            using FileStream stream = File.OpenRead(path);
            return (ulong)stream.Length == plan.Binding.ModelLengthBytes
                && string.Equals(
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream))
                        .ToLowerInvariant(),
                    plan.Binding.ModelSha256,
                    StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidOperationException)
        {
            return false;
        }
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
    private readonly OpenVinoPublishedOutputRegistry _publishedOutputs =
        new(outputRoot);

    public OptimizationRoute Route => OptimizationRoute.OpenVino;

    internal bool TryGetPublishedOutput(
        OptimizationExecutionResult result,
        out OpenVinoPublishedOutput? publication) =>
        _publishedOutputs.TryResolve(result, out publication);

    internal Task<OpenVinoExportResult> ExportPersistentAsync(
        OptimizationExecutionResult result,
        string destinationDirectory,
        ulong maximumBytes,
        CancellationToken cancellationToken) =>
        _publishedOutputs.ExportPersistentAsync(
            result,
            destinationDirectory,
            maximumBytes,
            cancellationToken);

    internal async Task<OpenVinoOptimizationChatTarget?> CreateChatTargetAsync(
        OptimizationExecutionResult result,
        CancellationToken cancellationToken)
    {
        OpenVinoPublishedOutput? output = await _publishedOutputs.ResolveAsync(
            result,
            cancellationToken).ConfigureAwait(false);
        if (output is null)
        {
            return null;
        }

        if (output.Kind == OpenVinoPublishedOutputKind.PersistentPackage)
        {
            return new OpenVinoOptimizationChatTarget(
                result,
                output.Kind,
                output.PersistentDirectory!,
                output.RuntimeOptions,
                sourceLease: null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParseExact(
                result.ModelInspectionHandoffId,
                "N",
                out Guid handoffId)
            || !sourceCustody.TryAcquire(
                new ModelSourceCustodyKey(
                    handoffId,
                    result.SourceSha256,
                    checked((long)result.SourceLengthBytes),
                    OptimizationRoute.OpenVino),
                out ModelSourceLease? sourceLease))
        {
            return null;
        }

        return new OpenVinoOptimizationChatTarget(
            result,
            output.Kind,
            sourceLease!.SourcePath,
            output.RuntimeOptions,
            sourceLease);
    }

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
            if (result.IsSuccessful
                && !await _publishedOutputs.RegisterAsync(
                    result,
                    destination,
                    cancellationToken).ConfigureAwait(false))
            {
                return OptimizationExecutionResult.Failed(
                    plan,
                    OptimizationSupportCode.PublicationFailed,
                    context.Source.RehashMatches(),
                    DateTimeOffset.UtcNow);
            }
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
