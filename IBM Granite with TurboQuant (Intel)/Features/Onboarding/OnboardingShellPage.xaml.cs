using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelInspection;
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

        /// <summary>
        /// Creates the onboarding shell and displays the first stage.
        /// </summary>
        public OnboardingShellPage()
        {
            // Create all controls declared in OnboardingShellPage.xaml.
            InitializeComponent();

            // Record that onboarding starts on the model-import stage.
            CurrentStage = OnboardingStage.ImportModel;

            // Keep the permanent stage indicator synchronized.
            StageIndicator.CurrentStage = CurrentStage;

            // Display ModelImportPage and subscribe to its navigation request.
            ShowInitialStage();
        }

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
        }

        /// <summary>
        /// Navigates the onboarding frame to the model-inspection stage.
        /// </summary>
        /// <param name="modelPath">
        /// The validated local model path.
        /// </param>
        /// <returns>
        /// True when Frame navigation succeeds; otherwise, false.
        /// </returns>
        internal bool NavigateToModelInspection(string modelPath)
        {
            // Never navigate with an invalid model path.
            ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

            // Ask the onboarding Frame to create ModelInspectionPage.
            //
            // The second argument becomes NavigationEventArgs.Parameter on
            // the destination page.
            bool navigationSucceeded = StageFrame.Navigate(
                typeof(ModelInspectionPage),
                modelPath);

            // Preserve the current stage when WinUI reports navigation failure.
            if (!navigationSucceeded)
            {
                return false;
            }

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
            // Forward the validated path to the shell-owned navigation method.
            NavigateToModelInspection(eventArguments.ModelPath);
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

            // Release the reference to the old page.
            _attachedModelImportPage = null;
        }
    }
}