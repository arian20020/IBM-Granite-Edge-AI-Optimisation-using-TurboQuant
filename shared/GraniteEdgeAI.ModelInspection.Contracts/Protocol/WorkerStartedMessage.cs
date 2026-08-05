namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Confirms that the worker accepted one validated inspection request.
/// </summary>
public sealed record WorkerStartedMessage
{
    public int ProtocolVersion { get; init; }

    public WorkerMessageKind MessageType { get; init; }

    public Guid RequestId { get; init; }

    /// <summary>
    /// Verifies the request-scoped started message.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.RequireProtocolVersion(ProtocolVersion);
        WorkerProtocolValidation.Require(
            MessageType == WorkerMessageKind.Started,
            nameof(MessageType),
            "must equal Started");
        WorkerProtocolValidation.RequireRequestId(RequestId);
    }
}
