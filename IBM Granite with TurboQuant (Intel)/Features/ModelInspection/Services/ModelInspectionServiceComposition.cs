using System;
using System.IO;
using System.Security.Cryptography;
using System.Reflection;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
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
        OpenVinoWorkerInstallation installation =
            OpenVinoOfficialWorkerAuthority.CreateInstallation(
                workerRoot,
                expectedManifestDigest);
        OpenVinoWorkerClient client = new(
            OpenVinoWorkerClientOptions.CreateDefault(installation));
        return new OpenVinoRouteService(
            client,
            installation.ExpectedBuildEvidence);
#else
        throw new PlatformNotSupportedException(
            "The official OpenVINO route is available only on Windows x64.");
#endif
    }

    internal static PromptRouteRegistry CreatePromptRouteRegistry(
        OpenVinoRouteService openVinoRouteService) =>
        new([openVinoRouteService]);

    internal static OpenVinoConversionService CreateDefaultOpenVinoConversionService(
        OpenVinoRouteService routeService)
    {
#if MODEL_INSPECTION_X64
        ArgumentNullException.ThrowIfNull(routeService);
        (string converterRoot, string expectedDigest) = ResolveApprovedConverter();
        return new OpenVinoConversionService(new SealedOpenVinoConversionPipeline(
            converterRoot,
            expectedDigest,
            routeService));
#else
        throw new PlatformNotSupportedException(
            "OpenVINO conversion is available only on Windows x64.");
#endif
    }

    internal static OpenVinoOptimizationService CreateDefaultOpenVinoOptimizationService(
        OpenVinoRouteService routeService)
    {
#if MODEL_INSPECTION_X64
        ArgumentNullException.ThrowIfNull(routeService);
        (string converterRoot, string expectedDigest) = ResolveApprovedConverter();
        return new OpenVinoOptimizationService(new SealedOpenVinoOptimizationPipeline(
            converterRoot,
            expectedDigest,
            routeService));
#else
        throw new PlatformNotSupportedException(
            "OpenVINO optimization is available only on Windows x64.");
#endif
    }

#if MODEL_INSPECTION_X64
    private static (string Root, string Digest) ResolveApprovedConverter()
    {
        string converterRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "OpenVino", "Converter", "Worker"));
        string manifestPath = Path.Combine(converterRoot, "converter-manifest.json");
        string packagedDigest = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(manifestPath))).ToLowerInvariant();
        string expectedDigest = typeof(ModelInspectionServiceComposition)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => string.Equals(
                attribute.Key,
                "OpenVinoConverterManifestSha256",
                StringComparison.Ordinal))?.Value ??
            throw new InvalidOperationException(
                "The app does not contain an approved OpenVINO converter manifest identity.");
        if (!string.Equals(packagedDigest, expectedDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The packaged converter does not match the app-approved identity.");
        }
        return (converterRoot, expectedDigest);
    }
#endif

}
