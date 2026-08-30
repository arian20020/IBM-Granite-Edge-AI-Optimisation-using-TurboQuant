using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport
{
    public sealed partial class ModelImportPage
    {
        private readonly IModelSelectionClassifier _classifier;
        private ModelSelectionOperation? _activeOperation;
        private ModelSelectionOperationId? _acceptedFolderOperationId;
        private string? _acceptedFolderDisplayName;
        private string? _acceptedFolderLocalPath;
        private ModelSelectionResult? _acceptedFolderSelection;

        /// <summary>
        /// Submits one normalized candidate from either a picker or the future
        /// drop handler. This is the sole selection lifecycle entry point.
        /// </summary>
        internal async Task SubmitInputAsync(ModelSelectionInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            if (Volatile.Read(ref _isRetired) != 0)
            {
                return;
            }

            CancelDownloadedModelSearch();
            ClearSelectionAnnouncement();

            ModelSelectionOperation next = new();
            ModelSelectionOperation? retired = Interlocked.Exchange(
                ref _activeOperation, next);
            retired?.Retire();
            retired?.Dispose();

            SelectedModelPath = input.IsFolder ? null : input.LocalPath;
            ValidatedScanResult = null;
            CurrentRoute = null;
            _acceptedFolderOperationId = null;
            _acceptedFolderDisplayName = null;
            _acceptedFolderLocalPath = null;
            _acceptedFolderSelection = null;
            HasValidatedModel = false;
            ContinueToModelInspectionButton.IsEnabled = false;
            ImportModelCardControl.ShowScanning(input.DisplayName);

            try
            {
                ModelSelectionResult result = await _classifier.ClassifyAsync(next.Id, input, next.Token);
                if (Volatile.Read(ref _isRetired) != 0 ||
                    !IsCurrent(next) || next.Token.IsCancellationRequested || result.OperationId != next.Id)
                {
                    return;
                }

                await ApplyCurrentResultAsync(next, input, result);
            }
            catch (OperationCanceledException) when (next.Token.IsCancellationRequested)
            {
                if (IsCurrent(next))
                {
                    ResetToAwaitingSelection();
                }

                return;
            }
            catch (Exception)
            {
                if (IsCurrent(next) && !next.Token.IsCancellationRequested)
                {
                    CurrentRoute = null;
                    HasValidatedModel = false;
                    ContinueToModelInspectionButton.IsEnabled = false;
                    ImportModelCardControl.ShowFailure(input.DisplayName, "selection-access-denied", "This model could not be opened. Check that it is available on this computer, then try again.");
                }
            }
            finally
            {
                // Completion waits for both classifier and GGUF scanner work.
                next.Complete();
            }
        }

        private async Task ApplyCurrentResultAsync(
            ModelSelectionOperation operation,
            ModelSelectionInput input,
            ModelSelectionResult result)
        {
            if (!result.IsAccepted || result.Route is null)
            {
                CurrentRoute = null;
                ValidatedScanResult = null;
                HasValidatedModel = false;
                ContinueToModelInspectionButton.IsEnabled = false;
                ImportModelCardControl.ShowFailure(
                    result.DisplayName,
                    result.Diagnostic?.Code,
                    result.Diagnostic?.Message);
                return;
            }

            CurrentRoute = result.Route;
            if (result.Route == ModelSelectionRoute.OpenVinoDirectory)
            {
                HasValidatedModel = true;
                ContinueToModelInspectionButton.IsEnabled = true;
                _acceptedFolderOperationId = operation.Id;
                _acceptedFolderDisplayName = result.DisplayName;
                _acceptedFolderLocalPath = input.LocalPath;
                _acceptedFolderSelection = result;
                ImportModelCardControl.ShowFolderAccepted(result.DisplayName);
                return;
            }

            if (result.Route == ModelSelectionRoute.SourceModelDirectory)
            {
                HasValidatedModel = true;
                ContinueToModelInspectionButton.IsEnabled = true;
                _acceptedFolderOperationId = operation.Id;
                _acceptedFolderDisplayName = result.DisplayName;
                _acceptedFolderLocalPath = input.LocalPath;
                _acceptedFolderSelection = result;
                ImportModelCardControl.ShowFolderAccepted(result.DisplayName);
                return;
            }

            ModelQuickScanResult scanResult = await _scanModelAsync(
                FileImport.ModelFormatSelection.Gguf,
                input.LocalPath,
                operation.Token);
            if (!IsCurrent(operation) || operation.Token.IsCancellationRequested)
            {
                return;
            }

            ApplyScanResult(input.DisplayName, scanResult);
        }

        private bool IsCurrent(ModelSelectionOperation operation) =>
            ReferenceEquals(operation, Volatile.Read(ref _activeOperation));

        private void RetireActiveSelectionOperation()
        {
            ModelSelectionOperation? active = Interlocked.Exchange(
                ref _activeOperation, null);
            active?.Retire();
            active?.Dispose();
        }

        internal void CancelSelection()
        {
            CancelDownloadedModelSearch();
            RetireActiveSelectionOperation();
            ResetToAwaitingSelection();
        }

        // Keeps navigation retirement deterministic without requiring a Frame in tests.
        internal void RetireSelectionForNavigation()
        {
            _ = RetireForNavigationAsync();
        }

        internal Task RetireForNavigationAsync()
        {
            lock (_navigationRetirementLock)
            {
                if (_navigationRetirementTask is not null)
                {
                    return _navigationRetirementTask;
                }

                Interlocked.Exchange(ref _isRetired, 1);
                _automaticInspectionOperation = null;
                _automaticInspectionRequest = null;
                Loaded -= ModelImportPage_Loaded;
                _modelDownloadCoordinator.VerifiedModelAvailable -=
                    ModelDownloadCoordinator_VerifiedModelAvailable;
                CancelDownloadedModelSearch();
                RetireActiveSelectionOperation();
                CancelActiveScan();
                ResetToAwaitingSelection();
                Task detachCard = RecommendedModelDownloadCard.RetireAsync();
                _navigationRetirementTask = _ownsModelDownloadCoordinator
                    ? Task.WhenAll(detachCard, _modelDownloadCoordinator.RetireAsync())
                    : detachCard;
                return _navigationRetirementTask;
            }
        }

        private bool TryRequestFolderInspection()
        {
            ModelSelectionOperation? active = Volatile.Read(ref _activeOperation);
            if (!HasValidatedModel ||
                CurrentRoute is not (ModelSelectionRoute.OpenVinoDirectory or ModelSelectionRoute.SourceModelDirectory) ||
                active is null || active.Token.IsCancellationRequested ||
                _acceptedFolderOperationId != active.Id ||
                string.IsNullOrWhiteSpace(_acceptedFolderDisplayName) ||
                string.IsNullOrWhiteSpace(_acceptedFolderLocalPath) ||
                _acceptedFolderSelection is null)
            {
                return false;
            }

            string displayName = _acceptedFolderDisplayName;
            if (CurrentRoute == ModelSelectionRoute.OpenVinoDirectory)
            {
                var eventArguments = new OpenVinoInspectionRequestedEventArgs(
                    active.Id,
                    displayName);
                OpenVinoInspectionRequested?.Invoke(this, eventArguments);
                return CompleteFolderInspectionRequest(eventArguments.NavigationAccepted);
            }

            SourceModelConversionRequested?.Invoke(
                this,
                new SourceModelConversionRequestedEventArgs(_acceptedFolderSelection));

            // Source conversion has no navigator in this increment. Dispatch
            // consumes this exact current intent so a repeated Continue click
            // cannot emit it twice or create an executable action here.
            return CompleteFolderInspectionRequest(navigationAccepted: true);
        }

        private bool CompleteFolderInspectionRequest(bool navigationAccepted)
        {
            if (!navigationAccepted)
            {
                return false;
            }

            _acceptedFolderOperationId = null;
            _acceptedFolderDisplayName = null;
            _acceptedFolderLocalPath = null;
            _acceptedFolderSelection = null;
            return true;
        }

        // This is intentionally not an event payload. The shell obtains it
        // only while handling the active page's opaque operation token.
        internal bool TryGetAcceptedFolderLocalPath(
            ModelSelectionOperationId operationId,
            out string? localPath)
        {
            localPath = null;
            ModelSelectionOperation? active = Volatile.Read(ref _activeOperation);
            if (active is null || active.Token.IsCancellationRequested ||
                _acceptedFolderOperationId != operationId ||
                _acceptedFolderOperationId != active.Id ||
                string.IsNullOrWhiteSpace(_acceptedFolderLocalPath))
            {
                return false;
            }

            localPath = _acceptedFolderLocalPath;
            return true;
        }

        // A missing route implementation must fail closed without exposing the
        // selected folder path in UI text, diagnostics, or event payloads.
        internal void RejectFolderRoute(string failureCode, string userMessage)
        {
            string displayName = _acceptedFolderDisplayName ?? "selected folder";
            RetireActiveSelectionOperation();
            _acceptedFolderOperationId = null;
            _acceptedFolderDisplayName = null;
            _acceptedFolderLocalPath = null;
            _acceptedFolderSelection = null;
            CurrentRoute = null;
            HasValidatedModel = false;
            ContinueToModelInspectionButton.IsEnabled = false;
            ImportModelCardControl.ShowFailure(displayName, failureCode, userMessage);
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            await RetireForNavigationAsync();
        }
    }
}
