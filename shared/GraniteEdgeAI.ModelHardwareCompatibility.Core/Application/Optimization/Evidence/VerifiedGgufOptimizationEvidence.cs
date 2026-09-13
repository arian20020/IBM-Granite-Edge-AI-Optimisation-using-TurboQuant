using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;

/// <summary>
/// Exact, internal evidence for retained Granite 4.1 3B Q4_K_M and BF16
/// artifacts on frozen AtomicBot CPU runtime closures. Tracked digest declarations bind the
/// externally retained evidence members; they are not portable release proof
/// and deliberately authorize no other source or runtime package.
/// </summary>
public static class VerifiedGgufOptimizationEvidence
{
    private const string CurrentRuntimeManifestSha256 =
        "08EF00CF8CD425BC5409071A77C12BE5A90292C121A5109EDDE63BB109B3B7C4";
    private const string PackagedCurrentRuntimeManifestSha256 =
        "4C1EFEC8D10C2F4B2B477C9136F7E5284737C4E61B3BA6CE41B84D56A296201B";
    private const string CurrentApplicationRuntimeManifestSha256 =
        "D53299AB05D5B31DB28BA4C233FC30798EECE1BA83EAA11CDED1B5727FFEADC1";
    public const ulong Bf16SourceModelLengthBytes = 6_809_655_904;
    public const ulong Bf16Q3OutputLengthBytes = 1_725_577_824;
    public const ulong SourceModelLengthBytes = 2_099_501_664;
    public const ulong ParameterCount = 3_402_836_480;

    public static bool TryResolveParameterCount(
        string modelSha256,
        ulong modelLengthBytes,
        int? layerCount,
        int? embeddingSize,
        int? attentionHeadCount,
        int? keyValueHeadCount,
        int? declaredContextLimit,
        int? fileType,
        int? quantisationVersion,
        out ulong parameterCount)
    {
        bool exact = MatchesInspectedSource(
            modelSha256, modelLengthBytes, layerCount, embeddingSize,
            attentionHeadCount, keyValueHeadCount, declaredContextLimit,
            fileType, quantisationVersion);
        parameterCount = exact ? ParameterCount : 0;
        return exact;
    }

    public static bool MatchesInspectedSource(
        string modelSha256,
        ulong modelLengthBytes,
        int? layerCount,
        int? embeddingSize,
        int? attentionHeadCount,
        int? keyValueHeadCount,
        int? declaredContextLimit,
        int? fileType,
        int? quantisationVersion) =>
        MatchesQ4InspectedSource(
            modelSha256, modelLengthBytes, layerCount, embeddingSize,
            attentionHeadCount, keyValueHeadCount, declaredContextLimit,
            fileType, quantisationVersion)
        || MatchesBf16InspectedSource(
            modelSha256, modelLengthBytes, layerCount, embeddingSize,
            attentionHeadCount, keyValueHeadCount, declaredContextLimit,
            fileType, quantisationVersion);

    private static bool MatchesQ4InspectedSource(
        string modelSha256,
        ulong modelLengthBytes,
        int? layerCount,
        int? embeddingSize,
        int? attentionHeadCount,
        int? keyValueHeadCount,
        int? declaredContextLimit,
        int? fileType,
        int? quantisationVersion) =>
        string.Equals(modelSha256,
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
            StringComparison.Ordinal)
        && modelLengthBytes == SourceModelLengthBytes
        && layerCount == 40
        && embeddingSize == 2560
        && attentionHeadCount == 40
        && keyValueHeadCount == 8
        && declaredContextLimit == 131072
        && fileType == 15
        && quantisationVersion == 2;

    private static bool MatchesBf16InspectedSource(
        string modelSha256,
        ulong modelLengthBytes,
        int? layerCount,
        int? embeddingSize,
        int? attentionHeadCount,
        int? keyValueHeadCount,
        int? declaredContextLimit,
        int? fileType,
        int? quantisationVersion) =>
        string.Equals(modelSha256,
            "e5fc3d677f42a9cba091ea6084cf619bd434ff5ac56b893d3bf5d4f604581091",
            StringComparison.Ordinal)
        && modelLengthBytes == Bf16SourceModelLengthBytes
        && layerCount == 40
        && embeddingSize == 2560
        && attentionHeadCount == 40
        && keyValueHeadCount == 8
        && declaredContextLimit == 131072
        && fileType == 32
        && quantisationVersion == 2;

    public static bool MatchesClosure(
        string modelSha256,
        ulong modelLengthBytes,
        ulong? parameterCount,
        string runtimeManifestSha256,
        string runtimeBuildId,
        string runtimeSourceCommit) =>
        string.Equals(modelSha256,
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
            StringComparison.Ordinal)
        && modelLengthBytes == SourceModelLengthBytes
        && parameterCount == ParameterCount
        && string.Equals(runtimeManifestSha256,
            "93FA840C3DB0B623DDA3DAEBE27FE51564DCD292A420A9CF5A9A96D990E19DAA",
            StringComparison.OrdinalIgnoreCase)
        && string.Equals(runtimeBuildId,
            "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan",
            StringComparison.Ordinal)
        && string.Equals(runtimeSourceCommit,
            "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
            StringComparison.Ordinal);

    public static bool MatchesReleaseV5StandardClosure(
        string modelSha256,
        ulong modelLengthBytes,
        ulong? parameterCount,
        string runtimeManifestSha256,
        string runtimeBuildId,
        string runtimeSourceCommit) =>
        MatchesSourceIdentity(modelSha256, modelLengthBytes, parameterCount)
        && string.Equals(runtimeManifestSha256,
            "5E2204C791A44D2FC696F0F37EBB5568DDD40D7BD6BDE869D4844E029D326941",
            StringComparison.OrdinalIgnoreCase)
        && MatchesRuntimeIdentity(runtimeBuildId, runtimeSourceCommit);

    public static bool MatchesReleaseV5Bf16Q3Closure(
        string modelSha256,
        ulong modelLengthBytes,
        ulong? parameterCount,
        string runtimeManifestSha256,
        string runtimeBuildId,
        string runtimeSourceCommit) =>
        string.Equals(modelSha256,
            "e5fc3d677f42a9cba091ea6084cf619bd434ff5ac56b893d3bf5d4f604581091",
            StringComparison.Ordinal)
        && modelLengthBytes == Bf16SourceModelLengthBytes
        && parameterCount == ParameterCount
        && string.Equals(runtimeManifestSha256,
            "5E2204C791A44D2FC696F0F37EBB5568DDD40D7BD6BDE869D4844E029D326941",
            StringComparison.OrdinalIgnoreCase)
        && MatchesRuntimeIdentity(runtimeBuildId, runtimeSourceCommit);

    public static IReadOnlyList<OptimizationEvidenceRecord> Records(
        string modelSha256,
        ulong modelLengthBytes,
        ulong? parameterCount,
        string runtimeManifestSha256,
        string runtimeBuildId,
        string runtimeSourceCommit)
    {
        // F16/Q8/Turbo4 have fresh current-closure evidence. Turbo3 retains its
        // original quality evidence through the separately reviewed bounded
        // compatibility bridge; this is not a new quality measurement.
        if (MatchesSourceIdentity(modelSha256, modelLengthBytes, parameterCount)
            && MatchesCurrentRuntimeManifest(runtimeManifestSha256)
            && MatchesRuntimeIdentity(runtimeBuildId, runtimeSourceCommit))
        {
            return
            [
                Record("GGUF-CURRENT-08EF-CPU-F16-01", "f16", 6.316666666666666m,
                    "atomicbot-cpu-current-08ef00cf8cd425bc"),
                Record("GGUF-CURRENT-08EF-CPU-Q8-01", "q8_0", 5.783333333333334m,
                    "atomicbot-cpu-current-08ef00cf8cd425bc"),
                Record("GGUF-CURRENT-08EF-CPU-TURBO4-01", "turbo4", 5.3166666666666664m,
                    "atomicbot-cpu-current-08ef00cf8cd425bc"),
                Record("GGUF-CURRENT-08EF-CPU-TURBO3-COMPAT-01", "turbo3", 5.683333333333333m,
                    "atomicbot-cpu-current-08ef00cf8cd425bc"),
            ];
        }
        if (MatchesClosure(modelSha256, modelLengthBytes, parameterCount,
                runtimeManifestSha256, runtimeBuildId, runtimeSourceCommit))
        {
            return
            [
                Record("GGUF-V4-CPU-F16-01", "f16", 6.466666666666667m,
                    "atomicbot-cpu-v4-93fa840c3db0b623"),
                Record("GGUF-V4-CPU-Q8-01", "q8_0", 5.85m,
                    "atomicbot-cpu-v4-93fa840c3db0b623"),
                Record("GGUF-V4-CPU-TURBO4-01", "turbo4", 6.15m,
                    "atomicbot-cpu-v4-93fa840c3db0b623"),
                Record("GGUF-V4-CPU-TURBO3-01", "turbo3", 5.683333333333333m,
                    "atomicbot-cpu-v4-93fa840c3db0b623"),
            ];
        }

        if (MatchesReleaseV5StandardClosure(
                modelSha256, modelLengthBytes, parameterCount,
                runtimeManifestSha256, runtimeBuildId, runtimeSourceCommit))
        {
            return
            [
                Record("GGUF-V5-CPU-F16-01", "f16", 6.466666666666667m,
                    "atomicbot-cpu-v5-5e2204c791a44d2f"),
                Record("GGUF-V5-CPU-Q8-01", "q8_0", 5.85m,
                    "atomicbot-cpu-v5-5e2204c791a44d2f"),
                Record("GGUF-V5-CPU-TURBO4-T8-01", "turbo4", 6.15m,
                    "atomicbot-cpu-v5-5e2204c791a44d2f",
                    methodologyIdentity:
                        "granite-gguf-app-4k-runtime-v5-threads8-600s-v1"),
            ];
        }

        if (string.Equals(modelSha256,
                "e5fc3d677f42a9cba091ea6084cf619bd434ff5ac56b893d3bf5d4f604581091",
                StringComparison.Ordinal)
            && modelLengthBytes == Bf16SourceModelLengthBytes
            && parameterCount == ParameterCount
            && MatchesCurrentRuntimeManifest(runtimeManifestSha256)
            && MatchesRuntimeIdentity(runtimeBuildId, runtimeSourceCommit))
        {
            return
            [
                Record("GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01", "f16", 6.9m,
                    "atomicbot-cpu-current-08ef00cf8cd425bc",
                    "e5fc3d677f42a9cba091ea6084cf619bd434ff5ac56b893d3bf5d4f604581091",
                    "bf16", "q3_k_m"),
            ];
        }

        if (MatchesReleaseV5Bf16Q3Closure(
                modelSha256, modelLengthBytes, parameterCount,
                runtimeManifestSha256, runtimeBuildId, runtimeSourceCommit))
        {
            return
            [
                Record("GGUF-V5-BF16-Q3-CPU-F16-01", "f16", 6.9m,
                    "atomicbot-cpu-v5-5e2204c791a44d2f",
                    "e5fc3d677f42a9cba091ea6084cf619bd434ff5ac56b893d3bf5d4f604581091",
                    "bf16", "q3_k_m"),
            ];
        }

        return [];
    }

    public static IReadOnlyList<GgufAdmittedConfiguration> Admissions() =>
    [
        Admission("GGUF-V4-CPU-F16-01", GgufKvCacheFormat.F16,
            SupportLevel.DeclaredSupported, requiresEvidence: false),
        Admission("GGUF-V4-CPU-Q8-01", GgufKvCacheFormat.Q8_0,
            SupportLevel.DeclaredSupported, requiresEvidence: false),
        Admission("GGUF-V4-CPU-TURBO4-01", GgufKvCacheFormat.TurboQuant4Bit,
            SupportLevel.Experimental, requiresEvidence: true),
        Admission("GGUF-V4-CPU-TURBO3-01", GgufKvCacheFormat.TurboQuant3Bit,
            SupportLevel.Experimental, requiresEvidence: true),
    ];

    public static IReadOnlyList<GgufExecutionProfileAuthority> ExecutionProfiles() =>
    [
        Profile("GGUF-V4-CPU-F16-01"),
        Profile("GGUF-V4-CPU-Q8-01"),
        Profile("GGUF-V4-CPU-TURBO4-01"),
        Profile("GGUF-V4-CPU-TURBO3-01"),
    ];

    public static IReadOnlyList<GgufAdmittedConfiguration> Admissions(
        IReadOnlySet<string> evidenceIds)
    {
        ArgumentNullException.ThrowIfNull(evidenceIds);
        var selected = new List<GgufAdmittedConfiguration>();
        foreach (GgufAdmittedConfiguration admission in AllAdmissions())
        {
            if (evidenceIds.Contains(admission.EvidenceId))
            {
                selected.Add(admission);
            }
        }

        return selected;
    }

    public static IReadOnlyList<GgufExecutionProfileAuthority> ExecutionProfiles(
        IReadOnlySet<string> evidenceIds)
    {
        ArgumentNullException.ThrowIfNull(evidenceIds);
        var selected = new List<GgufExecutionProfileAuthority>();
        foreach (GgufExecutionProfileAuthority profile in AllProfiles())
        {
            if (evidenceIds.Contains(profile.EvidenceId))
            {
                selected.Add(profile);
            }
        }

        return selected;
    }

    internal static bool IsVerifiedEvidenceId(string evidenceId) => evidenceId is
        "GGUF-CURRENT-08EF-CPU-F16-01" or "GGUF-CURRENT-08EF-CPU-Q8-01"
        or "GGUF-CURRENT-08EF-CPU-TURBO4-01"
        or "GGUF-CURRENT-08EF-CPU-TURBO3-COMPAT-01"
        or
        "GGUF-V4-CPU-F16-01" or "GGUF-V4-CPU-Q8-01"
        or "GGUF-V4-CPU-TURBO4-01" or "GGUF-V4-CPU-TURBO3-01"
        or "GGUF-V5-CPU-F16-01" or "GGUF-V5-CPU-Q8-01"
        or "GGUF-V5-CPU-TURBO4-T8-01"
        or "GGUF-V5-BF16-Q3-CPU-F16-01"
        or "GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01";

    public static bool MatchesBf16Q3Quantizer(GgufQuantiserIdentity? quantizer) =>
        quantizer is not null
        && string.Equals(quantizer.PackageId,
            "granite-edge-ai-atomicbot-llama-quantize-x64",
            StringComparison.Ordinal)
        && string.Equals(quantizer.ToolVersion,
            "atomicbot-llama-quantize-519f0c594a8e31467d2e2f2cf17054c9e7e11536",
            StringComparison.Ordinal)
        && string.Equals(quantizer.ExecutableSha256,
            "0a17247d4807b520532df54f96ed73f3cf6fa921f879d8d465b44541b41d36e3",
            StringComparison.Ordinal);

    internal static bool MatchesExecutionProfile(
        GgufExecutionProfileAuthority profile) =>
        IsVerifiedEvidenceId(profile.EvidenceId)
        && profile.Evidence == EvidenceGrade.Measured
        && string.Equals(profile.ProfileId, "cpu", StringComparison.Ordinal)
        && profile.FlashAttention
        && profile.ThreadCount == (string.Equals(
            profile.EvidenceId,
            "GGUF-V5-CPU-TURBO4-T8-01",
            StringComparison.Ordinal) ? 8 : 4)
        && profile.BatchSize == 512
        && profile.MaximumGeneratedTokens == 256;

    // Structural eligibility is not execution evidence. Candidate generation
    // and plan issuance still bind the exact measured record and runtime
    // profile before this one released TurboQuant configuration can run.
    internal static bool IsReleasedTurbo4Threads8Configuration(
        string evidenceId,
        GgufRouteConfiguration configuration,
        int minimumContextTokens,
        int maximumContextTokens)
    {
        bool releasedTurbo4 = evidenceId is "GGUF-V5-CPU-TURBO4-T8-01"
            or "GGUF-CURRENT-08EF-CPU-TURBO4-01";
        bool releasedCurrentTurbo3 = evidenceId ==
            "GGUF-CURRENT-08EF-CPU-TURBO3-COMPAT-01";
        return (releasedTurbo4 || releasedCurrentTurbo3)
        && configuration.Backend == CompatibilityBackend.Cpu
        && configuration.Device == DeviceRouteId.Cpu
        && configuration.Weights == GgufWeightFormat.Imported
        && configuration.KvCache == (releasedCurrentTurbo3
            ? GgufKvCacheFormat.TurboQuant3Bit
            : GgufKvCacheFormat.TurboQuant4Bit)
        && configuration.Offload == GpuOffloadLevel.None
        && minimumContextTokens == 4096
        && maximumContextTokens == 4096;
    }

    internal static bool IsReleasedTurbo4Threads8Execution(
        OptimizationEvidenceRecord? evidence,
        GgufExecutionProfileAuthority? profile,
        GgufExecutionPayload payload)
    {
        bool currentClosure = evidence?.EvidenceId ==
            "GGUF-CURRENT-08EF-CPU-TURBO4-01";
        bool releaseV5Closure = evidence?.EvidenceId ==
            "GGUF-V5-CPU-TURBO4-T8-01";
        bool currentTurbo3Closure = evidence?.EvidenceId ==
            "GGUF-CURRENT-08EF-CPU-TURBO3-COMPAT-01";
        if (!currentClosure && !releaseV5Closure && !currentTurbo3Closure)
            return false;
        string packageIdentity = currentClosure || currentTurbo3Closure
            ? "atomicbot-cpu-current-08ef00cf8cd425bc"
            : "atomicbot-cpu-v5-5e2204c791a44d2f";
        string methodologyIdentity = currentClosure || currentTurbo3Closure
            ? "granite-gguf-app-4k-runtime-v4-600s-v2"
            : "granite-gguf-app-4k-runtime-v5-threads8-600s-v1";
        decimal quality = currentTurbo3Closure ? 5.683333333333333m
            : currentClosure ? 5.3166666666666664m : 6.15m;
        int threadCount = currentClosure || currentTurbo3Closure ? 4 : 8;
        string cacheConfiguration = currentTurbo3Closure ? "turbo3" : "turbo4";
        GgufCacheType cacheType = currentTurbo3Closure
            ? GgufCacheType.Turbo3
            : GgufCacheType.Turbo4;
        return evidence is { IsAdmitted: true }
        && evidence.Quality.Value == quality
        && evidence.Key.ModelFamily == OptimizationEvidenceModelFamily.Granite
        && string.Equals(
            evidence.Key.ModelIdentitySha256,
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
            StringComparison.Ordinal)
        && evidence.Key.ParameterCount == ParameterCount
        && evidence.Key.Route == OptimizationRoute.Gguf
        && string.Equals(evidence.Key.RuntimePackageIdentity,
            packageIdentity, StringComparison.Ordinal)
        && string.Equals(
            evidence.Key.SourceWeightRepresentation,
            "q4_k_m",
            StringComparison.Ordinal)
        && string.Equals(
            evidence.Key.TargetWeightRepresentation,
            "q4_k_m",
            StringComparison.Ordinal)
        && string.Equals(
            evidence.Key.CacheConfiguration,
            cacheConfiguration,
            StringComparison.Ordinal)
        && evidence.Key.Backend == OptimizationEvidenceBackend.Cpu
        && evidence.Key.DeviceClass == OptimizationEvidenceDeviceClass.Cpu
        && evidence.Key.ContextTokens == 4096
        && string.Equals(
            evidence.Key.Workload,
            "local-chat-v1",
            StringComparison.Ordinal)
        && string.Equals(evidence.Key.MethodologyIdentity,
            methodologyIdentity, StringComparison.Ordinal)
        && string.Equals(
            evidence.Key.MemoryPerformanceProtocol,
            "granite-gguf-app-4k-v1",
            StringComparison.Ordinal)
        && string.Equals(
            evidence.Key.ExecutionProfile,
            "cpu",
            StringComparison.Ordinal)
        && profile is not null
        && string.Equals(profile.EvidenceId, evidence.EvidenceId, StringComparison.Ordinal)
        && MatchesExecutionProfile(profile)
        && MatchesRuntimeIdentity(payload.RuntimeBuildId, payload.RuntimeSourceCommit)
        && payload.Backend == GgufRuntimeBackend.Cpu
        && string.Equals(payload.DeviceId, "CPU", StringComparison.Ordinal)
        && payload.ContextSize == 4096
        && payload.KeyCacheType == cacheType
        && payload.ValueCacheType == cacheType
        && payload.GpuLayerCount == 0
        && payload.FlashAttention
        && payload.ThreadCount == threadCount
        && payload.BatchSize == 512
        && string.Equals(payload.EvidenceGrade, "Measured", StringComparison.Ordinal)
        && string.Equals(payload.ProfileId, "cpu", StringComparison.Ordinal)
        && payload.MaximumGeneratedTokens == 256
        && payload.PersistentTargetWeightFormat == GgufWeightFormat.Imported
        && payload.Quantiser is null
        && payload.ConversionSource is null
        && payload.RequantisationPolicy is null;
    }

    internal static bool TryResolve(
        OptimizationEvidenceCatalog catalog,
        string modelSha256,
        ulong parameterCount,
        string sourceWeights,
        string targetWeights,
        string cache,
        OptimizationEvidenceBackend backend,
        OptimizationEvidenceDeviceClass deviceClass,
        int contextTokens,
        string executionProfile,
        out OptimizationEvidenceRecord? evidence) =>
        TryResolvePackage(
            catalog, modelSha256, parameterCount,
            "atomicbot-cpu-current-08ef00cf8cd425bc",
            sourceWeights, targetWeights, cache, backend, deviceClass,
            contextTokens, executionProfile, out evidence)
        ||
        TryResolvePackage(
            catalog, modelSha256, parameterCount,
            "atomicbot-cpu-v4-93fa840c3db0b623",
            sourceWeights, targetWeights, cache, backend, deviceClass,
            contextTokens, executionProfile, out evidence)
        || TryResolvePackage(
            catalog, modelSha256, parameterCount,
            "atomicbot-cpu-v5-5e2204c791a44d2f",
            sourceWeights, targetWeights, cache, backend, deviceClass,
            contextTokens, executionProfile, out evidence)
        || TryResolvePackage(
            catalog, modelSha256, parameterCount,
            "atomicbot-cpu-v5-5e2204c791a44d2f",
            sourceWeights, targetWeights, cache, backend, deviceClass,
            contextTokens, executionProfile, out evidence,
            "granite-gguf-app-4k-runtime-v5-threads8-600s-v1");

    private static bool TryResolvePackage(
        OptimizationEvidenceCatalog catalog,
        string modelSha256,
        ulong parameterCount,
        string runtimePackageIdentity,
        string sourceWeights,
        string targetWeights,
        string cache,
        OptimizationEvidenceBackend backend,
        OptimizationEvidenceDeviceClass deviceClass,
        int contextTokens,
        string executionProfile,
        out OptimizationEvidenceRecord? evidence,
        string methodologyIdentity =
            "granite-gguf-app-4k-runtime-v4-600s-v2") =>
        catalog.TryResolveArtifactConfiguration(
            OptimizationEvidenceModelFamily.Granite,
            modelSha256,
            parameterCount,
            OptimizationRoute.Gguf,
            runtimePackageIdentity,
            sourceWeights,
            targetWeights,
            cache,
            backend,
            deviceClass,
            contextTokens,
            "local-chat-v1",
            methodologyIdentity,
            "granite-gguf-app-4k-v1",
            executionProfile,
            out evidence);

    private static OptimizationEvidenceRecord Record(
        string evidenceId,
        string cache,
        decimal quality,
        string runtimePackageIdentity,
        string modelSha256 =
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
        string sourceWeights = "q4_k_m",
        string targetWeights = "q4_k_m",
        string methodologyIdentity =
            "granite-gguf-app-4k-runtime-v4-600s-v2") => new(
        evidenceId,
        new OptimizationEvidenceKey(
            OptimizationEvidenceModelFamily.Granite,
            modelSha256,
            ParameterCount,
            OptimizationRoute.Gguf,
            runtimePackageIdentity,
            sourceWeights,
            targetWeights,
            cache,
            OptimizationEvidenceBackend.Cpu,
            OptimizationEvidenceDeviceClass.Cpu,
            4096,
            "local-chat-v1",
            methodologyIdentity,
            "granite-gguf-app-4k-v1",
            "cpu"),
        new OptimizationQualityScore(quality),
        OutputHealthPassed: true,
        StabilityPassed: true,
        ActivationPassed: true,
        IntegrityPassed: true);

    private static GgufAdmittedConfiguration Admission(
        string evidenceId,
        GgufKvCacheFormat cache,
        SupportLevel level,
        bool requiresEvidence) => GgufAdmittedConfiguration.Create(
        evidenceId,
        CompatibilityBackend.Cpu,
        DeviceRouteId.Cpu,
        GgufWeightFormat.Imported,
        cache,
        GpuOffloadLevel.None,
        minimumContextTokens: 4096,
        maximumContextTokens: 4096,
        level,
        requiresEvidence);

    private static IReadOnlyList<GgufAdmittedConfiguration> AllAdmissions() =>
    [
        .. Admissions(),
        Admission("GGUF-CURRENT-08EF-CPU-F16-01", GgufKvCacheFormat.F16,
            SupportLevel.DeclaredSupported, requiresEvidence: false),
        Admission("GGUF-CURRENT-08EF-CPU-Q8-01", GgufKvCacheFormat.Q8_0,
            SupportLevel.DeclaredSupported, requiresEvidence: false),
        Admission("GGUF-CURRENT-08EF-CPU-TURBO4-01", GgufKvCacheFormat.TurboQuant4Bit,
            SupportLevel.DeclaredSupported, requiresEvidence: false),
        Admission("GGUF-CURRENT-08EF-CPU-TURBO3-COMPAT-01", GgufKvCacheFormat.TurboQuant3Bit,
            SupportLevel.DeclaredSupported, requiresEvidence: false),
        Admission("GGUF-V5-CPU-F16-01", GgufKvCacheFormat.F16,
            SupportLevel.DeclaredSupported, requiresEvidence: false),
        Admission("GGUF-V5-CPU-Q8-01", GgufKvCacheFormat.Q8_0,
            SupportLevel.DeclaredSupported, requiresEvidence: false),
        Admission("GGUF-V5-CPU-TURBO4-T8-01",
            GgufKvCacheFormat.TurboQuant4Bit,
            SupportLevel.DeclaredSupported, requiresEvidence: false),
        GgufAdmittedConfiguration.Create(
            "GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01",
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GgufWeightFormat.Q3KM,
            GgufKvCacheFormat.F16,
            GpuOffloadLevel.None,
            minimumContextTokens: 4096,
            maximumContextTokens: 4096,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false),
        GgufAdmittedConfiguration.Create(
            "GGUF-V5-BF16-Q3-CPU-F16-01",
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GgufWeightFormat.Q3KM,
            GgufKvCacheFormat.F16,
            GpuOffloadLevel.None,
            minimumContextTokens: 4096,
            maximumContextTokens: 4096,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false),
    ];

    private static IReadOnlyList<GgufExecutionProfileAuthority> AllProfiles() =>
    [
        .. ExecutionProfiles(),
        Profile("GGUF-CURRENT-08EF-CPU-F16-01"),
        Profile("GGUF-CURRENT-08EF-CPU-Q8-01"),
        Profile("GGUF-CURRENT-08EF-CPU-TURBO4-01"),
        Profile("GGUF-CURRENT-08EF-CPU-TURBO3-COMPAT-01"),
        Profile("GGUF-V5-CPU-F16-01"),
        Profile("GGUF-V5-CPU-Q8-01"),
        Profile("GGUF-V5-CPU-TURBO4-T8-01", threadCount: 8),
        Profile("GGUF-V5-BF16-Q3-CPU-F16-01"),
        Profile("GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01"),
    ];

    private static bool MatchesSourceIdentity(
        string modelSha256,
        ulong modelLengthBytes,
        ulong? parameterCount) =>
        string.Equals(modelSha256,
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
            StringComparison.Ordinal)
        && modelLengthBytes == SourceModelLengthBytes
        && parameterCount == ParameterCount;

    private static bool MatchesRuntimeIdentity(
        string runtimeBuildId,
        string runtimeSourceCommit) =>
        string.Equals(runtimeBuildId,
            "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan",
            StringComparison.Ordinal)
        && string.Equals(runtimeSourceCommit,
            "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
            StringComparison.Ordinal);

    private static bool MatchesCurrentRuntimeManifest(string runtimeManifestSha256) =>
        string.Equals(runtimeManifestSha256,
            CurrentRuntimeManifestSha256,
            StringComparison.OrdinalIgnoreCase)
        || string.Equals(runtimeManifestSha256,
            PackagedCurrentRuntimeManifestSha256,
            StringComparison.OrdinalIgnoreCase)
        || string.Equals(runtimeManifestSha256,
            CurrentApplicationRuntimeManifestSha256,
            StringComparison.OrdinalIgnoreCase);

    private static GgufExecutionProfileAuthority Profile(
        string evidenceId,
        int threadCount = 4) =>
        GgufExecutionProfileAuthority.Create(
            evidenceId,
            EvidenceGrade.Measured,
            "cpu",
            flashAttention: true,
            threadCount,
            batchSize: 512,
            maximumGeneratedTokens: 256);
}
