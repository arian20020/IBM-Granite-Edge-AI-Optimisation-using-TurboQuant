using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.OpenVinoRoute.TurboQuant;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
using RouteCompiledCachePolicy = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoCompiledCachePolicy;

namespace GraniteEdgeAI.OpenVino.Tests.Optimization;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class OpenVinoExperimentalCapabilityTests
{
    private const string EvidenceCommit =
        "64d8272666af8855dab306ee026d43b6986be519";
    private const string ModelId = "ibm-granite/granite-4.1-3b";
    private const string SourceDigest =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const ulong SourceLength = 88;
    private static readonly string PackageDigest = new('1', 64);
    private static readonly string ModelDigest = new('2', 64);

    [TestMethod]
    public void MissingActivationEvidencePublishesNoTurboQuantCandidate()
    {
        OpenVinoCapabilityPayload payload =
            OpenVinoOptimizationCapabilityProjector.Project(OfficialEvidence());

        Assert.IsFalse(payload.Admitted.Any(static admission =>
            admission.Level == SupportLevel.Experimental));
        AssertOfficialFallback(payload);
    }

    [TestMethod]
    [DataRow("incomplete")]
    [DataRow("worker")]
    [DataRow("source")]
    [DataRow("binary")]
    [DataRow("runtime")]
    [DataRow("device")]
    [DataRow("format")]
    [DataRow("activation")]
    [DataRow("evidence")]
    public void IncompleteOrMismatchedEvidencePublishesNoExperimentalCandidate(
        string mismatch)
    {
        TurboQuantCampaignEvidence evidence = mismatch switch
        {
            "incomplete" => Campaign() with { QualityPassed = false },
            "worker" => Campaign() with
            {
                BuildEvidence = BuildEvidence() with
                {
                    WorkerManifestDigest = new string('d', 64)
                }
            },
            "source" => Campaign() with
            {
                BuildEvidence = BuildEvidence() with
                {
                    TurboQuantBuild = BuildEvidence().TurboQuantBuild! with
                    {
                        SourceCommit = new string('d', 40)
                    }
                }
            },
            "binary" => Campaign() with
            {
                BuildEvidence = BuildEvidence() with
                {
                    TurboQuantBuild = BuildEvidence().TurboQuantBuild! with
                    {
                        PatchSeriesDigest = new string('d', 64)
                    }
                }
            },
            "runtime" => Campaign() with
            {
                BuildEvidence = BuildEvidence() with
                {
                    TurboQuantBuild = BuildEvidence().TurboQuantBuild! with
                    {
                        RuntimeManifestDigest = new string('e', 64)
                    }
                }
            },
            "device" => Campaign() with { RequestedDevice = "GPU" },
            "format" => Campaign() with
            {
                Activation = Activation() with
                {
                    ActualKeyCodec = TurboQuantCodec.ScalarU4
                }
            },
            "activation" => Campaign() with
            {
                Activation = Activation() with { RuntimeDispatchCount = 0 }
            },
            "evidence" => Campaign() with
            {
                EvidenceCommit = new string('f', 40)
            },
            _ => throw new AssertFailedException(mismatch)
        };
        TurboQuantActivationState state = Policy().Evaluate(evidence);

        OpenVinoCapabilityPayload payload =
            OpenVinoOptimizationCapabilityProjector.Project(OfficialEvidence());

        Assert.IsFalse(state.CanActivate);
        Assert.IsFalse(payload.Admitted.Any(static admission =>
            admission.Level == SupportLevel.Experimental));
        AssertOfficialFallback(payload);
    }

    [TestMethod]
    public void ExactEvidenceDoesNotPublishTbq4WithoutAnExactPlanExecutor()
    {
        TurboQuantActivationState state = Policy().Evaluate(Campaign());

        OpenVinoCapabilityPayload payload =
            OpenVinoOptimizationCapabilityProjector.Project(OfficialEvidence());

        Assert.IsTrue(state.CanActivate);
        Assert.IsFalse(payload.Admitted.Any(static admission =>
            admission.Level == SupportLevel.Experimental));
        AssertOfficialFallback(payload);
    }

    [TestMethod]
    public void ExactTbq4AdmissionCannotCrossTheExistingPlanExecutor()
    {
        OpenVinoCapabilityPayload payload = ExperimentalPayload();
        OpenVinoAdmittedConfiguration experimental = payload.Admitted.Single(
            static admission => admission.Level == SupportLevel.Experimental);
        OptimizationCapabilitySnapshot snapshot = Snapshot(payload);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => Plan(snapshot, experimental));
    }

    private static void AssertOfficialFallback(OpenVinoCapabilityPayload payload)
    {
        string[] expected =
        [
            "OV-STD-CPU-ORIGINAL-01",
            "OV-STD-CPU-FP16-01",
            "OV-STD-CPU-AUTO-01",
            "OV-STD-CPU-INT8-U8-01",
            "OV-STD-CPU-INT4-U8-01"
        ];
        CollectionAssert.AreEqual(
            expected,
            payload.Admitted
                .Where(static admission =>
                    admission.Level == SupportLevel.DeclaredSupported)
                .Select(static admission => admission.EvidenceId)
                .ToArray());
    }

    private static OpenVinoOptimizationCapabilityEvidence OfficialEvidence() =>
        new(
            BuildEvidence() with { TurboQuantBuild = null },
            new OpenVinoOptimizationToolVersions(
                "2026.3.0", "2026.3.0.0", "3.3.0",
                "2.3.0", "2.1.0", "5.5.4"),
            [
                Admission("OV-STD-CPU-ORIGINAL-01",
                    OpenVinoWeightPrecision.Original,
                    OpenVinoKvCachePrecision.ReleasedDefault),
                Admission("OV-STD-CPU-FP16-01",
                    OpenVinoWeightPrecision.Fp16,
                    OpenVinoKvCachePrecision.ReleasedDefault),
                Admission("OV-STD-CPU-AUTO-01",
                    OpenVinoWeightPrecision.EightBit,
                    OpenVinoKvCachePrecision.ReleasedDefault),
                Admission("OV-STD-CPU-INT8-U8-01",
                    OpenVinoWeightPrecision.EightBit,
                    OpenVinoKvCachePrecision.U8),
                Admission("OV-STD-CPU-INT4-U8-01",
                    OpenVinoWeightPrecision.FourBit,
                    OpenVinoKvCachePrecision.U8)
            ]);

    private static OpenVinoOptimizationCapabilityAdmission Admission(
        string evidenceId,
        OpenVinoWeightPrecision weights,
        OpenVinoKvCachePrecision kvCache) =>
        new(
            evidenceId,
            "CPU",
            weights,
            new OpenVinoRuntimeOptimization(
                kvCache, RouteCompiledCachePolicy.Disabled),
            OpenVinoCapabilityPerformanceHint.Latency,
            1,
            512,
            4_096,
            OpenVinoCapabilityMaturity.Released);

    private static TurboQuantActivationPolicy Policy() => new(
        new TurboQuantApprovedTuple(
            EvidenceCommit,
            ModelId,
            PackageDigest,
            ModelDigest,
            checked((long)SourceLength),
            BuildEvidence()));

    private static TurboQuantCampaignEvidence Campaign() => new(
        EvidenceCommit,
        ModelId,
        PackageDigest,
        ModelDigest,
        checked((long)SourceLength),
        "CPU",
        ["CPU"],
        BuildEvidence(),
        WorkerClosureVerified: true,
        SecurityReviewApproved: true,
        LicenseReviewApproved: true,
        Activation(),
        TurboQuantActivationPolicy.QualityRubricId,
        MatchedOfficialBaseline: true,
        DeterministicSmokePassed: true,
        MemoryReductionPassed: true,
        QualityPassed: true,
        PerformancePassed: true,
        RepeatabilityPassed: true,
        ContextScalingPassed: true,
        CancellationPassed: true,
        CleanupPassed: true,
        CorruptionPassed: true,
        StreamingPassed: true,
        CompletedTurnCount: 2);

    private static OpenVinoBuildEvidence BuildEvidence() => new(
        "2026.3.0-22451-8a17657b995-releases/2026/3",
        "2026.3.0.0-3277-bd8d6542e3c",
        "2026.3.0.0-703-183c6f25cda",
        new string('a', 64),
        new TurboQuantBuildEvidence(
            TurboQuantActivationPolicy.SourceCommit,
            TurboQuantActivationPolicy.ImplementationCommit,
            new string('b', 64),
            new string('c', 64)));

    private static TurboQuantActivationEvent Activation() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        TurboQuantCodec.Tbq4,
        TurboQuantCodec.Tbq4,
        TurboQuantCodec.Tbq4,
        TurboQuantCodec.Tbq4,
        TurboQuantAttentionPath.Sdpa,
        64,
        2,
        22,
        32,
        32,
        128,
        704,
        2816,
        TurboQuantEvidenceOrigin.OpenVinoProfilingApi,
        ForcedScalarNegative: true);

    private static OptimizationCapabilitySnapshot Snapshot(
        OpenVinoCapabilityPayload payload) =>
        OptimizationCapabilitySnapshot.ForOpenVino(
            "ov-capability-test-1",
            new string('3', 64),
            payload);

    private static OpenVinoCapabilityPayload ExperimentalPayload()
    {
        OpenVinoCapabilityPayload official =
            OpenVinoOptimizationCapabilityProjector.Project(OfficialEvidence());
        return OpenVinoCapabilityPayload.Create(
            official.RuntimeVersion,
            [
                .. official.Admitted,
                OpenVinoAdmittedConfiguration.Create(
                    "OV-TBQ4-SYNTHETIC-PARITY",
                    DeviceRouteId.Cpu,
                    OpenVinoWeightFormat.Int4,
                    OpenVinoKvCacheFormat.TurboQuantTbq4,
                    OpenVinoPerformanceHint.Latency,
                    GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino
                        .OpenVinoCompiledCachePolicy.Disabled,
                    1,
                    4_096,
                    4_096,
                    SupportLevel.Experimental,
                    requiresEvidence: true)
            ]);
    }

    private static OptimizationExecutionPlan Plan(
        OptimizationCapabilitySnapshot snapshot,
        OpenVinoAdmittedConfiguration admission)
    {
        OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
            admission.Weights,
            admission.KvCache,
            admission.Device,
            admission.PerformanceHint,
            admission.CompiledCache,
            admission.Streams);
        bool persistent = admission.Weights is not (OpenVinoWeightFormat.Original or
            OpenVinoWeightFormat.Fp16);
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            configuration,
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                OptimizationAssessment.Excellent,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                4_096,
                2UL * 1024 * 1024,
                8UL * 1024 * 1024,
                6UL * 1024 * 1024,
                persistent ? SourceLength : 0,
                persistent ? SourceLength / 2 : 0,
                requiresPersistentChange: persistent),
            admission.EvidenceId,
            isExperimental: admission.Level == SupportLevel.Experimental);
        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            [candidate], OptimizationPreferenceSelection.Manual(50))!;
        return OptimizationPlanIssuer.Issue(
            selection,
            OpenVinoV2TestPayload.For(
                admission.Weights, admission.KvCache, admission.CompiledCache,
                admission.EvidenceId),
            snapshot,
            OptimizationWorkload.Create(
                "chat",
                512,
                OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(4_096)]),
            OptimizationJourneyBinding.Create(
                "mi-run-1",
                "mi-handoff-1",
                SourceDigest,
                SourceLength,
                "hw-run-1",
                new string('2', 64)),
            modelLayerCount: 1,
            DateTimeOffset.UnixEpoch);
    }
}
