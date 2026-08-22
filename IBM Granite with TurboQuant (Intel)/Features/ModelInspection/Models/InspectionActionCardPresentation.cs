namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Contains the complete display state of the inspection action card.
    /// </summary>
    public sealed class InspectionActionCardPresentation
    {
        /// <summary>
        /// Provides a safe initial value while no action card should be displayed.
        /// </summary>
        public static InspectionActionCardPresentation Hidden { get; } = new();

        /// <summary>
        /// Gets the structural layout used by the card.
        /// </summary>
        public InspectionActionCardMode Mode { get; init; } =
            InspectionActionCardMode.Hidden;

        /// <summary>
        /// Gets the centred result-state heading.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets the supporting explanation shown beneath the heading or cancel action.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Gets the descriptive name exposed for the complete card.
        /// </summary>
        public string AutomationName { get; init; } =
            "Model inspection actions";

        /// <summary>
        /// Gets the cancellation action used while inspection is running.
        /// </summary>
        public InspectionActionPresentation CancelAction { get; init; } =
            InspectionActionPresentation.Hidden;

        /// <summary>
        /// Gets the first optional secondary result action.
        /// </summary>
        public InspectionActionPresentation SecondaryActionOne { get; init; } =
            InspectionActionPresentation.Hidden;

        /// <summary>
        /// Gets the second optional secondary result action.
        /// </summary>
        public InspectionActionPresentation SecondaryActionTwo { get; init; } =
            InspectionActionPresentation.Hidden;

        /// <summary>
        /// Gets the result state's main action.
        /// </summary>
        public InspectionActionPresentation PrimaryAction { get; init; } =
            InspectionActionPresentation.Hidden;
    }
}
