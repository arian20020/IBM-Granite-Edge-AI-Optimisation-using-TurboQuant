using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.ModeSelection;

[TestClass]
public sealed class ModeSelectorTests
{
    private static GgufRouteConfiguration Baseline() =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);

    private static EvaluatedCandidate Candidate(
        GgufKvCacheFormat kv = GgufKvCacheFormat.F16,
        int context = 4096,
        CompatibilityFitState state = CompatibilityFitState.Safe,
        bool isBaseline = false,
        bool requiresEvidence = false,
        EvidenceGrade evidence = EvidenceGrade.Estimated,
        ulong headroom = 1_000_000)
    {
        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                kv,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(context),
            CandidatePreparation.None,
            supportEntryId: requiresEvidence ? "needs-evidence" : "entry-1",
            isExperimental: false,
            isBaseline);

        ResourceEstimate estimate = ResourceEstimate.Established(
            [
                ResourceComponent.Create(
                    ResourceComponentKind.Weights,
                    ResourceTarget.SystemMemory,
                    ByteCount.FromBytes(1024),
                    new HashSet<LifecyclePhase> { LifecyclePhase.Load })
            ],
            new HashSet<EstimationLimitation>());

        return EvaluatedCandidate.Create(
            candidate,
            estimate,
            ResourcePhaseComposer.Compose(estimate.Components),
            new FitAssessment(
                state,
                state == CompatibilityFitState.Safe
                    ? FitLimitingReason.None
                    : FitLimitingReason.InsufficientSystemMemory,
                ByteCount.FromBytes(8192),
                ByteCount.FromBytes(1024),
                ByteCount.FromBytes(headroom),
                PressureRatio: 0.5m),
            WeightQuantisation.Q4_K_M,
            evidence,
            PerformanceIndicator.NotEstablished(),
            ByteCount.Zero);
    }

    private static ModeSelectionOutcome Select(
        IReadOnlyList<EvaluatedCandidate> candidates,
        IReadOnlySet<string>? evidenceRequiringEntries = null) =>
        ModeSelector.SelectAll(new ModeSelectionRequest(
            candidates,
            evidenceRequiringEntries ?? new HashSet<string>(),
            ContextTokenCount.FromTokens(4096),
            Baseline()));

    [TestMethod]
    public void SelectAll_AlwaysReturnsAllFourModes()
    {
        // A mode is never silently missing — it is disabled with a reason.
        ModeSelectionOutcome outcome = Select([Candidate()]);

        Assert.AreEqual(4, outcome.Selections.Count);
        CollectionAssert.AreEquivalent(
            new[]
            {
                CompatibilityMode.Automatic,
                CompatibilityMode.Quality,
                CompatibilityMode.Balanced,
                CompatibilityMode.Efficiency
            },
            outcome.Selections.Select(selection => selection.Mode).ToArray());
    }

    [TestMethod]
    public void SelectAll_MarksEveryModeAvailableWhenACandidateIsAdmitted()
    {
        Assert.IsTrue(Select([Candidate()]).Selections.All(
            selection => selection.Availability == ModeAvailability.Available));
    }

    [TestMethod]
    public void SelectAll_RecordsTheOrderingThatDecidedEachAvailableMode()
    {
        foreach (CompatibilityModeSelection selection in Select([Candidate()]).Selections)
        {
            Assert.IsTrue(selection.Factors.Count > 0, $"{selection.Mode} recorded no ordering.");
            Assert.AreEqual(SelectionFactor.Fingerprint, selection.Factors[^1]);
        }
    }

    [TestMethod]
    public void SelectAll_ReportsUnavailableWithAReasonWhenNothingIsAdmitted()
    {
        ModeSelectionOutcome outcome =
            Select([Candidate(state: CompatibilityFitState.DoesNotFit)]);

        foreach (CompatibilityModeSelection selection in outcome.Selections)
        {
            Assert.AreEqual(ModeAvailability.Unavailable, selection.Availability);
            Assert.AreEqual(
                nameof(ModeAdmissionReason.FitStateNotSafeOrNarrow),
                selection.Reason.ToString());
            Assert.IsNull(selection.SelectedFingerprint);
        }
    }

    [TestMethod]
    public void SelectAll_ReportsNotEstablishedWhenThereAreNoCandidatesAtAll()
    {
        // No candidates is different from candidates that all failed a gate:
        // nothing was assessed, so there is no reason to give.
        ModeSelectionOutcome outcome = Select([]);

        Assert.IsTrue(outcome.Selections.All(
            selection => selection.Availability == ModeAvailability.NotEstablished));
    }

    [TestMethod]
    public void SelectAll_ExcludesCandidatesWhoseEntryNeedsEvidenceThatDoesNotExist()
    {
        ModeSelectionOutcome outcome = Select(
            [Candidate(requiresEvidence: true)],
            new HashSet<string> { "needs-evidence" });

        Assert.IsTrue(outcome.Selections.All(
            selection => selection.Availability == ModeAvailability.Unavailable));
        Assert.IsTrue(outcome.Selections.All(selection =>
            selection.Reason == ModeAdmissionReason.EvidenceBelowAdmissionLevel));
    }

    [TestMethod]
    public void SelectAll_QualityPrefersThePreservedContext()
    {
        ModeSelectionOutcome outcome = Select(
            [Candidate(kv: GgufKvCacheFormat.Q8_0, context: 2048), Candidate(context: 4096)]);

        CompatibilityModeSelection quality =
            outcome.Selections.Single(s => s.Mode == CompatibilityMode.Quality);

        Assert.AreEqual(ModeAvailability.Available, quality.Availability);
    }

    [TestMethod]
    public void SelectAll_IsIndependentOfCandidateOrder()
    {
        EvaluatedCandidate first = Candidate(kv: GgufKvCacheFormat.F16);
        EvaluatedCandidate second = Candidate(kv: GgufKvCacheFormat.Q8_0);

        ModeSelectionOutcome forward = Select([first, second]);
        ModeSelectionOutcome reversed = Select([second, first]);

        foreach (CompatibilityMode mode in new[]
        {
            CompatibilityMode.Automatic,
            CompatibilityMode.Quality,
            CompatibilityMode.Balanced,
            CompatibilityMode.Efficiency
        })
        {
            Assert.AreEqual(
                forward.Selections.Single(s => s.Mode == mode).SelectedFingerprint?.Value,
                reversed.Selections.Single(s => s.Mode == mode).SelectedFingerprint?.Value,
                $"{mode} depended on the order candidates arrived in.");
        }
    }

    [TestMethod]
    public void SelectAll_OffersUseCurrentModelWhenTheBaselineIsSafe()
    {
        Assert.IsTrue(Select([Candidate(isBaseline: true)]).UseCurrentModelAvailable);
    }

    [TestMethod]
    public void SelectAll_OffersUseCurrentModelWhenTheBaselineIsNarrow()
    {
        Assert.IsTrue(
            Select([Candidate(isBaseline: true, state: CompatibilityFitState.Narrow)])
                .UseCurrentModelAvailable);
    }

    [TestMethod]
    public void SelectAll_WithholdsUseCurrentModelWhenTheBaselineDoesNotFit()
    {
        Assert.IsFalse(
            Select([Candidate(isBaseline: true, state: CompatibilityFitState.DoesNotFit)])
                .UseCurrentModelAvailable);
    }

    [TestMethod]
    public void SelectAll_WithholdsUseCurrentModelWhenThereIsNoBaseline()
    {
        Assert.IsFalse(Select([Candidate(isBaseline: false)]).UseCurrentModelAvailable);
    }

    [TestMethod]
    public void SelectAll_SelectsOnlyFromAdmittedCandidates()
    {
        // One admitted, one not. Every mode must pick the admitted one.
        EvaluatedCandidate admitted = Candidate(kv: GgufKvCacheFormat.F16);
        EvaluatedCandidate rejected = Candidate(
            kv: GgufKvCacheFormat.Q8_0, state: CompatibilityFitState.DoesNotFit);

        ModeSelectionOutcome outcome = Select([rejected, admitted]);

        Assert.IsTrue(outcome.Selections.All(selection =>
            selection.SelectedFingerprint?.Value == admitted.Fingerprint.Value));
    }
}
