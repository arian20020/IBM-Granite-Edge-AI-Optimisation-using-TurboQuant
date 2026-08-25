using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

[TestClass]
public sealed class OptimizationExecutionContractV3Tests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;
    private const string Digest64 =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string OtherDigest64 =
        "2222222222222222222222222222222222222222222222222222222222222222";
    private const string Commit40 = "0123456789abcdef0123456789abcdef01234567";

    [TestMethod]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq4, OpenVinoKvCachePrecision.Tbq4)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq3, OpenVinoKvCachePrecision.Tbq3)]
    public void OptedInTurboQuantCandidateIssuesAnExactV3Plan(
        OpenVinoKvCacheFormat cache,
        OpenVinoKvCachePrecision precision)
    {
        (OptimizationSelection selection, OptimizationCapabilitySnapshot snapshot,
            OptimizationWorkload workload) = SelectedTurboCandidate(cache);
        TurboQuantBuildIdentity turboBuild = TurboBuild();

        OptimizationExecutionPlan plan = OptimizationPlanIssuer.Issue(
            selection,
            OptimizationExecutionPayload.ForOpenVino(Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                precision,
                turboBuild)),
            snapshot,
            workload,
            Binding(),
            modelLayerCount: 32,
            DateTimeOffset.UnixEpoch);

        Assert.AreEqual(3, plan.ContractVersion);
        Assert.IsFalse(plan.IsExecutableBy(2));
        Assert.AreEqual(OpenVinoKvCacheAlgorithm.TurboQuant,
            plan.ExecutionPayload.OpenVino!.KvCacheAlgorithm);
        Assert.AreEqual(precision, plan.ExecutionPayload.OpenVino.KvCachePrecision);
        Assert.AreSame(turboBuild, plan.ExecutionPayload.OpenVino.TurboQuantBuild);
    }

    [TestMethod]
    [DataRow(OpenVinoKvCachePrecision.Tbq4)]
    [DataRow(OpenVinoKvCachePrecision.Tbq3)]
    public void TurboQuantPrecisionRequiresPinnedBuildIdentity(
        OpenVinoKvCachePrecision precision)
    {
        Assert.ThrowsExactly<ArgumentException>(() => Payload(
            OpenVinoKvCacheAlgorithm.TurboQuant,
            precision,
            turboBuild: null));
    }

    [TestMethod]
    public void ReleasedCacheForbidsTurboQuantBuildIdentity()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Payload(
            OpenVinoKvCacheAlgorithm.Released,
            OpenVinoKvCachePrecision.U4,
            TurboBuild()));
    }

    [TestMethod]
    public void CacheAlgorithmAndPrecisionMustDescribeTheSameFamily()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Payload(
            OpenVinoKvCacheAlgorithm.Released,
            OpenVinoKvCachePrecision.Tbq4,
            TurboBuild()));

        Assert.ThrowsExactly<ArgumentException>(() => Payload(
            OpenVinoKvCacheAlgorithm.TurboQuant,
            OpenVinoKvCachePrecision.U4,
            TurboBuild()));
    }

    [TestMethod]
    public void IssuerRejectsAnyTurboQuantCacheDisagreement()
    {
        (OptimizationSelection selection, OptimizationCapabilitySnapshot snapshot,
            OptimizationWorkload workload) =
            SelectedTurboCandidate(OpenVinoKvCacheFormat.TurboQuantTbq4);

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection,
            OptimizationExecutionPayload.ForOpenVino(Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq3,
                TurboBuild())),
            snapshot,
            workload,
            Binding(),
            32,
            DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void V3CanonicalOrderBindsAlgorithmPrecisionAndTurboQuantIdentity()
    {
        (OptimizationSelection selection, _, _) =
            SelectedTurboCandidate(OpenVinoKvCacheFormat.TurboQuantTbq4);
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForOpenVino(
            Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboBuild()));

        string canonical = OptimizationCanonicalizer.Canonicalize(
            selection.Candidate, payload, contractVersion: 3);

        int algorithm = canonical.IndexOf("ov.kvCacheAlgorithm", StringComparison.Ordinal);
        int precision = canonical.IndexOf("ov.kvCachePrecision", StringComparison.Ordinal);
        int identity = canonical.IndexOf("ov.turboQuant.sourceCommit", StringComparison.Ordinal);

        Assert.IsTrue(algorithm >= 0);
        Assert.IsTrue(algorithm < precision);
        Assert.IsTrue(precision < identity);
    }

    [TestMethod]
    public void TurboQuantTbq4V3CanonicalVectorRemainsFrozen()
    {
        OptimizationCandidate candidate = SelectedTurboCandidate(
            OpenVinoKvCacheFormat.TurboQuantTbq4).Selection.Candidate;
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForOpenVino(
            Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboBuild()));

        Assert.AreEqual(
            "v=1:3|route=1:2|config=79:openvino|w=Int4|kv=TurboQuantTbq4|dev=Cpu|hint=Latency|cache=Disabled|streams=1|ctx=4:4096|persistent=1:1|evidence=17:ov-turbo-evidence|experimental=1:1|ov.configurationId=39:openvino.experimental.cpu.int4.turbo.v3|ov.device=3:CPU|ov.maturity=22:Experimental candidate|ov.evidenceId=17:ov-turbo-evidence|ov.sourceWeightPrecision=1:0|ov.targetWeightPrecision=1:2|ov.kvCacheAlgorithm=1:2|ov.kvCachePrecision=1:5|ov.compiledCacheEnabled=1:0|ov.compiledCacheIsDisposable=1:1|ov.compiledCacheIsModelArtifact=1:0|ov.createsCompletePackage=1:1|ov.build.runtimeBuild=8:2026.3.0|ov.build.genAiBuild=10:2026.3.0.0|ov.build.tokenizersBuild=8:2026.3.0|ov.build.workerManifestDigest=64:1111111111111111111111111111111111111111111111111111111111111111|ov.optimizer.nncf=5:3.3.0|ov.optimizer.openvino=8:2026.3.0|ov.turboQuant.sourceCommit=40:0123456789abcdef0123456789abcdef01234567|ov.turboQuant.implementationCommit=40:0123456789abcdef0123456789abcdef01234567|ov.turboQuant.patchSeriesDigest=64:1111111111111111111111111111111111111111111111111111111111111111|ov.turboQuant.runtimeManifestDigest=64:2222222222222222222222222222222222222222222222222222222222222222",
            OptimizationCanonicalizer.Canonicalize(candidate, payload, contractVersion: 3));
        Assert.AreEqual(
            "8bcdb511fd67ff1c76e5cf43e52c3028cf0cbee7eb1ca20494358455bf72d3b4",
            OptimizationCanonicalizer.ConfigurationSha256(
                candidate, payload, contractVersion: 3));
    }

    [TestMethod]
    public void V3DigestChangesWithTurboQuantPrecisionAndBuildIdentity()
    {
        OptimizationCandidate tbq4 = SelectedTurboCandidate(
            OpenVinoKvCacheFormat.TurboQuantTbq4).Selection.Candidate;
        OptimizationCandidate tbq3 = SelectedTurboCandidate(
            OpenVinoKvCacheFormat.TurboQuantTbq3).Selection.Candidate;

        string baseline = OptimizationCanonicalizer.ConfigurationSha256(
            tbq4,
            OptimizationExecutionPayload.ForOpenVino(Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboBuild())),
            contractVersion: 3);

        string precisionChanged = OptimizationCanonicalizer.ConfigurationSha256(
            tbq3,
            OptimizationExecutionPayload.ForOpenVino(Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq3,
                TurboBuild())),
            contractVersion: 3);

        string identityChanged = OptimizationCanonicalizer.ConfigurationSha256(
            tbq4,
            OptimizationExecutionPayload.ForOpenVino(Payload(
                OpenVinoKvCacheAlgorithm.TurboQuant,
                OpenVinoKvCachePrecision.Tbq4,
                TurboQuantBuildIdentity.Create(
                    "89abcdef0123456789abcdef0123456789abcdef",
                    Commit40,
                    Digest64,
                    OtherDigest64))),
            contractVersion: 3);

        Assert.AreNotEqual(baseline, precisionChanged);
        Assert.AreNotEqual(baseline, identityChanged);
    }

    [TestMethod]
    public void ControlledGgufRequantisationRequiresExactQuantiserAndSourceBinding()
    {
        OptimizationJourneyBinding acknowledgedBinding = GgufBinding(Digest64);
        GgufQuantiserIdentity quantiser = GgufQuantiser(Digest64);
        (OptimizationSelection selection, OptimizationCapabilitySnapshot snapshot,
            OptimizationWorkload workload) = SelectedGgufCandidate(
                acknowledgedBinding, quantiser);

        OptimizationExecutionPlan plan = OptimizationPlanIssuer.Issue(
            selection,
            GgufPayload(quantiser),
            snapshot,
            workload,
            acknowledgedBinding,
            32,
            DateTimeOffset.UnixEpoch);

        Assert.IsTrue(plan.ProducesPersistentArtifact);
        Assert.AreEqual(quantiser, plan.ExecutionPayload.Gguf!.Quantiser);

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection,
            GgufPayload(GgufQuantiser(OtherDigest64)),
            snapshot,
            workload,
            acknowledgedBinding,
            32,
            DateTimeOffset.UnixEpoch));

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection,
            GgufPayload(quantiser),
            snapshot,
            workload,
            GgufBinding(OtherDigest64),
            32,
            DateTimeOffset.UnixEpoch));

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection,
            GgufPayload(quantiser),
            snapshot,
            workload,
            GgufBindingWithSource(OtherDigest64, 3 * Gibibyte),
            32,
            DateTimeOffset.UnixEpoch));

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            selection,
            GgufPayload(quantiser),
            snapshot,
            workload,
            GgufBindingWithSource(Digest64, 3 * Gibibyte + 1),
            32,
            DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void V3DigestBindsTheTypedLowQualityWarningWithoutChangingV2Layout()
    {
        OptimizationJourneyBinding binding = GgufBinding(Digest64);
        GgufQuantiserIdentity quantiser = GgufQuantiser(Digest64);
        OptimizationCandidate warned = SelectedGgufCandidate(binding, quantiser)
            .Selection.Candidate;
        OptimizationCandidate unwarned = OptimizationCandidate.Create(
            warned.Configuration,
            warned.Metrics,
            warned.EvidenceId,
            warned.IsExperimental,
            OptimizationCandidateNotice.LowQuality);
        OptimizationExecutionPayload payload = GgufPayload(quantiser);

        Assert.AreNotEqual(
            OptimizationCanonicalizer.ConfigurationSha256(warned, payload, 3),
            OptimizationCanonicalizer.ConfigurationSha256(unwarned, payload, 3));
        Assert.AreEqual(
            OptimizationCanonicalizer.ConfigurationSha256V2(warned, payload),
            OptimizationCanonicalizer.ConfigurationSha256V2(unwarned, payload));
    }

    private static (OptimizationSelection Selection,
        OptimizationCapabilitySnapshot Snapshot,
        OptimizationWorkload Workload) SelectedGgufCandidate(
            OptimizationJourneyBinding binding,
            GgufQuantiserIdentity quantiser)
    {
        GgufRequantisationPolicy policy = GgufRequantisationPolicy.Create(
            true, true, true, "gguf-q2", quantiser, binding);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap",
            Digest64,
            GgufCapabilityPayload.Create(
                "b4321",
                [
                    GgufAdmittedConfiguration.Create(
                        "gguf-q2", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                        GgufWeightFormat.Q2K, GgufKvCacheFormat.F16,
                        GpuOffloadLevel.None, 512, 32768,
                        SupportLevel.DeclaredSupported, requiresEvidence: true)
                ],
                hasHigherPrecisionSource: false,
                policy));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
            snapshot,
            InspectedModelFacts.Create(
                ByteCount.FromBytes(3 * Gibibyte), 32, 4096, 32, 8, 8192, 15, 2),
            workload,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>());
        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            generated.Candidates, OptimizationPreferenceSelection.Manual(10))
            ?? throw new AssertFailedException("The authorized Q2_K candidate was absent.");

        return (selection, snapshot, workload);
    }

    private static OptimizationJourneyBinding GgufBinding(string hardwareDigest) =>
        OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", Digest64, 3 * Gibibyte,
            "hw-run-1", hardwareDigest);

    private static OptimizationJourneyBinding GgufBindingWithSource(
        string modelDigest, ulong modelLength) =>
        OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", modelDigest, modelLength,
            "hw-run-1", Digest64);

    private static GgufQuantiserIdentity GgufQuantiser(string executableDigest) =>
        GgufQuantiserIdentity.Create("llama-quantize", "b4321", executableDigest);

    private static OptimizationExecutionPayload GgufPayload(
        GgufQuantiserIdentity quantiser) =>
        OptimizationExecutionPayload.ForGguf(GgufExecutionPayload.Create(
            "b4321", Commit40, GgufRuntimeBackend.Cpu, "CPU", 4096,
            GgufCacheType.F16, GgufCacheType.F16, 0, false, 4, 128,
            "estimated", "profile", 256, GgufWeightFormat.Q2K, quantiser));

    private static (OptimizationSelection Selection,
        OptimizationCapabilitySnapshot Snapshot,
        OptimizationWorkload Workload) SelectedTurboCandidate(OpenVinoKvCacheFormat cache)
    {
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForOpenVino(
            "ov-cap",
            Digest64,
            OpenVinoCapabilityPayload.Create(
                "2026.3.0",
                [
                    OpenVinoAdmittedConfiguration.Create(
                        "ov-turbo-evidence",
                        DeviceRouteId.Cpu,
                        OpenVinoWeightFormat.Int4,
                        cache,
                        OpenVinoPerformanceHint.Latency,
                        Core.Routes.OpenVino.OpenVinoCompiledCachePolicy.Disabled,
                        1,
                        512,
                        32768,
                        SupportLevel.Experimental,
                        requiresEvidence: true)
                ]));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat",
            512,
            OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
            snapshot,
            InspectedModelFacts.Create(
                ByteCount.FromBytes(3 * Gibibyte), 32, 4096, 32, 8, 8192, 15, 2),
            workload,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string> { "ov-turbo-evidence" });

        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            generated.Candidates,
            OptimizationPreferenceSelection.Automatic())
            ?? throw new AssertFailedException("The opted-in TurboQuant candidate was absent.");

        return (selection, snapshot, workload);
    }

    private static OpenVinoExecutionPayload Payload(
        OpenVinoKvCacheAlgorithm algorithm,
        OpenVinoKvCachePrecision precision,
        TurboQuantBuildIdentity? turboBuild) =>
        OpenVinoExecutionPayload.Create(
            "openvino.experimental.cpu.int4.turbo.v3",
            "CPU",
            "Experimental candidate",
            "ov-turbo-evidence",
            OpenVinoWeightPrecision.Fp16,
            OpenVinoWeightPrecision.FourBit,
            precision,
            compiledCacheEnabled: false,
            compiledCacheIsDisposable: true,
            compiledCacheIsModelArtifact: false,
            createsCompletePackage: true,
            Build(),
            Versions(),
            turboQuantBuild: turboBuild,
            kvCacheAlgorithm: algorithm);

    private static OptimizationJourneyBinding Binding() =>
        OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", Digest64, 4 * Gibibyte,
            "hw-run-1", OtherDigest64);

    private static OpenVinoBuildIdentity Build() =>
        OpenVinoBuildIdentity.Create("2026.3.0", "2026.3.0.0", "2026.3.0", Digest64);

    private static TurboQuantBuildIdentity TurboBuild() =>
        TurboQuantBuildIdentity.Create(Commit40, Commit40, Digest64, OtherDigest64);

    private static Dictionary<string, string> Versions() => new(StringComparer.Ordinal)
    {
        ["nncf"] = "3.3.0",
        ["openvino"] = "2026.3.0"
    };
}
