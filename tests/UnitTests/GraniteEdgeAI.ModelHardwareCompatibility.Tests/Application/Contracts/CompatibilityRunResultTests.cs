using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Contracts;

[TestClass]
public sealed class CompatibilityRunResultTests
{
    private static readonly DateTimeOffset Started =
        new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Completed =
        new(2026, 8, 20, 12, 0, 5, TimeSpan.Zero);

    private static IReadOnlyList<PolicyIdentity> Policies() =>
    [
        PolicyIdentity.Create("estimator", "estimator-policy-v1", PolicyProvenance.Provisional)
    ];

    /// <summary>
    /// All four modes accounted for and none of them resolved. Assessments are
    /// required to name every mode, so a test fixture naming one would be
    /// building something the type refuses to produce.
    /// </summary>
    private static IReadOnlyList<CompatibilityModeSelection> AllModesUnresolved() =>
    [
        CompatibilityModeSelection.NotEstablished(CompatibilityMode.Automatic),
        CompatibilityModeSelection.NotEstablished(CompatibilityMode.Quality),
        CompatibilityModeSelection.NotEstablished(CompatibilityMode.Balanced),
        CompatibilityModeSelection.NotEstablished(CompatibilityMode.Efficiency)
    ];

    private static CompatibilityAssessment EmptyAssessment() =>
        CompatibilityAssessment.Create(
            [],
            AllModesUnresolved(),
            baselineFingerprint: null,
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false);

    [TestMethod]
    public void Completed_CarriesTheAssessment()
    {
        CompatibilityRunResult result = CompatibilityRunResult.Completed(
            CompatibilityRunId.New(), EmptyAssessment(), [], Policies(), Started, Completed);

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Completed), result.Outcome.ToString());
        Assert.IsNotNull(result.Assessment);
    }

    [TestMethod]
    public void Cancelled_RetainsNoAssessment()
    {
        // a cancelled run's partial work must not read as a conclusion
        CompatibilityRunResult result = CompatibilityRunResult.Cancelled(
            CompatibilityRunId.New(), [], Policies(), Started, Completed);

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Cancelled), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
    }

    [TestMethod]
    public void Failed_RetainsNoAssessmentAndNamesAFinding()
    {
        CompatibilityRunResult result = CompatibilityRunResult.Failed(
            CompatibilityRunId.New(),
            [CompatibilityFinding.Create(
                CompatibilityFindingCode.HandoffClaimFailed, FindingSeverity.Blocking)],
            Policies(),
            Started,
            Completed);

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Failed), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
        Assert.AreEqual(1, result.Findings.Count);
    }

    [TestMethod]
    public void Failed_RejectsAnEmptyFindingList()
    {
        // a failure nobody can explain cannot be shown to a user or acted on
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityRunResult.Failed(
                CompatibilityRunId.New(), [], Policies(), Started, Completed));
    }

    [TestMethod]
    public void NotEstablished_RetainsNoAssessmentAndNamesWhatWasMissing()
    {
        CompatibilityRunResult result = CompatibilityRunResult.NotEstablished(
            CompatibilityRunId.New(),
            [CompatibilityFinding.Create(
                CompatibilityFindingCode.ModelFactsUnavailable, FindingSeverity.Blocking)],
            Policies(),
            Started,
            Completed);

        Assert.AreEqual(
            nameof(CompatibilityRunOutcome.NotEstablished), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
        Assert.AreEqual(1, result.Findings.Count);
    }

    [TestMethod]
    public void NotEstablished_RejectsAnEmptyFindingList()
    {
        // Section 12: screen 06 must list the exact missing evidence. A silent
        // not-established tells the user nothing to act on
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityRunResult.NotEstablished(
                CompatibilityRunId.New(), [], Policies(), Started, Completed));
    }

    [TestMethod]
    public void Result_RejectsCompletionBeforeStart()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => CompatibilityRunResult.Cancelled(
                CompatibilityRunId.New(), [], Policies(), Completed, Started));
    }

    [TestMethod]
    public void Result_CopiesFindingsSoLaterMutationCannotChangeIt()
    {
        List<CompatibilityFinding> findings =
        [
            CompatibilityFinding.Create(
                CompatibilityFindingCode.UncalibratedEstimate, FindingSeverity.Information)
        ];

        CompatibilityRunResult result = CompatibilityRunResult.Completed(
            CompatibilityRunId.New(), EmptyAssessment(), findings, Policies(), Started, Completed);

        findings.Add(CompatibilityFinding.Create(
            CompatibilityFindingCode.NoSafeConfigurationFound, FindingSeverity.Warning));

        Assert.AreEqual(1, result.Findings.Count);
    }

    [TestMethod]
    public void Result_RequiresAtLeastOnePolicyIdentity()
    {
        // every figure rests on a versioned policy; a result that names none
        // cannot be audited
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityRunResult.Cancelled(
                CompatibilityRunId.New(), [], [], Started, Completed));
    }

    [TestMethod]
    public void Assessment_RejectsAnEmptyModeList()
    {
        // all four modes are always accounted for, even when unavailable
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityAssessment.Create(
                [], [], baselineFingerprint: null, BaselineExclusionReason.None, useCurrentModelAvailable: false));
    }

    [TestMethod]
    public void Assessment_RejectsAModeListMissingAMode()
    {
        // Non-empty is not the promise. A screen disables an option it was told
        // about and omits one it was not, so three of four reaches the user as
        // a choice that never existed rather than one they cannot take
        List<CompatibilityModeSelection> incomplete =
            [.. AllModesUnresolved().Where(
                selection => selection.Mode != CompatibilityMode.Efficiency)];

        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityAssessment.Create(
                [],
                incomplete,
                baselineFingerprint: null,
                BaselineExclusionReason.None,
                useCurrentModelAvailable: false));
    }

    [TestMethod]
    public void Assessment_RejectsTheSameModeTwice()
    {
        // Duplicated rather than missing: the count still looks right, and the
        // screen would offer one mode twice while another vanished
        List<CompatibilityModeSelection> duplicated =
        [
            CompatibilityModeSelection.NotEstablished(CompatibilityMode.Automatic),
            CompatibilityModeSelection.NotEstablished(CompatibilityMode.Automatic),
            CompatibilityModeSelection.NotEstablished(CompatibilityMode.Quality),
            CompatibilityModeSelection.NotEstablished(CompatibilityMode.Balanced)
        ];

        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityAssessment.Create(
                [],
                duplicated,
                baselineFingerprint: null,
                BaselineExclusionReason.None,
                useCurrentModelAvailable: false));
    }

    [TestMethod]
    public void Assessment_CopiesItsCollections()
    {
        // both collections, not one of the pair. a copy test that exercises the
        // first argument and assumes the second proves nothing about the
        // second, and the two are copied by separate expressions
        List<EvaluatedCandidate> candidates = [];
        List<CompatibilityModeSelection> modes = [.. AllModesUnresolved()];

        CompatibilityAssessment assessment = CompatibilityAssessment.Create(
            candidates,
            modes,
            baselineFingerprint: null,
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false);

        int candidateCount = assessment.EvaluatedCandidates.Count;
        int modeCount = assessment.ModeSelections.Count;

        modes.Add(CompatibilityModeSelection.NotEstablished(CompatibilityMode.Quality));

        Assert.AreEqual(modeCount, assessment.ModeSelections.Count);
        Assert.AreEqual(candidateCount, assessment.EvaluatedCandidates.Count);
    }
}
