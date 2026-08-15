using System.Diagnostics;
using System.Text;
using HardwareInspection.LlmFitSpike.Execution;
using HardwareInspection.LlmFitSpike.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HardwareInspection.LlmFitSpike.Tests.Execution;

[TestClass]
[DoNotParallelize]
[TestCategory("Deterministic")]
#pragma warning disable CA1707 // Test names intentionally encode the required behavior.
public sealed class TcpListenerObserverTests
{
    private static readonly string[] NetstatArguments = ["-a", "-n", "-o", "-p", "tcp"];
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    [TestMethod]
    public async Task ObserveAsync_SuccessMode_RecordsNoDashboardPort()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("success")
            .ConfigureAwait(false);
        var observer = new TcpListenerObserver(Path.GetFileName(tool.ExecutablePath), PollInterval);

        LlmFitProcessResult execution = await new LlmFitProcessRunner()
            .ExecuteAsync(
                tool.SystemCommand,
                TimeSpan.FromSeconds(5),
                observer.ObserveWhileRunningAsync)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);
        LlmFitProcessObservation observation = await observer.CompleteAsync()
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);

        Assert.IsTrue(execution.Succeeded);
        Assert.IsFalse(execution.ObserverFailed);
        Assert.IsFalse(observation.CandidateSocketObserved);
        Assert.IsFalse(observation.DashboardPortObserved);
        Assert.HasCount(0, observation.CandidateListeningPorts);
        Assert.IsFalse(observation.CandidateProcessRemainedAfterExit);
    }

    [TestMethod]
    public async Task ObserveAsync_DashboardMode_DetectsPort8787()
    {
        using Process currentProcess = Process.GetCurrentProcess();
        var sampleCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new TcpListenerObserver(
            currentProcess.ProcessName + ".exe",
            PollInterval,
            _ =>
            {
                sampleCaptured.TrySetResult();
                return Task.FromResult(
                    $"  TCP    127.0.0.1:8787    0.0.0.0:0    LISTENING    {currentProcess.Id}");
            },
            TimeSpan.FromMilliseconds(100));
        using var cancellation = new CancellationTokenSource();

        Task observationTask = observer.ObserveWhileRunningAsync(
            currentProcess.Id,
            cancellation.Token);
        await sampleCaptured.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
                () => observationTask.WaitAsync(TimeSpan.FromSeconds(2)))
            .ConfigureAwait(false);

        LlmFitProcessObservation observation = await observer.CompleteAsync()
            .WaitAsync(TimeSpan.FromSeconds(2))
            .ConfigureAwait(false);

        Assert.IsTrue(observation.CandidateSocketObserved);
        Assert.IsTrue(observation.DashboardPortObserved);
        CollectionAssert.AreEqual(
            new[] { TcpListenerObserver.LlmFitDashboardPort },
            observation.CandidateListeningPorts.ToArray());
        Assert.IsFalse(observation.CandidateProcessRemainedAfterExit);
    }

    [TestMethod]
    public async Task ObserveAsync_AfterTimeout_RecordsNoNewCandidateProcess()
    {
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("spawn-child")
            .ConfigureAwait(false);
        var observer = new TcpListenerObserver(Path.GetFileName(tool.ExecutablePath), PollInterval);
        var runner = new LlmFitProcessRunner(TimeSpan.FromMilliseconds(500));

        LlmFitProcessResult execution = await runner
            .ExecuteAsync(
                tool.SystemCommand,
                TimeSpan.FromSeconds(1),
                observer.ObserveWhileRunningAsync)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);
        LlmFitProcessObservation observation = await observer.CompleteAsync()
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);

        Assert.IsTrue(execution.TimedOut);
        Assert.IsFalse(execution.ObserverFailed);
        Assert.IsTrue(int.TryParse(execution.StandardOutput.Trim(), out int childProcessId));
        Assert.IsGreaterThan(0, childProcessId);
        Assert.IsGreaterThan(0, observer.SampleCount);
        Assert.IsFalse(observation.CandidateProcessRemainedAfterExit);
    }

    [TestMethod]
    public async Task ObserveAsync_EstablishedRemote8787_IsSocketButNotDashboard()
    {
        using Process currentProcess = Process.GetCurrentProcess();
        var sampleCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new TcpListenerObserver(
            currentProcess.ProcessName + ".exe",
            PollInterval,
            _ =>
            {
                sampleCaptured.TrySetResult();
                return Task.FromResult(
                    $"TCP 127.0.0.1:50000 127.0.0.1:8787 ESTABLISHED {currentProcess.Id}");
            },
            TimeSpan.FromMilliseconds(100));
        using var cancellation = new CancellationTokenSource();

        Task observationTask = observer.ObserveWhileRunningAsync(currentProcess.Id, cancellation.Token);
        await sampleCaptured.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => observationTask).ConfigureAwait(false);
        LlmFitProcessObservation observation = await observer.CompleteAsync().ConfigureAwait(false);

        Assert.IsTrue(observation.CandidateSocketObserved);
        Assert.IsFalse(observation.DashboardPortObserved);
        Assert.HasCount(0, observation.CandidateListeningPorts);
    }

    [TestMethod]
    public async Task ObserveAsync_PidReusedDuringSample_IsNotAttributed()
    {
        using Process currentProcess = Process.GetCurrentProcess();
        const int ReusedProcessId = 4242;
        var snapshots = new Queue<IReadOnlyList<CandidateProcessIdentity>>(
        [
            [new CandidateProcessIdentity(ReusedProcessId, 1)],
            [new CandidateProcessIdentity(ReusedProcessId, 2)],
            [new CandidateProcessIdentity(ReusedProcessId, 3)],
            [new CandidateProcessIdentity(ReusedProcessId, 1)],
        ]);
        var sampleCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new TcpListenerObserver(
            "fixture.exe",
            PollInterval,
            _ =>
            {
                sampleCaptured.TrySetResult();
                return Task.FromResult($"TCP 127.0.0.1:8787 0.0.0.0:0 LISTENING {ReusedProcessId}");
            },
            TimeSpan.FromMilliseconds(100),
            () => snapshots.Count > 1 ? snapshots.Dequeue() : snapshots.Peek(),
            _ => new CandidateProcessIdentity(currentProcess.Id, 99));
        using var cancellation = new CancellationTokenSource();

        Task observationTask = observer.ObserveWhileRunningAsync(currentProcess.Id, cancellation.Token);
        await sampleCaptured.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => observationTask).ConfigureAwait(false);
        LlmFitProcessObservation observation = await observer.CompleteAsync().ConfigureAwait(false);

        Assert.IsFalse(observation.CandidateSocketObserved);
        Assert.IsFalse(observation.DashboardPortObserved);
    }

    [TestMethod]
    public async Task ObserveAsync_UnrelatedProcessOwns8787_IsNotAttributed()
    {
        using Process currentProcess = Process.GetCurrentProcess();
        var rootIdentity = new CandidateProcessIdentity(currentProcess.Id, 99);
        var sampleCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int snapshotNumber = 0;
        var observer = new TcpListenerObserver(
            "fixture.exe",
            PollInterval,
            _ =>
            {
                sampleCaptured.TrySetResult();
                return Task.FromResult("TCP 127.0.0.1:8787 0.0.0.0:0 LISTENING 4242");
            },
            TimeSpan.FromMilliseconds(100),
            () => Interlocked.Increment(ref snapshotNumber) == 1 ? [] : [rootIdentity],
            _ => rootIdentity);
        using var cancellation = new CancellationTokenSource();

        Task observationTask = observer.ObserveWhileRunningAsync(currentProcess.Id, cancellation.Token);
        await sampleCaptured.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => observationTask).ConfigureAwait(false);
        LlmFitProcessObservation observation = await observer.CompleteAsync().ConfigureAwait(false);

        Assert.IsFalse(observation.CandidateSocketObserved);
        Assert.IsFalse(observation.DashboardPortObserved);
        Assert.HasCount(0, observation.CandidateListeningPorts);
    }

    [TestMethod]
    public async Task CompleteAsync_ResidualProcessPersists_ReturnsFrozenIdempotentObservation()
    {
        using Process currentProcess = Process.GetCurrentProcess();
        var identity = new CandidateProcessIdentity(currentProcess.Id, 99);
        int snapshotNumber = 0;
        var sampleCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new TcpListenerObserver(
            "fixture.exe",
            PollInterval,
            _ =>
            {
                sampleCaptured.TrySetResult();
                return Task.FromResult(string.Empty);
            },
            TimeSpan.FromMilliseconds(100),
            () => Interlocked.Increment(ref snapshotNumber) == 1 ? [] : [identity],
            _ => identity);
        using var cancellation = new CancellationTokenSource();

        Task observationTask = observer.ObserveWhileRunningAsync(currentProcess.Id, cancellation.Token);
        await sampleCaptured.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => observationTask).ConfigureAwait(false);

        Task<LlmFitProcessObservation> firstCompletion = observer.CompleteAsync();
        Task<LlmFitProcessObservation> secondCompletion = observer.CompleteAsync();
        Assert.AreSame(firstCompletion, secondCompletion);
        LlmFitProcessObservation observation = await firstCompletion.ConfigureAwait(false);
        Assert.IsTrue(observation.CandidateProcessRemainedAfterExit);
    }

    [TestMethod]
    public async Task CaptureNetstatAsync_FixedSystem32Command_DrainsBoundedStreams()
    {
        var process = FakeNetstatProcess.Exited(
            standardOutput: "TCP 127.0.0.1:80 0.0.0.0:0 LISTENING 42",
            standardError: string.Empty,
            exitCode: 0);
        ProcessStartInfo? observedStartInfo = null;

        string output = await TcpListenerObserver.CaptureNetstatForTestsAsync(
                startInfo =>
                {
                    observedStartInfo = startInfo;
                    return process;
                },
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(100),
                maximumBytesPerStream: 1024,
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual("TCP 127.0.0.1:80 0.0.0.0:0 LISTENING 42", output);
        Assert.IsNotNull(observedStartInfo);
        Assert.AreEqual(
            Path.GetFullPath(Path.Combine(Environment.SystemDirectory, "netstat.exe")),
            observedStartInfo.FileName);
        Assert.AreEqual(Path.GetFullPath(Environment.SystemDirectory), observedStartInfo.WorkingDirectory);
        CollectionAssert.AreEqual(
            NetstatArguments,
            observedStartInfo.ArgumentList.ToArray());
        Assert.IsFalse(observedStartInfo.UseShellExecute);
        Assert.IsTrue(observedStartInfo.RedirectStandardOutput);
        Assert.IsTrue(observedStartInfo.RedirectStandardError);
        Assert.IsTrue(process.Disposed);
    }

    [TestMethod]
    public async Task CaptureNetstatAsync_NonzeroOrTruncatedOutput_FailsClosed()
    {
        FakeNetstatProcess[] invalidProcesses =
        [
            FakeNetstatProcess.Exited("output", "error", exitCode: 23),
            FakeNetstatProcess.Exited("output longer than cap", string.Empty, exitCode: 0),
        ];

        for (int index = 0; index < invalidProcesses.Length; index++)
        {
            int maximumBytes = index == 0 ? 1024 : 4;
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => TcpListenerObserver.CaptureNetstatForTestsAsync(
                        _ => invalidProcesses[index],
                        TimeSpan.FromMilliseconds(100),
                        TimeSpan.FromMilliseconds(100),
                        maximumBytes,
                        CancellationToken.None))
                .ConfigureAwait(false);
            Assert.IsTrue(invalidProcesses[index].Disposed);
        }
    }

    [TestMethod]
    public async Task CaptureNetstatAsync_PostStartSetupFailure_KillsAndAwaitsOwnedHelper()
    {
        var process = new FakeNetstatProcess
        {
            ThrowOnStandardOutputAccess = true,
            CompleteExitWhenKilled = true,
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => TcpListenerObserver.CaptureNetstatForTestsAsync(
                    _ => process,
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(100),
                    maximumBytesPerStream: 1024,
                    CancellationToken.None))
            .ConfigureAwait(false);

        Assert.IsTrue(process.StartCalled);
        Assert.IsTrue(process.KillCalled);
        Assert.IsTrue(process.WaitForExitCalled);
        Assert.IsTrue(process.Disposed);
    }

    [TestMethod]
    public async Task CaptureNetstatAsync_UnkillableHelper_FailsWithinCleanupDeadline()
    {
        var process = new FakeNetstatProcess
        {
            ThrowOnStandardOutputAccess = true,
            CompleteExitWhenKilled = false,
        };
        var elapsed = Stopwatch.StartNew();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => TcpListenerObserver.CaptureNetstatForTestsAsync(
                    _ => process,
                    TimeSpan.FromMilliseconds(50),
                    TimeSpan.FromMilliseconds(50),
                    maximumBytesPerStream: 1024,
                    CancellationToken.None))
            .ConfigureAwait(false);

        Assert.IsLessThan(TimeSpan.FromSeconds(1), elapsed.Elapsed);
        Assert.IsTrue(process.KillCalled);
        Assert.IsTrue(process.Disposed);
    }

    [TestMethod]
    public async Task CaptureNetstatAsync_HelperTimeout_KillsAndAwaitsOwnedHelper()
    {
        var process = new FakeNetstatProcess { CompleteExitWhenKilled = true };

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => TcpListenerObserver.CaptureNetstatForTestsAsync(
                    _ => process,
                    TimeSpan.FromMilliseconds(50),
                    TimeSpan.FromMilliseconds(100),
                    maximumBytesPerStream: 1024,
                    CancellationToken.None))
            .ConfigureAwait(false);

        Assert.AreEqual("The TCP observation helper exceeded its deadline.", exception.Message);
        Assert.IsTrue(process.KillCalled);
        Assert.IsTrue(process.WaitForExitCalled);
        Assert.IsTrue(process.Disposed);
    }

    [TestMethod]
    public async Task CaptureNetstatAsync_CallerCancellation_KillsBeforePropagating()
    {
        var process = new FakeNetstatProcess { CompleteExitWhenKilled = true };
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<OperationCanceledException>(
                () => TcpListenerObserver.CaptureNetstatForTestsAsync(
                    _ =>
                    {
                        cancellation.Cancel();
                        return process;
                    },
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(100),
                    maximumBytesPerStream: 1024,
                    cancellation.Token))
            .ConfigureAwait(false);

        Assert.IsTrue(process.StartCalled);
        Assert.IsTrue(process.KillCalled);
        Assert.IsTrue(process.WaitForExitCalled);
        Assert.IsTrue(process.Disposed);
    }

    [TestMethod]
    public async Task CaptureNetstatAsync_BlockingStart_ReturnsBoundedAndCleansLateStart()
    {
        var process = new FakeNetstatProcess
        {
            BlockStart = true,
            CompleteExitWhenKilled = true,
        };
        var elapsed = Stopwatch.StartNew();
        Task<string> capture = Task.Run(
            () => TcpListenerObserver.CaptureNetstatForTestsAsync(
                _ => process,
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromMilliseconds(50),
                maximumBytesPerStream: 1024,
                CancellationToken.None));

        try
        {
            await process.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => capture.WaitAsync(TimeSpan.FromMilliseconds(500)))
                .ConfigureAwait(false);

            Assert.IsLessThan(TimeSpan.FromMilliseconds(500), elapsed.Elapsed);
            Assert.IsFalse(process.Disposed);
        }
        finally
        {
            process.ReleaseStart();
            try
            {
                _ = await capture.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // The bounded timeout remains the caller-visible outcome.
            }
        }

        await WaitForConditionAsync(() => process.Disposed, TimeSpan.FromSeconds(2))
            .ConfigureAwait(false);
        Assert.IsTrue(process.KillCalled);
    }

    [TestMethod]
    public async Task CaptureNetstatAsync_BlockingKill_ReturnsBoundedAndDefersDisposal()
    {
        var process = new FakeNetstatProcess
        {
            BlockKill = true,
            CompleteExitWhenKilled = true,
        };
        var elapsed = Stopwatch.StartNew();
        Task<string> capture = TcpListenerObserver.CaptureNetstatForTestsAsync(
            _ => process,
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(50),
            maximumBytesPerStream: 1024,
            CancellationToken.None);

        try
        {
            await process.KillEntered.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => capture.WaitAsync(TimeSpan.FromMilliseconds(500)))
                .ConfigureAwait(false);

            Assert.IsLessThan(TimeSpan.FromMilliseconds(500), elapsed.Elapsed);
            Assert.IsFalse(process.Disposed);
        }
        finally
        {
            process.ReleaseKill();
            try
            {
                _ = await capture.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // The bounded cleanup failure remains the caller-visible outcome.
            }
        }

        await WaitForConditionAsync(() => process.Disposed, TimeSpan.FromSeconds(2))
            .ConfigureAwait(false);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            if (elapsed.Elapsed >= timeout)
            {
                Assert.Fail("The bounded background cleanup did not finish.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
        }
    }

    private sealed class FakeNetstatProcess : INetstatProcess
    {
        private readonly TaskCompletionSource _exit = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _killRelease = new(initialState: false);
        private readonly ManualResetEventSlim _startRelease = new(initialState: false);
        private Stream _standardError = new MemoryStream();
        private Stream _standardOutput = new MemoryStream();
        private int _disposed;

        internal bool CompleteExitWhenKilled { get; init; }

        internal bool BlockKill { get; init; }

        internal bool BlockStart { get; init; }

        internal bool Disposed => Volatile.Read(ref _disposed) != 0;

        internal int ExitCode { get; init; }

        internal bool KillCalled { get; private set; }

        internal bool StartCalled { get; private set; }

        internal TaskCompletionSource KillEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource StartEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool ThrowOnStandardOutputAccess { get; init; }

        internal bool WaitForExitCalled { get; private set; }

        internal void ReleaseKill()
        {
            _killRelease.Set();
        }

        internal void ReleaseStart()
        {
            _startRelease.Set();
        }

        Stream INetstatProcess.StandardError => _standardError;

        Stream INetstatProcess.StandardOutput => ThrowOnStandardOutputAccess
            ? throw new InvalidOperationException("controlled setup failure")
            : _standardOutput;

        int INetstatProcess.ExitCode => ExitCode;

        bool INetstatProcess.HasExited => _exit.Task.IsCompleted;

        internal static FakeNetstatProcess Exited(
            string standardOutput,
            string standardError,
            int exitCode)
        {
            var process = new FakeNetstatProcess
            {
                ExitCode = exitCode,
                _standardOutput = new MemoryStream(Encoding.UTF8.GetBytes(standardOutput), writable: false),
                _standardError = new MemoryStream(Encoding.UTF8.GetBytes(standardError), writable: false),
            };
            process._exit.TrySetResult();
            return process;
        }

        bool INetstatProcess.Start()
        {
            StartCalled = true;
            StartEntered.TrySetResult();
            if (BlockStart)
            {
                _startRelease.Wait();
            }

            return true;
        }

        Task INetstatProcess.WaitForExitAsync()
        {
            WaitForExitCalled = true;
            return _exit.Task;
        }

        void INetstatProcess.KillEntireProcessTree()
        {
            KillCalled = true;
            KillEntered.TrySetResult();
            if (BlockKill)
            {
                _killRelease.Wait();
            }

            if (CompleteExitWhenKilled)
            {
                _exit.TrySetResult();
            }
        }

        public void Dispose()
        {
            _standardOutput.Dispose();
            _standardError.Dispose();
            _killRelease.Dispose();
            _startRelease.Dispose();
            Volatile.Write(ref _disposed, 1);
        }
    }
}
#pragma warning restore CA1707
