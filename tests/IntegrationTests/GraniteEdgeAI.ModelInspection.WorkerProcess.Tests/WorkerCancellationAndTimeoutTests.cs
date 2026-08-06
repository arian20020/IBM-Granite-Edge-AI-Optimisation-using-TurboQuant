using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Proves that caller cancellation, the overall safety timeout, cooperative
/// grace, and forced Job Object termination remain distinct outcomes.
/// </summary>
[TestClass]
public sealed class WorkerCancellationAndTimeoutTests
{
    [TestMethod]
    public async Task CancellationBeforeExecutionThrowsWithoutTerminalClaim()
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        InspectionWorkerClient client = WorkerProcessTestData.CreateClient(
            fixture,
            "cooperative-cancellation");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        try
        {
            _ = await client.ExecuteAsync(
                    WorkerProcessTestData.StartCommand(),
                    progress: null,
                    cancellation.Token)
                .ConfigureAwait(false);
            Assert.Fail("Pre-start cancellation must throw.");
        }
        catch (OperationCanceledException)
        {
            // Expected: no verified worker request existed, so there can be no
            // trusted worker Cancelled terminal result.
        }

        await WorkerProcessTestData.AssertNoFixtureProcessRemainsAsync()
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task CancellationAfterProgressReturnsTrustedCancelledTerminal()
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        InspectionWorkerClient client = WorkerProcessTestData.CreateClient(
            fixture,
            "cooperative-cancellation",
            cancellationGrace: TimeSpan.FromSeconds(2));
        using CancellationTokenSource cancellation = new();
        DelegatingProgress<WorkerProgressMessage> progress = new(
            _ => cancellation.Cancel());

        WorkerClientResult result = await client.ExecuteAsync(
                WorkerProcessTestData.StartCommand(),
                progress,
                cancellation.Token)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        result.Validate();
        Assert.IsNull(result.Failure);
        Assert.IsNotNull(result.TerminalMessage);
        Assert.AreEqual(
            WorkerCompletionStatus.Cancelled,
            result.TerminalMessage.CompletionStatus);
        Assert.AreEqual(3, result.ExitCode);
        Assert.IsFalse(result.ForcedTermination);
        StringAssert.Contains(
            result.RetainedStandardError,
            "FIXTURE:CANCEL_RECEIVED");
        await WorkerProcessTestData.AssertNoFixtureProcessRemainsAsync()
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task IgnoredCancellationForcesWholeJobAndIsNotCancelled()
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        InspectionWorkerClient client = WorkerProcessTestData.CreateClient(
            fixture,
            "ignore-cancellation",
            cancellationGrace: TimeSpan.FromMilliseconds(300));
        using CancellationTokenSource cancellation = new();
        DelegatingProgress<WorkerProgressMessage> progress = new(
            _ => cancellation.Cancel());

        WorkerClientResult result = await client.ExecuteAsync(
                WorkerProcessTestData.StartCommand(),
                progress,
                cancellation.Token)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        result.Validate();
        Assert.IsNull(result.TerminalMessage);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerCancellationForced,
            result.Failure.Code);
        Assert.IsTrue(result.ForcedTermination);
        StringAssert.Contains(
            result.RetainedStandardError,
            "FIXTURE:CANCEL_RECEIVED");
        await WorkerProcessTestData.AssertNoFixtureProcessRemainsAsync()
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task OverallTimeoutRequestsCancellationButKeepsTimeoutPrimary()
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        InspectionWorkerClient client = WorkerProcessTestData.CreateClient(
            fixture,
            "cooperative-cancellation",
            overallTimeout: TimeSpan.FromMilliseconds(350),
            cancellationGrace: TimeSpan.FromSeconds(2));

        WorkerClientResult result = await client.ExecuteAsync(
                WorkerProcessTestData.StartCommand(),
                progress: null,
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        result.Validate();
        Assert.IsNull(result.TerminalMessage);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerOverallTimeout,
            result.Failure.Code);
        Assert.AreEqual(3, result.ExitCode);
        Assert.IsFalse(result.ForcedTermination);
        StringAssert.Contains(
            result.RetainedStandardError,
            "FIXTURE:CANCEL_RECEIVED");
        await WorkerProcessTestData.AssertNoFixtureProcessRemainsAsync()
            .ConfigureAwait(false);
    }
}
