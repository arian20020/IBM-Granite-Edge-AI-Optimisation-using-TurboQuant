namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Validates the command order accepted by one short-lived inspection worker.
/// </summary>
public sealed class WorkerCommandSequenceValidator
{
    private SequenceState _state = SequenceState.AwaitingStart;

    /// <summary>
    /// Gets whether one valid start command has been accepted.
    /// </summary>
    public bool HasStarted => _state != SequenceState.AwaitingStart;

    /// <summary>
    /// Gets whether at least one matching cancellation command was accepted.
    /// </summary>
    public bool CancellationRequested { get; private set; }

    /// <summary>
    /// Gets whether the worker has produced its terminal result.
    /// </summary>
    public bool IsTerminal => _state == SequenceState.Terminal;

    /// <summary>
    /// Gets the request identity established by the accepted start command.
    /// </summary>
    public Guid? RequestId { get; private set; }

    /// <summary>
    /// Accepts the only start command permitted for this worker process.
    /// </summary>
    public void AcceptStart(WorkerStartInspectionCommand command)
    {
        WorkerStartInspectionCommand validatedCommand =
            WorkerProtocolValidation.RequireNotNull(
                command,
                nameof(command));
        validatedCommand.Validate();

        EnsureState(
            SequenceState.AwaitingStart,
            "start command is only valid before inspection begins");

        RequestId = validatedCommand.RequestId;
        _state = SequenceState.Running;
    }

    /// <summary>
    /// Accepts an idempotent cancellation request for the active request.
    /// </summary>
    public void AcceptCancel(WorkerCancelInspectionCommand command)
    {
        WorkerCancelInspectionCommand validatedCommand =
            WorkerProtocolValidation.RequireNotNull(
                command,
                nameof(command));
        validatedCommand.Validate();

        EnsureState(
            SequenceState.Running,
            "cancel command requires one active non-terminal request");
        EnsureRequestId(validatedCommand.RequestId);

        CancellationRequested = true;
    }

    /// <summary>
    /// Marks the active request terminal so no later command can be accepted.
    /// </summary>
    public void MarkTerminal()
    {
        EnsureState(
            SequenceState.Running,
            "terminal state requires one active non-terminal request");

        _state = SequenceState.Terminal;
    }

    private void EnsureRequestId(Guid requestId)
    {
        if (RequestId != requestId)
        {
            throw new WorkerProtocolException(
                "RequestId must match the active worker request.");
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
        AwaitingStart,
        Running,
        Terminal
    }
}
