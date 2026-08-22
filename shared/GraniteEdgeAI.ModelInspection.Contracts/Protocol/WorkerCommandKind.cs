namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Identifies commands accepted by one short-lived inspection worker.
/// </summary>
public enum WorkerCommandKind
{
    StartInspection,
    CancelInspection
}
