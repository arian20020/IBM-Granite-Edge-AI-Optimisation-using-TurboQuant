using System;
using System.Linq;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Journey;

internal enum OptimizationProgressStage
{
    Preflight = 0,
    PrepareStaging,
    Optimise,
    Validate,
    SmokeTest,
    Reinspect,
    Publish
}

internal enum OptimizationProgressStatusKey
{
    Waiting = 0,
    Active,
    Completed
}

internal sealed record OptimizationProgress
{
    internal OptimizationProgress(
        long generation,
        Guid optimizationPlanId,
        string configurationSha256,
        OptimizationProgressStage stage,
        double fraction,
        OptimizationProgressStatusKey statusKey)
    {
        if (generation < 1
            || optimizationPlanId == Guid.Empty
            || configurationSha256.Length != 64
            || !configurationSha256.All(character => character is >= '0' and <= '9'
                or >= 'a' and <= 'f')
            || !Enum.IsDefined(stage)
            || !Enum.IsDefined(statusKey)
            || double.IsNaN(fraction)
            || fraction is < 0d or > 1d)
        {
            throw new ArgumentException("Optimisation progress is not canonical.");
        }

        Generation = generation;
        OptimizationPlanId = optimizationPlanId;
        ConfigurationSha256 = configurationSha256;
        Stage = stage;
        Fraction = fraction;
        StatusKey = statusKey;
    }

    internal long Generation { get; }
    internal Guid OptimizationPlanId { get; }
    internal string ConfigurationSha256 { get; }
    internal OptimizationProgressStage Stage { get; }
    internal double Fraction { get; }
    internal OptimizationProgressStatusKey StatusKey { get; }
}
