using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace GraniteEdgeAI.Features.ModelInspection
{
    /// <summary>
    /// Displays the model-inspection journey for one validated model package.
    /// </summary>
    public sealed partial class ModelInspectionPage : Page
    {
        // loaded can run again when the same page returns to the visual tree
        private bool _initialPresentationApplied;

        /// <summary>
        /// Creates the page and loads its XAML visual tree.
        /// </summary>
        public ModelInspectionPage()
        {
            InitializeComponent();

            // control bindings are ready only after the page enters the visual tree
            Loaded += ModelInspectionPage_Loaded;
        }

        /// <summary>
        /// Gets the immutable request supplied by onboarding navigation.
        /// </summary>
        internal ModelInspectionRequest? Request { get; private set; }

        /// <summary>
        /// Receives the immutable request passed through StageFrame.Navigate.
        /// </summary>
        protected override void OnNavigatedTo(
            NavigationEventArgs eventArguments)
        {
            base.OnNavigatedTo(eventArguments);

            if (eventArguments.Parameter is not ModelInspectionRequest request)
            {
                throw new ArgumentException(
                    "ModelInspectionPage requires a validated ModelInspectionRequest.",
                    nameof(eventArguments));
            }

            // keep the exact validated request so no handoff facts are reconstructed
            Request = request;
            _initialPresentationApplied = false;
        }

        /// <summary>
        /// Applies the initial card presentations after the page's visual tree
        /// and compiled XAML bindings are ready.
        /// </summary>
        private void ModelInspectionPage_Loaded(
            object sender,
            RoutedEventArgs eventArguments)
        {
            if (_initialPresentationApplied)
            {
                return;
            }

            ModelInspectionRequest request = Request
                ?? throw new InvalidOperationException(
                    "ModelInspectionPage loaded without an inspection request.");

            // set the guard first so a re-entrant loaded event cannot apply state twice
            _initialPresentationApplied = true;
            ShowInitialInspectionState(request);
        }

        /// <summary>
        /// Displays the selected model, the five inspection stages, and the
        /// inspection action area.
        /// </summary>
        private void ShowInitialInspectionState(ModelInspectionRequest request)
        {
            InspectionOutcomeCardControl.Presentation =
                InspectionOutcomePresentation.Hidden;
            InspectionModelCardControl.Presentation =
                CreateInitialModelPresentation(request);
            InspectionContentCardControl.Presentation =
                InitialInspectionProgressPresentationFactory.Create();
            InspectionActionCardControl.Presentation =
                CreateInitialActionPresentation();
        }

        /// <summary>
        /// Creates the compact selected-model presentation.
        /// </summary>
        private static InspectionModelCardPresentation
            CreateInitialModelPresentation(ModelInspectionRequest request)
        {
            string modelFileName = request.FileName;
            string formatName = request.QuickScan.Format;

            return new InspectionModelCardPresentation
            {
                DisplayMode = InspectionModelCardMode.Compact,
                BadgeState = InspectionModelBadgeState.ModelSelected,
                ModelName = modelFileName,
                CompactSummary =
                    $"{formatName} · Awaiting full inspection",
                FormatShortName = formatName,
                OverviewFormatBadgeText = $"{formatName} MODEL",
                FormatName = formatName,
                InspectionChecksSummary =
                    $"0 of {InitialInspectionProgressPresentationFactory.StageCount} " +
                    "inspection checks complete"
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
                Mode = InspectionActionCardMode.Inspecting,
                Message =
                    "You can safely return to model selection at any time.",
                AutomationName =
                    "Actions available while inspecting the model",
                CancelAction = new InspectionActionPresentation
                {
                    Text = "Cancel inspection",
                    Visibility = Visibility.Visible,

                    // cancellation stays disabled until the asynchronous service exists
                    IsEnabled = false,
                    AutomationName =
                        "Cancel model inspection",
                    MinimumWidth = 184d
                }
            };
        }
    }
}
