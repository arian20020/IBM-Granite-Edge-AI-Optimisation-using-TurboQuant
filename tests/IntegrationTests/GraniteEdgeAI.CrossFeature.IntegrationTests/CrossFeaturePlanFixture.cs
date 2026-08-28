using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
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
            "model-run-1",
            "model-handoff-1",
            modelDigest,
            4 * GiB,
            "hardware-run-1",
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

    internal static OptimizationWorkload Workload() =>
        OptimizationWorkload.Create(
            "chat",
            512,
            OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);

    internal static OptimizationCandidate Admit(
        OptimizationCandidate candidate,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationJourneyBinding? binding = null)
    {
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding journey = binding ?? Binding();
        OptimizationAdmissionProof proof = OptimizationAdmissionProof.Create(
            snapshot,
            workload,
            journey,
            candidate,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false,
            new HashSet<string>(),
            Issuance(candidate));
        return OptimizationCandidate.AttachAdmissionProof(candidate, proof);
    }

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
        OptimizationCapabilitySnapshot snapshot = SnapshotFor(candidate);
        OptimizationCandidate admitted = Admit(candidate, snapshot);
        OptimizationPreferenceSelection selectedPreference =
            preference ?? OptimizationPreferenceSelection.Manual(50);
        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            [admitted],
            selectedPreference)!;
        return OptimizationPlanIssuer.Issue(
            selection,
            Payload(admitted),
            snapshot,
            Workload(),
            Binding(),
            modelLayerCount: 32,
            Issuance(admitted),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
    }

    internal static OptimizationExecutionPlan PersistentGgufPlan(
        string sourceSha256,
        ulong sourceLengthBytes)
    {
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "model-run-gguf", "model-handoff-gguf", sourceSha256,
            sourceLengthBytes, "hardware-run-gguf", HardwareDigest);
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
                [GgufAdmittedConfiguration.Create(
                    "gguf-q3-persistent", CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu, GgufWeightFormat.Q3KM,
                    GgufKvCacheFormat.F16, GpuOffloadLevel.None,
                    512, 32768, SupportLevel.DeclaredSupported, false)],
                hasHigherPrecisionSource: true,
                conversionSource: source,
                admittedQuantiser: quantiser,
                runtimeAuthority: GgufRuntimeAuthority.Create(
                    "runtime",
                    "0123456789abcdef0123456789abcdef01234567",
                    [GgufExecutionProfileAuthority.Create(
                        "gguf-q3-persistent", EvidenceGrade.Estimated,
                        "profile", false, 4, 128, 256)])));
        OptimizationWorkload workload = Workload();
        OptimizationAdmissionProof proof = OptimizationAdmissionProof.Create(
            snapshot, workload, binding, candidate,
            SupportLevel.DeclaredSupported, false, new HashSet<string>(),
            Issuance(candidate));
        candidate = OptimizationCandidate.AttachAdmissionProof(candidate, proof);
        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            [candidate], OptimizationPreferenceSelection.Automatic())!;
        return OptimizationPlanIssuer.Issue(
            selection, payload, snapshot, workload, binding, 32,
            Issuance(candidate), new FixedTimeProvider(DateTimeOffset.UnixEpoch));
    }

    private static OptimizationHardwareAuthority Hardware() =>
        OptimizationHardwareAuthority.Create(
            HardwareDigest,
            [DeviceRouteId.Cpu],
            [CompatibilityBackend.OpenVinoCpu],
            ByteCount.FromBytes(64 * GiB),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            OptimizationFreshnessPolicy.Version);

    private static OptimizationIssuanceAuthority Issuance(
        OptimizationCandidate candidate) =>
        OptimizationIssuanceAuthority.FromGeneration(
            Hardware(),
            ByteCount.FromBytes(candidate.Metrics.SafeBudgetBytes),
            ByteCount.FromBytes(candidate.Metrics.AvailableDiskBytes!.Value));

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
