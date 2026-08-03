using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.FileImport.PickerRoute;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport
{
    /// <summary>
    /// Coordinates model-format selection, file picking, quick scanning, and
    /// the visible model-import state.
    /// </summary>
    public sealed partial class ModelImportPage : Page
    {
        // Delegate seams keep native UI and scanner dependencies replaceable in tests.
        private readonly Func<Task<ModelFormatSelection>> _selectModelFormatAsync;
        private readonly Func<Task<string?>> _pickGgufPathAsync;
        private readonly Func<
            ModelFormatSelection,
            string,
            CancellationToken,
            Task<ModelQuickScanResult>> _scanModelAsync;
        private readonly CultureInfo _displayCulture;
        private readonly Action<ModelQuickScanFailureDiagnostic>
            _recordScanFailure;

        // Identifies the scan whose result is currently allowed to update the page.
        private CancellationTokenSource? _scanCancellationTokenSource;

        public ModelImportPage()
            : this(null, null, null, null, null)
        {
        }

        internal ModelImportPage(
            Func<Task<ModelFormatSelection>>? selectModelFormatAsync,
            Func<Task<string?>>? pickGgufPathAsync,
            Func<
                ModelFormatSelection,
                string,
                CancellationToken,
                Task<ModelQuickScanResult>>? scanModelAsync = null,
            CultureInfo? displayCulture = null,
            Action<ModelQuickScanFailureDiagnostic>? recordScanFailure = null)
        {
            InitializeComponent();

            _selectModelFormatAsync =
                selectModelFormatAsync ?? ShowModelFormatSelectionAsync;
            _pickGgufPathAsync =
                pickGgufPathAsync ?? PickGgufPathAsync;

            if (scanModelAsync is null)
            {
                ModelQuickScanner modelQuickScanner = new();
                _scanModelAsync = modelQuickScanner.ScanAsync;
            }
            else
            {
                _scanModelAsync = scanModelAsync;
            }

            _displayCulture = displayCulture ?? CultureInfo.CurrentCulture;
            _recordScanFailure =
                recordScanFailure ?? WriteFailureDiagnosticToTrace;
        }

        internal string? SelectedModelPath { get; private set; }

        internal bool HasValidatedModel { get; private set; }

        internal ModelQuickScanResult? ValidatedScanResult { get; private set; }

        /// Raised when the user requests full inspection of the validated model.
        internal event EventHandler<ModelInspectionRequestedEventArgs>? ModelInspectionRequested;

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

            string selectedFileName = Path.GetFileName(selectedPath);
            PrepareForScan(selectedPath, selectedFileName);

            CancellationTokenSource scanCancellationTokenSource =
                BeginScan();
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
                shouldApplyResult = CompleteScan(
                    scanCancellationTokenSource);
                scanCancellationTokenSource.Dispose();
            }

            if (!shouldApplyResult)
            {
                // A removed or replaced scan must not alter the current page.
                return;
            }

            ApplyScanResult(selectedFileName, scanResult);
        }

        private void PrepareForScan(
            string selectedPath,
            string selectedFileName)
        {
            SelectedModelPath = selectedPath;
            ValidatedScanResult = null;
            HasValidatedModel = false;
            ContinueToModelInspectionButton.IsEnabled = false;
            ImportModelCardControl.ShowScanning(selectedFileName);
        }

        private CancellationTokenSource BeginScan()
        {
            CancellationTokenSource currentScan = new();
            CancellationTokenSource? previousScan =
                _scanCancellationTokenSource;

            // Publish the replacement before cancellation so the old result is stale.
            _scanCancellationTokenSource = currentScan;
            previousScan?.Cancel();

            return currentScan;
        }

        private bool CompleteScan(
            CancellationTokenSource completedScan)
        {
            if (!ReferenceEquals(
                _scanCancellationTokenSource,
                completedScan))
            {
                return false;
            }

            _scanCancellationTokenSource = null;
            return true;
        }

        private void ApplyScanResult(
            string selectedFileName,
            ModelQuickScanResult scanResult)
        {
            switch (scanResult.Outcome)
            {
                case ModelQuickScanOutcome.Cancelled:
                    ResetToAwaitingSelection();
                    return;

                case ModelQuickScanOutcome.Failure:
                    ApplyFailureResult(selectedFileName, scanResult);
                    return;

                case ModelQuickScanOutcome.Success:
                    ApplySuccessResult(selectedFileName, scanResult);
                    return;

                default:
                    throw new InvalidOperationException(
                        $"Unexpected model quick-scan outcome: {scanResult.Outcome}.");
            }
        }

        private void ApplyFailureResult(
            string selectedFileName,
            ModelQuickScanResult scanResult)
        {
            string failureCode =
                scanResult.FailureCode ?? "model-scan-failed";
            string userMessage =
                scanResult.UserMessage ??
                "The selected model could not be scanned.";
            string technicalMessage =
                scanResult.TechnicalMessage ??
                "No additional technical information was provided.";

            ValidatedScanResult = null;
            HasValidatedModel = false;
            ContinueToModelInspectionButton.IsEnabled = false;

            RecordFailureDiagnostic(
                new ModelQuickScanFailureDiagnostic(
                    selectedFileName,
                    failureCode,
                    technicalMessage));

            ImportModelCardControl.ShowFailure(
                selectedFileName,
                failureCode,
                userMessage);
        }

        private void ApplySuccessResult(
            string selectedFileName,
            ModelQuickScanResult scanResult)
        {
            ImportedModelCardData cardData =
                ImportedModelCardDataMapper.Create(
                    selectedFileName,
                    scanResult,
                    _displayCulture);

            ValidatedScanResult = scanResult;
            HasValidatedModel = true;
            ContinueToModelInspectionButton.IsEnabled = true;
            ImportModelCardControl.ShowSuccess(cardData);
        }

        private void RecordFailureDiagnostic(
            ModelQuickScanFailureDiagnostic diagnostic)
        {
            try
            {
                _recordScanFailure(diagnostic);
            }
            catch (Exception exception)
            {
                // Diagnostic reporting must never replace the controlled UI failure.
                Trace.TraceError(
                    "Recording model quick-scan failure '{0}' failed with {1}.",
                    diagnostic.FailureCode,
                    exception.GetType().Name);
            }
        }

        private static void WriteFailureDiagnosticToTrace(
            ModelQuickScanFailureDiagnostic diagnostic)
        {
            Trace.TraceWarning(
                "Model quick scan failed for '{0}' with code '{1}': {2}",
                diagnostic.SelectedFileName,
                diagnostic.FailureCode,
                diagnostic.TechnicalMessage);
        }

        private void CancelActiveScan()
        {
            CancellationTokenSource? activeScan =
                _scanCancellationTokenSource;
            _scanCancellationTokenSource = null;
            activeScan?.Cancel();
        }

        private void ResetToAwaitingSelection()
        {
            SelectedModelPath = null;
            ValidatedScanResult = null;
            HasValidatedModel = false;
            ContinueToModelInspectionButton.IsEnabled = false;
            ImportModelCardControl.ShowAwaitingSelection();
        }

        private async Task<ModelFormatSelection>
            ShowModelFormatSelectionAsync()
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

        private async void ImportModelCard_BrowseFilesRequested(
            object sender,
            RoutedEventArgs e)
        {
            await BrowseFilesAsync();
        }

        private void ImportModelCard_CancelScanRequested(
            object sender,
            RoutedEventArgs e)
        {
            CancelActiveScan();
            ResetToAwaitingSelection();
        }

        /// <summary>
        /// Requests model inspection only when a valid scanned model is available.
        /// </summary>
        /// <returns>
        /// True when the request is accepted; otherwise, false.
        /// </returns>
        internal bool TryRequestModelInspection()
        {
            // Copy the current path into a local variable so that the same validated
            // value is used throughout this method.
            string? selectedModelPath = SelectedModelPath;

            // Protect the navigation boundary even if this method is called directly.
            //
            // The disabled button is a user-interface guard, but the method must also
            // defend itself because future code or tests may call it independently.
            if (!HasValidatedModel ||
                ValidatedScanResult is null ||
                string.IsNullOrWhiteSpace(selectedModelPath))
            {
                return false;
            }

            // Tell the onboarding shell that the user wants to inspect this model.
            //
            // ModelImportPage deliberately does not manipulate StageFrame directly.
            ModelInspectionRequested?.Invoke(
                this,
                new ModelInspectionRequestedEventArgs(selectedModelPath));

            // Report that the current state allowed the request.
            return true;
        }

        // Handles the Continue to model inspection button.
        private void ContinueToModelInspectionButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // Use the guarded method instead of navigating directly from this page.
            TryRequestModelInspection();
        }
    }
}
