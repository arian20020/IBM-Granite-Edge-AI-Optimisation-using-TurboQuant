using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.Gguf;

[TestClass]
public sealed class GgufWeightEstimatorTests
{
    // File type 15 is Q4_K_M, quantisation version 2.
    private static InspectedModelFacts Facts(
        ulong fileLength = 4_000_000_000,
        int? fileType = 15,
        int? quantisationVersion = 2) =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(fileLength),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 32768,
            fileType,
            quantisationVersion);

    private static ulong Estimate(
        GgufWeightFormat target,
        InspectedModelFacts? facts = null,
        EstimatorPolicy? policy = null)
    {
        bool established = GgufWeightEstimator.TryEstimate(
            facts ?? Facts(),
            target,
            policy ?? EstimatorPolicy.ProvisionalV1(),
            out ByteCount bytes,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsTrue(established, $"Expected an established estimate, got {reason}.");
        return bytes.Bytes;
    }

    [TestMethod]
    public void Imported_IsFileLengthPlusOverheadAlignedUpward()
    {
        // 4,000,000,000 + ceil(4,000,000,000 x 0.03) = 4,120,000,000.
        // 4,120,000,000 mod 4096 = 1,536, so it aligns up to 4,120,002,560.
        Assert.AreEqual(4_120_002_560UL, Estimate(GgufWeightFormat.Imported));
    }

    [TestMethod]
    public void Imported_NeedsNoSourceQuantisation()
    {
        // The imported file is used unchanged, so its encoding is irrelevant to
        // its size. This is the common path and must survive an unread file type.
        bool established = GgufWeightEstimator.TryEstimate(
            Facts(fileType: null, quantisationVersion: null),
            GgufWeightFormat.Imported,
            EstimatorPolicy.ProvisionalV1(),
            out ByteCount bytes,
            out bool scaled,
            out EstimationUnavailableReason reason);

        Assert.IsTrue(established, $"Expected an established estimate, got {reason}.");
        Assert.IsFalse(scaled);
        Assert.IsTrue(bytes.Bytes > 0);
    }

    [TestMethod]
    public void ConvertingToALargerEncoding_ProducesMoreBytes()
    {
        // Q4_K_M at 4.8125 bits to Q8_0 at 8.5 bits is a factor of about 1.77.
        Assert.IsTrue(
            Estimate(GgufWeightFormat.Q8_0) > Estimate(GgufWeightFormat.Imported),
            "A higher-precision target must not estimate smaller than the source.");
    }

    [TestMethod]
    public void ConvertingToASmallerEncoding_ProducesFewerBytes()
    {
        Assert.IsTrue(
            Estimate(GgufWeightFormat.Q3KM) < Estimate(GgufWeightFormat.Imported),
            "A lower-precision target must not estimate larger than the source.");
    }

    [TestMethod]
    public void ConvertingToTheSourceEncoding_MatchesTheImportedEstimate()
    {
        // Source is Q4_K_M, so requesting Q4KM is a ratio of one.
        Assert.AreEqual(
            Estimate(GgufWeightFormat.Imported),
            Estimate(GgufWeightFormat.Q4KM));
    }

    [TestMethod]
    public void ScalingFlag_IsSetOnlyWhenTheEncodingChanged()
    {
        GgufWeightEstimator.TryEstimate(
            Facts(),
            GgufWeightFormat.Imported,
            EstimatorPolicy.ProvisionalV1(),
            out _,
            out bool importedScaled,
            out _);

        GgufWeightEstimator.TryEstimate(
            Facts(),
            GgufWeightFormat.Q8_0,
            EstimatorPolicy.ProvisionalV1(),
            out _,
            out bool convertedScaled,
            out _);

        Assert.IsFalse(importedScaled);
        Assert.IsTrue(convertedScaled);
    }

    [TestMethod]
    public void UnknownSourceEncoding_BlocksOnlyAConvertedTarget()
    {
        bool established = GgufWeightEstimator.TryEstimate(
            Facts(fileType: 9999),
            GgufWeightFormat.Q8_0,
            EstimatorPolicy.ProvisionalV1(),
            out ByteCount bytes,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownSourceQuantisation),
            reason.ToString());
        Assert.AreEqual(ByteCount.Zero, bytes);
    }

    [TestMethod]
    public void UnspecifiedTargetFormat_IsRejected()
    {
        bool established = GgufWeightEstimator.TryEstimate(
            Facts(),
            GgufWeightFormat.Unspecified,
            EstimatorPolicy.ProvisionalV1(),
            out _,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnsupportedWeightFormat),
            reason.ToString());
    }

    [TestMethod]
    public void AbsentPolicy_RefusesRatherThanUsingAZeroOverhead()
    {
        bool established = GgufWeightEstimator.TryEstimate(
            Facts(),
            GgufWeightFormat.Imported,
            EstimatorPolicy.Absent(),
            out _,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.EstimatorPolicyUnavailable),
            reason.ToString());
    }

    [TestMethod]
    public void Result_IsAlwaysAlignedToTheAllocationBoundary()
    {
        ulong alignment = EstimatorPolicy.ProvisionalV1().Terms.AllocationAlignment;

        foreach (ulong length in new ulong[] { 1, 4095, 4096, 4097, 1_234_567_890 })
        {
            Assert.AreEqual(
                0UL,
                Estimate(GgufWeightFormat.Imported, Facts(fileLength: length)) % alignment,
                $"A {length}-byte model must still align to {alignment}.");
        }
    }

    [TestMethod]
    public void Result_IsNeverSmallerThanTheFileItself()
    {
        // Overhead is added, never netted off. A weight estimate below the file
        // length would be a false-safe result for the imported baseline.
        foreach (ulong length in new ulong[] { 1024, 1_000_000, 8_000_000_000 })
        {
            Assert.IsTrue(
                Estimate(GgufWeightFormat.Imported, Facts(fileLength: length)) >= length);
        }
    }

    [TestMethod]
    public void ExtremeFileLength_OverflowsLoudlyRatherThanWrapping()
    {
        bool established = GgufWeightEstimator.TryEstimate(
            Facts(fileLength: ulong.MaxValue),
            GgufWeightFormat.Imported,
            EstimatorPolicy.ProvisionalV1(),
            out _,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.QuantitiesExceedRepresentableRange),
            reason.ToString());
    }
}
