using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>
/// A reading taken now, or a named reason there is none.
///
/// Built only through the two factories. An "established" reading with no
/// resources would be a null-reference at the safety gate, and an unavailable one
/// whose reason is None would report an unknown as a zero.
/// </summary>
internal sealed record FreshMemoryReading
{
    private FreshMemoryReading(
        bool isEstablished, AvailableResources? resources, PortUnavailableReason reason)
    {
        IsEstablished = isEstablished;
        Resources = resources;
        Reason = reason;
    }

    internal bool IsEstablished { get; }

    internal AvailableResources? Resources { get; }

    internal PortUnavailableReason Reason { get; }

    internal static FreshMemoryReading Established(AvailableResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        return new FreshMemoryReading(true, resources, PortUnavailableReason.None);
    }

    internal static FreshMemoryReading Unavailable(PortUnavailableReason reason)
    {
        if (reason == PortUnavailableReason.None)
        {
            throw new ArgumentException(
                "An unavailable reading must name why; the gate cannot act on silence.",
                nameof(reason));
        }

        return new FreshMemoryReading(false, null, reason);
    }
}

/// <summary>
/// Re-reads available memory at the safety gate.
///
/// Deliberately separate from IHardwareFactsSource: the handoff's availability
/// figure is historical, and the gate needs a reading taken now. Merging them
/// would make it easy to satisfy the gate with a stale number.
///
/// C1 never implements this against Windows. The only implementation here
/// returns unavailable.
/// </summary>
internal interface IFreshSystemMemoryProbe
{
    FreshMemoryReading Probe();
}
