namespace GraniteEdgeAI.Features.HardwareInspection.Application;

public enum HardwareInspectionRunStage
{
    StartingHardwareInspection,
    ReadingProcessorInformation,
    ReadingSystemMemory,
    DetectingGraphicsHardware,
    CheckingLocalInferenceRuntimes,
    NormalisingHardwareInformation,
    CreatingHardwareReport,
}
