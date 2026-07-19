using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelImport
{
    /// <summary>
    /// Represents the page where the user begins the model-import workflow.
    /// </summary>
    public sealed partial class ModelImportPage : Page
    {
        // Retains the chosen route for later import and validation stages.
        private ModelFormatSelection _selectedFormat =
            ModelFormatSelection.None;

        // Retains the selected path without inspecting or loading the model yet.
        private string? _selectedModelPath;

        /// <summary>
        /// Creates the page and loads its XAML controls.
        /// </summary>
        public ModelImportPage()
        {
            // Loads and connects the controls declared in ModelImportPage.xaml.
            InitializeComponent();
        }

        /// <summary>
        /// Opens the separate recommended-model selection page.
        /// </summary>
        private void RecommendedModelDownloadButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // Navigate to the recommended-model download workflow.
            Frame.Navigate(typeof(RecommendedModelDownloadPage));
        }

        /// <summary>
        /// Opens the format dialog and then runs the selected file-picker route.
        /// </summary>
        private async void BrowseFilesButton_ClickAsync(
            object sender,
            RoutedEventArgs e)
        {
            // Adapt the real WinUI dialog to the testable workflow interface.
            ModelFormatSelectionDialogService formatSelectionService =
                new ModelFormatSelectionDialogService(Content.XamlRoot);

            // Adapt the real Windows GGUF picker to the workflow interface.
            GgufModelFilePicker ggufModelFilePicker =
                new GgufModelFilePicker();

            // Coordinate the two selections without placing WinUI logic in tests.
            ModelImportWorkflow workflow =
                new ModelImportWorkflow(
                    formatSelectionService,
                    ggufModelFilePicker);

            // Wait for format selection and, when applicable, file selection.
            await workflow.StartFileSelectionAsync();

            // A cancelled format dialog must leave the page state unchanged.
            if (workflow.SelectedFormat == ModelFormatSelection.None)
            {
                return;
            }

            // Retain the selected route for the later model-import stages.
            _selectedFormat = workflow.SelectedFormat;

            // Replace the path only when the user selected an actual file.
            if (workflow.SelectedModelPath is not null)
            {
                _selectedModelPath = workflow.SelectedModelPath;
            }

            // File selection alone is not model validation, so this stays disabled.
            ContinueToModelInspectionButton.IsEnabled =
                workflow.CanContinueToModelInspection;
        }
    }
}
