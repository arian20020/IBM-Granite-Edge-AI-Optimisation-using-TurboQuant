using System.Text.RegularExpressions;
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

    [TestMethod]
    [TestCategory("ControlledRuntime")]
    public async Task ControlledGraniteModelUsesOneAssistantTurn()
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
            false,
            96);
        await using var engine = new LlamaSharpInferenceEngine(options);
        await engine.InitializeAsync([], CancellationToken.None);

        var chunks = new List<string>();
        await foreach (string chunk in engine.GenerateAsync(
            "Introduce yourself in one short sentence.",
            CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        string answer = string.Concat(chunks);
        Assert.IsFalse(string.IsNullOrWhiteSpace(answer));
        Assert.IsFalse(Regex.IsMatch(
            answer,
            @"(?im)^\s*(?:me|user|assistant)\s*:"));
        Assert.IsFalse(Regex.IsMatch(
            answer,
            @"(?m)(?:^\s*```\s*$\r?\n){2,}"));
    }
}
