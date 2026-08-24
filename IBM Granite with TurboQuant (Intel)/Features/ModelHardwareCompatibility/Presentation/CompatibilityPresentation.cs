using System.Collections.Generic;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;

/// <summary>
/// How an outcome card reads. The engine emits codes; this is where a code
/// becomes something a person can act on.
/// </summary>
internal enum CompatibilityOutcomeTone
{
    Neutral,
    Positive,
    Caution,
    Blocking
}

/// <summary>One tile in the dominant facts card.</summary>
internal sealed record CompatibilityFact(string Label, string Value, string Detail);

/// <summary>One line in a side mini-card, with an optional state pill.</summary>
internal sealed record CompatibilityRow(
    string Title,
    string Subtitle,
    string Value,
    CompatibilityOutcomeTone Tone,
    bool ShowPill);

/// <summary>
/// One recovery action offered alongside an outcome. Screen 06 in particular
/// must say what is missing *and* what to do about it, so a finding without a
/// recovery action is only half an answer.
/// </summary>
internal sealed record CompatibilityRecovery(string Title, string Detail);

/// <summary>
/// The compact, user-facing explanation of an evaluated peak-memory estimate.
/// Values remain bytes until the page formats them, so presentation never
/// reparses text or recalculates the compatibility result.
/// </summary>
internal sealed record CompatibilityEstimateSummary(
    ulong ModelWeightsBytes,
    ulong KvCacheBytes,
    ulong RuntimeAndBufferBytes,
    ulong MarginForErrorBytes,
    ulong EstimatedPeakBytes,
    ulong SafeMemoryBytes);

/// <summary>
/// Everything the page renders, resolved once per snapshot.
///
/// The page applies this as deltas to a stable control tree rather than
/// rebuilding, which is why every field is a value: two snapshots can be
/// compared cheaply to decide what actually changed.
/// </summary>
internal sealed record CompatibilityPresentation
{
    internal required string PageTitle { get; init; }

    internal required string PageLede { get; init; }

    internal required string ModelName { get; init; }

    internal required string ModelDetail { get; init; }

    internal required CompatibilityOutcomeTone Tone { get; init; }

    internal required string OutcomeTitle { get; init; }

    internal required string OutcomeDetail { get; init; }

    /// <summary>
    /// The short pill beside the outcome. Carries the estimated-versus-measured
    /// distinction, which the design puts here deliberately: a figure derived
    /// from documented defaults must never look like one that was observed.
    /// </summary>
    internal required string OutcomeBadge { get; init; }

    internal required IReadOnlyList<CompatibilityFact> Facts { get; init; }

    /// <summary>
    /// The memory picture: what this setup needs, against what it was allowed.
    /// Empty when no setup was evaluated, in which case the diagram is hidden
    /// rather than drawn at zero — an empty bar reads as a model that costs
    /// nothing, which is the opposite of "we could not work this out".
    /// </summary>
    internal required CompatibilityBudget Budget { get; init; }

    /// <summary>
    /// Null when no setup was evaluated. A missing estimate must remain absent
    /// rather than render as six zero-valued facts.
    /// </summary>
    internal required CompatibilityEstimateSummary? EstimateSummary { get; init; }

    internal required string RuntimeCardTitle { get; init; }

    internal required IReadOnlyList<CompatibilityRow> RuntimeRows { get; init; }

    internal required string ChecksCardTitle { get; init; }

    internal required IReadOnlyList<CompatibilityRow> CheckRows { get; init; }

    /// <summary>What is missing and what to do about it. Empty when nothing is.</summary>
    internal required IReadOnlyList<CompatibilityRecovery> Recoveries { get; init; }

    internal required string DisclosureTitle { get; init; }

    internal required string DisclosureDetail { get; init; }

    internal required string PrimaryActionText { get; init; }

    internal required bool PrimaryActionEnabled { get; init; }

    internal required string SecondaryActionText { get; init; }

    internal required bool SecondaryActionEnabled { get; init; }

    /// <summary>
    /// Zero-based index of the active step in the five-step stepper. The
    /// compatibility check is step three of the onboarding sequence.
    /// </summary>
    internal required int ActiveStepIndex { get; init; }

    /// <summary>
    /// A safe, non-committal default so a control can be constructed before any
    /// snapshot arrives without ever claiming a conclusion it does not have.
    /// </summary>
    internal static CompatibilityPresentation Empty { get; } = new()
    {
        PageTitle = "Model and hardware compatibility",
        PageLede = "Checking whether this model can run on this computer.",
        ModelName = string.Empty,
        ModelDetail = string.Empty,
        Tone = CompatibilityOutcomeTone.Neutral,
        OutcomeTitle = "Checking",
        OutcomeDetail = "This has not finished yet.",
        OutcomeBadge = string.Empty,
        Facts = [],
        Budget = CompatibilityBudget.Empty,
        EstimateSummary = null,
        RuntimeCardTitle = "Runtime",
        RuntimeRows = [],
        ChecksCardTitle = "Checks",
        CheckRows = [],
        Recoveries = [],
        DisclosureTitle = "How this was calculated",
        DisclosureDetail = string.Empty,
        PrimaryActionText = "Continue",
        PrimaryActionEnabled = false,
        SecondaryActionText = "Back",
        SecondaryActionEnabled = true,
        ActiveStepIndex = 2
    };
}
