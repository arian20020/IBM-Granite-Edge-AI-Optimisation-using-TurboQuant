namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Defines the two visible layouts and the hidden state of the action card.
    /// </summary>
    public enum InspectionActionCardMode
    {
        /// <summary>
        /// the complete action card is removed from the page layout
        /// </summary>
        Hidden,

        /// <summary>
        /// Inspection is running and only the cancellation action is available.
        /// </summary>
        Inspecting,

        /// <summary>
        /// Inspection has stopped and the result-specific actions are available.
        /// </summary>
        Result
    }
}
