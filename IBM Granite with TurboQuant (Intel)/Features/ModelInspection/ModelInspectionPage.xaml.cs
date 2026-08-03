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
            NavigationEventArgs e)
        {
            // Preserve the normal WinUI Page navigation lifecycle.
            base.OnNavigatedTo(e);

            // Confirm that the navigation parameter is a non-empty string.
            if (e.Parameter is not string modelPath ||
                string.IsNullOrWhiteSpace(modelPath))
            {
                throw new ArgumentException(
                    "ModelInspectionPage requires a non-empty model path.",
                    nameof(e));
            }

            // Store the exact original path for the future inspection service.
            SelectedModelPath = modelPath;
        }
    }
}