using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.TurboQuant;

public enum TurboQuantActivationDisposition
{
    Unavailable,
    Unverified,
    Active
}

public sealed record TurboQuantEvidenceRow(string Label, string Value);

public sealed record TurboQuantApprovedTuple(
    string EvidenceCommit,
    string ModelId,
    string PackageManifestSha256,
    string ModelSha256,
    long ModelLength,
    OpenVinoBuildEvidence BuildEvidence);

public sealed record TurboQuantCampaignEvidence(
    string EvidenceCommit,
    string ModelId,
    string PackageManifestSha256,
    string ModelSha256,
    long ModelLength,
    string RequestedDevice,
    IReadOnlyList<string> ActualExecutionDevices,
    OpenVinoBuildEvidence BuildEvidence,
    bool WorkerClosureVerified,
    bool SecurityReviewApproved,
    bool LicenseReviewApproved,
    TurboQuantActivationEvent? Activation,
    string QualityRubricId,
    bool MatchedOfficialBaseline,
    bool DeterministicSmokePassed,
    bool MemoryReductionPassed,
    bool QualityPassed,
    bool PerformancePassed,
    bool RepeatabilityPassed,
    bool ContextScalingPassed,
    bool CancellationPassed,
    bool CleanupPassed,
    bool CorruptionPassed,
    bool StreamingPassed,
    int CompletedTurnCount);

public sealed class TurboQuantActivationState
{
    internal TurboQuantActivationState(
        TurboQuantActivationDisposition disposition,
        string approvedPackageManifestSha256,
        string approvedModelSha256,
        long approvedModelLength,
        OpenVinoBuildEvidence approvedBuildEvidence,
        IReadOnlyList<TurboQuantEvidenceRow> evidenceRows)
    {
        Disposition = disposition;
        ApprovedPackageManifestSha256 = approvedPackageManifestSha256;
        ApprovedModelSha256 = approvedModelSha256;
        ApprovedModelLength = approvedModelLength;
        ApprovedBuildEvidence = approvedBuildEvidence;
        Maturity = "Experimental";
        EvidenceRows = Array.AsReadOnly(evidenceRows.ToArray());
    }

    public TurboQuantActivationDisposition Disposition { get; }
    public string Maturity { get; }
    public string ApprovedPackageManifestSha256 { get; }
    public string ApprovedModelSha256 { get; }
    public long ApprovedModelLength { get; }
    public OpenVinoBuildEvidence ApprovedBuildEvidence { get; }
    public IReadOnlyList<TurboQuantEvidenceRow> EvidenceRows { get; }

    public bool IsExperimentalVisible =>
        Disposition != TurboQuantActivationDisposition.Unavailable;

    public bool CanActivate =>
        Disposition == TurboQuantActivationDisposition.Active;
}

/// <summary>
/// Converts closed, path-free campaign evidence into the only three app
/// dispositions. It does not discover or infer approval from local files.
/// </summary>
public sealed class TurboQuantActivationPolicy
{
    public const string AcceptedModelId = "ibm-granite/granite-4.1-3b";
    public const string AcceptedDevice = "CPU";
    public const string SourceCommit = "8a17657b995fd3b4a52f8484acfcf2bb61214623";
    public const string ImplementationCommit = "b9a1f201c109e0bed74763934f79483cf6c4cbf4";
    public const string QualityRubricId = "GTQ-QUALITY-RUBRIC-v1";

    private readonly TurboQuantApprovedTuple approved;

    public TurboQuantActivationPolicy(TurboQuantApprovedTuple approved)
    {
        ArgumentNullException.ThrowIfNull(approved);
        RequireCommit(approved.EvidenceCommit, nameof(approved));
        if (!string.Equals(approved.ModelId, AcceptedModelId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The approved model is outside the TurboQuant slice.", nameof(approved));
        }
        RequireSha256(approved.PackageManifestSha256, nameof(approved));
        RequireSha256(approved.ModelSha256, nameof(approved));
        ArgumentOutOfRangeException.ThrowIfLessThan(approved.ModelLength, 1);
        ArgumentNullException.ThrowIfNull(approved.BuildEvidence);
        approved.BuildEvidence.Validate();
        TurboQuantBuildEvidence build = approved.BuildEvidence.TurboQuantBuild ??
            throw new ArgumentException("The approved build is not a TurboQuant closure.", nameof(approved));
        if (!string.Equals(build.SourceCommit, SourceCommit, StringComparison.Ordinal) ||
            !string.Equals(build.ImplementationCommit, ImplementationCommit, StringComparison.Ordinal))
        {
            throw new ArgumentException("The approved build is outside the audited source slice.", nameof(approved));
        }
        this.approved = approved;
    }

    public TurboQuantActivationState Evaluate(TurboQuantCampaignEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        bool buildVerified = BuildMatches(evidence);
        bool foundation = buildVerified && TupleMatches(evidence);
        bool activation = foundation && IsActivationVerified(evidence.Activation);
        bool qualityAccepted = string.Equals(
                evidence.QualityRubricId,
                QualityRubricId,
                StringComparison.Ordinal) &&
            evidence.QualityPassed;
        bool matchedEvidence = evidence.MatchedOfficialBaseline &&
            evidence.MemoryReductionPassed &&
            qualityAccepted &&
            evidence.PerformancePassed;
        bool complete = activation &&
            matchedEvidence &&
            evidence.DeterministicSmokePassed &&
            evidence.RepeatabilityPassed &&
            evidence.ContextScalingPassed &&
            evidence.CancellationPassed &&
            evidence.CleanupPassed &&
            evidence.CorruptionPassed &&
            evidence.StreamingPassed &&
            evidence.CompletedTurnCount >= 2;
        TurboQuantActivationDisposition disposition = !foundation
            ? TurboQuantActivationDisposition.Unavailable
            : complete
                ? TurboQuantActivationDisposition.Active
                : TurboQuantActivationDisposition.Unverified;
        return new TurboQuantActivationState(
            disposition,
            approved.PackageManifestSha256,
            approved.ModelSha256,
            approved.ModelLength,
            approved.BuildEvidence,
            Rows(
                evidence,
                foundation,
                buildVerified,
                activation,
                qualityAccepted,
                matchedEvidence,
                complete));
    }

    private bool BuildMatches(TurboQuantCampaignEvidence evidence)
    {
        try
        {
            evidence.BuildEvidence?.Validate();
        }
        catch (Exception error) when (error is OpenVinoProtocolException or ArgumentException)
        {
            return false;
        }
        return Equals(evidence.BuildEvidence, approved.BuildEvidence) &&
            evidence.WorkerClosureVerified &&
            evidence.SecurityReviewApproved &&
            evidence.LicenseReviewApproved;
    }

    private bool TupleMatches(TurboQuantCampaignEvidence evidence) =>
        string.Equals(evidence.EvidenceCommit, approved.EvidenceCommit, StringComparison.Ordinal) &&
            string.Equals(evidence.ModelId, approved.ModelId, StringComparison.Ordinal) &&
            string.Equals(evidence.PackageManifestSha256, approved.PackageManifestSha256, StringComparison.Ordinal) &&
            string.Equals(evidence.ModelSha256, approved.ModelSha256, StringComparison.Ordinal) &&
            evidence.ModelLength == approved.ModelLength &&
            string.Equals(evidence.RequestedDevice, AcceptedDevice, StringComparison.Ordinal) &&
            evidence.ActualExecutionDevices is { Count: 1 } &&
            string.Equals(evidence.ActualExecutionDevices[0], AcceptedDevice, StringComparison.Ordinal);

    private static bool IsActivationVerified(TurboQuantActivationEvent? activation)
    {
        if (activation is null || activation.ModelSdpaNodeCount <= 0)
        {
            return false;
        }
        try
        {
            activation.Validate();
            return true;
        }
        catch (Exception error) when (error is OpenVinoProtocolException or OverflowException)
        {
            return false;
        }
    }

    private static IReadOnlyList<TurboQuantEvidenceRow> Rows(
        TurboQuantCampaignEvidence evidence,
        bool foundation,
        bool buildVerified,
        bool activation,
        bool qualityAccepted,
        bool matchedEvidence,
        bool complete) =>
    [
        new("Maturity", !foundation
            ? "Experimental · Unavailable"
            : complete ? "Experimental · Active" : "Experimental · Unverified"),
        new("KV cache", activation
            ? "Requested TBQ4/TBQ4 · Actual TBQ4/TBQ4"
            : "Requested TBQ4/TBQ4 · Actual Unverified"),
        new("Build", buildVerified ? "Verified exact TurboQuant closure" : "Unavailable"),
        new("Activation", activation ? "Verified" : "Unverified"),
        new("Matched evidence", matchedEvidence
            ? "Memory Passed · Quality Passed · Performance Passed"
            : $"Memory {Pass(evidence.MemoryReductionPassed)} · " +
              $"Quality {Pass(qualityAccepted)} · " +
              $"Performance {Pass(evidence.PerformancePassed)}")
    ];

    private static string Pass(bool value) => value ? "Passed" : "Unverified";

    private static void RequireSha256(string value, string name)
    {
        if (value is not { Length: 64 } || value.Any(character =>
                character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new ArgumentException("A lowercase SHA-256 identity is required.", name);
        }
    }

    private static void RequireCommit(string value, string name)
    {
        if (value is not { Length: 40 } || value.Any(character =>
                character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new ArgumentException("A lowercase Git commit identity is required.", name);
        }
    }
}
