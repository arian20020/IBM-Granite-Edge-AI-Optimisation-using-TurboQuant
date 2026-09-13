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
        "--flash-attention",
        "off",
        "--max-tokens",
        "64",
    ];
    private static readonly string[] ExpectedVulkanTurboQuantArguments =
    [
        "--model", "C:\\Models\\granite.gguf",
        "--ctx-size", "4096",
        "--cache-type-k", "turbo4",
        "--cache-type-v", "turbo4",
        "--n-gpu-layers", "999",
        "--threads", "8",
        "--batch-size", "512",
        "--flash-attention", "on",
        "--max-tokens", "64",
        "--native-runtime", "C:\\App\\GgufRuntime\\Native\\Vulkan\\llama-server.exe",
        "--backend", "vulkan"
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

    [TestMethod]
    public void BuildVulkanTurboQuantArgumentsNamesTheVerifiedRuntimeAndExactBackend()
    {
        var configuration = new GgufRuntimeConfiguration(
            "granite-3b",
            new string('a', 64),
            "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan",
            "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
            GgufRuntimeBackend.Vulkan,
            "vulkan:0",
            4096,
            GgufCacheType.Turbo4,
            GgufCacheType.Turbo4,
            999,
            true,
            8,
            512,
            "measured",
            "vulkan-turbo4",
            64);

        IReadOnlyList<string> arguments = GgufCliArgumentBuilder.Build(
            "C:\\Models\\granite.gguf",
            configuration,
            "C:\\App\\GgufRuntime\\Native\\Vulkan\\llama-server.exe");

        CollectionAssert.AreEqual(
            ExpectedVulkanTurboQuantArguments, arguments.ToArray());
    }

    [TestMethod]
    public void BuildVulkanRefusesToProceedWithoutAVerifiedNativeRuntimePath()
    {
        var configuration = new GgufRuntimeConfiguration(
            "granite-3b", new string('a', 64), "atomicbot",
            new string('b', 40), GgufRuntimeBackend.Vulkan, "vulkan:0",
            4096, GgufCacheType.Turbo3, GgufCacheType.Turbo3, 999,
            true, 8, 512, "measured", "vulkan-turbo3", 64);

        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufCliArgumentBuilder.Build(
                "C:\\Models\\granite.gguf", configuration, nativeRuntimePath: null));
    }

    [TestMethod]
    public void BuildVerifiedAtomicBotCpuRefusesToLoseItsNativeRuntimePath()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufCliArgumentBuilder.Build(
                "C:\\Models\\granite.gguf",
                CreatePinnedAtomicBotCpuConfiguration(),
                nativeRuntimePath: null));
    }

    [TestMethod]
    public void BuildLegacyCpuRefusesAnUnverifiedNativeRuntimePath()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufCliArgumentBuilder.Build(
                "C:\\Models\\granite.gguf",
                CreateConfiguration(),
                "C:\\App\\GgufRuntime\\Native\\Cpu\\llama-server.exe"));
    }

    [TestMethod]
    [DataRow("wrong-build", "519f0c594a8e31467d2e2f2cf17054c9e7e11536")]
    [DataRow(
        "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan",
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void BuildCpuRefusesNativePathForAnUnpinnedRuntimeIdentity(
        string runtimeBuildId,
        string runtimeSourceCommit)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufCliArgumentBuilder.Build(
                "C:\\Models\\granite.gguf",
                CreateCpuConfiguration(runtimeBuildId, runtimeSourceCommit),
                "C:\\App\\GgufRuntime\\Native\\Cpu\\llama-server.exe"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("Native\\Cpu\\llama-server.exe")]
    [DataRow("\\\\server\\share\\llama-server.exe")]
    [DataRow("\\\\?\\C:\\runtime\\llama-server.exe")]
    public void BuildPinnedAtomicBotCpuRejectsAnUnsafeNativeRuntimePath(string path)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufCliArgumentBuilder.Build(
                "C:\\Models\\granite.gguf",
                CreatePinnedAtomicBotCpuConfiguration(),
                path));
    }

    [TestMethod]
    public void PinnedAtomicBotCpuDeviceAliasDoesNotChangeTheNativeArgumentVector()
    {
        const string runtime =
            "C:\\App\\GgufRuntime\\Native\\Cpu\\llama-server.exe";
        IReadOnlyList<string> lower = GgufCliArgumentBuilder.Build(
            "C:\\Models\\granite.gguf",
            CreatePinnedAtomicBotCpuConfiguration("cpu"),
            runtime);
        IReadOnlyList<string> canonical = GgufCliArgumentBuilder.Build(
            "C:\\Models\\granite.gguf",
            CreatePinnedAtomicBotCpuConfiguration("CPU"),
            runtime);

        CollectionAssert.AreEqual(lower.ToArray(), canonical.ToArray());
        CollectionAssert.AreEqual(new[]
        {
            "--model", "C:\\Models\\granite.gguf",
            "--ctx-size", "4096",
            "--cache-type-k", "f16",
            "--cache-type-v", "f16",
            "--n-gpu-layers", "0",
            "--threads", "8",
            "--batch-size", "512",
            "--flash-attention", "off",
            "--max-tokens", "64",
            "--native-runtime", runtime,
            "--backend", "cpu",
        }, canonical.ToArray());
        Assert.IsFalse(canonical.Contains("--device", StringComparer.Ordinal));
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
            "cpu-safe",
            64);
    }

    private static GgufRuntimeConfiguration CreatePinnedAtomicBotCpuConfiguration(
        string deviceId = "cpu") => new(
        "granite-3b",
        new string('a', 64),
        "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan",
        "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
        GgufRuntimeBackend.Cpu,
        deviceId,
        4096,
        GgufCacheType.F16,
        GgufCacheType.F16,
        0,
        false,
        8,
        512,
        "controlled",
        "atomicbot-cpu-f16",
        64);

    private static GgufRuntimeConfiguration CreateCpuConfiguration(
        string runtimeBuildId,
        string runtimeSourceCommit) => new(
        "granite-3b",
        new string('a', 64),
        runtimeBuildId,
        runtimeSourceCommit,
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
        "atomicbot-cpu-f16",
        64);
}
