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

        /// <summary>
        /// Submits one normalized candidate from either a picker or the future
        /// drop handler. This is the sole selection lifecycle entry point.
        /// </summary>
        internal async Task SubmitInputAsync(ModelSelectionInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

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
            HasValidatedModel = false;
            ContinueToModelInspectionButton.IsEnabled = false;
            ImportModelCardControl.ShowScanning(input.DisplayName);

            try
            {
                ModelSelectionResult result = await _classifier.ClassifyAsync(next.Id, input, next.Token);
                if (!IsCurrent(next) || next.Token.IsCancellationRequested || result.OperationId != next.Id)
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
                return;
            }

            if (result.Route == ModelSelectionRoute.SourceModelDirectory)
            {
                HasValidatedModel = true;
                ContinueToModelInspectionButton.IsEnabled = true;
                _acceptedFolderOperationId = operation.Id;
                _acceptedFolderDisplayName = result.DisplayName;
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
            RetireActiveSelectionOperation();
            ResetToAwaitingSelection();
        }

        // Keeps navigation retirement deterministic without requiring a Frame in tests.
        internal void RetireSelectionForNavigation()
        {
            RetireActiveSelectionOperation();
            ResetToAwaitingSelection();
        }

        private bool TryRequestFolderInspection()
        {
            ModelSelectionOperation? active = Volatile.Read(ref _activeOperation);
            if (!HasValidatedModel ||
                CurrentRoute is not (ModelSelectionRoute.OpenVinoDirectory or ModelSelectionRoute.SourceModelDirectory) ||
                active is null || active.Token.IsCancellationRequested ||
                _acceptedFolderOperationId != active.Id ||
                string.IsNullOrWhiteSpace(_acceptedFolderDisplayName))
            {
                return false;
            }

            string displayName = _acceptedFolderDisplayName;
            _acceptedFolderOperationId = null;
            _acceptedFolderDisplayName = null;
            if (CurrentRoute == ModelSelectionRoute.OpenVinoDirectory)
            {
                OpenVinoInspectionRequested?.Invoke(this, new OpenVinoInspectionRequestedEventArgs(active.Id, displayName));
            }
            else
            {
                SourceModelInspectionRequested?.Invoke(this, new SourceModelInspectionRequestedEventArgs(active.Id, displayName));
            }
            return true;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            RetireSelectionForNavigation();
            CancelActiveScan();
            base.OnNavigatedFrom(e);
        }
    }
}
