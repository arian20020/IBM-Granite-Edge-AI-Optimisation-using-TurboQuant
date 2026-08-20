using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// Composes components into a per-pool peak. The peak is the largest single
/// phase rather than the total of every component, because components that
/// never coexist must not be added together.
/// </summary>
internal static class ResourcePhaseComposer
{
    private static readonly LifecyclePhase[] Phases =
    [
        LifecyclePhase.Load,
        LifecyclePhase.Compile,
        LifecyclePhase.SteadyStateGeneration
    ];

    private static readonly ResourceTarget[] Targets =
    [
        ResourceTarget.SystemMemory,
        ResourceTarget.DedicatedDeviceMemory,
        ResourceTarget.SharedDeviceMemory,
        ResourceTarget.Storage
    ];

    internal static ResourcePeakProfile Compose(IReadOnlyList<ResourceComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        Dictionary<ResourceTarget, ByteCount> peaks = [];

        foreach (ResourceTarget target in Targets)
        {
            ByteCount peak = ByteCount.Zero;

            foreach (LifecyclePhase phase in Phases)
            {
                ByteCount phaseTotal = ByteCount.Zero;

                foreach (ResourceComponent component in components)
                {
                    if (component.Target == target && component.Phases.Contains(phase))
                    {
                        phaseTotal = phaseTotal.Add(component.Bytes);
                    }
                }

                if (phaseTotal > peak)
                {
                    peak = phaseTotal;
                }
            }

            peaks[target] = peak;
        }

        // Shared device memory lives in system RAM, so it is charged to system
        // pressure exactly once and never treated as additional capacity.
        ByteCount pressure = peaks[ResourceTarget.SystemMemory]
            .Add(peaks[ResourceTarget.SharedDeviceMemory]);

        return new ResourcePeakProfile(peaks, pressure);
    }
}
