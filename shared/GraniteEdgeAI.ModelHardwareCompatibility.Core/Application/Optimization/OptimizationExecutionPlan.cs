using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// The journey identities a plan is bound to.
///
/// Serialized names are fixed by the established seams and are not C1's to
/// change: <c>modelInspectionRunId</c>, <c>modelInspectionHandoffId</c>,
/// <c>modelSha256</c>, <c>modelLengthBytes</c> and <c>productHardwareRunId</c>.
/// The C# properties are the usual PascalCase; the wire names stay exact.
///
/// An executor recomputes the source identity and matches every binding
/// immediately before it runs. That is the whole point of collecting them here:
/// a plan that was valid when it was issued may not be valid when it executes,
/// and the difference has to be detectable rather than assumed away.
/// </summary>
public sealed record OptimizationJourneyBinding
{
    private OptimizationJourneyBinding(
        string modelInspectionRunId,
        string modelInspectionHandoffId,
        string modelSha256,
        ulong modelLengthBytes,
        string productHardwareRunId,
        string hardwareSnapshotSha256)
    {
        ModelInspectionRunId = modelInspectionRunId;
        ModelInspectionHandoffId = modelInspectionHandoffId;
        ModelSha256 = modelSha256;
        ModelLengthBytes = modelLengthBytes;
        ProductHardwareRunId = productHardwareRunId;
        HardwareSnapshotSha256 = hardwareSnapshotSha256;
    }

    /// <summary>Serialized as <c>modelInspectionRunId</c>.</summary>
    public string ModelInspectionRunId { get; }

    /// <summary>Serialized as <c>modelInspectionHandoffId</c>.</summary>
    public string ModelInspectionHandoffId { get; }

    /// <summary>Serialized as <c>modelSha256</c>.</summary>
    public string ModelSha256 { get; }

    /// <summary>Serialized as <c>modelLengthBytes</c>.</summary>
    public ulong ModelLengthBytes { get; }

    /// <summary>Serialized as <c>productHardwareRunId</c>.</summary>
    public string ProductHardwareRunId { get; }

    public string HardwareSnapshotSha256 { get; }

    public static OptimizationJourneyBinding Create(
        string modelInspectionRunId,
        string modelInspectionHandoffId,
        string modelSha256,
        ulong modelLengthBytes,
        string productHardwareRunId,
        string hardwareSnapshotSha256)
    {
        OptimizationIdentifier.Require(
            modelInspectionRunId, nameof(modelInspectionRunId), "The model inspection run");
        OptimizationIdentifier.Require(
            modelInspectionHandoffId,
            nameof(modelInspectionHandoffId),
            "The model inspection handoff");
        OptimizationIdentifier.Require(
            productHardwareRunId, nameof(productHardwareRunId), "The hardware run");

        RequireDigest(modelSha256, nameof(modelSha256));
        RequireDigest(hardwareSnapshotSha256, nameof(hardwareSnapshotSha256));

        if (modelLengthBytes == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modelLengthBytes),
                modelLengthBytes,
                "A validated model has a length. Zero would match an absent file as "
                + "readily as the real one, so it cannot bind anything.");
        }

        return new OptimizationJourneyBinding(
            modelInspectionRunId,
            modelInspectionHandoffId,
            modelSha256,
            modelLengthBytes,
            productHardwareRunId,
            hardwareSnapshotSha256);
    }

    private static void RequireDigest(string value, string parameter)
    {
        if (!OptimizationDigest.IsCanonical(value))
        {
            throw new ArgumentException(
                "A binding digest must be exactly 64 lowercase hex characters, "
                + "because an executor recomputes it and compares ordinally.",
                parameter);
        }
    }
}

/// <summary>
/// The exact work an executor is permitted to do, and nothing else.
///
/// Immutable after the user confirms it. If any input or capability changes,
/// the executor rejects the plan and C1 issues a new one with a new id. There
/// is no in-place amendment and no substitution: a plan the user agreed to and
/// a plan that gets executed have to be the same plan, or confirmation meant
/// nothing.
///
/// Every field an executor needs to verify is here, because a check it cannot
/// perform is a check that does not happen.
/// </summary>
public sealed record OptimizationExecutionPlan
{
    /// <summary>
    /// Bumped when the shape of a plan changes in a way an older executor could
    /// misread. An executor that does not recognise the version refuses rather
    /// than interpreting fields it may not understand.
    ///
    /// Version 2 added the route execution payload. Version 3 separates the
    /// OpenVINO cache algorithm from its precision and binds TurboQuant build
    /// identity. A version 1 plan carries no
    /// payload at all, so an executor built for 2 cannot read one as if it had
    /// one - it would have to invent every runtime setting, which is the defect
    /// this version exists to close. Construction requires a payload, so a
    /// version 1 plan cannot be produced by this assembly any more.
    /// </summary>
    public const int CurrentContractVersion = 3;

    /// <summary>
    /// The lowest version an executor built against this contract may accept.
    /// Reading a version 1 plan would mean supplying the missing execution
    /// fields from somewhere, and there is nowhere honest to get them.
    /// </summary>
    public const int MinimumExecutableContractVersion = 2;

    internal OptimizationExecutionPlan(
        int contractVersion,
        Guid optimizationPlanId,
        OptimizationJourneyBinding binding,
        OptimizationCapabilitySnapshot capabilitySnapshot,
        OptimizationWorkload workload,
        OptimizationCandidate candidate,
        OptimizationExecutionPayload executionPayload,
        OptimizationPreferenceSelection preference,
        bool sharedWithAdjacentBand,
        string configurationSha256,
        DateTimeOffset createdAtUtc)
    {
        ContractVersion = contractVersion;
        ExecutionPayload = executionPayload;
        OptimizationPlanId = optimizationPlanId;
        Binding = binding;
        CapabilitySnapshot = capabilitySnapshot;
        Workload = workload;
        Candidate = candidate;
        Preference = preference;
        SharedWithAdjacentBand = sharedWithAdjacentBand;
        ConfigurationSha256 = configurationSha256;
        CreatedAtUtc = createdAtUtc;
    }

    public int ContractVersion { get; }

    public Guid OptimizationPlanId { get; }

    public OptimizationJourneyBinding Binding { get; }

    public OptimizationCapabilitySnapshot CapabilitySnapshot { get; }

    public OptimizationWorkload Workload { get; }

    /// <summary>The complete configuration. An executor runs this or fails closed.</summary>
    public OptimizationCandidate Candidate { get; }

    /// <summary>
    /// The exact settings the route executor will use.
    ///
    /// Present on every version 2-or-later plan. Its route always equals both
    /// <see cref="Candidate"/>'s and <see cref="CapabilitySnapshot"/>'s, and
    /// every field in it is inside <see cref="ConfigurationSha256"/> - so there
    /// is nothing left for an executor to choose after the user has confirmed.
    /// </summary>
    public OptimizationExecutionPayload ExecutionPayload { get; }

    public OptimizationPreferenceSelection Preference { get; }

    /// <summary>
    /// Whether a neighbouring band would have chosen the same thing. Carried so
    /// the confirmation surface can say so truthfully rather than implying the
    /// slider position bought something.
    /// </summary>
    public bool SharedWithAdjacentBand { get; }

    /// <summary>
    /// Digest of the complete configuration, not merely its weight format.
    /// Recomputed by the executor before it runs.
    /// </summary>
    public string ConfigurationSha256 { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public OptimizationRoute Route => Candidate.Route;

    /// <summary>
    /// Whether executing this writes a new model or package. The one fact the
    /// confirmation surface must not get wrong.
    ///
    /// Read off the candidate, and plan issuance proves the payload agrees. Two
    /// places that could disagree about this would be two different promises to
    /// the user.
    /// </summary>
    public bool ProducesPersistentArtifact => Candidate.Metrics.RequiresPersistentChange;

    /// <summary>
    /// Whether an executor built against this contract may run this plan.
    ///
    /// A version below the minimum is refused outright rather than partially
    /// interpreted: the missing fields are exactly the ones that decide what
    /// runs, so filling them in would be the substitution the whole contract
    /// forbids.
    /// </summary>
    public bool IsExecutableBy(int executorContractVersion) =>
        ContractVersion >= MinimumExecutableContractVersion
        && executorContractVersion >= ContractVersion;

    /// <summary>Convenience for the shorthand the seams use.</summary>
    public string ModelInspectionRunId => Binding.ModelInspectionRunId;

    /// <summary>Convenience for the shorthand the seams use.</summary>
    public string ProductHardwareRunId => Binding.ProductHardwareRunId;

    /// <summary>
    /// Whether a snapshot taken now still matches the one this plan was built
    /// from.
    ///
    /// Capability drift never triggers substitution. An executor that found a
    /// different snapshot returns ReplanRequired and hands control back, so the
    /// user re-confirms a plan that reflects what the machine can currently do.
    /// </summary>
    public bool MatchesCapability(OptimizationCapabilitySnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);

        return current.Route == CapabilitySnapshot.Route
            && string.Equals(
                current.CapabilitySnapshotSha256,
                CapabilitySnapshot.CapabilitySnapshotSha256,
                StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether the source file is still the one that was planned against.
    ///
    /// Both the digest and the length, because a length alone is trivially
    /// collidable and a digest alone would accept a file that had been
    /// truncated and re-padded to match.
    /// </summary>
    public bool MatchesSource(string modelSha256, ulong modelLengthBytes) =>
        string.Equals(modelSha256, Binding.ModelSha256, StringComparison.Ordinal)
        && modelLengthBytes == Binding.ModelLengthBytes;
}
