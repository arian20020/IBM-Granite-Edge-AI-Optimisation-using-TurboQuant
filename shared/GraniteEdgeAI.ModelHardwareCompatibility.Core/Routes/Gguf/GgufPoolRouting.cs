using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Which pool a candidate's model memory is charged against, and whether loading
/// it needs a transient host copy.
/// </summary>
internal readonly record struct GgufPoolRouting(
    ResourceTarget ModelTarget,
    bool RequiresHostStaging);

/// <summary>
/// Decides the pool for one device and offload pairing.
///
/// Integrated graphics are charged to shared device memory, which the phase
/// composer folds into system pressure exactly once - it is carved out of the
/// same physical RAM and never adds capacity. Discrete graphics are charged to
/// dedicated memory and additionally need a host staging buffer during load.
/// </summary>
internal static class GgufPoolRouter
{
    internal static bool TryResolve(
        DeviceRouteId device,
        GpuOffloadLevel offload,
        out GgufPoolRouting routing,
        out EstimationUnavailableReason reason)
    {
        routing = default;

        // Partial offload declares no layer count, so there is no split to
        // compute. Refusing is the only honest answer available today.
        if (offload == GpuOffloadLevel.Partial)
        {
            reason = EstimationUnavailableReason.UnknownOffloadSplit;
            return false;
        }

        switch (device, offload)
        {
            case (DeviceRouteId.Cpu, GpuOffloadLevel.None):
            case (DeviceRouteId.IntelIntegratedGpu, GpuOffloadLevel.None):
            case (DeviceRouteId.IntelDiscreteGpu, GpuOffloadLevel.None):
                routing = new GgufPoolRouting(ResourceTarget.SystemMemory, false);
                break;

            case (DeviceRouteId.IntelIntegratedGpu, GpuOffloadLevel.Full):
                routing = new GgufPoolRouting(ResourceTarget.SharedDeviceMemory, false);
                break;

            case (DeviceRouteId.IntelDiscreteGpu, GpuOffloadLevel.Full):
                routing = new GgufPoolRouting(ResourceTarget.DedicatedDeviceMemory, true);
                break;

            default:
                reason = EstimationUnavailableReason.UnsupportedDeviceRoute;
                return false;
        }

        reason = EstimationUnavailableReason.None;
        return true;
    }
}
