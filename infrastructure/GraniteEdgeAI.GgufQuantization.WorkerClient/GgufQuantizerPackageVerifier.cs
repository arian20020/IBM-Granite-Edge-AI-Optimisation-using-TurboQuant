using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.GgufQuantization.WorkerClient;

public sealed class VerifiedGgufQuantizerPackage
{
    internal VerifiedGgufQuantizerPackage(
        string stageRoot,
        string executablePath,
        string manifestSha256,
        long maximumSourceBytes,
        long maximumOutputBytes,
        int standardOutputMaximumBytes,
        int standardErrorMaximumBytes)
    {
        StageRoot = stageRoot;
        ExecutablePath = executablePath;
        ManifestSha256 = manifestSha256;
        MaximumSourceBytes = maximumSourceBytes;
        MaximumOutputBytes = maximumOutputBytes;
        StandardOutputMaximumBytes = standardOutputMaximumBytes;
        StandardErrorMaximumBytes = standardErrorMaximumBytes;
    }

    internal string StageRoot { get; }
    internal string ExecutablePath { get; }
    internal string ManifestSha256 { get; }
    internal long MaximumSourceBytes { get; }
    internal long MaximumOutputBytes { get; }
    internal int StandardOutputMaximumBytes { get; }
    internal int StandardErrorMaximumBytes { get; }
}

public static class GgufQuantizerPackageVerifier
{
    private const string PackageId = "granite-edge-ai-llama-quantize-x64";
    private const string SourceCommit = "3f7c29d318e317b63f54c558bc69803963d7d88c";
    private static readonly string[] AllowedTokens =
        ["Q2_K", "Q3_K_M", "Q4_K_M", "Q5_K_M", "Q6_K", "Q8_0"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static VerifiedGgufQuantizerPackage Verify(
        string stageDirectory,
        string expectedManifestSha256)
    {
        RequireDigest(expectedManifestSha256, nameof(expectedManifestSha256));
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stageDirectory));
        RequireRegularDirectory(root);
        string manifestPath = RequireChild(root, Path.Combine(root, "llama-quantize.package.manifest.json"));
        byte[] bytes = File.ReadAllBytes(manifestPath);
        if (bytes.Length > 262_144)
        {
            throw new InvalidDataException("The quantizer manifest exceeds the size limit.");
        }
        string actualManifestSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(actualManifestSha, expectedManifestSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The quantizer manifest identity changed.");
        }

        PackageManifest manifest = JsonSerializer.Deserialize<PackageManifest>(bytes, JsonOptions)
            ?? throw new InvalidDataException("The quantizer manifest is empty.");
        if (manifest.SchemaVersion != 1
            || !string.Equals(manifest.PackageId, PackageId, StringComparison.Ordinal)
            || manifest.Source is null
            || !string.Equals(manifest.Source.Url, "https://github.com/ggml-org/llama.cpp.git", StringComparison.Ordinal)
            || !string.Equals(manifest.Source.Commit, SourceCommit, StringComparison.Ordinal)
            || !string.Equals(manifest.Target, "llama-quantize", StringComparison.Ordinal)
            || !string.Equals(manifest.Architecture, "x64", StringComparison.Ordinal)
            || !string.Equals(manifest.Configuration, "Release", StringComparison.Ordinal)
            || !string.Equals(manifest.LibraryLinkage, "static", StringComparison.Ordinal)
            || !string.Equals(manifest.ExecutableRelativePath, "bin/llama-quantize.exe", StringComparison.Ordinal)
            || manifest.MaximumSourceBytes <= 0
            || manifest.MaximumOutputBytes <= 0
            || manifest.StandardOutputMaximumBytes <= 0
            || manifest.StandardErrorMaximumBytes <= 0
            || manifest.TimeoutSeconds <= 0
            || manifest.CmakeFlags is null
            || manifest.Toolchain is null
            || manifest.OsProvidedDependencies is null
            || manifest.AppLocalDependencies is null
            || manifest.License is null
            || manifest.AllowedTokens is null
            || !manifest.AllowedTokens.SequenceEqual(AllowedTokens, StringComparer.Ordinal)
            || manifest.Files is null
            || manifest.Files.Length == 0)
        {
            throw new InvalidDataException("The quantizer manifest identity or bounds are invalid.");
        }

        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PackageFile file in manifest.Files)
        {
            if (!listed.Add(file.RelativePath) || !IsDigest(file.Sha256) || file.Length < 0)
            {
                throw new InvalidDataException("The quantizer file table is invalid.");
            }
            string path = RequireChild(root, Path.Combine(root, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            FileInfo info = new(path);
            if (!info.Exists || info.Length != file.Length || IsReparse(info))
            {
                throw new InvalidDataException("A quantizer package member changed.");
            }
            using FileStream stream = info.OpenRead();
            string digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (!string.Equals(digest, file.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("A quantizer package member failed verification.");
            }
        }

        string[] actualFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !string.Equals(path, "llama-quantize.package.manifest.json", StringComparison.Ordinal))
            .ToArray();
        if (actualFiles.Any(path => !listed.Contains(path)) || actualFiles.Length != listed.Count)
        {
            throw new InvalidDataException("The quantizer stage has missing or unlisted files.");
        }

        string executable = RequireChild(root, Path.Combine(root, "bin", "llama-quantize.exe"));
        if (!listed.Contains("bin/llama-quantize.exe"))
        {
            throw new InvalidDataException("The quantizer executable is not manifested.");
        }
        return new VerifiedGgufQuantizerPackage(
            root,
            executable,
            actualManifestSha,
            manifest.MaximumSourceBytes,
            manifest.MaximumOutputBytes,
            manifest.StandardOutputMaximumBytes,
            manifest.StandardErrorMaximumBytes);
    }

    internal static VerifiedGgufQuantizerPackage Reverify(VerifiedGgufQuantizerPackage package) =>
        Verify(package.StageRoot, package.ManifestSha256);

    private static string RequireChild(string root, string path)
    {
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A package path escaped the verified stage.");
        }
        return full;
    }

    private static void RequireRegularDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        if (!info.Exists || IsReparse(info))
        {
            throw new InvalidDataException("The quantizer stage is unavailable or redirected.");
        }
    }

    private static bool IsReparse(FileSystemInfo info) =>
        (info.Attributes & FileAttributes.ReparsePoint) != 0;

    private static bool IsDigest(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireDigest(string value, string parameter)
    {
        if (!IsDigest(value))
        {
            throw new ArgumentException("A lowercase SHA-256 identity is required.", parameter);
        }
    }

    private sealed record PackageManifest(
        int SchemaVersion,
        string PackageId,
        PackageSource Source,
        string Target,
        string Architecture,
        string Configuration,
        string LibraryLinkage,
        string[] CmakeFlags,
        PackageToolchain Toolchain,
        string[] OsProvidedDependencies,
        string[] AppLocalDependencies,
        string ExecutableRelativePath,
        string[] AllowedTokens,
        long MaximumSourceBytes,
        long MaximumOutputBytes,
        int TimeoutSeconds,
        int StandardOutputMaximumBytes,
        int StandardErrorMaximumBytes,
        PackageLicense License,
        PackageFile[] Files);

    private sealed record PackageSource(string Url, string Commit);

    private sealed record PackageToolchain(string VisualStudio, string Msvc, string Cmake);

    private sealed record PackageLicense(string Identity, string RelativePath);

    private sealed record PackageFile(string RelativePath, long Length, string Sha256);
}
