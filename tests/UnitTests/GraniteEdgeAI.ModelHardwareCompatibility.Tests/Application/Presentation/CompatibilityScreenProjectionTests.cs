using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using System.Reflection;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Presentation;

[TestClass]
public sealed class CompatibilityScreenProjectionTests
{
    [TestMethod]
    public void CancelledOpenVinoBaselineRetainsPolicyIdentityWithoutReadingInput()
    {
        MethodInfo baseline = typeof(CompatibilityEngine).GetMethod(
            "EvaluateOpenVinoBaseline", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        // Null deliberately proves that the cancelled path never reads or evaluates model inputs.
        var result = (CompatibilityRunResult)baseline.Invoke(null,
            [null, TimeProvider.System, cancelled.Token])!;
        Assert.AreEqual(CompatibilityRunOutcome.Cancelled, result.Outcome);
        Assert.IsNull(result.Assessment);
        Assert.HasCount(0, result.Findings);
        Assert.HasCount(2, result.PolicyIdentities);
        var screen = CompatibilityScreenModel.From(result);
        Assert.AreEqual(CompatibilityScreenState.Cancelled, screen.State);
        Assert.IsFalse(screen.ContinueEnabled);
        Assert.IsFalse(screen.UseCurrentModelAvailable);
        Assert.IsNull(screen.Optimization);
    }

    [TestMethod]
    [DataRow("exact")]
    [DataRow("other-model")]
    [DataRow("other-runtime")]
    [DataRow("other-score")]
    [DataRow("no-evidence")]
    public void ExactKnownFailedOpenVinoDefaultOffersVerifiedU4WithoutAllowingCurrentModel(string variation)
    {
        string modelSha = variation == "other-model" ? new string('b', 64)
            : "fcfb6ec62a2b823d7d1aebedee193083eaa2f86d9e89722e46102c7a4b90bd27";
        string workerSha = variation == "other-runtime" ? new string('c', 64)
            : "f0089dae967a0b4249238f9bf49db02ba44e111c78b83ebff36778f6d0ddcbe2";
        var versions = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["nncf"] = "3.3.0", ["openvino"] = "2026.3.0", ["openvino-genai"] = "2026.3.0.0",
            ["optimum"] = "2.3.0", ["optimum-intel"] = "2.1.0", ["transformers"] = "5.5.4"
        };
        var build = OpenVinoBuildIdentity.Create(
            VerifiedOpenVinoOptimizationEvidence.RuntimeBuild, VerifiedOpenVinoOptimizationEvidence.GenAiBuild,
            VerifiedOpenVinoOptimizationEvidence.TokenizersBuild, workerSha);
        var baselineExecution = OpenVinoExecutionAuthority.Create("OV-STD-CPU-INT4-DEFAULT-01",
            "openvino.standard.cpu.int4.default.v1", OpenVinoWeightPrecision.FourBit, build, versions, true);
        var alternativeExecution = OpenVinoExecutionAuthority.Create("OV-STD-CPU-INT4-U4-01",
            "openvino.standard.cpu.int4.u4.v1", OpenVinoWeightPrecision.FourBit, build, versions, true);
        OpenVinoAdmittedConfiguration Admission(string id, OpenVinoKvCacheFormat cache) =>
            OpenVinoAdmittedConfiguration.Create(id, DeviceRouteId.Cpu, OpenVinoWeightFormat.Int4, cache,
                OpenVinoPerformanceHint.Latency, OpenVinoCompiledCachePolicy.Disabled, 1, 4096, 4096,
                SupportLevel.DeclaredSupported, false);
        var snapshot = OptimizationCapabilitySnapshot.ForOpenVino("current-int4", Digest,
            OpenVinoCapabilityPayload.Create(build.RuntimeBuild,
                [Admission(baselineExecution.EvidenceId, OpenVinoKvCacheFormat.RouteDefault),
                 Admission(alternativeExecution.EvidenceId, OpenVinoKvCacheFormat.U4)],
                [baselineExecution, alternativeExecution]));
        var facts = InspectedModelFacts.Create(ByteCount.FromBytes(2 * Gibibyte), 40, 2560, 40, 8,
            131072, null, null, 3_402_836_480);
        var workload = OptimizationWorkload.Create("local-chat", 4096, OptimizationAssessment.Acceptable,
            [ContextTokenCount.FromTokens(4096)]);
        var binding = OptimizationJourneyBinding.Create("mi", "handoff", modelSha, 2 * Gibibyte, "hw", Digest);
        var evidence = new OptimizationEvidenceRecord(alternativeExecution.EvidenceId,
            new OptimizationEvidenceKey(OptimizationEvidenceModelFamily.Granite, modelSha, 3_402_836_480,
                OptimizationRoute.OpenVino, CrossRouteCandidateGenerator.OpenVinoEvidencePackageIdentity(alternativeExecution),
                "int4", "int4", "u4", OptimizationEvidenceBackend.OpenVinoCpu, OptimizationEvidenceDeviceClass.Cpu,
                4096, "local-chat-v1", VerifiedOpenVinoOptimizationEvidence.SectorQualityMethodologyIdentity,
                VerifiedOpenVinoOptimizationEvidence.SectorQualityMemoryPerformanceProtocol, "openvino-cpu"),
            new OptimizationQualityScore(variation == "other-score" ? 5m : 5.520833333333333m), true, true, true, true);
        var generated = CrossRouteCandidateGenerator.Generate(snapshot, facts, workload, binding,
            ByteCount.FromBytes(32 * Gibibyte), ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(), HardwareAuthority(),
            new OptimizationEvidenceCatalog(variation == "no-evidence" ? [] : [evidence]), 3_402_836_480);
        Assert.HasCount(variation == "no-evidence" ? 0 : 1, generated.Candidates);
        if (variation == "exact")
        {
            Assert.IsFalse(generated.Candidates.Single().Metrics.RequiresPersistentChange,
                "The exact verified U4 choice changes runtime cache only; the source is already INT4.");
            Assert.AreEqual(0UL, generated.Candidates.Single().Metrics.OutputDiskBytes);
        }
        Assert.IsTrue(generated.Exclusions.Any(item => item.EvidenceId == baselineExecution.EvidenceId));
        var baseline = EvaluatedForConfiguration(CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.Safe, true, OpenVinoRouteConfiguration.Create(OpenVinoWeightFormat.Int4,
                OpenVinoKvCacheFormat.RouteDefault, DeviceRouteId.Cpu, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1));
        var input = CompatibilityOptimizationProjectionInput.Create(generated, snapshot, facts, workload, binding,
            ByteCount.FromBytes(32 * Gibibyte), ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(), HardwareAuthority(), IdentityFor(baseline, binding));
        var screen = CompatibilityScreenModel.From(CompletedWith(baseline), input);
        if (variation != "exact")
        {
            Assert.AreEqual(CompatibilityScreenState.NotEstablished, screen.State);
            Assert.IsFalse(screen.ContinueEnabled);
            Assert.IsFalse(screen.UseCurrentModelAvailable);
            Assert.IsFalse(screen.Findings.Any(item => item.Code == CompatibilityFindingCode.BaselineConfigurationUnavailable));
            return;
        }
        Assert.AreEqual(CompatibilityScreenState.OptimisationRequired, screen.State);
        Assert.AreEqual(CompatibilityFitState.Safe, screen.CurrentSetup!.Fit);
        Assert.IsFalse(screen.UseCurrentModelAvailable);
        Assert.IsTrue(screen.ContinueEnabled);
        Assert.AreEqual(OpenVinoKvCacheFormat.U4, screen.RecommendedSetup!.OpenVinoKvCache);
        Assert.IsTrue(screen.Findings.Any(item => item.Code == CompatibilityFindingCode.BaselineConfigurationUnavailable));
        Assert.IsTrue(CompatibilityScreenModel.RetainPlanningCandidates(screen, input)
            .All(item => item.CanonicalDescriptor != input.Baseline.OptimizationDescriptor));
    }

    private const ulong Gibibyte = 1024UL * 1024 * 1024;
    private const string Digest =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string Commit = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence.PublishedGgufOptimizationEvidence.RuntimeSourceCommit;

    [TestMethod]
    public void AuthorityMinting_IsStructurallyPrivateToTheGenerator()
    {
        Type resultType = typeof(CrossRouteGenerationResult);
        Assert.IsNull(resultType.Assembly.GetType(
            "GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.OptimizationGenerationAuthority"));
        Assert.IsNull(resultType.GetProperty(
            "Authority", BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.NonPublic));
        Type? nestedAuthority = typeof(CrossRouteCandidateGenerator).GetNestedType(
            "GenerationAuthority", BindingFlags.NonPublic);
        Assert.IsNotNull(nestedAuthority);
        Assert.IsTrue(nestedAuthority.IsNestedPrivate);
        ConstructorInfo[] constructors = resultType.GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsFalse(constructors.Any(constructor =>
            constructor.GetParameters().Length > 2
            || constructor.GetParameters().Any(parameter =>
                parameter.ParameterType.Name.Contains(
                    "Authority", StringComparison.Ordinal))));
    }

    private sealed class Gateway : ICompatibilityInputGateway
    {
        public HandoffClaim Claim() => HandoffClaim.Claimed("model-run-1", "hardware-run-1");

        public void Rollback()
        {
        }

        public bool Commit(CompatibilityRunId runId) => true;
    }

    private sealed class Facts(InspectedModelFacts? facts) : IInspectedModelFactsSource
    {
        public ModelFactsResolution Resolve(string modelInspectionRunId) => facts is null
            ? ModelFactsResolution.Unavailable(PortUnavailableReason.HandoffUnavailable)
            : ModelFactsResolution.Established(facts);
    }

    private sealed class Machine(HardwareFacts facts) : IHardwareFactsSource
    {
        public HardwareFactsResolution Resolve(string productHardwareRunId) =>
            HardwareFactsResolution.Established(facts);
    }

    private sealed class MemoryProbe(ulong gibibytes) : IFreshSystemMemoryProbe
    {
        public FreshMemoryReading Probe() =>
            FreshMemoryReading.Established(
                AvailableResources.Create(
                    ByteCount.FromBytes(gibibytes * Gibibyte),
                    ByteCount.FromBytes(8 * Gibibyte),
                    ByteCount.FromBytes(500 * Gibibyte),
                    DateTimeOffset.UtcNow));
    }

    private static InspectedModelFacts ModelFacts() =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte), 32, 4096, 32, 8, 8192, 15, 2);

    private static CompatibilityRunResult RunWith(ulong availableGibibytes)
    {
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

        return CompatibilityRunCoordinator.Execute(
            new CompatibilityRunRequest(CompatibilityContextRequest.ApplicationDefault()),
            CompatibilityRunDependencies.Create(
                new Gateway(),
                new Facts(ModelFacts()),
                new Machine(HardwareFacts.Create(
                    ByteCount.FromBytes(64 * Gibibyte),
                    ByteCount.FromBytes(8 * Gibibyte),
                    ByteCount.FromBytes(500 * Gibibyte),
                    new HashSet<DeviceRouteId> { DeviceRouteId.Cpu },
                    new HashSet<CompatibilityBackend> { CompatibilityBackend.Cpu })),
                new MemoryProbe(availableGibibytes),
                matrix,
                EstimatorPolicy.ProvisionalV1(),
                SafetyPolicy.ProvisionalV1(),
                new HashSet<string>(),
                TrustedSourceAvailability.None(),
                GgufRouteConfiguration.Create(
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None),
                ContextTokenCount.FromTokens(4096),
                TimeProvider.System),
            CancellationToken.None);
    }

    private static CompatibilityRunResult NotEstablishedResult() =>
        CompatibilityRunResult.NotEstablished(
            CompatibilityRunId.New(),
            [CompatibilityFinding.Create(
                CompatibilityFindingCode.ModelFactsUnavailable, FindingSeverity.Blocking)],
            [PolicyIdentity.Create("estimator", "v1", PolicyProvenance.Provisional)],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static EvaluatedCandidate Evaluated(
        CandidatePreparation preparation,
        CompatibilityFitState fit,
        bool isBaseline,
        GgufKvCacheFormat cache)
        => EvaluatedForConfiguration(
            preparation,
            fit,
            isBaseline,
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                cache,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None));

    private static EvaluatedCandidate EvaluatedForConfiguration(
        CandidatePreparation preparation,
        CompatibilityFitState fit,
        bool isBaseline,
        RouteConfiguration configuration,
        ulong safeBudgetBytes = 3 * Gibibyte,
        ulong requiredBytes = 2 * Gibibyte,
        int contextTokens = 4096)
    {
        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            configuration,
            ContextTokenCount.FromTokens(contextTokens),
            preparation,
            isBaseline ? "baseline" : "alternative",
            isExperimental: false,
            isBaseline);

        ResourceEstimate estimate = ResourceEstimate.Established(
            [ResourceComponent.Create(
                ResourceComponentKind.Weights,
                ResourceTarget.SystemMemory,
                ByteCount.FromBytes(2 * Gibibyte),
                new HashSet<LifecyclePhase> { LifecyclePhase.Load })],
            new HashSet<EstimationLimitation>());

        return EvaluatedCandidate.Create(
            candidate,
            estimate,
            ResourcePhaseComposer.Compose(estimate.Components),
            new FitAssessment(
                fit,
                fit is CompatibilityFitState.Safe or CompatibilityFitState.Narrow
                    ? FitLimitingReason.None
                    : FitLimitingReason.InsufficientSystemMemory,
                ByteCount.FromBytes(safeBudgetBytes),
                ByteCount.FromBytes(requiredBytes),
                fit is CompatibilityFitState.Safe or CompatibilityFitState.Narrow
                    ? ByteCount.FromBytes(Gibibyte)
                    : ByteCount.Zero,
                1.5m),
            WeightQuantisation.Q4_K_M,
            EvidenceGrade.Estimated,
            PerformanceIndicator.NotEstablished(),
            ByteCount.Zero);
    }

    private static CompatibilityRunResult CompletedWith(
        EvaluatedCandidate baseline,
        params EvaluatedCandidate[] alternatives)
    {
        IReadOnlyList<EvaluatedCandidate> evaluated = [baseline, .. alternatives];
        IReadOnlyList<CompatibilityModeSelection> selections =
            baseline.Candidate.Configuration is GgufRouteConfiguration gguf
                ? ModeSelector.SelectAll(
                    ModeSelectionRequest.Create(
                        evaluated,
                        new HashSet<string>(),
                        ContextTokenCount.FromTokens(4096),
                        gguf)).Selections
                :
                [
                    CompatibilityModeSelection.NotEstablished(
                        CompatibilityMode.Automatic),
                    CompatibilityModeSelection.NotEstablished(
                        CompatibilityMode.Quality),
                    CompatibilityModeSelection.NotEstablished(
                        CompatibilityMode.Balanced),
                    CompatibilityModeSelection.NotEstablished(
                        CompatibilityMode.Efficiency)
                ];

        CompatibilityAssessment assessment = CompatibilityAssessment.Create(
            evaluated,
            selections,
            baseline.Fingerprint,
            BaselineExclusionReason.None,
            baseline.Fit.State is CompatibilityFitState.Safe
                or CompatibilityFitState.Narrow);

        return CompatibilityRunResult.Completed(
            CompatibilityRunId.New(),
            assessment,
            [],
            [PolicyIdentity.Create("estimator", "v1", PolicyProvenance.Provisional)],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static OptimizationJourneyBinding Binding(string hardwareRun = "hw-run-1") =>
        OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", Digest, 3 * Gibibyte, hardwareRun, Digest);

    private static OptimizationWorkload Workload() =>
        OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);

    private static OptimizationCapabilitySnapshot OptimizationSnapshot(
        string runtimeVersion = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence.PublishedGgufOptimizationEvidence.RuntimeBuildId,
        int maximumContextTokens = 32768)
    {
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-q8-cache",
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.Q8_0,
            GpuOffloadLevel.None,
            512,
            maximumContextTokens,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false);

        return OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap",
            Digest,
            GgufCapabilityPayload.Create(
                runtimeVersion,
                [admitted],
                runtimeAuthority: GgufRuntimeAuthority.Create(
                    runtimeVersion,
                    Commit,
                    [GgufExecutionProfileAuthority.Create(
                        admitted.EvidenceId,
                        EvidenceGrade.Estimated,
                        "profile",
                        flashAttention: false,
                        threadCount: 4,
                        batchSize: 128,
                        maximumGeneratedTokens: 256)])));
    }

    private static OptimizationCapabilitySnapshot TwoCandidateSnapshot()
    {
        GgufAdmittedConfiguration f16 = GgufAdmittedConfiguration.Create(
            "gguf-f16-cache", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, requiresEvidence: false);
        GgufAdmittedConfiguration q8 = GgufAdmittedConfiguration.Create(
            "gguf-q8-cache", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.Q8_0,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, requiresEvidence: false);
        GgufExecutionProfileAuthority Profile(string evidenceId) =>
            GgufExecutionProfileAuthority.Create(
                evidenceId, EvidenceGrade.Estimated, $"profile-{evidenceId}",
                flashAttention: false, threadCount: 4, batchSize: 128,
                maximumGeneratedTokens: 256);

        return OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create(
                GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence.PublishedGgufOptimizationEvidence.RuntimeBuildId, [f16, q8],
                runtimeAuthority: GgufRuntimeAuthority.Create(
                    GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence.PublishedGgufOptimizationEvidence.RuntimeBuildId, Commit,
                    [Profile(f16.EvidenceId), Profile(q8.EvidenceId)])));
    }

    private static OptimizationCapabilitySnapshot OpenVinoSnapshot(
        DeviceRouteId device = DeviceRouteId.Cpu)
    {
        OpenVinoAdmittedConfiguration admitted = OpenVinoAdmittedConfiguration.Create(
            "ov-int4",
            device,
            OpenVinoWeightFormat.Int4,
            OpenVinoKvCacheFormat.U8,
            OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled,
            1,
            512,
            32768,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false);

        return OptimizationCapabilitySnapshot.ForOpenVino(
            "ov-cap",
            Digest,
            OpenVinoCapabilityPayload.Create(
                SyntheticQualityEvidence.OpenVinoBuild,
                [admitted],
                [OpenVinoExecutionAuthority.Create(
                    "ov-int4",
                    "ov-int4",
                    OpenVinoWeightPrecision.Fp16,
                    OpenVinoBuildIdentity.Create(
                        SyntheticQualityEvidence.OpenVinoBuild, SyntheticQualityEvidence.OpenVinoBuild, SyntheticQualityEvidence.OpenVinoBuild, Digest),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["openvino"] = SyntheticQualityEvidence.OpenVinoBuild
                    },
                    compiledCacheIsDisposable: true,
                    turboQuantBuild: null)]));
    }

    private static CrossRouteGenerationResult AdmittedAlternative(
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding) =>
        SyntheticQualityEvidence.Generate(
            snapshot,
            ModelFacts(),
            workload,
            binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority());

    private static OptimizationHardwareAuthority HardwareAuthority(
        DateTimeOffset? evaluatedAtUtc = null) =>
        OptimizationHardwareAuthority.Create(
            Digest,
            [DeviceRouteId.Cpu, DeviceRouteId.IntelIntegratedGpu,
             DeviceRouteId.IntelDiscreteGpu, DeviceRouteId.IntelNpu],
            [CompatibilityBackend.Cpu, CompatibilityBackend.IntelSycl,
             CompatibilityBackend.IntelVulkan, CompatibilityBackend.OpenVinoCpu,
             CompatibilityBackend.OpenVinoGpu, CompatibilityBackend.OpenVinoNpu],
            ByteCount.FromBytes(64 * Gibibyte),
            DateTimeOffset.UnixEpoch,
            evaluatedAtUtc ?? DateTimeOffset.UnixEpoch,
            "test-freshness-v1");

    private static CompatibilityBaselineIdentity BaselineIdentity(
        CompatibilityRunResult result,
        OptimizationJourneyBinding binding)
    {
        EvaluatedCandidate baseline = result.Assessment!.EvaluatedCandidates
            .Single(candidate => candidate.Candidate.IsBaseline);

        return IdentityFor(baseline, binding);
    }

    private static CompatibilityBaselineIdentity IdentityFor(
        EvaluatedCandidate baseline,
        OptimizationJourneyBinding binding) =>
        baseline.Preparation == CandidatePreparation.None
            ? CompatibilityBaselineIdentity.ForLegacyNone(baseline, binding)
            : CompatibilityBaselineIdentity.Create(
                CandidatePreparation.RuntimeProfileOnly,
                baseline.Candidate.Configuration,
                baseline.Context,
                binding);

    private static CompatibilityScreenModel ProjectWith(
        CompatibilityRunResult result,
        CrossRouteGenerationResult generated,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding generatedBinding,
        OptimizationJourneyBinding? currentBinding = null,
        ulong safeBudgetBytes = 32 * Gibibyte,
        ulong availableDiskBytes = 500 * Gibibyte,
        EstimatorPolicy? policy = null) =>
        CompatibilityScreenModel.From(
            result,
            CompatibilityOptimizationProjectionInput.Create(
                generated,
                snapshot,
                ModelFacts(),
                workload,
                currentBinding ?? generatedBinding,
                ByteCount.FromBytes(safeBudgetBytes),
                ByteCount.FromBytes(availableDiskBytes),
                policy ?? EstimatorPolicy.ProvisionalV1(),
                new HashSet<string>(),
                HardwareAuthority(),
                BaselineIdentity(result, generatedBinding)));

    private static CompatibilityOptimizationProjectionInput ProjectionInput(
        CrossRouteGenerationResult generated,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        CompatibilityBaselineIdentity identity) =>
        CompatibilityOptimizationProjectionInput.Create(
            generated, snapshot, ModelFacts(), workload, binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
            HardwareAuthority(), identity);

    [TestMethod]
    public void AGenerousMachine_ShowsEstimatedCompatible()
    {
        Assert.AreEqual(
            nameof(CompatibilityScreenState.EstimatedCompatible),
            CompatibilityScreenModel.From(RunWith(64)).State.ToString());
    }

    [TestMethod]
    public void RealGgufCurrentProfileQualityAbsenceDoesNotContradictItsMemoryVerdict()
    {
        (OptimizationCapabilitySnapshot snapshot, OptimizationWorkload workload,
            OptimizationJourneyBinding binding, CrossRouteGenerationResult generated) =
            GenerateCurrentGgufProfile();
        GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);

        Assert.AreEqual(0, generated.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.CurrentModelQualityEvidenceUnavailable,
            generated.Exclusions.Single().Reason);

        CompatibilityRunResult fits = CompletedWith(EvaluatedForConfiguration(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.Safe,
            isBaseline: true,
            configuration,
            contextTokens: 32768));
        CompatibilityRunResult overBudget = CompletedWith(EvaluatedForConfiguration(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            configuration,
            contextTokens: 32768));

        Assert.AreEqual(
            CompatibilityScreenState.EstimatedCompatible,
            ProjectWith(fits, generated, snapshot, workload, binding).State);
        Assert.AreEqual(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            ProjectWith(overBudget, generated, snapshot, workload, binding).State);
    }

    [TestMethod]
    [DataRow("exact", CompatibilityFitState.Safe, CompatibilityScreenState.EstimatedCompatible)]
    [DataRow("exact", CompatibilityFitState.Narrow, CompatibilityScreenState.EstimatedCompatible)]
    [DataRow("exact", CompatibilityFitState.DoesNotFit, CompatibilityScreenState.NoEstimatedSafeConfiguration)]
    [DataRow("other-sha", CompatibilityFitState.Safe, CompatibilityScreenState.NotEstablished)]
    [DataRow("other-length", CompatibilityFitState.Safe, CompatibilityScreenState.NotEstablished)]
    [DataRow("other-shape", CompatibilityFitState.Safe, CompatibilityScreenState.NotEstablished)]
    public void PinnedQ3CurrentMemoryVerdictSurvivesMissingOptimisationQualityEvidence(
        string variation, CompatibilityFitState fit, CompatibilityScreenState expected)
    {
        var (snapshot, workload, _, _) = GenerateCurrentGgufProfile();
        ulong length = variation == "other-length" ? 1_555_472_033UL : 1_555_472_032UL;
        var binding = OptimizationJourneyBinding.Create("mi", "handoff",
            variation == "other-sha" ? new string('a', 64)
                : "bb684177d546a2c6d3aec9cc591fc8fa75d9c16a27e798cbb2400fe52d9b7e29",
            length, "hw", Digest);
        var facts = InspectedModelFacts.Create(ByteCount.FromBytes(length),
            40, variation == "other-shape" ? 2560 : 2048, 32, 8, 1_048_576, 12, 2);
        var generated = CrossRouteCandidateGenerator.Generate(snapshot, facts, workload, binding,
            ByteCount.FromBytes(32 * Gibibyte), ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(), HardwareAuthority(),
            PublishedGgufOptimizationEvidence.CreateCatalog(), inspectedParameterCount: null);
        Assert.HasCount(0, generated.Candidates);
        Assert.AreEqual(OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
            generated.Exclusions.Single().Reason);
        var configuration = GgufRouteConfiguration.Create(GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16, CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None);
        var baseline = EvaluatedForConfiguration(CandidatePreparation.None, fit, true,
            configuration, contextTokens: 32768);
        var input = CompatibilityOptimizationProjectionInput.Create(generated, snapshot, facts,
            workload, binding, ByteCount.FromBytes(32 * Gibibyte), ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(), HardwareAuthority(),
            IdentityFor(baseline, binding));
        var screen = CompatibilityScreenModel.From(CompletedWith(baseline), input);
        Assert.AreEqual(expected, screen.State);
        Assert.IsNull(screen.Optimization);
        Assert.HasCount(0, generated.Candidates,
            "Preserving a current-model memory verdict must not admit optimisation candidates.");
    }

    private static (OptimizationCapabilitySnapshot Snapshot,
        OptimizationWorkload Workload,
        OptimizationJourneyBinding Binding,
        CrossRouteGenerationResult Generated) GenerateCurrentGgufProfile()
    {
        const string modelSha =
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29";
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-current-cpu",
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            GpuOffloadLevel.None,
            512,
            32768,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-current",
                Digest,
                GgufCapabilityPayload.Create(
                    PublishedGgufOptimizationEvidence.RuntimeBuildId,
                    [admitted],
                    runtimeAuthority: GgufRuntimeAuthority.Create(
                        PublishedGgufOptimizationEvidence.RuntimeBuildId,
                        PublishedGgufOptimizationEvidence.RuntimeSourceCommit,
                        [GgufExecutionProfileAuthority.Create(
                            admitted.EvidenceId,
                            EvidenceGrade.Estimated,
                            "cpu-imported",
                            flashAttention: false,
                            threadCount: 4,
                            batchSize: 128,
                            maximumGeneratedTokens: 512)])));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat",
            512,
            OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(32768)]);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", modelSha, 3 * Gibibyte,
            "hw-run-1", Digest);
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
            snapshot,
            ModelFacts(),
            workload,
            binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority(),
            PublishedGgufOptimizationEvidence.CreateCatalog(),
            inspectedParameterCount: 3_000_000_000);
        return (snapshot, workload, binding, generated);
    }

    [TestMethod]
    public void BaselineFailsButAnAdmittedAlternativeFits_ShowsOptimisationRequired()
    {
        // Regression caught: deciding from "anything fits" labels a model that
        // cannot run as imported as already compatible and skips quantisation
        EvaluatedCandidate baseline = Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16);
        EvaluatedCandidate alternative = Evaluated(
            CandidatePreparation.WeightConversionRequired,
            CompatibilityFitState.Safe,
            isBaseline: false,
            GgufKvCacheFormat.Q8_0);

        Assert.AreEqual(
            CompatibilityScreenState.OptimisationRequired,
            CompatibilityScreenModel.From(CompletedWith(baseline, alternative)).State);
    }

    [TestMethod]
    public void DecisionTable_BaselineFits_RemainsEstimatedCompatible()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.Narrow,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();

        CompatibilityScreenModel model = ProjectWith(
            result,
            AdmittedAlternative(snapshot, workload, binding),
            snapshot,
            workload,
            binding);

        Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible, model.State);
        Assert.AreEqual(CompatibilityFitState.Narrow, model.Setup!.Fit);
    }

    [TestMethod]
    public void DecisionTable_BaselineFailsAndAdmittedAlternativeFits_ProjectsModes()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();

        CompatibilityScreenModel model = ProjectWith(
            result,
            AdmittedAlternative(snapshot, workload, binding),
            snapshot,
            workload,
            binding);

        Assert.AreEqual(CompatibilityScreenState.OptimisationRequired, model.State);
        Assert.IsNotNull(model.Optimization);
        Assert.AreEqual(6, model.Optimization.Modes.Count);
        Assert.IsTrue(model.Optimization.ExactSafeModes.Count > 0);
        Assert.IsFalse(model.Optimization.HasAdditionalExactSafeModes);
        Assert.HasCount(1, model.Optimization.SafeSliderModes);
        Assert.AreEqual(0, model.Optimization.SafeSliderSelectedIndex);
        Assert.IsTrue(model.Optimization.ExactSafeModes.All(item =>
            item.CandidateIdentity.Length == 64
            && item.Mode.PredictedPeakBytes <= item.Mode.SafeBudgetBytes
            && item.Mode.GgufKvCache == GgufKvCacheFormat.Q8_0));
        Assert.AreEqual(model.Optimization.ExactSafeModes.Count,
            model.Optimization.ExactSafeModes.Select(item => item.CandidateIdentity)
                .Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(
            CompatibilityOptimizationLabelCode.Automatic,
            model.Optimization.RecommendedLabelCode);
        Assert.IsNull(model.Optimization.RecommendedSliderValue);
        Assert.IsFalse(model.Optimization.RequiresPersistentArtifact);
        Assert.IsFalse(model.Optimization.RequiresRequantisationAcknowledgement);
        CollectionAssert.AreEqual(
            new[]
            {
                CompatibilityOptimizationLabelCode.Automatic,
                CompatibilityOptimizationLabelCode.MaximumEfficiency,
                CompatibilityOptimizationLabelCode.Efficient,
                CompatibilityOptimizationLabelCode.Balanced,
                CompatibilityOptimizationLabelCode.HighCapability,
                CompatibilityOptimizationLabelCode.MaximumCapability
            },
            model.Optimization.Modes.Select(mode => mode.LabelCode).ToArray());
        Assert.IsTrue(model.Optimization.Modes.All(mode =>
            mode.Route == OptimizationRoute.Gguf
            && mode.GgufKvCache == GgufKvCacheFormat.Q8_0
            && mode.OpenVinoKvCache is null));
    }

    [TestMethod]
    public void OptimisationRequired_SeparatesCurrentSetupFromRecommendedSetup()
    {
        EvaluatedCandidate baseline = Evaluated(
            CandidatePreparation.None,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16);
        EvaluatedCandidate oldAlternative = Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.Safe,
            isBaseline: false,
            GgufKvCacheFormat.Q8_0);
        CompatibilityRunResult result = CompletedWith(baseline, oldAlternative);
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();

        CompatibilityScreenModel model = ProjectWith(
            result,
            AdmittedAlternative(snapshot, workload, binding),
            snapshot,
            workload,
            binding);

        Assert.IsNull(model.Setup);
        Assert.AreEqual(CompatibilityFitState.DoesNotFit, model.CurrentSetup!.Fit);
        Assert.AreEqual(GgufKvCacheFormat.F16, model.CurrentSetup.GgufKvCache);
        Assert.IsNull(model.CurrentSetup.OpenVinoKvCache);
        Assert.AreEqual(
            CompatibilityOptimizationLabelCode.Automatic,
            model.RecommendedSetup!.LabelCode);
        Assert.AreEqual(GgufKvCacheFormat.Q8_0, model.RecommendedSetup.GgufKvCache);
    }

    [TestMethod]
    public void ExactProjectionPreservesSafeChoicesOmittedByRecommendationBands()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly, CompatibilityFitState.DoesNotFit,
            isBaseline: true, GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = OptimizationWorkload.Create("chat", 512,
            OptimizationAssessment.Poor,
            [.. new[] { 512, 1024, 2048, 4096, 8192, 16384, 24576, 32768 }
                .Select(ContextTokenCount.FromTokens)]);
        OptimizationJourneyBinding binding = Binding();
        CompatibilityScreenModel model = ProjectWith(result,
            AdmittedAlternative(snapshot, workload, binding), snapshot, workload, binding);
        Assert.IsNotNull(model.Optimization);
        Assert.IsTrue(model.Optimization.HasAdditionalExactSafeModes);
        Assert.IsTrue(model.Optimization.ExactSafeModes.Count > model.Optimization.Modes.Count);
        CollectionAssert.AreEqual(
            model.Optimization.ExactSafeModes.OrderBy(item => item.Mode.IsExperimental)
                .ThenBy(item => item.CandidateIdentity, StringComparer.Ordinal)
                .Select(item => item.CandidateIdentity).ToArray(),
            model.Optimization.ExactSafeModes.Select(item => item.CandidateIdentity).ToArray());
        Assert.IsTrue(model.Optimization.ExactSafeModes.All(item =>
            item.Mode.PredictedPeakBytes <= item.Mode.SafeBudgetBytes));
    }

    [TestMethod]
    public void ReleasedSliderRetainsAllSafeFormatsInIncreasingMemoryOrder()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly, CompatibilityFitState.DoesNotFit,
            isBaseline: true, GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = OptimizationWorkload.Create("chat", 512,
            OptimizationAssessment.Poor,
            [.. new[] { 512, 1024, 2048, 4096, 8192, 16384, 24576, 32768 }
                .Select(ContextTokenCount.FromTokens)]);
        OptimizationJourneyBinding binding = Binding();
        CompatibilityScreenModel model = ProjectWith(result,
            AdmittedAlternative(snapshot, workload, binding), snapshot, workload, binding);
        Assert.IsNotNull(model.Optimization);
        Assert.AreEqual(model.Optimization.ExactSafeModes.Count, model.Optimization.SafeSliderModes.Count,
            "A format slider must not discard safe setups through recommendation-band reduction.");
        Assert.IsTrue(model.Optimization.SafeSliderModes.Count > model.Optimization.Modes.Count);
        Assert.IsTrue(model.Optimization.SafeSliderModes.All(item => !item.Mode.IsExperimental));
        var memory = model.Optimization.SafeSliderModes.Select(item => item.Mode.PredictedPeakBytes).ToArray();
        CollectionAssert.AreEqual(memory.Order().ToArray(), memory);
        Assert.IsTrue(model.Optimization.SafeSliderSelectedIndex >= 0
            && model.Optimization.SafeSliderSelectedIndex < model.Optimization.SafeSliderModes.Count);
    }

    [TestMethod]
    public void OpenVinoAlternative_IsFlattenedWithoutProviderPayloads()
    {
        OpenVinoRouteConfiguration imported = OpenVinoRouteConfiguration.Create(
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.U8,
            DeviceRouteId.Cpu,
            OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled,
            1);
        CompatibilityRunResult result = CompletedWith(EvaluatedForConfiguration(
            CandidatePreparation.None,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            imported));
        OptimizationCapabilitySnapshot snapshot = OpenVinoSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();

        CompatibilityScreenModel model = ProjectWith(
            result,
            AdmittedAlternative(snapshot, workload, binding),
            snapshot,
            workload,
            binding);
        CompatibilityOptimizationView view = model.Optimization!;

        Assert.IsTrue(view.RequiresPersistentArtifact);
        Assert.HasCount(1, view.SafeSliderModes);
        Assert.AreEqual(0, view.SafeSliderSelectedIndex);
        Assert.AreEqual(OpenVinoWeightFormat.Int4, view.SafeSliderModes[0].Mode.OpenVinoWeights);
        Assert.IsTrue(view.ExactSafeModes.Count > 0);
        Assert.IsTrue(view.ExactSafeModes.All(item =>
            item.Mode.Route == OptimizationRoute.OpenVino
            && item.Mode.OpenVinoWeights == OpenVinoWeightFormat.Int4));
        Assert.AreEqual(OpenVinoKvCacheFormat.U8,
            model.CurrentSetup!.OpenVinoKvCache);
        Assert.IsNull(model.CurrentSetup.GgufKvCache);
        Assert.IsTrue(view.Modes.All(mode =>
            mode.Route == OptimizationRoute.OpenVino
            && mode.OpenVinoWeights == OpenVinoWeightFormat.Int4
            && mode.GgufWeights is null));
    }

    [TestMethod]
    public void DecisionTable_BaselineFailsAndNoAdmittedAlternative_IsNoSafeConfiguration()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CrossRouteGenerationResult generated = SyntheticQualityEvidence.Generate(
            snapshot, ModelFacts(), workload, binding,
            ByteCount.FromBytes(1), ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
            HardwareAuthority());

        CompatibilityScreenModel model = ProjectWith(
            result, generated, snapshot, workload, binding,
            safeBudgetBytes: 1);

        Assert.AreEqual(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            model.State);
        Assert.IsNull(model.Optimization);
    }

    [TestMethod]
    public void DecisionTable_UnknownAlternativeEvidence_IsNotEstablished()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        EstimatorPolicy absent = EstimatorPolicy.Absent();
        CrossRouteGenerationResult generated = SyntheticQualityEvidence.Generate(
            snapshot, ModelFacts(), workload, binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte), absent,
            new HashSet<string>(), HardwareAuthority());

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            ProjectWith(
                result, generated, snapshot, workload, binding,
                policy: absent).State);
    }

    [TestMethod]
    public void BaselineFit_DoesNotHideUnknownAlternativeEvidence()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.Safe,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        EstimatorPolicy absent = EstimatorPolicy.Absent();
        CrossRouteGenerationResult generated = SyntheticQualityEvidence.Generate(
            snapshot, ModelFacts(), workload, binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte), absent,
            new HashSet<string>(), HardwareAuthority());

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            ProjectWith(
                result, generated, snapshot, workload, binding,
                policy: absent).State);
    }

    [TestMethod]
    public void CandidateFromStaleHardwareRun_FailsClosed()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding generatedBinding = Binding("hw-run-old");
        OptimizationJourneyBinding currentBinding = Binding("hw-run-current");

        CompatibilityScreenModel model = ProjectWith(
            result,
            AdmittedAlternative(snapshot, workload, generatedBinding),
            snapshot,
            workload,
            generatedBinding,
            currentBinding);

        Assert.AreEqual(CompatibilityScreenState.NotEstablished, model.State);
        Assert.IsNull(model.Optimization);
    }

    [TestMethod]
    [DataRow("model")]
    [DataRow("budget")]
    [DataRow("disk")]
    [DataRow("policy")]
    [DataRow("opt-in")]
    [DataRow("payload")]
    [DataRow("admission")]
    public void EveryGenerationInput_IsBoundAgainstReplay(string mutation)
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot generatedSnapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CrossRouteGenerationResult generated = AdmittedAlternative(
            generatedSnapshot, workload, binding);

        InspectedModelFacts facts = mutation == "model"
            ? InspectedModelFacts.Create(
                ByteCount.FromBytes(4 * Gibibyte), 32, 4096, 32, 8, 8192, 15, 2)
            : ModelFacts();
        ByteCount budget = ByteCount.FromBytes(
            (mutation == "budget" ? 31UL : 32UL) * Gibibyte);
        ByteCount disk = ByteCount.FromBytes(
            (mutation == "disk" ? 499UL : 500UL) * Gibibyte);
        EstimatorPolicy policy = mutation == "policy"
            ? EstimatorPolicy.Absent()
            : EstimatorPolicy.ProvisionalV1();
        IReadOnlySet<string> optedIn = mutation == "opt-in"
            ? new HashSet<string> { "gguf-q8-cache" }
            : new HashSet<string>();
        OptimizationCapabilitySnapshot currentSnapshot = mutation == "payload"
            ? OptimizationSnapshot("b4322")
            : mutation == "admission"
                ? OptimizationSnapshot(maximumContextTokens: 16384)
            : generatedSnapshot;
        CompatibilityOptimizationProjectionInput input =
            CompatibilityOptimizationProjectionInput.Create(
                generated, currentSnapshot, facts, workload, binding, budget,
                disk, policy, optedIn, HardwareAuthority(),
                BaselineIdentity(result, binding));

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            CompatibilityScreenModel.From(result, input).State,
            mutation);
    }

    [TestMethod]
    public void AllExcludedResult_CannotBeReplayedAgainstChangedDiskAuthority()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CrossRouteGenerationResult generated = SyntheticQualityEvidence.Generate(
            snapshot, ModelFacts(), workload, binding,
            ByteCount.FromBytes(1), ByteCount.FromBytes(1),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
            HardwareAuthority());
        CompatibilityOptimizationProjectionInput replay =
            CompatibilityOptimizationProjectionInput.Create(
                generated, snapshot, ModelFacts(), workload, binding,
                ByteCount.FromBytes(1), ByteCount.FromBytes(2),
                EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
                HardwareAuthority(),
                BaselineIdentity(result, binding));

        Assert.AreEqual(0, generated.Candidates.Count);
        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            CompatibilityScreenModel.From(result, replay).State);
    }

    [TestMethod]
    public void GeneratedFrontier_CannotBeReplayedAgainstAnotherEvaluationInstant()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.None,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        OptimizationHardwareAuthority generatedHardware = HardwareAuthority();
        CrossRouteGenerationResult generated = SyntheticQualityEvidence.Generate(
            snapshot, ModelFacts(), workload, binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
            generatedHardware);
        CompatibilityOptimizationProjectionInput replay =
            CompatibilityOptimizationProjectionInput.Create(
                generated, snapshot, ModelFacts(), workload, binding,
                ByteCount.FromBytes(32 * Gibibyte),
                ByteCount.FromBytes(500 * Gibibyte),
                EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
                HardwareAuthority(DateTimeOffset.UnixEpoch.AddSeconds(1)),
                BaselineIdentity(result, binding));

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            CompatibilityScreenModel.From(result, replay).State);
    }

    [TestMethod]
    public void DuplicateAdmittedCandidate_FailsClosedRatherThanChoosingByOrder()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        OptimizationCandidate candidate = AdmittedAlternative(
            snapshot, workload, binding).Candidates.Single();

        CrossRouteGenerationResult duplicated = new(
            [candidate, candidate], []);

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            ProjectWith(result, duplicated, snapshot, workload, binding).State);
    }

    [TestMethod]
    public void BaselineIdentity_RejectsAnythingOtherThanRuntimeProfileOnly()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        EvaluatedCandidate baseline = result.Assessment!.EvaluatedCandidates.Single();

        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityBaselineIdentity.Create(
                CandidatePreparation.WeightConversionRequired,
                baseline.Candidate.Configuration,
                baseline.Context,
                Binding()));
    }

    [TestMethod]
    public void RuntimeBaselineIdentity_RejectsAConversionCandidateEvenWhenDescriptorsMatch()
    {
        EvaluatedCandidate converted = Evaluated(
            CandidatePreparation.WeightConversionRequired,
            CompatibilityFitState.Safe,
            isBaseline: true,
            GgufKvCacheFormat.F16);
        CompatibilityBaselineIdentity identity = CompatibilityBaselineIdentity.Create(
            CandidatePreparation.RuntimeProfileOnly,
            converted.Candidate.Configuration,
            converted.Context,
            Binding());

        Assert.IsFalse(identity.Matches(converted, Binding()));
    }

    [TestMethod]
    public void LegacyNoneBaseline_RequiresItsExactPublishedFingerprint()
    {
        EvaluatedCandidate legacy = Evaluated(
            CandidatePreparation.None,
            CompatibilityFitState.Safe,
            isBaseline: true,
            GgufKvCacheFormat.F16);
        EvaluatedCandidate converted = Evaluated(
            CandidatePreparation.WeightConversionRequired,
            CompatibilityFitState.Safe,
            isBaseline: true,
            GgufKvCacheFormat.F16);
        CompatibilityBaselineIdentity identity =
            CompatibilityBaselineIdentity.ForLegacyNone(legacy, Binding());

        Assert.IsTrue(identity.Matches(legacy, Binding()));
        Assert.IsFalse(identity.Matches(converted, Binding()));
    }

    [TestMethod]
    public void SafeBaselineExcludedByTheCurrentFrontier_FailsClosed()
    {
        EvaluatedCandidate baseline = Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.Safe,
            isBaseline: true,
            GgufKvCacheFormat.F16);
        CompatibilityRunResult result = CompletedWith(baseline);
        OptimizationCapabilitySnapshot snapshot = TwoCandidateSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CrossRouteGenerationResult generated = SyntheticQualityEvidence.Generate(
            snapshot, ModelFacts(), workload, binding,
            ByteCount.FromBytes(1), ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
            HardwareAuthority());

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            ProjectWith(
                result, generated, snapshot, workload, binding,
                safeBudgetBytes: 1).State);
    }

    [TestMethod]
    public void UnstampedNoFitExclusions_CannotBecomeANoSafeConclusion()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.None,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CrossRouteGenerationResult unstamped = new(
            [],
            [new OptimizationExclusion(
                "ov-int4",
                "weights=Int4|ctx=4096",
                OptimizationExclusionReason.ExceedsSafeMemoryBudget)]);

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            ProjectWith(result, unstamped, snapshot, workload, binding).State);
    }

    [TestMethod]
    public void SyntheticEmptyOutput_CannotReceiveGeneratorAuthority()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CrossRouteGenerationResult fabricated = new([], []);

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            ProjectWith(result, fabricated, snapshot, workload, binding).State);
    }

    [TestMethod]
    public void CopyingAGenuineResult_DoesNotTransferGeneratorAuthority()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CrossRouteGenerationResult genuine = AdmittedAlternative(
            snapshot, workload, binding);
        CrossRouteGenerationResult copied = genuine with { };

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            ProjectWith(result, copied, snapshot, workload, binding).State);
    }

    [TestMethod]
    public void ValidOutputStamp_CannotAuthorizeAMutatedExclusion()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CrossRouteGenerationResult genuine = SyntheticQualityEvidence.Generate(
            snapshot, ModelFacts(), workload, binding,
            ByteCount.FromBytes(1), ByteCount.FromBytes(1),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
            HardwareAuthority());
        OptimizationExclusion exclusion = genuine.Exclusions.Single();
        CrossRouteGenerationResult mutated = new(
            genuine.Candidates,
            [exclusion with
            {
                Reason = OptimizationExclusionReason.InsufficientDiskSpace
            }]);

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            ProjectWith(result, mutated, snapshot, workload, binding).State);
    }

    [TestMethod]
    public void ValidOutputStamp_CannotAuthorizeCandidateReordering()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = TwoCandidateSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CrossRouteGenerationResult genuine = SyntheticQualityEvidence.Generate(
            snapshot, ModelFacts(), workload, binding,
            ByteCount.FromBytes(64 * Gibibyte),
            ByteCount.FromBytes(64 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
            HardwareAuthority());
        Assert.AreEqual(2, genuine.Candidates.Count);
        CrossRouteGenerationResult reordered = new(
            genuine.Candidates.Reverse().ToArray(),
            genuine.Exclusions);

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            ProjectWith(result, reordered, snapshot, workload, binding).State);
    }

    [TestMethod]
    public void BaselineFitDisagreementAcrossCurrentAuthorities_FailsClosed()
    {
        OpenVinoRouteConfiguration baselineConfiguration =
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int4,
                OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                1);
        EvaluatedCandidate baseline = EvaluatedForConfiguration(
            CandidatePreparation.None,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            baselineConfiguration);
        CompatibilityRunResult result = CompletedWith(baseline);
        OptimizationCapabilitySnapshot snapshot = OpenVinoSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            ProjectWith(
                result,
                AdmittedAlternative(snapshot, workload, binding),
                snapshot,
                workload,
                binding).State);
    }

    [TestMethod]
    public void LegacyNoneBaseline_IsMappedSeparatelyToRuntimeProfileAuthority()
    {
        EvaluatedCandidate legacyBaseline = Evaluated(
            CandidatePreparation.None,
            CompatibilityFitState.Safe,
            isBaseline: true,
            GgufKvCacheFormat.F16);
        CompatibilityRunResult result = CompletedWith(legacyBaseline);
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CompatibilityBaselineIdentity identity = IdentityFor(legacyBaseline, binding);
        CompatibilityOptimizationProjectionInput input =
            ProjectionInput(
                AdmittedAlternative(snapshot, workload, binding),
                snapshot, workload, binding, identity);

        Assert.AreEqual(CandidatePreparation.None, legacyBaseline.Preparation);
        Assert.AreEqual(CandidatePreparation.None, identity.Preparation);
        Assert.AreEqual(legacyBaseline.Fingerprint, identity.Fingerprint);
        Assert.AreEqual(
            CompatibilityScreenState.EstimatedCompatible,
            CompatibilityScreenModel.From(result, input).State);
    }

    [TestMethod]
    public void ZeroMatchingBaselineIdentities_FailClosed()
    {
        EvaluatedCandidate baseline = Evaluated(
            CandidatePreparation.None,
            CompatibilityFitState.Safe,
            isBaseline: true,
            GgufKvCacheFormat.F16);
        EvaluatedCandidate different = Evaluated(
            CandidatePreparation.None,
            CompatibilityFitState.Safe,
            isBaseline: true,
            GgufKvCacheFormat.Q8_0);
        CompatibilityRunResult result = CompletedWith(baseline);
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CompatibilityOptimizationProjectionInput input =
            ProjectionInput(
                AdmittedAlternative(snapshot, workload, binding),
                snapshot, workload, binding, IdentityFor(different, binding));

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            CompatibilityScreenModel.From(result, input).State);
    }

    [TestMethod]
    public void MultipleMatchingBaselineIdentities_FailClosed()
    {
        EvaluatedCandidate first = Evaluated(
            CandidatePreparation.None,
            CompatibilityFitState.Safe,
            isBaseline: true,
            GgufKvCacheFormat.F16);
        EvaluatedCandidate duplicate = Evaluated(
            CandidatePreparation.None,
            CompatibilityFitState.Safe,
            isBaseline: true,
            GgufKvCacheFormat.F16);
        CompatibilityRunResult result = CompletedWith(first, duplicate);
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CompatibilityOptimizationProjectionInput input =
            ProjectionInput(
                AdmittedAlternative(snapshot, workload, binding),
                snapshot, workload, binding, IdentityFor(first, binding));

        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            CompatibilityScreenModel.From(result, input).State);
    }

    [TestMethod]
    public void OptimizationProjection_OwnsImmutableModeViewsAndNoFreeformAuthorityData()
    {
        CompatibilityRunResult result = CompletedWith(Evaluated(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            GgufKvCacheFormat.F16));
        OptimizationCapabilitySnapshot snapshot = OptimizationSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CompatibilityOptimizationView optimization = ProjectWith(
            result,
            AdmittedAlternative(snapshot, workload, binding),
            snapshot,
            workload,
            binding).Optimization!;

        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<CompatibilityOptimizationModeView>)optimization.Modes).Clear());
        Assert.AreEqual(
            0,
            typeof(CompatibilityOptimizationView).GetProperties()
                .Count(property => property.PropertyType == typeof(string)));
        Assert.AreEqual(
            0,
            typeof(CompatibilityOptimizationModeView).GetProperties()
                .Count(property => property.PropertyType == typeof(string)));
    }

    [TestMethod]
    public void PresentationFactories_CopyModeCollectionsForDownstreamFixtures()
    {
        List<CompatibilityOptimizationModeView> modes =
        [
            PresentationMode(CompatibilityOptimizationLabelCode.Automatic, null),
            PresentationMode(CompatibilityOptimizationLabelCode.MaximumEfficiency, 10),
            PresentationMode(CompatibilityOptimizationLabelCode.Efficient, 30),
            PresentationMode(CompatibilityOptimizationLabelCode.Balanced, 50),
            PresentationMode(CompatibilityOptimizationLabelCode.HighCapability, 70),
            PresentationMode(CompatibilityOptimizationLabelCode.MaximumCapability, 90)
        ];

        CompatibilityOptimizationView view =
            CompatibilityOptimizationView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                recommendedSliderValue: null,
                modes,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                OptimizationQualityNotice.None);
        modes.Clear();

        Assert.AreEqual(6, view.Modes.Count);
    }

    [TestMethod]
    public void PresentationFactory_RejectsAggregateFlagsThatDisagreeWithRecommendation()
    {
        CompatibilityOptimizationModeView mode =
            CompatibilityOptimizationModeView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                sliderValue: null,
                OptimizationRoute.Gguf,
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.Q8_0,
                null,
                null,
                DeviceRouteId.Cpu,
                OptimizationAssessment.Good,
                4096,
                2 * Gibibyte,
                3 * Gibibyte,
                Gibibyte,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                OptimizationQualityNotice.None,
                isExperimental: false,
                sharedWithAdjacentBand: false);

        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityOptimizationView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                null,
                [mode],
                requiresPersistentArtifact: true,
                requiresRequantisationAcknowledgement: false,
                OptimizationQualityNotice.None));
    }

    [TestMethod]
    public void OptimizationFixture_RejectsAnythingOtherThanTheFrozenSixModes()
    {
        CompatibilityOptimizationModeView automatic = PresentationMode(
            CompatibilityOptimizationLabelCode.Automatic, null);
        CompatibilityOptimizationModeView[] valid =
            PresentationOptimization().Modes.ToArray();

        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityOptimizationView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                null,
                [automatic],
                false,
                false,
                OptimizationQualityNotice.None));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityOptimizationView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                null,
                [valid[0], valid[2], valid[1], valid[3], valid[4], valid[5]],
                false,
                false,
                OptimizationQualityNotice.None));
        CompatibilityOptimizationModeView wrongPosition = PresentationMode(
            CompatibilityOptimizationLabelCode.Balanced, 51);
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityOptimizationView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                null,
                [valid[0], valid[1], valid[2], wrongPosition, valid[4], valid[5]],
                false,
                false,
                OptimizationQualityNotice.None));
    }

    [TestMethod]
    public void ScreenFixture_RejectsUndefinedStateAndOptimizationContradictions()
    {
        CompatibilityOptimizationView optimization = PresentationOptimization();

        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityScreenModel.ForPresentation(
                (CompatibilityScreenState)999, [], [],
                BaselineExclusionReason.None, false, false));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.EstimatedCompatible, [], [],
                BaselineExclusionReason.None, true, true,
                optimization: optimization));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.OptimisationRequired, [], [],
                BaselineExclusionReason.None, false, true));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.OptimisationRequired, [], [],
                BaselineExclusionReason.None, false, true,
                setup: null,
                optimization: optimization));
    }

    [TestMethod]
    public void PresentationFixture_CannotBecomeActionAuthoritative()
    {
        CompatibilitySetupView setup = CompatibilitySetupView.ForPresentation(
            RuntimeRouteId.LlamaCpp, CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu, WeightQuantisation.Q4_K_M, 4096,
            CompatibilityFitState.Safe, 1, 2, 1, 0,
            isExperimental: false, requiresConversion: false, []);

        CompatibilityScreenModel fixture = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [], [], BaselineExclusionReason.None,
            useCurrentModelAvailable: true,
            continueEnabled: true,
            setup: setup);

        Assert.IsFalse(fixture.ContinueEnabled);
        Assert.IsFalse(fixture.UseCurrentModelAvailable);
    }

    [TestMethod]
    public void SetupFixture_RejectsZeroOrPartialDedicatedMemoryEvidence()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilitySetupView.ForPresentation(
                RuntimeRouteId.OpenVinoGenAi, CompatibilityBackend.OpenVinoGpu,
                DeviceRouteId.IntelDiscreteGpu, WeightQuantisation.F16, 4096,
                CompatibilityFitState.Safe, 1, 2, 1, 0,
                false, false, [],
                dedicatedRequiredBytes: 0,
                dedicatedSafeBudgetBytes: 1,
                dedicatedHeadroomBytes: 1));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilitySetupView.ForPresentation(
                RuntimeRouteId.OpenVinoGenAi, CompatibilityBackend.OpenVinoGpu,
                DeviceRouteId.IntelDiscreteGpu, WeightQuantisation.F16, 4096,
                CompatibilityFitState.Safe, 1, 2, 1, 0,
                false, false, [],
                dedicatedRequiredBytes: 1));
    }

    [TestMethod]
    [DataRow(DeviceRouteId.Cpu, CompatibilityBackend.OpenVinoCpu)]
    [DataRow(DeviceRouteId.IntelIntegratedGpu, CompatibilityBackend.OpenVinoGpu)]
    [DataRow(DeviceRouteId.IntelDiscreteGpu, CompatibilityBackend.OpenVinoGpu)]
    [DataRow(DeviceRouteId.IntelNpu, CompatibilityBackend.OpenVinoNpu)]
    public void OpenVinoSetupProjection_PreservesExactDeviceAndRequiredBackend(
        DeviceRouteId device,
        CompatibilityBackend expectedBackend)
    {
        OpenVinoRouteConfiguration configuration =
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.U8,
                device, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1);
        CompatibilityScreenModel model = CompatibilityScreenModel.From(
            CompletedWith(EvaluatedForConfiguration(
                CandidatePreparation.None,
                CompatibilityFitState.Safe,
                isBaseline: true,
                configuration)));

        Assert.IsNotNull(model.Setup);
        Assert.AreEqual(device, model.Setup.Device);
        Assert.AreEqual(expectedBackend, model.Setup.Backend);
    }

    [TestMethod]
    public void OpenVinoSetupProjection_UndefinedDeviceFailsClosed()
    {
        OpenVinoRouteConfiguration configuration =
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1);
        EvaluatedCandidate evaluated = EvaluatedForConfiguration(
            CandidatePreparation.None,
            CompatibilityFitState.Safe,
            isBaseline: true,
            configuration);
        typeof(OpenVinoRouteConfiguration).GetField(
            "<Device>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
                configuration, (DeviceRouteId)999);

        CompatibilityScreenModel model = CompatibilityScreenModel.From(
            CompletedWith(evaluated));

        Assert.AreEqual(CompatibilityScreenState.NotEstablished, model.State);
        Assert.IsNull(model.Setup);
        Assert.IsFalse(model.ContinueEnabled);
    }

    [TestMethod]
    public void NoFitOpenVinoProjection_ReportsSmallestEvaluatedOptimizedRequirement()
    {
        OpenVinoRouteConfiguration baselineConfiguration =
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.RouteDefault,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                1);
        CompatibilityRunResult result = CompletedWith(
            EvaluatedForConfiguration(
                CandidatePreparation.RuntimeProfileOnly,
                CompatibilityFitState.DoesNotFit,
                isBaseline: true,
                baselineConfiguration,
                safeBudgetBytes: 1,
                requiredBytes: 8 * Gibibyte));
        OptimizationCapabilitySnapshot snapshot = OpenVinoSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CrossRouteGenerationResult generated = SyntheticQualityEvidence.Generate(
            snapshot,
            ModelFacts(),
            workload,
            binding,
            ByteCount.FromBytes(1),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority());

        CompatibilityScreenModel model = ProjectWith(
            result,
            generated,
            snapshot,
            workload,
            binding,
            safeBudgetBytes: 1);

        Assert.AreEqual(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            model.State);
        Assert.IsNotNull(model.SmallestOptimizedRequiredBytes);
        Assert.IsTrue(model.SmallestOptimizedRequiredBytes > 0);
        Assert.IsTrue(
            model.SmallestOptimizedRequiredBytes > model.Setup!.SafeBudgetBytes,
            "The optimized requirement must include the fit-assessment margin so it "
            + "can be compared directly with the displayed safe budget.");
        Assert.IsTrue(
            model.SmallestOptimizedRequiredBytes < model.Setup.RequiredBytes,
            "The no-fit screen must not call the larger raw import the lightest setup.");
    }

    [TestMethod]
    public void NoFitOpenVinoProjection_LowDiskStillReportsTheSmallestAcceptableRuntimeRequirement()
    {
        OpenVinoRouteConfiguration baselineConfiguration =
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.RouteDefault,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                1);
        CompatibilityRunResult result = CompletedWith(
            EvaluatedForConfiguration(
                CandidatePreparation.RuntimeProfileOnly,
                CompatibilityFitState.DoesNotFit,
                isBaseline: true,
                baselineConfiguration,
                safeBudgetBytes: 6 * Gibibyte,
                requiredBytes: 8 * Gibibyte));
        OptimizationCapabilitySnapshot snapshot = OpenVinoSnapshot();
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();
        CrossRouteGenerationResult generated = SyntheticQualityEvidence.Generate(
            snapshot,
            ModelFacts(),
            workload,
            binding,
            ByteCount.FromBytes(6 * Gibibyte),
            ByteCount.Zero,
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority());

        CompatibilityScreenModel model = ProjectWith(
            result,
            generated,
            snapshot,
            workload,
            binding,
            safeBudgetBytes: 6 * Gibibyte,
            availableDiskBytes: 0);

        Assert.AreEqual(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            model.State);
        Assert.IsNotNull(model.SmallestOptimizedRequiredBytes);
        Assert.IsTrue(
            model.SmallestOptimizedRequiredBytes < model.Setup!.SafeBudgetBytes,
            "A disk-blocked lower format that fits RAM must remain the displayed minimum RAM target.");
        Assert.IsTrue(
            model.SmallestOptimizedRequiredBytes < model.Setup.RequiredBytes,
            "Low disk must not replace the lower format with an FP16-like memory target.");
        Assert.IsNotNull(model.OptimizationStorageRequirement);
        Assert.IsTrue(
            model.OptimizationStorageRequirement.RequiredBytes
                > model.OptimizationStorageRequirement.AvailableBytes,
            "A storage-blocked format must report storage as the remaining blocker.");
    }

    [TestMethod]
    public void RealGraniteOpenVinoInt4Candidate_FitsWithinAReportedTwoPointNineGibBudget()
    {
        InspectedModelFacts facts = InspectedModelFacts.Create(
            ByteCount.FromBytes(6_805_673_303),
            layerCount: 40,
            embeddingSize: 4_096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 131_072,
            fileType: null,
            quantisationVersion: null);
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "local-chat",
            512,
            OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(512),
             ContextTokenCount.FromTokens(2_048),
             ContextTokenCount.FromTokens(4_096),
             ContextTokenCount.FromTokens(32_768)]);

        CrossRouteGenerationResult generated = SyntheticQualityEvidence.Generate(
            OpenVinoSnapshot(),
            facts,
            workload,
            Binding(),
            ByteCount.FromBytes(2_900UL * 1024 * 1024),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority());

        Assert.IsTrue(
            generated.Candidates.Count > 0,
            "The admitted INT4/U8 OpenVINO candidate should be offered at the "
            + "same 2.9 GiB safe budget displayed by the application.");
        Assert.IsTrue(generated.Candidates.Min(candidate =>
            candidate.Metrics.PredictedPeakBytes) < 2_900UL * 1024 * 1024);
    }

    [TestMethod]
    public void SetupProjection_PreservesDedicatedMemorySeparateFromSystemShared()
    {
        OpenVinoRouteConfiguration configuration =
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.U8,
                DeviceRouteId.IntelDiscreteGpu, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1);
        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            configuration, ContextTokenCount.FromTokens(4096),
            CandidatePreparation.None, "baseline", false, true);
        ResourceEstimate estimate = ResourceEstimate.Established(
        [
            ResourceComponent.Create(
                ResourceComponentKind.Weights,
                ResourceTarget.SystemMemory, ByteCount.FromBytes(Gibibyte),
                new HashSet<LifecyclePhase>
                {
                    LifecyclePhase.SteadyStateGeneration
                }),
            ResourceComponent.Create(
                ResourceComponentKind.KvCache,
                ResourceTarget.DedicatedDeviceMemory,
                ByteCount.FromBytes(2 * Gibibyte),
                new HashSet<LifecyclePhase>
                {
                    LifecyclePhase.SteadyStateGeneration
                })
        ], new HashSet<EstimationLimitation>());
        ResourcePeakProfile peaks = ResourcePhaseComposer.Compose(estimate.Components);
        FitAssessment fit = FitPolicy.Assess(
            peaks,
            AvailableResources.Create(
                ByteCount.FromBytes(20 * Gibibyte),
                ByteCount.FromBytes(3 * Gibibyte),
                ByteCount.FromBytes(500 * Gibibyte),
                DateTimeOffset.UnixEpoch),
            SafetyPolicy.ProvisionalV1());
        EvaluatedCandidate evaluated = EvaluatedCandidate.Create(
            candidate, estimate, peaks, fit, WeightQuantisation.F16,
            EvidenceGrade.Estimated, PerformanceIndicator.NotEstablished(),
            ByteCount.Zero);

        CompatibilitySetupView setup = CompatibilityScreenModel.From(
            CompletedWith(evaluated)).Setup!;

        Assert.AreEqual(fit.RequiredBytes.Bytes, setup.SystemSharedRequiredBytes);
        Assert.AreEqual(
            fit.DedicatedRequiredBytes.Bytes, setup.DedicatedRequiredBytes);
        Assert.AreEqual(
            fit.DedicatedSafeBudget.Bytes, setup.DedicatedSafeBudgetBytes);
        Assert.AreEqual(
            fit.DedicatedHeadroom.Bytes, setup.DedicatedHeadroomBytes);
    }

    [TestMethod]
    public void OptimizationModes_PreserveGeneratedDedicatedMemoryAdmission()
    {
        OpenVinoRouteConfiguration baselineConfiguration =
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.U8,
                DeviceRouteId.IntelDiscreteGpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1);
        CompatibilityRunResult result = CompletedWith(EvaluatedForConfiguration(
            CandidatePreparation.RuntimeProfileOnly,
            CompatibilityFitState.DoesNotFit,
            isBaseline: true,
            baselineConfiguration));
        OptimizationCapabilitySnapshot snapshot =
            OpenVinoSnapshot(DeviceRouteId.IntelDiscreteGpu);
        OptimizationWorkload workload = Workload();
        OptimizationJourneyBinding binding = Binding();

        CompatibilityScreenModel model = ProjectWith(
            result,
            AdmittedAlternative(snapshot, workload, binding),
            snapshot,
            workload,
            binding);

        Assert.AreEqual(CompatibilityScreenState.OptimisationRequired, model.State);
        CompatibilityOptimizationModeView mode =
            model.Optimization!.Modes.Single(candidate =>
                candidate.LabelCode == CompatibilityOptimizationLabelCode.Automatic);
        Assert.AreEqual(DeviceRouteId.IntelDiscreteGpu, mode.Device);
        Assert.IsNotNull(mode.DedicatedRequiredBytes);
        Assert.IsNotNull(mode.DedicatedSafeBudgetBytes);
        Assert.AreEqual(
            mode.DedicatedSafeBudgetBytes - mode.DedicatedRequiredBytes,
            mode.DedicatedHeadroomBytes);
    }

    private static CompatibilityOptimizationModeView PresentationMode(
        CompatibilityOptimizationLabelCode label,
        int? slider) =>
        CompatibilityOptimizationModeView.ForPresentation(
            label, slider, OptimizationRoute.Gguf,
            GgufWeightFormat.Imported, GgufKvCacheFormat.Q8_0,
            null, null, DeviceRouteId.Cpu, OptimizationAssessment.Good,
            4096, 2 * Gibibyte, 3 * Gibibyte, Gibibyte,
            false, false, OptimizationQualityNotice.None, false, false);

    private static CompatibilityOptimizationView PresentationOptimization() =>
        CompatibilityOptimizationView.ForPresentation(
            CompatibilityOptimizationLabelCode.Automatic,
            null,
            [
                PresentationMode(CompatibilityOptimizationLabelCode.Automatic, null),
                PresentationMode(CompatibilityOptimizationLabelCode.MaximumEfficiency, 10),
                PresentationMode(CompatibilityOptimizationLabelCode.Efficient, 30),
                PresentationMode(CompatibilityOptimizationLabelCode.Balanced, 50),
                PresentationMode(CompatibilityOptimizationLabelCode.HighCapability, 70),
                PresentationMode(CompatibilityOptimizationLabelCode.MaximumCapability, 90)
            ],
            false,
            false,
            OptimizationQualityNotice.None);

    [TestMethod]
    public void AMachineThatCannotRunAnything_ShowsNoEstimatedSafeConfiguration()
    {
        // every candidate was sized and compared. this is a conclusion
        Assert.AreEqual(
            nameof(CompatibilityScreenState.NoEstimatedSafeConfiguration),
            CompatibilityScreenModel.From(RunWith(4)).State.ToString());
    }

    [TestMethod]
    public void AnUnestablishedRun_ShowsNotEstablishedRatherThanNothingFits()
    {
        // The distinction that matters most: "we could not tell you" is not the
        // same claim as "we checked and the answer is no".
        Assert.AreEqual(
            nameof(CompatibilityScreenState.NotEstablished),
            CompatibilityScreenModel.From(NotEstablishedResult()).State.ToString());
    }

    [TestMethod]
    public void AFailedRun_ShowsNotEstablished()
    {
        CompatibilityRunResult failed = CompatibilityRunResult.Failed(
            CompatibilityRunId.New(),
            [CompatibilityFinding.Create(
                CompatibilityFindingCode.HandoffClaimFailed, FindingSeverity.Blocking)],
            [PolicyIdentity.Create("estimator", "v1", PolicyProvenance.Provisional)],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        Assert.AreEqual(
            nameof(CompatibilityScreenState.NotEstablished),
            CompatibilityScreenModel.From(failed).State.ToString());
    }

    [TestMethod]
    public void ACancelledRun_ShowsCancelled()
    {
        CompatibilityRunResult cancelled = CompatibilityRunResult.Cancelled(
            CompatibilityRunId.New(),
            [],
            [PolicyIdentity.Create("estimator", "v1", PolicyProvenance.Provisional)],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        Assert.AreEqual(
            nameof(CompatibilityScreenState.Cancelled),
            CompatibilityScreenModel.From(cancelled).State.ToString());
    }

    [TestMethod]
    public void ContinueIsEnabledOnlyWhereSomethingWasConcluded()
    {
        Assert.IsTrue(CompatibilityScreenModel.From(RunWith(64)).ContinueEnabled);
        Assert.IsFalse(CompatibilityScreenModel.From(RunWith(4)).ContinueEnabled);
        Assert.IsFalse(CompatibilityScreenModel.From(NotEstablishedResult()).ContinueEnabled);
    }

    [TestMethod]
    public void AnUnestablishedScreen_CarriesWhatWasMissing()
    {
        // Screen 06 must list the exact missing evidence with recovery actions,
        // which it can only do if the engine hands the codes over
        CompatibilityScreenModel model = CompatibilityScreenModel.From(NotEstablishedResult());

        Assert.IsTrue(model.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.ModelFactsUnavailable));
    }

    [TestMethod]
    public void EveryConcludedScreen_CarriesAllFourModes()
    {
        // an unavailable mode is disabled with its reason, never hidden
        Assert.AreEqual(4, CompatibilityScreenModel.From(RunWith(64)).Modes.Count);
        Assert.AreEqual(4, CompatibilityScreenModel.From(RunWith(4)).Modes.Count);
    }

    [TestMethod]
    public void AnUnestablishedScreen_CarriesNoModes()
    {
        // nothing was assessed, so there is nothing to disable with a reason
        Assert.AreEqual(
            0, CompatibilityScreenModel.From(NotEstablishedResult()).Modes.Count);
    }

    [TestMethod]
    public void TheScreenModel_CarriesNoWordingOfItsOwn()
    {
        // presentation owns every string the user reads. a string here would be
        // a message the engine wrote, which section 14 forbids reaching a result
        // and which would also be untranslatable
        Assert.AreEqual(
            0,
            typeof(CompatibilityScreenModel)
                .GetProperties()
                .Count(property => property.PropertyType == typeof(string)));
    }

    [TestMethod]
    public void EveryTerminalOutcome_MapsToAScreen()
    {
        // A new outcome added without a rule would fall to Unspecified, and the
        // page would have nothing to show
        foreach (CompatibilityRunOutcome outcome in Enum.GetValues<CompatibilityRunOutcome>())
        {
            CompatibilityRunResult result = outcome switch
            {
                CompatibilityRunOutcome.Completed => RunWith(64),
                CompatibilityRunOutcome.Cancelled => CompatibilityRunResult.Cancelled(
                    CompatibilityRunId.New(),
                    [],
                    [PolicyIdentity.Create("e", "v1", PolicyProvenance.Provisional)],
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow),
                _ => NotEstablishedResult()
            };

            Assert.AreNotEqual(
                CompatibilityScreenState.Unspecified,
                CompatibilityScreenModel.From(result).State,
                $"{outcome} maps to no screen.");
        }
    }

    [TestMethod]
    public void ConcludedRun_CarriesTheSetupItConcludedAbout()
    {
        // a verdict with no figures under it asks the user to trust a number
        // they are never shown
        CompatibilitySetupView? setup = CompatibilityScreenModel.From(RunWith(64)).Setup;

        Assert.IsNotNull(setup, "A completed run described no setup.");
        Assert.AreEqual(RuntimeRouteId.LlamaCpp, setup.Route);
        Assert.AreEqual(CompatibilityBackend.Cpu, setup.Backend);
        Assert.AreEqual(DeviceRouteId.Cpu, setup.Device);
        Assert.AreEqual(4096, setup.ContextTokens);
        Assert.IsTrue(setup.RequiredBytes > 0, "A setup that needs nothing is not a setup.");
    }

    [TestMethod]
    public void SetupBreakdown_SumsToTheRequirementItExplains()
    {
        // Components are live in different phases; listing them all would
        // produce a breakdown larger than the total it claims to explain, and a
        // bar whose parts overflow its own length
        CompatibilitySetupView setup = CompatibilityScreenModel.From(RunWith(64)).Setup!;

        ulong total = setup.Components.Aggregate(0UL, (sum, part) => sum + part.Bytes)
            + setup.UncertaintyAllowanceBytes;

        Assert.AreEqual(setup.RequiredBytes, total);
    }

    [TestMethod]
    public void UncalibratedEstimate_DemandsAnAllowanceOnTopOfTheParts()
    {
        // every figure today comes from documented defaults rather than
        // measurement. an allowance of zero would mean the engine considers its
        // own arithmetic exact, which is the false-safe this design forbids
        CompatibilitySetupView setup = CompatibilityScreenModel.From(RunWith(64)).Setup!;

        Assert.IsTrue(
            setup.UncertaintyAllowanceBytes > 0,
            "An uncalibrated estimate claimed to need no margin.");
    }

    [TestMethod]
    public void SetupBreakdown_IsOrderedLargestFirst()
    {
        // A user facing "it does not fit" needs the responsible part at the top.
        CompatibilitySetupView setup = CompatibilityScreenModel.From(RunWith(64)).Setup!;

        CollectionAssert.AreEqual(
            setup.Components.Select(part => part.Bytes).OrderByDescending(bytes => bytes).ToList(),
            setup.Components.Select(part => part.Bytes).ToList());
    }

    [TestMethod]
    public void SetupBreakdown_NamesEachComponent()
    {
        // Unspecified would reach presentation as "Other", hiding the very cost
        // the breakdown exists to expose
        CompatibilitySetupView setup = CompatibilityScreenModel.From(RunWith(64)).Setup!;

        Assert.IsFalse(
            setup.Components.Any(part => part.Kind == ResourceComponentKind.Unspecified));
    }

    [TestMethod]
    public void RunThatEstablishedNothing_DescribesNoSetup()
    {
        // figures drawn at zero would read as a model that costs nothing, which
        // is the opposite of what "we could not work this out" means.
        Assert.IsNull(CompatibilityScreenModel.From(NotEstablishedResult()).Setup);
    }

    [TestMethod]
    public void SetupHeadroom_IsConsistentWithTheBudgetAndRequirement()
    {
        // presentation subtracts nothing of its own, so these three figures have
        // to agree or the page will contradict itself
        CompatibilitySetupView setup = CompatibilityScreenModel.From(RunWith(64)).Setup!;

        Assert.AreEqual(
            setup.SafeBudgetBytes - setup.RequiredBytes,
            setup.HeadroomBytes);
    }

    [TestMethod]
    public void SetupUnderMemoryPressure_ReportsTheClosestAttempt()
    {
        // Nothing was admitted, so the screen is explaining a refusal. Showing
        // the nearest miss is what tells the user how much has to change
        CompatibilityScreenModel model = CompatibilityScreenModel.From(RunWith(2));

        if (model.State != CompatibilityScreenState.NoEstimatedSafeConfiguration)
        {
            Assert.Inconclusive("This machine fits at 2 GiB, so there is no refusal to describe.");
        }

        Assert.IsNotNull(model.Setup, "A refusal described no setup, so it explained nothing.");
        Assert.AreEqual(CompatibilityFitState.DoesNotFit, model.Setup.Fit);
        Assert.AreEqual(0UL, model.Setup.HeadroomBytes);
    }

    [TestMethod]
    public void SetupForPresentation_CopiesItsComponents()
    {
        // a fixture handing in a list it still holds could rewrite a screen's
        // figures after the screen was built
        List<CompatibilityComponentView> components =
        [
            new(ResourceComponentKind.Weights, 4 * Gibibyte)
        ];

        CompatibilitySetupView setup = CompatibilitySetupView.ForPresentation(
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            WeightQuantisation.Q4_K_M,
            4096,
            CompatibilityFitState.Safe,
            4 * Gibibyte,
            8 * Gibibyte,
            4 * Gibibyte,
            0,
            isExperimental: false,
            requiresConversion: false,
            components);

        components.Clear();

        Assert.AreEqual(1, setup.Components.Count);
    }

    [TestMethod]
    public void RunWhoseAdapterThrows_PropagatesTheProgrammingFault()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => CompatibilityRunCoordinator.Execute(
            new CompatibilityRunRequest(CompatibilityContextRequest.ApplicationDefault()),
            CompatibilityRunDependencies.Create(
                new ThrowingGateway(),
                new Facts(ModelFacts()),
                new Machine(HardwareFacts.Create(
                    ByteCount.FromBytes(64 * Gibibyte),
                    ByteCount.FromBytes(8 * Gibibyte),
                    ByteCount.FromBytes(500 * Gibibyte),
                    new HashSet<DeviceRouteId> { DeviceRouteId.Cpu },
                    new HashSet<CompatibilityBackend> { CompatibilityBackend.Cpu })),
                new MemoryProbe(64),
                SupportMatrix.ProvisionalV1(),
                EstimatorPolicy.ProvisionalV1(),
                SafetyPolicy.ProvisionalV1(),
                new HashSet<string>(),
                TrustedSourceAvailability.None(),
                GgufRouteConfiguration.Create(
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None),
                ContextTokenCount.FromTokens(4096),
                TimeProvider.System),
            CancellationToken.None));

        Assert.AreEqual("An adapter failed the way adapters do.", exception.Message);
    }

    [TestMethod]
    public void ThrowingAdapter_StillReleasesTheClaim()
    {
        // the rollback lives in a finally, and an exception is the one exit that
        // most easily skips cleanup. a held claim blocks every later run
        ThrowingGateway gateway = new();

        _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
            CompatibilityRunCoordinator.Execute(
            new CompatibilityRunRequest(CompatibilityContextRequest.ApplicationDefault()),
            CompatibilityRunDependencies.Create(
                gateway,
                new Facts(ModelFacts()),
                new Machine(HardwareFacts.Create(
                    ByteCount.FromBytes(64 * Gibibyte),
                    ByteCount.FromBytes(8 * Gibibyte),
                    ByteCount.FromBytes(500 * Gibibyte),
                    new HashSet<DeviceRouteId> { DeviceRouteId.Cpu },
                    new HashSet<CompatibilityBackend> { CompatibilityBackend.Cpu })),
                new MemoryProbe(64),
                SupportMatrix.ProvisionalV1(),
                EstimatorPolicy.ProvisionalV1(),
                SafetyPolicy.ProvisionalV1(),
                new HashSet<string>(),
                TrustedSourceAvailability.None(),
                GgufRouteConfiguration.Create(
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None),
                ContextTokenCount.FromTokens(4096),
                TimeProvider.System),
            CancellationToken.None));

        Assert.IsTrue(gateway.RolledBack, "A throwing run kept the claim it took.");
    }

    [TestMethod]
    public void PublishedBaselineFingerprint_BelongsToTheBaselineCandidate()
    {
        // This fingerprint is what "use what I already have" resolves against.
        // Reading it off a position in another file's output rather than off the
        // baseline itself would let it name a stranger's configuration
        CompatibilityRunResult result = RunWith(64);

        if (result.Assessment is not { BaselineFingerprint: { } published })
        {
            Assert.Inconclusive("This run published no baseline to check.");
            return;
        }

        EvaluatedCandidate baseline = result.Assessment.EvaluatedCandidates
            .Single(candidate => candidate.Candidate.IsBaseline);

        Assert.AreEqual(baseline.Fingerprint, published);
    }

    [TestMethod]
    public void EngineDoor_NeverThrows()
    {
        // the public door builds its adapters outside the coordinator's guard
        // every one of them becomes something that touches the machine, and an
        // engine that can throw is an app that can disappear
        CompatibilityScreenModel model = CompatibilityEngine.RunWithAvailableAdapters();

        Assert.AreNotEqual(CompatibilityScreenState.Unspecified, model.State);
    }

    private sealed class ThrowingGateway : ICompatibilityInputGateway
    {
        internal bool RolledBack { get; private set; }

        public HandoffClaim Claim() =>
            throw new InvalidOperationException("An adapter failed the way adapters do.");

        public void Rollback() => RolledBack = true;

        public bool Commit(CompatibilityRunId runId) => true;
    }
}
