using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;

public sealed partial class HardwareInspectionDetailsCard : UserControl
{
    public HardwareInspectionDetailsCard()
    {
        InitializeComponent();
    }

    internal ObservableCollection<HardwareInspectionDetailRowViewData> DetailRows { get; } = [];
    internal ObservableCollection<HardwareInspectionTechnicalGroup> TechnicalGroups { get; } = [];

    internal void Apply(HardwareInspectionDetailsState state, bool preserveDisclosureState)
    {
        ArgumentNullException.ThrowIfNull(state);
        bool outerExpanded = preserveDisclosureState && DetailsExpander.IsExpanded;
        bool technicalExpanded = preserveDisclosureState && TechnicalExpander.IsExpanded;

        DetailsHelperTextBlock.Text = state.Helper;
        SummaryTextBlock.Text = state.Summary;
        ReportDescriptionTextBlock.Text = state.ReportDescription;
        ReportBadgeTextBlock.Text = state.ReportBadge;
        DetailRows.Clear();
        foreach (HardwareInspectionDetailRow row in state.Rows)
        {
            DetailRows.Add(new HardwareInspectionDetailRowViewData(row));
        }
        TechnicalGroups.Clear();
        foreach (HardwareInspectionTechnicalGroup group in state.TechnicalGroups)
        {
            TechnicalGroups.Add(group);
        }

        DetailsExpander.IsExpanded = outerExpanded;
        TechnicalExpander.IsExpanded = technicalExpanded;
    }
}

internal sealed class HardwareInspectionDetailRowViewData
{
    internal HardwareInspectionDetailRowViewData(HardwareInspectionDetailRow row)
    {
        Title = row.Title;
        Sentence = row.Sentence;
        Status = row.Status;
        AccessibleName = row.AccessibleName;
        Glyph = row.Status switch
        {
            "Completed" or "Report created" => "✓",
            "Completed with note" or "Could not check" => "!",
            "Could not confirm" or "Stopped here" => "×",
            "Cancelled here" or "Not started" or "Not used in report" => "–",
            _ => "•",
        };
    }

    public string Title { get; }
    public string Sentence { get; }
    public string Status { get; }
    public string AccessibleName { get; }
    public string Glyph { get; }
}
