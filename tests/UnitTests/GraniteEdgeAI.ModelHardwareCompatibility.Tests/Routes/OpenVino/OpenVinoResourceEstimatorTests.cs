using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.OpenVino;

/// <summary>
/// What an OpenVINO configuration would actually cost.
///
/// The estimate is the number a safety gate is compared against, so the tests
/// that matter here are the ones about what it must never do: leave a component
/// out, invent a value it does not have, or count the same memory twice.
/// </summary>
[TestClass]
public sealed class OpenVinoResourceEstimatorTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private static InspectedModelFacts Facts() =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte), 32, 4096, 32, 8, 8192, 15, 2);

    private static OpenVinoRouteConfiguration Configuration(
        DeviceRouteId device = DeviceRouteId.Cpu,
        OpenVinoWeightFormat weights = OpenVinoWeightFormat.Int8,
        OpenVinoKvCacheFormat cache = OpenVinoKvCacheFormat.U8) =>
        OpenVinoRouteConfiguration.Create(
            weights,
            cache,
            device,
            OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Enabled,
            streams: 1);

    private static ResourceEstimate Estimate(
        OpenVinoRouteConfiguration configuration,
        InspectedModelFacts? facts = null,
        int contextTokens = 4096) =>
        OpenVinoResourceEstimator.Estimate(
            facts ?? Facts(),
            configuration,
            ContextTokenCount.FromTokens(contextTokens),
            EstimatorPolicy.ProvisionalV1());

    [TestMethod]
    public void EstimateIsEstablishedForACompleteConfiguration()
    {
        Assert.AreEqual(EstimationStatus.Established, Estimate(Configuration()).Status);
    }

    [TestMethod]
    public void NonDivisibleAttentionShapeFailsClosedForEveryCacheLayout()
    {
        InspectedModelFacts inconsistent = InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte), 32, 4097, 32, 8,
            8192, 15, 2);

        foreach (OpenVinoKvCacheFormat format in Enum.GetValues<OpenVinoKvCacheFormat>()
            .Where(value => value != OpenVinoKvCacheFormat.Unspecified))
        {
            ResourceEstimate estimate = Estimate(
                Configuration(cache: format), inconsistent);
            Assert.AreEqual(EstimationStatus.NotEstablished, estimate.Status, format.ToString());
            Assert.AreEqual(
                EstimationUnavailableReason.UnknownArchitecture,
                estimate.Reason,
                format.ToString());
        }
    }

    [TestMethod]
    [DataRow(12, 5)]
    [DataRow(8, 16)]
    public void InvalidGroupedAttentionShapeFailsClosed(
        int attentionHeads, int keyValueHeads)
    {
        int embedding = attentionHeads == 12 ? 4092 : 4096;
        InspectedModelFacts inconsistent = InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte), 32, embedding,
            attentionHeads, keyValueHeads, 8192, 15, 2);

        ResourceEstimate estimate = Estimate(Configuration(), inconsistent);

        Assert.AreEqual(EstimationStatus.NotEstablished, estimate.Status);
        Assert.AreEqual(
            EstimationUnavailableReason.UnknownArchitecture, estimate.Reason);
    }

    [TestMethod]
    [DataRow(12, 12)]
    [DataRow(12, 3)]
    [DataRow(12, 1)]
    public void ValidMhaGqaAndMqaShapesRemainEstablished(
        int attentionHeads, int keyValueHeads)
    {
        InspectedModelFacts valid = InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte), 32, 4092,
            attentionHeads, keyValueHeads, 8192, 15, 2);

        Assert.AreEqual(
            EstimationStatus.Established,
            Estimate(Configuration(), valid).Status);
    }

    [TestMethod]
    public void ExtremeCacheProductsReturnTypedRangeFailureInsteadOfThrowing()
    {
        InspectedModelFacts extreme = InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte), int.MaxValue, int.MaxValue,
            1, 1, int.MaxValue, 15, 2);

        ResourceEstimate estimate = Estimate(
            Configuration(cache: OpenVinoKvCacheFormat.F16),
            extreme,
            contextTokens: int.MaxValue);

        Assert.AreEqual(EstimationStatus.NotEstablished, estimate.Status);
        Assert.AreEqual(
            EstimationUnavailableReason.QuantitiesExceedRepresentableRange,
            estimate.Reason);
    }

    [TestMethod]
    public void LargeRepresentableCacheProductRemainsEstablished()
    {
        InspectedModelFacts large = InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte), int.MaxValue, 128,
            1, 1, 8192, 15, 2);

        Assert.AreEqual(
            EstimationStatus.Established,
            Estimate(Configuration(cache: OpenVinoKvCacheFormat.U8),
                large, contextTokens: 1).Status);
    }

    [TestMethod]
    public void MaximumConservativeCacheProductJustInsideUlongRemainsEstablished()
    {
        // F16 is conservatively floored by the 272-byte one-block U8 K/V
        // layout. This is the largest layer count that fits that layout at
        // int.MaxValue context; one more layer would exceed UInt64.
        InspectedModelFacts justInside = InspectedModelFacts.Create(
            ByteCount.FromBytes(1), 31_580_641, 1,
            1, 1, 8192, 15, 2);

        ResourceEstimate estimate = Estimate(
            Configuration(cache: OpenVinoKvCacheFormat.F16),
            justInside,
            contextTokens: int.MaxValue);

        Assert.AreEqual(
            EstimationStatus.Established, estimate.Status, estimate.Reason.ToString());
    }

    [TestMethod]
    public void IntegratedGpuSharedMemoryIsCountedOnce()
    {
        // An integrated GPU has no memory of its own: what it uses is system
        // RAM. Charging the weights to shared device memory and again to system
        // memory would double the requirement and reject a setup that fits.
        // Charging them to a dedicated pool that does not exist would halve it
        // and admit one that does not.
        ResourceEstimate estimate =
            Estimate(Configuration(DeviceRouteId.IntelIntegratedGpu));

        ResourcePeakProfile peaks = ResourcePhaseComposer.Compose(estimate.Components);

        Assert.AreEqual(
            ByteCount.Zero,
            peaks.PeakFor(ResourceTarget.DedicatedDeviceMemory),
            "An integrated GPU was charged against dedicated memory it does not have.");

        Assert.AreEqual(
            peaks.PeakFor(ResourceTarget.SystemMemory)
                .Add(peaks.PeakFor(ResourceTarget.SharedDeviceMemory)),
            peaks.SystemMemoryPressure,
            "Shared device memory was not folded into system pressure exactly once.");
    }

    [TestMethod]
    public void NoComponentIsChargedToTwoPools()
    {
        // The same allocation appearing under two targets is the double count
        // that section 7.2 forbids, and it survives every total-based check
        // because both totals stay individually plausible.
        ResourceEstimate estimate =
            Estimate(Configuration(DeviceRouteId.IntelIntegratedGpu));

        IEnumerable<IGrouping<ResourceComponentKind, ResourceComponent>> byKind =
            estimate.Components
                .Where(component => component.Target
                    is ResourceTarget.SystemMemory
                    or ResourceTarget.SharedDeviceMemory
                    or ResourceTarget.DedicatedDeviceMemory)
                .GroupBy(component => component.Kind);

        foreach (IGrouping<ResourceComponentKind, ResourceComponent> group in byKind)
        {
            Assert.AreEqual(
                1,
                group.Select(component => component.Target).Distinct().Count(),
                $"{group.Key} is charged against more than one memory pool.");
        }
    }

    [TestMethod]
    public void EveryMandatoryComponentIsPresent()
    {
        // A missing component is an underestimate, and an underestimate is the
        // false-safe this whole design exists to prevent.
        ResourceComponentKind[] required =
        [
            ResourceComponentKind.Weights,
            ResourceComponentKind.KvCache,
            ResourceComponentKind.ComputeBuffer,
            ResourceComponentKind.BackendAllocation,
            ResourceComponentKind.ApplicationOverhead
        ];

        IReadOnlyList<ResourceComponent> components = Estimate(Configuration()).Components;

        foreach (ResourceComponentKind kind in required)
        {
            Assert.IsTrue(
                components.Any(component => component.Kind == kind),
                $"{kind} is missing from the estimate.");
        }
    }

    [TestMethod]
    public void EveryComponentCostsSomething()
    {
        foreach (ResourceComponent component in Estimate(Configuration()).Components)
        {
            Assert.IsTrue(
                component.Bytes > ByteCount.Zero,
                $"{component.Kind} was estimated at zero, which reads as free.");
        }
    }

    [TestMethod]
    [DataRow(null, null, null, null)]
    [DataRow(32, null, 32, 8)]
    [DataRow(32, 4096, null, 8)]
    [DataRow(32, 4096, 32, null)]
    public void UnknownModelShapeFailsClosed(
        int? layers, int? embedding, int? heads, int? kvHeads)
    {
        // Missing a layer count does not make the cache free. Estimating around
        // an unknown produces a confident number with nothing behind it, so the
        // estimate refuses and names why.
        InspectedModelFacts incomplete = InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte), layers, embedding, heads, kvHeads, 8192, 15, 2);

        ResourceEstimate estimate = Estimate(Configuration(), incomplete);

        Assert.AreEqual(EstimationStatus.NotEstablished, estimate.Status);
        Assert.AreNotEqual(EstimationUnavailableReason.None, estimate.Reason);
        Assert.AreEqual(0, estimate.Components.Count);
    }

    [TestMethod]
    public void AbsentPolicyRefusesRatherThanInventingTerms()
    {
        ResourceEstimate estimate = OpenVinoResourceEstimator.Estimate(
            Facts(),
            Configuration(),
            ContextTokenCount.FromTokens(4096),
            EstimatorPolicy.Absent());

        Assert.AreEqual(EstimationStatus.NotEstablished, estimate.Status);
    }

    [TestMethod]
    public void LongerContextCostsMoreCache()
    {
        ulong Cache(int tokens) => Estimate(Configuration(), contextTokens: tokens)
            .Components
            .Where(component => component.Kind == ResourceComponentKind.KvCache)
            .Aggregate(0UL, (sum, component) => sum + component.Bytes.Bytes);

        Assert.IsTrue(Cache(8192) > Cache(4096));
    }

    [TestMethod]
    public void SmallerCacheFormatCostsLess()
    {
        // U4 stores the same context in fewer bits than F16. If the estimate
        // did not reflect that, the frontier would rank cache formats by
        // nothing and a slider band would move memory without moving cost.
        ulong Cache(OpenVinoKvCacheFormat format) =>
            Estimate(Configuration(cache: format))
                .Components
                .Where(component => component.Kind == ResourceComponentKind.KvCache)
                .Aggregate(0UL, (sum, component) => sum + component.Bytes.Bytes);

        Assert.IsTrue(Cache(OpenVinoKvCacheFormat.U4) < Cache(OpenVinoKvCacheFormat.U8));
        Assert.IsTrue(Cache(OpenVinoKvCacheFormat.U8) < Cache(OpenVinoKvCacheFormat.F16));
    }

    [TestMethod]
    public void TurboQuantCacheFormatsReduceOnlyCacheMemoryMonotonically()
    {
        (ulong Weights, ulong Cache) Memory(OpenVinoKvCacheFormat format)
        {
            IReadOnlyList<ResourceComponent> components =
                Estimate(Configuration(weights: OpenVinoWeightFormat.Int8, cache: format))
                    .Components;

            ulong weights = components
                .Where(component => component.Kind == ResourceComponentKind.Weights)
                .Aggregate(0UL, (sum, component) => sum + component.Bytes.Bytes);
            ulong cache = components
                .Where(component => component.Kind == ResourceComponentKind.KvCache)
                .Aggregate(0UL, (sum, component) => sum + component.Bytes.Bytes);

            return (weights, cache);
        }

        (ulong u4Weights, ulong u4Cache) = Memory(OpenVinoKvCacheFormat.U4);
        (ulong tbq4Weights, ulong tbq4Cache) = Memory(OpenVinoKvCacheFormat.TurboQuantTbq4);
        (ulong tbq3Weights, ulong tbq3Cache) = Memory(OpenVinoKvCacheFormat.TurboQuantTbq3);

        Assert.IsTrue(u4Cache > tbq4Cache);
        Assert.IsTrue(tbq4Cache > tbq3Cache);
        Assert.AreEqual(u4Weights, tbq4Weights);
        Assert.AreEqual(tbq4Weights, tbq3Weights);
    }

    [TestMethod]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq4, 127, 20480UL)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq4, 128, 20480UL)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq4, 129, 36864UL)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq3, 127, 16384UL)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq3, 128, 16384UL)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq3, 129, 28672UL)]
    public void TurboQuantCachePadsEachHeadToAComplete128ValueBlock(
        OpenVinoKvCacheFormat format,
        int headDimension,
        ulong expectedBytes)
    {
        InspectedModelFacts facts = InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte),
            layerCount: 1,
            embeddingSize: headDimension,
            attentionHeadCount: 1,
            keyValueHeadCount: 1,
            declaredContextLimit: 8192,
            fileType: 15,
            quantisationVersion: 2);

        ulong actual = Estimate(
                Configuration(cache: format),
                facts,
                contextTokens: 128)
            .Components
            .Single(component => component.Kind == ResourceComponentKind.KvCache)
            .Bytes
            .Bytes;

        Assert.AreEqual(expectedBytes, actual);
    }

    [TestMethod]
    [DataRow(OpenVinoKvCacheFormat.U8, 127, 36864UL)]
    [DataRow(OpenVinoKvCacheFormat.U8, 128, 36864UL)]
    [DataRow(OpenVinoKvCacheFormat.U8, 129, 69632UL)]
    [DataRow(OpenVinoKvCacheFormat.U4, 127, 20480UL)]
    [DataRow(OpenVinoKvCacheFormat.U4, 128, 20480UL)]
    [DataRow(OpenVinoKvCacheFormat.U4, 129, 36864UL)]
    public void ReleasedQuantisedCachesPadEachHeadToAComplete128ValueGroup(
        OpenVinoKvCacheFormat format,
        int headDimension,
        ulong expectedBytes)
    {
        InspectedModelFacts facts = InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte),
            layerCount: 1,
            embeddingSize: headDimension,
            attentionHeadCount: 1,
            keyValueHeadCount: 1,
            declaredContextLimit: 8192,
            fileType: 15,
            quantisationVersion: 2);

        ulong actual = Estimate(
                Configuration(cache: format),
                facts,
                contextTokens: 128)
            .Components
            .Single(component => component.Kind == ResourceComponentKind.KvCache)
            .Bytes
            .Bytes;

        Assert.AreEqual(expectedBytes, actual);
    }

    [TestMethod]
    public void SmallerWeightFormatCostsLess()
    {
        ulong Weights(OpenVinoWeightFormat format) =>
            Estimate(Configuration(weights: format))
                .Components
                .Where(component => component.Kind == ResourceComponentKind.Weights)
                .Aggregate(0UL, (sum, component) => sum + component.Bytes.Bytes);

        Assert.IsTrue(Weights(OpenVinoWeightFormat.Int4) < Weights(OpenVinoWeightFormat.Int8));
        Assert.IsTrue(Weights(OpenVinoWeightFormat.Int8) < Weights(OpenVinoWeightFormat.Fp16));
    }

    [TestMethod]
    public void OriginalWeightsCostWhatTheFileAlreadyIs()
    {
        // Nothing is converted, so the weights are exactly the bytes on disk.
        // Deriving a figure from a bit-width here would contradict a value we
        // already measured.
        ulong weights = Estimate(Configuration(weights: OpenVinoWeightFormat.Original))
            .Components
            .Where(component => component.Kind == ResourceComponentKind.Weights)
            .Aggregate(0UL, (sum, component) => sum + component.Bytes.Bytes);

        Assert.IsTrue(
            weights >= 3 * Gibibyte,
            "Original weights were estimated below the file they come from.");
    }

    [TestMethod]
    public void PersistentConversionChargesWorkspaceAndOutput()
    {
        // A conversion needs room for what it writes. Omitting it lets a plan
        // promise a persistent artifact the disk cannot hold.
        IReadOnlyList<ResourceComponent> converted =
            Estimate(Configuration(weights: OpenVinoWeightFormat.Int4)).Components;

        Assert.IsTrue(
            converted.Any(component =>
                component.Target == ResourceTarget.Storage
                && component.Kind == ResourceComponentKind.PersistentArtifact),
            "A converting configuration charged no storage for its output.");
    }

    [TestMethod]
    public void OriginalWeightsChargeNoConversionStorage()
    {
        // Runtime-only work writes no model, so claiming disk for one would
        // overstate what the user is agreeing to.
        Assert.IsFalse(
            Estimate(Configuration(weights: OpenVinoWeightFormat.Original))
                .Components
                .Any(component => component.Kind == ResourceComponentKind.PersistentArtifact),
            "A runtime-only configuration claimed storage for a model copy.");
    }

    [TestMethod]
    public void MoreStreamsCostMoreWorkingMemory()
    {
        ulong Compute(int streams) => OpenVinoResourceEstimator.Estimate(
                Facts(),
                OpenVinoRouteConfiguration.Create(
                    OpenVinoWeightFormat.Int8,
                    OpenVinoKvCacheFormat.U8,
                    DeviceRouteId.Cpu,
                    OpenVinoPerformanceHint.Throughput,
                    OpenVinoCompiledCachePolicy.Enabled,
                    streams),
                ContextTokenCount.FromTokens(4096),
                EstimatorPolicy.ProvisionalV1())
            .Components
            .Where(component => component.Kind == ResourceComponentKind.ComputeBuffer)
            .Aggregate(0UL, (sum, component) => sum + component.Bytes.Bytes);

        Assert.IsTrue(
            Compute(4) > Compute(1),
            "Four streams were estimated to cost no more working memory than one.");
    }

    [TestMethod]
    public void CompiledCacheOnDiskIsCharged()
    {
        // Checked on a runtime-only configuration, so the compiled blob is the
        // only thing that could put anything on disk. It is charged as model
        // state rather than as a persistent artifact: it is a real file, but it
        // is not a model copy, and only a model copy may make the page say one
        // was created.
        bool ChargesCache(OpenVinoCompiledCachePolicy policy) =>
            OpenVinoResourceEstimator.Estimate(
                    Facts(),
                    OpenVinoRouteConfiguration.Create(
                        OpenVinoWeightFormat.Original,
                        OpenVinoKvCacheFormat.U8,
                        DeviceRouteId.Cpu,
                        OpenVinoPerformanceHint.Latency,
                        policy,
                        1),
                    ContextTokenCount.FromTokens(4096),
                    EstimatorPolicy.ProvisionalV1())
                .Components
                .Any(component => component.Target == ResourceTarget.Storage
                    && component.Kind == ResourceComponentKind.ModelState);

        Assert.IsTrue(ChargesCache(OpenVinoCompiledCachePolicy.Enabled));
        Assert.IsFalse(ChargesCache(OpenVinoCompiledCachePolicy.Disabled));
    }
}
