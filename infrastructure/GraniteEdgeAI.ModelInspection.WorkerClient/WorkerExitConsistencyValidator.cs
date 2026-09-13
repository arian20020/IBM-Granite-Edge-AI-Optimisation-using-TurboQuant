using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Verifies that a trusted terminal message agrees with the worker process exit
/// and that the parent did not have to force termination.
/// </summary>
internal static class WorkerExitConsistencyValidator
{
    // These values mirror the locked Gate 2 worker process contract. They are
    // intentionally private to prevent callers from treating them as a second
    // public protocol surface.
    private const int CompletedExitCode = 0;
    private const int OperationalFailureExitCode = 1;
    private const int CancelledExitCode = 3;

    internal static bool IsConsistent(
        WorkerCompletionStatus status,
        int exitCode,
        bool forcedTermination)
    {
        if (forcedTermination)
        {
            return false;
        }

        return status switch
        {
            WorkerCompletionStatus.Completed =>
                exitCode == CompletedExitCode,
            WorkerCompletionStatus.OperationalFailure =>
                exitCode == OperationalFailureExitCode,
            WorkerCompletionStatus.Cancelled =>
                exitCode == CancelledExitCode,
            _ => false
        };
    }
}
