using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class SafetyPolicyTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    [TestMethod]
    public void ProvisionalV1_DeclaresItsProvenanceHonestly()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();

        Assert.AreEqual(PolicyProvenance.Provisional, policy.Provenance);
        Assert.AreEqual("fit-safety-policy-v1", policy.PolicyVersion);
    }

    [TestMethod]
    public void ProvisionalV1_UsesTheApprovedThresholds()
    {
        FitThresholds thresholds = SafetyPolicy.ProvisionalV1().Thresholds;

        Assert.AreEqual(0.75m, thresholds.ComfortableCeiling);
        Assert.AreEqual(0.90m, thresholds.ModerateHeadroomCeiling);
        Assert.AreEqual(1.00m, thresholds.NarrowCeiling);
    }

    [TestMethod]
    public void CalibrationMargin_SmallPeak_UsesTheAbsoluteFloor()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();

        ByteCount margin = policy.CalibrationMarginFor(ByteCount.FromBytes(100));

        Assert.AreEqual(policy.CalibrationMarginFloor, margin);
    }

    [TestMethod]
    public void CalibrationMargin_LargePeak_UsesThePercentage()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();

        ByteCount margin = policy.CalibrationMarginFor(ByteCount.FromBytes(100 * Gibibyte));

        Assert.IsTrue(margin > policy.CalibrationMarginFloor);
        Assert.AreEqual((ulong)(100 * Gibibyte * 0.10m), margin.Bytes);
    }

    [TestMethod]
    public void CalibrationMargin_IsAlwaysPositive_SoItCanOnlyIncreaseARequirement()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();

        // A margin that shrank a requirement would manufacture a false-safe fit.
        Assert.IsTrue(policy.CalibrationMarginFor(ByteCount.FromBytes(1)) > ByteCount.Zero);
        Assert.IsTrue(policy.CalibrationMarginFor(ByteCount.Zero) > ByteCount.Zero);
    }

    [TestMethod]
    public void CalibrationMargin_IsMonotonicInPredictedPeak()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();

        ByteCount smaller = policy.CalibrationMarginFor(ByteCount.FromBytes(1_000_000_000));
        ByteCount larger = policy.CalibrationMarginFor(ByteCount.FromBytes(2_000_000_000));

        Assert.IsTrue(larger >= smaller);
    }

    [TestMethod]
    public void AbsentPolicy_ExposesNoThresholds()
    {
        SafetyPolicy policy = SafetyPolicy.Absent();

        Assert.AreEqual(PolicyProvenance.Absent, policy.Provenance);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = policy.Thresholds);
    }

    [TestMethod]
    public void Absent_ExposesNoTerms()
    {
        // Mirrors EstimatorPolicyTests.Absent_ExposesNoTerms: reaching for an
        // allowance on an absent policy is the moment a zero-margin,
        // full-availability budget would be handed out, so it throws rather
        // than returning zero.
        SafetyPolicy policy = SafetyPolicy.Absent();

        Assert.ThrowsExactly<InvalidOperationException>(() => _ = policy.OsAllowance);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = policy.OperationalReserve);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = policy.CalibrationMarginFloor);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = policy.CalibrationMarginFraction);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => policy.CalibrationMarginFor(ByteCount.FromBytes(1)));
    }
}
