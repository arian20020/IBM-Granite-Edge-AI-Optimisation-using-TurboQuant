namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.State;

public enum HardwareInspectionStage
{
    StartingHardwareInspection,
    ReadingProcessorInformation,
    ReadingSystemMemory,
    DetectingGraphicsHardware,
    CheckingLocalInferenceRuntimes,
    NormalisingHardwareInformation,
    CreatingHardwareReport,
}

public enum HardwareInspectionStageRowState
{
    Waiting,
    Active,
    Complete,
}

public enum HardwareInspectionPresentationKind
{
    InvalidHandoff,
    Active,
    Stopping,
    Completed,
    CompletedWithWarnings,
    FailedCriticalEvidence,
    FailedTransientOperation,
    FailedApplicationRepairRequired,
    Cancelled,
}

public enum HardwareInspectionFailureClass
{
    CriticalEvidence,
    TransientOperation,
    ApplicationRepairRequired,
}

public enum HardwareInspectionActionKind
{
    BackToModelInspection,
    CancelInspection,
    Stopping,
    ContinueToCompatibility,
    RunInspectionAgain,
    Back,
    TryAgain,
}
