using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Estimation;

[TestClass]
public sealed class EstimatorPolicyTests
{
    [TestMethod]
    public void ProvisionalV1_IsProvisionalNotCalibrated()
    {
        // These numbers are documented defaults, not measurements. Claiming
        // Calibrated here would let a higher evidence grade become reachable
        // without a predicted-versus-measured dataset behind it.
        Assert.AreEqual(
            nameof(PolicyProvenance.Provisional),
            EstimatorPolicy.ProvisionalV1().Provenance.ToString());
    }

    [TestMethod]
    public void ProvisionalV1_CarriesAStableVersionString()
    {
        Assert.AreEqual("estimator-policy-v1", EstimatorPolicy.ProvisionalV1().PolicyVersion);
    }

    [TestMethod]
    public void ProvisionalV1_VersionIsDistinctFromTheSafetyPolicyVersion()
    {
        // Two policies that calibrate from different datasets must be versioned
        // separately, or recalibrating one silently invalidates the other's claim.
        Assert.AreNotEqual(
            SafetyPolicy.ProvisionalV1().PolicyVersion,
            EstimatorPolicy.ProvisionalV1().PolicyVersion);
    }

    [TestMethod]
    public void Absent_IsAbsentProvenance()
    {
        Assert.AreEqual(
            nameof(PolicyProvenance.Absent),
            EstimatorPolicy.Absent().Provenance.ToString());
    }

    [TestMethod]
    public void Absent_ExposesNoTerms()
    {
        // Reaching for a term on an absent policy is the moment a constant would
        // be invented, so it throws rather than returning zero.
        Assert.ThrowsExactly<InvalidOperationException>(
            () => _ = EstimatorPolicy.Absent().Terms);
    }

    [TestMethod]
    public void Absent_RefusesToSizeAComputeBuffer()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => EstimatorPolicy.Absent().ComputeBufferFor(ContextTokenCount.FromTokens(4096)));
    }

    [TestMethod]
    [DataRow("AllocationAlignment")]
    [DataRow("ComputeBufferFloor")]
    [DataRow("ComputeBufferBytesPerContextToken")]
    [DataRow("CpuBackendAllocation")]
    [DataRow("GpuBackendAllocation")]
    [DataRow("StagingBufferFloor")]
    [DataRow("ApplicationOverhead")]
    public void ProvisionalV1_EveryByteTermIsPositive(string termName)
    {
        EstimatorTerms terms = EstimatorPolicy.ProvisionalV1().Terms;

        ulong value = termName switch
        {
            "AllocationAlignment" => terms.AllocationAlignment,
            "ComputeBufferFloor" => terms.ComputeBufferFloor.Bytes,
            "ComputeBufferBytesPerContextToken" => terms.ComputeBufferBytesPerContextToken,
            "CpuBackendAllocation" => terms.CpuBackendAllocation.Bytes,
            "GpuBackendAllocation" => terms.GpuBackendAllocation.Bytes,
            "StagingBufferFloor" => terms.StagingBufferFloor.Bytes,
            "ApplicationOverhead" => terms.ApplicationOverhead.Bytes,
            _ => throw new ArgumentOutOfRangeException(nameof(termName))
        };

        Assert.IsTrue(value > 0, $"{termName} must be a positive number of bytes.");
    }

    [TestMethod]
    public void ProvisionalV1_OverheadFractionsAreBetweenZeroAndOne()
    {
        EstimatorTerms terms = EstimatorPolicy.ProvisionalV1().Terms;

        Assert.IsTrue(terms.WeightOverheadFraction is > 0m and < 1m);
        Assert.IsTrue(terms.StagingBufferFraction is > 0m and < 1m);
    }

    [TestMethod]
    public void ComputeBufferFor_NeverFallsBelowTheFloor()
    {
        EstimatorPolicy policy = EstimatorPolicy.ProvisionalV1();

        Assert.AreEqual(
            policy.Terms.ComputeBufferFloor.Bytes,
            policy.ComputeBufferFor(ContextTokenCount.FromTokens(1)).Bytes);
    }

    [TestMethod]
    public void ComputeBufferFor_NeverShrinksAsContextGrows()
    {
        EstimatorPolicy policy = EstimatorPolicy.ProvisionalV1();
        ulong previous = 0;

        foreach (int tokens in new[] { 1024, 2048, 4096, 8192, 16384, 32768 })
        {
            ulong current = policy.ComputeBufferFor(ContextTokenCount.FromTokens(tokens)).Bytes;

            Assert.IsTrue(
                current >= previous,
                $"A larger context must never need a smaller compute buffer ({tokens} tokens).");

            previous = current;
        }
    }

    [TestMethod]
    public void ComputeBufferFor_ScalesWithContextOnceTheFloorIsExceeded()
    {
        EstimatorPolicy policy = EstimatorPolicy.ProvisionalV1();

        ulong atSixteenK = policy.ComputeBufferFor(ContextTokenCount.FromTokens(16384)).Bytes;
        ulong atThirtyTwoK = policy.ComputeBufferFor(ContextTokenCount.FromTokens(32768)).Bytes;

        Assert.IsTrue(
            atThirtyTwoK > atSixteenK,
            "Above the floor the compute buffer must track the context length.");
    }
}
