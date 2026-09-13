using System;
using System.Collections.Generic;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelOptimization.Controls;

public sealed partial class OptimizationRecoveryCard : UserControl
{
    public OptimizationRecoveryCard()
    {
        InitializeComponent();
        RecoveryCore.ActionRequested += actionId => ActionRequested?.Invoke(actionId);
    }

    internal event Action<OptimizationCommand>? ActionRequested;

    internal IReadOnlyList<string> VisibleActionTexts => RecoveryCore.VisibleActionTexts;

    internal string VisibleSupportCode => RecoveryCore.VisibleSupportCode;

    internal void Apply(OptimizationPresentationState presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (presentation.Kind is OptimizationPageStateKind.Confirming
            or OptimizationPageStateKind.Running
            or OptimizationPageStateKind.SucceededPersistent
            or OptimizationPageStateKind.SucceededRuntimeProfile)
        {
            throw new ArgumentException("Recovery cards require a recoverable terminal result.", nameof(presentation));
        }

        RecoveryCore.Apply(presentation);
    }
}
