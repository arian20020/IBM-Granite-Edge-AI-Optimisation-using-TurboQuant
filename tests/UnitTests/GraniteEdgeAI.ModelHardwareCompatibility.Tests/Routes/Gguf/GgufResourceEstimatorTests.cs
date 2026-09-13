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
    // Section 8: every mandatory component has exactly one owner, so a
    // requirement can never be counted twice. Exercised with a weight
    // conversion so StagingBuffer and PersistentArtifact - the only kinds
    // that are appended rather than always emitted - are actually present in
    // at least one case (discrete GPU builds the maximal seven-component
    // set). the expected route travels as three names because a public test
    // method cannot take internal enum parameters
    [DataRow(
        nameof(DeviceRouteId.Cpu), nameof(CompatibilityBackend.Cpu), nameof(GpuOffloadLevel.None))]
    [DataRow(
        nameof(DeviceRouteId.IntelIntegratedGpu), nameof(CompatibilityBackend.IntelVulkan),
        nameof(GpuOffloadLevel.Full))]
    [DataRow(
        nameof(DeviceRouteId.IntelDiscreteGpu), nameof(CompatibilityBackend.IntelSycl),
        nameof(GpuOffloadLevel.Full))]
    public void EveryComponentKind_AppearsAtMostOnce(
        string deviceName, string backendName, string offloadName)
    {
        DeviceRouteId device = Enum.Parse<DeviceRouteId>(deviceName);
        CompatibilityBackend backend = Enum.Parse<CompatibilityBackend>(backendName);
        GpuOffloadLevel offload = Enum.Parse<GpuOffloadLevel>(offloadName);

        ResourceEstimate estimate = Estimate(Candidate(
            device: device,
            backend: backend,
            offload: offload,
            weights: GgufWeightFormat.Q4KM,
            preparation: CandidatePreparation.WeightConversionRequired));

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
        // taken in any phase must include them. Asserted by membership rather
        // than by count alone, so swapping one phase for another of the same
        // set size would still be caught
        ResourceComponent weights = Single(Estimate(), ResourceComponentKind.Weights);

        Assert.IsTrue(weights.Phases.Contains(LifecyclePhase.Load));
        Assert.IsTrue(weights.Phases.Contains(LifecyclePhase.Compile));
        Assert.IsTrue(weights.Phases.Contains(LifecyclePhase.SteadyStateGeneration));
        Assert.AreEqual(3, weights.Phases.Count);
    }

    [TestMethod]
    public void ComputeBuffer_IsLiveDuringCompileAndGenerationOnly()
    {
        // A regression that shifted ComputeBuffer to load-only (or to every
        // phase) would leave every other assertion in this file green, since
        // nothing else pins its phase set
        ResourceComponent computeBuffer = Single(Estimate(), ResourceComponentKind.ComputeBuffer);

        Assert.IsFalse(computeBuffer.Phases.Contains(LifecyclePhase.Load));
        Assert.IsTrue(computeBuffer.Phases.Contains(LifecyclePhase.Compile));
        Assert.IsTrue(computeBuffer.Phases.Contains(LifecyclePhase.SteadyStateGeneration));
        Assert.AreEqual(2, computeBuffer.Phases.Count);
    }

    [TestMethod]
    public void BackendAllocation_IsLiveInEveryPhase()
    {
        ResourceComponent backend = Single(Estimate(), ResourceComponentKind.BackendAllocation);

        Assert.IsTrue(backend.Phases.Contains(LifecyclePhase.Load));
        Assert.IsTrue(backend.Phases.Contains(LifecyclePhase.Compile));
        Assert.IsTrue(backend.Phases.Contains(LifecyclePhase.SteadyStateGeneration));
        Assert.AreEqual(3, backend.Phases.Count);
    }

    [TestMethod]
    public void ApplicationOverhead_IsLiveInEveryPhase()
    {
        ResourceComponent overhead =
            Single(Estimate(), ResourceComponentKind.ApplicationOverhead);

        Assert.IsTrue(overhead.Phases.Contains(LifecyclePhase.Load));
        Assert.IsTrue(overhead.Phases.Contains(LifecyclePhase.Compile));
        Assert.IsTrue(overhead.Phases.Contains(LifecyclePhase.SteadyStateGeneration));
        Assert.AreEqual(3, overhead.Phases.Count);
    }

    [TestMethod]
    // Renamed from KvCache_IsLiveOnlyDuringGeneration: the previous name
    // asserted a claim about llama.cpp's runtime behaviour that is not true -
    // llama.cpp allocates the KV cache at context creation, before generation
    // starts. What this actually pins is this estimator's own phase labelling
    // for the KvCache component, not when the runtime allocates it. The
    // mislabel is peak-neutral today only because the generation phase's
    // model-target pool already dominates load/compile on all three routes,
    // so charging KvCache to SteadyStateGeneration alone still lands inside
    // the true peak. a future route where load or compile could exceed
    // generation would need this revisited
    public void KvCache_IsLabelledLiveOnlyDuringGeneration()
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

        // the application itself never follows the model to device memory,
        // even when the model's own weights are routed off SystemMemory.
        Assert.AreEqual(
            ResourceTarget.SystemMemory,
            Single(estimate, ResourceComponentKind.ApplicationOverhead).Target);
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

        // Shared device memory still is not system memory: the application's
        // own overhead must stay pinned to SystemMemory even here.
        Assert.AreEqual(
            ResourceTarget.SystemMemory,
            Single(estimate, ResourceComponentKind.ApplicationOverhead).Target);
    }

    [TestMethod]
    // Inverting the CPU/GPU ternary in BackendAllocation would charge a
    // full-offload route the CPU term (and vice versa) with every other test
    // still green, so the expected term name travels as a string and the
    // actual figure is read from the policy rather than hardcoded
    [DataRow(
        nameof(DeviceRouteId.Cpu), nameof(CompatibilityBackend.Cpu), nameof(GpuOffloadLevel.None),
        "Cpu")]
    [DataRow(
        nameof(DeviceRouteId.IntelIntegratedGpu), nameof(CompatibilityBackend.IntelVulkan),
        nameof(GpuOffloadLevel.Full), "Gpu")]
    [DataRow(
        nameof(DeviceRouteId.IntelDiscreteGpu), nameof(CompatibilityBackend.IntelSycl),
        nameof(GpuOffloadLevel.Full), "Gpu")]
    public void BackendAllocation_UsesTheRouteAppropriateTerm(
        string deviceName, string backendName, string offloadName, string expectedTerm)
    {
        DeviceRouteId device = Enum.Parse<DeviceRouteId>(deviceName);
        CompatibilityBackend backend = Enum.Parse<CompatibilityBackend>(backendName);
        GpuOffloadLevel offload = Enum.Parse<GpuOffloadLevel>(offloadName);

        ResourceEstimate estimate = Estimate(Candidate(
            device: device, backend: backend, offload: offload));

        EstimatorTerms terms = EstimatorPolicy.ProvisionalV1().Terms;
        ByteCount expected = expectedTerm == "Cpu"
            ? terms.CpuBackendAllocation
            : terms.GpuBackendAllocation;

        Assert.AreEqual(
            expected, Single(estimate, ResourceComponentKind.BackendAllocation).Bytes);
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
        // space would be inventing a cost the user does not pay
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
        // a partial component set would compose into a peak that looks like a
        // real number and would be compared against a real budget
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
