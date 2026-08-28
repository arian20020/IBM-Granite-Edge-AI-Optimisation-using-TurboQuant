using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload
{
    /// <summary>
    /// Displays the currently selected recommended-model profile.
    /// </summary>
    public sealed partial class ModelDownloadCard : UserControl
    {
        private RecommendedModelOffer? _offer;
        private ModelDownloadController? _controller;
        private readonly object _retirementGate = new();
        private Task _detachedOperations = Task.CompletedTask;
        private Task? _retirementTask;
        private bool _retired;

        public ModelDownloadCard()
        {
            // Loads and connects the controls declared in ModelCard.xaml.
            InitializeComponent();

            // Ensures the initial label matches the slider's starting value.
            UpdateModelScaleLabel(ModelScaleSlider.Value);
        }

        internal event EventHandler<CompletedModelDownload>? VerifiedDownloadCompleted;

        internal ModelDownloadViewState DownloadState =>
            _controller?.State ?? ModelDownloadViewState.Unavailable();

        internal void BindDownload(
            RecommendedModelOffer offer,
            IRecommendedModelDownloadService service)
        {
            ArgumentNullException.ThrowIfNull(offer);
            ArgumentNullException.ThrowIfNull(service);
            if (_retired)
            {
                throw new InvalidOperationException(
                    "A retired model download card cannot be rebound.");
            }
            if (!_detachedOperations.IsCompletedSuccessfully)
            {
                throw new InvalidOperationException(
                    "A retiring model download cannot be replaced.");
            }
            if (_controller?.State.Kind is ModelDownloadStateKind.Running
                or ModelDownloadStateKind.Cancelling)
            {
                throw new InvalidOperationException(
                    "An active model download cannot be replaced.");
            }
            if (_controller is not null)
            {
                _controller.StateChanged -= Controller_StateChanged;
                _detachedOperations = Task.WhenAll(
                    _detachedOperations,
                    _controller.RetireAsync());
            }

            _offer = offer;
            _controller = new ModelDownloadController(service);
            _controller.StateChanged += Controller_StateChanged;
            ModelNameText.Text = offer.DisplayName;
            ModelPackageSummaryText.Text = offer.PackageSummary;
            ModelDescriptionText.Text = offer.Description;
            ModelFormatValueText.Text = offer.Format;
            ModelOptimizationValueText.Text = offer.Optimization;
            EstimatedMemoryValueText.Text = offer.EstimatedMemory;
            ContextLengthValueText.Text = offer.ContextLength;
            _controller.MarkReady();
        }

        internal Task<bool> TryStartDownloadAsync()
        {
            if (_offer is null
                || _controller is null
                || _retired
                || !DownloadModelButton.IsEnabled)
            {
                return Task.FromResult(false);
            }
            int preference = checked((int)Math.Round(
                ModelScaleSlider.Value,
                MidpointRounding.AwayFromZero));
            return _controller.TryStartAsync(_offer, preference);
        }

        internal bool TryCancelDownload() =>
            _controller?.TryCancel() ?? false;

        internal Task<bool> TryRetryDownloadAsync() =>
            !_retired && _controller is not null
                ? _controller.TryRetryAsync()
                : Task.FromResult(false);

        internal Task RetireAsync()
        {
            lock (_retirementGate)
            {
                if (_retirementTask is not null)
                {
                    return _retirementTask;
                }

                _retired = true;
                _offer = null;
                ModelDownloadController? controller = _controller;
                _controller = null;
                if (controller is not null)
                {
                    controller.StateChanged -= Controller_StateChanged;
                }
                _retirementTask = Task.WhenAll(
                    _detachedOperations,
                    controller?.RetireAsync() ?? Task.CompletedTask);
                ApplyDownloadState(ModelDownloadViewState.Unavailable());
                return _retirementTask;
            }
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

        private async void DownloadModelButton_Click(
            object sender,
            RoutedEventArgs eventArguments) =>
            await TryStartDownloadAsync();

        private void CancelDownloadButton_Click(
            object sender,
            RoutedEventArgs eventArguments) =>
            TryCancelDownload();

        private async void RetryDownloadButton_Click(
            object sender,
            RoutedEventArgs eventArguments) =>
            await TryRetryDownloadAsync();

        private void Controller_StateChanged(
            object? sender,
            ModelDownloadViewState state)
        {
            if (sender is not ModelDownloadController controller
                || !ReferenceEquals(controller, _controller))
            {
                return;
            }
            DispatcherQueue dispatcher = DispatcherQueue;
            if (!dispatcher.HasThreadAccess)
            {
                dispatcher.TryEnqueue(() =>
                {
                    if (ReferenceEquals(controller, _controller)
                        && Equals(controller.State, state))
                    {
                        ApplyDownloadState(state);
                    }
                });
                return;
            }
            if (Equals(controller.State, state))
            {
                ApplyDownloadState(state);
            }
        }

        private void ApplyDownloadState(ModelDownloadViewState state)
        {
            if (_retired && state.Kind != ModelDownloadStateKind.Unavailable)
            {
                return;
            }
            bool running = state.Kind is ModelDownloadStateKind.Running
                or ModelDownloadStateKind.Cancelling;
            DownloadModelButton.IsEnabled = state.Kind == ModelDownloadStateKind.Ready;
            ModelScaleSlider.IsEnabled = !running && state.Kind != ModelDownloadStateKind.Succeeded;
            DownloadStatusText.Text = state.StatusText;
            DownloadProgressBar.Visibility = running
                ? Visibility.Visible
                : Visibility.Collapsed;
            DownloadProgressBar.IsIndeterminate = state.Fraction is null;
            if (state.Fraction is double fraction)
            {
                DownloadProgressBar.Value = fraction * 100;
            }
            CancelDownloadButton.Visibility = state.Kind == ModelDownloadStateKind.Running
                ? Visibility.Visible
                : Visibility.Collapsed;
            RetryDownloadButton.Visibility = state.Kind is ModelDownloadStateKind.Cancelled
                or ModelDownloadStateKind.Failed
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (state.Kind is ModelDownloadStateKind.Cancelled
                or ModelDownloadStateKind.Failed)
            {
                RetryDownloadButton.Focus(FocusState.Programmatic);
            }
            else if (state.Kind == ModelDownloadStateKind.Succeeded
                && state.CompletedDownload is not null)
            {
                VerifiedDownloadCompleted?.Invoke(
                    this,
                    state.CompletedDownload);
            }
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
    }
}
