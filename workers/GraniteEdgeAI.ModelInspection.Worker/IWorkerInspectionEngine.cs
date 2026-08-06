using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.ModelInspection.Worker;

/// <summary>
/// Separates the worker protocol lifecycle from the future model-runtime
/// adapter. Gate 2 supplies only the truthful unavailable implementation.
/// </summary>
internal interface IWorkerInspectionEngine
{
    Task<WorkerEngineResult> InspectAsync(
        WorkerStartInspectionCommand command,
        IProgress<WorkerProgressMessage>? progress,
        CancellationToken cancellationToken);
}
