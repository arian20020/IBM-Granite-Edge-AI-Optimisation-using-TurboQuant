using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// A complete llama.cpp configuration. Every field is required, so there is no
/// partially specified GGUF configuration anywhere in the system.
/// </summary>
internal sealed record GgufRouteConfiguration : RouteConfiguration
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

    internal GgufWeightFormat Weights { get; }

    internal GgufKvCacheFormat KvCache { get; }

    internal CompatibilityBackend Backend { get; }

    internal DeviceRouteId Device { get; }

    internal GpuOffloadLevel Offload { get; }

    internal override RuntimeRouteId RouteId => RuntimeRouteId.LlamaCpp;

    internal override string CanonicalDescriptor =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"gguf|w={Weights}|kv={KvCache}|be={Backend}|dev={Device}|off={Offload}");

    internal static GgufRouteConfiguration Create(
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GpuOffloadLevel offload)
    {
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
}
