using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.ModelInspection.Worker;

/// <summary>
/// Carries one engine outcome into the protocol terminal message.
/// </summary>
internal sealed record WorkerEngineResult(
    WorkerCompletionStatus CompletionStatus,
    WorkerInspectionEvidence? Evidence,
    WorkerOperationalFailure? OperationalFailure)
{
    internal static WorkerEngineResult ControlledFailure(
        string code,
        string message) => new(
            WorkerCompletionStatus.OperationalFailure,
            Evidence: null,
            new WorkerOperationalFailure
            {
                Code = code,
                Message = message
            });

    internal static WorkerEngineResult Cancelled() => new(
        WorkerCompletionStatus.Cancelled,
        Evidence: null,
        OperationalFailure: null);

    internal static WorkerEngineResult Completed(
        WorkerInspectionEvidence evidence) => new(
            WorkerCompletionStatus.Completed,
            evidence,
            OperationalFailure: null);
}
