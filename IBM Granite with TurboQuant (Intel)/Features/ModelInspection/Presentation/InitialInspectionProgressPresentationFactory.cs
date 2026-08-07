using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation
{
    /// <summary>
    /// Creates the initial five-row presentation shown before the real
    /// inspection service begins reporting progress.
    /// </summary>
    internal static class InitialInspectionProgressPresentationFactory
    {
        /// <summary>
        /// Gets the fixed number of user-visible core inspection stages.
        /// </summary>
        internal const int StageCount = 5;

        /// <summary>
        /// Creates the initial progress card with stage one active and the
        /// remaining stages waiting.
        /// </summary>
        internal static InspectionContentCardPresentation Create()
        {
            return new InspectionContentCardPresentation
            {
                Mode = InspectionContentCardMode.Progress,
                SectionTitle = "Inspection progress",
                ProgressSummary = $"0 of {StageCount} checks complete",
                Items =
                [
                    CreateStage(
                        stageNumber: "1",
                        title: "Check model package",
                        detail:
                            "Checking that the selected file still exists and " +
                            "matches the validated import.",
                        isActive: true,
                        showConnector: true),

                    CreateStage(
                        stageNumber: "2",
                        title: "Read model configuration",
                        detail:
                            "Reading lightweight configuration through the " +
                            "pinned core runtime.",
                        isActive: false,
                        showConnector: true),

                    CreateStage(
                        stageNumber: "3",
                        title: "Validate tokenizer and chat setup",
                        detail:
                            "Checking vocabulary, special-token and " +
                            "chat-template evidence.",
                        isActive: false,
                        showConnector: true),

                    CreateStage(
                        stageNumber: "4",
                        title: "Validate model structure",
                        detail:
                            "Checking architecture, context, parameters and " +
                            "model characteristics.",
                        isActive: false,
                        showConnector: true),

                    CreateStage(
                        stageNumber: "5",
                        title: "Confirm core runtime compatibility",
                        detail:
                            "Confirming that the pinned core runtime recognises " +
                            "the model before hardware analysis.",
                        isActive: false,
                        showConnector: false)
                ]
            };
        }

        /// <summary>
        /// Creates one row while preserving consistent status and accessibility
        /// data across all five stages.
        /// </summary>
        private static InspectionContentItemPresentation CreateStage(
            string stageNumber,
            string title,
            string detail,
            bool isActive,
            bool showConnector)
        {
            InspectionContentStatus status = isActive
                ? InspectionContentStatus.Active
                : InspectionContentStatus.Waiting;
            string statusText = isActive
                ? "Checking"
                : "Waiting";
            Visibility detailVisibility = isActive
                ? Visibility.Visible
                : Visibility.Collapsed;

            return new InspectionContentItemPresentation
            {
                StageNumber = stageNumber,
                Title = title,
                Detail = detail,
                DetailVisibility = detailVisibility,
                Status = status,
                StatusText = statusText,
                IsActive = isActive,
                ShowConnector = showConnector,
                AutomationName = $"{title}. {statusText}."
            };
        }
    }
}
