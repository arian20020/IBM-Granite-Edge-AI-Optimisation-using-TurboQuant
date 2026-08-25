using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Properties that must hold whatever the estimator constants become. The exact
/// figures asserted elsewhere will change when calibration data arrives; these
/// will not.
/// </summary>
[TestClass]
public sealed class MetamorphicPropertyTests
{
    private const int CaseCount = 250;
    private const ulong Seed = 0xC1C0_11A7_2026_0820;

    private static readonly EstimatorPolicy Policy = EstimatorPolicy.ProvisionalV1();

    private static ResourceEstimate EstimateFor(
        EstimationCase generated,
        GgufKvCacheFormat? kvOverride = null,
        ContextTokenCount? contextOverride = null)
    {
        CompatibilityCandidate candidate =
            EstimationCaseGenerator.Candidate(generated, kvOverride, contextOverride);

        ResourceEstimate estimate =
            GgufResourceEstimator.Estimate(generated.Facts, candidate, Policy);

        Assert.AreEqual(
            nameof(EstimationStatus.Established),
            estimate.Status.ToString(),
            $"Generated case {generated.Seed:x16} should be estimable but gave {estimate.Reason}.");

        return estimate;
    }

    private static ulong BytesOf(ResourceEstimate estimate, ResourceComponentKind kind) =>
        estimate.Components.Single(component => component.Kind == kind).Bytes.Bytes;

    [TestMethod]
    public void ALargerContextCanNeverReduceTheKvPayload()
    {
        foreach (EstimationCase generated in EstimationCaseGenerator.Cases(CaseCount, Seed))
        {
            ulong smaller = BytesOf(
                EstimateFor(generated, contextOverride: ContextTokenCount.FromTokens(1024)),
                ResourceComponentKind.KvCache);

            ulong larger = BytesOf(
                EstimateFor(generated, contextOverride: ContextTokenCount.FromTokens(8192)),
                ResourceComponentKind.KvCache);

            Assert.IsTrue(
                larger > smaller,
                $"Case {generated.Seed:x16}: 8192 tokens did not strictly grow the cache over 1024.");
        }
    }

    [TestMethod]
    public void AddingALiveComponentCanNeverReduceItsPhaseRequirement()
    {
        // Left non-strict deliberately: see the code review response in
        // task-10-report.md for why >= is correct here, not >=-as-a-shortcut.
        // A discrete-GPU route's LoadOnly staging buffer (>= 64 MiB by policy)
        // already exceeds the tiny 1024-byte component this test adds to
        // SteadyStateGeneration, so the composed peak for those generated cases
        // stays at the Load-phase figure - unchanged, not reduced. Tightening
        // to > makes this fail on those cases regardless of weight-format
        // variety, so it is not the item-1 interaction the reviewer asked about.
        foreach (EstimationCase generated in EstimationCaseGenerator.Cases(CaseCount, Seed))
        {
            ResourceEstimate estimate = EstimateFor(generated);

            ResourcePeakProfile before = ResourcePhaseComposer.Compose(estimate.Components);

            List<ResourceComponent> widened =
            [
                .. estimate.Components,
                ResourceComponent.Create(
                    ResourceComponentKind.ModelState,
                    ResourceTarget.SystemMemory,
                    ByteCount.FromBytes(1024),
                    new HashSet<LifecyclePhase> { LifecyclePhase.SteadyStateGeneration })
            ];

            ResourcePeakProfile after = ResourcePhaseComposer.Compose(widened);

            Assert.IsTrue(
                after.PeakFor(ResourceTarget.SystemMemory)
                    >= before.PeakFor(ResourceTarget.SystemMemory),
                $"Case {generated.Seed:x16}: adding a live component lowered the requirement.");
        }
    }

    [TestMethod]
    public void ChangingTheKvFormatCanNeverAlterTheWeightPayload()
    {
        foreach (EstimationCase generated in EstimationCaseGenerator.Cases(CaseCount, Seed))
        {
            ulong withF16 = BytesOf(
                EstimateFor(generated, kvOverride: GgufKvCacheFormat.F16),
                ResourceComponentKind.Weights);

            ulong withTurboQuant = BytesOf(
                EstimateFor(generated, kvOverride: GgufKvCacheFormat.TurboQuant3Bit),
                ResourceComponentKind.Weights);

            Assert.AreEqual(
                withF16,
                withTurboQuant,
                $"Case {generated.Seed:x16}: the KV format leaked into the weight estimate.");
        }
    }

    [TestMethod]
    public void SharedDeviceMemoryCanNeverIncreaseTotalSystemCapacity()
    {
        foreach (EstimationCase generated in EstimationCaseGenerator.Cases(CaseCount, Seed))
        {
            ResourceEstimate estimate = EstimateFor(generated);
            ResourcePeakProfile profile = ResourcePhaseComposer.Compose(estimate.Components);

            // Shared memory is carved out of RAM. It must add to the pressure on
            // RAM and must never appear as a separate pool that absorbs demand.
            Assert.AreEqual(
                profile.PeakFor(ResourceTarget.SystemMemory).Bytes
                    + profile.PeakFor(ResourceTarget.SharedDeviceMemory).Bytes,
                profile.SystemMemoryPressure.Bytes,
                $"Case {generated.Seed:x16}: shared memory was not charged to RAM exactly once.");

            Assert.IsTrue(
                profile.SystemMemoryPressure >= profile.PeakFor(ResourceTarget.SystemMemory),
                $"Case {generated.Seed:x16}: shared memory reduced system pressure.");
        }
    }

    [TestMethod]
    public void AQuantisedCacheCanNeverCostMoreThanAnUnquantisedOne()
    {
        foreach (EstimationCase generated in EstimationCaseGenerator.Cases(CaseCount, Seed))
        {
            ulong f16 = BytesOf(
                EstimateFor(generated, kvOverride: GgufKvCacheFormat.F16),
                ResourceComponentKind.KvCache);

            ulong quantised = BytesOf(
                EstimateFor(generated, kvOverride: GgufKvCacheFormat.Q8_0),
                ResourceComponentKind.KvCache);

            Assert.IsTrue(
                quantised <= f16,
                $"Case {generated.Seed:x16}: Q8_0 cost more than F16, so the block table is wrong.");
        }
    }

    [TestMethod]
    public void ReducingOpenVinoCachePrecisionCanNeverIncreaseTheKvPayload()
    {
        InspectedModelFacts facts = InspectedModelFacts.Create(
            ByteCount.FromBytes(3UL * 1024 * 1024 * 1024),
            layerCount: 1,
            embeddingSize: 129,
            attentionHeadCount: 1,
            keyValueHeadCount: 1,
            declaredContextLimit: 8192,
            fileType: 15,
            quantisationVersion: 2);

        OpenVinoKvCacheFormat[] precisionOrder =
        [
            OpenVinoKvCacheFormat.F16,
            OpenVinoKvCacheFormat.U8,
            OpenVinoKvCacheFormat.U4,
            OpenVinoKvCacheFormat.TurboQuantTbq4,
            OpenVinoKvCacheFormat.TurboQuantTbq3
        ];

        ulong previous = ulong.MaxValue;

        foreach (OpenVinoKvCacheFormat cache in precisionOrder)
        {
            OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int8,
                cache,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                streams: 1);

            ResourceEstimate estimate = OpenVinoResourceEstimator.Estimate(
                facts,
                configuration,
                ContextTokenCount.FromTokens(128),
                Policy);

            ulong current = BytesOf(estimate, ResourceComponentKind.KvCache);

            Assert.IsTrue(
                current <= previous,
                $"Reducing cache precision from the preceding format to {cache} "
                + $"increased KV bytes from {previous} to {current}.");

            previous = current;
        }
    }

    [TestMethod]
    public void ReducingOpenVinoWeightPrecisionCanNeverIncreaseEstimatedMemory()
    {
        InspectedModelFacts facts = InspectedModelFacts.Create(
            ByteCount.FromBytes(3UL * 1024 * 1024 * 1024),
            32, 4096, 32, 8, 8192, 15, 2);
        OpenVinoWeightFormat[] precisionOrder =
        [
            OpenVinoWeightFormat.Fp16,
            OpenVinoWeightFormat.Int8,
            OpenVinoWeightFormat.Int4
        ];

        ulong previous = ulong.MaxValue;

        foreach (OpenVinoWeightFormat weights in precisionOrder)
        {
            ResourceEstimate estimate = OpenVinoResourceEstimator.Estimate(
                facts,
                OpenVinoRouteConfiguration.Create(
                    weights,
                    OpenVinoKvCacheFormat.F16,
                    DeviceRouteId.Cpu,
                    OpenVinoPerformanceHint.Latency,
                    OpenVinoCompiledCachePolicy.Disabled,
                    streams: 1),
                ContextTokenCount.FromTokens(4096),
                Policy);
            ResourcePeakProfile peak = ResourcePhaseComposer.Compose(estimate.Components);
            ulong current = peak.SystemMemoryPressure.Bytes
                + peak.PeakFor(ResourceTarget.DedicatedDeviceMemory).Bytes;

            Assert.IsTrue(
                current <= previous,
                $"Reducing weight precision to {weights} increased peak memory "
                + $"from {previous} to {current}.");

            previous = current;
        }
    }

    [TestMethod]
    public void ChangingOpenVinoCachePrecisionCannotAlterWeightBytes()
    {
        InspectedModelFacts facts = InspectedModelFacts.Create(
            ByteCount.FromBytes(3UL * 1024 * 1024 * 1024),
            32, 4096, 32, 8, 8192, 15, 2);

        ulong Weights(OpenVinoKvCacheFormat cache) => BytesOf(
            OpenVinoResourceEstimator.Estimate(
                facts,
                OpenVinoRouteConfiguration.Create(
                    OpenVinoWeightFormat.Int8,
                    cache,
                    DeviceRouteId.Cpu,
                    OpenVinoPerformanceHint.Latency,
                    OpenVinoCompiledCachePolicy.Disabled,
                    streams: 1),
                ContextTokenCount.FromTokens(4096),
                Policy),
            ResourceComponentKind.Weights);

        ulong expected = Weights(OpenVinoKvCacheFormat.F16);

        foreach (OpenVinoKvCacheFormat cache in new[]
        {
            OpenVinoKvCacheFormat.U8,
            OpenVinoKvCacheFormat.U4,
            OpenVinoKvCacheFormat.TurboQuantTbq4,
            OpenVinoKvCacheFormat.TurboQuantTbq3
        })
        {
            Assert.AreEqual(
                expected,
                Weights(cache),
                $"Cache format {cache} leaked into the weight component.");
        }
    }

    [TestMethod]
    public void ComponentOrderCanNeverChangeTheComposedPeak()
    {
        foreach (EstimationCase generated in EstimationCaseGenerator.Cases(CaseCount, Seed))
        {
            ResourceEstimate estimate = EstimateFor(generated);

            ResourcePeakProfile forward = ResourcePhaseComposer.Compose(estimate.Components);
            ResourcePeakProfile reversed = ResourcePhaseComposer.Compose(
                [.. estimate.Components.Reverse()]);

            Assert.AreEqual(
                forward.SystemMemoryPressure.Bytes,
                reversed.SystemMemoryPressure.Bytes,
                $"Case {generated.Seed:x16}: composition depended on component order.");
        }
    }
}
