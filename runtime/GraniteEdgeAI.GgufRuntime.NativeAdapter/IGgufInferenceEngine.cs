namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal interface IGgufInferenceEngine : IAsyncDisposable
{
    ValueTask InitializeAsync(
        IReadOnlyList<GgufAdapterMessage> initialHistory,
        CancellationToken cancellationToken);

    IAsyncEnumerable<string> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken);
}
