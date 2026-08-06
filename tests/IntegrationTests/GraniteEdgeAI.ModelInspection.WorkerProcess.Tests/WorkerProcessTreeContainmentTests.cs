using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Proves that root-process exit is not treated as complete cleanup while a
/// child remains assigned to the worker's Job Object.
/// </summary>
[TestClass]
public sealed class WorkerProcessTreeContainmentTests
{
    [TestMethod]
    public async Task RootExitWithLiveChildIsProcessTreeIntegrityFailure()
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        InspectionWorkerClient client = WorkerProcessTestData.CreateClient(
            fixture,
            "exit-root-with-live-child");

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
            WorkerClientFailureCodes.WorkerProcessTreeIntegrityFailed,
            result.Failure.Code);
        Assert.IsTrue(result.ForcedTermination);
        StringAssert.Contains(
            result.RetainedStandardError,
            "FIXTURE:CHILD_STARTED:");
        await WorkerProcessTestData.AssertNoFixtureProcessRemainsAsync()
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task TimeoutTerminatesRootAndWaitingChildTogether()
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        InspectionWorkerClient client = WorkerProcessTestData.CreateClient(
            fixture,
            "spawn-child-and-wait",
            overallTimeout: TimeSpan.FromMilliseconds(400),
            cancellationGrace: TimeSpan.FromMilliseconds(250));

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
        Assert.IsTrue(result.ForcedTermination);
        StringAssert.Contains(
            result.RetainedStandardError,
            "FIXTURE:CHILD_STARTED:");
        await WorkerProcessTestData.AssertNoFixtureProcessRemainsAsync()
            .ConfigureAwait(false);
    }
}
