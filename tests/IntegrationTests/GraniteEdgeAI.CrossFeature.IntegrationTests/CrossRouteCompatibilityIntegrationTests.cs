using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class CrossRouteCompatibilityIntegrationTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void CurrentModelFitAllowsDirectChatAndOptionalAlternative(int route)
    {
        RouteConfiguration baselineConfiguration = Configuration(route, baseline: true);
        RouteConfiguration alternativeConfiguration = Configuration(route, baseline: false);
        EvaluatedCandidate baseline = Evaluated(
            baselineConfiguration, CompatibilityFitState.Safe, isBaseline: true);
        EvaluatedCandidate alternative = Evaluated(
            alternativeConfiguration, CompatibilityFitState.Safe, isBaseline: false);

        CompatibilityScreenModel screen = CompatibilityScreenModel.From(
            CompletedWith(baseline, alternative));

        Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible, screen.State);
        Assert.IsTrue(screen.UseCurrentModelAvailable,
            "A safe current configuration must permit direct Chat.");
        Assert.IsTrue(screen.ContinueEnabled);
        Assert.IsNotNull(screen.CurrentSetup);
        Assert.IsTrue(alternative.Candidate.Preparation != CandidatePreparation.None,
            "A distinct safe alternative remains available for optional optimisation.");
        AssertRouteSpecificConfiguration(route, baselineConfiguration);
        AssertRouteSpecificConfiguration(route, alternativeConfiguration);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void OnlyAdmittedAlternativeFitRequiresOptimization(int route)
    {
        EvaluatedCandidate baseline = Evaluated(
            Configuration(route, baseline: true),
            CompatibilityFitState.DoesNotFit,
            isBaseline: true);
        EvaluatedCandidate admittedAlternative = Evaluated(
            Configuration(route, baseline: false),
            CompatibilityFitState.Safe,
            isBaseline: false);

        CompatibilityScreenModel screen = CompatibilityScreenModel.From(
            CompletedWith(baseline, admittedAlternative));

        Assert.AreEqual(CompatibilityScreenState.OptimisationRequired, screen.State);
        Assert.IsFalse(screen.UseCurrentModelAvailable,
            "The failing current model must never be the Continue target.");
        Assert.IsTrue(screen.ContinueEnabled,
            "The independently safe alternative is the only executable route.");
        Assert.IsNull(screen.Setup);
        Assert.IsNotNull(screen.CurrentSetup);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void NoSafeConfigurationDisablesExecution(int route)
    {
        EvaluatedCandidate baseline = Evaluated(
            Configuration(route, baseline: true),
            CompatibilityFitState.DoesNotFit,
            isBaseline: true);
        EvaluatedCandidate alternative = Evaluated(
            Configuration(route, baseline: false),
            CompatibilityFitState.DoesNotFit,
            isBaseline: false);

        CompatibilityScreenModel screen = CompatibilityScreenModel.From(
            CompletedWith(baseline, alternative));

        Assert.AreEqual(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            screen.State);
        Assert.IsFalse(screen.UseCurrentModelAvailable);
        Assert.IsFalse(screen.ContinueEnabled);
    }

    [TestMethod]
    public void InstalledAvailableReserveAndExecutableBudgetRemainDistinct()
    {
        // Independent H1 calculation: reserve=max(ceil(10 GiB * 10%), 512 MiB)
        // = 1 GiB, so the executable budget is 9 GiB, not installed memory.
        CompatibilityMachineMemory memory = CompatibilityMachineMemory.Create(
            installedSystemMemoryBytes: 16 * GiB,
            availableSystemMemoryBytes: 10 * GiB,
            safetyReserveBytes: GiB,
            safeModelBudgetBytes: 9 * GiB);

        Assert.AreEqual(16 * GiB, memory.InstalledSystemMemoryBytes);
        Assert.AreEqual(10 * GiB, memory.AvailableSystemMemoryBytes);
        Assert.AreEqual(GiB, memory.SafetyReserveBytes);
        Assert.AreEqual(9 * GiB, memory.SafeModelBudgetBytes);
        Assert.AreNotEqual(memory.InstalledSystemMemoryBytes,
            memory.AvailableSystemMemoryBytes);
        Assert.AreNotEqual(memory.AvailableSystemMemoryBytes,
            memory.SafeModelBudgetBytes);
        Assert.AreEqual(
            memory.AvailableSystemMemoryBytes - memory.SafetyReserveBytes,
            memory.SafeModelBudgetBytes);
    }

    private static RouteConfiguration Configuration(int route, bool baseline) =>
        route == 0
            ? GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                baseline ? GgufKvCacheFormat.F16 : GgufKvCacheFormat.Q8_0,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None)
            : OpenVinoRouteConfiguration.Create(
                baseline ? OpenVinoWeightFormat.Original : OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                streams: 1);

    private static EvaluatedCandidate Evaluated(
        RouteConfiguration configuration,
        CompatibilityFitState fit,
        bool isBaseline)
    {
        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            configuration,
            ContextTokenCount.FromTokens(4096),
            isBaseline ? CandidatePreparation.None : CandidatePreparation.RuntimeProfileOnly,
            isBaseline ? "baseline" : "alternative",
            isExperimental: false,
            isBaseline);
        ResourceEstimate estimate = ResourceEstimate.Established(
            [ResourceComponent.Create(
                ResourceComponentKind.Weights,
                ResourceTarget.SystemMemory,
                ByteCount.FromBytes(2 * GiB),
                new HashSet<LifecyclePhase> { LifecyclePhase.Load })],
            new HashSet<EstimationLimitation>());
        bool fits = fit is CompatibilityFitState.Safe or CompatibilityFitState.Narrow;

        return EvaluatedCandidate.Create(
            candidate,
            estimate,
            ResourcePhaseComposer.Compose(estimate.Components),
            new FitAssessment(
                fit,
                fits ? FitLimitingReason.None : FitLimitingReason.InsufficientSystemMemory,
                ByteCount.FromBytes(3 * GiB),
                ByteCount.FromBytes(2 * GiB),
                fits ? ByteCount.FromBytes(GiB) : ByteCount.Zero,
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
        IReadOnlyList<CompatibilityModeSelection> modes =
            baseline.Candidate.Configuration is GgufRouteConfiguration gguf
                ? ModeSelector.SelectAll(
                    ModeSelectionRequest.Create(
                        evaluated,
                        new HashSet<string>(),
                        ContextTokenCount.FromTokens(4096),
                        gguf)).Selections
                :
                [
                    CompatibilityModeSelection.NotEstablished(CompatibilityMode.Automatic),
                    CompatibilityModeSelection.NotEstablished(CompatibilityMode.Quality),
                    CompatibilityModeSelection.NotEstablished(CompatibilityMode.Balanced),
                    CompatibilityModeSelection.NotEstablished(CompatibilityMode.Efficiency)
                ];
        CompatibilityAssessment assessment = CompatibilityAssessment.Create(
            evaluated,
            modes,
            baseline.Fingerprint,
            BaselineExclusionReason.None,
            baseline.Fit.State is CompatibilityFitState.Safe
                or CompatibilityFitState.Narrow);

        return CompatibilityRunResult.Completed(
            CompatibilityRunId.New(),
            assessment,
            [],
            [PolicyIdentity.Create(
                "independent-test-estimator",
                "v1",
                PolicyProvenance.Provisional)],
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
    }

    private static void AssertRouteSpecificConfiguration(
        int route,
        RouteConfiguration configuration)
    {
        if (route == 0)
        {
            Assert.IsInstanceOfType<GgufRouteConfiguration>(configuration);
            StringAssert.StartsWith(configuration.CanonicalDescriptor, "gguf|");
        }
        else
        {
            Assert.IsInstanceOfType<OpenVinoRouteConfiguration>(configuration);
            StringAssert.StartsWith(configuration.CanonicalDescriptor, "openvino|");
        }
    }
}
