using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// The whole computational spine: model facts to components to per-pool peaks to
/// a fit verdict. Each stage is unit-tested on its own; this proves they compose.
/// </summary>
[TestClass]
public sealed class EstimationSpineTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private static InspectedModelFacts Facts() =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(4 * Gibibyte),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

    private static CompatibilityCandidate Candidate() =>
        CompatibilityCandidate.Create(
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(4096),
            CandidatePreparation.None,
            supportEntryId: "entry-1",
            isExperimental: false,
            isBaseline: true);

    private static FitAssessment AssessWith(ulong availableGibibytes, SafetyPolicy? policy = null)
    {
        ResourceEstimate estimate = GgufResourceEstimator.Estimate(
            Facts(), Candidate(), EstimatorPolicy.ProvisionalV1());

        Assert.AreEqual(nameof(EstimationStatus.Established), estimate.Status.ToString());

        return FitPolicy.Assess(
            ResourcePhaseComposer.Compose(estimate.Components),
            AvailableResources.Create(
                ByteCount.FromBytes(availableGibibytes * Gibibyte),
                dedicatedDeviceMemory: ByteCount.Zero,
                storage: ByteCount.FromBytes(200 * Gibibyte),
                observedAtUtc: DateTimeOffset.UtcNow),
            policy ?? SafetyPolicy.ProvisionalV1());
    }

    [TestMethod]
    public void AGenerousMachine_ReachesASafeVerdict()
    {
        Assert.AreEqual(
            nameof(CompatibilityFitState.Safe),
            AssessWith(availableGibibytes: 48).State.ToString());
    }

    [TestMethod]
    public void AConstrainedMachine_ReachesADoesNotFitVerdict()
    {
        FitAssessment assessment = AssessWith(availableGibibytes: 6);

        Assert.AreEqual(
            nameof(CompatibilityFitState.DoesNotFit),
            assessment.State.ToString());
        Assert.AreEqual(
            nameof(FitLimitingReason.InsufficientSystemMemory),
            assessment.LimitingReason.ToString());
    }

    [TestMethod]
    public void AnAbsentSafetyPolicy_ReachesNotEstablishedRatherThanAGuess()
    {
        FitAssessment assessment = AssessWith(availableGibibytes: 48, SafetyPolicy.Absent());

        Assert.AreEqual(
            nameof(CompatibilityFitState.NotEstablished),
            assessment.State.ToString());
        Assert.AreEqual(
            nameof(FitLimitingReason.SafetyPolicyUnavailable),
            assessment.LimitingReason.ToString());
    }

    [TestMethod]
    public void TheRequirementAlwaysExceedsTheRawPeak()
    {
        // The calibration margin is added to the requirement and never subtracted
        // from it, so an uncalibrated estimator errs toward refusing to run.
        ResourceEstimate estimate = GgufResourceEstimator.Estimate(
            Facts(), Candidate(), EstimatorPolicy.ProvisionalV1());

        ResourcePeakProfile profile = ResourcePhaseComposer.Compose(estimate.Components);
        FitAssessment assessment = AssessWith(availableGibibytes: 48);

        Assert.IsTrue(
            assessment.RequiredBytes > profile.SystemMemoryPressure,
            "The safety margin must be inside the required figure.");
    }
}
