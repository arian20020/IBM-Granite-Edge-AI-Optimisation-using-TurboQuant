using System.Text;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Proves that stderr is drained concurrently through EOF while only a bounded,
/// decoded diagnostic snapshot is retained.
/// </summary>
[TestClass]
public sealed class WorkerStandardErrorTests
{
    [TestMethod]
    public async Task FloodedAndInvalidStderrCannotDeadlockOrEscapeBounds()
    {
        const int RetentionLimit = 4 * 1024;
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        InspectionWorkerClient client = WorkerProcessTestData.CreateClient(
            fixture,
            "flood-stderr",
            maximumRetainedStandardErrorBytes: RetentionLimit);

        WorkerClientResult result = await client.ExecuteAsync(
                WorkerProcessTestData.StartCommand(),
                progress: null,
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        result.Validate();
        Assert.IsNull(result.Failure);
        Assert.IsNotNull(result.TerminalMessage);
        Assert.AreEqual(
            WorkerCompletionStatus.OperationalFailure,
            result.TerminalMessage.CompletionStatus);
        Assert.IsTrue(result.StandardErrorTruncated);
        Assert.IsTrue(result.StandardErrorInvalidUtf8Detected);
        Assert.IsTrue(
            Encoding.UTF8.GetByteCount(result.RetainedStandardError) <=
            RetentionLimit);
        await WorkerProcessTestData.AssertNoFixtureProcessRemainsAsync()
            .ConfigureAwait(false);
    }
}
