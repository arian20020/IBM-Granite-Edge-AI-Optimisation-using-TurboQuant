namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal static class GgufAdapterApplication
{
    internal static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        TextReader input,
        TextWriter output,
        Func<GgufAdapterOptions, IGgufInferenceEngine> engineFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(engineFactory);
        GgufAdapterOptions options;
        try
        {
            options = GgufAdapterOptions.Parse(arguments);
        }
        catch (GgufAdapterConfigurationException exception)
        {
            await output.WriteLineAsync(
                    $"G1FAIL {exception.Code}".AsMemory(),
                    CancellationToken.None)
                .ConfigureAwait(false);
            await output.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            return 64;
        }

        await using IGgufInferenceEngine engine = engineFactory(options);
        var host = new GgufAdapterHost(engine, input, output);
        return await host.RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
