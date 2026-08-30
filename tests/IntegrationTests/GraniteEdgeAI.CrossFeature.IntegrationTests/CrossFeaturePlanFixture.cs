using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

internal static class CrossFeaturePlanFixture
{
    internal const ulong GiB = 1024UL * 1024 * 1024;
    internal const string ModelDigest =
        "1111111111111111111111111111111111111111111111111111111111111111";
    internal const string HardwareDigest =
        "2222222222222222222222222222222222222222222222222222222222222222";
    internal const string CapabilityDigest =
        "3333333333333333333333333333333333333333333333333333333333333333";

    internal static OptimizationJourneyBinding Binding(
        string modelDigest = ModelDigest,
        string hardwareDigest = HardwareDigest) =>
        OptimizationJourneyBinding.Create(
            "11111111111141118111111111111111",
            "33333333333343338333333333333333",
            modelDigest,
            4 * GiB,
            "22222222222242228222222222222222",
            hardwareDigest);

    internal static OptimizationCandidate Candidate(
        OpenVinoWeightFormat weights = OpenVinoWeightFormat.Int8,
        string evidenceId = "ov-int8",
        OptimizationAssessment quality = OptimizationAssessment.Good,
        ulong predictedPeakBytes = 8 * GiB)
    {
        bool persistent = weights != OpenVinoWeightFormat.Original;
        return OptimizationCandidate.Create(
            OpenVinoRouteConfiguration.Create(
                weights,
                OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                streams: 1),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                quality,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                contextTokens: 4096,
                predictedPeakBytes,
                safeBudgetBytes: 32 * GiB,
                headroomBytes: 32 * GiB - predictedPeakBytes,
                workingDiskBytes: persistent ? 4 * GiB : 0,
                outputDiskBytes: persistent ? 4 * GiB : 0,
                requiresPersistentChange: persistent,
                availableDiskBytes: 500 * GiB),
            evidenceId,
            isExperimental: false);
    }

    internal static OptimizationCapabilitySnapshot SnapshotFor(
        params OptimizationCandidate[] candidates)
    {
        List<OpenVinoAdmittedConfiguration> admitted = [];
        List<OpenVinoExecutionAuthority> authorities = [];
        foreach (OptimizationCandidate candidate in candidates)
        {
            OpenVinoRouteConfiguration configuration =
                (OpenVinoRouteConfiguration)candidate.Configuration;
            (OpenVinoWeightPrecision source, OpenVinoWeightPrecision target) =
                Precisions(configuration.Weights);
            string profile = Profile(candidate.EvidenceId);
            OpenVinoBuildIdentity build = Build();
            Dictionary<string, string> versions = Versions();
            admitted.Add(OpenVinoAdmittedConfiguration.Create(
                candidate.EvidenceId,
                configuration.Device,
                configuration.Weights,
                configuration.KvCache,
                configuration.PerformanceHint,
                configuration.CompiledCache,
                configuration.Streams,
                minimumContextTokens: 512,
                maximumContextTokens: 32768,
                SupportLevel.DeclaredSupported,
                requiresEvidence: false));
            authorities.Add(OpenVinoExecutionAuthority.Create(
                candidate.EvidenceId,
                profile,
                source,
                build,
                versions,
                compiledCacheIsDisposable: true));
        }

        return OptimizationCapabilitySnapshot.ForOpenVino(
            "ov-capability-1",
            CapabilityDigest,
            OpenVinoCapabilityPayload.Create(
                "2026.3.0",
                admitted,
                authorities));
    }

    private static OptimizationWorkload Workload() =>
        OptimizationWorkload.Create(
            "chat",
            512,
            OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);

    internal static OptimizationExecutionPayload Payload(
        OptimizationCandidate candidate)
    {
        OpenVinoRouteConfiguration configuration =
            (OpenVinoRouteConfiguration)candidate.Configuration;
        (OpenVinoWeightPrecision source, OpenVinoWeightPrecision target) =
            Precisions(configuration.Weights);
        return OptimizationExecutionPayload.ForOpenVino(
            OpenVinoExecutionPayload.Create(
                Profile(candidate.EvidenceId),
                "CPU",
                "Standard candidate",
                candidate.EvidenceId,
                source,
                target,
                OpenVinoKvCachePrecision.U8,
                compiledCacheEnabled: false,
                compiledCacheIsDisposable: true,
                compiledCacheIsModelArtifact: false,
                createsCompletePackage: candidate.Metrics.RequiresPersistentChange,
                Build(),
                Versions()));
    }

    internal static OptimizationExecutionPlan Issue(
        OpenVinoWeightFormat weights = OpenVinoWeightFormat.Int8,
        OptimizationPreferenceSelection? preference = null)
    {
        string evidence = weights == OpenVinoWeightFormat.Original
            ? "ov-original"
            : "ov-int8";
        OptimizationCandidate candidate = Candidate(weights, evidence);
        OpenVinoWeightFormat baselineWeights =
            weights == OpenVinoWeightFormat.Original
                ? OpenVinoWeightFormat.Int8
                : OpenVinoWeightFormat.Original;
        OptimizationCandidate baseline = Candidate(
            baselineWeights,
            baselineWeights == OpenVinoWeightFormat.Original
                ? "ov-original"
                : "ov-int8");
        OptimizationCapabilitySnapshot snapshot = SnapshotFor(baseline, candidate);
        OptimizationPreferenceSelection selectedPreference =
            preference ?? OptimizationPreferenceSelection.Manual(50);
        CompatibilityProductionInput input = OpenVinoInput(candidate, snapshot);
        CompatibilityEvaluation evaluation = CompatibilityEngine.EvaluateProduction(
            input, new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        return evaluation.PlanningSession!.Issue(
            selectedPreference,
            new OpenVinoComposer(),
            CompatibilityEngine.CreateOptimizationIssuanceAuthority(
                input, DateTimeOffset.UnixEpoch),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
    }

    internal static OptimizationExecutionPlan PersistentGgufPlan(
        string sourceSha256,
        ulong sourceLengthBytes)
    {
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "44444444444444448444444444444444",
            "66666666666646668666666666666666",
            sourceSha256, sourceLengthBytes,
            "55555555555545558555555555555555", HardwareDigest);
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Q3KM, GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated, OptimizationAssessment.Good,
                OptimizationAssessment.Good, OptimizationAssessment.Good,
                4096, GiB, 4 * GiB, 3 * GiB, 2 * GiB, 2 * GiB,
                requiresPersistentChange: true,
                availableDiskBytes: 500 * GiB),
            "gguf-q3-persistent", isExperimental: false,
            OptimizationConversionProvenance.HigherPrecisionSource);
        GgufQuantiserIdentity quantiser = GgufQuantiserIdentity.Create(
            "llama-quantize-test", "build-test-1", CapabilityDigest);
        GgufConversionSourceBinding source =
            GgufConversionSourceBinding.Create(WeightQuantisation.F16, binding);
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForGguf(
            GgufExecutionPayload.Create(
                "runtime", "0123456789abcdef0123456789abcdef01234567",
                GgufRuntimeBackend.Cpu, "CPU", 4096,
                GgufCacheType.F16, GgufCacheType.F16, 0, false, 4, 128,
                "Estimated", "profile", 256, GgufWeightFormat.Q3KM,
                quantiser, source));
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-capability-persistent", CapabilityDigest,
            GgufCapabilityPayload.Create(
                "runtime",
                [
                    GgufAdmittedConfiguration.Create(
                        "gguf-imported", CompatibilityBackend.Cpu,
                        DeviceRouteId.Cpu, GgufWeightFormat.Imported,
                        GgufKvCacheFormat.F16, GpuOffloadLevel.None,
                        512, 32768, SupportLevel.DeclaredSupported, false),
                    GgufAdmittedConfiguration.Create(
                        "gguf-q3-persistent", CompatibilityBackend.Cpu,
                        DeviceRouteId.Cpu, GgufWeightFormat.Q3KM,
                        GgufKvCacheFormat.F16, GpuOffloadLevel.None,
                        512, 32768, SupportLevel.DeclaredSupported, false)
                ],
                hasHigherPrecisionSource: true,
                conversionSource: source,
                admittedQuantiser: quantiser,
                runtimeAuthority: GgufRuntimeAuthority.Create(
                    "runtime",
                    "0123456789abcdef0123456789abcdef01234567",
                    [
                        GgufExecutionProfileAuthority.Create(
                            "gguf-imported", EvidenceGrade.Estimated,
                            "profile-imported", false, 4, 128, 256),
                        GgufExecutionProfileAuthority.Create(
                            "gguf-q3-persistent", EvidenceGrade.Estimated,
                            "profile", false, 4, 128, 256)
                    ])));
        CompatibilityProductionInput input = GgufInput(
            sourceLengthBytes, binding, snapshot);
        CompatibilityEvaluation evaluation = CompatibilityEngine.EvaluateProduction(
            input, new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        return evaluation.PlanningSession!.Issue(
            OptimizationPreferenceSelection.Automatic(),
            new FixedPayloadComposer(OptimizationRoute.Gguf, payload),
            CompatibilityEngine.CreateOptimizationIssuanceAuthority(
                input, DateTimeOffset.UnixEpoch),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
    }

    private static CompatibilityProductionInput OpenVinoInput(
        OptimizationCandidate candidate,
        OptimizationCapabilitySnapshot snapshot)
    {
        Guid modelRun = Guid.Parse("11111111-1111-4111-8111-111111111111");
        Guid hardwareRun = Guid.Parse("22222222-2222-4222-8222-222222222222");
        Guid handoff = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var model = OpenVinoCompatibilityModelInput.Create(
            4 * GiB, 32, 4096, 32, 8, 8192);
        OpenVinoWeightFormat currentWeights =
            candidate.Configuration is OpenVinoRouteConfiguration desired
                && desired.Weights == OpenVinoWeightFormat.Original
                    ? OpenVinoWeightFormat.Int8
                    : OpenVinoWeightFormat.Original;
        var current = CompatibilityCurrentModelInput.ForOpenVino(
            model,
            OpenVinoRouteConfiguration.Create(
                currentWeights, OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1),
            OpenVinoWeightPrecision.Fp16);
        var hardware = CompatibilityHardwareInput.Create(
            64 * GiB, 8 * GiB, 500 * GiB,
            [DeviceRouteId.Cpu], [CompatibilityBackend.OpenVinoCpu]);
        OptimizationJourneyBinding binding = Binding();
        return CompatibilityProductionInput.Create(
            modelRun, hardwareRun, current,
            CompatibilityJourneyAuthorityInput.Create(
                handoff, ModelDigest, CompatibilityFactDigest.ComputeModel(current),
                HardwareDigest, CompatibilityFactDigest.ComputeHardware(hardware)),
            hardware,
            CompatibilityFreshResourcesInput.Create(
                48 * GiB, 4 * GiB, 500 * GiB, DateTimeOffset.UnixEpoch),
            CompatibilityOptimizationProductionInput.Create(
                snapshot, Workload(), binding, new HashSet<string>()));
    }

    private static CompatibilityProductionInput GgufInput(
        ulong sourceLengthBytes,
        OptimizationJourneyBinding binding,
        OptimizationCapabilitySnapshot snapshot)
    {
        Guid modelRun = Guid.Parse("44444444-4444-4444-8444-444444444444");
        Guid hardwareRun = Guid.Parse("55555555-5555-4555-8555-555555555555");
        Guid handoff = Guid.Parse("66666666-6666-4666-8666-666666666666");
        var model = GgufCompatibilityModelInput.Create(
            sourceLengthBytes, 32, 4096, 32, 8, 8192, 1, 2);
        var current = CompatibilityCurrentModelInput.ForGguf(
            model,
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                GpuOffloadLevel.None));
        var hardware = CompatibilityHardwareInput.Create(
            64 * GiB, 0, 500 * GiB,
            [DeviceRouteId.Cpu], [CompatibilityBackend.Cpu]);
        return CompatibilityProductionInput.Create(
            modelRun, hardwareRun, current,
            CompatibilityJourneyAuthorityInput.Create(
                handoff, binding.ModelSha256,
                CompatibilityFactDigest.ComputeModel(current), HardwareDigest,
                CompatibilityFactDigest.ComputeHardware(hardware)),
            hardware,
            CompatibilityFreshResourcesInput.Create(
                48 * GiB, 0, 500 * GiB, DateTimeOffset.UnixEpoch),
            CompatibilityOptimizationProductionInput.Create(
                snapshot, Workload(), binding, new HashSet<string>()));
    }

    private sealed class OpenVinoComposer : IOptimizationExecutionPayloadComposer
    {
        public OptimizationRoute Route => OptimizationRoute.OpenVino;
        public OptimizationExecutionPayload Compose(OptimizationCandidate candidate) =>
            Payload(candidate);
    }

    private sealed class FixedPayloadComposer(
        OptimizationRoute route,
        OptimizationExecutionPayload payload) : IOptimizationExecutionPayloadComposer
    {
        public OptimizationRoute Route { get; } = route;
        public OptimizationExecutionPayload Compose(OptimizationCandidate candidate) =>
            payload;
    }

    private static OpenVinoBuildIdentity Build() =>
        OpenVinoBuildIdentity.Create(
            "2026.3.0",
            "2026.3.0.0",
            "2026.3.0",
            HardwareDigest);

    private static Dictionary<string, string> Versions() =>
        new(StringComparer.Ordinal) { ["openvino"] = "2026.3.0" };

    private static string Profile(string evidenceId) =>
        evidenceId == "ov-original"
            ? "openvino.standard.cpu.original.default.v1"
            : "openvino.standard.cpu.int8.default.v1";

    private static (OpenVinoWeightPrecision Source, OpenVinoWeightPrecision Target)
        Precisions(OpenVinoWeightFormat weights) => weights switch
        {
            OpenVinoWeightFormat.Original =>
                (OpenVinoWeightPrecision.Fp16, OpenVinoWeightPrecision.Fp16),
            OpenVinoWeightFormat.Int8 =>
                (OpenVinoWeightPrecision.Fp16, OpenVinoWeightPrecision.EightBit),
            _ =>
                (OpenVinoWeightPrecision.Fp16, OpenVinoWeightPrecision.FourBit)
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
