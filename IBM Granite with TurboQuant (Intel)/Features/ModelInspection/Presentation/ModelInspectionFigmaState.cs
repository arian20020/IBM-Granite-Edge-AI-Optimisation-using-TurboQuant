namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal enum ModelInspectionFigmaState
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
