using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelImport.FileImport
{
    /// <summary>
    /// Represents the model formats available from the
    /// model-format selection dialog.
    /// </summary>
    public enum ModelFormatSelection
    {
        // No format was selected because the user cancelled.
        None,

        // The user selected the GGUF model route.
        Gguf,

        // The user selected the OpenVINO model route.
        OpenVino
    }

    /// <summary>
    /// Displays the available model-format choices.
    /// </summary>
    public sealed partial class ModelFormatSelectionCard : ContentDialog
    {
        /// <summary>
        /// Creates the dialog and connects it to its XAML file.
        /// </summary>
        public ModelFormatSelectionCard()
        {
            // Load the controls declared in ModelFormatSelectionCard.xaml.
            InitializeComponent();
        }

        /// <summary>
        /// Gets the format chosen before the dialog was closed.
        /// </summary>
        public ModelFormatSelection SelectedFormat { get; private set; }
            = ModelFormatSelection.None;

        /// <summary>
        /// Closes the dialog without selecting a model format.
        /// </summary>
        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // Record that the user cancelled the format-selection stage.
            SelectedFormat = ModelFormatSelection.None;

            // Close the dialog and return control to ModelImportPage.
            Hide();
        }

        /// <summary>
        /// Records GGUF as the selected model format.
        /// </summary>
        private void GgufFormatButton_ClickAsync(
            object sender,
            RoutedEventArgs e)
        {
            // Store the GGUF choice so ModelImportPage can read it.
            SelectedFormat = ModelFormatSelection.Gguf;

            // Close the dialog so ModelImportPage can open the file picker.
            Hide();
        }

        /// <summary>
        /// Records OpenVINO as the selected model format.
        /// </summary>
        private void OpenVINOFormatButton_ClickAsync(
            object sender,
            RoutedEventArgs e)
        {
            // Store the OpenVINO choice for its later workflow.
            SelectedFormat = ModelFormatSelection.OpenVino;

            // Close the dialog and return control to ModelImportPage.
            Hide();
        }
    }
}