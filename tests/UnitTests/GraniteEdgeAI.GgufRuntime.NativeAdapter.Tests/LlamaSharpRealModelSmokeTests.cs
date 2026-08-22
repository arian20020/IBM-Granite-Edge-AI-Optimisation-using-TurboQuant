using GraniteEdgeAI.GgufRuntime.NativeAdapter;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests;

[TestClass]
public sealed class LlamaSharpRealModelSmokeTests
{
    private const string ModelVariable = "GRANITE_GGUF_ADAPTER_TEST_MODEL";

    [TestMethod]
    [TestCategory("ControlledRuntime")]
    public async Task ControlledGraniteModelLoadsIntoTheCpuAdapter()
    {
        string? model = Environment.GetEnvironmentVariable(ModelVariable);
        if (string.IsNullOrWhiteSpace(model))
        {
            Assert.Inconclusive("controlled GGUF adapter model not configured");
        }

        var options = new GgufAdapterOptions(
            Path.GetFullPath(model!),
            2048,
            GgufAdapterCacheType.F16,
            GgufAdapterCacheType.F16,
            0,
            4,
            256,
            false);
        await using var engine = new LlamaSharpInferenceEngine(options);

        await engine.InitializeAsync([], CancellationToken.None);
    }
}
