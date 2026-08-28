using System.Net;
using System.Security.Cryptography;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal sealed class ResumableVerifiedModelDownloadService : IModelDownloadService
{
    private const int BufferSize = 128 * 1024;
    private const long CheckpointIntervalBytes = 8L * 1024 * 1024;

    private readonly IModelDownloadTransport _transport;
    private readonly AppModelLibrary _library;
    private readonly TimeSpan _inactivityTimeout;

    internal ResumableVerifiedModelDownloadService(
        IModelDownloadTransport transport,
        AppModelLibrary library,
        TimeSpan? inactivityTimeout = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _inactivityTimeout = inactivityTimeout ?? TimeSpan.FromSeconds(30);
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
                await using Stream destination = await _library.OpenPartialWriteAsync(
                    entry,
                    writeOffset,
                    cancellationToken);
                await WriteCheckpointAsync(entry, destination, downloaded, entityTag, cancellationToken);

                progress.Report(new ModelDownloadProgress(
                    ModelDownloadStage.Downloading,
                    downloaded,
                    entry.ExpectedByteLength));

                byte[] buffer = new byte[BufferSize];
                while (true)
                {
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

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    downloaded += read;
                    progress.Report(new ModelDownloadProgress(
                        ModelDownloadStage.Downloading,
                        downloaded,
                        entry.ExpectedByteLength));

                    if (downloaded - lastCheckpoint >= CheckpointIntervalBytes)
                    {
                        await WriteCheckpointAsync(
                            entry,
                            destination,
                            downloaded,
                            entityTag,
                            cancellationToken);
                        lastCheckpoint = downloaded;
                    }
                }

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
                byte[] actual = await _library.ComputePartialSha256Async(entry, cancellationToken);
                byte[] expected = Convert.FromHexString(entry.ExpectedSha256);
                if (!CryptographicOperations.FixedTimeEquals(actual, expected))
                {
                    await _library.DiscardPartialAsync(entry, CancellationToken.None);
                    return Failed("download-integrity-failed");
                }

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
            catch (InvalidDataException)
            {
                return Failed("download-http-rejected");
            }
            catch (HttpRequestException)
            {
                return Interrupted("download-interrupted");
            }
            catch (IOException)
            {
                return Interrupted("download-interrupted");
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
        await destination.FlushAsync(cancellationToken);
        if (destination is FileStream fileStream)
        {
            fileStream.Flush(flushToDisk: true);
        }

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
