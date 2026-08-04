using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.RealModel;

/// <summary>
/// Verifies sequential child-process probes remain stable and release native
/// resources between runs.
/// </summary>
[TestClass]
[TestCategory("RealModelIntegration")]
public sealed class GraniteVocabOnlyRepeatabilityTests
{
    [TestMethod]
    public async Task Run_ThreeTimesSequentially_ReturnsStableEvidenceAndPreservesModel()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string expectedHash = await ModelFileHash.ComputeSha256Async(
            context.Model.ModelPath,
            CancellationToken.None);
        var stableResults = new List<StableEvidence>();

        for (int run = 1; run <= 3; run++)
        {
            string evidencePath = context.CreateEvidencePath(
                $"repeatability-{run}");

            ProbeExecutionResult process = await new ProbeProcessRunner().RunAsync(
                sandbox.CreateRequest(
                    new[]
                    {
                        "--model", context.Model.ModelPath,
                        "--output", evidencePath
                    },
                    TimeSpan.FromMinutes(2)),
                CancellationToken.None);

            Assert.AreEqual(ProcessTerminationKind.Exited, process.TerminationKind);
            Assert.AreEqual(0, process.ExitCode, $"Repeatability run {run} failed.");

            using JsonDocument document = EvidenceAssertions.LoadJson(evidencePath);
            EvidenceAssertions.AssertSuccessfulGraniteVocabOnly(
                document.RootElement,
                context.Model.Manifest);
            stableResults.Add(StableEvidence.From(document.RootElement));

            string hashAfterRun = await ModelFileHash.ComputeSha256Async(
                context.Model.ModelPath,
                CancellationToken.None);
            Assert.AreEqual(expectedHash, hashAfterRun);
        }

        Assert.AreEqual(stableResults[0], stableResults[1]);
        Assert.AreEqual(stableResults[0], stableResults[2]);
        Assert.AreEqual(
            0,
            Directory.GetFiles(
                context.EvidenceRoot,
                "*.tmp-*",
                SearchOption.AllDirectories).Length);
        EvidenceAssertions.AssertNoGgufFiles(context.EvidenceRoot);
    }

    private sealed record StableEvidence(
        string Architecture,
        string ModelName,
        int ContextSize,
        int EmbeddingSize,
        int LayerCount,
        int HeadCount,
        int KvHeadCount,
        int MetadataCount,
        int VocabularyCount,
        string? ChatTemplateSha256,
        string BeforeSha256,
        string AfterSha256)
    {
        internal static StableEvidence From(JsonElement root)
        {
            JsonElement model = root.GetProperty("modelEvidence");
            JsonElement chatTemplate = model.GetProperty("chatTemplate");

            return new StableEvidence(
                model.GetProperty("architecture").GetString() ?? string.Empty,
                model.GetProperty("modelName").GetString() ?? string.Empty,
                model.GetProperty("contextSize").GetInt32(),
                model.GetProperty("embeddingSize").GetInt32(),
                model.GetProperty("layerCount").GetInt32(),
                model.GetProperty("headCount").GetInt32(),
                model.GetProperty("kvHeadCount").GetInt32(),
                model.GetProperty("metadataCount").GetInt32(),
                model.GetProperty("vocabulary").GetProperty("count").GetInt32(),
                chatTemplate.GetProperty("sha256").GetString(),
                root.GetProperty("beforeSnapshot").GetProperty("sha256").GetString() ?? string.Empty,
                root.GetProperty("afterSnapshot").GetProperty("sha256").GetString() ?? string.Empty);
        }
    }
}
