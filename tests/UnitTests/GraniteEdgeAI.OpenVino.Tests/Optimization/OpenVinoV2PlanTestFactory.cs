using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

namespace GraniteEdgeAI.OpenVino.Tests.Optimization;

/// <summary>
/// Builds frozen C1 V2 plans for route-adapter tests after the shared preference
/// resolver moved to V3-only admission proofs. Production V2 issuance still
/// validates candidate/payload agreement; only the now-internal legacy selection
/// construction is reproduced here.
/// </summary>
internal static class OpenVinoV2PlanTestFactory
{
    private static readonly ConstructorInfo SelectionConstructor =
        typeof(OptimizationSelection).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [
                typeof(OptimizationCandidate),
                typeof(OptimizationPreferenceSelection),
                typeof(bool)
            ],
            modifiers: null)
        ?? throw new InvalidOperationException(
            "The frozen V2 selection constructor is unavailable.");

    internal static OptimizationExecutionPlan Issue(
        OptimizationCandidate candidate,
        OptimizationExecutionPayload executionPayload,
        OptimizationCapabilitySnapshot capabilitySnapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        int modelLayerCount,
        DateTimeOffset createdAtUtc)
    {
        OptimizationPreferenceSelection preference =
            OptimizationPreferenceSelection.Manual(50);
        OptimizationSelection selection = (OptimizationSelection)
            SelectionConstructor.Invoke([candidate, preference, false]);

        return OptimizationPlanIssuer.Issue(
            selection,
            executionPayload,
            capabilitySnapshot,
            workload,
            binding,
            modelLayerCount,
            createdAtUtc);
    }
}
