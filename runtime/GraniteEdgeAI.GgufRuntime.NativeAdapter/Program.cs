using System.Text;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = new UTF8Encoding(false, true);
        Console.OutputEncoding = new UTF8Encoding(false);
        return await GgufAdapterApplication.RunAsync(
            args,
            Console.In,
            Console.Out,
            options => options.Backend == GgufAdapterBackend.UpstreamCpu
                ? new LlamaSharpInferenceEngine(options)
                : new AtomicBotInferenceEngine(options),
            CancellationToken.None).ConfigureAwait(false);
    }
}
