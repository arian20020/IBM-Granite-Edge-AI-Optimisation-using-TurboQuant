using System;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;

internal enum OptimizationJourneyOrigin
{
    Required = 0,
    Optional
}

internal sealed record OptimizationJourneyEntryContext
{
    internal OptimizationJourneyEntryContext(
        OptimizationSelectionHandoff optimizationHandoff,
        OptimizationJourneyOrigin origin,
        CurrentModelLaunchHandoff? currentModelFallback)
    {
        OptimizationHandoff = optimizationHandoff
            ?? throw new ArgumentNullException(nameof(optimizationHandoff));
        if (!Enum.IsDefined(origin)
            || origin == OptimizationJourneyOrigin.Required
                && currentModelFallback is not null
            || currentModelFallback is not null
                && !BindingsAgree(optimizationHandoff, currentModelFallback))
        {
            throw new ArgumentException(
                "Only an optional optimisation journey may retain an exactly matching current-model fallback.");
        }

        Origin = origin;
        CurrentModelFallback = currentModelFallback;
    }

    internal OptimizationSelectionHandoff OptimizationHandoff { get; }
    internal OptimizationJourneyOrigin Origin { get; }
    internal CurrentModelLaunchHandoff? CurrentModelFallback { get; }

    private static bool BindingsAgree(
        OptimizationSelectionHandoff optimization,
        CurrentModelLaunchHandoff fallback) =>
        string.Equals(
            optimization.ModelInspectionRunId,
            fallback.ModelInspectionRunId.ToString("N"),
            StringComparison.Ordinal)
        && string.Equals(
            optimization.ModelInspectionHandoffId,
            fallback.ModelInspectionHandoffId.ToString("N"),
            StringComparison.Ordinal)
        && string.Equals(
            optimization.ProductHardwareRunId,
            fallback.ProductHardwareRunId.ToString("N"),
            StringComparison.Ordinal);
}
