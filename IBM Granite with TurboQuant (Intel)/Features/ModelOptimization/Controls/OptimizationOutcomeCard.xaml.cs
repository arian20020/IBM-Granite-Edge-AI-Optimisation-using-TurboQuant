using System;
using System.Collections.Generic;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GraniteEdgeAI.Features.ModelOptimization.Controls;

public sealed partial class OptimizationOutcomeCard : UserControl
{
    private readonly List<string> _visibleActionTexts = [];

    public OptimizationOutcomeCard() => InitializeComponent();

    internal event Action<string>? ActionRequested;

    internal IReadOnlyList<string> VisibleActionTexts => _visibleActionTexts;

    internal string VisibleSupportCode { get; private set; } = string.Empty;

    internal void Apply(OptimizationPresentationState presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        OutcomeSummary.Text = presentation.Summary;
        OutcomeResult.Text = presentation.Configuration.Output;
        OutcomeNewModelCopy.Text = presentation.Configuration.NewModelCopy;
        OutcomeValidation.Text = presentation.Configuration.RouteValidation;
        OutcomeGlyph.Text = Glyph(presentation.Tone);

        VisibleSupportCode = presentation.SupportCode == OptimizationSupportCode.None
            ? string.Empty
            : presentation.SupportCode.ToString();
        SupportCodeText.Text = string.IsNullOrEmpty(VisibleSupportCode)
            ? string.Empty
            : $"Support code: {VisibleSupportCode}";
        SupportCodeText.Visibility = string.IsNullOrEmpty(VisibleSupportCode)
            ? Visibility.Collapsed
            : Visibility.Visible;

        ActionsHost.Children.Clear();
        _visibleActionTexts.Clear();
        foreach (OptimizationActionPresentation action in presentation.Actions)
        {
            Button button = CreateActionButton(action);
            ActionsHost.Children.Add(button);
            _visibleActionTexts.Add(action.Text);
        }
    }

    private Button CreateActionButton(OptimizationActionPresentation action)
    {
        Button button = new()
        {
            MinHeight = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsEnabled = action.IsEnabled,
            UseSystemFocusVisuals = true,
            Tag = action.Id
        };
        button.Content = new TextBlock
        {
            Text = action.Text,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        AutomationProperties.SetName(button, action.Text);
        if (action.IsPrimary)
        {
            button.Background = (Brush)Resources["OptimizationPrimaryBrush"];
            button.BorderBrush = (Brush)Resources["OptimizationPrimaryBrush"];
            button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        }

        button.Click += OnActionClicked;
        return button;
    }

    private static string Glyph(OptimizationPresentationTone tone) => tone switch
    {
        OptimizationPresentationTone.Success => "✓",
        OptimizationPresentationTone.Warning => "!",
        OptimizationPresentationTone.Error => "×",
        _ => "i"
    };

    private void OnActionClicked(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string actionId })
        {
            ActionRequested?.Invoke(actionId);
        }
    }
}
