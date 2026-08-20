using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GraniteEdgeAI.Features.Onboarding
{
    /// <summary>
    /// Hosts the onboarding journey and displays one onboarding page at a time.
    /// </summary>
    public sealed partial class OnboardingShellPage : Page
    {
        // Stores the Model Import page whose inspection request the shell is
        // currently listening to.
        private ModelImportPage? _attachedModelImportPage;

        // Stores the Model Inspection page whose choose-another request the
        // shell is currently listening to.
        private ModelInspectionPage? _attachedModelInspectionPage;

        private readonly Func<Frame, ModelInspectionRequest, bool>
            _modelInspectionNavigator;

        /// <summary>
        /// Creates the onboarding shell and displays the first stage.
        /// </summary>
        public OnboardingShellPage()
            : this(static (frame, request) => frame.Navigate(
                typeof(ModelInspectionPage),
                request))
        {
        }

        internal OnboardingShellPage(
            Func<Frame, ModelInspectionRequest, bool> modelInspectionNavigator)
        {
            _modelInspectionNavigator = modelInspectionNavigator ??
                throw new ArgumentNullException(nameof(modelInspectionNavigator));

            // Create all controls declared in OnboardingShellPage.xaml.
            InitializeComponent();
            InitializeFixtureGalleryEntry();

            // Record that onboarding starts on the model-import stage.
            CurrentStage = OnboardingStage.ImportModel;

            // Keep the permanent stage indicator synchronized.
            StageIndicator.CurrentStage = CurrentStage;

            // Display ModelImportPage and subscribe to its navigation request.
            ShowInitialStage();
        }

        partial void InitializeFixtureGalleryEntry();

        /// <summary>
        /// Gets the onboarding stage currently displayed by the shell.
        /// </summary>
        public OnboardingStage CurrentStage { get; private set; }

        internal event EventHandler<SourceModelConversionRequestedEventArgs>?
            SourceModelConversionRequested;

        /// <summary>
        /// Subscribes the shell to one Model Import page.
        /// </summary>
        internal void AttachModelImportPage(
            ModelImportPage modelImportPage)
        {
            // Reject a missing page immediately.
            ArgumentNullException.ThrowIfNull(modelImportPage);

            // Avoid subscribing twice to the same page instance.
            if (ReferenceEquals(
                _attachedModelImportPage,
                modelImportPage))
            {
                return;
            }

            // Stop listening to an older Model Import page.
            DetachModelImportPage();

            // Store the current page.
            _attachedModelImportPage = modelImportPage;

            // Listen for the page's request to begin model inspection.
            _attachedModelImportPage.ModelInspectionRequested +=
                ModelImportPage_ModelInspectionRequested;
            _attachedModelImportPage.OpenVinoInspectionRequested +=
                ModelImportPage_OpenVinoInspectionRequested;
            _attachedModelImportPage.SourceModelConversionRequested +=
                ModelImportPage_SourceModelConversionRequested;
        }

        /// <summary>
        /// Subscribes the shell to one active Model Inspection page.
        /// </summary>
        internal void AttachModelInspectionPage(
            ModelInspectionPage modelInspectionPage)
        {
            ArgumentNullException.ThrowIfNull(modelInspectionPage);

            // Reattaching the same Frame content must not duplicate the event
            // handler and therefore cannot cause duplicate reset navigation.
            if (ReferenceEquals(
                _attachedModelInspectionPage,
                modelInspectionPage))
            {
                return;
            }

            // A page replaced by successful Frame navigation is no longer
            // allowed to drive the shell.
            DetachModelInspectionPage();
            _attachedModelInspectionPage = modelInspectionPage;
            _attachedModelInspectionPage.ChooseAnotherModelRequested +=
                ModelInspectionPage_ChooseAnotherModelRequested;
            _attachedModelInspectionPage.FooterStatusChanged +=
                ModelInspectionPage_FooterStatusChanged;

            // Navigation has already completed by the time the Frame hands
            // ownership to the shell, so sample the authoritative status now.
            StageIndicator.InspectionStatus =
                _attachedModelInspectionPage.CurrentFooterStatus;
        }

        /// <summary>
        /// Navigates the onboarding frame to the model-inspection stage.
        /// </summary>
        /// <param name="request">
        /// The immutable, validated Model Inspection request.
        /// </param>
        /// <returns>
        /// True when Frame navigation succeeds; otherwise, false.
        /// </returns>
        internal bool NavigateToModelInspection(
            ModelInspectionRequest request)
        {
            // Never navigate without the validated request created by Model Import.
            ArgumentNullException.ThrowIfNull(request);

            object? previousContent = StageFrame.Content;

            // Ask the onboarding Frame to create ModelInspectionPage.
            // The request becomes NavigationEventArgs.Parameter on that page.
            bool navigationSucceeded = _modelInspectionNavigator(
                StageFrame,
                request);

            // A cancelled Navigating event can leave Navigate reporting true.
            // Confirm the destination before transferring any ownership.
            if (!navigationSucceeded ||
                ReferenceEquals(StageFrame.Content, previousContent) ||
                StageFrame.Content is not ModelInspectionPage modelInspectionPage ||
                !ReferenceEquals(modelInspectionPage.Request, request))
            {
                return false;
            }

            // Onboarding owns navigation through explicit stage transitions.
            // Do not retain the import page or its path-bearing state in the
            // Frame journal where Back could resurrect it outside that state.
            StageFrame.BackStack.Clear();

            AttachModelInspectionPage(modelInspectionPage);

            // Model Import is no longer the active page after navigation.
            DetachModelImportPage();

            // Record that onboarding has advanced to stage two.
            CurrentStage = OnboardingStage.InspectModel;

            // Keep the permanent visual indicator synchronized.
            StageIndicator.CurrentStage = CurrentStage;

            return true;
        }

        /// <summary>
        /// Displays ModelImportPage inside the shell's StageFrame.
        /// </summary>
        private void ShowInitialStage()
        {
            // Navigate the internal Frame to the initial onboarding page.
            bool navigationSucceeded =
                StageFrame.Navigate(typeof(ModelImportPage));

            // Fail immediately if the first page could not be displayed.
            if (!navigationSucceeded)
            {
                throw new InvalidOperationException(
                    "The onboarding shell could not display ModelImportPage.");
            }

            // Confirm that Frame created the expected page type.
            if (StageFrame.Content is not ModelImportPage modelImportPage)
            {
                throw new InvalidOperationException(
                    "StageFrame did not create the expected ModelImportPage.");
            }

            // Listen to the real Model Import page created by Frame navigation.
            AttachModelImportPage(modelImportPage);
        }

        /// <summary>
        /// Responds when ModelImportPage requests full model inspection.
        /// </summary>
        private void ModelImportPage_ModelInspectionRequested(
            object? sender,
            ModelInspectionRequestedEventArgs eventArguments)
        {
            // Forward the exact immutable request without reconstructing it.
            NavigateToModelInspection(eventArguments.Request);
        }

        private void ModelImportPage_OpenVinoInspectionRequested(
            object? sender,
            OpenVinoInspectionRequestedEventArgs eventArguments)
        {
            if (sender is ModelImportPage page &&
                ReferenceEquals(page, _attachedModelImportPage))
            {
                // O1 has no frozen inspection invocation port in this build.
                // Do not navigate to a placeholder or imply that inspection ran.
                page.RejectFolderRoute(
                    "openvino-inspection-unavailable",
                    "OpenVINO folder inspection is not available in this build.");
            }
        }

        private void ModelImportPage_SourceModelConversionRequested(
            object? sender,
            SourceModelConversionRequestedEventArgs eventArguments)
        {
            if (sender is not ModelImportPage page ||
                !ReferenceEquals(page, _attachedModelImportPage))
            {
                return;
            }

            // The shell relays the exact immutable intent. Conversion belongs
            // to a later route; this boundary never starts a converter/process
            // and never retains the selected folder path.
            SourceModelConversionRequested?.Invoke(this, eventArguments);
        }

        /// <summary>
        /// Responds only to the currently active inspection page and starts a
        /// fresh model-selection journey.
        /// </summary>
        private void ModelInspectionPage_ChooseAnotherModelRequested(
            object? sender,
            EventArgs eventArguments)
        {
            if (!ReferenceEquals(sender, _attachedModelInspectionPage))
            {
                return;
            }

            NavigateToFreshModelImport();
        }

        private void ModelInspectionPage_FooterStatusChanged(
            object? sender,
            InspectionFooterStatusChangedEventArgs eventArguments)
        {
            if (!ReferenceEquals(sender, _attachedModelInspectionPage))
            {
                return;
            }

            StageIndicator.InspectionStatus = eventArguments.Status;
        }

        /// <summary>
        /// Replaces the inspection page with a new Model Import page without
        /// retaining Frame back-stack state as the active journey.
        /// </summary>
        private bool NavigateToFreshModelImport()
        {
            object? previousContent = StageFrame.Content;
            bool navigationSucceeded =
                StageFrame.Navigate(typeof(ModelImportPage));

            // Frame.Navigate can report true after a cancelled Navigating
            // event, so the expected content is the completion boundary.
            if (!navigationSucceeded ||
                ReferenceEquals(StageFrame.Content, previousContent) ||
                StageFrame.Content is not ModelImportPage modelImportPage)
            {
                return false;
            }


            // A new selection is a new journey. Remove the inspection page
            // and its request from the Frame journal before exposing the new
            // active stage.
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();

            // Transfer event ownership only after the new page exists.
            AttachModelImportPage(modelImportPage);
            DetachModelInspectionPage();

            CurrentStage = OnboardingStage.ImportModel;
            StageIndicator.CurrentStage = CurrentStage;
            return true;
        }

        /// <summary>
        /// Removes the current event subscription.
        /// </summary>
        private void DetachModelImportPage()
        {
            // There is nothing to detach before a page has been attached.
            if (_attachedModelImportPage is null)
            {
                return;
            }

            // Remove the event subscription to avoid retaining an inactive page.
            _attachedModelImportPage.ModelInspectionRequested -=
                ModelImportPage_ModelInspectionRequested;
            _attachedModelImportPage.OpenVinoInspectionRequested -=
                ModelImportPage_OpenVinoInspectionRequested;
            _attachedModelImportPage.SourceModelConversionRequested -=
                ModelImportPage_SourceModelConversionRequested;

            // Release the reference to the old page.
            _attachedModelImportPage = null;
        }

        /// <summary>
        /// Removes the current Model Inspection event subscription.
        /// </summary>
        private void DetachModelInspectionPage()
        {
            if (_attachedModelInspectionPage is null)
            {
                return;
            }

            _attachedModelInspectionPage.ChooseAnotherModelRequested -=
                ModelInspectionPage_ChooseAnotherModelRequested;
            _attachedModelInspectionPage.FooterStatusChanged -=
                ModelInspectionPage_FooterStatusChanged;
            _attachedModelInspectionPage = null;
        }

    }
}
