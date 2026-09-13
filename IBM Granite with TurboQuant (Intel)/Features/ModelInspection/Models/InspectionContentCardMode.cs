namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Identifies the content layout displayed inside the inspection content card.
    /// </summary>
    public enum InspectionContentCardMode
    {
        /// <summary>
        /// the content card is removed from the page layout
        /// </summary>
        Hidden,

        /// <summary>
        /// the five-stage inspection tracker is displayed
        /// </summary>
        Progress,

        /// <summary>
        /// Inspection completed with non-blocking warnings.
        /// </summary>
        Warnings,

        /// <summary>
        /// the selected model needs conversion before hardware checking
        /// </summary>
        ConversionRequired,

        /// <summary>
        /// one or more required model-package files are missing
        /// </summary>
        IncompletePackage,

        /// <summary>
        /// the model architecture is not supported by the selected runtime
        /// </summary>
        Unsupported,

        /// <summary>
        /// the model package failed structural validation
        /// </summary>
        Invalid,

        /// <summary>
        /// the user stopped inspection before it completed
        /// </summary>
        Cancelled,

        /// <summary>
        /// Inspection stopped because of an operational failure.
        /// </summary>
        OperationalFailure
    }
}
