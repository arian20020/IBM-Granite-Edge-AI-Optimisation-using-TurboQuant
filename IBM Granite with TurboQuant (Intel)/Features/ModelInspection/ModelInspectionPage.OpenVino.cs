using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using ModelOutcome = GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionOutcome;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;
using System.Collections.Generic;
using ModelInspectionHandoffV2 = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionHandoffV2;

namespace GraniteEdgeAI.Features.ModelInspection;

public sealed partial class ModelInspectionPage
{
    private OpenVinoRouteService? _openVinoRouteService;
    private PromptRouteRegistry? _promptRouteRegistry;
    private CancellationTokenSource? _openVinoCancellation;
    private IPromptRouteSession? _promptSession;
    private PromptSessionPresenter? _promptPresenter;
    private OpenVinoConversionOffer? _conversionOffer;
    private OpenVinoConversionService? _conversionService;
    private OpenVinoInspectionRequestedEventArgs? _openVinoConversionSourceRequest;
    private OpenVinoInspectionRequestedEventArgs? _openVinoRetryRequest;
    private string? _openVinoDirectoryPath;
    private ModelInspectionHandoff? _openVinoHardwareHandoff;
    private OpenVinoConfigurationCandidate? _openVinoConfiguration;
    private OpenVinoStaticPackageEvidence? _openVinoTerminalPackageEvidence;
    internal string? VerifiedOpenVinoWeightLabel =>
        _openVinoTerminalPackageEvidence?.WeightPrecision ?? _openVinoTerminalPackageEvidence?.Precision;
    private bool _openVinoHardwareRouteAvailable;
    private readonly object _openVinoRetirementLock = new();
    private readonly object _navigationRetirementLock = new();
    private Task? _navigationRetirementTask;
    private long _openVinoLifetime;
    private long _openVinoProgressRevision;
    private InspectionProgressRows? _openVinoProgressRows;
    private readonly Queue<ModelInspectionProgress> _openVinoProgressPresentationQueue = new();
    private readonly Dictionary<ModelInspectionStage, ModelInspectionProgress> _openVinoQueuedActiveStages = [];
    private ModelInspectionProgress? _openVinoPresentedActiveProgress;
    private IModelInspectionMilestoneScheduler? _openVinoProgressScheduler;
    private IDisposable? _openVinoProgressScheduledDwell;
    private TaskCompletionSource<object?>? _openVinoProgressDrainCompletion;
    private long _openVinoProgressPacingEpoch;
    private long _openVinoProgressScheduleRevision;
    private long _openVinoMotionGeneration;
    private long _openVinoActivationGeneration;
    private Frame? _openVinoOwnerFrame;
    private Frame? _pendingOpenVinoActivationOwner;
    private RoutedEventHandler? _pendingOpenVinoLoadedHandler;
    private RoutedEventHandler? _pendingOpenVinoUnloadedHandler;
    private EventHandler<object>? _pendingOpenVinoLayoutUpdatedHandler;
    private TaskCompletionSource<bool>? _pendingOpenVinoActivationCompletion;
    private bool _openVinoProgressInputSealed;
    private long _openVinoPendingSuccessLifetime = -1;
    private long _openVinoCancelledOutcomeAccessibilityLifetime = -1;
    private IModelInspectionMotionSettings? _openVinoMotionSettings;
    private EventHandler? _openVinoMotionSettingsChangedHandler;
    private IReadOnlyList<InspectionContentItemPresentation>? _openVinoConversionRows;
    private InspectionModelCardPresentation? _openVinoTerminalModelPresentation;
    private int _requestedOpenVinoNewTokens =
        OpenVinoRouteCapability.DefaultRequestedNewTokens;
    private OpenVinoChatController? _openVinoChatController;
    private bool _openVinoChatWired;
    private string? _activeChatPackageDirectory;
    private OpenVinoRuntimeOptions? _activeChatRuntimeOptions;
    private long _openVinoChatGeneration;
    private Task? _newOpenVinoConversationTask;
    private string? _lastOpenVinoAnnouncement;

    internal GraniteEdgeAI.Features.GgufRuntime.ChatPage OpenVinoChatView => OpenVinoChat;
    private GraniteEdgeAI.Features.GgufRuntime.Controls.ChatComposer OpenVinoComposer =>
        (GraniteEdgeAI.Features.GgufRuntime.Controls.ChatComposer)OpenVinoChat.FindName("Composer");
    private TextBox PromptInput => (TextBox)OpenVinoComposer.FindName("PromptTextBox");
    private Button PromptSendButton => (Button)OpenVinoComposer.FindName("SendButton");
    private Button PromptStopButton => (Button)OpenVinoComposer.FindName("StopButton");
    private Button PromptCancelButton => (Button)OpenVinoChat.FindName("RouteCloseButton");
    private TextBlock PromptCapabilitySummary => (TextBlock)OpenVinoChat.FindName("RouteCapabilityText");
    private TextBlock PromptExecutionEvidenceText => (TextBlock)OpenVinoChat.FindName("RouteExecutionText");
    private TextBlock PromptBuildEvidenceText => (TextBlock)OpenVinoChat.FindName("RouteBuildText");
    private TextBlock PromptResponseText => (TextBlock)OpenVinoChat.FindName("RouteStatusText");

    private void EnsureOpenVinoChat()
    {
        if (_openVinoChatWired) return;
        _openVinoChatWired = true;
        OpenVinoChat.ConfigureOpenVinoChat("OpenVINO");
        OpenVinoChat.SendRequested += async (_, prompt) =>
        {
            if (_promptPresenter?.State.SendEnabled != true) return;
            PromptInput.Text = prompt;
            Task? task = StartOpenVinoPrompt();
            if (task is not null) await task;
        };
        OpenVinoChat.StopRequested += (_, _) => PromptStopButton_Click(this, new RoutedEventArgs());
        OpenVinoChat.CloseSessionRequested += (_, _) => PromptCancelButton_Click(this, new RoutedEventArgs());
        OpenVinoChat.NewChatRequested += async (_, _) =>
        {
            if (_newOpenVinoConversationTask is { IsCompleted: false }) return;
            OpenVinoChat.SetHistoryLoading(true);
            try
            {
                _newOpenVinoConversationTask = RestartOpenVinoConversationAsync();
                await _newOpenVinoConversationTask;
            }
            finally
            {
                OpenVinoChat.SetHistoryLoading(false);
            }
        };
    }

    internal Task? CurrentOpenVinoInspectionTask { get; private set; }
    internal Task? CurrentOpenVinoConversionTask { get; private set; }
    internal Task? CurrentOpenVinoPromptTask { get; private set; }
    internal Task? CurrentOpenVinoStopTask { get; private set; }
    internal Task? CurrentOpenVinoCancelTask { get; private set; }
    internal Task? CurrentOpenVinoCleanupTask { get; private set; }
    internal PromptSurfaceState? CurrentPromptSurfaceState =>
        _promptPresenter?.State;
    internal Func<Task>? NavigationRetirementOverride { get; set; }
    internal PromptTurnResult? LastOpenVinoTurnResult { get; private set; }
    internal int RequestedOpenVinoNewTokens
    {
        get => _requestedOpenVinoNewTokens;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                value,
                OpenVinoRouteCapability.MaximumRequestedNewTokens);
            _requestedOpenVinoNewTokens = value;
        }
    }

    private void BeginOpenVinoInspection(
        OpenVinoInspectionRequestedEventArgs request,
        string directoryPath)
    {
        _openVinoPendingSuccessLifetime = -1;
        _openVinoCancelledOutcomeAccessibilityLifetime = -1;
        ActivePreview.CancelProgressMotion();
        _openVinoRetryRequest = request;
        CancellationTokenSource cancellation = new();
        _openVinoCancellation = cancellation;
        long lifetime = checked(++_openVinoLifetime);
        StartOpenVinoProgressPresentation(lifetime);
        ApplyOpenVinoInspectingPresentation(request.DisplayName);
        OpenVinoRouteService service;
        try
        {
            service = _openVinoRouteService ??=
                ModelInspectionServiceComposition.CreateDefaultOpenVinoRouteService();
            _promptRouteRegistry ??=
                ModelInspectionServiceComposition.CreatePromptRouteRegistry(service);
        }
        catch (Exception)
        {
            StopOpenVinoProgressPresentation(cancelAwaiter: false);
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref _openVinoCancellation,
                    null,
                    cancellation),
                cancellation))
            {
                cancellation.Dispose();
            }
            ApplyOpenVinoInspectionFailurePresentation(
                "runtime_load_failed",
                "The verified OpenVINO worker is unavailable.",
                "Repair or reinstall the app, then retry.");
            OpenVinoRequest = null;
            CurrentOpenVinoInspectionTask = Task.CompletedTask;
            return;
        }
        CurrentOpenVinoInspectionTask = InspectAndStartOpenVinoAsync(
            service,
            request,
            directoryPath,
            lifetime,
            cancellation.Token);
    }

    private async Task InspectAndStartOpenVinoAsync(
        OpenVinoRouteService service,
        OpenVinoInspectionRequestedEventArgs request,
        string directoryPath,
        long lifetime,
        CancellationToken cancellationToken)
    {
        OpenVinoRouteHandoffLease? handoffLease = null;
        OpenVinoConversionOffer? conversionOffer = null;
        OpenVinoConversionOffer? retainedConversionOffer = null;
        try
        {
            IProgress<ModelInspectionProgress> progress =
                new Progress<ModelInspectionProgress>(update =>
                    ApplyOpenVinoProgress(lifetime, update));
            OpenVinoRouteInspectionResult result = await Task.Run(
                () => service.InspectAsync(
                    directoryPath,
                    progress,
                    cancellationToken),
                cancellationToken);
            handoffLease = result.HandoffLease;
            conversionOffer = result.ConversionOffer;
            if (result.Outcome is OpenVinoRouteInspectionOutcome.Ready or
                OpenVinoRouteInspectionOutcome.ReadyWithWarnings)
            {
                await SealAndDrainOpenVinoProgressPresentationAsync(
                    lifetime,
                    cancellationToken);
            }
            else
            {
                StopOpenVinoProgressPresentation(cancelAwaiter: false);
            }
            if (result.Outcome == OpenVinoRouteInspectionOutcome.ConversionRequired &&
                conversionOffer is not null)
            {
                if (!IsCurrentOpenVinoLifetime(lifetime))
                {
                    return;
                }
                _conversionOffer?.Dispose();
                _conversionOffer = conversionOffer;
                retainedConversionOffer = conversionOffer;
                _openVinoConversionSourceRequest = request;
                conversionOffer = null;
            }

            if (!TryApplyOpenVinoInspectionResult(lifetime, result) &&
                retainedConversionOffer is not null &&
                ReferenceEquals(
                    Interlocked.CompareExchange(
                        ref _conversionOffer,
                        null,
                        retainedConversionOffer),
                    retainedConversionOffer))
            {
                retainedConversionOffer.Dispose();
                _openVinoConversionSourceRequest = null;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StopOpenVinoProgressPresentation(cancelAwaiter: false);
            if (IsCurrentOpenVinoLifetime(lifetime))
            {
                ApplyOpenVinoCancelledPresentation();
            }
        }
        finally
        {
            handoffLease?.Dispose();
            conversionOffer?.Dispose();
            if (IsCurrentOpenVinoLifetime(lifetime))
            {
                OpenVinoRequest = null;
            }
        }
    }

    private void ApplyOpenVinoInspectingPresentation(string displayName)
    {
        ApplyOpenVinoRouteVisuals();
        InspectionModelCardControl.DisclosureToggleRequested -=
            OpenVinoInspectionDetails_ToggleRequested;
        _openVinoTerminalModelPresentation = null;
        ApplyFooterStatus(InspectionFooterStatus.InProgress);
        InspectionProgressRows progressRows = new();
        progressRows.Reset(new ModelInspectionRenderKey(
            _openVinoLifetime,
            presentationRevision: 0));
        _openVinoProgressRows = progressRows;
        _openVinoProgressRevision = 0;
        InspectionModelCardControl.Presentation = new InspectionModelCardPresentation
        {
            DisplayMode = InspectionModelCardMode.Compact,
            BadgeState = InspectionModelBadgeState.ModelSelected,
            ModelName = displayName,
            CompactSummary = "OpenVINO GenAI package",
            FormatShortName = "OV",
            OverviewFormatBadgeText = "OpenVINO",
            FormatName = "OpenVINO GenAI IR"
        };
        InspectionContentCardControl.Presentation =
            InitialInspectionProgressPresentationFactory.Create(
                progressRows,
                new InspectionStartupPresentation
                {
                    Visibility = Visibility.Visible,
                    Summary = "Starting secure inspection…",
                    AutomationName = "Checking OpenVINO package. Starting secure inspection."
                });
        HideOpenVinoSpecialProgress();
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Inspecting,
            Message = "Inspection runs locally in a protected worker.",
            CancelAction = new InspectionActionPresentation
            {
                Text = "Cancel",
                AutomationName = "Cancel OpenVINO inspection",
                ActionId = "cancel-openvino-inspection",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ =>
                    _openVinoCancellation?.Cancel())
            }
        };
        InspectionOutcomeCardControl.Presentation =
            InspectionOutcomePresentation.Hidden;
        SetPromptSurfaceVisible(false);
        SetPromptControlsEnabled(send: false, stop: false, cancel: false);
        PromptCapabilitySummary.Text = string.Empty;
        PromptExecutionEvidenceText.Text = string.Empty;
        PromptBuildEvidenceText.Text = string.Empty;
    }

    private void ApplyOpenVinoProgress(
        long lifetime,
        ModelInspectionProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (!IsCurrentOpenVinoLifetime(lifetime) ||
            _openVinoProgressInputSealed ||
            _openVinoCancellation?.IsCancellationRequested == true ||
            _openVinoProgressRows is not InspectionProgressRows rows)
        {
            return;
        }

        progress = ProjectOpenVinoProgressForDisplay(progress);
        ApplyOpenVinoProgressCore(lifetime, progress, rows);
    }

    private void ApplyOpenVinoProgressCore(
        long lifetime,
        ModelInspectionProgress progress,
        InspectionProgressRows rows)
    {
        if (!IsCurrentOpenVinoLifetime(lifetime) ||
            !ReferenceEquals(_openVinoProgressRows, rows))
        {
            return;
        }

        long revision = checked(++_openVinoProgressRevision);
        ModelInspectionRenderKey ownerKey = new(lifetime, revision);
        InspectionProgressRowsUpdate update =
            InspectionProgressPresentationFactory.Create(progress, ownerKey);
        InspectionProgressRowsApplyResult changes = rows.Apply(update);
        if (!changes.IsEmpty)
        {
            InspectionContentCardControl.RefreshProgressPresentation();
            if (changes.ProgressSummaryChanged || changes.RowChanges.Any(change => change.StatusChanged || change.DetailChanged))
            {
                InspectionContentCardControl.AnnounceProgress(
                    $"{rows.ProgressSummary}. {progress.UserMessage}");
            }
        }
    }

    private static ModelInspectionProgress ProjectOpenVinoProgressForDisplay(
        ModelInspectionProgress progress)
    {
        const string verboseInitialDetail =
            "Reading and verifying the large model file securely. This can take up to a minute.";
        if (progress.Stage != ModelInspectionStage.CheckModelPackage ||
            progress.StageStatus != ModelInspectionStageStatus.Active ||
            !string.Equals(
                progress.UserMessage,
                verboseInitialDetail,
                StringComparison.Ordinal))
        {
            return progress;
        }

        return new ModelInspectionProgress(
            progress.Stage,
            progress.StageStatus,
            progress.CompletedStageCount,
            progress.TotalStageCount,
            progress.StageFraction,
            "Reading and verifying the large model file securely.");
    }

    private static bool IsOpenVinoFractionOnlyChange(
        ModelInspectionProgress previous,
        ModelInspectionProgress current) =>
        previous.Stage == current.Stage &&
        previous.StageStatus == ModelInspectionStageStatus.Active &&
        current.StageStatus == ModelInspectionStageStatus.Active &&
        previous.CompletedStageCount == current.CompletedStageCount &&
        previous.TotalStageCount == current.TotalStageCount &&
        (previous.StageFraction != current.StageFraction ||
         !string.Equals(previous.UserMessage, current.UserMessage, StringComparison.Ordinal));

    private void StartOpenVinoProgressPresentation(long lifetime)
    {
        StopOpenVinoProgressPresentation(cancelAwaiter: true);
        _openVinoProgressPacingEpoch = lifetime;
        _openVinoProgressScheduleRevision = 0;
        _openVinoProgressInputSealed = false;
        _openVinoQueuedActiveStages.Clear();
        _openVinoPresentedActiveProgress = null;
        _openVinoProgressDrainCompletion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void PresentNextOpenVinoProgress(long lifetime)
    {
        if (!IsCurrentOpenVinoLifetime(lifetime) ||
            lifetime != _openVinoProgressPacingEpoch ||
            _openVinoProgressScheduledDwell is not null ||
            _openVinoProgressRows is not InspectionProgressRows rows)
        {
            return;
        }

        while (_openVinoProgressPresentationQueue.TryDequeue(
            out ModelInspectionProgress? progress))
        {
            if (progress.StageStatus == ModelInspectionStageStatus.Active)
            {
                if (_openVinoQueuedActiveStages.Remove(progress.Stage, out var latest))
                    progress = latest;
                _openVinoPresentedActiveProgress = progress;
            }
            else if (_openVinoPresentedActiveProgress?.Stage == progress.Stage)
            {
                _openVinoPresentedActiveProgress = null;
            }

            ApplyOpenVinoProgressCore(lifetime, progress, rows);
            if (progress.StageStatus != ModelInspectionStageStatus.Active ||
                _motionSettings?.AnimationsEnabled == false)
            {
                continue;
            }

            IModelInspectionMilestoneScheduler? scheduler =
                _openVinoProgressScheduler;
            if (scheduler is null)
            {
                return;
            }

            long epoch = _openVinoProgressPacingEpoch;
            long revision = checked(++_openVinoProgressScheduleRevision);
            _openVinoProgressScheduledDwell = scheduler.Schedule(
                ModelInspectionMilestoneSequencer.MinimumVisibleStage,
                () => OnOpenVinoProgressDwellElapsed(
                    lifetime,
                    epoch,
                    revision));
            return;
        }

        if (_openVinoProgressInputSealed)
        {
            _openVinoProgressDrainCompletion?.TrySetResult(null);
        }
    }

    private void OnOpenVinoProgressDwellElapsed(
        long lifetime,
        long epoch,
        long revision)
    {
        if (!IsCurrentOpenVinoLifetime(lifetime) ||
            epoch != _openVinoProgressPacingEpoch ||
            revision != _openVinoProgressScheduleRevision)
        {
            return;
        }

        IDisposable? completedDwell = _openVinoProgressScheduledDwell;
        _openVinoProgressScheduledDwell = null;
        completedDwell?.Dispose();
        PresentNextOpenVinoProgress(lifetime);
    }

    private async Task SealAndDrainOpenVinoProgressPresentationAsync(
        long lifetime,
        CancellationToken cancellationToken)
    {
        if (!IsCurrentOpenVinoLifetime(lifetime) ||
            lifetime != _openVinoProgressPacingEpoch)
        {
            return;
        }

        _openVinoProgressInputSealed = true;
        TaskCompletionSource<object?>? completion =
            _openVinoProgressDrainCompletion;
        if (completion is null)
        {
            return;
        }

        completion.TrySetResult(null);
        using CancellationTokenRegistration registration =
            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        await completion.Task;
        StopOpenVinoProgressPresentation(cancelAwaiter: false);
    }

    private void StopOpenVinoProgressPresentation(bool cancelAwaiter)
    {
        checked
        {
            _openVinoProgressScheduleRevision++;
        }

        _openVinoProgressInputSealed = false;
        _openVinoProgressPresentationQueue.Clear();
        _openVinoQueuedActiveStages.Clear();
        _openVinoPresentedActiveProgress = null;
        Interlocked.Exchange(ref _openVinoProgressScheduledDwell, null)?.Dispose();
        Interlocked.Exchange(ref _openVinoProgressScheduler, null)?.Dispose();
        TaskCompletionSource<object?>? completion =
            Interlocked.Exchange(ref _openVinoProgressDrainCompletion, null);
        if (cancelAwaiter)
        {
            completion?.TrySetCanceled();
        }
        else
        {
            completion?.TrySetResult(null);
        }
    }

    private bool TryApplyOpenVinoInspectionResult(
        long lifetime,
        OpenVinoRouteInspectionResult result)
    {
        if (!IsCurrentOpenVinoLifetime(lifetime))
        {
            return false;
        }

        if (result.Outcome is OpenVinoRouteInspectionOutcome.Ready or
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings)
        {
            if (result.HandoffLease is null)
            {
                ApplyOpenVinoInspectionFailurePresentation(
                    "runtime_protocol_failed",
                    "The verified OpenVINO worker returned an incomplete result.",
                    "Repair or reinstall the app, then retry.");
                return true;
            }

            PrepareOpenVinoHardwareHandoff(result);
            InspectionProgressRows? rows = _openVinoProgressRows;
            _openVinoPendingSuccessLifetime = lifetime;
            ActivePreview.CompleteProgressPresentation(() =>
                CompleteOpenVinoInspectionPresentation(
                    lifetime,
                    rows,
                    result));
            return true;
        }

        _openVinoProgressRows = null;
        ApplyOpenVinoNonReadyPresentation(result);
        return true;
    }

    private void CompleteOpenVinoInspectionPresentation(
        long lifetime,
        InspectionProgressRows? rows,
        OpenVinoRouteInspectionResult result)
    {
        if (_openVinoPendingSuccessLifetime != lifetime
            || !IsCurrentOpenVinoLifetime(lifetime)
            || rows is null
            || !ReferenceEquals(_openVinoProgressRows, rows)) return;
        _openVinoPendingSuccessLifetime = -1;
        _openVinoProgressRows = null;
        ApplyOpenVinoReadyPresentation(result);
    }

    private void ApplyOpenVinoReadyPresentation(
        OpenVinoRouteInspectionResult result)
    {
        HideOpenVinoSpecialProgress();
        ApplyFooterStatus(InspectionFooterStatus.Complete);
        bool warnings = result.Outcome ==
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings;
        InspectionOutcomeCardControl.Presentation = new InspectionOutcomePresentation
        {
            Kind = warnings
                ? InspectionOutcomePresentationKind.ReadyWithWarnings
                : InspectionOutcomePresentationKind.Ready,
            Tone = warnings ? InspectionOutcomeTone.Warning : InspectionOutcomeTone.Success,
            GlyphKind = warnings
                ? InspectionStatusGlyphKind.Warning
                : InspectionStatusGlyphKind.Success,
            Title = warnings
                ? "Model inspection complete with warnings"
                : "Model inspection complete",
            Message = warnings
                ? "The package is ready for local CPU prompting with a non-blocking chat-template warning."
                : "The package is ready for local CPU prompting.",
            AutomationName = warnings
                ? "OpenVINO inspection ready with warnings"
                : "OpenVINO inspection ready"
        };
        ApplyDirectOutcomeTone(warnings
            ? InspectionOutcomeTone.Warning
            : InspectionOutcomeTone.Success);
        _openVinoTerminalModelPresentation =
            CreateOpenVinoReadyModelPresentation(result, warnings, isExpanded: false);
        InspectionModelCardControl.DisclosureToggleRequested -=
            OpenVinoInspectionDetails_ToggleRequested;
        InspectionModelCardControl.DisclosureToggleRequested +=
            OpenVinoInspectionDetails_ToggleRequested;
        InspectionModelCardControl.Presentation =
            _openVinoTerminalModelPresentation;
        ApplyOpenVinoTerminalMetadata(result);
        ApplyOpenVinoModelVisualStates(warnings);
        InspectionContentCardControl.Presentation =
            InspectionContentCardPresentation.Hidden;
        ApplyOpenVinoHardwareAction(warnings);
        SetPromptSurfaceVisible(false);
        SetPromptControlsEnabled(send: false, stop: false, cancel: false);
        AnnouncePromptStatus(warnings
            ? "OpenVINO model inspection completed with warnings. Continue to the hardware check."
            : "OpenVINO model inspection completed. Continue to the hardware check.");
    }

    private InspectionModelCardPresentation CreateOpenVinoReadyModelPresentation(
        OpenVinoRouteInspectionResult result,
        bool warnings,
        bool isExpanded)
    {
        string modelName = InspectionModelCardControl.Presentation.ModelName;
        OpenVinoConfigurationCandidate? configuration = result.Configuration;
        string context = configuration is null
            ? "Not reported"
            : $"{configuration.MaximumContextTokens:N0} tokens";
        InspectionCheckPresentation[] checks =
        [
            CreateOpenVinoCheck(
                "Model package",
                "The OpenVINO package was opened and its required resources were read."),
            CreateOpenVinoCheck(
                "Model configuration",
                "Core configuration fields required for inspection were available."),
            CreateOpenVinoCheck(
                "Tokenizer and chat setup",
                warnings
                    ? "Tokenizer resources are valid; no embedded chat template was reported."
                    : "Tokenizer resources and chat configuration were checked.",
                warnings),
            CreateOpenVinoCheck(
                "Model structure",
                "The model graph and declared dimensions passed validation."),
            CreateOpenVinoCheck(
                "Core runtime support",
                "The package uses an OpenVINO format supported by the local runtime.")
        ];
        return new InspectionModelCardPresentation
        {
            DisplayMode = InspectionModelCardMode.Detailed,
            BadgeState = warnings
                ? InspectionModelBadgeState.Incomplete
                : InspectionModelBadgeState.Inspected,
            ModelName = modelName,
            CompactSummary = "OpenVINO GenAI package",
            FormatShortName = "OV",
            OverviewFormatBadgeText = "OpenVINO GenAI",
            Publisher = "OpenVINO IR package",
            FormatName = "Verified",
            Quantisation = "Text generation",
            ParameterCount = "Granite",
            ModelType = context,
            DeclaredContext = "Not reported",
            FileSize = "Validated",
            InspectionChecksSummary = warnings
                ? "4 checks passed · 1 warning"
                : "All 5 inspection checks passed",
            InspectionChecks = checks,
            InspectionDetailsVisibility = Visibility.Visible,
            IsInspectionDetailsExpanded = isExpanded
        };
    }

    private static InspectionCheckPresentation CreateOpenVinoCheck(
        string title,
        string detail,
        bool warning = false)
    {
        string status = warning ? "Warning" : "Passed";
        return new InspectionCheckPresentation
        {
            Title = title,
            Detail = detail,
            Status = warning
                ? InspectionCheckStatus.Warning
                : InspectionCheckStatus.Passed,
            StatusText = status,
            AutomationName = $"{title}. {status}. {detail}"
        };
    }

    private void ApplyOpenVinoModelVisualStates(bool warnings)
    {
        // Route identity and warning tone are native to the exact OpenVINO
        // preview tree and are projected from the typed presentation
    }

    private void ApplyOpenVinoRouteVisuals()
    {
        SelectOpenVinoPreview();
    }

    private void ApplyOpenVinoTerminalMetadata(
        OpenVinoRouteInspectionResult result)
    {
        _openVinoTerminalPackageEvidence = null;
        OpenVinoStaticPackageEvidence? evidence = null;
        if (!string.IsNullOrWhiteSpace(_openVinoDirectoryPath))
        {
            OpenVinoStaticPackageInspectionResult inspection =
                new OpenVinoStaticPackageInspector().Inspect(_openVinoDirectoryPath);
            if (inspection.Status ==
                    OpenVinoStaticInspectionStatus.NativeValidationRequired &&
                inspection.Evidence is { } acceptedEvidence)
            {
                evidence = acceptedEvidence;
                _openVinoTerminalPackageEvidence = acceptedEvidence;
            }
        }

        string context = evidence is not null
            ? $"{evidence.ContextLength:N0} tokens"
            : result.Configuration is not null
                ? $"{result.Configuration.MaximumContextTokens:N0} tokens"
                : "Not reported";
        string precision = evidence?.WeightPrecision ??
            evidence?.Precision ??
            "Not reported";

        ActivePreview.ApplyOpenVinoMetadata(
            evidence?.ModelType ?? "Not reported",
            "OpenVINO GenAI IR",
            evidence?.Architecture ?? "Not reported",
            context,
            precision,
            evidence?.TokenizerClass ?? "Not reported",
            evidence is null ? "Not reported" : $"{evidence.ResourceCount:N0} files",
            evidence?.Task ?? "Not reported");
    }

    private void OpenVinoInspectionDetails_ToggleRequested(
        object? sender,
        InspectionDisclosureToggleRequestedEventArgs eventArguments)
    {
        if (!ReferenceEquals(sender, ActivePreview) ||
            _openVinoTerminalModelPresentation is not { } current)
        {
            return;
        }

        InspectionModelCardControl.ClaimDisclosureTarget(
            eventArguments.IsExpanded);
        InspectionModelCardControl.PrepareDisclosureTarget(
            eventArguments.IsExpanded);
        _openVinoTerminalModelPresentation = CloneOpenVinoModelPresentation(
            current,
            eventArguments.IsExpanded);
        InspectionModelCardControl.Presentation =
            _openVinoTerminalModelPresentation;
        ApplyOpenVinoModelVisualStates(
            current.BadgeState == InspectionModelBadgeState.Incomplete);
        InspectionModelCardControl.CompleteDisclosureTarget(
            eventArguments.IsExpanded);
    }

    private static InspectionModelCardPresentation CloneOpenVinoModelPresentation(
        InspectionModelCardPresentation source,
        bool isExpanded) => new()
    {
        DisplayMode = source.DisplayMode,
        BadgeState = source.BadgeState,
        ModelName = source.ModelName,
        CompactSummary = source.CompactSummary,
        FormatShortName = source.FormatShortName,
        OverviewFormatBadgeText = source.OverviewFormatBadgeText,
        Publisher = source.Publisher,
        FormatName = source.FormatName,
        Quantisation = source.Quantisation,
        ParameterCount = source.ParameterCount,
        ModelType = source.ModelType,
        DeclaredContext = source.DeclaredContext,
        FileSize = source.FileSize,
        InspectionChecksSummary = source.InspectionChecksSummary,
        InspectionChecks = source.InspectionChecks,
        InspectionDetailsVisibility = source.InspectionDetailsVisibility,
        IsInspectionDetailsExpanded = isExpanded
    };

    private void PrepareOpenVinoHardwareHandoff(
        OpenVinoRouteInspectionResult result)
    {
        ModelInspectionHandoffV2 source = result.Handoff
            ?? throw new InvalidOperationException("A ready OpenVINO result requires a handoff.");
        ModelOutcome outcome = source.Outcome ==
            GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionOutcomeV2.Ready
                ? ModelOutcome.Ready
                : ModelOutcome.ReadyWithWarnings;
        _openVinoHardwareHandoff = new ModelInspectionHandoff(
            ModelInspectionHandoff.CurrentSchemaVersion,
            source.ModelInspectionHandoffId,
            source.ModelInspectionRunId,
            outcome,
            source.ModelSha256,
            source.ModelLengthBytes);
        _openVinoConfiguration = result.Configuration;
    }

    private void ApplyOpenVinoHardwareAction(bool warnings)
    {
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            Title = warnings ? "Model is ready with warnings" : "Model is ready",
            Message = "Check this model against the memory and devices available on this computer.",
            AutomationName = "Actions after OpenVINO model inspection",
            SecondaryActionOne = new InspectionActionPresentation
            {
                Text = "Choose another model",
                AutomationName = "Choose another model",
                ActionId = "choose-another-model",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ =>
                    ChooseAnotherModelRequested?.Invoke(this, EventArgs.Empty))
            },
            PrimaryAction = new InspectionActionPresentation
            {
                Text = "Check hardware fit",
                AutomationName = "Check model hardware fit",
                ActionId = "hardware-fit",
                Visibility = Visibility.Visible,
                IsEnabled = _openVinoHardwareRouteAvailable &&
                    _openVinoHardwareHandoff is not null,
                AutomationHelpText = _openVinoHardwareRouteAvailable
                    ? string.Empty
                    : "Hardware inspection is not available.",
                Command = new DelegateCommand(_ => RequestOpenVinoHardwareInspection())
            }
        };
    }

    private void RequestOpenVinoHardwareInspection()
    {
        if (!_openVinoHardwareRouteAvailable ||
            _openVinoHardwareHandoff is not { } handoff)
        {
            return;
        }

        HardwareInspectionRequested?.Invoke(
            this,
            new HardwareInspectionRequestedEventArgs(handoff));
    }

    internal void SetOpenVinoHardwareRouteAvailable(bool isAvailable)
    {
        _openVinoHardwareRouteAvailable = isAvailable;
        if (_openVinoHardwareHandoff is not null)
        {
            ApplyOpenVinoHardwareAction(
                _openVinoHardwareHandoff.Outcome == ModelOutcome.ReadyWithWarnings);
        }
    }

    internal bool TryGetOpenVinoCompatibilityEvidence(
        ModelInspectionHandoff handoff,
        out OpenVinoStaticPackageEvidence? evidence)
    {
        evidence = null;
        if (_openVinoHardwareHandoff is null ||
            !ReferenceEquals(handoff, _openVinoHardwareHandoff) ||
            string.IsNullOrWhiteSpace(_openVinoDirectoryPath) ||
            _openVinoConfiguration is null ||
            _openVinoTerminalPackageEvidence is not { } candidate)
        {
            return false;
        }

        if (!string.Equals(candidate.ModelSha256, handoff.ModelSha256,
                StringComparison.Ordinal) ||
            candidate.ModelLengthBytes != handoff.ModelLengthBytes)
        {
            return false;
        }

        evidence = candidate;
        return true;
    }

    internal bool TryGetOpenVinoSourceDirectory(
        ModelInspectionHandoff handoff,
        out string? directoryPath)
    {
        directoryPath = null;
        if (_openVinoHardwareHandoff is null ||
            !ReferenceEquals(handoff, _openVinoHardwareHandoff) ||
            string.IsNullOrWhiteSpace(_openVinoDirectoryPath))
        {
            return false;
        }

        directoryPath = _openVinoDirectoryPath;
        return true;
    }

    internal bool TryGetOpenVinoBuildEvidence(
        out OpenVinoBuildEvidence? buildEvidence)
    {
        buildEvidence = _openVinoRouteService?.ExpectedBuildEvidence;
        return _openVinoRouteService is not null && buildEvidence is not null;
    }

    internal bool TryCreateOpenVinoOptimizationService(
        out OpenVinoOptimizationService? service,
        out OpenVinoBuildEvidence? turboQuantBuildEvidence)
    {
        service = null;
        turboQuantBuildEvidence = null;
        if (_openVinoRouteService is null)
        {
            return false;
        }
        try
        {
            ModelInspectionServiceComposition
                .TryCreateDefaultOpenVinoTurboQuantRouteService(
                    out OpenVinoRouteService? turboQuantRouteService);
            turboQuantBuildEvidence = turboQuantRouteService?.ExpectedBuildEvidence;
            service = ModelInspectionServiceComposition
                .CreateDefaultOpenVinoOptimizationService(
                    _openVinoRouteService,
                    turboQuantRouteService);
            return true;
        }
        catch
        {
            service = null;
            turboQuantBuildEvidence = null;
            return false;
        }
    }

    internal GraniteEdgeAI.Features.GgufRuntime.Services.IGgufChatSession CreateSharedOpenVinoChatSession(
        string packageDirectory, OpenVinoRuntimeOptions runtimeOptions)
    {
        OpenVinoRouteService? service = _openVinoRouteService;
        if (RequiresTurboQuantChatWorker(runtimeOptions))
            ModelInspectionServiceComposition.TryCreateDefaultOpenVinoTurboQuantRouteService(out service);
        else
            service ??= ModelInspectionServiceComposition.CreateDefaultOpenVinoRouteService();
        if (service is null) throw new OpenVinoRouteWorkerFailureException(
            OpenVinoSupportCode.RuntimeDependencyMissing, "The verified OpenVINO worker is unavailable.");
        return new OpenVinoSharedChatSessionAdapter(async (turns, sink, token) =>
        {
            OpenVinoRouteInspectionResult inspection = await service.InspectAsync(packageDirectory, token);
            using OpenVinoRouteHandoffLease? lease = inspection.HandoffLease;
            if (inspection.Outcome is not (OpenVinoRouteInspectionOutcome.Ready or OpenVinoRouteInspectionOutcome.ReadyWithWarnings) || lease is null)
                throw CreateSharedOpenVinoInspectionFailure(inspection, token);
            return await service.StartSessionAsync(lease, sink, runtimeOptions, token, turns);
        });
    }

    internal static Exception CreateSharedOpenVinoInspectionFailure(
        OpenVinoRouteInspectionResult inspection, CancellationToken cancellationToken)
    {
        OpenVinoSupportCode? code = inspection.Outcome is OpenVinoRouteInspectionOutcome.Ready or OpenVinoRouteInspectionOutcome.ReadyWithWarnings
            ? OpenVinoSupportCode.RuntimeProtocolFailed
            : OpenVinoActivationOutcomePolicy.FromInspection(inspection).SupportCode;
        if (code == OpenVinoSupportCode.OperationCancelled)
            return new OperationCanceledException(cancellationToken);
        return code is { } supportCode
            ? new OpenVinoRouteWorkerFailureException(supportCode, "The selected OpenVINO model could not be revalidated.")
            : new InvalidOperationException("The selected OpenVINO model requires preparation before chat.");
    }

    internal async Task<bool> ActivateOpenVinoChatAsync(
        CancellationToken cancellationToken) =>
        (await ActivateOpenVinoChatWithResultAsync(cancellationToken)).IsActivated;

    internal async Task<bool> ActivateOpenVinoChatFromDirectoryAsync(
        string? packageDirectory,
        CancellationToken cancellationToken) =>
        (await ActivateOpenVinoChatFromDirectoryWithResultAsync(
            packageDirectory,
            cancellationToken)).IsActivated;

    internal async Task<bool> ActivateOpenVinoChatTargetAsync(
        OpenVinoOptimizationChatTarget target,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        return (await ActivateOpenVinoChatTargetWithResultAsync(target, cancellationToken)).IsActivated;
    }

    internal Task<OpenVinoChatActivationResult> ActivateOpenVinoChatTargetWithResultAsync(
        OpenVinoOptimizationChatTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        return ActivateOpenVinoChatFromDirectoryWithResultAsync(
            target.PackageDirectory,
            target.RuntimeOptions,
            cancellationToken);
    }

    internal static bool RequiresTurboQuantChatWorker(OpenVinoRuntimeOptions options) =>
        options.KvCachePrecision is "tbq3" or "tbq4";

    internal Task<OpenVinoChatActivationResult> ActivateOpenVinoChatWithResultAsync(
        CancellationToken cancellationToken) =>
        ActivateOpenVinoChatFromDirectoryWithResultAsync(
            _openVinoDirectoryPath,
            cancellationToken);

    internal async Task<OpenVinoChatActivationResult>
        ActivateOpenVinoChatFromDirectoryWithResultAsync(
            string? packageDirectory,
            CancellationToken cancellationToken) =>
        await ActivateOpenVinoChatFromDirectoryWithResultAsync(
            packageDirectory,
            OpenVinoRuntimeOptions.ReleasedDefault,
            cancellationToken);

    private async Task<OpenVinoChatActivationResult>
        ActivateOpenVinoChatFromDirectoryWithResultAsync(
            string? packageDirectory,
            OpenVinoRuntimeOptions runtimeOptions,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        runtimeOptions.Validate();
        OpenVinoRouteService? chatService = _openVinoRouteService;
        if (RequiresTurboQuantChatWorker(runtimeOptions))
        {
            ModelInspectionServiceComposition.TryCreateDefaultOpenVinoTurboQuantRouteService(out chatService);
        }
        if (chatService is null
            || _promptRouteRegistry is null
            || string.IsNullOrWhiteSpace(packageDirectory))
        {
            return OpenVinoActivationOutcomePolicy.FromSupportCode(
                OpenVinoSupportCode.RuntimeDependencyMissing);
        }
        OpenVinoRouteHandoffLease? lease = null;
        long lifetime = _openVinoLifetime;
        long chatGeneration = _openVinoChatGeneration;
        try
        {
            OpenVinoRouteInspectionResult inspection =
                await chatService.InspectAsync(
                    packageDirectory, cancellationToken);
            lease = inspection.HandoffLease;
            if (!IsCurrentOpenVinoLifetime(lifetime) || chatGeneration != _openVinoChatGeneration)
                return OpenVinoActivationOutcomePolicy.FromSupportCode(OpenVinoSupportCode.OperationCancelled);
            if (inspection.Outcome is not (OpenVinoRouteInspectionOutcome.Ready or
                OpenVinoRouteInspectionOutcome.ReadyWithWarnings))
            {
                return OpenVinoActivationOutcomePolicy.FromInspection(inspection);
            }
            if (lease is null)
            {
                return OpenVinoActivationOutcomePolicy.FromSupportCode(
                    OpenVinoSupportCode.RuntimeProtocolFailed);
            }
            List<PromptEvent> buffered = [];
            object eventGate = new();
            bool presenterReady = false;
            PromptRouteSessionActivation activation =
                await chatService.ActivateAsync(
                    lease,
                    runtimeOptions,
                    promptEvent =>
                    {
                        lock (eventGate)
                        {
                            if (!presenterReady)
                            {
                                buffered.Add(promptEvent);
                                return;
                            }
                        }
                        ApplyPromptEvent(lifetime, promptEvent, chatGeneration);
                    },
                    cancellationToken);
            lease = null;
            if (!IsCurrentOpenVinoLifetime(lifetime) || chatGeneration != _openVinoChatGeneration)
            {
                await activation.Session.DisposeAsync();
                return OpenVinoActivationOutcomePolicy.FromSupportCode(
                    OpenVinoSupportCode.OperationCancelled);
            }
            _promptSession = activation.Session;
            CurrentOpenVinoStopTask = null;
            CurrentOpenVinoCancelTask = null;
            _activeChatPackageDirectory = packageDirectory;
            _activeChatRuntimeOptions = runtimeOptions;
            _openVinoChatController = new OpenVinoChatController();
            EnsureOpenVinoChat();
            OpenVinoChat.ClearTranscript();
            OpenVinoChat.SetModelHeader(System.IO.Path.GetFileName(packageDirectory),
                string.IsNullOrWhiteSpace(activation.Presentation.ExecutionEvidence) ? "OpenVINO" : activation.Presentation.ExecutionEvidence,
                $"OpenVINO · {activation.Session.Capability.Device}");
            OpenVinoChat.ConfigureOpenVinoChat($"OpenVINO · {runtimeOptions.KvCachePrecision}");
            _promptPresenter = new PromptSessionPresenter(activation.Presentation);
            lock (eventGate)
            {
                presenterReady = true;
                foreach (PromptEvent promptEvent in buffered)
                {
                    _promptPresenter.Apply(promptEvent);
                }
                buffered.Clear();
            }
            SetPromptSurfaceVisible(true);
            ApplyPromptSurfaceState(_promptPresenter.State);
            AnnouncePromptStatus("OpenVINO local chat is ready.");
            return OpenVinoActivationOutcomePolicy.Activated(inspection.Outcome);
        }
        catch (OperationCanceledException)
        {
            return OpenVinoActivationOutcomePolicy.FromSupportCode(
                OpenVinoSupportCode.OperationCancelled);
        }
        catch (OpenVinoRouteWorkerFailureException failure)
        {
            return OpenVinoActivationOutcomePolicy.FromSupportCode(
                failure.SupportCode);
        }
        finally
        {
            lease?.Dispose();
        }
    }

    private void ApplyOpenVinoNonReadyPresentation(
        OpenVinoRouteInspectionResult result)
    {
        HideOpenVinoSpecialProgress();
        OpenVinoInspectionPresentationDisposition disposition =
            OpenVinoInspectionPresentationPolicy.Create(result);
        if (disposition.Kind == OpenVinoInspectionPresentationKind.Cancelled)
        {
            ApplyOpenVinoCancelledPresentation();
            return;
        }

        ApplyFooterStatus(
            disposition.Kind == OpenVinoInspectionPresentationKind.OperationalFailure
                ? InspectionFooterStatus.Interrupted
                : InspectionFooterStatus.NotComplete);

        (InspectionOutcomePresentationKind kind, InspectionContentCardMode mode) =
            disposition.Kind switch
        {
            OpenVinoInspectionPresentationKind.ConversionRequired =>
                (InspectionOutcomePresentationKind.ConversionRequired,
                    InspectionContentCardMode.ConversionRequired),
            OpenVinoInspectionPresentationKind.IncompletePackage =>
                (InspectionOutcomePresentationKind.IncompletePackage,
                    InspectionContentCardMode.IncompletePackage),
            OpenVinoInspectionPresentationKind.Unsupported =>
                (InspectionOutcomePresentationKind.Unsupported,
                    InspectionContentCardMode.Unsupported),
            OpenVinoInspectionPresentationKind.OperationalFailure =>
                (InspectionOutcomePresentationKind.OperationalFailure,
                    InspectionContentCardMode.OperationalFailure),
            OpenVinoInspectionPresentationKind.Invalid =>
                (InspectionOutcomePresentationKind.Invalid,
                    InspectionContentCardMode.Invalid),
            OpenVinoInspectionPresentationKind.Cancelled =>
                throw new InvalidOperationException(
                    "Cancelled OpenVINO presentation must use its established state."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(disposition),
                disposition.Kind,
                "Unknown OpenVINO presentation disposition.")
        };
        bool conversion = disposition.Kind ==
            OpenVinoInspectionPresentationKind.ConversionRequired;
        InspectionOutcomeTone tone = disposition.IsWarning
            ? InspectionOutcomeTone.Warning
            : InspectionOutcomeTone.Error;
        InspectionStatusGlyphKind glyphKind = disposition.IsWarning
            ? InspectionStatusGlyphKind.Warning
            : InspectionStatusGlyphKind.Error;
        InspectionContentStatus diagnosticStatus = disposition.IsWarning
            ? InspectionContentStatus.Warning
            : InspectionContentStatus.Error;
        InspectionOutcomeCardControl.Presentation = new InspectionOutcomePresentation
        {
            Kind = kind,
            Tone = tone,
            GlyphKind = glyphKind,
            Title = disposition.Title,
            Message = disposition.Message,
            AutomationName = $"OpenVINO inspection. {disposition.Title}. " +
                disposition.Message
        };
        ApplyDirectOutcomeTone(tone);
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = mode,
            SectionTitle = disposition.Title,
            SupportingText = disposition.RecoveryAction,
            SupportingTextVisibility = Visibility.Visible,
            DiagnosticCode = disposition.DiagnosticCode,
            DiagnosticCodeVisibility = Visibility.Visible,
            DiagnosticStatus = diagnosticStatus
        };
        if (conversion)
        {
            ApplyOpenVinoConversionAction();
        }
        else if (disposition.Kind ==
            OpenVinoInspectionPresentationKind.OperationalFailure)
        {
            ApplyOpenVinoInspectionRecoveryAction(disposition.Title);
        }
        else
        {
            ApplyChooseAnotherAction(disposition.Title);
        }
        SetPromptSurfaceVisible(false);
        SetPromptControlsEnabled(send: false, stop: false, cancel: false);
    }

    private void ApplyOpenVinoConversionAction()
    {
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            Title = "Prepare this model",
            Message = "Create a verified FP16 OpenVINO package beside the selected source.",
            AutomationName = "OpenVINO conversion actions",
            SecondaryActionOne = new InspectionActionPresentation
            {
                Text = "Choose another model",
                AutomationName = "Choose another model",
                ActionId = "choose-another-model",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ =>
                    ChooseAnotherModelRequested?.Invoke(this, EventArgs.Empty))
            },
            PrimaryAction = new InspectionActionPresentation
            {
                Text = "Convert to OpenVINO",
                AutomationName = "Convert model to OpenVINO",
                ActionId = "convert-openvino",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ => BeginOpenVinoConversion())
            }
        };
    }

    private async void BeginOpenVinoConversion()
    {
        OpenVinoConversionOffer? offer = _conversionOffer;
        if (offer is null || _openVinoRouteService is null ||
            _openVinoCancellation is null)
        {
            return;
        }
        ContentDialog confirmation = new()
        {
            XamlRoot = XamlRoot,
            Title = "Create an OpenVINO package?",
            Content = "A new FP16 package will be created beside the selected source. The source will not be changed.",
            PrimaryButtonText = "Convert",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _conversionOffer = null;
        long lifetime = _openVinoLifetime;
        try
        {
            _conversionService ??=
                ModelInspectionServiceComposition.CreateDefaultOpenVinoConversionService(
                    _openVinoRouteService);
        }
        catch (Exception)
        {
            offer.Dispose();
            ApplyOpenVinoConversionFailure(OpenVinoSupportCode.RuntimeIntegrityFailed);
            return;
        }
        ApplyOpenVinoConvertingPresentation();
        CurrentOpenVinoConversionTask = ConvertAndActivateOpenVinoAsync(
            offer,
            lifetime,
            _openVinoCancellation.Token);
        await CurrentOpenVinoConversionTask;
    }

    private async Task ConvertAndActivateOpenVinoAsync(
        OpenVinoConversionOffer offer,
        long lifetime,
        CancellationToken cancellationToken)
    {
        try
        {
            OpenVinoConversionResult converted = await _conversionService!.ConvertAsync(
                offer,
                confirmed: true,
                new Progress<OpenVinoConversionProgress>(value =>
                {
                    if (IsCurrentOpenVinoLifetime(lifetime))
                    {
                        ApplyOpenVinoConversionProgress(value.Stage);
                    }
                }),
                cancellationToken);
            if (!IsCurrentOpenVinoLifetime(lifetime)) return;
            if (converted.Status == OpenVinoConversionStatus.Cancelled)
            {
                ApplyOpenVinoConversionFailure(OpenVinoSupportCode.OperationCancelled);
                return;
            }
            if (converted.Status != OpenVinoConversionStatus.Published ||
                converted.PublishedDirectory is null)
            {
                ApplyOpenVinoConversionFailure(converted.SupportCode ??
                    OpenVinoSupportCode.ConversionFailed);
                return;
            }

            OpenVinoRouteInspectionResult inspection = await _openVinoRouteService!
                .InspectAsync(converted.PublishedDirectory, cancellationToken);
            OpenVinoRouteHandoffLease? lease = inspection.HandoffLease;
            OpenVinoConversionOffer? conversionOffer = inspection.ConversionOffer;
            try
            {
                if (inspection.Outcome is not (OpenVinoRouteInspectionOutcome.Ready or
                    OpenVinoRouteInspectionOutcome.ReadyWithWarnings))
                {
                    ApplyOpenVinoConversionFailure(
                        OpenVinoActivationOutcomePolicy.GetConversionFailureCode(inspection));
                    return;
                }
                if (lease is null)
                {
                    ApplyOpenVinoConversionFailure(
                        OpenVinoSupportCode.RuntimeProtocolFailed);
                    return;
                }
                _openVinoDirectoryPath = converted.PublishedDirectory;
                PrepareOpenVinoHardwareHandoff(inspection);
                _openVinoConversionSourceRequest = null;
                ApplyOpenVinoReadyPresentation(inspection);
            }
            finally
            {
                lease?.Dispose();
                conversionOffer?.Dispose();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (IsCurrentOpenVinoLifetime(lifetime))
                ApplyOpenVinoConversionFailure(OpenVinoSupportCode.OperationCancelled);
        }
        finally
        {
            offer.Dispose();
        }
    }

    private void ApplyOpenVinoConvertingPresentation()
    {
        ApplyFooterStatus(InspectionFooterStatus.InProgress);
        InspectionOutcomeCardControl.Presentation = InspectionOutcomePresentation.Hidden;
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Progress,
            SectionTitle = "Preparing OpenVINO package",
            Startup = new InspectionStartupPresentation
            {
                Visibility = Visibility.Visible,
                Summary = "Running verified offline conversion",
                AutomationName = "Preparing OpenVINO package. Running verified offline conversion."
            }
        };
        _openVinoConversionRows = CreateOpenVinoConversionRows();
        UpdateOpenVinoConversionRows(OpenVinoConversionStage.Preflight);
        ShowOpenVinoSpecialProgress(
            "Preparing OpenVINO package",
            "Checking source and destination",
            "Step 1 of 6",
            _openVinoConversionRows);
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Inspecting,
            Message = "Conversion runs locally and leaves the source unchanged.",
            CancelAction = new InspectionActionPresentation
            {
                Text = "Cancel",
                AutomationName = "Cancel OpenVINO conversion",
                ActionId = "cancel-openvino-conversion",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ => _openVinoCancellation?.Cancel())
            }
        };
    }

    private void ApplyOpenVinoConversionProgress(OpenVinoConversionStage stage)
    {
        string summary = stage switch
        {
            OpenVinoConversionStage.Preflight => "Checking source and destination",
            OpenVinoConversionStage.Converting => "Converting model and tokenizer",
            OpenVinoConversionStage.ValidatingOutput => "Validating converted package",
            OpenVinoConversionStage.SmokeTesting => "Running official CPU smoke test",
            OpenVinoConversionStage.Publishing => "Publishing complete package",
            OpenVinoConversionStage.Reinspecting => "Reinspecting published package",
            _ => "Finishing conversion"
        };
        _openVinoConversionRows ??= CreateOpenVinoConversionRows();
        UpdateOpenVinoConversionRows(stage);
        int ordinal = GetConversionStageOrdinal(stage);
        ShowOpenVinoSpecialProgress(
            "Preparing OpenVINO package",
            summary,
            $"Step {ordinal} of 6",
            _openVinoConversionRows);
    }

    private void ApplyOpenVinoConversionFailure(OpenVinoSupportCode supportCode)
    {
        HideOpenVinoSpecialProgress();
        bool cancelled = supportCode == OpenVinoSupportCode.OperationCancelled;
        ApplyFooterStatus(cancelled
            ? InspectionFooterStatus.NotComplete
            : InspectionFooterStatus.Interrupted);
        bool hardware = supportCode is OpenVinoSupportCode.RuntimeLoadFailed or
            OpenVinoSupportCode.RuntimeDeviceUnavailable or
            OpenVinoSupportCode.RuntimeDeviceMismatch;
        string title = cancelled ? "Conversion cancelled" :
            hardware ? "Hardware check failed" : "Conversion failed";
        string recovery = cancelled
            ? "Inspect the source again to start a new conversion."
            : hardware
                ? "The source is suitable, but the official CPU smoke test could not complete. Retry or check the runtime installation."
                : "No package was published. Inspect the source again and retry.";
        InspectionOutcomeCardControl.Presentation = new InspectionOutcomePresentation
        {
            Kind = cancelled ? InspectionOutcomePresentationKind.Cancelled :
                InspectionOutcomePresentationKind.Invalid,
            Tone = cancelled ? InspectionOutcomeTone.Neutral : InspectionOutcomeTone.Error,
            GlyphKind = cancelled ? InspectionStatusGlyphKind.NotComplete :
                InspectionStatusGlyphKind.Error,
            Title = title,
            Message = recovery,
            AutomationName = title + ". " + recovery
        };
        ApplyDirectOutcomeTone(cancelled
            ? InspectionOutcomeTone.Neutral
            : InspectionOutcomeTone.Error);
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = cancelled ? InspectionContentCardMode.Cancelled :
                InspectionContentCardMode.Invalid,
            SectionTitle = title,
            SupportingText = recovery,
            SupportingTextVisibility = Visibility.Visible,
            DiagnosticCode = supportCode.ToProtocolValue(),
            DiagnosticCodeVisibility = Visibility.Visible,
            DiagnosticStatus = cancelled ? InspectionContentStatus.Neutral :
                InspectionContentStatus.Error
        };
        ApplyOpenVinoConversionRecoveryAction(title);
    }

    private void ApplyOpenVinoConversionRecoveryAction(string title)
    {
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            Title = title,
            Message = "Retry the verified conversion or choose another model.",
            SecondaryActionOne = new InspectionActionPresentation
            {
                Text = "Choose another model",
                AutomationName = "Choose another model",
                ActionId = "choose-another-model",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ =>
                    ChooseAnotherModelRequested?.Invoke(this, EventArgs.Empty))
            },
            PrimaryAction = new InspectionActionPresentation
            {
                Text = "Retry conversion",
                AutomationName = "Retry OpenVINO conversion",
                ActionId = "retry-openvino-conversion",
                Visibility = _openVinoConversionSourceRequest is null
                    ? Visibility.Collapsed
                    : Visibility.Visible,
                Command = new DelegateCommand(_ => RetryOpenVinoConversion())
            }
        };
    }

    private async void RetryOpenVinoConversion()
    {
        OpenVinoInspectionRequestedEventArgs? request = _openVinoConversionSourceRequest;
        string? directoryPath = _openVinoDirectoryPath;
        if (request is null || string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }
        await RetireOpenVinoLifetime();
        ActivateOpenVinoInspection(request, directoryPath);
    }

    private void ApplyOpenVinoCancelledPresentation()
    {
        ApplyFooterStatus(InspectionFooterStatus.NotComplete);
        InspectionOutcomeCardControl.Presentation = new InspectionOutcomePresentation
        {
            Kind = InspectionOutcomePresentationKind.Cancelled,
            Tone = InspectionOutcomeTone.Neutral,
            GlyphKind = InspectionStatusGlyphKind.NotComplete,
            Title = "Inspection cancelled",
            Message = "The local OpenVINO operation was cancelled.",
            AutomationName = "OpenVINO inspection cancelled"
        };
        ApplyDirectOutcomeTone(InspectionOutcomeTone.Neutral);
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Cancelled,
            SectionTitle = "Inspection cancelled"
        };
        if (_openVinoProgressRows is { } retainedRows)
        {
            ShowOpenVinoSpecialProgress(
                "Inspection cancelled",
                "The local OpenVINO operation was cancelled.",
                retainedRows.ProgressSummary,
                retainedRows.Items);
        }
        ApplyOpenVinoInspectionRecoveryAction("Inspection cancelled");
        SetPromptSurfaceVisible(false);
        QueueOpenVinoCancelledOutcomeAccessibility(_openVinoLifetime);
    }

    private void QueueOpenVinoCancelledOutcomeAccessibility(long lifetime)
    {
        if (!IsCurrentOpenVinoLifetime(lifetime) ||
            _openVinoCancelledOutcomeAccessibilityLifetime == lifetime)
        {
            return;
        }

        _openVinoCancelledOutcomeAccessibilityLifetime = lifetime;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (!IsCurrentOpenVinoLifetime(lifetime) ||
                _openVinoCancelledOutcomeAccessibilityLifetime != lifetime)
            {
                return;
            }

            UpdateLayout();
            if (!IsCurrentOpenVinoLifetime(lifetime) ||
                _openVinoCancelledOutcomeAccessibilityLifetime != lifetime)
            {
                return;
            }

            InspectionOutcomeCardControl.FocusOutcome();
            InspectionOutcomeCardControl.AnnounceOutcome(
                "OpenVINO inspection cancelled");
        });
    }

    private void ApplyChooseAnotherAction(string title)
    {
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            Title = title,
            Message = "Select a different model package to continue.",
            PrimaryAction = new InspectionActionPresentation
            {
                Text = "Choose another model",
                AutomationName = "Choose another model",
                ActionId = "choose-another-model",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ =>
                    ChooseAnotherModelRequested?.Invoke(this, EventArgs.Empty))
            }
        };
    }

    private void ApplyOpenVinoInspectionRecoveryAction(string title)
    {
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            Title = title,
            Message = "Retry this inspection, or choose another model.",
            SecondaryActionOne = new InspectionActionPresentation
            {
                Text = "Choose another model",
                AutomationName = "Choose another model",
                ActionId = "choose-another-model",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ =>
                    ChooseAnotherModelRequested?.Invoke(this, EventArgs.Empty))
            },
            PrimaryAction = new InspectionActionPresentation
            {
                Text = "Retry inspection",
                AutomationName = "Retry OpenVINO inspection",
                ActionId = "retry-openvino-inspection",
                Visibility = _openVinoRetryRequest is null ||
                    string.IsNullOrWhiteSpace(_openVinoDirectoryPath)
                        ? Visibility.Collapsed
                        : Visibility.Visible,
                Command = new DelegateCommand(_ => RetryOpenVinoInspection())
            }
        };
    }

    private async void RetryOpenVinoInspection()
    {
        OpenVinoInspectionRequestedEventArgs? request = _openVinoRetryRequest;
        string? directoryPath = _openVinoDirectoryPath;
        if (request is null || string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        await RetireOpenVinoLifetime();
        ActivateOpenVinoInspection(request, directoryPath);
    }

    private void ApplyOpenVinoFailurePresentation(
        string supportCode,
        string message,
        string recovery)
    {
        PromptFailure failure = new(supportCode, message, recovery);
        _promptPresenter ??= new PromptSessionPresenter(
            new PromptRoutePresentation(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty));
        _promptPresenter.ApplyFailure(failure);
        SetPromptSurfaceVisible(true);
        ApplyPromptSurfaceState(_promptPresenter.State);
        PromptInput.Focus(FocusState.Programmatic);
    }

    private void ApplyOpenVinoInspectionFailurePresentation(
        string supportCode,
        string message,
        string recovery)
    {
        HideOpenVinoSpecialProgress();
        ApplyFooterStatus(InspectionFooterStatus.Interrupted);
        InspectionOutcomeCardControl.Presentation = new InspectionOutcomePresentation
        {
            Kind = InspectionOutcomePresentationKind.OperationalFailure,
            Tone = InspectionOutcomeTone.Error,
            GlyphKind = InspectionStatusGlyphKind.Error,
            Title = "Model inspection could not start",
            Message = message,
            AutomationName = $"OpenVINO inspection failed. {message}"
        };
        ApplyDirectOutcomeTone(InspectionOutcomeTone.Error);
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.OperationalFailure,
            SectionTitle = "OpenVINO inspection unavailable",
            SupportingText = recovery,
            SupportingTextVisibility = Visibility.Visible,
            DiagnosticCode = supportCode,
            DiagnosticCodeVisibility = Visibility.Visible,
            DiagnosticStatus = InspectionContentStatus.Error
        };
        ApplyOpenVinoInspectionRecoveryAction(
            "Model inspection could not start");
        SetPromptSurfaceVisible(false);
        SetPromptControlsEnabled(send: false, stop: false, cancel: false);
        PromptCapabilitySummary.Text = string.Empty;
        PromptExecutionEvidenceText.Text = string.Empty;
        PromptBuildEvidenceText.Text = string.Empty;
        PromptResponseText.Text = string.Empty;
    }

    private static string GetInspectionStageTitle(ModelInspectionStage? stage) =>
        stage switch
        {
            ModelInspectionStage.CheckModelPackage => "Check model package",
            ModelInspectionStage.ReadModelConfiguration => "Read model configuration",
            ModelInspectionStage.ValidateTokenizerAndChatSetup =>
                "Validate tokenizer and chat setup",
            ModelInspectionStage.ValidateModelStructure => "Validate model structure",
            ModelInspectionStage.ConfirmCoreRuntimeCompatibility =>
                "Confirm core runtime compatibility",
            _ => "Model inspection"
        };

    private void ApplyDirectOutcomeTone(InspectionOutcomeTone tone)
    {
        // Tone-specific surfaces are selected by the exact route projection.
    }

    internal string GetCompactInspectionSummary(string summary) =>
        string.Equals(
            summary,
            "All 5 inspection checks passed",
            StringComparison.Ordinal)
                ? "5 checks passed"
                : summary;

    private void ShowOpenVinoSpecialProgress(
        string title,
        string summary,
        string count,
        IReadOnlyList<InspectionContentItemPresentation> rows)
    {
        ActivePreview.ShowOpenVinoSpecialProgress(title, summary, count, rows);
    }

    private void HideOpenVinoSpecialProgress()
    {
        ActivePreview.HideOpenVinoSpecialProgress();
    }

    private static IReadOnlyList<InspectionContentItemPresentation>
        CreateOpenVinoConversionRows()
    {
        string[] titles =
        [
            "Preflight",
            "Converting",
            "Validating output",
            "Smoke testing",
            "Publishing",
            "Reinspecting"
        ];
        var rows = new List<InspectionContentItemPresentation>(titles.Length);
        for (int index = 0; index < titles.Length; index++)
        {
            rows.Add(new InspectionContentItemPresentation
            {
                StageNumber = (index + 1).ToString(),
                Title = titles[index],
                DefaultDetail = titles[index] + ".",
                Status = InspectionContentStatus.Waiting,
                StatusText = "Waiting",
                IsActive = false,
                ShowConnector = index < titles.Length - 1,
                AutomationName =
                    $"{titles[index]}. Waiting. Step {index + 1} of 6."
            });
        }
        return rows;
    }

    private void UpdateOpenVinoConversionRows(OpenVinoConversionStage stage)
    {
        if (_openVinoConversionRows is not { } rows)
        {
            return;
        }

        int activeIndex = GetConversionStageOrdinal(stage) - 1;
        for (int index = 0; index < rows.Count; index++)
        {
            InspectionContentItemPresentation row = rows[index];
            InspectionContentStatus status = index < activeIndex
                ? InspectionContentStatus.Passed
                : index == activeIndex
                    ? InspectionContentStatus.Active
                    : InspectionContentStatus.Waiting;
            string statusText = status switch
            {
                InspectionContentStatus.Passed => "Passed",
                InspectionContentStatus.Active => "Checking",
                _ => "Waiting"
            };
            row.Status = status;
            row.StatusText = statusText;
            row.IsActive = status == InspectionContentStatus.Active;
            row.Detail = string.Empty;
            row.DetailVisibility = Visibility.Collapsed;
            row.AutomationName =
                $"{row.Title}. {statusText}. Step {index + 1} of 6.";
        }
    }

    private static int GetConversionStageOrdinal(OpenVinoConversionStage stage) =>
        stage switch
        {
            OpenVinoConversionStage.Preflight => 1,
            OpenVinoConversionStage.Converting => 2,
            OpenVinoConversionStage.ValidatingOutput => 3,
            OpenVinoConversionStage.SmokeTesting => 4,
            OpenVinoConversionStage.Publishing => 5,
            OpenVinoConversionStage.Reinspecting => 6,
            OpenVinoConversionStage.Completed => 6,
            _ => 1
        };

    private void OpenVinoSpecialProgressItems_ElementPrepared(
        ItemsRepeater sender,
        ItemsRepeaterElementPreparedEventArgs eventArguments)
    {
        // Static literal conversion rows are projected by the route adapter.
    }

    private void ApplyPromptEvent(long lifetime, PromptEvent promptEvent, long? chatGeneration = null)
    {
        void Apply()
        {
            if (!IsCurrentOpenVinoLifetime(lifetime) ||
                (chatGeneration.HasValue && chatGeneration.Value != _openVinoChatGeneration) ||
                _promptPresenter is null)
            {
                return;
            }

            _promptPresenter.Apply(promptEvent);
            ApplyPromptSurfaceState(_promptPresenter.State);
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            Apply();
        }
        else
        {
            DispatcherQueue.TryEnqueue(Apply);
        }
    }

    private static bool IsPromptSendKey(VirtualKey key, bool isShiftPressed) =>
        key == VirtualKey.Enter && !isShiftPressed;

    private async void PromptInput_PreviewKeyDown(
        object sender,
        KeyRoutedEventArgs eventArguments)
    {
        bool isShiftPressed = InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);
        if (!IsPromptSendKey(eventArguments.Key, isShiftPressed))
        {
            return;
        }

        eventArguments.Handled = true;
        Task? promptTask = StartOpenVinoPrompt();
        if (promptTask is not null)
        {
            await promptTask;
        }
    }

    private async void PromptSendButton_Click(object sender, RoutedEventArgs e)
    {
        Task? promptTask = StartOpenVinoPrompt();
        if (promptTask is not null)
        {
            await promptTask;
        }
    }

    private Task? StartOpenVinoPrompt()
    {
        IPromptRouteSession? session = _promptSession;
        string prompt = PromptInput.Text;
        if (session is null || string.IsNullOrWhiteSpace(prompt)
            || CurrentOpenVinoPromptTask is { IsCompleted: false }
            || _promptPresenter?.State.SendEnabled != true)
        {
            PromptInput.Focus(FocusState.Programmatic);
            return null;
        }

        LastOpenVinoTurnResult = null;
        _openVinoChatController ??= new OpenVinoChatController();
        _openVinoChatController.BeginTurn(prompt);
        OpenVinoChat.SynchronizeTranscript(_openVinoChatController.ConversationId, _openVinoChatController.Messages, true);
        PromptInput.Text = string.Empty;
        OpenVinoComposer.ApplyExternalRouteState(false, false, true);
        CurrentOpenVinoPromptTask = GenerateOpenVinoPromptAsync(
            session,
            prompt,
            _openVinoCancellation?.Token ?? CancellationToken.None);
        return CurrentOpenVinoPromptTask;
    }

    private async Task GenerateOpenVinoPromptAsync(
        IPromptRouteSession session,
        string prompt,
        CancellationToken cancellationToken)
    {
        long chatGeneration = _openVinoChatGeneration;
        try
        {
            PromptTurnResult result = await session.GenerateAsync(
                prompt,
                RequestedOpenVinoNewTokens,
                cancellationToken);
            if (chatGeneration != _openVinoChatGeneration || !ReferenceEquals(session, _promptSession)) return;
            LastOpenVinoTurnResult = result;
            _openVinoChatController?.Complete(result);
            if (_openVinoChatController is { } bridge)
                OpenVinoChat.SynchronizeTranscript(bridge.ConversationId, bridge.Messages, false);
            if (LastOpenVinoTurnResult.Status == PromptTurnStatus.Failed)
            {
                Interlocked.CompareExchange(
                    ref _promptSession,
                    null,
                    session);
                await session.DisposeAsync();
            }
        }
        catch (OperationCanceledException)
        {
            if (chatGeneration == _openVinoChatGeneration) PromptInput.Focus(FocusState.Programmatic);
        }
        catch (Exception)
        {
            if (chatGeneration != _openVinoChatGeneration) return;
            ApplyOpenVinoFailurePresentation(
                "runtime_protocol_failed",
                "The local prompt could not continue.",
                "Close the session and inspect the model again.");
        }
    }

    private async void PromptStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_promptSession is not null)
        {
            CurrentOpenVinoStopTask = StopOpenVinoPromptAsync(
                _promptSession);
            await CurrentOpenVinoStopTask;
        }
    }

    private async Task StopOpenVinoPromptAsync(IPromptRouteSession session)
    {
        long generation = _openVinoChatGeneration;
        try
        {
            Guid confirmedTurnId = _promptPresenter?.State.ActiveTurnId ??
                throw new InvalidOperationException(
                    "No worker-confirmed generation turn is active.");
            await session.StopActiveTurnAsync(
                confirmedTurnId,
                CancellationToken.None);
        }
        catch (Exception)
        {
            if (generation != _openVinoChatGeneration || !ReferenceEquals(session, _promptSession)) return;
            ApplyOpenVinoFailurePresentation(
                "runtime_protocol_failed",
                "The local prompt could not be stopped safely.",
                "Close the session and inspect the model again.");
        }
    }

    private async void PromptCancelButton_Click(object sender, RoutedEventArgs e)
    {
        long generation = _openVinoChatGeneration;
        if (_promptSession is not null)
        {
            CurrentOpenVinoCancelTask = CancelOpenVinoSessionAsync(
                _promptSession);
            await CurrentOpenVinoCancelTask;
        }
        if (generation == _openVinoChatGeneration && _openVinoCancellation is not null)
            PromptInput.Focus(FocusState.Programmatic);
    }

    private async Task CancelOpenVinoSessionAsync(IPromptRouteSession session)
    {
        long generation = _openVinoChatGeneration;
        try
        {
            Guid? activeTurnId = _promptPresenter?.State.ActiveTurnId;
            if (activeTurnId is Guid confirmedTurnId)
            {
                await session.CancelActiveTurnAsync(
                    confirmedTurnId,
                    CancellationToken.None);
            }
            else
            {
                await session.CancelAsync(CancellationToken.None);
            }
        }
        catch (Exception)
        {
            if (generation != _openVinoChatGeneration || !ReferenceEquals(session, _promptSession)) return;
            ApplyOpenVinoFailurePresentation(
                "runtime_protocol_failed",
                "The local session could not be cancelled safely.",
                "Close this page and inspect the model again.");
        }
    }

    private async Task RestartOpenVinoConversationAsync()
    {
        string? directory = _activeChatPackageDirectory;
        OpenVinoRuntimeOptions? options = _activeChatRuntimeOptions;
        if (directory is null || options is null || _openVinoCancellation is null) return;
        long lifetime = _openVinoLifetime;
        Task? previousStop = CurrentOpenVinoStopTask;
        Task? previousCancel = CurrentOpenVinoCancelTask;
        checked { _openVinoChatGeneration++; }
        _openVinoChatController?.Retire();
        IPromptRouteSession? previous = Interlocked.Exchange(ref _promptSession, null);
        Guid? activeTurn = _promptPresenter?.State.ActiveTurnId;
        bool alreadyClosed = _promptPresenter?.State.LastEventKind is PromptEventKind.Cancelled or PromptEventKind.SessionCompleted;
        _promptPresenter = null;
        OpenVinoChat.SetNewConversationEnabled(false);
        OpenVinoComposer.ApplyExternalRouteState(false, false, false);
        PromptCancelButton.IsEnabled = false;
        try
        {
            if (previous is not null)
            {
                try
                {
                    if (previousStop is not null) await previousStop;
                    if (previousCancel is not null) await previousCancel;
                    if (previousCancel is null && !alreadyClosed)
                    {
                        if (activeTurn is Guid turn && CurrentOpenVinoPromptTask is { IsCompleted: false })
                            await previous.CancelActiveTurnAsync(turn, CancellationToken.None);
                        else await previous.CancelAsync(CancellationToken.None);
                    }
                    if (CurrentOpenVinoPromptTask is { } promptTask) await promptTask;
                }
                finally { await previous.DisposeAsync(); }
            }
            if (!IsCurrentOpenVinoLifetime(lifetime)) return;
            OpenVinoChatActivationResult result = await ActivateOpenVinoChatFromDirectoryWithResultAsync(
                directory, options, _openVinoCancellation!.Token);
            if (!result.IsActivated && IsCurrentOpenVinoLifetime(lifetime))
                ApplyOpenVinoFailurePresentation("runtime_load_failed", "The new conversation could not start.", "Import the model again to retry.");
        }
        catch (Exception)
        {
            if (IsCurrentOpenVinoLifetime(lifetime))
                ApplyOpenVinoFailurePresentation("runtime_protocol_failed", "The previous session could not close safely.", "Import the model again to retry.");
        }
        finally
        {
            if (IsCurrentOpenVinoLifetime(lifetime))
            {
                OpenVinoChat.SetNewConversationEnabled(true);
            }
        }
    }

    private void SetPromptSurfaceVisible(bool visible)
    {
        EnsureOpenVinoChat();
        Visibility visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        PromptSurface.Visibility = visibility;
        InspectionScrollViewer.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        ActionToPromptGap.Visibility = Visibility.Collapsed;
    }

    private void SetPromptControlsEnabled(bool send, bool stop, bool cancel)
    {
        PromptSendButton.IsEnabled = send;
        PromptStopButton.IsEnabled = stop;
        PromptCancelButton.IsEnabled = cancel;
    }

    private void ApplyPromptSurfaceState(PromptSurfaceState state)
    {
        EnsureOpenVinoChat();
        PromptCapabilitySummary.Text = state.CapabilitySummary;
        PromptExecutionEvidenceText.Text = state.ExecutionEvidence;
        PromptBuildEvidenceText.Text = state.BuildEvidence;
        PromptResponseText.Text = state.ResponseText;
        _openVinoChatController?.Apply(state);
        if (_openVinoChatController is { } bridge)
            OpenVinoChat.SynchronizeTranscript(bridge.ConversationId, bridge.Messages, false);
        OpenVinoChat.ApplyOpenVinoChatState(state);
        SetPromptControlsEnabled(
            state.SendEnabled,
            state.StopEnabled,
            state.CancelEnabled);
        if (state.SendEnabled ||
            (!state.SendEnabled && !state.StopEnabled && !state.CancelEnabled))
        {
            PromptInput.Focus(FocusState.Programmatic);
        }
        if (!string.IsNullOrWhiteSpace(state.Announcement))
        {
            AnnouncePromptStatus(state.Announcement);
        }
    }

    private void AnnouncePromptStatus(string text)
    {
        if (string.Equals(_lastOpenVinoAnnouncement, text, StringComparison.Ordinal)) return;
        _lastOpenVinoAnnouncement = text;
        AutomationProperties.SetName(PromptResponseText, text);
        AutomationPeer peer = FrameworkElementAutomationPeer.FromElement(
            PromptResponseText) ??
            FrameworkElementAutomationPeer.CreatePeerForElement(PromptResponseText);
        peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private bool IsCurrentOpenVinoLifetime(long lifetime) =>
        lifetime == _openVinoLifetime && _openVinoCancellation is not null;

    private Task RetireOpenVinoLifetime()
    {
        lock (_openVinoRetirementLock)
        {
            if (_openVinoCancellation is null &&
                _promptSession is null &&
                OpenVinoRequest is null &&
                _openVinoMotionSettings is null)
            {
                return CurrentOpenVinoCleanupTask ?? Task.CompletedTask;
            }

            CancellationTokenSource? cancellation =
                Interlocked.Exchange(ref _openVinoCancellation, null);
            IPromptRouteSession? session =
                Interlocked.Exchange(ref _promptSession, null);
            checked { _openVinoChatGeneration++; }
            _openVinoChatController?.Retire();
            _activeChatPackageDirectory = null;
            _activeChatRuntimeOptions = null;
            _promptPresenter = null;
            Task? inspectionTask = CurrentOpenVinoInspectionTask;
            Task? conversionTask = CurrentOpenVinoConversionTask;
            Task? promptTask = CurrentOpenVinoPromptTask;
            Task? stopTask = CurrentOpenVinoStopTask;
            Task? cancelTask = CurrentOpenVinoCancelTask;
            Task? newConversationTask = _newOpenVinoConversationTask;
            OpenVinoRequest = null;
            _openVinoConversionSourceRequest = null;
            _openVinoRetryRequest = null;
            _openVinoDirectoryPath = null;
            _openVinoHardwareHandoff = null;
            _openVinoConfiguration = null;
            _openVinoTerminalPackageEvidence = null;
            _openVinoProgressRows = null;
            _openVinoProgressRevision = 0;
            _openVinoPendingSuccessLifetime = -1;
            _openVinoCancelledOutcomeAccessibilityLifetime = -1;
            IModelInspectionMotionSettings? motionSettings =
                Interlocked.Exchange(ref _openVinoMotionSettings, null);
            EventHandler? motionSettingsChangedHandler =
                Interlocked.Exchange(
                    ref _openVinoMotionSettingsChangedHandler,
                    null);
            checked
            {
                _openVinoMotionGeneration++;
            }
            if (motionSettings is not null)
            {
                if (motionSettingsChangedHandler is not null)
                {
                    try
                    {
                        motionSettings.AnimationsEnabledChanged -=
                            motionSettingsChangedHandler;
                    }
                    catch
                    {
                        // motion-provider teardown is best effort and must not
                        // block cancellation or worker/session cleanup.
                    }
                }
                try
                {
                    motionSettings.Dispose();
                }
                catch
                {
                    // Operational retirement below remains authoritative.
                }
            }
            StopOpenVinoProgressPresentation(cancelAwaiter: true);
            try
            {
                _activePreview.CancelProgressMotion();
            }
            catch
            {
                // Visual teardown cannot block cancellation or worker cleanup.
            }
            Interlocked.Exchange(ref _conversionOffer, null)?.Dispose();
            checked
            {
                _openVinoLifetime++;
            }
            cancellation?.Cancel();
            CurrentOpenVinoCleanupTask = CleanupOpenVinoAsync(
                session,
                cancellation,
                inspectionTask,
                conversionTask,
                promptTask,
                stopTask,
                cancelTask,
                newConversationTask);
            return CurrentOpenVinoCleanupTask;
        }
    }

    private static async Task CleanupOpenVinoAsync(
        IPromptRouteSession? session,
        CancellationTokenSource? cancellation,
        params Task?[] activeTasks)
    {
        try
        {
            foreach (Task activeTask in activeTasks
                         .Where(static task => task is not null)
                         .Cast<Task>()
                         .Distinct())
            {
                try
                {
                    await activeTask.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Retired UI ignores task presentation failures; owned
                    // channel cleanup below remains authoritative
                }
            }

            if (session is not null)
            {
                try
                {
                    await session.CancelAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // disposal still owns the terminal resource release
                }
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    internal Task<bool> ActivateOpenVinoInspectionWhenOwnedAsync(
        OpenVinoInspectionRequestedEventArgs request,
        string directoryPath,
        Frame owner)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(owner);

        CancelPendingOpenVinoActivation();
        if (HasLoadedOpenVinoFrameOwnership(owner))
        {
            _openVinoOwnerFrame = owner;
            ActivateOpenVinoInspectionCore(request, directoryPath);
            return Task.FromResult(true);
        }

        long generation = checked(++_openVinoActivationGeneration);
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler loadedHandler = (_, _) =>
            TryCompletePendingOpenVinoActivation(generation, request, directoryPath);
        RoutedEventHandler unloadedHandler = (_, _) =>
            CompletePendingOpenVinoActivation(generation, succeeded: false);
        EventHandler<object> layoutUpdatedHandler = (_, _) =>
            TryRejectPendingOpenVinoActivationIfOwnershipWasLost(generation);

        _pendingOpenVinoActivationOwner = owner;
        _pendingOpenVinoLoadedHandler = loadedHandler;
        _pendingOpenVinoUnloadedHandler = unloadedHandler;
        _pendingOpenVinoLayoutUpdatedHandler = layoutUpdatedHandler;
        _pendingOpenVinoActivationCompletion = completion;
        Loaded += loadedHandler;
        Unloaded += unloadedHandler;
        owner.LayoutUpdated += layoutUpdatedHandler;

        if (!ReferenceEquals(owner.Content, this))
        {
            CompletePendingOpenVinoActivation(generation, succeeded: false);
        }

        return completion.Task;
    }

    internal void ActivateOpenVinoInspection(
        OpenVinoInspectionRequestedEventArgs request,
        string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        CancelPendingOpenVinoActivation();
        Frame? owner = _openVinoOwnerFrame ?? Frame;
        if (owner is null ||
            !HasLoadedOpenVinoFrameOwnership(owner))
        {
            throw new InvalidOperationException(
                "OpenVINO inspection requires a loaded Frame-owned page.");
        }

        ActivateOpenVinoInspectionCore(request, directoryPath);
    }

    private void ActivateOpenVinoInspectionCore(
        OpenVinoInspectionRequestedEventArgs request,
        string directoryPath)
    {
        SelectOpenVinoPreview();
        RetireOpenVinoLifetime();
        OpenVinoRequest = request;
        _openVinoDirectoryPath = directoryPath;
        StartOpenVinoMotionSettingsLifetime();
        BeginOpenVinoInspection(request, directoryPath);
    }

    private bool HasLoadedOpenVinoFrameOwnership(Frame owner) =>
        IsLoaded &&
        XamlRoot is not null &&
        ReferenceEquals(owner.Content, this);

    private void TryCompletePendingOpenVinoActivation(
        long generation,
        OpenVinoInspectionRequestedEventArgs request,
        string directoryPath)
    {
        Frame? owner = _pendingOpenVinoActivationOwner;
        if (generation != Volatile.Read(ref _openVinoActivationGeneration) ||
            owner is null ||
            !HasLoadedOpenVinoFrameOwnership(owner))
        {
            TryRejectPendingOpenVinoActivationIfOwnershipWasLost(generation);
            return;
        }

        TaskCompletionSource<bool>? completion =
            DetachPendingOpenVinoActivation(generation);
        if (completion is null)
        {
            return;
        }

        try
        {
            _openVinoOwnerFrame = owner;
            ActivateOpenVinoInspectionCore(request, directoryPath);
            completion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private void TryRejectPendingOpenVinoActivationIfOwnershipWasLost(
        long generation)
    {
        Frame? owner = _pendingOpenVinoActivationOwner;
        if (generation == Volatile.Read(ref _openVinoActivationGeneration) &&
            owner is not null &&
            !ReferenceEquals(owner.Content, this))
        {
            CompletePendingOpenVinoActivation(generation, succeeded: false);
        }
    }

    private void CompletePendingOpenVinoActivation(
        long generation,
        bool succeeded)
    {
        DetachPendingOpenVinoActivation(generation)?.TrySetResult(succeeded);
    }

    private TaskCompletionSource<bool>? DetachPendingOpenVinoActivation(
        long generation)
    {
        if (generation != Volatile.Read(ref _openVinoActivationGeneration))
        {
            return null;
        }

        Frame? owner = _pendingOpenVinoActivationOwner;
        RoutedEventHandler? loadedHandler = _pendingOpenVinoLoadedHandler;
        RoutedEventHandler? unloadedHandler = _pendingOpenVinoUnloadedHandler;
        EventHandler<object>? layoutUpdatedHandler =
            _pendingOpenVinoLayoutUpdatedHandler;
        TaskCompletionSource<bool>? completion =
            _pendingOpenVinoActivationCompletion;

        _pendingOpenVinoActivationOwner = null;
        _pendingOpenVinoLoadedHandler = null;
        _pendingOpenVinoUnloadedHandler = null;
        _pendingOpenVinoLayoutUpdatedHandler = null;
        _pendingOpenVinoActivationCompletion = null;
        if (loadedHandler is not null)
        {
            Loaded -= loadedHandler;
        }
        if (unloadedHandler is not null)
        {
            Unloaded -= unloadedHandler;
        }
        if (owner is not null && layoutUpdatedHandler is not null)
        {
            owner.LayoutUpdated -= layoutUpdatedHandler;
        }
        return completion;
    }

    private void CancelPendingOpenVinoActivation()
    {
        long generation = Volatile.Read(ref _openVinoActivationGeneration);
        TaskCompletionSource<bool>? completion =
            DetachPendingOpenVinoActivation(generation);
        checked
        {
            _openVinoActivationGeneration++;
        }
        completion?.TrySetResult(false);
    }

    private void StartOpenVinoMotionSettingsLifetime()
    {
        IModelInspectionMotionSettings settings =
            _motionSettingsFactory() ?? throw new InvalidOperationException(
                "The Model Inspection motion-settings factory returned null.");
        long generation = checked(++_openVinoMotionGeneration);
        EventHandler changedHandler = (sender, _) =>
        {
            if (!ReferenceEquals(sender, settings) ||
                generation != Volatile.Read(ref _openVinoMotionGeneration) ||
                !ReferenceEquals(settings, _openVinoMotionSettings))
            {
                return;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (generation != Volatile.Read(ref _openVinoMotionGeneration) ||
                    !ReferenceEquals(settings, _openVinoMotionSettings))
                {
                    return;
                }

                _activePreview.SetMotionEnabled(settings.AnimationsEnabled);
            });
        };

        try
        {
            _openVinoMotionSettings = settings;
            _openVinoMotionSettingsChangedHandler = changedHandler;
            settings.AnimationsEnabledChanged += changedHandler;
            _activePreview.SetMotionEnabled(settings.AnimationsEnabled);
        }
        catch
        {
            settings.AnimationsEnabledChanged -= changedHandler;
            Interlocked.CompareExchange(
                ref _openVinoMotionSettings,
                null,
                settings);
            Interlocked.CompareExchange(
                ref _openVinoMotionSettingsChangedHandler,
                null,
                changedHandler);
            checked
            {
                _openVinoMotionGeneration++;
            }
            settings.Dispose();
            throw;
        }
    }

    internal async Task RetireOpenVinoInspectionAsync()
    {
        CancelPendingOpenVinoActivation();
        await RetireOpenVinoLifetime();
        _openVinoOwnerFrame = null;
    }

    internal Task RetireForNavigationAsync()
    {
        lock (_navigationRetirementLock)
        {
            return _navigationRetirementTask ??=
                RetireForNavigationCoreAsync();
        }
    }

    private async Task RetireForNavigationCoreAsync()
    {
        if (NavigationRetirementOverride is not null)
        {
            await NavigationRetirementOverride();
        }
        else
        {
            await RetireOpenVinoInspectionAsync();
        }
        RetirePageLifetime();
    }
}
