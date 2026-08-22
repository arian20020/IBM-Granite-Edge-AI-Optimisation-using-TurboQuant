namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Contains the text, icon and visual tone of the inspection outcome banner.
    /// </summary>
    public sealed class InspectionOutcomePresentation
    {
        /// <summary>
        /// Provides a safe initial value while no outcome should be displayed.
        /// </summary>
        public static InspectionOutcomePresentation Hidden { get; } = new();

        /// <summary>
        /// Gets the semantic inspection outcome.
        /// </summary>
        public InspectionOutcomePresentationKind Kind { get; init; } =
            InspectionOutcomePresentationKind.Hidden;

        /// <summary>
        /// Gets the visual tone used by the outcome banner.
        /// </summary>
        public InspectionOutcomeTone Tone { get; init; } =
            InspectionOutcomeTone.Neutral;

        /// <summary>
        /// Gets the semantic vector glyph displayed by the outcome banner.
        /// </summary>
        public InspectionStatusGlyphKind GlyphKind { get; init; } =
            InspectionStatusGlyphKind.NotComplete;

        /// <summary>
        /// Gets the concise outcome heading.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets the plain-English explanation beneath the outcome heading.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Gets the descriptive name exposed to accessibility tools.
        /// </summary>
        public string AutomationName { get; init; } =
            "Model inspection outcome";
    }
}
