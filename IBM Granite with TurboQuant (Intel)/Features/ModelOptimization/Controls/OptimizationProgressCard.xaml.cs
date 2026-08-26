using System;
using System.Collections.Generic;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GraniteEdgeAI.Features.ModelOptimization.Controls;

public sealed partial class OptimizationProgressCard : UserControl
{
    private readonly List<FrameworkElement> _progressRows = [];
    private readonly List<string> _progressRowTitles = [];

    public OptimizationProgressCard() => InitializeComponent();

    internal event EventHandler? CancelRequested;

    internal IReadOnlyList<FrameworkElement> ProgressRowElements => _progressRows;

    internal IReadOnlyList<string> ProgressRowTitles => _progressRowTitles;

    internal void Apply(OptimizationPresentationState presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ProgressRowsHost.Children.Clear();
        _progressRows.Clear();
        _progressRowTitles.Clear();

        foreach (OptimizationProgressRow progress in presentation.ProgressRows)
        {
            Grid row = CreateRow(progress);
            ProgressRowsHost.Children.Add(row);
            _progressRows.Add(row);
            _progressRowTitles.Add(progress.Title);
        }

        CancelButton.Visibility = presentation.CanCancel ? Visibility.Visible : Visibility.Collapsed;
    }

    private Grid CreateRow(OptimizationProgressRow progress)
    {
        Grid row = new()
        {
            MinHeight = 64,
            Padding = new Thickness(12, 8, 12, 8),
            ColumnSpacing = 12,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = (Brush)Resources["OptimizationSurfaceSubtleBrush"],
            CornerRadius = new CornerRadius(10),
            Tag = "progress-row"
        };
        row.ColumnDefinitions.Add(new() { Width = new GridLength(28) });
        row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });

        TextBlock glyph = new()
        {
            Text = Glyph(progress.Status),
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(glyph, 0);

        StackPanel copy = new()
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2
        };
        copy.Children.Add(new TextBlock
        {
            Text = progress.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        copy.Children.Add(new TextBlock { Text = progress.Description, TextWrapping = TextWrapping.WrapWholeWords });
        Grid.SetColumn(copy, 1);

        TextBlock status = new()
        {
            Text = Status(progress.Status),
            MaxWidth = 88,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(status, 2);

        row.Children.Add(glyph);
        row.Children.Add(copy);
        row.Children.Add(status);
        AutomationProperties.SetName(row, $"{progress.Title}: {Status(progress.Status)}. {progress.Description}");
        return row;
    }

    private static string Glyph(OptimizationStageStatus status) => status switch
    {
        OptimizationStageStatus.Completed => "✓",
        OptimizationStageStatus.Active => "●",
        OptimizationStageStatus.NotApplicable => "—",
        _ => "○"
    };

    private static string Status(OptimizationStageStatus status) => status switch
    {
        OptimizationStageStatus.Completed => "Complete",
        OptimizationStageStatus.Active => "In progress",
        OptimizationStageStatus.NotApplicable => "Not needed",
        _ => "Waiting"
    };

    private void OnCancelClicked(object sender, RoutedEventArgs args) =>
        CancelRequested?.Invoke(this, EventArgs.Empty);
}
