namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Defines the presentation status of one inspection stage or finding.
    /// </summary>
    public enum InspectionContentStatus
    {
        /// <summary>
        /// No stronger semantic status is required.
        /// </summary>
        Neutral,

        /// <summary>
        /// The stage has not started.
        /// </summary>
        Waiting,

        /// <summary>
        /// The stage is currently running.
        /// </summary>
        Active,

        /// <summary>
        /// The stage or check completed successfully.
        /// </summary>
        Passed,

        /// <summary>
        /// The result is usable but needs user attention.
        /// </summary>
        Warning,

        /// <summary>
        /// The result blocks progression or reports a failure.
        /// </summary>
        Error,

        /// <summary>
        /// The row contains explanatory information.
        /// </summary>
        Information
    }
}
