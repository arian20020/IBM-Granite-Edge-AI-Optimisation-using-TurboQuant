using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

/// <summary>
/// Captures one stable, read-only identity snapshot of a local model file.
/// </summary>
public sealed class ModelFileSnapshotService
{
    private const int BufferSize = 1024 * 1024;
    private readonly IModelFileHasher _hasher;

    public ModelFileSnapshotService()
        : this(new Sha256ModelFileHasher())
    {
    }

    public ModelFileSnapshotService(IModelFileHasher hasher)
    {
        _hasher = hasher ??
            throw new ArgumentNullException(nameof(hasher));
    }

    /// <summary>
    /// Reads the selected file without write access and calculates SHA-256.
    /// </summary>
    public async Task<ModelFileSnapshot> CaptureAsync(
        string modelPath,
        CancellationToken cancellationToken)
    {
        return await CaptureCoreAsync(
                modelPath,
                expectedIdentity: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task<ModelFileSnapshot> CaptureAsync(
        VocabOnlyProbeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await CaptureCoreAsync(
                request.ModelPath,
                request,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ModelFileSnapshot> CaptureCoreAsync(
        string modelPath,
        VocabOnlyProbeRequest? expectedIdentity,
        CancellationToken cancellationToken,
        Action<long, long>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPath = Path.GetFullPath(modelPath);
        var fileInfo = new FileInfo(fullPath);
        fileInfo.Refresh();

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                "The selected model file was not found.",
                fullPath);
        }

        long lengthBeforeHash = fileInfo.Length;
        DateTime lastWriteBeforeHash = fileInfo.LastWriteTimeUtc;

        if (expectedIdentity is not null &&
            (lengthBeforeHash != expectedIdentity.ExpectedLengthBytes ||
             new DateTimeOffset(lastWriteBeforeHash, TimeSpan.Zero) !=
             expectedIdentity.ExpectedLastWriteTimeUtc))
        {
            throw new ModelFileContinuityException();
        }

        byte[] hash;

        await using (
            var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan))
        {
            using var measuredStream = progress is null ? null : new MeasuredReadStream(stream, progress);
            hash = await _hasher.ComputeHashAsync(
                measuredStream ?? (Stream)stream,
                cancellationToken);
        }

        fileInfo.Refresh();

        if (!fileInfo.Exists ||
            fileInfo.Length != lengthBeforeHash ||
            fileInfo.LastWriteTimeUtc != lastWriteBeforeHash)
        {
            throw new IOException(
                "The selected model changed while its identity was being captured.");
        }

        return new ModelFileSnapshot
        {
            FileName = fileInfo.Name,
            CanonicalPathSha256 =
                CalculateCanonicalPathSha256(fullPath),
            LengthBytes = lengthBeforeHash,
            LastWriteTimeUtc = new DateTimeOffset(
                lastWriteBeforeHash,
                TimeSpan.Zero),
            Sha256 = Convert
                .ToHexString(hash)
                .ToLowerInvariant()
        };
    }

    internal Task<ModelFileSnapshot> CaptureMeasuredAsync(string modelPath,
        VocabOnlyProbeRequest? expectedIdentity, Action<long, long> progress, CancellationToken token) =>
        CaptureCoreAsync(modelPath, expectedIdentity, token, progress);

    internal async Task<(ModelFileSnapshot Snapshot, Exception? ProgressFailure)> CaptureIntegrityAsync(
        string modelPath, Action<long, long> progress)
    {
        Exception? failure = null;
        var snapshot = await CaptureCoreAsync(modelPath, null, CancellationToken.None, (done, total) =>
        {
            if (failure is not null) return;
            try { progress(done, total); }
            catch (Exception exception) { failure = exception; }
        }).ConfigureAwait(false);
        return (snapshot, failure);
    }

    private sealed class MeasuredReadStream(Stream inner, Action<long, long> progress) : Stream
    {
        private long reported;
        private int lastPercentage = -1;
        private void Report(int count)
        {
            if (count <= 0) return;
            long position = inner.Position;
            if (position > reported)
            {
                reported = position;
                int percentage = inner.Length == 0 ? 100 : (int)(100d * position / inner.Length);
                if (percentage > lastPercentage)
                {
                    lastPercentage = percentage;
                    progress(position, inner.Length);
                }
            }
        }
        public override int Read(byte[] buffer, int offset, int count) { int n = inner.Read(buffer, offset, count); Report(n); return n; }
        public override int Read(Span<byte> buffer) { int n = inner.Read(buffer); Report(n); return n; }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        { int n = await inner.ReadAsync(buffer.AsMemory(offset, count), token).ConfigureAwait(false); Report(n); return n; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        { int n = await inner.ReadAsync(buffer, token).ConfigureAwait(false); Report(n); return n; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        // The surrounding snapshot operation owns and disposes the retained file.
    }

    private static string CalculateCanonicalPathSha256(string fullPath)
    {
        string canonicalPath = OperatingSystem.IsWindows()
            ? fullPath.ToUpperInvariant()
            : fullPath;

        byte[] pathBytes = Encoding.UTF8.GetBytes(canonicalPath);
        byte[] hash = SHA256.HashData(pathBytes);

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
    }
}

internal sealed class ModelFileContinuityException : IOException
{
    internal ModelFileContinuityException()
        : base("The selected model changed after it was prepared for inspection.")
    {
    }
}
