using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Proves that malformed request-scoped conversations, post-terminal output,
/// missing terminals, and terminal/exit disagreement are never accepted as
/// partial inspection results.
/// </summary>
[TestClass]
public sealed class WorkerProtocolIntegrityTests
{
    [TestMethod]
    public async Task ProgressBeforeStartedIsRejected()
    {
        WorkerClientResult result = await RunAsync("progress-before-started")
            .ConfigureAwait(false);

        AssertFailure(
            result,
            WorkerClientFailureCodes.WorkerProtocolInvalid);
    }

    [TestMethod]
    public async Task WrongRequestIdentityIsRejected()
    {
        WorkerClientResult result = await RunAsync("wrong-request-id")
            .ConfigureAwait(false);

        AssertFailure(
            result,
            WorkerClientFailureCodes.WorkerProtocolInvalid);
    }

    [TestMethod]
    public async Task DuplicateTerminalIsRejectedAfterFirstTerminal()
    {
        WorkerClientResult result = await RunAsync("duplicate-terminal")
            .ConfigureAwait(false);

        AssertFailure(
            result,
            WorkerClientFailureCodes.WorkerProtocolInvalid);
    }

    [TestMethod]
    public async Task ProcessExitWithoutTerminalIsRejected()
    {
        WorkerClientResult result = await RunAsync("exit-without-terminal")
            .ConfigureAwait(false);

        AssertFailure(
            result,
            WorkerClientFailureCodes.WorkerProtocolInvalid);
    }

    [TestMethod]
    public async Task TerminalAndExitCodeMustAgree()
    {
        WorkerClientResult result = await RunAsync("terminal-exit-mismatch")
            .ConfigureAwait(false);

        AssertFailure(
            result,
            WorkerClientFailureCodes.WorkerExitMismatch);
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
            .WaitAsync(TimeSpan.FromSeconds(10))
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
            result.RetainedStandardError.Contains(
                "model.gguf",
                StringComparison.OrdinalIgnoreCase));
    }
}
