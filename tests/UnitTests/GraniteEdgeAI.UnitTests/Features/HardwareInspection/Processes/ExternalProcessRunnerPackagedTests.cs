using System.Diagnostics;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Support;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Processes;

[TestClass]
[DoNotParallelize]
public sealed class ExternalProcessRunnerPackagedTests
{
    [TestMethod]
    public async Task RepeatedShortLivedProcessesAlwaysReturnTheirAuthoritativeExitCode()
    {
        using VerifiedPackagedToolFixture fixture =
            VerifiedPackagedToolFixture.CreateLlmFit("success");
        var runner = new ExternalProcessRunner();

        for (int attempt = 0; attempt < 64; attempt++)
        {
            ExternalProcessResult result = await runner.RunAsync(
                fixture.Tool,
                Request("version"),
                CancellationToken.None);

            Assert.AreEqual(
                ExternalProcessTerminationReason.Exited,
                result.TerminationReason,
                $"attempt {attempt}");
            Assert.AreEqual(0, result.ExitCode, $"attempt {attempt}");
        }
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RunUsesOnlyManifestDeclaredVersionArguments()
    {
        using VerifiedPackagedToolFixture fixture = VerifiedPackagedToolFixture.CreateLlmFit("success");

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("version"),
            CancellationToken.None);

        Assert.AreEqual(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("llmfit 1.1.9", result.StandardOutput.Trim());
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RunReturnsBoundedSuccessfulOutput()
    {
        using VerifiedPackagedToolFixture fixture = VerifiedPackagedToolFixture.CreateLlmFit("success");

        Assert.IsTrue(fixture.Tool.TryAcquireExecutionCustody(out IDisposable? custody),
            "Launch preflight failed: verified execution custody could not be acquired.");
        custody!.Dispose();
        Assert.IsTrue(WindowsKillOnCloseJob.TryCreate(out WindowsKillOnCloseJob job),
            "Launch preflight failed: kill-on-close Job Object could not be created.");
        job.Dispose();
        TrustedToolOperationEnvironment environment;
        try
        {
            environment = TrustedToolOperationEnvironment.CreateCurrent(includeDotnetRoots: false);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Launch preflight failed: protected operation environment. " +
                $"LocalApplicationData={Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}; " +
                $"SystemRoot={Environment.GetEnvironmentVariable("SystemRoot")}; " +
                $"WINDIR={Environment.GetEnvironmentVariable("WINDIR")}; " +
                $"OS={Environment.OSVersion}; BaseDirectory={AppContext.BaseDirectory}.", exception);
        }
        environment.Dispose();
        Assert.IsTrue(environment.CleanupSucceeded,
            "Launch preflight failed: protected operation environment cleanup was not verified.");

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system"),
            CancellationToken.None);

        Assert.AreEqual(ExternalProcessTerminationReason.Exited, result.TerminationReason,
            "Execution custody, Job creation and protected environment preflights passed. " +
            $"ExitCode={result.ExitCode}; StandardError={result.StandardError}; " +
            $"PackageRoot={fixture.PackageRoot}; OS={Environment.OSVersion}.");
        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.StandardOutput, "\"total_ram_gb\":32");
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RunPreservesNonZeroExitWithoutConvertingItToStartFailure()
    {
        using VerifiedPackagedToolFixture fixture = VerifiedPackagedToolFixture.CreateLlmFit("nonzero");

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system"),
            CancellationToken.None);

        Assert.AreEqual(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.AreEqual(23, result.ExitCode);
        StringAssert.Contains(result.StandardError, "nonzero exit");
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RunKillsProcessWhenStandardOutputExceedsItsIndependentLimit()
    {
        using VerifiedPackagedToolFixture fixture = VerifiedPackagedToolFixture.CreateLlmFit("large-output");

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system", stdoutLimit: 1024, stderrLimit: 3 * 1024 * 1024),
            CancellationToken.None);

        Assert.AreEqual(
            ExternalProcessTerminationReason.OutputLimitExceeded,
            result.TerminationReason);
        Assert.IsLessThanOrEqualTo(1024, System.Text.Encoding.UTF8.GetByteCount(result.StandardOutput));
        Assert.IsNull(result.ExitCode);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RunKillsProcessWhenStandardErrorExceedsItsIndependentLimit()
    {
        using VerifiedPackagedToolFixture fixture = VerifiedPackagedToolFixture.CreateLlmFit("large-output");

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system", stdoutLimit: 3 * 1024 * 1024, stderrLimit: 1024),
            CancellationToken.None);

        Assert.AreEqual(
            ExternalProcessTerminationReason.OutputLimitExceeded,
            result.TerminationReason);
        Assert.IsLessThanOrEqualTo(1024, System.Text.Encoding.UTF8.GetByteCount(result.StandardError));
        Assert.IsNull(result.ExitCode);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RunTimesOutAndKillsRootProcess()
    {
        using VerifiedPackagedToolFixture fixture = VerifiedPackagedToolFixture.CreateLlmFit("sleep");

        Task<ExternalProcessResult> execution = new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system", timeout: TimeSpan.FromSeconds(2)),
            CancellationToken.None);
        using Process rootProcess = await WaitForLiveProcessAsync(
            Path.Combine(fixture.ControlRoot, "owned-root-ready.txt"));
        ExternalProcessResult result = await execution;

        Assert.AreEqual(ExternalProcessTerminationReason.TimedOut, result.TerminationReason);
        Assert.IsNull(result.ExitCode);
        await AssertProcessExitedAsync(rootProcess);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RunCancellationKillsEntireProcessTree()
    {
        using VerifiedPackagedToolFixture fixture = VerifiedPackagedToolFixture.CreateLlmFit("spawn-child");
        using CancellationTokenSource cancellation = new();
        Task<ExternalProcessResult> execution = new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system", timeout: TimeSpan.FromSeconds(10)),
            cancellation.Token);
        string childReady = Path.Combine(fixture.ControlRoot, "spawn-child-ready.txt");
        using Process childProcess = await WaitForLiveProcessAsync(childReady);

        cancellation.Cancel();
        ExternalProcessResult result = await execution;

        Assert.AreEqual(ExternalProcessTerminationReason.Cancelled, result.TerminationReason);
        Assert.IsNull(result.ExitCode);
        await AssertProcessExitedAsync(childProcess);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RunJobCustodyKillsChildThatOutlivesNormallyExitedParent()
    {
        using VerifiedPackagedToolFixture fixture = VerifiedPackagedToolFixture.CreateLlmFit("spawn-child-exit");

        Task<ExternalProcessResult> execution = new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system"),
            CancellationToken.None);
        using Process childProcess = await WaitForLiveProcessAsync(
            Path.Combine(fixture.ControlRoot, "spawn-child-ready.txt"));
        ExternalProcessResult result = await execution;

        Assert.AreEqual(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.AreEqual(0, result.ExitCode);
        await AssertProcessExitedAsync(childProcess);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RunPlacesRootInJobBeforeManagedEntryPointExecutes()
    {
        using VerifiedPackagedToolFixture fixture = VerifiedPackagedToolFixture.CreateLlmFit("assert-in-job");

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system"),
            CancellationToken.None);

        Assert.AreEqual(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RunReturnsStartFailedWhenVerifiedCustodyWasDisposed()
    {
        using VerifiedPackagedToolFixture fixture = VerifiedPackagedToolFixture.CreateLlmFit("success");
        fixture.Tool.Dispose();

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system"),
            CancellationToken.None);

        Assert.AreEqual(ExternalProcessTerminationReason.StartFailed, result.TerminationReason);
        Assert.IsNull(result.ExitCode);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RunRetainsExecutionCustodyAfterOwnerIsDisposed()
    {
        using VerifiedPackagedToolFixture fixture = VerifiedPackagedToolFixture.CreateLlmFit(
            "sleep",
            useWritableCopy: true);
        string executablePath = fixture.Tool.ExecutablePath;
        using CancellationTokenSource cancellation = new();
        Task<ExternalProcessResult> execution = new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system", timeout: TimeSpan.FromSeconds(10)),
            cancellation.Token);
        await WaitForProcessIdAsync(Path.Combine(fixture.ControlRoot, "owned-root-ready.txt"));

        fixture.Tool.Dispose();
        Assert.Throws<IOException>(() => OpenForWrite(executablePath));

        cancellation.Cancel();
        ExternalProcessResult result = await execution;

        Assert.AreEqual(ExternalProcessTerminationReason.Cancelled, result.TerminationReason);
        await OpenForWriteWhenReleasedAsync(executablePath);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RunReturnsCancelledWithoutStartingWhenCallerAlreadyCancelled()
    {
        using VerifiedPackagedToolFixture fixture = VerifiedPackagedToolFixture.CreateLlmFit("sleep");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system"),
            cancellation.Token);

        Assert.AreEqual(ExternalProcessTerminationReason.Cancelled, result.TerminationReason);
        Assert.IsFalse(File.Exists(Path.Combine(fixture.ControlRoot, "owned-root-ready.txt")));
    }

    private static ExternalProcessRequest Request(
        string command,
        TimeSpan? timeout = null,
        int stdoutLimit = 64 * 1024,
        int stderrLimit = 64 * 1024) =>
        new(command, timeout ?? TimeSpan.FromSeconds(5), stdoutLimit, stderrLimit);

    private static async Task<int> WaitForProcessIdAsync(string path)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            try
            {
                if (File.Exists(path) &&
                    int.TryParse(await File.ReadAllTextAsync(path), out int processId))
                {
                    return processId;
                }
            }
            catch (IOException)
            {
                // the fixture has created the marker but has not released its write handle yet
            }

            await Task.Delay(20);
        }

        Assert.Fail("The harmless fixture did not publish its bounded readiness marker.");
        return 0;
    }

    private static async Task<Process> WaitForLiveProcessAsync(string path)
    {
        int processId = await WaitForProcessIdAsync(path);
        Process process = Process.GetProcessById(processId);
        if (process.HasExited)
        {
            process.Dispose();
            Assert.Fail("The harmless fixture process exited before it could be observed.");
        }

        try
        {
            if (Path.GetFileName(path).Equals(
                "spawn-child-ready.txt",
                StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(
                    Path.Combine(
                        Path.GetDirectoryName(path)!,
                        "spawn-child-observed.txt"),
                    string.Empty);
            }

            return process;
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    private static async Task AssertProcessExitedAsync(Process process)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            process.Refresh();
            if (process.HasExited)
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("A harmless fixture process remained after bounded cleanup.");
    }

    private static void OpenForWrite(string path)
    {
        using FileStream ignored = new(path, FileMode.Open, FileAccess.Write, FileShare.Read);
    }

    private static async Task OpenForWriteWhenReleasedAsync(string path)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                OpenForWrite(path);
                return;
            }
            catch (IOException) when (attempt < 99)
            {
                await Task.Delay(20);
            }
        }
    }

}
