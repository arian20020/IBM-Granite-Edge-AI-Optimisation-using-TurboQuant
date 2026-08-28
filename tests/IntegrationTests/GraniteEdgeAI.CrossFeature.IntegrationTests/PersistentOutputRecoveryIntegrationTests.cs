using System.Security.Cryptography;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class PersistentOutputRecoveryIntegrationTests
{
    [TestMethod]
    public async Task PublishedGgufIdentityIsReusedByChatAndExportAfterRestart()
    {
        using var fixture = new OutputFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot, fixture.CommittedRoot);
        using OptimizationOutputLease lease = registry.CreateLease(plan, 1);
        byte[] outputBytes = "deterministic-optimized-gguf"u8.ToArray();
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync(outputBytes);
        SealedOptimizationCandidate candidate = lease.Seal("optimized-model-1");
        OptimizationCommitReceipt receipt = await registry.AdmitAsync(
            plan, 1, fixture.SourceSnapshot(), true, candidate,
            CancellationToken.None);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan, receipt.Key.OutputIdentity,
            receipt.Key.OutputManifestSha256, receipt.OutputSizeBytes,
            sourceUnchanged: true, DateTimeOffset.UnixEpoch);

        var restarted = new OptimizationOutputRegistry(
            fixture.StagingRoot, fixture.CommittedRoot);
        Assert.IsTrue(restarted.TryGetPublishedGgufFile(
            result, out string? chatPath, out string? chatDigest,
            out ulong chatLength));
        Assert.IsTrue(restarted.TryGetPublishedGgufFile(
            result, out string? exportPath, out string? exportDigest,
            out ulong exportLength));
        string independentlyHashed = Convert.ToHexString(
            SHA256.HashData(outputBytes)).ToLowerInvariant();
        Assert.AreEqual(chatPath, exportPath);
        Assert.AreEqual(independentlyHashed, chatDigest);
        Assert.AreEqual(chatDigest, exportDigest);
        Assert.AreEqual((ulong)outputBytes.Length, chatLength);
        Assert.AreEqual(chatLength, exportLength);
        CollectionAssert.AreEqual(outputBytes, File.ReadAllBytes(chatPath!));
    }

    [TestMethod]
    public void RestartQuarantinesOutputWithoutIdentityBoundReceipt()
    {
        using var fixture = new OutputFixture();
        string orphan = Path.Combine(
            fixture.CommittedRoot, "publication-interrupted");
        Directory.CreateDirectory(orphan);
        File.WriteAllText(Path.Combine(orphan, "model.gguf"), "unreceipted");

        var restarted = new OptimizationOutputRegistry(
            fixture.StagingRoot, fixture.CommittedRoot);

        Assert.AreEqual(0, restarted.AdmittedCount);
        Assert.IsFalse(Directory.Exists(orphan));
    }

    [TestMethod]
    public void SamePlanAttemptCannotPublishThroughDuplicateLiveLeases()
    {
        using var fixture = new OutputFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot, fixture.CommittedRoot);
        using OptimizationOutputLease first = registry.CreateLease(plan, 17);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            registry.CreateLease(plan, 17));
    }

    [TestMethod]
    public async Task PublishedPlanRejectsASecondTerminalOutputAsStale()
    {
        using var fixture = new OutputFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot, fixture.CommittedRoot);
        using (OptimizationOutputLease first = registry.CreateLease(plan, 21))
        {
            await using FileStream output = first.CreateFileForWrite("model.gguf");
            await output.WriteAsync("first-terminal-output"u8.ToArray());
            await output.DisposeAsync();
            SealedOptimizationCandidate candidate = first.Seal("first-output");
            _ = await registry.AdmitAsync(
                plan, 21, fixture.SourceSnapshot(), true, candidate,
                CancellationToken.None);
        }

        using OptimizationOutputLease stale = registry.CreateLease(plan, 22);
        await using (FileStream output = stale.CreateFileForWrite("model.gguf"))
            await output.WriteAsync("stale-terminal-output"u8.ToArray());
        SealedOptimizationCandidate staleCandidate = stale.Seal("stale-output");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            registry.AdmitAsync(
                plan, 22, fixture.SourceSnapshot(), true, staleCandidate,
                CancellationToken.None));
        Assert.AreEqual(1, registry.AdmittedCount);
    }

    private sealed class OutputFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "geai-t1-output-" + Guid.NewGuid().ToString("N"));
        private readonly byte[] _source = "inspected-source"u8.ToArray();

        internal OutputFixture()
        {
            Directory.CreateDirectory(StagingRoot);
            Directory.CreateDirectory(CommittedRoot);
            File.WriteAllBytes(SourcePath, _source);
        }

        internal string StagingRoot => Path.Combine(_root, "staging");
        internal string CommittedRoot => Path.Combine(_root, "committed");
        private string SourcePath => Path.Combine(_root, "source.gguf");
        private string Digest => Convert.ToHexString(
            SHA256.HashData(_source)).ToLowerInvariant();

        internal OptimizationExecutionPlan Plan() =>
            CrossFeaturePlanFixture.PersistentGgufPlan(Digest, (ulong)_source.Length);

        internal StagedSourceSnapshot SourceSnapshot() => new(
            Digest, (ulong)_source.Length, "sealed-source-t1", SourcePath);

        public void Dispose()
        {
            if (!Directory.Exists(_root)) return;
            foreach (string file in Directory.EnumerateFiles(
                _root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_root, recursive: true);
        }
    }
}
