using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies bounded child-process observation without invoking LLamaSharp.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class ProbeProcessRunnerTests
{
    [TestMethod]
    public async Task RunAsync_WhenProcessExits_CapturesStreamsAndExitCode()
    {
        ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
            PowerShellRequest(
                "[Console]::Out.Write('out'); " +
                "[Console]::Error.Write('err'); exit 7",
                TimeSpan.FromSeconds(10)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, result.TerminationKind);
        Assert.AreEqual(7, result.ExitCode);
        Assert.AreEqual("out", result.StandardOutput);
        Assert.AreEqual("err", result.StandardError);
        Assert.IsTrue(result.ProcessId > 0);
        Assert.IsTrue(result.Duration >= TimeSpan.Zero);
    }

    [TestMethod]
    public async Task RunAsync_PreservesArgumentsWithSpacesAndUnicode()
    {
        const string expected = "value with spaces グラナイト";
        using var directory = new TemporaryDirectory(
            "process-argument-preservation");
        string outputPath = directory.Combine("received-argument.txt");
        string scriptPath = await TestFileBuilder.WriteTextAsync(
            directory,
            "write-argument.ps1",
            """
            param(
                [Parameter(Mandatory = $true, Position = 0)]
                [string]$Value,
                [Parameter(Mandatory = $true, Position = 1)]
                [string]$OutputPath
            )

            [IO.File]::WriteAllText(
                $OutputPath,
                $Value,
                [Text.UTF8Encoding]::new($false))
            """);

        ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
            new ProbeProcessRequest
            {
                ExecutablePath = "powershell.exe",
                Arguments = new[]
                {
                    "-NoLogo",
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    scriptPath,
                    expected,
                    outputPath
                },
                WorkingDirectory = directory.Path,
                Timeout = TimeSpan.FromSeconds(10)
            },
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, result.TerminationKind);
        Assert.AreEqual(
            0,
            result.ExitCode,
            $"PowerShell stderr: {result.StandardError}");
        Assert.IsTrue(
            File.Exists(outputPath),
            "The child process did not write its received argument.");
        Assert.AreEqual(
            expected,
            await File.ReadAllTextAsync(outputPath));
    }

    [TestMethod]
    public async Task RunAsync_PassesExplicitEnvironmentVariables()
    {
        const string expected = "environment-value";

        ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
            new ProbeProcessRequest
            {
                ExecutablePath = "powershell.exe",
                Arguments = new[]
                {
                    "-NoLogo",
                    "-NoProfile",
                    "-Command",
                    "[Console]::Out.Write($env:GRANITE_TEST_VALUE)"
                },
                WorkingDirectory = Environment.CurrentDirectory,
                Timeout = TimeSpan.FromSeconds(10),
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    ["GRANITE_TEST_VALUE"] = expected
                }
            },
            CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(expected, result.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_WithObserver_SuppliesPidCancelsAndAwaitsObserver()
    {
        var observerStarted = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var observerStopped = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        ProbeProcessRequest request = PowerShellRequest(
            "Start-Sleep -Milliseconds 500; exit 0",
            TimeSpan.FromSeconds(10)) with
        {
            WhileRunningObserver = async (processId, token) =>
            {
                observerStarted.TrySetResult(processId);

                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                finally
                {
                    observerStopped.TrySetResult();
                }
            }
        };

        ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
            request,
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, result.TerminationKind);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(result.ProcessId, await observerStarted.Task);
        await observerStopped.Task;
        Assert.IsTrue(observerStopped.Task.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task RunAsync_WhenObserverFails_PropagatesFailureAfterContainingChild()
    {
        ProbeProcessRequest request = PowerShellRequest(
            "Start-Sleep -Seconds 30",
            TimeSpan.FromSeconds(10)) with
        {
            WhileRunningObserver = (_, _) =>
                Task.FromException(
                    new InvalidOperationException("observer failed"))
        };

        InvalidOperationException exception =
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                async () => await new ProbeProcessRunner().RunAsync(
                    request,
                    CancellationToken.None));

        Assert.AreEqual("observer failed", exception.Message);
    }

    [TestMethod]
    public async Task RunAsync_WhenTimeoutExpires_KillsProcessAndReturnsTimedOut()
    {
        ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
            PowerShellRequest(
                "Start-Sleep -Seconds 30",
                TimeSpan.FromMilliseconds(300)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.TimedOut, result.TerminationKind);
        Assert.IsTrue(result.ProcessId > 0);
        Assert.IsTrue(result.Duration < TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    public async Task RunAsync_WhenExecutableCannotStart_ReturnsStartFailed()
    {
        string missingExecutable = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"),
            "missing.exe");

        ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
            new ProbeProcessRequest
            {
                ExecutablePath = missingExecutable,
                Arguments = Array.Empty<string>(),
                WorkingDirectory = Environment.CurrentDirectory,
                Timeout = TimeSpan.FromSeconds(5)
            },
            CancellationToken.None);

        Assert.AreEqual(
            ProcessTerminationKind.StartFailed,
            result.TerminationKind);
        Assert.IsNull(result.ExitCode);
        Assert.AreEqual(0, result.ProcessId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.StandardError));
    }

    [TestMethod]
    public async Task RunAsync_WhenCallerCancels_KillsProcessAndPropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await new ProbeProcessRunner().RunAsync(
                PowerShellRequest(
                    "Start-Sleep -Seconds 30",
                    TimeSpan.FromSeconds(20)),
                cancellationSource.Token));
    }

    [TestMethod]
    public async Task RunAsync_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException()
    {
        ProbeProcessRequest request = PowerShellRequest(
            "exit 0",
            TimeSpan.Zero);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            async () => await new ProbeProcessRunner().RunAsync(
                request,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task RunAsync_WithMissingWorkingDirectory_ThrowsDirectoryNotFoundException()
    {
        ProbeProcessRequest request = new()
        {
            ExecutablePath = "powershell.exe",
            Arguments = Array.Empty<string>(),
            WorkingDirectory = Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString("N")),
            Timeout = TimeSpan.FromSeconds(5)
        };

        await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(
            async () => await new ProbeProcessRunner().RunAsync(
                request,
                CancellationToken.None));
    }

    private static ProbeProcessRequest PowerShellRequest(
        string command,
        TimeSpan timeout)
    {
        return new ProbeProcessRequest
        {
            ExecutablePath = "powershell.exe",
            Arguments = new[]
            {
                "-NoLogo",
                "-NoProfile",
                "-Command",
                command
            },
            WorkingDirectory = Environment.CurrentDirectory,
            Timeout = timeout
        };
    }
}
