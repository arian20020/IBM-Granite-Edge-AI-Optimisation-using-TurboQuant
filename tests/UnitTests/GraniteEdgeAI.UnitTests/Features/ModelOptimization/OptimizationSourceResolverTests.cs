using System.Security.Cryptography;
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
