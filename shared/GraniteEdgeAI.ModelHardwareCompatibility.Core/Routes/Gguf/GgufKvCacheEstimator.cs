using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Sizes the key/value cache for one context length and format.
///
/// Blocking is applied per layer and per tensor rather than once over the whole
/// cache, because each tensor is allocated separately and each rounds up to a
/// whole block on its own.
/// </summary>
internal static class GgufKvCacheEstimator
{
    /// <summary>
    /// Nothing in a candidate declares parallel sequences yet, so one sequence is
    /// assumed and the assumption is recorded as a limitation by the caller.
    /// </summary>
    private const ulong ParallelSequences = 1;

    internal static bool TryEstimate(
        InspectedModelFacts facts,
        ContextTokenCount context,
        GgufKvCacheFormat format,
        out ByteCount bytes,
        out EstimationUnavailableReason reason)
    {
        ArgumentNullException.ThrowIfNull(facts);

        bytes = ByteCount.Zero;

        if (!GgufKvCacheBlockSpec.TryFor(format, out GgufKvCacheBlockSpec spec))
        {
            reason = EstimationUnavailableReason.UnsupportedCacheFormat;
            return false;
        }

        if (facts.LayerCount is not { } layers ||
            facts.KeyValueHeadCount is not { } keyValueHeads ||
            facts.EmbeddingSize is not { } embedding ||
            facts.AttentionHeadCount is not { } attentionHeads)
        {
            reason = EstimationUnavailableReason.UnknownArchitecture;
            return false;
        }

        // A head dimension that does not divide evenly means the architecture as
        // read does not describe a model this estimator can size. Rounding it
        // would be inventing a shape.
        if (embedding % attentionHeads != 0)
        {
            reason = EstimationUnavailableReason.UnknownArchitecture;
            return false;
        }

        int headDimension = embedding / attentionHeads;
        bool turboQuant = format is
            GgufKvCacheFormat.TurboQuant4Bit
            or GgufKvCacheFormat.TurboQuant3Bit
            or GgufKvCacheFormat.TurboQuant2Bit;

        // The pinned AtomicBot runtime switches to an asymmetric layout for
        // high grouped-query ratios. That layout is not yet represented by the
        // product contract, so a symmetric estimate would be a false-safe
        // claim. Standard cache formats do not use this TurboQuant path.
        if (turboQuant && (long)attentionHeads >= 6L * keyValueHeads)
        {
            reason = EstimationUnavailableReason.UnknownArchitecture;
            return false;
        }

        try
        {
            checked
            {
                // AtomicBot pads each TurboQuant head independently to a
                // 128-value boundary before laying out tokens and KV heads.
                // Padding only the aggregate tensor undercounts models whose
                // head width is below, or crosses, that boundary.
                ulong effectiveHeadDimension = turboQuant
                    ? CeilingDivide((ulong)headDimension, 128) * 128
                    : (ulong)headDimension;
                ulong valuesPerTensorPerLayer =
                    (ulong)context.Tokens
                    * (ulong)keyValueHeads
                    * effectiveHeadDimension
                    * ParallelSequences;

                ulong blocks = CeilingDivide(valuesPerTensorPerLayer, (ulong)spec.ValuesPerBlock);
                ulong bytesPerTensorPerLayer = blocks * (ulong)spec.BytesPerBlock;

                // Key and value tensors are sized separately so an asymmetric
                // format can be introduced without reshaping this calculation.
                ulong keyBytes = bytesPerTensorPerLayer;
                ulong valueBytes = bytesPerTensorPerLayer;

                bytes = ByteCount.FromBytes((keyBytes + valueBytes) * (ulong)layers);
            }
        }
        catch (OverflowException)
        {
            bytes = ByteCount.Zero;
            reason = EstimationUnavailableReason.QuantitiesExceedRepresentableRange;
            return false;
        }

        reason = EstimationUnavailableReason.None;
        return true;
    }

    private static ulong CeilingDivide(ulong value, ulong divisor)
    {
        ulong quotient = value / divisor;
        return value % divisor == 0 ? quotient : checked(quotient + 1);
    }
}
