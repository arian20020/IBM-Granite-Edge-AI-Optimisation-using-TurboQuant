using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using System.Globalization;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload
{
    /// <summary>
    /// Displays the currently selected recommended-model profile.
    /// </summary>
    public sealed partial class ModelDownloadCard : UserControl
    {
        private ModelDownloadCoordinator? _coordinator;
        private readonly object _retirementGate = new();
        private Task? _retirementTask;
        private bool _retired;

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
            if (_retired)
            {
                throw new InvalidOperationException(
                    "A retired model download card cannot be rebound.");
            }
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
            ModelDownloadCoordinator? source = sender as ModelDownloadCoordinator;
            if (_retired || !DispatcherQueue.TryEnqueue(() =>
                {
                    if (!_retired && source is not null && ReferenceEquals(_coordinator, source)
                        && source.State == state)
                    {
                        Render(state);
                    }
                }))
            {
                return;
            }
        }

        private async void DownloadModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_retired || _coordinator is null)
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
                        await _coordinator.CancelAsync(discardPartial: false, CancellationToken.None);
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
            catch (Exception exception) when (exception is InvalidOperationException
                or IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                // A second activation while the current transaction is settling is ignored.
            }
        }

        private async void DiscardDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_retired && _coordinator is not null)
            {
                try
                {
                    await _coordinator.CancelAsync(discardPartial: true, CancellationToken.None);
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException)
                {
                    // The coordinator remains authoritative for the visible retry state.
                }
            }
        }

        private void Render(ModelDownloadCoordinatorState state)
        {
            bool active = state.Stage is ModelDownloadStage.Preparing or
                ModelDownloadStage.Downloading or ModelDownloadStage.Verifying;
            bool indeterminate = state.Stage is ModelDownloadStage.Preparing or
                ModelDownloadStage.Verifying;
            bool cancelling = active && state.ErrorCode == "download-cancelling";
            bool showStatus = state.Stage != ModelDownloadStage.Idle;

            DownloadStatusRegion.Visibility = showStatus ? Visibility.Visible : Visibility.Collapsed;
            ModelScaleSlider.IsEnabled = !active;
            DownloadActivityRing.IsActive = indeterminate;
            DownloadActivityRing.Visibility = indeterminate ? Visibility.Visible : Visibility.Collapsed;
            DownloadProgressBar.IsIndeterminate = indeterminate;
            DownloadProgressBar.Value = state.TotalBytes <= 0 ? 0 : 100d * state.DownloadedBytes / state.TotalBytes;
            DownloadProgressText.Text = state.TotalBytes <= 0
                ? string.Empty
                : $"{FormatBytes(state.DownloadedBytes)} of {FormatBytes(state.TotalBytes)} "
                    + $"({100d * state.DownloadedBytes / state.TotalBytes:0}%)";
            DiscardDownloadButton.Visibility = state.Stage == ModelDownloadStage.Interrupted
                ? Visibility.Visible : Visibility.Collapsed;

            (DownloadStatusText.Text, DownloadModelButton.Content) = state.Stage switch
            {
                ModelDownloadStage.Downloading when state.ErrorCode == "download-cancelling" =>
                    ("Cancelling the download and preserving resumable progress...", "Cancelling..."),
                ModelDownloadStage.Preparing when state.ErrorCode == "download-cancelling" =>
                    ("Cancelling the download and preserving resumable progress...", "Cancelling..."),
                ModelDownloadStage.Verifying when state.ErrorCode == "download-cancelling" =>
                    ("Cancelling the download and preserving resumable progress...", "Cancelling..."),
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
                ModelDownloadStage.Failed when state.ErrorCode == "download-storage-insufficient" =>
                    ("There is not enough free storage for this download. Free space, then try again.", "Try again"),
                ModelDownloadStage.Failed when state.ErrorCode is "download-http-rejected" or "download-timeout" =>
                    ("The model service could not be reached. Check the connection, then try again.", "Try again"),
                ModelDownloadStage.Failed when state.ErrorCode is "download-range-invalid" or "download-size-invalid" or "download-identity-changed" =>
                    ("The server copy changed during download. Discard saved progress, then try again.", "Try again"),
                ModelDownloadStage.Failed => ("The download failed integrity verification. No model was installed. Try again.", "Try again"),
                _ => (string.Empty, "Download selected model")
            };
            DownloadModelButton.IsEnabled = !cancelling &&
                state.Stage != ModelDownloadStage.Completed;
            AutomationProperties.SetName(
                DownloadModelButton,
                state.Stage switch
                {
                    ModelDownloadStage.Preparing or
                    ModelDownloadStage.Downloading or
                    ModelDownloadStage.Verifying when cancelling =>
                        "Model download cancellation in progress",
                    ModelDownloadStage.Preparing or
                    ModelDownloadStage.Downloading or
                    ModelDownloadStage.Verifying =>
                        "Cancel the model download",
                    ModelDownloadStage.Interrupted =>
                        "Resume the model download",
                    ModelDownloadStage.Failed =>
                        "Retry the model download",
                    ModelDownloadStage.Completed =>
                        "Model download completed",
                    _ => "Download the selected model"
                });
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

        internal Task RetireAsync()
        {
            lock (_retirementGate)
            {
                if (_retirementTask is not null)
                {
                    return _retirementTask;
                }

                _retired = true;
                ModelDownloadCoordinator? coordinator = _coordinator;
                _coordinator = null;
                if (coordinator is not null)
                {
                    coordinator.StateChanged -= Coordinator_StateChanged;
                }
                DownloadModelButton.IsEnabled = false;
                DiscardDownloadButton.IsEnabled = false;
                DownloadActivityRing.IsActive = false;
                _retirementTask = Task.CompletedTask;
                return _retirementTask;
            }
        }
    }
}
