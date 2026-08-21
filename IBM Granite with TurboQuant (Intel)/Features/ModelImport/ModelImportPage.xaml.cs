using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.FileImport.PickerRoute;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
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
            Action<ModelQuickScanFailureDiagnostic>? recordScanFailure = null,
            IModelSelectionClassifier? classifier = null)
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
            _classifier = classifier ?? new BoundedModelSelectionClassifier();
        }

        internal string? SelectedModelPath { get; private set; }

        internal bool HasValidatedModel { get; private set; }

        internal ModelQuickScanResult? ValidatedScanResult { get; private set; }

        internal ModelSelectionRoute? CurrentRoute { get; private set; }

        /// Raised when the user requests full inspection of the validated model.
        internal event EventHandler<ModelInspectionRequestedEventArgs>? ModelInspectionRequested;
        internal event EventHandler<OpenVinoInspectionRequestedEventArgs>? OpenVinoInspectionRequested;
        internal event EventHandler<SourceModelInspectionRequestedEventArgs>? SourceModelInspectionRequested;

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

            await SubmitInputAsync(
                new ModelSelectionInput(
                    selectedPath,
                    Path.GetFileName(selectedPath),
                    isFolder: false));
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
            RetireActiveSelectionOperation();
        }

        private void ResetToAwaitingSelection()
        {
            SelectedModelPath = null;
            _selectedOpenVinoDirectory = null;
            _selectedOpenVinoDisplayName = null;
            _selectedOpenVinoOperationId = null;
            ValidatedScanResult = null;
            CurrentRoute = null;
            HasValidatedModel = false;
            ContinueToModelInspectionButton.IsEnabled = false;
            ImportModelCardControl.ShowAwaitingSelection();
        }

        /// <summary>
        /// Invalidates a model that no longer agrees with its successful quick
        /// scan immediately before the navigation handoff.
        /// </summary>
        private void InvalidateChangedModelSelection(
            string selectedModelPath)
        {
            // Preserve only the safe final file name for UI and diagnostics.
            string selectedFileName = Path.GetFileName(selectedModelPath);
            const string failureCode = "model-selection-changed";
            const string userMessage =
                "The selected model changed after validation. Choose the model again.";
            const string technicalMessage =
                "The model file could not be reopened with the identity expected from its successful quick scan.";

            // Remove every value that could otherwise permit stale navigation.
            SelectedModelPath = null;
            ValidatedScanResult = null;
            HasValidatedModel = false;
            ContinueToModelInspectionButton.IsEnabled = false;

            // Record a path-minimised diagnostic without exposing the directory.
            RecordFailureDiagnostic(
                new ModelQuickScanFailureDiagnostic(
                    selectedFileName,
                    failureCode,
                    technicalMessage));

            // Keep the user on Model Import with one recoverable failure state.
            ImportModelCardControl.ShowFailure(
                selectedFileName,
                failureCode,
                userMessage);
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
            if (CurrentRoute == ModelSelectionRoute.OpenVinoDirectory)
            {
                if (!HasValidatedModel ||
                    _selectedOpenVinoOperationId is not ModelSelectionOperationId operationId ||
                    string.IsNullOrWhiteSpace(_selectedOpenVinoDirectory) ||
                    string.IsNullOrWhiteSpace(_selectedOpenVinoDisplayName))
                {
                    return false;
                }

                OpenVinoInspectionRequested?.Invoke(
                    this,
                    new OpenVinoInspectionRequestedEventArgs(
                        operationId,
                        _selectedOpenVinoDirectory,
                        _selectedOpenVinoDisplayName));
                return true;
            }

            // Copy the current state into locals so one coherent validated
            // selection is used throughout this boundary method.
            string? selectedModelPath = SelectedModelPath;
            ModelQuickScanResult? validatedScanResult = ValidatedScanResult;

            // Protect the navigation boundary even if this method is called directly.
            // The disabled button is only the first user-interface guard.
            if (!HasValidatedModel ||
                validatedScanResult is null ||
                string.IsNullOrWhiteSpace(selectedModelPath))
            {
                return false;
            }

            // Reopen the file and convert the successful scan into one immutable
            // request immediately before any navigation is raised.
            bool requestCreated = ModelInspectionRequestFactory.TryCreate(
                selectedModelPath,
                validatedScanResult,
                out ModelInspectionRequest? request);

            // A missing, changed, locked, or otherwise untrusted selection must
            // remain on this page and require explicit reselection.
            if (!requestCreated || request is null)
            {
                InvalidateChangedModelSelection(selectedModelPath);
                return false;
            }

            // Tell the onboarding shell to forward the exact validated request.
            // ModelImportPage deliberately does not manipulate StageFrame directly.
            ModelInspectionRequested?.Invoke(
                this,
                new ModelInspectionRequestedEventArgs(request));

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
