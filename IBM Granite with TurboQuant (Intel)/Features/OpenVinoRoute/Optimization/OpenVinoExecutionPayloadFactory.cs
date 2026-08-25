using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
using ExecutionKv = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision;
using ExecutionWeight = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoWeightPrecision;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

internal static class OpenVinoExecutionPayloadFactory
{
    internal static OptimizationExecutionPayload Create(
        OptimizationCandidate candidate,
        OpenVinoBuildEvidence builds)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(builds);
        if (candidate.Route != OptimizationRoute.OpenVino ||
            candidate.Configuration is not OpenVinoRouteConfiguration configuration)
        {
            throw new ArgumentException("An OpenVINO candidate is required.", nameof(candidate));
        }

        ExecutionWeight target = configuration.Weights switch
        {
            OpenVinoWeightFormat.Original or OpenVinoWeightFormat.Fp16 =>
                ExecutionWeight.Fp16,
            OpenVinoWeightFormat.Int8 => ExecutionWeight.EightBit,
            OpenVinoWeightFormat.Int4 => ExecutionWeight.FourBit,
            _ => throw new ArgumentOutOfRangeException(nameof(candidate))
        };
        ExecutionKv kvCache = configuration.KvCache switch
        {
            OpenVinoKvCacheFormat.RouteDefault => ExecutionKv.ReleasedDefault,
            OpenVinoKvCacheFormat.U8 => ExecutionKv.U8,
            _ => throw new ArgumentOutOfRangeException(nameof(candidate))
        };
        string configurationId = (configuration.Weights, configuration.KvCache) switch
        {
            (OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.RouteDefault) =>
                "openvino.standard.cpu.original.default.v1",
            (OpenVinoWeightFormat.Fp16, OpenVinoKvCacheFormat.RouteDefault) =>
                "openvino.standard.cpu.fp16.default.v1",
            (OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.RouteDefault) =>
                "openvino.standard.cpu.int8.default.v1",
            (OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U8) =>
                "openvino.standard.cpu.int8.u8.v1",
            (OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U8) =>
                "openvino.standard.cpu.int4.u8.v1",
            _ => throw new ArgumentOutOfRangeException(nameof(candidate))
        };
        bool cacheEnabled = configuration.CompiledCache ==
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy.Enabled;
        OpenVinoExecutionPayload payload = OpenVinoExecutionPayload.Create(
            configurationId,
            "CPU",
            "Standard candidate",
            candidate.EvidenceId,
            ExecutionWeight.Fp16,
            target,
            kvCache,
            cacheEnabled,
            compiledCacheIsDisposable: true,
            compiledCacheIsModelArtifact: false,
            createsCompletePackage: target != ExecutionWeight.Fp16,
            OpenVinoBuildIdentity.Create(
                builds.RuntimeBuild,
                builds.GenAiBuild,
                builds.TokenizersBuild,
                builds.WorkerManifestDigest),
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["nncf"] = "3.3.0",
                ["openvino"] = "2026.3.0",
                ["openvino-genai"] = "2026.3.0.0",
                ["optimum"] = "2.3.0",
                ["optimum-intel"] = "2.1.0",
                ["transformers"] = "5.5.4"
            },
            turboQuantBuild: null);
        return OptimizationExecutionPayload.ForOpenVino(payload);
    }
}
