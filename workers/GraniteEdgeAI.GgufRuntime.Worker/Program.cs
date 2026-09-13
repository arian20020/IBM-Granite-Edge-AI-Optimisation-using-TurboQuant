using GraniteEdgeAI.GgufRuntime.Transport;

namespace GraniteEdgeAI.GgufRuntime.Worker;

internal static class Program
{
    private static async Task<int> Main()
    {
        await using Stream input = Console.OpenStandardInput();
        await using Stream output = Console.OpenStandardOutput();
        await using Stream error = Console.OpenStandardError();
        try
        {
            byte[] payload = await GgufFrameReader.ReadAsync(
                input,
                CancellationToken.None).ConfigureAwait(false);
            GgufWorkerBootstrap bootstrap = GgufWorkerBootstrapCodec.Deserialize(payload);
            using var channel = new GgufFramedChannel(input, output);
            await using var host = new GgufWorkerHost(bootstrap, channel);
            return await host.RunAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is GgufTransportException or IOException or
                InvalidOperationException or ArgumentException)
        {
            byte[] failure = System.Text.Encoding.UTF8.GetBytes("G1-WORKER-FAILURE\n");
            await error.WriteAsync(failure).ConfigureAwait(false);
            return 70;
        }
    }
}
