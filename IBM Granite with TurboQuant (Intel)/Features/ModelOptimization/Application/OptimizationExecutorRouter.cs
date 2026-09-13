using System;
using System.Collections.Generic;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Journey;

internal sealed class OptimizationExecutorRouter
{
    private readonly IReadOnlyDictionary<OptimizationRoute, IOptimizationExecutor>
        _executors;

    internal OptimizationExecutorRouter(IEnumerable<IOptimizationExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);
        Dictionary<OptimizationRoute, IOptimizationExecutor> mapped = [];
        foreach (IOptimizationExecutor executor in executors)
        {
            ArgumentNullException.ThrowIfNull(executor);
            if (!Enum.IsDefined(executor.Route)
                || !mapped.TryAdd(executor.Route, executor))
            {
                throw new ArgumentException(
                    "Each optimisation route must have exactly one executor.",
                    nameof(executors));
            }
        }
        _executors = mapped;
    }

    internal bool TryResolve(
        OptimizationRoute route,
        out IOptimizationExecutor? executor) =>
        _executors.TryGetValue(route, out executor);
}
