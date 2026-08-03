using Microsoft.UI.Xaml;

namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Contains the display data for one inspection stage, warning or report row.
    /// </summary>
    public sealed class InspectionContentItemPresentation
    {
        /// <summary>
        /// Gets the stage number shown while the item is used in the progress tracker.
        /// </summary>
        public string StageNumber { get; init; } = string.Empty;

        /// <summary>
        /// Gets the primary row label.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets the optional explanation shown beneath the row label.
        /// </summary>
        public string Detail { get; init; } = string.Empty;

        /// <summary>
        /// Gets whether the optional explanation is displayed.
        /// </summary>
        public Visibility DetailVisibility { get; init; } = Visibility.Collapsed;

        /// <summary>
        /// Gets the semantic status used for the icon, text and badge.
        /// </summary>
        public InspectionContentStatus Status { get; init; } =
            InspectionContentStatus.Neutral;

        /// <summary>
        /// Gets the explicit status text shown to the user.
        /// </summary>
        public string StatusText { get; init; } = string.Empty;

        /// <summary>
        /// Gets whether the progress ring should animate for this stage.
        /// </summary>
        public bool IsActive { get; init; }

        /// <summary>
        /// Gets whether the connector below this stage is displayed.
        /// </summary>
        public bool ShowConnector { get; init; }

        /// <summary>
        /// Gets the complete accessible description of this row.
        /// </summary>
        public string AutomationName { get; init; } = string.Empty;
    }
}
