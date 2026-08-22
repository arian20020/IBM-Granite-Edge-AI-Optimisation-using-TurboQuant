namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Identifies the content layout displayed inside the inspection content card.
    /// </summary>
    public enum InspectionContentCardMode
    {
        /// <summary>
        /// The content card is removed from the page layout.
        /// </summary>
        Hidden,

        /// <summary>
        /// The five-stage inspection tracker is displayed.
        /// </summary>
        Progress,

        /// <summary>
        /// Inspection completed with non-blocking warnings.
        /// </summary>
        Warnings,

        /// <summary>
        /// The selected model needs conversion before hardware checking.
        /// </summary>
        ConversionRequired,

        /// <summary>
        /// One or more required model-package files are missing.
        /// </summary>
        IncompletePackage,

        /// <summary>
        /// The model architecture is not supported by the selected runtime.
        /// </summary>
        Unsupported,

        /// <summary>
        /// The model package failed structural validation.
        /// </summary>
        Invalid,

        /// <summary>
        /// The user stopped inspection before it completed.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Inspection stopped because of an operational failure.
        /// </summary>
        OperationalFailure
    }
}
