using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;
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
        IReadOnlyDictionary<string, string> metadata = CompleteGraniteMetadata();

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
    public void Create_WhenArchitectureIsMissing_ReturnsUnavailableStructure()
    {
        var metadata = new Dictionary<string, string>
        {
            ["granite.block_count"] = "40"
        };

        VocabOnlyMetadataProjection result =
            VocabOnlyMetadataProjection.Create(metadata);

        Assert.IsNull(result.Architecture);
        Assert.IsNull(result.LayerCount);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WhenArchitectureIsBlank_ReturnsUnavailableStructure(
        string architecture)
    {
        var metadata = new Dictionary<string, string>
        {
            ["general.architecture"] = architecture,
            ["granite.block_count"] = "40"
        };

        VocabOnlyMetadataProjection result =
            VocabOnlyMetadataProjection.Create(metadata);

        Assert.IsNull(result.Architecture);
        Assert.IsNull(result.LayerCount);
    }

    [TestMethod]
    public void Create_WithUnknownArchitecture_UsesThatArchitecturePrefix()
    {
        var metadata = new Dictionary<string, string>
        {
            ["general.architecture"] = "future",
            ["future.block_count"] = "12"
        };

        VocabOnlyMetadataProjection result =
            VocabOnlyMetadataProjection.Create(metadata);

        Assert.AreEqual("future", result.Architecture);
        Assert.AreEqual(12, result.LayerCount);
    }

    [TestMethod]
    public void Create_WithMultipleArchitecturePrefixes_UsesOnlySelectedArchitecture()
    {
        var metadata = new Dictionary<string, string>
        {
            ["general.architecture"] = "granite",
            ["granite.block_count"] = "40",
            ["llama.block_count"] = "99"
        };

        VocabOnlyMetadataProjection result =
            VocabOnlyMetadataProjection.Create(metadata);

        Assert.AreEqual(40, result.LayerCount);
    }

    [TestMethod]
    public void Create_WhenOptionalMetadataIsMissing_ReturnsUnavailableValues()
    {
        var metadata = new Dictionary<string, string>
        {
            ["general.architecture"] = "granite",
            ["general.name"] = "Granite"
        };

        VocabOnlyMetadataProjection result =
            VocabOnlyMetadataProjection.Create(metadata);

        Assert.AreEqual("granite", result.Architecture);
        Assert.AreEqual("Granite", result.ModelName);
        Assert.IsNull(result.FileType);
        Assert.IsNull(result.QuantizationVersion);
        Assert.IsNull(result.TokenizerModel);
        Assert.IsNull(result.ContextSize);
        Assert.IsNull(result.EmbeddingSize);
        Assert.IsNull(result.LayerCount);
        Assert.IsNull(result.HeadCount);
        Assert.IsNull(result.KvHeadCount);
        Assert.IsNull(result.ParameterCount);
    }

    [TestMethod]
    public void Create_WithZeroNumericValues_PreservesZero()
    {
        var metadata = new Dictionary<string, string>
        {
            ["general.architecture"] = "granite",
            ["granite.context_length"] = "0",
            ["granite.block_count"] = "0",
            ["general.parameter_count"] = "0"
        };

        VocabOnlyMetadataProjection result =
            VocabOnlyMetadataProjection.Create(metadata);

        Assert.AreEqual(0, result.ContextSize);
        Assert.AreEqual(0, result.LayerCount);
        Assert.AreEqual(0UL, result.ParameterCount);
    }

    [TestMethod]
    public void Create_WithNegativeSignedValues_ReturnsUnavailableValues()
    {
        var metadata = new Dictionary<string, string>
        {
            ["general.architecture"] = "granite",
            ["granite.context_length"] = "-1",
            ["granite.block_count"] = "-2"
        };

        VocabOnlyMetadataProjection result =
            VocabOnlyMetadataProjection.Create(metadata);

        Assert.IsNull(result.ContextSize);
        Assert.IsNull(result.LayerCount);
    }

    [TestMethod]
    public void Create_WithInt32Overflow_ReturnsUnavailableValue()
    {
        var metadata = new Dictionary<string, string>
        {
            ["general.architecture"] = "granite",
            ["granite.context_length"] = "2147483648"
        };

        Assert.IsNull(
            VocabOnlyMetadataProjection.Create(metadata).ContextSize);
    }

    [TestMethod]
    public void Create_WithUInt64Overflow_ReturnsUnavailableParameterCount()
    {
        var metadata = new Dictionary<string, string>
        {
            ["general.architecture"] = "granite",
            ["general.parameter_count"] = "18446744073709551616"
        };

        Assert.IsNull(
            VocabOnlyMetadataProjection.Create(metadata).ParameterCount);
    }

    [TestMethod]
    [DataRow("not-a-number")]
    [DataRow("1,024")]
    [DataRow("1.024")]
    public void Create_WithMalformedOrCultureSpecificNumber_ReturnsUnavailable(
        string value)
    {
        var metadata = new Dictionary<string, string>
        {
            ["general.architecture"] = "granite",
            ["granite.context_length"] = value
        };

        Assert.IsNull(
            VocabOnlyMetadataProjection.Create(metadata).ContextSize);
    }

    [TestMethod]
    public void Create_WithWrongArchitecturePrefix_DoesNotLeakValue()
    {
        var metadata = new Dictionary<string, string>
        {
            ["general.architecture"] = "granite",
            ["llama.context_length"] = "8192"
        };

        Assert.IsNull(
            VocabOnlyMetadataProjection.Create(metadata).ContextSize);
    }

    [TestMethod]
    public void Create_WithEmptyOptionalStrings_ReturnsNull()
    {
        var metadata = new Dictionary<string, string>
        {
            ["general.architecture"] = "granite",
            ["general.name"] = "",
            ["general.file_type"] = "   ",
            ["tokenizer.ggml.model"] = ""
        };

        VocabOnlyMetadataProjection result =
            VocabOnlyMetadataProjection.Create(metadata);

        Assert.IsNull(result.ModelName);
        Assert.IsNull(result.FileType);
        Assert.IsNull(result.TokenizerModel);
    }

    [TestMethod]
    public void Create_WithNullMetadata_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => VocabOnlyMetadataProjection.Create(null!));
    }

    private static IReadOnlyDictionary<string, string>
        CompleteGraniteMetadata()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
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
    }
}
