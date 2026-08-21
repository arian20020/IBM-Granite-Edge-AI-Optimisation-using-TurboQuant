using GraniteEdgeAI.ModelInspection.WorkerClient;
using GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;
using GraniteEdgeAI.ModelInspection.Transport;
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
    private readonly CancellationTokenSource _watchdogCancellation = new();
    private readonly Task _watchdogTask;
    private readonly OpenVinoTerminalCleanup _terminalCleanup;
    private readonly VerifiedOpenVinoWorkerClosure _closure;

    private DateTimeOffset _lastActivityUtc = DateTimeOffset.UtcNow;
    private Guid? _activeTurnId;
    private bool _generationStarted;
    private bool _stopSent;
    private bool _closeSent;
    private TerminalCancellationState _cancellationState;
    private bool _disposed;

    internal OpenVinoConversation(
        ProtectedWorkerSession session,
        Task<StandardErrorSnapshot> stderrTask,
        Task processExit,
        OpenVinoConversationValidator validator,
        Guid sessionId,
        OpenVinoWorkerClientOptions options,
        VerifiedOpenVinoWorkerClosure closure)
    {
        _session = session;
        _stderrTask = stderrTask;
        _processExit = processExit;
        _validator = validator;
        _sessionId = sessionId;
        _options = options;
        _closure = closure;
        _terminalCleanup = new OpenVinoTerminalCleanup(
            session,
            processExit,
            options.CleanupTimeout,
            _watchdogCancellation.Cancel);
        _watchdogTask = RunWatchdogAsync();
    }

    public Guid SessionId => _sessionId;

    public async Task<IOpenVinoEvent> PromptAsync(
        PromptCommand command,
        IProgress<TokenEvent>? tokens,
        CancellationToken cancellationToken) =>
        await PromptAsync(
            command,
            tokens,
            generationStarted: null,
            cancellationToken).ConfigureAwait(false);

    public async Task<IOpenVinoEvent> PromptAsync(
        PromptCommand command,
        IProgress<TokenEvent>? tokens,
        IProgress<GenerationStartedEvent>? generationStarted,
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
            if (TryGetCancellationOutcome(out _))
            {
                await AwaitTerminalCleanupOrForceAsync().ConfigureAwait(false);
                throw GetCancellationOutcome();
            }

            throw OpenVinoWorkerClient.ProtocolFailure();
        }
        try
        {
            ThrowIfDisposed();
            DateTimeOffset turnDeadline =
                DateTimeOffset.UtcNow + _options.TurnTimeout;
            RequireSessionWithinBounds();
            lock (_stateLock)
            {
                _validator.Accept(command);
                _activeTurnId = command.TurnId;
                _generationStarted = false;
                _stopSent = false;
                _lastActivityUtc = DateTimeOffset.UtcNow;
            }

            await WriteCommandWithDeadlineAsync(
                    command,
                    turnDeadline,
                    cancellationToken)
                .ConfigureAwait(false);

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
                    await CancelCoreAsync(
                            OpenVinoWorkerClient.CancellationFailure(),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    await AwaitCancellationTerminalAsync().ConfigureAwait(false);
                    throw OpenVinoWorkerClient.CancellationFailure();
                }

                if (IsStale(@event, command.TurnId))
                {
                    continue;
                }

                lock (_stateLock)
                {
                    _validator.Accept(@event);
                    _lastActivityUtc = DateTimeOffset.UtcNow;
                    if (@event is GenerationStartedEvent)
                    {
                        _generationStarted = true;
                    }
                }

                if (@event is GenerationStartedEvent started)
                {
                    generationStarted?.Report(started);
                    continue;
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
                    await _terminalCleanup.CompleteAsync(
                            GetCancellationDeadline(),
                            force: false)
                        .ConfigureAwait(false);
                    if (IsCancellationInProgress())
                    {
                        throw GetCancellationOutcome();
                    }

                    return @event;
                }
            }
        }
        catch (Exception error) when (OpenVinoWorkerClient.IsControlled(error))
        {
            if (IsCancellationInProgress())
            {
                await AwaitTerminalCleanupOrForceAsync().ConfigureAwait(false);
                throw GetCancellationOutcome();
            }

            throw await FailAndMapAsync(error, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>Sends at most one stop for the active generation.</summary>
    public async Task StopAsync(
        Guid expectedTurnId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(expectedTurnId, Guid.Empty);
        StopTurnCommand? command;
        lock (_stateLock)
        {
            ThrowIfDisposed();
            if (_activeTurnId is null)
            {
                return;
            }
            if (_activeTurnId != expectedTurnId)
            {
                throw OpenVinoWorkerClient.ProtocolFailure();
            }
            if (!_generationStarted)
            {
                return;
            }
            if (_stopSent)
            {
                return;
            }

            command = new StopTurnCommand(_sessionId, expectedTurnId);
            _validator.Accept(command);
            _stopSent = true;
            _lastActivityUtc = DateTimeOffset.UtcNow;
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
    public Task CancelAsync(CancellationToken cancellationToken) =>
        CancelOwnedSessionAsync(
            OpenVinoWorkerClient.CancellationFailure(),
            cancellationToken);

    /// <summary>Gracefully closes one idle session and verifies terminal cleanup.</summary>
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (_terminalCleanup.Completion.IsCompleted)
        {
            return;
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_terminalCleanup.Completion.IsCompleted)
            {
                return;
            }

            DateTimeOffset deadline =
                DateTimeOffset.UtcNow + _options.CancellationGrace;
            CloseSessionCommand? command = null;
            lock (_stateLock)
            {
                if (!_closeSent)
                {
                    command = new CloseSessionCommand(_sessionId);
                    _validator.Accept(command);
                    _closeSent = true;
                    _lastActivityUtc = DateTimeOffset.UtcNow;
                }
            }

            if (command is not null)
            {
                await WriteCommandWithDeadlineAsync(
                        command,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            while (!_terminalCleanup.Completion.IsCompleted)
            {
                IOpenVinoEvent @event = await OpenVinoWorkerClient
                    .ReadEventWithDeadlineAsync(
                        _session,
                        _processExit,
                        RemainingUntil(deadline),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (IsStale(@event, turnId: null))
                {
                    continue;
                }

                lock (_stateLock)
                {
                    _validator.Accept(@event);
                    _lastActivityUtc = DateTimeOffset.UtcNow;
                }

                if (@event is SessionCompletedEvent)
                {
                    await _terminalCleanup.CompleteAsync(deadline, force: false)
                        .ConfigureAwait(false);
                    return;
                }

                if (@event is SessionFailedEvent failed)
                {
                    await _terminalCleanup.CompleteAsync(deadline, force: false)
                        .ConfigureAwait(false);
                    throw OpenVinoWorkerClient.WorkerReportedFailure(
                        failed.SupportCode);
                }

                throw OpenVinoWorkerClient.ProtocolFailure();
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

    private async Task CancelOwnedSessionAsync(
        OpenVinoWorkerClientException requestedOutcome,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        bool ownsOperationGate = false;
        try
        {
            await CancelCoreAsync(requestedOutcome, cancellationToken)
                .ConfigureAwait(false);
            if (HasActiveTurn())
            {
                await AwaitTerminalCleanupOrForceAsync().ConfigureAwait(false);
                return;
            }

            await _operationGate.WaitAsync(CancellationToken.None)
                .ConfigureAwait(false);
            ownsOperationGate = true;
            await ReadCancellationTerminalAsOwnerAsync().ConfigureAwait(false);
        }
        catch (Exception error) when (OpenVinoWorkerClient.IsControlled(error))
        {
            OpenVinoWorkerClientException mapped = error switch
            {
                OpenVinoWorkerClientException known => known,
                TimeoutException => OpenVinoWorkerClient.TimeoutFailure(),
                OpenVinoProtocolException or ProtocolStreamException =>
                    OpenVinoWorkerClient.ProtocolFailure(),
                _ => GetCancellationOutcome()
            };
            PromoteCancellationOutcome(mapped);
            await _terminalCleanup.CompleteAsync(
                    deadlineUtc: null,
                    force: true)
                .ConfigureAwait(false);
            throw mapped;
        }
        finally
        {
            if (ownsOperationGate)
            {
                _operationGate.Release();
            }
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
            _watchdogCancellation.Cancel();
            await _watchdogTask.ConfigureAwait(false);
            if (!_terminalCleanup.Completion.IsCompleted)
            {
                try
                {
                    if (HasActiveTurn() || IsCancellationInProgress())
                    {
                        await CancelAsync(CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await CloseAsync(CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception error) when (
                    OpenVinoWorkerClient.IsControlled(error))
                {
                    await _terminalCleanup.CompleteAsync(
                            deadlineUtc: null,
                            force: true)
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _disposed = true;
            _watchdogCancellation.Dispose();
            _operationGate.Dispose();
            _terminalCleanup.Dispose();
            await _session.DisposeAsync().ConfigureAwait(false);
            _closure.Dispose();
        }
    }

    private async Task CancelCoreAsync(
        OpenVinoWorkerClientException requestedOutcome,
        CancellationToken cancellationToken)
    {
        CancelSessionCommand? command = null;
        DateTimeOffset deadlineUtc;
        lock (_stateLock)
        {
            TerminalCancellationState current = _cancellationState;
            OpenVinoWorkerClientException outcome = SelectCancellationOutcome(
                current.Outcome,
                requestedOutcome);
            if (!current.IsOwned && !_terminalCleanup.Completion.IsCompleted)
            {
                command = new CancelSessionCommand(_sessionId);
                _validator.Accept(command);
                deadlineUtc = DateTimeOffset.UtcNow + _options.CancellationGrace;
                _cancellationState = new TerminalCancellationState(
                    IsOwned: true,
                    DeadlineUtc: deadlineUtc,
                    Outcome: outcome);
                _lastActivityUtc = DateTimeOffset.UtcNow;
            }
            else
            {
                deadlineUtc = current.DeadlineUtc ?? DateTimeOffset.UtcNow;
                _cancellationState = current with { Outcome = outcome };
            }
        }

        if (command is not null)
        {
            using CancellationTokenSource deadlineCancellation = new(
                RemainingUntil(deadlineUtc));
            using CancellationTokenSource linked =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    deadlineCancellation.Token);
            try
            {
                await _session.StandardInput.WriteLineAsync(
                        OpenVinoProtocolJson.Serialize(command),
                        linked.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (deadlineCancellation.IsCancellationRequested)
            {
                OpenVinoWorkerClientException timeout =
                    OpenVinoWorkerClient.TimeoutFailure();
                PromoteCancellationOutcome(timeout);
                throw timeout;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw OpenVinoWorkerClient.CancellationFailure();
            }
        }
    }

    private async Task AwaitCancellationTerminalAsync()
    {
        await ReadCancellationTerminalAsOwnerAsync().ConfigureAwait(false);
    }

    private async Task ReadCancellationTerminalAsOwnerAsync()
    {
        while (!_terminalCleanup.Completion.IsCompleted)
        {
            TimeSpan remaining = RemainingCancellationGrace();
            IOpenVinoEvent @event = await OpenVinoWorkerClient
                .ReadEventWithDeadlineAsync(
                    _session,
                    _processExit,
                    remaining,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (IsStale(@event, _activeTurnId))
            {
                continue;
            }

            lock (_stateLock)
            {
                _validator.Accept(@event);
                _lastActivityUtc = DateTimeOffset.UtcNow;
            }

            if (@event is SessionCancelledEvent or SessionFailedEvent)
            {
                await _terminalCleanup.CompleteAsync(
                        GetCancellationDeadline(),
                        force: false)
                    .ConfigureAwait(false);
            }
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
        _terminalCleanup.MarkCompleted();
        return mapped;
    }

    private async Task WriteCommandWithDeadlineAsync(
        IOpenVinoCommand command,
        DateTimeOffset turnDeadline,
        CancellationToken cancellationToken)
    {
        TimeSpan remaining = MinimumRemaining(turnDeadline);
        using CancellationTokenSource deadlineCancellation = new(remaining);
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                deadlineCancellation.Token);
        try
        {
            await _session.StandardInput.WriteLineAsync(
                    OpenVinoProtocolJson.Serialize(command),
                    linked.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested &&
                deadlineCancellation.IsCancellationRequested)
        {
            throw OpenVinoWorkerClient.TimeoutFailure();
        }
    }

    private async Task RunWatchdogAsync()
    {
        CancellationToken cancellationToken = _watchdogCancellation.Token;
        try
        {
            while (!_terminalCleanup.Completion.IsCompleted)
            {
                TimeSpan remaining = RemainingIdleOrSessionLifetime();
                await Task.Delay(remaining, cancellationToken)
                    .ConfigureAwait(false);
                if (_terminalCleanup.Completion.IsCompleted)
                {
                    return;
                }

                bool expired;
                lock (_stateLock)
                {
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    expired =
                        now - _lastActivityUtc >= _options.IdleTimeout ||
                        now - _sessionStartedUtc >= _options.SessionTimeout;
                }
                if (!expired)
                {
                    continue;
                }

                try
                {
                    await CancelOwnedSessionAsync(
                            OpenVinoWorkerClient.TimeoutFailure(),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception error) when (
                    OpenVinoWorkerClient.IsControlled(error))
                {
                    if (!_terminalCleanup.Completion.IsCompleted)
                    {
                        await _terminalCleanup.CompleteAsync(
                                deadlineUtc: null,
                                force: true)
                            .ConfigureAwait(false);
                    }
                }

                return;
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private TimeSpan RemainingIdleOrSessionLifetime()
    {
        lock (_stateLock)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan idle = _lastActivityUtc + _options.IdleTimeout - now;
            TimeSpan session =
                _sessionStartedUtc + _options.SessionTimeout - now;
            TimeSpan remaining = idle < session ? idle : session;
            return remaining > TimeSpan.Zero
                ? remaining
                : TimeSpan.FromTicks(1);
        }
    }

    private async Task AwaitTerminalCleanupOrForceAsync()
    {
        try
        {
            await _terminalCleanup.Completion
                .WaitAsync(RemainingCancellationGrace(), CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            OpenVinoWorkerClientException timeout =
                OpenVinoWorkerClient.TimeoutFailure();
            PromoteCancellationOutcome(timeout);
            await _terminalCleanup.CompleteAsync(
                    deadlineUtc: null,
                    force: true)
                .ConfigureAwait(false);
            throw timeout;
        }
    }

    private bool HasActiveTurn()
    {
        lock (_stateLock)
        {
            return _activeTurnId is not null;
        }
    }

    private bool IsCancellationInProgress()
    {
        lock (_stateLock)
        {
            return _cancellationState.IsOwned;
        }
    }

    private bool TryGetCancellationOutcome(
        out OpenVinoWorkerClientException outcome)
    {
        lock (_stateLock)
        {
            if (_cancellationState.IsOwned &&
                _cancellationState.Outcome is OpenVinoWorkerClientException known)
            {
                outcome = known;
                return true;
            }

            outcome = null!;
            return false;
        }
    }

    private DateTimeOffset? GetCancellationDeadline()
    {
        lock (_stateLock)
        {
            return _cancellationState.DeadlineUtc;
        }
    }

    private TimeSpan RemainingCancellationGrace()
    {
        DateTimeOffset deadline = GetCancellationDeadline()
            ?? DateTimeOffset.UtcNow;
        TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.FromTicks(1);
    }

    private static TimeSpan RemainingUntil(DateTimeOffset deadlineUtc)
    {
        TimeSpan remaining = deadlineUtc - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.FromTicks(1);
    }

    private void PromoteCancellationOutcome(
        OpenVinoWorkerClientException outcome)
    {
        lock (_stateLock)
        {
            _cancellationState = _cancellationState with
            {
                Outcome = SelectCancellationOutcome(
                    _cancellationState.Outcome,
                    outcome)
            };
        }
    }

    private OpenVinoWorkerClientException GetCancellationOutcome()
    {
        lock (_stateLock)
        {
            return _cancellationState.Outcome ??
                OpenVinoWorkerClient.CancellationFailure();
        }
    }

    private static OpenVinoWorkerClientException SelectCancellationOutcome(
        OpenVinoWorkerClientException? current,
        OpenVinoWorkerClientException requested)
    {
        if (current is null ||
            requested.SupportCode == OpenVinoSupportCode.RuntimeTimedOut)
        {
            return requested;
        }

        return current;
    }

    private TimeSpan MinimumRemaining(DateTimeOffset turnDeadline)
    {
        lock (_stateLock)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan turn = turnDeadline - now;
            TimeSpan idle = _lastActivityUtc + _options.IdleTimeout - now;
            TimeSpan session =
                _sessionStartedUtc + _options.SessionTimeout - now;
            TimeSpan minimum = new[] { turn, idle, session }.Min();
            if (minimum <= TimeSpan.Zero)
            {
                throw OpenVinoWorkerClient.TimeoutFailure();
            }

            return minimum;
        }
    }

    private void RequireSessionWithinBounds()
    {
        lock (_stateLock)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now - _lastActivityUtc >= _options.IdleTimeout ||
                now - _sessionStartedUtc >= _options.SessionTimeout)
            {
                throw OpenVinoWorkerClient.TimeoutFailure();
            }
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

    private readonly record struct TerminalCancellationState(
        bool IsOwned,
        DateTimeOffset? DeadlineUtc,
        OpenVinoWorkerClientException? Outcome);
}
