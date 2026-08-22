namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Describes the state of one worker-reported inspection stage.
/// </summary>
public enum WorkerStageStatus
{
    Active,
    Completed,
    Warning,
    Failed,
    Cancelled
}
