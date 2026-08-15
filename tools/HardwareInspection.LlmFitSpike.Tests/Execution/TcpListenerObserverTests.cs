using System.Diagnostics;
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
        await using FakeLlmFitTool tool = await FakeLlmFitTool.CreateAsync("sleep")
            .ConfigureAwait(false);
        var observer = new TcpListenerObserver(Path.GetFileName(tool.ExecutablePath), PollInterval);
        var runner = new LlmFitProcessRunner(TimeSpan.FromMilliseconds(500));

        LlmFitProcessResult execution = await runner
            .ExecuteAsync(
                tool.SystemCommand,
                TimeSpan.FromMilliseconds(500),
                observer.ObserveWhileRunningAsync)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);
        LlmFitProcessObservation observation = await observer.CompleteAsync()
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);

        Assert.IsTrue(execution.TimedOut);
        Assert.IsFalse(execution.ObserverFailed);
        Assert.IsFalse(observation.CandidateProcessRemainedAfterExit);
    }
}
#pragma warning restore CA1707
