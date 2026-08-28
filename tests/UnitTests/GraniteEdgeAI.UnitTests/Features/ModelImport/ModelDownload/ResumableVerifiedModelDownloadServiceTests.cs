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

    private sealed class TestDownloadFixture : IDisposable
    {
        private readonly string _root;

        private TestDownloadFixture(
            string root,
            ModelDownloadCatalogEntry entry,
            AppModelLibrary library,
            FakeTransport transport,
            TimeSpan? inactivityTimeout)
        {
            _root = root;
            Entry = entry;
            Library = library;
            Transport = transport;
            Service = new ResumableVerifiedModelDownloadService(transport, library, inactivityTimeout);
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
            TimeSpan? inactivityTimeout = null)
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
            var library = new AppModelLibrary(root, _ => availableBytes);
            var transport = new FakeTransport(
                responseBytes ?? expectedBytes,
                statusCode,
                contentRange,
                transportException,
                customStream);
            return new TestDownloadFixture(root, entry, library, transport, inactivityTimeout);
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
        Stream? customStream) : IModelDownloadTransport
    {
        internal List<(long Offset, string? EntityTag)> Requests { get; } = [];

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
            response.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
            return Task.FromResult(
                new ModelDownloadTransportResponse(
                    response,
                    customStream ?? response.Content.ReadAsStream(),
                    entry.ResolveUri));
        }
    }

    private sealed class CancellationOnlyStream : Stream
    {
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
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
