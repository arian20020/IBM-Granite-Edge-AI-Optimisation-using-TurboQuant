using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

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

        internal Task CurrentNavigationTask { get; private set; } =
            Task.CompletedTask;

        internal ModelInspectionPage? ActiveInspectionPageForTesting =>
            _attachedModelInspectionPage;

        private readonly Func<Frame, ModelInspectionRequest, bool>
            _modelInspectionNavigator;
        private readonly Func<Frame, OpenVinoInspectionRequestedEventArgs, bool>
            _openVinoInspectionNavigator;

        /// <summary>
        /// Creates the onboarding shell and displays the first stage.
        /// </summary>
        public OnboardingShellPage()
            : this(
                static (frame, request) => frame.Navigate(
                    typeof(ModelInspectionPage), request),
                static (frame, request) => frame.Navigate(
                    typeof(ModelInspectionPage), request))
        {
        }

        internal OnboardingShellPage(
            Func<Frame, ModelInspectionRequest, bool> modelInspectionNavigator)
            : this(
                modelInspectionNavigator,
                static (frame, request) => frame.Navigate(
                    typeof(ModelInspectionPage), request))
        {
        }

        internal OnboardingShellPage(
            Func<Frame, ModelInspectionRequest, bool> modelInspectionNavigator,
            Func<Frame, OpenVinoInspectionRequestedEventArgs, bool>
                openVinoInspectionNavigator)
        {
            _modelInspectionNavigator = modelInspectionNavigator ??
                throw new ArgumentNullException(nameof(modelInspectionNavigator));
            _openVinoInspectionNavigator = openVinoInspectionNavigator ??
                throw new ArgumentNullException(nameof(openVinoInspectionNavigator));

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

        internal bool NavigateToOpenVinoInspection(
            OpenVinoInspectionRequestedEventArgs request)
        {
            ArgumentNullException.ThrowIfNull(request);
            object? previousContent = StageFrame.Content;
            bool navigationSucceeded = _openVinoInspectionNavigator(
                StageFrame,
                request);
            if (!navigationSucceeded ||
                ReferenceEquals(StageFrame.Content, previousContent) ||
                StageFrame.Content is not ModelInspectionPage modelInspectionPage ||
                !ReferenceEquals(modelInspectionPage.OpenVinoRequest, request))
            {
                return false;
            }

            StageFrame.BackStack.Clear();
            AttachModelInspectionPage(modelInspectionPage);
            DetachModelImportPage();
            CurrentStage = OnboardingStage.InspectModel;
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
            NavigateToOpenVinoInspection(eventArguments);
        }

        /// <summary>
        /// Responds only to the currently active inspection page and starts a
        /// fresh model-selection journey.
        /// </summary>
        private async void ModelInspectionPage_ChooseAnotherModelRequested(
            object? sender,
            EventArgs eventArguments)
        {
            if (sender is not ModelInspectionPage page ||
                !ReferenceEquals(page, _attachedModelInspectionPage))
            {
                return;
            }

            Task<bool> navigation = ReturnToModelImportAsync();
            try
            {
                await navigation;
            }
            catch (Exception)
            {
                // The owner task retains the cleanup/navigation failure for
                // diagnostics while the current page remains authoritative.
            }
        }

        private async Task<bool> NavigateToFreshModelImportAsync(
            ModelInspectionPage page)
        {
            if (!ReferenceEquals(page, _attachedModelInspectionPage))
            {
                return false;
            }

            object? previousContent = StageFrame.Content;
            bool priorHitTestVisibility = StageFrame.IsHitTestVisible;
            StageFrame.IsHitTestVisible = false;
            bool ownershipCommitted = false;
            ModelImportPage? modelImportPage = null;
            try
            {
                bool navigationSucceeded =
                    StageFrame.Navigate(typeof(ModelImportPage));
                if (!navigationSucceeded ||
                    ReferenceEquals(StageFrame.Content, previousContent) ||
                    StageFrame.Content is not ModelImportPage destination)
                {
                    StageFrame.IsHitTestVisible = priorHitTestVisibility;
                    return false;
                }
                modelImportPage = destination;
                modelImportPage.IsEnabled = false;

                // Frame navigation starts the page's one retirement task. The
                // shell owns that same completion boundary and keeps the new
                // destination inert until every owner has transferred.
                await page.RetireForNavigationAsync();
                if (!ReferenceEquals(StageFrame.Content, modelImportPage))
                {
                    return false;
                }

                StageFrame.BackStack.Clear();
                StageFrame.ForwardStack.Clear();
                AttachModelImportPage(modelImportPage);
                DetachModelInspectionPage();
                CurrentStage = OnboardingStage.ImportModel;
                StageIndicator.CurrentStage = CurrentStage;
                ownershipCommitted = true;
                return true;
            }
            finally
            {
                if (modelImportPage is not null)
                {
                    modelImportPage.IsEnabled = ownershipCommitted;
                }
                if (ownershipCommitted ||
                    ReferenceEquals(StageFrame.Content, previousContent))
                {
                    StageFrame.IsHitTestVisible = priorHitTestVisibility;
                }
            }
        }

        internal Task<bool> ReturnToModelImportAsync()
        {
            ModelInspectionPage? page = _attachedModelInspectionPage;
            if (page is null)
            {
                return Task.FromResult(false);
            }

            Task<bool> navigation = NavigateToFreshModelImportAsync(page);
            CurrentNavigationTask = navigation;
            return navigation;
        }

        internal async Task ShutdownAsync()
        {
            Task navigation = CurrentNavigationTask;
            if (!navigation.IsCompleted)
            {
                await navigation;
            }

            ModelInspectionPage? page = _attachedModelInspectionPage;
            if (page is not null)
            {
                await page.RetireForNavigationAsync();
                if (ReferenceEquals(page, _attachedModelInspectionPage))
                {
                    DetachModelInspectionPage();
                }
            }
            DetachModelImportPage();
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
