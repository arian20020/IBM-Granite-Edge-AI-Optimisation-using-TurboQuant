using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

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
                    string? selectedFileName = null)
        {
            // Record the current state so it can be inspected and tested.
            CurrentState = state;

            // Show the default interface only while waiting for selection.
            AwaitingSelectionView.Visibility =
                state == ImportModelCardState.AwaitingSelection
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            // Show the loading interface only while scanning.
            ScanningView.Visibility =
                state == ImportModelCardState.Scanning
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            // Display the selected file name in the scanning interface.
            ScanningFileNameTextBlock.Text =
                selectedFileName ?? string.Empty;
        }

        /// Handles the internal Browse button click.
        private void BrowseFilesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BrowseFilesRequested?.Invoke(this, e);
        }

        private void CancelScanButton_Click(object sender, RoutedEventArgs e)
        {
            // Notify the containing page that cancellation was requested.
            CancelScanRequested?.Invoke(this, e);
        }
    }
}
