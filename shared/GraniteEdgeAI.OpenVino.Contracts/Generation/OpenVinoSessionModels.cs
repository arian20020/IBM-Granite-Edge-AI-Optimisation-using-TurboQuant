namespace GraniteEdgeAI.OpenVino.Contracts;

/// <summary>Validates the only legal event order for one bounded OpenVINO session.</summary>
public sealed class OpenVinoSessionSequenceValidator
{
    private readonly Guid _sessionId;
    private SessionState _state = SessionState.AwaitingHello;
    private Guid? _turnId;
    private long _nextSequence;
    private int _turnCount;
    private int _operationTextBytes;

    public OpenVinoSessionSequenceValidator(Guid sessionId)
    {
        OpenVinoProtocol.RequireUuid(sessionId, nameof(sessionId));
        _sessionId = sessionId;
    }

    public bool IsTerminal => _state == SessionState.Terminal;

    public void Accept(IOpenVinoEvent @event)
    {
        if (@event is null)
        {
            throw new OpenVinoProtocolException("event must be present.");
        }

        @event.Validate();

        switch (@event)
        {
            case HelloEvent hello:
                RequireState(SessionState.AwaitingHello, "hello must be first and occur once");
                hello.Validate();
                _state = SessionState.AwaitingSessionStarted;
                break;
            case SessionStartedEvent started:
                RequireState(SessionState.AwaitingSessionStarted, "sessionStarted requires hello and occurs once");
                RequireSession(started.SessionId);
                _state = SessionState.SessionReady;
                break;
            case GenerationStartedEvent generation:
                RequireState(SessionState.SessionReady, "generationStarted requires a ready session");
                RequireSession(generation.SessionId);
                OpenVinoProtocol.Require(_turnCount < OpenVinoProtocol.MaximumTurns, "session exceeds the maximum turn count.");
                _turnId = generation.TurnId;
                _nextSequence = 0;
                _turnCount++;
                _state = SessionState.Generating;
                break;
            case TokenEvent token:
                RequireState(SessionState.Generating, "token requires an active generation");
                RequireSession(token.SessionId);
                RequireTurn(token.TurnId);
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
                CompleteSession(completed.SessionId);
                break;
            case SessionFailedEvent failed:
                CompleteSession(failed.SessionId);
                break;
            case SessionCancelledEvent cancelled:
                RequireStateOneOf("sessionCancelled requires an active session", SessionState.SessionReady, SessionState.Generating);
                RequireSession(cancelled.SessionId);
                _state = SessionState.Terminal;
                break;
            default:
                throw new OpenVinoProtocolException("event type is not approved by the OpenVINO protocol.");
        }
    }

    private void CompleteTurn(Guid sessionId, Guid turnId)
    {
        RequireState(SessionState.Generating, "turn terminal event requires an active generation");
        RequireSession(sessionId);
        RequireTurn(turnId);
        _turnId = null;
        _state = SessionState.SessionReady;
    }

    private void CompleteSession(Guid sessionId)
    {
        RequireState(SessionState.SessionReady, "session terminal event requires a ready session");
        RequireSession(sessionId);
        _state = SessionState.Terminal;
    }

    private void RequireSession(Guid sessionId) => OpenVinoProtocol.Require(
        sessionId == _sessionId,
        "sessionId must match the active OpenVINO session.");

    private void RequireTurn(Guid turnId) => OpenVinoProtocol.Require(
        _turnId == turnId,
        "turnId must match the active OpenVINO turn.");

    private void RequireState(SessionState expected, string message) => OpenVinoProtocol.Require(_state == expected, message + ".");

    private void RequireStateOneOf(string message, params SessionState[] expected) => OpenVinoProtocol.Require(expected.Contains(_state), message + ".");

    private enum SessionState
    {
        AwaitingHello,
        AwaitingSessionStarted,
        SessionReady,
        Generating,
        Terminal
    }
}
