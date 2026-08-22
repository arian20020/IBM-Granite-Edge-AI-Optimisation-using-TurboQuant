using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

/// <summary>
/// What a screen needs from a run, decided once and handed over whole.
///
/// This carries no wording, no colour and no layout — presentation owns all of
/// that. It carries the decision (which screen) and the codes that screen must
/// explain, so the view is a projection rather than a second round of reasoning
/// over the result.
/// </summary>
internal sealed record CompatibilityScreenModel
{
    private CompatibilityScreenModel(
        CompatibilityScreenState state,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<CompatibilityModeSelection> modeSelections,
        BaselineExclusionReason baselineExclusionReason,
        bool useCurrentModelAvailable,
        bool continueEnabled)
    {
        State = state;
        Findings = findings;
        ModeSelections = modeSelections;
        BaselineExclusionReason = baselineExclusionReason;
        UseCurrentModelAvailable = useCurrentModelAvailable;
        ContinueEnabled = continueEnabled;
    }

    internal CompatibilityScreenState State { get; }

    /// <summary>
    /// What the screen must explain, as codes. Screen 06 in particular has to
    /// list the exact missing evidence with recovery actions, and it can only do
    /// that if the engine hands over what was missing.
    /// </summary>
    internal IReadOnlyList<CompatibilityFinding> Findings { get; }

    /// <summary>
    /// All four modes, always. An unavailable mode is disabled with its reason
    /// rather than hidden — a hidden option looks like one that never existed.
    /// Empty only when no assessment was reached at all.
    /// </summary>
    internal IReadOnlyList<CompatibilityModeSelection> ModeSelections { get; }

    /// <summary>Why the user's current configuration is not offered, when it is not.</summary>
    internal BaselineExclusionReason BaselineExclusionReason { get; }

    internal bool UseCurrentModelAvailable { get; }

    /// <summary>
    /// Continue is enabled only from a state that concluded something the user
    /// can act on. Elsewhere it stays visible and disabled.
    /// </summary>
    internal bool ContinueEnabled { get; }

    internal static CompatibilityScreenModel From(CompatibilityRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        CompatibilityScreenState state = DecideState(result);

        return new CompatibilityScreenModel(
            state,
            result.Findings,
            result.Assessment?.ModeSelections ?? [],
            result.Assessment?.BaselineExclusionReason ?? BaselineExclusionReason.None,
            result.Assessment?.UseCurrentModelAvailable ?? false,
            state is CompatibilityScreenState.EstimatedCompatible
                or CompatibilityScreenState.OptimisationRequired);
    }

    private static CompatibilityScreenState DecideState(CompatibilityRunResult result)
    {
        if (result.Outcome == CompatibilityRunOutcome.Cancelled)
        {
            return CompatibilityScreenState.Cancelled;
        }

        // A failed run reached no conclusion, so it shows the same screen as one
        // that could not establish anything — the user's position is identical.
        if (result.Outcome is CompatibilityRunOutcome.NotEstablished
            or CompatibilityRunOutcome.Failed
            or CompatibilityRunOutcome.Unspecified
            || result.Assessment is not { } assessment)
        {
            return CompatibilityScreenState.NotEstablished;
        }

        IReadOnlyList<EvaluatedCandidate> admitted =
        [
            .. assessment.EvaluatedCandidates.Where(candidate =>
                candidate.Fit.State is CompatibilityFitState.Safe
                    or CompatibilityFitState.Narrow)
        ];

        if (admitted.Count == 0)
        {
            return CompatibilityScreenState.NoEstimatedSafeConfiguration;
        }

        // Narrow means it fits only after every mandatory margin, with little
        // room left. Saying so is the difference between a user proceeding
        // informed and proceeding surprised.
        return admitted.Any(candidate => candidate.Fit.State == CompatibilityFitState.Safe)
            ? CompatibilityScreenState.EstimatedCompatible
            : CompatibilityScreenState.OptimisationRequired;
    }
}
