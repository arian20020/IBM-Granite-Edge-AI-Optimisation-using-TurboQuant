namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal readonly record struct ModelDownloadOperationId(Guid Value)
{
    internal static ModelDownloadOperationId CreateNew() => new(Guid.NewGuid());
}

internal enum ModelDownloadResultKind
{
    Completed,
    AlreadyAvailable,
    Interrupted,
    Failed
}

internal enum ModelDownloadStage
{
    Idle,
    Preparing,
    Downloading,
    Verifying,
    Completed,
    Interrupted,
    Failed
}

internal sealed record ModelDownloadProgress(
    ModelDownloadStage Stage,
    long DownloadedBytes,
    long TotalBytes);

internal sealed class VerifiedDownloadedModel
{
    internal VerifiedDownloadedModel(
        string localPath,
        string displayName,
        string catalogId,
        long byteLength,
        string sha256)
    {
        LocalPath = localPath ?? throw new ArgumentNullException(nameof(localPath));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        CatalogId = catalogId ?? throw new ArgumentNullException(nameof(catalogId));
        ByteLength = byteLength;
        Sha256 = sha256 ?? throw new ArgumentNullException(nameof(sha256));
    }

    internal string LocalPath { get; }

    internal string DisplayName { get; }

    internal string CatalogId { get; }

    internal long ByteLength { get; }

    internal string Sha256 { get; }

    public override string ToString() =>
        $"VerifiedDownloadedModel {{ DisplayName = {DisplayName}, CatalogId = {CatalogId}, ByteLength = {ByteLength} }}";
}

internal sealed record ModelDownloadResult(
    ModelDownloadResultKind Kind,
    VerifiedDownloadedModel? VerifiedModel,
    string? ErrorCode);

internal sealed record ModelDownloadResumeInfo(
    string CatalogId,
    long DownloadedBytes,
    long TotalBytes,
    string? EntityTag);
