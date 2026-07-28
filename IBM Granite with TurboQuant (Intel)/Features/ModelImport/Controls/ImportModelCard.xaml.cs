using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GraniteEdgeAI.Features.ModelImport.Controls
{
    public sealed partial class ImportModelCard : UserControl
    {
        public ImportModelCard()
        {
            InitializeComponent();

            SetState(ImportModelCardState.AwaitingSelection);
        }

        public event RoutedEventHandler? BrowseFilesRequested;

        public event RoutedEventHandler? CancelScanRequested;

        public ImportModelCardState CurrentState { get; private set; }

        public void SetState(
            ImportModelCardState state,
            string? selectedFileName = null,
            string? failureCode = null,
            string? failureMessage = null)
        {
            CurrentState = state;

            AwaitingSelectionView.Visibility =
                state == ImportModelCardState.AwaitingSelection
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            ScanningView.Visibility =
                state == ImportModelCardState.Scanning
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            SuccessView.Visibility =
                state == ImportModelCardState.ScanSucceeded
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            FailureView.Visibility =
                state == ImportModelCardState.ScanFailed
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            string displayedFileName =
                selectedFileName ?? string.Empty;

            ScanningFileNameTextBlock.Text =
                state == ImportModelCardState.Scanning
                    ? displayedFileName
                    : string.Empty;

            SuccessFileNameTextBlock.Text =
                state == ImportModelCardState.ScanSucceeded
                    ? displayedFileName
                    : string.Empty;

            FailureFileNameTextBlock.Text =
                state == ImportModelCardState.ScanFailed
                    ? displayedFileName
                    : string.Empty;

            if (state == ImportModelCardState.ScanFailed)
            {
                FailureCodeTextBlock.Text =
                    string.IsNullOrWhiteSpace(failureCode)
                        ? "model-scan-failed"
                        : failureCode;

                FailureMessageTextBlock.Text =
                    string.IsNullOrWhiteSpace(failureMessage)
                        ? "The selected model could not be scanned."
                        : failureMessage;
            }
            else
            {
                FailureCodeTextBlock.Text = string.Empty;
                FailureMessageTextBlock.Text = string.Empty;
            }

            if (state != ImportModelCardState.ScanSucceeded)
            {
                ClearSuccessValues();
            }
        }

        /// <summary>
        /// Displays the imported-model card using already-formatted
        /// presentation values.
        /// </summary>
        internal void ShowSuccess(
            ImportedModelCardData model)
        {
            ArgumentNullException.ThrowIfNull(model);

            SetState(
                ImportModelCardState.ScanSucceeded,
                model.FileName);

            SuccessModelNameTextBlock.Text =
                model.ModelName;

            SuccessParametersTextBlock.Text =
                model.Parameters;

            SuccessArchitectureTextBlock.Text =
                model.Architecture;

            SuccessQuantizationTextBlock.Text =
                model.Quantization;

            SuccessFileSizeTextBlock.Text =
                model.FileSize;

            SuccessDeclaredContextTextBlock.Text =
                model.DeclaredContext;
        }

        private void ClearSuccessValues()
        {
            SuccessModelNameTextBlock.Text =
                string.Empty;

            SuccessParametersTextBlock.Text =
                string.Empty;

            SuccessArchitectureTextBlock.Text =
                string.Empty;

            SuccessQuantizationTextBlock.Text =
                string.Empty;

            SuccessFileSizeTextBlock.Text =
                string.Empty;

            SuccessDeclaredContextTextBlock.Text =
                string.Empty;
        }

        private void BrowseFilesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BrowseFilesRequested?.Invoke(this, e);
        }

        private void CancelScanButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            CancelScanRequested?.Invoke(this, e);
        }
    }
}