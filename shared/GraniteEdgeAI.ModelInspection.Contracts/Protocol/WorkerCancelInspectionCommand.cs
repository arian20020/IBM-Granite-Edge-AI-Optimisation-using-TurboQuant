namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Requests cooperative cancellation of the current inspection request.
/// </summary>
public sealed record WorkerCancelInspectionCommand
{
    public int ProtocolVersion { get; init; }

    public WorkerCommandKind CommandType { get; init; }

    public Guid RequestId { get; init; }

    /// <summary>
    /// Verifies the request-scoped cancellation command.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.RequireProtocolVersion(ProtocolVersion);
        WorkerProtocolValidation.Require(
            CommandType == WorkerCommandKind.CancelInspection,
            nameof(CommandType),
            "must equal CancelInspection");
        WorkerProtocolValidation.RequireRequestId(RequestId);
    }
}
