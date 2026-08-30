using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal enum ModelDownloadCancellationBoundary
{
    Connection,
    StoragePreflight,
    ResponseRead,
    PartialWrite,
    CheckpointFlush,
    CheckpointWrite,
    CheckpointReplace,
    IntegrityHash,
    FinalPublish
}

internal sealed class ResumableVerifiedModelDownloadService : IModelDownloadService
{
    private const int BufferSize = 128 * 1024;
    private const long CheckpointIntervalBytes = 8L * 1024 * 1024;
    private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromMilliseconds(100);

    private readonly IModelDownloadTransport _transport;
    private readonly AppModelLibrary _library;
    private readonly TimeSpan _inactivityTimeout;
    private readonly Func<ModelDownloadCancellationBoundary, CancellationToken, ValueTask>? _boundaryObserver;
    private readonly Func<Stream, ReadOnlyMemory<byte>, CancellationToken, ValueTask> _blockWriter;

    internal ResumableVerifiedModelDownloadService(
        IModelDownloadTransport transport,
        AppModelLibrary library,
        TimeSpan? inactivityTimeout = null,
        Func<ModelDownloadCancellationBoundary, CancellationToken, ValueTask>? boundaryObserver = null,
        Func<Stream, ReadOnlyMemory<byte>, CancellationToken, ValueTask>? blockWriter = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _inactivityTimeout = inactivityTimeout ?? TimeSpan.FromSeconds(30);
        _boundaryObserver = boundaryObserver;
        _blockWriter = blockWriter ?? ((stream, bytes, token) => stream.WriteAsync(bytes, token));
        if (_inactivityTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(inactivityTimeout));
        }
    }

    public async Task<ModelDownloadResult> DownloadAsync(
        ModelDownloadCatalogEntry entry,
        IProgress<ModelDownloadProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(progress);
        progress.Report(new ModelDownloadProgress(ModelDownloadStage.Preparing, 0, entry.ExpectedByteLength));

        ModelDownloadLibraryLease lease;
        try
        {
            lease = await _library.AcquireLeaseAsync(entry, cancellationToken);
        }
        catch (IOException)
        {
            return Failed("download-busy");
        }
        catch (OperationCanceledException)
        {
            return Interrupted("download-cancelled");
        }

        await using (lease)
        {
            ModelDownloadCancellationBoundary phase = ModelDownloadCancellationBoundary.StoragePreflight;
            try
            {
                VerifiedDownloadedModel? existing =
                    await _library.GetVerifiedExistingAsync(entry, cancellationToken);
                if (existing is not null)
                {
                    progress.Report(new ModelDownloadProgress(
                        ModelDownloadStage.Completed,
                        entry.ExpectedByteLength,
                        entry.ExpectedByteLength));
                    return new ModelDownloadResult(
                        ModelDownloadResultKind.AlreadyAvailable,
                        existing,
                        null);
                }

                if (!await _library.HasSufficientSpaceAsync(entry, cancellationToken))
                {
                    return Failed("download-storage-insufficient");
                }

                ModelDownloadResumeInfo? resume =
                    await _library.RecoverAsync(entry, cancellationToken);
                long requestedOffset = resume?.DownloadedBytes ?? 0;

                await ObserveBoundaryAsync(ModelDownloadCancellationBoundary.Connection, cancellationToken);
                phase = ModelDownloadCancellationBoundary.Connection;
                await using ModelDownloadTransportResponse response = await _transport.OpenAsync(
                    entry,
                    requestedOffset,
                    resume?.EntityTag,
                    cancellationToken);

                long writeOffset = requestedOffset;
                if (requestedOffset == 0)
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return Failed("download-http-rejected");
                    }
                }
                else if (response.StatusCode == HttpStatusCode.PartialContent)
                {
                    if (!IsValidContentRange(response, requestedOffset, entry.ExpectedByteLength))
                    {
                        return Failed("download-range-invalid");
                    }
                    if (!string.IsNullOrWhiteSpace(resume?.EntityTag)
                        && !string.Equals(resume.EntityTag, response.EntityTag, StringComparison.Ordinal))
                    {
                        await _library.DiscardPartialAsync(entry, CancellationToken.None);
                        return Failed("download-identity-changed");
                    }
                }
                else if (response.StatusCode == HttpStatusCode.OK)
                {
                    writeOffset = 0;
                }
                else
                {
                    return Failed("download-http-rejected");
                }

                if (response.ContentLength is long contentLength &&
                    (contentLength < 0 || writeOffset + contentLength > entry.ExpectedByteLength))
                {
                    await _library.DiscardPartialAsync(entry, CancellationToken.None);
                    return Failed("download-size-invalid");
                }

                string? entityTag = response.EntityTag ?? resume?.EntityTag;
                long downloaded = writeOffset;
                long lastCheckpoint = writeOffset;
                phase = ModelDownloadCancellationBoundary.PartialWrite;
                await using Stream destination = await _library.OpenPartialWriteAsync(
                    entry,
                    writeOffset,
                    cancellationToken);
                if (downloaded > 0)
                {
                    phase = ModelDownloadCancellationBoundary.CheckpointFlush;
                    await WriteCheckpointAsync(entry, destination, downloaded, entityTag, cancellationToken);
                }

                progress.Report(new ModelDownloadProgress(
                    ModelDownloadStage.Downloading,
                    downloaded,
                    entry.ExpectedByteLength));
                long lastProgressReportTimestamp = Stopwatch.GetTimestamp();

                byte[] buffer = new byte[BufferSize];
                while (true)
                {
                    phase = ModelDownloadCancellationBoundary.ResponseRead;
                    await ObserveBoundaryAsync(ModelDownloadCancellationBoundary.ResponseRead, cancellationToken);
                    int read = await ReadWithInactivityTimeoutAsync(
                        response.Content,
                        buffer,
                        cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    if (downloaded + read > entry.ExpectedByteLength)
                    {
                        await _library.DiscardPartialAsync(entry, CancellationToken.None);
                        return Failed("download-size-invalid");
                    }

                    phase = ModelDownloadCancellationBoundary.PartialWrite;
                    await ObserveBoundaryAsync(ModelDownloadCancellationBoundary.PartialWrite, cancellationToken);
                    await _blockWriter(destination, buffer.AsMemory(0, read), cancellationToken);
                    downloaded += read;
                    bool reachedCheckpoint = downloaded - lastCheckpoint >= CheckpointIntervalBytes;
                    if (reachedCheckpoint ||
                        Stopwatch.GetElapsedTime(lastProgressReportTimestamp) >= ProgressReportInterval)
                    {
                        progress.Report(new ModelDownloadProgress(
                            ModelDownloadStage.Downloading,
                            downloaded,
                            entry.ExpectedByteLength));
                        lastProgressReportTimestamp = Stopwatch.GetTimestamp();
                    }

                    if (reachedCheckpoint)
                    {
                        phase = ModelDownloadCancellationBoundary.CheckpointFlush;
                        await WriteCheckpointAsync(
                            entry,
                            destination,
                            downloaded,
                            entityTag,
                            cancellationToken);
                        lastCheckpoint = downloaded;
                    }
                }

                phase = ModelDownloadCancellationBoundary.CheckpointFlush;
                await WriteCheckpointAsync(
                    entry,
                    destination,
                    downloaded,
                    entityTag,
                    cancellationToken);

                if (downloaded != entry.ExpectedByteLength)
                {
                    return Interrupted("download-interrupted");
                }

                await destination.DisposeAsync();
                progress.Report(new ModelDownloadProgress(
                    ModelDownloadStage.Verifying,
                    downloaded,
                    entry.ExpectedByteLength));
                phase = ModelDownloadCancellationBoundary.IntegrityHash;
                await ObserveBoundaryAsync(ModelDownloadCancellationBoundary.IntegrityHash, cancellationToken);
                byte[] actual = await _library.ComputePartialSha256Async(entry, cancellationToken);
                byte[] expected = Convert.FromHexString(entry.ExpectedSha256);
                if (!CryptographicOperations.FixedTimeEquals(actual, expected))
                {
                    await _library.DiscardPartialAsync(entry, CancellationToken.None);
                    return Failed("download-integrity-failed");
                }

                phase = ModelDownloadCancellationBoundary.FinalPublish;
                await ObserveBoundaryAsync(ModelDownloadCancellationBoundary.FinalPublish, cancellationToken);
                VerifiedDownloadedModel verified =
                    await _library.PublishAsync(entry, cancellationToken);
                progress.Report(new ModelDownloadProgress(
                    ModelDownloadStage.Completed,
                    downloaded,
                    entry.ExpectedByteLength));
                return new ModelDownloadResult(ModelDownloadResultKind.Completed, verified, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Interrupted("download-cancelled");
            }
            catch (OperationCanceledException)
            {
                return Interrupted("download-inactivity-timeout");
            }
            catch (TimeoutException)
            {
                return Interrupted("download-inactivity-timeout");
            }
            catch (InvalidDataException)
            {
                return Failed("download-http-rejected");
            }
            catch (HttpRequestException)
            {
                return Interrupted("download-interrupted");
            }
            catch (IOException) when (phase is ModelDownloadCancellationBoundary.Connection or ModelDownloadCancellationBoundary.ResponseRead)
            {
                return Interrupted("download-interrupted");
            }
            catch (IOException)
            {
                return Failed("download-storage-failed");
            }
        }
    }

    private async Task<int> ReadWithInactivityTimeoutAsync(
        Stream source,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(_inactivityTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        return await source.ReadAsync(buffer, linked.Token);
    }

    public Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken) =>
        _library.RecoverAsync(entry, cancellationToken);

    public Task DiscardPartialAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken) =>
        _library.DiscardPartialAsync(entry, cancellationToken);

    private async Task WriteCheckpointAsync(
        ModelDownloadCatalogEntry entry,
        Stream destination,
        long downloaded,
        string? entityTag,
        CancellationToken cancellationToken)
    {
        // From this point through checkpoint replacement, an I/O failure is storage-owned.
        await ObserveBoundaryAsync(ModelDownloadCancellationBoundary.CheckpointFlush, cancellationToken);
        await destination.FlushAsync(cancellationToken);

        await ObserveBoundaryAsync(ModelDownloadCancellationBoundary.CheckpointWrite, cancellationToken);
        await _library.WriteCheckpointAsync(
            entry,
            new ModelDownloadPartialState(
                1,
                entry.Id,
                entry.Revision,
                entry.FileName,
                entry.ExpectedByteLength,
                entry.ExpectedSha256,
                downloaded,
                entityTag,
                DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private async ValueTask ObserveBoundaryAsync(
        ModelDownloadCancellationBoundary boundary,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_boundaryObserver is not null)
            await _boundaryObserver(boundary, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static bool IsValidContentRange(
        ModelDownloadTransportResponse response,
        long requestedOffset,
        long expectedLength)
    {
        var range = response.ContentRange;
        return range is not null &&
            range.HasRange &&
            range.From == requestedOffset &&
            range.To is long to &&
            to >= requestedOffset &&
            (!range.HasLength || range.Length == expectedLength) &&
            (response.ContentLength is null || response.ContentLength == to - requestedOffset + 1);
    }

    private static ModelDownloadResult Failed(string code) =>
        new(ModelDownloadResultKind.Failed, null, code);

    private static ModelDownloadResult Interrupted(string code) =>
        new(ModelDownloadResultKind.Interrupted, null, code);
}
