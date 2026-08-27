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

    [TestMethod]
    public void ChildInsertedAfterAcquisitionInvalidatesTheClosedInventory()
    {
        using ClosureFixture fixture = ClosureFixture.Create();
        OpenVinoWorkerClosureResolver resolver = new(afterHandlesAcquired: () =>
            File.WriteAllText(Path.Combine(fixture.Root, "unlisted.txt"), "inserted"));

        WorkerClientPolicyException? error = null;
        try
        {
            using VerifiedOpenVinoWorkerClosure unexpected =
                resolver.Resolve(fixture.Installation(fixture.ManifestDigest));
        }
        catch (WorkerClientPolicyException caught)
        {
            error = caught;
        }

        Assert.IsNotNull(error, "The inserted child was accepted.");
        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            error.Failure.Code);
    }

    [TestMethod]
    public void AcceptedFileCannotBeRemovedAndRestoredWhileLeaseIsHeld()
    {
        using ClosureFixture fixture = ClosureFixture.Create();
        bool removalDenied = false;
        string displaced = fixture.ResourcePath + ".displaced";
        OpenVinoWorkerClosureResolver resolver = new(afterHandlesAcquired: () =>
        {
            try
            {
                File.Move(fixture.ResourcePath, displaced);
                File.Move(displaced, fixture.ResourcePath);
            }
            catch (IOException)
            {
                removalDenied = true;
            }
        });

        using VerifiedOpenVinoWorkerClosure closure =
            resolver.Resolve(fixture.Installation(fixture.ManifestDigest));

        Assert.IsTrue(removalDenied);
        Assert.IsTrue(File.Exists(fixture.ResourcePath));
        Assert.IsFalse(File.Exists(displaced));
    }

    [TestMethod]
    public void BinaryOmittedFromCallerPolicyInvalidatesTheClosure()
    {
        using ClosureFixture fixture = ClosureFixture.Create();

        WorkerClientPolicyException? error = null;
        try
        {
            using VerifiedOpenVinoWorkerClosure unexpected =
                new OpenVinoWorkerClosureResolver().Resolve(
                    fixture.Installation(
                        fixture.ManifestDigest,
                        new Dictionary<string, OpenVinoWorkerBinaryMachine>(StringComparer.Ordinal)
                        {
                            ["worker.exe"] = OpenVinoWorkerBinaryMachine.Amd64
                        }));
        }
        catch (WorkerClientPolicyException caught)
        {
            error = caught;
        }

        Assert.IsNotNull(error, "The omitted manifest binary was accepted.");
        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            error.Failure.Code);
    }

    [TestMethod]
    public void ExtraCallerBinaryInvalidatesTheClosure()
    {
        using ClosureFixture fixture = ClosureFixture.Create();
        Dictionary<string, OpenVinoWorkerBinaryMachine> policy = ClosureFixture.BinaryPolicy();
        policy.Add("ghost.dll", OpenVinoWorkerBinaryMachine.Amd64);

        WorkerClientPolicyException error = Assert.ThrowsExactly<WorkerClientPolicyException>(() =>
            new OpenVinoWorkerClosureResolver().Resolve(
                fixture.Installation(fixture.ManifestDigest, policy)));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            error.Failure.Code);
    }

    [TestMethod]
    public void CallerMachineMismatchInvalidatesTheClosure()
    {
        using ClosureFixture fixture = ClosureFixture.Create();
        Dictionary<string, OpenVinoWorkerBinaryMachine> policy = ClosureFixture.BinaryPolicy();
        policy["native.dll"] = OpenVinoWorkerBinaryMachine.I386;

        WorkerClientPolicyException error = Assert.ThrowsExactly<WorkerClientPolicyException>(() =>
            new OpenVinoWorkerClosureResolver().Resolve(
                fixture.Installation(fixture.ManifestDigest, policy)));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            error.Failure.Code);
    }

    [TestMethod]
    public void DirectoryReparseSwapBetweenEnumerationAndOpenIsRejected()
    {
        using ClosureFixture fixture = ClosureFixture.Create();
        string displaced = fixture.ResourceDirectory + ".original";
        string target = Directory.CreateTempSubdirectory("OpenVinoClosureTarget-").FullName;
        bool swapped = false;
        try
        {
            OpenVinoWorkerClosureResolver resolver = new(
                beforePathHandleOpened: relativePath =>
                {
                    if (swapped || !string.Equals(
                        relativePath,
                        "resources",
                        StringComparison.Ordinal))
                    {
                        return;
                    }

                    Directory.Move(fixture.ResourceDirectory, displaced);
                    Directory.CreateSymbolicLink(fixture.ResourceDirectory, target);
                    swapped = true;
                });

            WorkerClientPolicyException error =
                Assert.ThrowsExactly<WorkerClientPolicyException>(() =>
                    resolver.Resolve(fixture.Installation(fixture.ManifestDigest)));

            Assert.IsTrue(swapped);
            Assert.AreEqual(
                WorkerClientFailureCodes.WorkerExecutableUntrusted,
                error.Failure.Code);
        }
        finally
        {
            if (Directory.Exists(fixture.ResourceDirectory) &&
                (File.GetAttributes(fixture.ResourceDirectory) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(fixture.ResourceDirectory);
            }
            if (Directory.Exists(displaced))
            {
                Directory.Move(displaced, fixture.ResourceDirectory);
            }
            Directory.Delete(target);
        }
    }

    private sealed class ClosureFixture : IDisposable
    {
        private ClosureFixture(string root)
        {
            Root = root;
            DependencyPath = Path.Combine(root, "native.dll");
            ResourceDirectory = Path.Combine(root, "resources");
            ResourcePath = Path.Combine(ResourceDirectory, "license.txt");
        }

        internal string Root { get; }
        internal string DependencyPath { get; }
        internal string ResourceDirectory { get; }
        internal string ResourcePath { get; }
        internal string ManifestDigest => Sha256(Path.Combine(Root, "worker-manifest.json"));

        internal static ClosureFixture Create()
        {
            string root = Directory.CreateTempSubdirectory("OpenVinoClosure-").FullName;
            ClosureFixture fixture = new(root);
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException();
            File.Copy(executable, Path.Combine(root, "worker.exe"));
            File.Copy(executable, fixture.DependencyPath);
            Directory.CreateDirectory(fixture.ResourceDirectory);
            File.WriteAllText(fixture.ResourcePath, "approved notice");
            fixture.WriteManifest();
            return fixture;
        }

        internal OpenVinoWorkerInstallation Installation(
            string manifestDigest,
            IReadOnlyDictionary<string, OpenVinoWorkerBinaryMachine>? expectedBinaryMachines = null) => new(
            Root,
            "worker.exe",
            OpenVinoProtocol.OfficialProtocolId,
            new OpenVinoBuildEvidence(
                "runtime-test-build",
                "genai-test-build",
                "tokenizers-test-build",
                manifestDigest),
            expectedBinaryMachines ?? BinaryPolicy());

        internal static Dictionary<string, OpenVinoWorkerBinaryMachine> BinaryPolicy() => new(
            StringComparer.Ordinal)
        {
            ["native.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
            ["worker.exe"] = OpenVinoWorkerBinaryMachine.Amd64
        };

        internal void ReplaceDependencyAndRewriteManifest()
        {
            File.WriteAllText(DependencyPath, "replacement dependency");
            WriteManifest();
        }

        private void WriteManifest()
        {
            string[] paths = ["native.dll", "resources/license.txt", "worker.exe"];
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
