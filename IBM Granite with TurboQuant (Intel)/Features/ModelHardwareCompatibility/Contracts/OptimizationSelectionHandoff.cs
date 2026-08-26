using System;
using System.Linq;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Planning;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;

/// <summary>
/// The path-free, immutable boundary between compatibility and a future
/// optimisation destination. Every duplicated identity is derived from, and
/// checked against, the exact version-three plan carried by the handoff.
/// </summary>
internal sealed record OptimizationSelectionHandoff
{
    private OptimizationSelectionHandoff(OptimizationExecutionPlan plan)
    {
        ModelInspectionRunId = plan.Binding.ModelInspectionRunId;
        ModelInspectionHandoffId = plan.Binding.ModelInspectionHandoffId;
        ProductHardwareRunId = plan.Binding.ProductHardwareRunId;
        OptimizationPlanId = plan.OptimizationPlanId;
        ConfigurationSha256 = plan.ConfigurationSha256;
        Plan = plan;
    }

    public string ModelInspectionRunId { get; }

    public string ModelInspectionHandoffId { get; }

    public string ProductHardwareRunId { get; }

    public Guid OptimizationPlanId { get; }

    public string ConfigurationSha256 { get; }

    public OptimizationExecutionPlan Plan { get; }

    /// <summary>
    /// Revalidates the complete decision boundary at the moment a handoff is
    /// requested. Drift never amends a plan: the caller must issue a new plan
    /// for the current model, hardware evidence, capability and preference.
    /// </summary>
    internal static bool TryCreate(
        OptimizationExecutionPlan? plan,
        OptimizationJourneyBinding? currentBinding,
        OptimizationCapabilitySnapshot? currentCapability,
        OptimizationPreferenceSelection? currentPreference,
        out OptimizationSelectionHandoff? handoff)
    {
        handoff = null;
        if (plan is null
            || currentBinding is null
            || currentCapability is null
            || currentPreference is null
            || plan.ContractVersion != OptimizationExecutionPlan.CurrentContractVersion
            || !plan.IsExecutableBy(OptimizationExecutionPlan.CurrentContractVersion)
            || plan.OptimizationPlanId == Guid.Empty
            || plan.Binding is null
            || plan.CapabilitySnapshot is null
            || plan.Candidate is null
            || plan.ExecutionPayload is null
            || plan.Preference is null
            || !IsCanonicalDigest(plan.ConfigurationSha256)
            || plan.Route != plan.ExecutionPayload.Route
            || plan.Route != currentCapability.Route
            || !plan.MatchesCapability(currentCapability)
            || !string.Equals(
                plan.CapabilitySnapshot.SnapshotId,
                currentCapability.SnapshotId,
                StringComparison.Ordinal)
            || !plan.MatchesSource(
                currentBinding.ModelSha256,
                currentBinding.ModelLengthBytes)
            || !BindingsAgree(plan.Binding, currentBinding)
            || plan.Preference != currentPreference)
        {
            return false;
        }

        handoff = new OptimizationSelectionHandoff(plan);
        return true;
    }

    internal static bool TryCreate(
        OptimizationExecutionPlan? plan,
        CompatibilityPlanningSession? planningSession,
        OptimizationPreferenceSelection? currentPreference,
        out OptimizationSelectionHandoff? handoff)
    {
        handoff = null;
        if (plan is null
            || planningSession is null
            || currentPreference is null
            || !planningSession.MatchesIssuedPlan(plan, currentPreference)
            || !IsCanonicalDigest(plan.ConfigurationSha256))
        {
            return false;
        }

        handoff = new OptimizationSelectionHandoff(plan);
        return true;
    }

    private static bool BindingsAgree(
        OptimizationJourneyBinding planned,
        OptimizationJourneyBinding current) =>
        string.Equals(
            planned.ModelInspectionRunId,
            current.ModelInspectionRunId,
            StringComparison.Ordinal)
        && string.Equals(
            planned.ModelInspectionHandoffId,
            current.ModelInspectionHandoffId,
            StringComparison.Ordinal)
        && string.Equals(
            planned.ProductHardwareRunId,
            current.ProductHardwareRunId,
            StringComparison.Ordinal)
        && string.Equals(
            planned.ModelSha256,
            current.ModelSha256,
            StringComparison.Ordinal)
        && planned.ModelLengthBytes == current.ModelLengthBytes
        && string.Equals(
            planned.HardwareSnapshotSha256,
            current.HardwareSnapshotSha256,
            StringComparison.Ordinal);

    private static bool IsCanonicalDigest(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}
