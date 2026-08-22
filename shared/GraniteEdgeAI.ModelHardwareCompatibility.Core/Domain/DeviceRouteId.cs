namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// The hardware a configuration targets.
/// </summary>
public enum DeviceRouteId
{
    Unspecified = 0,
    Cpu,
    IntelIntegratedGpu,
    IntelDiscreteGpu,
    IntelNpu
}
