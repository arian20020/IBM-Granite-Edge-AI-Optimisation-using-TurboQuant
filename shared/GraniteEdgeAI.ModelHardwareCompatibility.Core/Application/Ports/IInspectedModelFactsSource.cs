using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>Model facts, or a named reason there are none.</summary>
internal sealed record ModelFactsResolution(
    bool IsEstablished,
    InspectedModelFacts? Facts,
    PortUnavailableReason Reason);

/// <summary>
/// Resolves an inspection identity into C1's own model facts. It never returns an
/// owner type, so this compiles before the Model Inspection handoff exists.
/// </summary>
internal interface IInspectedModelFactsSource
{
    ModelFactsResolution Resolve(string modelInspectionRunId);
}
