using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Reflection;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
#if MODEL_INSPECTION_X64
using GraniteEdgeAI.Features.ModelInspection.Infrastructure;
#endif

namespace GraniteEdgeAI.Features.ModelInspection.Services;

/// <summary>
/// Provides the page-facing service entry point without making UI code own
/// worker or protocol composition. The supported production route is x64.
/// </summary>
internal static class ModelInspectionServiceComposition
{
    internal static IModelInspectionService CreateDefault()
    {
#if MODEL_INSPECTION_X64
        return ModelInspectionWorkerComposition.CreateDefaultService();
#else
        throw new PlatformNotSupportedException(
            "Model Inspection is currently available only for Windows x64.");
#endif
    }

    internal static OpenVinoRouteService CreateDefaultOpenVinoRouteService()
    {
#if MODEL_INSPECTION_X64
        string workerRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "OpenVino",
            "Official",
            "Worker"));
        return CreateOpenVinoRouteService(workerRoot);
#else
        throw new PlatformNotSupportedException(
            "The official OpenVINO route is available only on Windows x64.");
#endif
    }

    private static OpenVinoRouteService CreateOpenVinoRouteService(
        string workerRoot)
    {
#if MODEL_INSPECTION_X64
        ArgumentException.ThrowIfNullOrWhiteSpace(workerRoot);
        workerRoot = Path.GetFullPath(workerRoot);
        string manifestPath = Path.Combine(workerRoot, "worker-manifest.json");
        string packagedManifestDigest = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(manifestPath))).ToLowerInvariant();
        string expectedManifestDigest = typeof(ModelInspectionServiceComposition)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => string.Equals(
                attribute.Key,
                "OpenVinoOfficialWorkerManifestSha256",
                StringComparison.Ordinal))?.Value ??
            throw new InvalidOperationException(
                "The app does not contain an approved OpenVINO worker manifest identity.");
        if (!string.Equals(
                packagedManifestDigest,
                expectedManifestDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The packaged OpenVINO worker manifest does not match the app-approved identity.");
        }
        OpenVinoBuildEvidence buildEvidence = new(
            "2026.3.0-22451-8a17657b995-releases/2026/3",
            "2026.3.0.0-3277-bd8d6542e3c",
            "2026.3.0.0-703-183c6f25cda",
            expectedManifestDigest);
        OpenVinoWorkerInstallation installation = new(
            workerRoot,
            "OpenVinoOfficial.Worker.exe",
            OpenVinoProtocol.OfficialProtocolId,
            buildEvidence,
            OfficialBinaryMachines());
        OpenVinoWorkerClient client = new(
            OpenVinoWorkerClientOptions.CreateDefault(installation));
        return new OpenVinoRouteService(client, buildEvidence);
#else
        throw new PlatformNotSupportedException(
            "The official OpenVINO route is available only on Windows x64.");
#endif
    }

    internal static PromptRouteRegistry CreatePromptRouteRegistry(
        OpenVinoRouteService openVinoRouteService) =>
        new([openVinoRouteService]);

    private static IReadOnlyDictionary<string, OpenVinoWorkerBinaryMachine>
        OfficialBinaryMachines() =>
        new Dictionary<string, OpenVinoWorkerBinaryMachine>(StringComparer.Ordinal)
        {
            ["OpenVinoOfficial.Worker.exe"] = OpenVinoWorkerBinaryMachine.Amd64,
            ["openvino.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
            ["openvino_genai.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
            ["openvino_intel_cpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
            ["openvino_intel_gpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
            ["openvino_ir_frontend.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
            ["openvino_tokenizers.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
            ["tbb12.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
            ["tbbbind_2_5.dll"] = OpenVinoWorkerBinaryMachine.Amd64
        };
}
