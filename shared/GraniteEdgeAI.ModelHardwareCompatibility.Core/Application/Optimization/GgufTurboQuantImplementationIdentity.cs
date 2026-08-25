using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// Exact GGUF TurboQuant implementation admitted by capability evidence.
///
/// This is deliberately a closed two-entry identity rather than a generic
/// runtime label. No runtime manifest is inferred or fabricated: only the two
/// pinned source implementations may be represented.
/// </summary>
public sealed record GgufTurboQuantImplementationIdentity
{
    private GgufTurboQuantImplementationIdentity(
        string runtimeName,
        string sourceCommit,
        CompatibilityBackend backend,
        DeviceRouteId device)
    {
        RuntimeName = runtimeName;
        SourceCommit = sourceCommit;
        Backend = backend;
        Device = device;
    }

    public string RuntimeName { get; }

    public string SourceCommit { get; }

    public CompatibilityBackend Backend { get; }

    public DeviceRouteId Device { get; }

    public static GgufTurboQuantImplementationIdentity Create(
        string runtimeName,
        string sourceCommit,
        CompatibilityBackend backend,
        DeviceRouteId device)
    {
        bool atomicBotVulkan = string.Equals(
                runtimeName, "turbo3", StringComparison.Ordinal)
            && string.Equals(
                sourceCommit,
                "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
                StringComparison.Ordinal)
            && backend == CompatibilityBackend.IntelVulkan
            && device == DeviceRouteId.IntelIntegratedGpu;
        bool animehackerSycl = string.Equals(
                runtimeName, "tq3_0", StringComparison.Ordinal)
            && string.Equals(
                sourceCommit,
                "5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc",
                StringComparison.Ordinal)
            && backend == CompatibilityBackend.IntelSycl
            && device == DeviceRouteId.IntelDiscreteGpu;

        if (!atomicBotVulkan && !animehackerSycl)
        {
            throw new ArgumentException(
                "GGUF TurboQuant identity must be one exact pinned runtime, source "
                + "commit, backend, and device capability; arbitrary availability "
                + "fails closed.",
                nameof(runtimeName));
        }

        return new GgufTurboQuantImplementationIdentity(
            runtimeName, sourceCommit, backend, device);
    }
}
