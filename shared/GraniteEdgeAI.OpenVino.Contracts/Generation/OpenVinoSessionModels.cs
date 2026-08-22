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
    private string? _protocolId;
    private OpenVinoBuildEvidence? _helloBuildEvidence;
    private string? _packageManifestDigest;
    private string? _modelSha256;
    private long _modelLengthBytes;
    private string? _requestedDevice;
    private string? _requestedKvCachePrecision;
    private OpenVinoInspectionStage? _nextInspectionStage;
    private long _nextSequence;
    private int _maximumContextTokens;
    private int _requestedNewTokens;
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
                CapturePackageIdentity(
                    startInspection.PackageManifestDigest,
                    startInspection.ModelSha256,
                    startInspection.ModelLengthBytes);
                _state = ConversationState.AwaitingInspectionStarted;
                break;
            case StartSessionCommand startSession:
                RequireState(ConversationState.AwaitingStart, "startSession requires hello and occurs once");
                _sessionId = startSession.SessionId;
                _inspectionRunId = startSession.InspectionRunId;
                _requestedDevice = startSession.Device.DeviceId;
                _requestedKvCachePrecision = startSession.Runtime.KvCachePrecision;
                _maximumContextTokens = startSession.Limits.MaximumContextTokens;
                CapturePackageIdentity(
                    startSession.PackageManifestDigest,
                    startSession.ModelSha256,
                    startSession.ModelLengthBytes);
                _state = ConversationState.AwaitingSessionStarted;
                break;
            case PromptCommand prompt:
                RequireState(ConversationState.SessionReady, "prompt requires a ready session");
                RequireSession(prompt.SessionId);
                OpenVinoProtocol.Require(_turnCount < OpenVinoProtocol.MaximumTurns, "session exceeds the maximum turn count.");
                _pendingTurnId = prompt.TurnId;
                _requestedNewTokens = prompt.RequestedNewTokens;
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
            case CloseSessionCommand close:
                RequireState(
                    ConversationState.SessionReady,
                    "closeSession requires an idle ready session");
                RequireSession(close.SessionId);
                _state = ConversationState.Closing;
                break;
            default:
                throw new OpenVinoProtocolException("command type is not approved by the OpenVINO protocol.");
        }
    }

    private void AcceptEvent(IOpenVinoEvent @event)
    {
        switch (@event)
        {
            case HelloEvent hello:
                RequireState(ConversationState.AwaitingHello, "hello must be first and occur once");
                _protocolId = hello.ProtocolId;
                _helloBuildEvidence = hello.BuildEvidence;
                _state = ConversationState.AwaitingStart;
                break;
            case InspectionStartedEvent started:
                RequireState(
                    ConversationState.AwaitingInspectionStarted,
                    "inspectionStarted requires one startInspection command");
                RequireInspection(started.InspectionRunId);
                _nextInspectionStage = OpenVinoInspectionStage.ManifestVerified;
                _state = ConversationState.Inspecting;
                break;
            case InspectionProgressEvent progress:
                RequireState(
                    ConversationState.Inspecting,
                    "inspectionProgress requires an active inspection");
                RequireInspection(progress.InspectionRunId);
                OpenVinoProtocol.Require(
                    progress.Stage == _nextInspectionStage,
                    "inspectionProgress stage must be ordered and occur once.");
                _nextInspectionStage = progress.Stage switch
                {
                    OpenVinoInspectionStage.ManifestVerified =>
                        OpenVinoInspectionStage.MainModelParsed,
                    OpenVinoInspectionStage.MainModelParsed =>
                        OpenVinoInspectionStage.TokenizerParsed,
                    OpenVinoInspectionStage.TokenizerParsed =>
                        OpenVinoInspectionStage.DetokenizerParsed,
                    OpenVinoInspectionStage.DetokenizerParsed => null,
                    _ => throw new OpenVinoProtocolException(
                        "inspectionProgress stage is not approved.")
                };
                break;
            case InspectionCompletedEvent completed:
                CompleteInspection(completed);
                break;
            case InspectionFailedEvent failed:
                RequireState(
                    ConversationState.Inspecting,
                    "inspection failure requires inspectionStarted");
                RequireInspection(failed.InspectionRunId);
                _state = ConversationState.Terminal;
                break;
            case SessionStartedEvent started:
                RequireState(ConversationState.AwaitingSessionStarted, "sessionStarted requires one startSession command");
                RequireSession(started.SessionId);
                OpenVinoProtocol.Require(
                    string.Equals(
                        started.RequestedDevice,
                        _requestedDevice,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        started.ProtocolId,
                        _protocolId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        started.RequestedKvCachePrecision,
                        _requestedKvCachePrecision,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        started.ActualKvCachePrecision,
                        _requestedKvCachePrecision,
                        StringComparison.Ordinal) &&
                    started.ActualExecutionDevices.Count == 1 &&
                    string.Equals(
                        started.ActualExecutionDevices[0],
                        _requestedDevice,
                        StringComparison.Ordinal) &&
                    started.BuildEvidence == _helloBuildEvidence,
                    "sessionStarted runtime evidence must match the request.");
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
                OpenVinoProtocol.Require(
                    completed.PromptTokenCount <= _maximumContextTokens,
                    "turnCompleted prompt token count must fit the session context limit.");
                OpenVinoProtocol.Require(
                    completed.GeneratedTokenCount <= _requestedNewTokens,
                    "turnCompleted generated token count must not exceed the prompt request.");
                OpenVinoProtocol.Require(
                    completed.PromptTokenCount <= _maximumContextTokens &&
                    completed.GeneratedTokenCount <=
                        _maximumContextTokens - completed.PromptTokenCount,
                    "turnCompleted prompt and generated token counts must fit the session context limit.");
                OpenVinoProtocol.Require(
                    completed.Disposition ==
                        (_state == ConversationState.Stopping
                            ? OpenVinoTurnDisposition.Stopped
                            : OpenVinoTurnDisposition.Completed),
                    "turnCompleted disposition must match stop ownership.");
                CompleteTurn(completed.SessionId, completed.TurnId);
                break;
            case TurnFailedEvent failed:
                CompleteTurn(failed.SessionId, failed.TurnId);
                break;
            case SessionCompletedEvent completed:
                RequireState(ConversationState.Closing, "sessionCompleted requires closeSession");
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
                    ConversationState.Cancelling,
                    ConversationState.Closing);
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
        RequireState(
            ConversationState.Inspecting,
            "inspection terminal event requires inspectionStarted");
        RequireInspection(inspectionRunId);
        OpenVinoProtocol.Require(
            _nextInspectionStage is null,
            "inspection completion requires every ordered progress stage.");
        _state = ConversationState.Terminal;
    }

    private void CompleteInspection(InspectionCompletedEvent completed)
    {
        OpenVinoProtocol.Require(
            string.Equals(
                completed.PackageManifestDigest,
                _packageManifestDigest,
                StringComparison.Ordinal) &&
            string.Equals(
                completed.ModelSha256,
                _modelSha256,
                StringComparison.Ordinal) &&
            completed.ModelLengthBytes == _modelLengthBytes,
            "inspection completion identity must match the request.");
        OpenVinoProtocol.Require(
            completed.BuildEvidence == _helloBuildEvidence,
            "inspection completion build evidence must match hello.");
        CompleteInspection(completed.InspectionRunId);
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
        _requestedNewTokens = 0;
        _state = ConversationState.SessionReady;
    }

    private void RequireSession(Guid sessionId) => OpenVinoProtocol.Require(
        _sessionId == sessionId,
        "sessionId must match the active OpenVINO session.");

    private void RequireInspection(Guid inspectionRunId) =>
        OpenVinoProtocol.Require(
            _inspectionRunId == inspectionRunId,
            "inspectionRunId must match the active OpenVINO inspection.");

    private void CapturePackageIdentity(
        string packageManifestDigest,
        string modelSha256,
        long modelLengthBytes)
    {
        _packageManifestDigest = packageManifestDigest;
        _modelSha256 = modelSha256;
        _modelLengthBytes = modelLengthBytes;
    }

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
        AwaitingInspectionStarted,
        Inspecting,
        AwaitingSessionStarted,
        SessionReady,
        PromptAccepted,
        Generating,
        Stopping,
        Cancelling,
        Closing,
        Terminal
    }
}
