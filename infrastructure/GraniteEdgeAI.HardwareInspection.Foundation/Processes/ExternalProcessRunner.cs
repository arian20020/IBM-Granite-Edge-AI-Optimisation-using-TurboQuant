using System.Diagnostics;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Processes;

public sealed class ExternalProcessRunner : IExternalProcessRunner
{
    private static readonly TimeSpan CleanupDeadline = TimeSpan.FromSeconds(5);

    public async Task<ExternalProcessResult> RunAsync(
        VerifiedTrustedTool tool,
        ExternalProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(request);
        Stopwatch elapsed = Stopwatch.StartNew();
        if (cancellationToken.IsCancellationRequested)
        {
            return CreateResult(ExternalProcessTerminationReason.Cancelled, elapsed);
        }

        if (!tool.Commands.TryGetValue(request.CommandIdentity, out TrustedToolCommand? command))
        {
            return CreateResult(ExternalProcessTerminationReason.StartFailed, elapsed);
        }

        if (!tool.TryAcquireExecutionCustody(out IDisposable? executionCustody))
        {
            return CreateResult(ExternalProcessTerminationReason.StartFailed, elapsed);
        }

        using (executionCustody)
        {
            if (!WindowsKillOnCloseJob.TryCreate(out WindowsKillOnCloseJob job))
            {
                return CreateResult(ExternalProcessTerminationReason.StartFailed, elapsed);
            }

            using (job)
            {
                return await RunInJobAsync(
                        tool,
                        command,
                        request,
                        elapsed,
                        job,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static async Task<ExternalProcessResult> RunInJobAsync(
        VerifiedTrustedTool tool,
        TrustedToolCommand command,
        ExternalProcessRequest request,
        Stopwatch elapsed,
        WindowsKillOnCloseJob job,
        CancellationToken cancellationToken)
    {

        if (!WindowsSuspendedProcess.TryStart(
                tool,
                command,
                job,
                out WindowsSuspendedProcess? launched))
        {
            return CreateResult(ExternalProcessTerminationReason.StartFailed, elapsed);
        }

        WindowsSuspendedProcess running = launched!;
        using (running)
        {
            return await MonitorAsync(
                    running,
                    running.StandardOutput,
                    running.StandardError,
                    request,
                    elapsed,
                    job,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task<ExternalProcessResult> MonitorAsync(
        WindowsSuspendedProcess running,
        Stream standardOutputStream,
        Stream standardErrorStream,
        ExternalProcessRequest request,
        Stopwatch elapsed,
        WindowsKillOnCloseJob job,
        CancellationToken cancellationToken)
    {
        Process process = running.Process;
        BoundedProcessOutput standardOutput = BoundedProcessOutput.Start(
            standardOutputStream,
            request.StandardOutputByteLimit);
        BoundedProcessOutput standardError = BoundedProcessOutput.Start(
            standardErrorStream,
            request.StandardErrorByteLimit);
        Task exit = process.WaitForExitAsync(CancellationToken.None);
        using CancellationTokenSource timeoutLifetime = new();
        Task timeout = Task.Delay(request.Timeout, timeoutLifetime.Token);
        var cancellationSignal = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            cancellationSignal);
        Task cancellation = cancellationSignal.Task;

        Task terminal;
        try
        {
            terminal = await Task.WhenAny(
                    exit,
                    standardOutput.LimitExceeded,
                    standardError.LimitExceeded,
                    timeout,
                    cancellation)
                .ConfigureAwait(false);
        }
        finally
        {
            await timeoutLifetime.CancelAsync().ConfigureAwait(false);
        }

        ExternalProcessTerminationReason reason;
        if (terminal == cancellation)
        {
            reason = ExternalProcessTerminationReason.Cancelled;
        }
        else if (terminal == timeout)
        {
            reason = ExternalProcessTerminationReason.TimedOut;
        }
        else if (terminal == standardOutput.LimitExceeded ||
                 terminal == standardError.LimitExceeded)
        {
            reason = ExternalProcessTerminationReason.OutputLimitExceeded;
        }
        else
        {
            reason = ExternalProcessTerminationReason.Exited;
        }

        int? exitCode = null;
        bool exitCodeCaptured = reason != ExternalProcessTerminationReason.Exited;
        if (reason == ExternalProcessTerminationReason.Exited)
        {
            exitCodeCaptured = running.TryGetExitCode(out int capturedExitCode);
            if (exitCodeCaptured)
            {
                exitCode = capturedExitCode;
            }
        }

        if (reason != ExternalProcessTerminationReason.Exited &&
            !await KillAndWaitAsync(process, job).ConfigureAwait(false))
        {
            return CreateResult(ExternalProcessTerminationReason.CleanupFailed, elapsed);
        }

        else if (reason == ExternalProcessTerminationReason.Exited)
        {
            if (!job.TryTerminate() ||
                !await job.WaitForEmptyAsync(CleanupDeadline).ConfigureAwait(false))
            {
                return CreateResult(ExternalProcessTerminationReason.CleanupFailed, elapsed);
            }
        }

        BoundedProcessOutputResult[] captures;
        try
        {
            captures = await Task.WhenAll(standardOutput.Completion, standardError.Completion)
                .WaitAsync(CleanupDeadline, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is TimeoutException or IOException)
        {
            if (reason == ExternalProcessTerminationReason.Exited)
            {
                await KillAndWaitAsync(process, job).ConfigureAwait(false);
            }

            return CreateResult(ExternalProcessTerminationReason.CleanupFailed, elapsed);
        }

        if (captures.Any(capture => capture.Failed))
        {
            return new ExternalProcessResult(
                ExternalProcessTerminationReason.CleanupFailed,
                null,
                captures[0].Text,
                captures[1].Text,
                elapsed.Elapsed);
        }

        if (captures.Any(capture => capture.LimitExceeded))
        {
            reason = ExternalProcessTerminationReason.OutputLimitExceeded;
        }

        if (!exitCodeCaptured)
        {
            return new ExternalProcessResult(
                ExternalProcessTerminationReason.CleanupFailed,
                null,
                captures[0].Text,
                captures[1].Text,
                elapsed.Elapsed);
        }

        return new ExternalProcessResult(
            reason,
            ExitCodeForFinalReason(reason, exitCode),
            captures[0].Text,
            captures[1].Text,
            elapsed.Elapsed);
    }

    internal static int? ExitCodeForFinalReason(
        ExternalProcessTerminationReason finalReason,
        int? capturedExitCode) =>
        finalReason == ExternalProcessTerminationReason.Exited
            ? capturedExitCode
            : null;

    private static async Task<bool> KillAndWaitAsync(
        Process process,
        WindowsKillOnCloseJob job)
    {
        try
        {
            bool jobTerminated = job.TryTerminate();
            if (!jobTerminated && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None)
                .WaitAsync(CleanupDeadline)
                .ConfigureAwait(false);
            return await job.WaitForEmptyAsync(CleanupDeadline).ConfigureAwait(false);
        }
        catch (Exception error) when (
            error is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException)
        {
            return false;
        }
    }

    private static ExternalProcessResult CreateResult(
        ExternalProcessTerminationReason reason,
        Stopwatch elapsed) =>
        new(reason, null, string.Empty, string.Empty, elapsed.Elapsed);
}
