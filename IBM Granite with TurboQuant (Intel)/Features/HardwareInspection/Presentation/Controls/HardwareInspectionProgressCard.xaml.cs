using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Presentation.Progress;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;

public sealed partial class HardwareInspectionProgressCard : UserControl
{
    private bool managedLoaded;
    private bool automationProjectionPending;
    private long automationProjectionGeneration;
    private long pendingAutomationProjectionGeneration = -1;
    private readonly SerializedProgressSequence progressSequence = new();
    private readonly Windows.UI.ViewManagement.UISettings uiSettings = new();
    private DispatcherProgressPresenter? estimatePresenter;
    private object? estimateOwner;
    private long estimateRevision;
    private long estimateEpoch;
    private bool motionEnabled;
    private bool successfulHandoffPending;
    private int lastAnnouncedDisplayedStage = -1;
    private int displayedStage = -1;
    private long announcementGeneration;

    internal event EventHandler? SuccessfulPresentationReady;

    public HardwareInspectionProgressCard()
    {
        InitializeComponent();
        Loaded += HardwareInspectionProgressCard_Loaded;
        Unloaded += HardwareInspectionProgressCard_Unloaded;
        ProgressRowsItemsControl.LayoutUpdated += ProgressRowsItemsControl_LayoutUpdated;
    }

    internal ObservableCollection<HardwareInspectionProgressRowViewData> Rows { get; } = [];

    internal HardwareInspectionPresentationState? CurrentState { get; private set; }

    internal void Apply(HardwareInspectionPresentationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Kind != HardwareInspectionPresentationKind.Active)
        {
            throw new ArgumentException("Progress card accepts only Active presentation state.", nameof(state));
        }

        // a fast group can settle before the next paced stage is displayed
        bool awaitingNextStage = state.CompletedStageCount > 0 && state.StageRows.Any(row => row.State == HardwareInspectionStageRowState.Waiting);
        if (state.StageRows.Count != 7
            || (state.StageRows.All(row => row.State != HardwareInspectionStageRowState.Active) && !awaitingNextStage))
        {
            throw new ArgumentException("Active presentation must contain seven rows and active work.", nameof(state));
        }

        bool newOwner = !Equals(estimateOwner, state.ProgressOwner);
        estimateOwner = state.ProgressOwner;
        if (newOwner)
        {
            successfulHandoffPending = false;
            lastAnnouncedDisplayedStage = -1;
            displayedStage = -1;
            checked { announcementGeneration++; }
        }
        CurrentState = state;
        KickerTextBlock.Text = state.Kicker;
        TitleTextBlock.Text = state.Title;
        BodyTextBlock.Text = state.Body;
        OverallProgressBar.Visibility = Visibility.Visible;
        OverallPercentage.Visibility = Visibility.Visible;
        CountLabelTextBlock.Text = "checks complete";

        if (Rows.Count == 0)
        {
            for (int index = 0; index < state.StageRows.Count; index++)
            {
                Rows.Add(new HardwareInspectionProgressRowViewData(index + 1, state.StageRows[index]));
            }
        }
        else
        {
            for (int index = 0; index < state.StageRows.Count; index++)
            {
                Rows[index].Update(index + 1, state.StageRows[index]);
            }
        }

        SerializedProgressStageObservation[] observations = state.StageRows
            .Select(row => new SerializedProgressStageObservation(
                row.State == HardwareInspectionStageRowState.Active,
                row.State == HardwareInspectionStageRowState.Complete,
                false,
                MeasuredFraction(state, row)))
            .ToArray();
        motionEnabled = uiSettings.AnimationsEnabled;
        progressSequence.Observe(
            estimateOwner!, ++estimateRevision, observations,
            motionEnabled);
        estimateEpoch = progressSequence.Epoch;
        RefreshEstimatedValues();
        estimatePresenter ??= new DispatcherProgressPresenter(
            this, RefreshEstimatedValues, TimeSpan.FromMilliseconds(16));
        estimatePresenter.Start();
        RequestAutomationProjectionRefresh();
    }

    private void RefreshEstimatedValues()
    {
        if (CurrentState is not { Kind: HardwareInspectionPresentationKind.Active } state
            || Rows.Count != 7 || estimateEpoch != progressSequence.Epoch) return;
        SerializedProgressFrame frame = progressSequence.GetFrame();
        if (frame.Stages.Count != 7) return;
        int settled = 0;
        for (int index = 0; index < 7; index++)
        {
            Rows[index].Update(index + 1, state.StageRows[index], frame.Stages[index],
                index == frame.ActiveIndex ? frame.ActiveFraction : 0,
                motionEnabled);
            if (frame.Stages[index] is SerializedProgressStageState.Completed
                or SerializedProgressStageState.NotNeeded) settled++;
        }
        if (frame.ActiveIndex >= 0)
        {
            HardwareInspectionStageCopy copy = HardwareInspectionCopyCatalog.Stage(
                (HardwareInspectionStage)frame.ActiveIndex);
            TitleTextBlock.Text = copy.Title;
            BodyTextBlock.Text = frame.ActiveIndex < Rows.Count
                ? Rows[frame.ActiveIndex].Sentence
                : copy.ActiveExplanation;
        }
        AnnounceDisplayedStage(frame, state);
        CountTextBlock.Text = $"{settled} of 7 stages complete";
        OverallProgressBar.IsIndeterminate = false;
        OverallProgressBar.Value = frame.OverallValue;
        string overallPercentage = $"{Math.Min(100m, Math.Floor((decimal)frame.OverallValue * 100m / 7m)):0}%";
        string estimatedOverall = $"Estimated {overallPercentage}";
        if (OverallPercentage.Text != overallPercentage)
        {
            OverallPercentage.Text = overallPercentage;
        }
        AutomationProperties.SetItemStatus(
            OverallProgressBar, estimatedOverall);
        RequestAutomationProjectionRefresh();
        if (!frame.NeedsTicks)
        {
            estimatePresenter?.Stop();
            if (frame.IsFinished && successfulHandoffPending)
            {
                successfulHandoffPending = false;
                SuccessfulPresentationReady?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    internal void BeginSuccessfulPresentationHandoff()
    {
        if (CurrentState is not { Kind: HardwareInspectionPresentationKind.Active }
            || estimateOwner is null)
        {
            SuccessfulPresentationReady?.Invoke(this, EventArgs.Empty);
            return;
        }

        successfulHandoffPending = true;
        SerializedProgressStageObservation[] observations = CurrentState.StageRows
            .Select(_ => new SerializedProgressStageObservation(false, true, false))
            .ToArray();
        progressSequence.Observe(
            estimateOwner, ++estimateRevision, observations, motionEnabled);
        estimateEpoch = progressSequence.Epoch;
        RefreshEstimatedValues();
        estimatePresenter ??= new DispatcherProgressPresenter(
            this, RefreshEstimatedValues, TimeSpan.FromMilliseconds(16));
        estimatePresenter.Start();
    }

    internal void AbortProgressPresentation()
    {
        successfulHandoffPending = false;
        estimatePresenter?.Stop();
        progressSequence.Abort();
        estimateEpoch = progressSequence.Epoch;
        displayedStage = -1;
        lastAnnouncedDisplayedStage = -1;
        checked { announcementGeneration++; }
    }

    private void AnnounceDisplayedStage(
        SerializedProgressFrame frame,
        HardwareInspectionPresentationState state)
    {
        displayedStage = frame.ActiveIndex;
        if (!managedLoaded || displayedStage < 0
            || displayedStage == lastAnnouncedDisplayedStage) return;
        lastAnnouncedDisplayedStage = displayedStage;
        int stage = displayedStage;
        long epoch = estimateEpoch;
        long generation = checked(++announcementGeneration);
        string announcement =
            $"Hardware inspection. {state.StageRows[stage].Title}. Step {stage + 1} of 7.";
        AutomationProperties.SetName(this, announcement);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (!managedLoaded || generation != announcementGeneration
                || epoch != progressSequence.Epoch || displayedStage != stage
                || CurrentState?.Kind != HardwareInspectionPresentationKind.Active) return;
            AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(this)
                ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        });
    }

    private static double? MeasuredFraction(
        HardwareInspectionPresentationState state,
        HardwareInspectionStageRow row)
    {
        if (row.State != HardwareInspectionStageRowState.Active
            || row.Stage < HardwareInspectionStage.ReadingProcessorInformation
            || row.Stage > HardwareInspectionStage.CheckingLocalInferenceRuntimes
            || !state.GroupChecks.TryGetValue(row.Stage, out var check)
            || check.Total <= 0 || check.Completed < 0 || check.Completed > check.Total)
            return null;
        return (double)check.Completed / check.Total;
    }

    internal void ApplyStopping(HardwareInspectionPresentationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Kind is not HardwareInspectionPresentationKind.Stopping
            and not HardwareInspectionPresentationKind.Cancelled)
        {
            throw new ArgumentException("Stopping or cancelled presentation required.", nameof(state));
        }

        bool isCancelled = state.Kind == HardwareInspectionPresentationKind.Cancelled;
        CurrentState = state;
        if (!isCancelled)
        {
            successfulHandoffPending = false;
            estimatePresenter?.Stop();
            checked { announcementGeneration++; }
            foreach (HardwareInspectionProgressRowViewData row in Rows)
            {
                row.FreezeActiveAnimation();
            }

            KickerTextBlock.Text = state.Kicker;
            TitleTextBlock.Text = state.Title;
            BodyTextBlock.Text = state.Body;
            string frozenPercentage = OverallPercentage.Text;
            string stoppingStatus = string.IsNullOrWhiteSpace(frozenPercentage)
                ? "Hardware inspection stopping."
                : $"Hardware inspection stopping at {frozenPercentage}.";
            AutomationProperties.SetName(this, stoppingStatus);
            AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Off);
            AutomationProperties.SetItemStatus(
                OverallProgressBar,
                string.IsNullOrWhiteSpace(frozenPercentage)
                    ? "Stopping"
                    : $"Stopping at {frozenPercentage}");
            return;
        }

        KickerTextBlock.Text = isCancelled ? string.Empty : state.Kicker;
        TitleTextBlock.Text = isCancelled ? "Hardware inspection cancelled" : state.Title;
        BodyTextBlock.Text = isCancelled ? "No hardware report was created." : state.Body;

        if (Rows.Count != 7)
        {
            Rows.Clear();
            foreach (HardwareInspectionStage stage in Enum.GetValues<HardwareInspectionStage>())
            {
                HardwareInspectionStageCopy copy = HardwareInspectionCopyCatalog.Stage(stage);
                Rows.Add(new HardwareInspectionProgressRowViewData(
                    (int)stage + 1,
                    new HardwareInspectionStageRow(
                        stage,
                        HardwareInspectionStageRowState.Waiting,
                        copy.Title,
                        copy.WaitingSentence,
                        $"Waiting — {copy.Title}. {copy.WaitingSentence} State: Waiting.")));
            }
        }

        if (isCancelled)
        {
            int interruptedIndex = Rows
                .Select((row, index) => (row, index))
                .Where(entry => entry.row.ActiveVisibility == Visibility.Visible)
                .Select(entry => entry.index)
                .DefaultIfEmpty(-1)
                .First();
            if (interruptedIndex < 0)
            {
                interruptedIndex = Rows
                    .Select((row, index) => (row, index))
                    .Where(entry => entry.row.CompletedVisibility != Visibility.Visible)
                    .Select(entry => entry.index)
                    .DefaultIfEmpty(0)
                    .First();
            }

            for (int index = 0; index < Rows.Count; index++)
            {
                Rows[index].ApplyCancelledOutcome(index == interruptedIndex);
            }
        }

        CountTextBlock.Text = isCancelled ? "Cancelled" : "Stopping safely";
        AbortProgressPresentation();
        OverallProgressBar.Visibility = Visibility.Collapsed;
        OverallProgressBar.Value = 0;
        OverallPercentage.Visibility = isCancelled
            ? Visibility.Collapsed
            : Visibility.Visible;
        AutomationProperties.SetItemStatus(
            OverallProgressBar,
            isCancelled ? "Cancelled" : "Stopping");
        AutomationProperties.SetName(
            this,
            "Hardware inspection cancelled. No hardware report was created.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        CountLabelTextBlock.Text = string.Empty;

        RequestAutomationProjectionRefresh();
    }

    private void ProgressRowsItemsControl_ContainerContentChanging(
        ListViewBase sender,
        ContainerContentChangingEventArgs eventArguments)
    {
        if (!managedLoaded)
        {
            return;
        }

        if (eventArguments.ItemContainer is not ListViewItem container ||
            eventArguments.Item is not HardwareInspectionProgressRowViewData row)
        {
            return;
        }

        int position = Rows.IndexOf(row) + 1;
        if (position <= 0)
        {
            return;
        }

        ApplyAutomationProjection(container, row, position);
    }

    private void HardwareInspectionProgressCard_Loaded(
        object sender,
        RoutedEventArgs eventArguments)
    {
        managedLoaded = true;
        uiSettings.AnimationsEnabledChanged -= UiSettings_AnimationsEnabledChanged;
        uiSettings.AnimationsEnabledChanged += UiSettings_AnimationsEnabledChanged;
        if (CurrentState is { Kind: HardwareInspectionPresentationKind.Active } state)
        {
            Apply(state);
        }
        checked
        {
            automationProjectionGeneration++;
        }
        automationProjectionPending = false;
        pendingAutomationProjectionGeneration = -1;
        RequestAutomationProjectionRefresh();
    }

    private void HardwareInspectionProgressCard_Unloaded(
        object sender,
        RoutedEventArgs eventArguments)
    {
        managedLoaded = false;
        uiSettings.AnimationsEnabledChanged -= UiSettings_AnimationsEnabledChanged;
        AbortProgressPresentation();
        OverallProgressBar.Value = 0;
        checked
        {
            automationProjectionGeneration++;
        }
        automationProjectionPending = false;
        pendingAutomationProjectionGeneration = -1;
    }

    private void UiSettings_AnimationsEnabledChanged(
        Windows.UI.ViewManagement.UISettings sender,
        Windows.UI.ViewManagement.UISettingsAnimationsEnabledChangedEventArgs args)
    {
        long generation = automationProjectionGeneration;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (!managedLoaded || generation != automationProjectionGeneration
                || CurrentState is not { Kind: HardwareInspectionPresentationKind.Active } state) return;
            motionEnabled = uiSettings.AnimationsEnabled;
            if (successfulHandoffPending) BeginSuccessfulPresentationHandoff();
            else Apply(state);
        });
    }

    private void ProgressRowsItemsControl_LayoutUpdated(object? sender, object eventArguments)
    {
        if (!managedLoaded)
        {
            automationProjectionPending = false;
            pendingAutomationProjectionGeneration = -1;
            return;
        }

        if (automationProjectionPending)
        {
            RefreshRealizedAutomationProjection(
                pendingAutomationProjectionGeneration);
        }
    }

    private void RequestAutomationProjectionRefresh()
    {
        if (!managedLoaded)
        {
            automationProjectionPending = false;
            pendingAutomationProjectionGeneration = -1;
            return;
        }

        long generation = automationProjectionGeneration;
        if (automationProjectionPending &&
            pendingAutomationProjectionGeneration == generation)
        {
            return;
        }

        automationProjectionPending = true;
        pendingAutomationProjectionGeneration = generation;
        if (!DispatcherQueue.TryEnqueue(
                () => RefreshRealizedAutomationProjection(generation)))
        {
            ClearPendingAutomationProjection(generation);
        }
    }

    private void RefreshRealizedAutomationProjection(long generation)
    {
        if (!managedLoaded || generation != automationProjectionGeneration)
        {
            ClearPendingAutomationProjection(generation);
            return;
        }

        ClearPendingAutomationProjection(generation);
        bool allRowsRealized = Rows.Count > 0;
        for (int index = 0; index < Rows.Count; index++)
        {
            if (ProgressRowsItemsControl.ContainerFromIndex(index) is ListViewItem container)
            {
                ApplyAutomationProjection(container, Rows[index], index + 1);
            }
            else
            {
                allRowsRealized = false;
            }
        }

        if (!allRowsRealized &&
            managedLoaded &&
            generation == automationProjectionGeneration)
        {
            automationProjectionPending = true;
            pendingAutomationProjectionGeneration = generation;
        }
    }

    private void ClearPendingAutomationProjection(long generation)
    {
        if (pendingAutomationProjectionGeneration != generation)
        {
            return;
        }

        automationProjectionPending = false;
        pendingAutomationProjectionGeneration = -1;
    }

    private void ApplyAutomationProjection(
        ListViewItem container,
        HardwareInspectionProgressRowViewData row,
        int position)
    {
        if (!managedLoaded)
        {
            return;
        }

        AutomationProperties.SetName(container, row.AccessibleName);
        AutomationProperties.SetItemStatus(container, row.Status);
        AutomationProperties.SetPositionInSet(container, position);
        AutomationProperties.SetSizeOfSet(container, Rows.Count);
    }
}

internal sealed class HardwareInspectionProgressRowViewData : INotifyPropertyChanged
{
    internal void FreezeActiveAnimation()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
    }

    internal bool SetProgressDetail(string detail)
    {
        string display = $"Checking\n{detail}";
        if (DisplayStatus == display) return false;
        DisplayStatus = display;
        Status = $"Checking · {detail}";
        AccessibleName = $"{Title}. {Status}. Step {Number} of 7.";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        return true;
    }
    internal HardwareInspectionProgressRowViewData(int number, HardwareInspectionStageRow row, int? completed = null, int? total = null)
    {
        Update(number, row, completed, total);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Update(int number, HardwareInspectionStageRow row, int? completed = null, int? total = null)
    {
        Number = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Title = row.Title;
        Sentence = row.State == HardwareInspectionStageRowState.Waiting
            ? string.Empty : row.Sentence;
        Status = row.State switch
        {
            HardwareInspectionStageRowState.Complete => "Complete",
            HardwareInspectionStageRowState.Active => "Checking · Working…",
            HardwareInspectionStageRowState.Waiting => "Waiting",
            _ => throw new ArgumentOutOfRangeException(nameof(row)),
        };
        DisplayStatus = Status;
        if (row.State == HardwareInspectionStageRowState.Active)
        {
            string detail = "Working…";
            // Collection completion samples are emitted only in this stage.
            // Retained counters cannot quantify subsequent normalisation/report work.
            if (row.Stage == HardwareInspectionStage.CheckingLocalInferenceRuntimes &&
                completed.HasValue && total is > 0 && completed >= 0 && completed <= total)
            {
                detail = $"{Math.Round(100d * completed.Value / total.Value):0}%";
                Status = $"Checking · {completed} of {total} checks · {detail}";
            }
            DisplayStatus = $"Checking\n{detail}";
        }
        AccessibleName = $"{Title}. {Status}. Step {number} of 7.";
        CompletedVisibility = row.State == HardwareInspectionStageRowState.Complete
            ? Visibility.Visible
            : Visibility.Collapsed;
        ActiveVisibility = row.State == HardwareInspectionStageRowState.Active
            ? Visibility.Visible
            : Visibility.Collapsed;
        WaitingVisibility = row.State == HardwareInspectionStageRowState.Waiting
            ? Visibility.Visible
            : Visibility.Collapsed;
        CancelledVisibility = Visibility.Collapsed;
        IsActive = row.State == HardwareInspectionStageRowState.Active;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    internal void Update(
        int number,
        HardwareInspectionStageRow row,
        SerializedProgressStageState state,
        double fraction,
        bool animate)
    {
        Number = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Title = row.Title;
        Sentence = state switch
        {
            SerializedProgressStageState.Active =>
                row.State == HardwareInspectionStageRowState.Active
                    ? row.Sentence
                    : HardwareInspectionCopyCatalog.Stage(row.Stage).ActiveExplanation,
            SerializedProgressStageState.Completed or SerializedProgressStageState.NotNeeded => row.Sentence,
            _ => string.Empty,
        };
        string percentage = $"{Math.Min(100m, Math.Floor((decimal)fraction * 100m)):0}%";
        string estimatedPercentage = $"Estimated {percentage}";
        Status = state switch
        {
            SerializedProgressStageState.Completed => "Complete",
            SerializedProgressStageState.NotNeeded => "Not needed",
            SerializedProgressStageState.Active => $"Checking · {estimatedPercentage}",
            _ => "Waiting",
        };
        DisplayStatus = state == SerializedProgressStageState.Active
            ? $"Checking\n{percentage}" : Status;
        AccessibleName = $"{Title}. {Status}. Step {number} of 7.";
        CompletedVisibility = state is SerializedProgressStageState.Completed
            or SerializedProgressStageState.NotNeeded ? Visibility.Visible : Visibility.Collapsed;
        ActiveVisibility = state == SerializedProgressStageState.Active
            ? Visibility.Visible : Visibility.Collapsed;
        WaitingVisibility = state == SerializedProgressStageState.Waiting
            ? Visibility.Visible : Visibility.Collapsed;
        CancelledVisibility = Visibility.Collapsed;
        IsActive = state == SerializedProgressStageState.Active && animate;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    internal void ApplyCancelledOutcome(bool isInterruptedStage)
    {
        if (CompletedVisibility == Visibility.Visible)
        {
            CancelledVisibility = Visibility.Collapsed;
            IsActive = false;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            return;
        }

        Status = isInterruptedStage ? "Cancelled" : "Not run";
        DisplayStatus = Status;
        Sentence = isInterruptedStage
            ? "This check was cancelled."
            : "This check was not run.";
        AccessibleName = $"{Title}. {Status}. Step {Number} of 7.";
        CompletedVisibility = Visibility.Collapsed;
        ActiveVisibility = Visibility.Collapsed;
        WaitingVisibility = isInterruptedStage
            ? Visibility.Collapsed
            : Visibility.Visible;
        CancelledVisibility = isInterruptedStage
            ? Visibility.Visible
            : Visibility.Collapsed;
        IsActive = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public string Number { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Sentence { get; private set; } = string.Empty;
    public string AccessibleName { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string DisplayStatus { get; private set; } = string.Empty;
    public Visibility CompletedVisibility { get; private set; }
    public Visibility ActiveVisibility { get; private set; }
    public Visibility WaitingVisibility { get; private set; }
    public Visibility CancelledVisibility { get; private set; }
    public bool IsActive { get; private set; }
}
