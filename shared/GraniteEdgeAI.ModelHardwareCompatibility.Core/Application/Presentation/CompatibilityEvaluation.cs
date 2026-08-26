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
    CurrentCompatibleConfiguration? CurrentConfiguration)
{
    /// <summary>
    /// Exact, path-free capability evidence that may be enabled by explicit
    /// user consent. Provider text is deliberately absent.
    /// </summary>
    public IReadOnlyList<CompatibilityExperimentalConsentOption>
        ExperimentalConsentOptions { get; init; } = [];

    public CompatibilityOptimizationView? OptionalOptimization { get; init; }
}

public sealed record CompatibilityExperimentalConsentOption
{
    internal CompatibilityExperimentalConsentOption(
        OptimizationRoute route,
        string evidenceId)
    {
        if (!Enum.IsDefined(route))
        {
            throw new ArgumentOutOfRangeException(nameof(route));
        }
        OptimizationIdentifier.Require(
            evidenceId,
            nameof(evidenceId),
            "The experimental consent evidence");
        Route = route;
        EvidenceId = evidenceId;
    }

    public OptimizationRoute Route { get; }
    public string EvidenceId { get; }

    public static CompatibilityExperimentalConsentOption Create(
        OptimizationRoute route,
        string evidenceId) => new(route, evidenceId);
}

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
        if (!OptimizationDigest.IsCanonical(runtimeConfigurationSha256)
            || !string.Equals(
                runtimeConfigurationSha256,
                exactExecutionPayload.ComputeRuntimeConfigurationSha256(),
                StringComparison.Ordinal))
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
