using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.GgufRuntime.Services;

internal abstract record GgufChatEvent;
internal sealed record GgufChatDelta(string Text) : GgufChatEvent;
internal sealed record GgufChatCompleted : GgufChatEvent;
internal sealed record GgufChatStopped(bool NeedsReload) : GgufChatEvent;
internal sealed record GgufChatFailed(string Code, string Message) : GgufChatEvent;

internal interface IGgufChatSession : IAsyncDisposable
{
    IAsyncEnumerable<GgufChatEvent> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken);

    ValueTask StopAsync(CancellationToken cancellationToken);
}
