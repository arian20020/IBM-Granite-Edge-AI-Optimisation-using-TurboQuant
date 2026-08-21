using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

public sealed record OpenVinoRouteInspectionResult(
    OpenVinoRouteInspectionOutcome Outcome,
    ModelInspectionHandoffV2? Handoff,
    OpenVinoPromptFailure? Failure,
    OpenVinoConfigurationCandidate? Configuration);

/// <summary>
/// Owns OpenVINO directory inspection and the local path registry. Raw paths
/// never enter its presentation-facing results.
/// </summary>
public sealed class OpenVinoRouteService
{
    private readonly OpenVinoStaticPackageInspector staticInspector;
    private readonly OpenVinoInspectionHandoffFactory handoffFactory;
    private readonly IOpenVinoWorkerClient workerClient;
    private readonly IOpenVinoPromptChannelFactory channelFactory;
    private readonly ConcurrentDictionary<Guid, RegisteredPackage> packages =
        new();

    public OpenVinoRouteService(IOpenVinoWorkerClient workerClient)
        : this(
            new OpenVinoStaticPackageInspector(),
            new OpenVinoInspectionHandoffFactory(),
            workerClient,
            new OpenVinoWorkerPromptChannelFactory(workerClient))
    {
    }

    internal OpenVinoRouteService(
        OpenVinoStaticPackageInspector staticInspector,
        OpenVinoInspectionHandoffFactory handoffFactory,
        IOpenVinoWorkerClient workerClient,
        IOpenVinoPromptChannelFactory channelFactory)
    {
        this.staticInspector = staticInspector ??
            throw new ArgumentNullException(nameof(staticInspector));
        this.handoffFactory = handoffFactory ??
            throw new ArgumentNullException(nameof(handoffFactory));
        this.workerClient = workerClient ??
            throw new ArgumentNullException(nameof(workerClient));
        this.channelFactory = channelFactory ??
            throw new ArgumentNullException(nameof(channelFactory));
    }

    public async Task<OpenVinoRouteInspectionResult> InspectAsync(
        string packageDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        OpenVinoRouteStateMachine stateMachine = new();
        Guid operationId = stateMachine.Snapshot.Identity.OperationId;
        if (!stateMachine.TryBeginInspection(operationId))
        {
            throw new InvalidOperationException("The inspection could not start.");
        }

        OpenVinoStaticPackageInspectionResult staticResult =
            staticInspector.Inspect(packageDirectory);
        if (staticResult.Status !=
                OpenVinoStaticInspectionStatus.NativeValidationRequired ||
            staticResult.Evidence is null)
        {
            OpenVinoSupportCode code = staticResult.SupportCode ??
                OpenVinoSupportCode.PackageInconsistentResource;
            OpenVinoRouteInspectionOutcome outcome = InspectionOutcome(code);
            stateMachine.TryCompleteInspection(operationId, outcome);
            return new OpenVinoRouteInspectionResult(
                outcome,
                Handoff: null,
                OpenVinoPromptAdapter.MapFailure(code),
                Configuration: null);
        }

        OpenVinoStaticPackageEvidence evidence = staticResult.Evidence;
        Guid inspectionRunId = Guid.NewGuid();
        IOpenVinoEvent terminal;
        try
        {
            terminal = await workerClient.InspectAsync(
                new StartInspectionCommand(
                    inspectionRunId,
                    packageDirectory,
                    evidence.PackageManifestDigest,
                    evidence.ModelSha256,
                    evidence.ModelLengthBytes),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OpenVinoWorkerClientException failure)
        {
            OpenVinoRouteInspectionOutcome outcome =
                InspectionOutcome(failure.SupportCode);
            stateMachine.TryCompleteInspection(operationId, outcome);
            return new OpenVinoRouteInspectionResult(
                outcome,
                Handoff: null,
                OpenVinoPromptAdapter.MapFailure(failure.SupportCode),
                Configuration: null);
        }

        if (terminal is InspectionFailedEvent failed)
        {
            OpenVinoRouteInspectionOutcome outcome =
                InspectionOutcome(failed.SupportCode);
            stateMachine.TryCompleteInspection(operationId, outcome);
            return new OpenVinoRouteInspectionResult(
                outcome,
                Handoff: null,
                OpenVinoPromptAdapter.MapFailure(failed.SupportCode),
                Configuration: null);
        }

        if (terminal is not InspectionCompletedEvent completed)
        {
            OpenVinoSupportCode code = OpenVinoSupportCode.RuntimeProtocolFailed;
            stateMachine.TryCompleteInspection(
                operationId,
                OpenVinoRouteInspectionOutcome.Invalid);
            return new OpenVinoRouteInspectionResult(
                OpenVinoRouteInspectionOutcome.Invalid,
                Handoff: null,
                OpenVinoPromptAdapter.MapFailure(code),
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
        if (!stateMachine.TryCompleteInspection(operationId, readyOutcome))
        {
            throw new InvalidOperationException(
                "The inspection result became stale before publication.");
        }

        RegisteredPackage package = new(
            stateMachine,
            new OpenVinoSessionDescriptor(
                inspectionRunId,
                packageDirectory,
                evidence.PackageManifestDigest,
                evidence.ModelSha256,
                evidence.ModelLengthBytes));
        if (!packages.TryAdd(handoff.ModelInspectionHandoffId, package))
        {
            throw new InvalidOperationException(
                "The model handoff identity is already registered.");
        }

        return new OpenVinoRouteInspectionResult(
            readyOutcome,
            handoff,
            Failure: null,
            OpenVinoRouteCapability.Candidates[0]);
    }

    public async Task<OpenVinoRouteSession> StartSessionAsync(
        ModelInspectionHandoffV2 handoff,
        Action<OpenVinoPromptEvent> eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        handoff.Validate();
        ArgumentNullException.ThrowIfNull(eventSink);
        if (!packages.TryRemove(
                handoff.ModelInspectionHandoffId,
                out RegisteredPackage? package) ||
            package.Descriptor.InspectionRunId != handoff.ModelInspectionRunId ||
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
            cancellationToken).ConfigureAwait(false);
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
        _ => OpenVinoRouteInspectionOutcome.Invalid
    };

    private sealed record RegisteredPackage(
        OpenVinoRouteStateMachine StateMachine,
        OpenVinoSessionDescriptor Descriptor);
}
