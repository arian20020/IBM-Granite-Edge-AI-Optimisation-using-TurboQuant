using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;

internal sealed record HardwareInspectionRecoveryItem(
    string Marker,
    string Title,
    string Body,
    string? Status = null);

public sealed partial class HardwareInspectionRecoveryCard : UserControl
{
    public HardwareInspectionRecoveryCard()
    {
        InitializeComponent();
    }

    internal ObservableCollection<HardwareInspectionRecoveryItem> Items { get; } = [];

    internal void Apply(HardwareInspectionPresentationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Items.Clear();
        switch (state.Kind)
        {
            case HardwareInspectionPresentationKind.FailedCriticalEvidence:
                SetHeader("What was and was not confirmed", "Compatibility needs reliable processor and memory information.");
                Items.Add(new("✓", "Processor", "Processor information was collected successfully.", "Checked"));
                Items.Add(new("×", "Installed memory", "The available sources did not provide one reliable value.", "Not confirmed"));
                Items.Add(new("–", "Remaining stages", "Some later information was retained only for safe diagnostics.", "Incomplete"));
                break;
            case HardwareInspectionPresentationKind.FailedTransientOperation:
                SetHeader("What you can do", "Start with the quickest recovery step.");
                Items.Add(new("1", "Try again", "A temporary Windows or application problem may clear on a new run."));
                Items.Add(new("2", "Restart the application", "If the problem returns, open Inspection details, then Technical information for IT."));
                break;
            case HardwareInspectionPresentationKind.FailedApplicationRepairRequired:
                SetHeader("What to do next", "The approved local component needs attention before another inspection.");
                Items.Add(new("1", "Ask your organisation's IT support for help", "They may need to repair or reinstall Granite Edge AI from the approved package."));
                Items.Add(new("2", "Share the safe support details", "Open Inspection details, then Technical information for IT. Private paths and raw output stay hidden."));
                break;
            case HardwareInspectionPresentationKind.Cancelled:
                SetHeader("What you can do", "Run the inspection again whenever you are ready, or return to the model results.");
                Items.Add(new("i", "No hardware conclusion was made", "Partial information from this run is kept only for safe diagnostics and cannot be sent to compatibility."));
                break;
            case HardwareInspectionPresentationKind.Stopping:
                SetHeader("Why this may take a moment", "The app confirms that every inspection process has stopped before it reports the run as cancelled.");
                Items.Add(new("i", "You do not need to do anything", "This state changes automatically when cleanup is confirmed."));
                break;
            default:
                throw new ArgumentException("State does not use recovery guidance.", nameof(state));
        }
    }

    private void SetHeader(string heading, string helper)
    {
        RecoveryHeadingTextBlock.Text = heading;
        RecoveryHelperTextBlock.Text = helper;
    }
}
