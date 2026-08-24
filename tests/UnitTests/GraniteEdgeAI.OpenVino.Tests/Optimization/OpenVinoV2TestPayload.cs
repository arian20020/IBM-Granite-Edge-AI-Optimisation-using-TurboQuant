using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using ContractCache = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;
using ExecutionKv = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision;
using ExecutionWeight = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoWeightPrecision;

namespace GraniteEdgeAI.OpenVino.Tests.Optimization;

internal static class OpenVinoV2TestPayload
{
    internal const string CapabilityRuntimeVersion =
        "openvino-2026.3.0_openvino-genai-2026.3.0.0_nncf-3.3.0_" +
        "optimum-2.3.0_optimum-intel-2.1.0_transformers-5.5.4";

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
        TurboQuantBuildIdentity? turboQuantBuild = null)
    {
        bool runtimeOnly = weights is OpenVinoWeightFormat.Original or
            OpenVinoWeightFormat.Fp16;
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
        bool cacheEnabled = compiledCache == ContractCache.Enabled;
        OpenVinoExecutionPayload payload = OpenVinoExecutionPayload.Create(
            configurationId ?? ConfigurationId(weights, kvCache, compiledCache),
            "CPU",
            "Standard candidate",
            evidenceId,
            ExecutionWeight.Fp16,
            target,
            executionKv,
            cacheEnabled,
            compiledCacheIsDisposable: true,
            compiledCacheIsModelArtifact: false,
            createsCompletePackage: !runtimeOnly,
            OpenVinoBuildIdentity.Create(
                "2026.3.0-22451-8a17657b995",
                "2026.3.0.0-3277-bd8d6542e3c",
                "2026.3.0.0-703-183c6f25cda",
                new string('d', 64)),
            optimizerVersions ?? OptimizerVersions,
            turboQuantBuild);
        return OptimizationExecutionPayload.ForOpenVino(payload);
    }

    private static string ConfigurationId(OpenVinoWeightFormat weights,
        OpenVinoKvCacheFormat kvCache, ContractCache compiledCache) =>
        $"openvino.v2.{weights.ToString().ToLowerInvariant()}." +
        $"{kvCache.ToString().ToLowerInvariant()}." +
        (compiledCache == ContractCache.Enabled ? "cache" : "nocache");
}
