namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Validates worker-message ordering, request identity, and monotonic progress.
/// </summary>
public sealed class WorkerMessageSequenceValidator
{
    private SequenceState _state = SequenceState.AwaitingHello;

    /// <summary>
    /// Gets whether the one connection-scoped hello was accepted.
    /// </summary>
    public bool HasHello => _state != SequenceState.AwaitingHello;

    /// <summary>
    /// Gets whether the application supplied the request identity it expects.
    /// </summary>
    public bool HasExpectedRequest =>
        _state is SequenceState.AwaitingStarted or
        SequenceState.Running or
        SequenceState.Terminal;

    /// <summary>
    /// Gets whether the worker accepted and acknowledged the request.
    /// </summary>
    public bool HasStarted =>
        _state is SequenceState.Running or SequenceState.Terminal;

    /// <summary>
    /// Gets whether one terminal completion message was accepted.
    /// </summary>
    public bool IsTerminal => _state == SequenceState.Terminal;

    /// <summary>
    /// Gets the request identity supplied by the application.
    /// </summary>
    public Guid? ExpectedRequestId { get; private set; }

    /// <summary>
    /// Gets the most recent accepted progress stage, when progress exists.
    /// </summary>
    public WorkerStage? LastStage { get; private set; }

    /// <summary>
    /// Gets the greatest accepted completed-stage count.
    /// </summary>
    public int CompletedStageCount { get; private set; }

    /// <summary>
    /// Accepts the only connection-scoped worker hello.
    /// </summary>
    public void AcceptHello(WorkerHelloMessage message)
    {
        WorkerHelloMessage validatedMessage =
            WorkerProtocolValidation.RequireNotNull(
                message,
                nameof(message));
        validatedMessage.Validate();

        EnsureState(
            SequenceState.AwaitingHello,
            "hello must be the first and only connection-scoped message");

        _state = SequenceState.AwaitingExpectedRequest;
    }

    /// <summary>
    /// Establishes the request identity expected on all later worker output.
    /// </summary>
    public void SetExpectedRequest(Guid requestId)
    {
        WorkerProtocolValidation.RequireRequestId(requestId);
        EnsureState(
            SequenceState.AwaitingExpectedRequest,
            "expected request can be set once after hello and before started");

        ExpectedRequestId = requestId;
        _state = SequenceState.AwaitingStarted;
    }

    /// <summary>
    /// Accepts the worker's single acknowledgement of the expected request.
    /// </summary>
    public void AcceptStarted(WorkerStartedMessage message)
    {
        WorkerStartedMessage validatedMessage =
            WorkerProtocolValidation.RequireNotNull(
                message,
                nameof(message));
        validatedMessage.Validate();

        EnsureState(
            SequenceState.AwaitingStarted,
            "started requires hello and one expected request and occurs once");
        EnsureRequestId(validatedMessage.RequestId);

        _state = SequenceState.Running;
    }

    /// <summary>
    /// Accepts one request-scoped monotonic progress message.
    /// </summary>
    public void AcceptProgress(WorkerProgressMessage message)
    {
        WorkerProgressMessage validatedMessage =
            WorkerProtocolValidation.RequireNotNull(
                message,
                nameof(message));
        validatedMessage.Validate();

        EnsureState(
            SequenceState.Running,
            "progress requires one started non-terminal request");
        EnsureRequestId(validatedMessage.RequestId);

        if (LastStage is WorkerStage previousStage &&
            validatedMessage.Stage < previousStage)
        {
            throw new WorkerProtocolException(
                "Progress stage must not move backwards.");
        }

        if (validatedMessage.CompletedStageCount < CompletedStageCount)
        {
            throw new WorkerProtocolException(
                "CompletedStageCount must not decrease.");
        }

        LastStage = validatedMessage.Stage;
        CompletedStageCount = validatedMessage.CompletedStageCount;
    }

    /// <summary>
    /// Accepts the only terminal result for the expected request.
    /// </summary>
    public void AcceptCompleted(WorkerCompletedMessage message)
    {
        WorkerCompletedMessage validatedMessage =
            WorkerProtocolValidation.RequireNotNull(
                message,
                nameof(message));
        validatedMessage.Validate();

        EnsureState(
            SequenceState.Running,
            "completed requires one started non-terminal request");
        EnsureRequestId(validatedMessage.RequestId);

        _state = SequenceState.Terminal;
    }

    private void EnsureRequestId(Guid requestId)
    {
        if (ExpectedRequestId != requestId)
        {
            throw new WorkerProtocolException(
                "RequestId must match the application request.");
        }
    }

    private void EnsureState(
        SequenceState expectedState,
        string requirement)
    {
        if (_state != expectedState)
        {
            throw new WorkerProtocolException(requirement + ".");
        }
    }

    private enum SequenceState
    {
        AwaitingHello,
        AwaitingExpectedRequest,
        AwaitingStarted,
        Running,
        Terminal
    }
}
