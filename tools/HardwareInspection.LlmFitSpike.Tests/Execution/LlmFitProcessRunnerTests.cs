using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
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
        var failures = new List<Exception>();
        OwnedObservedProcess? observedProcess = null;
        Task<LlmFitProcessResult>? execution = null;

        try
        {
            execution = CreateShortTimeoutRunner()
                .ExecuteAsync(
                    tool.SystemCommand,
                    TestTimeout,
                    async (processId, token) =>
                    {
                        observedProcess = OwnedObservedProcess.TryCapture(processId, tool);
                        observedProcessId.TrySetResult(processId);
                        try
                        {
                            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                        }
                        finally
                        {
                            observerStopped.TrySetResult();
                        }
                    });
            LlmFitProcessResult result = await execution
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);

            int processId = await observedProcessId.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            await observerStopped.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            Assert.IsTrue(result.TimedOut);
            Assert.IsFalse(result.Cancelled);
            Assert.AreEqual(processId, result.ProcessId);
            Assert.IsNotNull(observedProcess);
            await observedProcess.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            await CaptureCleanupFailureAsync(
                    failures,
                    async () =>
                    {
                        if (execution is not null)
                        {
                            _ = await execution.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        }
                    })
                .ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedProcess).ConfigureAwait(false);
        }

        RethrowFailures(failures);
    }

    [TestMethod]
    public async Task ExecuteAsync_Timeout_KillsDescendantProcess()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("spawn-child").ConfigureAwait(false);
        var failures = new List<Exception>();
        OwnedObservedProcess? observedRoot = null;
        OwnedObservedProcess? observedChild = null;
        Task<LlmFitProcessResult>? execution = null;

        try
        {
            execution = CreateShortTimeoutRunner()
                .ExecuteAsync(
                    tool.SystemCommand,
                    TestTimeout,
                    (processId, _) =>
                    {
                        observedRoot = OwnedObservedProcess.TryCapture(processId, tool);
                        return Task.CompletedTask;
                    });
            int readyChildProcessId = await tool
                .WaitForChildReadyAsync(TimeSpan.FromSeconds(3))
                .ConfigureAwait(false);
            observedChild = OwnedObservedProcess.TryCapture(readyChildProcessId, tool);
            LlmFitProcessResult result = await execution
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);

            int childProcessId = int.Parse(result.StandardOutput.Trim(), CultureInfo.InvariantCulture);
            Assert.AreEqual(readyChildProcessId, childProcessId);
            Assert.IsTrue(result.TimedOut);
            Assert.IsNotNull(result.ProcessId);
            Assert.IsNotNull(observedRoot);
            Assert.IsNotNull(observedChild);
            await observedRoot.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            await observedChild.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            await CaptureCleanupFailureAsync(
                    failures,
                    async () =>
                    {
                        if (execution is not null)
                        {
                            _ = await execution.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        }
                    })
                .ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedChild).ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedRoot).ConfigureAwait(false);
        }

        RethrowFailures(failures);
    }

    [TestMethod]
    public async Task ExecuteAsync_CallerCancellation_IsDistinctFromTimeout()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("spawn-child").ConfigureAwait(false);
        using var cancellation = new CancellationTokenSource();
        bool observerSawLiveProcess = false;
        bool observerWasAwaitedAfterCancellation = false;
        var failures = new List<Exception>();
        OwnedObservedProcess? observedRoot = null;
        OwnedObservedProcess? observedChild = null;
        Task<LlmFitProcessResult>? execution = null;

        try
        {
            execution = CreateShortTimeoutRunner()
                .ExecuteAsync(
                    tool.SystemCommand,
                    TimeSpan.FromSeconds(5),
                    async (processId, token) =>
                    {
                        observedRoot = OwnedObservedProcess.TryCapture(processId, tool);
                        observerSawLiveProcess = observedRoot is not null &&
                            !observedRoot.WaitForExit(TimeSpan.Zero);
                        int childProcessId = await tool
                            .WaitForChildReadyAsync(TimeSpan.FromSeconds(3))
                            .ConfigureAwait(false);
                        observedChild = OwnedObservedProcess.TryCapture(childProcessId, tool);
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
                    cancellation.Token);
            LlmFitProcessResult result = await execution
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);

            int childProcessId = int.Parse(result.StandardOutput.Trim(), CultureInfo.InvariantCulture);
            Assert.IsTrue(observerSawLiveProcess);
            Assert.IsTrue(observerWasAwaitedAfterCancellation);
            Assert.IsTrue(result.Cancelled);
            Assert.IsFalse(result.TimedOut);
            Assert.IsNotNull(result.ProcessId);
            Assert.IsNotNull(observedRoot);
            Assert.IsNotNull(observedChild);
            await observedRoot.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            await observedChild.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            Assert.AreEqual(childProcessId, observedChild.ProcessId);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            await CaptureCleanupFailureAsync(
                    failures,
                    async () =>
                    {
                        if (execution is not null)
                        {
                            _ = await execution.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        }
                    })
                .ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedChild).ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedRoot).ConfigureAwait(false);
        }

        RethrowFailures(failures);
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
        var failures = new List<Exception>();
        OwnedObservedProcess? observedRoot = null;
        OwnedObservedProcess? observedChild = null;
        Task<LlmFitProcessResult>? execution = null;

        try
        {
            execution = CreateShortTimeoutRunner()
                .ExecuteAsync(
                    tool.SystemCommand,
                    TimeSpan.FromSeconds(5),
                    async (processId, _) =>
                    {
                        observedRoot = OwnedObservedProcess.TryCapture(processId, tool);
                        int childProcessId = await tool
                            .WaitForChildReadyAsync(TimeSpan.FromSeconds(3))
                            .ConfigureAwait(false);
                        observedChild = OwnedObservedProcess.TryCapture(childProcessId, tool);
                        throw new InvalidOperationException(SensitiveObserverText);
                    });
            LlmFitProcessResult result = await execution
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
            Assert.IsNotNull(observedRoot);
            Assert.IsNotNull(observedChild);
            Assert.AreEqual(childProcessId, observedChild.ProcessId);
            await observedRoot.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            await observedChild.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            await CaptureCleanupFailureAsync(
                    failures,
                    async () =>
                    {
                        if (execution is not null)
                        {
                            _ = await execution.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        }
                    })
                .ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedChild).ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedRoot).ConfigureAwait(false);
        }

        RethrowFailures(failures);
    }

    [TestMethod]
    public async Task ExecuteAsync_ObserverCancellationCallbackFailure_CannotBypassCleanup()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("spawn-child").ConfigureAwait(false);
        const string SensitiveObserverText = "C:\\secret\\observer-cancellation-callback";
        OwnedObservedProcess? observedRoot = null;
        OwnedObservedProcess? observedChild = null;
        using var cancellation = new CancellationTokenSource();
        bool observerWasAwaited = false;
        var failures = new List<Exception>();
        Task<LlmFitProcessResult>? execution = null;

        try
        {
            execution = CreateShortTimeoutRunner()
                .ExecuteAsync(
                    tool.SystemCommand,
                    TimeSpan.FromSeconds(5),
                    async (processId, token) =>
                    {
                        observedRoot = OwnedObservedProcess.TryCapture(processId, tool);
                        int childProcessId = await tool
                            .WaitForChildReadyAsync(TimeSpan.FromSeconds(3))
                            .ConfigureAwait(false);
                        observedChild = OwnedObservedProcess.TryCapture(childProcessId, tool);
                        using CancellationTokenRegistration registration = token.Register(
                            () => throw new InvalidOperationException(SensitiveObserverText));
                        cancellation.Cancel();
                        try
                        {
                            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                        }
                        finally
                        {
                            observerWasAwaited = true;
                        }
                    },
                    cancellation.Token);
            LlmFitProcessResult result = await execution
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);

            int childProcessId = int.Parse(result.StandardOutput.Trim(), CultureInfo.InvariantCulture);
            Assert.IsTrue(result.ObserverFailed);
            Assert.IsFalse(result.TimedOut);
            Assert.IsTrue(result.Cancelled);
            Assert.IsTrue(observerWasAwaited);
            Assert.IsFalse(result.StandardOutput.Contains(SensitiveObserverText, StringComparison.Ordinal));
            Assert.IsFalse(result.StandardError.Contains(SensitiveObserverText, StringComparison.Ordinal));
            Assert.IsNotNull(result.ProcessId);
            Assert.IsNotNull(observedRoot);
            Assert.IsNotNull(observedChild);
            Assert.AreEqual(childProcessId, observedChild.ProcessId);
            await observedRoot.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            await observedChild.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            await CaptureCleanupFailureAsync(
                    failures,
                    async () =>
                    {
                        if (execution is not null)
                        {
                            _ = await execution.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        }
                    })
                .ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedChild).ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedRoot).ConfigureAwait(false);
        }

        RethrowFailures(failures);
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

        var runner = new LlmFitProcessRunner(TestTimeout, CaptureAsync);
        LlmFitProcessResult result = await runner
            .ExecuteAsync(tool.SystemCommand, TimeSpan.FromSeconds(5))
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

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
        int captureInvocation = 0;
        OwnedObservedProcess? observedRoot = null;
        OwnedObservedProcess? observedChild = null;

        Task<BoundedTextCapture> CaptureAsync(Stream stream, int maximumRetainedBytes)
        {
            return Interlocked.Increment(ref captureInvocation) == 2
                ? FailCaptureAsync()
                : BoundedTextReader.ReadAsync(stream, maximumRetainedBytes);
        }

        async Task<BoundedTextCapture> FailCaptureAsync()
        {
            int childProcessId = await tool
                .WaitForChildReadyAsync(TimeSpan.FromSeconds(3))
                .ConfigureAwait(false);
            observedChild = OwnedObservedProcess.TryCapture(childProcessId, tool);
            throw new NotSupportedException("C:\\secret\\non-io-capture-failure");
        }

        var failures = new List<Exception>();
        Task<LlmFitProcessResult>? execution = null;
        try
        {
            var runner = new LlmFitProcessRunner(TestTimeout, CaptureAsync);
            execution = runner
                .ExecuteAsync(
                    tool.SystemCommand,
                    TimeSpan.FromSeconds(5),
                    (processId, _) =>
                    {
                        observedRoot = OwnedObservedProcess.TryCapture(processId, tool);
                        return Task.CompletedTask;
                    });
            LlmFitProcessResult result = await execution
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);

            int childProcessId = int.Parse(result.StandardOutput.Trim(), CultureInfo.InvariantCulture);
            Assert.IsTrue(result.ObserverFailed);
            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.TimedOut);
            Assert.IsFalse(result.Cancelled);
            Assert.IsFalse(result.StandardOutput.Contains("non-io-capture-failure", StringComparison.Ordinal));
            Assert.IsFalse(result.StandardError.Contains("non-io-capture-failure", StringComparison.Ordinal));
            Assert.IsNotNull(result.ProcessId);
            Assert.IsNotNull(observedRoot);
            Assert.IsNotNull(observedChild);
            Assert.AreEqual(childProcessId, observedChild.ProcessId);
            await observedRoot.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            await observedChild.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            await CaptureCleanupFailureAsync(
                    failures,
                    async () =>
                    {
                        if (execution is not null)
                        {
                            _ = await execution.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        }
                    })
                .ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedChild).ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedRoot).ConfigureAwait(false);
        }

        RethrowFailures(failures);
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
        var failures = new List<Exception>();
        OwnedObservedProcess? observedProcess = null;
        Task<LlmFitProcessResult>? execution = null;

        try
        {
            execution = runner.ExecuteAsync(
                tool.SystemCommand,
                TestTimeout,
                (processId, _) =>
                {
                    observedProcess = OwnedObservedProcess.TryCapture(processId, tool);
                    return Task.CompletedTask;
                },
                cancellation.Token);
            LlmFitProcessResult result = await execution
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);

            Assert.IsTrue(cancellation.IsCancellationRequested);
            Assert.IsTrue(result.TimedOut);
            Assert.IsFalse(result.Cancelled);
            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.ProcessId);
            Assert.IsNotNull(observedProcess);
            await observedProcess.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            await CaptureCleanupFailureAsync(
                    failures,
                    async () =>
                    {
                        if (execution is not null)
                        {
                            _ = await execution.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        }
                    })
                .ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedProcess).ConfigureAwait(false);
        }

        RethrowFailures(failures);
    }

    [TestMethod]
    public async Task ExecuteAsync_SynchronousExitWaitSetupFailure_IsContained()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("sleep").ConfigureAwait(false);
        bool observerWasAwaited = false;
        OwnedObservedProcess? observedProcess = null;

        static Task WaitForExitAsync(Process _)
        {
            throw new NotSupportedException("C:\\secret\\exit-wait-setup");
        }

        var failures = new List<Exception>();
        Task<LlmFitProcessResult>? execution = null;
        try
        {
            var runner = new LlmFitProcessRunner(
                TestTimeout,
                BoundedTextReader.ReadAsync,
                WaitForExitAsync);
            execution = runner
                .ExecuteAsync(
                    tool.SystemCommand,
                    TimeSpan.FromSeconds(5),
                    async (processId, token) =>
                    {
                        observedProcess = OwnedObservedProcess.TryCapture(processId, tool);
                        try
                        {
                            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                        }
                        finally
                        {
                            observerWasAwaited = true;
                        }
                    });
            LlmFitProcessResult result = await execution
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);

            Assert.IsTrue(result.ObserverFailed);
            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.TimedOut);
            Assert.IsFalse(result.Cancelled);
            Assert.IsTrue(observerWasAwaited);
            Assert.IsFalse(result.StandardOutput.Contains("exit-wait-setup", StringComparison.Ordinal));
            Assert.IsFalse(result.StandardError.Contains("exit-wait-setup", StringComparison.Ordinal));
            Assert.IsNotNull(result.ProcessId);
            Assert.IsNotNull(observedProcess);
            await observedProcess.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            await CaptureCleanupFailureAsync(
                    failures,
                    async () =>
                    {
                        if (execution is not null)
                        {
                            _ = await execution.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        }
                    })
                .ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedProcess).ConfigureAwait(false);
        }

        RethrowFailures(failures);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuccessfulProcessWithObserverIgnoringCancellation_ReturnsBoundedFailure()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("success").ConfigureAwait(false);
        var observerNeverCompletes = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new LlmFitProcessRunner(TestTimeout, TimeSpan.FromMilliseconds(300));
        var elapsed = Stopwatch.StartNew();

        LlmFitProcessResult? result = null;
        try
        {
            result = await runner
                .ExecuteAsync(
                    tool.SystemCommand,
                    TimeSpan.FromSeconds(5),
                    (_, _) => observerNeverCompletes.Task)
                .WaitAsync(TimeSpan.FromSeconds(3))
                .ConfigureAwait(false);
        }
        finally
        {
            observerNeverCompletes.TrySetResult();
        }

        Assert.IsNotNull(result);
        Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(2));
        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(result.ObserverFailed);
        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(result.TimedOut);
        Assert.IsFalse(result.Cancelled);
        Assert.IsNotNull(result.ProcessId);
        await AssertProcessExitedAsync(result.ProcessId.Value).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ExecuteAsync_ObserverBlocksSynchronouslyBeforeReturning_ReturnsBoundedFailure()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("sleep").ConfigureAwait(false);
        using var releaseObserver = new ManualResetEventSlim(initialState: false);
        var observerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OwnedObservedProcess? observedProcess = null;
        var runner = new LlmFitProcessRunner(TestTimeout, TimeSpan.FromMilliseconds(300));
        Task<LlmFitProcessResult>? execution = null;
        var elapsed = Stopwatch.StartNew();
        var failures = new List<Exception>();

        try
        {
            execution = Task.Factory.StartNew(
                () => runner.ExecuteAsync(
                    tool.SystemCommand,
                    TestTimeout,
                    (processId, _) =>
                    {
                        observedProcess = OwnedObservedProcess.TryCapture(processId, tool);
                        observerStarted.TrySetResult();
                        releaseObserver.Wait(TimeSpan.FromSeconds(10), CancellationToken.None);
                        return Task.CompletedTask;
                    }),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
            await observerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

            LlmFitProcessResult result = await execution
                .WaitAsync(TimeSpan.FromSeconds(3))
                .ConfigureAwait(false);

            Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(2));
            Assert.IsTrue(result.TimedOut);
            Assert.IsFalse(result.Cancelled);
            Assert.IsTrue(result.ObserverFailed);
            Assert.IsNotNull(result.ProcessId);
            Assert.IsNotNull(observedProcess);
            await observedProcess.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            releaseObserver.Set();
            await CaptureCleanupFailureAsync(
                    failures,
                    async () =>
                    {
                        if (execution is not null)
                        {
                            _ = await execution.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        }
                    })
                .ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedProcess).ConfigureAwait(false);
        }

        RethrowFailures(failures);
    }

    [TestMethod]
    public async Task ExecuteAsync_BlockingObserverCancellationCallback_KillsBeforeBoundedCancellation()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("sleep").ConfigureAwait(false);
        using var releaseCallback = new ManualResetEventSlim(initialState: false);
        var callbackCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OwnedObservedProcess? observedProcess = null;
        bool callbackObservedProcessExit = false;
        var runner = new LlmFitProcessRunner(TestTimeout, TimeSpan.FromMilliseconds(300));
        var elapsed = Stopwatch.StartNew();
        var failures = new List<Exception>();
        Task<LlmFitProcessResult>? execution = null;

        try
        {
            execution = runner
                .ExecuteAsync(
                    tool.SystemCommand,
                    TestTimeout,
                    (processId, token) =>
                    {
                        observedProcess = OwnedObservedProcess.TryCapture(processId, tool);
                        _ = token.Register(() =>
                        {
                            try
                            {
                                callbackObservedProcessExit = observedProcess?.WaitForExit(TimeSpan.FromSeconds(1)) == true;
                                releaseCallback.Wait(TimeSpan.FromSeconds(5));
                            }
                            finally
                            {
                                callbackCompleted.TrySetResult();
                            }
                        });
                        return Task.Delay(Timeout.InfiniteTimeSpan, token);
                    });
            LlmFitProcessResult result = await execution
                .WaitAsync(TimeSpan.FromSeconds(3))
                .ConfigureAwait(false);

            Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(2));
            Assert.IsTrue(callbackObservedProcessExit);
            Assert.IsTrue(result.TimedOut);
            Assert.IsFalse(result.Cancelled);
            Assert.IsTrue(result.ObserverFailed);
            Assert.IsNotNull(result.ProcessId);
            Assert.IsNotNull(observedProcess);
            await observedProcess.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            releaseCallback.Set();
            await CaptureCleanupFailureAsync(
                    failures,
                    () => callbackCompleted.Task.WaitAsync(TimeSpan.FromSeconds(3)))
                .ConfigureAwait(false);
            await CaptureCleanupFailureAsync(
                    failures,
                    async () =>
                    {
                        if (execution is not null)
                        {
                            _ = await execution.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        }
                    })
                .ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedProcess).ConfigureAwait(false);
        }

        RethrowFailures(failures);
    }

    [TestMethod]
    public async Task ExecuteAsync_IncompleteCaptureCleanup_ReturnsBoundedFailure()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("sleep").ConfigureAwait(false);
        var retainedCapture = new TaskCompletionSource<BoundedTextCapture>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int captureInvocation = 0;
        OwnedObservedProcess? observedProcess = null;

        Task<BoundedTextCapture> CaptureAsync(Stream stream, int maximumRetainedBytes)
        {
            return Interlocked.Increment(ref captureInvocation) == 1
                ? retainedCapture.Task
                : BoundedTextReader.ReadAsync(stream, maximumRetainedBytes);
        }

        var runner = new LlmFitProcessRunner(
            TestTimeout,
            TimeSpan.FromMilliseconds(300),
            CaptureAsync,
            static process => process.WaitForExitAsync(CancellationToken.None));
        var elapsed = Stopwatch.StartNew();
        var failures = new List<Exception>();
        Task<LlmFitProcessResult>? execution = null;

        try
        {
            execution = runner
                .ExecuteAsync(
                    tool.SystemCommand,
                    TestTimeout,
                    (processId, _) =>
                    {
                        observedProcess = OwnedObservedProcess.TryCapture(processId, tool);
                        return Task.CompletedTask;
                    });
            LlmFitProcessResult result = await execution
                .WaitAsync(TimeSpan.FromSeconds(3))
                .ConfigureAwait(false);

            Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(2));
            Assert.IsTrue(
                result.TimedOut,
                $"TimedOut={result.TimedOut}; Cancelled={result.Cancelled}; ObserverFailed={result.ObserverFailed}; ExitCode={result.ExitCode}");
            Assert.IsTrue(result.ObserverFailed);
            Assert.IsFalse(result.StandardOutput.Contains("late-capture", StringComparison.Ordinal));
            Assert.IsFalse(result.StandardError.Contains("late-capture", StringComparison.Ordinal));
            Assert.IsNotNull(result.ProcessId);
            Assert.IsNotNull(observedProcess);
            await observedProcess.AssertExitedAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            retainedCapture.TrySetException(new NotSupportedException("C:\\secret\\late-capture"));
            await CaptureCleanupFailureAsync(
                    failures,
                    async () =>
                    {
                        if (execution is not null)
                        {
                            _ = await execution.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        }
                    })
                .ConfigureAwait(false);
            await CaptureOwnedProcessCleanupFailureAsync(failures, observedProcess).ConfigureAwait(false);
        }

        RethrowFailures(failures);
    }

    [TestMethod]
    public async Task ExecuteAsync_IncompleteInjectedExitCleanup_ReturnsBoundedFailure()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("success").ConfigureAwait(false);
        var retainedExit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new LlmFitProcessRunner(
            TestTimeout,
            TimeSpan.FromMilliseconds(300),
            BoundedTextReader.ReadAsync,
            _ => retainedExit.Task);
        var elapsed = Stopwatch.StartNew();

        LlmFitProcessResult result;
        try
        {
            result = await runner
                .ExecuteAsync(tool.SystemCommand, TestTimeout)
                .WaitAsync(TimeSpan.FromSeconds(3))
                .ConfigureAwait(false);
        }
        finally
        {
            retainedExit.TrySetException(new NotSupportedException("C:\\secret\\late-exit"));
        }

        Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(2));
        Assert.IsTrue(result.TimedOut);
        Assert.IsTrue(result.ObserverFailed);
        Assert.AreEqual(0, result.ExitCode);
        Assert.IsFalse(result.StandardOutput.Contains("late-exit", StringComparison.Ordinal));
        Assert.IsFalse(result.StandardError.Contains("late-exit", StringComparison.Ordinal));
        Assert.IsNotNull(result.ProcessId);
        await AssertProcessExitedAsync(result.ProcessId.Value).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ExecuteAsync_RepeatedSuccess_ReleasesAllMonitorResources()
    {
        int baseline = LlmFitProcessRunner.ActiveMonitorResourceCount;

        for (int iteration = 0; iteration < 24; iteration++)
        {
            await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("success").ConfigureAwait(false);
            using var unusedCancellation = new CancellationTokenSource();
            LlmFitProcessResult result = await new LlmFitProcessRunner()
                .ExecuteAsync(
                    tool.SystemCommand,
                    cancellationToken: unusedCancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(baseline, LlmFitProcessRunner.ActiveMonitorResourceCount);
            unusedCancellation.Cancel();
        }
    }

    [TestMethod]
    public async Task BoundedTextReader_ExactByteCap_RetainsAllWithoutTruncation()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("ABCD");
        using var stream = new MemoryStream(bytes, writable: false);

        BoundedTextCapture capture = await BoundedTextReader.ReadAsync(stream, bytes.Length)
            .ConfigureAwait(false);

        Assert.AreEqual("ABCD", capture.Text);
        Assert.IsFalse(capture.Truncated);
        Assert.AreEqual(stream.Length, stream.Position);
    }

    [TestMethod]
    public async Task BoundedTextReader_ExcessBytes_AreDrainedAndReportedTruncated()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("ABCDEF");
        using var stream = new MemoryStream(bytes, writable: false);

        BoundedTextCapture capture = await BoundedTextReader.ReadAsync(stream, 3)
            .ConfigureAwait(false);

        Assert.AreEqual("ABC", capture.Text);
        Assert.IsTrue(capture.Truncated);
        Assert.AreEqual(stream.Length, stream.Position);
    }

    [TestMethod]
    public async Task BoundedTextReader_ValidMultibyteUtf8_RoundTrips()
    {
        const string Expected = "Gránite 🪨";
        byte[] bytes = Encoding.UTF8.GetBytes(Expected);
        using var stream = new MemoryStream(bytes, writable: false);

        BoundedTextCapture capture = await BoundedTextReader.ReadAsync(stream, bytes.Length)
            .ConfigureAwait(false);

        Assert.AreEqual(Expected, capture.Text);
        Assert.IsFalse(capture.Truncated);
    }

    [TestMethod]
    public async Task BoundedTextReader_CutoffInsideCodePoint_UsesReplacementFallback()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("A€B");
        using var stream = new MemoryStream(bytes, writable: false);

        BoundedTextCapture capture = await BoundedTextReader.ReadAsync(stream, 2)
            .ConfigureAwait(false);

        Assert.AreEqual("A\uFFFD", capture.Text);
        Assert.IsTrue(capture.Truncated);
        Assert.AreEqual(stream.Length, stream.Position);
    }

    [TestMethod]
    public async Task BoundedTextReader_MalformedUtf8_UsesReplacementFallback()
    {
        byte[] bytes = [0x66, 0x80, 0x67];
        using var stream = new MemoryStream(bytes, writable: false);

        BoundedTextCapture capture = await BoundedTextReader.ReadAsync(stream, bytes.Length)
            .ConfigureAwait(false);

        Assert.AreEqual("f\uFFFDg", capture.Text);
        Assert.IsFalse(capture.Truncated);
    }

    [TestMethod]
    public async Task BoundedTextReader_ReturnsPooledBufferCleared()
    {
        var pool = new RecordingArrayPool();
        using var stream = new MemoryStream([0x41], writable: false);

        _ = await BoundedTextReader.ReadAsync(stream, 1, pool).ConfigureAwait(false);

        Assert.IsTrue(pool.ClearArrayRequested);
        Assert.IsTrue(pool.Buffer.All(static value => value == 0));
    }

    [TestMethod]
    public void FakeLlmFitTool_ControlledRootUnset_AllowsLocalPublishResolution()
    {
        Assert.IsNull(FakeLlmFitTool.ResolveControlledRootForTests(configuredRoot: null));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("invalid\0controlled-root")]
    public void FakeLlmFitTool_ControlledRootSetMalformed_ThrowsStableConfigurationFailure(
        string configuredRoot)
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => FakeLlmFitTool.ResolveControlledRootForTests(configuredRoot));

        Assert.AreEqual("The controlled LLM Fit fake tool root is invalid.", exception.Message);
        if (configuredRoot.Length > 0)
        {
            Assert.IsFalse(exception.Message.Contains(configuredRoot, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void FakeLlmFitTool_ControlledRootMissingExecutable_ThrowsStableConfigurationFailure()
    {
        string missingRoot = Path.Combine(
            Path.GetTempPath(),
            "sensitive-missing-controlled-root-" + Guid.NewGuid().ToString("N"));

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => FakeLlmFitTool.ResolveControlledRootForTests(missingRoot));

        Assert.AreEqual("The controlled LLM Fit fake tool root is invalid.", exception.Message);
        Assert.IsFalse(exception.Message.Contains(missingRoot, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task FakeLlmFitTool_ControlledRootValid_ReturnsCanonicalRoot()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("success").ConfigureAwait(false);

        string? resolvedRoot = FakeLlmFitTool.ResolveControlledRootForTests(tool.Root);

        Assert.AreEqual(Path.GetFullPath(tool.Root), resolvedRoot);
    }

    [TestMethod]
    public async Task FakeLlmFitTool_ControlledRootWithReparseContent_ThrowsStableConfigurationFailure()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("success").ConfigureAwait(false);
        string linkPath = Path.Combine(tool.Root, "linked-fixture-content.exe");

        try
        {
            File.CreateSymbolicLink(linkPath, tool.ExecutablePath);
            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
                () => FakeLlmFitTool.ResolveControlledRootForTests(tool.Root));

            Assert.AreEqual("The controlled LLM Fit fake tool root is invalid.", exception.Message);
            Assert.IsFalse(exception.Message.Contains(tool.Root, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(linkPath);
        }
    }

    [TestMethod]
    public async Task FakeLlmFitTool_OwnedDirectoryDeletionFailure_ThrowsStableCleanupFailure()
    {
        string ownedRoot = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-LlmFit-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ownedRoot);
        string lockedPath = Path.Combine(ownedRoot, "locked.bin");

        try
        {
            await using (var lockedFile = new FileStream(
                lockedPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                InvalidOperationException exception = await Assert
                    .ThrowsExactlyAsync<InvalidOperationException>(
                        () => FakeLlmFitTool.DeleteOwnedDirectoryForTestsAsync(ownedRoot))
                    .ConfigureAwait(false);

                Assert.AreEqual("The owned fake tool directory could not be cleaned up.", exception.Message);
                Assert.IsFalse(exception.Message.Contains(ownedRoot, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            if (Directory.Exists(ownedRoot))
            {
                await FakeLlmFitTool.DeleteOwnedDirectoryForTestsAsync(ownedRoot).ConfigureAwait(false);
            }
        }
    }

    [TestMethod]
    public async Task FakeLlmFitTool_OwnedDirectoryDeletion_NormalizesReadOnlyContent()
    {
        string ownedRoot = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-LlmFit-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ownedRoot);
        string readOnlyPath = Path.Combine(ownedRoot, "read-only.bin");
        await File.WriteAllTextAsync(readOnlyPath, "owned").ConfigureAwait(false);
        File.SetAttributes(readOnlyPath, FileAttributes.ReadOnly);

        try
        {
            await FakeLlmFitTool.DeleteOwnedDirectoryForTestsAsync(ownedRoot).ConfigureAwait(false);
            Assert.IsFalse(Directory.Exists(ownedRoot));
        }
        finally
        {
            if (Directory.Exists(ownedRoot))
            {
                if (File.Exists(readOnlyPath))
                {
                    File.SetAttributes(readOnlyPath, FileAttributes.Normal);
                }

                Directory.Delete(ownedRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task OwnedObservedProcess_CaptureRejectsMismatchedExecutableIdentity()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("success").ConfigureAwait(false);
        using Process currentProcess = Process.GetCurrentProcess();
        OwnedObservedProcess? observedProcess = null;

        try
        {
            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
                () => observedProcess = OwnedObservedProcess.Capture(currentProcess.Id, tool));

            Assert.AreEqual("The owned process identity could not be verified.", exception.Message);
        }
        finally
        {
            if (observedProcess is not null)
            {
                await observedProcess.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static LlmFitProcessRunner CreateShortTimeoutRunner()
    {
        return new LlmFitProcessRunner(TestTimeout);
    }

    private static async Task CaptureCleanupFailureAsync(
        List<Exception> failures,
        Func<Task> cleanup)
    {
        try
        {
            await cleanup().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static Task CaptureOwnedProcessCleanupFailureAsync(
        List<Exception> failures,
        OwnedObservedProcess? process)
    {
        return CaptureCleanupFailureAsync(
            failures,
            async () =>
            {
                if (process is not null)
                {
                    await process.DisposeAsync().ConfigureAwait(false);
                }
            });
    }

    private static void RethrowFailures(List<Exception> failures)
    {
        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(failures);
        }
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

    private sealed class OwnedObservedProcess : IAsyncDisposable
    {
        private readonly string _expectedExecutablePath;
        private readonly DateTime _startTimeUtc;
        private readonly Process _process;

        private OwnedObservedProcess(Process process, string expectedExecutablePath, DateTime startTimeUtc)
        {
            _process = process;
            _expectedExecutablePath = expectedExecutablePath;
            _startTimeUtc = startTimeUtc;
        }

        internal int ProcessId => _process.Id;

        internal static OwnedObservedProcess Capture(int processId, FakeLlmFitTool tool)
        {
            Process? process = null;
            try
            {
                process = Process.GetProcessById(processId);
                var observedProcess = new OwnedObservedProcess(
                    process,
                    Path.GetFullPath(tool.ExecutablePath),
                    process.StartTime.ToUniversalTime());
                var verificationWindow = Stopwatch.StartNew();
                while (verificationWindow.Elapsed < TimeSpan.FromMilliseconds(250))
                {
                    if (observedProcess.IdentityStillMatches())
                    {
                        return observedProcess;
                    }

                    if (process.HasExited)
                    {
                        break;
                    }

                    Thread.Sleep(TimeSpan.FromMilliseconds(10));
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                InvalidOperationException or
                Win32Exception or
                NotSupportedException or
                UnauthorizedAccessException)
            {
                // Identity validation below returns a stable test-harness failure.
            }

            process?.Dispose();
            throw new InvalidOperationException("The owned process identity could not be verified.");
        }

        internal static OwnedObservedProcess? TryCapture(int processId, FakeLlmFitTool tool)
        {
            try
            {
                return Capture(processId, tool);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        internal bool WaitForExit(TimeSpan timeout)
        {
            return _process.WaitForExit((int)timeout.TotalMilliseconds);
        }

        internal async Task AssertExitedAsync(TimeSpan timeout)
        {
            var elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < timeout)
            {
                try
                {
                    if (_process.HasExited || !IdentityStillMatches())
                    {
                        return;
                    }
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException or
                    Win32Exception or
                    NotSupportedException)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
            }

            Assert.Fail("The verified owned process remained alive after bounded cleanup.");
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_process.HasExited && IdentityStillMatches())
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync(CancellationToken.None)
                        .WaitAsync(TimeSpan.FromSeconds(3))
                        .ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                // The retained owned process exited or could no longer be
                // reverified during defensive cleanup.
            }
            finally
            {
                _process.Dispose();
            }
        }

        private bool IdentityStillMatches()
        {
            _process.Refresh();
            string? imagePath = _process.MainModule?.FileName;
            return _process.StartTime.ToUniversalTime() == _startTimeUtc &&
                imagePath is not null &&
                string.Equals(
                    Path.GetFullPath(imagePath),
                    _expectedExecutablePath,
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class RecordingArrayPool : ArrayPool<byte>
    {
        internal byte[] Buffer { get; } = Enumerable.Repeat((byte)0xA5, 81_920).ToArray();

        internal bool ClearArrayRequested { get; private set; }

        public override byte[] Rent(int minimumLength)
        {
            Assert.IsTrue(minimumLength <= Buffer.Length);
            return Buffer;
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            Assert.AreSame(Buffer, array);
            ClearArrayRequested = clearArray;
            if (clearArray)
            {
                Array.Clear(array);
            }
        }
    }
}
#pragma warning restore CA1707
