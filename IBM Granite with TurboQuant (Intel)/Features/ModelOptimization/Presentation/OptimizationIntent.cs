using System;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Presentation;

internal enum OptimizationIntentKind
{
    PreferenceChanged,
    ReviewConfigurationRequested,
    ConfirmRequested,
    BackRequested,
    CancelRequested,
    RetryRequested,
    ReviewAgainRequested,
    ChatRequested,
    SaveRequested
}

internal sealed class OptimizationIntentEventArgs : EventArgs
{
    internal OptimizationIntentEventArgs(
        OptimizationIntentKind kind,
        OptimizationPreferenceSelection? preference = null)
    {
        Kind = kind;
        Preference = preference;
    }

    internal OptimizationIntentKind Kind { get; }

    internal OptimizationPreferenceSelection? Preference { get; }
}
