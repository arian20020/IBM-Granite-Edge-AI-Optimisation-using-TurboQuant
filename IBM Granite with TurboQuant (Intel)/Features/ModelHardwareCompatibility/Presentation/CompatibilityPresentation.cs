using System.Collections.Generic;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

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

/// <summary>
/// Closed secondary-action authority. Copy and command are carried together so
/// a button can never say Cancel while navigating Back.
/// </summary>
internal enum CompatibilitySecondaryActionKind
{
    None,
    Back,
    Cancel,
    Retry
}

/// <summary>Closed authority for the optional forward action slot.</summary>
internal enum CompatibilityForwardActionKind
{
    None,
    ImportAnotherModel
}

/// <summary>
/// Typed authority for showing memory-recovery controls. A screen state alone
/// is insufficient: a no-safe result can arise from a non-memory constraint.
/// </summary>
internal enum CompatibilityMemoryRecoveryReason
{
    None,
    SystemMemoryPressure
}

/// <summary>one tile in the dominant facts card</summary>
internal sealed record CompatibilityFact(string Label, string Value, string Detail);

/// <summary>one line in a side mini-card, with an optional state pill</summary>
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
/// the compact, user-facing explanation of an evaluated peak-memory estimate.
/// values remain bytes until the page formats them, so presentation never
/// reparses text or recalculates the compatibility result
/// </summary>
internal sealed record CompatibilityEstimateSummary(
    ulong ModelWeightsBytes,
    ulong KvCacheBytes,
    ulong RuntimeAndBufferBytes,
    ulong MarginForErrorBytes,
    ulong EstimatedPeakBytes,
    ulong SafeMemoryBytes);

/// <summary>the machine-memory values behind one established evaluation</summary>
internal sealed record CompatibilityMachineMemoryPresentation(
    IReadOnlyList<CompatibilityFact> Facts);

/// <summary>
/// exact memory evidence used only to explain an established no-fit result.
/// these values are projected from one evaluation and never inferred from copy
/// </summary>
internal sealed record CompatibilityMemoryClarityPresentation(
    ulong AvailableSystemMemoryBytes,
    ulong SafetyReserveBytes,
    ulong SafeModelBudgetBytes,
    ulong CurrentRequiredBytes,
    ulong SmallestOptimizedRequiredBytes,
    ulong MinimumFreeToOptimizeBytes,
    ulong AdditionalFreeRequiredBytes,
    int ContextTokens);

/// <summary>
/// Same-evaluation system/shared-memory evidence for an established choice
/// screen. A nullable minimum remains absent when neither the engine nor the
/// offered acceptable modes support one.
/// </summary>
internal sealed record CompatibilityMemoryOverviewPresentation(
    ulong AvailableSystemMemoryBytes,
    ulong SafetyReserveBytes,
    ulong SafeModelBudgetBytes,
    ulong CurrentRequiredBytes,
    int ContextTokens,
    ulong? MinimumRequiredBytes,
    string? MinimumRequirementLabel);

/// <summary>
/// Exact persistent-storage evidence for an optimisation that fits the current
/// safe RAM budget but cannot yet be created on this computer.
/// </summary>
internal sealed record CompatibilityStorageShortagePresentation(
    ulong RequiredBytes,
    ulong AvailableBytes,
    ulong AdditionalRequiredBytes);

/// <summary>one route-specific, already-resolved optimisation choice</summary>
internal sealed record CompatibilityOptimizationModePresentation(
    string Label,
    int? SliderValue,
    string ExpectedQualityText,
    string WeightFormat,
    string CacheFormat,
    string ContextText,
    string SystemSharedRequirementText,
    string SystemSharedBudgetText,
    string SystemSharedHeadroomText,
    string DedicatedRequirementText,
    string DedicatedBudgetText,
    string DedicatedHeadroomText,
    bool IsExperimental,
    bool HasStrongQualityWarning,
    string WarningText,
    bool RequiresPersistentArtifact,
    bool RequiresRequantisationAcknowledgement);

internal enum CompatibilityExactOptimizationAvailability
{
    Released,
    ExperimentalPreview
}

/// <summary>
/// One exact setup supplied by the authoritative planning projection. Released
/// entries are executable when the normal action gates pass. Experimental
/// preview entries still require evidence consent, fresh reevaluation and final
/// confirmation. The opaque identity is only a retained selection key.
/// </summary>
internal sealed record CompatibilityExactOptimizationModePresentation(
    string CandidateIdentity,
    CompatibilityOptimizationModePresentation Mode,
    CompatibilityExactOptimizationAvailability Availability);

/// <summary>
/// rendering-only optimisation state. it is flattened from the core projection;
/// the page never asks the candidate resolver to run again
/// </summary>
internal sealed record CompatibilityOptimizationPresentation(
    string Instruction,
    OptimizationRoute Route,
    IReadOnlyList<CompatibilityOptimizationModePresentation> Modes,
    CompatibilityOptimizationModePresentation SelectedMode,
    bool IsAutomatic,
    int SliderValue,
    string CurrentWeightFormat,
    string CurrentCacheFormat,
    string CurrentContextText,
    string CurrentSystemSharedRequirementText,
    string CurrentSystemSharedBudgetText,
    string CurrentSystemSharedHeadroomText,
    string CurrentDedicatedRequirementText,
    string CurrentDedicatedBudgetText,
    string CurrentDedicatedHeadroomText,
    bool IsActionAuthoritative,
    IReadOnlyList<CompatibilityExactOptimizationModePresentation> ExactSafeModes,
    bool HasAdditionalExactSafeModes,
    IReadOnlyList<CompatibilityExactOptimizationModePresentation> SafeSliderModes,
    int? SafeSliderSelectedIndex,
    string SelectionStatusText,
    OptimizationPreferenceSelection Preference);

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

    internal required CompatibilityMachineMemoryPresentation? MachineMemory
        { get; init; }

    internal required CompatibilityMemoryClarityPresentation? MemoryClarity
        { get; init; }

    internal CompatibilityMemoryOverviewPresentation? MemoryOverview
        { get; init; }

    internal CompatibilityStorageShortagePresentation? StorageShortage
        { get; init; }

    internal required CompatibilityOptimizationPresentation? Optimization { get; init; }

    internal required string RuntimeCardTitle { get; init; }

    internal required IReadOnlyList<CompatibilityRow> RuntimeRows { get; init; }

    internal required string ChecksCardTitle { get; init; }

    internal required IReadOnlyList<CompatibilityRow> CheckRows { get; init; }

    /// <summary>What is missing and what to do about it. Empty when nothing is.</summary>
    internal required IReadOnlyList<CompatibilityRecovery> Recoveries { get; init; }

    internal required CompatibilityMemoryRecoveryReason MemoryRecoveryReason { get; init; }

    internal required string DisclosureTitle { get; init; }

    internal required string DisclosureDetail { get; init; }

    internal required string PrimaryActionText { get; init; }

    internal required bool PrimaryActionEnabled { get; init; }

    internal required string SecondaryActionText { get; init; }

    internal required bool SecondaryActionEnabled { get; init; }

    internal required CompatibilitySecondaryActionKind SecondaryActionKind { get; init; }

    internal CompatibilityForwardActionKind ForwardActionKind { get; init; }

    /// <summary>
    /// View-only copy for the optional GGUF configuration entry point. Command
    /// authority remains with the view model; visibility never grants it.
    /// </summary>
    internal bool ShowOptimiseFurtherAction { get; init; }

    internal string OptimiseFurtherActionText { get; init; } = string.Empty;

    /// <summary>
    /// Optional route-specific copy for the existing current-model chat action.
    /// An empty value preserves the established OpenVINO wording.
    /// </summary>
    internal string CurrentModelChatActionText { get; init; } = string.Empty;

    /// <summary>
    /// Zero-based index of the active step in the five-step stepper. The
    /// compatibility check is step three of the onboarding sequence.
    /// </summary>
    internal required int ActiveStepIndex { get; init; }

    /// <summary>
    /// a safe, non-committal default so a control can be constructed before any
    /// snapshot arrives without ever claiming a conclusion it does not have
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
        MachineMemory = null,
        MemoryClarity = null,
        MemoryOverview = null,
        Optimization = null,
        RuntimeCardTitle = "Runtime",
        RuntimeRows = [],
        ChecksCardTitle = "Checks",
        CheckRows = [],
        Recoveries = [],
        MemoryRecoveryReason = CompatibilityMemoryRecoveryReason.None,
        DisclosureTitle = "How this was calculated",
        DisclosureDetail = string.Empty,
        PrimaryActionText = "Continue",
        PrimaryActionEnabled = false,
        SecondaryActionText = "Back",
        SecondaryActionEnabled = true,
        SecondaryActionKind = CompatibilitySecondaryActionKind.Back,
        ForwardActionKind = CompatibilityForwardActionKind.None,
        ShowOptimiseFurtherAction = false,
        OptimiseFurtherActionText = string.Empty,
        CurrentModelChatActionText = string.Empty,
        ActiveStepIndex = 2
    };
}
