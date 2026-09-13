using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;

/// <summary>
/// Exact evidence from the retained Granite 4.1 3B FP16 source converted to INT4
/// and exercised twice, in counterbalanced order, through the official CPU worker.
/// The current worker observations are retained under
/// experiments/raw-results/official-openvino-final.
/// This authority deliberately matches no other model or execution closure.
/// </summary>
public static class VerifiedOpenVinoOptimizationEvidence
{
    public const ulong CurrentRetainedInt4ParameterCount = 3_402_836_480;
    public const string CurrentRetainedPackageManifestSha256 =
        "d5f33732fddb37c150f8ebd4be163c40474a245cd3a8f7683b63996d5f60ed68";
    public const string CurrentOfficialWorkerManifestSha256 =
        "f0089dae967a0b4249238f9bf49db02ba44e111c78b83ebff36778f6d0ddcbe2";
    public const string PackagedOfficialWorkerManifestSha256 =
        "0c642015d9b6912533d8c6d5e8f136e97f1071aa71ff62b6dbf46f818771a6f9";
    public const string SourceModelSha256 =
        "f07519a2fbb3bacdd267e2e05ed9d8693f03fd752f0a80d5366f318f6b13c632";
    public const string OptimizedModelSha256 =
        "fcfb6ec62a2b823d7d1aebedee193083eaa2f86d9e89722e46102c7a4b90bd27";
    public const string OptimizedPackageManifestSha256 =
        "f67472992d309cdbb6e4df90afccf6c0f2e9a7b5950316e37a7b1d5c61f23a55";
    public const string VerifiedInt8ModelSha256 =
        "f92a009e300ee1c01edc8a6cbaf4f591e2998421757ce63c090d75a5687faa06";
    public const string FreshInt8ModelSha256 =
        "e26ab43c444545079a36fc2e1645d11cc6f628cbcf8f6ad392633f940b1d6072";
    public const string FreshInt8MemoryPerformanceProtocol =
        "ov-int8-2026-09-06-functional-sanity-v1";
    public const string RuntimeBuild =
        "2026.3.0-22451-8a17657b995-releases/2026/3";
    public const string GenAiBuild = "2026.3.0.0-3277-bd8d6542e3c";
    public const string TokenizersBuild = "2026.3.0.0-703-183c6f25cda";
    public const string WorkerManifestSha256 =
        "e6171b0e77b3b71356109e7529794b9a8a034d6b4f8576d3ad15c7ed4cd579bf";
    public const ulong ParameterCount = 3_000_000_000;
    public const string MethodologyIdentity =
        "openvino-real-output-sanity-v1";
    public const string SectorQualityMethodologyIdentity =
        "openvino-sector-experience-quality-v3";
    public const string SectorQualityMemoryPerformanceProtocol =
        "ov-app-sector-2026-09-06-v1";
    public const string MemoryPerformanceProtocol =
        "ov-real-2026-09-05-two-pass-v1";
    public const string TurboMethodologyIdentity =
        "openvino-turboquant-real-activation-and-output-health-v1";
    public const string TurboMemoryPerformanceProtocol =
        "ov-main-f5f594dc-tbq3-tbq4-real-granite-v1";
    public const string TurboRuntimeBuild =
        "2026.5.0-22950-f5f594dc0c9";
    public const string TurboGenAiBuild =
        "2026.5.0.0-3409-6fbc103538d";
    public const string TurboTokenizersBuild =
        "2026.5.0.0-737-824033c3061";
    public const string TurboWorkerManifestSha256 =
        "2c8b77eba5f26d693fe3cbde164c8fe88f5d6d2496a6f79ea666eae0dd4271d5";
    public const string PackagedTurboWorkerManifestSha256 =
        "7455fc0fcd20a2dcb38ef9636b26323e988083819ee810fba110b6171c7e6e88";
    public const string TurboPatchSeriesSha256 =
        "ec7a476d54be02ec6b3ce3f16381e6e42e700ce8aef72684a56ec73e03ac7fc0";
    public const string TurboRuntimeManifestSha256 =
        "128c18ec8c32289735a5bc3c66f118bf83a4ff4eabbc077c61a9ccbf42d17157";

    private static readonly IReadOnlyDictionary<string, string> OptimizerVersions =
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["nncf"] = "3.3.0",
            ["openvino"] = "2026.3.0",
            ["openvino-genai"] = "2026.3.0.0",
            ["optimum"] = "2.3.0",
            ["optimum-intel"] = "2.1.0",
            ["transformers"] = "5.5.4"
        };

    public static bool TryResolveParameterCount(
        string modelSha256,
        out ulong parameterCount)
    {
        if (string.Equals(modelSha256, OptimizedModelSha256, StringComparison.Ordinal))
        {
            parameterCount = CurrentRetainedInt4ParameterCount;
            return true;
        }
        bool exact = string.Equals(
            modelSha256,
            SourceModelSha256,
            StringComparison.Ordinal);
        parameterCount = exact ? ParameterCount : 0;
        return exact;
    }

    public static OptimizationEvidenceCatalog CreateCatalog(
        string modelSha256,
        ulong? parameterCount,
        IEnumerable<OpenVinoExecutionAuthority> executionAuthorities) =>
        new(Records(modelSha256, parameterCount, executionAuthorities));

    public static OptimizationEvidenceCatalog CreateCatalog(
        string modelSha256,
        ulong? parameterCount,
        IEnumerable<OpenVinoExecutionAuthority> executionAuthorities,
        string? packageManifestSha256) =>
        new(Records(modelSha256, parameterCount, executionAuthorities, packageManifestSha256));

    public static IReadOnlyList<OptimizationEvidenceRecord> Records(
        string modelSha256,
        ulong? parameterCount,
        IEnumerable<OpenVinoExecutionAuthority> executionAuthorities)
        => Records(modelSha256, parameterCount, executionAuthorities, null);

    public static IReadOnlyList<OptimizationEvidenceRecord> Records(
        string modelSha256,
        ulong? parameterCount,
        IEnumerable<OpenVinoExecutionAuthority> executionAuthorities,
        string? packageManifestSha256)
    {
        ArgumentNullException.ThrowIfNull(modelSha256);
        ArgumentNullException.ThrowIfNull(executionAuthorities);
        if (parameterCount == CurrentRetainedInt4ParameterCount
            && string.Equals(modelSha256, OptimizedModelSha256, StringComparison.Ordinal))
        {
            if (!string.Equals(packageManifestSha256, CurrentRetainedPackageManifestSha256, StringComparison.Ordinal))
                return [];
            return executionAuthorities
                .Where(execution => execution.EvidenceId == "OV-STD-CPU-INT4-U4-01"
                    && execution.ConfigurationId == "openvino.standard.cpu.int4.u4.v1"
                    && MatchesCurrentRetainedClosure(execution))
                .Select(execution => Record(modelSha256, execution, execution.EvidenceId,
                    "int4", "u4", new OptimizationQualityScore(5.520833333333333m),
                    SectorQualityMethodologyIdentity, SectorQualityMemoryPerformanceProtocol,
                    CurrentRetainedInt4ParameterCount))
                .ToArray();
        }
        if (parameterCount != ParameterCount)
        {
            return [];
        }

        List<OptimizationEvidenceRecord> records = [];
        bool rawSource = string.Equals(
            modelSha256, SourceModelSha256, StringComparison.Ordinal);
        bool int4Artifact = string.Equals(
            modelSha256, OptimizedModelSha256, StringComparison.Ordinal);
        bool int8Artifact = string.Equals(
            modelSha256, VerifiedInt8ModelSha256, StringComparison.Ordinal);
        if (!rawSource && !int4Artifact && !int8Artifact)
        {
            return [];
        }

        if (rawSource)
        {
            // Fresh FP16 -> INT8 service publication and four-prompt runs on
            // the resulting FreshInt8ModelSha256 artifact. These establish
            // the admission floor only, not a comparative Good quality grade.
            foreach ((string evidenceId, string cache, bool turbo) in new[]
            {
                ("OV-STD-CPU-AUTO-01", "released-default", false),
                ("OV-STD-CPU-INT8-U8-01", "u8", false),
                ("OV-STD-CPU-INT8-U4-01", "u4", false),
                ("OV-TBQ4-CPU-INT8-01", "tbq4", true),
                ("OV-TBQ3-CPU-INT8-01", "tbq3", true)
            })
            {
                OpenVinoExecutionAuthority? execution = executionAuthorities.SingleOrDefault(
                    item => string.Equals(item.EvidenceId, evidenceId, StringComparison.Ordinal));
                if (execution is not null
                    && execution.SourceWeightPrecision == OpenVinoWeightPrecision.Fp16
                    && (turbo ? MatchesRawTurboClosure(execution) : MatchesRawOfficialClosure(execution)))
                {
                    records.Add(Record(modelSha256, execution, evidenceId,
                        "int8", cache, new OptimizationQualityScore(4m),
                        turbo ? TurboMethodologyIdentity : MethodologyIdentity,
                        FreshInt8MemoryPerformanceProtocol));
                }
            }

            foreach ((string evidenceId, string cache) in new[]
            {
                ("OV-STD-CPU-INT4-U8-01", "u8"),
                ("OV-STD-CPU-INT4-U4-01", "u4")
            })
            {
                OpenVinoExecutionAuthority? execution = executionAuthorities.SingleOrDefault(
                    item => string.Equals(item.EvidenceId, evidenceId, StringComparison.Ordinal));
                if (execution is not null && MatchesRawOfficialClosure(execution))
                {
                    // All 48 frozen sector prompts completed on the exact current
                    // INT4/U4 artifact and worker, with healthy streamed output.
                    // This is a quality result, not a full-context memory stress test.
                    bool completeSectorQuality = cache == "u4";
                    records.Add(Record(modelSha256, execution, evidenceId,
                        "int4", cache, new OptimizationQualityScore(completeSectorQuality ? 5.52m : 4m),
                        completeSectorQuality ? SectorQualityMethodologyIdentity : MethodologyIdentity,
                        completeSectorQuality ? SectorQualityMemoryPerformanceProtocol : MemoryPerformanceProtocol));
                }
            }

            foreach ((string evidenceId, string cache) in new[]
            {
                ("OV-TBQ4-CPU-MXFP4-01", "tbq4"),
                ("OV-TBQ3-CPU-MXFP4-01", "tbq3")
            })
            {
                OpenVinoExecutionAuthority? execution = executionAuthorities.SingleOrDefault(
                    item => string.Equals(item.EvidenceId, evidenceId, StringComparison.Ordinal));
                if (execution is not null && MatchesRawTurboClosure(execution))
                {
                    records.Add(Record(modelSha256, execution, evidenceId,
                        "mxfp4", cache, new OptimizationQualityScore(4m),
                        TurboMethodologyIdentity, TurboMemoryPerformanceProtocol));
                }
            }
        }

        string turboWeights = int8Artifact ? "int8" : "int4";
        foreach ((string evidenceId, string cache, decimal quality) in
            TurboRows(turboWeights))
        {
            OpenVinoExecutionAuthority? execution = executionAuthorities.SingleOrDefault(
                item => string.Equals(item.EvidenceId, evidenceId, StringComparison.Ordinal));
            if (execution is null || !(rawSource ? MatchesRawTurboClosure(execution) : MatchesTurboClosure(execution)))
            {
                continue;
            }
            records.Add(Record(modelSha256, execution, evidenceId,
                turboWeights, cache,
                new OptimizationQualityScore(turboWeights == "int4" ? cache == "tbq3" ? 5.19m : 5.48m : quality),
                turboWeights == "int4" ? SectorQualityMethodologyIdentity : TurboMethodologyIdentity,
                turboWeights == "int4" ? SectorQualityMemoryPerformanceProtocol : TurboMemoryPerformanceProtocol));
        }
        return records;
    }

    private static IEnumerable<(string EvidenceId, string Cache, decimal Quality)>
        TurboRows(string weights) => weights switch
        {
            "int8" =>
            [
                ("OV-TBQ4-CPU-INT8-01", "tbq4", 5m),
                ("OV-TBQ3-CPU-INT8-01", "tbq3", 4m),
            ],
            _ =>
            [
                ("OV-TBQ4-CPU-INT4-01", "tbq4", 5m),
                ("OV-TBQ3-CPU-INT4-01", "tbq3", 4m),
            ]
        };

    private static OptimizationEvidenceRecord Record(
        string modelSha256,
        OpenVinoExecutionAuthority execution,
        string evidenceId,
        string weights,
        string cache,
        OptimizationQualityScore quality,
        string methodology,
        string protocol,
        ulong parameterCount = ParameterCount) => new(
            evidenceId,
            new OptimizationEvidenceKey(
                OptimizationEvidenceModelFamily.Granite,
                modelSha256,
                parameterCount,
                OptimizationRoute.OpenVino,
                CrossRouteCandidateGenerator.OpenVinoEvidencePackageIdentity(execution),
                weights,
                weights,
                cache,
                OptimizationEvidenceBackend.OpenVinoCpu,
                OptimizationEvidenceDeviceClass.Cpu,
                4096,
                "local-chat-v1",
                methodology,
                protocol,
                "openvino-cpu"),
            quality,
            OutputHealthPassed: true,
            StabilityPassed: true,
            ActivationPassed: true,
            IntegrityPassed: true);

    internal static bool IsCurrentRetainedU4Evidence(
        OptimizationEvidenceRecord? record, OpenVinoExecutionAuthority execution) =>
        record is not null && Records(OptimizedModelSha256, CurrentRetainedInt4ParameterCount,
            [execution], CurrentRetainedPackageManifestSha256).Contains(record);

    internal static bool IsKnownFailedRetainedDefault(
        string modelSha256, ulong? parameterCount, OpenVinoExecutionAuthority execution,
        OpenVinoRouteConfiguration configuration, int contextTokens) =>
        modelSha256 == OptimizedModelSha256 && parameterCount == CurrentRetainedInt4ParameterCount
        && MatchesCurrentRetainedClosure(execution)
        && execution.EvidenceId == "OV-STD-CPU-INT4-DEFAULT-01"
        && execution.ConfigurationId == "openvino.standard.cpu.int4.default.v1"
        && configuration.Weights == OpenVinoWeightFormat.Int4
        && configuration.KvCache == OpenVinoKvCacheFormat.RouteDefault
        && configuration.Device == DeviceRouteId.Cpu
        && configuration.PerformanceHint == OpenVinoPerformanceHint.Latency
        && configuration.CompiledCache == OpenVinoCompiledCachePolicy.Disabled
        && configuration.Streams == 1 && contextTokens == 4096;

    private static bool MatchesCurrentRetainedClosure(OpenVinoExecutionAuthority execution) =>
        execution.SourceWeightPrecision == OpenVinoWeightPrecision.FourBit
        && execution.TurboQuantBuild is null && execution.CompiledCacheIsDisposable
        && execution.BuildIdentity.RuntimeBuild == RuntimeBuild
        && execution.BuildIdentity.GenAiBuild == GenAiBuild
        && execution.BuildIdentity.TokenizersBuild == TokenizersBuild
        && execution.BuildIdentity.WorkerManifestDigest == CurrentOfficialWorkerManifestSha256
        && execution.OptimizerVersions.Count == OptimizerVersions.Count
        && OptimizerVersions.All(pair => execution.OptimizerVersions.TryGetValue(pair.Key, out string? value)
            && string.Equals(value, pair.Value, StringComparison.Ordinal));

    internal static bool MatchesObservedClosure(OpenVinoExecutionAuthority execution)
        => MatchesObservedClosure(execution, WorkerManifestSha256);

    private static bool MatchesRawOfficialClosure(OpenVinoExecutionAuthority execution) =>
        MatchesObservedClosure(execution)
        || MatchesObservedClosure(execution, CurrentOfficialWorkerManifestSha256)
        || MatchesObservedClosure(execution, PackagedOfficialWorkerManifestSha256);

    private static bool MatchesObservedClosure(
        OpenVinoExecutionAuthority execution, string expectedWorkerManifestSha256)
    {
        OpenVinoBuildIdentity build = execution.BuildIdentity;
        return execution.SourceWeightPrecision == OpenVinoWeightPrecision.Fp16
            && execution.TurboQuantBuild is null
            && execution.CompiledCacheIsDisposable
            && string.Equals(build.RuntimeBuild, RuntimeBuild, StringComparison.Ordinal)
            && string.Equals(build.GenAiBuild, GenAiBuild, StringComparison.Ordinal)
            && string.Equals(
                build.TokenizersBuild,
                TokenizersBuild,
                StringComparison.Ordinal)
            && string.Equals(
                build.WorkerManifestDigest,
                expectedWorkerManifestSha256,
                StringComparison.Ordinal)
            && execution.OptimizerVersions.Count == OptimizerVersions.Count
            && OptimizerVersions.All(expected =>
                execution.OptimizerVersions.TryGetValue(
                    expected.Key,
                    out string? actual)
                && string.Equals(actual, expected.Value, StringComparison.Ordinal));
    }

    internal static bool MatchesTurboClosure(OpenVinoExecutionAuthority execution)
        => MatchesTurboClosure(execution, TurboWorkerManifestSha256)
        || MatchesTurboClosure(execution,
            "39a4eccc05d4677b59f7f882cc50f875591ee3d3f90f1037e61e17fe3a34c35e")
        || MatchesTurboClosure(execution, PackagedTurboWorkerManifestSha256);

    private static bool MatchesRawTurboClosure(OpenVinoExecutionAuthority execution) =>
        execution.SourceWeightPrecision == OpenVinoWeightPrecision.Fp16
        && MatchesTurboClosure(execution);

    private static bool MatchesTurboClosure(
        OpenVinoExecutionAuthority execution, string expectedWorkerManifestSha256)
    {
        OpenVinoBuildIdentity build = execution.BuildIdentity;
        return execution.TurboQuantBuild is { } turbo
            && execution.CompiledCacheIsDisposable
            && string.Equals(build.RuntimeBuild, TurboRuntimeBuild, StringComparison.Ordinal)
            && string.Equals(build.GenAiBuild, TurboGenAiBuild, StringComparison.Ordinal)
            && string.Equals(build.TokenizersBuild, TurboTokenizersBuild, StringComparison.Ordinal)
            && string.Equals(build.WorkerManifestDigest,
                expectedWorkerManifestSha256, StringComparison.Ordinal)
            && string.Equals(turbo.SourceCommit,
                "f5f594dc0c9e5961785f0d17743486d52eac87e7", StringComparison.Ordinal)
            && string.Equals(turbo.ImplementationCommit,
                "b9a1f201c109e0bed74763934f79483cf6c4cbf4", StringComparison.Ordinal)
            && string.Equals(turbo.PatchSeriesDigest,
                TurboPatchSeriesSha256, StringComparison.Ordinal)
            && string.Equals(turbo.RuntimeManifestDigest,
                TurboRuntimeManifestSha256, StringComparison.Ordinal)
            && execution.OptimizerVersions.Count == OptimizerVersions.Count
            && OptimizerVersions.All(expected =>
                execution.OptimizerVersions.TryGetValue(expected.Key, out string? actual)
                && string.Equals(actual, expected.Value, StringComparison.Ordinal));
    }

    // Structural eligibility is not evidence: generation and issuance also
    // require IsReleasedTurboQuantEvidence against the exact current closure.
    public static bool IsReleasedTurboQuantConfiguration(
        string evidenceId, OpenVinoRouteConfiguration configuration,
        int minimumContextTokens, int maximumContextTokens) =>
        configuration.Weights == OpenVinoWeightFormat.Int4
        && configuration.Device == DeviceRouteId.Cpu
        && configuration.PerformanceHint == OpenVinoPerformanceHint.Latency
        && configuration.CompiledCache == OpenVinoCompiledCachePolicy.Disabled
        && configuration.Streams == 1
        && minimumContextTokens == 4096 && maximumContextTokens == 4096
        && (configuration.KvCache, evidenceId) is
            (OpenVinoKvCacheFormat.TurboQuantTbq3, "OV-TBQ3-CPU-INT4-01") or
            (OpenVinoKvCacheFormat.TurboQuantTbq4, "OV-TBQ4-CPU-INT4-01");

    public static bool IsReleasedTurboQuantEvidence(
        OptimizationEvidenceRecord? evidence, OpenVinoExecutionAuthority execution) =>
        evidence is { IsAdmitted: true }
        && (MatchesTurboClosure(execution)
            || (evidence.Key.ModelIdentitySha256 == SourceModelSha256
                && MatchesRawTurboClosure(execution)))
        && execution.SourceWeightPrecision is OpenVinoWeightPrecision.Fp16 or OpenVinoWeightPrecision.FourBit
        && evidence.EvidenceId == execution.EvidenceId
        && (evidence.EvidenceId, execution.ConfigurationId, evidence.Key.CacheConfiguration) is
            ("OV-TBQ3-CPU-INT4-01", "openvino.turboquant.cpu.int4.tbq3.v1", "tbq3") or
            ("OV-TBQ4-CPU-INT4-01", "openvino.turboquant.cpu.int4.tbq4.v1", "tbq4")
        && evidence.Key.ModelFamily == OptimizationEvidenceModelFamily.Granite
        && evidence.Key.ModelIdentitySha256 is SourceModelSha256 or OptimizedModelSha256
        && evidence.Key.ParameterCount == ParameterCount
        && evidence.Key.Route == OptimizationRoute.OpenVino
        && evidence.Key.RuntimePackageIdentity == CrossRouteCandidateGenerator.OpenVinoEvidencePackageIdentity(execution)
        && evidence.Key.SourceWeightRepresentation == "int4"
        && evidence.Key.TargetWeightRepresentation == "int4"
        && evidence.Key.Backend == OptimizationEvidenceBackend.OpenVinoCpu
        && evidence.Key.DeviceClass == OptimizationEvidenceDeviceClass.Cpu
        && evidence.Key.ContextTokens == 4096
        && evidence.Key.Workload == "local-chat-v1"
        && evidence.Key.ExecutionProfile == "openvino-cpu"
        && evidence.Key.MethodologyIdentity == SectorQualityMethodologyIdentity
        && evidence.Key.MemoryPerformanceProtocol == SectorQualityMemoryPerformanceProtocol;

    public static bool IsReleasedTurboQuantEvidence(
        OptimizationEvidenceRecord? evidence, OpenVinoExecutionPayload payload) =>
        payload.Device == "CPU"
        && payload.TargetWeightPrecision == OpenVinoWeightPrecision.FourBit
        && payload.KvCacheAlgorithm == OpenVinoKvCacheAlgorithm.TurboQuant
        && !payload.CompiledCacheEnabled
        && (payload.KvCachePrecision, payload.EvidenceId) is
            (OpenVinoKvCachePrecision.Tbq3, "OV-TBQ3-CPU-INT4-01") or
            (OpenVinoKvCachePrecision.Tbq4, "OV-TBQ4-CPU-INT4-01")
        && IsReleasedTurboQuantEvidence(evidence, OpenVinoExecutionAuthority.Create(
            payload.EvidenceId, payload.ConfigurationId, payload.SourceWeightPrecision,
            payload.BuildIdentity, payload.OptimizerVersions, payload.CompiledCacheIsDisposable,
            payload.TurboQuantBuild));
}
