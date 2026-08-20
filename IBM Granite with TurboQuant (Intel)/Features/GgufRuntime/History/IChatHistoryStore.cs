using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.GgufRuntime.History;

internal interface IChatHistoryStore
{
    Task<IReadOnlyList<ChatConversation>> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(ChatConversation conversation, CancellationToken cancellationToken);
    Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
