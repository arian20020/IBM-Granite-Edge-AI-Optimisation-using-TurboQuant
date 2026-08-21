using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>A reading taken now, or a named reason there is none.</summary>
internal sealed record FreshMemoryReading(
    bool IsEstablished,
    AvailableResources? Resources,
    PortUnavailableReason Reason);

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
