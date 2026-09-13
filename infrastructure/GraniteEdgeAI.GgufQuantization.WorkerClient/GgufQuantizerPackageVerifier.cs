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
        string executableSha256,
        long maximumSourceBytes,
        long maximumOutputBytes,
        int standardOutputMaximumBytes,
        int standardErrorMaximumBytes)
    {
        StageRoot = stageRoot;
        ExecutablePath = executablePath;
        ManifestSha256 = manifestSha256;
        ExecutableSha256 = executableSha256;
        MaximumSourceBytes = maximumSourceBytes;
        MaximumOutputBytes = maximumOutputBytes;
        StandardOutputMaximumBytes = standardOutputMaximumBytes;
        StandardErrorMaximumBytes = standardErrorMaximumBytes;
    }

    internal string StageRoot { get; }
    internal string ExecutablePath { get; }
    public string ManifestSha256 { get; }
    public string ExecutableSha256 { get; }
    internal long MaximumSourceBytes { get; }
    internal long MaximumOutputBytes { get; }
    internal int StandardOutputMaximumBytes { get; }
    internal int StandardErrorMaximumBytes { get; }
}

public static class GgufQuantizerPackageVerifier
{
    private const int MaximumManifestBytes = 262_144;
    private const int MaximumManifestFiles = 256;
    private const int MaximumPackageDirectories = 256;
    private const string LegacyPackageId = "granite-edge-ai-llama-quantize-x64";
    private const string LegacySourceCommit = "3f7c29d318e317b63f54c558bc69803963d7d88c";
    private const string AtomicBotPackageId =
        "granite-edge-ai-atomicbot-llama-quantize-x64";
    private const string AtomicBotSourceCommit =
        "519f0c594a8e31467d2e2f2cf17054c9e7e11536";
    private const string AtomicBotExecutableSha256 =
        "0a17247d4807b520532df54f96ed73f3cf6fa921f879d8d465b44541b41d36e3";
    private static readonly string[] AllowedTokens =
        ["Q2_K", "Q3_K_M", "Q4_K_M", "Q5_K_M", "Q6_K", "Q8_0"];
    private static readonly string[] AtomicBotOsProvidedDependencies =
    [
        "KERNEL32.dll",
        "VCRUNTIME140.dll",
        "api-ms-win-crt-heap-l1-1-0.dll",
        "api-ms-win-crt-locale-l1-1-0.dll",
        "api-ms-win-crt-math-l1-1-0.dll",
        "api-ms-win-crt-runtime-l1-1-0.dll",
        "api-ms-win-crt-stdio-l1-1-0.dll",
    ];
    private static readonly string[] AtomicBotAppLocalDependencies =
    [
        "ggml-base.dll",
        "ggml-cpu-alderlake.dll",
        "ggml-cpu-cannonlake.dll",
        "ggml-cpu-cascadelake.dll",
        "ggml-cpu-haswell.dll",
        "ggml-cpu-icelake.dll",
        "ggml-cpu-sandybridge.dll",
        "ggml-cpu-skylakex.dll",
        "ggml-cpu-sse42.dll",
        "ggml-cpu-x64.dll",
        "ggml.dll",
        "llama-common.dll",
        "llama-quantize-impl.dll",
        "llama.dll",
    ];
    private static readonly IReadOnlyDictionary<string, PackageFileIdentity> AtomicBotFiles =
        new Dictionary<string, PackageFileIdentity>(StringComparer.Ordinal)
        {
            ["bin/ggml-base.dll"] = new(684_544, "06facaebf58c5178735b7747a1b38def0ade58d8b604b9d1a9ae036384026217"),
            ["bin/ggml-cpu-alderlake.dll"] = new(894_464, "6c24ef9a6910698fc111644b934e741d3c0d1c37386ed6da6ca3279c2bc2e5e9"),
            ["bin/ggml-cpu-cannonlake.dll"] = new(998_400, "c33bd3e48e1681e60ad7a124e177b3d8afb9a4d726e4e766820e500125a5f0a1"),
            ["bin/ggml-cpu-cascadelake.dll"] = new(997_376, "2f4d274df698bee5f963111a17344d6e1f511d1cc4e136bf556113887f538596"),
            ["bin/ggml-cpu-haswell.dll"] = new(896_000, "032b0ee79134e08a8cb8eabd5f24389212f6507ddd787b34bc1465c8c5471d91"),
            ["bin/ggml-cpu-icelake.dll"] = new(997_376, "380b22746796e26da54de6a7f3d516eb8b223fb1305ac38199e85a9966d4c541"),
            ["bin/ggml-cpu-sandybridge.dll"] = new(839_168, "fa794aa2a87c3558cd016b3e2ffc029aa3f12a55f916115886b38d3757f79a42"),
            ["bin/ggml-cpu-skylakex.dll"] = new(998_400, "9953a96e8a8390636cc96a247f081a4a813843fea8405eb8bf466ce7215cc8fc"),
            ["bin/ggml-cpu-sse42.dll"] = new(758_784, "04d9cb2f00c2be9cfa728e2c1d7cd78c99ffcccb83fccb7b4d7ff10844cfdd03"),
            ["bin/ggml-cpu-x64.dll"] = new(760_832, "c549f4ebd163c8bedb6a081b27472e20c494093321c064c352a3839c6700c09f"),
            ["bin/ggml.dll"] = new(66_560, "5e10680be91cd5a2748d90fb56c56c29fbab1b8f5587beaeda23ae53c5a4f07d"),
            ["bin/llama-common.dll"] = new(9_086_976, "b715e90d25ea63a554245eb485d6f869f59cd6ed0b6df236e3da52ab3dbeb6f7"),
            ["bin/llama-quantize-impl.dll"] = new(331_264, "a51b4b9371043e4fd8c88b320b38b9127a562dc4ebff025e75483ee3c789a6f2"),
            ["bin/llama-quantize.exe"] = new(10_752, AtomicBotExecutableSha256),
            ["bin/llama.dll"] = new(2_382_848, "9cb8142e207cc8b9fff8bf79daca4dc56367643cc615034db5836daf4e8168fc"),
            ["licenses/LICENSE.atomicbot-llama.cpp.txt"] = new(1_099, "bcd8ec749126d45cb06737d0690295d73df4b6e7e194205bcf91190368f27285"),
        };
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
        byte[] bytes = ReadManifest(manifestPath);
        string actualManifestSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(actualManifestSha, expectedManifestSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The quantizer manifest identity changed.");
        }

        PackageManifest manifest;
        try
        {
            ValidateNoDuplicateProperties(bytes);
            manifest = JsonSerializer.Deserialize<PackageManifest>(bytes, JsonOptions)
                ?? throw new InvalidDataException("The quantizer manifest is empty.");
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or ArgumentException)
        {
            throw new InvalidDataException("The quantizer manifest JSON is invalid.");
        }
        if (manifest.SchemaVersion != 1
            || manifest.Source is null
            || !string.Equals(manifest.Target, "llama-quantize", StringComparison.Ordinal)
            || !string.Equals(manifest.Architecture, "x64", StringComparison.Ordinal)
            || !string.Equals(manifest.Configuration, "Release", StringComparison.Ordinal)
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
            || manifest.Files.Length is <= 0 or > MaximumManifestFiles)
        {
            throw new InvalidDataException("The quantizer manifest identity or bounds are invalid.");
        }

        bool atomicBot = IsAtomicBotManifest(manifest);
        if (!atomicBot && !IsLegacyManifest(manifest))
        {
            throw new InvalidDataException("The quantizer manifest identity or bounds are invalid.");
        }

        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var listedExact = new HashSet<string>(StringComparer.Ordinal);
        foreach (PackageFile file in manifest.Files)
        {
            if (!IsManifestPath(file.RelativePath)
                || !listed.Add(file.RelativePath)
                || !listedExact.Add(file.RelativePath)
                || !IsDigest(file.Sha256)
                || file.Length < 0)
            {
                throw new InvalidDataException("The quantizer file table is invalid.");
            }
            string path = RequireRegularPackageMember(
                root,
                Path.Combine(
                    root,
                    file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
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

        string[] actualFiles = EnumeratePackageFiles(root)
            .Where(path => !string.Equals(path, "llama-quantize.package.manifest.json", StringComparison.Ordinal))
            .ToArray();
        if (actualFiles.Any(path => !listedExact.Contains(path)) ||
            actualFiles.Length != listedExact.Count)
        {
            throw new InvalidDataException("The quantizer stage has missing or unlisted files.");
        }

        string executable = RequireRegularPackageMember(
            root,
            Path.Combine(root, "bin", "llama-quantize.exe"));
        if (!listedExact.Contains("bin/llama-quantize.exe"))
        {
            throw new InvalidDataException("The quantizer executable is not manifested.");
        }
        using FileStream executableStream = File.OpenRead(executable);
        string executableSha256 = Convert.ToHexString(
            SHA256.HashData(executableStream)).ToLowerInvariant();
        if (atomicBot
            && !string.Equals(
                executableSha256,
                AtomicBotExecutableSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The AtomicBot quantizer executable identity changed.");
        }
        return new VerifiedGgufQuantizerPackage(
            root,
            executable,
            actualManifestSha,
            executableSha256,
            manifest.MaximumSourceBytes,
            manifest.MaximumOutputBytes,
            manifest.StandardOutputMaximumBytes,
            manifest.StandardErrorMaximumBytes);
    }

    internal static VerifiedGgufQuantizerPackage Reverify(VerifiedGgufQuantizerPackage package) =>
        Verify(package.StageRoot, package.ManifestSha256);

    private static bool IsLegacyManifest(PackageManifest manifest) =>
        string.Equals(manifest.PackageId, LegacyPackageId, StringComparison.Ordinal)
        && string.Equals(
            manifest.Source.Url,
            "https://github.com/ggml-org/llama.cpp.git",
            StringComparison.Ordinal)
        && string.Equals(
            manifest.Source.Commit,
            LegacySourceCommit,
            StringComparison.Ordinal)
        && string.Equals(manifest.LibraryLinkage, "static", StringComparison.Ordinal);

    private static bool IsAtomicBotManifest(PackageManifest manifest)
    {
        if (!string.Equals(manifest.PackageId, AtomicBotPackageId, StringComparison.Ordinal)
            || !string.Equals(
                manifest.Source.Url,
                "https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant",
                StringComparison.Ordinal)
            || !string.Equals(
                manifest.Source.Commit,
                AtomicBotSourceCommit,
                StringComparison.Ordinal)
            || !string.Equals(manifest.LibraryLinkage, "dynamic", StringComparison.Ordinal)
            || manifest.CmakeFlags.Length != 0
            || !string.Equals(
                manifest.Toolchain.VisualStudio,
                "not-attested-by-upstream-release",
                StringComparison.Ordinal)
            || !string.Equals(
                manifest.Toolchain.Msvc,
                "runtime-banner-msvc-19.51.36248.0_pe-linker-14.44",
                StringComparison.Ordinal)
            || !string.Equals(
                manifest.Toolchain.Cmake,
                "not-attested-by-upstream-release",
                StringComparison.Ordinal)
            || !manifest.OsProvidedDependencies.SequenceEqual(
                AtomicBotOsProvidedDependencies,
                StringComparer.Ordinal)
            || !manifest.AppLocalDependencies.SequenceEqual(
                AtomicBotAppLocalDependencies,
                StringComparer.Ordinal)
            || manifest.MaximumSourceBytes != 68_719_476_736L
            || manifest.MaximumOutputBytes != 68_719_476_736L
            || manifest.TimeoutSeconds != 21_600
            || manifest.StandardOutputMaximumBytes != 1_048_576
            || manifest.StandardErrorMaximumBytes != 1_048_576
            || !string.Equals(manifest.License.Identity, "MIT", StringComparison.Ordinal)
            || !string.Equals(
                manifest.License.RelativePath,
                "licenses/LICENSE.atomicbot-llama.cpp.txt",
                StringComparison.Ordinal)
            || manifest.Files.Length != AtomicBotFiles.Count)
        {
            return false;
        }

        foreach (PackageFile file in manifest.Files)
        {
            if (!AtomicBotFiles.TryGetValue(file.RelativePath, out PackageFileIdentity? expected)
                || expected is null
                || file.Length != expected.Length
                || !string.Equals(file.Sha256, expected.Sha256, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string RequireChild(string root, string path)
    {
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A package path escaped the verified stage.");
        }
        return full;
    }

    private static string RequireRegularPackageMember(string root, string path)
    {
        string full = RequireChild(root, path);
        for (DirectoryInfo? directory = new FileInfo(full).Directory;
             directory is not null;
             directory = directory.Parent)
        {
            if (!directory.Exists || IsReparse(directory))
            {
                throw new InvalidDataException(
                    "A quantizer package directory is unavailable or redirected.");
            }

            if (string.Equals(
                    Path.TrimEndingDirectorySeparator(directory.FullName),
                    root,
                    StringComparison.OrdinalIgnoreCase))
            {
                return full;
            }
        }

        throw new InvalidDataException(
            "A quantizer package member escaped the verified stage.");
    }

    private static string[] EnumeratePackageFiles(string root)
    {
        try
        {
            var files = new List<string>(MaximumManifestFiles + 1);
            var pending = new Stack<DirectoryInfo>();
            pending.Push(new DirectoryInfo(root));
            int directoryCount = 0;

            while (pending.TryPop(out DirectoryInfo? directory))
            {
                foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos())
                {
                    if (IsReparse(entry))
                    {
                        throw new InvalidDataException(
                            "A quantizer package entry is redirected.");
                    }

                    if (entry is DirectoryInfo child)
                    {
                        directoryCount = checked(directoryCount + 1);
                        if (directoryCount > MaximumPackageDirectories)
                        {
                            throw new InvalidDataException(
                                "The quantizer package directory count exceeds the limit.");
                        }

                        pending.Push(child);
                        continue;
                    }

                    if (entry is not FileInfo)
                    {
                        throw new InvalidDataException(
                            "The quantizer package contains an unsupported entry.");
                    }

                    files.Add(Path.GetRelativePath(root, entry.FullName).Replace('\\', '/'));
                    if (files.Count > MaximumManifestFiles + 1)
                    {
                        throw new InvalidDataException(
                            "The quantizer package file count exceeds the limit.");
                    }
                }
            }

            return files.ToArray();
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "The quantizer package inventory is unavailable.");
        }
    }

    private static void RequireRegularDirectory(string path)
    {
        for (DirectoryInfo? info = new(path);
             info is not null;
             info = info.Parent)
        {
            if (!info.Exists || IsReparse(info))
            {
                throw new InvalidDataException(
                    "The quantizer stage is unavailable or redirected.");
            }
        }
    }

    private static bool IsReparse(FileSystemInfo info) =>
        (info.Attributes & FileAttributes.ReparsePoint) != 0;

    private static byte[] ReadManifest(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || IsReparse(info))
        {
            throw new InvalidDataException(
                "The quantizer manifest is unavailable or redirected.");
        }

        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.SequentialScan);
        if (stream.Length is <= 0 or > MaximumManifestBytes)
        {
            throw new InvalidDataException(
                "The quantizer manifest exceeds the size limit.");
        }

        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("The quantizer manifest changed while reading.");
        }

        return bytes;
    }

    private static void ValidateNoDuplicateProperties(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 16,
        });
        ValidateNoDuplicateProperties(document.RootElement);
    }

    private static void ValidateNoDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException(
                        "The quantizer manifest contains a duplicate JSON property.");
                }

                ValidateNoDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                ValidateNoDuplicateProperties(item);
            }
        }
    }

    private static bool IsManifestPath(string? value) =>
        value is { Length: > 0 and <= 512 }
        && !Path.IsPathFullyQualified(value)
        && !value.Contains('\\')
        && !value.Contains('\0')
        && value.Split('/').All(segment =>
            segment.Length > 0 && segment is not "." and not "..");

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

    private sealed record PackageFileIdentity(long Length, string Sha256);
}
