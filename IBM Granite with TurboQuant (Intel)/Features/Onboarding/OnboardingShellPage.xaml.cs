using GraniteEdgeAI.Features.ModelImport;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GraniteEdgeAI.Features.Onboarding
{
    /// <summary>
    /// Hosts the complete onboarding journey and displays one stage page at a time.
    /// </summary>
    public sealed partial class OnboardingShellPage : Page
    {
        /// <summary>
        /// Creates the onboarding shell and displays the first onboarding stage.
        /// </summary>
        public OnboardingShellPage()
        {
            // Creates all controls declared in OnboardingShellPage.xaml.
            InitializeComponent();

            // Records that onboarding begins at the model-selection stage.
            CurrentStage = OnboardingStage.ImportModel;

            // Keeps the persistent indicator synchronized with the active page.
            StageIndicator.CurrentStage = CurrentStage;

            // Displays the page belonging to the first onboarding stage.
            ShowInitialStage();
        }

        /// <summary>
        /// Gets the onboarding stage currently displayed by the shell.
        /// </summary>
        public OnboardingStage CurrentStage { get; private set; }

        /// <summary>
        /// Displays ModelImportPage inside the shell's stage frame.
        /// </summary>
        private void ShowInitialStage()
        {
            // Ask the internal frame to display the model-import page.
            bool navigationSucceeded =
                StageFrame.Navigate(typeof(ModelImportPage));

            // Stop immediately if the initial onboarding page could not be loaded.
            if (!navigationSucceeded)
            {
                throw new InvalidOperationException(
                    "The onboarding shell could not display ModelImportPage.");
            }
        }
    }
}
