using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HardwareInspection.LlmFitSpike.Command;

[assembly: InternalsVisibleTo("HardwareInspection.LlmFitSpike.Tests")]

namespace HardwareInspection.LlmFitSpike.Execution;

public sealed class LlmFitProcessRunner
{
    public const int MaximumCapturedBytesPerStream = 1_048_576;
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaximumTimeout = TimeSpan.FromSeconds(120);
    private static readonly Task NeverCompletes = Task.Delay(Timeout.InfiniteTimeSpan);
    private readonly Func<Stream, int, Task<BoundedTextCapture>> _captureReader;
    private readonly Func<Process, Task> _waitForExit;
    private readonly TimeSpan _minimumTimeout;

    public LlmFitProcessRunner()
        : this(TimeSpan.FromSeconds(1), BoundedTextReader.ReadAsync, WaitForExitAsync)
    {
    }

    internal LlmFitProcessRunner(TimeSpan minimumTimeout)
        : this(minimumTimeout, BoundedTextReader.ReadAsync, WaitForExitAsync)
    {
    }

    internal LlmFitProcessRunner(
        TimeSpan minimumTimeout,
        Func<Stream, int, Task<BoundedTextCapture>> captureReader)
        : this(minimumTimeout, captureReader, WaitForExitAsync)
    {
    }

    internal LlmFitProcessRunner(
        TimeSpan minimumTimeout,
        Func<Stream, int, Task<BoundedTextCapture>> captureReader,
        Func<Process, Task> waitForExit)
    {
        if (minimumTimeout <= TimeSpan.Zero || minimumTimeout > TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumTimeout));
        }

        _minimumTimeout = minimumTimeout;
        _captureReader = captureReader ?? throw new ArgumentNullException(nameof(captureReader));
        _waitForExit = waitForExit ?? throw new ArgumentNullException(nameof(waitForExit));
    }

    public async Task<LlmFitProcessResult> ExecuteAsync(
        LlmFitCommand command,
        TimeSpan? timeout = null,
        Func<int, CancellationToken, Task>? whileRunningObserver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        TimeSpan effectiveTimeout = timeout ?? DefaultTimeout;
        ValidateTimeout(effectiveTimeout);

        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        if (cancellationToken.IsCancellationRequested)
        {
            return CreateResult(
                startedAtUtc,
                processId: null,
                exitCode: null,
                processStartFailed: false,
                observerFailed: false,
                timedOut: false,
                cancelled: true,
                BoundedTextCapture.Empty,
                BoundedTextCapture.Empty);
        }

        using var process = new Process();
        try
        {
            process.StartInfo = command.CreateStartInfo();
            if (!process.Start())
            {
                return CreateStartFailure(startedAtUtc);
            }
        }
        catch (Exception exception) when (IsKnownStartFailure(exception))
        {
            return CreateStartFailure(startedAtUtc);
        }

        int? processId = null;
        Task<BoundedTextCapture>? standardOutputTask = null;
        Task<BoundedTextCapture>? standardErrorTask = null;
        Task? exitTask = null;
        Task? observerTask = null;
        CancellationTokenSource? observerLifetime = null;
        TerminalCause? terminalCause = null;
        TerminalCause cause = TerminalCause.ExitFailure;
        bool observerFailed = false;
        CaptureCompletion standardOutput = new(BoundedTextCapture.Empty, Failed: false);
        CaptureCompletion standardError = new(BoundedTextCapture.Empty, Failed: false);
        Task timeoutTask = Task.Delay(effectiveTimeout, CancellationToken.None);
        Task callerCancellationTask = cancellationToken.CanBeCanceled
            ? Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
            : NeverCompletes;

        try
        {
            try
            {
                processId = process.Id;
            }
            catch
            {
                terminalCause = TerminalCause.ExitFailure;
            }

            if (terminalCause is null)
            {
                standardOutputTask = TryStartCapture(
                    () => process.StandardOutput.BaseStream,
                    out bool captureFailed);
                if (captureFailed)
                {
                    terminalCause = TerminalCause.CaptureFailure;
                }
            }

            if (terminalCause is null)
            {
                standardErrorTask = TryStartCapture(
                    () => process.StandardError.BaseStream,
                    out bool captureFailed);
                if (captureFailed)
                {
                    terminalCause = TerminalCause.CaptureFailure;
                }
            }

            if (terminalCause is null)
            {
                try
                {
                    observerLifetime = new CancellationTokenSource();
                    observerTask = StartObserver(
                        whileRunningObserver,
                        processId!.Value,
                        observerLifetime.Token);
                }
                catch
                {
                    terminalCause = TerminalCause.ObserverFailure;
                }
            }

            if (terminalCause is null)
            {
                exitTask = TryStartExitWait(process, out bool exitWaitFailed);
                if (exitWaitFailed)
                {
                    terminalCause = TerminalCause.ExitFailure;
                }
            }

            if (terminalCause is null)
            {
                terminalCause = await WaitForTerminalCauseAsync(
                        exitTask!,
                        standardOutputTask!,
                        standardErrorTask!,
                        observerTask,
                        timeoutTask,
                        callerCancellationTask)
                    .ConfigureAwait(false);
            }
            else if (callerCancellationTask.IsCompleted)
            {
                // Caller cancellation has explicit precedence while setup is
                // still choosing the one terminal cause for this execution.
                terminalCause = TerminalCause.CallerCancellation;
            }

            cause = terminalCause.Value;
        }
        catch
        {
            cause = terminalCause ?? TerminalCause.ExitFailure;
            observerFailed = true;
        }
        finally
        {
            try
            {
                if (cause == TerminalCause.ProcessExited && !HasExited(process))
                {
                    cause = TerminalCause.ExitFailure;
                }

                observerFailed |= IsInfrastructureFailure(cause);
                if (!TryCancelObserverLifetime(observerLifetime))
                {
                    observerFailed = true;
                }

                if (cause != TerminalCause.ProcessExited && !TryKillEntireProcessTree(process))
                {
                    observerFailed = true;
                }

                if (await AwaitProcessExitAsync(process, exitTask).ConfigureAwait(false))
                {
                    observerFailed = true;
                }

                standardOutput = await AwaitCaptureAsync(standardOutputTask).ConfigureAwait(false);
                standardError = await AwaitCaptureAsync(standardErrorTask).ConfigureAwait(false);
                if (standardOutput.Failed || standardError.Failed)
                {
                    // The exact public result has no CaptureFailed member.
                    // Stream and wait infrastructure failures therefore map
                    // to ObserverFailed so Succeeded remains false.
                    observerFailed = true;
                }

                if (await AwaitObserverAsync(observerTask, observerLifetime).ConfigureAwait(false))
                {
                    observerFailed = true;
                }
            }
            finally
            {
                observerLifetime?.Dispose();
            }
        }

        return CreateResult(
            startedAtUtc,
            processId,
            TryGetExitCode(process),
            processStartFailed: false,
            observerFailed,
            timedOut: cause == TerminalCause.Timeout,
            cancelled: cause == TerminalCause.CallerCancellation,
            standardOutput.Capture,
            standardError.Capture);
    }

    private static Task? StartObserver(
        Func<int, CancellationToken, Task>? observer,
        int processId,
        CancellationToken observerLifetime)
    {
        if (observer is null)
        {
            return null;
        }

        try
        {
            return observer(processId, observerLifetime) ??
                Task.FromException(new InvalidOperationException());
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private Task<BoundedTextCapture>? TryStartCapture(
        Func<Stream> streamFactory,
        out bool failed)
    {
        try
        {
            Task<BoundedTextCapture>? task = _captureReader(
                streamFactory(),
                MaximumCapturedBytesPerStream);
            failed = task is null;
            return task;
        }
        catch
        {
            failed = true;
            return null;
        }
    }

    private Task? TryStartExitWait(Process process, out bool failed)
    {
        try
        {
            Task? task = _waitForExit(process);
            failed = task is null;
            return task;
        }
        catch
        {
            failed = true;
            return null;
        }
    }

    private static async Task<TerminalCause> WaitForTerminalCauseAsync(
        Task exitTask,
        Task<BoundedTextCapture> standardOutputTask,
        Task<BoundedTextCapture> standardErrorTask,
        Task? observerTask,
        Task timeoutTask,
        Task callerCancellationTask)
    {
        Task observerFailureTask = observerTask is null
            ? NeverCompletes
            : WaitForFailureAsync(observerTask);
        Task captureFailureTask = Task.WhenAny(
            WaitForFailureAsync(standardOutputTask),
            WaitForFailureAsync(standardErrorTask));

        // The order gives deterministic precedence when multiple terminal
        // signals are already complete: caller, observer, capture, exit, then
        // timeout. Once WhenAny selects a signal, its cause is never relabeled.
        Task completedTask = await Task.WhenAny(
                callerCancellationTask,
                observerFailureTask,
                captureFailureTask,
                exitTask,
                timeoutTask)
            .ConfigureAwait(false);

        if (ReferenceEquals(completedTask, callerCancellationTask))
        {
            return TerminalCause.CallerCancellation;
        }

        if (ReferenceEquals(completedTask, observerFailureTask))
        {
            return TerminalCause.ObserverFailure;
        }

        if (ReferenceEquals(completedTask, captureFailureTask))
        {
            return TerminalCause.CaptureFailure;
        }

        if (ReferenceEquals(completedTask, exitTask))
        {
            return exitTask.IsCompletedSuccessfully
                ? TerminalCause.ProcessExited
                : TerminalCause.ExitFailure;
        }

        return TerminalCause.Timeout;
    }

    private static async Task WaitForFailureAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            return;
        }

        await NeverCompletes.ConfigureAwait(false);
    }

    private static bool TryCancelObserverLifetime(CancellationTokenSource? observerLifetime)
    {
        if (observerLifetime is null)
        {
            return true;
        }

        try
        {
            observerLifetime.Cancel(throwOnFirstException: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryKillEntireProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return true;
        }
        catch (InvalidOperationException) when (HasExited(process))
        {
            // The process exited between the state check and tree termination.
            return true;
        }
        catch (InvalidOperationException)
        {
            // A live-process InvalidOperationException is not an exited race
            // and is retained as a stable infrastructure failure.
            return false;
        }
        catch (Win32Exception)
        {
            return HasExited(process);
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> AwaitProcessExitAsync(Process process, Task? exitTask)
    {
        bool failed = false;
        if (exitTask is not null)
        {
            try
            {
                await exitTask.ConfigureAwait(false);
            }
            catch
            {
                failed = true;
            }
        }

        if (exitTask is null || !exitTask.IsCompletedSuccessfully || !HasExited(process))
        {
            try
            {
                await WaitForExitAsync(process).ConfigureAwait(false);
            }
            catch
            {
                failed = true;
            }
        }

        return failed || !HasExited(process);
    }

    private static async Task<CaptureCompletion> AwaitCaptureAsync(
        Task<BoundedTextCapture>? captureTask)
    {
        if (captureTask is null)
        {
            return new CaptureCompletion(BoundedTextCapture.Empty, Failed: false);
        }

        try
        {
            BoundedTextCapture? capture = await captureTask.ConfigureAwait(false);
            return capture is null
                ? new CaptureCompletion(BoundedTextCapture.Empty, Failed: true)
                : new CaptureCompletion(capture, Failed: false);
        }
        catch
        {
            return new CaptureCompletion(BoundedTextCapture.Empty, Failed: true);
        }
    }

    private static async Task<bool> AwaitObserverAsync(
        Task? observerTask,
        CancellationTokenSource? observerLifetime)
    {
        if (observerTask is null)
        {
            return false;
        }

        try
        {
            await observerTask.ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException) when (observerLifetime?.IsCancellationRequested == true)
        {
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsInfrastructureFailure(TerminalCause cause)
    {
        return cause is
            TerminalCause.ObserverFailure or
            TerminalCause.CaptureFailure or
            TerminalCause.ExitFailure;
    }

    private static Task WaitForExitAsync(Process process)
    {
        return process.WaitForExitAsync(CancellationToken.None);
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            Win32Exception or
            NotSupportedException)
        {
            return false;
        }
    }

    private static int? TryGetExitCode(Process process)
    {
        try
        {
            return process.HasExited ? process.ExitCode : null;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            Win32Exception or
            NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsKnownStartFailure(Exception exception)
    {
        return exception is Win32Exception or
            InvalidOperationException or
            FileNotFoundException or
            DirectoryNotFoundException or
            UnauthorizedAccessException;
    }

    private static LlmFitProcessResult CreateStartFailure(DateTimeOffset startedAtUtc)
    {
        return CreateResult(
            startedAtUtc,
            processId: null,
            exitCode: null,
            processStartFailed: true,
            observerFailed: false,
            timedOut: false,
            cancelled: false,
            BoundedTextCapture.Empty,
            BoundedTextCapture.Empty);
    }

    private static LlmFitProcessResult CreateResult(
        DateTimeOffset startedAtUtc,
        int? processId,
        int? exitCode,
        bool processStartFailed,
        bool observerFailed,
        bool timedOut,
        bool cancelled,
        BoundedTextCapture standardOutput,
        BoundedTextCapture standardError)
    {
        DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
        if (completedAtUtc < startedAtUtc)
        {
            completedAtUtc = startedAtUtc;
        }

        return new LlmFitProcessResult(
            startedAtUtc,
            completedAtUtc,
            processId,
            exitCode,
            processStartFailed,
            observerFailed,
            timedOut,
            cancelled,
            standardOutput.Text,
            standardError.Text,
            standardOutput.Truncated,
            standardError.Truncated);
    }

    private void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout < _minimumTimeout || timeout > MaximumTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
    }

    private readonly record struct CaptureCompletion(BoundedTextCapture Capture, bool Failed);

    private enum TerminalCause
    {
        ProcessExited,
        CallerCancellation,
        Timeout,
        ObserverFailure,
        CaptureFailure,
        ExitFailure,
    }
}
