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
        ulong headroom = 1_000_000,
        WeightQuantisation quantisation = WeightQuantisation.Q4_K_M)
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
            quantisation,
            evidence,
            PerformanceIndicator.NotEstablished(),
            ByteCount.Zero);
    }

    private static ModeSelectionOutcome Select(
        IReadOnlyList<EvaluatedCandidate> candidates,
        IReadOnlySet<string>? evidenceRequiringEntries = null) =>
        ModeSelector.SelectAll(ModeSelectionRequest.Create(
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
        EvaluatedCandidate preservedContext = Candidate(context: 4096);

        ModeSelectionOutcome outcome = Select(
            [Candidate(kv: GgufKvCacheFormat.Q8_0, context: 2048), preservedContext]);

        CompatibilityModeSelection quality =
            outcome.Selections.Single(s => s.Mode == CompatibilityMode.Quality);

        Assert.AreEqual(ModeAvailability.Available, quality.Availability);
        Assert.AreEqual(
            preservedContext.Fingerprint.Value,
            quality.SelectedFingerprint?.Value,
            "Quality must select the candidate that preserves the requested context.");
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
    public void SelectAll_ReportsTheSameStableReasonRegardlessOfCandidateOrder()
    {
        // A mixed failure set: one candidate refused for not fitting, the other
        // for evidence below the admission level. Reversing the input must not
        // change which reason is reported - the explanation shown to a user
        // cannot depend on the order candidates happened to arrive in.
        EvaluatedCandidate doesNotFit = Candidate(state: CompatibilityFitState.DoesNotFit);
        EvaluatedCandidate needsEvidence = Candidate(
            kv: GgufKvCacheFormat.Q8_0, requiresEvidence: true, evidence: EvidenceGrade.Estimated);

        IReadOnlySet<string> evidenceRequiring = new HashSet<string> { "needs-evidence" };

        ModeSelectionOutcome forward = Select([doesNotFit, needsEvidence], evidenceRequiring);
        ModeSelectionOutcome reversed = Select([needsEvidence, doesNotFit], evidenceRequiring);

        foreach (CompatibilityMode mode in new[]
        {
            CompatibilityMode.Automatic,
            CompatibilityMode.Quality,
            CompatibilityMode.Balanced,
            CompatibilityMode.Efficiency
        })
        {
            ModeAdmissionReason forwardReason =
                forward.Selections.Single(s => s.Mode == mode).Reason;
            ModeAdmissionReason reversedReason =
                reversed.Selections.Single(s => s.Mode == mode).Reason;

            Assert.AreEqual(
                ModeAdmissionReason.FitStateNotSafeOrNarrow,
                forwardReason,
                $"{mode} did not report the highest-precedence reason.");
            Assert.AreEqual(
                forwardReason,
                reversedReason,
                $"{mode} depended on candidate order for its reported reason.");
        }
    }

    // Proves ModeSelector actually threads the best admitted quality tier into
    // ModeComparers.For, rather than merely computing it and dropping it.
    // Candidate() previously hardcoded one quantisation for every candidate, so
    // this wiring could be deleted from ModeSelector without any test failing.
    //
    // Three admitted candidates, all preserving the requested context so the
    // first Balanced factor ties: bestFit at F16 (the best tier present, so
    // it is in band), oneTierLower at Q8_0 (adjacent to F16, so also in band)
    // with far more headroom, and twoTiersLower at Q6_K (two ladder positions
    // from F16, so out of band) with even more headroom still.
    //
    // With the band wired in, QualityBand outranks Headroom: the two in-band
    // candidates are compared on headroom before the out-of-band one is even
    // considered, so oneTierLower (more headroom, in band) beats both bestFit
    // (less headroom, in band) and twoTiersLower (out of band regardless of
    // its headroom). Without the wiring - bestAdmittedTier always null - every
    // candidate reads as out of band, QualityBand ties for all three, and the
    // winner would fall straight through to raw headroom, making
    // twoTiersLower win instead. That difference is what this test pins.
    [TestMethod]
    public void SelectAll_BalancedPrefersAnInBandLowerTierOverAHigherHeadroomOutOfBandOne()
    {
        EvaluatedCandidate bestFit = Candidate(
            kv: GgufKvCacheFormat.F16, quantisation: WeightQuantisation.F16, headroom: 1);

        EvaluatedCandidate oneTierLower = Candidate(
            kv: GgufKvCacheFormat.Q8_0, quantisation: WeightQuantisation.Q8_0, headroom: 100_000_000);

        EvaluatedCandidate twoTiersLower = Candidate(
            kv: GgufKvCacheFormat.TurboQuant3Bit,
            quantisation: WeightQuantisation.Q6_K,
            headroom: 1_000_000_000);

        ModeSelectionOutcome outcome = Select([bestFit, oneTierLower, twoTiersLower]);

        CompatibilityModeSelection balanced =
            outcome.Selections.Single(s => s.Mode == CompatibilityMode.Balanced);

        Assert.AreEqual(ModeAvailability.Available, balanced.Availability);
        Assert.AreEqual(
            oneTierLower.Fingerprint.Value,
            balanced.SelectedFingerprint?.Value,
            "Balanced must prefer the in-band, higher-headroom candidate over both the "
            + "best tier with less headroom and the out-of-band candidate with even more "
            + "headroom - proving the quality band ModeSelector computes is the one "
            + "actually driving the comparison.");
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
