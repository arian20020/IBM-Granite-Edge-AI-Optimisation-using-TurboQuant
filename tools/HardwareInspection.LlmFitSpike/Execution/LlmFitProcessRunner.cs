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
    private static readonly TimeSpan DefaultCleanupDeadline = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaximumTimeout = TimeSpan.FromSeconds(120);
    private static int _activeMonitorResourceCount;
    private readonly Func<Stream, int, Task<BoundedTextCapture>> _captureReader;
    private readonly TimeSpan _cleanupDeadline;
    private readonly Func<Process, Task> _waitForExit;
    private readonly TimeSpan _minimumTimeout;

    public LlmFitProcessRunner()
        : this(
            TimeSpan.FromSeconds(1),
            DefaultCleanupDeadline,
            BoundedTextReader.ReadAsync,
            WaitForExitAsync)
    {
    }

    internal LlmFitProcessRunner(TimeSpan minimumTimeout)
        : this(minimumTimeout, DefaultCleanupDeadline, BoundedTextReader.ReadAsync, WaitForExitAsync)
    {
    }

    internal LlmFitProcessRunner(TimeSpan minimumTimeout, TimeSpan cleanupDeadline)
        : this(minimumTimeout, cleanupDeadline, BoundedTextReader.ReadAsync, WaitForExitAsync)
    {
    }

    internal LlmFitProcessRunner(
        TimeSpan minimumTimeout,
        Func<Stream, int, Task<BoundedTextCapture>> captureReader)
        : this(minimumTimeout, DefaultCleanupDeadline, captureReader, WaitForExitAsync)
    {
    }

    internal LlmFitProcessRunner(
        TimeSpan minimumTimeout,
        Func<Stream, int, Task<BoundedTextCapture>> captureReader,
        Func<Process, Task> waitForExit)
        : this(minimumTimeout, DefaultCleanupDeadline, captureReader, waitForExit)
    {
    }

    internal LlmFitProcessRunner(
        TimeSpan minimumTimeout,
        TimeSpan cleanupDeadline,
        Func<Stream, int, Task<BoundedTextCapture>> captureReader,
        Func<Process, Task> waitForExit)
    {
        if (minimumTimeout <= TimeSpan.Zero || minimumTimeout > TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumTimeout));
        }

        if (cleanupDeadline <= TimeSpan.Zero || cleanupDeadline > DefaultCleanupDeadline)
        {
            throw new ArgumentOutOfRangeException(nameof(cleanupDeadline));
        }

        _minimumTimeout = minimumTimeout;
        _cleanupDeadline = cleanupDeadline;
        _captureReader = captureReader ?? throw new ArgumentNullException(nameof(captureReader));
        _waitForExit = waitForExit ?? throw new ArgumentNullException(nameof(waitForExit));
    }

    internal static int ActiveMonitorResourceCount => Volatile.Read(ref _activeMonitorResourceCount);

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
        Task? observerCancellationTask = null;
        Task? realExitTask = null;
        Task? timeoutTask = null;
        CancellationTokenSource? observerLifetime = null;
        CancellationTokenSource? timeoutLifetime = null;
        CancellationTokenRegistration callerRegistration = default;
        Task? callerCancellationTask = null;
        bool callerRegistrationCreated = false;
        bool callerRegistrationDisposed = false;
        bool monitorResourcesCounted = false;
        TerminalCause? terminalCause = null;
        TerminalCause cause = TerminalCause.ExitFailure;
        bool observerFailed = false;
        CaptureCompletion standardOutput = new(BoundedTextCapture.Empty, Failed: false);
        CaptureCompletion standardError = new(BoundedTextCapture.Empty, Failed: false);

        try
        {
            timeoutLifetime = new CancellationTokenSource();
            timeoutTask = Task.Delay(effectiveTimeout, timeoutLifetime.Token);
            if (cancellationToken.CanBeCanceled)
            {
                var callerSignal = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                callerRegistration = cancellationToken.Register(
                    static state => ((TaskCompletionSource)state!).TrySetResult(),
                    callerSignal);
                callerRegistrationCreated = true;
                callerCancellationTask = callerSignal.Task;
            }

            Interlocked.Increment(ref _activeMonitorResourceCount);
            monitorResourcesCounted = true;

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
            else if (callerCancellationTask?.IsCompleted == true)
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
                if (cause != TerminalCause.ProcessExited && !TryKillEntireProcessTree(process))
                {
                    observerFailed = true;
                }

                observerCancellationTask = TryStartObserverCancellation(
                    observerLifetime,
                    out bool observerCancellationFailed);
                if (observerCancellationFailed)
                {
                    observerFailed = true;
                }

                if (!TryCancelSource(timeoutLifetime))
                {
                    observerFailed = true;
                }

                if (callerRegistrationCreated)
                {
                    try
                    {
                        callerRegistration.Dispose();
                        callerRegistrationDisposed = true;
                    }
                    catch
                    {
                        observerFailed = true;
                    }
                }

                realExitTask = TryStartRealExitWait(process, out bool realExitWaitFailed);
                CleanupCompletion cleanup = await AwaitBoundedCleanupAsync(
                        process,
                        exitTask,
                        realExitTask,
                        standardOutputTask,
                        standardErrorTask,
                        observerTask,
                        observerCancellationTask,
                        timeoutTask,
                        observerLifetime,
                        _cleanupDeadline)
                    .ConfigureAwait(false);
                standardOutput = cleanup.StandardOutput;
                standardError = cleanup.StandardError;
                observerFailed |= realExitWaitFailed || cleanup.Failed;
            }
            finally
            {
                if (callerRegistrationCreated && !callerRegistrationDisposed)
                {
                    TryDisposeCallerRegistration(callerRegistration);
                }

                ReleaseMonitorResources(timeoutLifetime, timeoutTask, monitorResourcesCounted);
                DisposeObserverLifetimeWhenSafe(observerLifetime, observerCancellationTask);
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
        Task? callerCancellationTask)
    {
        while (true)
        {
            TerminalCause? completedCause = SelectCompletedTerminalCause(
                exitTask,
                standardOutputTask,
                standardErrorTask,
                observerTask,
                timeoutTask,
                callerCancellationTask);
            if (completedCause is not null)
            {
                return completedCause.Value;
            }

            var pendingTasks = new List<Task> { exitTask, timeoutTask };
            if (callerCancellationTask is not null)
            {
                pendingTasks.Add(callerCancellationTask);
            }

            if (!standardOutputTask.IsCompleted)
            {
                pendingTasks.Add(standardOutputTask);
            }

            if (!standardErrorTask.IsCompleted)
            {
                pendingTasks.Add(standardErrorTask);
            }

            if (observerTask is { IsCompleted: false })
            {
                pendingTasks.Add(observerTask);
            }

            _ = await Task.WhenAny(pendingTasks).ConfigureAwait(false);
        }
    }

    private static TerminalCause? SelectCompletedTerminalCause(
        Task exitTask,
        Task standardOutputTask,
        Task standardErrorTask,
        Task? observerTask,
        Task timeoutTask,
        Task? callerCancellationTask)
    {
        // Fixed tie precedence is caller, observer failure, capture failure,
        // exit, then timeout. Successful observer/capture tasks are removed
        // from subsequent arbitration without allocating monitor continuations.
        if (callerCancellationTask?.IsCompleted == true)
        {
            return TerminalCause.CallerCancellation;
        }

        if (observerTask is { IsCompleted: true, IsCompletedSuccessfully: false })
        {
            return TerminalCause.ObserverFailure;
        }

        if (standardOutputTask is { IsCompleted: true, IsCompletedSuccessfully: false } ||
            standardErrorTask is { IsCompleted: true, IsCompletedSuccessfully: false })
        {
            return TerminalCause.CaptureFailure;
        }

        if (exitTask.IsCompleted)
        {
            return exitTask.IsCompletedSuccessfully
                ? TerminalCause.ProcessExited
                : TerminalCause.ExitFailure;
        }

        if (timeoutTask.IsCompleted)
        {
            return timeoutTask.IsCompletedSuccessfully
                ? TerminalCause.Timeout
                : TerminalCause.ExitFailure;
        }

        return null;
    }

    private static Task TryStartObserverCancellation(
        CancellationTokenSource? observerLifetime,
        out bool failed)
    {
        if (observerLifetime is null)
        {
            failed = false;
            return Task.CompletedTask;
        }

        try
        {
            Task? cancellationTask = observerLifetime.CancelAsync();
            failed = cancellationTask is null;
            return cancellationTask ?? Task.CompletedTask;
        }
        catch
        {
            failed = true;
            return Task.CompletedTask;
        }
    }

    private static bool TryCancelSource(CancellationTokenSource? source)
    {
        if (source is null)
        {
            return true;
        }

        try
        {
            source.Cancel(throwOnFirstException: false);
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

    private static Task? TryStartRealExitWait(Process process, out bool failed)
    {
        try
        {
            Task? task = WaitForExitAsync(process);
            failed = task is null;
            return task;
        }
        catch
        {
            failed = true;
            return null;
        }
    }

    private static async Task<CleanupCompletion> AwaitBoundedCleanupAsync(
        Process process,
        Task? exitTask,
        Task? realExitTask,
        Task<BoundedTextCapture>? standardOutputTask,
        Task<BoundedTextCapture>? standardErrorTask,
        Task? observerTask,
        Task? observerCancellationTask,
        Task? timeoutTask,
        CancellationTokenSource? observerLifetime,
        TimeSpan cleanupDeadline)
    {
        var cleanupTasks = new List<Task>();
        AddCleanupTask(cleanupTasks, exitTask);
        AddCleanupTask(cleanupTasks, realExitTask);
        AddCleanupTask(cleanupTasks, standardOutputTask);
        AddCleanupTask(cleanupTasks, standardErrorTask);
        AddCleanupTask(cleanupTasks, observerTask);
        AddCleanupTask(cleanupTasks, observerCancellationTask);
        AddCleanupTask(cleanupTasks, timeoutTask);

        Task allCleanup = Task.WhenAll(cleanupTasks);
        ObserveFault(allCleanup);
        using var deadlineLifetime = new CancellationTokenSource();
        Task deadlineTask = Task.Delay(cleanupDeadline, deadlineLifetime.Token);
        Task completedTask = await Task.WhenAny(allCleanup, deadlineTask).ConfigureAwait(false);
        bool completedWithinDeadline = ReferenceEquals(completedTask, allCleanup);
        if (completedWithinDeadline)
        {
            _ = TryCancelSource(deadlineLifetime);
            try
            {
                await allCleanup.ConfigureAwait(false);
            }
            catch
            {
                // Individual completion states below map failures without
                // exposing callback, path, or operating-system text.
            }
        }

        ObserveFault(deadlineTask);
        CaptureCompletion standardOutput = GetCaptureCompletion(standardOutputTask);
        CaptureCompletion standardError = GetCaptureCompletion(standardErrorTask);
        bool failed = !completedWithinDeadline ||
            !HasExited(process) ||
            TaskFailedOrIncomplete(exitTask) ||
            realExitTask is null ||
            !realExitTask.IsCompletedSuccessfully ||
            standardOutput.Failed ||
            standardError.Failed ||
            ObserverFailedOrIncomplete(observerTask, observerLifetime) ||
            observerCancellationTask is null ||
            !observerCancellationTask.IsCompletedSuccessfully ||
            timeoutTask is { IsCompleted: false };

        return new CleanupCompletion(standardOutput, standardError, failed);
    }

    private static void AddCleanupTask(List<Task> cleanupTasks, Task? task)
    {
        if (task is not null)
        {
            ObserveFault(task);
            cleanupTasks.Add(task);
        }
    }

    private static CaptureCompletion GetCaptureCompletion(
        Task<BoundedTextCapture>? captureTask)
    {
        if (captureTask is null)
        {
            return new CaptureCompletion(BoundedTextCapture.Empty, Failed: false);
        }

        if (!captureTask.IsCompletedSuccessfully)
        {
            return new CaptureCompletion(BoundedTextCapture.Empty, Failed: true);
        }

        try
        {
            BoundedTextCapture? capture = captureTask.Result;
            return capture is null
                ? new CaptureCompletion(BoundedTextCapture.Empty, Failed: true)
                : new CaptureCompletion(capture, Failed: false);
        }
        catch
        {
            return new CaptureCompletion(BoundedTextCapture.Empty, Failed: true);
        }
    }

    private static bool ObserverFailedOrIncomplete(
        Task? observerTask,
        CancellationTokenSource? observerLifetime)
    {
        if (observerTask is null || observerTask.IsCompletedSuccessfully)
        {
            return false;
        }

        return !observerTask.IsCanceled || observerLifetime?.IsCancellationRequested != true;
    }

    private static bool TaskFailedOrIncomplete(Task? task)
    {
        return task is not null && !task.IsCompletedSuccessfully;
    }

    private static void ObserveFault(Task task)
    {
        if (task.IsFaulted)
        {
            _ = task.Exception;
            return;
        }

        if (!task.IsCompleted)
        {
            _ = task.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private static void TryDisposeCallerRegistration(CancellationTokenRegistration registration)
    {
        try
        {
            registration.Dispose();
        }
        catch
        {
            // Cleanup failure is already mapped by the owning execution.
        }
    }

    private static void ReleaseMonitorResources(
        CancellationTokenSource? timeoutLifetime,
        Task? timeoutTask,
        bool monitorResourcesCounted)
    {
        _ = TryCancelSource(timeoutLifetime);
        if (timeoutTask is not null)
        {
            ObserveFault(timeoutTask);
        }

        if (timeoutTask is { IsCompleted: false })
        {
            _ = timeoutTask.ContinueWith(
                _ =>
                {
                    SafeDispose(timeoutLifetime);
                    if (monitorResourcesCounted)
                    {
                        Interlocked.Decrement(ref _activeMonitorResourceCount);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return;
        }

        SafeDispose(timeoutLifetime);
        if (monitorResourcesCounted)
        {
            Interlocked.Decrement(ref _activeMonitorResourceCount);
        }
    }

    private static void DisposeObserverLifetimeWhenSafe(
        CancellationTokenSource? observerLifetime,
        Task? observerCancellationTask)
    {
        if (observerLifetime is null)
        {
            return;
        }

        if (observerCancellationTask is { IsCompleted: false })
        {
            ObserveFault(observerCancellationTask);
            _ = observerCancellationTask.ContinueWith(
                _ => SafeDispose(observerLifetime),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return;
        }

        SafeDispose(observerLifetime);
    }

    private static void SafeDispose(CancellationTokenSource? disposable)
    {
        try
        {
            disposable?.Dispose();
        }
        catch
        {
            // Disposal cannot bypass the stable result contract.
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

    private readonly record struct CleanupCompletion(
        CaptureCompletion StandardOutput,
        CaptureCompletion StandardError,
        bool Failed);

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
