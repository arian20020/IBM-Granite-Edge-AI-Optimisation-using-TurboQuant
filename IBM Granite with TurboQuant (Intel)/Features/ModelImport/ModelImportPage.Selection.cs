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
        private string? _selectedOpenVinoDirectory;
        private string? _selectedOpenVinoDisplayName;
        private ModelSelectionOperationId? _selectedOpenVinoOperationId;

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
            _selectedOpenVinoDirectory = null;
            _selectedOpenVinoDisplayName = null;
            _selectedOpenVinoOperationId = null;
            ValidatedScanResult = null;
            CurrentRoute = null;
            HasValidatedModel = false;
            ContinueToModelInspectionButton.IsEnabled = false;
            ImportModelCardControl.ShowScanning(input.DisplayName);

            ModelSelectionResult? result = null;
            try
            {
                result = await _classifier.ClassifyAsync(next.Id, input, next.Token);
            }
            catch (OperationCanceledException) when (next.Token.IsCancellationRequested)
            {
                if (IsCurrent(next))
                {
                    ResetToAwaitingSelection();
                }

                return;
            }
            finally
            {
                // Always finish task-side operation ownership, including failures.
                next.Complete();
            }

            if (!IsCurrent(next) || next.Token.IsCancellationRequested || result is null)
            {
                return;
            }

            await ApplyCurrentResultAsync(next, input, result);
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
                _selectedOpenVinoDirectory = input.LocalPath;
                _selectedOpenVinoDisplayName = result.DisplayName;
                _selectedOpenVinoOperationId = operation.Id;
                HasValidatedModel = true;
                ContinueToModelInspectionButton.IsEnabled = true;
                return;
            }

            if (result.Route == ModelSelectionRoute.SourceModelDirectory)
            {
                HasValidatedModel = true;
                ContinueToModelInspectionButton.IsEnabled = true;
                SourceModelInspectionRequested?.Invoke(this,
                    new SourceModelInspectionRequestedEventArgs(operation.Id, result.DisplayName));
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

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            RetireActiveSelectionOperation();
            CancelActiveScan();
            ResetToAwaitingSelection();
            base.OnNavigatedFrom(e);
        }
    }
}
