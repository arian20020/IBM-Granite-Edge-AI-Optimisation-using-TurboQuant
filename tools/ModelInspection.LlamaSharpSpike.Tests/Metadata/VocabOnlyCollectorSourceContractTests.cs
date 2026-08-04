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
            "tools",
            "ModelInspection.LlamaSharpSpike",
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
        string sourcePath = Path.Combine(
            repositoryRoot,
            "tools",
            "ModelInspection.LlamaSharpSpike",
            "ModelProbe",
            "VocabOnlyEvidenceCollector.cs");

        string source = File.ReadAllText(sourcePath);

        StringAssert.Contains(
            source,
            "VocabOnlyMetadataProjection.Create(metadata)");
        StringAssert.Contains(
            source,
            "ChatTemplateEvidenceFactory.Create(metadata)");
    }
}
