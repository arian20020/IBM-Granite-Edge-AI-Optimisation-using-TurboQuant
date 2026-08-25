using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

internal static class OpenVinoOfficialCapabilityEvidenceFactory
{
    internal static OpenVinoOptimizationCapabilityEvidence Create(
        OpenVinoBuildEvidence builds) =>
        new(
            builds ?? throw new ArgumentNullException(nameof(builds)),
            new OpenVinoOptimizationToolVersions(
                OpenVino: "2026.3.0",
                OpenVinoGenAi: "2026.3.0.0",
                Nncf: "3.3.0",
                Optimum: "2.3.0",
                OptimumIntel: "2.1.0",
                Transformers: "5.5.4"),
            [
                Admission(
                    "OV-STD-CPU-ORIGINAL-01",
                    OpenVinoWeightPrecision.Original,
                    OpenVinoKvCachePrecision.ReleasedDefault),
                Admission(
                    "OV-STD-CPU-FP16-01",
                    OpenVinoWeightPrecision.Fp16,
                    OpenVinoKvCachePrecision.ReleasedDefault),
                Admission(
                    "OV-STD-CPU-AUTO-01",
                    OpenVinoWeightPrecision.EightBit,
                    OpenVinoKvCachePrecision.ReleasedDefault),
                Admission(
                    "OV-STD-CPU-INT8-U8-01",
                    OpenVinoWeightPrecision.EightBit,
                    OpenVinoKvCachePrecision.U8),
                Admission(
                    "OV-STD-CPU-INT4-U8-01",
                    OpenVinoWeightPrecision.FourBit,
                    OpenVinoKvCachePrecision.U8)
            ]);

    private static OpenVinoOptimizationCapabilityAdmission Admission(
        string evidenceId,
        OpenVinoWeightPrecision weights,
        OpenVinoKvCachePrecision kvCache) =>
        new(
            evidenceId,
            Device: "CPU",
            weights,
            new OpenVinoRuntimeOptimization(
                kvCache,
                OpenVinoCompiledCachePolicy.Disabled),
            OpenVinoCapabilityPerformanceHint.Latency,
            Streams: 1,
            MinimumContextTokens: 512,
            MaximumContextTokens: 4_096,
            OpenVinoCapabilityMaturity.Released);
}
