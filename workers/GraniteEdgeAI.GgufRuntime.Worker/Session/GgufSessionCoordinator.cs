using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;

namespace GraniteEdgeAI.GgufRuntime.Worker.Session;

internal sealed class GgufSessionCoordinator
{
    private readonly IGgufCliProcess _process;
    private readonly string _modelPath;
    private readonly GgufSessionStateMachine _state = new(GgufSessionState.Created);
    private GgufSessionId? _sessionId;
    private Guid _activeRequestId;
    private long _nextSequence;

    internal GgufSessionCoordinator(IGgufCliProcess process, string modelPath)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        _process = process;
        _modelPath = modelPath;
    }

    internal GgufSessionState State => _state.Current;

    internal async ValueTask<IReadOnlyList<GgufRuntimeEvent>> StartSessionAsync(
        StartSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (State != GgufSessionState.Created)
        {
            throw new InvalidOperationException("A session can be started only once.");
        }

        _sessionId = command.SessionId;
        _activeRequestId = command.RequestId;
        _state.AdvanceTo(GgufSessionState.Starting);
        _state.AdvanceTo(GgufSessionState.Loading);
        var loading = new SessionLoadingEvent(
            GgufProtocolVersion.Current,
            command.RequestId,
            command.SessionId,
            NextSequence(),
            "load-model");
        try
        {
            await _process.StartAsync(
                _modelPath,
                command.Configuration,
                command.InitialTurns,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _state.AdvanceTo(GgufSessionState.Failed);
            throw;
        }

        _state.AdvanceTo(GgufSessionState.Ready);
        var ready = new SessionReadyEvent(
            GgufProtocolVersion.Current,
            command.RequestId,
            command.SessionId,
            NextSequence());
        return [loading, ready];
    }

    internal async ValueTask<ResponseStartedEvent> SubmitPromptAsync(
        SubmitPromptCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (State != GgufSessionState.Ready || _sessionId != command.SessionId)
        {
            throw new InvalidOperationException("The session is not ready for a prompt.");
        }

        _state.AdvanceTo(GgufSessionState.Generating);
        _activeRequestId = command.RequestId;
        try
        {
            await _process.WritePromptAsync(
                command.Content,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _state.AdvanceTo(GgufSessionState.Failed);
            throw;
        }

        return new ResponseStartedEvent(
            GgufProtocolVersion.Current,
            command.RequestId,
            command.SessionId,
            NextSequence());
    }

    internal GgufRuntimeEvent? HandleStandardOutput(string line)
    {
        GgufCliOutput output = GgufCliOutputParser.Parse(
            line,
            GgufCliOutputSource.StandardOutput);
        if (_sessionId is not GgufSessionId sessionId)
        {
            throw new InvalidOperationException("The session has not started.");
        }

        return output.Kind switch
        {
            GgufCliOutputKind.Ready => null,
            GgufCliOutputKind.ResponseStarted => null,
            GgufCliOutputKind.TextDelta when State == GgufSessionState.Generating =>
                new TextDeltaEvent(
                    GgufProtocolVersion.Current,
                    _activeRequestId,
                    sessionId,
                    NextSequence(),
                    output.Text!),
            GgufCliOutputKind.ResponseCompleted when State == GgufSessionState.Generating =>
                CompleteResponse(sessionId),
            _ => throw new InvalidOperationException(
                "The CLI output is not valid for the current session state."),
        };
    }

    private ResponseCompletedEvent CompleteResponse(GgufSessionId sessionId)
    {
        _state.AdvanceTo(GgufSessionState.Ready);
        return new ResponseCompletedEvent(
            GgufProtocolVersion.Current,
            _activeRequestId,
            sessionId,
            NextSequence());
    }

    private long NextSequence()
    {
        return _nextSequence++;
    }
}
