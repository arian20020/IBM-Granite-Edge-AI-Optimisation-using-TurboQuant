using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;

public sealed partial class HardwareInspectionSummaryCard : UserControl
{
    public HardwareInspectionSummaryCard()
    {
        InitializeComponent();
    }

    internal IReadOnlyList<HardwareFactPresentation> FactItems { get; private set; } =
        Array.Empty<HardwareFactPresentation>();

    internal string CardTitle { get; private set; } = "This computer";

    internal void Apply(HardwareSummaryPresentation summary) =>
        Apply(summary, "This computer", includedGroups: null);

    internal void Apply(
        HardwareSummaryPresentation summary,
        string title,
        IEnumerable<string>? includedGroups)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        HashSet<string>? groupSet = includedGroups is null
            ? null
            : new HashSet<string>(includedGroups, StringComparer.Ordinal);
        HardwareFactPresentation[] selected = summary.Facts
            .Where(fact => groupSet is null || groupSet.Contains(fact.Group))
            .ToArray();
        CardTitle = title;
        TitleTextBlock.Text = title;
        FactItems = Array.AsReadOnly(selected);
        FactsPanel.Children.Clear();
        string? currentGroup = null;
        foreach (HardwareFactPresentation fact in selected)
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
