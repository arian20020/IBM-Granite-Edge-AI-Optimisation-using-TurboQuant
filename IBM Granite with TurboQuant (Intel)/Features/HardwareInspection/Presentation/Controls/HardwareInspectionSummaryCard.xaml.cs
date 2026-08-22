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
        SizeChanged += (_, args) =>
        {
            if (args.NewSize.Width > 0)
            {
                ApplyAvailableWidth(args.NewSize.Width);
            }
        };
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
        HelperTextBlock.Visibility = string.Equals(title, "This computer", StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
        FactItems = Array.AsReadOnly(selected);
        BuildFacts(compact: ActualWidth > 0 && ActualWidth < 480);
    }

    internal void ApplyAvailableWidth(double width)
    {
        bool compact = width > 0 && width < 480;
        SummaryBorder.Padding = compact
            ? new Thickness(16)
            : new Thickness(24);
        BuildFacts(compact);
    }

    private void BuildFacts(bool compact)
    {
        FactsPanel.Children.Clear();
        FactsPanel.RowDefinitions.Clear();
        FactsPanel.ColumnDefinitions.Clear();
        int columnCount = compact ? 1 : 2;
        for (int column = 0; column < columnCount; column++)
        {
            FactsPanel.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
            });
        }

        int row = -1;
        int itemColumn = 0;
        foreach (IGrouping<string, HardwareFactPresentation> group in FactItems.GroupBy(fact => fact.Group))
        {
            if (itemColumn == 0)
            {
                row++;
                FactsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            HardwareFactPresentation primary = group.First();
            StackPanel content = new() { Spacing = 4 };
            content.Children.Add(new TextBlock
            {
                Text = group.Key.ToUpperInvariant(),
                Style = (Style)Resources["HardwareInspectionFactGroupStyle"],
            });
            content.Children.Add(new TextBlock
            {
                Text = primary.Value,
                Style = (Style)Resources["HardwareInspectionFactValueStyle"],
            });
            string helper = string.Join(
                " · ",
                group.Skip(1).Select(fact => $"{fact.Label}: {fact.Value}"));
            if (!string.IsNullOrWhiteSpace(helper))
            {
                content.Children.Add(new TextBlock
                {
                    Text = helper,
                    Style = (Style)Resources["HardwareInspectionFactHelperStyle"],
                });
            }
            Border tile = new()
            {
                Style = (Style)Resources["HardwareInspectionFactTileStyle"],
                Child = content,
            };
            Grid.SetRow(tile, row);
            Grid.SetColumn(tile, itemColumn);
            FactsPanel.Children.Add(tile);
            itemColumn = (itemColumn + 1) % columnCount;
        }
    }
}
