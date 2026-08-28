using System.Net;
using System.Net.Http.Headers;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal sealed class HttpModelDownloadTransport : IModelDownloadTransport
{
    private const int MaximumRedirects = 5;

    private static readonly string[] ApprovedHostRoots =
    [
        "huggingface.co",
        "hf.co"
    ];

    private readonly HttpClient _client;

    internal HttpModelDownloadTransport(HttpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        if (_client.DefaultRequestHeaders.Authorization is not null ||
            _client.DefaultRequestHeaders.Contains("Cookie"))
        {
            throw new ArgumentException(
                "The model download client cannot carry default credentials or cookies.",
                nameof(client));
        }
    }

    public async Task<ModelDownloadTransportResponse> OpenAsync(
        ModelDownloadCatalogEntry entry,
        long offset,
        string? entityTag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (offset < 0 || offset > entry.ExpectedByteLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        EntityTagHeaderValue? ifRange = null;
        if (entityTag is not null && !EntityTagHeaderValue.TryParse(entityTag, out ifRange))
        {
            throw new InvalidDataException("The stored HTTP entity tag is invalid.");
        }

        Uri currentUri = entry.ResolveUri;
        ValidateUri(currentUri);

        for (int redirects = 0; ; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            if (offset > 0)
            {
                request.Headers.Range = new RangeHeaderValue(offset, null);
                if (ifRange is not null)
                {
                    request.Headers.IfRange = new RangeConditionHeaderValue(ifRange);
                }
            }

            HttpResponseMessage response = await _client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!IsRedirect(response.StatusCode))
            {
                try
                {
                    Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
                    return new ModelDownloadTransportResponse(response, content, currentUri);
                }
                catch
                {
                    response.Dispose();
                    throw;
                }
            }

            try
            {
                if (redirects >= MaximumRedirects)
                {
                    throw new InvalidDataException("The model source exceeded the redirect limit.");
                }

                Uri? location = response.Headers.Location;
                if (location is null)
                {
                    throw new InvalidDataException("The model source returned a redirect without a location.");
                }

                currentUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                ValidateUri(currentUri);
            }
            finally
            {
                response.Dispose();
            }
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently or
            HttpStatusCode.Redirect or
            HttpStatusCode.SeeOther or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;

    private static void ValidateUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !IsApprovedHost(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidDataException("The model source location is not approved.");
        }
    }

    private static bool IsApprovedHost(string host) =>
        ApprovedHostRoots.Any(root =>
            string.Equals(host, root, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith('.' + root, StringComparison.OrdinalIgnoreCase));
}
