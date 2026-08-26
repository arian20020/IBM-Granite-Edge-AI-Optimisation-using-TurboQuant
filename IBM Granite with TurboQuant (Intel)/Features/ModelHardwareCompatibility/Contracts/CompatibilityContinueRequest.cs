namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;

internal abstract record CompatibilityContinueRequest
{
    private CompatibilityContinueRequest() { }

    internal sealed record ChatCurrent(CurrentModelLaunchHandoff Handoff)
        : CompatibilityContinueRequest;

    internal sealed record Optimize(OptimizationSelectionHandoff Handoff)
        : CompatibilityContinueRequest;
}
