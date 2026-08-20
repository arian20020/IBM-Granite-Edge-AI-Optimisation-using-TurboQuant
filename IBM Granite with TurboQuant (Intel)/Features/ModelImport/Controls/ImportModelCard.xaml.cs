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
            ShowAwaitingSelection();
        }

        public event RoutedEventHandler? BrowseFilesRequested;

        public event RoutedEventHandler? ChooseModelFolderRequested;

        public event RoutedEventHandler? CancelScanRequested;

        public ImportModelCardState CurrentState { get; private set; }

        /// <summary>
        /// Displays the initial browse and drag-and-drop presentation.
        /// </summary>
        internal void ShowAwaitingSelection()
        {
            SetVisibleState(ImportModelCardState.AwaitingSelection);
        }

        /// <summary>
        /// Displays the pending quick-scan presentation for one selected file.
        /// </summary>
        internal void ShowScanning(string selectedFileName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(selectedFileName);

            SetVisibleState(ImportModelCardState.Scanning);
            ScanningFileNameTextBlock.Text = selectedFileName;
        }

        internal void ShowFolderAccepted(string folderName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

            SetVisibleState(ImportModelCardState.SelectionAccepted);
            FolderAcceptedNameTextBlock.Text = folderName;
        }

        // Native drag feedback is limited to the accepted operation; card visuals stay stable.
        internal void ShowDragValidation(bool isValid)
        {
            SetVisibleState(
                isValid
                    ? ImportModelCardState.DragOverValid
                    : ImportModelCardState.DragOverInvalid);
        }

        internal void ClearDragValidation()
        {
            if (CurrentState is ImportModelCardState.DragOverValid or ImportModelCardState.DragOverInvalid)
            {
                SetVisibleState(ImportModelCardState.AwaitingSelection);
            }

        }

        /// <summary>
        /// Displays a controlled quick-scan failure and its user-facing details.
        /// </summary>
        internal void ShowFailure(
            string selectedFileName,
            string? failureCode,
            string? failureMessage)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(selectedFileName);

            SetVisibleState(ImportModelCardState.ScanFailed);
            FailureFileNameTextBlock.Text = selectedFileName;
            FailureCodeTextBlock.Text =
                string.IsNullOrWhiteSpace(failureCode)
                    ? "model-scan-failed"
                    : failureCode;
            FailureMessageTextBlock.Text =
                string.IsNullOrWhiteSpace(failureMessage)
                    ? "The selected model could not be scanned."
                    : failureMessage;
        }

        /// <summary>
        /// Displays the imported-model card using complete formatted values.
        /// </summary>
        internal void ShowSuccess(ImportedModelCardData model)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentException.ThrowIfNullOrWhiteSpace(model.FileName);
            ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelName);
            ArgumentException.ThrowIfNullOrWhiteSpace(model.Parameters);
            ArgumentException.ThrowIfNullOrWhiteSpace(model.Architecture);
            ArgumentException.ThrowIfNullOrWhiteSpace(model.Quantization);
            ArgumentException.ThrowIfNullOrWhiteSpace(model.FileSize);
            ArgumentException.ThrowIfNullOrWhiteSpace(model.DeclaredContext);

            SetVisibleState(ImportModelCardState.ScanSucceeded);
            SuccessFileNameTextBlock.Text = model.FileName;
            SuccessModelNameTextBlock.Text = model.ModelName;
            SuccessParametersTextBlock.Text = model.Parameters;
            SuccessArchitectureTextBlock.Text = model.Architecture;
            SuccessQuantizationTextBlock.Text = model.Quantization;
            SuccessFileSizeTextBlock.Text = model.FileSize;
            SuccessDeclaredContextTextBlock.Text = model.DeclaredContext;
        }

        /// <summary>
        /// Preserves the existing test seam for non-success states while making
        /// it impossible to show an incomplete success card.
        /// </summary>
        internal void SetState(
            ImportModelCardState state,
            string? selectedFileName = null,
            string? failureCode = null,
            string? failureMessage = null)
        {
            switch (state)
            {
                case ImportModelCardState.AwaitingSelection:
                    ShowAwaitingSelection();
                    return;

                case ImportModelCardState.DragOverValid:
                    ShowDragValidation(isValid: true);
                    return;

                case ImportModelCardState.DragOverInvalid:
                    ShowDragValidation(isValid: false);
                    return;

                case ImportModelCardState.SelectionAccepted:
                    ShowFolderAccepted(selectedFileName ?? string.Empty);
                    return;

                case ImportModelCardState.Scanning:
                    ShowScanning(selectedFileName ?? string.Empty);
                    return;

                case ImportModelCardState.ScanFailed:
                    ShowFailure(
                        selectedFileName ?? string.Empty,
                        failureCode,
                        failureMessage);
                    return;

                case ImportModelCardState.ScanSucceeded:
                    throw new InvalidOperationException(
                        "Use ShowSuccess so the success card receives complete data.");

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(state),
                        state,
                        "Unknown model-import card state.");
            }
        }

        private void SetVisibleState(ImportModelCardState state)
        {
            CurrentState = state;

            AwaitingSelectionView.Visibility =
                state is ImportModelCardState.AwaitingSelection
                    or ImportModelCardState.DragOverValid
                    or ImportModelCardState.DragOverInvalid
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
            FolderAcceptedView.Visibility =
                state == ImportModelCardState.SelectionAccepted
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            ClearDragValidationPresentation();
            ClearScanningValues();
            ClearFailureValues();
            ClearSuccessValues();
            FolderAcceptedNameTextBlock.Text = string.Empty;
        }

        private void ClearDragValidationPresentation()
        {
        }

        private void ClearScanningValues()
        {
            ScanningFileNameTextBlock.Text = string.Empty;
        }

        private void ClearFailureValues()
        {
            FailureFileNameTextBlock.Text = string.Empty;
            FailureCodeTextBlock.Text = string.Empty;
            FailureMessageTextBlock.Text = string.Empty;
        }

        private void ClearSuccessValues()
        {
            SuccessFileNameTextBlock.Text = string.Empty;
            SuccessModelNameTextBlock.Text = string.Empty;
            SuccessParametersTextBlock.Text = string.Empty;
            SuccessArchitectureTextBlock.Text = string.Empty;
            SuccessQuantizationTextBlock.Text = string.Empty;
            SuccessFileSizeTextBlock.Text = string.Empty;
            SuccessDeclaredContextTextBlock.Text = string.Empty;
        }

        private void BrowseFilesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BrowseFilesRequested?.Invoke(this, e);
        }

        private void ChooseModelFolderButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ChooseModelFolderRequested?.Invoke(this, e);
        }

        private void CancelScanButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            CancelScanRequested?.Invoke(this, e);
        }
    }
}
