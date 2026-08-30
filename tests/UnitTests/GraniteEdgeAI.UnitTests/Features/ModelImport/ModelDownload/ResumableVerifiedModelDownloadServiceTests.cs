using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ResumableVerifiedModelDownloadServiceTests
{
    [TestMethod]
    public async Task DownloadAsync_CompleteResponsePublishesVerifiedArtifact()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create([1, 2, 3, 4]);

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new Progress<ModelDownloadProgress>(),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Completed, result.Kind);
        Assert.IsNotNull(result.VerifiedModel);
        Assert.AreEqual(fixture.Entry.ExpectedSha256, result.VerifiedModel.Sha256);
        Assert.IsTrue(await fixture.Library.FinalExistsAsync(fixture.Entry, CancellationToken.None));
    }

    [TestMethod]
    public async Task DownloadAsync_WrongDigestNeverPublishes()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            [1, 2, 3, 4],
            expectedSha256: new string('0', 64));

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new Progress<ModelDownloadProgress>(),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Failed, result.Kind);
        Assert.AreEqual("download-integrity-failed", result.ErrorCode);
        Assert.IsFalse(await fixture.Library.FinalExistsAsync(fixture.Entry, CancellationToken.None));
        Assert.AreEqual(0, await fixture.Library.GetPartialLengthAsync(fixture.Entry, CancellationToken.None));
    }

    [TestMethod]
    public async Task DownloadAsync_OversizedResponseFailsClosed()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            expectedBytes: [1, 2, 3, 4],
            responseBytes: [1, 2, 3, 4, 5]);

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new Progress<ModelDownloadProgress>(),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Failed, result.Kind);
        Assert.AreEqual("download-size-invalid", result.ErrorCode);
        Assert.IsFalse(await fixture.Library.FinalExistsAsync(fixture.Entry, CancellationToken.None));
    }

    [TestMethod]
    public async Task DownloadAsync_TruncatedResponsePreservesResumeState()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            expectedBytes: [1, 2, 3, 4],
            responseBytes: [1, 2]);

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new Progress<ModelDownloadProgress>(),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Interrupted, result.Kind);
        ModelDownloadResumeInfo? resume = await fixture.Service.GetResumeInfoAsync(
            fixture.Entry,
            CancellationToken.None);
        Assert.IsNotNull(resume);
        Assert.AreEqual(2, resume.DownloadedBytes);
    }

    [TestMethod]
    public async Task DownloadAsync_ValidPartialResponseAppendsFromExactOffset()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            expectedBytes: [1, 2, 3, 4],
            responseBytes: [3, 4],
            statusCode: HttpStatusCode.PartialContent,
            contentRange: new ContentRangeHeaderValue(2, 3, 4));
        await fixture.SeedPartialAsync([1, 2], "\"v1\"");

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new Progress<ModelDownloadProgress>(),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Completed, result.Kind);
        Assert.AreEqual(2, fixture.Transport.Requests.Single().Offset);
        Assert.AreEqual("\"v1\"", fixture.Transport.Requests.Single().EntityTag);
    }

    [TestMethod]
    public async Task DownloadAsync_FullResponseToRangeRequestSafelyRestartsFromZero()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create([1, 2, 3, 4]);
        await fixture.SeedPartialAsync([9, 9], "\"old\"");

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new Progress<ModelDownloadProgress>(),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Completed, result.Kind);
        Assert.AreEqual(2, fixture.Transport.Requests.Single().Offset);
    }

    [TestMethod]
    public async Task DownloadAsync_InvalidContentRangeFailsWithoutAppending()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            expectedBytes: [1, 2, 3, 4],
            responseBytes: [3, 4],
            statusCode: HttpStatusCode.PartialContent,
            contentRange: new ContentRangeHeaderValue(1, 2, 4));
        await fixture.SeedPartialAsync([1, 2], "\"v1\"");

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new Progress<ModelDownloadProgress>(),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Failed, result.Kind);
        Assert.AreEqual("download-range-invalid", result.ErrorCode);
        Assert.AreEqual(2, await fixture.Library.GetPartialLengthAsync(fixture.Entry, CancellationToken.None));
    }

    [TestMethod]
    public async Task DownloadAsync_ChangedEntityTagDiscardsIncompatiblePartial()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            expectedBytes: [1, 2, 3, 4], responseBytes: [3, 4],
            statusCode: HttpStatusCode.PartialContent,
            contentRange: new ContentRangeHeaderValue(2, 3, 4),
            responseEntityTag: "\"v2\"");
        await fixture.SeedPartialAsync([1, 2], "\"v1\"");

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry, new Progress<ModelDownloadProgress>(), CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Failed, result.Kind);
        Assert.AreEqual("download-identity-changed", result.ErrorCode);
        Assert.AreEqual(0, await fixture.Library.GetPartialLengthAsync(fixture.Entry, CancellationToken.None));
    }

    [TestMethod]
    public async Task DownloadAsync_ReusesOnlyAnExistingArtifactWithMatchingDigest()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create([1, 2, 3, 4]);
        await fixture.SeedPublishedAsync([1, 2, 3, 4]);

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new Progress<ModelDownloadProgress>(),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.AlreadyAvailable, result.Kind);
        Assert.AreEqual(0, fixture.Transport.Requests.Count);
    }

    [TestMethod]
    public async Task DownloadAsync_InsufficientSpaceDoesNotOpenTransport()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            [1, 2, 3, 4],
            availableBytes: 3);

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new Progress<ModelDownloadProgress>(),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Failed, result.Kind);
        Assert.AreEqual("download-storage-insufficient", result.ErrorCode);
        Assert.AreEqual(0, fixture.Transport.Requests.Count);
    }

    [TestMethod]
    public async Task DownloadAsync_UnsafeRedirectFailureBecomesBoundedResult()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            [1, 2, 3, 4],
            transportException: new InvalidDataException("unsafe redirect"));

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new Progress<ModelDownloadProgress>(),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Failed, result.Kind);
        Assert.AreEqual("download-http-rejected", result.ErrorCode);
    }

    [TestMethod]
    public async Task DownloadAsync_StalledBodyReturnsBoundedInterruption()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            [1, 2, 3, 4],
            customStream: new CancellationOnlyStream(),
            inactivityTimeout: TimeSpan.FromMilliseconds(25));

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new Progress<ModelDownloadProgress>(),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Interrupted, result.Kind);
        Assert.AreEqual("download-inactivity-timeout", result.ErrorCode);
    }

    [TestMethod]
    public async Task DownloadAsync_ResponseHeaderTimeoutReturnsBoundedInterruption()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            [1, 2, 3, 4],
            transportException: new TimeoutException("response headers stalled"));

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new Progress<ModelDownloadProgress>(),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Interrupted, result.Kind);
        Assert.AreEqual("download-inactivity-timeout", result.ErrorCode);
    }

    [TestMethod]
    public async Task DownloadAsync_ConnectionCancellationUsesCallerToken()
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create([1, 2, 3, 4]);
        var transport = new CancellationBlockingTransport();
        var service = new ResumableVerifiedModelDownloadService(transport, fixture.Library);
        using var cancellation = new CancellationTokenSource();

        Task<ModelDownloadResult> operation = service.DownloadAsync(
            fixture.Entry,
            new InlineProgress<ModelDownloadProgress>(_ => { }),
            cancellation.Token);
        CancellationToken observed = await transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(cancellation.Token, observed);
        cancellation.Cancel();

        ModelDownloadResult result = await operation.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual("download-cancelled", result.ErrorCode);
        Assert.IsFalse(await fixture.Library.FinalExistsAsync(fixture.Entry, CancellationToken.None));
    }

    [TestMethod]
    public async Task DownloadAsync_ResponseReadCancellationUsesCallerToken()
    {
        var stream = new CancellationOnlyStream();
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            [1, 2, 3, 4],
            customStream: stream,
            inactivityTimeout: TimeSpan.FromMinutes(1));
        using var cancellation = new CancellationTokenSource();

        Task<ModelDownloadResult> operation = fixture.Service.DownloadAsync(
            fixture.Entry,
            new InlineProgress<ModelDownloadProgress>(_ => { }),
            cancellation.Token);
        CancellationToken observed = await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsFalse(observed.IsCancellationRequested);
        cancellation.Cancel();

        ModelDownloadResult result = await operation.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsTrue(observed.IsCancellationRequested);
        Assert.AreEqual("download-cancelled", result.ErrorCode);
        Assert.IsFalse(await fixture.Library.FinalExistsAsync(fixture.Entry, CancellationToken.None));
    }

    [TestMethod]
    public async Task DownloadAsync_CancelAtFirstDurableCheckpointCompletesPromptly()
    {
        byte[] payload = new byte[(8 * 1024 * 1024) + 1];
        RandomNumberGenerator.Fill(payload);
        using TestDownloadFixture fixture = TestDownloadFixture.Create(payload);
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress<ModelDownloadProgress>(value =>
        {
            if (value.Stage == ModelDownloadStage.Downloading &&
                value.DownloadedBytes >= 8L * 1024 * 1024)
            {
                cancellation.Cancel();
            }
        });

        Task<ModelDownloadResult> download = fixture.Service.DownloadAsync(
            fixture.Entry,
            progress,
            cancellation.Token);
        ModelDownloadResult result = await download.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(ModelDownloadResultKind.Interrupted, result.Kind);
        Assert.AreEqual("download-cancelled", result.ErrorCode);
        Assert.IsFalse(await fixture.Library.FinalExistsAsync(fixture.Entry, CancellationToken.None));
    }

    [TestMethod]
    [DataRow((int)ModelDownloadCancellationBoundary.Connection, false)]
    [DataRow((int)ModelDownloadCancellationBoundary.ResponseRead, false)]
    [DataRow((int)ModelDownloadCancellationBoundary.PartialWrite, false)]
    [DataRow((int)ModelDownloadCancellationBoundary.CheckpointFlush, false)]
    [DataRow((int)ModelDownloadCancellationBoundary.CheckpointWrite, false)]
    [DataRow((int)ModelDownloadCancellationBoundary.IntegrityHash, true)]
    [DataRow((int)ModelDownloadCancellationBoundary.FinalPublish, true)]
    public async Task DownloadAsync_CancellationAtOwnedBoundarySettlesWithoutPublishing(
        int boundaryValue,
        bool durableResumeExpected)
    {
        ModelDownloadCancellationBoundary boundary = (ModelDownloadCancellationBoundary)boundaryValue;
        using var cancellation = new CancellationTokenSource();
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            [1, 2, 3, 4],
            boundaryObserver: (observed, observedToken) =>
            {
                if (observed == boundary)
                {
                    Assert.AreEqual(cancellation.Token, observedToken);
                    cancellation.Cancel();
                }
                return ValueTask.CompletedTask;
            });

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new InlineProgress<ModelDownloadProgress>(_ => { }),
            cancellation.Token).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(ModelDownloadResultKind.Interrupted, result.Kind);
        Assert.AreEqual("download-cancelled", result.ErrorCode);
        Assert.IsFalse(await fixture.Library.FinalExistsAsync(fixture.Entry, CancellationToken.None));
        ModelDownloadResumeInfo? resume = await fixture.Service.GetResumeInfoAsync(
            fixture.Entry,
            CancellationToken.None);
        Assert.AreEqual(durableResumeExpected, resume is not null);
        if (resume is not null) Assert.AreEqual(fixture.Entry.ExpectedByteLength, resume.DownloadedBytes);
        await using ModelDownloadLibraryLease reacquired = await fixture.Library.AcquireLeaseAsync(
            fixture.Entry,
            CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task DownloadAsync_CancellationWhilePartialWriteIsPendingUsesCallerToken()
    {
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            [1, 2, 3, 4],
            blockWriter: async (_, _, token) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });

        Task<ModelDownloadResult> operation = fixture.Service.DownloadAsync(
            fixture.Entry,
            new InlineProgress<ModelDownloadProgress>(_ => { }),
            cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        ModelDownloadResult result = await operation.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(ModelDownloadResultKind.Interrupted, result.Kind);
        Assert.AreEqual("download-cancelled", result.ErrorCode);
        Assert.IsFalse(await fixture.Library.FinalExistsAsync(fixture.Entry, CancellationToken.None));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DownloadAsync_LocalPreflightIoFailureIsStorageFailure(bool accessDenied)
    {
        using TestDownloadFixture fixture = TestDownloadFixture.Create(
            [1, 2, 3, 4],
            availableSpaceException: accessDenied
                ? new UnauthorizedAccessException("private local failure")
                : new IOException("private local failure"));

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            new InlineProgress<ModelDownloadProgress>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Failed, result.Kind);
        Assert.AreEqual("download-storage-failed", result.ErrorCode);
        Assert.AreEqual(0, fixture.Transport.OpenCount);
    }

    [TestMethod]
    public async Task DownloadAsync_FastTransferCoalescesProgressNotifications()
    {
        byte[] payload = new byte[4 * 1024 * 1024];
        RandomNumberGenerator.Fill(payload);
        using TestDownloadFixture fixture = TestDownloadFixture.Create(payload);
        var reports = new List<ModelDownloadProgress>();
        var progress = new InlineProgress<ModelDownloadProgress>(reports.Add);

        ModelDownloadResult result = await fixture.Service.DownloadAsync(
            fixture.Entry,
            progress,
            CancellationToken.None);

        Assert.AreEqual(ModelDownloadResultKind.Completed, result.Kind);
        Assert.IsTrue(reports.Count <= 8, $"Expected coalesced UI progress, received {reports.Count} reports.");
        Assert.AreEqual(ModelDownloadStage.Preparing, reports[0].Stage);
        Assert.AreEqual(ModelDownloadStage.Completed, reports[^1].Stage);
        Assert.AreEqual(payload.LongLength, reports[^1].DownloadedBytes);
    }

    private sealed class TestDownloadFixture : IDisposable
    {
        private readonly string _root;

        private TestDownloadFixture(
            string root,
            ModelDownloadCatalogEntry entry,
            AppModelLibrary library,
            FakeTransport transport,
            TimeSpan? inactivityTimeout,
            Func<ModelDownloadCancellationBoundary, CancellationToken, ValueTask>? boundaryObserver = null,
            Func<Stream, ReadOnlyMemory<byte>, CancellationToken, ValueTask>? blockWriter = null)
        {
            _root = root;
            Entry = entry;
            Library = library;
            Transport = transport;
            Service = new ResumableVerifiedModelDownloadService(
                transport,
                library,
                inactivityTimeout,
                boundaryObserver,
                blockWriter);
        }

        internal ModelDownloadCatalogEntry Entry { get; }
        internal AppModelLibrary Library { get; }
        internal FakeTransport Transport { get; }
        internal ResumableVerifiedModelDownloadService Service { get; }

        internal static TestDownloadFixture Create(
            byte[] expectedBytes,
            byte[]? responseBytes = null,
            string? expectedSha256 = null,
            HttpStatusCode statusCode = HttpStatusCode.OK,
            ContentRangeHeaderValue? contentRange = null,
            long availableBytes = long.MaxValue,
            Exception? transportException = null,
            Stream? customStream = null,
            TimeSpan? inactivityTimeout = null,
            string responseEntityTag = "\"v1\"",
            Func<ModelDownloadCancellationBoundary, CancellationToken, ValueTask>? boundaryObserver = null,
            Func<Stream, ReadOnlyMemory<byte>, CancellationToken, ValueTask>? blockWriter = null,
            Exception? availableSpaceException = null)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "granite-edge-ai-download-service-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string sha = expectedSha256 ?? Convert.ToHexString(SHA256.HashData(expectedBytes)).ToLowerInvariant();
            var entry = new ModelDownloadCatalogEntry(
                "test-artifact",
                "Balanced",
                "Q4_K_M",
                40,
                60,
                false,
                "ibm-granite/granite-4.0-h-micro-GGUF",
                "51ce07a9c9cfa971ca359d9625836bf8a4a1b61f",
                "test-artifact.gguf",
                expectedBytes.LongLength,
                sha);
            var library = new AppModelLibrary(root, _ => availableSpaceException is null
                ? availableBytes
                : throw availableSpaceException);
            var transport = new FakeTransport(
                responseBytes ?? expectedBytes,
                statusCode,
                contentRange,
                transportException,
                customStream,
                responseEntityTag);
            return new TestDownloadFixture(
                root,
                entry,
                library,
                transport,
                inactivityTimeout,
                boundaryObserver,
                blockWriter);
        }

        internal async Task SeedPartialAsync(byte[] bytes, string entityTag)
        {
            await using (Stream partial = await Library.OpenPartialWriteAsync(
                Entry,
                0,
                CancellationToken.None))
            {
                await partial.WriteAsync(bytes);
            }

            await Library.WriteCheckpointAsync(
                Entry,
                new ModelDownloadPartialState(
                    1,
                    Entry.Id,
                    Entry.Revision,
                    Entry.FileName,
                    Entry.ExpectedByteLength,
                    Entry.ExpectedSha256,
                    bytes.LongLength,
                    entityTag,
                    DateTimeOffset.UtcNow),
                CancellationToken.None);
        }

        internal async Task SeedPublishedAsync(byte[] bytes)
        {
            await using (Stream partial = await Library.OpenPartialWriteAsync(
                Entry,
                0,
                CancellationToken.None))
            {
                await partial.WriteAsync(bytes);
            }

            await Library.PublishAsync(Entry, CancellationToken.None);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }

    private sealed class FakeTransport(
        byte[] bytes,
        HttpStatusCode statusCode,
        ContentRangeHeaderValue? contentRange,
        Exception? openException,
        Stream? customStream,
        string responseEntityTag) : IModelDownloadTransport
    {
        internal List<(long Offset, string? EntityTag)> Requests { get; } = [];
        internal int OpenCount => Requests.Count;

        public Task<ModelDownloadTransportResponse> OpenAsync(
            ModelDownloadCatalogEntry entry,
            long offset,
            string? entityTag,
            CancellationToken cancellationToken)
        {
            Requests.Add((offset, entityTag));
            if (openException is not null)
            {
                return Task.FromException<ModelDownloadTransportResponse>(openException);
            }
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new ByteArrayContent(bytes)
            };
            response.Content.Headers.ContentLength = bytes.LongLength;
            response.Content.Headers.ContentRange = contentRange;
            response.Headers.ETag = new EntityTagHeaderValue(responseEntityTag);
            return Task.FromResult(
                new ModelDownloadTransportResponse(
                    response,
                    customStream ?? response.Content.ReadAsStream(),
                    entry.ResolveUri));
        }
    }

    private sealed class CancellationOnlyStream : Stream
    {
        internal TaskCompletionSource<CancellationToken> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(cancellationToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }

    private sealed class CancellationBlockingTransport : IModelDownloadTransport
    {
        internal TaskCompletionSource<CancellationToken> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ModelDownloadTransportResponse> OpenAsync(
            ModelDownloadCatalogEntry entry,
            long offset,
            string? entityTag,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult(cancellationToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Cancellation must end the connection wait.");
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
