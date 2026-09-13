using System.Security.Cryptography;
using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Tests;

[TestClass]
public sealed class GgufRuntimeManifestVerifierTests
{
    [TestMethod]
    public void VerifyMatchingPackageReturnsCanonicalRolePaths()
    {
        using var package = TemporaryRuntimePackage.Create();

        VerifiedGgufRuntimePackage result =
            GgufRuntimeManifestVerifier.Verify(package.Root, package.Manifest);

        Assert.AreEqual(
            Path.GetFullPath(package.SupervisorPath),
            result.SupervisorExecutable);
        Assert.AreEqual(Path.GetFullPath(package.AdapterPath), result.AdapterExecutable);
        Assert.AreEqual(package.Manifest.RuntimeBuildId, result.RuntimeBuildId);
    }

    [TestMethod]
    public void VerifyTraversalEntryRejectsPackageWithoutReadingOutsideRoot()
    {
        using var package = TemporaryRuntimePackage.Create();
        GgufRuntimeManifest manifest = package.Manifest with
        {
            Files =
            [
                package.Manifest.Files[0] with { RelativePath = "..\\outside.exe" },
                package.Manifest.Files[1],
            ],
        };

        GgufRuntimeTrustException exception = Assert.ThrowsExactly<GgufRuntimeTrustException>(
            () => GgufRuntimeManifestVerifier.Verify(package.Root, manifest));

        Assert.AreEqual("runtime-manifest-path-invalid", exception.Code);
    }

    [TestMethod]
    public void VerifyHashSubstitutionRejectsPackage()
    {
        using var package = TemporaryRuntimePackage.Create();
        GgufRuntimeManifest manifest = package.Manifest with
        {
            Files =
            [
                package.Manifest.Files[0] with { Sha256 = new string('0', 64) },
                package.Manifest.Files[1],
            ],
        };

        GgufRuntimeTrustException exception = Assert.ThrowsExactly<GgufRuntimeTrustException>(
            () => GgufRuntimeManifestVerifier.Verify(package.Root, manifest));

        Assert.AreEqual("runtime-manifest-hash-mismatch", exception.Code);
    }

    [TestMethod]
    public void VerifyUnlistedPackageMemberRejectsClosure()
    {
        using var package = TemporaryRuntimePackage.Create();
        File.WriteAllText(Path.Combine(package.Root, "unexpected.dll"), "unexpected");

        GgufRuntimeTrustException exception = Assert.ThrowsExactly<GgufRuntimeTrustException>(
            () => GgufRuntimeManifestVerifier.Verify(package.Root, package.Manifest));

        Assert.AreEqual("runtime-package-member-unlisted", exception.Code);
    }

    [TestMethod]
    public void VerifyAcceptsManifestedNonPeLicenseMaterial()
    {
        using var package = TemporaryRuntimePackage.Create();
        string licensePath = Path.Combine(package.Root, "LICENSE.txt");
        File.WriteAllText(licensePath, "fixture license");
        GgufRuntimeManifest manifest = package.Manifest with
        {
            Files =
            [
                .. package.Manifest.Files,
                TemporaryRuntimePackage.CreateEntry(
                    licensePath,
                    "LICENSE.txt",
                    GgufRuntimeFileRole.License),
            ],
        };

        VerifiedGgufRuntimePackage result =
            GgufRuntimeManifestVerifier.Verify(package.Root, manifest);

        Assert.AreEqual(package.SupervisorPath, result.SupervisorExecutable);
    }

    [TestMethod]
    public void VerifyAcceptsHashedArchitectureNeutralManagedDependency()
    {
        using var package = TemporaryRuntimePackage.Create();
        string dependencyPath = Path.Combine(package.Root, "managed.deps.json");
        File.WriteAllText(dependencyPath, "{\"runtimeTarget\":{}}");
        GgufRuntimeManifestEntry dependency = TemporaryRuntimePackage.CreateEntry(
            dependencyPath,
            "managed.deps.json",
            GgufRuntimeFileRole.Dependency) with
        {
            Architecture = GgufRuntimeArchitecture.Any,
        };
        GgufRuntimeManifest manifest = package.Manifest with
        {
            Files = [.. package.Manifest.Files, dependency],
        };

        VerifiedGgufRuntimePackage result =
            GgufRuntimeManifestVerifier.Verify(package.Root, manifest);

        Assert.AreEqual(package.AdapterPath, result.AdapterExecutable);
    }

    [TestMethod]
    public void VerifyVersionTwoReturnsEveryPinnedAtomicBotExecutableRole()
    {
        using var package = TemporaryRuntimePackage.Create();
        GgufRuntimeManifest manifest = package.CreateAtomicBotManifest();

        VerifiedGgufRuntimePackage result =
            GgufRuntimeManifestVerifier.Verify(package.Root, manifest);

        Assert.AreEqual(
            Path.Combine(package.Root, "Native", "Cpu", "llama-server.exe"),
            result.CpuRuntimeExecutable);
        Assert.AreEqual(
            Path.Combine(package.Root, "Native", "Vulkan", "llama-server.exe"),
            result.VulkanRuntimeExecutable);
        Assert.AreEqual(
            Path.Combine(package.Root, "Native", "Cpu", "llama-quantize.exe"),
            result.QuantizerExecutable);
    }

    [TestMethod]
    public void VerifyVersionTwoRejectsAMissingAtomicBotRole()
    {
        using var package = TemporaryRuntimePackage.Create();
        GgufRuntimeManifest manifest = package.Manifest with { SchemaVersion = 2 };

        GgufRuntimeTrustException exception =
            Assert.ThrowsExactly<GgufRuntimeTrustException>(() =>
                GgufRuntimeManifestVerifier.Verify(package.Root, manifest));

        Assert.AreEqual("runtime-manifest-native-role-missing", exception.Code);
    }

    [TestMethod]
    public void VerifyVersionTwoRejectsAMissingVulkanDependencyClosureMember()
    {
        using var package = TemporaryRuntimePackage.Create();
        GgufRuntimeManifest manifest = package.CreateAtomicBotManifest(
            omitRelativePath: "Native/Vulkan/ggml-vulkan.dll");

        GgufRuntimeTrustException exception =
            Assert.ThrowsExactly<GgufRuntimeTrustException>(() =>
                GgufRuntimeManifestVerifier.Verify(package.Root, manifest));

        Assert.AreEqual("runtime-manifest-native-closure-missing", exception.Code);
    }

    [TestMethod]
    public void VerifyVersionTwoRejectsNativeRoleAtANonCanonicalPath()
    {
        using var package = TemporaryRuntimePackage.Create();
        GgufRuntimeManifest manifest = package.CreateAtomicBotManifest();
        GgufRuntimeManifestEntry cpu = manifest.Files.Single(entry =>
            entry.Role == GgufRuntimeFileRole.NativeRuntimeCpu);
        GgufRuntimeManifestEntry vulkan = manifest.Files.Single(entry =>
            entry.Role == GgufRuntimeFileRole.NativeRuntimeVulkan);
        manifest = manifest with
        {
            Files = [.. manifest.Files.Where(entry => entry != cpu && entry != vulkan),
                cpu with { Role = GgufRuntimeFileRole.NativeRuntimeVulkan },
                vulkan with { Role = GgufRuntimeFileRole.NativeRuntimeCpu }],
        };

        GgufRuntimeTrustException exception =
            Assert.ThrowsExactly<GgufRuntimeTrustException>(() =>
                GgufRuntimeManifestVerifier.Verify(package.Root, manifest));

        Assert.AreEqual("runtime-manifest-native-role-path-invalid", exception.Code);
    }

    [TestMethod]
    public void VerifyRejectsAReparsePointDirectoryInAListedPath()
    {
        using var package = TemporaryRuntimePackage.Create();
        string target = Path.Combine(package.Root, "target");
        string link = Path.Combine(package.Root, "linked");
        Directory.CreateDirectory(target);
        File.Copy(Environment.ProcessPath!, Path.Combine(target, "adapter.exe"));
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is IOException or
            UnauthorizedAccessException or NotSupportedException)
        {
            Assert.Inconclusive($"Directory symbolic links are unavailable: {exception.GetType().Name}.");
        }

        GgufRuntimeManifestEntry linkedAdapter = TemporaryRuntimePackage.CreateEntry(
            Path.Combine(link, "adapter.exe"),
            "linked/adapter.exe",
            GgufRuntimeFileRole.Adapter);
        GgufRuntimeManifest manifest = package.Manifest with
        {
            Files = [package.Manifest.Files[0], linkedAdapter],
        };

        GgufRuntimeTrustException failure =
            Assert.ThrowsExactly<GgufRuntimeTrustException>(() =>
                GgufRuntimeManifestVerifier.Verify(package.Root, manifest));

        Assert.AreEqual("runtime-manifest-reparse-directory", failure.Code);
    }

    internal sealed class TemporaryRuntimePackage : IDisposable
    {
        private TemporaryRuntimePackage(
            string root,
            string supervisorPath,
            string cliPath,
            GgufRuntimeManifest manifest)
        {
            Root = root;
            SupervisorPath = supervisorPath;
            AdapterPath = cliPath;
            Manifest = manifest;
        }

        public string Root { get; }

        public string SupervisorPath { get; }

        public string AdapterPath { get; }

        public GgufRuntimeManifest Manifest { get; }

        public static TemporaryRuntimePackage Create()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "granite-g1-runtime-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string source = Environment.ProcessPath
                ?? throw new InvalidOperationException("The test host path is unavailable.");
            string supervisor = Path.Combine(root, "gguf-worker.exe");
            string cli = Path.Combine(root, "granite-edge-stdio-adapter.exe");
            File.Copy(source, supervisor);
            File.Copy(source, cli);
            GgufRuntimeManifestEntry supervisorEntry = CreateEntry(
                supervisor,
                "gguf-worker.exe",
                GgufRuntimeFileRole.Supervisor);
            GgufRuntimeManifestEntry cliEntry = CreateEntry(
                cli,
                "granite-edge-stdio-adapter.exe",
                GgufRuntimeFileRole.Adapter);
            var manifest = new GgufRuntimeManifest(
                SchemaVersion: 1,
                RuntimeBuildId: "cpu-test-build",
                RuntimeSourceCommit: new string('b', 40),
                BuildFlags: ["GGML_NATIVE=OFF", "GGML_VULKAN=OFF"],
                Files: [supervisorEntry, cliEntry]);
            return new TemporaryRuntimePackage(root, supervisor, cli, manifest);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        internal static GgufRuntimeManifestEntry CreateEntry(
            string path,
            string relativePath,
            GgufRuntimeFileRole role)
        {
            var info = new FileInfo(path);
            using FileStream stream = File.OpenRead(path);
            string hash = Convert.ToHexString(SHA256.HashData(stream));
            return new GgufRuntimeManifestEntry(
                relativePath,
                info.Length,
                hash,
                GgufRuntimeArchitecture.X64,
                role,
                "MIT");
        }

        internal string CopyHost(string relativePath)
        {
            string destination = Path.Combine(
                Root,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(Environment.ProcessPath!, destination);
            return destination;
        }

        internal GgufRuntimeManifest CreateAtomicBotManifest(
            string? omitRelativePath = null)
        {
            string[] cpuDependencies =
            [
                "ggml-base.dll", "ggml.dll", "llama-common.dll", "llama.dll",
                "llama-server-impl.dll", "llama-quantize-impl.dll", "mtmd.dll",
                "ggml-cpu-alderlake.dll", "ggml-cpu-cannonlake.dll",
                "ggml-cpu-cascadelake.dll", "ggml-cpu-haswell.dll",
                "ggml-cpu-icelake.dll", "ggml-cpu-sandybridge.dll",
                "ggml-cpu-skylakex.dll", "ggml-cpu-sse42.dll", "ggml-cpu-x64.dll",
            ];
            string[] vulkanDependencies =
            [
                "ggml-base.dll", "ggml.dll", "ggml-vulkan.dll", "llama-common.dll",
                "llama.dll", "llama-server-impl.dll", "mtmd.dll",
                "ggml-cpu-alderlake.dll", "ggml-cpu-cannonlake.dll",
                "ggml-cpu-cascadelake.dll", "ggml-cpu-haswell.dll",
                "ggml-cpu-icelake.dll", "ggml-cpu-sandybridge.dll",
                "ggml-cpu-skylakex.dll", "ggml-cpu-sse42.dll", "ggml-cpu-x64.dll",
            ];
            var entries = new List<GgufRuntimeManifestEntry>(Manifest.Files);
            Add("Native/Cpu/llama-server.exe", GgufRuntimeFileRole.NativeRuntimeCpu);
            Add("Native/Cpu/llama-quantize.exe", GgufRuntimeFileRole.Quantizer);
            Add("Native/Vulkan/llama-server.exe", GgufRuntimeFileRole.NativeRuntimeVulkan);
            Add("Native/Cpu/LICENSE", GgufRuntimeFileRole.License, license: true);
            Add("Native/Vulkan/LICENSE", GgufRuntimeFileRole.License, license: true);
            foreach (string name in cpuDependencies) Add($"Native/Cpu/{name}", GgufRuntimeFileRole.Dependency);
            foreach (string name in vulkanDependencies) Add($"Native/Vulkan/{name}", GgufRuntimeFileRole.Dependency);
            return new GgufRuntimeManifest(
                2,
                "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan",
                new string('b', 40),
                ["Backend=CPU+Vulkan"],
                entries);

            void Add(string relativePath, GgufRuntimeFileRole role, bool license = false)
            {
                if (string.Equals(relativePath, omitRelativePath, StringComparison.Ordinal)) return;
                string absolute = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
                if (license)
                {
                    File.WriteAllText(absolute, "MIT");
                    entries.Add(CreateEntry(absolute, relativePath, role) with
                    {
                        Architecture = GgufRuntimeArchitecture.Any,
                    });
                }
                else
                {
                    File.Copy(Environment.ProcessPath!, absolute);
                    entries.Add(CreateEntry(absolute, relativePath, role));
                }
            }
        }
    }
}
