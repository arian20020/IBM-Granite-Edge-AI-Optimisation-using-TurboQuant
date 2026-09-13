using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Transport;

namespace GraniteEdgeAI.GgufRuntime.Worker.Session;

internal static class GgufNativeRuntimeSelector
{
    internal static string? Select(
        GgufWorkerBootstrap bootstrap,
        GgufRuntimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(configuration);

        if (bootstrap.RuntimeBuildId is not null
            && !bootstrap.RuntimeBuildId.Equals(
                configuration.RuntimeBuildId,
                StringComparison.Ordinal))
        {
            throw new GgufCliStartupException("runtime-build-mismatch");
        }
        if (bootstrap.RuntimeSourceCommit is not null
            && !bootstrap.RuntimeSourceCommit.Equals(
                configuration.RuntimeSourceCommit,
                StringComparison.Ordinal))
        {
            throw new GgufCliStartupException("runtime-source-mismatch");
        }

        if (configuration.Backend == GgufRuntimeBackend.Vulkan)
        {
            return bootstrap.VulkanRuntimeExecutable
                ?? throw new GgufCliStartupException("vulkan-runtime-unavailable");
        }

        // A bootstrap carrying a verified runtime identity is an admitted
        // AtomicBot authority, not an optional TurboQuant add-on.  Running an
        // F16/Q8 CPU configuration through the legacy adapter in that state
        // would retain the AtomicBot identity while executing another engine.
        if (configuration.Backend == GgufRuntimeBackend.Cpu
            && bootstrap.RuntimeBuildId is not null
            && bootstrap.RuntimeSourceCommit is not null)
        {
            return bootstrap.CpuRuntimeExecutable
                ?? throw new GgufCliStartupException("cpu-runtime-unavailable");
        }

        bool turboQuant = configuration.KeyCacheType is GgufCacheType.Turbo2
                or GgufCacheType.Turbo3 or GgufCacheType.Turbo4
            || configuration.ValueCacheType is GgufCacheType.Turbo2
                or GgufCacheType.Turbo3 or GgufCacheType.Turbo4;
        if (configuration.Backend == GgufRuntimeBackend.Cpu && turboQuant)
        {
            return bootstrap.CpuRuntimeExecutable
                ?? throw new GgufCliStartupException("cpu-turboquant-runtime-unavailable");
        }

        return null;
    }
}
