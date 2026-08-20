using System.Collections.Generic;

namespace GraniteEdgeAI.Features.GgufRuntime.History;

internal sealed record ChatHistoryGroup(
    string Label,
    IReadOnlyList<ChatConversation> Conversations);
