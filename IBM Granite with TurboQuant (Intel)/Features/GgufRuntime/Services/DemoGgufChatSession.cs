using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.Features.GgufRuntime.Services;

internal sealed class DemoGgufChatSession : IGgufChatSession
{
    private volatile bool stopRequested;

    public ValueTask PrepareConversationAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        cancellationToken.ThrowIfCancellationRequested();
        stopRequested = false;
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        stopRequested = false;
        string[] chunks =
        [
            "Preview mode is active. I received “",
            prompt,
            "”. ",
            "Import a compatible GGUF model to run local generation. ",
            "This preview currently demonstrates streaming, Stop, attachments, and dated history.",
        ];
        foreach (string chunk in chunks)
        {
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            if (stopRequested)
            {
                yield return new GgufChatStopped(NeedsReload: false);
                yield break;
            }

            yield return new GgufChatDelta(chunk);
        }

        yield return new GgufChatCompleted();
    }

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        stopRequested = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
