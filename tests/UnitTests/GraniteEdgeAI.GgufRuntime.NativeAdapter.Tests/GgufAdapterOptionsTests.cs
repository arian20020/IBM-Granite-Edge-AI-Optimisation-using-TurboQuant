using GraniteEdgeAI.GgufRuntime.NativeAdapter;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests;

[TestClass]
public sealed class GgufAdapterOptionsTests
{
    private static readonly string[] ValidArguments =
    [
        "--model", "C:\\Models\\granite.gguf",
        "--ctx-size", "4096",
        "--cache-type-k", "q8_0",
        "--cache-type-v", "q4_0",
        "--n-gpu-layers", "0",
        "--threads", "8",
        "--batch-size", "512",
        "--flash-attention", "off",
        "--max-tokens", "64",
    ];

    [TestMethod]
    public void ParseReturnsTheCompleteApprovedCpuConfiguration()
    {
        GgufAdapterOptions options = GgufAdapterOptions.Parse(ValidArguments);

        Assert.AreEqual("C:\\Models\\granite.gguf", options.ModelPath);
        Assert.AreEqual(4096u, options.ContextSize);
        Assert.AreEqual(GgufAdapterCacheType.Q8Zero, options.KeyCacheType);
        Assert.AreEqual(GgufAdapterCacheType.Q4Zero, options.ValueCacheType);
        Assert.AreEqual(0, options.GpuLayerCount);
        Assert.AreEqual(8, options.ThreadCount);
        Assert.AreEqual(512u, options.BatchSize);
        Assert.IsFalse(options.FlashAttention);
        Assert.AreEqual(64, options.MaximumGeneratedTokens);
    }

    [TestMethod]
    public void ParseRejectsTurboQuantOnTheUpstreamBackend()
    {
        string[] arguments = ValidArguments.ToArray();
        arguments[5] = "turbo3";

        GgufAdapterConfigurationException exception =
            Assert.ThrowsExactly<GgufAdapterConfigurationException>(() =>
                GgufAdapterOptions.Parse(arguments));

        Assert.AreEqual("turboquant-runtime-required", exception.Code);
    }

    [TestMethod]
    public void ParseRejectsGpuOffloadInTheCpuAdapter()
    {
        string[] arguments = ValidArguments.ToArray();
        arguments[9] = "1";

        GgufAdapterConfigurationException exception =
            Assert.ThrowsExactly<GgufAdapterConfigurationException>(() =>
                GgufAdapterOptions.Parse(arguments));

        Assert.AreEqual("unsupported-configuration", exception.Code);
    }
}
