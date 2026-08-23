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

        Assert.AreEqual(4, history.Messages.Count);
        Assert.AreEqual(AuthorRole.System, history.Messages[0].AuthorRole);
        StringAssert.Contains(
            history.Messages[0].Content,
            "Check numerical claims and units");
        Assert.AreEqual(AuthorRole.User, history.Messages[1].AuthorRole);
        Assert.AreEqual("first question", history.Messages[1].Content);
        Assert.AreEqual(AuthorRole.Assistant, history.Messages[2].AuthorRole);
        Assert.AreEqual("first answer", history.Messages[2].Content);
        Assert.AreEqual(AuthorRole.User, history.Messages[3].AuthorRole);
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
    public void CreateInferenceParametersUsesRepeatResistantTurnBoundaries()
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

        Assert.AreEqual(513, inference.MaxTokens);
        DefaultSamplingPipeline sampling = Assert.IsInstanceOfType<DefaultSamplingPipeline>(
            inference.SamplingPipeline);
        Assert.AreEqual(42u, sampling.Seed);
        Assert.AreEqual(0.2f, sampling.Temperature);
        Assert.AreEqual(1.1f, sampling.RepeatPenalty);
        CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nUser:");
        CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nuser:");
        CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nAssistant:");
        CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nassistant:");
        CollectionAssert.DoesNotContain(inference.AntiPrompts.ToList(), "\nMe:");
        CollectionAssert.DoesNotContain(inference.AntiPrompts.ToList(), "\nme:");
        CollectionAssert.DoesNotContain(inference.AntiPrompts.ToList(), "\n```\n```");
        CollectionAssert.DoesNotContain(inference.AntiPrompts.ToList(), "\r\n```\r\n```");
    }

    [TestMethod]
    public void BeginGenerationClearsReasonFromThePreviousTurn()
    {
        var observer = new GraniteGenerationBoundaryObserver();
        observer.Complete(GgufAdapterCompletionReason.Length);

        LlamaSharpInferenceEngine.BeginGeneration(observer);
        Assert.IsNull(observer.Reason);

        observer.Complete(GgufAdapterCompletionReason.Stop);
        LlamaSharpInferenceEngine.BeginGeneration(observer);
        Assert.IsNull(observer.Reason);
    }
}
