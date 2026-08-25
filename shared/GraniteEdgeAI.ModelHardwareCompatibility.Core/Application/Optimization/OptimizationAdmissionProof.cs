using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// Opaque evidence that the internal generator admitted one exact candidate.
/// Public metric records remain useful descriptions, but cannot become planner
/// authority without this proof.
/// </summary>
internal sealed record OptimizationAdmissionProof
{
    private OptimizationAdmissionProof(
        string snapshotId,
        string capabilitySnapshotSha256,
        string workloadId,
        string workloadSha256,
        string journeySha256,
        string routeExecutionAuthoritySha256,
        string configurationDescriptor,
        string evidenceId,
        SupportLevel supportLevel,
        bool requiresEvidence,
        string? optedInEvidenceId,
        bool isExperimental,
        OptimizationCandidate candidate)
    {
        SnapshotId = snapshotId;
        CapabilitySnapshotSha256 = capabilitySnapshotSha256;
        WorkloadId = workloadId;
        WorkloadSha256 = workloadSha256;
        JourneySha256 = journeySha256;
        RouteExecutionAuthoritySha256 = routeExecutionAuthoritySha256;
        ConfigurationDescriptor = configurationDescriptor;
        EvidenceId = evidenceId;
        SupportLevel = supportLevel;
        RequiresEvidence = requiresEvidence;
        OptedInEvidenceId = optedInEvidenceId;
        IsExperimental = isExperimental;
        ConversionProvenance = candidate.ConversionProvenance;
        Notice = candidate.Notice;
        Evidence = candidate.Metrics.Evidence;
        Quality = candidate.Metrics.Quality;
        Performance = candidate.Metrics.Performance;
        Stability = candidate.Metrics.Stability;
        ContextTokens = candidate.Metrics.ContextTokens;
        PredictedPeakBytes = candidate.Metrics.PredictedPeakBytes;
        SafeBudgetBytes = candidate.Metrics.SafeBudgetBytes;
        HeadroomBytes = candidate.Metrics.HeadroomBytes;
        WorkingStoragePhasePeakBytes = candidate.Metrics.WorkingDiskBytes;
        OutputDiskBytes = candidate.Metrics.OutputDiskBytes;
        DiskObligationBytes = candidate.Metrics.DiskObligationBytes;
        AvailableDiskBytes = candidate.Metrics.AvailableDiskBytes!.Value;
        RequiresPersistentChange = candidate.Metrics.RequiresPersistentChange;
    }

    internal string SnapshotId { get; }
    internal string CapabilitySnapshotSha256 { get; }
    internal string WorkloadId { get; }
    internal string WorkloadSha256 { get; }
    internal string JourneySha256 { get; }
    internal string RouteExecutionAuthoritySha256 { get; }
    internal string ConfigurationDescriptor { get; }
    internal string EvidenceId { get; }
    internal SupportLevel SupportLevel { get; }
    internal bool RequiresEvidence { get; }
    internal string? OptedInEvidenceId { get; }
    internal bool IsExperimental { get; }
    internal OptimizationConversionProvenance ConversionProvenance { get; }
    internal OptimizationCandidateNotice Notice { get; }
    internal EvidenceGrade Evidence { get; }
    internal OptimizationAssessment Quality { get; }
    internal OptimizationAssessment Performance { get; }
    internal OptimizationAssessment Stability { get; }
    internal int ContextTokens { get; }
    internal ulong PredictedPeakBytes { get; }
    internal ulong SafeBudgetBytes { get; }
    internal ulong HeadroomBytes { get; }

    /// <summary>
    /// Composite peak storage obligation in any lifecycle phase. It is already
    /// phase-composed and must not be added to the output a second time.
    /// </summary>
    internal ulong WorkingStoragePhasePeakBytes { get; }

    internal ulong OutputDiskBytes { get; }
    internal ulong DiskObligationBytes { get; }
    internal ulong AvailableDiskBytes { get; }
    internal bool RequiresPersistentChange { get; }

    internal static OptimizationAdmissionProof Create(
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        OptimizationCandidate candidate,
        SupportLevel supportLevel,
        bool requiresEvidence,
        IReadOnlySet<string> optedInEvidenceIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(optedInEvidenceIds);

        OptimizationSupportLevelPolicy.RequireAdmitted(
            supportLevel, nameof(supportLevel));

        bool isExperimental = supportLevel == SupportLevel.Experimental;
        string? exactOptIn = requiresEvidence || isExperimental
            ? optedInEvidenceIds.Contains(candidate.EvidenceId)
                ? candidate.EvidenceId
                : null
            : null;
        if ((requiresEvidence || isExperimental) && exactOptIn is null)
        {
            throw new ArgumentException(
                "Evidence-gated admission requires opt-in to the exact evidence identifier.",
                nameof(optedInEvidenceIds));
        }

        ValidateMetrics(candidate.Metrics);
        return new OptimizationAdmissionProof(
            snapshot.SnapshotId,
            snapshot.CapabilitySnapshotSha256,
            workload.WorkloadId,
            DigestWorkload(workload),
            DigestJourney(binding),
            DigestRouteExecutionAuthority(snapshot, candidate.EvidenceId),
            candidate.Configuration.CanonicalDescriptor,
            candidate.EvidenceId,
            supportLevel,
            requiresEvidence,
            exactOptIn,
            isExperimental,
            candidate);
    }

    internal bool MatchesCandidate(OptimizationCandidate candidate) =>
        ConfigurationDescriptor == candidate.Configuration.CanonicalDescriptor
        && EvidenceId == candidate.EvidenceId
        && IsExperimental == candidate.IsExperimental
        && ConversionProvenance == candidate.ConversionProvenance
        && Notice == candidate.Notice
        && Evidence == candidate.Metrics.Evidence
        && Quality == candidate.Metrics.Quality
        && Performance == candidate.Metrics.Performance
        && Stability == candidate.Metrics.Stability
        && ContextTokens == candidate.Metrics.ContextTokens
        && PredictedPeakBytes == candidate.Metrics.PredictedPeakBytes
        && SafeBudgetBytes == candidate.Metrics.SafeBudgetBytes
        && HeadroomBytes == candidate.Metrics.HeadroomBytes
        && WorkingStoragePhasePeakBytes == candidate.Metrics.WorkingDiskBytes
        && OutputDiskBytes == candidate.Metrics.OutputDiskBytes
        && DiskObligationBytes == candidate.Metrics.DiskObligationBytes
        && AvailableDiskBytes == candidate.Metrics.AvailableDiskBytes
        && RequiresPersistentChange == candidate.Metrics.RequiresPersistentChange;

    internal bool MatchesAuthority(
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding) =>
        SnapshotId == snapshot.SnapshotId
        && CapabilitySnapshotSha256 == snapshot.CapabilitySnapshotSha256
        && WorkloadId == workload.WorkloadId
        && WorkloadSha256 == DigestWorkload(workload)
        && JourneySha256 == DigestJourney(binding)
        && RouteExecutionAuthoritySha256
            == DigestRouteExecutionAuthority(snapshot, EvidenceId);

    private static void ValidateMetrics(OptimizationCandidateMetrics metrics)
    {
        if (metrics.PredictedPeakBytes == 0
            || metrics.SafeBudgetBytes == 0
            || metrics.PredictedPeakBytes > metrics.SafeBudgetBytes
            || metrics.HeadroomBytes != metrics.SafeBudgetBytes - metrics.PredictedPeakBytes
            || metrics.AvailableDiskBytes is not > 0
            || metrics.DiskObligationBytes != Math.Max(
                metrics.WorkingDiskBytes, metrics.OutputDiskBytes)
            || metrics.DiskObligationBytes > metrics.AvailableDiskBytes.Value
            || metrics.WorkingDiskBytes < metrics.OutputDiskBytes
            || (metrics.RequiresPersistentChange && metrics.OutputDiskBytes == 0)
            || (!metrics.RequiresPersistentChange && metrics.OutputDiskBytes != 0))
        {
            throw new ArgumentException(
                "Candidate admission quantities or persistence relationships are inconsistent.",
                nameof(metrics));
        }
    }

    internal static string DigestWorkload(OptimizationWorkload workload) => Digest(
        workload.WorkloadId,
        workload.MinimumContextTokens.ToString(CultureInfo.InvariantCulture),
        ((int)workload.MinimumQuality).ToString(CultureInfo.InvariantCulture),
        string.Join(",", workload.CandidateContexts
            .Select(value => value.Tokens)
            .OrderBy(value => value)
            .Select(value => value.ToString(CultureInfo.InvariantCulture))));

    internal static string DigestJourney(OptimizationJourneyBinding binding) => Digest(
        binding.ModelInspectionRunId,
        binding.ModelInspectionHandoffId,
        binding.ModelSha256,
        binding.ModelLengthBytes.ToString(CultureInfo.InvariantCulture),
        binding.ProductHardwareRunId,
        binding.HardwareSnapshotSha256);

    private static string DigestRouteExecutionAuthority(
        OptimizationCapabilitySnapshot snapshot,
        string evidenceId)
    {
        if (snapshot.Gguf?.RuntimeAuthority is { } gguf
            && gguf.Profiles.TryGetValue(evidenceId, out GgufExecutionProfileAuthority? profile))
        {
            return Digest(
                "gguf",
                gguf.RuntimeBuildId,
                gguf.RuntimeSourceCommit,
                profile.EvidenceId,
                ((int)profile.Evidence).ToString(CultureInfo.InvariantCulture),
                profile.ProfileId,
                profile.FlashAttention ? "1" : "0",
                profile.ThreadCount.ToString(CultureInfo.InvariantCulture),
                profile.BatchSize.ToString(CultureInfo.InvariantCulture),
                profile.MaximumGeneratedTokens.ToString(CultureInfo.InvariantCulture));
        }

        if (snapshot.OpenVino?.ExecutionAuthorities.TryGetValue(
                evidenceId, out OpenVinoExecutionAuthority? openVino) == true)
        {
            List<string> fields =
            [
                "openvino",
                openVino.EvidenceId,
                openVino.ConfigurationId,
                ((int)openVino.SourceWeightPrecision).ToString(CultureInfo.InvariantCulture),
                openVino.BuildIdentity.RuntimeBuild,
                openVino.BuildIdentity.GenAiBuild,
                openVino.BuildIdentity.TokenizersBuild,
                openVino.BuildIdentity.WorkerManifestDigest,
                openVino.CompiledCacheIsDisposable ? "1" : "0"
            ];
            foreach (KeyValuePair<string, string> version in
                openVino.OptimizerVersions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                fields.Add(version.Key);
                fields.Add(version.Value);
            }

            if (openVino.TurboQuantBuild is { } turbo)
            {
                fields.Add("turboquant");
                fields.Add(turbo.SourceCommit);
                fields.Add(turbo.ImplementationCommit);
                fields.Add(turbo.PatchSeriesDigest);
                fields.Add(turbo.RuntimeManifestDigest);
            }
            else
            {
                fields.Add("released");
            }

            return Digest(fields.ToArray());
        }

        return "none";
    }

    private static string Digest(params string[] fields)
    {
        string canonical = string.Concat(fields.Select(field =>
            $"{field.Length.ToString(CultureInfo.InvariantCulture)}:{field}"));
        return Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false).GetBytes(canonical))).ToLowerInvariant();
    }
}
