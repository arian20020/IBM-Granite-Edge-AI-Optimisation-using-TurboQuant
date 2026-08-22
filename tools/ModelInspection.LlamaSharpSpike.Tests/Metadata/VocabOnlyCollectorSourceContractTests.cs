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

        Assert.AreEqual(
            5,
            CountOccurrences(
                probe[probe.IndexOf(
                    "VocabOnlyProbePhase.ReadModelConfiguration",
                    StringComparison.Ordinal)..tokenizerPhase],
                "operationCancellation.Token.ThrowIfCancellationRequested();"),
            "Stage 2 must check before native work, after selection, after load, " +
            "after configuration, and after completion.");
        Assert.AreEqual(
            3,
            CountOccurrences(
                probe[tokenizerPhase..structurePhase],
                "operationCancellation.Token.ThrowIfCancellationRequested();"),
            "Stage 3 must check before/after tokenizer work and after completion.");
        Assert.AreEqual(
            3,
            CountOccurrences(
                probe[stageFourBodyStart..stageFourBodyEnd],
                "operationCancellation.Token.ThrowIfCancellationRequested();"),
            "Stage 4 must check before/after structure work and after cleanup.");

        int successfulCompletion = probe.IndexOf(
            "completionStatus = VocabOnlyProbeCompletionStatus.Succeeded;",
            stageFourBodyEnd,
            StringComparison.Ordinal);
        AssertBetween(
            probe,
            "operationCancellation.Token.ThrowIfCancellationRequested();",
            stageFourBodyEnd,
            successfulCompletion);

        int nativeLoad = probe.IndexOf(
            ".LoadFromFileAsync(",
            StringComparison.Ordinal);
        int configurationCollection = probe.IndexOf(
            ".CollectConfiguration(",
            nativeLoad,
            StringComparison.Ordinal);
        int nativeLoadCompletion = probe.IndexOf(
            ".ConfigureAwait(false);",
            nativeLoad,
            StringComparison.Ordinal);
        int nativeTimeoutDisposal = probe.IndexOf(
            "nativeLoadCancellation.Dispose();",
            nativeLoadCompletion,
            StringComparison.Ordinal);
        int postLoadStopwatch = probe.IndexOf(
            "loadStopwatch.Stop();",
            nativeLoadCompletion,
            StringComparison.Ordinal);

        AssertBetween(
            probe,
            "nativeLoadCancellation.Token,",
            nativeLoad,
            nativeLoadCompletion);
        AssertBetween(
            probe,
            "finally",
            nativeLoadCompletion,
            nativeTimeoutDisposal);
        Assert.IsTrue(
            nativeTimeoutDisposal > nativeLoadCompletion &&
            nativeTimeoutDisposal < postLoadStopwatch,
            "The native-only timeout must be disposed immediately after the load await.");
        AssertBetween(
            probe,
            "operationCancellation.Token.ThrowIfCancellationRequested();",
            nativeTimeoutDisposal,
            configurationCollection);
        Assert.AreEqual(
            0,
            CountOccurrences(
                probe[nativeLoadCompletion..configurationCollection],
                "nativeLoadCancellation.Token.ThrowIfCancellationRequested();"),
            "Post-load work must observe only caller/post-preflight cancellation.");
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

    private static int CountOccurrences(string source, string expression)
    {
        int count = 0;
        int startIndex = 0;

        while ((startIndex = source.IndexOf(
                   expression,
                   startIndex,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += expression.Length;
        }

        return count;
    }
}
