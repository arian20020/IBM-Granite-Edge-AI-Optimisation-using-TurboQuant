using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;

public sealed partial class HardwareInspectionSummaryCard : UserControl
{
    public HardwareInspectionSummaryCard()
    {
        InitializeComponent();
    }

    internal IReadOnlyList<HardwareFactPresentation> FactItems { get; private set; } =
        Array.Empty<HardwareFactPresentation>();

    internal void Apply(HardwareSummaryPresentation summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        FactItems = summary.Facts;
        FactsPanel.Children.Clear();
        string? currentGroup = null;
        foreach (HardwareFactPresentation fact in summary.Facts)
        {
            if (!string.Equals(currentGroup, fact.Group, StringComparison.Ordinal))
            {
                currentGroup = fact.Group;
                FactsPanel.Children.Add(new TextBlock
                {
                    Text = currentGroup,
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Resources["HardwareInspectionTextPrimaryBrush"],
                });
            }

            StackPanel tile = new() { Spacing = 2 };
            tile.Children.Add(new TextBlock
            {
                Text = fact.Label,
                FontSize = 12,
                Foreground = (Brush)Resources["HardwareInspectionTextMutedBrush"],
                TextWrapping = TextWrapping.Wrap,
            });
            tile.Children.Add(new TextBlock
            {
                Text = fact.Value,
                FontSize = 14,
                Foreground = (Brush)Resources["HardwareInspectionTextPrimaryBrush"],
                TextWrapping = TextWrapping.WrapWholeWords,
            });
            FactsPanel.Children.Add(tile);
        }
    }
}
