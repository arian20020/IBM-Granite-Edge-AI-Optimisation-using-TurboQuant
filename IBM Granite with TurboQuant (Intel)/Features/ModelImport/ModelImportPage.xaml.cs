using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.DragDropRoute;
using GraniteEdgeAI.Features.ModelImport.DownloadedModels;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.FileImport.PickerRoute;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
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
        private readonly Func<Task<ModelSelectionInput?>>? _pickOpenVinoInputAsync;
        private readonly Func<
            ModelFormatSelection,
            string,
            CancellationToken,
            Task<ModelQuickScanResult>> _scanModelAsync;
        private readonly CultureInfo _displayCulture;
        private readonly Action<ModelQuickScanFailureDiagnostic>
            _recordScanFailure;
        private readonly ModelImportDropHandler _dropHandler;

        // Identifies the scan whose result is currently allowed to update the page.
        private CancellationTokenSource? _scanCancellationTokenSource;
        private readonly object _navigationRetirementLock = new();
        private Task? _navigationRetirementTask;
        private int _isRetired;

        public ModelImportPage()
            : this(null, null, null, null, null)
        {
        }

        internal ModelImportPage(
            Func<Task<ModelFormatSelection>>? selectModelFormatAsync,
            Func<Task<string?>>? pickGgufPathAsync,
            Func<Task<string?>> pickOpenVinoPathAsync)
            : this(
                selectModelFormatAsync,
                pickGgufPathAsync,
                pickOpenVinoInputAsync:
                    AdaptOpenVinoPathPicker(pickOpenVinoPathAsync))
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
            IModelSelectionClassifier? classifier = null,
            IDownloadedModelFinder? downloadedModelFinder = null,
            Func<Task<ModelSelectionInput?>>? pickOpenVinoInputAsync = null,
            RecommendedModelOffer? downloadOffer = null,
            IRecommendedModelDownloadService? downloadService = null)
        {
            InitializeComponent();

            _selectModelFormatAsync =
                selectModelFormatAsync ?? ShowModelFormatSelectionAsync;
            _pickGgufPathAsync =
                pickGgufPathAsync ?? PickGgufPathAsync;
            _pickOpenVinoInputAsync = pickOpenVinoInputAsync;

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
            _downloadedModelFinder = downloadedModelFinder ?? new BoundedDownloadedModelFinder();
            _dropHandler = new ModelImportDropHandler(
                new ModelSelectionInputNormalizer());

            if (downloadOffer is not null && downloadService is not null)
            {
                BindRecommendedModelDownload(downloadOffer, downloadService);
            }
        }

        internal void BindRecommendedModelDownload(
            RecommendedModelOffer offer,
            IRecommendedModelDownloadService service)
        {
            ArgumentNullException.ThrowIfNull(offer);
            ArgumentNullException.ThrowIfNull(service);
            if (Volatile.Read(ref _isRetired) != 0)
            {
                throw new InvalidOperationException(
                    "A retired model import page cannot be rebound.");
            }
            RecommendedModelDownloadCard.VerifiedDownloadCompleted -=
                RecommendedModelDownloadCard_VerifiedDownloadCompleted;
            RecommendedModelDownloadCard.BindDownload(offer, service);
            RecommendedModelDownloadCard.VerifiedDownloadCompleted +=
                RecommendedModelDownloadCard_VerifiedDownloadCompleted;
        }

        private async void RecommendedModelDownloadCard_VerifiedDownloadCompleted(
            object? sender,
            CompletedModelDownload completedDownload)
        {
            if (Volatile.Read(ref _isRetired) != 0)
            {
                return;
            }
            await SubmitInputAsync(completedDownload.Selection);
            if (Volatile.Read(ref _isRetired) == 0 && HasValidatedModel)
            {
                ContinueToModelInspectionButton.Focus(FocusState.Programmatic);
            }
        }

        private static Func<Task<ModelSelectionInput?>> AdaptOpenVinoPathPicker(
            Func<Task<string?>> picker)
        {
            ArgumentNullException.ThrowIfNull(picker);
            return async () =>
            {
                string? path = await picker();
                return path is null
                    ? null
                    : new ModelSelectionInputNormalizer().FromPickerPath(
                        path,
                        isFolder: true);
            };
        }

        internal string? SelectedModelPath { get; private set; }

        internal bool HasValidatedModel { get; private set; }

        internal ModelQuickScanResult? ValidatedScanResult { get; private set; }

        internal ModelSelectionRoute? CurrentRoute { get; private set; }

        /// Raised when the user requests full inspection of the validated model.
        internal event EventHandler<ModelInspectionRequestedEventArgs>? ModelInspectionRequested;
        internal event EventHandler<OpenVinoInspectionRequestedEventArgs>? OpenVinoInspectionRequested;
        internal event EventHandler<SourceModelConversionRequestedEventArgs>? SourceModelConversionRequested;

        internal async Task BrowseFilesAsync()
        {
            if (Volatile.Read(ref _isRetired) != 0)
            {
                return;
            }
            ModelFormatSelection selectedFormat =
                await _selectModelFormatAsync();

            if (Volatile.Read(ref _isRetired) != 0)
            {
                return;
            }

            if (selectedFormat == ModelFormatSelection.OpenVino)
            {
                await PickOpenVinoFolderAsync();
                return;
            }

            if (selectedFormat != ModelFormatSelection.Gguf)
            {
                return;
            }

            string? selectedPath = await _pickGgufPathAsync();
            if (selectedPath is null || Volatile.Read(ref _isRetired) != 0)
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
            RecommendedModelDownloadCard.TryCancelDownload();
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
            ClearSelectionAnnouncement();
            SelectedModelPath = null;
            ValidatedScanResult = null;
            CurrentRoute = null;
            _acceptedFolderOperationId = null;
            _acceptedFolderDisplayName = null;
            _acceptedFolderLocalPath = null;
            HasValidatedModel = false;
            ContinueToModelInspectionButton.IsEnabled = false;
            ImportModelCardControl.ShowAwaitingSelection();
        }

        private void ClearSelectionAnnouncement()
        {
            SelectionAnnouncement.Text = string.Empty;
            SelectionAnnouncement.Visibility = Visibility.Collapsed;
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

        private async void ImportModelCard_ChooseModelFolderRequested(
            object sender,
            RoutedEventArgs e)
        {
            await PickOpenVinoFolderAsync();
        }

        private async Task PickOpenVinoFolderAsync()
        {
            if (Volatile.Read(ref _isRetired) != 0)
            {
                return;
            }
            ModelSelectionInput? input;
            if (_pickOpenVinoInputAsync is not null)
            {
                input = await _pickOpenVinoInputAsync();
            }
            else
            {
                var picker = new OpenVINOFolderPicker();
                input = await picker.PickInputAsync(
                    new ModelSelectionInputNormalizer());
            }

            if (input is not null && Volatile.Read(ref _isRetired) == 0)
            {
                await SubmitInputAsync(input);
            }
        }

        private void ModelDropTarget_DragOver(object sender, DragEventArgs e)
        {
            _dropHandler.HandleDragOver(e);
            ImportModelCardControl.ShowDragValidation(
                e.AcceptedOperation == DataPackageOperation.Copy);
        }

        private void ModelDropTarget_DragLeave(object sender, DragEventArgs e)
        {
            ImportModelCardControl.ClearDragValidation();
        }

        private async void ModelDropTarget_Drop(object sender, DragEventArgs e)
        {
            ImportModelCardControl.ClearDragValidation();
            await _dropHandler.HandleDropAsync(
                e,
                SubmitInputAsync,
                RejectDroppedSelectionAsync,
                CancellationToken.None);
        }

        // Task 5 delivers only a safe diagnostic for rejected native drops.
        // Accepted items still enter solely through SubmitInputAsync.
        private Task RejectDroppedSelectionAsync(ModelSelectionDiagnostic diagnostic)
        {
            CancelSelection();
            ImportModelCardControl.ShowFailure(
                "dropped item",
                diagnostic.Code,
                diagnostic.Message);
            // A collapsed live region is absent from the accessibility tree.
            // Reveal it before changing text so assistive technology can announce
            // the recoverable rejection without exposing a local path.
            SelectionAnnouncement.Visibility = Visibility.Visible;
            SelectionAnnouncement.Text = diagnostic.Message;
            return Task.CompletedTask;
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
            if (TryRequestFolderInspection())
            {
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
