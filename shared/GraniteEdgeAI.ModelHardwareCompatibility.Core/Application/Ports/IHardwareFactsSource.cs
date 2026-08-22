using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>
/// Hardware facts, or a named reason there are none. Built only through the two
/// factories, so an established resolution always carries facts and an
/// unavailable one always names its reason.
/// </summary>
internal sealed record HardwareFactsResolution
{
    private HardwareFactsResolution(
        bool isEstablished, HardwareFacts? facts, PortUnavailableReason reason)
    {
        IsEstablished = isEstablished;
        Facts = facts;
        Reason = reason;
    }

    internal bool IsEstablished { get; }

    internal HardwareFacts? Facts { get; }

    internal PortUnavailableReason Reason { get; }

    internal static HardwareFactsResolution Established(HardwareFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return new HardwareFactsResolution(true, facts, PortUnavailableReason.None);
    }

    internal static HardwareFactsResolution Unavailable(PortUnavailableReason reason)
    {
        if (reason == PortUnavailableReason.None)
        {
            throw new ArgumentException(
                "An unavailable resolution must name why.", nameof(reason));
        }

        return new HardwareFactsResolution(false, null, reason);
    }
}

/// <summary>
/// Resolves a hardware run identity into C1's own machine facts. Hardware
/// providers receive no model data; this seam carries facts one way only.
/// </summary>
internal interface IHardwareFactsSource
{
    HardwareFactsResolution Resolve(string productHardwareRunId);
}
