using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using SharedProjection = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionProjectionV2;
using SharedOutcome = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionOutcomeV2;

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
                HandoffLease: null,
                OpenVinoPromptAdapter.MapFailure(failure.SupportCode),
                Configuration: null);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
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

        OpenVinoRouteSession session = await StartSessionAsync(
            lease,
            eventSink,
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
        projection.ModelInspectionResult.Outcome == MapOutcome(handoff.Outcome) &&
        projection.ModelInspectionHandoff.Outcome == MapOutcome(handoff.Outcome) &&
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

    private static SharedOutcome MapOutcome(ModelInspectionOutcome outcome) =>
        outcome switch
        {
            ModelInspectionOutcome.Ready => SharedOutcome.Ready,
            ModelInspectionOutcome.ReadyWithWarnings =>
                SharedOutcome.ReadyWithWarnings,
            _ => throw new InvalidOperationException(
                "The OpenVINO handoff outcome is unsupported.")
        };
}
