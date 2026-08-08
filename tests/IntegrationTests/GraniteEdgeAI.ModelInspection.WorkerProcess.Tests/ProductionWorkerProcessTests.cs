using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Proves the application client can launch and complete a protocol conversation
/// with the production worker apphost rather than only the abnormal test fixture.
/// </summary>
[TestClass]
public sealed class ProductionWorkerProcessTests
{
    [TestMethod]
    public async Task ProductionWorkerReturnsTruthfulUnavailableEngineFailure()
    {
        try
        {
            await using PublishedWorker worker =
                await PublishedWorker.CreateAsync().ConfigureAwait(false);
            InspectionWorkerClient client =
                WorkerProcessTestData.CreateProductionClient(worker);

            WorkerStartInspectionCommand command =
                WorkerProcessTestData.StartCommand();
            WorkerClientResult result = await client.ExecuteAsync(
                    command,
                    progress: null,
                    CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(15))
                .ConfigureAwait(false);

            result.Validate();
            Assert.IsNull(result.Failure);
            Assert.IsNotNull(result.TerminalMessage);
            Assert.AreEqual(
                WorkerCompletionStatus.OperationalFailure,
                result.TerminalMessage.CompletionStatus);
            Assert.AreEqual(
                "MI-OP-ENGINE-NOT-CONFIGURED",
                result.TerminalMessage.OperationalFailure?.Code);
            Assert.AreEqual(1, result.ExitCode);
            Assert.IsFalse(result.ForcedTermination);
            Assert.AreEqual(string.Empty, result.RetainedStandardError);
            Assert.IsFalse(result.StandardErrorTruncated);
            Assert.IsFalse(result.StandardErrorInvalidUtf8Detected);
            Assert.AreEqual(0, result.SecondaryDiagnostics.Count);
            Assert.IsFalse(
                result.RetainedStandardError.Contains(
                    command.ModelPath,
                    StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(
                result.RetainedStandardError.Contains(
                    Path.GetFileName(command.ModelPath),
                    StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(
                result.RetainedStandardError.Contains(
                    command.RequestId.ToString(),
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await WorkerProcessTestData
                .AssertNoProductionWorkerProcessRemainsAsync()
                .ConfigureAwait(false);
        }
    }
}
