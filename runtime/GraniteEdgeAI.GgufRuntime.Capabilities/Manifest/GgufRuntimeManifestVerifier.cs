using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;

public static class GgufRuntimeManifestVerifier
{
    private static readonly Dictionary<string, GgufRuntimeFileRole>
        AtomicBotNativeRolePaths = new Dictionary<string, GgufRuntimeFileRole>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Native/Cpu/llama-server.exe"] = GgufRuntimeFileRole.NativeRuntimeCpu,
            ["Native/Vulkan/llama-server.exe"] = GgufRuntimeFileRole.NativeRuntimeVulkan,
            ["Native/Cpu/llama-quantize.exe"] = GgufRuntimeFileRole.Quantizer,
        };

    private static readonly string[] AtomicBotCpuClosure =
    [
        "Native/Cpu/LICENSE", "Native/Cpu/ggml-base.dll", "Native/Cpu/ggml.dll",
        "Native/Cpu/llama-common.dll", "Native/Cpu/llama.dll",
        "Native/Cpu/llama-server-impl.dll", "Native/Cpu/llama-quantize-impl.dll",
        "Native/Cpu/mtmd.dll", "Native/Cpu/ggml-cpu-alderlake.dll",
        "Native/Cpu/ggml-cpu-cannonlake.dll", "Native/Cpu/ggml-cpu-cascadelake.dll",
        "Native/Cpu/ggml-cpu-haswell.dll", "Native/Cpu/ggml-cpu-icelake.dll",
        "Native/Cpu/ggml-cpu-sandybridge.dll", "Native/Cpu/ggml-cpu-skylakex.dll",
        "Native/Cpu/ggml-cpu-sse42.dll", "Native/Cpu/ggml-cpu-x64.dll",
    ];

    private static readonly string[] AtomicBotVulkanClosure =
    [
        "Native/Vulkan/LICENSE", "Native/Vulkan/ggml-base.dll", "Native/Vulkan/ggml.dll",
        "Native/Vulkan/ggml-vulkan.dll", "Native/Vulkan/llama-common.dll",
        "Native/Vulkan/llama.dll", "Native/Vulkan/llama-server-impl.dll",
        "Native/Vulkan/mtmd.dll", "Native/Vulkan/ggml-cpu-alderlake.dll",
        "Native/Vulkan/ggml-cpu-cannonlake.dll", "Native/Vulkan/ggml-cpu-cascadelake.dll",
        "Native/Vulkan/ggml-cpu-haswell.dll", "Native/Vulkan/ggml-cpu-icelake.dll",
        "Native/Vulkan/ggml-cpu-sandybridge.dll", "Native/Vulkan/ggml-cpu-skylakex.dll",
        "Native/Vulkan/ggml-cpu-sse42.dll", "Native/Vulkan/ggml-cpu-x64.dll",
    ];

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
        RejectReparseDirectory(root);

        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        var listedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? supervisor = null;
        string? adapter = null;
        string? cpuRuntime = null;
        string? vulkanRuntime = null;
        string? quantizer = null;

        foreach (GgufRuntimeManifestEntry entry in manifest.Files)
        {
            ValidateEntryShape(entry);
            string fullPath = ResolveContainedPath(rootPrefix, root, entry.RelativePath);
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
                case GgufRuntimeFileRole.NativeRuntimeCpu when cpuRuntime is null:
                    cpuRuntime = fullPath;
                    break;
                case GgufRuntimeFileRole.NativeRuntimeVulkan when vulkanRuntime is null:
                    vulkanRuntime = fullPath;
                    break;
                case GgufRuntimeFileRole.Quantizer when quantizer is null:
                    quantizer = fullPath;
                    break;
                case GgufRuntimeFileRole.Supervisor:
                case GgufRuntimeFileRole.Adapter:
                case GgufRuntimeFileRole.NativeRuntimeCpu:
                case GgufRuntimeFileRole.NativeRuntimeVulkan:
                case GgufRuntimeFileRole.Quantizer:
                    throw new GgufRuntimeTrustException("runtime-manifest-role-duplicate");
            }
        }

        foreach (string actualPath in EnumerateFilesSafely(root))
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

        if (manifest.SchemaVersion == 2
            && (cpuRuntime is null || vulkanRuntime is null || quantizer is null))
        {
            throw new GgufRuntimeTrustException("runtime-manifest-native-role-missing");
        }

        if (manifest.SchemaVersion == 2)
        {
            ValidateAtomicBotClosure(manifest.Files);
        }

        if (manifest.SchemaVersion == 1
            && (cpuRuntime is not null || vulkanRuntime is not null || quantizer is not null))
        {
            throw new GgufRuntimeTrustException("runtime-manifest-invalid");
        }

        return new VerifiedGgufRuntimePackage(
            supervisor,
            adapter,
            manifest.RuntimeBuildId,
            manifest.RuntimeSourceCommit,
            manifest.BuildFlags.ToArray(),
            cpuRuntime,
            vulkanRuntime,
            quantizer);
    }

    private static void ValidateManifestHeader(GgufRuntimeManifest manifest)
    {
        if (manifest.SchemaVersion is not (1 or 2) ||
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

    private static string ResolveContainedPath(
        string rootPrefix,
        string root,
        string relativePath)
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

        string? directory = Path.GetDirectoryName(fullPath);
        while (directory is not null)
        {
            RejectReparseDirectory(directory);
            if (directory.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            directory = Path.GetDirectoryName(directory);
        }

        return fullPath;
    }

    private static void ValidateAtomicBotClosure(
        IReadOnlyList<GgufRuntimeManifestEntry> files)
    {
        var entries = files.ToDictionary(
            static entry => entry.RelativePath,
            StringComparer.OrdinalIgnoreCase);
        foreach ((string path, GgufRuntimeFileRole role) in AtomicBotNativeRolePaths)
        {
            if (!entries.TryGetValue(path, out GgufRuntimeManifestEntry? entry)
                || entry.Role != role)
            {
                throw new GgufRuntimeTrustException(
                    "runtime-manifest-native-role-path-invalid");
            }
        }

        foreach (GgufRuntimeManifestEntry entry in files.Where(entry =>
                     entry.Role is GgufRuntimeFileRole.NativeRuntimeCpu
                         or GgufRuntimeFileRole.NativeRuntimeVulkan
                         or GgufRuntimeFileRole.Quantizer))
        {
            if (!AtomicBotNativeRolePaths.TryGetValue(entry.RelativePath, out GgufRuntimeFileRole role)
                || role != entry.Role)
            {
                throw new GgufRuntimeTrustException(
                    "runtime-manifest-native-role-path-invalid");
            }
        }

        foreach (string path in AtomicBotCpuClosure.Concat(AtomicBotVulkanClosure))
        {
            if (!entries.ContainsKey(path))
            {
                throw new GgufRuntimeTrustException(
                    "runtime-manifest-native-closure-missing");
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            RejectReparseDirectory(directory);
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    RejectReparseDirectory(path);
                    pending.Push(path);
                }
                else
                {
                    yield return path;
                }
            }
        }
    }

    private static void RejectReparseDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new GgufRuntimeTrustException("runtime-manifest-reparse-directory");
        }
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
