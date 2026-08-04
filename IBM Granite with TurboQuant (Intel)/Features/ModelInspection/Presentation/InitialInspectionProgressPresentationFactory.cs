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
                // Select the running-inspection template.
                Mode = InspectionContentCardMode.Progress,

                // Keep the card heading stable across every progress update.
                SectionTitle = "Inspection progress",

                // No stage has completed when the page first appears.
                ProgressSummary = $"0 of {StageCount} checks complete",

                Items =
                [
                    CreateStage(
                        stageNumber: "1",
                        title: "Check model package",
                        detail:
                            "Checking that the selected file still exists and " +
                            "matches the validated import.",
                        status: InspectionContentStatus.Active,
                        statusText: "Checking",
                        isActive: true,
                        showConnector: true,
                        showDetail: true),

                    CreateStage(
                        stageNumber: "2",
                        title: "Read model configuration",
                        detail:
                            "Reading lightweight configuration through the " +
                            "pinned core runtime.",
                        status: InspectionContentStatus.Waiting,
                        statusText: "Waiting",
                        isActive: false,
                        showConnector: true,
                        showDetail: false),

                    CreateStage(
                        stageNumber: "3",
                        title: "Validate tokenizer and chat setup",
                        detail:
                            "Checking vocabulary, special-token and " +
                            "chat-template evidence.",
                        status: InspectionContentStatus.Waiting,
                        statusText: "Waiting",
                        isActive: false,
                        showConnector: true,
                        showDetail: false),

                    CreateStage(
                        stageNumber: "4",
                        title: "Validate model structure",
                        detail:
                            "Checking architecture, context, parameters and " +
                            "model characteristics.",
                        status: InspectionContentStatus.Waiting,
                        statusText: "Waiting",
                        isActive: false,
                        showConnector: true,
                        showDetail: false),

                    CreateStage(
                        stageNumber: "5",
                        title: "Confirm core runtime compatibility",
                        detail:
                            "Confirming that the pinned core runtime recognises " +
                            "the model before hardware analysis.",
                        status: InspectionContentStatus.Waiting,
                        statusText: "Waiting",
                        isActive: false,
                        showConnector: false,
                        showDetail: false)
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
            InspectionContentStatus status,
            string statusText,
            bool isActive,
            bool showConnector,
            bool showDetail)
        {
            return new InspectionContentItemPresentation
            {
                StageNumber = stageNumber,
                Title = title,
                Detail = detail,
                DetailVisibility = showDetail
                    ? Visibility.Visible
                    : Visibility.Collapsed,
                Status = status,
                StatusText = statusText,
                IsActive = isActive,
                ShowConnector = showConnector,
                AutomationName = $"{title}. {statusText}."
            };
        }
    }
}
