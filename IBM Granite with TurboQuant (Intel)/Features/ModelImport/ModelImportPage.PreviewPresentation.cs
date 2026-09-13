using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.Presentation.Progress;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;

namespace GraniteEdgeAI.Features.ModelImport;

public sealed partial class ModelImportPage
{
    private ModelDownloadCoordinator? _downloadPresentationCoordinator;
    private string? _lastPreferenceAutomationName;
    private Brush? _preferenceAccentBrush;
    private Brush? _preferenceMutedBrush;
    private readonly BoundedProgressEstimator _downloadEstimate = new();
    private DispatcherProgressPresenter? _downloadEstimatePresenter;
    private ModelDownloadCoordinatorState? _estimatedDownloadState;
    private object _downloadEstimateOwner = new();
    private long _downloadEstimateRevision;
    private bool _downloadEstimateLifecycleAttached;

    internal ImportModelCardState CurrentImportState { get; private set; } =
        ImportModelCardState.AwaitingSelection;

    private void ShowAwaitingSelection()
    {
        SetLocalImportState(ImportModelCardState.AwaitingSelection);
        LocalMetadataModelNameTextBlock.Text = string.Empty;
        LocalMetadataSourceTextBlock.Text = string.Empty;
        LocalMetadataIconLabel.Text = string.Empty;
        LocalMetadataFormatTextBlock.Text = string.Empty;
        LocalMetadataQuantizationTextBlock.Text = string.Empty;
        LocalMetadataParametersTextBlock.Text = string.Empty;
        LocalMetadataArchitectureTextBlock.Text = string.Empty;
        LocalMetadataFileSizeTextBlock.Text = string.Empty;
        LocalMetadataDeclaredContextTextBlock.Text = string.Empty;
        LocalMetadataRuntimeRouteTextBlock.Text = string.Empty;
        LocalMetadataNextStepTextBlock.Text = string.Empty;
        LocalMetadataStatusTextBlock.Text = string.Empty;
        AutomationProperties.SetName(LocalMetadataFormatBadge, string.Empty);
        AutomationProperties.SetName(LocalMetadataQuantizationBadge, string.Empty);
        AutomationProperties.SetName(LocalMetadataStatusStrip, string.Empty);
        FailureFileNameTextBlock.Text = string.Empty;
        FailureIconLabel.Text = string.Empty;
        FailureFormatTextBlock.Text = string.Empty;
        FailureCodeTextBlock.Text = string.Empty;
        FailureMessageTextBlock.Text = string.Empty;
        AutomationProperties.SetName(FailureFormatBadge, string.Empty);
        AutomationProperties.SetName(FailureCodeBadge, string.Empty);
        LocalImportStateStatus.Text = "Awaiting selection";
        LocalScanProgressRing.IsActive = false;
        LocalScanProgressRing.Visibility = Visibility.Collapsed;
        BtnContinueToInspection.IsEnabled = false;
    }

    private void ShowScanning(string selectedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedName);
        SetLocalImportState(ImportModelCardState.Scanning);
        LocalMetadataModelNameTextBlock.Text = selectedName;
        LocalMetadataSourceTextBlock.Text = "Selected local model";
        LocalMetadataIconLabel.Text = "MODEL";
        LocalMetadataFormatTextBlock.Text = "Pending";
        LocalMetadataQuantizationBadge.Visibility = Visibility.Collapsed;
        LocalMetadataQuantizationTextBlock.Text = string.Empty;
        AutomationProperties.SetName(LocalMetadataFormatBadge, "Model format pending classification");
        AutomationProperties.SetName(LocalMetadataQuantizationBadge, string.Empty);
        LocalMetadataRuntimeRouteTextBlock.Text = "Pending classification";
        LocalMetadataNextStepTextBlock.Text = "Classify source";
        LocalMetadataParametersTextBlock.Visibility = Visibility.Collapsed;
        LocalMetadataArchitectureTextBlock.Visibility = Visibility.Collapsed;
        LocalMetadataFileSizeTextBlock.Visibility = Visibility.Collapsed;
        LocalMetadataDeclaredContextTextBlock.Visibility = Visibility.Collapsed;
        LocalMetadataParametersSkeleton.Visibility = Visibility.Visible;
        LocalMetadataArchitectureSkeleton.Visibility = Visibility.Visible;
        LocalMetadataFileSizeSkeleton.Visibility = Visibility.Visible;
        LocalMetadataDeclaredContextSkeleton.Visibility = Visibility.Visible;
        LocalMetadataProgressRing.IsActive = true;
        LocalMetadataProgressRing.Visibility = Visibility.Visible;
        LocalMetadataStatusIcon.Visibility = Visibility.Collapsed;
        LocalMetadataStatusTextBlock.Text = "Classifying selected model";
        AutomationProperties.SetName(LocalMetadataStatusStrip, "Classifying selected model");
        LocalImportStateStatus.Text = "Classifying selected model";
        LocalScanProgressRing.IsActive = true;
        LocalScanProgressRing.Visibility = Visibility.Visible;
    }

    private void ShowFolderAccepted(string folderName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);
        SetLocalImportState(ImportModelCardState.SelectionAccepted);
        bool openVino = CurrentRoute == Selection.ModelSelectionRoute.OpenVinoDirectory;
        string format = openVino ? "OpenVINO" : "Source model";
        LocalMetadataModelNameTextBlock.Text = folderName;
        LocalMetadataSourceTextBlock.Text = "Selected local model folder";
        LocalMetadataIconLabel.Text = openVino ? "OV" : "SRC";
        LocalMetadataFormatTextBlock.Text = format;
        LocalMetadataQuantizationBadge.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(LocalMetadataFormatBadge, $"{format} format");
        LocalMetadataParametersTextBlock.Text = "Pending inspection";
        LocalMetadataArchitectureTextBlock.Text = "Pending inspection";
        LocalMetadataFileSizeTextBlock.Text = "Folder";
        LocalMetadataDeclaredContextTextBlock.Text = "Pending inspection";
        LocalMetadataRuntimeRouteTextBlock.Text = openVino ? "OpenVINO" : "Conversion required";
        LocalMetadataNextStepTextBlock.Text = openVino ? "Model inspection" : "Conversion";
        LocalMetadataStatusTextBlock.Text = openVino
            ? "OpenVINO folder selected · Ready for model inspection"
            : "Source model folder selected · Ready to continue";
        AutomationProperties.SetName(
            LocalMetadataStatusStrip,
            LocalMetadataStatusTextBlock.Text);
        LocalImportStateStatus.Text = LocalMetadataStatusTextBlock.Text;
        BtnContinueToInspection.IsEnabled = true;
    }

    private void ShowSuccess(ImportedModelCardData model)
    {
        ArgumentNullException.ThrowIfNull(model);
        SetLocalImportState(ImportModelCardState.ScanSucceeded);
        LocalMetadataModelNameTextBlock.Text = model.ModelName;
        LocalMetadataSourceTextBlock.Text = $"Selected local model file · {model.FileName}";
        LocalMetadataIconLabel.Text = "GGUF";
        LocalMetadataFormatTextBlock.Text = "GGUF";
        LocalMetadataQuantizationBadge.Visibility = Visibility.Visible;
        LocalMetadataQuantizationTextBlock.Text = model.Quantization;
        AutomationProperties.SetName(LocalMetadataFormatBadge, "GGUF format");
        AutomationProperties.SetName(
            LocalMetadataQuantizationBadge,
            $"{model.Quantization} quantisation");
        LocalMetadataParametersTextBlock.Text = model.Parameters;
        LocalMetadataArchitectureTextBlock.Text = model.Architecture;
        LocalMetadataFileSizeTextBlock.Text = model.FileSize;
        LocalMetadataDeclaredContextTextBlock.Text = model.DeclaredContext;
        LocalMetadataRuntimeRouteTextBlock.Text = "llama.cpp";
        LocalMetadataNextStepTextBlock.Text = "Model inspection";
        LocalMetadataStatusTextBlock.Text = "Quick scan complete · Ready for model inspection";
        AutomationProperties.SetName(
            LocalMetadataStatusStrip,
            LocalMetadataStatusTextBlock.Text);
        LocalImportStateStatus.Text = LocalMetadataStatusTextBlock.Text;
        BtnContinueToInspection.IsEnabled = true;
    }

    private void ShowFailure(string selectedName, string? failureCode, string? message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedName);
        SetLocalImportState(ImportModelCardState.ScanFailed);
        string code = string.IsNullOrWhiteSpace(failureCode) ? "model-scan-failed" : failureCode;
        FailureFileNameTextBlock.Text = selectedName;
        FailureCodeTextBlock.Text = code;
        AutomationProperties.SetName(FailureCodeBadge, $"{code} failure code");
        FailureMessageTextBlock.Text = string.IsNullOrWhiteSpace(message)
            ? "The selected model could not be scanned."
            : message;
        bool folder = code is "selection-unsupported-folder" or "selection-incomplete-openvino";
        FailureIconLabel.Text = folder ? "OV" : "GGUF";
        FailureFormatTextBlock.Text = code == "selection-incomplete-openvino" ? "OpenVINO" : "GGUF";
        AutomationProperties.SetName(
            FailureFormatBadge,
            $"{FailureFormatTextBlock.Text} format");
        FailureFormatBadge.Visibility = code == "selection-unsupported-folder"
            ? Visibility.Collapsed
            : Visibility.Visible;
        FailureRecoveryStep1Text.Text = code switch
        {
            "selection-unsupported-folder" => "1. Choose a GGUF file or a complete OpenVINO model folder.",
            "selection-incomplete-openvino" => "1. Make sure the folder contains a complete OpenVINO model package.",
            _ => "1. Make sure the file is a valid GGUF model with the required header and metadata."
        };
        LocalImportStateStatus.Text = $"Quick scan failed. {FailureMessageTextBlock.Text}";
        LocalScanProgressRing.IsActive = false;
        LocalScanProgressRing.Visibility = Visibility.Collapsed;
        BtnContinueToInspection.IsEnabled = false;
    }

    private void SetLocalImportState(ImportModelCardState state)
    {
        CurrentImportState = state;
        bool awaiting = state is ImportModelCardState.AwaitingSelection
            or ImportModelCardState.DragOverValid or ImportModelCardState.DragOverInvalid;
        AwaitingSelectionView.Visibility = awaiting ? Visibility.Visible : Visibility.Collapsed;
        LocalReferenceStateHost.Visibility = awaiting ? Visibility.Collapsed : Visibility.Visible;
        LocalMetadataCardView.Visibility = state is ImportModelCardState.Scanning
            or ImportModelCardState.ScanSucceeded or ImportModelCardState.SelectionAccepted
            ? Visibility.Visible : Visibility.Collapsed;
        FailureView.Visibility = state == ImportModelCardState.ScanFailed
            ? Visibility.Visible : Visibility.Collapsed;
        ValidDragHoverOverlay.Visibility = state == ImportModelCardState.DragOverValid
            ? Visibility.Visible : Visibility.Collapsed;
        if (state != ImportModelCardState.Scanning)
        {
            LocalScanProgressRing.IsActive = false;
            LocalScanProgressRing.Visibility = Visibility.Collapsed;
            LocalMetadataProgressRing.IsActive = false;
            LocalMetadataProgressRing.Visibility = Visibility.Collapsed;
            LocalMetadataStatusIcon.Visibility = Visibility.Visible;
            LocalMetadataParametersSkeleton.Visibility = Visibility.Collapsed;
            LocalMetadataArchitectureSkeleton.Visibility = Visibility.Collapsed;
            LocalMetadataFileSizeSkeleton.Visibility = Visibility.Collapsed;
            LocalMetadataDeclaredContextSkeleton.Visibility = Visibility.Collapsed;
            LocalMetadataParametersTextBlock.Visibility = Visibility.Visible;
            LocalMetadataArchitectureTextBlock.Visibility = Visibility.Visible;
            LocalMetadataFileSizeTextBlock.Visibility = Visibility.Visible;
            LocalMetadataDeclaredContextTextBlock.Visibility = Visibility.Visible;
        }
    }

    private void ShowDragValidation(bool valid) =>
        SetLocalImportState(valid ? ImportModelCardState.DragOverValid : ImportModelCardState.DragOverInvalid);

    private void ClearDragValidation()
    {
        if (CurrentImportState is ImportModelCardState.DragOverValid or ImportModelCardState.DragOverInvalid)
        {
            ShowAwaitingSelection();
        }
    }

    private bool FocusChooseModel() => BtnChooseLocalModel.Focus(FocusState.Programmatic);

    private void RemoveSelectedModelButton_Click(object sender, RoutedEventArgs args) =>
        ImportModelCard_CancelScanRequested(sender, args);

    private void AttachDownloadPresentation(ModelDownloadCoordinator coordinator)
    {
        _downloadPresentationCoordinator = coordinator;
        coordinator.StateChanged += DownloadCoordinator_StateChanged;
        UpdateRecommendedPreference(RecommendedModelPreference?.Value ?? 50d);
        RenderDownload(coordinator.State);
    }

    private void DownloadCoordinator_StateChanged(object? sender, ModelDownloadCoordinatorState state)
    {
        ModelDownloadCoordinator? source = sender as ModelDownloadCoordinator;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (Volatile.Read(ref _isRetired) == 0 && source is not null &&
                ReferenceEquals(source, _downloadPresentationCoordinator) && source.State == state)
            {
                RenderDownload(state);
            }
        });
    }

    private Task RetireDownloadPresentationAsync()
    {
        _downloadEstimatePresenter?.Stop();
        _downloadEstimate.Reset();
        _estimatedDownloadState = null;
        ModelDownloadCoordinator? coordinator = _downloadPresentationCoordinator;
        _downloadPresentationCoordinator = null;
        if (coordinator is not null)
        {
            coordinator.StateChanged -= DownloadCoordinator_StateChanged;
        }
        BtnDownloadSelectedModel.IsEnabled = false;
        BtnDiscardDownload.IsEnabled = false;
        DownloadProgressRing.IsActive = false;
        return Task.CompletedTask;
    }

    private void RecommendedModelPreference_ValueChanged(object sender, RangeBaseValueChangedEventArgs args) =>
        UpdateRecommendedPreference(args.NewValue);

    private void UpdateRecommendedPreference(double value)
    {
        if (RecommendedPreferenceLabel is null)
        {
            return;
        }
        string label = PreferenceLabel(value);
        ModelDownloadCatalogEntry entry = PinnedGraniteModelCatalog.ForSliderValue(value);
        _preferenceAccentBrush ??= RecommendedPreferenceLabel.Foreground;
        _preferenceMutedBrush ??= RecommendedPreferenceMaximumEfficiency.Foreground;
        RecommendedPreferenceLabel.Text = label;
        RecommendedModelQuantization.Text = entry.Quantisation;
        RecommendedModelPackageSummary.Text = $"3B parameters · {entry.DownloadSizeText}";
        var labels = new[] { RecommendedPreferenceMaximumEfficiency, RecommendedPreferenceEfficient,
            RecommendedPreferenceBalanced, RecommendedPreferenceHighCapability, RecommendedPreferenceMaximumCapability };
        int selected = value switch { < 20 => 0, < 40 => 1, < 60 => 2, < 80 => 3, _ => 4 };
        for (int index = 0; index < labels.Length; index++)
        {
            labels[index].Foreground = index == selected
                ? _preferenceAccentBrush
                : _preferenceMutedBrush;
            labels[index].FontWeight = index == selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
        string automationName = $"Model preference. {label}. {entry.Quantisation}. 3B parameters. {entry.DownloadSizeText} download.";
        if (!string.Equals(_lastPreferenceAutomationName, automationName, StringComparison.Ordinal))
        {
            AutomationProperties.SetName(RecommendedModelPreference, automationName);
            _lastPreferenceAutomationName = automationName;
        }
    }

    private static string PreferenceLabel(double value) => value switch
    {
        < 20 => "Maximum efficiency", < 40 => "Efficient", < 60 => "Balanced",
        < 80 => "High capability", _ => "Maximum capability"
    };

    private async void DownloadModelButton_Click(object sender, RoutedEventArgs args)
    {
        ModelDownloadCoordinator? coordinator = _downloadPresentationCoordinator;
        if (coordinator is null || Volatile.Read(ref _isRetired) != 0) return;
        try
        {
            switch (coordinator.State.Stage)
            {
                case ModelDownloadStage.Preparing:
                case ModelDownloadStage.Downloading:
                case ModelDownloadStage.Verifying:
                    _downloadEstimatePresenter?.Stop();
                    _downloadEstimate.Freeze();
                    await coordinator.CancelAsync(false, CancellationToken.None); break;
                case ModelDownloadStage.Interrupted:
                    await coordinator.ResumeAsync(coordinator.State.ErrorCode == "download-network-confirmation-required", CancellationToken.None); break;
                default:
                    await coordinator.StartAsync(RecommendedModelPreference.Value, false, CancellationToken.None); break;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or OperationCanceledException) { }
    }

    private async void DiscardDownloadButton_Click(object sender, RoutedEventArgs args)
    {
        ModelDownloadCoordinator? coordinator = _downloadPresentationCoordinator;
        if (coordinator is null || Volatile.Read(ref _isRetired) != 0) return;
        try { await coordinator.CancelAsync(true, CancellationToken.None); }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or OperationCanceledException) { }
    }

    private void UpdateDownloadEstimate()
    {
        if (Volatile.Read(ref _isRetired) != 0 || _estimatedDownloadState is not { } state ||
            state.ErrorCode == "download-cancelling" || state.Stage is not (ModelDownloadStage.Preparing or ModelDownloadStage.Downloading or ModelDownloadStage.Verifying)) return;
        bool motion = new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        double? measured = state.Stage == ModelDownloadStage.Downloading && state.TotalBytes > 0
            ? Math.Clamp((double)state.DownloadedBytes / state.TotalBytes, 0, 1) : null;
        if (state.Stage == ModelDownloadStage.Downloading && measured is null)
        {
            RecommendedDownloadProgress.IsIndeterminate = true;
            const string unknownTotal = "Downloading · Total size not reported";
            if (RecommendedDownloadProgressText.Text != unknownTotal)
            {
                RecommendedDownloadProgressText.Text = unknownTotal;
                AutomationProperties.SetItemStatus(
                    RecommendedDownloadProgress,
                    unknownTotal);
            }
            return;
        }

        _downloadEstimate.Update(_downloadEstimateOwner, ++_downloadEstimateRevision, state.Stage, measured, false, motion);
        double local = _downloadEstimate.GetFraction();
        double overall = state.Stage switch
        {
            ModelDownloadStage.Preparing => 0,
            ModelDownloadStage.Downloading => Math.Min(95, local * 95),
            ModelDownloadStage.Verifying => Math.Min(99, 95 + local * 4),
            _ => 0
        };
        RecommendedDownloadProgress.IsIndeterminate = false;
        _downloadEstimatePresenter!.SetValue(RecommendedDownloadProgress, overall, motion);
        string phase = state.Stage == ModelDownloadStage.Verifying ? "Verifying" : state.Stage == ModelDownloadStage.Preparing ? "Preparing" : "Downloading";
        string detail = $"Estimated {Math.Floor((decimal)overall):0}% overall · {phase}: {(_downloadEstimate.IsEstimated() ? "Estimated " : string.Empty)}{Math.Floor((decimal)local * 100m):0}%";
        if (state.Stage == ModelDownloadStage.Downloading && state.TotalBytes > 0)
            detail += $" · {FormatDownloadBytes(state.DownloadedBytes)} of {FormatDownloadBytes(state.TotalBytes)}";
        if (RecommendedDownloadProgressText.Text != detail)
        {
            RecommendedDownloadProgressText.Text = detail;
            AutomationProperties.SetItemStatus(RecommendedDownloadProgress, detail);
        }
    }

    private void RenderDownload(ModelDownloadCoordinatorState state)
    {
        // successful discard resets only this card's projection, not coordinator history
        if (state.Stage == ModelDownloadStage.Interrupted &&
            state.ErrorCode == "download-cancelled-discarded")
        {
            state = state with
            {
                Stage = ModelDownloadStage.Idle,
                DownloadedBytes = 0,
                TotalBytes = 0,
                ErrorCode = null
            };
        }
        bool active = state.Stage is ModelDownloadStage.Preparing or ModelDownloadStage.Downloading or ModelDownloadStage.Verifying;
        bool indeterminate = state.Stage is ModelDownloadStage.Preparing or ModelDownloadStage.Verifying || state.Stage == ModelDownloadStage.Downloading && state.TotalBytes <= 0;
        bool cancelling = active && state.ErrorCode == "download-cancelling";
        DownloadStatusRegion.Visibility = state.Stage == ModelDownloadStage.Idle ? Visibility.Collapsed : Visibility.Visible;
        DownloadStateStatus.Visibility = state.Stage == ModelDownloadStage.Idle ? Visibility.Collapsed : Visibility.Visible;
        DownloadProgressRing.IsActive = indeterminate;
        DownloadProgressRing.Visibility = indeterminate ? Visibility.Visible : Visibility.Collapsed;
        RecommendedDownloadProgress.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        bool operationChanged = !Equals(
            _estimatedDownloadState?.OperationId,
            state.OperationId);
        if (operationChanged ||
            (active && _estimatedDownloadState?.Stage is ModelDownloadStage.Idle or ModelDownloadStage.Failed or ModelDownloadStage.Interrupted or ModelDownloadStage.Completed))
        {
            _downloadEstimateOwner = new();
            _downloadEstimate.Reset();
            if (operationChanged)
            {
                RecommendedDownloadProgress.IsIndeterminate = false;
                RecommendedDownloadProgress.Value = 0;
            }
        }
        _estimatedDownloadState = state;
        _downloadEstimatePresenter ??= new DispatcherProgressPresenter(this, UpdateDownloadEstimate);
        if (!_downloadEstimateLifecycleAttached)
        {
            _downloadEstimateLifecycleAttached = true;
            Unloaded += (_, _) => { _downloadEstimatePresenter.Stop(); _downloadEstimate.Reset(); };
            Loaded += (_, _) =>
            {
                if (_downloadPresentationCoordinator is { } source && Volatile.Read(ref _isRetired) == 0) RenderDownload(source.State);
            };
        }
        if (active && !cancelling)
        {
            UpdateDownloadEstimate();
            _downloadEstimatePresenter.Start();
        }
        else { _downloadEstimatePresenter.Stop(); _downloadEstimate.Freeze(); }
        RecommendedDownloadProgressText.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(RecommendedDownloadProgress, "Overall recommended model download progress");
        BtnDiscardDownload.Visibility = state.Stage == ModelDownloadStage.Interrupted ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(BtnDownloadSelectedModel, state.Stage == ModelDownloadStage.Interrupted ? 1 : 0);
        Grid.SetColumnSpan(BtnDownloadSelectedModel, state.Stage == ModelDownloadStage.Interrupted ? 1 : 2);
        (string status, string action) = state.Stage switch
        {
            ModelDownloadStage.Preparing when cancelling => ("Cancelling the download and preserving resumable progress...", "Cancelling..."),
            ModelDownloadStage.Downloading when cancelling => ("Cancelling the download and preserving resumable progress...", "Cancelling..."),
            ModelDownloadStage.Verifying when cancelling => ("Cancelling the download and preserving resumable progress...", "Cancelling..."),
            ModelDownloadStage.Preparing => ("Preparing secure download...", "Cancel download"),
            ModelDownloadStage.Downloading => ("Downloading and saving progress...", "Cancel download"),
            ModelDownloadStage.Verifying => ("Verifying the downloaded model...", "Cancel download"),
            ModelDownloadStage.Completed => ("Download verified. Starting model inspection...", "Downloaded"),
            ModelDownloadStage.Interrupted when state.ErrorCode == "download-network-confirmation-required" => ("This connection may be metered. Continue only if you accept the data use.", "Download using this connection"),
            ModelDownloadStage.Interrupted when state.ErrorCode == "download-offline" => ("Internet connection required. Reconnect to download this model.", "Try again"),
            ModelDownloadStage.Interrupted when state.ErrorCode == "download-cancelled-discarded" => ("Download cancelled. The partial file was removed.", "Download selected model"),
            ModelDownloadStage.Interrupted => ("Download paused. Your progress is saved.", "Resume download"),
            ModelDownloadStage.Failed when state.ErrorCode == "download-storage-insufficient" => ("There is not enough free storage for this download. Free space, then try again.", "Try again"),
            ModelDownloadStage.Failed when state.ErrorCode is "download-http-rejected" or "download-timeout" => ("The model service could not be reached. Check the connection, then try again.", "Try again"),
            ModelDownloadStage.Failed when state.ErrorCode is "download-range-invalid" or "download-size-invalid" or "download-identity-changed" => ("The server copy changed during download. Discard saved progress, then try again.", "Try again"),
            ModelDownloadStage.Failed when state.ErrorCode == "download-cancellation-cleanup-pending" => ("Cancellation cleanup is still in progress. Retry is unavailable until it finishes.", "Cleanup pending"),
            ModelDownloadStage.Failed when state.ErrorCode == "download-cancellation-cleanup-failed" => ("The download stopped, but cleanup could not be confirmed. You can try the download again.", "Try again"),
            ModelDownloadStage.Failed => ("The download failed integrity verification. No model was installed. Try again.", "Try again"),
            _ => (string.Empty, "Download selected model")
        };
        DownloadStateStatus.Text = status;
        BtnDownloadSelectedModel.Content = action;
        BtnDownloadSelectedModel.IsEnabled = !cancelling && state.ErrorCode != "download-cancellation-cleanup-pending" && state.Stage != ModelDownloadStage.Completed;
        RecommendedModelPreference.IsEnabled = !active;
        AutomationProperties.SetName(
            BtnDownloadSelectedModel,
            state.Stage switch
            {
                ModelDownloadStage.Preparing or ModelDownloadStage.Downloading or ModelDownloadStage.Verifying when cancelling =>
                    "Model download cancellation in progress",
                ModelDownloadStage.Failed when state.ErrorCode == "download-cancellation-cleanup-pending" =>
                    "Model download cleanup in progress",
                ModelDownloadStage.Failed when state.ErrorCode == "download-cancellation-cleanup-failed" =>
                    "Retry the model download after a cleanup failure",
                ModelDownloadStage.Preparing or ModelDownloadStage.Downloading or ModelDownloadStage.Verifying =>
                    "Cancel the model download",
                ModelDownloadStage.Interrupted when state.ErrorCode == "download-cancelled-discarded" =>
                    "Download the selected model",
                ModelDownloadStage.Interrupted => "Resume the model download",
                ModelDownloadStage.Failed => "Retry the model download",
                ModelDownloadStage.Completed => "Model download completed",
                _ => "Download the selected model"
            });
        AutomationProperties.SetHelpText(BtnDownloadSelectedModel, active ? "Cancel the current model download." : "Download the selected verified model package.");
        AutomationProperties.SetHelpText(BtnDiscardDownload, "Discard the interrupted partial download.");
    }

    private static string FormatDownloadBytes(long bytes) => bytes >= 1_000_000_000
        ? $"{bytes / 1_000_000_000d:0.00} GB"
        : $"{bytes / 1_000_000d:0.0} MB";
}
