using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using System;
using System.IO;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal sealed class FixedHardwareToolAcquisition : IHardwareToolAcquisition
{
    private readonly string _llmFitApprovedRoot;
    private readonly string _llmFitPackageRoot;
    private readonly string _probeApprovedRoot;
    private readonly string _probePackageRoot;
    private readonly Func<byte[]> _probeManifestReader;
    private readonly Func<string, string, TrustedToolPackageManifest, TrustedToolVerificationResult> _verify;

    internal FixedHardwareToolAcquisition(
        string llmFitApprovedRoot,
        string llmFitPackageRoot,
        string probeApprovedRoot,
        string probePackageRoot,
        byte[] probeManifestBytes,
        Func<string, string, TrustedToolPackageManifest, TrustedToolVerificationResult> verify)
        : this(
            llmFitApprovedRoot,
            llmFitPackageRoot,
            probeApprovedRoot,
            probePackageRoot,
            CreateFixedReader(probeManifestBytes),
            verify)
    {
    }

    private FixedHardwareToolAcquisition(
        string llmFitApprovedRoot,
        string llmFitPackageRoot,
        string probeApprovedRoot,
        string probePackageRoot,
        Func<byte[]> probeManifestReader,
        Func<string, string, TrustedToolPackageManifest, TrustedToolVerificationResult> verify)
    {
        _llmFitApprovedRoot = CanonicalizeRequiredPath(llmFitApprovedRoot, nameof(llmFitApprovedRoot));
        _llmFitPackageRoot = CanonicalizeRequiredPath(llmFitPackageRoot, nameof(llmFitPackageRoot));
        _probeApprovedRoot = CanonicalizeRequiredPath(probeApprovedRoot, nameof(probeApprovedRoot));
        _probePackageRoot = CanonicalizeRequiredPath(probePackageRoot, nameof(probePackageRoot));
        _probeManifestReader = probeManifestReader ?? throw new ArgumentNullException(nameof(probeManifestReader));
        _verify = verify ?? throw new ArgumentNullException(nameof(verify));
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
        string packageBase = Path.GetFullPath(AppContext.BaseDirectory);
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
            verifier.Verify);
    }

    public HardwareToolAcquisitionResult Acquire()
    {
        VerifiedTrustedTool? llmFit = null;
        VerifiedTrustedTool? probe = null;
        bool ownershipTransferred = false;
        try
        {
            bool llmFitUnavailable = !Directory.Exists(_llmFitPackageRoot);
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
