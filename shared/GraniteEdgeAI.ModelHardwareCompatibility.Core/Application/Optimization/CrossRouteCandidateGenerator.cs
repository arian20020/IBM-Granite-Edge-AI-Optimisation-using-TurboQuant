using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>One candidate that was considered and refused, and why.</summary>
public sealed record OptimizationExclusion(
    string EvidenceId, string CanonicalDescriptor, OptimizationExclusionReason Reason);

/// <summary>
/// Everything the planner considered: what survived, and what did not and why.
/// </summary>
public sealed record CrossRouteGenerationResult
{
    internal CrossRouteGenerationResult(
        IReadOnlyList<OptimizationCandidate> candidates,
        IReadOnlyList<OptimizationExclusion> exclusions)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(exclusions);

        Candidates = Array.AsReadOnly([.. candidates]);
        Exclusions = Array.AsReadOnly([.. exclusions]);
    }

    public IReadOnlyList<OptimizationCandidate> Candidates { get; }

    /// <summary>
    /// Kept rather than discarded. "No setup fits" and "we could not assess
    /// anything" are different answers, and only the exclusion list tells them
    /// apart.
    /// </summary>
    public IReadOnlyList<OptimizationExclusion> Exclusions { get; }
}

/// <summary>
/// What this machine could actually be asked to run.
///
/// One generator for both routes, because the alternative is two that drift.
/// It never invents a combination: every candidate comes from an entry the
/// capability snapshot admitted, and an entry naming a combination the route
/// records cannot construct is refused rather than approximated.
///
/// Unknown fails closed throughout. An estimate that could not be established
/// becomes a typed exclusion, never a zero - a zero would rank as the cheapest
/// option available and win every efficiency band.
/// </summary>
internal static class CrossRouteCandidateGenerator
{
    internal static CrossRouteGenerationResult Generate(
        OptimizationCapabilitySnapshot snapshot,
        InspectedModelFacts facts,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        ByteCount safeBudget,
        ByteCount availableDisk,
        EstimatorPolicy policy,
        IReadOnlySet<string> optedInExperimentalEvidenceIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(optedInExperimentalEvidenceIds);

        List<OptimizationCandidate> candidates = [];
        List<OptimizationExclusion> exclusions = [];

        foreach (ContextTokenCount context in workload.CandidateContexts)
        {
            switch (snapshot.Route)
            {
                case OptimizationRoute.Gguf:
                    GenerateGguf(
                        snapshot, snapshot.Gguf!, facts, workload, binding, context,
                        safeBudget, availableDisk,
                        policy, optedInExperimentalEvidenceIds, candidates, exclusions);
                    break;

                case OptimizationRoute.OpenVino:
                    GenerateOpenVino(
                        snapshot, snapshot.OpenVino!, facts, workload, binding, context,
                        safeBudget, availableDisk,
                        policy, optedInExperimentalEvidenceIds, candidates, exclusions);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(snapshot),
                        snapshot.Route,
                        "A route with no generation arm would silently produce nothing "
                        + "and report it as a machine that fits nothing.");
            }
        }

        // De-duplicated by complete configuration, so the same setup reached
        // through two admitted entries is offered once rather than competing
        // with itself for a band.
        List<OptimizationCandidate> distinct =
        [
            .. candidates
                .GroupBy(
                    candidate => candidate.CanonicalDescriptor,
                    StringComparer.Ordinal)
                .Select(group => group.Aggregate(
                    (best, candidate) =>
                        OptimizationPreferenceResolver.PrefersFirst(candidate, best)
                            ? candidate
                            : best))
                .OrderBy(
                    candidate => candidate.CanonicalDescriptor,
                    StringComparer.Ordinal)
        ];

        return new CrossRouteGenerationResult(distinct, exclusions);
    }

    private static void GenerateOpenVino(
        OptimizationCapabilitySnapshot snapshot,
        OpenVinoCapabilityPayload payload,
        InspectedModelFacts facts,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        ContextTokenCount context,
        ByteCount safeBudget,
        ByteCount availableDisk,
        EstimatorPolicy policy,
        IReadOnlySet<string> optedIn,
        List<OptimizationCandidate> candidates,
        List<OptimizationExclusion> exclusions)
    {
        foreach (OpenVinoAdmittedConfiguration admitted in payload.Admitted)
        {
            if (!payload.ExecutionAuthorities.ContainsKey(admitted.EvidenceId))
            {
                exclusions.Add(new OptimizationExclusion(
                    admitted.EvidenceId,
                    $"weights={admitted.Weights}|ctx={context.Tokens}",
                    OptimizationExclusionReason.ExecutionAuthorityNotEstablished));
                continue;
            }

            if (context.Tokens < admitted.MinimumContextTokens
                || context.Tokens > admitted.MaximumContextTokens)
            {
                continue;
            }

            OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
                admitted.Weights,
                admitted.KvCache,
                admitted.Device,
                admitted.PerformanceHint,
                admitted.CompiledCache,
                admitted.Streams);

            string descriptor = $"{configuration.CanonicalDescriptor}|ctx={context.Tokens}";

            if (Refused(
                admitted.Level, admitted.RequiresEvidence, admitted.EvidenceId,
                optedIn, descriptor, exclusions))
            {
                continue;
            }

            ResourceEstimate estimate = OpenVinoResourceEstimator.Estimate(
                facts, configuration, context, policy);

            Admit(
                configuration,
                estimate,
                context,
                admitted.EvidenceId,
                admitted.Level,
                OpenVinoQuality.Of(admitted.Weights, admitted.KvCache),
                OpenVinoFormatMap.RequiresPersistentConversion(admitted.Weights),
                workload,
                safeBudget,
                availableDisk,
                descriptor,
                candidates,
                exclusions,
                snapshot,
                binding,
                admitted.RequiresEvidence,
                optedIn);
        }
    }

    private static void GenerateGguf(
        OptimizationCapabilitySnapshot snapshot,
        GgufCapabilityPayload payload,
        InspectedModelFacts facts,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        ContextTokenCount context,
        ByteCount safeBudget,
        ByteCount availableDisk,
        EstimatorPolicy policy,
        IReadOnlySet<string> optedIn,
        List<OptimizationCandidate> candidates,
        List<OptimizationExclusion> exclusions)
    {
        foreach (GgufAdmittedConfiguration admitted in payload.Admitted)
        {
            if (payload.RuntimeAuthority is not { } runtimeAuthority
                || !runtimeAuthority.Profiles.TryGetValue(
                    admitted.EvidenceId, out GgufExecutionProfileAuthority? profile)
                || profile.Evidence != EvidenceGrade.Estimated)
            {
                exclusions.Add(new OptimizationExclusion(
                    admitted.EvidenceId,
                    $"weights={admitted.Weights}|ctx={context.Tokens}",
                    OptimizationExclusionReason.ExecutionAuthorityNotEstablished));
                continue;
            }

            if (context.Tokens < admitted.MinimumContextTokens
                || context.Tokens > admitted.MaximumContextTokens)
            {
                continue;
            }

            if (!TryResolveGgufPreparation(
                payload, facts, admitted, out GgufWeightFormat effectiveWeights,
                out bool requiresPersistentChange, out OptimizationAssessment quality,
                out OptimizationConversionProvenance provenance))
            {
                string refusedDescriptor =
                    $"weights={admitted.Weights}|ctx={context.Tokens}";
                exclusions.Add(new OptimizationExclusion(
                    admitted.EvidenceId,
                    refusedDescriptor,
                    OptimizationExclusionReason.RequantisationNotAuthorized));
                continue;
            }

            GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
                effectiveWeights,
                admitted.KvCache,
                admitted.Backend,
                admitted.Device,
                admitted.Offload);

            string descriptor = $"{configuration.CanonicalDescriptor}|ctx={context.Tokens}";

            if (Refused(
                admitted.Level, admitted.RequiresEvidence, admitted.EvidenceId,
                optedIn, descriptor, exclusions))
            {
                continue;
            }

            CompatibilityCandidate candidate = CompatibilityCandidate.Create(
                configuration,
                context,
                !requiresPersistentChange
                    ? CandidatePreparation.RuntimeProfileOnly
                    : CandidatePreparation.WeightConversionRequired,
                admitted.EvidenceId,
                admitted.Level == SupportLevel.Experimental,
                isBaseline: false);

            ResourceEstimate estimate =
                GgufResourceEstimator.Estimate(facts, candidate, policy);
            GgufWeightNormalizationProof? normalizationProof =
                effectiveWeights == GgufWeightFormat.Imported
                    && admitted.Weights != GgufWeightFormat.Imported
                    ? GgufWeightNormalizationProof.FromInspection(
                        facts.FileType, facts.QuantisationVersion, admitted.Weights)
                    : null;

            Admit(
                configuration,
                estimate,
                context,
                admitted.EvidenceId,
                admitted.Level,
                quality,
                requiresPersistentChange,
                workload,
                safeBudget,
                availableDisk,
                descriptor,
                candidates,
                exclusions,
                snapshot,
                binding,
                admitted.RequiresEvidence,
                optedIn,
                provenance,
                normalizationProof);
        }
    }

    private static bool TryResolveGgufPreparation(
        GgufCapabilityPayload payload,
        InspectedModelFacts facts,
        GgufAdmittedConfiguration admitted,
        out GgufWeightFormat effectiveWeights,
        out bool requiresPersistentChange,
        out OptimizationAssessment quality,
        out OptimizationConversionProvenance provenance)
    {
        effectiveWeights = admitted.Weights;
        requiresPersistentChange = false;
        quality = OptimizationAssessment.Unknown;
        provenance = OptimizationConversionProvenance.None;

        WeightQuantisation source = WeightQuantisationMap.FromGgufFileType(
            facts.FileType, facts.QuantisationVersion);

        if (admitted.Weights == GgufWeightFormat.Imported)
        {
            quality = GgufQuality.Of(source);
            if (quality == OptimizationAssessment.Unknown)
            {
                // Preserve the established imported-file behavior when its
                // encoding was not established; reachability does not depend
                // on knowing the encoding because no conversion is requested.
                quality = OptimizationAssessment.Excellent;
            }

            return true;
        }

        WeightQuantisation target = GgufWeightFormatMap.ToCanonical(admitted.Weights);

        if (source == WeightQuantisation.Unknown || target == WeightQuantisation.Unknown)
        {
            return false;
        }

        quality = GgufQuality.Of(target);

        if (source == target)
        {
            effectiveWeights = GgufWeightFormat.Imported;
            return true;
        }

        if (payload.ConversionSource is not { } conversionSource
            || payload.AdmittedQuantiser is null
            || !conversionSource.CanProduce(target))
        {
            return false;
        }

        if (conversionSource.IsAlreadyQuantised
            && (!(payload.RequantisationPolicy?.Authorizes(admitted) ?? false)
                || payload.RequantisationPolicy.Source != conversionSource
                || payload.RequantisationPolicy.Quantiser != payload.AdmittedQuantiser))
        {
            return false;
        }

        requiresPersistentChange = true;
        provenance = conversionSource.IsAlreadyQuantised
            ? OptimizationConversionProvenance.ControlledRequantisation
            : OptimizationConversionProvenance.HigherPrecisionSource;

        return true;
    }

    /// <summary>
    /// Experimental entries are absent unless the user opted in to that exact
    /// evidence record. Opting in to one experimental route must not admit
    /// another the user never saw.
    /// </summary>
    private static bool Refused(
        SupportLevel level,
        bool requiresEvidence,
        string evidenceId,
        IReadOnlySet<string> optedIn,
        string descriptor,
        List<OptimizationExclusion> exclusions)
    {
        if (!OptimizationSupportLevelPolicy.IsAdmitted(level))
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId,
                descriptor,
                OptimizationExclusionReason.EvidenceBelowAdmissionLevel));

            return true;
        }

        if ((level == SupportLevel.Experimental || requiresEvidence)
            && !optedIn.Contains(evidenceId))
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId,
                descriptor,
                level == SupportLevel.Experimental
                    ? OptimizationExclusionReason.ExperimentalNotAdmitted
                    : OptimizationExclusionReason.EvidenceBelowAdmissionLevel));

            return true;
        }

        return false;
    }

    private static void Admit(
        RouteConfiguration configuration,
        ResourceEstimate estimate,
        ContextTokenCount context,
        string evidenceId,
        SupportLevel level,
        OptimizationAssessment quality,
        bool requiresPersistentChange,
        OptimizationWorkload workload,
        ByteCount safeBudget,
        ByteCount availableDisk,
        string descriptor,
        List<OptimizationCandidate> candidates,
        List<OptimizationExclusion> exclusions,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationJourneyBinding binding,
        bool requiresEvidence,
        IReadOnlySet<string> optedInEvidenceIds,
        OptimizationConversionProvenance provenance =
            OptimizationConversionProvenance.None,
        GgufWeightNormalizationProof? normalizationProof = null)
    {
        if (estimate.Status != EstimationStatus.Established)
        {
            // A typed exclusion, never a zero. Zero would be the cheapest
            // option on the board and would win every efficiency band.
            exclusions.Add(new OptimizationExclusion(
                evidenceId, descriptor, OptimizationExclusionReason.EstimateNotEstablished));

            return;
        }

        ResourcePeakProfile peaks = ResourcePhaseComposer.Compose(estimate.Components);
        ByteCount peak = peaks.SystemMemoryPressure.Add(
            peaks.PeakFor(ResourceTarget.DedicatedDeviceMemory));

        if (peak > safeBudget)
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId, descriptor, OptimizationExclusionReason.ExceedsSafeMemoryBudget));

            return;
        }

        ByteCount disk = peaks.PeakFor(ResourceTarget.Storage);

        if (availableDisk == ByteCount.Zero || disk > availableDisk)
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId, descriptor, OptimizationExclusionReason.InsufficientDiskSpace));

            return;
        }

        if (context.Tokens < workload.MinimumContextTokens)
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId, descriptor,
                OptimizationExclusionReason.ContextBelowWorkloadMinimum));

            return;
        }

        if (quality < workload.MinimumQuality)
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId, descriptor, OptimizationExclusionReason.QualityBelowFloor));

            return;
        }

        _ = safeBudget.TrySubtract(peak, out ByteCount headroom);

        OptimizationCandidateMetrics metrics = OptimizationCandidateMetrics.Create(
            EvidenceGrade.Estimated,
            quality,
            // Not measured. Nothing has run, so performance and stability
            // are derived from the support level rather than observed, and
            // the evidence grade above says so.
            level == SupportLevel.Experimental
                ? OptimizationAssessment.Acceptable
                : OptimizationAssessment.Good,
            level == SupportLevel.Experimental
                ? OptimizationAssessment.Acceptable
                : OptimizationAssessment.Good,
            context.Tokens,
            peak.Bytes,
            safeBudget.Bytes,
            headroom.Bytes,
            peaks.PeakFor(ResourceTarget.Storage).Bytes,
            requiresPersistentChange ? disk.Bytes : 0,
            requiresPersistentChange,
            availableDisk.Bytes);
        OptimizationCandidate admittedCandidate = normalizationProof is null
            ? OptimizationCandidate.Create(
                configuration, metrics, evidenceId,
                level == SupportLevel.Experimental, provenance)
            : OptimizationCandidate.CreateWithGgufWeightNormalization(
                (GgufRouteConfiguration)configuration, metrics, evidenceId,
                level == SupportLevel.Experimental, normalizationProof);
        OptimizationAdmissionProof admissionProof = OptimizationAdmissionProof.Create(
            snapshot, workload, binding, admittedCandidate,
            level, requiresEvidence, optedInEvidenceIds);
        candidates.Add(OptimizationCandidate.AttachAdmissionProof(
            admittedCandidate, admissionProof));
    }
}

/// <summary>
/// How good each representation is, on the shared coarse scale.
///
/// Conservatively combines weight and cache representation grades. The weaker
/// one wins: compressing one part more aggressively cannot improve quality lost
/// in the other. Bands rather than a continuous score avoid implying precision
/// nothing here supports.
/// </summary>
internal static class OpenVinoQuality
{
    internal static OptimizationAssessment Of(
        OpenVinoWeightFormat weights,
        OpenVinoKvCacheFormat cache)
    {
        OptimizationAssessment weightQuality = Weight(weights);
        OptimizationAssessment cacheQuality = Cache(cache);

        return weightQuality < cacheQuality ? weightQuality : cacheQuality;
    }

    private static OptimizationAssessment Weight(OpenVinoWeightFormat format) => format switch
    {
        OpenVinoWeightFormat.Original or OpenVinoWeightFormat.Fp16 =>
            OptimizationAssessment.Excellent,
        OpenVinoWeightFormat.Int8 => OptimizationAssessment.Good,
        OpenVinoWeightFormat.Int4 => OptimizationAssessment.Acceptable,
        _ => OptimizationAssessment.Unknown
    };

    private static OptimizationAssessment Cache(OpenVinoKvCacheFormat format) => format switch
    {
        OpenVinoKvCacheFormat.RouteDefault
            or OpenVinoKvCacheFormat.F16
            or OpenVinoKvCacheFormat.Bf16 => OptimizationAssessment.Excellent,
        OpenVinoKvCacheFormat.U8 => OptimizationAssessment.Good,
        OpenVinoKvCacheFormat.U4
            or OpenVinoKvCacheFormat.TurboQuantTbq4 => OptimizationAssessment.Acceptable,
        OpenVinoKvCacheFormat.TurboQuantTbq3 => OptimizationAssessment.Poor,
        _ => OptimizationAssessment.Unknown
    };
}

/// <summary>The same scale, for GGUF representations.</summary>
internal static class GgufQuality
{
    internal static OptimizationAssessment Of(GgufWeightFormat format) => format switch
    {
        GgufWeightFormat.Imported or GgufWeightFormat.BF16 or GgufWeightFormat.F16 =>
            OptimizationAssessment.Excellent,
        GgufWeightFormat.Q8_0 or GgufWeightFormat.Q6K => OptimizationAssessment.Good,
        GgufWeightFormat.Q5KM or GgufWeightFormat.Q4KM => OptimizationAssessment.Acceptable,
        GgufWeightFormat.Q3KM or GgufWeightFormat.Q2K => OptimizationAssessment.Poor,
        _ => OptimizationAssessment.Unknown
    };

    internal static OptimizationAssessment Of(WeightQuantisation format) => format switch
    {
        WeightQuantisation.BF16 or WeightQuantisation.F16 =>
            OptimizationAssessment.Excellent,
        WeightQuantisation.Q8_0 or WeightQuantisation.Q6_K =>
            OptimizationAssessment.Good,
        WeightQuantisation.Q5_K_M or WeightQuantisation.Q4_K_M =>
            OptimizationAssessment.Acceptable,
        WeightQuantisation.Q3_K_M or WeightQuantisation.Q2_K =>
            OptimizationAssessment.Poor,
        _ => OptimizationAssessment.Unknown
    };
}
