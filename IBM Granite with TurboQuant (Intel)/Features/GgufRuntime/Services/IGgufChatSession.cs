using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.Features.GgufRuntime.Services;

internal abstract record GgufChatEvent;
internal sealed record GgufChatDelta(string Text) : GgufChatEvent;
internal enum GgufChatCompletionKind
{
    Stop,
    Length,
}

internal sealed record GgufChatCompleted(GgufChatCompletionKind Kind)
    : GgufChatEvent;
internal sealed record GgufChatStopped(bool NeedsReload) : GgufChatEvent;
internal sealed record GgufChatFailed(string Code, string Message) : GgufChatEvent;

internal interface IGgufChatTitleSession
{
    Task<string?> GenerateTitleAsync(string prompt, Task stopRequested, CancellationToken cancellationToken);
}

internal interface IBackgroundChatTitleSession
{
    Task<string?> TryGenerateTitleAsync(string prompt, CancellationToken cancellationToken);
}

internal sealed class GgufTitleRestoreException : InvalidOperationException
{
    internal GgufTitleRestoreException() : base("The model needs to be reloaded before continuing. Select the model again or start a new chat.") { }
}

internal interface IGgufChatSession : IAsyncDisposable
{
    ValueTask PrepareConversationAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken);

    IAsyncEnumerable<GgufChatEvent> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken);

    ValueTask StopAsync(CancellationToken cancellationToken);
}
