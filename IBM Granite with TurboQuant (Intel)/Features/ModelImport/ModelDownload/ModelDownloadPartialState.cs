namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal sealed record ModelDownloadPartialState(
    int SchemaVersion,
    string CatalogId,
    string Revision,
    string FileName,
    long ExpectedByteLength,
    string ExpectedSha256,
    long DurableByteLength,
    string? EntityTag,
    DateTimeOffset UpdatedAtUtc);
