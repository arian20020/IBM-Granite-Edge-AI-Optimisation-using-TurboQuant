using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class FitPolicyTests
{
    private const ulong Gib = 1024UL * 1024 * 1024;

    private static ResourcePeakProfile SystemPeak(ulong bytes) =>
        ResourcePhaseComposer.Compose(
        [
            ResourceComponent.Create(
                ResourceComponentKind.Weights,
                ResourceTarget.SystemMemory,
                ByteCount.FromBytes(bytes),
                new HashSet<LifecyclePhase> { LifecyclePhase.SteadyStateGeneration }),
        ]);

    private static ResourcePeakProfile DedicatedPeak(ulong bytes) =>
        ResourcePhaseComposer.Compose(
        [
            ResourceComponent.Create(
                ResourceComponentKind.Weights,
                ResourceTarget.DedicatedDeviceMemory,
                ByteCount.FromBytes(bytes),
                new HashSet<LifecyclePhase> { LifecyclePhase.SteadyStateGeneration }),
        ]);

    private static AvailableResources Available(ulong systemBytes, bool isFresh = true) =>
        AvailableResources.Create(
            systemMemory: ByteCount.FromBytes(systemBytes),
            dedicatedDeviceMemory: ByteCount.Zero,
            storage: ByteCount.FromBytes(500 * Gib),
            observedAtUtc: DateTimeOffset.UnixEpoch,
            isFresh: isFresh);

    [TestMethod]
    public void AbsentPolicy_AlwaysNotEstablished_AndInventsNoNumber()
    {
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(Gib), Available(64 * Gib), SafetyPolicy.Absent());

        Assert.AreEqual(CompatibilityFitState.NotEstablished, assessment.State);
        Assert.AreEqual(FitLimitingReason.SafetyPolicyUnavailable, assessment.LimitingReason);
        Assert.AreEqual(ByteCount.Zero, assessment.SafeBudget);
    }

    [TestMethod]
    public void StaleAvailability_IsRejectedAsNotEstablished()
    {
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(Gib), Available(20 * Gib, isFresh: false), SafetyPolicy.ProvisionalV1());

        Assert.AreEqual(CompatibilityFitState.NotEstablished, assessment.State);
        Assert.AreEqual(FitLimitingReason.FreshAvailabilityUnavailable, assessment.LimitingReason);
    }

    [TestMethod]
    public void SafeBudget_SubtractsAllowanceAndReserveFromFreshAvailable()
    {
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(Gib), Available(20 * Gib), SafetyPolicy.ProvisionalV1());

        // 20 GiB available - 2 GiB OS allowance - 1 GiB operational reserve.
        Assert.AreEqual(17 * Gib, assessment.SafeBudget.Bytes);
    }

    [TestMethod]
    public void ProportionalV2_DoesNotSubtractWindowsUsageTwice()
    {
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(Gib), Available(4 * Gib), SafetyPolicy.ProportionalV2());

        // Windows already reports memory that is physically available. V2
        // keeps a 512 MiB floor but does not remove a second fixed OS budget.
        Assert.AreEqual(3 * Gib + Gib / 2, assessment.SafeBudget.Bytes);
    }

    [TestMethod]
    public void Required_IncludesTheCalibrationMargin()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();
        ByteCount peak = ByteCount.FromBytes(10 * Gib);

        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(peak.Bytes), Available(20 * Gib), policy);

        Assert.AreEqual(
            peak.Add(policy.CalibrationMarginFor(peak)).Bytes,
            assessment.RequiredBytes.Bytes);
    }

    [TestMethod]
    public void ExhaustedBudget_DoesNotFit_RatherThanUnderflowing()
    {
        // Available is below allowance plus reserve, so the budget collapses to zero.
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(Gib), Available(Gib), SafetyPolicy.ProvisionalV1());

        Assert.AreEqual(CompatibilityFitState.DoesNotFit, assessment.State);
        Assert.AreEqual(FitLimitingReason.InsufficientSystemMemory, assessment.LimitingReason);
        Assert.AreEqual(ByteCount.Zero, assessment.SafeBudget);
        Assert.AreEqual(ByteCount.Zero, assessment.Headroom);
    }

    [TestMethod]
    // Budget is 17 GiB. Required is peak plus max(0.5 GiB, 10% of peak).
    // The expected state travels as a name because a public test method
    // cannot take an internal enum as a parameter.
    [DataRow(10UL, nameof(CompatibilityFitState.Safe))]        // 11.00 / 17 = 0.6471
    [DataRow(13UL, nameof(CompatibilityFitState.Safe))]        // 14.30 / 17 = 0.8412
    [DataRow(14UL, nameof(CompatibilityFitState.Narrow))]      // 15.40 / 17 = 0.9059
    [DataRow(15UL, nameof(CompatibilityFitState.Narrow))]      // 16.50 / 17 = 0.9706
    [DataRow(16UL, nameof(CompatibilityFitState.DoesNotFit))]  // 17.60 / 17 = 1.0353
    [DataRow(17UL, nameof(CompatibilityFitState.DoesNotFit))]  // 18.70 / 17 = 1.1000
    public void State_FollowsTheApprovedThresholdBands(ulong peakGib, string expected)
    {
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(peakGib * Gib), Available(20 * Gib), SafetyPolicy.ProvisionalV1());

        Assert.AreEqual(expected, assessment.State.ToString());
    }

    [TestMethod]
    public void RequiredExactlyEqualToBudget_Fits_BecauseMarginsAreAlreadyIncluded()
    {
        // Peak 10 GiB gives required 11 GiB. Available 14 GiB gives budget 11 GiB.
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(10 * Gib), Available(14 * Gib), SafetyPolicy.ProvisionalV1());

        Assert.AreEqual(assessment.RequiredBytes, assessment.SafeBudget);
        Assert.AreEqual(CompatibilityFitState.Narrow, assessment.State);
        Assert.AreEqual(ByteCount.Zero, assessment.Headroom);
    }

    [TestMethod]
    public void OneByteOverBudget_DoesNotFit()
    {
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(10 * Gib), Available(14 * Gib - 1), SafetyPolicy.ProvisionalV1());

        Assert.AreEqual(CompatibilityFitState.DoesNotFit, assessment.State);
        Assert.AreEqual(FitLimitingReason.InsufficientSystemMemory, assessment.LimitingReason);
    }

    [TestMethod]
    public void Headroom_IsBudgetMinusRequired()
    {
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(10 * Gib), Available(20 * Gib), SafetyPolicy.ProvisionalV1());

        // 17 GiB budget - 11 GiB required.
        Assert.AreEqual(6 * Gib, assessment.Headroom.Bytes);
    }

    [TestMethod]
    public void SharedDeviceMemory_IsChargedToTheSystemGate()
    {
        ResourcePeakProfile profile = ResourcePhaseComposer.Compose(
        [
            ResourceComponent.Create(
                ResourceComponentKind.Weights,
                ResourceTarget.SystemMemory,
                ByteCount.FromBytes(10 * Gib),
                new HashSet<LifecyclePhase> { LifecyclePhase.SteadyStateGeneration }),
            ResourceComponent.Create(
                ResourceComponentKind.KvCache,
                ResourceTarget.SharedDeviceMemory,
                ByteCount.FromBytes(6 * Gib),
                new HashSet<LifecyclePhase> { LifecyclePhase.SteadyStateGeneration }),
        ]);

        // Ignoring shared memory would give required 11 GiB and report Safe.
        // Charging it gives a 16 GiB peak, required 17.6 GiB, over the budget.
        FitAssessment assessment = FitPolicy.Assess(
            profile, Available(20 * Gib), SafetyPolicy.ProvisionalV1());

        Assert.AreEqual(CompatibilityFitState.DoesNotFit, assessment.State);
    }

    [TestMethod]
    public void DedicatedMemory_IsNeverAddedToTheSystemMemoryBudget()
    {
        FitAssessment assessment = FitPolicy.Assess(
            DedicatedPeak(2 * Gib),
            AvailableResources.Create(
                ByteCount.FromBytes(20 * Gib), ByteCount.FromBytes(3 * Gib),
                ByteCount.FromBytes(500 * Gib), DateTimeOffset.UnixEpoch),
            SafetyPolicy.ProvisionalV1());

        Assert.AreEqual(CompatibilityFitState.Safe, assessment.State);
        Assert.AreEqual(17 * Gib, assessment.SafeBudget.Bytes);
        Assert.AreEqual(3 * Gib, assessment.DedicatedSafeBudget.Bytes);
        Assert.AreEqual(2 * Gib + Gib / 2, assessment.DedicatedRequiredBytes.Bytes);
    }

    [TestMethod]
    public void DedicatedMemory_InsufficientAndUnknownFailClosed()
    {
        FitAssessment insufficient = FitPolicy.Assess(
            DedicatedPeak(3 * Gib),
            AvailableResources.Create(
                ByteCount.FromBytes(20 * Gib), ByteCount.FromBytes(3 * Gib),
                ByteCount.FromBytes(500 * Gib), DateTimeOffset.UnixEpoch),
            SafetyPolicy.ProvisionalV1());
        FitAssessment unknown = FitPolicy.Assess(
            DedicatedPeak(Gib),
            AvailableResources.Create(
                ByteCount.FromBytes(20 * Gib), ByteCount.Zero,
                ByteCount.FromBytes(500 * Gib), DateTimeOffset.UnixEpoch,
                dedicatedDeviceMemoryEstablished: false),
            SafetyPolicy.ProvisionalV1());

        Assert.AreEqual(CompatibilityFitState.DoesNotFit, insufficient.State);
        Assert.AreEqual(
            FitLimitingReason.InsufficientDedicatedDeviceMemory,
            insufficient.LimitingReason);
        Assert.AreEqual(CompatibilityFitState.NotEstablished, unknown.State);
        Assert.AreEqual(
            FitLimitingReason.DedicatedAvailabilityUnavailable,
            unknown.LimitingReason);
    }

    [TestMethod]
    public void DedicatedMemory_ExactBoundaryFitsAndOneByteOverDoesNot()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();
        ByteCount peak = ByteCount.FromBytes(2 * Gib);
        ByteCount exact = peak.Add(policy.CalibrationMarginFor(peak));
        FitAssessment fits = FitPolicy.Assess(
            DedicatedPeak(peak.Bytes),
            AvailableResources.Create(
                ByteCount.FromBytes(20 * Gib), exact,
                ByteCount.FromBytes(500 * Gib), DateTimeOffset.UnixEpoch),
            policy);
        FitAssessment fails = FitPolicy.Assess(
            DedicatedPeak(peak.Bytes + 1),
            AvailableResources.Create(
                ByteCount.FromBytes(20 * Gib), exact,
                ByteCount.FromBytes(500 * Gib), DateTimeOffset.UnixEpoch),
            policy);

        Assert.AreEqual(CompatibilityFitState.Narrow, fits.State);
        Assert.AreEqual(CompatibilityFitState.DoesNotFit, fails.State);
    }

    [TestMethod]
    public void Assess_RejectsNullArguments()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => FitPolicy.Assess(null!, Available(Gib), SafetyPolicy.ProvisionalV1()));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => FitPolicy.Assess(SystemPeak(Gib), null!, SafetyPolicy.ProvisionalV1()));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => FitPolicy.Assess(SystemPeak(Gib), Available(Gib), null!));
    }
}
