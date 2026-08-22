namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Contains the display data for one inspection-check row.
    /// </summary>
    public sealed class InspectionCheckPresentation
    {
        /// <summary>
        /// Gets the name of the inspection check.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets the concise evidence or result explanation.
        /// </summary>
        public string Detail { get; init; } = string.Empty;

        /// <summary>
        /// Gets the semantic status of this check.
        /// </summary>
        public InspectionCheckStatus Status { get; init; } =
            InspectionCheckStatus.Information;

        /// <summary>
        /// Gets the explicit user-facing status text.
        /// </summary>
        public string StatusText { get; init; } = string.Empty;

        /// <summary>
        /// Gets the complete accessible description of this check.
        /// </summary>
        public string AutomationName { get; init; } = string.Empty;
    }
}
