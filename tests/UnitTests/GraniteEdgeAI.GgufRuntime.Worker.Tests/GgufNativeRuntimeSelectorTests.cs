using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Failures;
using GraniteEdgeAI.GgufRuntime.Transport;
using GraniteEdgeAI.GgufRuntime.Worker.Session;

namespace GraniteEdgeAI.GgufRuntime.Worker.Tests;

[TestClass]
public sealed class GgufNativeRuntimeSelectorTests
{
    [TestMethod]
    public void VulkanRuntimeUnavailabilityIsTypedAsRuntimeUnavailable()
    {
        GgufRuntimeFailure failure = GgufWorkerHost.MapStartupFailure(
            "vulkan-runtime-unavailable");

        Assert.AreEqual(GgufRuntimeFailureCategory.RuntimeUnavailable, failure.Category);
        Assert.AreEqual("vulkan-runtime-unavailable", failure.Code);
    }

    [TestMethod]
    public void SelectUsesTheVerifiedVulkanRoleForVulkan()
    {
        var bootstrap = new GgufWorkerBootstrap(
            "C:\\runtime\\adapter.exe",
            "C:\\models\\model.gguf",
            [],
            "C:\\runtime\\cpu\\llama-server.exe",
            "C:\\runtime\\vulkan\\llama-server.exe",
            "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan",
            "519f0c594a8e31467d2e2f2cf17054c9e7e11536");

        string? selected = GgufNativeRuntimeSelector.Select(
            bootstrap,
            CreateConfiguration(GgufRuntimeBackend.Vulkan, GgufCacheType.Turbo4));

        Assert.AreEqual(bootstrap.VulkanRuntimeExecutable, selected);
    }

    [TestMethod]
    public void SelectUsesTheVerifiedCpuRoleForCpuTurboQuant()
    {
        GgufWorkerBootstrap bootstrap = CreateBootstrap(
            "C:\\runtime\\cpu\\llama-server.exe",
            "C:\\runtime\\vulkan\\llama-server.exe");

        string? selected = GgufNativeRuntimeSelector.Select(
            bootstrap,
            CreateConfiguration(GgufRuntimeBackend.Cpu, GgufCacheType.Turbo3));

        Assert.AreEqual(bootstrap.CpuRuntimeExecutable, selected);
    }

    [TestMethod]
    public void SelectUsesVerifiedAtomicBotCpuRoleForF16()
    {
        GgufWorkerBootstrap bootstrap = new(
            "C:\\runtime\\adapter.exe",
            "C:\\models\\model.gguf",
            [],
            "C:\\runtime\\cpu\\llama-server.exe",
            "C:\\runtime\\vulkan\\llama-server.exe",
            "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan",
            "519f0c594a8e31467d2e2f2cf17054c9e7e11536");

        Assert.AreEqual(
            bootstrap.CpuRuntimeExecutable,
            GgufNativeRuntimeSelector.Select(
                bootstrap,
                CreateConfiguration(GgufRuntimeBackend.Cpu, GgufCacheType.F16)));
    }

    [TestMethod]
    [DataRow(GgufCacheType.F16, "f16")]
    [DataRow(GgufCacheType.Q8Zero, "q8_0")]
    public void VerifiedAtomicBotCpuSelectionBuildsNativeArgumentsForStandardCaches(
        GgufCacheType cacheType,
        string cacheToken)
    {
        var bootstrap = new GgufWorkerBootstrap(
            "C:\\runtime\\adapter.exe",
            "C:\\models\\model.gguf",
            [],
            "C:\\runtime\\cpu\\llama-server.exe",
            "C:\\runtime\\vulkan\\llama-server.exe",
            "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan",
            "519f0c594a8e31467d2e2f2cf17054c9e7e11536");
        GgufRuntimeConfiguration configuration = CreateConfiguration(
            GgufRuntimeBackend.Cpu,
            cacheType);

        string? selected = GgufNativeRuntimeSelector.Select(
            bootstrap,
            configuration);
        IReadOnlyList<string> arguments = GgufCliArgumentBuilder.Build(
            "C:\\models\\model.gguf",
            configuration,
            selected);

        CollectionAssert.AreEqual(
            new[]
            {
                "--model", "C:\\models\\model.gguf",
                "--ctx-size", "4096",
                "--cache-type-k", cacheToken,
                "--cache-type-v", cacheToken,
                "--n-gpu-layers", "0",
                "--threads", "8",
                "--batch-size", "512",
                "--flash-attention", "off",
                "--max-tokens", "64",
                "--native-runtime", bootstrap.CpuRuntimeExecutable,
                "--backend", "cpu",
            },
            arguments.ToArray());
    }

    [TestMethod]
    public void SelectReportsTypedVulkanUnavailability()
    {
        GgufCliStartupException exception = Assert.ThrowsExactly<GgufCliStartupException>(() =>
            GgufNativeRuntimeSelector.Select(
                CreateBootstrap("C:\\runtime\\cpu\\llama-server.exe", null),
                CreateConfiguration(GgufRuntimeBackend.Vulkan, GgufCacheType.Turbo4)));

        Assert.AreEqual("vulkan-runtime-unavailable", exception.Code);
    }

    [TestMethod]
    public void SelectRejectsConfigurationFromAnotherRuntimeBuild()
    {
        var bootstrap = new GgufWorkerBootstrap(
            "C:\\runtime\\adapter.exe", "C:\\models\\model.gguf", [],
            "C:\\runtime\\cpu\\llama-server.exe",
            "C:\\runtime\\vulkan\\llama-server.exe",
            "expected-build", new string('b', 40));

        GgufCliStartupException exception = Assert.ThrowsExactly<GgufCliStartupException>(() =>
            GgufNativeRuntimeSelector.Select(
                bootstrap,
                CreateConfiguration(GgufRuntimeBackend.Vulkan, GgufCacheType.Turbo4)));

        Assert.AreEqual("runtime-build-mismatch", exception.Code);
    }

    [TestMethod]
    public void SelectRejectsConfigurationFromAnotherRuntimeSource()
    {
        var bootstrap = new GgufWorkerBootstrap(
            "C:\\runtime\\adapter.exe", "C:\\models\\model.gguf", [],
            "C:\\runtime\\cpu\\llama-server.exe",
            "C:\\runtime\\vulkan\\llama-server.exe",
            "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan",
            new string('b', 40));

        GgufCliStartupException exception = Assert.ThrowsExactly<GgufCliStartupException>(() =>
            GgufNativeRuntimeSelector.Select(
                bootstrap,
                CreateConfiguration(GgufRuntimeBackend.Cpu, GgufCacheType.F16)));

        Assert.AreEqual("runtime-source-mismatch", exception.Code);
    }

    private static GgufWorkerBootstrap CreateBootstrap(string? cpu, string? vulkan) =>
        new(
            "C:\\runtime\\adapter.exe",
            "C:\\models\\model.gguf",
            [],
            cpu,
            vulkan);

    private static GgufRuntimeConfiguration CreateConfiguration(
        GgufRuntimeBackend backend,
        GgufCacheType cacheType) => new(
            "granite-3b", new string('a', 64),
            "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan",
            "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
            backend, backend == GgufRuntimeBackend.Cpu ? "cpu" : "vulkan:0",
            4096, cacheType, cacheType, backend == GgufRuntimeBackend.Cpu ? 0 : 999,
            backend == GgufRuntimeBackend.Vulkan, 8, 512, "measured", "candidate", 64);
}
