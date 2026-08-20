using System.Security.Cryptography;
using System.Text.Json;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.WorkerClient.Tests;

[TestClass]
public sealed class OpenVinoWorkerClosureResolverTests
{
    [TestMethod]
    public void RewrittenManifestCannotAuthorizeAReplacedDependency()
    {
        using ClosureFixture fixture = ClosureFixture.Create();
        string approvedDigest = fixture.ManifestDigest;
        fixture.ReplaceDependencyAndRewriteManifest();

        WorkerClientPolicyException error = Assert.ThrowsExactly<WorkerClientPolicyException>(() =>
            new OpenVinoWorkerClosureResolver().Resolve(fixture.Installation(approvedDigest)));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            error.Failure.Code);
    }

    [TestMethod]
    public void RetainedClosureLeaseDeniesPostVerificationMutationUntilDisposed()
    {
        using ClosureFixture fixture = ClosureFixture.Create();
        bool writeDenied = false;
        OpenVinoWorkerClosureResolver resolver = new(afterHandlesAcquired: () =>
        {
            try
            {
                File.WriteAllText(fixture.DependencyPath, "replacement");
            }
            catch (IOException)
            {
                writeDenied = true;
            }
        });

        using (VerifiedOpenVinoWorkerClosure closure =
            resolver.Resolve(fixture.Installation(fixture.ManifestDigest)))
        {
            Assert.IsTrue(writeDenied, "The verified dependency was writable after acquisition.");
            Assert.ThrowsExactly<IOException>(() =>
                File.Open(
                    fixture.ResourcePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None).Dispose());
        }

        using FileStream exclusive = File.Open(
            fixture.ResourcePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
    }

    private sealed class ClosureFixture : IDisposable
    {
        private ClosureFixture(string root)
        {
            Root = root;
            DependencyPath = Path.Combine(root, "native.dll");
            ResourcePath = Path.Combine(root, "license.txt");
        }

        internal string Root { get; }
        internal string DependencyPath { get; }
        internal string ResourcePath { get; }
        internal string ManifestDigest => Sha256(Path.Combine(Root, "worker-manifest.json"));

        internal static ClosureFixture Create()
        {
            string root = Directory.CreateTempSubdirectory("OpenVinoClosure-").FullName;
            ClosureFixture fixture = new(root);
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException();
            File.Copy(executable, Path.Combine(root, "worker.exe"));
            File.Copy(executable, fixture.DependencyPath);
            File.WriteAllText(fixture.ResourcePath, "approved notice");
            fixture.WriteManifest();
            return fixture;
        }

        internal OpenVinoWorkerInstallation Installation(string manifestDigest) => new(
            Root,
            "worker.exe",
            OpenVinoProtocol.OfficialProtocolId,
            new OpenVinoBuildEvidence(
                "runtime-test-build",
                "genai-test-build",
                "tokenizers-test-build",
                manifestDigest),
            ["native.dll", "worker.exe"]);

        internal void ReplaceDependencyAndRewriteManifest()
        {
            File.WriteAllText(DependencyPath, "replacement dependency");
            WriteManifest();
        }

        private void WriteManifest()
        {
            string[] paths = ["license.txt", "native.dll", "worker.exe"];
            object[] files = paths.Select(path =>
            {
                string fullPath = Path.Combine(Root, path);
                return (object)new
                {
                    path,
                    length = new FileInfo(fullPath).Length,
                    sha256 = Sha256(fullPath)
                };
            }).ToArray();
            string json = JsonSerializer.Serialize(new { schemaVersion = 1, files });
            File.WriteAllText(Path.Combine(Root, "worker-manifest.json"), json);
        }

        private static string Sha256(string path) => Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
