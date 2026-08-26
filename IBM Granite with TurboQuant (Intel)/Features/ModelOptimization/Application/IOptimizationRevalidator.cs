using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Journey;

internal sealed record OptimizationRevalidationResult(
    bool IsCurrent,
    OptimizationSupportCode SupportCode)
{
    internal static OptimizationRevalidationResult Current { get; } =
        new(true, OptimizationSupportCode.None);
}

internal interface IOptimizationRevalidator
{
    Task<OptimizationRevalidationResult> RevalidateAsync(
        OptimizationExecutionPlan plan,
        CancellationToken cancellationToken);
}
