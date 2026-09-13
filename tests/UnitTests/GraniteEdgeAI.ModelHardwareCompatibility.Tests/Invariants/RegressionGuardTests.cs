using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Regression guards raised by review of Tasks 6 and 7: gaps in coverage where a
/// plausible-looking but wrong implementation would still have passed every
/// existing test.
/// </summary>
[TestClass]
public sealed class RegressionGuardTests
{
    // Guard A. GgufKvCacheEstimatorTests covers the 32-token boundary and the
    // 33-token one-over case, but nothing strictly below one full block. That is
    // the "value < divisor" branch of the ceiling-divide: a regression that
    // computed blocks as `value / divisor` (integer division truncating to zero)
    // would return zero bytes for any sub-block quantity - a total false-safe
    // that the boundary tests alone cannot catch
    [TestMethod]
    public void BelowOneBlock_StillChargesAWholeBlock()
    {
        // headDim 1, one kv head, one layer, 31 tokens: 31 values in Q8_0 still
        // needs one whole 32-value block, so 34 bytes per tensor and 68 for K
        // and V together.
        InspectedModelFacts facts = InspectedModelFacts.Create(
            ByteCount.FromBytes(4_000_000_000),
            layerCount: 1,
            embeddingSize: 1,
            attentionHeadCount: 1,
            keyValueHeadCount: 1,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

        bool established = GgufKvCacheEstimator.TryEstimate(
            facts,
            ContextTokenCount.FromTokens(31),
            GgufKvCacheFormat.Q8_0,
            out ByteCount bytes,
            out EstimationUnavailableReason reason);

        Assert.IsTrue(established, $"Expected an established estimate, got {reason}.");
        Assert.AreEqual(68UL, bytes.Bytes);
    }

    // Guard B. GgufWeightEstimatorTests only asserts ordering for a converted
    // target ("bigger encoding costs more, smaller costs less"), so an
    // implementation that computed the overhead on the unscaled file length
    // instead of the scaled payload would still pass every existing test. this
    // pins the exact byte figure
    //
    // Derivation (checked against a standalone decimal calculation using the
    // same EstimatorPolicy.ProvisionalV1 constants and ByteCount rounding
    // rules - MultiplyByFraction rounds up via Math.Ceiling, AlignUpTo rounds
    // up to the next multiple of the alignment):
    //   ratio           = 8.5m / 4.8125m = 1.7662337662337662337662337662m
    //   payloadExact    = 4,000,000,000 * ratio = 7,064,935,064.9350649350649350648m
    //   payload         = ceiling(payloadExact) = 7,064,935,065
    //   overheadExact   = payload * 0.03m = 211,948,051.95m
    //   overhead        = ceiling(overheadExact) = 211,948,052
    //   total           = payload + overhead = 7,276,883,117
    //   aligned (4096)  = 7,276,883,117 rounds up to 7,276,883,968
    // Two independent derivations agree on 7,276,883,968.
    [TestMethod]
    public void ConvertedTarget_MatchesTheHandDerivedByteFigure()
    {
        InspectedModelFacts facts = InspectedModelFacts.Create(
            ByteCount.FromBytes(4_000_000_000),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

        bool established = GgufWeightEstimator.TryEstimate(
            facts,
            GgufWeightFormat.Q8_0,
            EstimatorPolicy.ProvisionalV1(),
            out ByteCount bytes,
            out bool scaledAcrossQuantisation,
            out EstimationUnavailableReason reason);

        Assert.IsTrue(established, $"Expected an established estimate, got {reason}.");
        Assert.IsTrue(scaledAcrossQuantisation);
        Assert.AreEqual(7_276_883_968UL, bytes.Bytes);
    }

    // Guard C. The existing overflow test uses Imported, where
    // scaledAcrossQuantisation is never set true in the first place, so the
    // reset of that flag inside the catch block is never exercised. this drives
    // overflow through a converted target so the flag is true right before the
    // overflow and must be observed false afterward
    //
    // fileLength = ulong.MaxValue does not exercise this: for target Q8_0 the
    // scaled payload (~3.26e19) itself exceeds ulong.MaxValue, so the overflow
    // is thrown by MultiplyByFraction's (ulong)Math.Ceiling(...) cast on the
    // line before `scaledAcrossQuantisation = true` runs, and the flag never
    // becomes true in the first place. the input below is derived to let the
    // scaling succeed and instead overflow the +3% overhead addition, so the
    // flag really is true when the catch block resets it
    //
    // Derivation: ratio = 8.5m / 4.8125m = 136/77 exactly (BitsPerWeight for
    // Q8_0 over Q4_K_M). The scaled payload is
    // ceiling(fileLength * 136/77); it fits in ulong exactly when
    //   fileLength <= floor(ulong.MaxValue * 77 / 136).
    // ulong.MaxValue * 77 = 1,420,399,293,675,635,474,355 (exact integer
    // arithmetic), and floor(that / 136) = 10,444,112,453,497,319,664. At this
    // fileLength the scaled payload lands exactly on ulong.MaxValue
    // (18,446,744,073,709,551,615), verified against the same decimal
    // arithmetic C# uses:
    //   ratio        = 1.7662337662337662337662337662m
    //   payloadExact = 18,446,744,073,709,551,614.337662337m
    //   payload      = ceiling(payloadExact) = 18,446,744,073,709,551,615  (= ulong.MaxValue)
    //   overhead     = ceiling(payload * 0.03m) = 553,402,322,211,286,549
    // payload is already ulong.MaxValue and overhead is strictly positive, so
    // the checked `payload.Add(overhead)` inside ByteCount.Add necessarily
    // overflows - scaling has already succeeded and set the flag true by then
    [TestMethod]
    public void OverflowOnAConvertedTarget_ResetsTheScalingFlag()
    {
        InspectedModelFacts facts = InspectedModelFacts.Create(
            ByteCount.FromBytes(10_444_112_453_497_319_664UL),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

        bool established = GgufWeightEstimator.TryEstimate(
            facts,
            GgufWeightFormat.Q8_0,
            EstimatorPolicy.ProvisionalV1(),
            out ByteCount bytes,
            out bool scaledAcrossQuantisation,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.QuantitiesExceedRepresentableRange),
            reason.ToString());
        Assert.AreEqual(ByteCount.Zero, bytes);
        Assert.IsFalse(scaledAcrossQuantisation);
    }
}
