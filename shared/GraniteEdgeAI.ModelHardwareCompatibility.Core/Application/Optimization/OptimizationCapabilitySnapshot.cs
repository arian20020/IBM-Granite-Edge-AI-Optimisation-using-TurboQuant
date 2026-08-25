using System.Collections.ObjectModel;
using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
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

        OptimizationSupportLevelPolicy.RequireAdmitted(level, nameof(level));

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
        if ((kvCache is OpenVinoKvCacheFormat.TurboQuantTbq4
                or OpenVinoKvCacheFormat.TurboQuantTbq3)
            && level != SupportLevel.Experimental)
        {
            throw new ArgumentException(
                "TurboQuant KV-cache configurations require experimental evidence.",
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

        OptimizationSupportLevelPolicy.RequireAdmitted(level, nameof(level));

        GgufRouteConfiguration validated = GgufRouteConfiguration.Create(
            weights, kvCache, backend, device, offload);

        if (minimumContextTokens < 1 || maximumContextTokens < minimumContextTokens)
        {
            throw new ArgumentException(
                OptimizationBounds.Bounds(minimumContextTokens, maximumContextTokens),
                nameof(minimumContextTokens));
        }

        if (kvCache == GgufKvCacheFormat.TurboQuant3Bit
            && (level != SupportLevel.Experimental || !requiresEvidence))
        {
            throw new ArgumentException(
                "GGUF TurboQuant cache requires experimental support and exact "
                + "evidence admission.",
                nameof(kvCache));
        }

        return new GgufAdmittedConfiguration(
            evidenceId,
            validated.Backend,
            validated.Device,
            validated.Weights,
            validated.KvCache,
            validated.Offload,
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
        string runtimeVersion,
        IReadOnlyList<OpenVinoAdmittedConfiguration> admitted,
        IReadOnlyDictionary<string, OpenVinoExecutionAuthority> executionAuthorities)
    {
        RuntimeVersion = runtimeVersion;
        Admitted = admitted;
        ExecutionAuthorities = executionAuthorities;
    }

    /// <summary>
    /// The runtime build these admissions were established against. Bound into
    /// the plan so an executor running a different build fails closed instead
    /// of assuming the evidence still applies.
    /// </summary>
    public string RuntimeVersion { get; }

    public IReadOnlyList<OpenVinoAdmittedConfiguration> Admitted { get; }

    public IReadOnlyDictionary<string, OpenVinoExecutionAuthority> ExecutionAuthorities { get; }

    public static OpenVinoCapabilityPayload Create(
        string runtimeVersion,
        IReadOnlyList<OpenVinoAdmittedConfiguration> admitted,
        IReadOnlyList<OpenVinoExecutionAuthority>? executionAuthorities = null)
    {
        ArgumentNullException.ThrowIfNull(admitted);

        OpenVinoAdmittedConfiguration[] admittedSnapshot = [.. admitted];
        OpenVinoExecutionAuthority[] authoritySnapshot =
            executionAuthorities is null ? [] : [.. executionAuthorities];

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

        if (admittedSnapshot.Length == 0)
        {
            throw new ArgumentException(
                "An empty admitted set is not a route with modest capability, it is "
                + "a route with no evidence. Planning from it would produce nothing "
                + "while reporting that the machine had been assessed.",
                nameof(admitted));
        }

        OptimizationAdmissionIdentity.RequireUnique(
            admittedSnapshot.Select(entry => entry.EvidenceId), nameof(admitted));

        SortedDictionary<string, OpenVinoExecutionAuthority> copiedAuthorities =
            new(StringComparer.Ordinal);
        foreach (OpenVinoExecutionAuthority authority in authoritySnapshot)
        {
            ArgumentNullException.ThrowIfNull(authority);
            OpenVinoAdmittedConfiguration? authoritativeAdmission = admittedSnapshot
                .SingleOrDefault(entry => string.Equals(
                    entry.EvidenceId, authority.EvidenceId, StringComparison.Ordinal));
            if (!string.Equals(
                    runtimeVersion,
                    authority.BuildIdentity.RuntimeBuild,
                    StringComparison.Ordinal)
                || authoritativeAdmission is null)
            {
                throw new ArgumentException(
                    "OpenVINO execution authority must name this runtime and admitted evidence.",
                    nameof(executionAuthorities));
            }
            bool turboCache = authoritativeAdmission.KvCache is
                OpenVinoKvCacheFormat.TurboQuantTbq4
                or OpenVinoKvCacheFormat.TurboQuantTbq3;
            if (turboCache != (authority.TurboQuantBuild is not null))
            {
                throw new ArgumentException(
                    "OpenVINO TurboQuant execution identity must agree with the admitted cache family.",
                    nameof(executionAuthorities));
            }
            if (!copiedAuthorities.TryAdd(authority.EvidenceId, authority))
            {
                throw new ArgumentException(
                    "OpenVINO execution authority evidence identifiers must be unique.",
                    nameof(executionAuthorities));
            }
        }

        // Copied so a caller still holding the list cannot add an unadmitted
        // combination after the snapshot has been hashed.
        return new OpenVinoCapabilityPayload(
            runtimeVersion,
            Array.AsReadOnly(admittedSnapshot),
            new ReadOnlyDictionary<string, OpenVinoExecutionAuthority>(
                copiedAuthorities));
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

/// <summary>The complete allowlist of support claims that may authorize planning.</summary>
internal static class OptimizationSupportLevelPolicy
{
    internal static bool IsAdmitted(SupportLevel level) =>
        level is SupportLevel.DeclaredSupported or SupportLevel.Experimental;

    internal static void RequireAdmitted(SupportLevel level, string parameter)
    {
        if (!IsAdmitted(level))
        {
            throw new ArgumentException(
                "Only explicitly declared or experimental support can be admitted; "
                + "unknown and undefined values fail closed.",
                parameter);
        }
    }
}

internal static class OptimizationAdmissionIdentity
{
    internal static void RequireUnique(IEnumerable<string> evidenceIds, string parameter)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (string evidenceId in evidenceIds)
        {
            if (!seen.Add(evidenceId))
            {
                throw new ArgumentException(
                    "Admitted evidence identifiers must be unique within a route. "
                    + "One identifier naming two configurations cannot bind a policy "
                    + "or plan unambiguously.",
                    parameter);
            }
        }
    }
}

/// <summary>What the GGUF route can currently do on this machine.</summary>
public sealed record GgufCapabilityPayload
{
    private GgufCapabilityPayload(
        string runtimeVersion,
        IReadOnlyList<GgufAdmittedConfiguration> admitted,
        bool hasHigherPrecisionSource,
        GgufRequantisationPolicy? requantisationPolicy,
        GgufConversionSourceBinding? conversionSource,
        GgufQuantiserIdentity? admittedQuantiser,
        GgufTurboQuantImplementationIdentity? turboQuantImplementation,
        GgufRuntimeAuthority? runtimeAuthority)
    {
        RuntimeVersion = runtimeVersion;
        Admitted = admitted;
        HasHigherPrecisionSource = hasHigherPrecisionSource;
        RequantisationPolicy = requantisationPolicy;
        ConversionSource = conversionSource;
        AdmittedQuantiser = admittedQuantiser;
        TurboQuantImplementation = turboQuantImplementation;
        RuntimeAuthority = runtimeAuthority;
    }

    public string RuntimeVersion { get; }

    public IReadOnlyList<GgufAdmittedConfiguration> Admitted { get; }

    /// <summary>
    /// Legacy discovery information only. Conversion authority comes solely
    /// from <see cref="ConversionSource"/> and <see cref="AdmittedQuantiser"/>.
    /// </summary>
    public bool HasHigherPrecisionSource { get; }

    public GgufRequantisationPolicy? RequantisationPolicy { get; }

    public GgufConversionSourceBinding? ConversionSource { get; }

    public GgufQuantiserIdentity? AdmittedQuantiser { get; }

    public GgufTurboQuantImplementationIdentity? TurboQuantImplementation { get; }

    public GgufRuntimeAuthority? RuntimeAuthority { get; }

    public static GgufCapabilityPayload Create(
        string runtimeVersion,
        IReadOnlyList<GgufAdmittedConfiguration> admitted,
        bool hasHigherPrecisionSource = false,
        GgufRequantisationPolicy? requantisationPolicy = null,
        GgufConversionSourceBinding? conversionSource = null,
        GgufQuantiserIdentity? admittedQuantiser = null,
        GgufTurboQuantImplementationIdentity? turboQuantImplementation = null,
        GgufRuntimeAuthority? runtimeAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(admitted);

        GgufAdmittedConfiguration[] admittedSnapshot = [.. admitted];
        GgufAdmittedConfiguration[] validatedAdmissions =
            new GgufAdmittedConfiguration[admittedSnapshot.Length];
        for (int index = 0; index < admittedSnapshot.Length; index++)
        {
            GgufAdmittedConfiguration entry = admittedSnapshot[index];
            ArgumentNullException.ThrowIfNull(entry);
            validatedAdmissions[index] = GgufAdmittedConfiguration.Create(
                entry.EvidenceId,
                entry.Backend,
                entry.Device,
                entry.Weights,
                entry.KvCache,
                entry.Offload,
                entry.MinimumContextTokens,
                entry.MaximumContextTokens,
                entry.Level,
                entry.RequiresEvidence);
        }

        OptimizationIdentifier.Require(
            runtimeVersion,
            nameof(runtimeVersion),
            "The runtime build a capability payload was established against");

        if (validatedAdmissions.Length == 0)
        {
            throw new ArgumentException(
                "An empty admitted set is a route with no evidence, not a modest one.",
                nameof(admitted));
        }

        OptimizationAdmissionIdentity.RequireUnique(
            validatedAdmissions.Select(entry => entry.EvidenceId), nameof(admitted));

        if (runtimeAuthority is not null)
        {
            if (!string.Equals(
                    runtimeVersion,
                    runtimeAuthority.RuntimeBuildId,
                    StringComparison.Ordinal)
                || runtimeAuthority.Profiles.Keys.Any(evidenceId =>
                    validatedAdmissions.All(entry => !string.Equals(
                        entry.EvidenceId, evidenceId, StringComparison.Ordinal))))
            {
                throw new ArgumentException(
                    "GGUF runtime authority must name this build and only admitted evidence.",
                    nameof(runtimeAuthority));
            }
        }

        GgufAdmittedConfiguration[] turboQuant =
        [
            .. validatedAdmissions.Where(entry =>
                entry.KvCache == GgufKvCacheFormat.TurboQuant3Bit)
        ];

        if (turboQuant.Length == 0 && turboQuantImplementation is not null)
        {
            throw new ArgumentException(
                "A non-TurboQuant GGUF capability must carry no TurboQuant identity.",
                nameof(turboQuantImplementation));
        }

        if (turboQuant.Length > 0)
        {
            if (turboQuantImplementation is null)
            {
                throw new ArgumentException(
                    "GGUF TurboQuant remains unavailable without an exact pinned "
                    + "implementation identity.",
                    nameof(turboQuantImplementation));
            }

            if (!string.Equals(
                    runtimeVersion,
                    turboQuantImplementation.RuntimeName,
                    StringComparison.Ordinal)
                || turboQuant.Any(entry =>
                    entry.Backend != turboQuantImplementation.Backend
                    || entry.Device != turboQuantImplementation.Device))
            {
                throw new ArgumentException(
                    "GGUF TurboQuant runtime, backend, or device differs from its "
                    + "pinned implementation identity.",
                    nameof(turboQuantImplementation));
            }

            if (runtimeAuthority is not null
                && !string.Equals(
                    runtimeAuthority.RuntimeSourceCommit,
                    turboQuantImplementation.SourceCommit,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "GGUF TurboQuant runtime authority must use the pinned source commit.",
                    nameof(runtimeAuthority));
            }
        }

        return new GgufCapabilityPayload(
            runtimeVersion,
            Array.AsReadOnly(validatedAdmissions),
            hasHigherPrecisionSource,
            requantisationPolicy,
            conversionSource,
            admittedQuantiser,
            turboQuantImplementation,
            runtimeAuthority);
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
