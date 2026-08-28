using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationSourceResolverTests
{
    [TestMethod]
    public async Task GgufSourceIsCopiedHashedAndMutationFailsFinalAttestation()
    {
        using var fixture = new SourceFixture();
        byte[] bytes = "verified gguf source"u8.ToArray();
        File.WriteAllBytes(fixture.SourcePath, bytes);
        string digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Guid handoff = Guid.NewGuid();
        var key = new ModelSourceCustodyKey(
            handoff, digest, bytes.Length, OptimizationRoute.Gguf);
        using var custody = new ModelSourceCustodyRegistry();
        Assert.IsTrue(custody.Register(new ModelSourceCustodyRecord(key, fixture.SourcePath)));
        var resolver = new OptimizationSourceResolver(custody, fixture.StagingRoot);

        using StagedSourceSnapshot snapshot = await resolver.ResolveGgufAsync(
            key, 1, CancellationToken.None);

        Assert.AreEqual(digest, snapshot.SourceSha256);
        Assert.AreEqual((ulong)bytes.Length, snapshot.SourceLengthBytes);
        Assert.IsTrue(snapshot.RehashMatches());
        File.WriteAllText(fixture.SourcePath, "changed");
        Assert.IsFalse(snapshot.RehashMatches());
    }

    [TestMethod]
    public async Task ChangedSourceBeforeCopyIsRejected()
    {
        using var fixture = new SourceFixture();
        File.WriteAllText(fixture.SourcePath, "new content");
        string oldDigest = new string('1', 64);
        var key = new ModelSourceCustodyKey(
            Guid.NewGuid(), oldDigest, 12, OptimizationRoute.Gguf);
        using var custody = new ModelSourceCustodyRegistry();
        custody.Register(new ModelSourceCustodyRecord(key, fixture.SourcePath));
        var resolver = new OptimizationSourceResolver(custody, fixture.StagingRoot);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            resolver.ResolveGgufAsync(key, 1, CancellationToken.None));
    }

    [TestMethod]
    public async Task OpenVinoPackageUsesExactCustodyLeaseWithoutRedundantCopy()
    {
        using var fixture = new SourceFixture();
        string package = Path.Combine(fixture.Root, "openvino");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "config.json"), "{}");
        File.WriteAllBytes(Path.Combine(package, "openvino_model.bin"), [1, 2, 3, 4]);
        OpenVinoSourceMember[] members = Directory.EnumerateFiles(package)
            .Select(path => new OpenVinoSourceMember(
                Path.GetFileName(path),
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
                    .ToLowerInvariant(),
                checked((ulong)new FileInfo(path).Length)))
            .OrderBy(member => member.RelativePath, StringComparer.Ordinal)
            .ToArray();
        using var packageHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (OpenVinoSourceMember member in members)
        {
            packageHash.AppendData(Encoding.UTF8.GetBytes(
                member.RelativePath + "\0" + member.Sha256 + "\0"
                + member.LengthBytes + "\n"));
        }
        string digest = Convert.ToHexString(packageHash.GetHashAndReset())
            .ToLowerInvariant();
        long length = checked((long)members.Aggregate(
            0UL,
            static (total, member) => checked(total + member.LengthBytes)));
        var key = new ModelSourceCustodyKey(
            Guid.NewGuid(), digest, length, OptimizationRoute.OpenVino);
        using var custody = new ModelSourceCustodyRegistry();
        Assert.IsTrue(custody.Register(new ModelSourceCustodyRecord(key, package)));
        var resolver = new OptimizationSourceResolver(custody, fixture.StagingRoot);

        using StagedSourceSnapshot snapshot = await resolver.ResolveOpenVinoAsync(
            key, members, 1, CancellationToken.None);

        Assert.IsTrue(snapshot.RehashMatches());
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(fixture.StagingRoot).Any());
        File.WriteAllBytes(Path.Combine(package, "openvino_model.bin"), [4, 3, 2, 1]);
        Assert.IsFalse(snapshot.RehashMatches());
    }

    [TestMethod]
    public void SnapshotPublicSurfaceDoesNotExposeAPath()
    {
        string[] names = typeof(StagedSourceSnapshot)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();
        Assert.IsTrue(names.All(name =>
            !name.Contains("Path", StringComparison.OrdinalIgnoreCase)
            && !name.Contains("File", StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class SourceFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "geai-source-test-" + Guid.NewGuid().ToString("N"));

        internal SourceFixture()
        {
            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(StagingRoot);
        }

        internal string SourcePath => Path.Combine(_root, "model.gguf");
        internal string Root => _root;
        internal string StagingRoot => Path.Combine(_root, "staging");

        public void Dispose()
        {
            if (!Directory.Exists(_root)) return;
            foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_root, recursive: true);
        }
    }
}
