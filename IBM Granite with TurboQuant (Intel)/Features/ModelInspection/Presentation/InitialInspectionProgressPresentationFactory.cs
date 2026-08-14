using GraniteEdgeAI.Features.ModelInspection.Models;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation
{
    /// <summary>
    /// Creates the initial progress card around one stable five-row owner.
    /// </summary>
    internal static class InitialInspectionProgressPresentationFactory
    {
        /// <summary>
        /// Gets the fixed number of user-visible core inspection stages.
        /// </summary>
        internal const int StageCount = 5;

        /// <summary>
        /// Creates a standalone initial progress card for compatibility callers.
        /// </summary>
        internal static InspectionContentCardPresentation Create() =>
            Create(new InspectionProgressRows());

        /// <summary>
        /// Creates the progress card without replacing or mutating its owner.
        /// </summary>
        internal static InspectionContentCardPresentation Create(
            InspectionProgressRows progressRows) => Create(
                progressRows,
                InspectionStartupPresentation.Hidden);

        /// <summary>
        /// Creates the progress card with an explicit pre-stage startup state.
        /// </summary>
        internal static InspectionContentCardPresentation Create(
            InspectionProgressRows progressRows,
            InspectionStartupPresentation startup)
        {
            ArgumentNullException.ThrowIfNull(progressRows);
            ArgumentNullException.ThrowIfNull(startup);
            return new InspectionContentCardPresentation
            {
                Mode = InspectionContentCardMode.Progress,
                SectionTitle = "Inspection progress",
                Startup = startup,
                ProgressRows = progressRows
            };
        }
    }
}
