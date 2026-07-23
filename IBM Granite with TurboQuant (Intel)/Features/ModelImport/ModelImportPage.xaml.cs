using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.FileImport.PickerRoute;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.Threading.Tasks;
using System.IO;

namespace GraniteEdgeAI.Features.ModelImport
{
    /// <summary>
    /// Represents the page where the user begins the model-import workflow.
    /// </summary>
    public sealed partial class ModelImportPage : Page //Declare the class called ModelImportPage inheriting from WinUIs Page class
    {
        //store functions that the page can call later:
        private readonly Func<Task<ModelFormatSelection>> _selectModelFormatAsync; 
        private readonly Func<Task<string?>> _pickGgufPathAsync;

        // Public Constructor:clean entry point for WinUI and normal application code
        public ModelImportPage()
            : this(null, null) //this(null, null) calls the internal constructor, so that i sees that both supplied parameters are null.
        {
        }

        // the main constructor that performs the real setup of the page:
        // We use this to allow controlled replacement functions for tests (makes testing easier)
        internal ModelImportPage(
            Func<Task<ModelFormatSelection>>? selectModelFormatAsync,
            Func<Task<string?>>? pickGgufPathAsync)
        {
            InitializeComponent();

            _selectModelFormatAsync =
                selectModelFormatAsync ?? ShowModelFormatSelectionAsync;
            _pickGgufPathAsync = pickGgufPathAsync ?? PickGgufPathAsync;
            //The ?? operator means “use the right-hand value when the left-hand value is null.
        }

        // tell the page where the selected file is located
        internal string? SelectedModelPath { get; private set; }

        //record the outcome after the scanner returns
        internal bool HasValidatedModel { get; private set; }

        // the click handler for the “Download a recommended model”
        private void RecommendedModelDownloadButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Frame.Navigate(typeof(RecommendedModelDownloadPage));
        }

        internal async Task BrowseFilesAsync()
        {
            ModelFormatSelection selectedFormat =
                await _selectModelFormatAsync();

            if (selectedFormat != ModelFormatSelection.Gguf)
            {
                return;
            }

            string? selectedPath = await _pickGgufPathAsync();

            if (selectedPath is not null)
            {
                SelectedModelPath = selectedPath;

                string selectedFileName = Path.GetFileName(selectedPath);

                ImportModelCardControl.SetState( ImportModelCardState.Scanning, selectedFileName);
            }
        }

        private async Task<ModelFormatSelection> ShowModelFormatSelectionAsync()
        {
            ModelFormatSelectionCard selectFormat = new();
            selectFormat.XamlRoot = Content.XamlRoot;

            await selectFormat.ShowAsync();

            return selectFormat.SelectedFormat;
        }

        private static async Task<string?> PickGgufPathAsync()
        {
            GgufModelFilePicker modelFilePicker = new();
            PickFileResult? selectedFile =
                await modelFilePicker.PickGGUFAsync();

            return selectedFile?.Path;
        }
        
        /// Responds when the import card asks the page to open the model picker.
        private async void ImportModelCard_BrowseFilesRequested(object sender, RoutedEventArgs e)
        {
            await BrowseFilesAsync();
        }

        private void ImportModelCard_CancelScanRequested(object sender, RoutedEventArgs e)
        {
            SelectedModelPath = null;

            ImportModelCardControl.SetState(ImportModelCardState.AwaitingSelection);
        }
    }
}
