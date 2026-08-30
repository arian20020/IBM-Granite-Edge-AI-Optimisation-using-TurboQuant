namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal interface IModelDownloadService
{
    Task<ModelDownloadResult> DownloadAsync(
        ModelDownloadCatalogEntry entry,
        IProgress<ModelDownloadProgress> progress,
        CancellationToken cancellationToken);

    Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken);

    Task DiscardPartialAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken);
}
