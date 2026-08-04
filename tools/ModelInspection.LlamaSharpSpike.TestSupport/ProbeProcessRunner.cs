using System.Diagnostics;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Runs feasibility executables outside the MSTest host so native aborts,
/// corrupt DLLs and malformed model inputs cannot terminate the test runner.
/// </summary>
public sealed class ProbeProcessRunner
{
    /// <summary>
    /// Executes one child process with redirected streams and a bounded timeout.
    /// A timeout kills the complete child process tree and returns a result.
    /// Caller cancellation kills the tree and then propagates cancellation.
    /// An optional observer is cancelled and awaited on every termination path.
    /// </summary>
    public async Task<ProbeExecutionResult> RunAsync(
        ProbeProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);
        ArgumentNullException.ThrowIfNull(request.Arguments);
        ArgumentNullException.ThrowIfNull(request.EnvironmentVariables);

        if (request.Timeout <= TimeSpan.Zero ||
            request.Timeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Process timeout must be a positive, finite duration.");
        }

        if (!Directory.Exists(request.WorkingDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Process working directory does not exist: {request.WorkingDirectory}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach ((string key, string? value) in request.EnvironmentVariables)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(key);
            }
            else
            {
                startInfo.Environment[key] = value;
            }
        }

        var stopwatch = Stopwatch.StartNew();
        using var process = new Process
        {
            StartInfo = startInfo
        };

        try
        {
            if (!process.Start())
            {
                stopwatch.Stop();
                return StartFailed(
                    stopwatch.Elapsed,
                    "Process.Start returned false.");
            }
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or
                  System.ComponentModel.Win32Exception or
                  FileNotFoundException)
        {
            stopwatch.Stop();
            return StartFailed(stopwatch.Elapsed, exception.Message);
        }

        int processId = process.Id;
        Task<string> standardOutputTask =
            process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask =
            process.StandardError.ReadToEndAsync();

        using var observerSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        Task observerTask;

        try
        {
            observerTask = request.WhileRunningObserver?.Invoke(
                processId,
                observerSource.Token) ?? Task.CompletedTask;
        }
        catch
        {
            KillProcessTree(process);
            await WaitAfterKillAsync(process);
            throw;
        }

        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(request.Timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            observerSource.Cancel();
            await AwaitObserverAsync(observerTask, observerSource.Token);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            observerSource.Cancel();
            KillProcessTree(process);
            await WaitAfterKillAsync(process);
            await AwaitObserverAsync(observerTask, observerSource.Token);
            stopwatch.Stop();

            return new ProbeExecutionResult
            {
                TerminationKind = ProcessTerminationKind.TimedOut,
                ExitCode = process.HasExited ? process.ExitCode : null,
                StandardOutput = await standardOutputTask,
                StandardError = await standardErrorTask,
                Duration = stopwatch.Elapsed,
                ProcessId = processId
            };
        }
        catch (OperationCanceledException)
        {
            observerSource.Cancel();
            KillProcessTree(process);
            await WaitAfterKillAsync(process);
            await AwaitObserverAsync(observerTask, observerSource.Token);
            throw;
        }
        catch
        {
            observerSource.Cancel();
            KillProcessTree(process);
            await WaitAfterKillAsync(process);
            await AwaitObserverAsync(observerTask, observerSource.Token);
            throw;
        }

        stopwatch.Stop();

        return new ProbeExecutionResult
        {
            TerminationKind = ProcessTerminationKind.Exited,
            ExitCode = process.ExitCode,
            StandardOutput = await standardOutputTask,
            StandardError = await standardErrorTask,
            Duration = stopwatch.Elapsed,
            ProcessId = processId
        };
    }

    private static ProbeExecutionResult StartFailed(
        TimeSpan duration,
        string error)
    {
        return new ProbeExecutionResult
        {
            TerminationKind = ProcessTerminationKind.StartFailed,
            ExitCode = null,
            StandardOutput = string.Empty,
            StandardError = error,
            Duration = duration,
            ProcessId = 0
        };
    }

    private static async Task AwaitObserverAsync(
        Task observerTask,
        CancellationToken observerToken)
    {
        try
        {
            await observerTask;
        }
        catch (OperationCanceledException)
            when (observerToken.IsCancellationRequested)
        {
            // Normal observer shutdown after the child has exited, timed out,
            // or caller cancellation has begun.
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and kill request.
        }
    }

    private static async Task WaitAfterKillAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // The process already completed or was never associated with a
            // running operating-system process by the time cleanup ran.
        }
    }
}
