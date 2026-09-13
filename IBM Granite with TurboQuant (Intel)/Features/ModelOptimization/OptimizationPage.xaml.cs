using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.Presentation.Progress;
using OptimizationRoute = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.OptimizationRoute;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;

namespace GraniteEdgeAI.Features.ModelOptimization;

public sealed partial class OptimizationPage : Page
{
    private enum OptimizationExportPresentationKind
    {
        None,
        PersistentModel,
        GgufRuntimeBundle,
    }

    private OptimizationExportController? _exportController;
    private Task _detachedExportOperations = Task.CompletedTask;
    private Task _detachedExportCleanup = Task.CompletedTask;
    private Task _observedExportOperation = Task.CompletedTask;
    private long _exportBindingGeneration;
    private readonly UISettings _uiSettings = new();
    private readonly OptimizationProgressPacer _progressPacer = new();
    private readonly Stopwatch _progressClock = Stopwatch.StartNew();
    private DispatcherQueueTimer? _progressTimer;
    private long _progressTimerEpoch;
    private OptimizationPresentationState? _displayedProgress;
    private bool _focusFirstProgress;
    private OptimizationPresentationState? _presentation;
    private OptimizationJourneyEntryContext? _entryContext;
    private readonly object _navigationRetirementLock = new();
    private Task? _navigationRetirementTask;
    private int _isRetired;
    private bool _realRunAuthorized;
    private bool _sawAuthorizedRunningState;
    private int _lastAnnouncedRunningStage = -1;
    private OptimizationExportStateKind _lastExportKind = OptimizationExportStateKind.Unbound;
    private OptimizationExportPresentationKind _exportPresentationKind;
    private int _importNavigationPending;
    private readonly SerializedProgressSequence _operationSequence = new();
    private readonly BoundedProgressEstimator _exportEstimate = new();
    private DispatcherProgressPresenter? _estimatePresenter;
    private DispatcherProgressPresenter? _exportEstimatePresenter;
    private object _operationEstimateOwner = new();
    private object _exportEstimateOwner = new();
    private Guid _estimatePlan;
    private long _estimateRevision;
    private long _estimateEpoch;
    private int _estimateActiveIndex = -1;
    private OptimizationPresentationState? _estimatePresentation;
    private OptimizationPresentationState? _pendingSuccessfulTerminal;
    private OptimizationExportViewState? _estimatedExportState;

    public OptimizationPage()
    {
        InitializeComponent();
        BtnOptimizationBack.Click += (_, _) => RaiseIntent(OptimizationCommand.BackToCompatibility);
        BtnStartOptimization.Click += (_, _) => Confirm();
        BtnCancelOptimization.Click += (_, _) => RaiseIntent(OptimizationCommand.Cancel);
        BtnOptimizationTerminalBack.Click += TerminalAction_Click;
        BtnOptimizationAlternative.Click += TerminalAction_Click;
        BtnOptimizationPrimary.Click += TerminalAction_Click;
        BtnCancelExport.Click += CancelExport_Click;
        BtnRetryExport.Click += RetryExport_Click;
        OptimizationOverallPercentage.Text = "0%";
        AutomationProperties.SetItemStatus(
            OptimizationOverallProgress, "Estimated 0%");
        Loaded += ProgressPage_Loaded;
        Unloaded += ProgressPage_Unloaded;
        Unloaded += (_, _) => { _exportEstimatePresenter?.Stop(); _exportEstimate.Reset(); };
    }

    internal OptimizationPage(OptimizationJourneyEntryContext entryContext) : this()
    {
        _entryContext = entryContext ?? throw new ArgumentNullException(nameof(entryContext));
        ApplyPresentation(OptimizationPresentationFactory.Confirmation(
            entryContext.OptimizationHandoff.Plan.Preference,
            OptimizationConfigurationProjection.From(entryContext.OptimizationHandoff.Plan),
            entryContext.OptimizationHandoff.OptimizationPlanId,
            entryContext.OptimizationHandoff.ConfigurationSha256));
    }

    internal event EventHandler<OptimizationIntentEventArgs>? IntentRequested;
    internal OptimizationPresentationState? Presentation => _presentation;
    internal OptimizationExportViewState ExportState =>
        _exportController?.State ?? OptimizationExportViewState.Unbound();
    internal Task ObservedExportOperation => _observedExportOperation;
    internal Task DetachedExportOperations => _detachedExportOperations;
    internal Task DetachedExportCleanup => _detachedExportCleanup;
    internal Type? ObservedExportFaultType { get; private set; }

    internal bool BindVerifiedExport(
        VerifiedPersistentExportTarget target,
        IOptimizationExportService service,
        TimeSpan? retirementTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(service);
        if (Volatile.Read(ref _isRetired) != 0
            || !_detachedExportOperations.IsCompletedSuccessfully
            || !_detachedExportCleanup.IsCompletedSuccessfully
            || _exportController?.IsCleanupPending == true)
        {
            SetSaveEnabled(false);
            return false;
        }
        if (_exportController?.State.Kind is OptimizationExportStateKind.Running
                or OptimizationExportStateKind.Cancelling
            || _presentation is not { Kind: OptimizationPageStateKind.SucceededPersistent } presentation
            || presentation.OptimizationPlanId == Guid.Empty
            || !presentation.Configuration.ProducesPersistentArtifact
            || target.OptimizationPlanId != presentation.OptimizationPlanId
            || !string.Equals(target.ConfigurationSha256,
                presentation.ConfigurationSha256, StringComparison.Ordinal))
        {
            SetSaveEnabled(false);
            ResetExportBinding();
            return false;
        }
        ResetExportBinding();
        _exportPresentationKind =
            OptimizationExportPresentationKind.PersistentModel;
        long generation = _exportBindingGeneration;
        var controller = new OptimizationExportController(service, retirementTimeout);
        _exportController = controller;
        controller.StateChanged += ExportController_StateChanged;
        controller.Bind(target);
        OptimizationExportPanel.Visibility = Visibility.Visible;
        if (IsCurrentExportState(controller, controller.State, generation))
            ApplyExportState(controller, controller.State, generation);
        return true;
    }

    internal bool BindVerifiedRuntimeBundleExport(
        VerifiedGgufRuntimeBundleExportTarget target,
        IGgufRuntimeBundleExportService service,
        TimeSpan? retirementTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(service);
        if (Volatile.Read(ref _isRetired) != 0
            || !_detachedExportOperations.IsCompletedSuccessfully
            || !_detachedExportCleanup.IsCompletedSuccessfully
            || _exportController?.IsCleanupPending == true)
        {
            SetSaveEnabled(false);
            return false;
        }
        if (_exportController?.State.Kind is OptimizationExportStateKind.Running
                or OptimizationExportStateKind.Cancelling
            || _presentation is not
                { Kind: OptimizationPageStateKind.SucceededRuntimeProfile }
                    presentation
            || presentation.OptimizationPlanId == Guid.Empty
            || presentation.Configuration.ProducesPersistentArtifact
            || target.Kind != OptimizationExportTargetKind.GgufRuntimeBundle
            || target.Route != OptimizationRoute.Gguf
            || target.OptimizationPlanId != presentation.OptimizationPlanId
            || !string.Equals(target.ConfigurationSha256,
                presentation.ConfigurationSha256, StringComparison.Ordinal))
        {
            SetSaveEnabled(false);
            ResetExportBinding();
            return false;
        }
        ResetExportBinding();
        _exportPresentationKind =
            OptimizationExportPresentationKind.GgufRuntimeBundle;
        long generation = _exportBindingGeneration;
        var controller = new OptimizationExportController(
            service, retirementTimeout);
        _exportController = controller;
        controller.StateChanged += ExportController_StateChanged;
        controller.Bind(target);
        OptimizationExportPanel.Visibility = Visibility.Visible;
        if (IsCurrentExportState(controller, controller.State, generation))
            ApplyExportState(controller, controller.State, generation);
        return true;
    }

    internal void ApplyPresentation(OptimizationPresentationState presentation)
    {
        if (Volatile.Read(ref _isRetired) != 0) return;
        ResetExportBinding();
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        bool succeeded = presentation.Kind is OptimizationPageStateKind.SucceededPersistent
            or OptimizationPageStateKind.SucceededRuntimeProfile;
        if (succeeded && _estimatePresentation is not null)
        {
            BeginSuccessfulTerminalHandoff(presentation);
            return;
        }
        if (presentation.Kind != OptimizationPageStateKind.Running)
        {
            InvalidateProgressPlayback();
            StopProgressRings();
        }

        ApplyIdentity(presentation);
        ApplyConfiguration(presentation.Configuration);
        bool confirming = presentation.Kind == OptimizationPageStateKind.Confirming;
        bool running = presentation.Kind == OptimizationPageStateKind.Running;
        bool terminal = !confirming && !running;
        if (succeeded)
        {
            SetSaveEnabled(false);
        }

        OptimizationConfirmingPanel.Visibility = confirming ? Visibility.Visible : Visibility.Collapsed;
        OptimizationProgressPanel.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        OptimizationTerminalPanel.Visibility = terminal ? Visibility.Visible : Visibility.Collapsed;

        if (confirming)
        {
            OptimizationConfirmingHeading.Text = presentation.Title;
            OptimizationConfirmingSummary.Text = presentation.Summary;
            OptimizationTechnicalDetails.Text = TechnicalDetails(presentation);
        }
        else if (running)
        {
            bool firstAuthorizedRunningState = _realRunAuthorized && !_sawAuthorizedRunningState;
            if (_realRunAuthorized) _sawAuthorizedRunningState = true;
            _focusFirstProgress |= firstAuthorizedRunningState;
            // Actions always follow the latest semantic update, never a queued visual snapshot.
            if (OptimizationProgressPacer.ActiveIndex(presentation) >= 0)
                ApplyRunningActions(presentation);
            StopScheduledProgress();
            DisplayRunningProgress(presentation);
        }
        else
        {
            ApplyTerminal(presentation);
        }

        if (_sawAuthorizedRunningState && terminal)
        {
            _sawAuthorizedRunningState = false;
            _realRunAuthorized = false;
            _lastAnnouncedRunningStage = -1;
            FocusAndAnnounceTerminal(presentation);
        }
    }

    private void BeginSuccessfulTerminalHandoff(OptimizationPresentationState terminal)
    {
        _pendingSuccessfulTerminal = terminal;
        ApplyIdentity(terminal);
        ApplyConfiguration(terminal.Configuration);
        SetSaveEnabled(false);
        OptimizationConfirmingPanel.Visibility = Visibility.Collapsed;
        OptimizationProgressPanel.Visibility = Visibility.Visible;
        OptimizationTerminalPanel.Visibility = Visibility.Collapsed;
        BtnCancelOptimization.IsEnabled = false;
        BtnCancelOptimization.Visibility = Visibility.Collapsed;
        OptimizationCancellingCopy.Visibility = Visibility.Collapsed;

        OptimizationPresentationState running = _estimatePresentation!;
        SerializedProgressStageObservation[] observations = running.ProgressRows
            .Select(item => new SerializedProgressStageObservation(
                false,
                item.Status != OptimizationStageStatus.NotApplicable,
                item.Status == OptimizationStageStatus.NotApplicable))
            .ToArray();
        _operationSequence.Observe(
            _operationEstimateOwner, ++_estimateRevision, observations,
            _uiSettings.AnimationsEnabled);
        _estimateEpoch = _operationSequence.Epoch;
        UpdateOperationEstimate();
        _estimatePresenter ??= new DispatcherProgressPresenter(
            this, UpdateOperationEstimate, TimeSpan.FromMilliseconds(16));
        _estimatePresenter.Start();
    }

    private void CompleteSuccessfulTerminalHandoff()
    {
        if (_pendingSuccessfulTerminal is not { } terminal) return;
        _pendingSuccessfulTerminal = null;
        _estimatePresenter?.Stop();
        StopScheduledProgress();
        _progressPacer.Reset();
        _displayedProgress = null;
        _estimatePresentation = null;
        StopProgressRings();
        OptimizationProgressPanel.Visibility = Visibility.Collapsed;
        OptimizationTerminalPanel.Visibility = Visibility.Visible;
        ApplyTerminal(terminal);
        if (_sawAuthorizedRunningState)
        {
            _sawAuthorizedRunningState = false;
            _realRunAuthorized = false;
            _lastAnnouncedRunningStage = -1;
            FocusAndAnnounceTerminal(terminal);
        }
    }

    private void DisplayRunningProgress(OptimizationPresentationState presentation)
    {
        _displayedProgress = presentation;
        ApplyProgress(presentation);
    }

    private void ScheduleProgressPlayback()
    {
        StopScheduledProgress();
        if (!IsLoaded || Volatile.Read(ref _isRetired) != 0
            || _progressPacer.Remaining(_progressClock.Elapsed) is not TimeSpan remaining) return;
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.IsRepeating = false;
        timer.Interval = remaining;
        timer.Tick += ProgressTimer_Tick;
        _progressTimerEpoch = _progressPacer.Epoch;
        _progressTimer = timer;
        timer.Start();
    }

    private void ProgressTimer_Tick(DispatcherQueueTimer timer, object args)
    {
        if (!ReferenceEquals(timer, _progressTimer)) return;
        long epoch = _progressTimerEpoch;
        StopScheduledProgress();
        if (epoch != _progressPacer.Epoch || !IsLoaded || Volatile.Read(ref _isRetired) != 0
            || _presentation is not { Kind: OptimizationPageStateKind.Running } latest) return;
        OptimizationPresentationState? display = !_uiSettings.AnimationsEnabled || !latest.CanCancel
            ? _progressPacer.Offer(latest, _progressClock.Elapsed, false)
            : _progressPacer.Advance(_progressClock.Elapsed);
        if (display is not null) DisplayRunningProgress(display);
        ScheduleProgressPlayback();
    }

    private void StopScheduledProgress()
    {
        if (_progressTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= ProgressTimer_Tick;
        }
        _progressTimer = null;
    }

    private void InvalidateProgressPlayback()
    {
        StopScheduledProgress();
        _progressPacer.Reset();
        _estimatePresenter?.Stop();
        _operationSequence.Abort();
        _estimateEpoch = _operationSequence.Epoch;
        _displayedProgress = null;
        _pendingSuccessfulTerminal = null;
        _focusFirstProgress = false;
    }

    private void ProgressPage_Loaded(object sender, RoutedEventArgs args)
    {
        ApplyResponsiveLayout(ActualWidth);
        SetProgressMotionSubscription(false);
        if (Volatile.Read(ref _isRetired) != 0) return;
        SetProgressMotionSubscription(true);
        if (_presentation is { Kind: OptimizationPageStateKind.Running } latest)
        {
            StopScheduledProgress();
            DisplayRunningProgress(latest);
        }
    }

    private void ProgressPage_Unloaded(object sender, RoutedEventArgs args)
    {
        SetProgressMotionSubscription(false);
        if (_pendingSuccessfulTerminal is not null)
            CompleteSuccessfulTerminalHandoff();
        InvalidateProgressPlayback();
        StopProgressRings();
    }

    private void SetProgressMotionSubscription(bool subscribe)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)) return;
        _uiSettings.AnimationsEnabledChanged -= ProgressAnimationsEnabledChanged;
        if (subscribe) _uiSettings.AnimationsEnabledChanged += ProgressAnimationsEnabledChanged;
    }

    private void ProgressAnimationsEnabledChanged(
        UISettings sender, UISettingsAnimationsEnabledChangedEventArgs args)
    {
        long epoch = _progressPacer.Epoch;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (epoch != _progressPacer.Epoch || !IsLoaded
                || Volatile.Read(ref _isRetired) != 0 || _uiSettings.AnimationsEnabled
                || _presentation is not { Kind: OptimizationPageStateKind.Running } latest) return;
            DisplayRunningProgress(latest);
        });
    }

    private void ApplyIdentity(OptimizationPresentationState presentation)
    {
        OptimizationIdentityDetail.Text =
            $"{presentation.PreferenceLabel} · {presentation.Configuration.Backend} · {presentation.Configuration.Device}";
        var colors = Tone(presentation.Tone);
        OptimizationIdentityStatus.Background = colors.surface;
        OptimizationIdentityStatus.BorderBrush = colors.border;
        OptimizationIdentityStatusText.Foreground = colors.accent;
        OptimizationIdentityStatusText.Text = presentation.Kind switch
        {
            OptimizationPageStateKind.Confirming => "Ready to start",
            OptimizationPageStateKind.Running => "Optimising",
            OptimizationPageStateKind.SucceededPersistent or
            OptimizationPageStateKind.SucceededRuntimeProfile => "Ready",
            OptimizationPageStateKind.Cancelled => "Cancelled",
            _ => "Needs attention"
        };
        AutomationProperties.SetName(OptimizationIdentityStatus, OptimizationIdentityStatusText.Text);
    }

    private void ApplyConfiguration(OptimizationConfigurationPresentation c)
    {
        OptimizationWeights.Text = c.Weights;
        OptimizationCache.Text = c.Cache;
        OptimizationBackend.Text = c.Backend;
        OptimizationDevice.Text = c.Device;
        OptimizationContext.Text = c.Context;
        OptimizationOffload.Text = c.Offload;
        OptimizationFlashAttention.Text = c.FlashAttention;
        OptimizationModelMemory.Text = c.ModelMemory;
        OptimizationCacheMemory.Text = c.CacheMemory;
        OptimizationRuntimeMemory.Text = c.RuntimeMemory;
        OptimizationPredictedPeak.Text = c.PredictedPeak;
        OptimizationSafeBudget.Text = c.SafeBudget;
        OptimizationHeadroom.Text = c.Headroom;
        OptimizationEvidence.Text = $"Evidence: {c.Evidence}";
        OptimizationTradeoff.Text = $"Trade-off: {c.Tradeoff}";
        OptimizationLimitations.Text = $"Limitation: {c.Limitations}";
        OptimizationRouteValidation.Text = $"Route validation: {c.RouteValidation}";
        OptimizationNewCopy.Text = c.ProducesPersistentArtifact
            ? "Yes · original unchanged" : "No · runtime setup only";
        OptimizationOutput.Text = c.Output;
        OptimizationWorkingDisk.Text = c.WorkingDisk;
        OptimizationFinalDisk.Text = c.FinalDisk;
    }

    private void ApplyProgress(OptimizationPresentationState presentation)
    {
        if (presentation.ProgressRows.Count != 7)
        {
            RejectProgressUpdate();
            return;
        }

        OptimizationProgressRows.Opacity = 1;
        OptimizationProgressRows.IsHitTestVisible = true;
        AutomationProperties.SetAccessibilityView(
            OptimizationProgressRows,
            AccessibilityView.Content);

        int activeIndex = -1;
        int activeCount = 0;
        for (int i = 0; i < presentation.ProgressRows.Count; i++)
        {
            if (presentation.ProgressRows[i].Status == OptimizationStageStatus.Active)
            {
                activeIndex = i;
                activeCount++;
            }
        }

        if (activeCount != 1)
        {
            RejectProgressUpdate();
            return;
        }

        OptimizationOverallProgress.Visibility = Visibility.Visible;
        if (_estimatePresentation is null || _estimatePlan != presentation.OptimizationPlanId || activeIndex < _estimateActiveIndex)
        {
            _estimatePlan = presentation.OptimizationPlanId;
            _operationEstimateOwner = (presentation.OptimizationPlanId, presentation.ConfigurationSha256);
        }
        _estimateActiveIndex = activeIndex;
        _estimatePresentation = presentation;
        SerializedProgressStageObservation[] observations = presentation.ProgressRows
            .Select(item => new SerializedProgressStageObservation(
                item.Status == OptimizationStageStatus.Active,
                item.Status == OptimizationStageStatus.Completed,
                item.Status == OptimizationStageStatus.NotApplicable))
            .ToArray();
        _operationSequence.Observe(
            _operationEstimateOwner, ++_estimateRevision, observations,
            _uiSettings.AnimationsEnabled && presentation.CanCancel);
        _estimateEpoch = _operationSequence.Epoch;
        UpdateOperationEstimate();
        _estimatePresenter ??= new DispatcherProgressPresenter(
            this, UpdateOperationEstimate, TimeSpan.FromMilliseconds(16));
        if (presentation.CanCancel) _estimatePresenter.Start();
        else _estimatePresenter.Stop();
    }

    private void UpdateOperationEstimate()
    {
        if (_estimatePresentation is not { } presentation
            || _estimateEpoch != _operationSequence.Epoch) return;
        SerializedProgressFrame frame = _operationSequence.GetFrame();
        FrameworkElement[] rows = [OptimizationStage1, OptimizationStage2, OptimizationStage3,
            OptimizationStage4, OptimizationStage5, OptimizationStage6, OptimizationStage7];
        Border[] surfaces = [OptimizationStage1GlyphSurface, OptimizationStage2GlyphSurface,
            OptimizationStage3GlyphSurface, OptimizationStage4GlyphSurface,
            OptimizationStage5GlyphSurface, OptimizationStage6GlyphSurface, OptimizationStage7GlyphSurface];
        TextBlock[] numbers = [OptimizationStage1Number, OptimizationStage2Number, OptimizationStage3Number,
            OptimizationStage4Number, OptimizationStage5Number, OptimizationStage6Number, OptimizationStage7Number];
        FontIcon[] glyphs = [OptimizationStage1Glyph, OptimizationStage2Glyph, OptimizationStage3Glyph,
            OptimizationStage4Glyph, OptimizationStage5Glyph, OptimizationStage6Glyph, OptimizationStage7Glyph];
        ProgressRing[] orbits = [OptimizationStage1OrbitPresenter, OptimizationStage2OrbitPresenter,
            OptimizationStage3OrbitPresenter, OptimizationStage4OrbitPresenter,
            OptimizationStage5OrbitPresenter, OptimizationStage6OrbitPresenter, OptimizationStage7OrbitPresenter];
        TextBlock[] statuses = [OptimizationStage1Status, OptimizationStage2Status, OptimizationStage3Status,
            OptimizationStage4Status, OptimizationStage5Status, OptimizationStage6Status, OptimizationStage7Status];
        for (int i = 0; i < 7; i++)
        {
            var item = presentation.ProgressRows[i];
            SerializedProgressStageState state = frame.Stages[i];
            bool complete = state is SerializedProgressStageState.Completed
                or SerializedProgressStageState.NotNeeded;
            bool current = state == SerializedProgressStageState.Active;
            string percentage = $"{Math.Min(100m, Math.Floor((decimal)frame.ActiveFraction * 100m)):0}%";
            string estimatedPercentage = $"Estimated {percentage}";
            string status = complete
                ? state == SerializedProgressStageState.NotNeeded ? "Not needed" : "Complete"
                : current ? $"Checking\n{percentage}" : "Waiting";
            string surfaceKey = complete ? "OptimizationSuccessSurfaceBrush" :
                current ? "OptimizationAccentSurfaceBrush" : "OptimizationWaitingSurfaceBrush";
            string borderKey = complete ? "OptimizationSuccessBorderBrush" :
                current ? "OptimizationAccentBorderBrush" : "OptimizationBorderBrush";
            string accentKey = complete ? "OptimizationSuccessBrush" :
                current ? "OptimizationAccentBrush" : "OptimizationWaitingTextBrush";
            surfaces[i].Background = Brush(surfaceKey);
            surfaces[i].BorderBrush = Brush(borderKey);
            numbers[i].Foreground = Brush(accentKey);
            numbers[i].Visibility = complete || current ? Visibility.Collapsed : Visibility.Visible;
            glyphs[i].Foreground = Brush(accentKey);
            glyphs[i].Visibility = complete ? Visibility.Visible : Visibility.Collapsed;
            orbits[i].IsActive = current && presentation.CanCancel
                && _uiSettings.AnimationsEnabled;
            orbits[i].Visibility = current ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetAccessibilityView(orbits[i], AccessibilityView.Raw);
            statuses[i].Text = status;
            statuses[i].Foreground = Brush(accentKey);
            string accessibleStatus = current
                ? $"Checking · {estimatedPercentage}" : status;
            AutomationProperties.SetName(rows[i], $"{item.Title}, step {i + 1} of 7, {accessibleStatus}");
            AutomationProperties.SetItemStatus(rows[i], accessibleStatus);
        }
        OptimizationOverallProgress.IsIndeterminate = false;
        OptimizationOverallProgress.Value = frame.OverallValue;
        string overallPercentage = $"{Math.Min(100m, Math.Floor((decimal)frame.OverallValue * 100m / 7m)):0}%";
        string estimatedOverall = $"Estimated {overallPercentage}";
        if (OptimizationOverallPercentage.Text != overallPercentage)
        {
            OptimizationOverallPercentage.Text = overallPercentage;
            AutomationProperties.SetItemStatus(
                OptimizationOverallProgress, estimatedOverall);
        }
        int completed = frame.Stages.Count(state => state is SerializedProgressStageState.Completed
            or SerializedProgressStageState.NotNeeded);
        OptimizationProgressCount.Text = $"{completed} of 7 stages complete";
        if (frame.ActiveIndex >= 0)
        {
            OptimizationProgressRow active = presentation.ProgressRows[frame.ActiveIndex];
            OptimizationProgressHeading.Text = active.Title;
            OptimizationProgressDetail.Text = active.Description;
        }
        _estimateActiveIndex = frame.ActiveIndex;
        if (_realRunAuthorized && frame.ActiveIndex >= 0)
        {
            ApplyRunningAccessibility(presentation, _focusFirstProgress);
            _focusFirstProgress = false;
        }
        if (!frame.NeedsTicks)
        {
            _estimatePresenter?.Stop();
            if (frame.IsFinished) CompleteSuccessfulTerminalHandoff();
        }
    }

    private void ApplyRunningActions(OptimizationPresentationState presentation)
    {
        BtnCancelOptimization.IsEnabled = presentation.CanCancel;
        BtnCancelOptimization.Visibility = presentation.CanCancel ? Visibility.Visible : Visibility.Collapsed;
        OptimizationCancellingCopy.Visibility = presentation.CanCancel ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RejectProgressUpdate()
    {
        StopProgressRings();
        OptimizationProgressHeading.Text = "Optimisation progress unavailable";
        OptimizationProgressDetail.Text = "No stage evidence was accepted for this update.";
        OptimizationProgressCount.Text = "Progress unavailable";
        OptimizationOverallProgress.Visibility = Visibility.Collapsed;
        OptimizationProgressRows.Opacity = 0;
        OptimizationProgressRows.IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(
            OptimizationProgressRows,
            AccessibilityView.Raw);
        BtnCancelOptimization.IsEnabled = false;
        BtnCancelOptimization.Visibility = Visibility.Collapsed;
        OptimizationCancellingCopy.Text = "Progress details are unavailable. No further stage has been assumed.";
        OptimizationCancellingCopy.Visibility = Visibility.Visible;
    }

    private void StopProgressRings()
    {
        _estimatePresenter?.Stop();
        _operationSequence.Abort();
        _estimateEpoch = _operationSequence.Epoch;
        _estimatePresentation = null;
        ProgressRing[] rings = [OptimizationStage1OrbitPresenter, OptimizationStage2OrbitPresenter,
            OptimizationStage3OrbitPresenter, OptimizationStage4OrbitPresenter,
            OptimizationStage5OrbitPresenter, OptimizationStage6OrbitPresenter, OptimizationStage7OrbitPresenter];
        foreach (ProgressRing ring in rings)
        {
            ring.IsActive = false;
            ring.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyTerminal(OptimizationPresentationState presentation)
    {
        bool success = presentation.Kind is OptimizationPageStateKind.SucceededPersistent
            or OptimizationPageStateKind.SucceededRuntimeProfile;
        var colors = Tone(presentation.Tone);
        OptimizationTerminalGlyphSurface.Background = colors.surface;
        OptimizationTerminalGlyphSurface.BorderBrush = colors.border;
        OptimizationTerminalGlyph.Foreground = colors.accent;
        OptimizationTerminalGlyph.Glyph = presentation.Tone switch
        {
            OptimizationPresentationTone.Success => "\uE73E",
            OptimizationPresentationTone.Warning => "\uE7BA",
            OptimizationPresentationTone.Error => "\uE711",
            _ => "\uE946"
        };
        OptimizationTerminalHeading.Text = presentation.Title;
        OptimizationTerminalSummary.Text = presentation.Summary;
        OptimizationTerminalBadge.Background = colors.surface;
        OptimizationTerminalBadge.BorderBrush = colors.border;
        OptimizationTerminalBadgeText.Foreground = colors.accent;
        OptimizationTerminalBadgeText.Text = success
            ? presentation.Kind == OptimizationPageStateKind.SucceededPersistent
                ? "VALIDATED" : "RUNTIME SETUP"
            : presentation.Kind == OptimizationPageStateKind.Cancelled ? "CANCELLED" : "ACTION NEEDED";
        OptimizationTerminalTruthHeading.Text = success ? "Validated result" : "What happened";
        OptimizationTerminalTruth.Text = presentation.Summary;
        OptimizationSupportCode.Text = $"Support code: {presentation.SupportCode}.";
        bool hideEmptySuccessDetails = success
            && presentation.SupportCode ==
                GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization
                    .OptimizationSupportCode.None;
        OptimizationTerminalDetailsExpander.IsExpanded = false;
        OptimizationTerminalDetailsExpander.Visibility = hideEmptySuccessDetails
            ? Visibility.Collapsed : Visibility.Visible;
        bool verifiedExportBound = _exportController is not null &&
            _exportPresentationKind != OptimizationExportPresentationKind.None &&
            ExportState.Kind != OptimizationExportStateKind.Unbound;
        OptimizationExportPanel.Visibility = success && verifiedExportBound
            ? Visibility.Visible : Visibility.Collapsed;
        ApplyTerminalActions(presentation);
        if (presentation.Kind == OptimizationPageStateKind.SucceededPersistent &&
            _exportPresentationKind == OptimizationExportPresentationKind.PersistentModel)
        {
            _exportPresentationKind =
                OptimizationExportPresentationKind.PersistentModel;
            OptimizationExportHeading.Text = "Save the validated model";
            OptimizationExportDetail.Text =
                "Choose where to save this verified result. Your original model remains unchanged.";
        }
        else if (presentation.Kind == OptimizationPageStateKind.SucceededRuntimeProfile &&
            _exportPresentationKind == OptimizationExportPresentationKind.GgufRuntimeBundle)
        {
            _exportPresentationKind =
                OptimizationExportPresentationKind.GgufRuntimeBundle;
            OptimizationExportHeading.Text = "Save the validated setup";
            OptimizationExportDetail.Text =
                "Choose where to save this verified runtime setup. Your original model remains unchanged.";
        }

        if (verifiedExportBound)
        {
            ApplyExportState(ExportState);
        }
        else
        {
            SetSaveEnabled(false);
        }
        UpdateImportActionEnabled();
    }

    private void ApplyTerminalActions(OptimizationPresentationState presentation)
    {
        IReadOnlyList<OptimizationActionPresentation> actions = presentation.Actions;
        OptimizationActionPresentation? back = actions.FirstOrDefault(
            action => action.Command is OptimizationCommand.BackToCompatibility
                or OptimizationCommand.ImportAnotherModel);
        OptimizationActionPresentation? primary = actions.FirstOrDefault(
            action => action.IsPrimary
                && action.Command != OptimizationCommand.BackToCompatibility);
        OptimizationActionPresentation? alternative = actions.FirstOrDefault(
            action => !action.IsPrimary
                && action.Command is not OptimizationCommand.BackToCompatibility
                and not OptimizationCommand.ImportAnotherModel);

        ApplyAction(BtnOptimizationTerminalBack, back);
        ApplyAction(BtnOptimizationAlternative, alternative);
        ApplyAction(BtnOptimizationPrimary, primary);
        OptimizationTerminalForwardActions.Visibility =
            alternative is null && primary is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void ApplyAction(Button button, OptimizationActionPresentation? action)
    {
        button.Visibility = action is null ? Visibility.Collapsed : Visibility.Visible;
        button.Tag = action?.Command;
        if (action is null)
        {
            button.IsEnabled = false;
            if (string.Equals(button.Name, "BtnOptimizationTerminalBack",
                    StringComparison.Ordinal))
            {
                AutomationProperties.SetAutomationId(button, string.Empty);
                AutomationProperties.SetName(button, string.Empty);
            }
            return;
        }
        button.Content = action.Text;
        button.IsEnabled = action.IsEnabled;
        if (string.Equals(button.Name, "BtnOptimizationTerminalBack",
                StringComparison.Ordinal))
        {
            AutomationProperties.SetAutomationId(button, action.Command switch
            {
                OptimizationCommand.ImportAnotherModel => "OptimizationAction.ImportAnotherModel",
                OptimizationCommand.BackToCompatibility => "OptimizationAction.BackToCompatibility",
                _ => string.Empty
            });
        }
        AutomationProperties.SetName(button, action.Text);
    }

    private void Confirm()
    {
        if (Volatile.Read(ref _isRetired) == 0 && _entryContext is not null)
        {
            _realRunAuthorized = true;
            _lastAnnouncedRunningStage = -1;
        }
        RaiseIntent(OptimizationCommand.Confirm);
    }

    private void TerminalAction_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: OptimizationCommand command }) return;
        if (command == OptimizationCommand.Save)
        {
            _observedExportOperation = ObserveExportOperationAsync(TryStartExportAsync());
            return;
        }
        if (command == OptimizationCommand.ImportAnotherModel
            && !TryBeginImportNavigation())
        {
            return;
        }
        RaiseIntent(command);
    }

    internal Task<bool> TryStartExportAsync() =>
        Volatile.Read(ref _isRetired) != 0
            || BtnOptimizationAlternative.Tag is not OptimizationCommand.Save
            || !BtnOptimizationAlternative.IsEnabled
            ? Task.FromResult(false)
            : _exportController?.TryStartAsync() ?? Task.FromResult(false);

    internal bool TryCancelExport() => _exportController?.TryCancel() ?? false;

    internal Task<bool> TryRetryExportAsync()
    {
        OptimizationExportController? controller = _exportController;
        OptimizationExportViewState state = controller?.State
            ?? OptimizationExportViewState.Unbound();
        bool retryable = state.Kind == OptimizationExportStateKind.Cancelled
            || state.Kind == OptimizationExportStateKind.Failed
                && state.Failure != OptimizationExportFailure.CleanupFailure;
        return retryable
            ? controller!.TryRetryAsync()
            : Task.FromResult(false);
    }

    private void ExportController_StateChanged(object? sender, OptimizationExportViewState state)
    {
        if (sender is not OptimizationExportController controller) return;
        long generation = Volatile.Read(ref _exportBindingGeneration);
        if (!IsCurrentExportState(controller, state, generation)) return;
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (IsCurrentExportState(controller, state, generation))
                    ApplyExportState(controller, state, generation);
            });
            return;
        }
        ApplyExportState(controller, state, generation);
    }

    private bool IsCurrentExportState(
        OptimizationExportController controller,
        OptimizationExportViewState state,
        long generation) =>
        Volatile.Read(ref _isRetired) == 0
        && generation == Volatile.Read(ref _exportBindingGeneration)
        && ReferenceEquals(controller, _exportController)
        && Equals(controller.State, state);

    private void ApplyExportState(
        OptimizationExportController controller,
        OptimizationExportViewState state,
        long generation)
    {
        if (!IsCurrentExportState(controller, state, generation)) return;
        ApplyExportState(state);
    }

    private void ApplyExportState(OptimizationExportViewState state)
    {
        OptimizationExportStateKind priorKind = _lastExportKind;
        _lastExportKind = state.Kind;
        bool succeeded = state.Kind == OptimizationExportStateKind.Succeeded;
        bool runtimeBundle = _exportPresentationKind ==
            OptimizationExportPresentationKind.GgufRuntimeBundle;
        OptimizationExportHeading.Text = runtimeBundle
            ? succeeded
                ? "Model and settings saved successfully"
                : "Save model and runtime settings"
            : succeeded
                ? "Model saved successfully"
                : "Save the validated model";
        OptimizationExportDetail.Text = runtimeBundle
            ? succeeded
                ? "The unchanged model weights and verified runtime settings were saved together."
                : "Choose where to save the unchanged model weights and verified runtime settings."
            : succeeded
                ? "Your optimised model has been saved. Your original model remains unchanged."
                : "Choose where to save this verified result. Your original model remains unchanged.";
        OptimizationExportStatus.Text = state.StatusText;
        AutomationProperties.SetName(
            OptimizationExportPanel,
            runtimeBundle
                ? $"Verified model and runtime settings export. {state.StatusText}"
                : $"Verified model export. {state.StatusText}");
        OptimizationExportPanel.IsTabStop = state.Kind == OptimizationExportStateKind.Cancelling;
        bool busy = state.Kind is OptimizationExportStateKind.Running
            or OptimizationExportStateKind.Cancelling;
        OptimizationExportProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        bool enteringRunning = state.Kind == OptimizationExportStateKind.Running &&
            _estimatedExportState?.Kind != OptimizationExportStateKind.Running;
        if (enteringRunning)
        {
            _exportEstimateOwner = new();
            _exportEstimate.Reset();
            _exportEstimatePresenter ??= new DispatcherProgressPresenter(
                this, UpdateExportEstimate);
            _exportEstimatePresenter.SetValue(
                OptimizationExportProgress, 0, motion: false);
            OptimizationExportPercentage.Text = "0% overall";
            AutomationProperties.SetItemStatus(
                OptimizationExportProgress,
                "Estimated 0% overall");
        }
        _estimatedExportState = state;
        UpdateExportEstimate();
        _exportEstimatePresenter ??= new DispatcherProgressPresenter(this, UpdateExportEstimate);
        if (state.Kind == OptimizationExportStateKind.Running && state.Stage != OptimizationExportStage.CleaningUp) _exportEstimatePresenter.Start();
        else { _exportEstimatePresenter.Stop(); _exportEstimate.Freeze(); }
        OptimizationExportPercentage.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        SetSaveEnabled(state.Kind == OptimizationExportStateKind.Ready);
        BtnCancelExport.Visibility = state.Kind == OptimizationExportStateKind.Running
            ? Visibility.Visible : Visibility.Collapsed;
        BtnCancelExport.IsEnabled = state.Kind == OptimizationExportStateKind.Running;
        bool retryable = state.Kind == OptimizationExportStateKind.Cancelled
            || state.Kind == OptimizationExportStateKind.Failed
                && state.Failure != OptimizationExportFailure.CleanupFailure;
        BtnRetryExport.Visibility = retryable ? Visibility.Visible : Visibility.Collapsed;
        BtnRetryExport.IsEnabled = retryable;
        OptimizationExportActions.Visibility =
            BtnCancelExport.Visibility == Visibility.Visible
                || BtnRetryExport.Visibility == Visibility.Visible
                ? Visibility.Visible : Visibility.Collapsed;
        UpdateImportActionEnabled();
        if ((state.Kind is OptimizationExportStateKind.Cancelled
                or OptimizationExportStateKind.Failed)
            && _exportController is { } cleanupController)
        {
            _ = RefreshImportAfterCleanupAsync(
                cleanupController, Volatile.Read(ref _exportBindingGeneration));
        }
        if (priorKind != state.Kind)
        {
            RestoreVisibleExportFocus(priorKind, state, retryable,
                _exportController, Volatile.Read(ref _exportBindingGeneration));
        }
    }

    private void UpdateExportEstimate()
    {
        if (_estimatedExportState is not { } state) return;
        if (state.Kind != OptimizationExportStateKind.Running) return;
        var stage = state.Stage ?? OptimizationExportStage.ChoosingDestination;
        if (stage == OptimizationExportStage.CleaningUp)
        {
            _exportEstimatePresenter?.Stop();
            _exportEstimate.Freeze();
            return;
        }
        double? measured = state.Fraction is double fraction && double.IsFinite(fraction) && fraction >= 0 && fraction <= 1 ? fraction : null;
        _exportEstimate.Update(_exportEstimateOwner, ++_estimateRevision, stage, measured, false, _uiSettings.AnimationsEnabled);
        double local = _exportEstimate.GetFraction();
        int visibleStageIndex = stage switch
        {
            OptimizationExportStage.Writing => 0,
            OptimizationExportStage.Verifying => 1,
            OptimizationExportStage.Publishing => 2,
            _ => -1
        };
        double overall = visibleStageIndex < 0
            ? 0
            : Math.Min(99, (visibleStageIndex + local) * (100d / 3d));
        OptimizationExportProgress.IsIndeterminate = false;
        _exportEstimatePresenter ??= new DispatcherProgressPresenter(this, UpdateExportEstimate);
        _exportEstimatePresenter.SetValue(OptimizationExportProgress, overall, _uiSettings.AnimationsEnabled);
        double presentedOverall = OptimizationExportProgress.Value;
        double presentedLocal = visibleStageIndex < 0
            ? 0
            : Math.Clamp(
                presentedOverall / (100d / 3d) - visibleStageIndex,
                0,
                1);
        string stageName = stage switch { OptimizationExportStage.ChoosingDestination => "Choose destination", OptimizationExportStage.Writing => "Copying", OptimizationExportStage.Verifying => "Verifying", _ => "Publishing" };
        string overallPercentage = $"{Math.Floor((decimal)presentedOverall):0}%";
        string localPercentage = $"{Math.Floor((decimal)presentedLocal * 100m):0}%";
        string text = $"{overallPercentage} overall · {stageName}: {localPercentage}";
        string accessibleText = $"Estimated {overallPercentage} overall · {stageName}: {(_exportEstimate.IsEstimated() ? "Estimated " : string.Empty)}{localPercentage}";
        if (OptimizationExportPercentage.Text != text)
        {
            OptimizationExportPercentage.Text = text;
            AutomationProperties.SetItemStatus(
                OptimizationExportProgress, accessibleText);
        }
    }

    private void CancelExport_Click(object sender, RoutedEventArgs args)
    {
        _exportEstimatePresenter?.Stop();
        _exportEstimate.Freeze();
        TryCancelExport();
    }

    private void RetryExport_Click(object sender, RoutedEventArgs args)
    {
        _observedExportOperation = ObserveExportOperationAsync(TryRetryExportAsync());
    }

    private void RestoreVisibleExportFocus(
        OptimizationExportStateKind priorKind,
        OptimizationExportViewState state,
        bool retryable,
        OptimizationExportController? controller,
        long generation)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (Volatile.Read(ref _isRetired) != 0
                || OptimizationExportPanel.Visibility != Visibility.Visible
                || _lastExportKind != state.Kind
                || controller is null
                || !IsCurrentExportState(controller, state, generation))
            {
                return;
            }

            if (state.Kind == OptimizationExportStateKind.Running
                && priorKind != OptimizationExportStateKind.Running
                && BtnCancelExport.Visibility == Visibility.Visible)
            {
                BtnCancelExport.Focus(FocusState.Programmatic);
            }
            else if (state.Kind == OptimizationExportStateKind.Cancelling)
            {
                OptimizationExportPanel.Focus(FocusState.Programmatic);
            }
            else if (retryable && BtnRetryExport.Visibility == Visibility.Visible)
            {
                BtnRetryExport.Focus(FocusState.Programmatic);
            }
            else if (state.Kind == OptimizationExportStateKind.Succeeded
                && BtnOptimizationPrimary.Visibility == Visibility.Visible
                && BtnOptimizationPrimary.IsEnabled)
            {
                BtnOptimizationPrimary.Focus(FocusState.Programmatic);
            }
        });
    }

    private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs args) =>
        ApplyResponsiveLayout(args.NewSize.Width);

    private void ApplyResponsiveLayout(double availableWidth)
    {
        if (availableWidth <= 0 || double.IsNaN(availableWidth))
        {
            return;
        }

        OptimizationContentHost.Width = Math.Min(960d, availableWidth);
        bool elevatedText = _uiSettings.TextScaleFactor >= 1.5d;
        bool oneColumn = availableWidth < 720d || elevatedText;
        bool compactActions = availableWidth < 720d || elevatedText;

        string stateName = availableWidth < 560d
            ? "CompactPageState"
            : availableWidth < 1040d
                ? "StandardPageState"
                : "WidePageState";
        VisualStateManager.GoToState(this, stateName, false);

        OptimizationFactsSecondaryColumn.Width = oneColumn
            ? new GridLength(0d) : new GridLength(1d, GridUnitType.Star);
        Grid.SetRow(OptimizationRuntimeFacts, 0);
        Grid.SetColumn(OptimizationRuntimeFacts, 0);
        Grid.SetRow(OptimizationMemoryFacts, oneColumn ? 1 : 0);
        Grid.SetColumn(OptimizationMemoryFacts, oneColumn ? 0 : 1);
        Grid.SetRow(OptimizationOutputFacts, oneColumn ? 2 : 1);
        Grid.SetColumn(OptimizationOutputFacts, 0);
        Grid.SetRow(OptimizationTrustFacts, oneColumn ? 3 : 1);
        Grid.SetColumn(OptimizationTrustFacts, oneColumn ? 0 : 1);

        Orientation orientation = compactActions ? Orientation.Vertical : Orientation.Horizontal;
        OptimizationConfirmingActions.Orientation = orientation;
        OptimizationTerminalActions.Orientation = orientation;
        OptimizationTerminalForwardActions.Orientation = orientation;
        OptimizationExportActions.Orientation = orientation;
        OptimizationProgressRows.Height = oneColumn ? double.NaN : 392d;
    }

    private void ApplyRunningAccessibility(
        OptimizationPresentationState presentation, bool focus)
    {
        int active = _estimateActiveIndex;
        if (active < 0 || active == _lastAnnouncedRunningStage) return;
        _lastAnnouncedRunningStage = active;
        string message = $"Optimisation. {presentation.ProgressRows[active].Title}. Step {active + 1} of 7.";
        AutomationProperties.SetName(OptimizationProgressPanel, message);
        AutomationProperties.SetLiveSetting(OptimizationProgressPanel, AutomationLiveSetting.Polite);
        long epoch = _progressPacer.Epoch;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (Volatile.Read(ref _isRetired) != 0 || epoch != _progressPacer.Epoch
                || _displayedProgress is null
                || _estimateActiveIndex != active
                || _displayedProgress.OptimizationPlanId != presentation.OptimizationPlanId
                || _displayedProgress.ConfigurationSha256 != presentation.ConfigurationSha256
                || _presentation?.Kind != OptimizationPageStateKind.Running
                || OptimizationProgressPanel.Visibility != Visibility.Visible) return;
            if (focus) OptimizationProgressPanel.Focus(FocusState.Programmatic);
            RaiseLive(OptimizationProgressPanel);
        });
    }

    private void FocusAndAnnounceTerminal(OptimizationPresentationState presentation)
    {
        AutomationProperties.SetName(OptimizationTerminalPanel, $"{presentation.Title}. {presentation.Summary}");
        AutomationProperties.SetLiveSetting(OptimizationTerminalPanel, AutomationLiveSetting.Polite);
        OptimizationTerminalPanel.IsTabStop = true;
        long epoch = _progressPacer.Epoch;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (Volatile.Read(ref _isRetired) != 0 || epoch != _progressPacer.Epoch
                || !ReferenceEquals(_presentation, presentation)
                || OptimizationTerminalPanel.Visibility != Visibility.Visible) return;
            OptimizationTerminalPanel.Focus(FocusState.Programmatic);
            RaiseLive(OptimizationTerminalPanel);
        });
    }

    private static void RaiseLive(FrameworkElement element)
    {
        AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(element)
            ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private string TechnicalDetails(OptimizationPresentationState presentation) =>
        $"Plan and configuration identities are revalidated only after Start optimisation is requested. Support code: {presentation.SupportCode}.";

    private void RaiseIntent(OptimizationCommand command)
    {
        if (Volatile.Read(ref _isRetired) != 0 || _entryContext is null) return;
        if (command == OptimizationCommand.Cancel)
        {
            _estimatePresenter?.Stop();
            _operationSequence.Abort();
            _estimateEpoch = _operationSequence.Epoch;
        }
        if (command is OptimizationCommand.Cancel or OptimizationCommand.Retry
            or OptimizationCommand.Confirm)
            InvalidateProgressPlayback();
        IntentRequested?.Invoke(this, new OptimizationIntentEventArgs(command));
    }

    internal bool IsImportNavigationPending =>
        Volatile.Read(ref _importNavigationPending) != 0;

    internal bool CanImportAnotherModel
    {
        get
        {
            if (Volatile.Read(ref _isRetired) != 0
                || IsImportNavigationPending
                || _presentation is not
                    { Kind: OptimizationPageStateKind.SucceededPersistent
                        or OptimizationPageStateKind.SucceededRuntimeProfile })
            {
                return false;
            }

            OptimizationExportController? controller = _exportController;
            return controller?.IsCleanupPending != true
                && controller?.State.Kind is not OptimizationExportStateKind.Running
                    and not OptimizationExportStateKind.Cancelling;
        }
    }

    internal bool CanCompleteImportNavigation =>
        IsImportNavigationPending
        && Volatile.Read(ref _isRetired) == 0
        && _presentation is
            { Kind: OptimizationPageStateKind.SucceededPersistent
                or OptimizationPageStateKind.SucceededRuntimeProfile }
        && _exportController?.IsCleanupPending != true
        && _exportController?.State.Kind is not OptimizationExportStateKind.Running
            and not OptimizationExportStateKind.Cancelling;

    private bool TryBeginImportNavigation()
    {
        if (!CanImportAnotherModel
            || Interlocked.CompareExchange(ref _importNavigationPending, 1, 0) != 0)
        {
            return false;
        }
        UpdateImportActionEnabled();
        return true;
    }

    internal void CancelImportNavigation()
    {
        Interlocked.Exchange(ref _importNavigationPending, 0);
        UpdateImportActionEnabled();
    }

    private void UpdateImportActionEnabled()
    {
        if (BtnOptimizationTerminalBack.Tag is OptimizationCommand.ImportAnotherModel)
        {
            BtnOptimizationTerminalBack.IsEnabled = CanImportAnotherModel;
        }
    }

    private async Task RefreshImportAfterCleanupAsync(
        OptimizationExportController controller,
        long generation)
    {
        try
        {
            await controller.CleanupReconciliation.ConfigureAwait(false);
        }
        catch
        {
            return;
        }
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (generation == Volatile.Read(ref _exportBindingGeneration)
                && ReferenceEquals(controller, _exportController))
            {
                UpdateImportActionEnabled();
            }
        });
    }

    internal Task RetireForNavigationAsync()
    {
        lock (_navigationRetirementLock)
        {
            if (_navigationRetirementTask is not null) return _navigationRetirementTask;
            Interlocked.Exchange(ref _isRetired, 1);
            Interlocked.Exchange(ref _importNavigationPending, 0);
            SetProgressMotionSubscription(false);
            InvalidateProgressPlayback();
            StopProgressRings();
            RetireExportController();
            DisableVisibleActionsForRetirement();
            _navigationRetirementTask = _detachedExportOperations;
            return _navigationRetirementTask;
        }
    }

    private void ResetExportBinding()
    {
        _exportEstimatePresenter?.Stop();
        _exportEstimate.Reset();
        _estimatedExportState = null;
        checked { _exportBindingGeneration++; }
        RetireExportController();
        _exportPresentationKind = OptimizationExportPresentationKind.None;
        _lastExportKind = OptimizationExportStateKind.Unbound;
        OptimizationExportStatus.Text = OptimizationExportViewState.Unbound().StatusText;
        OptimizationExportProgress.Visibility = Visibility.Collapsed;
        OptimizationExportProgress.IsIndeterminate = false;
        OptimizationExportProgress.Value = 0d;
        OptimizationExportPercentage.Text = string.Empty;
        OptimizationExportPercentage.Visibility = Visibility.Collapsed;
        BtnCancelExport.Visibility = Visibility.Collapsed;
        BtnRetryExport.Visibility = Visibility.Collapsed;
        OptimizationExportActions.Visibility = Visibility.Collapsed;
        OptimizationExportPanel.Visibility = Visibility.Collapsed;
        SetSaveEnabled(false);
    }

    private void RetireExportController()
    {
        OptimizationExportController? controller = _exportController;
        if (controller is null) return;
        checked { _exportBindingGeneration++; }
        _exportController = null;
        controller.StateChanged -= ExportController_StateChanged;
        Task retirement = controller.RetireAsync();
        _detachedExportCleanup = Task.WhenAll(
            _detachedExportCleanup, controller.CleanupReconciliation);
        _detachedExportOperations = Task.WhenAll(
            _detachedExportOperations, retirement);
        _ = ObserveDetachedTaskAsync(
            _detachedExportCleanup, "cleanup reconciliation");
        _ = ObserveDetachedTaskAsync(
            _detachedExportOperations, "bounded retirement");
    }

    private void SetSaveEnabled(bool enabled)
    {
        if (BtnOptimizationAlternative.Tag is OptimizationCommand.Save)
            BtnOptimizationAlternative.IsEnabled = enabled;
    }

    private async Task ObserveExportOperationAsync(Task<bool> operation)
    {
        try { await operation.ConfigureAwait(false); }
        catch (Exception exception)
        {
            ObservedExportFaultType = exception.GetType();
            Trace.TraceError(
                "The optimization-export page boundary observed {0}.",
                exception.GetType().Name);
        }
    }

    private static async Task ObserveDetachedTaskAsync(
        Task operation,
        string operationName)
    {
        try { await operation.ConfigureAwait(false); }
        catch (Exception exception)
        {
            Trace.TraceWarning(
                "Optimization-export {0} observed {1}.",
                operationName,
                exception.GetType().Name);
        }
    }

    private void DisableVisibleActionsForRetirement()
    {
        Button[] actions = [BtnOptimizationBack, BtnStartOptimization,
            BtnCancelOptimization, BtnOptimizationTerminalBack,
            BtnOptimizationAlternative, BtnOptimizationPrimary,
            BtnCancelExport, BtnRetryExport];
        foreach (Button action in actions) action.IsEnabled = false;
    }

    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (IsImportNavigationPending) return;
        await RetireForNavigationAsync();
    }

    private (Brush surface, Brush border, Brush accent) Tone(OptimizationPresentationTone tone) =>
        tone switch
        {
            OptimizationPresentationTone.Success => (Brush("OptimizationSuccessSurfaceBrush"), Brush("OptimizationSuccessBorderBrush"), Brush("OptimizationSuccessBrush")),
            OptimizationPresentationTone.Warning => (Brush("OptimizationWarningSurfaceBrush"), Brush("OptimizationWarningBorderBrush"), Brush("OptimizationWarningBrush")),
            OptimizationPresentationTone.Error => (Brush("OptimizationErrorSurfaceBrush"), Brush("OptimizationErrorBorderBrush"), Brush("OptimizationErrorBrush")),
            _ => (Brush("OptimizationAccentSurfaceBrush"), Brush("OptimizationAccentBorderBrush"), Brush("OptimizationAccentBrush"))
        };

    private Brush Brush(string key) => (Brush)Resources[key];
}
