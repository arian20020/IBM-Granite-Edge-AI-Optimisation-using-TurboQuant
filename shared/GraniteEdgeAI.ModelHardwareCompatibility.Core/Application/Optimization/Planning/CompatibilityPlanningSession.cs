using System.Collections.ObjectModel;

using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Planning;

/// <summary>
/// Immutable, path-free authority retained from one compatibility evaluation.
/// A screen may choose a preference, but it cannot rebuild candidates or fill
/// in executor settings after the decision.
/// </summary>
public sealed class CompatibilityPlanningSession
{
    private readonly ReadOnlyCollection<OptimizationCandidate> frontier;
    private readonly OptimizationCapabilitySnapshot capabilitySnapshot;
    private readonly OptimizationWorkload workload;
    private readonly OptimizationJourneyBinding binding;
    private readonly int modelLayerCount;

    private CompatibilityPlanningSession(
        OptimizationRoute route,
        IReadOnlyList<OptimizationCandidate> frontier,
        OptimizationCapabilitySnapshot capabilitySnapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        int modelLayerCount)
    {
        Route = route;
        this.frontier = Array.AsReadOnly([.. frontier]);
        this.capabilitySnapshot = capabilitySnapshot;
        this.workload = workload;
        this.binding = binding;
        this.modelLayerCount = modelLayerCount;
    }

    public OptimizationRoute Route { get; }

    internal static CompatibilityPlanningSession? Create(
        OptimizationRoute route,
        IReadOnlyList<OptimizationCandidate> candidates,
        OptimizationCapabilitySnapshot capabilitySnapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        int modelLayerCount)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(capabilitySnapshot);
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(binding);

        if (!Enum.IsDefined(route)
            || capabilitySnapshot.Route != route
            || modelLayerCount < 1
            || candidates.Count == 0
            || candidates.Any(candidate =>
                candidate.Route != route
                || candidate.AdmissionProof is null
                || !candidate.AdmissionProof.MatchesCandidate(candidate)
                || !candidate.AdmissionProof.MatchesAuthority(
                    capabilitySnapshot, workload, binding)
                || !candidate.Metrics.FitsSafely
                || !candidate.Metrics.FitsDiskSafely))
        {
            return null;
        }

        return new CompatibilityPlanningSession(
            route, candidates, capabilitySnapshot, workload, binding,
            modelLayerCount);
    }

    public OptimizationExecutionPlan Issue(
        OptimizationPreferenceSelection preference,
        IOptimizationExecutionPayloadComposer composer,
        OptimizationIssuanceAuthority currentHardwareAuthority,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(preference);
        ArgumentNullException.ThrowIfNull(composer);
        ArgumentNullException.ThrowIfNull(currentHardwareAuthority);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (composer.Route != Route)
        {
            throw new ArgumentException(
                "The execution payload composer must own the planning session route.",
                nameof(composer));
        }

        OptimizationSelection selection =
            OptimizationPreferenceResolver.Resolve(frontier, preference)
            ?? throw new InvalidOperationException(
                "The retained frontier no longer contains an admitted selection.");
        OptimizationExecutionPayload payload = composer.Compose(selection.Candidate)
            ?? throw new InvalidOperationException(
                "The route composer returned no execution payload.");

        if (payload.Route != Route)
        {
            throw new InvalidOperationException(
                "The route composer returned a payload for a different route.");
        }

        return OptimizationPlanIssuer.Issue(
            selection,
            payload,
            capabilitySnapshot,
            workload,
            binding,
            modelLayerCount,
            currentHardwareAuthority,
            timeProvider);
    }
}
