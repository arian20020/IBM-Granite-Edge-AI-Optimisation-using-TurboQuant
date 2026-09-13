using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests;

/// <summary>
/// Explicit synthetic inputs for downstream serialization/issuance tests.
/// These are not evidence of product admission; generator tests must call the
/// real generator with a catalog. Estimators preserve the frozen V3 vectors.
/// </summary>
internal static class SyntheticPlanCandidateFactory
{
    internal static CrossRouteGenerationResult Create(
        OptimizationCapabilitySnapshot snapshot, InspectedModelFacts facts,
        OptimizationWorkload workload, OptimizationJourneyBinding binding,
        ByteCount safeBudget, ByteCount availableDisk, EstimatorPolicy policy,
        IReadOnlySet<string> optedInExperimentalEvidenceIds,
        OptimizationHardwareAuthority hardwareAuthority)
    {
        List<OptimizationCandidate> candidates = [];
        foreach (ContextTokenCount context in workload.CandidateContexts)
        {
            if (snapshot.OpenVino is { } ov)
            {
                foreach (OpenVinoAdmittedConfiguration entry in ov.Admitted)
                {
                    OpenVinoRouteConfiguration config = OpenVinoRouteConfiguration.Create(
                        entry.Weights, entry.KvCache, entry.Device, entry.PerformanceHint,
                        entry.CompiledCache, entry.Streams);
                    Add(config, OpenVinoResourceEstimator.Estimate(facts, config, context, policy),
                        entry.EvidenceId, entry.Level, entry.RequiresEvidence,
                        entry.KvCache == OpenVinoKvCacheFormat.TurboQuantTbq3
                            ? OptimizationAssessment.Poor
                            : entry.Weights == OpenVinoWeightFormat.Int4
                                ? OptimizationAssessment.Acceptable : OptimizationAssessment.Good,
                        OpenVinoFormatMap.RequiresPersistentConversion(entry.Weights));
                }
            }
            if (snapshot.Gguf is { } gguf)
            {
                foreach (GgufAdmittedConfiguration entry in gguf.Admitted)
                {
                    bool normalized = GgufWeightFormatMap.ToCanonical(entry.Weights)
                        == WeightQuantisationMap.FromGgufFileType(facts.FileType, facts.QuantisationVersion);
                    bool persistent = entry.Weights != GgufWeightFormat.Imported && !normalized;
                    GgufRouteConfiguration config = GgufRouteConfiguration.Create(
                        normalized ? GgufWeightFormat.Imported : entry.Weights,
                        entry.KvCache, entry.Backend, entry.Device, entry.Offload);
                    ResourceEstimate estimate = GgufResourceEstimator.Estimate(facts,
                        CompatibilityCandidate.Create(config, context,
                            persistent ? CandidatePreparation.WeightConversionRequired : CandidatePreparation.RuntimeProfileOnly,
                            entry.EvidenceId, entry.Level == SupportLevel.Experimental, false), policy);
                    Add(config, estimate, entry.EvidenceId, entry.Level, entry.RequiresEvidence,
                        entry.Weights == GgufWeightFormat.Q2K ? OptimizationAssessment.Poor : OptimizationAssessment.Acceptable,
                        persistent,
                        persistent ? OptimizationConversionProvenance.ControlledRequantisation : OptimizationConversionProvenance.None,
                        normalized ? GgufWeightNormalizationProof.FromInspection(facts.FileType, facts.QuantisationVersion, entry.Weights) : null);
                }
            }

            void Add(RouteConfiguration configuration, ResourceEstimate estimate,
                string id, SupportLevel level, bool requiresEvidence,
                OptimizationAssessment quality, bool persistent,
                OptimizationConversionProvenance provenance = OptimizationConversionProvenance.None,
                GgufWeightNormalizationProof? normalization = null)
            {
                ResourcePeakProfile peaks = ResourcePhaseComposer.Compose(estimate.Components);
                ulong peak = peaks.SystemMemoryPressure.Bytes;
                ulong disk = peaks.PeakFor(ResourceTarget.Storage).Bytes;
                OptimizationAssessment support = level == SupportLevel.Experimental
                    ? OptimizationAssessment.Acceptable : OptimizationAssessment.Good;
                OptimizationCandidateMetrics metrics = OptimizationCandidateMetrics.Create(
                    EvidenceGrade.Estimated, quality, support, support, context.Tokens,
                    peak, safeBudget.Bytes, safeBudget.Bytes - peak, disk,
                    persistent ? disk : 0, persistent, availableDisk.Bytes);
                OptimizationCandidate candidate = normalization is null
                    ? OptimizationCandidate.Create(configuration, metrics, id, level == SupportLevel.Experimental, provenance)
                    : OptimizationCandidate.CreateWithGgufWeightNormalization(
                        (GgufRouteConfiguration)configuration, metrics, id, level == SupportLevel.Experimental, normalization);
                candidates.Add(OptimizationCandidate.AttachAdmissionProof(candidate,
                    OptimizationAdmissionProof.Create(snapshot, workload, binding, candidate,
                        level, requiresEvidence, optedInExperimentalEvidenceIds,
                        OptimizationIssuanceAuthority.FromGeneration(hardwareAuthority, safeBudget, availableDisk))));
            }
        }
        return new CrossRouteGenerationResult(candidates, []);
    }
}
