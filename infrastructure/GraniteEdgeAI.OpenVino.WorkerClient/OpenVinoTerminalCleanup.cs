using GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>
/// Owns the single idempotent transition from a terminal protocol event or
/// forced stop to closed stdin, exited root process, empty Job Object, and EOF.
/// </summary>
internal sealed class OpenVinoTerminalCleanup : IDisposable
{
    private readonly ProtectedWorkerSession _session;
    private readonly Task _processExit;
    private readonly TimeSpan _cleanupTimeout;
    private readonly Action _onCompleted;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TaskCompletionSource<bool> _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _inputCompleted;
    private bool _isComplete;

    internal OpenVinoTerminalCleanup(
        ProtectedWorkerSession session,
        Task processExit,
        TimeSpan cleanupTimeout,
        Action onCompleted)
    {
        _session = session;
        _processExit = processExit;
        _cleanupTimeout = cleanupTimeout;
        _onCompleted = onCompleted;
    }

    internal Task Completion => _completion.Task;

    internal async Task CompleteAsync(
        DateTimeOffset? deadlineUtc,
        bool force)
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_isComplete)
            {
                return;
            }

            if (!_inputCompleted)
            {
                await _session.CompleteInputAsync().ConfigureAwait(false);
                _inputCompleted = true;
            }

            bool timedOut = false;
            if (force)
            {
                _ = await _session.TerminateAndVerifyEmptyAsync()
                    .ConfigureAwait(false);
            }
            else
            {
                try
                {
                    await _processExit.WaitAsync(
                            Remaining(deadlineUtc),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    timedOut = true;
                    _ = await _session.TerminateAndVerifyEmptyAsync()
                        .ConfigureAwait(false);
                }

                if (!timedOut &&
                    !await _session.WaitForTreeEmptyAsync().ConfigureAwait(false))
                {
                    throw OpenVinoWorkerClient.ProtocolFailure();
                }

                if (!timedOut)
                {
                    byte[]? extra = await _session.StandardOutput
                        .ReadLineAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                    if (extra is not null)
                    {
                        throw OpenVinoWorkerClient.ProtocolFailure();
                    }
                }
            }

            MarkCompleted();
            if (timedOut)
            {
                throw OpenVinoWorkerClient.TimeoutFailure();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal void MarkCompleted()
    {
        _isComplete = true;
        _completion.TrySetResult(true);
        _onCompleted();
    }

    public void Dispose() => _gate.Dispose();

    private TimeSpan Remaining(DateTimeOffset? deadlineUtc)
    {
        if (deadlineUtc is null)
        {
            return _cleanupTimeout;
        }

        TimeSpan remaining = deadlineUtc.Value - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.FromTicks(1);
    }
}
