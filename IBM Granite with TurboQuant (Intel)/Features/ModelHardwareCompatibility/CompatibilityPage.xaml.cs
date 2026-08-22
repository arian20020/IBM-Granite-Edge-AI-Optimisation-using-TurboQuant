using System;
using System.Collections.Generic;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility;

/// <summary>
/// Renders one compatibility presentation onto a stable control tree.
///
/// The tree is built once by XAML and never rebuilt; applying a snapshot writes
/// values into the controls that already exist. That is what keeps the page from
/// flickering between states and what lets a later snapshot change one card
/// without disturbing the rest. There is no business logic here — the page shows
/// what it is given and decides nothing.
/// </summary>
internal sealed partial class CompatibilityPage : Page
{
    private static readonly string[] StepLabels =
    [
        "Choose model",
        "Inspect model",
        "Check hardware fit",
        "Choose settings",
        "Finish"
    ];

    private CompatibilityPresentation _presentation = CompatibilityPresentation.Empty;

    public CompatibilityPage()
    {
        InitializeComponent();
        BuildStepper();
        Apply(CompatibilityPresentation.Empty);

        ViewModel.PresentationChanged += (_, presentation) => Apply(presentation);
        PrimaryAction.Command = ViewModel.ContinueCommand;
        SecondaryAction.Command = ViewModel.BackCommand;

        // One automatic attempt per navigation: arriving here starts the check,
        // because that is the only reason to be on this page.
        Loaded += async (_, _) =>
        {
            if (StartAutomatically)
            {
                await ViewModel.StartAsync();
            }
        };
    }

    /// <summary>
    /// False only when something else is driving what this page shows — the
    /// fixture gallery, which would otherwise have its chosen state immediately
    /// replaced by a real attempt.
    /// </summary>
    internal bool StartAutomatically { get; set; } = true;

    /// <summary>
    /// Owned by the page for the lifetime of one navigation, so a check started
    /// here cannot outlive the screen that asked for it.
    /// </summary>
    internal ViewModels.CompatibilityViewModel ViewModel { get; } = new();

    /// <summary>
    /// Applies a snapshot. Safe to call with the same value twice.
    /// </summary>
    internal void Apply(CompatibilityPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        _presentation = presentation;

        PageTitleText.Text = presentation.PageTitle;
        PageLedeText.Text = presentation.PageLede;

        ModelNameText.Text = presentation.ModelName;
        ModelDetailText.Text = presentation.ModelDetail;

        ApplyOutcome(presentation);
        ApplyFacts(presentation.Facts);

        RuntimeCardTitleText.Text = presentation.RuntimeCardTitle;
        ApplyRows(RuntimeRows, presentation.RuntimeRows);

        ChecksCardTitleText.Text = presentation.ChecksCardTitle;
        ApplyRows(CheckRows, presentation.CheckRows);

        ApplyRecoveries(presentation.Recoveries);

        DisclosureTitleText.Text = presentation.DisclosureTitle;
        DisclosureDetailText.Text = presentation.DisclosureDetail;

        PrimaryAction.Content = presentation.PrimaryActionText;
        PrimaryAction.IsEnabled = presentation.PrimaryActionEnabled;
        SecondaryAction.Content = presentation.SecondaryActionText;
        SecondaryAction.IsEnabled = presentation.SecondaryActionEnabled;

        ApplyStepper(presentation.ActiveStepIndex);
    }

    private void ApplyOutcome(CompatibilityPresentation presentation)
    {
        (Brush surface, Brush border, Brush accent, string glyph) = presentation.Tone switch
        {
            CompatibilityOutcomeTone.Positive => (
                Brush("CompatibilitySuccessSurfaceBrush"),
                Brush("CompatibilitySuccessBorderBrush"),
                Brush("CompatibilitySuccessTextBrush"),
                "✓"),
            CompatibilityOutcomeTone.Caution => (
                Brush("CompatibilityWarningSurfaceBrush"),
                Brush("CompatibilityWarningBorderBrush"),
                Brush("CompatibilityWarningAccentBrush"),
                "!"),
            CompatibilityOutcomeTone.Blocking => (
                Brush("CompatibilityErrorSurfaceBrush"),
                Brush("CompatibilityErrorBorderBrush"),
                Brush("CompatibilityErrorTextBrush"),
                "!"),
            _ => (
                Brush("CompatibilityBlueSurfaceBrush"),
                Brush("CompatibilityBlueBorderBrush"),
                Brush("CompatibilityPrimaryBlueBrush"),
                "i")
        };

        OutcomeCard.Background = surface;
        OutcomeCard.BorderBrush = border;
        OutcomeGlyphHost.Background = accent;
        OutcomeGlyphText.Text = glyph;

        OutcomeTitleText.Text = presentation.OutcomeTitle;
        OutcomeDetailText.Text = presentation.OutcomeDetail;

        OutcomeBadgeText.Text = presentation.OutcomeBadge;
        OutcomeBadgeText.Foreground = accent;
        OutcomeBadgeHost.BorderBrush = border;
        OutcomeBadgeHost.Visibility = string.IsNullOrEmpty(presentation.OutcomeBadge)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ApplyFacts(IReadOnlyList<CompatibilityFact> facts)
    {
        FactsGrid.Children.Clear();

        for (int index = 0; index < facts.Count && index < 4; index++)
        {
            CompatibilityFact fact = facts[index];

            // The detail sits in its own bottom-aligned row so every tile's
            // detail line rests on the same baseline, whatever the value above
            // it wraps to.
            Grid content = new()
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
                }
            };

            TextBlock label = new()
            {
                Text = fact.Label.ToUpperInvariant(),
                FontSize = Size("CompatibilityFactLabelFontSize"),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = Brush("CompatibilityTextMutedBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            TextBlock value = new()
            {
                Text = fact.Value,
                FontSize = Size("CompatibilityFactValueFontSize"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("CompatibilityTextPrimaryBrush"),
                Margin = new Thickness(0, 5, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            TextBlock detail = new()
            {
                Text = fact.Detail,
                FontSize = Size("CompatibilityFactDetailFontSize"),
                Foreground = Brush("CompatibilityTextMutedBrush"),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };

            Grid.SetRow(label, 0);
            Grid.SetRow(value, 1);
            Grid.SetRow(detail, 2);
            content.Children.Add(label);
            content.Children.Add(value);
            content.Children.Add(detail);

            Border tile = new()
            {
                Background = Brush("CompatibilityCanvasBrush"),
                BorderBrush = Brush("CompatibilityBorderLightBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = Radius("CompatibilityFactRadius"),
                Padding = Pad("CompatibilityFactPadding"),
                Child = content
            };

            Grid.SetColumn(tile, index % 2);
            Grid.SetRow(tile, index / 2);
            FactsGrid.Children.Add(tile);
        }
    }

    private void ApplyRows(Panel host, IReadOnlyList<CompatibilityRow> rows)
    {
        host.Children.Clear();

        foreach (CompatibilityRow row in rows)
        {
            Grid line = new()
            {
                ColumnSpacing = 8,
                Padding = new Thickness(0, 5, 0, 5),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            StackPanel text = new();

            text.Children.Add(new TextBlock
            {
                Text = row.Title,
                FontSize = Size("CompatibilityRowTitleFontSize"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("CompatibilityTextPrimaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            if (!string.IsNullOrEmpty(row.Subtitle))
            {
                text.Children.Add(new TextBlock
                {
                    Text = row.Subtitle,
                    FontSize = Size("CompatibilityRowSubFontSize"),
                    Foreground = Brush("CompatibilityTextMutedBrush"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            Grid.SetColumn(text, 0);
            line.Children.Add(text);

            FrameworkElement trailing = row.ShowPill
                ? BuildPill(row)
                : new TextBlock
                {
                    Text = row.Value,
                    FontSize = Size("CompatibilityRowTitleFontSize"),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = Brush("CompatibilityTextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                };

            Grid.SetColumn(trailing, 1);
            line.Children.Add(trailing);

            host.Children.Add(line);
        }
    }

    private Border BuildPill(CompatibilityRow row)
    {
        (Brush surface, Brush border, Brush accent) = row.Tone switch
        {
            CompatibilityOutcomeTone.Positive => (
                Brush("CompatibilitySuccessSurfaceBrush"),
                Brush("CompatibilitySuccessBorderBrush"),
                Brush("CompatibilitySuccessTextBrush")),
            CompatibilityOutcomeTone.Caution => (
                Brush("CompatibilityWarningSurfaceBrush"),
                Brush("CompatibilityWarningBorderBrush"),
                Brush("CompatibilityWarningAccentBrush")),
            CompatibilityOutcomeTone.Blocking => (
                Brush("CompatibilityErrorSurfaceBrush"),
                Brush("CompatibilityErrorBorderBrush"),
                Brush("CompatibilityErrorTextBrush")),
            _ => (
                Brush("CompatibilityBlueSurfaceBrush"),
                Brush("CompatibilityBlueBorderBrush"),
                Brush("CompatibilityPrimaryBlueBrush"))
        };

        return new Border
        {
            Background = surface,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = Radius("CompatibilityPillRadius"),
            Padding = new Thickness(7, 4, 7, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = row.Value,
                FontSize = Size("CompatibilityPillFontSize"),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = accent
            }
        };
    }

    private void ApplyRecoveries(IReadOnlyList<CompatibilityRecovery> recoveries)
    {
        RecoveryRows.Children.Clear();

        RecoveryCard.Visibility = recoveries.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;

        foreach (CompatibilityRecovery recovery in recoveries)
        {
            StackPanel item = new() { Margin = new Thickness(0, 0, 0, 8) };

            item.Children.Add(new TextBlock
            {
                Text = recovery.Title,
                FontSize = Size("CompatibilityRowTitleFontSize"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("CompatibilityTextPrimaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });

            item.Children.Add(new TextBlock
            {
                Text = recovery.Detail,
                FontSize = Size("CompatibilityOutcomeBodyFontSize"),
                Foreground = Brush("CompatibilityTextMutedBrush"),
                TextWrapping = TextWrapping.Wrap
            });

            RecoveryRows.Children.Add(item);
        }
    }

    private void BuildStepper()
    {
        for (int index = 0; index < StepLabels.Length; index++)
        {
            StackPanel step = new();

            Border dot = new()
            {
                CornerRadius = new CornerRadius(10),
                Height = 19,
                Width = 19,
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    FontSize = 7.5,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                }
            };

            step.Children.Add(dot);
            step.Children.Add(new TextBlock
            {
                Text = StepLabels[index],
                FontSize = Size("CompatibilityStepLabelFontSize"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });

            Grid.SetColumn(step, index);
            StepperSteps.Children.Add(step);
        }
    }

    private void ApplyStepper(int activeIndex)
    {
        for (int index = 0; index < StepperSteps.Children.Count; index++)
        {
            if (StepperSteps.Children[index] is not StackPanel step
                || step.Children[0] is not Border dot
                || step.Children[1] is not TextBlock label)
            {
                continue;
            }

            bool reached = index <= activeIndex;

            dot.Background = reached
                ? Brush("CompatibilityPrimaryBlueBrush")
                : Brush("CompatibilitySurfaceBrush");
            dot.BorderBrush = reached
                ? Brush("CompatibilityPrimaryBlueBrush")
                : Brush("CompatibilityBorderStrongBrush");

            if (dot.Child is TextBlock number)
            {
                number.Foreground = reached
                    ? Brush("CompatibilitySurfaceBrush")
                    : Brush("CompatibilityTextMutedBrush");
            }

            label.Foreground = index == activeIndex
                ? Brush("CompatibilityTextPrimaryBrush")
                : Brush("CompatibilityTextMutedBrush");
        }
    }

    private Brush Brush(string key) =>
        CompatibilityResources.Brush(this, key);

    private double Size(string key) =>
        CompatibilityResources.Value(this, key, 10d);

    private Thickness Pad(string key) =>
        CompatibilityResources.Value(this, key, new Thickness(8));

    private CornerRadius Radius(string key) =>
        CompatibilityResources.Value(this, key, new CornerRadius(8));
}
