namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Defines the presentation status of one inspection stage or finding.
    /// </summary>
    public enum InspectionContentStatus
    {
        /// <summary>
        /// no stronger semantic status is required
        /// </summary>
        Neutral,

        /// <summary>
        /// the stage has not started
        /// </summary>
        Waiting,

        /// <summary>
        /// the stage is currently running
        /// </summary>
        Active,

        /// <summary>
        /// the stage or check completed successfully
        /// </summary>
        Passed,

        /// <summary>
        /// the result is usable but needs user attention
        /// </summary>
        Warning,

        /// <summary>
        /// the result blocks progression or reports a failure
        /// </summary>
        Error,

        /// <summary>
        /// the row contains explanatory information
        /// </summary>
        Information
    }
}
