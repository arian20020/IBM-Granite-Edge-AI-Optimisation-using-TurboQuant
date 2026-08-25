using System;
using System.Linq;
using System.Threading;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

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

    internal bool MatchesSelection(OptimizationPreferenceSelection? preference) =>
        preference is not null
        && Plan.Preference == preference
        && OptimizationPlanId == Plan.OptimizationPlanId
        && string.Equals(
            ConfigurationSha256,
            Plan.ConfigurationSha256,
            StringComparison.Ordinal);

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

internal delegate bool OptimizationHandoffIssuer(
    OptimizationPreferenceSelection preference,
    out OptimizationSelectionHandoff? handoff);

/// <summary>
/// UI-owned authority for one rendered optimisation decision. It binds the
/// plan to the exact journey and capability evidence that were current when
/// that decision was rendered; it is not an executor authority.
/// </summary>
internal sealed class OptimizationHandoffAuthority
{
    private OptimizationHandoffAuthority(
        OptimizationExecutionPlan plan,
        OptimizationJourneyBinding binding,
        OptimizationCapabilitySnapshot capability,
        OptimizationPreferenceSelection preference)
    {
        Plan = plan;
        Binding = binding;
        Capability = capability;
        Preference = preference;
    }

    private OptimizationExecutionPlan Plan { get; }

    private OptimizationJourneyBinding Binding { get; }

    private OptimizationCapabilitySnapshot Capability { get; }

    internal OptimizationPreferenceSelection Preference { get; }

    internal static bool TryCreate(
        OptimizationExecutionPlan? plan,
        OptimizationJourneyBinding? currentBinding,
        OptimizationCapabilitySnapshot? currentCapability,
        OptimizationPreferenceSelection? currentPreference,
        out OptimizationHandoffAuthority? authority)
    {
        authority = null;
        if (!OptimizationSelectionHandoff.TryCreate(
                plan,
                currentBinding,
                currentCapability,
                currentPreference,
                out _))
        {
            return false;
        }

        authority = new OptimizationHandoffAuthority(
            plan!, currentBinding!, currentCapability!, currentPreference!);
        return true;
    }

    internal bool Matches(OptimizationHandoffAuthority? current) =>
        current is not null
        && ReferenceEquals(Plan, current.Plan)
        && Plan.OptimizationPlanId == current.Plan.OptimizationPlanId
        && string.Equals(
            Plan.ConfigurationSha256,
            current.Plan.ConfigurationSha256,
            StringComparison.Ordinal)
        && Preference == current.Preference
        && BindingsAgree(Binding, current.Binding)
        && Capability.Route == current.Capability.Route
        && string.Equals(
            Capability.SnapshotId,
            current.Capability.SnapshotId,
            StringComparison.Ordinal)
        && string.Equals(
            Capability.CapabilitySnapshotSha256,
            current.Capability.CapabilitySnapshotSha256,
            StringComparison.Ordinal);

    internal bool Accepts(
        OptimizationSelectionHandoff? handoff,
        OptimizationPreferenceSelection preference)
    {
        if (handoff is null
            || preference != Preference
            || !ReferenceEquals(handoff.Plan, Plan)
            || !OptimizationSelectionHandoff.TryCreate(
                handoff.Plan,
                Binding,
                Capability,
                Preference,
                out OptimizationSelectionHandoff? revalidated)
            || revalidated is null)
        {
            return false;
        }

        return handoff.OptimizationPlanId == revalidated.OptimizationPlanId
            && string.Equals(
                handoff.ConfigurationSha256,
                revalidated.ConfigurationSha256,
                StringComparison.Ordinal)
            && string.Equals(
                handoff.ModelInspectionRunId,
                revalidated.ModelInspectionRunId,
                StringComparison.Ordinal)
            && string.Equals(
                handoff.ModelInspectionHandoffId,
                revalidated.ModelInspectionHandoffId,
                StringComparison.Ordinal)
            && string.Equals(
                handoff.ProductHardwareRunId,
                revalidated.ProductHardwareRunId,
                StringComparison.Ordinal);
    }

    private static bool BindingsAgree(
        OptimizationJourneyBinding expected,
        OptimizationJourneyBinding current) =>
        string.Equals(expected.ModelInspectionRunId, current.ModelInspectionRunId,
            StringComparison.Ordinal)
        && string.Equals(expected.ModelInspectionHandoffId,
            current.ModelInspectionHandoffId, StringComparison.Ordinal)
        && string.Equals(expected.ProductHardwareRunId,
            current.ProductHardwareRunId, StringComparison.Ordinal)
        && string.Equals(expected.ModelSha256, current.ModelSha256,
            StringComparison.Ordinal)
        && expected.ModelLengthBytes == current.ModelLengthBytes
        && string.Equals(expected.HardwareSnapshotSha256,
            current.HardwareSnapshotSha256, StringComparison.Ordinal);
}

/// <summary>
/// Makes downstream availability explicit. The unavailable state owns no
/// nullable callback and cannot issue anything; the available state requires
/// a real issuer supplied by the destination integration.
/// </summary>
internal sealed class OptimizationDestination
{
    private readonly OptimizationHandoffIssuer? _issuer;
    private readonly OptimizationHandoffAuthority? _expectedAuthority;
    private readonly Func<OptimizationHandoffAuthority?>? _currentAuthority;
    private int _invalidated;

    private OptimizationDestination(
        OptimizationHandoffAuthority? expectedAuthority,
        Func<OptimizationHandoffAuthority?>? currentAuthority,
        OptimizationHandoffIssuer? issuer)
    {
        _expectedAuthority = expectedAuthority;
        _currentAuthority = currentAuthority;
        _issuer = issuer;
    }

    internal static OptimizationDestination Unavailable { get; } =
        new(null, null, null);

    internal bool IsAvailable => _issuer is not null
        && _expectedAuthority is not null
        && _currentAuthority is not null
        && Volatile.Read(ref _invalidated) == 0;

    internal bool MatchesExpectedPreference(
        OptimizationPreferenceSelection preference) =>
        IsAvailable && preference == _expectedAuthority!.Preference;

    internal void Invalidate() =>
        Interlocked.Exchange(ref _invalidated, 1);

    internal static OptimizationDestination Available(
        OptimizationHandoffAuthority expectedAuthority,
        Func<OptimizationHandoffAuthority?> currentAuthority,
        OptimizationHandoffIssuer issuer) =>
        new(
            expectedAuthority
                ?? throw new ArgumentNullException(nameof(expectedAuthority)),
            currentAuthority
                ?? throw new ArgumentNullException(nameof(currentAuthority)),
            issuer ?? throw new ArgumentNullException(nameof(issuer)));

    internal bool TryIssue(
        OptimizationPreferenceSelection preference,
        out OptimizationSelectionHandoff? handoff)
    {
        ArgumentNullException.ThrowIfNull(preference);
        if (_issuer is null
            || _expectedAuthority is null
            || _currentAuthority is null
            || Volatile.Read(ref _invalidated) != 0)
        {
            handoff = null;
            return false;
        }

        if (preference != _expectedAuthority.Preference)
        {
            Invalidate();
            handoff = null;
            return false;
        }

        try
        {
            OptimizationHandoffAuthority? before = _currentAuthority();
            if (!_expectedAuthority.Matches(before))
            {
                Invalidate();
                handoff = null;
                return false;
            }

            if (!_issuer(preference, out handoff))
            {
                handoff = null;
                return false;
            }

            OptimizationHandoffAuthority? after = _currentAuthority();
            if (handoff is null
                || !_expectedAuthority.Matches(after)
                || !before!.Matches(after)
                || !_expectedAuthority.Accepts(handoff, preference))
            {
                Invalidate();
                handoff = null;
                return false;
            }

            return true;
        }
        catch
        {
            Invalidate();
            handoff = null;
            throw;
        }
    }
}
