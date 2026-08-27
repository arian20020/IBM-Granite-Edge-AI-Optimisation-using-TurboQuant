using GraniteEdgeAI.Features.Prompting;

namespace GraniteEdgeAI.Features.OpenVinoRoute.TurboQuant;

public sealed record TurboQuantFallbackTarget(
    PromptRouteCapability Capability,
    bool IsVerified,
    bool IsApplicable,
    Func<Action<PromptEvent>, CancellationToken, Task<PromptRouteSessionActivation>> ActivateAsync);

public sealed record TurboQuantFallbackOffer(
    Guid OfferId,
    string RouteId,
    string Label);

/// <summary>
/// Publishes inert recovery choices. A target is not invoked until the exact
/// offer is explicitly confirmed by the user.
/// </summary>
public sealed class TurboQuantFallbackService
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, TurboQuantFallbackTarget> targets = [];

    public TurboQuantFallbackService(
        TurboQuantFallbackTarget? officialOpenVino,
        TurboQuantFallbackTarget? applicableGguf)
    {
        List<TurboQuantFallbackOffer> offers = [];
        AddIfEligible(
            officialOpenVino,
            PromptRouteKind.OpenVino,
            OpenVinoRouteCapability.RouteId,
            "Use verified official OpenVINO",
            offers);
        AddIfEligible(
            applicableGguf,
            PromptRouteKind.Gguf,
            requiredRouteId: null,
            "Use applicable verified GGUF",
            offers);
        Offers = offers.AsReadOnly();
    }

    public IReadOnlyList<TurboQuantFallbackOffer> Offers { get; }

    public async Task<PromptRouteSessionActivation> ConfirmAsync(
        Guid offerId,
        bool userConfirmed,
        Action<PromptEvent> eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(offerId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(eventSink);
        if (!userConfirmed)
        {
            throw new InvalidOperationException(
                "A fallback route requires explicit user confirmation.");
        }

        TurboQuantFallbackTarget target;
        lock (sync)
        {
            if (!targets.Remove(offerId, out target!))
            {
                throw new InvalidOperationException(
                    "The fallback offer is unknown, stale, or already consumed.");
            }
        }

        PromptRouteSessionActivation activation = await target.ActivateAsync(
            eventSink,
            cancellationToken).ConfigureAwait(false);
        try
        {
            ValidateActivatedRoute(target.Capability, activation);
        }
        catch (Exception)
        {
            if (activation?.Session is not null)
            {
                try
                {
                    await activation.Session.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // The identity failure remains authoritative.
                }
            }
            throw;
        }
        return activation;
    }

    private void AddIfEligible(
        TurboQuantFallbackTarget? target,
        PromptRouteKind requiredKind,
        string? requiredRouteId,
        string label,
        List<TurboQuantFallbackOffer> offers)
    {
        if (target is null || !target.IsVerified || !target.IsApplicable)
        {
            return;
        }
        ArgumentNullException.ThrowIfNull(target.Capability);
        ArgumentNullException.ThrowIfNull(target.ActivateAsync);
        if (target.Capability.Kind != requiredKind ||
            (requiredRouteId is not null && !string.Equals(
                target.Capability.RouteId,
                requiredRouteId,
                StringComparison.Ordinal)) ||
            string.Equals(
                target.Capability.RouteId,
                TurboQuantRouteAdapter.RouteId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A fallback target has an invalid route identity.",
                nameof(target));
        }
        Guid offerId = Guid.NewGuid();
        targets.Add(offerId, target);
        offers.Add(new TurboQuantFallbackOffer(
            offerId,
            target.Capability.RouteId,
            label));
    }

    private static void ValidateActivatedRoute(
        PromptRouteCapability expected,
        PromptRouteSessionActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation);
        ArgumentNullException.ThrowIfNull(activation.Session);
        ArgumentNullException.ThrowIfNull(activation.Presentation);
        PromptRouteCapability actual = activation.Session.Capability;
        if (actual.Kind != expected.Kind ||
            !string.Equals(actual.RouteId, expected.RouteId, StringComparison.Ordinal) ||
            !string.Equals(actual.ConfigurationId, expected.ConfigurationId, StringComparison.Ordinal) ||
            ContainsTurboQuant(activation.Presentation.CapabilitySummary) ||
            ContainsTurboQuant(activation.Presentation.ExecutionEvidence) ||
            ContainsTurboQuant(activation.Presentation.BuildEvidence) ||
            ContainsTurboQuant(activation.Presentation.ReadyAnnouncement))
        {
            throw new InvalidOperationException(
                "The confirmed fallback did not report its real route identity.");
        }
    }

    private static bool ContainsTurboQuant(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Contains("TurboQuant", StringComparison.OrdinalIgnoreCase);
}
