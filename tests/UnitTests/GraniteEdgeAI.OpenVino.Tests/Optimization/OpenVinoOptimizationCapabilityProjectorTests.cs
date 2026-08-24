using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using ContractCompiledCachePolicy = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;
using RouteCompiledCachePolicy = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoCompiledCachePolicy;

namespace GraniteEdgeAI.OpenVino.Tests.Optimization;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class OpenVinoOptimizationCapabilityProjectorTests
{
    [TestMethod]
    public void ProjectorReportsAdmittedEvidenceWithoutSelectingAMode()
    {
        OpenVinoCapabilityPayload payload =
            OpenVinoOptimizationCapabilityProjector.Project(
                OpenVinoCapabilityTestData.StandardCpuEvidence());

        Assert.AreEqual(4, payload.Admitted.Count);
        Assert.IsTrue(payload.Admitted.All(static admission =>
            !string.IsNullOrWhiteSpace(admission.EvidenceId)));
        Assert.IsNull(typeof(OpenVinoOptimizationCapabilityAdmission)
            .GetProperty(nameof(OpenVinoOptimizationCandidate.Objective)));
        Assert.IsNull(typeof(OpenVinoOptimizationCapabilityAdmission)
            .GetProperty("Preference"));
    }

    [TestMethod]
    public void ProjectorPreservesExactReleasedCpuCapabilityEvidence()
    {
        OpenVinoCapabilityPayload payload =
            OpenVinoOptimizationCapabilityProjector.Project(
                OpenVinoCapabilityTestData.StandardCpuEvidence());

        Assert.AreEqual(
            "openvino-2026.3.0_openvino-genai-2026.3.0.0_nncf-3.3.0",
            payload.RuntimeVersion);

        (string EvidenceId, OpenVinoWeightFormat Weights,
            OpenVinoKvCacheFormat KvCache,
            ContractCompiledCachePolicy CompiledCache)[] expected =
        [
            ("OV-STD-CPU-ORIGINAL-01", OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.RouteDefault,
                ContractCompiledCachePolicy.Disabled),
            ("OV-STD-CPU-FP16-01", OpenVinoWeightFormat.Fp16,
                OpenVinoKvCacheFormat.RouteDefault,
                ContractCompiledCachePolicy.Disabled),
            ("OV-STD-CPU-INT8-U8-01", OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8,
                ContractCompiledCachePolicy.Enabled),
            ("OV-STD-CPU-INT4-U8-01", OpenVinoWeightFormat.Int4,
                OpenVinoKvCacheFormat.U8,
                ContractCompiledCachePolicy.Enabled)
        ];

        for (int index = 0; index < expected.Length; index++)
        {
            OpenVinoAdmittedConfiguration actual = payload.Admitted[index];
            Assert.AreEqual(expected[index].EvidenceId, actual.EvidenceId);
            Assert.AreEqual(expected[index].Weights, actual.Weights);
            Assert.AreEqual(expected[index].KvCache, actual.KvCache);
            Assert.AreEqual(expected[index].CompiledCache, actual.CompiledCache);
            Assert.AreEqual(DeviceRouteId.Cpu, actual.Device);
            Assert.AreEqual(OpenVinoPerformanceHint.Latency, actual.PerformanceHint);
            Assert.AreEqual(1, actual.Streams);
            Assert.AreEqual(512, actual.MinimumContextTokens);
            Assert.AreEqual(4_096, actual.MaximumContextTokens);
            Assert.AreEqual(SupportLevel.DeclaredSupported, actual.Level);
            Assert.IsFalse(actual.RequiresEvidence);
        }
    }

    [TestMethod]
    public void OriginalEvidenceDoesNotWidenThePersistentExecutor()
    {
        Assert.ThrowsExactly<OpenVinoOptimizationException>(() =>
            OpenVinoPersistentArtifact.Create(OpenVinoWeightPrecision.Original));
    }

    private static class OpenVinoCapabilityTestData
    {
        internal static OpenVinoOptimizationCapabilityEvidence StandardCpuEvidence() =>
            new(
                new OpenVinoOptimizationToolVersions(
                    OpenVino: "2026.3.0",
                    OpenVinoGenAi: "2026.3.0.0",
                    Nncf: "3.3.0"),
                [
                    Admission(
                        "OV-STD-CPU-ORIGINAL-01",
                        OpenVinoWeightPrecision.Original,
                        OpenVinoKvCachePrecision.ReleasedDefault,
                        compiledCache: false),
                    Admission(
                        "OV-STD-CPU-FP16-01",
                        OpenVinoWeightPrecision.Fp16,
                        OpenVinoKvCachePrecision.ReleasedDefault,
                        compiledCache: false),
                    Admission(
                        "OV-STD-CPU-INT8-U8-01",
                        OpenVinoWeightPrecision.EightBit,
                        OpenVinoKvCachePrecision.U8,
                        compiledCache: true),
                    Admission(
                        "OV-STD-CPU-INT4-U8-01",
                        OpenVinoWeightPrecision.FourBit,
                        OpenVinoKvCachePrecision.U8,
                        compiledCache: true)
                ]);

        private static OpenVinoOptimizationCapabilityAdmission Admission(
            string evidenceId,
            OpenVinoWeightPrecision weights,
            OpenVinoKvCachePrecision kvCache,
            bool compiledCache) =>
            new(
                evidenceId,
                Device: "CPU",
                weights,
                new OpenVinoRuntimeOptimization(
                    kvCache,
                    compiledCache
                        ? RouteCompiledCachePolicy.Disposable
                        : RouteCompiledCachePolicy.Disabled),
                OpenVinoCapabilityPerformanceHint.Latency,
                Streams: 1,
                MinimumContextTokens: 512,
                MaximumContextTokens: 4_096,
                OpenVinoCapabilityMaturity.Released);
    }
}
