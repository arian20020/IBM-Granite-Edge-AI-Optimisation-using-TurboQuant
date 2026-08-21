using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Decomposes one complete llama.cpp candidate into the component set the phase
/// composer turns into per-pool peaks.
///
/// Every mandatory component has exactly one owner here, so no requirement is
/// counted twice, and any unknown input collapses the whole estimate rather than
/// producing a partial component set that would read as a real number.
/// </summary>
internal static class GgufResourceEstimator
{
    private static readonly IReadOnlySet<LifecyclePhase> AllPhases =
        new HashSet<LifecyclePhase>
        {
            LifecyclePhase.Load,
            LifecyclePhase.Compile,
            LifecyclePhase.SteadyStateGeneration
        };

    private static readonly IReadOnlySet<LifecyclePhase> GenerationOnly =
        new HashSet<LifecyclePhase> { LifecyclePhase.SteadyStateGeneration };

    private static readonly IReadOnlySet<LifecyclePhase> CompileAndGeneration =
        new HashSet<LifecyclePhase>
        {
            LifecyclePhase.Compile,
            LifecyclePhase.SteadyStateGeneration
        };

    private static readonly IReadOnlySet<LifecyclePhase> LoadOnly =
        new HashSet<LifecyclePhase> { LifecyclePhase.Load };

    internal static ResourceEstimate Estimate(
        InspectedModelFacts facts,
        CompatibilityCandidate candidate,
        EstimatorPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(policy);

        // Refuse anything that is not a known-good provenance, rather than
        // naming the provenances that refuse. A future PolicyProvenance member
        // that fell between Absent/Unspecified and Provisional/Calibrated would
        // otherwise proceed with no limitation recorded, presenting an
        // unvalidated policy as if it were calibrated.
        if (policy.Provenance is not (PolicyProvenance.Provisional or PolicyProvenance.Calibrated))
        {
            return ResourceEstimate.NotEstablished(
                EstimationUnavailableReason.EstimatorPolicyUnavailable);
        }

        if (candidate.Configuration is not GgufRouteConfiguration configuration)
        {
            return ResourceEstimate.NotEstablished(
                EstimationUnavailableReason.UnsupportedDeviceRoute);
        }

        if (!GgufPoolRouter.TryResolve(
                configuration.Device,
                configuration.Offload,
                out GgufPoolRouting routing,
                out EstimationUnavailableReason routingReason))
        {
            return ResourceEstimate.NotEstablished(routingReason);
        }

        if (!GgufWeightEstimator.TryEstimate(
                facts,
                configuration.Weights,
                policy,
                out ByteCount weightBytes,
                out bool scaledAcrossQuantisation,
                out EstimationUnavailableReason weightReason))
        {
            return ResourceEstimate.NotEstablished(weightReason);
        }

        if (!GgufKvCacheEstimator.TryEstimate(
                facts,
                candidate.Context,
                configuration.KvCache,
                out ByteCount kvBytes,
                out EstimationUnavailableReason kvReason))
        {
            return ResourceEstimate.NotEstablished(kvReason);
        }

        EstimatorTerms terms = policy.Terms;

        try
        {
            List<ResourceComponent> components =
            [
                ResourceComponent.Create(
                    ResourceComponentKind.Weights,
                    routing.ModelTarget,
                    weightBytes,
                    AllPhases),

                ResourceComponent.Create(
                    ResourceComponentKind.KvCache,
                    routing.ModelTarget,
                    kvBytes,
                    GenerationOnly),

                ResourceComponent.Create(
                    ResourceComponentKind.ComputeBuffer,
                    routing.ModelTarget,
                    policy.ComputeBufferFor(candidate.Context),
                    CompileAndGeneration),

                ResourceComponent.Create(
                    ResourceComponentKind.BackendAllocation,
                    routing.ModelTarget,
                    routing.ModelTarget == ResourceTarget.SystemMemory
                        ? terms.CpuBackendAllocation
                        : terms.GpuBackendAllocation,
                    AllPhases),

                // The application itself occupies RAM regardless of where the
                // model runs, and it is charged once.
                ResourceComponent.Create(
                    ResourceComponentKind.ApplicationOverhead,
                    ResourceTarget.SystemMemory,
                    terms.ApplicationOverhead,
                    AllPhases)
            ];

            if (routing.RequiresHostStaging)
            {
                ByteCount scaled = weightBytes.MultiplyByFraction(terms.StagingBufferFraction);

                components.Add(ResourceComponent.Create(
                    ResourceComponentKind.StagingBuffer,
                    ResourceTarget.SystemMemory,
                    scaled > terms.StagingBufferFloor ? scaled : terms.StagingBufferFloor,
                    LoadOnly));
            }

            if (candidate.Preparation == CandidatePreparation.WeightConversionRequired)
            {
                // The converted file stays on disk for the model's whole
                // lifetime, not just while loading, so it is charged in every
                // phase like Weights rather than confined to Load.
                components.Add(ResourceComponent.Create(
                    ResourceComponentKind.PersistentArtifact,
                    ResourceTarget.Storage,
                    weightBytes,
                    AllPhases));
            }

            HashSet<EstimationLimitation> limitations =
            [
                EstimationLimitation.WeightsDerivedFromFileLength,
                EstimationLimitation.SingleSequenceAssumed
            ];

            if (scaledAcrossQuantisation)
            {
                limitations.Add(EstimationLimitation.WeightsScaledAcrossQuantisation);
            }

            if (policy.Provenance == PolicyProvenance.Provisional)
            {
                limitations.Add(EstimationLimitation.UncalibratedEstimatorPolicy);
            }

            // The storage charge above covers the artifact a conversion writes,
            // but not the trusted higher-precision source it reads from — which
            // is larger than both the imported file and the target, and coexists
            // with the target for the whole conversion. TrustedSourceAvailability
            // carries no size yet, so the omission is recorded rather than
            // guessed at.
            if (candidate.Preparation == CandidatePreparation.WeightConversionRequired)
            {
                limitations.Add(EstimationLimitation.ConversionSourceStorageNotCounted);
            }

            return ResourceEstimate.Established(components, limitations);
        }
        catch (OverflowException)
        {
            return ResourceEstimate.NotEstablished(
                EstimationUnavailableReason.QuantitiesExceedRepresentableRange);
        }
    }
}
