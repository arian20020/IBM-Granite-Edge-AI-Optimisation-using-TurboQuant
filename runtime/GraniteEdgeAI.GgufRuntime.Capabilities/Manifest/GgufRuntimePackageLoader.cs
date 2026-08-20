using System.Security.Cryptography;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;

public static class GgufRuntimePackageLoader
{
    public const string DetachedManifestFileName = "runtime-manifest.json";

    public static VerifiedGgufRuntimePackage Verify(
        string packageRoot,
        ReadOnlySpan<byte> trustedManifest,
        string detachedManifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(detachedManifestPath);
        if (trustedManifest.IsEmpty ||
            trustedManifest.Length > GgufRuntimeManifestJson.MaximumManifestBytes)
        {
            throw new GgufRuntimeTrustException("runtime-manifest-size-invalid");
        }

        string root = Path.GetFullPath(packageRoot);
        string expectedManifest = Path.GetFullPath(Path.Combine(
            root,
            DetachedManifestFileName));
        string detached = Path.GetFullPath(detachedManifestPath);
        if (!detached.Equals(expectedManifest, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(detached) ||
            (File.GetAttributes(detached) & FileAttributes.ReparsePoint) != 0)
        {
            throw new GgufRuntimeTrustException("runtime-manifest-copy-invalid");
        }

        byte[] detachedBytes = File.ReadAllBytes(detached);
        if (detachedBytes.Length != trustedManifest.Length ||
            detachedBytes.Length > GgufRuntimeManifestJson.MaximumManifestBytes ||
            !CryptographicOperations.FixedTimeEquals(
                detachedBytes,
                trustedManifest))
        {
            throw new GgufRuntimeTrustException("runtime-manifest-copy-mismatch");
        }

        GgufRuntimeManifest manifest =
            GgufRuntimeManifestJson.Deserialize(trustedManifest);
        return GgufRuntimeManifestVerifier.Verify(root, manifest, detached);
    }
}
