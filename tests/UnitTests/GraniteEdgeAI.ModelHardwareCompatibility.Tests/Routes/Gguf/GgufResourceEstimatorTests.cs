using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.Gguf;

[TestClass]
public sealed class GgufResourceEstimatorTests
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

    private static CompatibilityCandidate Candidate(
        DeviceRouteId device = DeviceRouteId.Cpu,
        CompatibilityBackend backend = CompatibilityBackend.Cpu,
        GpuOffloadLevel offload = GpuOffloadLevel.None,
        GgufWeightFormat weights = GgufWeightFormat.Imported,
        GgufKvCacheFormat kvCache = GgufKvCacheFormat.F16,
        CandidatePreparation preparation = CandidatePreparation.None,
        int context = 4096) =>
        CompatibilityCandidate.Create(
            GgufRouteConfiguration.Create(weights, kvCache, backend, device, offload),
            ContextTokenCount.FromTokens(context),
            preparation,
            supportEntryId: "entry-1",
            isExperimental: false,
            isBaseline: true);

    private static ResourceEstimate Estimate(
        CompatibilityCandidate? candidate = null,
        InspectedModelFacts? facts = null,
        EstimatorPolicy? policy = null) =>
        GgufResourceEstimator.Estimate(
            facts ?? Facts(),
            candidate ?? Candidate(),
            policy ?? EstimatorPolicy.ProvisionalV1());

    private static ResourceComponent Single(
        ResourceEstimate estimate,
        ResourceComponentKind kind) =>
        estimate.Components.Single(component => component.Kind == kind);

    [TestMethod]
    public void CpuBaseline_ChargesEveryComponentToSystemMemory()
    {
        ResourceEstimate estimate = Estimate();

        Assert.AreEqual(nameof(EstimationStatus.Established), estimate.Status.ToString());

        foreach (ResourceComponent component in estimate.Components)
        {
            Assert.AreEqual(
                ResourceTarget.SystemMemory,
                component.Target,
                $"{component.Kind} must be charged to system memory on a CPU route.");
        }
    }

    [TestMethod]
    public void EveryComponentKind_AppearsAtMostOnce()
    {
        // Section 8: every mandatory component has exactly one owner, so a
        // requirement can never be counted twice.
        ResourceEstimate estimate = Estimate();

        int distinct = estimate.Components.Select(component => component.Kind).Distinct().Count();

        Assert.AreEqual(estimate.Components.Count, distinct);
    }

    [TestMethod]
    public void CpuBaseline_EmitsWeightsKvComputeBackendAndApplicationOverhead()
    {
        ResourceEstimate estimate = Estimate();

        ResourceComponentKind[] kinds =
            [.. estimate.Components.Select(component => component.Kind).Order()];

        CollectionAssert.AreEquivalent(
            new[]
            {
                ResourceComponentKind.Weights,
                ResourceComponentKind.KvCache,
                ResourceComponentKind.ComputeBuffer,
                ResourceComponentKind.BackendAllocation,
                ResourceComponentKind.ApplicationOverhead
            },
            kinds);
    }

    [TestMethod]
    public void Weights_AreLiveInEveryPhase()
    {
        // Weights are resident from load until the model is released, so a peak
        // taken in any phase must include them.
        ResourceComponent weights = Single(Estimate(), ResourceComponentKind.Weights);

        Assert.AreEqual(3, weights.Phases.Count);
    }

    [TestMethod]
    public void KvCache_IsLiveOnlyDuringGeneration()
    {
        ResourceComponent kv = Single(Estimate(), ResourceComponentKind.KvCache);

        Assert.AreEqual(1, kv.Phases.Count);
        Assert.IsTrue(kv.Phases.Contains(LifecyclePhase.SteadyStateGeneration));
    }

    [TestMethod]
    public void DiscreteGpu_SplitsDeviceMemoryFromASystemStagingBuffer()
    {
        ResourceEstimate estimate = Estimate(Candidate(
            device: DeviceRouteId.IntelDiscreteGpu,
            backend: CompatibilityBackend.IntelSycl,
            offload: GpuOffloadLevel.Full));

        Assert.AreEqual(
            ResourceTarget.DedicatedDeviceMemory,
            Single(estimate, ResourceComponentKind.Weights).Target);

        ResourceComponent staging = Single(estimate, ResourceComponentKind.StagingBuffer);

        Assert.AreEqual(ResourceTarget.SystemMemory, staging.Target);
        Assert.AreEqual(1, staging.Phases.Count);
        Assert.IsTrue(
            staging.Phases.Contains(LifecyclePhase.Load),
            "A staging buffer is transient and exists only while loading.");
    }

    [TestMethod]
    public void IntegratedGpu_ChargesSharedMemoryAndNeedsNoStaging()
    {
        ResourceEstimate estimate = Estimate(Candidate(
            device: DeviceRouteId.IntelIntegratedGpu,
            backend: CompatibilityBackend.IntelVulkan,
            offload: GpuOffloadLevel.Full));

        Assert.AreEqual(
            ResourceTarget.SharedDeviceMemory,
            Single(estimate, ResourceComponentKind.Weights).Target);

        Assert.IsFalse(
            estimate.Components.Any(
                component => component.Kind == ResourceComponentKind.StagingBuffer),
            "Integrated graphics read the same RAM, so nothing is staged.");
    }

    [TestMethod]
    public void PartialOffload_IsNotEstablished()
    {
        ResourceEstimate estimate = Estimate(Candidate(
            device: DeviceRouteId.IntelDiscreteGpu,
            backend: CompatibilityBackend.IntelSycl,
            offload: GpuOffloadLevel.Partial));

        Assert.AreEqual(nameof(EstimationStatus.NotEstablished), estimate.Status.ToString());
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownOffloadSplit),
            estimate.Reason.ToString());
    }

    [TestMethod]
    public void WeightConversion_AddsAPersistentArtifactChargedToStorage()
    {
        ResourceEstimate estimate = Estimate(Candidate(
            weights: GgufWeightFormat.Q4KM,
            preparation: CandidatePreparation.WeightConversionRequired));

        ResourceComponent artifact = Single(estimate, ResourceComponentKind.PersistentArtifact);

        Assert.AreEqual(ResourceTarget.Storage, artifact.Target);
        Assert.IsTrue(artifact.Bytes.Bytes > 0);
    }

    [TestMethod]
    public void NoConversion_ChargesNothingToStorage()
    {
        // Running the imported file writes no new artifact, so claiming disk
        // space would be inventing a cost the user does not pay.
        Assert.IsFalse(
            Estimate().Components.Any(
                component => component.Target == ResourceTarget.Storage));
    }

    [TestMethod]
    public void ProvisionalPolicy_RecordsTheUncalibratedLimitation()
    {
        ResourceEstimate estimate = Estimate();

        Assert.IsTrue(
            estimate.Limitations.Contains(EstimationLimitation.UncalibratedEstimatorPolicy));
    }

    [TestMethod]
    public void EveryEstimate_RecordsTheFileLengthAndSingleSequenceLimitations()
    {
        ResourceEstimate estimate = Estimate();

        Assert.IsTrue(
            estimate.Limitations.Contains(EstimationLimitation.WeightsDerivedFromFileLength));
        Assert.IsTrue(
            estimate.Limitations.Contains(EstimationLimitation.SingleSequenceAssumed));
    }

    [TestMethod]
    public void ConvertedWeights_RecordTheScalingLimitation()
    {
        ResourceEstimate estimate = Estimate(Candidate(
            weights: GgufWeightFormat.Q8_0,
            preparation: CandidatePreparation.WeightConversionRequired));

        Assert.IsTrue(
            estimate.Limitations.Contains(EstimationLimitation.WeightsScaledAcrossQuantisation));
    }

    [TestMethod]
    public void ImportedWeights_RecordNoScalingLimitation()
    {
        Assert.IsFalse(
            Estimate().Limitations.Contains(
                EstimationLimitation.WeightsScaledAcrossQuantisation));
    }

    [TestMethod]
    public void AbsentPolicy_IsNotEstablished()
    {
        ResourceEstimate estimate = Estimate(policy: EstimatorPolicy.Absent());

        Assert.AreEqual(nameof(EstimationStatus.NotEstablished), estimate.Status.ToString());
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.EstimatorPolicyUnavailable),
            estimate.Reason.ToString());
    }

    [TestMethod]
    public void UnknownArchitecture_IsNotEstablished()
    {
        ResourceEstimate estimate = Estimate(facts: Facts(layers: null));

        Assert.AreEqual(nameof(EstimationStatus.NotEstablished), estimate.Status.ToString());
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownArchitecture),
            estimate.Reason.ToString());
    }

    [TestMethod]
    public void NotEstablished_CarriesNoComponents()
    {
        // A partial component set would compose into a peak that looks like a
        // real number and would be compared against a real budget.
        Assert.AreEqual(0, Estimate(facts: Facts(layers: null)).Components.Count);
    }

    [TestMethod]
    public void LargerContext_NeverProducesASmallerKvComponent()
    {
        ulong small = Single(
            Estimate(Candidate(context: 2048)), ResourceComponentKind.KvCache).Bytes.Bytes;
        ulong large = Single(
            Estimate(Candidate(context: 8192)), ResourceComponentKind.KvCache).Bytes.Bytes;

        Assert.IsTrue(large > small);
    }
}
