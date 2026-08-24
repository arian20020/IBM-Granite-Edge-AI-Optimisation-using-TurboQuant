using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

public enum OpenVinoOptimizationStatus
{
    Published,
    Failed,
    Cancelled,
    ReplanRequired
}

public enum OpenVinoOptimizationStage
{
    Preflight, Optimizing, ValidatingOutput, SmokeTesting, Publishing, Reinspecting, Completed
}

public sealed record OpenVinoOptimizationCurrentState(
    OptimizationCapabilitySnapshot Capabilities,
    OpenVinoOptimizationCapabilityEvidence CapabilityEvidence,
    string ModelInspectionRunId,
    string ModelInspectionHandoffId,
    string ProductHardwareRunId,
    string HardwareSnapshotSha256);

public enum OpenVinoOptimizationCheckpoint
{
    InitialPreflight,
    BeforeStaging,
    BeforePublish
}

public interface IOpenVinoOptimizationCurrentStateProvider
{
    ValueTask<OpenVinoOptimizationCurrentState> GetCurrentStateAsync(
        OpenVinoOptimizationCheckpoint checkpoint,
        CancellationToken cancellationToken);
}

public sealed record OpenVinoOptimizationRequest(
    string SourceDirectory,
    string DestinationDirectory,
    OptimizationExecutionPlan Plan,
    IOpenVinoOptimizationCurrentStateProvider CurrentStateProvider,
    bool Confirmed);

public sealed record OpenVinoOptimizationLegacyRequestV1(
    string SourceDirectory,
    string DestinationDirectory,
    OpenVinoOptimizationCandidate Candidate,
    bool Confirmed);

public sealed record OpenVinoOptimizationProgress(
    Guid OperationId,
    OpenVinoOptimizationStage Stage);

public sealed record OpenVinoOptimizationResult(
    OpenVinoOptimizationStatus Status,
    Guid OperationId,
    Guid InspectionRunId,
    OpenVinoSupportCode? SupportCode,
    OpenVinoWeightPrecision? ActualWeightPrecision,
    OpenVinoKvCachePrecision? ActualKvCachePrecision,
    string? ActualDevice)
{
    internal string? PublishedDirectory { get; init; }
    internal string? OutputIdentity { get; init; }
    internal string? OutputManifestSha256 { get; init; }
    internal ulong OutputSizeBytes { get; init; }
    internal OptimizationSupportCode? ExecutionSupportCode { get; init; }
    public OptimizationSupportCode? ReplanSupportCode { get; init; }
}

internal sealed record OpenVinoOptimizationInvocation(
    Guid OperationId,
    string SourceDirectory,
    string StagingDirectory,
    string SourceManifestSha256,
    OpenVinoOptimizationCandidate Candidate);

internal sealed record OpenVinoOptimizationCompletion(
    OpenVinoWeightPrecision ActualWeightPrecision,
    IReadOnlyDictionary<string, string> Versions)
{
    internal static OpenVinoOptimizationCompletion CreateTestInstance(
        OpenVinoWeightPrecision precision) => new(
            precision,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["nncf"] = "3.3.0",
                ["openvino"] = "2026.3.0",
                ["openvino-genai"] = "2026.3.0.0",
                ["optimum"] = "2.3.0",
                ["optimum-intel"] = "2.1.0",
                ["transformers"] = "5.5.4"
            });
}

internal sealed class OpenVinoOptimizationValidation : IDisposable
{
    private IDisposable? retainedLease;

    internal OpenVinoOptimizationValidation(Guid validationRunId, IDisposable? retainedLease = null)
    {
        if (validationRunId == Guid.Empty) throw new ArgumentException(
            "A validation identity is required.", nameof(validationRunId));
        ValidationRunId = validationRunId;
        this.retainedLease = retainedLease;
    }

    internal Guid ValidationRunId { get; }
    internal IDisposable ConsumeLease() => Interlocked.Exchange(ref retainedLease, null) ??
        throw new OpenVinoOptimizationException(OpenVinoSupportCode.ConversionOutputInvalid);
    public void Dispose() => Interlocked.Exchange(ref retainedLease, null)?.Dispose();
    internal static OpenVinoOptimizationValidation CreateTestInstance() => new(Guid.NewGuid());
}

internal sealed record OpenVinoRuntimeOptimizationEvidence(
    string ActualDevice,
    OpenVinoKvCachePrecision ActualKvCachePrecision,
    string GenerationDisposition,
    string QualityDisposition);

internal interface IOpenVinoOptimizationPipeline
{
    Task<OpenVinoOptimizationCompletion> OptimizeAsync(
        OpenVinoOptimizationInvocation invocation,
        CancellationToken cancellationToken);
    Task<OpenVinoOptimizationValidation> ValidateAsync(
        string stagingDirectory,
        CancellationToken cancellationToken);
    Task<OpenVinoRuntimeOptimizationEvidence> SmokeAsync(
        OpenVinoOptimizationValidation validation,
        string stagingDirectory,
        OpenVinoOptimizationCandidate candidate,
        CancellationToken cancellationToken);
    Task<Guid> ReinspectPublishedAsync(
        string destinationDirectory,
        CancellationToken cancellationToken);
}

public sealed class OpenVinoOptimizationService
{
    private readonly IOpenVinoOptimizationPipeline pipeline;
    private readonly Func<string, string, bool> hasSufficientSpace;
    private readonly Func<Guid> operationIdFactory;

    internal OpenVinoOptimizationService(
        IOpenVinoOptimizationPipeline pipeline,
        Func<string, bool> hasSufficientSpace,
        Func<Guid>? operationIdFactory = null)
    {
        this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        ArgumentNullException.ThrowIfNull(hasSufficientSpace);
        this.hasSufficientSpace = (_, destination) => hasSufficientSpace(destination);
        this.operationIdFactory = operationIdFactory ?? Guid.NewGuid;
    }

    internal OpenVinoOptimizationService(IOpenVinoOptimizationPipeline pipeline)
    {
        this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        hasSufficientSpace = HasSufficientSpace;
        operationIdFactory = Guid.NewGuid;
    }

    public async Task<OpenVinoOptimizationResult> OptimizeAsync(
        OpenVinoOptimizationRequest request,
        IProgress<OpenVinoOptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Plan);
        ArgumentNullException.ThrowIfNull(request.CurrentStateProvider);
        return await OptimizeCoreAsync(
            request.SourceDirectory,
            request.DestinationDirectory,
            request.Confirmed,
            request.Plan,
            request.CurrentStateProvider,
            legacyCandidate: null,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OptimizationExecutionResult> ExecuteAsync(
        OpenVinoOptimizationRequest request,
        IProgress<OpenVinoOptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Plan);
        OpenVinoOptimizationResult routeResult = await OptimizeAsync(
            request,
            progress,
            cancellationToken).ConfigureAwait(false);
        DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
        if (routeResult.Status == OpenVinoOptimizationStatus.Published)
        {
            return OptimizationExecutionResult.Succeeded(
                request.Plan,
                routeResult.OutputIdentity ?? throw new InvalidDataException(
                    "optimization_output_invalid"),
                routeResult.OutputManifestSha256 ?? throw new InvalidDataException(
                    "optimization_output_invalid"),
                routeResult.OutputSizeBytes,
                sourceUnchanged: true,
                completedAtUtc);
        }
        if (routeResult.Status == OpenVinoOptimizationStatus.ReplanRequired)
        {
            return OptimizationExecutionResult.ReplanRequired(
                request.Plan,
                routeResult.ReplanSupportCode ??
                    OptimizationSupportCode.ModelBindingMismatch,
                completedAtUtc);
        }
        if (routeResult.Status == OpenVinoOptimizationStatus.Cancelled)
        {
            return OptimizationExecutionResult.Cancelled(request.Plan, completedAtUtc);
        }
        return OptimizationExecutionResult.Failed(
            request.Plan,
            routeResult.ExecutionSupportCode ?? MapFailure(routeResult.SupportCode),
            completedAtUtc);
    }

    public async Task<OpenVinoOptimizationResult> OptimizeLegacyV1Async(
        OpenVinoOptimizationLegacyRequestV1 request,
        IProgress<OpenVinoOptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Confirmed ||
            !OpenVinoOptimizationLegacyRegistryV1.IsRegistered(request.Candidate))
        {
            return Failed(
                operationIdFactory(),
                OpenVinoSupportCode.OptimizationUnsupported);
        }
        return await OptimizeCoreAsync(
            request.SourceDirectory,
            request.DestinationDirectory,
            request.Confirmed,
            plan: null,
            currentStateProvider: null,
            request.Candidate,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<OpenVinoOptimizationResult> OptimizeCoreAsync(
        string sourceDirectory,
        string destinationDirectory,
        bool confirmed,
        OptimizationExecutionPlan? plan,
        IOpenVinoOptimizationCurrentStateProvider? currentStateProvider,
        OpenVinoOptimizationCandidate? legacyCandidate,
        IProgress<OpenVinoOptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        Guid operationId = operationIdFactory();
        if (!confirmed || plan is null && legacyCandidate is null)
        {
            return Failed(operationId, OpenVinoSupportCode.OptimizationUnsupported);
        }

        OpenVinoOptimizationCandidate? candidate = legacyCandidate;
        OpenVinoOptimizationCurrentState? initialCurrentState = null;
        OpenVinoPackageSnapshot? source = null;
        OpenVinoOptimizationValidation? validation = null;
        TrustedSourceContext? trustedSource = null;
        string admittedSourceDirectory = sourceDirectory;
        try
        {
            Report(progress, operationId, OpenVinoOptimizationStage.Preflight);
            cancellationToken.ThrowIfCancellationRequested();
            if (plan is not null)
            {
                if (currentStateProvider is null)
                {
                    return Replan(
                        operationId,
                        OptimizationSupportCode.CapabilityDrift);
                }
                initialCurrentState = await currentStateProvider.GetCurrentStateAsync(
                    OpenVinoOptimizationCheckpoint.InitialPreflight,
                    cancellationToken).ConfigureAwait(false);
                if (initialCurrentState?.Capabilities is null)
                {
                    return Replan(
                        operationId,
                        OptimizationSupportCode.CapabilityDrift);
                }
                OptimizationSupportCode bindingFailure = ValidateCurrentBinding(
                    plan, initialCurrentState);
                if (bindingFailure != OptimizationSupportCode.None)
                {
                    return Replan(operationId, bindingFailure);
                }
                try
                {
                    trustedSource = TrustedSourceContext.ForPlan(
                        plan,
                        Path.Combine(sourceDirectory, "openvino_model.bin"));
                }
                catch (Exception exception) when (exception is ArgumentException or
                                                  NotSupportedException or
                                                  PathTooLongException)
                {
                    return Replan(
                        operationId,
                        OptimizationSupportCode.SourceIdentityMismatch);
                }
                admittedSourceDirectory = RevealTrustedPackageRoot(
                    plan, trustedSource, sourceDirectory) ?? string.Empty;
                if (admittedSourceDirectory.Length == 0)
                {
                    return Replan(
                        operationId,
                        OptimizationSupportCode.SourceIdentityMismatch);
                }
            }
            OpenVinoPackageSnapshotCapture capture =
                new OpenVinoPackageSnapshotter().Capture(admittedSourceDirectory);
            source = capture.Snapshot;
            if (source is null)
            {
                return Failed(operationId, OpenVinoSupportCode.PackageUnreadable);
            }
            OpenVinoStaticPackageInspectionResult inspection =
                new OpenVinoStaticPackageInspector().Inspect(admittedSourceDirectory);
            if (inspection.Status != OpenVinoStaticInspectionStatus.NativeValidationRequired ||
                inspection.Evidence is null)
            {
                return Failed(operationId, inspection.SupportCode ??
                    OpenVinoSupportCode.ConversionPreflightFailed);
            }

            if (plan is not null)
            {
                OpenVinoOptimizationAdaptation adaptation =
                    OpenVinoOptimizationPlanAdapter.Adapt(
                        plan,
                        initialCurrentState!.Capabilities,
                        initialCurrentState.CapabilityEvidence,
                        inspection.Evidence.ModelSha256,
                        checked((ulong)inspection.Evidence.ModelLengthBytes));
                if (adaptation.Status ==
                    OpenVinoOptimizationAdaptationStatus.ReplanRequired)
                {
                    return Replan(operationId, adaptation.SupportCode);
                }

                candidate = adaptation.Candidate;
            }

            if (candidate is null || candidate.PersistentArtifact is null)
            {
                if (plan is null || candidate is null ||
                    candidate.WeightPrecision != candidate.SourceWeightPrecision ||
                    candidate.ExecutionPayload is null ||
                    plan.ProducesPersistentArtifact)
                {
                    return Failed(operationId, OpenVinoSupportCode.OptimizationUnsupported);
                }
            }

            try
            {
                OpenVinoRuntimeTechnicalConfiguration.From(candidate)
                    .ValidateSupported();
            }
            catch (OpenVinoOptimizationException)
            {
                return plan is null
                    ? Failed(operationId, OpenVinoSupportCode.OptimizationUnsupported)
                    : Replan(operationId, OptimizationSupportCode.ToolNotAdmitted);
            }

            if (candidate.PersistentArtifact is null)
            {
                OpenVinoOptimizationResult runtimeProfile =
                    await StoreRuntimeProfileAsync(
                        operationId,
                        admittedSourceDirectory,
                        destinationDirectory,
                        plan!,
                        currentStateProvider!,
                        trustedSource!,
                        candidate,
                        source,
                        cancellationToken).ConfigureAwait(false);
                if (runtimeProfile.Status == OpenVinoOptimizationStatus.Published)
                {
                    Report(progress, operationId, OpenVinoOptimizationStage.Completed);
                }
                return runtimeProfile;
            }
            if (!hasSufficientSpace(admittedSourceDirectory, destinationDirectory))
            {
                return Failed(
                    operationId,
                    OpenVinoSupportCode.ConversionPreflightFailed,
                    OptimizationSupportCode.InsufficientDiskSpace);
            }

            OpenVinoWeightPrecision sourcePrecision =
                OpenVinoOptimizationProvenance.ReadSourcePrecision(source);
            OpenVinoWeightPrecision targetPrecision =
                candidate.PersistentArtifact.WeightPrecision;
            if (plan is not null &&
                sourcePrecision != candidate.SourceWeightPrecision)
            {
                return Replan(
                    operationId,
                    OptimizationSupportCode.SourceIdentityMismatch);
            }
            if (!CanOptimize(sourcePrecision, targetPrecision))
            {
                return Failed(operationId, OpenVinoSupportCode.OptimizationUnsupported);
            }

            if (plan is not null)
            {
                OpenVinoOptimizationAdaptation beforeStaging =
                    await RevalidatePlanAsync(
                        plan,
                        currentStateProvider!,
                        trustedSource!,
                        admittedSourceDirectory,
                        OpenVinoOptimizationCheckpoint.BeforeStaging,
                        cancellationToken).ConfigureAwait(false);
                if (beforeStaging.Status ==
                    OpenVinoOptimizationAdaptationStatus.ReplanRequired)
                {
                    return Replan(operationId, beforeStaging.SupportCode);
                }
                candidate = beforeStaging.Candidate!;
            }
            using ConversionTransaction transaction = ConversionTransaction.Create(
                admittedSourceDirectory,
                destinationDirectory,
                operationId);
            Report(progress, operationId, OpenVinoOptimizationStage.Optimizing);
            cancellationToken.ThrowIfCancellationRequested();
            OpenVinoOptimizationCompletion completion = await pipeline.OptimizeAsync(
                new OpenVinoOptimizationInvocation(
                    operationId,
                    admittedSourceDirectory,
                    transaction.StagingDirectory,
                    inspection.Evidence.PackageManifestDigest,
                    candidate),
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (completion.ActualWeightPrecision != targetPrecision)
            {
                return Failed(operationId, OpenVinoSupportCode.ConversionOutputInvalid);
            }
            if (candidate.ExecutionPayload is not null &&
                !VersionsMatch(
                    candidate.ExecutionPayload.OptimizerVersions,
                    completion.Versions))
            {
                return Failed(operationId, OpenVinoSupportCode.ConversionOutputInvalid);
            }
            if (source.ValidateStillCurrent() != OpenVinoSnapshotFailure.None)
            {
                return plan is null
                    ? Failed(operationId, OpenVinoSupportCode.PackageChanged)
                    : Replan(operationId,
                        OptimizationSupportCode.SourceIdentityMismatch);
            }

            Report(progress, operationId, OpenVinoOptimizationStage.ValidatingOutput);
            cancellationToken.ThrowIfCancellationRequested();
            validation = await pipeline.ValidateAsync(
                transaction.StagingDirectory, cancellationToken).ConfigureAwait(false);
            Report(progress, operationId, OpenVinoOptimizationStage.SmokeTesting);
            cancellationToken.ThrowIfCancellationRequested();
            OpenVinoRuntimeOptimizationEvidence runtime = await pipeline.SmokeAsync(
                validation,
                transaction.StagingDirectory,
                candidate,
                cancellationToken).ConfigureAwait(false);
            string expectedDevice = candidate.ExecutionPayload?.Device ?? candidate.Device;
            OpenVinoKvCachePrecision expectedKvCache = candidate.ExecutionPayload is null
                ? candidate.Runtime.KvCachePrecision
                : MapKvCache(candidate.ExecutionPayload.KvCachePrecision);
            if (runtime.ActualDevice != expectedDevice ||
                runtime.ActualKvCachePrecision != expectedKvCache ||
                runtime.GenerationDisposition != "passed" ||
                runtime.QualityDisposition != "passed")
            {
                return Failed(operationId, OpenVinoSupportCode.ConversionOutputInvalid);
            }
            if (source.ValidateStillCurrent() != OpenVinoSnapshotFailure.None)
            {
                return plan is null
                    ? Failed(operationId, OpenVinoSupportCode.PackageChanged)
                    : Replan(operationId,
                        OptimizationSupportCode.SourceIdentityMismatch);
            }

            IReadOnlyList<OpenVinoOutputArtifact> output =
                OpenVinoOptimizationProvenance.CaptureOutput(transaction.StagingDirectory);
            string outputManifestSha256 =
                OpenVinoProvenance.ComputeOutputManifestDigest(output);
            OpenVinoOptimizationProvenance provenance = plan is null
                ? new OpenVinoOptimizationProvenance(
                    OpenVinoOptimizationProvenance.LegacySchemaVersion,
                    operationId,
                    validation.ValidationRunId,
                    inspection.Evidence.PackageManifestDigest,
                    candidate.ConfigurationId,
                    sourcePrecision,
                    targetPrecision,
                    candidate.Runtime.KvCachePrecision,
                    runtime.ActualDevice,
                    runtime.ActualKvCachePrecision,
                    completion.Versions,
                    output,
                    outputManifestSha256,
                    "passed",
                    runtime.GenerationDisposition,
                    runtime.QualityDisposition)
                : OpenVinoOptimizationProvenance.CreatePlanBound(
                    plan,
                    operationId,
                    validation.ValidationRunId,
                    inspection.Evidence.PackageManifestDigest,
                    candidate,
                    runtime,
                    completion.Versions,
                    output,
                    outputManifestSha256);
            provenance.Write(transaction.StagingDirectory);

            Report(progress, operationId, OpenVinoOptimizationStage.Publishing);
            cancellationToken.ThrowIfCancellationRequested();
            if (plan is not null)
            {
                OpenVinoOptimizationAdaptation beforePublish =
                    await RevalidatePlanAsync(
                        plan,
                        currentStateProvider!,
                        trustedSource!,
                        admittedSourceDirectory,
                        OpenVinoOptimizationCheckpoint.BeforePublish,
                        cancellationToken).ConfigureAwait(false);
                if (beforePublish.Status ==
                    OpenVinoOptimizationAdaptationStatus.ReplanRequired)
                {
                    return Replan(operationId, beforePublish.SupportCode);
                }
            }
            if (source.ValidateStillCurrent() != OpenVinoSnapshotFailure.None)
            {
                return plan is null
                    ? Failed(operationId, OpenVinoSupportCode.PackageChanged)
                    : Replan(operationId,
                        OptimizationSupportCode.SourceIdentityMismatch);
            }
            transaction.Publish();
            Guid inspectionRunId;
            try
            {
                Report(progress, operationId, OpenVinoOptimizationStage.Reinspecting);
                cancellationToken.ThrowIfCancellationRequested();
                inspectionRunId = await pipeline.ReinspectPublishedAsync(
                    transaction.DestinationDirectory,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                if (!transaction.TryRollbackPublished())
                {
                    throw new ConversionTransactionException(
                        OpenVinoSupportCode.ConversionPublishFailed);
                }
                if (failure is OperationCanceledException &&
                    cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                return Failed(
                    operationId,
                    OpenVinoSupportCode.ConversionOutputInvalid,
                    OptimizationSupportCode.ReinspectionFailed);
            }
            Report(progress, operationId, OpenVinoOptimizationStage.Completed);
            return new OpenVinoOptimizationResult(
                OpenVinoOptimizationStatus.Published,
                operationId,
                inspectionRunId,
                SupportCode: null,
                completion.ActualWeightPrecision,
                runtime.ActualKvCachePrecision,
                runtime.ActualDevice)
            {
                PublishedDirectory = transaction.DestinationDirectory,
                OutputIdentity = "openvino-package-v2-" + outputManifestSha256,
                OutputManifestSha256 = outputManifestSha256,
                OutputSizeBytes = checked((ulong)output.Sum(static file => file.Length))
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new OpenVinoOptimizationResult(
                OpenVinoOptimizationStatus.Cancelled,
                operationId,
                Guid.Empty,
                OpenVinoSupportCode.OperationCancelled,
                null,
                null,
                null);
        }
        catch (Exception exception) when (exception is OpenVinoOptimizationException or
                                          ConversionTransactionException or InvalidDataException)
        {
            OpenVinoSupportCode code = exception switch
            {
                OpenVinoOptimizationException optimization => optimization.SupportCode,
                ConversionTransactionException transaction => transaction.SupportCode,
                _ => OpenVinoSupportCode.ConversionOutputInvalid
            };
            return Failed(operationId, code);
        }
        catch (Exception)
        {
            return Failed(operationId, OpenVinoSupportCode.OptimizationUnsupported);
        }
        finally
        {
            validation?.Dispose();
            source?.Dispose();
        }
    }

    private static bool CanOptimize(
        OpenVinoWeightPrecision source,
        OpenVinoWeightPrecision target) =>
        target switch
        {
            OpenVinoWeightPrecision.Fp16 => source == OpenVinoWeightPrecision.Fp16,
            OpenVinoWeightPrecision.EightBit => source is OpenVinoWeightPrecision.Fp16 or
                OpenVinoWeightPrecision.EightBit,
            OpenVinoWeightPrecision.FourBit => true,
            _ => false
        };

    private static async ValueTask<OpenVinoOptimizationAdaptation> RevalidatePlanAsync(
        OptimizationExecutionPlan plan,
        IOpenVinoOptimizationCurrentStateProvider currentStateProvider,
        TrustedSourceContext trustedSource,
        string sourceDirectory,
        OpenVinoOptimizationCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        OpenVinoOptimizationCurrentState currentState =
            await currentStateProvider.GetCurrentStateAsync(
                checkpoint, cancellationToken).ConfigureAwait(false);
        if (currentState?.Capabilities is null)
        {
            return new OpenVinoOptimizationAdaptation(
                OpenVinoOptimizationAdaptationStatus.ReplanRequired,
                Candidate: null,
                OptimizationSupportCode.CapabilityDrift);
        }
        OptimizationSupportCode bindingFailure = ValidateCurrentBinding(
            plan, currentState);
        if (bindingFailure != OptimizationSupportCode.None)
        {
            return new OpenVinoOptimizationAdaptation(
                OpenVinoOptimizationAdaptationStatus.ReplanRequired,
                Candidate: null,
                bindingFailure);
        }
        string? admittedSourceDirectory = RevealTrustedPackageRoot(
            plan, trustedSource, sourceDirectory);
        if (admittedSourceDirectory is null)
        {
            return new OpenVinoOptimizationAdaptation(
                OpenVinoOptimizationAdaptationStatus.ReplanRequired,
                Candidate: null,
                OptimizationSupportCode.SourceIdentityMismatch);
        }
        OpenVinoStaticPackageInspectionResult current =
            new OpenVinoStaticPackageInspector().Inspect(admittedSourceDirectory);
        if (current.Status != OpenVinoStaticInspectionStatus.NativeValidationRequired ||
            current.Evidence is null)
        {
            return new OpenVinoOptimizationAdaptation(
                OpenVinoOptimizationAdaptationStatus.ReplanRequired,
                Candidate: null,
                OptimizationSupportCode.SourceIdentityMismatch);
        }
        return OpenVinoOptimizationPlanAdapter.Adapt(
            plan,
            currentState.Capabilities,
            currentState.CapabilityEvidence,
            current.Evidence.ModelSha256,
            checked((ulong)current.Evidence.ModelLengthBytes));
    }

    private static OptimizationSupportCode ValidateCurrentBinding(
        OptimizationExecutionPlan plan,
        OpenVinoOptimizationCurrentState currentState)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(currentState);
        if (!string.Equals(
                currentState.ModelInspectionRunId,
                plan.Binding.ModelInspectionRunId,
                StringComparison.Ordinal) ||
            !string.Equals(
                currentState.ModelInspectionHandoffId,
                plan.Binding.ModelInspectionHandoffId,
                StringComparison.Ordinal))
        {
            return OptimizationSupportCode.ModelBindingMismatch;
        }
        if (!string.Equals(
                currentState.ProductHardwareRunId,
                plan.Binding.ProductHardwareRunId,
                StringComparison.Ordinal) ||
            !string.Equals(
                currentState.HardwareSnapshotSha256,
                plan.Binding.HardwareSnapshotSha256,
                StringComparison.Ordinal))
        {
            return OptimizationSupportCode.HardwareBindingMismatch;
        }
        return OptimizationSupportCode.None;
    }

    private static async Task<OpenVinoOptimizationResult> StoreRuntimeProfileAsync(
        Guid operationId,
        string sourceDirectory,
        string destinationDirectory,
        OptimizationExecutionPlan plan,
        IOpenVinoOptimizationCurrentStateProvider currentStateProvider,
        TrustedSourceContext trustedSource,
        OpenVinoOptimizationCandidate candidate,
        OpenVinoPackageSnapshot source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OpenVinoOptimizationAdaptation beforeStaging = await RevalidatePlanAsync(
            plan,
            currentStateProvider,
            trustedSource,
            sourceDirectory,
            OpenVinoOptimizationCheckpoint.BeforeStaging,
            cancellationToken).ConfigureAwait(false);
        if (beforeStaging.Status ==
                OpenVinoOptimizationAdaptationStatus.ReplanRequired ||
            source.ValidateStillCurrent() != OpenVinoSnapshotFailure.None)
        {
            return Replan(
                operationId,
                beforeStaging.Status ==
                    OpenVinoOptimizationAdaptationStatus.ReplanRequired
                        ? beforeStaging.SupportCode
                        : OptimizationSupportCode.SourceIdentityMismatch);
        }
        using ConversionTransaction transaction = ConversionTransaction.Create(
            sourceDirectory,
            destinationDirectory,
            operationId);
        OpenVinoRuntimeOptimizationProfile profile =
            OpenVinoRuntimeOptimizationProfile.From(
                plan, candidate, DateTimeOffset.UtcNow);
        byte[] profileBytes = profile.Serialize();
        string profilePath = Path.Combine(
            transaction.StagingDirectory,
            OpenVinoRuntimeOptimizationProfile.FileName);
        await File.WriteAllBytesAsync(
            profilePath, profileBytes, cancellationToken).ConfigureAwait(false);
        string digest = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(profileBytes))
            .ToLowerInvariant();

        OpenVinoOptimizationAdaptation beforePublish = await RevalidatePlanAsync(
            plan,
            currentStateProvider,
            trustedSource,
            sourceDirectory,
            OpenVinoOptimizationCheckpoint.BeforePublish,
            cancellationToken).ConfigureAwait(false);
        if (beforePublish.Status ==
                OpenVinoOptimizationAdaptationStatus.ReplanRequired ||
            source.ValidateStillCurrent() != OpenVinoSnapshotFailure.None)
        {
            return Replan(
                operationId,
                beforePublish.Status ==
                    OpenVinoOptimizationAdaptationStatus.ReplanRequired
                        ? beforePublish.SupportCode
                        : OptimizationSupportCode.SourceIdentityMismatch);
        }
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Publish();
        return new OpenVinoOptimizationResult(
            OpenVinoOptimizationStatus.Published,
            operationId,
            Guid.Empty,
            SupportCode: null,
            candidate.WeightPrecision,
            candidate.Runtime.KvCachePrecision,
            candidate.Device)
        {
            PublishedDirectory = transaction.DestinationDirectory,
            OutputIdentity = "openvino-runtime-profile-v2-" + digest,
            OutputManifestSha256 = digest,
            OutputSizeBytes = checked((ulong)profileBytes.Length)
        };
    }

    private static string? RevealTrustedPackageRoot(
        OptimizationExecutionPlan plan,
        TrustedSourceContext trustedSource,
        string sourceDirectory)
    {
        try
        {
            if (!trustedSource.Verify(plan).IsVerified)
            {
                return null;
            }
            string revealedModel = trustedSource.RevealVerifiedPath(plan);
            string? revealedParent = Path.GetDirectoryName(revealedModel);
            string suppliedRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(sourceDirectory));
            if (revealedParent is null ||
                !string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(revealedParent)),
                    suppliedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return suppliedRoot;
        }
        catch (Exception exception) when (exception is ArgumentException or
                                          InvalidOperationException or
                                          NotSupportedException or
                                          PathTooLongException or
                                          IOException or
                                          UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool VersionsMatch(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual) =>
        expected.Count == actual.Count && expected.All(version =>
            actual.TryGetValue(version.Key, out string? actualVersion) &&
            string.Equals(version.Value, actualVersion, StringComparison.Ordinal));

    private static OpenVinoKvCachePrecision MapKvCache(
        GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution
            .OpenVinoKvCachePrecision value) => value switch
        {
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution
                .OpenVinoKvCachePrecision.ReleasedDefault =>
                    OpenVinoKvCachePrecision.ReleasedDefault,
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution
                .OpenVinoKvCachePrecision.U8 => OpenVinoKvCachePrecision.U8,
            _ => throw new OpenVinoOptimizationException(
                OpenVinoSupportCode.OptimizationUnsupported)
        };

    private static void Report(
        IProgress<OpenVinoOptimizationProgress>? progress,
        Guid operationId,
        OpenVinoOptimizationStage stage) =>
        progress?.Report(new OpenVinoOptimizationProgress(operationId, stage));

    private static OpenVinoOptimizationResult Failed(
        Guid operationId,
        OpenVinoSupportCode code,
        OptimizationSupportCode? executionSupportCode = null) => new(
            OpenVinoOptimizationStatus.Failed,
            operationId,
            Guid.Empty,
            code,
            null,
            null,
            null)
        {
            ExecutionSupportCode = executionSupportCode
        };

    private static OpenVinoOptimizationResult Replan(
        Guid operationId,
        OptimizationSupportCode supportCode) => new(
            OpenVinoOptimizationStatus.ReplanRequired,
            operationId,
            Guid.Empty,
            SupportCode: null,
            ActualWeightPrecision: null,
            ActualKvCachePrecision: null,
            ActualDevice: null)
        {
            ReplanSupportCode = supportCode
        };

    private static OptimizationSupportCode MapFailure(OpenVinoSupportCode? code) =>
        code switch
        {
            OpenVinoSupportCode.ConversionPreflightFailed =>
                OptimizationSupportCode.StagingUnavailable,
            OpenVinoSupportCode.ConversionPublishFailed =>
                OptimizationSupportCode.PublicationFailed,
            OpenVinoSupportCode.ConversionOutputInvalid or
            OpenVinoSupportCode.PackageInconsistentResource =>
                OptimizationSupportCode.ValidationFailed,
            OpenVinoSupportCode.RuntimeLoadFailed or
            OpenVinoSupportCode.RuntimeProtocolFailed =>
                OptimizationSupportCode.SmokeTestFailed,
            OpenVinoSupportCode.PackageChanged =>
                OptimizationSupportCode.SourceIdentityMismatch,
            OpenVinoSupportCode.OperationCancelled =>
                OptimizationSupportCode.CancelledByUser,
            OpenVinoSupportCode.ConversionFailed =>
                OptimizationSupportCode.ConversionFailed,
            _ => OptimizationSupportCode.UnexpectedFailure
        };

    private static bool HasSufficientSpace(
        string sourceDirectory,
        string destinationDirectory)
    {
        try
        {
            long sourceBytes = 0;
            foreach (string path in Directory.EnumerateFiles(
                         sourceDirectory, "*", SearchOption.AllDirectories))
            {
                sourceBytes = checked(sourceBytes + new FileInfo(path).Length);
            }
            long required = Math.Max(
                1024L * 1024 * 1024,
                checked(sourceBytes * 2 + 512L * 1024 * 1024));
            string? root = Path.GetPathRoot(Path.GetFullPath(destinationDirectory));
            return !string.IsNullOrWhiteSpace(root) &&
                new DriveInfo(root).AvailableFreeSpace >= required;
        }
        catch (Exception exception) when (exception is IOException or
                                          UnauthorizedAccessException or
                                          OverflowException or ArgumentException)
        {
            return false;
        }
    }
}
