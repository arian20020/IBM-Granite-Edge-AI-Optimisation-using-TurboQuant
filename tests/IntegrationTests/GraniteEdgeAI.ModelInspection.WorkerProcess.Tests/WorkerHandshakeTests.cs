using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Proves that the application adapter trusts only one exact first-frame hello
/// and sends the model request only after that identity has been verified.
/// </summary>
[TestClass]
public sealed class WorkerHandshakeTests
{
    [TestMethod]
    public async Task HealthyFixtureReturnsOneTrustedControlledFailure()
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        InspectionWorkerClient client = WorkerProcessTestData.CreateClient(
            fixture,
            "healthy-controlled-failure");

        WorkerClientResult result = await client.ExecuteAsync(
                WorkerProcessTestData.StartCommand(),
                progress: null,
                CancellationToken.None)
            .ConfigureAwait(false);

        result.Validate();
        Assert.IsNull(result.Failure);
        Assert.IsNotNull(result.TerminalMessage);
        Assert.AreEqual(
            WorkerCompletionStatus.OperationalFailure,
            result.TerminalMessage.CompletionStatus);
        Assert.AreEqual(
            "MI-FIXTURE-CONTROLLED-FAILURE",
            result.TerminalMessage.OperationalFailure?.Code);
        Assert.AreEqual(1, result.ExitCode);
        Assert.IsFalse(result.ForcedTermination);
    }

    [TestMethod]
    public async Task WrongWorkerIdentityFailsBeforeRequestConversation()
    {
        WorkerClientResult result = await RunAsync("wrong-worker-id")
            .ConfigureAwait(false);

        AssertFailure(
            result,
            WorkerClientFailureCodes.WorkerHandshakeInvalid);
    }

    [TestMethod]
    public async Task MalformedFirstFrameIsHandshakeFailure()
    {
        WorkerClientResult result = await RunAsync("malformed-hello")
            .ConfigureAwait(false);

        AssertFailure(
            result,
            WorkerClientFailureCodes.WorkerHandshakeInvalid);
    }

    [TestMethod]
    public async Task MissingHelloUsesDedicatedStartupTimeout()
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        InspectionWorkerClient client = WorkerProcessTestData.CreateClient(
            fixture,
            "no-hello",
            startupTimeout: TimeSpan.FromMilliseconds(300));

        WorkerClientResult result = await client.ExecuteAsync(
                WorkerProcessTestData.StartCommand(),
                progress: null,
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);

        AssertFailure(
            result,
            WorkerClientFailureCodes.WorkerHandshakeTimeout);
    }

    private static async Task<WorkerClientResult> RunAsync(string scenario)
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        InspectionWorkerClient client = WorkerProcessTestData.CreateClient(
            fixture,
            scenario);

        return await client.ExecuteAsync(
                WorkerProcessTestData.StartCommand(),
                progress: null,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static void AssertFailure(
        WorkerClientResult result,
        string expectedCode)
    {
        result.Validate();
        Assert.IsNull(result.TerminalMessage);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(expectedCode, result.Failure.Code);
        Assert.IsFalse(
            result.Failure.Message.Contains(
                "model.gguf",
                StringComparison.OrdinalIgnoreCase));
    }
}
