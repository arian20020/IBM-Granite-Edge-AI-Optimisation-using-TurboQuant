using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Configuration;

public sealed record GgufCapabilityMatrix
{
    private GgufCapabilityMatrix(
        string runtimeBuildId,
        int maxContextSize,
        IReadOnlySet<GgufRuntimeBackend> backends)
    {
        RuntimeBuildId = runtimeBuildId;
        MaxContextSize = maxContextSize;
        Backends = backends;
    }

    public string RuntimeBuildId { get; }

    public int MaxContextSize { get; }

    public IReadOnlySet<GgufRuntimeBackend> Backends { get; }

    public static GgufCapabilityMatrix CpuOnly(string runtimeBuildId, int maxContextSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeBuildId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxContextSize);
        return new GgufCapabilityMatrix(
            runtimeBuildId,
            maxContextSize,
            new HashSet<GgufRuntimeBackend> { GgufRuntimeBackend.Cpu });
    }
}
