using System.Net;
using System.Net.Http.Headers;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal interface IModelDownloadTransport
{
    Task<ModelDownloadTransportResponse> OpenAsync(
        ModelDownloadCatalogEntry entry,
        long offset,
        string? entityTag,
        CancellationToken cancellationToken);
}

internal sealed class ModelDownloadTransportResponse : IAsyncDisposable
{
    private HttpResponseMessage? _response;

    internal ModelDownloadTransportResponse(
        HttpResponseMessage response,
        Stream content,
        Uri finalUri)
    {
        _response = response;
        Content = content;
        FinalUri = finalUri;
        StatusCode = response.StatusCode;
        ContentLength = response.Content.Headers.ContentLength;
        ContentRange = response.Content.Headers.ContentRange;
        EntityTag = response.Headers.ETag?.ToString();
    }

    internal HttpStatusCode StatusCode { get; }

    internal Stream Content { get; }

    internal Uri FinalUri { get; }

    internal long? ContentLength { get; }

    internal ContentRangeHeaderValue? ContentRange { get; }

    internal string? EntityTag { get; }

    public ValueTask DisposeAsync()
    {
        HttpResponseMessage? response = Interlocked.Exchange(ref _response, null);
        response?.Dispose();
        return ValueTask.CompletedTask;
    }
}
