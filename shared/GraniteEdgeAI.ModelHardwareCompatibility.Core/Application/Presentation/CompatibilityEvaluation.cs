using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Planning;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

/// <summary>
/// One compatibility decision together with any authority that can safely
/// support the next action. Non-actionable decisions carry neither authority.
/// </summary>
public sealed record CompatibilityEvaluation(
    CompatibilityScreenModel Screen,
    CompatibilityPlanningSession? PlanningSession,
    CurrentCompatibleConfiguration? CurrentConfiguration);

/// <summary>
/// Exact path-free runtime configuration behind a current-model fit decision.
/// It is optional until a route supplies the corresponding execution payload.
/// </summary>
public sealed record CurrentCompatibleConfiguration
{
    public CurrentCompatibleConfiguration(
        OptimizationRoute route,
        OptimizationExecutionPayload exactExecutionPayload,
        string runtimeConfigurationSha256,
        string compatibilityDecisionId)
    {
        ArgumentNullException.ThrowIfNull(exactExecutionPayload);
        if (!Enum.IsDefined(route) || exactExecutionPayload.Route != route)
        {
            throw new ArgumentException(
                "The current execution payload must match the established route.",
                nameof(exactExecutionPayload));
        }
        if (!OptimizationDigest.IsCanonical(runtimeConfigurationSha256))
        {
            throw new ArgumentException(
                "The runtime configuration identity must be a canonical SHA-256.",
                nameof(runtimeConfigurationSha256));
        }
        OptimizationIdentifier.Require(
            compatibilityDecisionId,
            nameof(compatibilityDecisionId),
            "The compatibility decision");

        Route = route;
        ExactExecutionPayload = exactExecutionPayload;
        RuntimeConfigurationSha256 = runtimeConfigurationSha256;
        CompatibilityDecisionId = compatibilityDecisionId;
    }

    public OptimizationRoute Route { get; }
    public OptimizationExecutionPayload ExactExecutionPayload { get; }
    public string RuntimeConfigurationSha256 { get; }
    public string CompatibilityDecisionId { get; }
}
