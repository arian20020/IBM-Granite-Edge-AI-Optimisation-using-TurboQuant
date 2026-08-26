using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Configuration;

public sealed record GgufCapabilityMatrix
{
    private GgufCapabilityMatrix(
        string runtimeBuildId,
        int maxContextSize,
        IReadOnlySet<GgufRuntimeBackend> backends,
        IReadOnlySet<GgufCacheType> cacheTypes)
    {
        RuntimeBuildId = runtimeBuildId;
        MaxContextSize = maxContextSize;
        Backends = backends;
        CacheTypes = cacheTypes;
    }

    public string RuntimeBuildId { get; }

    public int MaxContextSize { get; }

    public IReadOnlySet<GgufRuntimeBackend> Backends { get; }

    public IReadOnlySet<GgufCacheType> CacheTypes { get; }

    public static GgufCapabilityMatrix CpuOnly(string runtimeBuildId, int maxContextSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeBuildId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxContextSize);
        return new GgufCapabilityMatrix(
            runtimeBuildId,
            maxContextSize,
            new HashSet<GgufRuntimeBackend> { GgufRuntimeBackend.Cpu },
            new HashSet<GgufCacheType>
            {
                GgufCacheType.F16,
                GgufCacheType.Q8Zero,
                GgufCacheType.Q4Zero,
            });
    }
}
