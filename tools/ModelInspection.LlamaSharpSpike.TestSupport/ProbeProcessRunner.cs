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

        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(request.Timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            await WaitAfterKillAsync(process);
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
            KillProcessTree(process);
            await WaitAfterKillAsync(process);
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
