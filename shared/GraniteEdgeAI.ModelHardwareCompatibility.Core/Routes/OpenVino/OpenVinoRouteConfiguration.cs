using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

/// <summary>
/// One complete OpenVINO setup, described the way the GGUF one is.
///
/// Two routes that describe themselves differently cannot be compared, and
/// comparing them is the entire point of the shared planner. So this carries
/// the same obligations: every field that materially changes what runs reaches
/// the canonical descriptor, and nothing is left implicit.
///
/// Context is deliberately absent. The candidate already carries it, and the
/// fingerprint already composes it; holding it here as well would give one
/// candidate two places to state its context length, which can then disagree.
/// The complete-configuration digest that the plan binds is taken over the
/// candidate, where context and configuration are combined exactly once.
/// </summary>
internal sealed record OpenVinoRouteConfiguration : RouteConfiguration
{
    private OpenVinoRouteConfiguration(
        OpenVinoWeightFormat weights,
        OpenVinoKvCacheFormat kvCache,
        DeviceRouteId device,
        OpenVinoPerformanceHint performanceHint,
        OpenVinoCompiledCachePolicy compiledCache,
        int streams)
    {
        Weights = weights;
        KvCache = kvCache;
        Device = device;
        PerformanceHint = performanceHint;
        CompiledCache = compiledCache;
        Streams = streams;
    }

    internal OpenVinoWeightFormat Weights { get; }

    internal OpenVinoKvCacheFormat KvCache { get; }

    internal DeviceRouteId Device { get; }

    internal OpenVinoPerformanceHint PerformanceHint { get; }

    internal OpenVinoCompiledCachePolicy CompiledCache { get; }

    /// <summary>
    /// How many inference streams the runtime is configured for. More than one
    /// multiplies the runtime's working allocations, so it is part of the
    /// configuration rather than a detail of it.
    /// </summary>
    internal int Streams { get; }

    internal override RuntimeRouteId RouteId => RuntimeRouteId.OpenVinoGenAi;

    /// <summary>
    /// Route-prefixed so two routes cannot produce the same text for
    /// coincidentally similar settings; the digest would otherwise stop
    /// identifying which executor a plan belongs to.
    /// </summary>
    internal override string CanonicalDescriptor =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"openvino|w={Weights}|kv={KvCache}|dev={Device}|hint={PerformanceHint}"
            + $"|cache={CompiledCache}|streams={Streams}");

    internal static OpenVinoRouteConfiguration Create(
        OpenVinoWeightFormat weights,
        OpenVinoKvCacheFormat kvCache,
        DeviceRouteId device,
        OpenVinoPerformanceHint performanceHint,
        OpenVinoCompiledCachePolicy compiledCache,
        int streams)
    {
        // Unspecified is the absence of a choice, not a modest one. Admitting
        // it would produce a candidate nobody could execute and an estimate of
        // something undefined.
        RequireSpecified(weights == OpenVinoWeightFormat.Unspecified, nameof(weights),
            "An OpenVINO configuration must declare how its weights are stored.");

        RequireSpecified(kvCache == OpenVinoKvCacheFormat.Unspecified, nameof(kvCache),
            "An OpenVINO configuration must declare how the context is stored; "
            + "the runtime default is itself a choice and must be named.");

        RequireSpecified(device == DeviceRouteId.Unspecified, nameof(device),
            "An OpenVINO configuration must declare the device it runs on.");

        RequireSpecified(
            performanceHint == OpenVinoPerformanceHint.Unspecified, nameof(performanceHint),
            "An OpenVINO configuration must declare its performance hint, which "
            + "changes how much the runtime commits.");

        RequireSpecified(
            compiledCache == OpenVinoCompiledCachePolicy.Unspecified, nameof(compiledCache),
            "An OpenVINO configuration must declare whether the compiled model is "
            + "cached, because it costs disk the plan has to state.");

        if (streams < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(streams),
                streams,
                "A configuration with no stream cannot run. Accepting zero would let "
                + "a candidate estimate as though inference cost nothing.");
        }

        return new OpenVinoRouteConfiguration(
            weights, kvCache, device, performanceHint, compiledCache, streams);
    }

    private static void RequireSpecified(bool unspecified, string parameter, string message)
    {
        if (unspecified)
        {
            throw new ArgumentException(message, parameter);
        }
    }
}
