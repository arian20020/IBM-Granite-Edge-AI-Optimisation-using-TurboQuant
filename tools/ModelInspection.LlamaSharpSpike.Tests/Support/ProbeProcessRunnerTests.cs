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

        ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
            new ProbeProcessRequest
            {
                ExecutablePath = "powershell.exe",
                Arguments = new[]
                {
                    "-NoLogo",
                    "-NoProfile",
                    "-Command",
                    "param([string]$value) [Console]::Out.Write($value)",
                    expected
                },
                WorkingDirectory = Environment.CurrentDirectory,
                Timeout = TimeSpan.FromSeconds(10)
            },
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, result.TerminationKind);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(expected, result.StandardOutput);
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

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
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
