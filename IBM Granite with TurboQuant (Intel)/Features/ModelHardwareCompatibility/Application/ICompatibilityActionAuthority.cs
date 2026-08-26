using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;

internal interface ICompatibilityActionAuthority
{
    bool TryGetOptimizationAuthority(
        OptimizationRoute route,
        out IOptimizationExecutionPayloadComposer? composer,
        out OptimizationIssuanceAuthority? issuanceAuthority);

    bool IsCurrentModelChatAvailable(OptimizationRoute route);
}

internal sealed class UnavailableCompatibilityActionAuthority
    : ICompatibilityActionAuthority
{
    internal static UnavailableCompatibilityActionAuthority Instance { get; } =
        new();

    private UnavailableCompatibilityActionAuthority() { }

    public bool TryGetOptimizationAuthority(
        OptimizationRoute route,
        out IOptimizationExecutionPayloadComposer? composer,
        out OptimizationIssuanceAuthority? issuanceAuthority)
    {
        composer = null;
        issuanceAuthority = null;
        return false;
    }

    public bool IsCurrentModelChatAvailable(OptimizationRoute route) => false;
}
