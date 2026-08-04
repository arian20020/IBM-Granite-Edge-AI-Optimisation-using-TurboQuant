using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies that VocabOnly structural evidence is projected from GGUF metadata
/// rather than unsafe native hyperparameter accessors.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class VocabOnlyMetadataProjectionTests
{
    [TestMethod]
    public void Create_WithGraniteMetadata_ProjectsArchitectureScopedValues()
    {
        IReadOnlyDictionary<string, string> metadata =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["general.architecture"] = "granite",
                ["general.name"] = "Granite 4.1 3B",
                ["general.file_type"] = "15",
                ["general.quantization_version"] = "2",
                ["tokenizer.ggml.model"] = "gpt2",
                ["granite.context_length"] = "131072",
                ["granite.embedding_length"] = "2560",
                ["granite.block_count"] = "40",
                ["granite.attention.head_count"] = "40",
                ["granite.attention.head_count_kv"] = "8",
                ["general.parameter_count"] = "3000000000"
            };

        VocabOnlyMetadataProjection result =
            VocabOnlyMetadataProjection.Create(metadata);

        Assert.AreEqual("granite", result.Architecture);
        Assert.AreEqual("Granite 4.1 3B", result.ModelName);
        Assert.AreEqual("15", result.FileType);
        Assert.AreEqual("2", result.QuantizationVersion);
        Assert.AreEqual("gpt2", result.TokenizerModel);
        Assert.AreEqual(131072, result.ContextSize);
        Assert.AreEqual(2560, result.EmbeddingSize);
        Assert.AreEqual(40, result.LayerCount);
        Assert.AreEqual(40, result.HeadCount);
        Assert.AreEqual(8, result.KvHeadCount);
        Assert.AreEqual(3000000000UL, result.ParameterCount);
    }

    [TestMethod]
    public void Create_WhenStructuralMetadataIsMissing_ReturnsUnavailableValues()
    {
        IReadOnlyDictionary<string, string> metadata =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["general.architecture"] = "granite",
                ["general.name"] = "Granite"
            };

        VocabOnlyMetadataProjection result =
            VocabOnlyMetadataProjection.Create(metadata);

        Assert.AreEqual("granite", result.Architecture);
        Assert.AreEqual("Granite", result.ModelName);
        Assert.IsNull(result.ContextSize);
        Assert.IsNull(result.EmbeddingSize);
        Assert.IsNull(result.LayerCount);
        Assert.IsNull(result.HeadCount);
        Assert.IsNull(result.KvHeadCount);
        Assert.IsNull(result.ParameterCount);
    }

    [TestMethod]
    public void Create_WithMalformedNumericMetadata_ReturnsUnavailableValues()
    {
        IReadOnlyDictionary<string, string> metadata =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["general.architecture"] = "granite",
                ["granite.context_length"] = "not-a-number",
                ["granite.block_count"] = "-1"
            };

        VocabOnlyMetadataProjection result =
            VocabOnlyMetadataProjection.Create(metadata);

        Assert.IsNull(result.ContextSize);
        Assert.IsNull(result.LayerCount);
    }
}
