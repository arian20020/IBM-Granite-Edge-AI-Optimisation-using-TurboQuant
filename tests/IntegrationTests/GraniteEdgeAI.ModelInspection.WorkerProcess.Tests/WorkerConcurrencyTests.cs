using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Proves that each ExecuteAsync call owns an independent request, process,
/// stream set, cancellation state, and Job Object.
/// </summary>
[TestClass]
public sealed class WorkerConcurrencyTests
{
    [TestMethod]
    public async Task CancellingOneSessionDoesNotAffectAnotherSession()
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        InspectionWorkerClient healthyClient = WorkerProcessTestData.CreateClient(
            fixture,
            "healthy-controlled-failure");
        InspectionWorkerClient cancelledClient = WorkerProcessTestData.CreateClient(
            fixture,
            "cooperative-cancellation",
            cancellationGrace: TimeSpan.FromSeconds(2));
        WorkerStartInspectionCommand healthyCommand =
            WorkerProcessTestData.StartCommand();
        WorkerStartInspectionCommand cancelledCommand =
            WorkerProcessTestData.StartCommand();
        using CancellationTokenSource cancellation = new();
        DelegatingProgress<WorkerProgressMessage> progress = new(
            _ => cancellation.Cancel());

        Task<WorkerClientResult> healthyTask = healthyClient.ExecuteAsync(
            healthyCommand,
            progress: null,
            CancellationToken.None);
        Task<WorkerClientResult> cancelledTask = cancelledClient.ExecuteAsync(
            cancelledCommand,
            progress,
            cancellation.Token);

        WorkerClientResult[] results = await Task.WhenAll(
                healthyTask,
                cancelledTask)
            .WaitAsync(TimeSpan.FromSeconds(12))
            .ConfigureAwait(false);
        WorkerClientResult healthy = results[0];
        WorkerClientResult cancelled = results[1];

        healthy.Validate();
        cancelled.Validate();
        Assert.IsNull(healthy.Failure);
        Assert.IsNull(cancelled.Failure);
        Assert.AreEqual(
            healthyCommand.RequestId,
            healthy.TerminalMessage?.RequestId);
        Assert.AreEqual(
            cancelledCommand.RequestId,
            cancelled.TerminalMessage?.RequestId);
        Assert.AreEqual(
            WorkerCompletionStatus.OperationalFailure,
            healthy.TerminalMessage?.CompletionStatus);
        Assert.AreEqual(
            WorkerCompletionStatus.Cancelled,
            cancelled.TerminalMessage?.CompletionStatus);
        Assert.IsFalse(healthy.ForcedTermination);
        Assert.IsFalse(cancelled.ForcedTermination);
        Assert.IsFalse(
            healthy.RetainedStandardError.Contains(
                "FIXTURE:CANCEL_RECEIVED",
                StringComparison.Ordinal));
        StringAssert.Contains(
            cancelled.RetainedStandardError,
            "FIXTURE:CANCEL_RECEIVED");
        await WorkerProcessTestData.AssertNoFixtureProcessRemainsAsync()
            .ConfigureAwait(false);
    }
}
