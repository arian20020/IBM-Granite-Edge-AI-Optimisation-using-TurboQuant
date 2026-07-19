using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelImport.FileImport
{
    /// <summary>
    /// Displays the available model-format choices.
    /// </summary>
    public sealed partial class ModelFormatSelectionCard : ContentDialog
    {
        /// <summary>
        /// Creates the dialog and loads its XAML controls.
        /// </summary>
        public ModelFormatSelectionCard()
        {
            // Connect this code-behind class to ModelFormatSelectionCard.xaml.
            InitializeComponent();
        }

        /// <summary>
        /// Gets the format selected before the dialog was closed.
        /// </summary>
        public ModelFormatSelection SelectedFormat { get; private set; }
            = ModelFormatSelection.None;

        /// <summary>
        /// Closes the dialog without selecting an import route.
        /// </summary>
        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // Preserve the explicit cancellation result for the calling page.
            SelectedFormat = ModelFormatSelection.None;

            // Close the dialog and reveal the unchanged ModelImportPage.
            Hide();
        }

        /// <summary>
        /// Records the GGUF route and returns control to ModelImportPage.
        /// </summary>
        private void GgufFormatButton_ClickAsync(
            object sender,
            RoutedEventArgs e)
        {
            // Record only the chosen format; the page opens the real picker.
            SelectedFormat = ModelFormatSelection.Gguf;

            // Finish the format-selection stage.
            Hide();
        }

        /// <summary>
        /// Records the OpenVINO route for its later implementation stage.
        /// </summary>
        private void OpenVINOFormatButton_ClickAsync(
            object sender,
            RoutedEventArgs e)
        {
            // Retain the choice without opening the deferred folder workflow.
            SelectedFormat = ModelFormatSelection.OpenVino;

            // Finish the format-selection stage.
            Hide();
        }
    }
}
