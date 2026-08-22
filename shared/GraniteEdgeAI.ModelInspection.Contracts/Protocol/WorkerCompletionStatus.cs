namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Describes how the worker operation ended independently of model outcome.
/// </summary>
public enum WorkerCompletionStatus
{
    Completed,
    Cancelled,
    OperationalFailure
}
