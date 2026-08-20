using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Worker.Session;

namespace GraniteEdgeAI.GgufRuntime.Worker.Tests;

[TestClass]
public sealed class GgufCliArgumentBuilderTests
{
    private static readonly string[] ExpectedArguments =
    [
        "--model",
        "C:\\Models With Spaces\\granite.gguf",
        "--ctx-size",
        "4096",
        "--cache-type-k",
        "f16",
        "--cache-type-v",
        "f16",
        "--n-gpu-layers",
        "0",
        "--threads",
        "8",
        "--batch-size",
        "512",
        "--simple-io",
        "--conversation",
        "--no-display-prompt",
    ];

    [TestMethod]
    public void BuildCpuArgumentsReturnsExactAllowlistedValues()
    {
        GgufRuntimeConfiguration configuration = CreateConfiguration();

        IReadOnlyList<string> arguments = GgufCliArgumentBuilder.Build(
            "C:\\Models With Spaces\\granite.gguf",
            configuration);

        CollectionAssert.AreEqual(ExpectedArguments, arguments.ToArray());
    }

    [TestMethod]
    public void BuildRejectsRelativeModelLocation()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufCliArgumentBuilder.Build("models\\granite.gguf", CreateConfiguration()));
    }

    private static GgufRuntimeConfiguration CreateConfiguration()
    {
        return new GgufRuntimeConfiguration(
            "granite-3b",
            new string('a', 64),
            "cpu-test-build",
            new string('b', 40),
            GgufRuntimeBackend.Cpu,
            "cpu",
            4096,
            GgufCacheType.F16,
            GgufCacheType.F16,
            0,
            false,
            8,
            512,
            "controlled",
            "cpu-safe");
    }
}
