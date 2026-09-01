using System.Security.Cryptography;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

internal static class ApprovedOpenVinoConverterResolver
{
    private const int MaximumManifestBytes = 8 * 1024 * 1024;

    internal static (string Root, string Digest) Resolve(
        string applicationBaseDirectory,
        string expectedDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedDigest);
        string converterRoot = Path.GetFullPath(Path.Combine(
            applicationBaseDirectory,
            "OpenVino",
            "Converter",
            "Worker"));
        string manifestPath = Path.Combine(converterRoot, "converter-manifest.json");
        string packagedDigest = Convert.ToHexString(
            SHA256.HashData(TrustedManifestFile.ReadBounded(
                manifestPath,
                MaximumManifestBytes))).ToLowerInvariant();
        if (!string.Equals(packagedDigest, expectedDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The packaged converter does not match the app-approved identity.");
        }
        return (converterRoot, expectedDigest);
    }
}
