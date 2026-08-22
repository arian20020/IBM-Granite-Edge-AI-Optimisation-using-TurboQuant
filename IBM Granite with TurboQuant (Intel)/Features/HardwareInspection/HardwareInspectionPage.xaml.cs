using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.HardwareInspection;

public sealed partial class HardwareInspectionPage : Page
{
    private static readonly string[] ComputerGroups =
    [
        "Processor",
        "Memory",
        "Graphics",
        "Storage",
    ];
    private readonly HardwareInspectionViewModel? _viewModel;
    private bool _isActive;
    private bool _isLoaded;
    private bool _startAuthorized;
    private bool _hasStarted;
    private long _appliedRevision = -1;
    private long _appliedAttemptGeneration = -1;

    public HardwareInspectionPage()
    {
        InitializeComponent();
        ActiveActionCard.ActionRequested += OnActionRequested;
        ActionCard.ActionRequested += OnActionRequested;
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

        bool preserveDisclosureState = _appliedAttemptGeneration
            == snapshot.AttemptGeneration;
        Apply(
            snapshot.Presentation,
            snapshot.Summary,
            snapshot.Details,
            preserveDisclosureState);
        _appliedAttemptGeneration = snapshot.AttemptGeneration;
        _appliedRevision = snapshot.Revision;
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
                    _viewModel.Cancel();
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
        if (state.Kind == HardwareInspectionPresentationKind.Active)
        {
            ProgressCard.Apply(state);
            ProgressCard.Visibility = Visibility.Visible;
            ActiveActionCard.Apply(state);
            ActiveActionCard.Visibility = Visibility.Visible;
            TerminalPanel.Visibility = Visibility.Collapsed;
            return;
        }

        ProgressCard.Visibility = Visibility.Collapsed;
        ActiveActionCard.Visibility = Visibility.Collapsed;
        TerminalPanel.Visibility = Visibility.Visible;
        OutcomeCard.Apply(state);
        bool hasRecovery = state.Kind is HardwareInspectionPresentationKind.FailedCriticalEvidence
            or HardwareInspectionPresentationKind.FailedTransientOperation
            or HardwareInspectionPresentationKind.FailedApplicationRepairRequired
            or HardwareInspectionPresentationKind.Cancelled
            or HardwareInspectionPresentationKind.Stopping;
        if (hasRecovery)
        {
            RecoveryPanel.Apply(state);
            RecoveryPanel.Visibility = Visibility.Visible;
        }
        else
        {
            RecoveryPanel.Visibility = Visibility.Collapsed;
        }
        LocalProcessingPanel.Visibility = state.Kind is HardwareInspectionPresentationKind.FailedCriticalEvidence
            or HardwareInspectionPresentationKind.FailedTransientOperation
            or HardwareInspectionPresentationKind.FailedApplicationRepairRequired
            ? Visibility.Visible
            : Visibility.Collapsed;
        ReviewPanel.Visibility = state.Kind == HardwareInspectionPresentationKind.CompletedWithWarnings
            ? Visibility.Visible
            : Visibility.Collapsed;
        LimitationPanel.Visibility = isCompleted
            ? Visibility.Visible
            : Visibility.Collapsed;
        ActionCard.Apply(state);
        ActionCard.Visibility = state.Actions.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (summary is null)
        {
            SummaryGrid.Visibility = Visibility.Collapsed;
        }
        else
        {
            ComputerSummaryCard.Apply(summary, "This computer", ComputerGroups);
            RuntimeSummaryCard.Apply(summary, "Local AI tools", ["Local AI tools"]);
            SourcesSummaryCard.Apply(summary, "Information sources", ["Information sources"]);
            bool hasRuntimeFacts = RuntimeSummaryCard.FactItems.Count > 0;
            bool hasSourceFacts = SourcesSummaryCard.FactItems.Count > 0;
            RuntimeSummaryCard.Visibility = hasRuntimeFacts
                ? Visibility.Visible
                : Visibility.Collapsed;
            SourcesSummaryCard.Visibility = hasSourceFacts
                ? Visibility.Visible
                : Visibility.Collapsed;
            SupportPanel.Visibility = hasRuntimeFacts || hasSourceFacts
                ? Visibility.Visible
                : Visibility.Collapsed;
            SummaryGrid.Visibility = Visibility.Visible;
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
}
