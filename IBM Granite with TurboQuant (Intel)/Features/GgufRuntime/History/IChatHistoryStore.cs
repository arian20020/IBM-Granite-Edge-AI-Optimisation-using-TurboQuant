using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.GgufRuntime.History;

internal sealed record ChatHistoryLoadResult(
    IReadOnlyList<ChatConversation> Conversations,
    bool HasUnavailableRecords);

internal sealed class ChatHistoryUnavailableException : Exception
{
    internal ChatHistoryUnavailableException()
        : base("Local Chat history is temporarily unavailable.")
    {
    }
}

internal interface IChatHistoryStore
{
    Task<ChatHistoryLoadResult> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(ChatConversation conversation, CancellationToken cancellationToken);
    Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
