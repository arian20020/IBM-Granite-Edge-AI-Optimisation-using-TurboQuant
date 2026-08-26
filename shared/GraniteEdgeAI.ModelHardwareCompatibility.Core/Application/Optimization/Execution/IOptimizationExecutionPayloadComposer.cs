using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

/// <summary>
/// Route-owned conversion from an admitted candidate to the exact path-free
/// executor payload. The compatibility layer selects; it never invents runtime
/// defaults on behalf of a route.
/// </summary>
public interface IOptimizationExecutionPayloadComposer
{
    OptimizationRoute Route { get; }

    OptimizationExecutionPayload Compose(OptimizationCandidate candidate);
}
