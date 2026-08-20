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

    internal ObservableCollection<HardwareInspectionDetailRow> DetailRows { get; } = [];
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
            DetailRows.Add(row);
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
