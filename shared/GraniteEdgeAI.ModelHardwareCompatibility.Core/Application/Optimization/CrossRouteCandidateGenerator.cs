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
        Candidates = candidates;
        Exclusions = exclusions;
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
        ByteCount safeBudget,
        ByteCount availableDisk,
        EstimatorPolicy policy,
        IReadOnlySet<string> optedInExperimentalEvidenceIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(workload);
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
                        snapshot.Gguf!, facts, workload, context, safeBudget, availableDisk,
                        policy, optedInExperimentalEvidenceIds, candidates, exclusions);
                    break;

                case OptimizationRoute.OpenVino:
                    GenerateOpenVino(
                        snapshot.OpenVino!, facts, workload, context, safeBudget, availableDisk,
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
        List<OptimizationCandidate> distinct = [];
        HashSet<string> seen = [];

        foreach (OptimizationCandidate candidate in candidates)
        {
            if (seen.Add(candidate.CanonicalDescriptor))
            {
                distinct.Add(candidate);
            }
        }

        return new CrossRouteGenerationResult(distinct, exclusions);
    }

    private static void GenerateOpenVino(
        OpenVinoCapabilityPayload payload,
        InspectedModelFacts facts,
        OptimizationWorkload workload,
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
                admitted.Level, admitted.EvidenceId, optedIn, descriptor, exclusions))
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
                OpenVinoQuality.Of(admitted.Weights),
                OpenVinoFormatMap.RequiresPersistentConversion(admitted.Weights),
                workload,
                safeBudget,
                availableDisk,
                descriptor,
                candidates,
                exclusions);
        }
    }

    private static void GenerateGguf(
        GgufCapabilityPayload payload,
        InspectedModelFacts facts,
        OptimizationWorkload workload,
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
            if (context.Tokens < admitted.MinimumContextTokens
                || context.Tokens > admitted.MaximumContextTokens)
            {
                continue;
            }

            GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
                admitted.Weights,
                admitted.KvCache,
                admitted.Backend,
                admitted.Device,
                admitted.Offload);

            string descriptor = $"{configuration.CanonicalDescriptor}|ctx={context.Tokens}";

            if (Refused(
                admitted.Level, admitted.EvidenceId, optedIn, descriptor, exclusions))
            {
                continue;
            }

            CompatibilityCandidate candidate = CompatibilityCandidate.Create(
                configuration,
                context,
                admitted.Weights == GgufWeightFormat.Imported
                    ? CandidatePreparation.RuntimeProfileOnly
                    : CandidatePreparation.WeightConversionRequired,
                admitted.EvidenceId,
                admitted.Level == SupportLevel.Experimental,
                isBaseline: false);

            ResourceEstimate estimate =
                GgufResourceEstimator.Estimate(facts, candidate, policy);

            Admit(
                configuration,
                estimate,
                context,
                admitted.EvidenceId,
                admitted.Level,
                GgufQuality.Of(admitted.Weights),
                admitted.Weights != GgufWeightFormat.Imported,
                workload,
                safeBudget,
                availableDisk,
                descriptor,
                candidates,
                exclusions);
        }
    }

    /// <summary>
    /// Experimental entries are absent unless the user opted in to that exact
    /// evidence record. Opting in to one experimental route must not admit
    /// another the user never saw.
    /// </summary>
    private static bool Refused(
        SupportLevel level,
        string evidenceId,
        IReadOnlySet<string> optedIn,
        string descriptor,
        List<OptimizationExclusion> exclusions)
    {
        if (level == SupportLevel.Experimental && !optedIn.Contains(evidenceId))
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId, descriptor, OptimizationExclusionReason.ExperimentalNotAdmitted));

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
        List<OptimizationExclusion> exclusions)
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

        if (disk > availableDisk)
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

        candidates.Add(OptimizationCandidate.Create(
            configuration,
            OptimizationCandidateMetrics.Create(
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
                requiresPersistentChange),
            evidenceId,
            level == SupportLevel.Experimental));
    }
}

/// <summary>
/// How good each representation is, on the shared coarse scale.
///
/// Derived from bit width, which is the only quality signal available before
/// anything has been measured. Bands rather than a continuous score, because a
/// finer scale would imply a precision nothing here supports.
/// </summary>
internal static class OpenVinoQuality
{
    internal static OptimizationAssessment Of(OpenVinoWeightFormat format) => format switch
    {
        OpenVinoWeightFormat.Original or OpenVinoWeightFormat.Fp16 =>
            OptimizationAssessment.Excellent,
        OpenVinoWeightFormat.Int8 => OptimizationAssessment.Good,
        OpenVinoWeightFormat.Int4 or OpenVinoWeightFormat.TurboQuantTbq4 =>
            OptimizationAssessment.Acceptable,
        OpenVinoWeightFormat.TurboQuantTbq3 => OptimizationAssessment.Poor,
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
        GgufWeightFormat.Q3KM => OptimizationAssessment.Poor,
        _ => OptimizationAssessment.Unknown
    };
}
