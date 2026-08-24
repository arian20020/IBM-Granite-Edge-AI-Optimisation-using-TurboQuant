using System;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelOptimization;

public sealed partial class OptimizationPage : Page
{
    private OptimizationPresentationState? _presentation;

    public OptimizationPage()
    {
        InitializeComponent();
        PreferenceCard.PreferenceChanged += OnPreferenceChanged;
    }

    internal event EventHandler<OptimizationIntentEventArgs>? IntentRequested;

    internal OptimizationPresentationState? Presentation => _presentation;

    internal void ApplyPresentation(OptimizationPresentationState presentation)
    {
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));

        PageTitle.Text = presentation.Title;
        PageSummary.Text = presentation.Summary;
        PreferenceCard.Apply(presentation.Preference
            ?? throw new ArgumentException("Selection presentation requires a preference.", nameof(presentation)));
        ConfigurationCard.Apply(presentation.Configuration);

        bool selecting = presentation.Kind == OptimizationPageStateKind.Selecting;
        PreferenceCard.Visibility = selecting ? Visibility.Visible : Visibility.Collapsed;
        ConfigurationCard.Visibility = selecting ? Visibility.Visible : Visibility.Collapsed;
        ReviewConfigurationButton.Visibility = selecting ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPreferenceChanged(
        object? sender,
        GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.OptimizationPreferenceSelection preference) =>
        IntentRequested?.Invoke(
            this,
            new OptimizationIntentEventArgs(OptimizationIntentKind.PreferenceChanged, preference));

    private void OnReviewConfigurationClicked(object sender, RoutedEventArgs args) =>
        IntentRequested?.Invoke(
            this,
            new OptimizationIntentEventArgs(
                OptimizationIntentKind.ReviewConfigurationRequested,
                _presentation?.Preference));
}
