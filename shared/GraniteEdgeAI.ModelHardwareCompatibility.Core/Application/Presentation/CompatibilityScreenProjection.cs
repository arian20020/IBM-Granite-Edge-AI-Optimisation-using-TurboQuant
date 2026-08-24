using System.Linq;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

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
        bool continueEnabled,
        CompatibilitySetupView? setup)
    {
        Setup = setup;
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

    /// <summary>
    /// The setup the screen is describing, or null when nothing was evaluated.
    ///
    /// This is the configuration a user would actually get if they continued:
    /// the balanced choice where one exists, and otherwise the best-fitting
    /// candidate found. A screen showing figures from a setup nobody would be
    /// given would be describing a decision that was never made.
    /// </summary>
    public CompatibilitySetupView? Setup { get; }

    /// <summary>
    /// Builds a screen model directly, for Debug fixtures and tests that need to
    /// render a state without an engine run behind it.
    ///
    /// This exists so all ten screens can be looked at and tested before the
    /// owner adapters land — otherwise nine of them would be unreachable and
    /// therefore unreviewable. It is not a way to fabricate a conclusion: what
    /// it produces is presentation input, never a run result, and nothing
    /// downstream can mistake one for the other.
    /// </summary>
    public static CompatibilityScreenModel ForPresentation(
        CompatibilityScreenState state,
        IReadOnlyList<CompatibilityFindingView> findings,
        IReadOnlyList<CompatibilityModeView> modes,
        BaselineExclusionReason baselineExclusionReason,
        bool useCurrentModelAvailable,
        bool continueEnabled,
        CompatibilitySetupView? setup = null)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(modes);

        if (state == CompatibilityScreenState.Unspecified)
        {
            throw new ArgumentException(
                "A screen model must name the state it renders.", nameof(state));
        }

        return new CompatibilityScreenModel(
            state,
            [.. findings],
            [.. modes],
            baselineExclusionReason,
            useCurrentModelAvailable,
            continueEnabled,
            setup);
    }

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
                or CompatibilityScreenState.OptimisationRequired,
            DescribeSetup(result.Assessment));
    }

    /// <summary>
    /// Picks the setup the screen speaks for and flattens it.
    ///
    /// Balanced is preferred because it is what a user who expresses no
    /// preference is given. Where no mode resolved, the best-fitting evaluated
    /// candidate stands in, so a screen saying nothing fits can still show how
    /// close the closest attempt came.
    /// </summary>
    private static CompatibilitySetupView? DescribeSetup(CompatibilityAssessment? assessment)
    {
        if (assessment is null || assessment.EvaluatedCandidates.Count == 0)
        {
            return null;
        }

        CandidateFingerprint? preferred = assessment.ModeSelections
            .FirstOrDefault(selection =>
                selection.Mode == CompatibilityMode.Balanced
                && selection.SelectedFingerprint is not null)
            ?.SelectedFingerprint
            ?? assessment.ModeSelections
                .FirstOrDefault(selection => selection.SelectedFingerprint is not null)
                ?.SelectedFingerprint;

        EvaluatedCandidate? chosen = preferred is { } fingerprint
            ? assessment.EvaluatedCandidates
                .FirstOrDefault(candidate => candidate.Fingerprint == fingerprint)
            : null;

        // Nothing was admitted, so the screen is explaining a refusal. The
        // candidate that came closest is the one worth showing, because it is
        // the one whose figures tell the user what would have to change.
        chosen ??= assessment.EvaluatedCandidates
            .OrderBy(candidate => Rank(candidate.Fit.State))
            .ThenBy(candidate => candidate.Fit.PressureRatio)
            .First();

        return Describe(chosen);
    }

    /// <summary>
    /// How close a fit state came to being usable, best first.
    ///
    /// Ranked explicitly rather than by the enum's own order, which is written
    /// for readability and would otherwise silently decide which setup a user
    /// is shown.
    /// </summary>
    /// <summary>
    /// The gap between what the components come to and what the fit policy
    /// demanded. Saturates at zero so a policy that stops adding a margin
    /// cannot produce a negative segment.
    /// </summary>
    private static ulong Allowance(EvaluatedCandidate candidate)
    {
        ulong peak = candidate.Peaks.SystemMemoryPressure.Bytes;
        ulong required = candidate.Fit.RequiredBytes.Bytes;

        return required > peak ? required - peak : 0;
    }

    private static int Rank(CompatibilityFitState state) => state switch
    {
        CompatibilityFitState.Safe => 0,
        CompatibilityFitState.Narrow => 1,
        CompatibilityFitState.DoesNotFit => 2,
        CompatibilityFitState.Unsupported => 3,
        _ => 4
    };

    private static CompatibilitySetupView Describe(EvaluatedCandidate candidate)
    {
        GgufRouteConfiguration? gguf = candidate.Candidate.Configuration as GgufRouteConfiguration;

        return new CompatibilitySetupView(
            candidate.Candidate.RouteId,
            gguf?.Backend ?? CompatibilityBackend.Unspecified,
            gguf?.Device ?? DeviceRouteId.Unspecified,
            candidate.EffectiveQuantisation,
            candidate.Context.Tokens,
            candidate.Fit.State,
            candidate.Fit.RequiredBytes.Bytes,
            candidate.Fit.SafeBudget.Bytes,
            candidate.Fit.Headroom.Bytes,
            Allowance(candidate),
            candidate.IsExperimental,
            candidate.Preparation == CandidatePreparation.WeightConversionRequired,
            BreakDown(candidate.Estimate.Components));
    }

    /// <summary>
    /// The components that are live at the moment memory pressure peaks.
    ///
    /// A component only counts when it is present in the phase that decided the
    /// peak, so these sum to the requirement rather than exceeding it. Listing
    /// every component regardless of phase would produce a breakdown larger
    /// than the total it claims to explain.
    /// </summary>
    private static IReadOnlyList<CompatibilityComponentView> BreakDown(
        IReadOnlyList<ResourceComponent> components)
    {
        ResourceTarget[] charged =
        [
            ResourceTarget.SystemMemory,
            ResourceTarget.SharedDeviceMemory
        ];

        LifecyclePhase[] phases =
        [
            LifecyclePhase.Load,
            LifecyclePhase.Compile,
            LifecyclePhase.SteadyStateGeneration
        ];

        LifecyclePhase peak = LifecyclePhase.SteadyStateGeneration;
        ulong largest = 0;

        foreach (LifecyclePhase phase in phases)
        {
            ulong total = 0;

            foreach (ResourceComponent component in components)
            {
                if (charged.Contains(component.Target) && component.Phases.Contains(phase))
                {
                    total += component.Bytes.Bytes;
                }
            }

            if (total > largest)
            {
                largest = total;
                peak = phase;
            }
        }

        return
        [
            .. components
                .Where(component =>
                    charged.Contains(component.Target) && component.Phases.Contains(peak))
                .GroupBy(component => component.Kind)
                .Select(group => new CompatibilityComponentView(
                    group.Key,
                    group.Aggregate(0UL, (sum, component) => sum + component.Bytes.Bytes)))
                .OrderByDescending(view => view.Bytes)
        ];
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
