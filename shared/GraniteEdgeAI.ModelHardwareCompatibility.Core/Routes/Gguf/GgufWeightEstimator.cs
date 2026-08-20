using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Sizes weight memory from the artifact's measured byte length.
///
/// Recorded limitation, spec section 8: C1 receives a file length and
/// quantisation identifiers, not per-tensor sizes. This is derived from a
/// measured quantity and is less precise than a tensor-level read, so every
/// estimate built here carries WeightsDerivedFromFileLength. Converting between
/// encodings scales by average bits per weight, which additionally carries
/// WeightsScaledAcrossQuantisation because embeddings and normalisation tensors
/// do not scale linearly.
/// </summary>
internal static class GgufWeightEstimator
{
    internal static bool TryEstimate(
        InspectedModelFacts facts,
        GgufWeightFormat target,
        EstimatorPolicy policy,
        out ByteCount bytes,
        out bool scaledAcrossQuantisation,
        out EstimationUnavailableReason reason)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(policy);

        bytes = ByteCount.Zero;
        scaledAcrossQuantisation = false;

        if (policy.Provenance is PolicyProvenance.Absent or PolicyProvenance.Unspecified)
        {
            reason = EstimationUnavailableReason.EstimatorPolicyUnavailable;
            return false;
        }

        if (target == GgufWeightFormat.Unspecified)
        {
            reason = EstimationUnavailableReason.UnsupportedWeightFormat;
            return false;
        }

        try
        {
            ByteCount payload = facts.FileLength;

            if (target != GgufWeightFormat.Imported)
            {
                WeightQuantisation targetQuantisation =
                    GgufWeightFormatMap.ToCanonical(target);

                if (targetQuantisation == WeightQuantisation.Unknown)
                {
                    reason = EstimationUnavailableReason.UnsupportedWeightFormat;
                    return false;
                }

                WeightQuantisation sourceQuantisation = WeightQuantisationMap.FromGgufFileType(
                    facts.FileType, facts.QuantisationVersion);

                // Without the source encoding there is nothing to scale from.
                // Assuming one would silently resize the whole model.
                if (sourceQuantisation == WeightQuantisation.Unknown)
                {
                    reason = EstimationUnavailableReason.UnknownSourceQuantisation;
                    return false;
                }

                decimal ratio =
                    WeightQuantisationMap.BitsPerWeight(targetQuantisation)
                    / WeightQuantisationMap.BitsPerWeight(sourceQuantisation);

                payload = payload.MultiplyByFraction(ratio);
                scaledAcrossQuantisation = true;
            }

            ByteCount overhead = payload.MultiplyByFraction(
                policy.Terms.WeightOverheadFraction);

            bytes = payload.Add(overhead).AlignUpTo(policy.Terms.AllocationAlignment);
        }
        catch (OverflowException)
        {
            bytes = ByteCount.Zero;
            scaledAcrossQuantisation = false;
            reason = EstimationUnavailableReason.QuantitiesExceedRepresentableRange;
            return false;
        }

        reason = EstimationUnavailableReason.None;
        return true;
    }
}
