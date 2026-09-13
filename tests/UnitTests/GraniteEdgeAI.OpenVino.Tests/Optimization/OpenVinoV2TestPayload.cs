using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
using ContractCache = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;
using ExecutionKv = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision;
using ExecutionWeight = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoWeightPrecision;
using RouteCache = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoCompiledCachePolicy;
using RouteKv = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoKvCachePrecision;
using RouteWeight = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoWeightPrecision;

namespace GraniteEdgeAI.OpenVino.Tests.Optimization;

internal static class OpenVinoV2TestPayload
{
    internal const string CapabilityRuntimeVersion =
        "2026.3.0-22451-8a17657b995-releases/2026/3";
    internal const string GenAiBuild = "2026.3.0.0-3277-bd8d6542e3c";
    internal const string TokenizersBuild = "2026.3.0.0-703-183c6f25cda";
    internal static readonly string FixtureWorkerManifestDigest = new('1', 64);

    internal static IReadOnlyDictionary<string, string> OptimizerVersions { get; } =
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["nncf"] = "3.3.0",
            ["openvino"] = "2026.3.0",
            ["openvino-genai"] = "2026.3.0.0",
            ["optimum"] = "2.3.0",
            ["optimum-intel"] = "2.1.0",
            ["transformers"] = "5.5.4"
        };

    internal static OptimizationExecutionPayload For(
        OpenVinoWeightFormat weights,
        OpenVinoKvCacheFormat kvCache,
        ContractCache compiledCache,
        string evidenceId,
        string? configurationId = null,
        IReadOnlyDictionary<string, string>? optimizerVersions = null,
        TurboQuantBuildIdentity? turboQuantBuild = null,
        ExecutionWeight sourceWeightPrecision = ExecutionWeight.Fp16)
    {
        ExecutionWeight target = weights switch
        {
            OpenVinoWeightFormat.Original or OpenVinoWeightFormat.Fp16 => ExecutionWeight.Fp16,
            OpenVinoWeightFormat.Int8 => ExecutionWeight.EightBit,
            OpenVinoWeightFormat.Int4 => ExecutionWeight.FourBit,
            _ => throw new ArgumentOutOfRangeException(nameof(weights))
        };
        ExecutionKv executionKv = kvCache switch
        {
            OpenVinoKvCacheFormat.RouteDefault => ExecutionKv.ReleasedDefault,
            OpenVinoKvCacheFormat.U8 => ExecutionKv.U8,
            _ => throw new ArgumentOutOfRangeException(nameof(kvCache))
        };
        bool runtimeOnly = sourceWeightPrecision == target;
        bool cacheEnabled = compiledCache == ContractCache.Enabled;
        OpenVinoExecutionPayload payload = OpenVinoExecutionPayload.Create(
            configurationId ?? ConfigurationId(weights, kvCache),
            "CPU",
            "Standard candidate",
            evidenceId,
            sourceWeightPrecision,
            target,
            executionKv,
            cacheEnabled,
            compiledCacheIsDisposable: true,
            compiledCacheIsModelArtifact: false,
            createsCompletePackage: !runtimeOnly,
            OpenVinoBuildIdentity.Create(
                CapabilityRuntimeVersion,
                GenAiBuild,
                TokenizersBuild,
                FixtureWorkerManifestDigest),
            optimizerVersions ?? OptimizerVersions,
            turboQuantBuild);
        return OptimizationExecutionPayload.ForOpenVino(payload);
    }

    internal static OpenVinoBuildEvidence Builds(
        TurboQuantBuildEvidence? turboQuantBuild = null) => new(
            CapabilityRuntimeVersion,
            GenAiBuild,
            TokenizersBuild,
            FixtureWorkerManifestDigest,
            turboQuantBuild);

    internal static OpenVinoOptimizationToolVersions ToolVersions() => new(
        OpenVino: "2026.3.0",
        OpenVinoGenAi: "2026.3.0.0",
        Nncf: "3.3.0",
        Optimum: "2.3.0",
        OptimumIntel: "2.1.0",
        Transformers: "5.5.4");

    internal static OpenVinoOptimizationCapabilityEvidence CapabilityEvidenceFor(
        OpenVinoWeightFormat weights,
        OpenVinoKvCacheFormat kvCache,
        ContractCache compiledCache,
        string evidenceId,
        int streams = 1) => new(
            Builds(),
            ToolVersions(),
            [
                new OpenVinoOptimizationCapabilityAdmission(
                    evidenceId,
                    "CPU",
                    MapWeight(weights),
                    new OpenVinoRuntimeOptimization(
                        MapKvCache(kvCache),
                        compiledCache == ContractCache.Enabled
                            ? RouteCache.Disposable
                            : RouteCache.Disabled),
                    OpenVinoCapabilityPerformanceHint.Latency,
                    streams,
                    MinimumContextTokens: 512,
                    MaximumContextTokens: 4_096,
                    OpenVinoCapabilityMaturity.Released)
            ]);

    private static string ConfigurationId(OpenVinoWeightFormat weights,
        OpenVinoKvCacheFormat kvCache) =>
        (weights, kvCache) switch
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
            _ => throw new ArgumentOutOfRangeException(
                nameof(weights),
                $"No published OpenVINO configuration exists for {weights}/{kvCache}."),
        };

    private static RouteWeight MapWeight(OpenVinoWeightFormat weights) =>
        weights switch
        {
            OpenVinoWeightFormat.Original => RouteWeight.Original,
            OpenVinoWeightFormat.Fp16 => RouteWeight.Fp16,
            OpenVinoWeightFormat.Int8 => RouteWeight.EightBit,
            OpenVinoWeightFormat.Int4 => RouteWeight.FourBit,
            _ => throw new ArgumentOutOfRangeException(nameof(weights))
        };

    private static RouteKv MapKvCache(OpenVinoKvCacheFormat kvCache) =>
        kvCache switch
        {
            OpenVinoKvCacheFormat.RouteDefault =>
                RouteKv.ReleasedDefault,
            OpenVinoKvCacheFormat.U8 => RouteKv.U8,
            _ => throw new ArgumentOutOfRangeException(nameof(kvCache))
        };
}
