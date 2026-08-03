namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Identifies the semantic model-inspection outcome being presented.
    /// </summary>
    public enum InspectionOutcomePresentationKind
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
}
