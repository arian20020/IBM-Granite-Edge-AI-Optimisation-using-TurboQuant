using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// A complete llama.cpp configuration. Every field is required, so there is no
/// partially specified GGUF configuration anywhere in the system.
/// </summary>
public sealed record GgufRouteConfiguration : RouteConfiguration
{
    private GgufRouteConfiguration(
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GpuOffloadLevel offload)
    {
        Weights = weights;
        KvCache = kvCache;
        Backend = backend;
        Device = device;
        Offload = offload;
    }

    public GgufWeightFormat Weights { get; }

    public GgufKvCacheFormat KvCache { get; }

    public CompatibilityBackend Backend { get; }

    public DeviceRouteId Device { get; }

    public GpuOffloadLevel Offload { get; }

    public override RuntimeRouteId RouteId => RuntimeRouteId.LlamaCpp;

    public override string CanonicalDescriptor =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"gguf|w={Weights}|kv={KvCache}|be={Backend}|dev={Device}|off={Offload}");

    public static GgufRouteConfiguration Create(
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GpuOffloadLevel offload)
    {
        RequireDefined(weights, nameof(weights));
        RequireDefined(kvCache, nameof(kvCache));
        RequireDefined(backend, nameof(backend));
        RequireDefined(device, nameof(device));
        RequireDefined(offload, nameof(offload));

        if (weights == GgufWeightFormat.Unspecified)
        {
            throw new ArgumentException(
                "A GGUF configuration must declare a weight format.", nameof(weights));
        }

        if (kvCache == GgufKvCacheFormat.Unspecified)
        {
            throw new ArgumentException(
                "A GGUF configuration must declare a KV-cache format.", nameof(kvCache));
        }

        // Route mixing is rejected at construction rather than filtered later,
        // so an OpenVINO fact can never reach a llama.cpp estimate.
        if (backend is CompatibilityBackend.Unspecified
            or CompatibilityBackend.OpenVinoCpu
            or CompatibilityBackend.OpenVinoGpu
            or CompatibilityBackend.OpenVinoNpu)
        {
            throw new ArgumentException(
                "A llama.cpp configuration must use a llama.cpp backend; "
                + "OpenVINO backends belong to the OpenVINO route.",
                nameof(backend));
        }

        if (device is DeviceRouteId.Unspecified or DeviceRouteId.IntelNpu)
        {
            throw new ArgumentException(
                "A llama.cpp configuration must target CPU or an Intel GPU; "
                + "no NPU route is admitted for llama.cpp.",
                nameof(device));
        }

        if (offload == GpuOffloadLevel.Unspecified)
        {
            throw new ArgumentException(
                "A GGUF configuration must declare its GPU offload level.",
                nameof(offload));
        }

        if (device == DeviceRouteId.Cpu && offload != GpuOffloadLevel.None)
        {
            throw new ArgumentException(
                "A CPU device cannot offload layers to a GPU.", nameof(offload));
        }

        return new GgufRouteConfiguration(weights, kvCache, backend, device, offload);
    }

    private static void RequireDefined<T>(T value, string parameter)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                parameter, value, "An undefined GGUF configuration value cannot run.");
        }
    }
}
