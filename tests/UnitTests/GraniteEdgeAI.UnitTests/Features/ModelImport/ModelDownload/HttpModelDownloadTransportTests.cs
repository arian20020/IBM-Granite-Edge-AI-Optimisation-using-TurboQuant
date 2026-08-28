using System.Net;
using System.Net.Http.Headers;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class HttpModelDownloadTransportTests
{
    [TestMethod]
    public async Task OpenAsync_InitialRequestUsesPinnedHttpsUriWithoutCredentials()
    {
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.OK, new byte[] { 1 }));
        using var client = new HttpClient(handler);
        var transport = new HttpModelDownloadTransport(client);

        await using ModelDownloadTransportResponse response = await transport.OpenAsync(
            PinnedGraniteModelCatalog.ForSliderValue(50),
            offset: 0,
            entityTag: null,
            CancellationToken.None);

        Assert.HasCount(1, handler.Requests);
        RecordedRequest request = handler.Requests[0];
        Assert.AreEqual(HttpMethod.Get, request.Method);
        Assert.AreEqual(
            "https://huggingface.co/ibm-granite/granite-4.0-h-micro-GGUF/resolve/51ce07a9c9cfa971ca359d9625836bf8a4a1b61f/granite-4.0-h-micro-Q4_K_M.gguf?download=true",
            request.Uri.AbsoluteUri);
        Assert.IsNull(request.Range);
        Assert.IsNull(request.IfRange);
        Assert.IsFalse(request.HasAuthorization);
        Assert.IsFalse(request.HasCookie);
    }

    [TestMethod]
    public async Task OpenAsync_ResumeUsesExactRangeAndIfRangeHeaders()
    {
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.PartialContent, new byte[] { 2 }));
        using var client = new HttpClient(handler);
        var transport = new HttpModelDownloadTransport(client);

        await using ModelDownloadTransportResponse response = await transport.OpenAsync(
            PinnedGraniteModelCatalog.ForSliderValue(50),
            offset: 123,
            entityTag: "\"pinned-v1\"",
            CancellationToken.None);

        Assert.HasCount(1, handler.Requests);
        RecordedRequest request = handler.Requests[0];
        Assert.AreEqual("bytes=123-", request.Range);
        Assert.AreEqual("\"pinned-v1\"", request.IfRange);
    }

    [TestMethod]
    public async Task OpenAsync_FollowsRelativeRedirectAndDisposesIntermediateResponse()
    {
        var intermediateContent = new TrackingContent();
        var handler = new RecordingHandler(request =>
        {
            if (request.Uri.Host.Equals("huggingface.co", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
                {
                    Headers = { Location = new Uri("https://cdn-lfs.huggingface.co/artifact", UriKind.Absolute) },
                    Content = intermediateContent
                };
            }

            return Response(HttpStatusCode.OK, new byte[] { 3 });
        });
        using var client = new HttpClient(handler);
        var transport = new HttpModelDownloadTransport(client);

        await using ModelDownloadTransportResponse response = await transport.OpenAsync(
            PinnedGraniteModelCatalog.ForSliderValue(50),
            0,
            null,
            CancellationToken.None);

        Assert.AreEqual(2, handler.Requests.Count);
        Assert.IsTrue(intermediateContent.IsDisposed);
        Assert.AreEqual("cdn-lfs.huggingface.co", response.FinalUri.Host);
    }

    [TestMethod]
    public async Task OpenAsync_RejectsHttpsDowngrade()
    {
        var handler = new RecordingHandler(_ => Redirect("http://huggingface.co/unsafe"));
        using var client = new HttpClient(handler);
        var transport = new HttpModelDownloadTransport(client);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => transport.OpenAsync(
                PinnedGraniteModelCatalog.ForSliderValue(50),
                0,
                null,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task OpenAsync_RejectsHostSuffixWithoutLabelBoundary()
    {
        var handler = new RecordingHandler(_ => Redirect("https://evil-huggingface.co/unsafe"));
        using var client = new HttpClient(handler);
        var transport = new HttpModelDownloadTransport(client);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => transport.OpenAsync(
                PinnedGraniteModelCatalog.ForSliderValue(50),
                0,
                null,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task OpenAsync_RejectsMoreThanFiveRedirects()
    {
        int redirect = 0;
        var handler = new RecordingHandler(_ =>
            Redirect($"https://cdn-lfs.huggingface.co/redirect/{++redirect}"));
        using var client = new HttpClient(handler);
        var transport = new HttpModelDownloadTransport(client);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => transport.OpenAsync(
                PinnedGraniteModelCatalog.ForSliderValue(50),
                0,
                null,
                CancellationToken.None));
        Assert.AreEqual(6, handler.Requests.Count);
    }

    [TestMethod]
    public void Constructor_RejectsClientDefaultCredentials()
    {
        using var client = new HttpClient(new RecordingHandler(_ => Response(HttpStatusCode.OK, [])));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "secret");

        Assert.ThrowsExactly<ArgumentException>(() => new HttpModelDownloadTransport(client));
    }

    private static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.TemporaryRedirect)
        {
            Headers = { Location = new Uri(location, UriKind.Absolute) }
        };

    private static HttpResponseMessage Response(HttpStatusCode statusCode, byte[] bytes) =>
        new(statusCode)
        {
            Content = new ByteArrayContent(bytes)
        };

    private sealed class RecordingHandler(
        Func<RecordedRequest, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        internal List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var snapshot = new RecordedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Range?.ToString(),
                request.Headers.IfRange?.ToString(),
                request.Headers.Authorization is not null,
                request.Headers.Contains("Cookie"));
            Requests.Add(snapshot);
            return Task.FromResult(responseFactory(snapshot));
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Range,
        string? IfRange,
        bool HasAuthorization,
        bool HasCookie);

    private sealed class TrackingContent : ByteArrayContent
    {
        internal TrackingContent() : base([])
        {
        }

        internal bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
