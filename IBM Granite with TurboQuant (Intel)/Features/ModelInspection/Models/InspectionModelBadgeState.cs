namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Identifies the badge displayed by the compact model card.
    /// </summary>
    public enum InspectionModelBadgeState
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
}
