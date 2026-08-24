using System;
using System.Collections.Generic;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelOptimization.Controls;

public sealed partial class OptimizationDestinationCard : UserControl
{
    public OptimizationDestinationCard()
    {
        InitializeComponent();
        DestinationCore.ActionRequested += actionId => ActionRequested?.Invoke(actionId);
    }

    internal event Action<string>? ActionRequested;

    internal string PrimaryActionText => DestinationCore.VisibleActionTexts[0];

    internal string SecondaryActionText => DestinationCore.VisibleActionTexts[1];

    internal void Apply(OptimizationPresentationState presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (presentation.Kind is not OptimizationPageStateKind.SucceededPersistent
            and not OptimizationPageStateKind.SucceededRuntimeProfile)
        {
            throw new ArgumentException("Destination cards require a successful result.", nameof(presentation));
        }

        DestinationCore.Apply(presentation);
    }
}
