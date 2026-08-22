using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Defines the process-independent application boundary for one protected
/// Model Inspection worker execution.
/// </summary>
public interface IInspectionWorkerClient
{
    /// <summary>
    /// Executes one already-validated worker command and returns either one
    /// trusted worker terminal message or one controlled infrastructure failure.
    /// </summary>
    /// <param name="command">The immutable command sent through worker stdin.</param>
    /// <param name="progress">An optional sink for validated progress messages.</param>
    /// <param name="cancellationToken">The caller's cancellation request.</param>
    /// <returns>The unambiguous result of the worker execution.</returns>
    Task<WorkerClientResult> ExecuteAsync(
        WorkerStartInspectionCommand command,
        IProgress<WorkerProgressMessage>? progress,
        CancellationToken cancellationToken);
}
