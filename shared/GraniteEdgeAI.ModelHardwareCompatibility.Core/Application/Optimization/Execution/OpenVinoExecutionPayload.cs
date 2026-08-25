using System.Collections.ObjectModel;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

/// <summary>
/// How OpenVINO stores the weights, in the route's own words.
///
/// Mirrors <c>OpenVinoWeightPrecision</c> from the published OpenVINO route
/// contracts member for member: <c>Fp16</c>, <c>EightBit</c>, <c>FourBit</c>.
///
/// Not translated into GGUF terms, and deliberately not this assembly's
/// planning-side <c>OpenVinoWeightFormat</c>. Original is a source-package
/// state rather than an execution precision, so a payload typed on the planning
/// enum could name a value O1 has no arm for.
/// </summary>
public enum OpenVinoWeightPrecision
{
    Fp16,
    EightBit,
    FourBit
}

/// <summary>The cache implementation family the OpenVINO executor must dispatch.</summary>
public enum OpenVinoKvCacheAlgorithm
{
    Released = 1,
    TurboQuant = 2
}

/// <summary>
/// How OpenVINO stores the attention cache.
///
/// ReleasedDefault is a real selection meaning the released runtime default,
/// not an absence. The original version-2 numeric values remain fixed; added
/// members never reinterpret an existing serialized value.
/// </summary>
public enum OpenVinoKvCachePrecision
{
    ReleasedDefault = 0,
    U8 = 1,
    F16 = 2,
    Bf16 = 3,
    U4 = 4,
    Tbq4 = 5,
    Tbq3 = 6
}

/// <summary>
/// The builds an OpenVINO admission was established against.
///
/// Mirrors <c>OpenVinoBuildEvidence</c>. Bound into the plan so an executor
/// running different builds fails closed instead of assuming the evidence still
/// applies to them.
/// </summary>
public sealed record OpenVinoBuildIdentity
{
    private OpenVinoBuildIdentity(
        string runtimeBuild,
        string genAiBuild,
        string tokenizersBuild,
        string workerManifestDigest)
    {
        RuntimeBuild = runtimeBuild;
        GenAiBuild = genAiBuild;
        TokenizersBuild = tokenizersBuild;
        WorkerManifestDigest = workerManifestDigest;
    }

    public string RuntimeBuild { get; }

    public string GenAiBuild { get; }

    public string TokenizersBuild { get; }

    public string WorkerManifestDigest { get; }

    public static OpenVinoBuildIdentity Create(
        string runtimeBuild,
        string genAiBuild,
        string tokenizersBuild,
        string workerManifestDigest)
    {
        // Vendor build strings, not identifiers this product chose. The official
        // runtime reports slash-delimited release-channel structure, and the
        // generic rule rejects a slash because an evidence id or a device id
        // carrying one would be a path. Refusing it here would leave O1 unable
        // to record the authoritative identity without truncating or rewriting
        // it, which is the substitution this contract forbids.
        //
        // WorkerManifestDigest below is not a vendor string and keeps the
        // canonical digest rule.
        OptimizationBuildIdentity.Require(
            runtimeBuild, nameof(runtimeBuild), "The OpenVINO runtime build");
        OptimizationBuildIdentity.Require(
            genAiBuild, nameof(genAiBuild), "The GenAI build");
        OptimizationBuildIdentity.Require(
            tokenizersBuild, nameof(tokenizersBuild), "The tokenizers build");

        if (!OptimizationDigest.IsCanonical(workerManifestDigest))
        {
            throw new ArgumentException(
                "The worker manifest digest must be 64 lowercase hex characters.",
                nameof(workerManifestDigest));
        }

        return new OpenVinoBuildIdentity(
            runtimeBuild, genAiBuild, tokenizersBuild, workerManifestDigest);
    }
}

/// <summary>
/// Evidence admitting an experimental TurboQuant build.
///
/// Mirrors <c>TurboQuantBuildEvidence</c>. Required whenever the plan selects a
/// TurboQuant path and forbidden otherwise: the whole point of the gate is that
/// an experimental route cannot run on ordinary released evidence.
/// </summary>
public sealed record TurboQuantBuildIdentity
{
    private TurboQuantBuildIdentity(
        string sourceCommit,
        string implementationCommit,
        string patchSeriesDigest,
        string runtimeManifestDigest)
    {
        SourceCommit = sourceCommit;
        ImplementationCommit = implementationCommit;
        PatchSeriesDigest = patchSeriesDigest;
        RuntimeManifestDigest = runtimeManifestDigest;
    }

    public string SourceCommit { get; }

    public string ImplementationCommit { get; }

    public string PatchSeriesDigest { get; }

    public string RuntimeManifestDigest { get; }

    public static TurboQuantBuildIdentity Create(
        string sourceCommit,
        string implementationCommit,
        string patchSeriesDigest,
        string runtimeManifestDigest)
    {
        RequireGitCommit(sourceCommit, nameof(sourceCommit));
        RequireGitCommit(implementationCommit, nameof(implementationCommit));

        if (!OptimizationDigest.IsCanonical(patchSeriesDigest))
        {
            throw new ArgumentException(
                "The patch series digest must be 64 lowercase hex characters.",
                nameof(patchSeriesDigest));
        }

        if (!OptimizationDigest.IsCanonical(runtimeManifestDigest))
        {
            throw new ArgumentException(
                "The runtime manifest digest must be 64 lowercase hex characters.",
                nameof(runtimeManifestDigest));
        }

        return new TurboQuantBuildIdentity(
            sourceCommit, implementationCommit, patchSeriesDigest, runtimeManifestDigest);
    }

    private static void RequireGitCommit(string value, string parameter)
    {
        bool valid = value is { Length: 40 }
            && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

        if (!valid)
        {
            throw new ArgumentException(
                "A TurboQuant build identity must be a lowercase 40-character Git "
                + "object identity, matching the published contract.",
                parameter);
        }
    }
}

/// <summary>
/// Everything the OpenVINO executor needs, with nothing left to infer.
///
/// Uses the published route's own terminology throughout - ConfigurationId,
/// WeightPrecision, KvCachePrecision, Device as a string, Maturity, EvidenceId -
/// because O1 already validates against those names and a translated field is
/// one it would have to translate back.
///
/// The compiled-cache policy is carried as its three published fields rather
/// than as a type called <c>OpenVinoCompiledCachePolicy</c>. That name already
/// exists twice in this codebase with different shapes, and this assembly holds
/// an invariant forbidding two types from sharing a short name - a rule that
/// exists so a reviewed member cannot silently exempt an unreviewed one. The
/// field names are preserved exactly; only the wrapper is not reintroduced.
/// </summary>
public sealed record OpenVinoExecutionPayload
{
    private OpenVinoExecutionPayload(
        string configurationId,
        string device,
        string maturity,
        string evidenceId,
        OpenVinoWeightPrecision sourceWeightPrecision,
        OpenVinoWeightPrecision targetWeightPrecision,
        OpenVinoKvCacheAlgorithm kvCacheAlgorithm,
        OpenVinoKvCachePrecision kvCachePrecision,
        bool compiledCacheEnabled,
        bool compiledCacheIsDisposable,
        bool compiledCacheIsModelArtifact,
        bool createsCompletePackage,
        OpenVinoBuildIdentity buildIdentity,
        IReadOnlyDictionary<string, string> optimizerVersions,
        TurboQuantBuildIdentity? turboQuantBuild)
    {
        ConfigurationId = configurationId;
        Device = device;
        Maturity = maturity;
        EvidenceId = evidenceId;
        SourceWeightPrecision = sourceWeightPrecision;
        TargetWeightPrecision = targetWeightPrecision;
        KvCacheAlgorithm = kvCacheAlgorithm;
        KvCachePrecision = kvCachePrecision;
        CompiledCacheEnabled = compiledCacheEnabled;
        CompiledCacheIsDisposable = compiledCacheIsDisposable;
        CompiledCacheIsModelArtifact = compiledCacheIsModelArtifact;
        CreatesCompletePackage = createsCompletePackage;
        BuildIdentity = buildIdentity;
        OptimizerVersions = optimizerVersions;
        TurboQuantBuild = turboQuantBuild;
    }

    /// <summary>The registered configuration this plan executes, e.g. an
    /// <c>openvino.standard.cpu.*</c> identifier.</summary>
    public string ConfigurationId { get; }

    /// <summary>The device string the route uses, e.g. <c>CPU</c>.</summary>
    public string Device { get; }

    public string Maturity { get; }

    public string EvidenceId { get; }

    /// <summary>
    /// What the source package is in now. Carried because the published route
    /// refuses an upward conversion, and it can only check that if it knows
    /// where it started.
    /// </summary>
    public OpenVinoWeightPrecision SourceWeightPrecision { get; }

    public OpenVinoWeightPrecision TargetWeightPrecision { get; }

    public OpenVinoKvCacheAlgorithm KvCacheAlgorithm { get; }

    public OpenVinoKvCachePrecision KvCachePrecision { get; }

    public bool CompiledCacheEnabled { get; }

    public bool CompiledCacheIsDisposable { get; }

    /// <summary>
    /// Always false in the published contract, and carried anyway so the plan
    /// states it. The compiled blob is a real file but not a model copy, and
    /// that distinction decides whether the destination offers to save a model.
    /// </summary>
    public bool CompiledCacheIsModelArtifact { get; }

    public bool CreatesCompletePackage { get; }

    public OpenVinoBuildIdentity BuildIdentity { get; }

    /// <summary>
    /// The optimiser toolchain versions, keyed as the published provenance keys
    /// them (<c>nncf</c>, <c>openvino</c>, <c>openvino-genai</c>, <c>optimum</c>,
    /// <c>optimum-intel</c>, <c>transformers</c>). Ordinal keys, copied on
    /// construction, and every entry reaches the digest.
    /// </summary>
    public IReadOnlyDictionary<string, string> OptimizerVersions { get; }

    /// <summary>
    /// Present only for a TurboQuant path. Its absence is what keeps an
    /// experimental route from running on released evidence.
    /// </summary>
    public TurboQuantBuildIdentity? TurboQuantBuild { get; }

    /// <summary>
    /// Whether executing this writes a new package. A target precision equal to
    /// the source converts nothing.
    /// </summary>
    public bool RequiresPersistentConversion =>
        TargetWeightPrecision != SourceWeightPrecision;

    public static OpenVinoExecutionPayload Create(
        string configurationId,
        string device,
        string maturity,
        string evidenceId,
        OpenVinoWeightPrecision sourceWeightPrecision,
        OpenVinoWeightPrecision targetWeightPrecision,
        OpenVinoKvCachePrecision kvCachePrecision,
        bool compiledCacheEnabled,
        bool compiledCacheIsDisposable,
        bool compiledCacheIsModelArtifact,
        bool createsCompletePackage,
        OpenVinoBuildIdentity buildIdentity,
        IReadOnlyDictionary<string, string> optimizerVersions,
        TurboQuantBuildIdentity? turboQuantBuild = null,
        OpenVinoKvCacheAlgorithm kvCacheAlgorithm = OpenVinoKvCacheAlgorithm.Released) =>
        CreateCore(
            configurationId,
            device,
            maturity,
            evidenceId,
            sourceWeightPrecision,
            targetWeightPrecision,
            kvCachePrecision,
            compiledCacheEnabled,
            compiledCacheIsDisposable,
            compiledCacheIsModelArtifact,
            createsCompletePackage,
            buildIdentity,
            optimizerVersions,
            turboQuantBuild,
            kvCacheAlgorithm,
            enforceVersionThreeCacheRules: true);

    /// <summary>
    /// Reconstructs the former version-2 payload shape for canonical digest
    /// verification only. New plans are issued through <see cref="Create"/>.
    /// </summary>
    internal static OpenVinoExecutionPayload CreateV2(
        string configurationId,
        string device,
        string maturity,
        string evidenceId,
        OpenVinoWeightPrecision sourceWeightPrecision,
        OpenVinoWeightPrecision targetWeightPrecision,
        OpenVinoKvCachePrecision kvCachePrecision,
        bool compiledCacheEnabled,
        bool compiledCacheIsDisposable,
        bool compiledCacheIsModelArtifact,
        bool createsCompletePackage,
        OpenVinoBuildIdentity buildIdentity,
        IReadOnlyDictionary<string, string> optimizerVersions,
        TurboQuantBuildIdentity? turboQuantBuild = null)
    {
        OpenVinoExecutionPayload payload = CreateCore(
            configurationId,
            device,
            maturity,
            evidenceId,
            sourceWeightPrecision,
            targetWeightPrecision,
            kvCachePrecision,
            compiledCacheEnabled,
            compiledCacheIsDisposable,
            compiledCacheIsModelArtifact,
            createsCompletePackage,
            buildIdentity,
            optimizerVersions,
            turboQuantBuild,
            OpenVinoKvCacheAlgorithm.Released,
            enforceVersionThreeCacheRules: false);

        payload.RequireVersionTwoCacheVocabulary();
        return payload;
    }

    /// <summary>
    /// Guards the immutable version-2 cache vocabulary wherever a legacy
    /// payload is reconstructed or canonicalized.
    /// </summary>
    internal void RequireVersionTwoCacheVocabulary()
    {
        bool legacyPrecision = KvCachePrecision is
            OpenVinoKvCachePrecision.ReleasedDefault or OpenVinoKvCachePrecision.U8;

        if (KvCacheAlgorithm != OpenVinoKvCacheAlgorithm.Released || !legacyPrecision)
        {
            throw new ArgumentException(
                "Version 2 permits only the released default or U8 cache precision; "
                + "post-version-2 cache vocabulary cannot be represented by its digest.",
                nameof(KvCachePrecision));
        }
    }

    private static OpenVinoExecutionPayload CreateCore(
        string configurationId,
        string device,
        string maturity,
        string evidenceId,
        OpenVinoWeightPrecision sourceWeightPrecision,
        OpenVinoWeightPrecision targetWeightPrecision,
        OpenVinoKvCachePrecision kvCachePrecision,
        bool compiledCacheEnabled,
        bool compiledCacheIsDisposable,
        bool compiledCacheIsModelArtifact,
        bool createsCompletePackage,
        OpenVinoBuildIdentity buildIdentity,
        IReadOnlyDictionary<string, string> optimizerVersions,
        TurboQuantBuildIdentity? turboQuantBuild,
        OpenVinoKvCacheAlgorithm kvCacheAlgorithm,
        bool enforceVersionThreeCacheRules)
    {
        ArgumentNullException.ThrowIfNull(buildIdentity);
        ArgumentNullException.ThrowIfNull(optimizerVersions);

        OptimizationIdentifier.Require(
            configurationId, nameof(configurationId), "The OpenVINO configuration");
        OptimizationIdentifier.Require(device, nameof(device), "The device");
        OptimizationIdentifier.Require(maturity, nameof(maturity), "The maturity");
        OptimizationIdentifier.Require(evidenceId, nameof(evidenceId), "The evidence record");

        RequireDefined(sourceWeightPrecision, nameof(sourceWeightPrecision));
        RequireDefined(targetWeightPrecision, nameof(targetWeightPrecision));
        RequireDefined(kvCacheAlgorithm, nameof(kvCacheAlgorithm));
        RequireDefined(kvCachePrecision, nameof(kvCachePrecision));

        if (enforceVersionThreeCacheRules)
        {
            bool turboPrecision = kvCachePrecision is OpenVinoKvCachePrecision.Tbq4
                or OpenVinoKvCachePrecision.Tbq3;
            bool turboAlgorithm = kvCacheAlgorithm == OpenVinoKvCacheAlgorithm.TurboQuant;

            if (turboPrecision != turboAlgorithm)
            {
                throw new ArgumentException(
                    "Cache algorithm and precision must name the same released or "
                    + "TurboQuant family.",
                    nameof(kvCacheAlgorithm));
            }

            if (turboAlgorithm && turboQuantBuild is null)
            {
                throw new ArgumentException(
                    "A TurboQuant cache requires its exact pinned build identity.",
                    nameof(turboQuantBuild));
            }

            if (!turboAlgorithm && turboQuantBuild is not null)
            {
                throw new ArgumentException(
                    "A released cache must not carry a TurboQuant build identity.",
                    nameof(turboQuantBuild));
            }
        }

        // The published route refuses this outright: converting upward never
        // restores quality that was already discarded.
        if (targetWeightPrecision == OpenVinoWeightPrecision.Fp16
            && sourceWeightPrecision != OpenVinoWeightPrecision.Fp16)
        {
            throw new ArgumentException(
                "Converting up to Fp16 from a lower precision does not restore "
                + "quality, and the published OpenVINO route refuses it.",
                nameof(targetWeightPrecision));
        }

        if (compiledCacheIsModelArtifact)
        {
            throw new ArgumentException(
                "The compiled cache is not a model artifact. Declaring it as one "
                + "would make a runtime-only result claim it created a model.",
                nameof(compiledCacheIsModelArtifact));
        }

        // Copied entry by entry rather than through a projection, for the same
        // reason the canonicalizer avoids one: a string-returning lambda is a
        // member the privacy canary must review under a name that moves.
        SortedDictionary<string, string> copied = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> version in optimizerVersions)
        {
            OptimizationIdentifier.Require(
                version.Key, nameof(optimizerVersions), "An optimiser name");
            OptimizationIdentifier.Require(
                version.Value, nameof(optimizerVersions), "An optimiser version");
            if (!copied.TryAdd(version.Key, version.Value))
            {
                throw new ArgumentException(
                    "Optimizer version keys must be unique.",
                    nameof(optimizerVersions));
            }
        }
        if (copied.Count == 0)
        {
            throw new ArgumentException(
                "The optimiser toolchain must be pinned. Without versions, an "
                + "output could be produced by a different toolchain than the one "
                + "the plan was validated against.",
                nameof(optimizerVersions));
        }

        return new OpenVinoExecutionPayload(
            configurationId,
            device,
            maturity,
            evidenceId,
            sourceWeightPrecision,
            targetWeightPrecision,
            kvCacheAlgorithm,
            kvCachePrecision,
            compiledCacheEnabled,
            compiledCacheIsDisposable,
            compiledCacheIsModelArtifact,
            createsCompletePackage,
            buildIdentity,
            new ReadOnlyDictionary<string, string>(copied),
            turboQuantBuild);
    }

    private static void RequireDefined<T>(T value, string parameter)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                parameter, value, "An undefined value cannot be executed.");
        }
    }
}
