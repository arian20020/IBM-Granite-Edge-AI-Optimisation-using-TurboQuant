namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// A physical pool a requirement is charged against. These are never summed
/// into a single capacity figure: RAM and dedicated video memory are separate
/// hardware, and treating them as one total is a classic false-safe error.
/// </summary>
internal enum ResourceTarget
{
    Unspecified = 0,
    SystemMemory,
    DedicatedDeviceMemory,

    /// <summary>
    /// Integrated-GPU memory carved out of system RAM. It contributes to
    /// system-memory pressure and never adds capacity.
    /// </summary>
    SharedDeviceMemory,
    Storage
}
