using System.Collections.ObjectModel;
using System.Collections.Frozen;

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
    private readonly FrozenSet<string> optedInExperimentalEvidenceIds;

    private CompatibilityPlanningSession(
        OptimizationRoute route,
        IReadOnlyList<OptimizationCandidate> frontier,
        OptimizationCapabilitySnapshot capabilitySnapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        int modelLayerCount,
        IReadOnlySet<string> optedInExperimentalEvidenceIds)
    {
        Route = route;
        this.frontier = Array.AsReadOnly([.. frontier]);
        this.capabilitySnapshot = capabilitySnapshot;
        this.workload = workload;
        this.binding = binding;
        this.modelLayerCount = modelLayerCount;
        this.optedInExperimentalEvidenceIds =
            optedInExperimentalEvidenceIds.ToFrozenSet(StringComparer.Ordinal);
    }

    public OptimizationRoute Route { get; }

    /// <summary>
    /// Returns the exact evidence identity required by the selected preference,
    /// or null when the resolved candidate is fully released. No provider text
    /// or candidate object crosses this boundary.
    /// </summary>
    public string? RequiredExperimentalEvidenceId(
        OptimizationPreferenceSelection preference)
    {
        ArgumentNullException.ThrowIfNull(preference);
        OptimizationSelection selection =
            OptimizationPreferenceResolver.Resolve(frontier, preference)
            ?? throw new InvalidOperationException(
                "The retained frontier no longer contains an admitted selection.");
        return selection.Candidate.AdmissionProof?.OptedInEvidenceId;
    }

    public bool MatchesIssuedPlan(
        OptimizationExecutionPlan? plan,
        OptimizationPreferenceSelection preference)
    {
        ArgumentNullException.ThrowIfNull(preference);
        if (plan is null
            || plan.ContractVersion
                != OptimizationExecutionPlan.CurrentContractVersion
            || !plan.IsExecutableBy(
                OptimizationExecutionPlan.CurrentContractVersion)
            || plan.Route != Route
            || plan.Preference != preference
            || plan.Binding != binding
            || plan.Workload != workload
            || !plan.MatchesCapability(capabilitySnapshot))
        {
            return false;
        }

        OptimizationSelection? selected =
            OptimizationPreferenceResolver.Resolve(frontier, preference);
        if (selected is null
            || plan.Candidate != selected.Candidate
            || plan.SharedWithAdjacentBand != selected.SharedWithAdjacentBand)
        {
            return false;
        }
        return plan.Candidate.AdmissionProof?.OptedInEvidenceId is not { } evidenceId
            || optedInExperimentalEvidenceIds.Contains(evidenceId);
    }

    internal static CompatibilityPlanningSession? Create(
        OptimizationRoute route,
        IReadOnlyList<OptimizationCandidate> candidates,
        OptimizationCapabilitySnapshot capabilitySnapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        int modelLayerCount,
        IReadOnlySet<string> optedInExperimentalEvidenceIds)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(capabilitySnapshot);
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(optedInExperimentalEvidenceIds);

        if (!Enum.IsDefined(route)
            || capabilitySnapshot.Route != route
            || modelLayerCount < 1
            || candidates.Count == 0)
        {
            return null;
        }

        foreach (OptimizationCandidate candidate in candidates)
        {
            OptimizationAdmissionProof? proof = candidate.AdmissionProof;
            if (candidate.Route != route
                || proof is null
                || !proof.MatchesCandidate(candidate)
                || !proof.MatchesAuthority(
                    capabilitySnapshot, workload, binding)
                || proof.OptedInEvidenceId is { } evidenceId
                    && !optedInExperimentalEvidenceIds.Contains(evidenceId)
                || !candidate.Metrics.FitsSafely
                || !candidate.Metrics.FitsDiskSafely)
            {
                return null;
            }
        }

        return new CompatibilityPlanningSession(
            route, candidates, capabilitySnapshot, workload, binding,
            modelLayerCount, optedInExperimentalEvidenceIds);
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
        if (selection.Candidate.AdmissionProof?.OptedInEvidenceId is { } evidenceId
            && !optedInExperimentalEvidenceIds.Contains(evidenceId))
        {
            throw new InvalidOperationException(
                "The selected experimental evidence is not admitted by this planning session.");
        }
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
