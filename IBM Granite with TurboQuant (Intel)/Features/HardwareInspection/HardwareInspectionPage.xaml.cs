using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

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

    public HardwareInspectionPage()
    {
        InitializeComponent();
        ActiveActionCard.ActionRequested += (_, args) => ActionRequested?.Invoke(this, args);
        ActionCard.ActionRequested += (_, args) => ActionRequested?.Invoke(this, args);
    }

    public event EventHandler<HardwareInspectionActionRequestedEventArgs>? ActionRequested;

    public object? FooterContent
    {
        get => FooterPresenter.Content;
        set => FooterPresenter.Content = value;
    }

    internal HardwareInspectionPresentationState? CurrentState { get; private set; }

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
