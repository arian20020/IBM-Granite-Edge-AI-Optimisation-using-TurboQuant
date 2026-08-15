using System.Text.Json.Serialization;

namespace GraniteEdgeAI.ModelInspection.Fixtures;

public sealed record ModelInspectionFixtureDescriptor(
    [property: JsonPropertyName("$schema")] string Schema,
    int SchemaVersion,
    string Id,
    string TargetCondition,
    string? Variant,
    string Title,
    ModelInspectionFixtureCategory Category,
    ModelInspectionFixtureCoverage Coverage,
    ModelInspectionFixtureInput Input,
    ModelInspectionExpectedScreen Expected,
    IReadOnlyDictionary<string, ModelInspectionPresetExpectation> PresetExpectations,
    IReadOnlyList<ModelInspectionFixtureInteraction> Interactions,
    IReadOnlyList<string> Presets);

public sealed record ModelInspectionFixtureCoverage(
    IReadOnlyList<ModelInspectionFixtureFigmaState> FigmaStates,
    IReadOnlyList<ModelInspectionFixtureStage> Stages,
    IReadOnlyList<ModelInspectionFixtureStageStatus> StageStatuses,
    IReadOnlyList<ModelInspectionFixtureOutcome> Outcomes,
    IReadOnlyList<ModelInspectionFixtureInteractionKind> Interactions,
    IReadOnlyList<ModelInspectionFixtureLifecycleTag> LifecycleTags,
    IReadOnlyList<ModelInspectionFixtureFailureProfile> FailureProfiles,
    IReadOnlyList<ModelInspectionFixtureStressTag> StressTags);

public sealed record ModelInspectionFixtureInput(
    ModelInspectionFixtureRequestDescriptor Request,
    IReadOnlyList<ModelInspectionFixtureAttemptDescriptor> Attempts,
    IReadOnlyList<ModelInspectionFixtureSetupStepDescriptor> SetupSteps,
    string ObservationCheckpoint);

public sealed record ModelInspectionFixtureRequestDescriptor(
    string DisplayName,
    string DisplayFileName,
    ModelInspectionFixtureEvidenceProfile EvidenceProfile);

public sealed record ModelInspectionFixtureAttemptDescriptor(
    int Attempt,
    IReadOnlyList<ModelInspectionFixtureServiceStepDescriptor> ServiceSteps);

public sealed record ModelInspectionFixtureServiceStepDescriptor(
    ModelInspectionFixtureServiceTriggerDescriptor Trigger,
    ModelInspectionFixtureServiceEffectDescriptor Effect);

public sealed record ModelInspectionFixtureServiceTriggerDescriptor(
    ModelInspectionFixtureServiceTriggerKind Kind,
    string? Checkpoint);

public sealed record ModelInspectionFixtureServiceEffectDescriptor(
    ModelInspectionFixtureServiceEffectKind Kind,
    ModelInspectionFixtureProgressDescriptor? Progress,
    ModelInspectionFixtureOutcome? Outcome,
    ModelInspectionFixtureEvidenceProfile? EvidenceProfile,
    ModelInspectionFixtureFailureProfile? FailureProfile,
    ModelInspectionFixtureFailureDetailProfile? FailureDetailProfile,
    string? DeferredCheckpoint);

public sealed record ModelInspectionFixtureProgressDescriptor(
    ModelInspectionFixtureStage Stage,
    ModelInspectionFixtureStageStatus Status,
    int CompletedStageCount,
    double? Fraction,
    ModelInspectionFixtureProgressDetailProfile DetailProfile);

public sealed record ModelInspectionFixtureSetupStepDescriptor(
    ModelInspectionFixtureSetupStepKind Kind,
    int? Attempt,
    string? Checkpoint,
    string? InteractionId);

public sealed record ModelInspectionFixtureInteraction(
    string Id,
    ModelInspectionFixtureInteractionKind Kind,
    string SourceCheckpoint,
    string Target,
    string? ExpectedFocus,
    int ExpectedAnnouncementCount,
    ModelInspectionExpectedFooterStatus ExpectedFooterStatus,
    ModelInspectionFixtureInteractionLifetimeEffect LifetimeEffect);

public sealed record ModelInspectionExpectedScreen(
    ModelInspectionExpectedFigmaGeometry Figma,
    ModelInspectionExpectedOutcomeRegion Outcome,
    ModelInspectionExpectedModelRegion Model,
    ModelInspectionExpectedContentRegion Content,
    ModelInspectionExpectedActionRegion Actions,
    ModelInspectionExpectedFooter Footer,
    ModelInspectionExpectedFocus Focus,
    ModelInspectionExpectedAutomation Automation,
    ModelInspectionExpectedAnnouncements Announcements,
    ModelInspectionExpectedRowsAndScroll RowsAndScroll,
    ModelInspectionExpectedRetainedIdentities RetainedIdentities);

public sealed record ModelInspectionExpectedFigmaGeometry(
    ModelInspectionExpectedFigmaState State,
    ModelInspectionExpectedGeometryProfile GeometryProfile);

public sealed record ModelInspectionExpectedCopy(
    string CopyKey,
    string DefaultText);

public sealed record ModelInspectionExpectedOutcomeRegion(
    bool Visible,
    ModelInspectionExpectedOutcomeKind Kind,
    ModelInspectionExpectedOutcomeTone Tone,
    ModelInspectionExpectedCopy? Badge,
    ModelInspectionExpectedCopy? Title,
    ModelInspectionExpectedCopy? SupportingText);

public sealed record ModelInspectionExpectedModelRegion(
    bool Visible,
    ModelInspectionExpectedModelMode Mode,
    ModelInspectionExpectedModelBadge? Badge,
    ModelInspectionExpectedCopy DisplayName,
    ModelInspectionExpectedCopy DisplayFileName,
    IReadOnlyList<ModelInspectionExpectedMetadataField> Metadata,
    IReadOnlyList<ModelInspectionExpectedCheckRow> Checks,
    bool DisclosureExpanded);

public sealed record ModelInspectionExpectedMetadataField(
    string Id,
    ModelInspectionExpectedCopy Label,
    ModelInspectionExpectedCopy Value);

public sealed record ModelInspectionExpectedCheckRow(
    string Id,
    ModelInspectionExpectedCopy Text,
    ModelInspectionExpectedRowStatus Status);

public sealed record ModelInspectionExpectedContentRegion(
    bool Visible,
    ModelInspectionExpectedContentMode Mode,
    ModelInspectionExpectedCopy? Heading,
    IReadOnlyList<ModelInspectionExpectedContentRow> Rows,
    bool DisclosureExpanded,
    ModelInspectionExpectedCopy? StartupStatus = null,
    bool? StartupVisible = null,
    bool? StartupActive = null);

public sealed record ModelInspectionExpectedContentRow(
    string Id,
    ModelInspectionExpectedCopy PrimaryText,
    ModelInspectionExpectedCopy? SecondaryText,
    ModelInspectionExpectedRowStatus Status);

public sealed record ModelInspectionExpectedActionRegion(
    bool Visible,
    ModelInspectionExpectedActionMode Mode,
    IReadOnlyList<ModelInspectionExpectedAction> Items);

public sealed record ModelInspectionExpectedAction(
    string Id,
    ModelInspectionExpectedCopy Label,
    bool Visible,
    bool Enabled,
    ModelInspectionExpectedCopy? HelpText);

public sealed record ModelInspectionExpectedFooter(
    ModelInspectionExpectedFooterStatus Status);

public sealed record ModelInspectionExpectedFocus(string Target);

public sealed record ModelInspectionExpectedAutomation(
    IReadOnlyList<ModelInspectionExpectedAutomationControl> Controls);

public sealed record ModelInspectionExpectedAutomationControl(
    string Id,
    ModelInspectionExpectedCopy AccessibleName,
    ModelInspectionExpectedControlType ControlType,
    ModelInspectionExpectedLiveSetting LiveSetting,
    ModelInspectionExpectedCopy? HelpText);

public sealed record ModelInspectionExpectedAnnouncements(
    int Count,
    IReadOnlyList<ModelInspectionExpectedCopy> Items);

public sealed record ModelInspectionExpectedRowsAndScroll(
    IReadOnlyList<string> OrderedRowIds,
    string? ScrollOwner);

public sealed record ModelInspectionExpectedRetainedIdentities(
    IReadOnlyList<string> Ids);

public sealed record ModelInspectionPresetExpectation(
    ModelInspectionFixtureResponsiveLayout ResponsiveLayout,
    double MinimumContentColumnWidth,
    double MaximumContentColumnWidth,
    bool NoClipping,
    bool NoOverlap,
    bool AllRequiredContentReachable,
    IReadOnlyList<ModelInspectionExpectedTextRole> TextRoles,
    string? ScrollOwner,
    double MinimumPointerTargetWidth,
    double MinimumPointerTargetHeight,
    IReadOnlyList<string> LogicalReadingOrder,
    IReadOnlyList<string> TabOrder,
    string FocusTarget,
    ModelInspectionFixtureResourceProfile Resources,
    ModelInspectionFixtureTextProfile TextScale,
    ModelInspectionFixtureMotionProfile Motion,
    bool SemanticBrushesResolvedWithoutColorOnlyMeaning,
    bool FinalGeometryAndSemanticsEquivalentToNormalMotion,
    int MinimumAnimationStarts,
    int MaximumAnimationStarts);

public sealed record ModelInspectionExpectedTextRole(
    string Id,
    ModelInspectionFixtureTextBehavior Behavior);
