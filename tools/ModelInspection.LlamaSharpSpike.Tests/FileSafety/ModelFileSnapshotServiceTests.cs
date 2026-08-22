using System.Text;
using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies read-only model identity capture, hashing and cancellation.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class ModelFileSnapshotServiceTests
{
    [TestMethod]
    public async Task CaptureAsync_RecordsExpectedFileIdentity()
    {
        using var directory = new TemporaryDirectory("known-file-identity");
        string modelPath = await TestFileBuilder.WriteBytesAsync(
            directory,
            "granite.gguf",
            Encoding.UTF8.GetBytes("granite"));

        ModelFileSnapshot snapshot =
            await new ModelFileSnapshotService().CaptureAsync(
                modelPath,
                CancellationToken.None);

        Assert.AreEqual("granite.gguf", snapshot.FileName);
        Assert.AreEqual(7L, snapshot.LengthBytes);
        Assert.AreEqual(
            "ac7daf28fd6bfc7a5c3e4b83c7fc9fd51f92ddff10bdc848f99417eca6fafc7c",
            snapshot.Sha256);
        Assert.AreEqual(64, snapshot.CanonicalPathSha256.Length);
    }

    [TestMethod]
    public async Task CaptureAsync_WithEmptyFile_RecordsKnownEmptySha256()
    {
        using var directory = new TemporaryDirectory("empty-file");
        string modelPath = await TestFileBuilder.WriteBytesAsync(
            directory,
            "empty.gguf",
            Array.Empty<byte>());

        ModelFileSnapshot snapshot =
            await new ModelFileSnapshotService().CaptureAsync(
                modelPath,
                CancellationToken.None);

        Assert.AreEqual(0L, snapshot.LengthBytes);
        Assert.AreEqual(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            snapshot.Sha256);
    }

    [TestMethod]
    public async Task CaptureAsync_WithReadOnlyFile_SucceedsWithoutChangingFile()
    {
        using var directory = new TemporaryDirectory("read-only-file");
        string modelPath = await TestFileBuilder.WriteTextAsync(
            directory,
            "read-only.gguf",
            "read-only-model");

        FileAttributes originalAttributes = File.GetAttributes(modelPath);
        File.SetAttributes(
            modelPath,
            originalAttributes | FileAttributes.ReadOnly);

        try
        {
            ModelFileSnapshot snapshot =
                await new ModelFileSnapshotService().CaptureAsync(
                    modelPath,
                    CancellationToken.None);

            Assert.AreEqual(
                new FileInfo(modelPath).Length,
                snapshot.LengthBytes);
            Assert.IsTrue(
                File.GetAttributes(modelPath).HasFlag(
                    FileAttributes.ReadOnly));
        }
        finally
        {
            File.SetAttributes(modelPath, originalAttributes);
        }
    }

    [TestMethod]
    public async Task CaptureAsync_WithMissingFile_ThrowsFileNotFoundException()
    {
        using var directory = new TemporaryDirectory("missing-file");
        string modelPath = directory.Combine("missing.gguf");

        await Assert.ThrowsExactlyAsync<FileNotFoundException>(
            async () => await new ModelFileSnapshotService().CaptureAsync(
                modelPath,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task CaptureAsync_WithDirectoryPath_ThrowsFileNotFoundException()
    {
        using var directory = new TemporaryDirectory("directory-input");

        await Assert.ThrowsExactlyAsync<FileNotFoundException>(
            async () => await new ModelFileSnapshotService().CaptureAsync(
                directory.Path,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task CaptureAsync_WithPreCancelledToken_ThrowsOperationCanceledException()
    {
        using var directory = new TemporaryDirectory("pre-cancelled");
        string modelPath = await TestFileBuilder.WriteTextAsync(
            directory,
            "model.gguf",
            "model-bytes");
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await new ModelFileSnapshotService().CaptureAsync(
                modelPath,
                cancellationSource.Token));
    }

    [TestMethod]
    public async Task CaptureAsync_WhenCancelledDuringHash_ThrowsOperationCanceledException()
    {
        using var directory = new TemporaryDirectory("cancel-during-hash");
        string modelPath = await TestFileBuilder.WriteTextAsync(
            directory,
            "model.gguf",
            "model-bytes");

        var hasher = new BlockingModelFileHasher();
        var service = new ModelFileSnapshotService(hasher);
        using var cancellationSource = new CancellationTokenSource();

        Task<ModelFileSnapshot> captureTask = service.CaptureAsync(
            modelPath,
            cancellationSource.Token);

        await hasher.Started;
        cancellationSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await captureTask);
    }

    [TestMethod]
    public async Task CaptureAsync_ForSameCanonicalPath_ReturnsStablePathFingerprint()
    {
        using var directory = new TemporaryDirectory("stable-path-hash");
        string modelPath = await TestFileBuilder.WriteTextAsync(
            directory,
            "model.gguf",
            "model-bytes");
        var service = new ModelFileSnapshotService();

        ModelFileSnapshot first = await service.CaptureAsync(
            modelPath,
            CancellationToken.None);
        ModelFileSnapshot second = await service.CaptureAsync(
            Path.GetFullPath(modelPath),
            CancellationToken.None);

        Assert.AreEqual(
            first.CanonicalPathSha256,
            second.CanonicalPathSha256);
    }

    [TestMethod]
    public async Task CaptureAsync_OnWindowsCaseVariant_ReturnsSamePathFingerprint()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory("case-path-hash");
        string modelPath = await TestFileBuilder.WriteTextAsync(
            directory,
            "Granite.gguf",
            "model-bytes");
        var service = new ModelFileSnapshotService();

        ModelFileSnapshot original = await service.CaptureAsync(
            modelPath,
            CancellationToken.None);
        ModelFileSnapshot upperCase = await service.CaptureAsync(
            modelPath.ToUpperInvariant(),
            CancellationToken.None);

        Assert.AreEqual(
            original.CanonicalPathSha256,
            upperCase.CanonicalPathSha256);
    }

    [TestMethod]
    public void Constructor_WithNullHasher_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new ModelFileSnapshotService(null!));
    }
}
