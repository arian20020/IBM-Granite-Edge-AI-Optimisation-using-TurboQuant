namespace GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;

/// <summary>
/// Starts one verified executable through the single reviewed Windows process
/// containment path without knowing the route-specific wire protocol.
/// </summary>
public interface IProtectedWorkerSessionFactory
{
    Task<ProtectedWorkerSession> StartAsync(
        ProtectedWorkerLaunchSpec spec,
        CancellationToken cancellationToken);
}
