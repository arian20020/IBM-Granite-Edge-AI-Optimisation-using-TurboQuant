using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using GraniteEdgeAI.GgufRuntime.NativeAdapter;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests;

[TestClass]
public sealed class LlamaSharpInferenceEngineTests
{
    [TestMethod]
    public void CreateHistoryMapsEveryTurnWithoutRegeneration()
    {
        GgufAdapterMessage[] messages =
        [
            new(GgufAdapterRole.User, "first question"),
            new(GgufAdapterRole.Assistant, "first answer"),
            new(GgufAdapterRole.User, "follow up"),
        ];

        ChatHistory history = LlamaSharpInferenceEngine.CreateHistory(messages);

        Assert.AreEqual(3, history.Messages.Count);
        Assert.AreEqual(AuthorRole.User, history.Messages[0].AuthorRole);
        Assert.AreEqual("first question", history.Messages[0].Content);
        Assert.AreEqual(AuthorRole.Assistant, history.Messages[1].AuthorRole);
        Assert.AreEqual("first answer", history.Messages[1].Content);
        Assert.AreEqual(AuthorRole.User, history.Messages[2].AuthorRole);
    }

    [TestMethod]
    public void CreateModelParametersMapsTheApprovedCpuProfileExactly()
    {
        var options = new GgufAdapterOptions(
            "C:\\Models\\granite.gguf",
            4096,
            GgufAdapterCacheType.Q8Zero,
            GgufAdapterCacheType.Q4Zero,
            0,
            8,
            512,
            false,
            64);

        ModelParams parameters = LlamaSharpInferenceEngine.CreateModelParameters(options);

        Assert.AreEqual(4096u, parameters.ContextSize);
        Assert.AreEqual(0, parameters.GpuLayerCount);
        Assert.AreEqual(8, parameters.Threads);
        Assert.AreEqual(512u, parameters.BatchSize);
        Assert.AreEqual(GGMLType.GGML_TYPE_Q8_0, parameters.TypeK);
        Assert.AreEqual(GGMLType.GGML_TYPE_Q4_0, parameters.TypeV);
        Assert.AreEqual(false, parameters.FlashAttention);
        Assert.AreEqual(64, options.MaximumGeneratedTokens);
    }

    [TestMethod]
    public void CreateInferenceParametersUsesGreedyTurnBoundaries()
    {
        InferenceParams inference = LlamaSharpInferenceEngine.CreateInferenceParameters(
            new GgufAdapterOptions(
                "C:\\Models\\granite.gguf",
                2048,
                GgufAdapterCacheType.F16,
                GgufAdapterCacheType.F16,
                0,
                4,
                256,
                false,
                512));

        Assert.AreEqual(512, inference.MaxTokens);
        Assert.IsInstanceOfType<GreedySamplingPipeline>(inference.SamplingPipeline);
        CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nUser:");
        CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nuser:");
        CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nAssistant:");
        CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nassistant:");
        CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nMe:");
        CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nme:");
    }
}
