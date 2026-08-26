using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;

public static class GgufRuntimeManifestVerifier
{
    public static VerifiedGgufRuntimePackage Verify(
        string packageRoot,
        GgufRuntimeManifest manifest)
        => Verify(packageRoot, manifest, excludedClosureFile: null);

    internal static VerifiedGgufRuntimePackage Verify(
        string packageRoot,
        GgufRuntimeManifest manifest,
        string? excludedClosureFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateManifestHeader(manifest);

        string root = Path.GetFullPath(packageRoot);
        if (!Directory.Exists(root))
        {
            throw new GgufRuntimeTrustException("runtime-package-missing");
        }

        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        var listedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? supervisor = null;
        string? adapter = null;

        foreach (GgufRuntimeManifestEntry entry in manifest.Files)
        {
            ValidateEntryShape(entry);
            string fullPath = ResolveContainedPath(rootPrefix, entry.RelativePath);
            if (!listedPaths.Add(fullPath))
            {
                throw new GgufRuntimeTrustException("runtime-manifest-member-duplicate");
            }

            VerifyFile(fullPath, entry);
            switch (entry.Role)
            {
                case GgufRuntimeFileRole.Supervisor when supervisor is null:
                    supervisor = fullPath;
                    break;
                case GgufRuntimeFileRole.Adapter when adapter is null:
                    adapter = fullPath;
                    break;
                case GgufRuntimeFileRole.Supervisor:
                case GgufRuntimeFileRole.Adapter:
                    throw new GgufRuntimeTrustException("runtime-manifest-role-duplicate");
            }
        }

        foreach (string actualPath in Directory.EnumerateFiles(
                     root,
                     "*",
                     SearchOption.AllDirectories))
        {
            if (excludedClosureFile is not null &&
                Path.GetFullPath(actualPath).Equals(
                    excludedClosureFile,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!listedPaths.Contains(Path.GetFullPath(actualPath)))
            {
                throw new GgufRuntimeTrustException("runtime-package-member-unlisted");
            }
        }

        if (supervisor is null || adapter is null)
        {
            throw new GgufRuntimeTrustException("runtime-manifest-role-missing");
        }

        return new VerifiedGgufRuntimePackage(
            supervisor,
            adapter,
            manifest.RuntimeBuildId,
            manifest.RuntimeSourceCommit,
            manifest.BuildFlags.ToArray());
    }

    private static void ValidateManifestHeader(GgufRuntimeManifest manifest)
    {
        if (manifest.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(manifest.RuntimeBuildId) ||
            manifest.RuntimeSourceCommit?.Length != 40 ||
            manifest.BuildFlags is null ||
            manifest.Files is null ||
            manifest.Files.Count == 0)
        {
            throw new GgufRuntimeTrustException("runtime-manifest-invalid");
        }
    }

    private static void ValidateEntryShape(GgufRuntimeManifestEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.RelativePath) ||
            entry.Length <= 0 ||
            entry.Sha256?.Length != 64 ||
            !Enum.IsDefined(entry.Architecture) ||
            !Enum.IsDefined(entry.Role) ||
            (entry.Architecture == GgufRuntimeArchitecture.Any &&
             entry.Role != GgufRuntimeFileRole.Dependency &&
             entry.Role != GgufRuntimeFileRole.License) ||
            string.IsNullOrWhiteSpace(entry.LicenseReference))
        {
            throw new GgufRuntimeTrustException("runtime-manifest-entry-invalid");
        }
    }

    private static string ResolveContainedPath(string rootPrefix, string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath))
        {
            throw new GgufRuntimeTrustException("runtime-manifest-path-invalid");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(rootPrefix, relativePath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new GgufRuntimeTrustException("runtime-manifest-path-invalid");
        }

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new GgufRuntimeTrustException("runtime-manifest-path-invalid");
        }

        return fullPath;
    }

    private static void VerifyFile(string fullPath, GgufRuntimeManifestEntry entry)
    {
        if (!File.Exists(fullPath))
        {
            throw new GgufRuntimeTrustException("runtime-manifest-member-missing");
        }

        FileAttributes attributes = File.GetAttributes(fullPath);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new GgufRuntimeTrustException("runtime-manifest-reparse-point");
        }

        var info = new FileInfo(fullPath);
        if (info.Length != entry.Length)
        {
            throw new GgufRuntimeTrustException("runtime-manifest-length-mismatch");
        }

        using FileStream stream = File.OpenRead(fullPath);
        string actualHash = Convert.ToHexString(SHA256.HashData(stream));
        if (!actualHash.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new GgufRuntimeTrustException("runtime-manifest-hash-mismatch");
        }

        if (entry.Role == GgufRuntimeFileRole.License)
        {
            return;
        }

        if (entry.Architecture == GgufRuntimeArchitecture.Any)
        {
            return;
        }

        stream.Position = 0;
        using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
        Machine actualMachine = peReader.PEHeaders.CoffHeader.Machine;
        Machine expectedMachine = entry.Architecture switch
        {
            GgufRuntimeArchitecture.X64 => Machine.Amd64,
            GgufRuntimeArchitecture.Arm64 => Machine.Arm64,
            _ => throw new GgufRuntimeTrustException("runtime-manifest-architecture-invalid"),
        };
        if (actualMachine != expectedMachine)
        {
            throw new GgufRuntimeTrustException("runtime-manifest-architecture-mismatch");
        }
    }
}
