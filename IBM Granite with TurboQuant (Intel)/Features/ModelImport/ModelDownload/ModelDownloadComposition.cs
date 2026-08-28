using System.Net;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal static class ModelDownloadComposition
{
    internal static ModelDownloadCoordinator CreateDefault()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(20)
        };
        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        var transport = new HttpModelDownloadTransport(client);
        var service = new ResumableVerifiedModelDownloadService(transport, AppModelLibrary.CreateDefault());
        return new ModelDownloadCoordinator(service, new WindowsModelDownloadNetworkPolicy());
    }
}
