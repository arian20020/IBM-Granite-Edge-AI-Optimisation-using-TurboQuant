using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using ModelInspectionProgress = GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionProgress;
using ModelInspectionStage = GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionStage;
using ModelInspectionStageStatus = GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionStageStatus;
using SharedProjection = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionProjectionV2;
using ModelInspectionHandoffV2 = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionHandoffV2;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

public sealed record OpenVinoRouteInspectionResult(
    OpenVinoRouteInspectionOutcome Outcome,
    OpenVinoRouteHandoffLease? HandoffLease,
    PromptFailure? Failure,
    OpenVinoConfigurationCandidate? Configuration,
    OpenVinoConversionOffer? ConversionOffer = null)
{
    public ModelInspectionHandoffV2? Handoff => HandoffLease?.Handoff;
}

public sealed class OpenVinoConversionOffer : IDisposable
{
    private SourceModelInspectionResult? source;
    private string? sourceDirectory;

    internal OpenVinoConversionOffer(
        SourceModelInspectionResult source,
        string sourceDirectory)
    {
        this.source = source;
        this.sourceDirectory = Path.GetFullPath(sourceDirectory);
        Evidence = source.Evidence ?? throw new ArgumentException(
            "A conversion offer requires accepted source evidence.", nameof(source));
    }

    public SourceModelEvidence Evidence { get; }

    internal (SourceModelInspectionResult Source, string SourceDirectory) Consume()
    {
        SourceModelInspectionResult? retained = Interlocked.Exchange(ref source, null);
        string? path = Interlocked.Exchange(ref sourceDirectory, null);
        if (retained is null || path is null)
        {
            throw new InvalidOperationException("The conversion offer is stale or consumed.");
        }
        return (retained, path);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref source, null)?.Dispose();
        Interlocked.Exchange(ref sourceDirectory, null);
    }
}

internal sealed record OpenVinoRouteLeasePayload(
    Guid ServiceIdentity,
    OpenVinoRouteStateMachine StateMachine,
    OpenVinoSessionDescriptor Descriptor);

/// <summary>
/// Owns the revocable path-bearing state behind one public path-free handoff.
/// </summary>
public sealed class OpenVinoRouteHandoffLease : IDisposable, IPromptRouteActivation
{
    private OpenVinoRouteLeasePayload? payload;

    internal OpenVinoRouteHandoffLease(
        ModelInspectionHandoffV2 handoff,
        SharedProjection projection,
        OpenVinoRouteLeasePayload payload)
    {
        Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
        Projection = projection ?? throw new ArgumentNullException(nameof(projection));
        this.payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    public ModelInspectionHandoffV2 Handoff { get; }

    internal SharedProjection Projection { get; }

    public PromptRouteKind Kind => PromptRouteKind.OpenVino;

    internal bool HasPathBearingDescriptor =>
        Volatile.Read(ref payload) is not null;

    internal string? RetainedPackageManifestDigest =>
        Volatile.Read(ref payload)?.Descriptor.PackageManifestDigest;

    internal OpenVinoRouteLeasePayload Consume(Guid serviceIdentity)
    {
        OpenVinoRouteLeasePayload? current = Volatile.Read(ref payload);
        if (current is null || current.ServiceIdentity != serviceIdentity ||
            Interlocked.CompareExchange(ref payload, null, current) != current)
        {
            throw new InvalidOperationException(
                "The model inspection handoff lease is stale, consumed, or invalid.");
        }

        return current;
    }

    public void Dispose() => Interlocked.Exchange(ref payload, null);
}

/// <summary>
/// Owns OpenVINO directory inspection and the local path registry. Raw paths
/// never enter its presentation-facing results.
/// </summary>
public sealed class OpenVinoRouteService : IPromptRouteAdapter
{
    private readonly OpenVinoStaticPackageInspector staticInspector;
    private readonly SourceModelInspector sourceInspector;
    private readonly OpenVinoInspectionHandoffFactory handoffFactory;
    private readonly IOpenVinoWorkerClient workerClient;
    private readonly IOpenVinoPromptChannelFactory channelFactory;
    private readonly OpenVinoBuildEvidence? expectedBuildEvidence;
    private readonly Guid serviceIdentity = Guid.NewGuid();

    public OpenVinoRouteService(IOpenVinoWorkerClient workerClient)
        : this(
            new OpenVinoStaticPackageInspector(),
            new OpenVinoInspectionHandoffFactory(),
            workerClient,
            new OpenVinoWorkerPromptChannelFactory(workerClient),
            expectedBuildEvidence: null)
    {
    }

    internal OpenVinoRouteService(
        IOpenVinoWorkerClient workerClient,
        OpenVinoBuildEvidence expectedBuildEvidence)
        : this(
            new OpenVinoStaticPackageInspector(),
            new OpenVinoInspectionHandoffFactory(),
            workerClient,
            new OpenVinoWorkerPromptChannelFactory(workerClient),
            expectedBuildEvidence)
    {
    }

    internal OpenVinoRouteService(
        OpenVinoStaticPackageInspector staticInspector,
        OpenVinoInspectionHandoffFactory handoffFactory,
        IOpenVinoWorkerClient workerClient,
        IOpenVinoPromptChannelFactory channelFactory)
        : this(
            staticInspector,
            handoffFactory,
            workerClient,
            channelFactory,
            expectedBuildEvidence: null)
    {
    }

    private OpenVinoRouteService(
        OpenVinoStaticPackageInspector staticInspector,
        OpenVinoInspectionHandoffFactory handoffFactory,
        IOpenVinoWorkerClient workerClient,
        IOpenVinoPromptChannelFactory channelFactory,
        OpenVinoBuildEvidence? expectedBuildEvidence)
    {
        this.staticInspector = staticInspector ??
            throw new ArgumentNullException(nameof(staticInspector));
        sourceInspector = new SourceModelInspector();
        this.handoffFactory = handoffFactory ??
            throw new ArgumentNullException(nameof(handoffFactory));
        this.workerClient = workerClient ??
            throw new ArgumentNullException(nameof(workerClient));
        this.channelFactory = channelFactory ??
            throw new ArgumentNullException(nameof(channelFactory));
        expectedBuildEvidence?.Validate();
        this.expectedBuildEvidence = expectedBuildEvidence;
    }

    internal OpenVinoBuildEvidence? ExpectedBuildEvidence =>
        expectedBuildEvidence;

    public PromptRouteCapability Capability =>
        OpenVinoRouteCapability.PromptCapability;

    public async Task<OpenVinoRouteInspectionResult> InspectAsync(
        string packageDirectory,
        CancellationToken cancellationToken) =>
        await InspectAsync(
            packageDirectory,
            progress: null,
            cancellationToken).ConfigureAwait(false);

    internal async Task<OpenVinoRouteInspectionResult> InspectAsync(
        string packageDirectory,
        IProgress<ModelInspectionProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        ModelInspectionStage currentStage = ModelInspectionStage.CheckModelPackage;
        void Report(
            ModelInspectionStage stage,
            ModelInspectionStageStatus status,
            int completed,
            double? fraction,
            string message)
        {
            currentStage = stage;
            progress?.Report(new ModelInspectionProgress(
                stage,
                status,
                completed,
                totalStageCount: 5,
                stageFraction: null,
                message));
        }

        Report(
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completed: 0,
            fraction: 0d,
            GetPackageVerificationMessage(0d));
        OpenVinoRouteStateMachine stateMachine = new();
        Guid operationId = stateMachine.Snapshot.Identity.OperationId;
        if (!stateMachine.TryBeginInspection(operationId))
        {
            throw new InvalidOperationException("The inspection could not start.");
        }

        OpenVinoStaticPackageInspectionResult staticResult;
        try
        {
            staticResult = staticInspector.Inspect(
                packageDirectory,
                cancellationToken,
                new InlineProgress<double>(fraction => Report(
                    ModelInspectionStage.CheckModelPackage,
                    ModelInspectionStageStatus.Active,
                    completed: 0,
                    fraction,
                    GetPackageVerificationMessage(fraction))));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            Report(
                ModelInspectionStage.CheckModelPackage,
                ModelInspectionStageStatus.Cancelled,
                completed: 0,
                fraction: null,
                "Model inspection was cancelled.");
            OpenVinoSupportCode code = OpenVinoSupportCode.OperationCancelled;
            OpenVinoRouteInspectionOutcome outcome = InspectionOutcome(code);
            stateMachine.TryCompleteInspection(operationId, outcome);
            return new OpenVinoRouteInspectionResult(
                outcome,
                HandoffLease: null,
                OpenVinoPromptAdapter.MapFailure(code),
                Configuration: null);
        }
        if (staticResult.Status !=
                OpenVinoStaticInspectionStatus.NativeValidationRequired ||
            staticResult.Evidence is null)
        {
            SourceModelInspectionResult sourceResult =
                sourceInspector.Inspect(packageDirectory);
            if (sourceResult.Status == SourceModelInspectionStatus.ConversionRequired &&
                sourceResult.Evidence is not null)
            {
                stateMachine.TryCompleteInspection(
                    operationId,
                    OpenVinoRouteInspectionOutcome.ConversionRequired);
                return new OpenVinoRouteInspectionResult(
                    OpenVinoRouteInspectionOutcome.ConversionRequired,
                    HandoffLease: null,
                    Failure: null,
                    Configuration: null,
                    new OpenVinoConversionOffer(sourceResult, packageDirectory));
            }
            sourceResult.Dispose();
            Report(
                currentStage,
                ModelInspectionStageStatus.Failed,
                completed: (int)currentStage - 1,
                fraction: null,
                "The OpenVINO package could not be validated.");
            OpenVinoSupportCode code = staticResult.SupportCode ??
                OpenVinoSupportCode.PackageInconsistentResource;
            OpenVinoRouteInspectionOutcome outcome = InspectionOutcome(code);
            stateMachine.TryCompleteInspection(operationId, outcome);
            return new OpenVinoRouteInspectionResult(
                outcome,
                HandoffLease: null,
                OpenVinoPromptAdapter.MapFailure(code),
                Configuration: null);
        }

        Report(
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Completed,
            completed: 1,
            fraction: 1d,
            "The OpenVINO package is complete and unchanged.");
        Report(
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completed: 1,
            fraction: null,
            "Reading the model configuration.");
        OpenVinoStaticPackageEvidence evidence = staticResult.Evidence;
        Guid inspectionRunId = Guid.NewGuid();
        IOpenVinoEvent terminal;
        try
        {
            IProgress<InspectionProgressEvent> nativeProgress =
                new InlineProgress<InspectionProgressEvent>(update =>
                {
                    switch (update.Stage)
                    {
                        case OpenVinoInspectionStage.ManifestVerified:
                            Report(
                                ModelInspectionStage.ReadModelConfiguration,
                                ModelInspectionStageStatus.Completed,
                                completed: 2,
                                fraction: 1d,
                                "The model configuration is valid.");
                            Report(
                                ModelInspectionStage.ValidateTokenizerAndChatSetup,
                                ModelInspectionStageStatus.Active,
                                completed: 2,
                                fraction: 0d,
                                "Validating the tokenizer and chat setup.");
                            break;
                        case OpenVinoInspectionStage.MainModelParsed:
                            Report(
                                ModelInspectionStage.ValidateTokenizerAndChatSetup,
                                ModelInspectionStageStatus.Active,
                                completed: 2,
                                fraction: 0.5d,
                                "The model graph is readable; checking tokenizer resources.");
                            break;
                        case OpenVinoInspectionStage.TokenizerParsed:
                            Report(
                                ModelInspectionStage.ValidateTokenizerAndChatSetup,
                                ModelInspectionStageStatus.Completed,
                                completed: 3,
                                fraction: 1d,
                                "The tokenizer and chat setup are valid.");
                            Report(
                                ModelInspectionStage.ValidateModelStructure,
                                ModelInspectionStageStatus.Active,
                                completed: 3,
                                fraction: null,
                                "Validating the model structure.");
                            break;
                        case OpenVinoInspectionStage.DetokenizerParsed:
                            Report(
                                ModelInspectionStage.ValidateModelStructure,
                                ModelInspectionStageStatus.Completed,
                                completed: 4,
                                fraction: 1d,
                                "The model structure is valid.");
                            Report(
                                ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
                                ModelInspectionStageStatus.Active,
                                completed: 4,
                                fraction: null,
                                "Confirming core OpenVINO runtime compatibility.");
                            break;
                    }
                });
            terminal = await workerClient.InspectAsync(
                new StartInspectionCommand(
                    inspectionRunId,
                    packageDirectory,
                    evidence.PackageManifestDigest,
                    evidence.ModelSha256,
                    evidence.ModelLengthBytes),
                nativeProgress,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OpenVinoWorkerClientException failure)
        {
            Report(
                currentStage,
                ModelInspectionStageStatus.Failed,
                completed: (int)currentStage - 1,
                fraction: null,
                "The local OpenVINO runtime inspection could not continue.");
            OpenVinoRouteInspectionOutcome outcome =
                InspectionOutcome(failure.SupportCode);
            stateMachine.TryCompleteInspection(operationId, outcome);
            return new OpenVinoRouteInspectionResult(
                outcome,
                HandoffLease: null,
                OpenVinoPromptAdapter.MapFailure(failure.SupportCode),
                Configuration: null);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            Report(
                currentStage,
                ModelInspectionStageStatus.Cancelled,
                completed: (int)currentStage - 1,
                fraction: null,
                "Model inspection was cancelled.");
            OpenVinoSupportCode code = OpenVinoSupportCode.OperationCancelled;
            OpenVinoRouteInspectionOutcome outcome = InspectionOutcome(code);
            stateMachine.TryCompleteInspection(operationId, outcome);
            return new OpenVinoRouteInspectionResult(
                outcome,
                HandoffLease: null,
                OpenVinoPromptAdapter.MapFailure(code),
                Configuration: null);
        }

        if (terminal is InspectionFailedEvent failed)
        {
            Report(
                currentStage,
                ModelInspectionStageStatus.Failed,
                completed: (int)currentStage - 1,
                fraction: null,
                "The local OpenVINO runtime rejected the package.");
            OpenVinoRouteInspectionOutcome outcome =
                InspectionOutcome(failed.SupportCode);
            stateMachine.TryCompleteInspection(operationId, outcome);
            return new OpenVinoRouteInspectionResult(
                outcome,
                HandoffLease: null,
                OpenVinoPromptAdapter.MapFailure(failed.SupportCode),
                Configuration: null);
        }

        if (terminal is not InspectionCompletedEvent completed)
        {
            OpenVinoSupportCode code = OpenVinoSupportCode.RuntimeProtocolFailed;
            OpenVinoRouteInspectionOutcome outcome = InspectionOutcome(code);
            stateMachine.TryCompleteInspection(
                operationId,
                outcome);
            return new OpenVinoRouteInspectionResult(
                outcome,
                HandoffLease: null,
                OpenVinoPromptAdapter.MapFailure(code),
                Configuration: null);
        }

        OpenVinoSupportCode? completedEvidenceFailure =
            ValidateCompletedEvidence(completed, inspectionRunId, evidence);
        if (completedEvidenceFailure is OpenVinoSupportCode failureCode)
        {
            Report(
                currentStage,
                ModelInspectionStageStatus.Failed,
                completed: (int)currentStage - 1,
                fraction: null,
                "The OpenVINO inspection evidence did not match the selected package.");
            OpenVinoRouteInspectionOutcome outcome = InspectionOutcome(failureCode);
            stateMachine.TryCompleteInspection(operationId, outcome);
            return new OpenVinoRouteInspectionResult(
                outcome,
                HandoffLease: null,
                OpenVinoPromptAdapter.MapFailure(failureCode),
                Configuration: null);
        }

        OpenVinoRouteInspectionOutcome readyOutcome = evidence.HasChatTemplate
            ? OpenVinoRouteInspectionOutcome.Ready
            : OpenVinoRouteInspectionOutcome.ReadyWithWarnings;
        ModelInspectionOutcome contractOutcome = evidence.HasChatTemplate
            ? ModelInspectionOutcome.Ready
            : ModelInspectionOutcome.ReadyWithWarnings;
        OpenVinoNativeValidationEvidence nativeEvidence = new(
            contractOutcome,
            completed.PackageManifestDigest,
            completed.ModelSha256,
            completed.ModelLengthBytes,
            completed.MainModelParsed,
            completed.TokenizerParsed,
            completed.DetokenizerParsed);
        ModelInspectionHandoffV2 handoff = handoffFactory.Create(
            staticResult,
            nativeEvidence,
            inspectionRunId);
        SharedProjection projection =
            ModelInspectionProjectionFactory.CreateOpenVino(handoff);
        if (!stateMachine.TryCompleteInspection(operationId, readyOutcome))
        {
            throw new InvalidOperationException(
                "The inspection result became stale before publication.");
        }

        Report(
            ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
            ModelInspectionStageStatus.Completed,
            completed: 5,
            fraction: 1d,
            "The OpenVINO package is ready for hardware inspection.");

        OpenVinoRouteHandoffLease lease = new(
            handoff,
            projection,
            new OpenVinoRouteLeasePayload(
                serviceIdentity,
                stateMachine,
                new OpenVinoSessionDescriptor(
                inspectionRunId,
                packageDirectory,
                evidence.PackageManifestDigest,
                evidence.ModelSha256,
                evidence.ModelLengthBytes)));

        return new OpenVinoRouteInspectionResult(
            readyOutcome,
            lease,
            Failure: null,
            OpenVinoRouteCapability.Candidates[0]);
    }

    internal static string GetPackageVerificationMessage(double fraction)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fraction, 0d);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fraction, 1d);

        return fraction switch
        {
            < 0.25d =>
                "Reading and verifying the large model file securely. " +
                "This can take up to a minute.",
            < 0.5d =>
                "Continuing the secure read of the large model file.",
            < 0.75d =>
                "Confirming the large model file did not change during inspection.",
            < 0.95d =>
                "Continuing the model-file consistency check.",
            _ => "Finishing secure model-file verification."
        };
    }

    public async Task<OpenVinoRouteSession> StartSessionAsync(
        OpenVinoRouteHandoffLease handoffLease,
        Action<PromptEvent> eventSink,
        CancellationToken cancellationToken) =>
        await StartSessionAsync(
            handoffLease,
            eventSink,
            OpenVinoRuntimeOptions.ReleasedDefault,
            cancellationToken).ConfigureAwait(false);

    internal async Task<OpenVinoRouteSession> StartSessionAsync(
        OpenVinoRouteHandoffLease handoffLease,
        Action<PromptEvent> eventSink,
        OpenVinoRuntimeOptions runtimeOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handoffLease);
        ModelInspectionHandoffV2 handoff = handoffLease.Handoff;
        ArgumentNullException.ThrowIfNull(handoff);
        handoff.Validate();
        SharedProjection projection = handoffLease.Projection;
        projection.Validate();
        if (!ProjectionMatchesHandoff(projection, handoff))
        {
            throw new InvalidOperationException(
                "The schema-v2 projection does not match the issued handoff.");
        }
        ArgumentNullException.ThrowIfNull(eventSink);
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        runtimeOptions.Validate();
        OpenVinoRouteLeasePayload package =
            handoffLease.Consume(serviceIdentity);
        if (package.Descriptor.InspectionRunId != handoff.ModelInspectionRunId ||
            package.Descriptor.ModelSha256 != handoff.ModelSha256 ||
            package.Descriptor.ModelLengthBytes != handoff.ModelLengthBytes ||
            !package.StateMachine.TryAwaitConfiguration(
                package.StateMachine.Snapshot.Identity.OperationId))
        {
            throw new InvalidOperationException(
                "The model inspection handoff is stale, consumed, or invalid.");
        }

        return await OpenVinoRouteSession.StartAsync(
            channelFactory,
            package.StateMachine,
            package.Descriptor,
            eventSink,
            cancellationToken,
            runtimeOptions: runtimeOptions).ConfigureAwait(false);
    }

    public async Task<PromptRouteSessionActivation> ActivateAsync(
        IPromptRouteActivation activation,
        Action<PromptEvent> eventSink,
        CancellationToken cancellationToken)
    {
        if (activation is not OpenVinoRouteHandoffLease lease)
        {
            throw new ArgumentException(
                "The activation does not belong to the official OpenVINO route.",
                nameof(activation));
        }

        return await ActivateAsync(
            lease,
            OpenVinoRuntimeOptions.ReleasedDefault,
            eventSink,
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<PromptRouteSessionActivation> ActivateAsync(
        OpenVinoRouteHandoffLease lease,
        OpenVinoRuntimeOptions runtimeOptions,
        Action<PromptEvent> eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        ArgumentNullException.ThrowIfNull(eventSink);
        runtimeOptions.Validate();
        OpenVinoRouteSession session = await StartSessionAsync(
            lease,
            eventSink,
            runtimeOptions,
            cancellationToken).ConfigureAwait(false);
        string buildEvidence = expectedBuildEvidence is null
            ? "Verified official worker build"
            : $"Runtime {expectedBuildEvidence.RuntimeBuild} · " +
              $"GenAI {expectedBuildEvidence.GenAiBuild} · " +
              $"Tokenizers {expectedBuildEvidence.TokenizersBuild} · " +
              $"Worker manifest {expectedBuildEvidence.WorkerManifestDigest}";
        return new PromptRouteSessionActivation(
            session,
            new PromptRoutePresentation(
                $"{Capability.BackendLabel} · {Capability.Device} · {Capability.Maturity}",
                $"Requested {Capability.Device} · Running {Capability.Device}",
                buildEvidence,
                $"{Capability.BackendLabel} {Capability.Device} session ready."));
    }

    private static OpenVinoRouteInspectionOutcome InspectionOutcome(
        OpenVinoSupportCode supportCode) => supportCode switch
    {
        OpenVinoSupportCode.PackageMissingResource or
        OpenVinoSupportCode.PackageInconsistentResource =>
            OpenVinoRouteInspectionOutcome.IncompletePackage,
        OpenVinoSupportCode.ModelArchitectureUnsupported or
        OpenVinoSupportCode.ModelTaskUnsupported or
        OpenVinoSupportCode.TokenizerUnsupported =>
            OpenVinoRouteInspectionOutcome.Unsupported,
        OpenVinoSupportCode.RuntimeDependencyMissing or
        OpenVinoSupportCode.RuntimeDeviceUnavailable =>
            OpenVinoRouteInspectionOutcome.DependencyUnavailable,
        OpenVinoSupportCode.OperationCancelled =>
            OpenVinoRouteInspectionOutcome.Cancelled,
        OpenVinoSupportCode.RuntimeTimedOut =>
            OpenVinoRouteInspectionOutcome.TimedOut,
        OpenVinoSupportCode.PackageChanged =>
            OpenVinoRouteInspectionOutcome.StaleEvidence,
        OpenVinoSupportCode.PackageUnsafePath or
        OpenVinoSupportCode.PackageUnreadable or
        OpenVinoSupportCode.RuntimeIntegrityFailed or
        OpenVinoSupportCode.RuntimeLoadFailed or
        OpenVinoSupportCode.RuntimeDeviceMismatch or
        OpenVinoSupportCode.RuntimeContextExceeded or
        OpenVinoSupportCode.RuntimeProtocolFailed or
        OpenVinoSupportCode.ConversionPreflightFailed or
        OpenVinoSupportCode.ConversionFailed or
        OpenVinoSupportCode.ConversionOutputInvalid or
        OpenVinoSupportCode.ConversionPublishFailed or
        OpenVinoSupportCode.OptimizationUnsupported or
        OpenVinoSupportCode.TurboQuantUnavailable or
        OpenVinoSupportCode.TurboQuantActivationUnverified =>
            OpenVinoRouteInspectionOutcome.InvalidEvidence,
        _ => throw new ArgumentOutOfRangeException(
            nameof(supportCode),
            supportCode,
            "Unknown OpenVINO support code.")
    };

    private static OpenVinoSupportCode? ValidateCompletedEvidence(
        InspectionCompletedEvent completed,
        Guid inspectionRunId,
        OpenVinoStaticPackageEvidence expected)
    {
        try
        {
            completed.Validate();
        }
        catch (OpenVinoProtocolException)
        {
            return OpenVinoSupportCode.RuntimeProtocolFailed;
        }

        return completed.InspectionRunId != inspectionRunId ||
            !string.Equals(
                completed.PackageManifestDigest,
                expected.PackageManifestDigest,
                StringComparison.Ordinal) ||
            !string.Equals(
                completed.ModelSha256,
                expected.ModelSha256,
                StringComparison.Ordinal) ||
            completed.ModelLengthBytes != expected.ModelLengthBytes
                ? OpenVinoSupportCode.PackageChanged
                : null;
    }

    private static bool ProjectionMatchesHandoff(
        SharedProjection projection,
        ModelInspectionHandoffV2 handoff) =>
        projection.SchemaVersion == handoff.SchemaVersion &&
        projection.ModelSource.Route ==
            GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionRoute.OpenVino &&
        projection.ModelInspectionResult.Route ==
            GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionRoute.OpenVino &&
        projection.ModelInspectionHandoff.SchemaVersion == handoff.SchemaVersion &&
        projection.ModelInspectionHandoff.ModelInspectionHandoffId ==
            handoff.ModelInspectionHandoffId &&
        projection.ModelInspectionResult.ModelInspectionRunId ==
            handoff.ModelInspectionRunId &&
        projection.ModelInspectionHandoff.ModelInspectionRunId ==
            handoff.ModelInspectionRunId &&
        projection.ModelInspectionResult.Outcome == handoff.Outcome &&
        projection.ModelInspectionHandoff.Outcome == handoff.Outcome &&
        string.Equals(
            projection.ModelSource.ModelSha256,
            handoff.ModelSha256,
            StringComparison.Ordinal) &&
        string.Equals(
            projection.ModelInspectionResult.ModelSha256,
            handoff.ModelSha256,
            StringComparison.Ordinal) &&
        string.Equals(
            projection.ModelInspectionHandoff.ModelSha256,
            handoff.ModelSha256,
            StringComparison.Ordinal) &&
        projection.ModelSource.ModelLengthBytes == handoff.ModelLengthBytes &&
        projection.ModelInspectionResult.ModelLengthBytes == handoff.ModelLengthBytes &&
        projection.ModelInspectionHandoff.ModelLengthBytes == handoff.ModelLengthBytes;

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

}
