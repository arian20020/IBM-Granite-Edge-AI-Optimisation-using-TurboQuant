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
}
#pragma warning restore CA1707
