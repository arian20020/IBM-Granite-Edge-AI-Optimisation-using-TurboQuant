using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests;

internal static class OptimizationHardwareAuthorityTestData
{
    private const ulong GiB = 1024UL * 1024 * 1024;

    internal static OptimizationHardwareAuthority AllEstablished() =>
        OptimizationHardwareAuthority.Create(
            new string('a', 64),
            [DeviceRouteId.Cpu, DeviceRouteId.IntelIntegratedGpu,
             DeviceRouteId.IntelDiscreteGpu, DeviceRouteId.IntelNpu],
            [CompatibilityBackend.Cpu, CompatibilityBackend.IntelSycl,
             CompatibilityBackend.IntelVulkan, CompatibilityBackend.OpenVinoCpu,
             CompatibilityBackend.OpenVinoGpu, CompatibilityBackend.OpenVinoNpu],
            ByteCount.FromBytes(64 * GiB),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            "test-freshness-v1");
}
