namespace GraniteEdgeAI.ModelInspection.Fixtures;

public enum ModelInspectionFixtureCategory
{
    Screen,
    Progress,
    Lifecycle,
    Failure,
    Stress
}

public enum ModelInspectionFixtureFigmaState
{
    InspectionProgress = 1,
    ReadyCollapsed = 2,
    ReadyExpanded = 3,
    ReadyWithWarningsCollapsed = 4,
    ReadyWithWarningsExpanded = 5,
    ConversionRequiredCollapsed = 6,
    ConversionRequiredExpanded = 7,
    IncompletePackage = 8,
    Unsupported = 9,
    InvalidCollapsed = 10,
    InvalidExpanded = 11,
    Cancelled = 12,
    OperationalFailure = 13
}

public enum ModelInspectionFixtureStage
{
    CheckModelPackage = 1,
    ReadModelConfiguration = 2,
    ValidateTokenizerAndChatSetup = 3,
    ValidateModelStructure = 4,
    ConfirmCoreRuntimeCompatibility = 5
}

public enum ModelInspectionFixtureStageStatus
{
    Active,
    Completed,
    Warning,
    Failed,
    Cancelled
}

public enum ModelInspectionFixtureOutcome
{
    Ready,
    ReadyWithWarnings,
    ConversionRequired,
    IncompletePackage,
    Unsupported,
    Invalid
}

public enum ModelInspectionFixtureEvidenceProfile
{
    Compatible,
    CompatibleMissingOptionalMetadata,
    MissingChatTemplate,
    VerifiedIncompatible,
    MissingPackageMember,
    UnsupportedArchitecture,
    CrossSourceContradiction
}

public enum ModelInspectionFixtureFailureProfile
{
    WorkerStartFailure,
    WorkerTimeout,
    WorkerCrashEarlyExit,
    MalformedWorkerResponse,
    CancellationUnconfirmed
}

public enum ModelInspectionFixtureProgressDetailProfile
{
    Default,
    Maximum
}

public enum ModelInspectionFixtureLifecycleTag
{
    CancellationRequested,
    CooperativeCancellation,
    ForcedCancellation,
    RetryAfterCancellation,
    RetryAfterOperationalFailure,
    StaleProgressRejected,
    StaleResultRejected,
    StaleMotionRejected,
    StaleAnnouncementRejected,
    ChooseAnotherRetired,
    GallerySwitchRetired
}

public enum ModelInspectionFixtureStressTag
{
    MaximumModelName,
    MissingOptionalMetadata,
    MaximumCheckRows,
    MaximumFindingRows,
    MaximumReportRows,
    MaximumDetailCopy
}

public enum ModelInspectionFixtureInteractionKind
{
    Expand,
    Collapse,
    Cancel,
    Retry,
    Restart,
    ChooseAnother,
    Reset
}

public enum ModelInspectionFixtureInteractionLifetimeEffect
{
    None,
    RetirePage,
    NoActiveFixture
}

public enum ModelInspectionFixtureServiceTriggerKind
{
    Automatic,
    Checkpoint
}

public enum ModelInspectionFixtureServiceEffectKind
{
    Progress,
    Completed,
    Cancelled,
    OperationalFailure,
    DeferStaleProgress,
    DeferStaleResultSnapshot,
    DeferStaleMotion,
    DeferStaleAnnouncement
}

public enum ModelInspectionFixtureSetupStepKind
{
    ReleaseServiceCheckpoint,
    InvokeDisclosure,
    InvokeCancel,
    InvokeRetry,
    InvokeRestart,
    InvokeChooseAnother,
    ReleaseStaleProgress,
    SubmitStaleResultSnapshot,
    ReleaseStaleMotion,
    ReleaseStaleAnnouncement,
    Observe
}

public enum ModelInspectionExpectedFigmaState
{
    InspectionProgress = 1,
    ReadyCollapsed = 2,
    ReadyExpanded = 3,
    ReadyWithWarningsCollapsed = 4,
    ReadyWithWarningsExpanded = 5,
    ConversionRequiredCollapsed = 6,
    ConversionRequiredExpanded = 7,
    IncompletePackage = 8,
    Unsupported = 9,
    InvalidCollapsed = 10,
    InvalidExpanded = 11,
    Cancelled = 12,
    OperationalFailure = 13
}

public enum ModelInspectionExpectedGeometryProfile
{
    Canonical,
    Desktop,
    Medium,
    Narrow
}

public enum ModelInspectionExpectedOutcomeKind
{
    Hidden,
    Ready,
    ReadyWithWarnings,
    ConversionRequired,
    IncompletePackage,
    Unsupported,
    Invalid,
    Cancelled,
    OperationalFailure
}

public enum ModelInspectionExpectedOutcomeTone
{
    Success,
    Warning,
    Information,
    Error,
    Neutral
}

public enum ModelInspectionExpectedModelMode
{
    Compact,
    Detailed
}

public enum ModelInspectionExpectedModelBadge
{
    ModelSelected,
    Inspected,
    SourceModel,
    Incomplete,
    Unsupported,
    Invalid,
    NotInspected,
    ResultUnknown
}

public enum ModelInspectionExpectedContentMode
{
    Hidden,
    Progress,
    Warnings,
    ConversionRequired,
    IncompletePackage,
    Unsupported,
    Invalid,
    Cancelled,
    OperationalFailure
}

public enum ModelInspectionExpectedActionMode
{
    Hidden,
    Inspecting,
    Result
}

public enum ModelInspectionExpectedFooterStatus
{
    InProgress,
    Complete,
    NotComplete,
    Interrupted
}

public enum ModelInspectionExpectedRowStatus
{
    Neutral,
    Waiting,
    Active,
    Passed,
    Warning,
    Error,
    Information
}

public enum ModelInspectionExpectedStage
{
    CheckModelPackage = 1,
    ReadModelConfiguration = 2,
    ValidateTokenizerAndChatSetup = 3,
    ValidateModelStructure = 4,
    ConfirmCoreRuntimeCompatibility = 5
}

public enum ModelInspectionExpectedControlType
{
    Button,
    Text,
    List,
    ListItem,
    ProgressBar,
    Group
}

public enum ModelInspectionExpectedLiveSetting
{
    Off,
    Polite,
    Assertive
}

public enum ModelInspectionFixtureResponsiveLayout
{
    Desktop,
    Medium,
    Narrow
}

public enum ModelInspectionFixtureWidthProfile
{
    Desktop1440,
    Medium600,
    Narrow360
}

public enum ModelInspectionFixtureResourceProfile
{
    Light,
    Dark,
    HighContrastPreview
}

public enum ModelInspectionFixtureTextProfile
{
    Standard100,
    Preview200
}

public enum ModelInspectionFixtureMotionProfile
{
    Normal,
    Reduced
}

public enum ModelInspectionFixtureTextBehavior
{
    Wrap,
    Truncate
}
