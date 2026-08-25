using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.OpenVino;

/// <summary>
/// The OpenVINO route configuration, held to the same rules as the GGUF one.
///
/// Two routes that describe themselves differently cannot be compared, and the
/// whole point of the shared planner is that they are. So this asserts the same
/// properties the GGUF configuration already has: every field reaches the
/// descriptor, unknown enums are refused, and the descriptor is ordinal.
/// </summary>
[TestClass]
public sealed class OpenVinoRouteConfigurationTests
{
    private static OpenVinoRouteConfiguration Standard() =>
        OpenVinoRouteConfiguration.Create(
            OpenVinoWeightFormat.Int8,
            OpenVinoKvCacheFormat.U8,
            DeviceRouteId.Cpu,
            OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Enabled,
            streams: 1);

    [TestMethod]
    public void WeightVocabularyContainsOnlyOpenVinoWeightFormats()
    {
        string[] expected =
        [
            nameof(OpenVinoWeightFormat.Unspecified),
            nameof(OpenVinoWeightFormat.Original),
            nameof(OpenVinoWeightFormat.Fp16),
            nameof(OpenVinoWeightFormat.Int8),
            nameof(OpenVinoWeightFormat.Int4)
        ];

        CollectionAssert.AreEqual(expected, Enum.GetNames<OpenVinoWeightFormat>());
    }

    [TestMethod]
    public void CacheVocabularyIncludesTurboQuantFormats()
    {
        string[] names = Enum.GetNames<OpenVinoKvCacheFormat>();

        CollectionAssert.Contains(names, nameof(OpenVinoKvCacheFormat.TurboQuantTbq4));
        CollectionAssert.Contains(names, nameof(OpenVinoKvCacheFormat.TurboQuantTbq3));
    }

    [TestMethod]
    public void ConfigurationNamesItsRoute()
    {
        Assert.AreEqual(RuntimeRouteId.OpenVinoGenAi, Standard().RouteId);
    }

    [TestMethod]
    [DataRow(nameof(OpenVinoWeightFormat))]
    [DataRow(nameof(OpenVinoKvCacheFormat))]
    public void UnspecifiedEnumsAreRefused(string which)
    {
        // Unspecified is not a configuration, it is the absence of one. Letting
        // it through would produce a candidate nobody could execute and an
        // estimate of something undefined.
        Assert.ThrowsExactly<ArgumentException>(() => which switch
        {
            nameof(OpenVinoWeightFormat) => OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Unspecified,
                OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled,
                1),
            _ => OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.Unspecified,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled,
                1)
        });
    }

    [TestMethod]
    public void UnspecifiedDeviceIsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => OpenVinoRouteConfiguration.Create(
            OpenVinoWeightFormat.Int8,
            OpenVinoKvCacheFormat.U8,
            DeviceRouteId.Unspecified,
            OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Enabled,
            1));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void NonPositiveStreamCountIsRefused(int streams)
    {
        // Zero streams is not a lighter configuration, it is one that cannot
        // run. Accepting it would let a candidate estimate as though inference
        // costs nothing.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled,
                streams));
    }

    [TestMethod]
    public void EveryFieldReachesTheDescriptor()
    {
        // The descriptor is what the configuration hash is taken over. A field
        // missing from it makes two materially different setups collide, and
        // the plan would then bind a configuration the executor did not get.
        string baseline = Standard().CanonicalDescriptor;

        OpenVinoRouteConfiguration[] variants =
        [
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U8, DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency, OpenVinoCompiledCachePolicy.Enabled, 1),
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.F16, DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency, OpenVinoCompiledCachePolicy.Enabled, 1),
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U8,
                DeviceRouteId.IntelIntegratedGpu,
                OpenVinoPerformanceHint.Latency, OpenVinoCompiledCachePolicy.Enabled, 1),
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U8, DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Throughput, OpenVinoCompiledCachePolicy.Enabled, 1),
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U8, DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency, OpenVinoCompiledCachePolicy.Disabled, 1),
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U8, DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency, OpenVinoCompiledCachePolicy.Enabled, 4)
        ];

        foreach (OpenVinoRouteConfiguration variant in variants)
        {
            Assert.AreNotEqual(
                baseline,
                variant.CanonicalDescriptor,
                "A field changed without changing the descriptor.");
        }
    }

    [TestMethod]
    public void DescriptorIsStableForTheSameConfiguration()
    {
        Assert.AreEqual(Standard().CanonicalDescriptor, Standard().CanonicalDescriptor);
    }

    [TestMethod]
    public void DescriptorCarriesNoRouteForeignVocabulary()
    {
        // Route leakage check. A GGUF term appearing in an OpenVINO descriptor
        // would mean the shared layer had started reinterpreting route-only
        // fields, which is the exact thing sealed payloads exist to prevent.
        string descriptor = Standard().CanonicalDescriptor;

        foreach (string foreign in new[] { "gguf", "Q4", "Q8_0", "offload", "llama" })
        {
            Assert.IsFalse(
                descriptor.Contains(foreign, StringComparison.OrdinalIgnoreCase),
                $"An OpenVINO descriptor mentions {foreign}.");
        }
    }

    [TestMethod]
    public void DescriptorNamesTheRoute()
    {
        // Two routes could otherwise produce the same descriptor text for
        // coincidentally similar settings, and the configuration hash would
        // stop identifying which executor the plan belongs to.
        StringAssert.StartsWith(Standard().CanonicalDescriptor, "openvino|");
    }

    [TestMethod]
    public void ConfigurationsWithEqualFieldsAreEqual()
    {
        Assert.AreEqual(Standard(), Standard());
    }
}
