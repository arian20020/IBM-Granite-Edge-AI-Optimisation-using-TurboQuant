using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// Per-pool peak requirements for one candidate configuration.
/// </summary>
internal sealed record ResourcePeakProfile
{
    private readonly IReadOnlyDictionary<ResourceTarget, ByteCount> _peaks;

    internal ResourcePeakProfile(
        IReadOnlyDictionary<ResourceTarget, ByteCount> peaks,
        ByteCount systemMemoryPressure)
    {
        _peaks = peaks;
        SystemMemoryPressure = systemMemoryPressure;
    }

    /// <summary>
    /// System memory plus shared device memory, which is carved out of the same
    /// physical RAM. This is the figure the RAM safety gate compares against.
    /// </summary>
    internal ByteCount SystemMemoryPressure { get; }

    internal ByteCount PeakFor(ResourceTarget target) =>
        _peaks.TryGetValue(target, out ByteCount peak) ? peak : ByteCount.Zero;
}
