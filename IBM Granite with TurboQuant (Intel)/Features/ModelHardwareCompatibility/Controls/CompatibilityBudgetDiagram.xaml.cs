using System;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Controls;

/// <summary>
/// Draws the memory budget.
///
/// The bar's bands are proportional to real byte counts, so they are built here
/// rather than in markup. Two rules from the design carry the meaning:
///
/// A reserve is hatched rather than coloured, because it is memory held back for
/// Windows and the user's other applications, not memory this model wants.
/// Colouring it as a requirement would say the model is bigger than it is.
///
/// When the requirement overruns the safe limit, the bar rescales to the
/// requirement so the overflow is drawn instead of clipped. A bar that stopped
/// at the limit would hide how far over it is, which is the one figure that
/// tells the user whether closing something would be enough.
/// </summary>
internal sealed partial class CompatibilityBudgetDiagram : UserControl
{
    public static readonly DependencyProperty BudgetProperty =
        DependencyProperty.Register(
            nameof(Budget),
            typeof(CompatibilityBudget),
            typeof(CompatibilityBudgetDiagram),
            new PropertyMetadata(null, OnBudgetChanged));

    public static readonly DependencyProperty ShowLegendProperty =
        DependencyProperty.Register(
            nameof(ShowLegend),
            typeof(bool),
            typeof(CompatibilityBudgetDiagram),
            new PropertyMetadata(false, OnBudgetChanged));

    public CompatibilityBudgetDiagram()
    {
        InitializeComponent();

        // A safe, non-committal default so the control can exist before any
        // figures do, without ever drawing a bar that implies one.
        Budget = CompatibilityBudget.Empty;
    }

    internal CompatibilityBudget Budget
    {
        get => (CompatibilityBudget)GetValue(BudgetProperty);
        set => SetValue(BudgetProperty, value);
    }

    /// <summary>
    /// False on the main surface, where brackets answer the question in three
    /// words. True inside the calculation disclosure, where the full breakdown
    /// belongs.
    /// </summary>
    public bool ShowLegend
    {
        get => (bool)GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    private static void OnBudgetChanged(
        DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is CompatibilityBudgetDiagram diagram)
        {
            diagram.Render();
        }
    }

    private void Render()
    {
        CompatibilityBudget budget = Budget ?? CompatibilityBudget.Empty;

        RenderVerdict(budget);
        RenderTrack(budget);
        RenderLimit(budget);
        RenderBrackets(budget);
        RenderLegend(budget);

        ScaleRightText.Text = CompatibilityBudget.Describe(budget.ScaleBytes);

        FooterText.Text = budget.Segments.Count == 0
            ? string.Empty
            : budget.Fits
                ? $"The biggest part of this is {budget.LimitingComponent.ToLowerInvariant()}."
                : $"What pushes this over is {budget.LimitingComponent.ToLowerInvariant()}.";
    }

    private void RenderVerdict(CompatibilityBudget budget)
    {
        if (budget.Segments.Count == 0)
        {
            VerdictText.Text = string.Empty;
            DiagramNote.Text = string.Empty;
            return;
        }

        VerdictText.Text = budget.Fits ? "Fits" : "Does not fit";
        VerdictText.Foreground = budget.Fits
            ? Brush("CompatibilitySuccessTextBrush")
            : Brush("CompatibilityErrorTextBrush");

        DiagramNote.Text = budget.Fits
            ? $"About {CompatibilityBudget.Describe(budget.RequiredBytes)} of the "
              + $"{CompatibilityBudget.Describe(budget.SafeLimitBytes)} we can safely use."
            : $"About {CompatibilityBudget.Describe(budget.RequiredBytes)}, which is more "
              + $"than the {CompatibilityBudget.Describe(budget.SafeLimitBytes)} we can "
              + "safely use.";
    }

    private void RenderTrack(CompatibilityBudget budget)
    {
        TrackHost.Children.Clear();
        TrackHost.ColumnDefinitions.Clear();

        if (budget.Segments.Count == 0)
        {
            return;
        }

        int column = 0;

        foreach (CompatibilityBudgetSegment segment in budget.Segments)
        {
            if (segment.Bytes == 0)
            {
                continue;
            }

            TrackHost.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(segment.Bytes, GridUnitType.Star)
            });

            Border band = new()
            {
                Background = segment.IsReserve
                    ? ReserveBrush()
                    : Brush("CompatibilityPrimaryBlueBrush"),
                BorderBrush = Brush("CompatibilitySurfaceBrush"),
                BorderThickness = new Thickness(column == 0 ? 0 : 1, 0, 0, 0)
            };

            ToolTipService.SetToolTip(
                band,
                $"{segment.Label}: {CompatibilityBudget.Describe(segment.Bytes)}");

            Grid.SetColumn(band, column);
            TrackHost.Children.Add(band);
            column++;
        }
    }

    /// <summary>
    /// The reserve is hatched rather than filled, so it reads as "held back"
    /// rather than "used by the model".
    /// </summary>
    private Brush ReserveBrush()
    {
        LinearGradientBrush hatch = new()
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0.04, 0.04),
            SpreadMethod = GradientSpreadMethod.Repeat
        };

        Color line = ((SolidColorBrush)Brush("CompatibilityBorderStrongBrush")).Color;
        Color ground = ((SolidColorBrush)Brush("CompatibilityCanvasBrush")).Color;

        hatch.GradientStops.Add(new GradientStop { Color = ground, Offset = 0.0 });
        hatch.GradientStops.Add(new GradientStop { Color = ground, Offset = 0.6 });
        hatch.GradientStops.Add(new GradientStop { Color = line, Offset = 0.61 });
        hatch.GradientStops.Add(new GradientStop { Color = line, Offset = 1.0 });

        return hatch;
    }

    private void RenderLimit(CompatibilityBudget budget)
    {
        double fraction = budget.ScaleBytes == 0
            ? 0
            : (double)budget.SafeLimitBytes / budget.ScaleBytes;

        fraction = Math.Clamp(fraction, 0, 1);

        LimitLeadColumn.Width = new GridLength(fraction, GridUnitType.Star);
        LimitTrailColumn.Width = new GridLength(1 - fraction, GridUnitType.Star);
        FlagLeadColumn.Width = new GridLength(fraction, GridUnitType.Star);
        FlagTrailColumn.Width = new GridLength(1 - fraction, GridUnitType.Star);

        bool visible = budget.Segments.Count > 0 && budget.SafeLimitBytes > 0;

        LimitRule.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        FlagHost.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        LimitRule.Fill = Brush("CompatibilityTextPrimaryBrush");
        FlagText.Text = "Safe limit";
    }

    /// <summary>
    /// The main-surface treatment: two spans that answer "does it fit" without
    /// asking the reader to compare numbers.
    /// </summary>
    private void RenderBrackets(CompatibilityBudget budget)
    {
        BracketHost.Children.Clear();
        BracketHost.ColumnDefinitions.Clear();

        if (budget.Segments.Count == 0 || budget.ScaleBytes == 0)
        {
            return;
        }

        double needed = Math.Clamp((double)budget.RequiredBytes / budget.ScaleBytes, 0, 1);

        BracketHost.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(needed, GridUnitType.Star)
        });
        BracketHost.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(Math.Max(1 - needed, 0.0001), GridUnitType.Star)
        });

        TextBlock neededLabel = new()
        {
            Text = $"Needs {CompatibilityBudget.Describe(budget.RequiredBytes)}",
            FontSize = 7.5,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = Brush("CompatibilityTextPrimaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        Grid.SetColumn(neededLabel, 0);
        BracketHost.Children.Add(neededLabel);

        TextBlock spareLabel = new()
        {
            Text = budget.Fits
                ? $"{CompatibilityBudget.Describe(budget.SafeLimitBytes - budget.RequiredBytes)} spare"
                : $"{CompatibilityBudget.Describe(budget.RequiredBytes - budget.SafeLimitBytes)} over",
            FontSize = 7.5,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = budget.Fits
                ? Brush("CompatibilitySuccessTextBrush")
                : Brush("CompatibilityErrorTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        Grid.SetColumn(spareLabel, 1);
        BracketHost.Children.Add(spareLabel);
    }

    private void RenderLegend(CompatibilityBudget budget)
    {
        LegendHost.Children.Clear();
        LegendHost.ColumnDefinitions.Clear();

        LegendHost.Visibility = ShowLegend && budget.Segments.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (LegendHost.Visibility == Visibility.Collapsed)
        {
            return;
        }

        int column = 0;

        foreach (CompatibilityBudgetSegment segment in budget.Segments)
        {
            LegendHost.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            StackPanel key = new();

            key.Children.Add(new Rectangle
            {
                Height = 4,
                RadiusX = 2,
                RadiusY = 2,
                Fill = segment.IsReserve
                    ? ReserveBrush()
                    : Brush("CompatibilityPrimaryBlueBrush")
            });

            key.Children.Add(new TextBlock
            {
                Text = segment.Label.ToUpperInvariant(),
                FontSize = 7.5,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = Brush("CompatibilityTextMutedBrush"),
                Margin = new Thickness(0, 6, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            key.Children.Add(new TextBlock
            {
                Text = CompatibilityBudget.Describe(segment.Bytes),
                FontSize = 10.5,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("CompatibilityTextPrimaryBrush"),
                Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            Border host = new()
            {
                Background = Brush("CompatibilityCanvasBrush"),
                BorderBrush = Brush("CompatibilityBorderLightBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(9, 8, 9, 8),
                Child = key
            };

            Grid.SetColumn(host, column);
            LegendHost.Children.Add(host);
            column++;
        }
    }

    private Brush Brush(string key) => CompatibilityResources.Brush(this, key);
}
