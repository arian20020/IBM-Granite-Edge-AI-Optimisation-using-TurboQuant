using GraniteEdgeAI.ModelInspection.WorkerClient;
using GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>Owns one protected OpenVINO generation process and its strict state.</summary>
public sealed class OpenVinoConversation : IAsyncDisposable
{
    private readonly ProtectedWorkerSession _session;
    private readonly Task<StandardErrorSnapshot> _stderrTask;
    private readonly Task _processExit;
    private readonly OpenVinoConversationValidator _validator;
    private readonly Guid _sessionId;
    private readonly OpenVinoWorkerClientOptions _options;
    private readonly DateTimeOffset _sessionStartedUtc = DateTimeOffset.UtcNow;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _stateLock = new();
    private readonly TaskCompletionSource<bool> _sessionTerminal = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private DateTimeOffset _lastActivityUtc = DateTimeOffset.UtcNow;
    private Guid? _activeTurnId;
    private bool _generationStarted;
    private bool _stopSent;
    private bool _cancelSent;
    private bool _disposed;

    internal OpenVinoConversation(
        ProtectedWorkerSession session,
        Task<StandardErrorSnapshot> stderrTask,
        Task processExit,
        OpenVinoConversationValidator validator,
        Guid sessionId,
        OpenVinoWorkerClientOptions options)
    {
        _session = session;
        _stderrTask = stderrTask;
        _processExit = processExit;
        _validator = validator;
        _sessionId = sessionId;
        _options = options;
    }

    public Guid SessionId => _sessionId;

    public async Task<IOpenVinoEvent> PromptAsync(
        PromptCommand command,
        IProgress<TokenEvent>? tokens,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();
        if (command.SessionId != _sessionId)
        {
            throw OpenVinoWorkerClient.ProtocolFailure();
        }

        if (!await _operationGate
            .WaitAsync(TimeSpan.Zero, cancellationToken)
            .ConfigureAwait(false))
        {
            throw OpenVinoWorkerClient.ProtocolFailure();
        }
        try
        {
            ThrowIfDisposed();
            RequireSessionWithinBounds();
            lock (_stateLock)
            {
                _validator.Accept(command);
                _activeTurnId = command.TurnId;
                _generationStarted = false;
                _stopSent = false;
            }

            await _session.StandardInput.WriteLineAsync(
                    OpenVinoProtocolJson.Serialize(command),
                    cancellationToken)
                .ConfigureAwait(false);
            DateTimeOffset turnDeadline =
                DateTimeOffset.UtcNow + _options.TurnTimeout;

            while (true)
            {
                TimeSpan wait = MinimumRemaining(turnDeadline);
                IOpenVinoEvent @event;
                try
                {
                    @event = await OpenVinoWorkerClient
                        .ReadEventWithDeadlineAsync(
                            _session,
                            _processExit,
                            wait,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    await CancelCoreAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                    await AwaitCancellationTerminalAsync().ConfigureAwait(false);
                    throw OpenVinoWorkerClient.CancellationFailure();
                }

                _lastActivityUtc = DateTimeOffset.UtcNow;
                if (IsStale(@event, command.TurnId))
                {
                    continue;
                }

                lock (_stateLock)
                {
                    _validator.Accept(@event);
                    if (@event is GenerationStartedEvent)
                    {
                        _generationStarted = true;
                    }
                }

                if (@event is TokenEvent token)
                {
                    tokens?.Report(token);
                    continue;
                }

                if (@event is TurnCompletedEvent or TurnFailedEvent)
                {
                    lock (_stateLock)
                    {
                        _activeTurnId = null;
                        _generationStarted = false;
                    }

                    return @event;
                }

                if (@event is SessionCancelledEvent or
                    SessionFailedEvent or SessionCompletedEvent)
                {
                    _sessionTerminal.TrySetResult(true);
                    await FinalizeTerminalAsync().ConfigureAwait(false);
                    return @event;
                }
            }
        }
        catch (Exception error) when (OpenVinoWorkerClient.IsControlled(error))
        {
            throw await FailAndMapAsync(error, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>Sends at most one stop for the active generation.</summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        StopTurnCommand? command;
        lock (_stateLock)
        {
            ThrowIfDisposed();
            if (_stopSent || !_generationStarted || _activeTurnId is not Guid turnId)
            {
                return;
            }

            command = new StopTurnCommand(_sessionId, turnId);
            _validator.Accept(command);
            _stopSent = true;
        }

        try
        {
            await _session.StandardInput.WriteLineAsync(
                    OpenVinoProtocolJson.Serialize(command),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (OpenVinoWorkerClient.IsControlled(error))
        {
            throw await FailAndMapAsync(error, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Sends one idempotent session cancellation and verifies exit.</summary>
    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await CancelCoreAsync(cancellationToken).ConfigureAwait(false);
        if (_operationGate.CurrentCount == 0)
        {
            await _sessionTerminal.Task
                .WaitAsync(_options.CancellationGrace, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (!_sessionTerminal.Task.IsCompleted)
            {
                IOpenVinoEvent @event = await OpenVinoWorkerClient
                    .ReadEventWithDeadlineAsync(
                        _session,
                        _processExit,
                        _options.CancellationGrace,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (IsStale(@event, _activeTurnId))
                {
                    continue;
                }

                lock (_stateLock)
                {
                    _validator.Accept(@event);
                }

                if (@event is SessionCancelledEvent or SessionFailedEvent)
                {
                    _sessionTerminal.TrySetResult(true);
                }
            }

            await FinalizeTerminalAsync().ConfigureAwait(false);
        }
        catch (Exception error) when (OpenVinoWorkerClient.IsControlled(error))
        {
            throw await FailAndMapAsync(error, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!_sessionTerminal.Task.IsCompleted)
            {
                try
                {
                    await CancelAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception error) when (
                    OpenVinoWorkerClient.IsControlled(error))
                {
                    _ = await _session.TerminateAndVerifyEmptyAsync()
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _disposed = true;
            _operationGate.Dispose();
            await _session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task CancelCoreAsync(CancellationToken cancellationToken)
    {
        CancelSessionCommand? command = null;
        lock (_stateLock)
        {
            if (!_cancelSent && !_sessionTerminal.Task.IsCompleted)
            {
                command = new CancelSessionCommand(_sessionId);
                _validator.Accept(command);
                _cancelSent = true;
            }
        }

        if (command is not null)
        {
            await _session.StandardInput.WriteLineAsync(
                    OpenVinoProtocolJson.Serialize(command),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task AwaitCancellationTerminalAsync()
    {
        while (!_sessionTerminal.Task.IsCompleted)
        {
            IOpenVinoEvent @event = await OpenVinoWorkerClient
                .ReadEventWithDeadlineAsync(
                    _session,
                    _processExit,
                    _options.CancellationGrace,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (IsStale(@event, _activeTurnId))
            {
                continue;
            }

            lock (_stateLock)
            {
                _validator.Accept(@event);
            }

            if (@event is SessionCancelledEvent or SessionFailedEvent)
            {
                _sessionTerminal.TrySetResult(true);
            }
        }

        await FinalizeTerminalAsync().ConfigureAwait(false);
    }

    private async Task FinalizeTerminalAsync()
    {
        await _session.CompleteInputAsync().ConfigureAwait(false);
        await _processExit.WaitAsync(_options.CleanupTimeout)
            .ConfigureAwait(false);
        if (!await _session.WaitForTreeEmptyAsync().ConfigureAwait(false))
        {
            throw OpenVinoWorkerClient.ProtocolFailure();
        }
    }

    private async Task<OpenVinoWorkerClientException> FailAndMapAsync(
        Exception error,
        CancellationToken callerToken)
    {
        OpenVinoWorkerClientException mapped =
            await OpenVinoWorkerClient.ConvertFailureAsync(
                    error,
                    _session,
                    _stderrTask,
                    callerToken)
                .ConfigureAwait(false);
        _sessionTerminal.TrySetResult(true);
        return mapped;
    }

    private TimeSpan MinimumRemaining(DateTimeOffset turnDeadline)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan turn = turnDeadline - now;
        TimeSpan idle = _lastActivityUtc + _options.IdleTimeout - now;
        TimeSpan session = _sessionStartedUtc + _options.SessionTimeout - now;
        TimeSpan minimum = new[] { turn, idle, session }.Min();
        if (minimum <= TimeSpan.Zero)
        {
            throw OpenVinoWorkerClient.TimeoutFailure();
        }

        return minimum;
    }

    private void RequireSessionWithinBounds()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - _lastActivityUtc >= _options.IdleTimeout ||
            now - _sessionStartedUtc >= _options.SessionTimeout)
        {
            throw OpenVinoWorkerClient.TimeoutFailure();
        }
    }

    private bool IsStale(IOpenVinoEvent @event, Guid? turnId) => @event switch
    {
        SessionStartedEvent value => value.SessionId != _sessionId,
        GenerationStartedEvent value =>
            value.SessionId != _sessionId || value.TurnId != turnId,
        TokenEvent value =>
            value.SessionId != _sessionId || value.TurnId != turnId,
        TurnCompletedEvent value =>
            value.SessionId != _sessionId || value.TurnId != turnId,
        TurnFailedEvent value =>
            value.SessionId != _sessionId || value.TurnId != turnId,
        SessionCompletedEvent value => value.SessionId != _sessionId,
        SessionFailedEvent value => value.SessionId != _sessionId,
        SessionCancelledEvent value => value.SessionId != _sessionId,
        _ => false
    };

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
