using System.Security.Cryptography;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationOutputRegistryTests
{
    [TestMethod]
    public async Task SuccessfulAdmissionIsReceiptBackedAndSurvivesRestart()
    {
        using var fixture = new RegistryFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(fixture.StagingRoot, fixture.CommittedRoot);
        using OptimizationOutputLease lease = registry.CreateLease(plan, 1);
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync("optimized"u8.ToArray());
        SealedOptimizationCandidate candidate = lease.Seal("optimized-model-1");

        OptimizationCommitReceipt receipt = await registry.AdmitAsync(
            plan, 1, fixture.SourceSnapshot(), true, candidate, CancellationToken.None);

        Assert.AreEqual(1, registry.AdmittedCount);
        Assert.IsTrue(receipt.SourceUnchanged);
        var restarted = new OptimizationOutputRegistry(fixture.StagingRoot, fixture.CommittedRoot);
        Assert.AreEqual(1, restarted.AdmittedCount);
    }

    [TestMethod]
    public async Task SuccessfulAdmissionDisposesTheEmptyOwnedStagingDirectory()
    {
        using var fixture = new RegistryFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot,
            fixture.CommittedRoot);
        OptimizationOutputLease lease = registry.CreateLease(plan, 14);
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync("published-output"u8.ToArray());
        SealedOptimizationCandidate candidate = lease.Seal("published-model-14");

        await registry.AdmitAsync(
            plan,
            14,
            fixture.SourceSnapshot(),
            true,
            candidate,
            CancellationToken.None);
        lease.Dispose();

        Assert.IsFalse(Directory.EnumerateDirectories(
            fixture.StagingRoot,
            "stage-*",
            SearchOption.TopDirectoryOnly).Any());
    }

    [TestMethod]
    public async Task SuccessfulAdmissionPreservesUnexpectedOwnedStagingEntries()
    {
        using var fixture = new RegistryFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot,
            fixture.CommittedRoot);
        OptimizationOutputLease lease = registry.CreateLease(plan, 15);
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync("published-output"u8.ToArray());
        SealedOptimizationCandidate candidate = lease.Seal("published-model-15");
        string owned = Path.Combine(
            fixture.StagingRoot,
            candidate.SealedStagingIdentity);
        string unexpectedFile = Path.Combine(owned, "unexpected.txt");
        string unexpectedDirectory = Path.Combine(owned, "unexpected-directory");
        string linkedTarget = Path.Combine(fixture.CommittedRoot, "linked-target.txt");
        string unexpectedLink = Path.Combine(owned, "unexpected-link.txt");

        await registry.AdmitAsync(
            plan,
            15,
            fixture.SourceSnapshot(),
            true,
            candidate,
            CancellationToken.None);
        File.WriteAllText(unexpectedFile, "preserve");
        Directory.CreateDirectory(unexpectedDirectory);
        File.WriteAllText(linkedTarget, "target");
        File.CreateSymbolicLink(unexpectedLink, linkedTarget);
        lease.Dispose();

        Assert.IsTrue(File.Exists(unexpectedFile));
        Assert.IsTrue(Directory.Exists(unexpectedDirectory));
        Assert.IsTrue(File.Exists(unexpectedLink));
        Assert.IsTrue(
            (File.GetAttributes(unexpectedLink) & FileAttributes.ReparsePoint) != 0);
    }

    [TestMethod]
    public async Task SuccessfulGgufResultResolvesOnlyItsVerifiedPublishedFile()
    {
        using var fixture = new RegistryFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot,
            fixture.CommittedRoot);
        using OptimizationOutputLease lease = registry.CreateLease(plan, 2);
        byte[] expected = "optimized-result"u8.ToArray();
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync(expected);
        SealedOptimizationCandidate candidate = lease.Seal("optimized-model-2");
        OptimizationCommitReceipt receipt = await registry.AdmitAsync(
            plan,
            2,
            fixture.SourceSnapshot(),
            true,
            candidate,
            CancellationToken.None);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan,
            receipt.Key.OutputIdentity,
            receipt.Key.OutputManifestSha256,
            receipt.OutputSizeBytes,
            sourceUnchanged: true,
            DateTimeOffset.UtcNow,
            receipt.Key.ExecutionId);

        Assert.IsTrue(registry.TryGetPublishedGgufFile(
            result,
            out string? path,
            out string? sha256,
            out ulong length));
        CollectionAssert.AreEqual(expected, File.ReadAllBytes(path!));
        Assert.AreEqual(
            Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant(),
            sha256);
        Assert.AreEqual((ulong)expected.Length, length);
    }

    [TestMethod]
    public async Task SubstitutedExecutionWithIdenticalClaimsIsRejected()
    {
        using var fixture = new RegistryFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot,
            fixture.CommittedRoot);
        using OptimizationOutputLease lease = registry.CreateLease(plan, 3);
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync("execution-bound"u8.ToArray());
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
            DateTimeOffset.UtcNow,
            executionId);
        OptimizationExecutionResult substituted = OptimizationExecutionResult.Succeeded(
            plan,
            receipt.Key.OutputIdentity,
            receipt.Key.OutputManifestSha256,
            receipt.OutputSizeBytes,
            sourceUnchanged: true,
            DateTimeOffset.UtcNow,
            Guid.NewGuid());

        Assert.IsTrue(registry.TryGetPublishedGgufFile(
            exact, out _, out _, out _));
        Assert.IsFalse(registry.TryGetPublishedGgufFile(
            substituted, out _, out _, out _));
    }

    [TestMethod]
    public async Task ChangedPublicationIsRejectedAtConsumption()
    {
        using var fixture = new RegistryFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot,
            fixture.CommittedRoot);
        using OptimizationOutputLease lease = registry.CreateLease(plan, 4);
        byte[] admitted = "admitted-output"u8.ToArray();
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync(admitted);
        SealedOptimizationCandidate candidate = lease.Seal("optimized-model-4");
        OptimizationCommitReceipt receipt = await registry.AdmitAsync(
            plan,
            4,
            fixture.SourceSnapshot(),
            true,
            candidate,
            CancellationToken.None);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan,
            receipt.Key.OutputIdentity,
            receipt.Key.OutputManifestSha256,
            receipt.OutputSizeBytes,
            sourceUnchanged: true,
            DateTimeOffset.UtcNow,
            receipt.Key.ExecutionId);
        Assert.IsTrue(registry.TryGetPublishedGgufFile(
            result, out string? path, out _, out _));
        byte[] changed = "changed--output"u8.ToArray();
        Assert.AreEqual(admitted.Length, changed.Length);
        File.WriteAllBytes(path!, changed);

        Assert.IsFalse(registry.TryGetPublishedGgufFile(
            result, out _, out _, out _));
    }

    [TestMethod]
    public void RestartNeverAdmitsMovedOutputWithoutDurableReceipt()
    {
        using var fixture = new RegistryFixture();
        string orphan = Path.Combine(fixture.CommittedRoot, "publication-orphan123");
        Directory.CreateDirectory(orphan);
        File.WriteAllText(Path.Combine(orphan, "model.gguf"), "unreceipted");

        var restarted = new OptimizationOutputRegistry(fixture.StagingRoot, fixture.CommittedRoot);

        Assert.AreEqual(0, restarted.AdmittedCount);
        Assert.IsFalse(Directory.Exists(orphan));
        Assert.AreEqual(1, Directory.EnumerateDirectories(
            Path.Combine(fixture.CommittedRoot, ".quarantine"),
            "rejected-*", SearchOption.TopDirectoryOnly).Count());
    }

    [TestMethod]
    public void DuplicateLeaseForSameAttemptIsRejectedButDisposeReleasesIt()
    {
        using var fixture = new RegistryFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(fixture.StagingRoot, fixture.CommittedRoot);
        using OptimizationOutputLease first = registry.CreateLease(plan, 9);
        Assert.ThrowsExactly<InvalidOperationException>(() => registry.CreateLease(plan, 9));
        first.Dispose();
        using OptimizationOutputLease replacement = registry.CreateLease(plan, 9);
        Assert.IsNotNull(replacement);
    }

    [TestMethod]
    public void PendingOutputPathIsOwnedAndDoesNotPrecreateTheToolOutput()
    {
        using var fixture = new RegistryFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(fixture.StagingRoot, fixture.CommittedRoot);
        using OptimizationOutputLease lease = registry.CreateLease(plan, 10);

        string pending = lease.CreatePendingFilePath("model.gguf");

        Assert.IsFalse(File.Exists(pending));
        Assert.IsTrue(Path.GetFullPath(pending).StartsWith(
            Path.GetFullPath(fixture.StagingRoot) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));
        Assert.ThrowsExactly<ArgumentException>(() =>
            lease.CreatePendingFilePath("..\\escaped.gguf"));
    }

    [TestMethod]
    public async Task IdentityBoundSealAcceptsOnlyTheValidatedFileBytes()
    {
        using var fixture = new RegistryFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot,
            fixture.CommittedRoot);
        using OptimizationOutputLease lease = registry.CreateLease(plan, 11);
        byte[] expected = "validated-output"u8.ToArray();
        await using (FileStream output = lease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync(expected);
        string sha256 = Convert.ToHexString(SHA256.HashData(expected))
            .ToLowerInvariant();

        SealedOptimizationCandidate candidate = lease.Seal(
            "validated-output-identity",
            sha256,
            checked((ulong)expected.Length));

        Assert.AreEqual((ulong)expected.Length, candidate.OutputSizeBytes);
    }

    [TestMethod]
    public async Task IdentityBoundSealRejectsHashOrLengthMismatch()
    {
        using var fixture = new RegistryFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.StagingRoot,
            fixture.CommittedRoot);
        using OptimizationOutputLease hashLease = registry.CreateLease(plan, 12);
        byte[] expected = "validated-output"u8.ToArray();
        await using (FileStream output = hashLease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync(expected);

        Assert.ThrowsExactly<InvalidDataException>(() => hashLease.Seal(
            "validated-hash-mismatch",
            new string('0', 64),
            checked((ulong)expected.Length)));

        using OptimizationOutputLease lengthLease = registry.CreateLease(plan, 13);
        await using (FileStream output = lengthLease.CreateFileForWrite("model.gguf"))
            await output.WriteAsync(expected);
        string sha256 = Convert.ToHexString(SHA256.HashData(expected))
            .ToLowerInvariant();
        Assert.ThrowsExactly<InvalidDataException>(() => lengthLease.Seal(
            "validated-length-mismatch",
            sha256,
            checked((ulong)expected.Length + 1)));
    }

    private sealed class RegistryFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "geai-output-test-" + Guid.NewGuid().ToString("N"));
        private readonly byte[] _source = "inspected model source"u8.ToArray();

        internal RegistryFixture()
        {
            Directory.CreateDirectory(StagingRoot);
            Directory.CreateDirectory(CommittedRoot);
            File.WriteAllBytes(SourcePath, _source);
        }

        internal string StagingRoot => Path.Combine(_root, "staging");
        internal string CommittedRoot => Path.Combine(_root, "committed");
        private string SourcePath => Path.Combine(_root, "source.gguf");
        private string Digest => Convert.ToHexString(SHA256.HashData(_source)).ToLowerInvariant();

        internal OptimizationExecutionPlan Plan() =>
            OptimizationSelectionHandoffTests.PersistentPlanForSource(Digest, (ulong)_source.Length);

        internal StagedSourceSnapshot SourceSnapshot() => new(
            Digest,
            (ulong)_source.Length,
            "sealed-source-test",
            SourcePath);

        public void Dispose()
        {
            if (!Directory.Exists(_root)) return;
            foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_root, recursive: true);
        }
    }
}
