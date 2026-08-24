using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

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
    /// </summary>
    public const int CurrentContractVersion = 1;

    internal OptimizationExecutionPlan(
        Guid optimizationPlanId,
        OptimizationJourneyBinding binding,
        OptimizationCapabilitySnapshot capabilitySnapshot,
        OptimizationWorkload workload,
        OptimizationCandidate candidate,
        OptimizationPreferenceSelection preference,
        bool sharedWithAdjacentBand,
        string configurationSha256,
        DateTimeOffset createdAtUtc)
    {
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

    public int ContractVersion => CurrentContractVersion;

    public Guid OptimizationPlanId { get; }

    public OptimizationJourneyBinding Binding { get; }

    public OptimizationCapabilitySnapshot CapabilitySnapshot { get; }

    public OptimizationWorkload Workload { get; }

    /// <summary>The complete configuration. An executor runs this or fails closed.</summary>
    public OptimizationCandidate Candidate { get; }

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
    /// </summary>
    public bool ProducesPersistentArtifact => Candidate.Metrics.RequiresPersistentChange;

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
