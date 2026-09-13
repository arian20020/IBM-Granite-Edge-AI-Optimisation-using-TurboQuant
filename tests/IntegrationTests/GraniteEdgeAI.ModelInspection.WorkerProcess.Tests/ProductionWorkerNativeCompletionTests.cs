using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Proves the published production worker can complete the real CPU/VocabOnly
/// path with a tiny controlled tokenizer-bearing GGUF.
/// </summary>
[TestClass]
public sealed class ProductionWorkerNativeCompletionTests
{
    private const string FixtureFileName =
        "N-001-vocab-only-spm.gguf";
    private const long ExpectedLength = 800;
    private const string ExpectedSha256 =
        "f8bf7ec29124287e0199e24fa2993998a6e110b14cdae2fc7a146d5041b70e0a";
    private const string ExpectedChatTemplateSha256 =
        "c4123e078b4bdc0f5f63fa38eca171496c1d7ee48987353469d9d611d0428e66";
    private const string ExpectedLlamaCppCommit =
        "3f7c29d318e317b63f54c558bc69803963d7d88c";
    private const string ChatTemplate =
        "{% for message in messages %}{{ message['content'] }}{% endfor %}";

    [TestMethod]
    public async Task ProductionWorkerCompletesAllFiveStagesWithControlledNativeFixture()
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "GGUF",
            FixtureFileName);
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The controlled native fixture was not deployed: {fixturePath}");

        FileInfo before = GetFileInfo(fixturePath);
        Assert.AreEqual(ExpectedLength, before.Length);
        string hashBefore = await ComputeSha256Async(fixturePath)
            .ConfigureAwait(false);
        Assert.AreEqual(ExpectedSha256, hashBefore);

        try
        {
            await using PublishedWorker worker =
                await PublishedWorker.CreateAsync().ConfigureAwait(false);
            Assert.AreEqual(
                0,
                Directory.EnumerateFiles(
                        worker.OutputDirectory,
                        "*.gguf",
                        SearchOption.AllDirectories)
                    .Count(),
                "The production worker publish root must never contain a model fixture.");

            InspectionWorkerClient client =
                WorkerProcessTestData.CreateProductionClient(worker);
            WorkerStartInspectionCommand baseline =
                WorkerProcessTestData.StartCommand();
            WorkerStartInspectionCommand command = baseline with
            {
                ModelPath = fixturePath,
                ExpectedFileIdentity = new WorkerExpectedFileIdentity
                {
                    LengthBytes = before.Length,
                    LastWriteTimeUtc = ToUtcOffset(before.LastWriteTimeUtc)
                },
                QuickScan = new WorkerQuickScanSnapshot
                {
                    Format = "GGUF",
                    ModelName = "Controlled VocabOnly SPM fixture",
                    Architecture = "granite",
                    FileSizeBytes = before.Length,
                    GgufVersion = 3
                }
            };
            var progress = new List<WorkerProgressMessage>();

            WorkerClientResult result = await client.ExecuteAsync(
                    command,
                    new DelegatingProgress<WorkerProgressMessage>(progress.Add),
                    CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(30))
                .ConfigureAwait(false);

            result.Validate();
            Assert.IsNull(result.Failure);
            Assert.IsNotNull(result.TerminalMessage);
            WorkerCompletedMessage terminal = result.TerminalMessage;
            Assert.AreEqual(
                WorkerCompletionStatus.Completed,
                terminal.CompletionStatus);
            Assert.IsNull(terminal.OperationalFailure);
            Assert.IsNotNull(terminal.Evidence);
            WorkerInspectionEvidence evidence = terminal.Evidence;
            evidence.Validate();
            Assert.AreEqual(0, result.ExitCode);
            Assert.IsFalse(result.ForcedTermination);
            Assert.AreEqual(string.Empty, result.RetainedStandardError);
            Assert.IsFalse(result.StandardErrorTruncated);
            Assert.IsFalse(result.StandardErrorInvalidUtf8Detected);
            Assert.HasCount(0, result.SecondaryDiagnostics);

            AssertFiveStageProgress(progress, command.RequestId);
            AssertRuntimeEvidence(evidence.Runtime);
            AssertFileEvidence(evidence.ModelFile, before, fixturePath);
            AssertConfigurationEvidence(evidence.Configuration);
            AssertTokenizerEvidence(evidence.Tokenizer);
            AssertChatTemplateEvidence(evidence.ChatTemplate);
            Assert.HasCount(0, evidence.Observations);

            string serializedTerminal = JsonSerializer.Serialize(terminal);
            Assert.IsFalse(
                serializedTerminal.Contains(
                    fixturePath,
                    StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(
                serializedTerminal.Contains(
                    fixturePath.Replace("\\", "\\\\", StringComparison.Ordinal),
                    StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(
                serializedTerminal.Contains(
                    ChatTemplate,
                    StringComparison.Ordinal));
        }
        finally
        {
            await WorkerProcessTestData
                .AssertNoProductionWorkerProcessRemainsAsync()
                .ConfigureAwait(false);

            FileInfo after = GetFileInfo(fixturePath);
            Assert.AreEqual(before.Length, after.Length);
            Assert.AreEqual(before.LastWriteTimeUtc, after.LastWriteTimeUtc);
            Assert.AreEqual(
                hashBefore,
                await ComputeSha256Async(fixturePath).ConfigureAwait(false),
                "The controlled native fixture changed during inspection.");
        }
    }

    private static void AssertFiveStageProgress(
        List<WorkerProgressMessage> progress,
        Guid requestId)
    {
        Assert.IsTrue(progress.Count >= 10);
        foreach (WorkerProgressMessage message in progress)
        {
            message.Validate();
            Assert.AreEqual(requestId, message.RequestId);
            Assert.AreEqual(5, message.TotalStageCount);
        }

        WorkerProgressMessage[] core = progress
            .Where(message => message.StageFraction is null)
            .ToArray();
        (WorkerStage Stage, WorkerStageStatus Status, int Completed)[] expected =
        [
            (WorkerStage.CheckModelPackage, WorkerStageStatus.Active, 0),
            (WorkerStage.CheckModelPackage, WorkerStageStatus.Completed, 1),
            (WorkerStage.ReadModelConfiguration, WorkerStageStatus.Active, 1),
            (WorkerStage.ReadModelConfiguration, WorkerStageStatus.Completed, 2),
            (WorkerStage.ValidateTokenizerAndChatSetup, WorkerStageStatus.Active, 2),
            (WorkerStage.ValidateTokenizerAndChatSetup, WorkerStageStatus.Completed, 3),
            (WorkerStage.ValidateModelStructure, WorkerStageStatus.Active, 3),
            (WorkerStage.ValidateModelStructure, WorkerStageStatus.Completed, 4),
            (WorkerStage.ConfirmCoreRuntimeCompatibility, WorkerStageStatus.Active, 4),
            (WorkerStage.ConfirmCoreRuntimeCompatibility, WorkerStageStatus.Completed, 5)
        ];
        Assert.AreEqual(expected.Length, core.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.AreEqual(expected[index].Stage, core[index].Stage);
            Assert.AreEqual(expected[index].Status, core[index].StageStatus);
            Assert.AreEqual(
                expected[index].Completed,
                core[index].CompletedStageCount);
        }

        int readActiveIndex = progress.IndexOf(core[2]);
        int readCompletedIndex = progress.IndexOf(core[3]);
        for (int index = 0; index < progress.Count; index++)
        {
            WorkerProgressMessage message = progress[index];
            if (message.StageFraction is not double fraction)
            {
                continue;
            }

            Assert.IsTrue(index > readActiveIndex && index < readCompletedIndex);
            Assert.AreEqual(
                WorkerStage.ReadModelConfiguration,
                message.Stage);
            Assert.AreEqual(WorkerStageStatus.Active, message.StageStatus);
            Assert.AreEqual(1, message.CompletedStageCount);
            Assert.IsTrue(double.IsFinite(fraction));
            Assert.IsTrue(fraction is >= 0 and <= 1);
        }
    }

    private static void AssertRuntimeEvidence(WorkerRuntimeIdentity runtime)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(runtime.WorkerVersion));
        Assert.AreEqual(WorkerProtocol.Version, runtime.ProtocolVersion);
        Assert.AreEqual(WorkerProtocol.RuntimeProfile, runtime.RuntimeProfile);
        Assert.AreEqual("0.27.0", runtime.LLamaSharpVersion);
        Assert.AreEqual("0.27.0", runtime.BackendPackageVersion);
        Assert.AreEqual(
            ExpectedLlamaCppCommit,
            runtime.MappedLlamaCppCommit);
        Assert.AreEqual("LLama", runtime.NativeLibraryName);
        Assert.AreEqual("X64", runtime.ProcessArchitecture);
        Assert.AreEqual("VocabOnly", runtime.InspectionMode);
        Assert.IsFalse(runtime.UsesCuda);
        Assert.IsFalse(runtime.UsesVulkan);
        Assert.AreEqual(0, runtime.GpuLayerCount);
    }

    private static void AssertFileEvidence(
        WorkerModelFileEvidence modelFile,
        FileInfo before,
        string fixturePath)
    {
        Assert.AreEqual(FixtureFileName, modelFile.FileName);
        Assert.AreEqual(
            ComputeCanonicalPathSha256(fixturePath),
            modelFile.CanonicalPathSha256);
        Assert.AreEqual(ExpectedLength, modelFile.LengthBefore);
        Assert.AreEqual(ExpectedLength, modelFile.LengthAfter);
        Assert.AreEqual(
            ToUtcOffset(before.LastWriteTimeUtc),
            modelFile.LastWriteTimeBeforeUtc);
        Assert.AreEqual(
            modelFile.LastWriteTimeBeforeUtc,
            modelFile.LastWriteTimeAfterUtc);
        Assert.AreEqual(ExpectedSha256, modelFile.Sha256Before);
        Assert.AreEqual(ExpectedSha256, modelFile.Sha256After);
        Assert.IsTrue(modelFile.IntegrityPreserved);
    }

    private static void AssertConfigurationEvidence(
        WorkerModelConfigurationEvidence configuration)
    {
        Assert.AreEqual("granite", configuration.Architecture);
        Assert.IsNull(configuration.ModelName);
        Assert.IsNull(configuration.FileType);
        Assert.IsNull(configuration.QuantisationVersion);
        Assert.AreEqual("llama", configuration.TokenizerModel);
        Assert.IsNull(configuration.DeclaredContextLength);
        Assert.IsNull(configuration.EmbeddingSize);
        Assert.IsNull(configuration.LayerCount);
        Assert.IsNull(configuration.AttentionHeadCount);
        Assert.IsNull(configuration.KvHeadCount);
        Assert.IsNull(configuration.ParameterCount);
    }

    private static void AssertTokenizerEvidence(
        WorkerTokenizerEvidence tokenizer)
    {
        Assert.AreEqual(8, tokenizer.VocabularyCount);
        Assert.AreEqual("SentencePiece", tokenizer.VocabularyType);
        Assert.IsTrue(tokenizer.TokenizerSmokePassed);
        Assert.AreEqual(1, tokenizer.TokenizerSmokeTokenCount);
        Assert.AreEqual(3, tokenizer.KnownSpecialTokenIds.Count);
        Assert.AreEqual(1, tokenizer.KnownSpecialTokenIds["bos"]);
        Assert.AreEqual(2, tokenizer.KnownSpecialTokenIds["eos"]);
        Assert.AreEqual(3, tokenizer.KnownSpecialTokenIds["newline"]);
    }

    private static void AssertChatTemplateEvidence(
        WorkerChatTemplateEvidence chatTemplate)
    {
        Assert.IsTrue(chatTemplate.Present);
        Assert.AreEqual(65, chatTemplate.LengthCharacters);
        Assert.AreEqual(
            ExpectedChatTemplateSha256,
            chatTemplate.Sha256);
    }

    private static FileInfo GetFileInfo(string path)
    {
        var file = new FileInfo(path);
        file.Refresh();
        return file;
    }

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(value, TimeSpan.Zero);

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeCanonicalPathSha256(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string canonicalPath = OperatingSystem.IsWindows()
            ? fullPath.ToUpperInvariant()
            : fullPath;
        byte[] hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(canonicalPath));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
