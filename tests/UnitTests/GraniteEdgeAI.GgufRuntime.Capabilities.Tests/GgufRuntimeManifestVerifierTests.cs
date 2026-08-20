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
        Assert.AreEqual(Path.GetFullPath(package.CliPath), result.CliExecutable);
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
            CliPath = cliPath;
            Manifest = manifest;
        }

        public string Root { get; }

        public string SupervisorPath { get; }

        public string CliPath { get; }

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
            string cli = Path.Combine(root, "llama-cli.exe");
            File.Copy(source, supervisor);
            File.Copy(source, cli);
            GgufRuntimeManifestEntry supervisorEntry = CreateEntry(
                supervisor,
                "gguf-worker.exe",
                GgufRuntimeFileRole.Supervisor);
            GgufRuntimeManifestEntry cliEntry = CreateEntry(
                cli,
                "llama-cli.exe",
                GgufRuntimeFileRole.Cli);
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
    }
}
