using System.Security.Cryptography;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class PersistentOutputRecoveryIntegrationTests
{
    [TestMethod]
    public async Task RegistryRejectsSubstitutedExecutionAndChangedBytes()
    {
        using var fixture = new OutputFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot,
            fixture.CommittedRoot);
        using OptimizationOutputLease lease = registry.CreateLease(plan, 2);
        byte[] original = "execution-bound-output"u8.ToArray();
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync(original);
        SealedOptimizationCandidate candidate = lease.Seal("optimized-model-2");
        Guid executionId = Guid.NewGuid();
        OptimizationCommitReceipt receipt = await registry.AdmitAsync(
            plan,
            2,
            executionId,
            fixture.SourceSnapshot(),
            true,
            candidate,
            CancellationToken.None);
        OptimizationExecutionResult exact = OptimizationExecutionResult.Succeeded(
            plan,
            receipt.Key.OutputIdentity,
            receipt.Key.OutputManifestSha256,
            receipt.OutputSizeBytes,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch,
            executionId);
        OptimizationExecutionResult substituted = OptimizationExecutionResult.Succeeded(
            plan,
            receipt.Key.OutputIdentity,
            receipt.Key.OutputManifestSha256,
            receipt.OutputSizeBytes,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);

        Assert.IsTrue(registry.TryGetPublishedGgufFile(
            exact, out string? path, out _, out _));
        Assert.IsFalse(registry.TryGetPublishedGgufFile(
            substituted, out _, out _, out _));
        byte[] changed = original.ToArray();
        changed[^1] ^= 1;
        File.WriteAllBytes(path!, changed);
        Assert.IsFalse(registry.TryGetPublishedGgufFile(
            exact, out _, out _, out _));
    }

    [TestMethod]
    public async Task ExportUsesExactResultAndAtomicTemporaryFile()
    {
        using var fixture = new OutputFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot,
            fixture.CommittedRoot);
        using OptimizationOutputLease lease = registry.CreateLease(plan, 3);
        byte[] bytes = "streamed-export-output"u8.ToArray();
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync(bytes);
        SealedOptimizationCandidate candidate = lease.Seal("optimized-model-3");
        Guid executionId = Guid.NewGuid();
        OptimizationCommitReceipt receipt = await registry.AdmitAsync(
            plan,
            3,
            executionId,
            fixture.SourceSnapshot(),
            true,
            candidate,
            CancellationToken.None);
        OptimizationExecutionResult exact = OptimizationExecutionResult.Succeeded(
            plan,
            receipt.Key.OutputIdentity,
            receipt.Key.OutputManifestSha256,
            receipt.OutputSizeBytes,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch,
            executionId);
        OptimizationExecutionResult substituted = OptimizationExecutionResult.Succeeded(
            plan,
            receipt.Key.OutputIdentity,
            receipt.Key.OutputManifestSha256,
            receipt.OutputSizeBytes,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);
        string destination = Path.Combine(fixture.ExportRoot, "exported.gguf");

        Assert.IsFalse(await OptimizationExporter.ExportGgufAsync(
            registry,
            substituted,
            destination,
            maximumBytes: 1024,
            CancellationToken.None));
        Assert.IsFalse(File.Exists(destination));
        Assert.IsTrue(await OptimizationExporter.ExportGgufAsync(
            registry,
            exact,
            destination,
            maximumBytes: 1024,
            CancellationToken.None));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(destination));
        Assert.IsFalse(Directory.EnumerateFiles(
            fixture.ExportRoot,
            ".export-*.tmp").Any());
        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await OptimizationExporter.ExportGgufAsync(
                registry,
                exact,
                destination,
                maximumBytes: 1024,
                CancellationToken.None));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(destination));
    }

    [TestMethod]
    public async Task SparseLargeExportStreamsWithBoundedAllocation()
    {
        const long sparseLength = 64L * 1024 * 1024;
        using var fixture = new OutputFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot,
            fixture.CommittedRoot);
        using OptimizationOutputLease lease = registry.CreateLease(plan, 4);
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            output.SetLength(sparseLength);
        SealedOptimizationCandidate candidate = lease.Seal("optimized-model-4");
        Guid executionId = Guid.NewGuid();
        OptimizationCommitReceipt receipt = await registry.AdmitAsync(
            plan,
            4,
            executionId,
            fixture.SourceSnapshot(),
            true,
            candidate,
            CancellationToken.None);
        OptimizationExecutionResult exact = OptimizationExecutionResult.Succeeded(
            plan,
            receipt.Key.OutputIdentity,
            receipt.Key.OutputManifestSha256,
            receipt.OutputSizeBytes,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch,
            executionId);
        string destination = Path.Combine(fixture.ExportRoot, "large.gguf");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetTotalAllocatedBytes(precise: true);
        Assert.IsTrue(await OptimizationExporter.ExportGgufAsync(
            registry,
            exact,
            destination,
            maximumBytes: (ulong)sparseLength,
            CancellationToken.None));
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

        Assert.AreEqual(sparseLength, new FileInfo(destination).Length);
        Assert.IsLessThan(8L * 1024 * 1024, allocated);
    }

    [TestMethod]
    public async Task CancelledExportCleansItsTemporarySibling()
    {
        using var fixture = new OutputFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot,
            fixture.CommittedRoot);
        using OptimizationOutputLease lease = registry.CreateLease(plan, 5);
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync("cancelled-export"u8.ToArray());
        SealedOptimizationCandidate candidate = lease.Seal("optimized-model-5");
        Guid executionId = Guid.NewGuid();
        OptimizationCommitReceipt receipt = await registry.AdmitAsync(
            plan,
            5,
            executionId,
            fixture.SourceSnapshot(),
            true,
            candidate,
            CancellationToken.None);
        OptimizationExecutionResult exact = OptimizationExecutionResult.Succeeded(
            plan,
            receipt.Key.OutputIdentity,
            receipt.Key.OutputManifestSha256,
            receipt.OutputSizeBytes,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch,
            executionId);
        string destination = Path.Combine(fixture.ExportRoot, "cancelled.gguf");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await OptimizationExporter.ExportGgufAsync(
                registry,
                exact,
                destination,
                maximumBytes: 1024,
                cancellation.Token));

        Assert.IsFalse(File.Exists(destination));
        Assert.IsFalse(Directory.EnumerateFiles(
            fixture.ExportRoot,
            ".export-*.tmp").Any());
    }

    [TestMethod]
    public async Task ExportRejectsReparsePointDestinationAncestry()
    {
        using var fixture = new OutputFixture();
        string link = Path.Combine(fixture.Root, "export-link");
        try
        {
            Directory.CreateSymbolicLink(link, fixture.ExportRoot);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or PlatformNotSupportedException)
        {
            Assert.Inconclusive("Directory symbolic links are unavailable: "
                + exception.GetType().Name);
        }
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot,
            fixture.CommittedRoot);
        using OptimizationOutputLease lease = registry.CreateLease(plan, 6);
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync("reparse-export"u8.ToArray());
        SealedOptimizationCandidate candidate = lease.Seal("optimized-model-6");
        Guid executionId = Guid.NewGuid();
        OptimizationCommitReceipt receipt = await registry.AdmitAsync(
            plan,
            6,
            executionId,
            fixture.SourceSnapshot(),
            true,
            candidate,
            CancellationToken.None);
        OptimizationExecutionResult exact = OptimizationExecutionResult.Succeeded(
            plan,
            receipt.Key.OutputIdentity,
            receipt.Key.OutputManifestSha256,
            receipt.OutputSizeBytes,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch,
            executionId);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await OptimizationExporter.ExportGgufAsync(
                registry,
                exact,
                Path.Combine(link, "escaped.gguf"),
                maximumBytes: 1024,
                CancellationToken.None));

        Assert.IsFalse(File.Exists(Path.Combine(fixture.ExportRoot, "escaped.gguf")));
    }

    [TestMethod]
    public async Task ExportRejectsEveryExistingAncestorReparsePoint()
    {
        using var fixture = new OutputFixture();
        string link = Path.Combine(fixture.Root, "export-ancestor-link");
        string nested = Path.Combine(fixture.ExportRoot, "nested");
        Directory.CreateDirectory(nested);
        try
        {
            Directory.CreateSymbolicLink(link, fixture.ExportRoot);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or PlatformNotSupportedException)
        {
            Assert.Inconclusive("Directory symbolic links are unavailable: "
                + exception.GetType().Name);
        }
        try
        {
            OptimizationExecutionPlan plan = fixture.Plan();
            var registry = new OptimizationOutputRegistry(
                fixture.StagingRoot,
                fixture.CommittedRoot);
            using OptimizationOutputLease lease = registry.CreateLease(plan, 7);
            await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
                await output.WriteAsync("ancestor-reparse"u8.ToArray());
            SealedOptimizationCandidate candidate = lease.Seal("optimized-model-7");
            Guid executionId = Guid.NewGuid();
            OptimizationCommitReceipt receipt = await registry.AdmitAsync(
                plan,
                7,
                executionId,
                fixture.SourceSnapshot(),
                true,
                candidate,
                CancellationToken.None);
            OptimizationExecutionResult exact = OptimizationExecutionResult.Succeeded(
                plan,
                receipt.Key.OutputIdentity,
                receipt.Key.OutputManifestSha256,
                receipt.OutputSizeBytes,
                sourceUnchanged: true,
                DateTimeOffset.UnixEpoch,
                executionId);
            string escaped = Path.Combine(link, "nested", "escaped.gguf");

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
                await OptimizationExporter.ExportGgufAsync(
                    registry,
                    exact,
                    escaped,
                    maximumBytes: 1024,
                    CancellationToken.None));

            Assert.IsFalse(File.Exists(Path.Combine(nested, "escaped.gguf")));
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }
        }
    }

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
            sourceUnchanged: true, DateTimeOffset.UnixEpoch,
            receipt.Key.ExecutionId);

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

    private sealed class OutputFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "geai-t1-output-" + Guid.NewGuid().ToString("N"));
        private readonly byte[] _source = "inspected-source"u8.ToArray();

        internal OutputFixture()
        {
            Directory.CreateDirectory(StagingRoot);
            Directory.CreateDirectory(CommittedRoot);
            Directory.CreateDirectory(ExportRoot);
            File.WriteAllBytes(SourcePath, _source);
        }

        internal string StagingRoot => Path.Combine(_root, "staging");
        internal string CommittedRoot => Path.Combine(_root, "committed");
        internal string ExportRoot => Path.Combine(_root, "export");
        internal string Root => _root;
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
