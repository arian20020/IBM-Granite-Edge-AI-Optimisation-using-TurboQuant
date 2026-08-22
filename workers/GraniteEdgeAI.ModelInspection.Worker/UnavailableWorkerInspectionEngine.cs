using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.ModelInspection.Worker;

/// <summary>
/// Gate 2 engine that opens no model file and makes no runtime claims.
/// </summary>
internal sealed class UnavailableWorkerInspectionEngine :
    IWorkerInspectionEngine
{
    private const string FailureCode = "MI-OP-ENGINE-NOT-CONFIGURED";
    private const string FailureMessage =
        "The model inspection runtime is not configured in this worker build.";

    public Task<WorkerEngineResult> InspectAsync(
        WorkerStartInspectionCommand command,
        IProgress<WorkerProgressMessage>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            WorkerEngineResult.ControlledFailure(
                FailureCode,
                FailureMessage));
    }
}
