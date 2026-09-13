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
        DetailsExpander.RegisterPropertyChangedCallback(
            Expander.IsExpandedProperty,
            (_, _) => UpdateDisclosureLabels());
        TechnicalExpander.RegisterPropertyChangedCallback(
            Expander.IsExpandedProperty,
            (_, _) => UpdateDisclosureLabels());
        UpdateDisclosureLabels();
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
        UpdateDisclosureLabels();
    }

    private void UpdateDisclosureLabels()
    {
        DetailsActionTextBlock.Text = DetailsExpander.IsExpanded
            ? "Hide details"
            : "Show details";
        TechnicalActionTextBlock.Text = TechnicalExpander.IsExpanded
            ? "Hide IT details"
            : "Show IT details";
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
            "Completed" or "Report created" => "\uE73E",
            "Completed with note" or "Could not check" => "\uE7BA",
            "Could not confirm" or "Stopped here" => "\uE711",
            "Cancelled here" or "Not started" or "Not used in report" => "\uE738",
            _ => "\uE946",
        };
    }

    public string Title { get; }
    public string Sentence { get; }
    public string Status { get; }
    public string AccessibleName { get; }
    public string Glyph { get; }
}
