using GraniteEdgeAI.GgufRuntime.Capabilities.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Tests;

[TestClass]
public sealed class GgufRuntimeConfigurationValidatorTests
{
    [TestMethod]
    public void ValidateMatchingCpuConfigurationReturnsSupportedWithoutReplacement()
    {
        GgufRuntimeConfiguration configuration = CreateConfiguration(GgufRuntimeBackend.Cpu);
        var matrix = GgufCapabilityMatrix.CpuOnly(
            runtimeBuildId: "cpu-test-build",
            maxContextSize: 8192);

        GgufCapabilityValidationResult result =
            GgufRuntimeConfigurationValidator.Validate(configuration, matrix);

        Assert.IsTrue(result.IsSupported);
        Assert.IsNull(result.Failure);
        Assert.AreSame(configuration, result.Configuration);
    }

    [TestMethod]
    public void ValidateVulkanConfigurationRejectsWithoutCpuFallbackMutation()
    {
        GgufRuntimeConfiguration configuration = CreateConfiguration(GgufRuntimeBackend.Vulkan);
        var matrix = GgufCapabilityMatrix.CpuOnly(
            runtimeBuildId: "cpu-test-build",
            maxContextSize: 8192);

        GgufCapabilityValidationResult result =
            GgufRuntimeConfigurationValidator.Validate(configuration, matrix);

        Assert.IsFalse(result.IsSupported);
        Assert.AreEqual("runtime-backend-unsupported", result.Failure?.Code);
        Assert.AreSame(configuration, result.Configuration);
        Assert.AreEqual(GgufRuntimeBackend.Vulkan, result.Configuration.Backend);
    }

    [TestMethod]
    public void ValidateCpuGpuOffloadRejectsUnsupportedConfiguration()
    {
        GgufRuntimeConfiguration configuration = CreateConfiguration(
            GgufRuntimeBackend.Cpu,
            gpuLayerCount: 1);
        var matrix = GgufCapabilityMatrix.CpuOnly(
            runtimeBuildId: "cpu-test-build",
            maxContextSize: 8192);

        GgufCapabilityValidationResult result =
            GgufRuntimeConfigurationValidator.Validate(configuration, matrix);

        Assert.IsFalse(result.IsSupported);
        Assert.AreEqual("runtime-gpu-offload-unsupported", result.Failure?.Code);
    }

    [TestMethod]
    public void ValidateTurboQuantCacheRejectsThePinnedUpstreamBaseline()
    {
        GgufRuntimeConfiguration configuration = CreateConfiguration(
            GgufRuntimeBackend.Cpu,
            cacheType: GgufCacheType.Turbo3);
        var matrix = GgufCapabilityMatrix.CpuOnly(
            runtimeBuildId: "cpu-test-build",
            maxContextSize: 8192);

        GgufCapabilityValidationResult result =
            GgufRuntimeConfigurationValidator.Validate(configuration, matrix);

        Assert.IsFalse(result.IsSupported);
        Assert.AreEqual("turboquant-runtime-required", result.Failure?.Code);
    }

    private static GgufRuntimeConfiguration CreateConfiguration(
        GgufRuntimeBackend backend,
        int gpuLayerCount = 0,
        GgufCacheType cacheType = GgufCacheType.F16)
    {
        return new GgufRuntimeConfiguration(
            "granite-3b",
            new string('a', 64),
            "cpu-test-build",
            new string('b', 40),
            backend,
            backend == GgufRuntimeBackend.Cpu ? "cpu" : "gpu0",
            4096,
            cacheType,
            cacheType,
            gpuLayerCount,
            false,
            8,
            512,
            "controlled",
            "cpu-safe");
    }
}
