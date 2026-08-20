using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.GgufRuntime.Services;

internal sealed class DemoGgufChatSession : IGgufChatSession
{
    private volatile bool stopRequested;

    public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        stopRequested = false;
        string[] chunks =
        [
            "This is a local preview response to “",
            prompt,
            "”. ",
            "The production path uses the protected GGUF CLI supervisor; ",
            "this preview lets you test streaming, Stop, and dated history without downloading a model.",
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
