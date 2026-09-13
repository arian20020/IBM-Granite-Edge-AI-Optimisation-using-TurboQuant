using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;

namespace GraniteEdgeAI.GgufRuntime.Worker.Session;

internal interface IGgufCliProcess : IAsyncDisposable
{
    ValueTask StartAsync(
        string modelPath,
        GgufRuntimeConfiguration configuration,
        IReadOnlyList<GgufConversationTurn> initialTurns,
        CancellationToken cancellationToken);

    ValueTask WritePromptAsync(string content, CancellationToken cancellationToken);
    ValueTask WriteTitleAsync(string content, CancellationToken cancellationToken) =>
        ValueTask.FromException(new NotSupportedException("Temporary title inference is unavailable."));

    ValueTask<string?> ReadOutputLineAsync(CancellationToken cancellationToken);

    ValueTask<bool> TryInterruptAsync(CancellationToken cancellationToken);

    ValueTask TerminateAsync(CancellationToken cancellationToken);
}
