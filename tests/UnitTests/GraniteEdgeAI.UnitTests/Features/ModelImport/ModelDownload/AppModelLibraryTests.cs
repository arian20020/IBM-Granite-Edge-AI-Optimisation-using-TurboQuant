using GraniteEdgeAI.Features.ModelImport.ModelDownload;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class AppModelLibraryTests
{
    [TestMethod]
    public async Task RecoverAsync_TruncatesBytesBeyondDurableCheckpoint()
    {
        using var root = new TemporaryModelLibraryRoot();
        var library = new AppModelLibrary(root.Path, _ => long.MaxValue);
        ModelDownloadCatalogEntry entry = PinnedGraniteModelCatalog.ForSliderValue(50);
        await using (Stream partial = await library.OpenPartialWriteAsync(
            entry,
            truncateToLength: 0,
            CancellationToken.None))
        {
            await partial.WriteAsync(new byte[12]);
        }

        await library.WriteCheckpointAsync(
            entry,
            CreateState(entry, durableByteLength: 8),
            CancellationToken.None);

        ModelDownloadResumeInfo? resume = await library.RecoverAsync(entry, CancellationToken.None);

        Assert.IsNotNull(resume);
        Assert.AreEqual(8, resume.DownloadedBytes);
        Assert.AreEqual(8, await library.GetPartialLengthAsync(entry, CancellationToken.None));
    }

    [TestMethod]
    public async Task RecoverAsync_DiscardsCheckpointWhenPartialIsShorter()
    {
        using var root = new TemporaryModelLibraryRoot();
        var library = new AppModelLibrary(root.Path, _ => long.MaxValue);
        ModelDownloadCatalogEntry entry = PinnedGraniteModelCatalog.ForSliderValue(50);
        await using (Stream partial = await library.OpenPartialWriteAsync(
            entry,
            truncateToLength: 0,
            CancellationToken.None))
        {
            await partial.WriteAsync(new byte[4]);
        }

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => library.WriteCheckpointAsync(
                entry,
                CreateState(entry, durableByteLength: 8),
                CancellationToken.None));
    }

    [TestMethod]
    public async Task AcquireLeaseAsync_RefusesSecondWriterForSameArtifact()
    {
        using var root = new TemporaryModelLibraryRoot();
        var library = new AppModelLibrary(root.Path, _ => long.MaxValue);
        ModelDownloadCatalogEntry entry = PinnedGraniteModelCatalog.ForSliderValue(50);
        await using ModelDownloadLibraryLease first =
            await library.AcquireLeaseAsync(entry, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<IOException>(
            async () =>
            {
                await using ModelDownloadLibraryLease second =
                    await library.AcquireLeaseAsync(entry, CancellationToken.None);
            });
    }

    [TestMethod]
    public async Task HasSufficientSpaceAsync_UsesOnlyRemainingArtifactBytes()
    {
        using var root = new TemporaryModelLibraryRoot();
        long reportedFreeSpace = 5;
        var library = new AppModelLibrary(root.Path, _ => reportedFreeSpace);
        ModelDownloadCatalogEntry entry = CreateSmallEntry(expectedBytes: 10);
        await using (Stream partial = await library.OpenPartialWriteAsync(
            entry,
            truncateToLength: 0,
            CancellationToken.None))
        {
            await partial.WriteAsync(new byte[6]);
        }

        Assert.IsTrue(await library.HasSufficientSpaceAsync(entry, CancellationToken.None));
        reportedFreeSpace = 3;
        Assert.IsFalse(await library.HasSufficientSpaceAsync(entry, CancellationToken.None));
    }

    [TestMethod]
    public async Task PublishAsync_AtomicallyMovesOnlyAnExactLengthPartial()
    {
        using var root = new TemporaryModelLibraryRoot();
        var library = new AppModelLibrary(root.Path, _ => long.MaxValue);
        ModelDownloadCatalogEntry entry = CreateSmallEntry(expectedBytes: 4);
        await using (Stream partial = await library.OpenPartialWriteAsync(
            entry,
            truncateToLength: 0,
            CancellationToken.None))
        {
            await partial.WriteAsync(new byte[] { 1, 2, 3, 4 });
        }

        VerifiedDownloadedModel verified =
            await library.PublishAsync(entry, CancellationToken.None);

        Assert.AreEqual(4, verified.ByteLength);
        Assert.AreEqual(entry.FileName, verified.DisplayName);
        Assert.AreEqual(entry.Id, verified.CatalogId);
        Assert.IsTrue(File.Exists(verified.LocalPath));
        Assert.AreEqual(0, await library.GetPartialLengthAsync(entry, CancellationToken.None));
        StringAssert.DoesNotContain(verified.ToString(), root.Path, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task PublishAsync_RejectsShortPartialAndCreatesNoFinalFile()
    {
        using var root = new TemporaryModelLibraryRoot();
        var library = new AppModelLibrary(root.Path, _ => long.MaxValue);
        ModelDownloadCatalogEntry entry = CreateSmallEntry(expectedBytes: 4);
        await using (Stream partial = await library.OpenPartialWriteAsync(
            entry,
            truncateToLength: 0,
            CancellationToken.None))
        {
            await partial.WriteAsync(new byte[] { 1, 2, 3 });
        }

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => library.PublishAsync(entry, CancellationToken.None));
        Assert.IsFalse(await library.FinalExistsAsync(entry, CancellationToken.None));
    }

    [TestMethod]
    public async Task PublishAsync_AtomicCommitWinsOverCancellationSignalledAfterMove()
    {
        using var root = new TemporaryModelLibraryRoot();
        using var cancellation = new CancellationTokenSource();
        var library = new AppModelLibrary(
            root.Path,
            _ => long.MaxValue,
            publisher: (source, destination, _) =>
            {
                File.Move(source, destination, overwrite: false);
                cancellation.Cancel();
                return ValueTask.CompletedTask;
            });
        ModelDownloadCatalogEntry entry = CreateSmallEntry(expectedBytes: 4);
        await using (Stream partial = await library.OpenPartialWriteAsync(entry, 0, CancellationToken.None))
            await partial.WriteAsync(new byte[] { 1, 2, 3, 4 });

        VerifiedDownloadedModel verified = await library.PublishAsync(entry, cancellation.Token);

        Assert.IsTrue(cancellation.IsCancellationRequested);
        Assert.IsTrue(await library.FinalExistsAsync(entry, CancellationToken.None));
        Assert.AreEqual(entry.FileName, verified.DisplayName);
    }

    [TestMethod]
    [DataRow((int)ModelDownloadCancellationBoundary.CheckpointWrite)]
    [DataRow((int)ModelDownloadCancellationBoundary.CheckpointReplace)]
    [DataRow((int)ModelDownloadCancellationBoundary.IntegrityHash)]
    [DataRow((int)ModelDownloadCancellationBoundary.FinalPublish)]
    public async Task OwnedStorageOperationObservesCancellationWithHandleAndPublishesNoFinal(int boundaryValue)
    {
        ModelDownloadCancellationBoundary boundary = (ModelDownloadCancellationBoundary)boundaryValue;
        using var root = new TemporaryModelLibraryRoot();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async ValueTask Gate(CancellationToken token)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
        var library = new AppModelLibrary(
            root.Path,
            _ => long.MaxValue,
            checkpointWriter: boundary == ModelDownloadCancellationBoundary.CheckpointWrite
                ? async (_, _, token) => await Gate(token) : null,
            checkpointReplacer: boundary == ModelDownloadCancellationBoundary.CheckpointReplace
                ? async (_, _, token) => await Gate(token) : null,
            hasher: boundary == ModelDownloadCancellationBoundary.IntegrityHash
                ? async (_, token) => { await Gate(token); return []; } : null,
            publisher: boundary == ModelDownloadCancellationBoundary.FinalPublish
                ? async (_, _, token) => await Gate(token) : null);
        ModelDownloadCatalogEntry entry = CreateSmallEntry(expectedBytes: 4);
        await using (Stream partial = await library.OpenPartialWriteAsync(entry, 0, CancellationToken.None))
        {
            await partial.WriteAsync(new byte[] { 1, 2, 3, 4 });
        }

        Task operation = boundary switch
        {
            ModelDownloadCancellationBoundary.CheckpointWrite => library.WriteCheckpointAsync(entry, CreateState(entry, 4), cancellation.Token),
            ModelDownloadCancellationBoundary.CheckpointReplace => library.WriteCheckpointAsync(entry, CreateState(entry, 4), cancellation.Token),
            ModelDownloadCancellationBoundary.IntegrityHash => library.ComputePartialSha256Async(entry, cancellation.Token),
            ModelDownloadCancellationBoundary.FinalPublish => library.PublishAsync(entry, cancellation.Token),
            _ => throw new InvalidOperationException()
        };
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.IsFalse(await library.FinalExistsAsync(entry, CancellationToken.None));
        await library.DiscardPartialAsync(entry, CancellationToken.None);
        Assert.AreEqual(0, await library.GetPartialLengthAsync(entry, CancellationToken.None));
    }

    [TestMethod]
    public async Task DiscardPartialAsync_RemovesOnlyCatalogueDerivedPartialState()
    {
        using var root = new TemporaryModelLibraryRoot();
        var library = new AppModelLibrary(root.Path, _ => long.MaxValue);
        ModelDownloadCatalogEntry entry = CreateSmallEntry(expectedBytes: 4);
        string unrelated = System.IO.Path.Combine(root.Path, "keep.txt");
        await File.WriteAllTextAsync(unrelated, "keep");
        await using (Stream partial = await library.OpenPartialWriteAsync(
            entry,
            truncateToLength: 0,
            CancellationToken.None))
        {
            await partial.WriteAsync(new byte[] { 1, 2 });
        }
        await library.WriteCheckpointAsync(
            entry,
            CreateState(entry, durableByteLength: 2),
            CancellationToken.None);

        await library.DiscardPartialAsync(entry, CancellationToken.None);

        Assert.AreEqual(0, await library.GetPartialLengthAsync(entry, CancellationToken.None));
        Assert.IsNull(await library.RecoverAsync(entry, CancellationToken.None));
        Assert.IsTrue(File.Exists(unrelated));
    }

    [TestMethod]
    public async Task WriteCheckpointAsync_RejectsReparsePointTemporaryArtifact()
    {
        using var root = new TemporaryModelLibraryRoot();
        var library = new AppModelLibrary(root.Path, _ => long.MaxValue);
        ModelDownloadCatalogEntry entry = CreateSmallEntry(expectedBytes: 4);
        await using (Stream partial = await library.OpenPartialWriteAsync(entry, 0, CancellationToken.None))
        {
            await partial.WriteAsync(new byte[] { 1, 2 });
        }

        string stateRoot = System.IO.Path.Combine(root.Path, "State");
        string target = System.IO.Path.Combine(root.Path, "external.txt");
        await File.WriteAllTextAsync(target, "do-not-change");
        string temporary = System.IO.Path.Combine(stateRoot, entry.Id + ".json.new");
        try
        {
            File.CreateSymbolicLink(temporary, target);
        }
        catch (UnauthorizedAccessException)
        {
            Assert.Inconclusive("This Windows host does not permit test symbolic links.");
        }

        await Assert.ThrowsExactlyAsync<IOException>(() => library.WriteCheckpointAsync(
            entry, CreateState(entry, durableByteLength: 2), CancellationToken.None));
        Assert.AreEqual("do-not-change", await File.ReadAllTextAsync(target));
    }

    private static ModelDownloadPartialState CreateState(
        ModelDownloadCatalogEntry entry,
        long durableByteLength) =>
        new(
            1,
            entry.Id,
            entry.Revision,
            entry.FileName,
            entry.ExpectedByteLength,
            entry.ExpectedSha256,
            durableByteLength,
            "\"v1\"",
            DateTimeOffset.UtcNow);

    private static ModelDownloadCatalogEntry CreateSmallEntry(long expectedBytes) =>
        new(
            "test-q4",
            "Balanced",
            "Q4_K_M",
            40,
            60,
            false,
            "ibm-granite/granite-4.0-h-micro-GGUF",
            "51ce07a9c9cfa971ca359d9625836bf8a4a1b61f",
            "test-Q4_K_M.gguf",
            expectedBytes,
            new string('0', 64));

    private sealed class TemporaryModelLibraryRoot : IDisposable
    {
        private const string Prefix = "granite-edge-ai-model-library-test-";

        internal TemporaryModelLibraryRoot()
        {
            Path = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            string fullPath = System.IO.Path.GetFullPath(Path);
            string tempRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
                !System.IO.Path.GetFileName(fullPath).StartsWith(Prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing to remove an unexpected test directory.");
            }

            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
    }
}
