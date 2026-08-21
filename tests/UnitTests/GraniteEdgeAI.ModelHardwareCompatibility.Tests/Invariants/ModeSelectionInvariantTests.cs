using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Properties of mode selection that survive any retuning of the orderings.
/// </summary>
[TestClass]
public sealed class ModeSelectionInvariantTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private static InspectedModelFacts Facts() =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 8192,
            fileType: 15,
            quantisationVersion: 2);

    private static GgufRouteConfiguration Baseline() =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);

    /// <summary>
    /// The whole spine: matrix, generation, estimation, fit, then selection.
    /// </summary>
    private static (ModeSelectionOutcome Outcome, IReadOnlyList<EvaluatedCandidate> Evaluated)
        RunSpine(ulong availableGibibytes)
    {
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

        CandidateGenerationResult generated = CandidateGenerator.Generate(
            CandidateGenerationRequest.Create(
                matrix,
                matrix.Entries.ToDictionary(
                    entry => entry.EntryId,
                    entry => entry.Level == SupportLevel.Experimental
                        ? InstallationState.VerifiedAndOptedIn
                        : InstallationState.InstalledAndVerified),
                Facts(),
                Baseline(),
                ContextTokenCount.FromTokens(4096),
                ContextTokenCount.FromTokens(4096),
                TrustedSourceAvailability.None()));

        AvailableResources available = AvailableResources.Create(
            ByteCount.FromBytes(availableGibibytes * Gibibyte),
            dedicatedDeviceMemory: ByteCount.FromBytes(8 * Gibibyte),
            storage: ByteCount.FromBytes(500 * Gibibyte),
            observedAtUtc: DateTimeOffset.UtcNow);

        List<EvaluatedCandidate> evaluated = [];

        foreach (CompatibilityCandidate candidate in generated.Candidates)
        {
            ResourceEstimate estimate = GgufResourceEstimator.Estimate(
                Facts(), candidate, EstimatorPolicy.ProvisionalV1());

            if (estimate.Status != EstimationStatus.Established)
            {
                continue;
            }

            ResourcePeakProfile peaks = ResourcePhaseComposer.Compose(estimate.Components);

            evaluated.Add(EvaluatedCandidate.Create(
                candidate,
                estimate,
                peaks,
                FitPolicy.Assess(peaks, available, SafetyPolicy.ProvisionalV1()),
                WeightQuantisation.Q4_K_M,
                EvidenceGrade.Estimated,
                PerformanceIndicator.NotEstablished(),
                peaks.PeakFor(ResourceTarget.Storage)));
        }

        HashSet<string> evidenceRequiring =
            [.. matrix.Entries.Where(entry => entry.RequiresEvidence)
                .Select(entry => entry.EntryId)];

        return (
            ModeSelector.SelectAll(ModeSelectionRequest.Create(
                evaluated,
                evidenceRequiring,
                ContextTokenCount.FromTokens(4096),
                Baseline())),
            evaluated);
    }

    [TestMethod]
    public void EveryModeIsAlwaysAccountedFor()
    {
        // At 64 GiB the support matrix's candidates fit comfortably, so every
        // mode must resolve to an actual selection. At 2 GiB none of them do,
        // so every mode must be unavailable for the specific reason that they
        // do not fit - not merely present with a null-fingerprint placeholder.
        (ModeSelectionOutcome ample, _) = RunSpine(64);

        Assert.AreEqual(4, ample.Selections.Count, "at 64 GiB");
        Assert.IsTrue(
            ample.Selections.All(s => s.Availability == ModeAvailability.Available
                && s.SelectedFingerprint is not null),
            "every mode must be available with a selection when memory is ample");

        (ModeSelectionOutcome scarce, _) = RunSpine(2);

        Assert.AreEqual(4, scarce.Selections.Count, "at 2 GiB");
        Assert.IsTrue(
            scarce.Selections.All(s => s.Availability == ModeAvailability.Unavailable
                && s.Reason == ModeAdmissionReason.FitStateNotSafeOrNarrow
                && s.SelectedFingerprint is null),
            "every mode must be unavailable for not fitting when memory is scarce");
    }

    [TestMethod]
    public void NoModeEverSelectsACandidateThatDoesNotFit()
    {
        // The one result that must never happen: a mode recommending a
        // configuration the machine cannot run.
        foreach (ulong memory in new ulong[] { 64, 16, 8, 4, 2 })
        {
            (ModeSelectionOutcome outcome, IReadOnlyList<EvaluatedCandidate> evaluated) =
                RunSpine(memory);

            foreach (CompatibilityModeSelection selection in outcome.Selections)
            {
                if (selection.SelectedFingerprint is not { } fingerprint)
                {
                    continue;
                }

                EvaluatedCandidate chosen = evaluated.Single(
                    candidate => candidate.Fingerprint.Value == fingerprint.Value);

                Assert.IsTrue(
                    chosen.Fit.State is CompatibilityFitState.Safe
                        or CompatibilityFitState.Narrow,
                    $"{selection.Mode} chose a {chosen.Fit.State} candidate at {memory} GiB.");
            }
        }
    }

    [TestMethod]
    public void NoModeEverSelectsAnEvidenceRequiringEntryWithoutEvidence()
    {
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();
        HashSet<string> requiring =
            [.. matrix.Entries.Where(entry => entry.RequiresEvidence)
                .Select(entry => entry.EntryId)];

        (ModeSelectionOutcome outcome, IReadOnlyList<EvaluatedCandidate> evaluated) =
            RunSpine(64);

        foreach (CompatibilityModeSelection selection in outcome.Selections)
        {
            if (selection.SelectedFingerprint is not { } fingerprint)
            {
                continue;
            }

            EvaluatedCandidate chosen = evaluated.Single(
                candidate => candidate.Fingerprint.Value == fingerprint.Value);

            Assert.IsFalse(
                requiring.Contains(chosen.Candidate.SupportEntryId),
                $"{selection.Mode} chose {chosen.Candidate.SupportEntryId}, which needs evidence.");
        }
    }

    [TestMethod]
    public void SelectionIsStableUnderCandidateReordering()
    {
        (ModeSelectionOutcome _, IReadOnlyList<EvaluatedCandidate> evaluated) = RunSpine(64);

        HashSet<string> requiring = [];

        ModeSelectionOutcome Run(IReadOnlyList<EvaluatedCandidate> order) =>
            ModeSelector.SelectAll(ModeSelectionRequest.Create(
                order, requiring, ContextTokenCount.FromTokens(4096), Baseline()));

        ModeSelectionOutcome forward = Run(evaluated);
        ModeSelectionOutcome reversed = Run([.. evaluated.Reverse()]);

        foreach (CompatibilityModeSelection selection in forward.Selections)
        {
            Assert.AreEqual(
                selection.SelectedFingerprint?.Value,
                reversed.Selections.Single(s => s.Mode == selection.Mode)
                    .SelectedFingerprint?.Value,
                $"{selection.Mode} depended on candidate order.");
        }
    }

    // AnUnavailableModeAlwaysNamesItsReason previously restated a guarantee
    // CompatibilityModeSelection.Unavailable already throws on (it cannot
    // construct an unavailable selection with ModeAdmissionReason.None), so the
    // assertion could never fail. EveryModeIsAlwaysAccountedFor above now
    // asserts the specific reason expected at 2 GiB, which subsumes it.

    [TestMethod]
    public void UseCurrentModelTracksTheBaselinesOwnFitAndNothingElse()
    {
        foreach (ulong memory in new ulong[] { 64, 2 })
        {
            (ModeSelectionOutcome outcome, IReadOnlyList<EvaluatedCandidate> evaluated) =
                RunSpine(memory);

            EvaluatedCandidate? baseline = evaluated.FirstOrDefault(
                candidate => candidate.Candidate.IsBaseline);

            bool expected = baseline is not null
                && baseline.Fit.State is CompatibilityFitState.Safe
                    or CompatibilityFitState.Narrow;

            Assert.AreEqual(expected, outcome.UseCurrentModelAvailable, $"at {memory} GiB");
        }
    }
}
