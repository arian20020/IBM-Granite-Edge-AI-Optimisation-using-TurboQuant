using System.Text.Json;
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
    public async Task ProductionWorkerReturnsFixedContinuityMismatchWithoutNativeProgress()
    {
        const string sentinel = "PRODUCTION-WORKER-PRIVATE-SENTINEL";
        string ownedDirectory = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-ProductionWorkerContinuity",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ownedDirectory);
        string modelPath = Path.Combine(
            ownedDirectory,
            sentinel + ".gguf");

        try
        {
            await File.WriteAllBytesAsync(
                    modelPath,
                    [0x47, 0x47, 0x55, 0x46])
                .ConfigureAwait(false);
            var file = new FileInfo(modelPath);
            file.Refresh();

            await using PublishedWorker worker =
                await PublishedWorker.CreateAsync().ConfigureAwait(false);
            InspectionWorkerClient client =
                WorkerProcessTestData.CreateProductionClient(worker);

            WorkerStartInspectionCommand baseline =
                WorkerProcessTestData.StartCommand();
            long intentionallyWrongLength = file.Length + 1;
            WorkerStartInspectionCommand command = baseline with
            {
                ModelPath = modelPath,
                ExpectedFileIdentity = new WorkerExpectedFileIdentity
                {
                    LengthBytes = intentionallyWrongLength,
                    LastWriteTimeUtc = new DateTimeOffset(
                        file.LastWriteTimeUtc,
                        TimeSpan.Zero)
                },
                QuickScan = baseline.QuickScan with
                {
                    FileSizeBytes = intentionallyWrongLength
                }
            };
            var progress = new List<WorkerProgressMessage>();
            WorkerClientResult result = await client.ExecuteAsync(
                    command,
                    new DelegatingProgress<WorkerProgressMessage>(
                        progress.Add),
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
                "MI-OP-MODEL-CONTINUITY-MISMATCH",
                result.TerminalMessage.OperationalFailure?.Code);
            Assert.AreEqual(
                "The selected model changed after it was prepared for inspection.",
                result.TerminalMessage.OperationalFailure?.Message);
            Assert.AreEqual(1, result.ExitCode);
            Assert.IsFalse(result.ForcedTermination);
            Assert.AreEqual(string.Empty, result.RetainedStandardError);
            Assert.IsFalse(result.StandardErrorTruncated);
            Assert.IsFalse(result.StandardErrorInvalidUtf8Detected);
            Assert.AreEqual(0, result.SecondaryDiagnostics.Count);
            Assert.AreEqual(2, progress.Count);
            Assert.AreEqual(
                WorkerStage.CheckModelPackage,
                progress[0].Stage);
            Assert.AreEqual(WorkerStageStatus.Active, progress[0].StageStatus);
            Assert.AreEqual(
                WorkerStage.CheckModelPackage,
                progress[1].Stage);
            Assert.AreEqual(WorkerStageStatus.Failed, progress[1].StageStatus);
            Assert.IsTrue(
                progress.All(message => message.StageFraction is null));
            Assert.IsFalse(
                progress.Any(
                    message =>
                        message.Stage == WorkerStage.ReadModelConfiguration));

            string publicEvidence =
                JsonSerializer.Serialize(result.TerminalMessage) +
                result.RetainedStandardError;
            Assert.IsFalse(
                publicEvidence.Contains(
                    command.ModelPath,
                    StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(
                publicEvidence.Contains(
                    Path.GetFileName(command.ModelPath),
                    StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(
                result.RetainedStandardError.Contains(
                    command.RequestId.ToString(),
                    StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(
                publicEvidence.Contains(
                    sentinel,
                    StringComparison.Ordinal));
        }
        finally
        {
            await WorkerProcessTestData
                .AssertNoProductionWorkerProcessRemainsAsync()
                .ConfigureAwait(false);

            string expectedParent = Path.GetFullPath(
                Path.Combine(
                    Path.GetTempPath(),
                    "GraniteEdgeAI-ProductionWorkerContinuity"));
            string fullOwnedDirectory = Path.GetFullPath(ownedDirectory);
            if (fullOwnedDirectory.StartsWith(
                    expectedParent + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(fullOwnedDirectory))
            {
                Directory.Delete(fullOwnedDirectory, recursive: true);
            }
        }
    }
}
