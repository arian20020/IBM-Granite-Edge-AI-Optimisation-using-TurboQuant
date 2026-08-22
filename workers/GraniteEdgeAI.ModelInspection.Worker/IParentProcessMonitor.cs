using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.ModelInspection.Worker;

/// <summary>
/// Verifies and retains the parent process identity, then completes only when
/// that exact process exits.
/// </summary>
internal interface IParentProcessMonitor
{
    Task MonitorAsync(
        WorkerStartInspectionCommand command,
        CancellationToken cancellationToken);
}
