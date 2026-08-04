using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

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
            hash = await _hasher.ComputeHashAsync(
                stream,
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
