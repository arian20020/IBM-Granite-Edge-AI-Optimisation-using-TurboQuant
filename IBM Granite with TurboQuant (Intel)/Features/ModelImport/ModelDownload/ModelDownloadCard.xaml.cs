using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml;
using System.Globalization;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload
{
    /// <summary>
    /// Displays the currently selected recommended-model profile.
    /// </summary>
    public sealed partial class ModelDownloadCard : UserControl
    {
        private ModelDownloadCoordinator? _coordinator;

        public ModelDownloadCard()
        {
            // Loads and connects the controls declared in ModelCard.xaml.
            InitializeComponent();

            // Ensures the initial label matches the slider's starting value.
            UpdateModelScaleLabel(ModelScaleSlider.Value);
            Unloaded += ModelDownloadCard_Unloaded;
        }

        /*
            Runs every time the slider value changes.

            This is currently view-only behaviour because it changes visible text.
            The final model-selection logic will later move into
            ModelImportViewModel.
        */
        private void ModelScaleSlider_ValueChanged(
            object sender,
            RangeBaseValueChangedEventArgs e)
        {
            UpdateModelScaleLabel(e.NewValue);
        }

        // Updates the visible preference label above the slider.
        private void UpdateModelScaleLabel(double sliderValue)
        {
            /*
                The event can potentially run while XAML is still being loaded.

                This check prevents us from accessing the TextBlock before
                WinUI has created it.
            */
            if (ModelScaleValueText is null)
            {
                return;
            }

            ModelScaleValueText.Text =
                GetModelScaleLabel(sliderValue);

            ModelDownloadCatalogEntry entry = PinnedGraniteModelCatalog.ForSliderValue(sliderValue);
            if (QuantizationValueText is not null)
            {
                QuantizationValueText.Text = entry.Quantisation;
                ModelPackageSummaryText.Text = $"3B parameters | {entry.DownloadSizeText} download";
            }
        }

        /*
            Converts the continuous 0–100 slider value into five
            user-friendly preference descriptions.

            The slider itself remains continuous—it does not snap
            to these five categories.
        */
        private static string GetModelScaleLabel(double sliderValue)
        {
            return sliderValue switch
            {
                < 20 => "Maximum efficiency",
                < 40 => "Efficient",
                < 60 => "Balanced",
                < 80 => "High capability",
                _ => "Maximum capability"
            };
        }

        internal void Attach(ModelDownloadCoordinator coordinator)
        {
            ArgumentNullException.ThrowIfNull(coordinator);
            if (ReferenceEquals(_coordinator, coordinator))
            {
                return;
            }

            if (_coordinator is not null)
            {
                _coordinator.StateChanged -= Coordinator_StateChanged;
            }

            _coordinator = coordinator;
            _coordinator.StateChanged += Coordinator_StateChanged;
            Render(coordinator.State);
        }

        private void Coordinator_StateChanged(object? sender, ModelDownloadCoordinatorState state)
        {
            if (!DispatcherQueue.TryEnqueue(() => Render(state)))
            {
                return;
            }
        }

        private async void DownloadModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_coordinator is null)
            {
                return;
            }

            try
            {
                switch (_coordinator.State.Stage)
                {
                    case ModelDownloadStage.Preparing:
                    case ModelDownloadStage.Downloading:
                    case ModelDownloadStage.Verifying:
                        await _coordinator.CancelAsync(discardPartial: true, CancellationToken.None);
                        break;
                    case ModelDownloadStage.Interrupted:
                        await _coordinator.ResumeAsync(
                            allowMetered: _coordinator.State.ErrorCode == "download-network-confirmation-required",
                            CancellationToken.None);
                        break;
                    default:
                        await _coordinator.StartAsync(ModelScaleSlider.Value, allowMetered: false, CancellationToken.None);
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                // A second activation while the current transaction is settling is ignored.
            }
        }

        private async void DiscardDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_coordinator is not null)
            {
                await _coordinator.CancelAsync(discardPartial: true, CancellationToken.None);
            }
        }

        private void Render(ModelDownloadCoordinatorState state)
        {
            bool active = state.Stage is ModelDownloadStage.Preparing or
                ModelDownloadStage.Downloading or ModelDownloadStage.Verifying;
            bool showStatus = state.Stage != ModelDownloadStage.Idle;

            DownloadStatusRegion.Visibility = showStatus ? Visibility.Visible : Visibility.Collapsed;
            ModelScaleSlider.IsEnabled = !active;
            DownloadProgressBar.IsIndeterminate = state.Stage is ModelDownloadStage.Preparing or ModelDownloadStage.Verifying;
            DownloadProgressBar.Value = state.TotalBytes <= 0 ? 0 : 100d * state.DownloadedBytes / state.TotalBytes;
            DownloadProgressText.Text = state.TotalBytes <= 0
                ? string.Empty
                : $"{FormatBytes(state.DownloadedBytes)} of {FormatBytes(state.TotalBytes)}";
            DiscardDownloadButton.Visibility = state.Stage == ModelDownloadStage.Interrupted
                ? Visibility.Visible : Visibility.Collapsed;

            (DownloadStatusText.Text, DownloadModelButton.Content) = state.Stage switch
            {
                ModelDownloadStage.Preparing => ("Preparing secure download...", "Cancel download"),
                ModelDownloadStage.Downloading => ("Downloading and saving progress...", "Cancel download"),
                ModelDownloadStage.Verifying => ("Verifying the downloaded model...", "Cancel download"),
                ModelDownloadStage.Completed => ("Download verified. Starting model inspection...", "Downloaded"),
                ModelDownloadStage.Interrupted when state.ErrorCode == "download-offline" =>
                    ("Internet connection required. Reconnect to download this model.", "Try again"),
                ModelDownloadStage.Interrupted when state.ErrorCode == "download-network-confirmation-required" =>
                    ("This connection may be metered. Continue only if you accept the data use.", "Download using this connection"),
                ModelDownloadStage.Interrupted when state.ErrorCode == "download-cancelled-discarded" =>
                    ("Download cancelled. The partial file was removed.", "Download selected model"),
                ModelDownloadStage.Interrupted => ("Download paused. Your progress is saved.", "Resume download"),
                ModelDownloadStage.Failed => ("The download could not be verified. No model was installed.", "Try again"),
                _ => (string.Empty, "Download selected model")
            };
        }

        private static string FormatBytes(long bytes) =>
            bytes >= 1_000_000_000
                ? $"{bytes / 1_000_000_000d:0.00} GB"
                : $"{bytes / 1_000_000d:0.0} MB";

        private void ModelDownloadCard_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_coordinator is not null)
            {
                _coordinator.StateChanged -= Coordinator_StateChanged;
            }
        }
    }
}
