using System;
using System.Collections.Generic;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.Presentation.Progress;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GraniteEdgeAI.Features.ModelOptimization.Controls;

public sealed partial class OptimizationProgressCard : UserControl
{
    private readonly List<FrameworkElement> _progressRows = [];
    private readonly List<string> _progressRowTitles = [];

    private readonly BoundedProgressEstimator estimate = new();
    private DispatcherProgressPresenter? presenter;
    private OptimizationPresentationState? current;
    private object estimateOwner = new();
    private TextBlock? activeStatus;
    private ListViewItem? activeRow;
    private long revision;

    public OptimizationProgressCard()
    {
        InitializeComponent();
        Loaded += (_, _) => { UpdateEstimate(); if (current?.CanCancel == true) presenter?.Start(); };
        Unloaded += (_, _) => { presenter?.Stop(); estimate.Reset(); };
    }

    internal event EventHandler? CancelRequested;

    internal IReadOnlyList<FrameworkElement> ProgressRowElements => _progressRows;

    internal IReadOnlyList<string> ProgressRowTitles => _progressRowTitles;

    internal void Apply(OptimizationPresentationState presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (current?.OptimizationPlanId != presentation.OptimizationPlanId)
        {
            estimateOwner = new();
            estimate.Reset();
        }
        current = presentation;
        activeStatus = null;
        activeRow = null;
        ProgressRowsHost.Items.Clear();
        _progressRows.Clear();
        _progressRowTitles.Clear();

        int setSize = presentation.ProgressRows.Count;
        for (int index = 0; index < setSize; index++)
        {
            OptimizationProgressRow progress = presentation.ProgressRows[index];
            ListViewItem row = CreateRow(progress, index + 1, setSize);
            ProgressRowsHost.Items.Add(row);
            _progressRows.Add(row);
            _progressRowTitles.Add(progress.Title);
        }

        CancelButton.Visibility = presentation.CanCancel ? Visibility.Visible : Visibility.Collapsed;
        UpdateEstimate();
        presenter ??= new DispatcherProgressPresenter(this, UpdateEstimate);
        if (presentation.CanCancel) presenter.Start();
        else { presenter.Stop(); estimate.Freeze(); }
    }

    private void UpdateEstimate()
    {
        if (current is null || activeStatus is null || activeRow is null) return;
        var item = System.Linq.Enumerable.FirstOrDefault(current.ProgressRows, row => row.IsActive);
        if (item is null) return;
        estimate.Update(estimateOwner, ++revision, item.Stage, null, false, new Windows.UI.ViewManagement.UISettings().AnimationsEnabled);
        string value = $"Estimated {Math.Floor((decimal)estimate.GetFraction() * 100m):0}%";
        string text = $"Active\n{value}";
        if (activeStatus.Text == text) return;
        activeStatus.Text = text;
        AutomationProperties.SetItemStatus(activeRow, $"Active · {value}");
        AutomationProperties.SetName(activeRow, $"{item.Title}. Active · {value}.");
    }

    private ListViewItem CreateRow(OptimizationProgressRow progress, int position, int setSize)
    {
        Grid content = new()
        {
            MinHeight = 56,
            Padding = new Thickness(8, 4, 8, 4),
            ColumnSpacing = 12,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = (Brush)Resources["OptimizationSurfaceSubtleBrush"],
            CornerRadius = new CornerRadius(8)
        };
        AutomationProperties.SetAccessibilityView(content, AccessibilityView.Raw);
        content.ColumnDefinitions.Add(new() { Width = new GridLength(28) });
        content.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new() { Width = GridLength.Auto });

        FrameworkElement indicator = CreateIndicator(progress);
        Grid.SetColumn(indicator, 0);

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

        content.Children.Add(indicator);
        content.Children.Add(copy);
        content.Children.Add(status);

        ListViewItem row = new()
        {
            MinHeight = 56,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsTabStop = false,
            Content = content,
            Tag = "progress-row"
        };
        string statusText = Status(progress.Status);
        AutomationProperties.SetName(
            row,
            $"{progress.Title}. {statusText}. Step {position} of {setSize}.");
        AutomationProperties.SetItemStatus(row, statusText);
        AutomationProperties.SetPositionInSet(row, position);
        AutomationProperties.SetSizeOfSet(row, setSize);
        if (progress.IsActive) { activeStatus = status; activeRow = row; }
        return row;
    }

    private FrameworkElement CreateIndicator(OptimizationProgressRow progress)
    {
        if (progress.Status == OptimizationStageStatus.Active)
        {
            ProgressRing ring = new()
            {
                Width = 20,
                Height = 20,
                IsActive = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetAccessibilityView(ring, AccessibilityView.Raw);
            return ring;
        }

        TextBlock glyph = new()
        {
            Text = Glyph(progress.Status),
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetAccessibilityView(glyph, AccessibilityView.Raw);
        return glyph;
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
        OptimizationStageStatus.Active => "Active",
        OptimizationStageStatus.NotApplicable => "Not applicable",
        _ => "Waiting"
    };

    private void OnCancelClicked(object sender, RoutedEventArgs args)
    {
        presenter?.Stop();
        estimate.Freeze();
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
