using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using GraniteEdgeAI.Features.ModelInspection.Classification;
using GraniteEdgeAI.Features.ModelInspection.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Windows.ApplicationModel;

namespace GraniteEdgeAI.Features.ModelInspection.Infrastructure;

/// <summary>
/// Selects the application-owned root beneath which the fixed Model
/// Inspection worker is installed. Package identity is authoritative; only a
/// controlled, non-adversarial unpackaged development run may use
/// <see cref="AppContext.BaseDirectory"/>.
/// </summary>
internal static class ModelInspectionWorkerComposition
{
    private const int MaximumTrustedManifestBytes = 1024 * 1024;
    private const string TrustedManifestResourceName =
        "GraniteEdgeAI.ModelInspection.WorkerManifest.json";
    private const string InstalledRootUnavailableMessage =
        "The installed Model Inspection worker root is unavailable.";
    private const string UnpackagedRootUnavailableMessage =
        "The unpackaged Model Inspection worker root is unavailable.";
    private const string PackageIdentityUnavailableMessage =
        "The Model Inspection package identity could not be determined.";
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;
    private const int AppModelErrorNoPackage = 15_700;

    internal static IInspectionWorkerClient CreateDefaultClient()
    {
        string approvedRoot = ResolveApprovedApplicationRoot();
        IInspectionWorkerClient processClient = new InspectionWorkerClient(
            WorkerClientOptions.CreateDefault(approvedRoot));
        return new ManifestVerifyingInspectionWorkerClient(
            approvedRoot,
            LoadTrustedWorkerManifest(),
            processClient);
    }

    /// <summary>
    /// Composes the complete application-owned GGUF inspection use case while
    /// keeping worker protocol and process details behind the runtime probe.
    /// </summary>
    internal static IModelInspectionService CreateDefaultService()
    {
        return new ModelInspectionService(
            new WorkerProcessLlamaModelProbe(CreateDefaultClient()),
            new ModelInspectionClassifier());
    }

    internal static string ResolveApprovedApplicationRoot()
    {
        if (!HasPackageIdentity())
        {
            return ResolveApprovedApplicationRoot(
                packageIdentityAvailable: false,
                installedPackageRoot: null,
                AppContext.BaseDirectory);
        }

        return ResolveApprovedApplicationRoot(
            packageIdentityAvailable: true,
            Package.Current.InstalledLocation.Path,
            AppContext.BaseDirectory);
    }

    internal static bool InterpretPackageIdentityProbeResult(int result) =>
        result switch
        {
            ErrorSuccess => true,
            ErrorInsufficientBuffer => true,
            AppModelErrorNoPackage => false,
            _ => throw new InvalidOperationException(
                PackageIdentityUnavailableMessage)
        };

    internal static string ResolveApprovedApplicationRoot(
        bool packageIdentityAvailable,
        string? installedPackageRoot,
        string applicationBaseDirectory)
    {
        if (packageIdentityAvailable)
        {
            return RequireExistingAbsoluteRoot(
                installedPackageRoot,
                InstalledRootUnavailableMessage);
        }

        return RequireExistingAbsoluteRoot(
            applicationBaseDirectory,
            UnpackagedRootUnavailableMessage);
    }

    private static string RequireExistingAbsoluteRoot(
        string? candidate,
        string failureMessage)
    {
        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Contains('\0') ||
            !Path.IsPathFullyQualified(candidate))
        {
            throw new InvalidOperationException(failureMessage);
        }

        string canonicalRoot;
        try
        {
            canonicalRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(candidate));
        }
        catch (Exception error) when (
            error is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            throw new InvalidOperationException(failureMessage);
        }

        if (!Directory.Exists(canonicalRoot))
        {
            throw new InvalidOperationException(failureMessage);
        }

        return canonicalRoot;
    }

    private static bool HasPackageIdentity()
    {
        uint packageFullNameLength = 0;
        int result = GetCurrentPackageFullName(
            ref packageFullNameLength,
            packageFullName: null);
        return InterpretPackageIdentityProbeResult(result);
    }

    private static byte[] LoadTrustedWorkerManifest()
    {
        using Stream? resource = typeof(ModelInspectionWorkerComposition)
            .Assembly
            .GetManifestResourceStream(TrustedManifestResourceName);
        if (resource is null ||
            resource.Length <= 0 ||
            resource.Length > MaximumTrustedManifestBytes)
        {
            throw new InvalidOperationException(
                "The trusted Model Inspection worker manifest is unavailable.");
        }

        byte[] content = GC.AllocateUninitializedArray<byte>(
            checked((int)resource.Length));
        resource.ReadExactly(content);
        return content;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetCurrentPackageFullName",
        CharSet = CharSet.Unicode,
        ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(
        ref uint packageFullNameLength,
        StringBuilder? packageFullName);
}
