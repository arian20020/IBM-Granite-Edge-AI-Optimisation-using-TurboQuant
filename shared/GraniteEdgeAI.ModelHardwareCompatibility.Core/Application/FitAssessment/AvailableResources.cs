using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// A capacity observation. Freshness is explicit and carried on the value
/// because a historical availability figure must never satisfy the safety
/// gate: the user may have opened other applications since it was taken.
/// </summary>
internal sealed record AvailableResources
{
    private AvailableResources(
        ByteCount systemMemory,
        ByteCount dedicatedDeviceMemory,
        ByteCount storage,
        DateTimeOffset observedAtUtc,
        bool isFresh,
        bool dedicatedDeviceMemoryEstablished)
    {
        SystemMemory = systemMemory;
        DedicatedDeviceMemory = dedicatedDeviceMemory;
        Storage = storage;
        ObservedAtUtc = observedAtUtc;
        IsFresh = isFresh;
        DedicatedDeviceMemoryEstablished = dedicatedDeviceMemoryEstablished;
    }

    internal ByteCount SystemMemory { get; }

    internal ByteCount DedicatedDeviceMemory { get; }

    internal ByteCount Storage { get; }

    internal DateTimeOffset ObservedAtUtc { get; }

    internal bool IsFresh { get; }

    internal bool DedicatedDeviceMemoryEstablished { get; }

    internal static AvailableResources Create(
        ByteCount systemMemory,
        ByteCount dedicatedDeviceMemory,
        ByteCount storage,
        DateTimeOffset observedAtUtc,
        bool isFresh = true,
        bool dedicatedDeviceMemoryEstablished = true) =>
        new(systemMemory,
            dedicatedDeviceMemory,
            storage,
            observedAtUtc.ToUniversalTime(),
            isFresh,
            dedicatedDeviceMemoryEstablished);
}
