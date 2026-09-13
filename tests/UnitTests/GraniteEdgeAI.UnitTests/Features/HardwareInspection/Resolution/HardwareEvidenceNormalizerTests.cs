using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;

[TestClass]
public sealed class HardwareEvidenceNormalizerTests
{
    private const double HalfByteInGiB = 0.5 / 1_073_741_824d;

    [TestMethod]
    [TestCategory("Unit")]
    public void GibConversion_UsesCheckedBinaryUnitsAndMidpointToEven()
    {
        Assert.IsTrue(HardwareEvidenceNormalizer.TryConvertGibToBytes(0, out ulong zero));
        Assert.AreEqual(0UL, zero);

        Assert.IsTrue(HardwareEvidenceNormalizer.TryConvertGibToBytes(1, out ulong oneGiB));
        Assert.AreEqual(1_073_741_824UL, oneGiB);

        Assert.IsTrue(HardwareEvidenceNormalizer.TryConvertGibToBytes(
            HalfByteInGiB,
            out ulong halfByte));
        Assert.AreEqual(0UL, halfByte);

        Assert.IsTrue(HardwareEvidenceNormalizer.TryConvertGibToBytes(
            3 * HalfByteInGiB,
            out ulong oneAndAHalfBytes));
        Assert.AreEqual(2UL, oneAndAHalfBytes);

        Assert.IsTrue(HardwareEvidenceNormalizer.TryConvertGibToBytes(
            16_384,
            out ulong maximumProviderValue));
        Assert.AreEqual(17_592_186_044_416UL, maximumProviderValue);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GibConversion_RejectsInvalidOrOverflowingInputWithoutAValue()
    {
        foreach (double invalid in new[]
                 {
                     -1d,
                     double.NaN,
                     double.NegativeInfinity,
                     double.PositiveInfinity,
                     double.MaxValue,
                 })
        {
            Assert.IsFalse(HardwareEvidenceNormalizer.TryConvertGibToBytes(
                invalid,
                out ulong converted));
            Assert.AreEqual(0UL, converted);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Freshness_AcceptsExactDynamicAndFutureBoundaries()
    {
        Assert.AreEqual(
            EvidenceFreshness.Accepted,
            HardwareEvidenceNormalizer.GetFreshness(
                HardwareResolutionTestData.Now.AddSeconds(-30),
                HardwareResolutionTestData.Now,
                HardwareResolutionPolicy.DynamicMemoryMaximumAge));
        Assert.AreEqual(
            EvidenceFreshness.Stale,
            HardwareEvidenceNormalizer.GetFreshness(
                HardwareResolutionTestData.Now.AddSeconds(-30).AddTicks(-1),
                HardwareResolutionTestData.Now,
                HardwareResolutionPolicy.DynamicMemoryMaximumAge));
        Assert.AreEqual(
            EvidenceFreshness.Accepted,
            HardwareEvidenceNormalizer.GetFreshness(
                HardwareResolutionTestData.Now.AddSeconds(5),
                HardwareResolutionTestData.Now,
                HardwareResolutionPolicy.DynamicMemoryMaximumAge));
        Assert.AreEqual(
            EvidenceFreshness.Future,
            HardwareEvidenceNormalizer.GetFreshness(
                HardwareResolutionTestData.Now.AddSeconds(5).AddTicks(1),
                HardwareResolutionTestData.Now,
                HardwareResolutionPolicy.DynamicMemoryMaximumAge));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Freshness_AcceptsExactStaticBoundaryAndRejectsInvalidClockInputs()
    {
        Assert.AreEqual(
            EvidenceFreshness.Accepted,
            HardwareEvidenceNormalizer.GetFreshness(
                HardwareResolutionTestData.Now.AddMinutes(-5),
                HardwareResolutionTestData.Now,
                HardwareResolutionPolicy.StaticEvidenceMaximumAge));
        Assert.AreEqual(
            EvidenceFreshness.Stale,
            HardwareEvidenceNormalizer.GetFreshness(
                HardwareResolutionTestData.Now.AddMinutes(-5).AddTicks(-1),
                HardwareResolutionTestData.Now,
                HardwareResolutionPolicy.StaticEvidenceMaximumAge));

        Assert.Throws<ArgumentException>(() =>
            HardwareEvidenceNormalizer.GetFreshness(
                HardwareResolutionTestData.Now.ToOffset(TimeSpan.FromHours(1)),
                HardwareResolutionTestData.Now,
                TimeSpan.FromSeconds(30)));
        Assert.Throws<ArgumentException>(() =>
            HardwareEvidenceNormalizer.GetFreshness(
                HardwareResolutionTestData.Now,
                HardwareResolutionTestData.Now.ToOffset(TimeSpan.FromHours(1)),
                TimeSpan.FromSeconds(30)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HardwareEvidenceNormalizer.GetFreshness(
                HardwareResolutionTestData.Now,
                HardwareResolutionTestData.Now,
                TimeSpan.FromTicks(-1)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void UnsignedTolerance_DoesNotOverflowAtEitherOrdering()
    {
        Assert.IsTrue(HardwareEvidenceNormalizer.AreWithinTolerance(
            ulong.MaxValue,
            0,
            ulong.MaxValue));
        Assert.IsTrue(HardwareEvidenceNormalizer.AreWithinTolerance(
            0,
            ulong.MaxValue,
            ulong.MaxValue));
        Assert.IsFalse(HardwareEvidenceNormalizer.AreWithinTolerance(
            ulong.MaxValue,
            0,
            ulong.MaxValue - 1));
        Assert.IsTrue(HardwareEvidenceNormalizer.AreWithinTolerance(100, 90, 10));
        Assert.IsFalse(HardwareEvidenceNormalizer.AreWithinTolerance(100, 89, 10));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AvailableMemoryTolerance_UsesTwoGiBMinimumThenTenPercent()
    {
        Assert.AreEqual(
            2 * HardwareResolutionTestData.GiB,
            HardwareEvidenceNormalizer.GetAvailableMemoryTolerance(
                19 * HardwareResolutionTestData.GiB));
        Assert.AreEqual(
            2 * HardwareResolutionTestData.GiB,
            HardwareEvidenceNormalizer.GetAvailableMemoryTolerance(
                20 * HardwareResolutionTestData.GiB));
        Assert.AreEqual(
            3 * HardwareResolutionTestData.GiB,
            HardwareEvidenceNormalizer.GetAvailableMemoryTolerance(
                30 * HardwareResolutionTestData.GiB));
    }
}
