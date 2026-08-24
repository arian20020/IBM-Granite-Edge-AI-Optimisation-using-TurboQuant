using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// Which executor a plan belongs to.
///
/// Closed, and never inferred from a path or a filename. A route guessed from
/// an extension is a route that can be wrong, and being wrong here means
/// handing a GGUF plan to the OpenVINO executor.
/// </summary>
public enum OptimizationRoute
{
    Gguf = 1,
    OpenVino = 2
}

/// <summary>
/// One OpenVINO combination that evidence has actually admitted.
///
/// Not a combination a document mentions. The design is explicit that a
/// planning example describes intended coverage rather than proving the
/// installed toolchain supports anything, so every entry here has to name the
/// evidence record behind it.
/// </summary>
public sealed record OpenVinoAdmittedConfiguration
{
    private OpenVinoAdmittedConfiguration(
        string evidenceId,
        DeviceRouteId device,
        OpenVinoWeightFormat weights,
        OpenVinoKvCacheFormat kvCache,
        OpenVinoPerformanceHint performanceHint,
        OpenVinoCompiledCachePolicy compiledCache,
        int streams,
        int minimumContextTokens,
        int maximumContextTokens,
        SupportLevel level,
        bool requiresEvidence)
    {
        EvidenceId = evidenceId;
        Device = device;
        Weights = weights;
        KvCache = kvCache;
        PerformanceHint = performanceHint;
        CompiledCache = compiledCache;
        Streams = streams;
        MinimumContextTokens = minimumContextTokens;
        MaximumContextTokens = maximumContextTokens;
        Level = level;
        RequiresEvidence = requiresEvidence;
    }

    /// <summary>
    /// The record that admitted this combination. A candidate carries it so a
    /// configuration can always be traced back to what allowed it.
    /// </summary>
    public string EvidenceId { get; }

    public DeviceRouteId Device { get; }

    public OpenVinoWeightFormat Weights { get; }

    public OpenVinoKvCacheFormat KvCache { get; }

    public OpenVinoPerformanceHint PerformanceHint { get; }

    public OpenVinoCompiledCachePolicy CompiledCache { get; }

    public int Streams { get; }

    public int MinimumContextTokens { get; }

    public int MaximumContextTokens { get; }

    public SupportLevel Level { get; }

    public bool RequiresEvidence { get; }

    public static OpenVinoAdmittedConfiguration Create(
        string evidenceId,
        DeviceRouteId device,
        OpenVinoWeightFormat weights,
        OpenVinoKvCacheFormat kvCache,
        OpenVinoPerformanceHint performanceHint,
        OpenVinoCompiledCachePolicy compiledCache,
        int streams,
        int minimumContextTokens,
        int maximumContextTokens,
        SupportLevel level,
        bool requiresEvidence)
    {
        OptimizationIdentifier.Require(
            evidenceId,
            nameof(evidenceId),
            "The evidence record behind an admitted combination");

        if (level == SupportLevel.Unknown)
        {
            throw new ArgumentException(
                "Support level Unknown fails closed: a combination whose standing "
                + "was never established must not be planned against.",
                nameof(level));
        }

        // Validated by constructing the configuration it describes, so an
        // admitted entry and a generated candidate can never disagree about
        // what counts as a valid combination.
        _ = OpenVinoRouteConfiguration.Create(
            weights, kvCache, device, performanceHint, compiledCache, streams);

        if (minimumContextTokens < 1 || maximumContextTokens < minimumContextTokens)
        {
            throw new ArgumentException(
                OptimizationBounds.Bounds(minimumContextTokens, maximumContextTokens)
                    + " so no context length could be admitted from them.",
                nameof(minimumContextTokens));
        }

        // TurboQuant is gated on exact evidence. Declaring it as ordinary
        // released support is the one mislabelling that would let it run
        // without anybody opting in, so it is refused at the door rather than
        // filtered later.
        if (weights is OpenVinoWeightFormat.TurboQuantTbq4 or OpenVinoWeightFormat.TurboQuantTbq3
            && level != SupportLevel.Experimental)
        {
            throw new ArgumentException(
                $"{weights} is experimental and may only be admitted as an "
                + "experimental entry; declaring it as released support would let it "
                + "run without an explicit opt-in.",
                nameof(level));
        }

        return new OpenVinoAdmittedConfiguration(
            evidenceId,
            device,
            weights,
            kvCache,
            performanceHint,
            compiledCache,
            streams,
            minimumContextTokens,
            maximumContextTokens,
            level,
            requiresEvidence);
    }
}

/// <summary>One GGUF combination that evidence has actually admitted.</summary>
public sealed record GgufAdmittedConfiguration
{
    private GgufAdmittedConfiguration(
        string evidenceId,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        GpuOffloadLevel offload,
        int minimumContextTokens,
        int maximumContextTokens,
        SupportLevel level,
        bool requiresEvidence)
    {
        EvidenceId = evidenceId;
        Backend = backend;
        Device = device;
        Weights = weights;
        KvCache = kvCache;
        Offload = offload;
        MinimumContextTokens = minimumContextTokens;
        MaximumContextTokens = maximumContextTokens;
        Level = level;
        RequiresEvidence = requiresEvidence;
    }

    public string EvidenceId { get; }

    public CompatibilityBackend Backend { get; }

    public DeviceRouteId Device { get; }

    public GgufWeightFormat Weights { get; }

    public GgufKvCacheFormat KvCache { get; }

    public GpuOffloadLevel Offload { get; }

    public int MinimumContextTokens { get; }

    public int MaximumContextTokens { get; }

    public SupportLevel Level { get; }

    public bool RequiresEvidence { get; }

    public static GgufAdmittedConfiguration Create(
        string evidenceId,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        GpuOffloadLevel offload,
        int minimumContextTokens,
        int maximumContextTokens,
        SupportLevel level,
        bool requiresEvidence)
    {
        OptimizationIdentifier.Require(
            evidenceId,
            nameof(evidenceId),
            "The evidence record behind an admitted combination");

        if (level == SupportLevel.Unknown)
        {
            throw new ArgumentException(
                "Support level Unknown fails closed.", nameof(level));
        }

        if (weights == GgufWeightFormat.Unspecified
            || kvCache == GgufKvCacheFormat.Unspecified
            || backend == CompatibilityBackend.Unspecified
            || device == DeviceRouteId.Unspecified)
        {
            throw new ArgumentException(
                "An admitted combination must be complete; Unspecified is the "
                + "absence of a choice rather than a modest one.",
                nameof(weights));
        }

        if (minimumContextTokens < 1 || maximumContextTokens < minimumContextTokens)
        {
            throw new ArgumentException(
                OptimizationBounds.Bounds(minimumContextTokens, maximumContextTokens),
                nameof(minimumContextTokens));
        }

        return new GgufAdmittedConfiguration(
            evidenceId,
            backend,
            device,
            weights,
            kvCache,
            offload,
            minimumContextTokens,
            maximumContextTokens,
            level,
            requiresEvidence);
    }
}

/// <summary>
/// What the OpenVINO route can currently do on this machine.
///
/// Sealed. Shared code compares normalised metrics derived from this; it never
/// reaches in and reinterprets a route-only field.
/// </summary>
public sealed record OpenVinoCapabilityPayload
{
    private OpenVinoCapabilityPayload(
        string runtimeVersion, IReadOnlyList<OpenVinoAdmittedConfiguration> admitted)
    {
        RuntimeVersion = runtimeVersion;
        Admitted = admitted;
    }

    /// <summary>
    /// The runtime build these admissions were established against. Bound into
    /// the plan so an executor running a different build fails closed instead
    /// of assuming the evidence still applies.
    /// </summary>
    public string RuntimeVersion { get; }

    public IReadOnlyList<OpenVinoAdmittedConfiguration> Admitted { get; }

    public static OpenVinoCapabilityPayload Create(
        string runtimeVersion, IReadOnlyList<OpenVinoAdmittedConfiguration> admitted)
    {
        ArgumentNullException.ThrowIfNull(admitted);

        // The same OpenVINO runtime build identity the execution payload
        // carries, so it takes the same rule. The official runtime reports
        // slash-delimited release-channel structure, and validating it as a
        // generic identifier here would reproduce the exact defect V2.1
        // corrects one field away.
        //
        // The GGUF payload below is deliberately not changed: llama.cpp build
        // tags have no published slash-bearing form, and widening a rule
        // without evidence is how a guard stops meaning anything.
        OptimizationBuildIdentity.Require(
            runtimeVersion,
            nameof(runtimeVersion),
            "The runtime build a capability payload was established against");

        if (admitted.Count == 0)
        {
            throw new ArgumentException(
                "An empty admitted set is not a route with modest capability, it is "
                + "a route with no evidence. Planning from it would produce nothing "
                + "while reporting that the machine had been assessed.",
                nameof(admitted));
        }

        // Copied so a caller still holding the list cannot add an unadmitted
        // combination after the snapshot has been hashed.
        return new OpenVinoCapabilityPayload(runtimeVersion, [.. admitted]);
    }
}

/// <summary>Shared validation phrasing for the two admitted-configuration records.</summary>
internal static class OptimizationBounds
{
    /// <summary>
    /// One phrasing of an impossible context range, shared so the two routes
    /// cannot describe the same defect differently.
    /// </summary>
    internal static string Bounds(int minimum, int maximum) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Context bounds {minimum}..{maximum} are not a range,");
}

/// <summary>What the GGUF route can currently do on this machine.</summary>
public sealed record GgufCapabilityPayload
{
    private GgufCapabilityPayload(
        string runtimeVersion, IReadOnlyList<GgufAdmittedConfiguration> admitted)
    {
        RuntimeVersion = runtimeVersion;
        Admitted = admitted;
    }

    public string RuntimeVersion { get; }

    public IReadOnlyList<GgufAdmittedConfiguration> Admitted { get; }

    public static GgufCapabilityPayload Create(
        string runtimeVersion, IReadOnlyList<GgufAdmittedConfiguration> admitted)
    {
        ArgumentNullException.ThrowIfNull(admitted);

        OptimizationIdentifier.Require(
            runtimeVersion,
            nameof(runtimeVersion),
            "The runtime build a capability payload was established against");

        if (admitted.Count == 0)
        {
            throw new ArgumentException(
                "An empty admitted set is a route with no evidence, not a modest one.",
                nameof(admitted));
        }

        return new GgufCapabilityPayload(runtimeVersion, [.. admitted]);
    }
}

/// <summary>
/// The route evidence a plan was built from: one route, one sealed payload.
///
/// This is the seam that keeps the shared planner honest. A snapshot carrying
/// both payloads would force the shared layer to decide which one it meant, and
/// that decision is precisely the route-specific reasoning the design puts in
/// the executors. So the union is enforced by construction rather than checked
/// afterwards.
/// </summary>
public sealed record OptimizationCapabilitySnapshot
{
    private OptimizationCapabilitySnapshot(
        string snapshotId,
        string capabilitySnapshotSha256,
        OptimizationRoute route,
        GgufCapabilityPayload? gguf,
        OpenVinoCapabilityPayload? openVino)
    {
        SnapshotId = snapshotId;
        CapabilitySnapshotSha256 = capabilitySnapshotSha256;
        Route = route;
        Gguf = gguf;
        OpenVino = openVino;
    }

    public string SnapshotId { get; }

    /// <summary>
    /// Digest of the exact capability evidence C1 planned against. An executor
    /// recomputes it immediately before running; a mismatch is capability drift
    /// and returns ReplanRequired rather than substituting anything.
    /// </summary>
    public string CapabilitySnapshotSha256 { get; }

    public OptimizationRoute Route { get; }

    /// <summary>Non-null exactly when <see cref="Route"/> is Gguf.</summary>
    public GgufCapabilityPayload? Gguf { get; }

    /// <summary>Non-null exactly when <see cref="Route"/> is OpenVino.</summary>
    public OpenVinoCapabilityPayload? OpenVino { get; }

    public static OptimizationCapabilitySnapshot ForGguf(
        string snapshotId, string capabilitySnapshotSha256, GgufCapabilityPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Validate(snapshotId, capabilitySnapshotSha256);

        return new OptimizationCapabilitySnapshot(
            snapshotId, capabilitySnapshotSha256, OptimizationRoute.Gguf, payload, null);
    }

    public static OptimizationCapabilitySnapshot ForOpenVino(
        string snapshotId, string capabilitySnapshotSha256, OpenVinoCapabilityPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Validate(snapshotId, capabilitySnapshotSha256);

        return new OptimizationCapabilitySnapshot(
            snapshotId, capabilitySnapshotSha256, OptimizationRoute.OpenVino, null, payload);
    }

    private static void Validate(string snapshotId, string digest)
    {
        OptimizationIdentifier.Require(
            snapshotId, nameof(snapshotId), "A capability snapshot");

        if (!OptimizationDigest.IsCanonical(digest))
        {
            throw new ArgumentException(
                "A capability digest must be exactly 64 lowercase hex characters. "
                + "An executor compares it ordinally, so an uppercase digest would "
                + "fail to match a snapshot that had not actually changed.",
                nameof(digest));
        }
    }
}
