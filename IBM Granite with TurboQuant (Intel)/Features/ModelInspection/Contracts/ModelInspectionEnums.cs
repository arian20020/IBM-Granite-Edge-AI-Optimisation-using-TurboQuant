namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Describes how the complete inspection use case ended.
/// </summary>
internal enum ModelInspectionExecutionStatus
{
    Completed,
    Cancelled,
    OperationalFailure
}

/// <summary>
/// Describes the model outcome produced after reliable evidence is classified.
/// </summary>
internal enum ModelInspectionOutcome
{
    Ready,
    ReadyWithWarnings,
    ConversionRequired,
    IncompletePackage,
    Unsupported,
    Invalid
}

/// <summary>
/// Identifies the five user-visible lightweight inspection stages.
/// </summary>
internal enum ModelInspectionStage
{
    CheckModelPackage = 1,
    ReadModelConfiguration = 2,
    ValidateTokenizerAndChatSetup = 3,
    ValidateModelStructure = 4,
    ConfirmCoreRuntimeCompatibility = 5
}

/// <summary>
/// Describes the truthful state reported for one inspection stage.
/// </summary>
internal enum ModelInspectionStageStatus
{
    Active,
    Completed,
    Warning,
    Failed,
    Cancelled
}

/// <summary>
/// Describes the user-facing importance of one classified finding.
/// </summary>
internal enum ModelInspectionFindingSeverity
{
    Information,
    Warning,
    Blocking
}
