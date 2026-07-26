using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.FileImport.PickerRoute;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly Func<
            ModelFormatSelection,
            string,
            CancellationToken,
            Task<ModelQuickScanResult>> _scanModelAsync;
        private CancellationTokenSource? _scanCancellationTokenSource;

        // Public Constructor:clean entry point for WinUI and normal application code
        public ModelImportPage()
            : this(null, null, null)
        {
        }

        // the main constructor that performs the real setup of the page:
        // We use this to allow controlled replacement functions for tests (makes testing easier)
        internal ModelImportPage(
            Func<Task<ModelFormatSelection>>? selectModelFormatAsync,
            Func<Task<string?>>? pickGgufPathAsync,
            Func<
                ModelFormatSelection,
                string,
                CancellationToken,
                Task<ModelQuickScanResult>>? scanModelAsync = null)
        {
            InitializeComponent();

            _selectModelFormatAsync =
                selectModelFormatAsync ?? ShowModelFormatSelectionAsync;
            _pickGgufPathAsync = pickGgufPathAsync ?? PickGgufPathAsync;

            if (scanModelAsync is null)
            {
                ModelQuickScanner modelQuickScanner = new();
                _scanModelAsync = modelQuickScanner.ScanAsync;
            }
            else
            {
                _scanModelAsync = scanModelAsync;
            }
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

            if (selectedPath is null)
            {
                return;
            }

            SelectedModelPath = selectedPath;
            HasValidatedModel = false;
            ContinueToModelInspectionButton.IsEnabled = false;

            string selectedFileName = Path.GetFileName(selectedPath);

            ImportModelCardControl.SetState(
                ImportModelCardState.Scanning,
                selectedFileName);

            CancellationTokenSource scanCancellationTokenSource = new();
            _scanCancellationTokenSource = scanCancellationTokenSource;

            ModelQuickScanResult scanResult;
            bool shouldApplyResult;

            try
            {
                scanResult = await _scanModelAsync(
                    selectedFormat,
                    selectedPath,
                    scanCancellationTokenSource.Token);
            }
            finally
            {
                shouldApplyResult = ReferenceEquals(
                    _scanCancellationTokenSource,
                    scanCancellationTokenSource);

                if (shouldApplyResult)
                {
                    _scanCancellationTokenSource = null;
                }

                scanCancellationTokenSource.Dispose();
            }

            if (!shouldApplyResult ||
                scanResult.Outcome == ModelQuickScanOutcome.Cancelled)
            {
                return;
            }

            if (scanResult.Outcome == ModelQuickScanOutcome.Failure)
            {
                HasValidatedModel = false;
                ContinueToModelInspectionButton.IsEnabled = false;

                ImportModelCardControl.SetState(
                    ImportModelCardState.ScanFailed,
                    selectedFileName: selectedFileName,
                    failureCode: scanResult.FailureCode,
                    failureMessage: scanResult.UserMessage);

                return;
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
            CancellationTokenSource? activeScan =
                _scanCancellationTokenSource;
            _scanCancellationTokenSource = null;
            activeScan?.Cancel();

            SelectedModelPath = null;
            HasValidatedModel = false;
            ContinueToModelInspectionButton.IsEnabled = false;

            ImportModelCardControl.SetState(ImportModelCardState.AwaitingSelection);
        }
    }
}
