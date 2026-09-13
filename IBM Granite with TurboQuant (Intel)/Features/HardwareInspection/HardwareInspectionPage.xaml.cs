using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.HardwareInspection;

public sealed partial class HardwareInspectionPage : Page
{
    private readonly HardwareInspectionViewModel? _viewModel;
    private bool _isActive;
    private bool _isLoaded;
    private bool _startAuthorized;
    private bool _hasStarted;
    private long _appliedRevision = -1;
    private long _appliedAttemptGeneration = -1;
    private long _publishedCompletionGeneration = -1;
    private long _acceptedCancelGeneration = -1;
    private bool _sawRealActivePresentation;
    private HardwareInspectionViewState? _pendingSuccessfulSnapshot;
    private HardwareInspectionViewState? _pendingCancelledSnapshot;
    private bool _cancelledPresentationQueued;
    private long _cancelledPresentationVersion;
    private long _acceptedCancellationPresentationVersion;
    private HardwareInspectionViewState? _acceptedStoppingSnapshot;
    private bool _acceptedCancellationSuppressesPendingSuccess;
    private bool _actionDispatchInProgress;
    private long _cancelledRecoveryVersion;
    private long _announcedCancelledGeneration = -1;

    public HardwareInspectionPage()
    {
        InitializeComponent();
        // Retain the normal compact badge at 100% while allowing its status
        // text to wrap and the surface to grow at elevated system text scales.
        InspectionIdentityStatus.Height = double.NaN;
        InspectionIdentityStatus.MinHeight = 32;
        InspectionIdentityStatusText.TextWrapping = TextWrapping.WrapWholeWords;
        ActiveActionCard.ActionRequested += OnActionRequested;
        ActionCard.ActionRequested += OnActionRequested;
        ProgressCard.SuccessfulPresentationReady += OnSuccessfulProgressPresentationReady;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public HardwareInspectionPage(HardwareInspectionViewModel viewModel)
        : this(viewModel, opaqueModelHandoff: null, startAuthorized: true)
    {
    }

    internal HardwareInspectionPage(
        HardwareInspectionViewModel viewModel,
        ModelInspectionHandoff opaqueModelHandoff)
        : this(viewModel, opaqueModelHandoff, startAuthorized: false)
    {
    }

    private HardwareInspectionPage(
        HardwareInspectionViewModel viewModel,
        ModelInspectionHandoff? opaqueModelHandoff,
        bool startAuthorized)
        : this()
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        OpaqueModelHandoff = opaqueModelHandoff;
        _startAuthorized = startAuthorized;
    }

    public event EventHandler<HardwareInspectionActionRequestedEventArgs>? ActionRequested;

    internal event EventHandler? JourneyAbandoned;

    internal event EventHandler<HardwareInspectionCompletedEventArgs>? InspectionCompleted;

    public object? FooterContent
    {
        get => FooterPresenter.Content;
        set => FooterPresenter.Content = value;
    }

    internal HardwareInspectionPresentationState? CurrentState { get; private set; }

    internal ModelInspectionHandoff? OpaqueModelHandoff { get; }

    internal bool IsStartAuthorized => _startAuthorized;

    internal Guid ConfiguredInspectionId =>
        _viewModel?.InitialInspectionId ?? Guid.Empty;

    internal bool TryGetCompletedReport(
        HardwareInspectionHandoff expectedHandoff,
        out HardwareInspectionPresentationState? presentation,
        out HardwareSummaryPresentation? summary,
        out HardwareInspectionDetailsState? details)
    {
        ArgumentNullException.ThrowIfNull(expectedHandoff);
        presentation = null;
        summary = null;
        details = null;
        if (_viewModel is null)
        {
            return false;
        }

        HardwareInspectionViewState snapshot = _viewModel.Snapshot;
        bool isCompleted = snapshot.Presentation.Kind is
            HardwareInspectionPresentationKind.Completed or
            HardwareInspectionPresentationKind.CompletedWithWarnings;
        if (snapshot.IsRunActive
            || !isCompleted
            || !ReferenceEquals(snapshot.Handoff, expectedHandoff)
            || snapshot.InspectionId != expectedHandoff.InspectionId
            || snapshot.AttemptGeneration != _appliedAttemptGeneration
            || snapshot.Revision != _appliedRevision
            || snapshot.AttemptGeneration != _publishedCompletionGeneration
            || snapshot.Summary is null
            || snapshot.Details is null
            || !snapshot.Presentation.ReportCreated
            || !snapshot.Presentation.DetailsAvailable)
        {
            return false;
        }

        presentation = snapshot.Presentation;
        summary = snapshot.Summary;
        details = snapshot.Details;
        return true;
    }

    private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        ContentWidthHost.Width = Math.Max(0, args.NewSize.Width);
    }

    internal void AuthorizeStart()
    {
        if (_viewModel is null || _startAuthorized)
        {
            return;
        }

        _startAuthorized = true;
        StartIfReady();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = true;
        StartIfReady();
    }

    private void StartIfReady()
    {
        if (_viewModel is null ||
            !_isLoaded ||
            !_startAuthorized ||
            _isActive ||
            _hasStarted)
        {
            return;
        }

        _isActive = true;
        _hasStarted = true;
        _viewModel.SnapshotChanged += OnSnapshotChanged;
        _ = ObserveAsync(_viewModel.ActivateAsync());
        ApplyLatestSnapshot();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = false;
        checked { _cancelledRecoveryVersion++; }
        _pendingSuccessfulSnapshot = null;
        CancelPendingAcceptedCancellationPresentation();
        CancelPendingCancelledPresentation();
        ProgressCard.AbortProgressPresentation();
        if (_viewModel is not null && _isActive)
        {
            _isActive = false;
            _viewModel.SnapshotChanged -= OnSnapshotChanged;
            _viewModel.Deactivate();
        }

        if (OpaqueModelHandoff is not null)
        {
            JourneyAbandoned?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSnapshotChanged(object? sender, EventArgs args)
    {
        if (!_isActive)
        {
            return;
        }

        if (_actionDispatchInProgress || _acceptedStoppingSnapshot is not null)
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyLatestSnapshot();
        }
        else
        {
            _ = DispatcherQueue.TryEnqueue(ApplyLatestSnapshot);
        }
    }

    private void ApplyLatestSnapshot()
    {
        if (_viewModel is null || !_isActive)
        {
            return;
        }

        HardwareInspectionViewState snapshot = _viewModel.Snapshot;
        if (snapshot.Revision <= _appliedRevision)
        {
            return;
        }

        if (snapshot.Presentation.Kind ==
                HardwareInspectionPresentationKind.Cancelled &&
            CurrentState?.Kind == HardwareInspectionPresentationKind.Stopping &&
            snapshot.AttemptGeneration == _appliedAttemptGeneration)
        {
            QueueCancelledPresentation(snapshot);
            return;
        }

        CancelPendingCancelledPresentation();

        bool preserveDisclosureState = _appliedAttemptGeneration
            == snapshot.AttemptGeneration;
        bool successful = snapshot.Presentation.Kind is
            HardwareInspectionPresentationKind.Completed or
            HardwareInspectionPresentationKind.CompletedWithWarnings;
        if (successful
            && ProgressCard.CurrentState?.Kind == HardwareInspectionPresentationKind.Active
            && ProgressPanel.Visibility == Visibility.Visible)
        {
            _pendingSuccessfulSnapshot = snapshot;
            _appliedAttemptGeneration = snapshot.AttemptGeneration;
            _appliedRevision = snapshot.Revision;
            ProgressCard.BeginSuccessfulPresentationHandoff();
            return;
        }

        _pendingSuccessfulSnapshot = null;
        Apply(
            snapshot.Presentation,
            snapshot.Summary,
            snapshot.Details,
            preserveDisclosureState);
        _appliedAttemptGeneration = snapshot.AttemptGeneration;
        _appliedRevision = snapshot.Revision;
        PublishCompletionIfReady(snapshot);
    }

    private void QueueAcceptedCancellationPresentation(
        HardwareInspectionViewState stopping,
        bool suppressPendingSuccess)
    {
        CancelPendingAcceptedCancellationPresentation();
        _acceptedStoppingSnapshot = stopping;
        _acceptedCancellationSuppressesPendingSuccess = suppressPendingSuccess;
        long version = ++_acceptedCancellationPresentationVersion;
        if (!DispatcherQueue.TryEnqueue(
                DispatcherQueuePriority.Low,
                () => PresentAcceptedCancellation(version)))
        {
            CancelPendingAcceptedCancellationPresentation();
        }
    }

    private void PresentAcceptedCancellation(long version)
    {
        if (version != _acceptedCancellationPresentationVersion)
        {
            return;
        }

        HardwareInspectionViewState? stopping = _acceptedStoppingSnapshot;
        bool suppressPendingSuccess =
            _acceptedCancellationSuppressesPendingSuccess;
        _acceptedStoppingSnapshot = null;
        _acceptedCancellationSuppressesPendingSuccess = false;
        if (!_isLoaded || !_isActive || _viewModel is null || stopping is null
            || _acceptedCancelGeneration != stopping.AttemptGeneration
            || CurrentState?.Kind != HardwareInspectionPresentationKind.Active
            || _viewModel.Snapshot.AttemptGeneration !=
                stopping.AttemptGeneration)
        {
            if (stopping is not null &&
                _acceptedCancelGeneration == stopping.AttemptGeneration)
            {
                _acceptedCancelGeneration = -1;
            }
            return;
        }

        _pendingSuccessfulSnapshot = null;
        ApplyStableStopping(stopping.Presentation);
        _appliedAttemptGeneration = stopping.AttemptGeneration;
        _appliedRevision = Math.Max(_appliedRevision, stopping.Revision);
        _viewModel.DispatchAcceptedCancellation(stopping.AttemptGeneration);
        if (suppressPendingSuccess)
        {
            _viewModel.CompletePendingSuccessfulPresentationCancellation(
                stopping.AttemptGeneration);
        }
        ApplyLatestSnapshot();
    }

    private void CancelPendingAcceptedCancellationPresentation()
    {
        _acceptedCancellationPresentationVersion++;
        _acceptedStoppingSnapshot = null;
        _acceptedCancellationSuppressesPendingSuccess = false;
    }

    private void QueueCancelledPresentation(HardwareInspectionViewState snapshot)
    {
        if (_pendingCancelledSnapshot is { } pending &&
            (pending.AttemptGeneration > snapshot.AttemptGeneration ||
             (pending.AttemptGeneration == snapshot.AttemptGeneration &&
              pending.Revision >= snapshot.Revision)))
        {
            return;
        }

        _pendingCancelledSnapshot = snapshot;
        if (_cancelledPresentationQueued)
        {
            return;
        }

        _cancelledPresentationQueued = true;
        long version = ++_cancelledPresentationVersion;
        _ = QueueCancelledPresentationAfterDelayAsync(
            DispatcherQueue,
            version);
    }

    private async Task QueueCancelledPresentationAfterDelayAsync(
        DispatcherQueue dispatcher,
        long version)
    {
        await Task.Delay(250).ConfigureAwait(false);
        _ = dispatcher.TryEnqueue(
            DispatcherQueuePriority.Low,
            () => PresentCancelled(version));
    }

    private void PresentCancelled(long version)
    {
        if (version != _cancelledPresentationVersion)
        {
            return;
        }

        HardwareInspectionViewState? pending = _pendingCancelledSnapshot;
        _pendingCancelledSnapshot = null;
        if (!_isLoaded || !_isActive || _viewModel is null || pending is null)
        {
            return;
        }

        HardwareInspectionViewState latest = _viewModel.Snapshot;
        if (latest.Presentation.Kind != HardwareInspectionPresentationKind.Cancelled ||
            latest.AttemptGeneration != pending.AttemptGeneration ||
            latest.Revision < pending.Revision ||
            CurrentState?.Kind != HardwareInspectionPresentationKind.Stopping)
        {
            ApplyLatestSnapshot();
            return;
        }

        Apply(
            latest.Presentation,
            latest.Summary,
            latest.Details,
            preserveDisclosureState: true);
        _appliedAttemptGeneration = latest.AttemptGeneration;
        _appliedRevision = latest.Revision;
        _cancelledPresentationQueued = false;
        QueueCancelledRecoveryPresentation(
            latest.AttemptGeneration,
            latest.Revision);
    }

    private void CancelPendingCancelledPresentation()
    {
        _cancelledPresentationVersion++;
        _cancelledPresentationQueued = false;
        _pendingCancelledSnapshot = null;
    }

    private void OnSuccessfulProgressPresentationReady(object? sender, EventArgs args)
    {
        if (!_isLoaded || !_isActive || _viewModel is null
            || _pendingSuccessfulSnapshot is not { } snapshot) return;
        HardwareInspectionViewState latest = _viewModel.Snapshot;
        if (latest.Revision != snapshot.Revision
            || latest.AttemptGeneration != snapshot.AttemptGeneration
            || latest.Presentation.Kind is not HardwareInspectionPresentationKind.Completed
                and not HardwareInspectionPresentationKind.CompletedWithWarnings)
        {
            _pendingSuccessfulSnapshot = null;
            ProgressCard.AbortProgressPresentation();
            return;
        }

        _pendingSuccessfulSnapshot = null;
        Apply(
            snapshot.Presentation,
            snapshot.Summary,
            snapshot.Details,
            preserveDisclosureState: true);
        PublishCompletionIfReady(snapshot);
    }

    private void PublishCompletionIfReady(HardwareInspectionViewState snapshot)
    {
        if (snapshot.AttemptGeneration != _acceptedCancelGeneration
            && snapshot.AttemptGeneration != _publishedCompletionGeneration
            && snapshot.Handoff is { } handoff
            && snapshot.Presentation.Kind is HardwareInspectionPresentationKind.Completed
                or HardwareInspectionPresentationKind.CompletedWithWarnings)
        {
            _publishedCompletionGeneration = snapshot.AttemptGeneration;
            InspectionCompleted?.Invoke(
                this,
                new HardwareInspectionCompletedEventArgs(handoff));
        }
    }

    private void OnActionRequested(
        object? sender,
        HardwareInspectionActionRequestedEventArgs args)
    {
        if (_viewModel is not null)
        {
            switch (args.Kind)
            {
                case HardwareInspectionActionKind.CancelInspection:
                    HardwareInspectionViewState beforeCancel = _viewModel.Snapshot;
                    if (CurrentState?.Kind != HardwareInspectionPresentationKind.Active)
                    {
                        return;
                    }

                    if (beforeCancel.AttemptGeneration == _acceptedCancelGeneration)
                    {
                        return;
                    }

                    // Do not query or move focus while the Button's pointer or
                    // automation invocation is unwinding. Recovery actions remain
                    // available through their normal tab order after cancellation.
                    _acceptedCancelGeneration = beforeCancel.AttemptGeneration;
                    HardwareInspectionViewState? acceptedStopping;
                    bool accepted;
                    _actionDispatchInProgress = true;
                    try
                    {
                        accepted = _viewModel.TryCancelAttempt(
                            beforeCancel.AttemptGeneration,
                            out acceptedStopping);
                    }
                    finally
                    {
                        _actionDispatchInProgress = false;
                    }
                    if (!accepted)
                    {
                        _acceptedCancelGeneration = -1;
                        if (!beforeCancel.IsRunActive)
                        {
                            _ = DispatcherQueue.TryEnqueue(() =>
                                ApplyAuthoritativeTerminalSnapshot(beforeCancel));
                        }
                        return;
                    }

                    if (acceptedStopping is null
                        || acceptedStopping.AttemptGeneration != beforeCancel.AttemptGeneration
                        || acceptedStopping.Presentation.Kind !=
                            HardwareInspectionPresentationKind.Stopping)
                    {
                        _acceptedCancelGeneration = -1;
                        return;
                    }

                    QueueAcceptedCancellationPresentation(
                        acceptedStopping,
                        suppressPendingSuccess: !beforeCancel.IsRunActive);
                    return;
                case HardwareInspectionActionKind.RunInspectionAgain:
                case HardwareInspectionActionKind.TryAgain:
                    if (OpaqueModelHandoff is null)
                    {
                        _ = ObserveAsync(_viewModel.RetryAsync());
                        return;
                    }

                    break;
                case HardwareInspectionActionKind.Stopping:
                    return;
            }
        }

        ActionRequested?.Invoke(this, args);
    }

    private void ApplyAuthoritativeTerminalSnapshot(
        HardwareInspectionViewState snapshot)
    {
        if (_viewModel is null ||
            !_isActive ||
            snapshot.IsRunActive ||
            snapshot.AttemptGeneration < _appliedAttemptGeneration ||
            snapshot.Presentation.Kind is HardwareInspectionPresentationKind.Active
                or HardwareInspectionPresentationKind.Stopping)
        {
            return;
        }

        HardwareInspectionViewState latest = _viewModel.Snapshot;
        if (latest.IsRunActive ||
            latest.AttemptGeneration != snapshot.AttemptGeneration ||
            latest.Revision < snapshot.Revision ||
            latest.Presentation.Kind is HardwareInspectionPresentationKind.Active
                or HardwareInspectionPresentationKind.Stopping)
        {
            ApplyLatestSnapshot();
            return;
        }

        _pendingSuccessfulSnapshot = null;
        CancelPendingCancelledPresentation();
        ProgressCard.AbortProgressPresentation();
        Apply(
            latest.Presentation,
            latest.Summary,
            latest.Details,
            preserveDisclosureState: true);
        _appliedAttemptGeneration = latest.AttemptGeneration;
        _appliedRevision = latest.Revision;
        PublishCompletionIfReady(latest);
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // The ViewModel maps service failures to privacy-safe terminal state.
        }
    }

    internal void Apply(
        HardwareInspectionPresentationState state,
        HardwareSummaryPresentation? summary = null,
        HardwareInspectionDetailsState? details = null,
        bool preserveDisclosureState = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Kind == HardwareInspectionPresentationKind.Cancelled)
        {
            CurrentState = state;
            PageSubtitleTextBlock.Text = state.Subtitle;
            ApplyIdentityStatus(state);
            ProgressCard.ApplyStopping(state);
            ActiveSafetyTextBlock.Text =
                "No hardware report was created. Return or start a fresh inspection.";
            ActiveActionCard.IsEnabled = true;
            ActiveActionCard.Apply(state);
            ProgressPanel.Visibility = Visibility.Visible;
            TerminalPanel.Visibility = Visibility.Collapsed;
            return;
        }

        bool isCompleted = state.Kind is HardwareInspectionPresentationKind.Completed
            or HardwareInspectionPresentationKind.CompletedWithWarnings;
        if (isCompleted && summary is null)
        {
            throw new ArgumentException("Completed presentation requires a hardware summary.", nameof(summary));
        }
        if (state.DetailsAvailable && details is null)
        {
            throw new ArgumentException("Stable terminal presentation requires details.", nameof(details));
        }

        CurrentState = state;
        PageSubtitleTextBlock.Text = state.Subtitle;
        ApplyIdentityStatus(state);
        if (state.Kind is HardwareInspectionPresentationKind.Active
            or HardwareInspectionPresentationKind.Stopping)
        {
            if (state.Kind == HardwareInspectionPresentationKind.Active)
            {
                if (_isActive && _hasStarted)
                {
                    _sawRealActivePresentation = true;
                }
                ProgressCard.Apply(state);
                ActiveSafetyTextBlock.Text =
                    "You can safely return to model inspection while this local check is running.";
                ActiveActionCard.IsEnabled = true;
                ActiveActionCard.Apply(state);
            }
            else
            {
                ApplyStableStopping(state);
                return;
            }
            ProgressPanel.Visibility = Visibility.Visible;
            TerminalPanel.Visibility = Visibility.Collapsed;
            return;
        }

        ProgressCard.AbortProgressPresentation();
        ProgressPanel.Visibility = Visibility.Collapsed;
        TerminalPanel.Visibility = Visibility.Visible;
        OutcomeCard.Apply(state);
        bool announceTerminal = _sawRealActivePresentation &&
            state.Kind != HardwareInspectionPresentationKind.Stopping;
        // The approved preview keeps terminal failures compact: outcome,
        // optional repair notice, disclosure, and the existing action band
        // the legacy recovery list duplicated that information and produced
        // the rejected "What to do next" nested-card layout.
        RecoveryPanel.Visibility = Visibility.Collapsed;
        bool showRepairNotice = state.Kind is
            HardwareInspectionPresentationKind.FailedApplicationRepairRequired or
            HardwareInspectionPresentationKind.FailedCriticalEvidence;
        LocalProcessingTextBlock.Text = state.Kind == HardwareInspectionPresentationKind.FailedCriticalEvidence
            ? "No hardware conclusion was made. Return to model inspection and try again."
            : "Repair or reinstall the application before trying hardware inspection again.";
        LocalProcessingPanel.Visibility = showRepairNotice
            ? Visibility.Visible
            : Visibility.Collapsed;
        ActionCard.Apply(state);
        ActionCard.Visibility = state.Actions.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (announceTerminal)
        {
            _sawRealActivePresentation = false;
            AutomationProperties.SetName(OutcomeCard, $"{state.Title}. {state.Body}");
            AutomationProperties.SetLiveSetting(OutcomeCard, AutomationLiveSetting.Polite);
            OutcomeCard.IsTabStop = true;
            if (state.Kind != HardwareInspectionPresentationKind.Cancelled)
            {
                _ = DispatcherQueue.TryEnqueue(() =>
                {
                    if (!_isActive || OutcomeCard.Visibility != Visibility.Visible)
                    {
                        return;
                    }

                    OutcomeCard.Focus(FocusState.Programmatic);
                    AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(OutcomeCard) ??
                        FrameworkElementAutomationPeer.CreatePeerForElement(OutcomeCard);
                    peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                });
            }
        }

        if (details is null)
        {
            DetailsCard.Visibility = Visibility.Collapsed;
        }
        else
        {
            DetailsCard.Apply(details, preserveDisclosureState);
            DetailsCard.Visibility = Visibility.Visible;
        }
    }

    private void ApplyStableStopping(HardwareInspectionPresentationState state)
    {
        CurrentState = state;
        PageSubtitleTextBlock.Text = state.Subtitle;
        ApplyIdentityStatus(state);
        ActiveSafetyTextBlock.Text =
            "Stopping safely. The current inspection layout will remain in place.";
        ProgressCard.ApplyStopping(state);
        ProgressCard.IsTabStop = true;
        ProgressCard.Focus(FocusState.Programmatic);
        ActiveActionCard.Apply(state);
        ProgressPanel.Visibility = Visibility.Visible;
        TerminalPanel.Visibility = Visibility.Collapsed;
    }

    private void QueueCancelledRecoveryPresentation(
        long attemptGeneration,
        long revision)
    {
        long version = checked(++_cancelledRecoveryVersion);
        _ = DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () => PresentCancelledRecovery(
                version,
                attemptGeneration,
                revision));
    }

    private void PresentCancelledRecovery(
        long version,
        long attemptGeneration,
        long revision)
    {
        if (version != _cancelledRecoveryVersion
            || !_isLoaded
            || !_isActive
            || _viewModel is null
            || _appliedAttemptGeneration != attemptGeneration
            || _appliedRevision != revision
            || _viewModel.Snapshot.AttemptGeneration != attemptGeneration
            || _viewModel.Snapshot.Revision != revision
            || CurrentState?.Kind != HardwareInspectionPresentationKind.Cancelled
            || !ActiveActionCard.IsLoaded)
        {
            return;
        }

        if (_announcedCancelledGeneration != attemptGeneration)
        {
            _announcedCancelledGeneration = attemptGeneration;
            AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(ProgressCard)
                ?? FrameworkElementAutomationPeer.CreatePeerForElement(ProgressCard);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }

        if (ActiveActionCard.FocusFirstEnabledAction())
        {
            ProgressCard.IsTabStop = false;
        }
    }

    private void ApplyIdentityStatus(HardwareInspectionPresentationState state)
    {
        string visualState;
        string status;
        switch (state.Kind)
        {
            case HardwareInspectionPresentationKind.Active:
                visualState = "IdentityInProgress";
                status = "Inspection in progress";
                break;
            case HardwareInspectionPresentationKind.Stopping:
                visualState = "IdentityInProgress";
                status = "Stopping";
                break;
            case HardwareInspectionPresentationKind.Completed:
                visualState = "IdentityReady";
                status = "Ready";
                break;
            case HardwareInspectionPresentationKind.CompletedWithWarnings:
                visualState = "IdentityNeedsAttention";
                status = "Review recommended";
                break;
            case HardwareInspectionPresentationKind.Cancelled:
                visualState = "IdentityNeedsAttention";
                status = "Cancelled";
                break;
            default:
                visualState = "IdentityFailed";
                status = "Inspection failed";
                break;
        }

        InspectionIdentityStatusText.Text = status;
        AutomationProperties.SetName(InspectionIdentityStatus, status);
        VisualStateManager.GoToState(this, visualState, useTransitions: true);
    }
}
