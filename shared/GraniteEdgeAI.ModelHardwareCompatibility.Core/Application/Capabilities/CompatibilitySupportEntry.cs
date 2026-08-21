using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;

/// <summary>
/// One admitted configuration shape, written down by a person.
///
/// The matrix is deliberately explicit rather than generative: every
/// combination that can be offered to a user exists because someone declared
/// it, so an unintended pairing of backend, device and format cannot appear by
/// accident. The only axis the generator expands is context length.
/// </summary>
internal sealed record CompatibilitySupportEntry
{
    private CompatibilitySupportEntry(
        string entryId,
        RuntimeRouteId route,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GpuOffloadLevel offload,
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        int minimumContextTokens,
        int maximumContextTokens,
        SupportLevel level,
        bool requiresEvidence)
    {
        EntryId = entryId;
        Route = route;
        Backend = backend;
        Device = device;
        Offload = offload;
        Weights = weights;
        KvCache = kvCache;
        MinimumContextTokens = minimumContextTokens;
        MaximumContextTokens = maximumContextTokens;
        Level = level;
        RequiresEvidence = requiresEvidence;
    }

    /// <summary>Stable identifier a generated candidate carries as provenance.</summary>
    internal string EntryId { get; }

    internal RuntimeRouteId Route { get; }

    internal CompatibilityBackend Backend { get; }

    internal DeviceRouteId Device { get; }

    internal GpuOffloadLevel Offload { get; }

    internal GgufWeightFormat Weights { get; }

    internal GgufKvCacheFormat KvCache { get; }

    internal int MinimumContextTokens { get; }

    internal int MaximumContextTokens { get; }

    internal SupportLevel Level { get; }

    /// <summary>
    /// True when the entry declares a quality, performance or stability
    /// threshold that measured evidence must satisfy before a candidate from it
    /// may be offered.
    /// </summary>
    internal bool RequiresEvidence { get; }

    internal static CompatibilitySupportEntry Create(
        string entryId,
        RuntimeRouteId route,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GpuOffloadLevel offload,
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        int minimumContextTokens,
        int maximumContextTokens,
        SupportLevel level,
        bool requiresEvidence)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            throw new ArgumentException(
                "An entry must be named so a candidate can carry its provenance.",
                nameof(entryId));
        }

        if (level == SupportLevel.Unknown)
        {
            throw new ArgumentException(
                "An entry must state a support claim. The absence of a claim is "
                + "expressed by the entry not existing.",
                nameof(level));
        }

        if (route != RuntimeRouteId.LlamaCpp)
        {
            throw new ArgumentException(
                "Only the llama.cpp route has an entry shape today; OpenVINO gets "
                + "its own entry type rather than optional fields on this one.",
                nameof(route));
        }

        if (minimumContextTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumContextTokens),
                "A minimum context must be a positive number of tokens.");
        }

        if (maximumContextTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumContextTokens),
                "A maximum context must be a positive number of tokens.");
        }

        if (maximumContextTokens < minimumContextTokens)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumContextTokens),
                "A maximum below the minimum admits nothing, so the entry could "
                + "never produce a candidate.");
        }

        // Building the configuration now proves the shape is one the route will
        // accept. An entry that cannot be turned into a configuration would sit
        // in the matrix and fail only when a user selected it.
        _ = GgufRouteConfiguration.Create(weights, kvCache, backend, device, offload);

        return new CompatibilitySupportEntry(
            entryId,
            route,
            backend,
            device,
            offload,
            weights,
            kvCache,
            minimumContextTokens,
            maximumContextTokens,
            level,
            requiresEvidence);
    }

    internal GgufRouteConfiguration ToRouteConfiguration() =>
        GgufRouteConfiguration.Create(Weights, KvCache, Backend, Device, Offload);
}
