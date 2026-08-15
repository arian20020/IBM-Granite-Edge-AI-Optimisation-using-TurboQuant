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
    private readonly TimeSpan _minimumTimeout;

    public LlmFitProcessRunner()
        : this(TimeSpan.FromSeconds(1), BoundedTextReader.ReadAsync)
    {
    }

    internal LlmFitProcessRunner(TimeSpan minimumTimeout)
        : this(minimumTimeout, BoundedTextReader.ReadAsync)
    {
    }

    internal LlmFitProcessRunner(
        TimeSpan minimumTimeout,
        Func<Stream, int, Task<BoundedTextCapture>> captureReader)
    {
        if (minimumTimeout <= TimeSpan.Zero || minimumTimeout > TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumTimeout));
        }

        _minimumTimeout = minimumTimeout;
        _captureReader = captureReader ?? throw new ArgumentNullException(nameof(captureReader));
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

        int processId = process.Id;
        Task<BoundedTextCapture> standardOutputTask = _captureReader(
            process.StandardOutput.BaseStream,
            MaximumCapturedBytesPerStream);
        Task<BoundedTextCapture> standardErrorTask = _captureReader(
            process.StandardError.BaseStream,
            MaximumCapturedBytesPerStream);
        Task exitTask = process.WaitForExitAsync(CancellationToken.None);
        using CancellationTokenSource observerLifetime =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task? observerTask = StartObserver(whileRunningObserver, processId, observerLifetime.Token);

        Task timeoutTask = Task.Delay(effectiveTimeout, CancellationToken.None);
        Task callerCancellationTask = cancellationToken.CanBeCanceled
            ? Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
            : NeverCompletes;
        Task observerFailureTask = observerTask is null
            ? NeverCompletes
            : WaitForFailureAsync(observerTask);
        Task captureFailureTask = Task.WhenAny(
            WaitForFailureAsync(standardOutputTask),
            WaitForFailureAsync(standardErrorTask));

        Task completedTask = await Task.WhenAny(
                exitTask,
                timeoutTask,
                callerCancellationTask,
                observerFailureTask,
                captureFailureTask)
            .ConfigureAwait(false);

        bool cancelled = cancellationToken.IsCancellationRequested;
        bool observerFailed = !cancelled &&
            observerTask is { IsCompleted: true, IsCompletedSuccessfully: false };
        bool captureFailed = !cancelled &&
            (standardOutputTask is { IsCompleted: true, IsCompletedSuccessfully: false } ||
             standardErrorTask is { IsCompleted: true, IsCompletedSuccessfully: false });
        bool exitFailed = !cancelled &&
            exitTask is { IsCompleted: true, IsCompletedSuccessfully: false };
        bool timedOut = !cancelled &&
            !observerFailed &&
            !captureFailed &&
            !exitFailed &&
            ReferenceEquals(completedTask, timeoutTask);
        bool terminateProcessTree = cancelled || observerFailed || captureFailed || exitFailed || timedOut;

        observerLifetime.Cancel();
        if (terminateProcessTree)
        {
            KillEntireProcessTree(process);
        }

        try
        {
            await exitTask.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            observerFailed = true;
        }

        BoundedTextCapture standardOutput = await AwaitCaptureAsync(standardOutputTask).ConfigureAwait(false);
        BoundedTextCapture standardError = await AwaitCaptureAsync(standardErrorTask).ConfigureAwait(false);
        if (standardOutputTask.IsFaulted || standardOutputTask.IsCanceled ||
            standardErrorTask.IsFaulted || standardErrorTask.IsCanceled)
        {
            // The public result has no CaptureFailed member. A stream-pump or
            // process-wait infrastructure failure is therefore represented by
            // ObserverFailed so Succeeded can never claim complete evidence.
            observerFailed = true;
        }

        if (observerTask is not null)
        {
            try
            {
                await observerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (observerLifetime.IsCancellationRequested)
            {
                // Expected when the process lifetime ends.
            }
            catch
            {
                observerFailed = true;
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
            timedOut = false;
        }

        int? exitCode = TryGetExitCode(process);
        return CreateResult(
            startedAtUtc,
            processId,
            exitCode,
            processStartFailed: false,
            observerFailed,
            timedOut,
            cancelled,
            standardOutput,
            standardError);
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

    private static async Task<BoundedTextCapture> AwaitCaptureAsync(Task<BoundedTextCapture> captureTask)
    {
        try
        {
            return await captureTask.ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or
            InvalidOperationException or
            ObjectDisposedException)
        {
            return BoundedTextCapture.Empty;
        }
    }

    private static void KillEntireProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException) when (HasExited(process))
        {
            // The process exited between the state check and tree termination.
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            throw new InvalidOperationException("The process tree could not be terminated safely.");
        }
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
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
        catch (InvalidOperationException)
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
}
