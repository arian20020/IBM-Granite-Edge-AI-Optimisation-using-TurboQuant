using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.Gguf;

[TestClass]
public sealed class GgufKvCacheEstimatorTests
{
    private static InspectedModelFacts Facts(
        int? layers = 32,
        int? embedding = 4096,
        int? attentionHeads = 32,
        int? kvHeads = 8) =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(4_000_000_000),
            layers,
            embedding,
            attentionHeads,
            kvHeads,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

    private static ulong Estimate(
        GgufKvCacheFormat format,
        int tokens = 4096,
        InspectedModelFacts? facts = null)
    {
        bool established = GgufKvCacheEstimator.TryEstimate(
            facts ?? Facts(),
            ContextTokenCount.FromTokens(tokens),
            format,
            out ByteCount bytes,
            out EstimationUnavailableReason reason);

        Assert.IsTrue(established, $"Expected an established estimate, got {reason}.");
        return bytes.Bytes;
    }

    [TestMethod]
    public void F16_MatchesTheHandCalculatedFigure()
    {
        // headDim = 4096 / 32 = 128. Per layer, per tensor:
        //   4096 tokens x 8 kv heads x 128 = 4,194,304 values x 2 bytes = 8,388,608.
        // Two tensors (K and V) across 32 layers: 8,388,608 x 2 x 32 = 536,870,912.
        Assert.AreEqual(536_870_912UL, Estimate(GgufKvCacheFormat.F16));
    }

    [TestMethod]
    public void Q8_0_UsesTheEncodedBlockSizeNotTheNominalByte()
    {
        // 4,194,304 values / 32 per block = 131,072 blocks x 34 bytes = 4,456,448
        // per tensor. Two tensors, 32 layers: 285,212,672.
        // A nominal "one byte per value" would give 268,435,456 - a 6% understatement.
        Assert.AreEqual(285_212_672UL, Estimate(GgufKvCacheFormat.Q8_0));
    }

    [TestMethod]
    public void TurboQuant3Bit_UsesThePinnedAtomicBotFiftyByteBlock()
    {
        // 4,194,304 values / 128 per block = 32,768 blocks x 50 bytes.
        // Two tensors, 32 layers: 104,857,600.
        Assert.AreEqual(104_857_600UL, Estimate(GgufKvCacheFormat.TurboQuant3Bit));
    }

    [TestMethod]
    public void GraniteThreeB_PerHeadTurboPaddingMatchesPinnedAtomicBotLayout()
    {
        // Granite 3B: head dimension 2560 / 40 = 64. AtomicBot pads each
        // TurboQuant head independently to 128 values before applying blocks.
        InspectedModelFacts granite = Facts(
            layers: 40,
            embedding: 2560,
            attentionHeads: 40,
            kvHeads: 8);

        Assert.AreEqual(
            178_257_920UL,
            Estimate(GgufKvCacheFormat.TurboQuant4Bit, facts: granite));
        Assert.AreEqual(
            131_072_000UL,
            Estimate(GgufKvCacheFormat.TurboQuant3Bit, facts: granite));
    }

    [TestMethod]
    [DataRow(64, 136UL)]
    [DataRow(128, 136UL)]
    [DataRow(129, 272UL)]
    public void TurboQuant_PadsEachHeadToTheNext128ValueBoundary(
        int headDimension,
        ulong expectedBytes)
    {
        InspectedModelFacts facts = Facts(
            layers: 1,
            embedding: headDimension * 4,
            attentionHeads: 4,
            kvHeads: 1);

        Assert.AreEqual(
            expectedBytes,
            Estimate(
                GgufKvCacheFormat.TurboQuant4Bit,
                tokens: 1,
                facts: facts));
    }

    [TestMethod]
    public void TurboQuant_HighGqaAutoAsymmetryFailsClosed()
    {
        bool established = GgufKvCacheEstimator.TryEstimate(
            Facts(layers: 1, embedding: 384, attentionHeads: 6, kvHeads: 1),
            ContextTokenCount.FromTokens(1),
            GgufKvCacheFormat.TurboQuant4Bit,
            out ByteCount bytes,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(ByteCount.Zero, bytes);
        Assert.AreEqual(
            EstimationUnavailableReason.UnknownArchitecture,
            reason);
    }

    [TestMethod]
    public void TurboQuant2Bit_UsesPaddingOnlyWithoutClaimingActivationSupport()
    {
        InspectedModelFacts facts = Facts(
            layers: 1,
            embedding: 64 * 4,
            attentionHeads: 4,
            kvHeads: 1);

        // Two tokens prove padding is per head: 2 x padded width 128 is two
        // blocks per tensor, 34 bytes each. Admission/activation remains a
        // separate capability decision outside this size-only estimator
        Assert.AreEqual(
            136UL,
            Estimate(
                GgufKvCacheFormat.TurboQuant2Bit,
                tokens: 2,
                facts: facts));
    }

    [TestMethod]
    public void PartialBlocks_RoundUpToAWholeBlock()
    {
        // headDim 1, one kv head, one layer, 33 tokens: 33 values in Q8_0 needs
        // two 32-value blocks, so 68 bytes per tensor and 136 for K and V.
        InspectedModelFacts facts = Facts(layers: 1, embedding: 1, attentionHeads: 1, kvHeads: 1);

        Assert.AreEqual(136UL, Estimate(GgufKvCacheFormat.Q8_0, tokens: 33, facts: facts));
    }

    [TestMethod]
    public void ExactBlockBoundary_DoesNotAddASpareBlock()
    {
        InspectedModelFacts facts = Facts(layers: 1, embedding: 1, attentionHeads: 1, kvHeads: 1);

        Assert.AreEqual(68UL, Estimate(GgufKvCacheFormat.Q8_0, tokens: 32, facts: facts));
    }

    [TestMethod]
    public void GroupedQueryAttention_UsesKeyValueHeadsNotAttentionHeads()
    {
        // Eight kv heads against 32 attention heads is a factor of four. Using the
        // attention head count would overstate the cache four times over
        ulong eight = Estimate(GgufKvCacheFormat.F16, facts: Facts(kvHeads: 8));
        ulong thirtyTwo = Estimate(GgufKvCacheFormat.F16, facts: Facts(kvHeads: 32));

        Assert.AreEqual(eight * 4, thirtyTwo);
    }

    [TestMethod]
    public void DoublingContext_DoublesTheCache()
    {
        Assert.AreEqual(
            Estimate(GgufKvCacheFormat.F16, tokens: 2048) * 2,
            Estimate(GgufKvCacheFormat.F16, tokens: 4096));
    }

    [TestMethod]
    [DataRow(null, 4096, 32, 8)]
    [DataRow(32, null, 32, 8)]
    [DataRow(32, 4096, null, 8)]
    [DataRow(32, 4096, 32, null)]
    public void AnyMissingArchitecturalFact_CollapsesTheEstimate(
        int? layers,
        int? embedding,
        int? attentionHeads,
        int? kvHeads)
    {
        bool established = GgufKvCacheEstimator.TryEstimate(
            Facts(layers, embedding, attentionHeads, kvHeads),
            ContextTokenCount.FromTokens(4096),
            GgufKvCacheFormat.F16,
            out ByteCount bytes,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownArchitecture),
            reason.ToString());
        Assert.AreEqual(ByteCount.Zero, bytes);
    }

    [TestMethod]
    public void NonDivisibleHeadDimension_CollapsesTheEstimate()
    {
        // 4097 / 32 is not a whole head dimension, so the architecture as read
        // does not describe a model this estimator can size
        bool established = GgufKvCacheEstimator.TryEstimate(
            Facts(embedding: 4097),
            ContextTokenCount.FromTokens(4096),
            GgufKvCacheFormat.F16,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownArchitecture),
            reason.ToString());
    }

    [TestMethod]
    public void UnspecifiedFormat_IsRejectedRatherThanSizedAsZero()
    {
        bool established = GgufKvCacheEstimator.TryEstimate(
            Facts(),
            ContextTokenCount.FromTokens(4096),
            GgufKvCacheFormat.Unspecified,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnsupportedCacheFormat),
            reason.ToString());
    }

    [TestMethod]
    public void ExtremeArchitecture_OverflowsLoudlyRatherThanWrapping()
    {
        // a wrapped multiplication would report a tiny cache for an enormous
        // model, which is the worst possible false-safe result
        InspectedModelFacts facts = Facts(
            layers: int.MaxValue,
            embedding: int.MaxValue,
            attentionHeads: 1,
            kvHeads: int.MaxValue);

        bool established = GgufKvCacheEstimator.TryEstimate(
            facts,
            ContextTokenCount.FromTokens(int.MaxValue),
            GgufKvCacheFormat.F16,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.QuantitiesExceedRepresentableRange),
            reason.ToString());
    }

    [TestMethod]
    [DataRow(nameof(GgufKvCacheFormat.F16), 1, 2)]
    [DataRow(nameof(GgufKvCacheFormat.Q8_0), 32, 34)]
    [DataRow(nameof(GgufKvCacheFormat.TurboQuant4Bit), 128, 68)]
    [DataRow(nameof(GgufKvCacheFormat.TurboQuant3Bit), 128, 50)]
    [DataRow(nameof(GgufKvCacheFormat.TurboQuant2Bit), 128, 34)]
    public void BlockSpec_RecordsTheRealEncoding(
        string format,
        int valuesPerBlock,
        int bytesPerBlock)
    {
        GgufKvCacheBlockSpec spec = GgufKvCacheBlockSpec.For(
            Enum.Parse<GgufKvCacheFormat>(format));

        Assert.AreEqual(valuesPerBlock, spec.ValuesPerBlock);
        Assert.AreEqual(bytesPerBlock, spec.BytesPerBlock);
    }
}
