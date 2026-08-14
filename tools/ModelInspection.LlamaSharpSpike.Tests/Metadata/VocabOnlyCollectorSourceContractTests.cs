using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Guards against reintroducing native hyperparameter accessors that are unsafe
/// after llama.cpp returns early for vocab_only loading.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class VocabOnlyCollectorSourceContractTests
{
    [TestMethod]
    public void Collector_DoesNotReferenceUnsafeNativeHyperparameterGetters()
    {
        string repositoryRoot = RepositoryPaths.FindRoot();
        string sourcePath = Path.Combine(
            repositoryRoot,
            "runtime",
            "GraniteEdgeAI.ModelInspection.LlamaSharp",
            "ModelProbe",
            "VocabOnlyEvidenceCollector.cs");

        string source = File.ReadAllText(sourcePath);
        string[] forbiddenExpressions =
        {
            "weights.ContextSize",
            "weights.SizeInBytes",
            "weights.ParameterCount",
            "weights.EmbeddingSize",
            "weights.NativeHandle",
            "handle.Description",
            "handle.LayerCount",
            "handle.HeadCount",
            "handle.KVHeadCount",
            "handle.HasEncoder",
            "handle.HasDecoder",
            "handle.IsRecurrent",
            "handle.IsDiffusion",
            "handle.GetTemplate("
        };

        foreach (string forbiddenExpression in forbiddenExpressions)
        {
            Assert.IsFalse(
                source.Contains(
                    forbiddenExpression,
                    StringComparison.Ordinal),
                $"VocabOnly collector must not call {forbiddenExpression}.");
        }
    }

    [TestMethod]
    public void Collector_UsesMetadataProjectionAndChatTemplateFactory()
    {
        string repositoryRoot = RepositoryPaths.FindRoot();
        string collectorPath = Path.Combine(
            repositoryRoot,
            "runtime",
            "GraniteEdgeAI.ModelInspection.LlamaSharp",
            "ModelProbe",
            "VocabOnlyEvidenceCollector.cs");
        string probePath = Path.Combine(
            repositoryRoot,
            "runtime",
            "GraniteEdgeAI.ModelInspection.LlamaSharp",
            "ModelProbe",
            "VocabOnlyModelProbe.cs");

        string collector = File.ReadAllText(collectorPath);
        string probe = File.ReadAllText(probePath);

        StringAssert.Contains(
            collector,
            "VocabOnlyMetadataProjection.Create(metadata)");
        StringAssert.Contains(
            collector,
            "ChatTemplateEvidenceFactory.Create(metadata)");
        StringAssert.Contains(collector, "CollectConfiguration(");
        StringAssert.Contains(collector, "CollectTokenizerAndChat(");
        StringAssert.Contains(collector, "CollectStructure(");
        StringAssert.Contains(collector, "Compose(");

        int tokenizerCollector = collector.IndexOf(
            "CollectTokenizerAndChat(",
            StringComparison.Ordinal);
        int structureCollector = collector.IndexOf(
            "CollectStructure(",
            tokenizerCollector,
            StringComparison.Ordinal);
        AssertBetween(
            collector,
            "RunTokenizerSmoke(weights)",
            tokenizerCollector,
            structureCollector);
        AssertBetween(
            collector,
            "ChatTemplateEvidenceFactory.Create(metadata)",
            tokenizerCollector,
            structureCollector);

        int tokenizerPhase = probe.IndexOf(
            "VocabOnlyProbePhase.ValidateTokenizerAndChatSetup",
            StringComparison.Ordinal);
        Assert.IsTrue(tokenizerPhase >= 0);
        int structurePhase = probe.IndexOf(
            "VocabOnlyProbePhase.ValidateModelStructure",
            tokenizerPhase,
            StringComparison.Ordinal);
        Assert.IsTrue(structurePhase > tokenizerPhase);
        int stageFourLambda = probe.IndexOf(
            "async () =>",
            structurePhase,
            StringComparison.Ordinal);
        int stageFourBodyStart = probe.IndexOf(
            '{',
            stageFourLambda);
        int stageFourBodyEnd = FindMatchingBrace(
            probe,
            stageFourBodyStart);

        Assert.IsTrue(stageFourLambda > structurePhase);
        Assert.IsTrue(stageFourBodyStart > stageFourLambda);
        Assert.IsTrue(stageFourBodyEnd > stageFourBodyStart);
        AssertBetween(
            probe,
            "VocabOnlyEvidenceCollector.CollectTokenizerAndChat(",
            tokenizerPhase,
            structurePhase);
        AssertBetween(
            probe,
            "VocabOnlyEvidenceCollector.CollectStructure(",
            stageFourBodyStart,
            stageFourBodyEnd);
        int stageFourFinally = probe.IndexOf(
            "finally",
            stageFourBodyStart,
            StringComparison.Ordinal);
        Assert.IsTrue(
            stageFourFinally > stageFourBodyStart &&
            stageFourFinally < stageFourBodyEnd,
            "Stage 4 must retain disposal and integrity work in a finally block.");
        AssertBetween(
            probe,
            "loadedWeights.Dispose();",
            stageFourFinally,
            stageFourBodyEnd);
        AssertBetween(
            probe,
            "_snapshotService.CaptureAsync(",
            stageFourFinally,
            stageFourBodyEnd);
        AssertBetween(
            probe,
            "ModelFileIntegrityComparison.Compare(",
            stageFourFinally,
            stageFourBodyEnd);
        AssertBetween(
            probe,
            "throw new FinalIntegrityVerificationException();",
            stageFourFinally,
            stageFourBodyEnd);
        Assert.IsFalse(
            probe.Contains("disposeAttempted", StringComparison.Ordinal),
            "Cleanup must follow actual handle ownership, not an attempted flag.");

        int unavailableCheck = probe.IndexOf(
            "if (!nativeBackendAvailable)",
            StringComparison.Ordinal);
        AssertBetween(
            probe,
            "throw new NativeBackendUnavailableException();",
            unavailableCheck,
            structurePhase);
    }

    private static void AssertBetween(
        string source,
        string expression,
        int lowerExclusive,
        int upperExclusive)
    {
        int index = source.IndexOf(
            expression,
            Math.Max(0, lowerExclusive),
            StringComparison.Ordinal);
        Assert.IsTrue(
            index > lowerExclusive && index < upperExclusive,
            $"Expected '{expression}' inside the required phase boundary.");
    }

    private static int FindMatchingBrace(string source, int openingBrace)
    {
        Assert.IsTrue(openingBrace >= 0);
        int depth = 0;

        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return index;
            }
        }

        Assert.Fail("The Stage 4 delegate has no matching closing brace.");
        return -1;
    }
}
