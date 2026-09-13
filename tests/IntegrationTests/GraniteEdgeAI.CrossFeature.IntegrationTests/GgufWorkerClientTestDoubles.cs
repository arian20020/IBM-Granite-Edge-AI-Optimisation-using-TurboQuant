using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient;

// the lifecycle tests use the adapter's injected-session constructor. these
// compile-only doubles keep its unused production constructor type-correct
// without loading an independently built unsigned worker-client assembly
internal sealed class GgufRuntimeClient
{
    internal Task<GgufRuntimeSession> StartAsync(
        GgufRuntimeConfiguration configuration,
        IReadOnlyList<GgufConversationTurn> turns,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("The production worker is not launched by T1.");
}

internal sealed class GgufRuntimeSession : IAsyncDisposable
{
    internal async IAsyncEnumerable<GgufRuntimeEvent> GenerateAsync(
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }

    internal ValueTask StopAsync(CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
