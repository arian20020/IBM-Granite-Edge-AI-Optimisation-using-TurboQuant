using System;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelOptimization.Controls;

public sealed partial class OptimizationConfirmationCard : UserControl
{
    public OptimizationConfirmationCard() => InitializeComponent();

    internal event EventHandler? BackRequested;

    internal event EventHandler? ConfirmRequested;

    internal void Apply(OptimizationPresentationState presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ConfirmationSummary.Text = presentation.Summary;
        ConfirmationConfiguration.Apply(presentation.Configuration);
    }

    private void OnBackClicked(object sender, RoutedEventArgs args) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void OnConfirmClicked(object sender, RoutedEventArgs args) =>
        ConfirmRequested?.Invoke(this, EventArgs.Empty);
}
