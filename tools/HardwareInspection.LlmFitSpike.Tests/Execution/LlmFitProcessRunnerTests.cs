using System.Diagnostics;
using System.Globalization;
using HardwareInspection.LlmFitSpike.Command;
using HardwareInspection.LlmFitSpike.Execution;
using HardwareInspection.LlmFitSpike.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HardwareInspection.LlmFitSpike.Tests.Execution;

[TestClass]
[DoNotParallelize]
[TestCategory("Deterministic")]
#pragma warning disable CA1707 // Test names intentionally encode the required behavior.
public sealed class LlmFitProcessRunnerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMilliseconds(500);

    [TestMethod]
    public async Task ExecuteAsync_Success_CapturesExitCodeAndBothStreams()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("success").ConfigureAwait(false);
        LlmFitCommand command = tool.SystemCommand;
        string[] originalArguments = command.Arguments.ToArray();

        LlmFitProcessResult result = await new LlmFitProcessRunner()
            .ExecuteAsync(command)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(0, result.ExitCode);
        Assert.IsNotNull(result.ProcessId);
        StringAssert.Contains(result.StandardOutput, "\"cpu_name\":\"Fixture CPU\"");
        Assert.AreEqual(string.Empty, result.StandardError);
        Assert.IsFalse(result.StandardOutputTruncated);
        Assert.IsFalse(result.StandardErrorTruncated);
        Assert.IsTrue(result.Duration >= TimeSpan.Zero);
        CollectionAssert.AreEqual(originalArguments, command.Arguments);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonZeroExit_PreservesExitCode()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("nonzero").ConfigureAwait(false);

        LlmFitProcessResult result = await new LlmFitProcessRunner()
            .ExecuteAsync(tool.SystemCommand)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        Assert.AreEqual(23, result.ExitCode);
        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.StandardError, "fake llmfit requested a nonzero exit");
    }

    [TestMethod]
    public async Task ExecuteAsync_LargeOutput_CapsEachStreamAtOneMiB()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("large-output").ConfigureAwait(false);

        LlmFitProcessResult result = await new LlmFitProcessRunner()
            .ExecuteAsync(tool.SystemCommand)
            .WaitAsync(TimeSpan.FromSeconds(15))
            .ConfigureAwait(false);

        Assert.AreEqual(LlmFitProcessRunner.MaximumCapturedBytesPerStream, result.StandardOutput.Length);
        Assert.AreEqual(LlmFitProcessRunner.MaximumCapturedBytesPerStream, result.StandardError.Length);
        Assert.IsTrue(result.StandardOutput.All(static character => character == 'O'));
        Assert.IsTrue(result.StandardError.All(static character => character == 'E'));
        Assert.IsTrue(result.StandardOutputTruncated);
        Assert.IsTrue(result.StandardErrorTruncated);
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Timeout_KillsRootProcess()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("sleep").ConfigureAwait(false);
        var observedProcessId = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observerStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        LlmFitProcessResult result = await CreateShortTimeoutRunner()
            .ExecuteAsync(
                tool.SystemCommand,
                TestTimeout,
                async (processId, token) =>
                {
                    observedProcessId.TrySetResult(processId);
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        observerStopped.TrySetResult();
                    }
                })
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        int processId = await observedProcessId.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        await observerStopped.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        Assert.IsTrue(result.TimedOut);
        Assert.IsFalse(result.Cancelled);
        Assert.AreEqual(processId, result.ProcessId);
        await AssertProcessExitedAsync(processId).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ExecuteAsync_Timeout_KillsDescendantProcess()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("spawn-child").ConfigureAwait(false);

        LlmFitProcessResult result = await CreateShortTimeoutRunner()
            .ExecuteAsync(tool.SystemCommand, TestTimeout)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        int childProcessId = int.Parse(result.StandardOutput.Trim(), CultureInfo.InvariantCulture);
        Assert.IsTrue(result.TimedOut);
        Assert.IsNotNull(result.ProcessId);
        await AssertProcessExitedAsync(result.ProcessId.Value).ConfigureAwait(false);
        await AssertProcessExitedAsync(childProcessId).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ExecuteAsync_CallerCancellation_IsDistinctFromTimeout()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("spawn-child").ConfigureAwait(false);
        using var cancellation = new CancellationTokenSource();
        bool observerSawLiveProcess = false;
        bool observerWasAwaitedAfterCancellation = false;

        LlmFitProcessResult result = await CreateShortTimeoutRunner()
            .ExecuteAsync(
                tool.SystemCommand,
                TimeSpan.FromSeconds(5),
                async (processId, token) =>
                {
                    using Process process = Process.GetProcessById(processId);
                    observerSawLiveProcess = !process.HasExited;
                    await Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None).ConfigureAwait(false);
                    cancellation.Cancel();
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        observerWasAwaitedAfterCancellation = true;
                    }
                },
                cancellation.Token)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        int childProcessId = int.Parse(result.StandardOutput.Trim(), CultureInfo.InvariantCulture);
        Assert.IsTrue(observerSawLiveProcess);
        Assert.IsTrue(observerWasAwaitedAfterCancellation);
        Assert.IsTrue(result.Cancelled);
        Assert.IsFalse(result.TimedOut);
        Assert.IsNotNull(result.ProcessId);
        await AssertProcessExitedAsync(result.ProcessId.Value).ConfigureAwait(false);
        await AssertProcessExitedAsync(childProcessId).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartFailure_ReturnsSanitizedFailure()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("success").ConfigureAwait(false);
        string sensitiveMissingPath = Path.Combine(tool.Root, "missing-sensitive-executable.exe");
        var command = new LlmFitCommand(sensitiveMissingPath, tool.Root, ["--version"]);

        LlmFitProcessResult result = await new LlmFitProcessRunner()
            .ExecuteAsync(command)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        Assert.IsTrue(result.ProcessStartFailed);
        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.ProcessId);
        Assert.IsNull(result.ExitCode);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
        Assert.IsFalse(result.StandardOutput.Contains(sensitiveMissingPath, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.StandardError.Contains(sensitiveMissingPath, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ExecuteAsync_ObserverFailure_KillsEntireProcessTree()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("spawn-child").ConfigureAwait(false);
        const string SensitiveObserverText = "C:\\secret\\observer-failure";

        LlmFitProcessResult result = await CreateShortTimeoutRunner()
            .ExecuteAsync(
                tool.SystemCommand,
                TimeSpan.FromSeconds(5),
                async (_, _) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None).ConfigureAwait(false);
                    throw new InvalidOperationException(SensitiveObserverText);
                })
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        int childProcessId = int.Parse(result.StandardOutput.Trim(), CultureInfo.InvariantCulture);
        Assert.IsTrue(result.ObserverFailed);
        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(result.StandardOutput.Contains(SensitiveObserverText, StringComparison.Ordinal));
        Assert.IsFalse(result.StandardError.Contains(SensitiveObserverText, StringComparison.Ordinal));
        Assert.IsFalse(result.StandardOutput.Contains(tool.Root, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.StandardError.Contains(tool.ExecutablePath, StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(result.ProcessId);
        await AssertProcessExitedAsync(result.ProcessId.Value).ConfigureAwait(false);
        await AssertProcessExitedAsync(childProcessId).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ExecuteAsync_ObserverCancellationCallbackFailure_CannotBypassCleanup()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("spawn-child").ConfigureAwait(false);
        const string SensitiveObserverText = "C:\\secret\\observer-cancellation-callback";
        var observedProcessId = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool observerWasAwaited = false;
        LlmFitProcessResult? result = null;

        try
        {
            result = await CreateShortTimeoutRunner()
                .ExecuteAsync(
                    tool.SystemCommand,
                    TestTimeout,
                    async (processId, token) =>
                    {
                        observedProcessId.TrySetResult(processId);
                        await Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None).ConfigureAwait(false);
                        using CancellationTokenRegistration registration = token.Register(
                            () => throw new InvalidOperationException(SensitiveObserverText));
                        try
                        {
                            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                        }
                        finally
                        {
                            observerWasAwaited = true;
                        }
                    })
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);
        }
        finally
        {
            if (observedProcessId.Task.IsCompletedSuccessfully)
            {
                await TryKillProcessTreeAsync(observedProcessId.Task.Result).ConfigureAwait(false);
            }
        }

        Assert.IsNotNull(result);
        int childProcessId = int.Parse(result.StandardOutput.Trim(), CultureInfo.InvariantCulture);
        Assert.IsTrue(result.ObserverFailed);
        Assert.IsTrue(result.TimedOut);
        Assert.IsFalse(result.Cancelled);
        Assert.IsTrue(observerWasAwaited);
        Assert.IsFalse(result.StandardOutput.Contains(SensitiveObserverText, StringComparison.Ordinal));
        Assert.IsFalse(result.StandardError.Contains(SensitiveObserverText, StringComparison.Ordinal));
        Assert.IsNotNull(result.ProcessId);
        await AssertProcessExitedAsync(result.ProcessId.Value).ConfigureAwait(false);
        await AssertProcessExitedAsync(childProcessId).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublicTimeoutBounds_AreEnforced()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("success").ConfigureAwait(false);
        var runner = new LlmFitProcessRunner();

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
                () => runner.ExecuteAsync(tool.SystemCommand, TimeSpan.FromMilliseconds(999)))
            .ConfigureAwait(false);
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
                () => runner.ExecuteAsync(tool.SystemCommand, TimeSpan.FromSeconds(121)))
            .ConfigureAwait(false);

        LlmFitProcessResult minimum = await runner.ExecuteAsync(tool.SystemCommand, TimeSpan.FromSeconds(1))
            .ConfigureAwait(false);
        LlmFitProcessResult maximum = await runner.ExecuteAsync(tool.SystemCommand, TimeSpan.FromSeconds(120))
            .ConfigureAwait(false);
        Assert.IsTrue(minimum.Succeeded);
        Assert.IsTrue(maximum.Succeeded);
    }

    [TestMethod]
    public async Task ExecuteAsync_CaptureFailure_KillsProcessAndMapsToObserverFailure()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("sleep").ConfigureAwait(false);
        int captureInvocation = 0;

        Task<BoundedTextCapture> CaptureAsync(Stream stream, int maximumRetainedBytes)
        {
            if (Interlocked.Increment(ref captureInvocation) == 1)
            {
                return Task.FromException<BoundedTextCapture>(
                    new IOException("C:\\secret\\capture-failure"));
            }

            return BoundedTextReader.ReadAsync(stream, maximumRetainedBytes);
        }

        var runner = new LlmFitProcessRunner(TestTimeout, CaptureAsync);
        LlmFitProcessResult result = await runner
            .ExecuteAsync(tool.SystemCommand, TimeSpan.FromSeconds(5))
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        Assert.IsTrue(result.ObserverFailed);
        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(result.TimedOut);
        Assert.IsFalse(result.StandardOutput.Contains("capture-failure", StringComparison.Ordinal));
        Assert.IsFalse(result.StandardError.Contains("capture-failure", StringComparison.Ordinal));
        Assert.IsNotNull(result.ProcessId);
        await AssertProcessExitedAsync(result.ProcessId.Value).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ExecuteAsync_SynchronousSecondCaptureStartupFailure_ContainsFirstPumpAndProcess()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("sleep").ConfigureAwait(false);
        HashSet<int> existingFixtureProcesses = SnapshotFixtureProcessIds();
        int captureInvocation = 0;
        bool firstPumpCompleted = false;

        Task<BoundedTextCapture> CaptureAsync(Stream stream, int maximumRetainedBytes)
        {
            if (Interlocked.Increment(ref captureInvocation) == 2)
            {
                throw new InvalidOperationException("C:\\secret\\synchronous-capture-startup");
            }

            return CaptureFirstAsync(stream, maximumRetainedBytes);
        }

        async Task<BoundedTextCapture> CaptureFirstAsync(Stream stream, int maximumRetainedBytes)
        {
            try
            {
                return await BoundedTextReader.ReadAsync(stream, maximumRetainedBytes).ConfigureAwait(false);
            }
            finally
            {
                firstPumpCompleted = true;
            }
        }

        LlmFitProcessResult? result = null;
        try
        {
            var runner = new LlmFitProcessRunner(TestTimeout, CaptureAsync);
            result = await runner
                .ExecuteAsync(tool.SystemCommand, TimeSpan.FromSeconds(5))
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);
        }
        finally
        {
            await KillNewFixtureProcessesAsync(existingFixtureProcesses).ConfigureAwait(false);
        }

        Assert.IsNotNull(result);
        Assert.IsTrue(result.ObserverFailed);
        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(result.TimedOut);
        Assert.IsTrue(firstPumpCompleted);
        Assert.IsFalse(result.StandardOutput.Contains("synchronous-capture-startup", StringComparison.Ordinal));
        Assert.IsFalse(result.StandardError.Contains("synchronous-capture-startup", StringComparison.Ordinal));
        Assert.IsNotNull(result.ProcessId);
        await AssertProcessExitedAsync(result.ProcessId.Value).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonIoAsynchronousCaptureFailure_IsSanitizedAndKillsTree()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("spawn-child").ConfigureAwait(false);
        HashSet<int> existingFixtureProcesses = SnapshotFixtureProcessIds();
        int captureInvocation = 0;

        Task<BoundedTextCapture> CaptureAsync(Stream stream, int maximumRetainedBytes)
        {
            return Interlocked.Increment(ref captureInvocation) == 2
                ? FailCaptureAsync()
                : BoundedTextReader.ReadAsync(stream, maximumRetainedBytes);
        }

        static async Task<BoundedTextCapture> FailCaptureAsync()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None).ConfigureAwait(false);
            throw new NotSupportedException("C:\\secret\\non-io-capture-failure");
        }

        LlmFitProcessResult? result = null;
        try
        {
            var runner = new LlmFitProcessRunner(TestTimeout, CaptureAsync);
            result = await runner
                .ExecuteAsync(tool.SystemCommand, TimeSpan.FromSeconds(5))
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);
        }
        finally
        {
            await KillNewFixtureProcessesAsync(existingFixtureProcesses).ConfigureAwait(false);
        }

        Assert.IsNotNull(result);
        int childProcessId = int.Parse(result.StandardOutput.Trim(), CultureInfo.InvariantCulture);
        Assert.IsTrue(result.ObserverFailed);
        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(result.TimedOut);
        Assert.IsFalse(result.Cancelled);
        Assert.IsFalse(result.StandardOutput.Contains("non-io-capture-failure", StringComparison.Ordinal));
        Assert.IsFalse(result.StandardError.Contains("non-io-capture-failure", StringComparison.Ordinal));
        Assert.IsNotNull(result.ProcessId);
        await AssertProcessExitedAsync(result.ProcessId.Value).ConfigureAwait(false);
        await AssertProcessExitedAsync(childProcessId).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ExecuteAsync_TimeoutCause_RemainsLatchedDuringDelayedCleanup()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("sleep").ConfigureAwait(false);
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(800));

        static async Task<BoundedTextCapture> DelayedCaptureAsync(
            Stream stream,
            int maximumRetainedBytes)
        {
            BoundedTextCapture capture = await BoundedTextReader.ReadAsync(stream, maximumRetainedBytes)
                .ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None).ConfigureAwait(false);
            return capture;
        }

        var runner = new LlmFitProcessRunner(TestTimeout, DelayedCaptureAsync);
        LlmFitProcessResult result = await runner
            .ExecuteAsync(
                tool.SystemCommand,
                TestTimeout,
                whileRunningObserver: null,
                cancellationToken: cancellation.Token)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        Assert.IsTrue(cancellation.IsCancellationRequested);
        Assert.IsTrue(result.TimedOut);
        Assert.IsFalse(result.Cancelled);
        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.ProcessId);
        await AssertProcessExitedAsync(result.ProcessId.Value).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ExecuteAsync_SynchronousExitWaitSetupFailure_IsContained()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("sleep").ConfigureAwait(false);
        HashSet<int> existingFixtureProcesses = SnapshotFixtureProcessIds();
        bool observerWasAwaited = false;

        static Task WaitForExitAsync(Process _)
        {
            throw new NotSupportedException("C:\\secret\\exit-wait-setup");
        }

        LlmFitProcessResult? result = null;
        try
        {
            var runner = new LlmFitProcessRunner(
                TestTimeout,
                BoundedTextReader.ReadAsync,
                WaitForExitAsync);
            result = await runner
                .ExecuteAsync(
                    tool.SystemCommand,
                    TimeSpan.FromSeconds(5),
                    async (_, token) =>
                    {
                        try
                        {
                            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                        }
                        finally
                        {
                            observerWasAwaited = true;
                        }
                    })
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);
        }
        finally
        {
            await KillNewFixtureProcessesAsync(existingFixtureProcesses).ConfigureAwait(false);
        }

        Assert.IsNotNull(result);
        Assert.IsTrue(result.ObserverFailed);
        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(result.TimedOut);
        Assert.IsFalse(result.Cancelled);
        Assert.IsTrue(observerWasAwaited);
        Assert.IsFalse(result.StandardOutput.Contains("exit-wait-setup", StringComparison.Ordinal));
        Assert.IsFalse(result.StandardError.Contains("exit-wait-setup", StringComparison.Ordinal));
        Assert.IsNotNull(result.ProcessId);
        await AssertProcessExitedAsync(result.ProcessId.Value).ConfigureAwait(false);
    }

    private static LlmFitProcessRunner CreateShortTimeoutRunner()
    {
        return new LlmFitProcessRunner(TestTimeout);
    }

    private static async Task AssertProcessExitedAsync(int processId)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(3))
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        }

        Assert.Fail($"Process {processId.ToString(CultureInfo.InvariantCulture)} remained alive after bounded cleanup.");
    }

    private static async Task TryKillProcessTreeAsync(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None)
                    .WaitAsync(TimeSpan.FromSeconds(3))
                    .ConfigureAwait(false);
            }
        }
        catch (ArgumentException)
        {
            // The process has already exited and its identifier is no longer present.
        }
        catch (InvalidOperationException)
        {
            // The process exited between lookup and termination.
        }
    }

    private static HashSet<int> SnapshotFixtureProcessIds()
    {
        return Process.GetProcessesByName("GraniteEdgeAI.HardwareInspection.LlmFitFakeTool")
            .Select(static process =>
            {
                using (process)
                {
                    return process.Id;
                }
            })
            .ToHashSet();
    }

    private static async Task KillNewFixtureProcessesAsync(HashSet<int> existingProcessIds)
    {
        foreach (Process process in Process.GetProcessesByName("GraniteEdgeAI.HardwareInspection.LlmFitFakeTool"))
        {
            using (process)
            {
                if (!existingProcessIds.Contains(process.Id) && !process.HasExited)
                {
                    await TryKillProcessTreeAsync(process.Id).ConfigureAwait(false);
                }
            }
        }
    }
}
#pragma warning restore CA1707
