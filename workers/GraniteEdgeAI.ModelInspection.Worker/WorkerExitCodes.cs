namespace GraniteEdgeAI.ModelInspection.Worker;

/// <summary>
/// Defines the only process exit codes emitted by the production worker.
/// </summary>
internal static class WorkerExitCodes
{
    internal const int Completed = 0;
    internal const int OperationalFailure = 1;
    internal const int ProtocolFailure = 2;
    internal const int Cancelled = 3;
}
