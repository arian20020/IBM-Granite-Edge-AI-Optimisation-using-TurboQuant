using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// The machine facts the engine consumes, owned by C1 and shaped by the
/// calculation rather than by any upstream contract.
///
/// Installed capacity may come from a handoff. Current availability may not —
/// that is what IFreshSystemMemoryProbe is for, and the separation is deliberate.
/// </summary>
internal sealed record HardwareFacts
{
    private HardwareFacts(
        ByteCount installedSystemMemory,
        ByteCount installedDedicatedDeviceMemory,
        ByteCount freeStorage,
        IReadOnlySet<DeviceRouteId> presentDevices,
        IReadOnlySet<CompatibilityBackend> verifiedBackends)
    {
        InstalledSystemMemory = installedSystemMemory;
        InstalledDedicatedDeviceMemory = installedDedicatedDeviceMemory;
        FreeStorage = freeStorage;
        PresentDevices = presentDevices;
        VerifiedBackends = verifiedBackends;
    }

    internal ByteCount InstalledSystemMemory { get; }

    internal ByteCount InstalledDedicatedDeviceMemory { get; }

    internal ByteCount FreeStorage { get; }

    /// <summary>Devices the machine has. Presence is not verification.</summary>
    internal IReadOnlySet<DeviceRouteId> PresentDevices { get; }

    /// <summary>
    /// Backends a check has actually run against and passed. A backend being
    /// installed is not the same as its having been verified, and offering an
    /// unverified backend is how a run fails at launch rather than at planning.
    /// </summary>
    internal IReadOnlySet<CompatibilityBackend> VerifiedBackends { get; }

    internal static HardwareFacts Create(
        ByteCount installedSystemMemory,
        ByteCount installedDedicatedDeviceMemory,
        ByteCount freeStorage,
        IReadOnlySet<DeviceRouteId> presentDevices,
        IReadOnlySet<CompatibilityBackend> verifiedBackends)
    {
        ArgumentNullException.ThrowIfNull(presentDevices);
        ArgumentNullException.ThrowIfNull(verifiedBackends);

        if (installedSystemMemory == ByteCount.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(installedSystemMemory),
                "Zero installed system memory is not a machine; it is a failed read.");
        }

        return new HardwareFacts(
            installedSystemMemory,
            installedDedicatedDeviceMemory,
            freeStorage,
            new HashSet<DeviceRouteId>(presentDevices),
            new HashSet<CompatibilityBackend>(verifiedBackends));
    }
}
