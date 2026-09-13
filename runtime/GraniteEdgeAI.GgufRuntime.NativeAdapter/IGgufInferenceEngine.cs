namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal interface IGgufTitleInferenceEngine
{
    IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateTitleAsync(
        string prompt, CancellationToken cancellationToken);
}

internal interface IGgufInferenceEngine : IAsyncDisposable
{
    ValueTask InitializeAsync(
        IReadOnlyList<GgufAdapterMessage> initialHistory,
        CancellationToken cancellationToken);

    IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken);
}
