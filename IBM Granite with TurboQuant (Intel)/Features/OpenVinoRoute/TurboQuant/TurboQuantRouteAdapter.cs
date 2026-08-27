using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.TurboQuant;

public sealed record TurboQuantRouteActivation : IPromptRouteActivation
{
    internal TurboQuantRouteActivation(
        IPromptRouteActivation innerActivation,
        string packageManifestSha256,
        string modelSha256,
        long modelLength)
    {
        InnerActivation = innerActivation ??
            throw new ArgumentNullException(nameof(innerActivation));
        PackageManifestSha256 = packageManifestSha256;
        ModelSha256 = modelSha256;
        ModelLength = modelLength;
    }

    public IPromptRouteActivation InnerActivation { get; }
    public string PackageManifestSha256 { get; }
    public string ModelSha256 { get; }
    public long ModelLength { get; }
    public PromptRouteKind Kind => PromptRouteKind.OpenVino;

    public static TurboQuantRouteActivation FromLease(OpenVinoRouteHandoffLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        string packageManifestSha256 = lease.RetainedPackageManifestDigest ??
            throw new InvalidOperationException(
                "The OpenVINO package lease is stale or consumed.");
        return new TurboQuantRouteActivation(
            lease,
            packageManifestSha256,
            lease.Handoff.ModelSha256,
            lease.Handoff.ModelLengthBytes);
    }
}

internal interface ITurboQuantSessionFactory
{
    Task<IPromptRouteSession> StartAsync(
        IPromptRouteActivation activation,
        Action<PromptEvent> eventSink,
        CancellationToken cancellationToken);
}

/// <summary>
/// Adapts the separately verified worker to the shared prompt lifecycle. An
/// instance can be shown only for an approved tuple and can activate only from
/// a complete campaign disposition.
/// </summary>
public sealed class TurboQuantRouteAdapter : IPromptRouteAdapter
{
    public const string RouteId = "openvino.turboquant";
    public const string ConfigurationId = "openvino.turboquant.cpu.tbq4";

    private readonly ITurboQuantSessionFactory sessionFactory;
    private readonly TurboQuantActivationState activationState;

    public TurboQuantRouteAdapter(
        OpenVinoRouteService routeService,
        TurboQuantActivationState activationState)
        : this(CreateFactory(routeService, activationState), activationState)
    {
    }

    internal TurboQuantRouteAdapter(
        ITurboQuantSessionFactory sessionFactory,
        TurboQuantActivationState activationState)
    {
        this.sessionFactory = sessionFactory ??
            throw new ArgumentNullException(nameof(sessionFactory));
        this.activationState = activationState ??
            throw new ArgumentNullException(nameof(activationState));
        if (!activationState.IsExperimentalVisible)
        {
            throw new ArgumentException(
                "An unavailable TurboQuant tuple cannot be exposed.",
                nameof(activationState));
        }
    }

    public PromptRouteCapability Capability { get; } = new(
        PromptRouteKind.OpenVino,
        RouteId,
        ConfigurationId,
        "OpenVINO TurboQuant",
        "CPU",
        "Experimental",
        OpenVinoRouteCapability.MaximumContextTokens,
        OpenVinoRouteCapability.DefaultRequestedNewTokens,
        OpenVinoRouteCapability.MaximumRequestedNewTokens);

    public IReadOnlyList<TurboQuantEvidenceRow> EvidenceRows =>
        activationState.EvidenceRows;

    public async Task<PromptRouteSessionActivation> ActivateAsync(
        IPromptRouteActivation activation,
        Action<PromptEvent> eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activation);
        ArgumentNullException.ThrowIfNull(eventSink);
        if (!activationState.CanActivate)
        {
            throw new InvalidOperationException(
                "TurboQuant activation remains unverified.");
        }
        if (activation is not TurboQuantRouteActivation selected ||
            selected.InnerActivation.Kind != PromptRouteKind.OpenVino ||
            !string.Equals(
                selected.PackageManifestSha256,
                activationState.ApprovedPackageManifestSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                selected.ModelSha256,
                activationState.ApprovedModelSha256,
                StringComparison.Ordinal) ||
            selected.ModelLength != activationState.ApprovedModelLength)
        {
            throw new ArgumentException(
                "The activation does not match the approved TurboQuant model.",
                nameof(activation));
        }

        IPromptRouteSession inner = await sessionFactory.StartAsync(
            selected.InnerActivation,
            eventSink,
            cancellationToken).ConfigureAwait(false);
        TurboQuantPromptSession session = new(inner, Capability);
        return new PromptRouteSessionActivation(
            session,
            new PromptRoutePresentation(
                "OpenVINO TurboQuant · CPU · Experimental",
                "Requested CPU · Running CPU · Requested/actual TBQ4/TBQ4 · Campaign activation verified",
                "Verified exact TurboQuant build and campaign evidence",
                "Experimental OpenVINO TurboQuant CPU session ready."));
    }

    private static OpenVinoTurboQuantSessionFactory CreateFactory(
        OpenVinoRouteService routeService,
        TurboQuantActivationState activationState)
    {
        ArgumentNullException.ThrowIfNull(routeService);
        ArgumentNullException.ThrowIfNull(activationState);
        if (!Equals(
                routeService.ExpectedBuildEvidence,
                activationState.ApprovedBuildEvidence) ||
            routeService.ExpectedBuildEvidence?.TurboQuantBuild is null)
        {
            throw new ArgumentException(
                "The route service is not bound to the approved TurboQuant worker.",
                nameof(routeService));
        }
        return new OpenVinoTurboQuantSessionFactory(routeService);
    }

    private sealed class OpenVinoTurboQuantSessionFactory(
        OpenVinoRouteService routeService) : ITurboQuantSessionFactory
    {
        private readonly OpenVinoRouteService routeService = routeService ??
            throw new ArgumentNullException(nameof(routeService));

        public async Task<IPromptRouteSession> StartAsync(
            IPromptRouteActivation activation,
            Action<PromptEvent> eventSink,
            CancellationToken cancellationToken)
        {
            if (activation is not OpenVinoRouteHandoffLease lease)
            {
                throw new ArgumentException(
                    "TurboQuant requires an OpenVINO package lease.",
                    nameof(activation));
            }
            return await routeService.StartSessionAsync(
                lease,
                eventSink,
                OpenVinoRuntimeOptions.Tbq4,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class TurboQuantPromptSession(
        IPromptRouteSession inner,
        PromptRouteCapability capability) : IPromptRouteSession
    {
        private readonly IPromptRouteSession inner = inner ??
            throw new ArgumentNullException(nameof(inner));

        public PromptRouteCapability Capability { get; } = capability;

        public Task<PromptTurnResult> GenerateAsync(
            string prompt,
            int requestedNewTokens,
            CancellationToken cancellationToken) =>
            inner.GenerateAsync(prompt, requestedNewTokens, cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) =>
            inner.StopAsync(cancellationToken);

        public Task StopActiveTurnAsync(
            Guid workerConfirmedTurnId,
            CancellationToken cancellationToken) =>
            inner.StopActiveTurnAsync(workerConfirmedTurnId, cancellationToken);

        public Task CancelAsync(CancellationToken cancellationToken) =>
            inner.CancelAsync(cancellationToken);

        public Task CancelActiveTurnAsync(
            Guid workerConfirmedTurnId,
            CancellationToken cancellationToken) =>
            inner.CancelActiveTurnAsync(workerConfirmedTurnId, cancellationToken);

        public Task CloseAsync(CancellationToken cancellationToken) =>
            inner.CloseAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
