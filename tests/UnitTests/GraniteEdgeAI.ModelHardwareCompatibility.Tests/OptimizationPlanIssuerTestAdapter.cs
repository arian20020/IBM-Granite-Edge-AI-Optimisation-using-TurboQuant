global using OptimizationPlanIssuer =
    GraniteEdgeAI.ModelHardwareCompatibility.Tests.OptimizationPlanIssuerTestAdapter;

using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests;

/// <summary>
/// Migrates pre-authority contract tests through an independently reconstructed
/// standard hardware fixture. New authority-bound tests call the core issuer
/// directly with their exact current observation.
/// </summary>
internal static class OptimizationPlanIssuerTestAdapter
{
    public static OptimizationExecutionPlan Issue(
        OptimizationSelection selection,
        OptimizationExecutionPayload executionPayload,
        OptimizationCapabilitySnapshot capabilitySnapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        int modelLayerCount,
        DateTimeOffset createdAtUtc) =>
        GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization
            .OptimizationPlanIssuer.Issue(
                selection, executionPayload, capabilitySnapshot, workload,
                binding, modelLayerCount,
                OptimizationHardwareAuthorityTestData.Issuance(selection.Candidate),
                new FixedTimeProvider(createdAtUtc));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
