using Microsoft.Win32.SafeHandles;
using GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;
using System.Runtime.ExceptionServices;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient;

internal sealed class GgufWorkerProcessSession : IAsyncDisposable
{
    private readonly SafeFileHandle _processHandle;
    private readonly GgufWorkerJob _job;
    private bool _disposed;

    internal GgufWorkerProcessSession(
        uint processId,
        SafeFileHandle processHandle,
        GgufWorkerJob job,
        FileStream standardInput,
        FileStream standardOutput,
        FileStream standardError)
    {
        ProcessId = processId;
        _processHandle = processHandle;
        _job = job;
        StandardInput = standardInput;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    internal uint ProcessId { get; }

    internal Stream StandardInput { get; }

    internal Stream StandardOutput { get; }

    internal Stream StandardError { get; }

    internal uint ActiveProcessCount => _job.ActiveProcessCount;

    internal IReadOnlyList<int> ProcessIds => _job.GetProcessIds();

    internal async Task TerminateAndVerifyEmptyAsync(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        if (_job.ActiveProcessCount != 0)
        {
            _job.Terminate();
        }

        DateTime deadline = DateTime.UtcNow + timeout;
        while (_job.ActiveProcessCount != 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25).ConfigureAwait(false);
        }

        if (_job.ActiveProcessCount != 0)
        {
            throw new GgufWorkerPolicyException(
                "worker-cleanup-failed",
                "The GGUF runtime worker process tree did not become empty.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Exception? firstFailure = null;
        async ValueTask AttemptAsync(Func<ValueTask> action)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
            }
        }

        void Attempt(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
            }
        }

        await AttemptAsync(StandardInput.DisposeAsync).ConfigureAwait(false);
        Attempt(() =>
        {
            if (_job.ActiveProcessCount != 0)
            {
                _job.Terminate();
            }
        });
        await AttemptAsync(StandardOutput.DisposeAsync).ConfigureAwait(false);
        await AttemptAsync(StandardError.DisposeAsync).ConfigureAwait(false);
        Attempt(_processHandle.Dispose);
        Attempt(_job.Dispose);
        if (firstFailure is not null)
        {
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
    }
}
