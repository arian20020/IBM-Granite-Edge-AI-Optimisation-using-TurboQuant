using System;
using System.IO;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;

namespace GraniteEdgeAI.Features.GgufRuntime;

internal static class GgufInspectedModelLaunchFactory
{
    private const int DefaultContextSize = 2_048;
    private const int MaximumContextSize = 4_096;
    private const int MaximumThreads = 8;

    internal static GgufChatLaunchRequest Create(
        ModelInspectionRequest request,
        ModelInspectionExecutionResult execution,
        string packageRoot,
        int? processorCount = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);

        ModelInspectionResult result = RequireReadyResult(execution);
        ModelInspectionFileEvidence file = result.Evidence.File;
        ValidateEvidence(request, file);

        string root = Path.GetFullPath(packageRoot);
        string manifestPath = Path.Combine(
            root,
            GgufRuntimePackageLoader.DetachedManifestFileName);
        byte[] trustedManifest = ReadManifest(manifestPath);
        GgufRuntimeManifest manifest =
            GgufRuntimeManifestJson.Deserialize(trustedManifest);
        int threads = Math.Clamp(
            processorCount ?? Environment.ProcessorCount,
            1,
            MaximumThreads);
        var configuration = new GgufRuntimeConfiguration(
            $"inspected-{file.ModelSha256[..12].ToLowerInvariant()}",
            file.ModelSha256,
            manifest.RuntimeBuildId,
            manifest.RuntimeSourceCommit,
            GgufRuntimeBackend.Cpu,
            "cpu",
            BoundedContext(request.QuickScan.DeclaredContextLength),
            GgufCacheType.F16,
            GgufCacheType.F16,
            0,
            false,
            threads,
            128,
            "model-inspection-complete",
            "cpu-inspected-default",
            512);
        return new GgufChatLaunchRequest(
            root,
            trustedManifest,
            request.ModelPath,
            request.QuickScan.ModelName,
            configuration);
    }

    private static ModelInspectionResult RequireReadyResult(
        ModelInspectionExecutionResult execution)
    {
        if (execution.Status != ModelInspectionExecutionStatus.Completed ||
            execution.Result is not { } result ||
            !ModelInspectionResult.CanContinue(result.Outcome))
        {
            throw new GgufChatLaunchException("model-inspection-not-ready");
        }

        return result;
    }

    private static void ValidateEvidence(
        ModelInspectionRequest request,
        ModelInspectionFileEvidence file)
    {
        if (!file.IntegrityPreserved ||
            !string.Equals(
                file.FileName,
                request.FileName,
                StringComparison.OrdinalIgnoreCase) ||
            file.LengthBytes != request.ExpectedFileIdentity.LengthBytes ||
            file.LastWriteTimeUtc != request.ExpectedFileIdentity.LastWriteTimeUtc)
        {
            throw new GgufChatLaunchException("model-inspection-evidence-mismatch");
        }
    }

    private static byte[] ReadManifest(string manifestPath)
    {
        try
        {
            var info = new FileInfo(manifestPath);
            if (!info.Exists ||
                info.Length <= 0 ||
                info.Length > GgufRuntimeManifestJson.MaximumManifestBytes)
            {
                throw new GgufChatLaunchException("runtime-manifest-unavailable");
            }

            return File.ReadAllBytes(manifestPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new GgufChatLaunchException("runtime-manifest-unavailable");
        }
    }

    private static int BoundedContext(ulong? declaredContext)
    {
        if (!declaredContext.HasValue)
        {
            return DefaultContextSize;
        }

        return (int)Math.Min(declaredContext.Value, MaximumContextSize);
    }
}
