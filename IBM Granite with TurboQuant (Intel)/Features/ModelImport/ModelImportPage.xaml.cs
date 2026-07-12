using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelImport
{
    /// <summary>
    /// Represents the page where the user begins the model-import workflow.
    /// </summary>
    public sealed partial class ModelImportPage : Page
    {
        public ModelImportPage()
        {
            // Loads and connects the controls declared in ModelImportPage.xaml.
            InitializeComponent();
        }

        /*
            Opens or closes the recommended-model section.

            This is view-only behaviour because it only controls whether
            part of the interface is visible.
        */
        private void RecommendedModelToggleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // Checks whether the recommended-model card is currently hidden.
            bool shouldOpen =
                RecommendedModelCard.Visibility == Visibility.Collapsed;

            // Shows the card when closed, or hides it when already open.
            RecommendedModelCard.Visibility = shouldOpen
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Changes the arrow to show what clicking the button will do next.
            RecommendedModelToggleArrow.Text = shouldOpen
                ? "↑"
                : "↓";
        }
    }
}