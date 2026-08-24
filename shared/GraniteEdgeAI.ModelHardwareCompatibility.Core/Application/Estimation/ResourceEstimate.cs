namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// The outcome of estimating one candidate. Either a complete component set with
/// its recorded caveats, or a refusal naming what was missing. There is no third
/// state, and an unestablished estimate never carries components: a partial
/// component set would compose into a peak that looks like a real number.
/// </summary>
internal sealed record ResourceEstimate
{
    private ResourceEstimate(
        EstimationStatus status,
        IReadOnlyList<ResourceComponent> components,
        IReadOnlySet<EstimationLimitation> limitations,
        EstimationUnavailableReason reason)
    {
        Status = status;
        Components = components;
        Limitations = limitations;
        Reason = reason;
    }

    internal EstimationStatus Status { get; }

    internal IReadOnlyList<ResourceComponent> Components { get; }

    internal IReadOnlySet<EstimationLimitation> Limitations { get; }

    internal EstimationUnavailableReason Reason { get; }

    internal static ResourceEstimate Established(
        IReadOnlyList<ResourceComponent> components,
        IReadOnlySet<EstimationLimitation> limitations)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(limitations);

        if (components.Count == 0)
        {
            throw new ArgumentException(
                "An established estimate with no components would compose to a peak "
                + "of zero, which reads as a configuration that costs nothing.",
                nameof(components));
        }

        if (limitations.Contains(EstimationLimitation.Unspecified))
        {
            throw new ArgumentException(
                "A caveat must name itself; an unspecified limitation tells the "
                + "user nothing about why the number is uncertain.",
                nameof(limitations));
        }

        // Copy both so a later caller mutation cannot change a recorded estimate.
        return new ResourceEstimate(
            EstimationStatus.Established,
            [.. components],
            new HashSet<EstimationLimitation>(limitations),
            EstimationUnavailableReason.None);
    }

    internal static ResourceEstimate NotEstablished(EstimationUnavailableReason reason)
    {
        if (reason == EstimationUnavailableReason.None)
        {
            throw new ArgumentException(
                "A refusal must name its cause so the user can be told what is "
                + "missing and what would fix it.",
                nameof(reason));
        }

        return new ResourceEstimate(
            EstimationStatus.NotEstablished,
            [],
            new HashSet<EstimationLimitation>(),
            reason);
    }
}
