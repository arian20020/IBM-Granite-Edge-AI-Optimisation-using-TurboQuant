namespace GraniteEdgeAI.OpenVino.Contracts;

/// <summary>
/// Validates the closed command and event conversation for one OpenVINO
/// inspection or bounded generation operation.
/// </summary>
public sealed class OpenVinoConversationValidator
{
    private ConversationState _state = ConversationState.AwaitingHello;
    private Guid? _sessionId;
    private Guid? _inspectionRunId;
    private Guid? _pendingTurnId;
    private Guid? _activeTurnId;
    private long _nextSequence;
    private int _turnCount;
    private int _operationTextBytes;

    public bool IsTerminal => _state == ConversationState.Terminal;

    /// <summary>Accepts exactly one approved command or event in legal order.</summary>
    public void Accept(object value)
    {
        if (value is null)
        {
            throw new OpenVinoProtocolException("protocol value must be present.");
        }

        switch (value)
        {
            case IOpenVinoCommand command:
                command.Validate();
                AcceptCommand(command);
                return;
            case IOpenVinoEvent @event:
                @event.Validate();
                AcceptEvent(@event);
                return;
            default:
                throw new OpenVinoProtocolException("only approved OpenVINO commands and events may enter a conversation.");
        }
    }

    private void AcceptCommand(IOpenVinoCommand command)
    {
        switch (command)
        {
            case StartInspectionCommand startInspection:
                RequireState(ConversationState.AwaitingStart, "startInspection requires hello and occurs once");
                _inspectionRunId = startInspection.InspectionRunId;
                _state = ConversationState.Inspecting;
                break;
            case StartSessionCommand startSession:
                RequireState(ConversationState.AwaitingStart, "startSession requires hello and occurs once");
                _sessionId = startSession.SessionId;
                _inspectionRunId = startSession.InspectionRunId;
                _state = ConversationState.AwaitingSessionStarted;
                break;
            case PromptCommand prompt:
                RequireState(ConversationState.SessionReady, "prompt requires a ready session");
                RequireSession(prompt.SessionId);
                OpenVinoProtocol.Require(_turnCount < OpenVinoProtocol.MaximumTurns, "session exceeds the maximum turn count.");
                _pendingTurnId = prompt.TurnId;
                _turnCount++;
                _state = ConversationState.PromptAccepted;
                break;
            case StopTurnCommand stop:
                RequireState(ConversationState.Generating, "stopTurn requires an active generation");
                RequireSession(stop.SessionId);
                RequireActiveTurn(stop.TurnId);
                _state = ConversationState.Stopping;
                break;
            case CancelSessionCommand cancel:
                RequireStateOneOf(
                    "cancelSession requires an active session",
                    ConversationState.AwaitingSessionStarted,
                    ConversationState.SessionReady,
                    ConversationState.PromptAccepted,
                    ConversationState.Generating,
                    ConversationState.Stopping);
                RequireSession(cancel.SessionId);
                _state = ConversationState.Cancelling;
                break;
            default:
                throw new OpenVinoProtocolException("command type is not approved by the OpenVINO protocol.");
        }
    }

    private void AcceptEvent(IOpenVinoEvent @event)
    {
        switch (@event)
        {
            case HelloEvent:
                RequireState(ConversationState.AwaitingHello, "hello must be first and occur once");
                _state = ConversationState.AwaitingStart;
                break;
            case InspectionCompletedEvent completed:
                CompleteInspection(completed.InspectionRunId);
                break;
            case InspectionFailedEvent failed:
                CompleteInspection(failed.InspectionRunId);
                break;
            case SessionStartedEvent started:
                RequireState(ConversationState.AwaitingSessionStarted, "sessionStarted requires one startSession command");
                RequireSession(started.SessionId);
                _state = ConversationState.SessionReady;
                break;
            case GenerationStartedEvent generation:
                RequireState(ConversationState.PromptAccepted, "generationStarted requires one accepted prompt");
                RequireSession(generation.SessionId);
                RequirePendingTurn(generation.TurnId);
                _pendingTurnId = null;
                _activeTurnId = generation.TurnId;
                _nextSequence = 0;
                _state = ConversationState.Generating;
                break;
            case TokenEvent token:
                RequireState(ConversationState.Generating, "token requires an active generation");
                RequireSession(token.SessionId);
                RequireActiveTurn(token.TurnId);
                OpenVinoProtocol.Require(token.Sequence == _nextSequence, "token sequence must be contiguous and monotonic.");
                checked
                {
                    _operationTextBytes += OpenVinoProtocol.StrictUtf8.GetByteCount(token.Text);
                }
                OpenVinoProtocol.Require(_operationTextBytes <= OpenVinoProtocol.MaximumOperationTextUtf8Bytes, "operation text exceeds the permitted UTF-8 length.");
                _nextSequence++;
                break;
            case TurnCompletedEvent completed:
                CompleteTurn(completed.SessionId, completed.TurnId);
                break;
            case TurnFailedEvent failed:
                CompleteTurn(failed.SessionId, failed.TurnId);
                break;
            case SessionCompletedEvent completed:
                RequireState(ConversationState.SessionReady, "sessionCompleted requires a ready session");
                RequireSession(completed.SessionId);
                _state = ConversationState.Terminal;
                break;
            case SessionFailedEvent failed:
                RequireStateOneOf(
                    "sessionFailed requires an active session",
                    ConversationState.AwaitingSessionStarted,
                    ConversationState.SessionReady,
                    ConversationState.PromptAccepted,
                    ConversationState.Generating,
                    ConversationState.Stopping,
                    ConversationState.Cancelling);
                RequireSession(failed.SessionId);
                _state = ConversationState.Terminal;
                break;
            case SessionCancelledEvent cancelled:
                RequireState(ConversationState.Cancelling, "sessionCancelled requires one cancelSession command");
                RequireSession(cancelled.SessionId);
                _state = ConversationState.Terminal;
                break;
            default:
                throw new OpenVinoProtocolException("event type is not approved by the OpenVINO protocol.");
        }
    }

    private void CompleteInspection(Guid inspectionRunId)
    {
        RequireState(ConversationState.Inspecting, "inspection terminal event requires startInspection");
        OpenVinoProtocol.Require(
            _inspectionRunId == inspectionRunId,
            "inspectionRunId must match the active OpenVINO inspection.");
        _state = ConversationState.Terminal;
    }

    private void CompleteTurn(Guid sessionId, Guid turnId)
    {
        RequireStateOneOf(
            "turn terminal event requires an active generation or stop request",
            ConversationState.Generating,
            ConversationState.Stopping);
        RequireSession(sessionId);
        RequireActiveTurn(turnId);
        _activeTurnId = null;
        _state = ConversationState.SessionReady;
    }

    private void RequireSession(Guid sessionId) => OpenVinoProtocol.Require(
        _sessionId == sessionId,
        "sessionId must match the active OpenVINO session.");

    private void RequirePendingTurn(Guid turnId) => OpenVinoProtocol.Require(
        _pendingTurnId == turnId,
        "turnId must match the accepted OpenVINO prompt.");

    private void RequireActiveTurn(Guid turnId) => OpenVinoProtocol.Require(
        _activeTurnId == turnId,
        "turnId must match the active OpenVINO turn.");

    private void RequireState(ConversationState expected, string message) =>
        OpenVinoProtocol.Require(_state == expected, message + ".");

    private void RequireStateOneOf(string message, params ConversationState[] expected) =>
        OpenVinoProtocol.Require(expected.Contains(_state), message + ".");

    private enum ConversationState
    {
        AwaitingHello,
        AwaitingStart,
        Inspecting,
        AwaitingSessionStarted,
        SessionReady,
        PromptAccepted,
        Generating,
        Stopping,
        Cancelling,
        Terminal
    }
}
