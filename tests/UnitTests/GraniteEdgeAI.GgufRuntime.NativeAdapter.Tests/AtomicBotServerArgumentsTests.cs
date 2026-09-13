namespace GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests;

[TestClass]
public sealed class AtomicBotServerArgumentsTests
{
    private static readonly string[] ExpectedVulkanArguments =
    [
        "--host", "127.0.0.1",
        "--port", "43127",
        "--no-ui",
        "--no-context-shift",
        "--model", "C:\\Models With Spaces\\granite.gguf",
        "--ctx-size", "4096",
        "--cache-type-k", "turbo4",
        "--cache-type-v", "turbo4",
        "--n-gpu-layers", "999",
        "--threads", "8",
        "--batch-size", "512",
        "--flash-attn", "on",
        "--parallel", "1",
        "--cache-ram", "0",
        "--fit", "off",
        "--offline",
    ];

    [TestMethod]
    public void BuildUsesOnlyLoopbackAndExactTurboQuantVulkanConfiguration()
    {
        var options = new GgufAdapterOptions(
            "C:\\Models With Spaces\\granite.gguf",
            4096,
            GgufAdapterCacheType.Turbo4,
            GgufAdapterCacheType.Turbo4,
            999,
            8,
            512,
            true,
            64,
            "C:\\runtime\\vulkan\\llama-server.exe",
            GgufAdapterBackend.Vulkan);

        IReadOnlyList<string> actual = AtomicBotServerArguments.Build(options, 43127);

        CollectionAssert.AreEqual(ExpectedVulkanArguments, actual.ToArray());
    }
}
