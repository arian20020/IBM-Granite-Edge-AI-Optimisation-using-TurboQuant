using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
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
    private const ulong Gibibyte = 1024UL * 1024 * 1024;
    private const string Digest =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

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
        RouteConfiguration configuration)
    {
        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            configuration,
            ContextTokenCount.FromTokens(4096),
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
                ByteCount.FromBytes(3 * Gibibyte),
                ByteCount.FromBytes(2 * Gibibyte),
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
        string runtimeVersion = "b4321",
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
                "b4321", [f16, q8],
                runtimeAuthority: GgufRuntimeAuthority.Create(
                    "b4321", Commit,
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
                "2026.1.0",
                [admitted],
                [OpenVinoExecutionAuthority.Create(
                    "ov-int4",
                    "ov-int4",
                    OpenVinoWeightPrecision.Fp16,
                    OpenVinoBuildIdentity.Create(
                        "2026.1.0", "2026.1.0", "2026.1.0", Digest),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["openvino"] = "2026.1.0"
                    },
                    compiledCacheIsDisposable: true,
                    turboQuantBuild: null)]));
    }

    private static CrossRouteGenerationResult AdmittedAlternative(
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding) =>
        CrossRouteCandidateGenerator.Generate(
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
    public void BaselineFailsButAnAdmittedAlternativeFits_ShowsOptimisationRequired()
    {
        // Regression caught: deciding from "anything fits" labels a model that
        // cannot run as imported as already compatible and skips quantisation.
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
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
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
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
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
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
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
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
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
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
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
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
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
        CrossRouteGenerationResult genuine = CrossRouteCandidateGenerator.Generate(
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
        CrossRouteGenerationResult genuine = CrossRouteCandidateGenerator.Generate(
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
        // Every candidate was sized and compared. This is a conclusion.
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
        // which it can only do if the engine hands the codes over.
        CompatibilityScreenModel model = CompatibilityScreenModel.From(NotEstablishedResult());

        Assert.IsTrue(model.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.ModelFactsUnavailable));
    }

    [TestMethod]
    public void EveryConcludedScreen_CarriesAllFourModes()
    {
        // An unavailable mode is disabled with its reason, never hidden.
        Assert.AreEqual(4, CompatibilityScreenModel.From(RunWith(64)).Modes.Count);
        Assert.AreEqual(4, CompatibilityScreenModel.From(RunWith(4)).Modes.Count);
    }

    [TestMethod]
    public void AnUnestablishedScreen_CarriesNoModes()
    {
        // Nothing was assessed, so there is nothing to disable with a reason.
        Assert.AreEqual(
            0, CompatibilityScreenModel.From(NotEstablishedResult()).Modes.Count);
    }

    [TestMethod]
    public void TheScreenModel_CarriesNoWordingOfItsOwn()
    {
        // Presentation owns every string the user reads. A string here would be
        // a message the engine wrote, which section 14 forbids reaching a result
        // and which would also be untranslatable.
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
        // page would have nothing to show.
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
        // A verdict with no figures under it asks the user to trust a number
        // they are never shown.
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
        // bar whose parts overflow its own length.
        CompatibilitySetupView setup = CompatibilityScreenModel.From(RunWith(64)).Setup!;

        ulong total = setup.Components.Aggregate(0UL, (sum, part) => sum + part.Bytes)
            + setup.UncertaintyAllowanceBytes;

        Assert.AreEqual(setup.RequiredBytes, total);
    }

    [TestMethod]
    public void UncalibratedEstimate_DemandsAnAllowanceOnTopOfTheParts()
    {
        // Every figure today comes from documented defaults rather than
        // measurement. An allowance of zero would mean the engine considers its
        // own arithmetic exact, which is the false-safe this design forbids.
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
        // the breakdown exists to expose.
        CompatibilitySetupView setup = CompatibilityScreenModel.From(RunWith(64)).Setup!;

        Assert.IsFalse(
            setup.Components.Any(part => part.Kind == ResourceComponentKind.Unspecified));
    }

    [TestMethod]
    public void RunThatEstablishedNothing_DescribesNoSetup()
    {
        // Figures drawn at zero would read as a model that costs nothing, which
        // is the opposite of what "we could not work this out" means.
        Assert.IsNull(CompatibilityScreenModel.From(NotEstablishedResult()).Setup);
    }

    [TestMethod]
    public void SetupHeadroom_IsConsistentWithTheBudgetAndRequirement()
    {
        // Presentation subtracts nothing of its own, so these three figures have
        // to agree or the page will contradict itself.
        CompatibilitySetupView setup = CompatibilityScreenModel.From(RunWith(64)).Setup!;

        Assert.AreEqual(
            setup.SafeBudgetBytes - setup.RequiredBytes,
            setup.HeadroomBytes);
    }

    [TestMethod]
    public void SetupUnderMemoryPressure_ReportsTheClosestAttempt()
    {
        // Nothing was admitted, so the screen is explaining a refusal. Showing
        // the nearest miss is what tells the user how much has to change.
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
        // A fixture handing in a list it still holds could rewrite a screen's
        // figures after the screen was built.
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
        // The rollback lives in a finally, and an exception is the one exit that
        // most easily skips cleanup. A held claim blocks every later run.
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
        // baseline itself would let it name a stranger's configuration.
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
        // The public door builds its adapters outside the coordinator's guard.
        // Every one of them becomes something that touches the machine, and an
        // engine that can throw is an app that can disappear.
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
