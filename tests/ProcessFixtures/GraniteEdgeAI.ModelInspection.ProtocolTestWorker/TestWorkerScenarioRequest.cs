namespace GraniteEdgeAI.ModelInspection.ProtocolTestWorker;

/// <summary>
/// Carries one parsed fixture scenario and its optional non-sensitive numeric
/// value. No path, request identifier, or model metadata is accepted here.
/// </summary>
internal sealed record TestWorkerScenarioRequest(
    TestWorkerScenario Scenario,
    long? NumericValue = null);
