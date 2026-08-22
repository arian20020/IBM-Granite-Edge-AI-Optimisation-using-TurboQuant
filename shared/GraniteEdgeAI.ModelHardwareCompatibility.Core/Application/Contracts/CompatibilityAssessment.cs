using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// What a completed run concluded.
///
/// It already carries the four mode selections with their ordered selection
/// factors, so the later "choose optimisation mode" screen is a view over data
/// the engine produced rather than a second round of derivation.
/// </summary>
internal sealed record CompatibilityAssessment
{
    private CompatibilityAssessment(
        IReadOnlyList<EvaluatedCandidate> evaluatedCandidates,
        IReadOnlyList<CompatibilityModeSelection> modeSelections,
        CandidateFingerprint? baselineFingerprint,
        BaselineExclusionReason baselineExclusionReason,
        bool useCurrentModelAvailable)
    {
        EvaluatedCandidates = evaluatedCandidates;
        ModeSelections = modeSelections;
        BaselineFingerprint = baselineFingerprint;
        BaselineExclusionReason = baselineExclusionReason;
        UseCurrentModelAvailable = useCurrentModelAvailable;
    }

    internal IReadOnlyList<EvaluatedCandidate> EvaluatedCandidates { get; }

    internal IReadOnlyList<CompatibilityModeSelection> ModeSelections { get; }

    /// <summary>Null when the as-imported configuration was not among the candidates.</summary>
    internal CandidateFingerprint? BaselineFingerprint { get; }

    /// <summary>
    /// Why the as-imported configuration is absent, when it is. Spec section 10
    /// requires the exact reason be preserved rather than the baseline silently
    /// going missing — the recovery action differs per reason, so "install the
    /// backend", "opt in to the experimental route" and "lower the context" are
    /// not interchangeable.
    /// </summary>
    internal BaselineExclusionReason BaselineExclusionReason { get; }

    internal bool UseCurrentModelAvailable { get; }

    internal static CompatibilityAssessment Create(
        IReadOnlyList<EvaluatedCandidate> evaluatedCandidates,
        IReadOnlyList<CompatibilityModeSelection> modeSelections,
        CandidateFingerprint? baselineFingerprint,
        BaselineExclusionReason baselineExclusionReason,
        bool useCurrentModelAvailable)
    {
        ArgumentNullException.ThrowIfNull(evaluatedCandidates);
        ArgumentNullException.ThrowIfNull(modeSelections);

        // Checked mode by mode, not merely non-empty. "Every mode is accounted
        // for" is the promise a screen relies on to disable an option rather
        // than omit it, and a list carrying three of the four, or one of them
        // twice, would satisfy a count check while breaking exactly that
        // promise. A hidden option looks like one that never existed.
        CompatibilityMode[] required =
        [
            CompatibilityMode.Automatic,
            CompatibilityMode.Quality,
            CompatibilityMode.Balanced,
            CompatibilityMode.Efficiency
        ];

        foreach (CompatibilityMode mode in required)
        {
            int occurrences = modeSelections.Count(selection => selection.Mode == mode);

            if (occurrences != 1)
            {
                throw new ArgumentException(
                    $"{mode} is accounted for {occurrences} times; every mode must "
                    + "appear exactly once so a screen can disable an option rather "
                    + "than omit it.",
                    nameof(modeSelections));
            }
        }

        if (modeSelections.Count != required.Length)
        {
            throw new ArgumentException(
                "A selection was supplied for a mode that is not one of the four; it "
                + "would reach a screen as an option nobody defined.",
                nameof(modeSelections));
        }

        return new CompatibilityAssessment(
            [.. evaluatedCandidates],
            [.. modeSelections],
            baselineFingerprint,
            baselineExclusionReason,
            useCurrentModelAvailable);
    }
}
