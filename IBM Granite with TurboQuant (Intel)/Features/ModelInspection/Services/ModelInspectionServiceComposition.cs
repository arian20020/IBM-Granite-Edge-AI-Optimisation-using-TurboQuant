using System;
using System.IO;
using System.Security.Cryptography;
using System.Reflection;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using GraniteEdgeAI.Features.ApplicationComposition;
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
            "OVRuntime"));
        return CreateOpenVinoRouteService(workerRoot);
#else
        throw new PlatformNotSupportedException(
            "The official OpenVINO route is available only on Windows x64.");
#endif
    }

    internal static bool TryCreateDefaultOpenVinoTurboQuantRouteService(
        out OpenVinoRouteService? service)
    {
        service = null;
#if MODEL_INSPECTION_X64
        try
        {
            string workerRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "OpenVino", "TurboQuant", "Worker"));
            string expectedManifestDigest = typeof(ModelInspectionServiceComposition)
                .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .SingleOrDefault(attribute => string.Equals(
                    attribute.Key, "OpenVinoTurboQuantWorkerManifestSha256",
                    StringComparison.Ordinal))?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(expectedManifestDigest))
            {
                return false;
            }
            string manifestPath = Path.Combine(workerRoot, "worker-manifest.json");
            string packagedManifestDigest = DigestFile(manifestPath);
            if (!string.Equals(packagedManifestDigest, expectedManifestDigest,
                    StringComparison.Ordinal))
            {
                return false;
            }
            string patchDigest = DigestFile(Path.Combine(workerRoot,
                "patch-manifest.json"));
            string runtimeDigest = DigestFile(Path.Combine(workerRoot,
                "turboquant-runtime.manifest.json"));
            OpenVinoWorkerInstallation installation =
                A1BackendProductionAuthorities.Shared
                    .CreateTurboQuantWorkerInstallation(
                        workerRoot, expectedManifestDigest, patchDigest, runtimeDigest);
            service = new OpenVinoRouteService(
                new OpenVinoWorkerClient(
                    OpenVinoWorkerClientOptions.CreateDefault(installation)),
                installation.ExpectedBuildEvidence);
            return true;
        }
        catch
        {
            service = null;
            return false;
        }
#else
        return false;
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
            SHA256.HashData(TrustedManifestFile.ReadBounded(
                manifestPath,
                1024 * 1024))).ToLowerInvariant();
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
            A1BackendProductionAuthorities.Shared.CreateOfficialWorkerInstallation(
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
        OpenVinoRouteService routeService,
        OpenVinoRouteService? turboQuantRouteService = null)
    {
#if MODEL_INSPECTION_X64
        ArgumentNullException.ThrowIfNull(routeService);
        (string converterRoot, string expectedDigest) = ResolveApprovedConverter();
        return new OpenVinoOptimizationService(new SealedOpenVinoOptimizationPipeline(
            converterRoot,
            expectedDigest,
            routeService,
            turboQuantRouteService));
#else
        throw new PlatformNotSupportedException(
            "OpenVINO optimization is available only on Windows x64.");
#endif
    }

#if MODEL_INSPECTION_X64
    private static string DigestFile(string path) => Convert.ToHexString(
        SHA256.HashData(TrustedManifestFile.ReadBounded(path, 1024 * 1024)))
        .ToLowerInvariant();

    private static (string Root, string Digest) ResolveApprovedConverter()
    {
        string expectedDigest = typeof(ModelInspectionServiceComposition)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => string.Equals(
                attribute.Key,
                "OpenVinoConverterManifestSha256",
                StringComparison.Ordinal))?.Value ??
            throw new InvalidOperationException(
                "The app does not contain an approved OpenVINO converter manifest identity.");
        return ApprovedOpenVinoConverterResolver.Resolve(
            AppContext.BaseDirectory,
            expectedDigest);
    }
#endif

}
