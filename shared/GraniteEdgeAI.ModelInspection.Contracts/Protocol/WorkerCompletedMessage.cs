namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Ends one worker request with either reliable technical evidence,
/// cooperative cancellation, or a controlled operational failure.
/// </summary>
public sealed record WorkerCompletedMessage
{
    public int ProtocolVersion { get; init; }

    public WorkerMessageKind MessageType { get; init; }

    public Guid RequestId { get; init; }

    public WorkerCompletionStatus CompletionStatus { get; init; }

    public WorkerInspectionEvidence? Evidence { get; init; }

    public WorkerOperationalFailure? OperationalFailure { get; init; }

    /// <summary>
    /// Verifies the exclusive data requirements of each completion status.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.RequireProtocolVersion(ProtocolVersion);
        WorkerProtocolValidation.Require(
            MessageType == WorkerMessageKind.Completed,
            nameof(MessageType),
            "must equal Completed");
        WorkerProtocolValidation.RequireRequestId(RequestId);
        WorkerProtocolValidation.RequireDefinedEnum(
            CompletionStatus,
            nameof(CompletionStatus));

        switch (CompletionStatus)
        {
            case WorkerCompletionStatus.Completed:
                WorkerProtocolValidation.Require(
                    Evidence is not null,
                    nameof(Evidence),
                    "must be present when CompletionStatus is Completed");
                WorkerProtocolValidation.Require(
                    OperationalFailure is null,
                    nameof(OperationalFailure),
                    "must be absent when CompletionStatus is Completed");
                Evidence!.Validate();
                break;

            case WorkerCompletionStatus.Cancelled:
                WorkerProtocolValidation.Require(
                    Evidence is null,
                    nameof(Evidence),
                    "must be absent when CompletionStatus is Cancelled");
                WorkerProtocolValidation.Require(
                    OperationalFailure is null,
                    nameof(OperationalFailure),
                    "must be absent when CompletionStatus is Cancelled");
                break;

            case WorkerCompletionStatus.OperationalFailure:
                WorkerProtocolValidation.Require(
                    Evidence is null,
                    nameof(Evidence),
                    "must be absent when CompletionStatus is OperationalFailure");
                WorkerProtocolValidation.Require(
                    OperationalFailure is not null,
                    nameof(OperationalFailure),
                    "must be present when CompletionStatus is OperationalFailure");
                OperationalFailure!.Validate();
                break;

            default:
                throw new WorkerProtocolException(
                    "CompletionStatus must be a defined protocol value.");
        }
    }
}
