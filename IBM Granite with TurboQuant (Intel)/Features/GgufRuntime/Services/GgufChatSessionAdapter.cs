using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.WorkerClient;

namespace GraniteEdgeAI.Features.GgufRuntime.Services;

internal sealed class GgufChatSessionAdapter : IGgufChatSession
{
    private readonly GgufRuntimeSession session;

    internal GgufChatSessionAdapter(GgufRuntimeSession session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        await foreach (GgufRuntimeEvent runtimeEvent in
            session.GenerateAsync(prompt, cancellationToken).ConfigureAwait(false))
        {
            GgufChatEvent? chatEvent = runtimeEvent switch
            {
                TextDeltaEvent delta => new GgufChatDelta(delta.Text),
                ResponseCompletedEvent => new GgufChatCompleted(),
                ResponseStoppedEvent stopped => new GgufChatStopped(
                    stopped.Disposition == GgufStopDisposition.StoppedNeedsReload),
                RuntimeFailureEvent failure => new GgufChatFailed(
                    failure.Failure.Code,
                    "The local model could not complete this response."),
                _ => null,
            };
            if (chatEvent is not null)
            {
                yield return chatEvent;
            }
        }
    }

    public ValueTask StopAsync(CancellationToken cancellationToken) =>
        session.StopAsync(cancellationToken);

    public ValueTask DisposeAsync() => session.DisposeAsync();
}
