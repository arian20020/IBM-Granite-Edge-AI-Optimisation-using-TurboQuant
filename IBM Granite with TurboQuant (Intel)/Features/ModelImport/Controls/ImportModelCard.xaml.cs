using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GraniteEdgeAI.Features.ModelImport.Controls
{
    public sealed partial class ImportModelCard : UserControl
    {
        private ImportModelCardState? _lastAnnouncedState;
        private bool _isInitializing = true;

        public ImportModelCard()
        {
            InitializeComponent();
            ShowAwaitingSelection();
            _isInitializing = false;
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
            _lastAnnouncedState = ImportModelCardState.AwaitingSelection;
        }

        /// <summary>
        /// Displays the pending quick-scan presentation for one selected file.
        /// </summary>
        internal void ShowScanning(string selectedFileName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(selectedFileName);

            SetVisibleState(ImportModelCardState.Scanning);
            ScanningFileNameTextBlock.Text = selectedFileName;
            AnnounceState(ImportModelCardState.Scanning, "Quick scan in progress.");
        }

        internal void ShowFolderAccepted(string folderName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

            SetVisibleState(ImportModelCardState.SelectionAccepted);
            FolderAcceptedNameTextBlock.Text = folderName;
            AnnounceState(ImportModelCardState.SelectionAccepted, "OpenVINO model folder selected. Ready for model inspection.");
        }

        // native drag feedback is limited to the accepted operation; only its surface darkens
        internal void ShowDragValidation(bool isValid)
        {
            SetVisibleState(
                isValid
                    ? ImportModelCardState.DragOverValid
                    : ImportModelCardState.DragOverInvalid);
            ValidDragHoverOverlay.Visibility =
                isValid ? Visibility.Visible : Visibility.Collapsed;
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
            string resolvedFailureCode =
                string.IsNullOrWhiteSpace(failureCode)
                    ? "model-scan-failed"
                    : failureCode;
            FailureCodeTextBlock.Text = resolvedFailureCode;
            FailureMessageTextBlock.Text =
                string.IsNullOrWhiteSpace(failureMessage)
                    ? "The selected model could not be scanned."
                    : failureMessage;

            bool isUnsupportedFolder = string.Equals(
                resolvedFailureCode,
                "selection-unsupported-folder",
                StringComparison.Ordinal);
            bool isIncompleteOpenVino = string.Equals(
                resolvedFailureCode,
                "selection-incomplete-openvino",
                StringComparison.Ordinal);
            bool isFolderFailure = isUnsupportedFolder || isIncompleteOpenVino;

            FailureGgufIcon.Visibility =
                isFolderFailure ? Visibility.Collapsed : Visibility.Visible;
            FailureFolderIcon.Visibility =
                isFolderFailure ? Visibility.Visible : Visibility.Collapsed;
            FailureHeaderGrid.Margin =
                isFolderFailure ? new Thickness(0, 0, 0, 8) : new Thickness(0);
            FailureSourceSubtitleTextBlock.Text =
                isFolderFailure ? "Selected local model folder" : "Selected local model";
            FailureFormatBadgeBorder.Visibility =
                isUnsupportedFolder ? Visibility.Collapsed : Visibility.Visible;
            FailureFormatBadgeTextBlock.Text =
                isIncompleteOpenVino ? "OpenVINO" : "GGUF";
            FailureRecoveryPrimaryTextBlock.Text =
                isUnsupportedFolder
                    ? "Choose a GGUF file or a complete OpenVINO model folder."
                    : isIncompleteOpenVino
                        ? "Make sure the folder contains a complete OpenVINO model package."
                        : "Make sure the file is a valid GGUF model with the required header and metadata.";
            AnnounceState(
                ImportModelCardState.ScanFailed,
                $"Quick scan failed. {FailureMessageTextBlock.Text}");
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
            AnnounceState(ImportModelCardState.ScanSucceeded, "Quick scan complete. Ready for model inspection.");
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

        internal bool FocusChooseModel() =>
            ChooseModelFileButton.Focus(FocusState.Programmatic);

        private void AnnounceState(ImportModelCardState state, string announcement)
        {
            if (_isInitializing || _lastAnnouncedState == state)
            {
                return;
            }

            _lastAnnouncedState = state;
            AutomationProperties.SetName(this, announcement);
            AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
            AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(this) ??
                FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }

        private void ClearDragValidationPresentation()
        {
            ValidDragHoverOverlay.Visibility = Visibility.Collapsed;
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
