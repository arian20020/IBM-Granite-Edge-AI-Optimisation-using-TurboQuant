using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;

namespace GraniteEdgeAI.Features.ModelInspection
{
    /// <summary>
    /// Displays the model-inspection journey for one validated model package.
    /// </summary>
    public sealed partial class ModelInspectionPage : Page
    {
        /// <summary>
        /// Creates the page and loads its XAML visual tree.
        /// </summary>
        public ModelInspectionPage()
        {
            // Build all controls declared in ModelInspectionPage.xaml.
            InitializeComponent();
        }

        /// <summary>
        /// Gets the authoritative model path supplied by onboarding navigation.
        /// </summary>
        internal string? SelectedModelPath { get; private set; }

        /// <summary>
        /// Receives the model path passed through StageFrame.Navigate.
        /// </summary>
        protected override void OnNavigatedTo(
            NavigationEventArgs eventArguments)
        {
            // Preserve the standard WinUI navigation lifecycle.
            base.OnNavigatedTo(eventArguments);

            // The inspection page requires one valid local model path.
            if (eventArguments.Parameter is not string modelPath ||
                string.IsNullOrWhiteSpace(modelPath))
            {
                throw new ArgumentException(
                    "ModelInspectionPage requires a non-empty model path.",
                    nameof(eventArguments));
            }

            // Preserve the exact original path for the future inspection service.
            SelectedModelPath = modelPath;

            // Display the initial inspection presentation immediately.
            //
            // This is still only a visual state. It does not run LLamaSharp yet.
            ShowInitialInspectionState(modelPath);
        }

        /// <summary>
        /// Displays the selected model, the five inspection stages, and the
        /// inspection action area.
        /// </summary>
        private void ShowInitialInspectionState(string modelPath)
        {
            // No outcome exists while inspection is beginning.
            InspectionOutcomeCardControl.Presentation =
                InspectionOutcomePresentation.Hidden;

            // Display the selected model using the compact card layout.
            InspectionModelCardControl.Presentation =
                CreateInitialModelPresentation(modelPath);

            // Display the five-stage progress tracker.
            InspectionContentCardControl.Presentation =
                CreateInitialProgressPresentation();

            // Display the inspection action area.
            InspectionActionCardControl.Presentation =
                CreateInitialActionPresentation();
        }

        /// <summary>
        /// Creates the compact selected-model presentation.
        /// </summary>
        private static InspectionModelCardPresentation
            CreateInitialModelPresentation(string modelPath)
        {
            // Show the real file name rather than the complete directory path.
            string modelFileName = Path.GetFileName(modelPath);

            // Determine the basic package format from the file extension.
            bool isGguf = string.Equals(
                Path.GetExtension(modelPath),
                ".gguf",
                StringComparison.OrdinalIgnoreCase);

            string formatName = isGguf
                ? "GGUF"
                : "MODEL";

            return new InspectionModelCardPresentation
            {
                // Inspection begins with the compact model card.
                DisplayMode = InspectionModelCardMode.Compact,

                // The full inspection has not finished yet.
                BadgeState = InspectionModelBadgeState.ModelSelected,

                // Use the actual selected file name.
                ModelName = modelFileName,

                // Do not invent quantisation, size, or parameter information.
                CompactSummary =
                    $"{formatName} · Awaiting full inspection",

                // Shown inside the format square.
                FormatShortName = formatName,

                // Values below are mainly used by the future detailed layout.
                OverviewFormatBadgeText = $"{formatName} MODEL",
                FormatName = formatName,
                InspectionChecksSummary =
                    "0 of 5 inspection checks complete"
            };
        }

        /// <summary>
        /// Creates the initial five-stage inspection tracker.
        /// </summary>
        private static InspectionContentCardPresentation
            CreateInitialProgressPresentation()
        {
            return new InspectionContentCardPresentation
            {
                // Select the progress DataTemplate.
                Mode = InspectionContentCardMode.Progress,

                // Heading displayed at the top of the content card.
                SectionTitle = "Inspection progress",

                // No checks have completed yet.
                ProgressSummary = "0 of 5 checks complete",

                // Only the first stage is active initially.
                Items =
                [
                    CreateProgressStage(
                        stageNumber: "1",
                        title: "Check model package",
                        detail:
                            "Checking the selected model package and file boundaries.",
                        status: InspectionContentStatus.Active,
                        statusText: "Checking",
                        isActive: true,
                        showConnector: true,
                        showDetail: true),

                    CreateProgressStage(
                        stageNumber: "2",
                        title: "Read model configuration",
                        detail: string.Empty,
                        status: InspectionContentStatus.Waiting,
                        statusText: "Waiting",
                        isActive: false,
                        showConnector: true,
                        showDetail: false),

                    CreateProgressStage(
                        stageNumber: "3",
                        title: "Validate tokenizer and chat setup",
                        detail: string.Empty,
                        status: InspectionContentStatus.Waiting,
                        statusText: "Waiting",
                        isActive: false,
                        showConnector: true,
                        showDetail: false),

                    CreateProgressStage(
                        stageNumber: "4",
                        title: "Validate model structure",
                        detail: string.Empty,
                        status: InspectionContentStatus.Waiting,
                        statusText: "Waiting",
                        isActive: false,
                        showConnector: true,
                        showDetail: false),

                    CreateProgressStage(
                        stageNumber: "5",
                        title: "Confirm runtime support",
                        detail: string.Empty,
                        status: InspectionContentStatus.Waiting,
                        statusText: "Waiting",
                        isActive: false,
                        showConnector: false,
                        showDetail: false)
                ]
            };
        }

        /// <summary>
        /// Creates one row in the five-stage inspection tracker.
        /// </summary>
        private static InspectionContentItemPresentation
            CreateProgressStage(
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

                // Hide empty explanations so they do not reserve layout space.
                DetailVisibility = showDetail
                    ? Visibility.Visible
                    : Visibility.Collapsed,

                Status = status,
                StatusText = statusText,
                IsActive = isActive,
                ShowConnector = showConnector,

                // Give screen readers one complete description of the row.
                AutomationName =
                    $"{title}. {statusText}."
            };
        }

        /// <summary>
        /// Creates the initial action-card presentation.
        /// </summary>
        private static InspectionActionCardPresentation
            CreateInitialActionPresentation()
        {
            return new InspectionActionCardPresentation
            {
                // Select the action card's inspecting layout.
                Mode = InspectionActionCardMode.Inspecting,

                // Supporting text beneath the Cancel button.
                Message =
                    "You can safely return to model selection at any time.",

                AutomationName =
                    "Actions available while inspecting the model",

                CancelAction = new InspectionActionPresentation
                {
                    Text = "Cancel inspection",

                    // Keep the action visible in the initial design.
                    Visibility = Visibility.Visible,

                    // The real cancellation command will be connected when
                    // the asynchronous inspection service is implemented.
                    IsEnabled = false,

                    AutomationName =
                        "Cancel model inspection",

                    MinimumWidth = 184d
                }
            };
        }
    }
}