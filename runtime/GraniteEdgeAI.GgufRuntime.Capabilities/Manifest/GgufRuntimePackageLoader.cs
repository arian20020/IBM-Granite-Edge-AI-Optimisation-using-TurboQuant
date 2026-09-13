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

        byte[] detachedBytes = ReadDetachedManifest(detached);
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

    private static byte[] ReadDetachedManifest(string path)
    {
        try
        {
            using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.SequentialScan);
            if (stream.Length is <= 0 or > GgufRuntimeManifestJson.MaximumManifestBytes)
            {
                throw new GgufRuntimeTrustException(
                    "runtime-manifest-copy-mismatch");
            }

            byte[] bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1)
            {
                throw new GgufRuntimeTrustException(
                    "runtime-manifest-copy-mismatch");
            }

            return bytes;
        }
        catch (GgufRuntimeTrustException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                NotSupportedException or ArgumentException)
        {
            throw new GgufRuntimeTrustException(
                "runtime-manifest-copy-mismatch");
        }
    }
}
