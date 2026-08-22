using System.Linq;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

/// <summary>
/// One finding, flattened for a caller outside this assembly.
/// </summary>
public sealed record CompatibilityFindingView(
    CompatibilityFindingCode Code,
    FindingSeverity Severity);

/// <summary>
/// One mode's answer, flattened for a caller outside this assembly.
///
/// The ordered selection factors that decided it stay internal: they are
/// engine reasoning, and a screen that needed them would be re-deriving the
/// decision rather than presenting it.
/// </summary>
public sealed record CompatibilityModeView(
    CompatibilityMode Mode,
    ModeAvailability Availability,
    ModeAdmissionReason Reason,
    bool HasSelection);

/// <summary>
/// What a screen needs from a run, decided once and handed over whole.
///
/// This is the engine's entire public surface. Everything that produced it —
/// the estimator, the generator, the policies, the candidates — stays internal,
/// because a screen has no business reaching into any of it. What crosses the
/// boundary is a decision and the codes explaining it.
///
/// It carries no wording, no colour and no layout. Presentation owns all of
/// that, which is also what keeps a message the engine wrote from reaching a
/// user in a language they did not choose.
/// </summary>
public sealed record CompatibilityScreenModel
{
    private CompatibilityScreenModel(
        CompatibilityScreenState state,
        IReadOnlyList<CompatibilityFindingView> findings,
        IReadOnlyList<CompatibilityModeView> modes,
        BaselineExclusionReason baselineExclusionReason,
        bool useCurrentModelAvailable,
        bool continueEnabled)
    {
        State = state;
        Findings = findings;
        Modes = modes;
        BaselineExclusionReason = baselineExclusionReason;
        UseCurrentModelAvailable = useCurrentModelAvailable;
        ContinueEnabled = continueEnabled;
    }

    public CompatibilityScreenState State { get; }

    /// <summary>
    /// What the screen must explain. Screen 06 in particular has to list the
    /// exact missing evidence with recovery actions, and it can only do that if
    /// the engine hands over what was missing.
    /// </summary>
    public IReadOnlyList<CompatibilityFindingView> Findings { get; }

    /// <summary>
    /// All four modes, always. An unavailable mode is disabled with its reason
    /// rather than hidden — a hidden option looks like one that never existed.
    /// Empty only when no assessment was reached at all.
    /// </summary>
    public IReadOnlyList<CompatibilityModeView> Modes { get; }

    /// <summary>Why the user's current configuration is not offered, when it is not.</summary>
    public BaselineExclusionReason BaselineExclusionReason { get; }

    public bool UseCurrentModelAvailable { get; }

    /// <summary>
    /// Continue is enabled only from a state that concluded something the user
    /// can act on. Elsewhere it stays visible and disabled.
    /// </summary>
    public bool ContinueEnabled { get; }

    internal static CompatibilityScreenModel From(CompatibilityRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        CompatibilityScreenState state = DecideState(result);

        IReadOnlyList<CompatibilityFindingView> findings =
        [
            .. result.Findings.Select(finding =>
                new CompatibilityFindingView(finding.Code, finding.Severity))
        ];

        IReadOnlyList<CompatibilityModeView> modes =
        [
            .. (result.Assessment?.ModeSelections ?? []).Select(selection =>
                new CompatibilityModeView(
                    selection.Mode,
                    selection.Availability,
                    selection.Reason,
                    selection.SelectedFingerprint is not null))
        ];

        return new CompatibilityScreenModel(
            state,
            findings,
            modes,
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
