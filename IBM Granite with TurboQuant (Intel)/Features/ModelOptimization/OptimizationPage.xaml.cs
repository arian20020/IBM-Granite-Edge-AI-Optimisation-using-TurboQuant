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
        ConfirmationCard.BackRequested += OnBackRequested;
        ConfirmationCard.ConfirmRequested += OnConfirmRequested;
        ProgressCard.CancelRequested += OnCancelRequested;
    }

    internal event EventHandler<OptimizationIntentEventArgs>? IntentRequested;

    internal OptimizationPresentationState? Presentation => _presentation;

    internal void ApplyPresentation(OptimizationPresentationState presentation)
    {
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));

        PageTitle.Text = presentation.Title;
        PageSummary.Text = presentation.Summary;
        PreferenceCard.Apply(presentation.Preference
            ?? throw new ArgumentException("Optimisation presentation requires a preference.", nameof(presentation)));
        ConfigurationCard.Apply(presentation.Configuration);
        ConfirmationCard.Apply(presentation);
        ProgressCard.Apply(presentation);

        bool selecting = presentation.Kind == OptimizationPageStateKind.Selecting;
        bool confirming = presentation.Kind == OptimizationPageStateKind.Confirming;
        bool running = presentation.Kind == OptimizationPageStateKind.Running;
        PreferenceCard.Visibility = selecting ? Visibility.Visible : Visibility.Collapsed;
        ConfigurationCard.Visibility = selecting ? Visibility.Visible : Visibility.Collapsed;
        ReviewConfigurationButton.Visibility = selecting ? Visibility.Visible : Visibility.Collapsed;
        ConfirmationCard.Visibility = confirming ? Visibility.Visible : Visibility.Collapsed;
        ProgressCard.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
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

    private void OnBackRequested(object? sender, EventArgs args) =>
        RaiseIntent(OptimizationIntentKind.BackRequested);

    private void OnConfirmRequested(object? sender, EventArgs args) =>
        RaiseIntent(OptimizationIntentKind.ConfirmRequested);

    private void OnCancelRequested(object? sender, EventArgs args) =>
        RaiseIntent(OptimizationIntentKind.CancelRequested);

    private void RaiseIntent(OptimizationIntentKind kind) =>
        IntentRequested?.Invoke(this, new OptimizationIntentEventArgs(kind, _presentation?.Preference));
}
