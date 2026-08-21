using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>Hardware facts, or a named reason there are none.</summary>
internal sealed record HardwareFactsResolution(
    bool IsEstablished,
    HardwareFacts? Facts,
    PortUnavailableReason Reason);

/// <summary>
/// Resolves a hardware run identity into C1's own machine facts. Hardware
/// providers receive no model data; this seam carries facts one way only.
/// </summary>
internal interface IHardwareFactsSource
{
    HardwareFactsResolution Resolve(string productHardwareRunId);
}
