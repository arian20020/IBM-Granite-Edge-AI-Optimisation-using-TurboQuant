using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal sealed class FixedHardwareToolAcquisition : IHardwareToolAcquisition
{
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;
    private const int AppModelErrorNoPackage = 15_700;
    private const string InstalledRootUnavailableMessage =
        "The installed Hardware Inspection root is unavailable.";
    private const string UnpackagedRootUnavailableMessage =
        "The unpackaged Hardware Inspection root is unavailable.";
    private const string PackageIdentityUnavailableMessage =
        "The Hardware Inspection package identity could not be determined.";
    private readonly string _llmFitApprovedRoot;
    private readonly string _llmFitPackageRoot;
    private readonly string _probeApprovedRoot;
    private readonly string _probePackageRoot;
    private readonly Func<byte[]> _probeManifestReader;
    private readonly Func<string, FileAttributes> _getAttributes;
    private readonly Func<string, string, TrustedToolPackageManifest, TrustedToolVerificationResult> _verify;

    internal FixedHardwareToolAcquisition(
        string llmFitApprovedRoot,
        string llmFitPackageRoot,
        string probeApprovedRoot,
        string probePackageRoot,
        byte[] probeManifestBytes,
        Func<string, string, TrustedToolPackageManifest, TrustedToolVerificationResult> verify,
        Func<string, FileAttributes>? getAttributes = null)
        : this(
            llmFitApprovedRoot,
            llmFitPackageRoot,
            probeApprovedRoot,
            probePackageRoot,
            CreateFixedReader(probeManifestBytes),
            verify,
            getAttributes ?? File.GetAttributes)
    {
    }

    private FixedHardwareToolAcquisition(
        string llmFitApprovedRoot,
        string llmFitPackageRoot,
        string probeApprovedRoot,
        string probePackageRoot,
        Func<byte[]> probeManifestReader,
        Func<string, string, TrustedToolPackageManifest, TrustedToolVerificationResult> verify,
        Func<string, FileAttributes> getAttributes)
    {
        _llmFitApprovedRoot = CanonicalizeRequiredPath(llmFitApprovedRoot, nameof(llmFitApprovedRoot));
        _llmFitPackageRoot = CanonicalizeRequiredPath(llmFitPackageRoot, nameof(llmFitPackageRoot));
        _probeApprovedRoot = CanonicalizeRequiredPath(probeApprovedRoot, nameof(probeApprovedRoot));
        _probePackageRoot = CanonicalizeRequiredPath(probePackageRoot, nameof(probePackageRoot));
        _probeManifestReader = probeManifestReader ?? throw new ArgumentNullException(nameof(probeManifestReader));
        _verify = verify ?? throw new ArgumentNullException(nameof(verify));
        _getAttributes = getAttributes ?? throw new ArgumentNullException(nameof(getAttributes));
    }

    internal static FixedHardwareToolAcquisition CreateProduction()
    {
        string commonApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(commonApplicationData))
        {
            throw new InvalidOperationException("Machine-wide application data is unavailable.");
        }

        string llmFitApprovedRoot = Path.GetFullPath(Path.Combine(
            commonApplicationData,
            "GraniteEdgeAI",
            "HardwareInspection"));
        string packageBase = ResolveApprovedApplicationRoot();
        string hardwareRoot = Path.GetFullPath(Path.Combine(packageBase, "HardwareInspection"));
        string probeRoot = Path.GetFullPath(Path.Combine(hardwareRoot, "LlamaCppProbe"));
        string manifestPath = Path.GetFullPath(Path.Combine(
            hardwareRoot,
            "llamacpp-probe-manifest.json"));
        TrustedToolPackageVerifier verifier = new();
        return new(
            llmFitApprovedRoot,
            LlmFitToolAuthority.GetProductionPackageRoot(),
            hardwareRoot,
            probeRoot,
            () => TrustedManifestFile.ReadBounded(
                manifestPath,
                LlamaCppProbeManifestParser.MaximumManifestBytes),
            verifier.Verify,
            File.GetAttributes);
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

    internal static string ResolveApprovedApplicationRoot(
        bool packageIdentityAvailable,
        string? installedPackageRoot,
        string applicationBaseDirectory)
    {
        return packageIdentityAvailable
            ? RequireExistingAbsoluteRoot(
                installedPackageRoot,
                InstalledRootUnavailableMessage)
            : RequireExistingAbsoluteRoot(
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
            error is ArgumentException or NotSupportedException or PathTooLongException)
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
        return result switch
        {
            ErrorSuccess or ErrorInsufficientBuffer => true,
            AppModelErrorNoPackage => false,
            _ => throw new InvalidOperationException(
                PackageIdentityUnavailableMessage),
        };
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

    public HardwareToolAcquisitionResult Acquire()
    {
        VerifiedTrustedTool? llmFit = null;
        VerifiedTrustedTool? probe = null;
        bool ownershipTransferred = false;
        try
        {
            bool llmFitUnavailable;
            try
            {
                _ = _getAttributes(_llmFitPackageRoot);
                llmFitUnavailable = false;
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                llmFitUnavailable = true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return HardwareToolAcquisitionResult.Failure(
                    HardwareToolAcquisitionDiagnosticCode.ToolIntegrityFailure);
            }
            if (!llmFitUnavailable)
            {
                TrustedToolVerificationResult llmFitVerification = _verify(
                    _llmFitApprovedRoot,
                    _llmFitPackageRoot,
                    LlmFitToolAuthority.Manifest);
                if (!llmFitVerification.IsVerified || llmFitVerification.Tool is null)
                {
                    return HardwareToolAcquisitionResult.Failure(
                        HardwareToolAcquisitionDiagnosticCode.ToolIntegrityFailure);
                }

                llmFit = llmFitVerification.Tool;
            }
            if (!Directory.Exists(_probePackageRoot))
            {
                return HardwareToolAcquisitionResult.Failure(
                    HardwareToolAcquisitionDiagnosticCode.PackagedProbeUnavailable);
            }

            TrustedToolPackageManifest probeManifest;
            try
            {
                probeManifest = LlamaCppProbeManifestParser.Parse(_probeManifestReader());
            }
            catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                return HardwareToolAcquisitionResult.Failure(
                    HardwareToolAcquisitionDiagnosticCode.PackagedProbeUnavailable);
            }

            TrustedToolVerificationResult probeVerification = _verify(
                _probeApprovedRoot,
                _probePackageRoot,
                probeManifest);
            if (!probeVerification.IsVerified || probeVerification.Tool is null)
            {
                return HardwareToolAcquisitionResult.Failure(
                    HardwareToolAcquisitionDiagnosticCode.PackagedProbeUnavailable);
            }

            probe = probeVerification.Tool;
            HardwareToolLease lease = new(
                llmFit,
                probe,
                llmFitUnavailable
                    ? HardwareToolAcquisitionDiagnosticCode.ToolNotAvailable
                    : null);
            ownershipTransferred = true;
            return HardwareToolAcquisitionResult.Success(lease);
        }
        finally
        {
            if (!ownershipTransferred)
            {
                try
                {
                    probe?.Dispose();
                }
                finally
                {
                    llmFit?.Dispose();
                }
            }
        }
    }

    private static Func<byte[]> CreateFixedReader(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        byte[] copy = (byte[])bytes.Clone();
        return () => (byte[])copy.Clone();
    }

    private static string CanonicalizeRequiredPath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A fixed tool path is required.", parameterName);
        }

        return Path.GetFullPath(path);
    }
}
