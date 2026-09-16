using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.DragDropRoute;
using GraniteEdgeAI.Features.ModelImport.DownloadedModels;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.FileImport.PickerRoute;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
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
    internal enum ModelImportPresentationMode
    {
        AllSources,
        SourceChoiceFirst,
        RecommendedDownloadOnly,
    }

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
        private readonly ModelDownloadCoordinator _modelDownloadCoordinator;
        private readonly bool _ownsModelDownloadCoordinator;
        private ModelDownloadOperationId? _automaticDownloadSubmission;
        private ModelDownloadOperationId? _automaticInspectionOperation;
        private ModelInspectionRequest? _automaticInspectionRequest;
        private Task _automaticDownloadHandoffTask = Task.CompletedTask;
        private readonly TimeSpan _automaticHandoffRetirementTimeout;
        private readonly object _navigationRetirementLock = new();
        private Task? _navigationRetirementTask;
        private int _isRetired;
        private ModelImportPresentationMode _presentationMode;
        private bool _presentationEntryActionStarted;
        private int _browseInProgress;

        // Identifies the scan whose result is currently allowed to update the page.
        private CancellationTokenSource? _scanCancellationTokenSource;

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
            ModelDownloadCoordinator? modelDownloadCoordinator = null,
            TimeSpan? automaticHandoffRetirementTimeout = null)
        {
            InitializeComponent();
            RecommendedModelPreference.ValueChanged +=
                RecommendedModelPreference_ValueChanged;

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
            _ownsModelDownloadCoordinator = modelDownloadCoordinator is null;
            _modelDownloadCoordinator = modelDownloadCoordinator ?? ModelDownloadComposition.CreateDefault();
            _automaticHandoffRetirementTimeout = automaticHandoffRetirementTimeout ?? TimeSpan.FromSeconds(5);
            if (_automaticHandoffRetirementTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(automaticHandoffRetirementTimeout));
            _modelDownloadCoordinator.VerifiedModelAvailable += ModelDownloadCoordinator_VerifiedModelAvailable;
            AttachDownloadPresentation(_modelDownloadCoordinator);
            ShowAwaitingSelection();
            Loaded += ModelImportPage_Loaded;
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

        internal void SetPresentationMode(ModelImportPresentationMode mode)
        {
            if (!Enum.IsDefined(mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            _presentationMode = mode;
            bool recommendedOnly =
                mode == ModelImportPresentationMode.RecommendedDownloadOnly;
            ImportAwaitingSelectionPanel.Visibility = recommendedOnly
                ? Visibility.Collapsed
                : Visibility.Visible;
            Grid.SetColumn(RecommendedDownloadPanel, recommendedOnly ? 0 : 1);
            Grid.SetColumnSpan(RecommendedDownloadPanel, recommendedOnly ? 2 : 1);
            RecommendedDownloadPanel.Margin = new Thickness(0);
        }

        /// Raised when the user requests full inspection of the validated model.
        internal event EventHandler<ModelInspectionRequestedEventArgs>? ModelInspectionRequested;
        internal event EventHandler<VerifiedDownloadInspectionReadyEventArgs>? VerifiedDownloadInspectionReady;
        internal event EventHandler<OpenVinoInspectionRequestedEventArgs>? OpenVinoInspectionRequested;
        internal event EventHandler<SourceModelConversionRequestedEventArgs>? SourceModelConversionRequested;

        internal async Task BrowseFilesAsync()
        {
            // Automatic entry and a user click can overlap. WinUI permits
            // only one ContentDialog, so keep one picker flow until it ends.
            if (Interlocked.CompareExchange(ref _browseInProgress, 1, 0) != 0)
            {
                return;
            }
            try
            {
                await BrowseFilesCoreAsync();
            }
            finally
            {
                Volatile.Write(ref _browseInProgress, 0);
            }
        }

        private async Task BrowseFilesCoreAsync()
        {
            if (Volatile.Read(ref _isRetired) != 0)
            {
                return;
            }
            RetireAutomaticDownloadHandoff();
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
            SelectedModelPath = selectedPath;
            ValidatedScanResult = null;
            HasValidatedModel = false;
            BtnContinueToInspection.IsEnabled = false;
            ShowScanning(selectedFileName);
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
            BtnContinueToInspection.IsEnabled = false;

            RecordFailureDiagnostic(
                new ModelQuickScanFailureDiagnostic(
                    selectedFileName,
                    failureCode,
                    technicalMessage));

            ShowFailure(
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
            BtnContinueToInspection.IsEnabled = true;
            ShowSuccess(cardData);
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
            BtnContinueToInspection.IsEnabled = false;
            ShowAwaitingSelection();
        }

        private void ClearSelectionAnnouncement()
        {
            LocalImportStateStatus.Text = "Awaiting selection";
            LocalImportStateStatus.Visibility = Visibility.Visible;
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

            // remove every value that could otherwise permit stale navigation
            SelectedModelPath = null;
            ValidatedScanResult = null;
            HasValidatedModel = false;
            BtnContinueToInspection.IsEnabled = false;

            // record a path-minimised diagnostic without exposing the directory
            RecordFailureDiagnostic(
                new ModelQuickScanFailureDiagnostic(
                    selectedFileName,
                    failureCode,
                    technicalMessage));

            // Keep the user on Model Import with one recoverable failure state.
            ShowFailure(
                selectedFileName,
                failureCode,
                userMessage);
        }

        private async Task<ModelFormatSelection>
            ShowModelFormatSelectionAsync()
        {
            ModelFormatSelectionCard selectFormat = new();
            selectFormat.XamlRoot = Content.XamlRoot;
            selectFormat.RequestedTheme = ActualTheme;

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
            ShowDragValidation(
                e.AcceptedOperation == DataPackageOperation.Copy);
        }

        private void ModelDropTarget_DragLeave(object sender, DragEventArgs e)
        {
            ClearDragValidation();
        }

        private async void ModelDropTarget_Drop(object sender, DragEventArgs e)
        {
            if (Volatile.Read(ref _isRetired) != 0)
            {
                return;
            }
            RetireAutomaticDownloadHandoff();
            ClearDragValidation();
            await _dropHandler.HandleDropAsync(
                e,
                SubmitDroppedInputAsync,
                RejectDroppedSelectionAsync,
                CancellationToken.None);
        }

        internal Task SubmitDroppedInputAsync(ModelSelectionInput input)
        {
            RetireAutomaticDownloadHandoff();
            return SubmitInputAsync(input);
        }

        private void ModelDownloadCoordinator_VerifiedModelAvailable(
            object? sender,
            VerifiedModelAvailableEventArgs e)
        {
            lock (_navigationRetirementLock)
            {
                if (Volatile.Read(ref _isRetired) != 0)
                {
                    return;
                }
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _automaticDownloadHandoffTask = Task.WhenAll(
                    _automaticDownloadHandoffTask,
                    completion.Task);
                _ = RunVerifiedDownloadHandoffAsync(e, completion);
            }
        }

        private async Task RunVerifiedDownloadHandoffAsync(
            VerifiedModelAvailableEventArgs e,
            TaskCompletionSource completion)
        {
            try
            {
                await CompleteVerifiedDownloadHandoffAsync(e);
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        private async Task CompleteVerifiedDownloadHandoffAsync(VerifiedModelAvailableEventArgs e)
        {
            if (!_modelDownloadCoordinator.TryClaimVerifiedModel(e.OperationId, out VerifiedDownloadedModel? model) ||
                model is null)
            {
                return;
            }

            lock (_navigationRetirementLock)
            {
                if (Volatile.Read(ref _isRetired) != 0 ||
                    !_modelDownloadCoordinator.IsAutomaticHandoffAuthorized(e.OperationId))
                    return;
                _automaticDownloadSubmission = e.OperationId;
            }
            await SubmitInputAsync(new ModelSelectionInput(
                model.LocalPath,
                model.DisplayName,
                isFolder: false));

            if (Volatile.Read(ref _isRetired) == 0 &&
                _automaticDownloadSubmission == e.OperationId &&
                _modelDownloadCoordinator.IsAutomaticHandoffAuthorized(e.OperationId) &&
                HasValidatedModel &&
                CurrentRoute == ModelSelectionRoute.Gguf)
            {
                string? selectedPath = SelectedModelPath;
                ModelQuickScanResult? scan = ValidatedScanResult;
                if (!string.IsNullOrWhiteSpace(selectedPath) && scan is not null
                    && ModelInspectionRequestFactory.TryCreate(selectedPath, scan, out ModelInspectionRequest? request)
                    && request is not null)
                {
                    lock (_navigationRetirementLock)
                    {
                        if (Volatile.Read(ref _isRetired) != 0 ||
                            _automaticDownloadSubmission != e.OperationId ||
                            !_modelDownloadCoordinator.IsAutomaticHandoffAuthorized(e.OperationId))
                            return;
                        _automaticInspectionOperation = e.OperationId;
                        _automaticInspectionRequest = request;
                        PublishVerifiedDownloadInspectionReady(
                            new VerifiedDownloadInspectionReadyEventArgs(e.OperationId, e.DisplayName));
                    }
                }
            }
        }

        // C0 claims this capability directly from the still-live import page and installs it
        // into Model Inspection without placing the path-bearing request in an event or Frame parameter.
        internal bool TryClaimVerifiedDownloadInspection(
            ModelDownloadOperationId operationId,
            out ModelInspectionRequest? request)
        {
            lock (_navigationRetirementLock)
            {
                if (Volatile.Read(ref _isRetired) == 0
                    && _modelDownloadCoordinator.IsAutomaticHandoffAuthorized(operationId)
                    && _automaticInspectionOperation == operationId
                    && _automaticInspectionRequest is not null)
                {
                    request = _automaticInspectionRequest;
                    _automaticInspectionOperation = null;
                    _automaticInspectionRequest = null;
                    return true;
                }
                request = null;
                return false;
            }
        }

        private void PublishVerifiedDownloadInspectionReady(VerifiedDownloadInspectionReadyEventArgs eventArguments)
        {
            Delegate[] handlers = VerifiedDownloadInspectionReady?.GetInvocationList() ?? [];
            foreach (Delegate candidate in handlers)
                try { ((EventHandler<VerifiedDownloadInspectionReadyEventArgs>)candidate)(this, eventArguments); }
                catch (Exception exception)
                {
                    Trace.TraceWarning("A verified-download ready observer failed with {0}.", exception.GetType().Name);
                }
        }

        private async void ModelImportPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ModelImportPage_Loaded;
            if (Volatile.Read(ref _isRetired) != 0)
            {
                return;
            }
            try
            {
                await _modelDownloadCoordinator.RecoverAsync(CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // a user-started operation won the race with startup recovery
            }
            catch (OperationCanceledException)
            {
                // Page lifetime cancellation leaves any durable partial available for a later resume.
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Startup recovery is best-effort. The normal card remains available for an explicit retry.
            }

            if (!_presentationEntryActionStarted &&
                _presentationMode == ModelImportPresentationMode.SourceChoiceFirst &&
                Volatile.Read(ref _isRetired) == 0)
            {
                _presentationEntryActionStarted = true;
                await BrowseFilesAsync();
            }
        }

        private void RetireAutomaticDownloadHandoff()
        {
            _automaticDownloadSubmission = null;
            _automaticInspectionOperation = null;
            _automaticInspectionRequest = null;
            _modelDownloadCoordinator.RetireAutomaticHandoff();
        }

        // Task 5 delivers only a safe diagnostic for rejected native drops.
        // Accepted items still enter solely through SubmitInputAsync.
        private Task RejectDroppedSelectionAsync(ModelSelectionDiagnostic diagnostic)
        {
            CancelSelection();
            ShowFailure(
                "dropped item",
                diagnostic.Code,
                diagnostic.Message);
            // a collapsed live region is absent from the accessibility tree
            // reveal it before changing text so assistive technology can announce
            // the recoverable rejection without exposing a local path
            LocalImportStateStatus.Visibility = Visibility.Visible;
            LocalImportStateStatus.Text = diagnostic.Message;
            return Task.CompletedTask;
        }

        private void ImportModelCard_CancelScanRequested(
            object sender,
            RoutedEventArgs e)
        {
            CancelActiveScan();
            ResetToAwaitingSelection();
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (IsLoaded && ReferenceEquals(Frame?.Content, this) &&
                    CurrentImportState == ImportModelCardState.AwaitingSelection)
                {
                    FocusChooseModel();
                }
            });
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

            // copy the current state into locals so one coherent validated
            // selection is used throughout this boundary method
            string? selectedModelPath = SelectedModelPath;
            ModelQuickScanResult? validatedScanResult = ValidatedScanResult;

            // Protect the navigation boundary even if this method is called directly.
            // the disabled button is only the first user-interface guard
            if (!HasValidatedModel ||
                validatedScanResult is null ||
                string.IsNullOrWhiteSpace(selectedModelPath))
            {
                return false;
            }

            // Reopen the file and convert the successful scan into one immutable
            // request immediately before any navigation is raised
            bool requestCreated = ModelInspectionRequestFactory.TryCreate(
                selectedModelPath,
                validatedScanResult,
                out ModelInspectionRequest? request);

            // a missing, changed, locked, or otherwise untrusted selection must
            // remain on this page and require explicit reselection
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

            // report that the current state allowed the request
            return true;
        }

        // Handles the Continue to model inspection button.
        private void ContinueToModelInspectionButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // use the guarded method instead of navigating directly from this page
            TryRequestModelInspection();
        }
    }
}
