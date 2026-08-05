namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Identifies messages emitted by the inspection worker.
/// </summary>
public enum WorkerMessageKind
{
    Hello,
    Started,
    Progress,
    Completed
}
