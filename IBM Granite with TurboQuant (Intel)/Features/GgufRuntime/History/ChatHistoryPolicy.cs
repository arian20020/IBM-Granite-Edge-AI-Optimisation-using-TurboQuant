using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.Features.GgufRuntime.History;

internal static class ChatHistoryPolicy
{
    internal const int MaximumMessageCharacters = 262_144;
    internal const int MaximumMessagesPerConversation = 512;
    internal const long MaximumRecordBytes = 4 * 1024 * 1024;
    internal const int MaximumConversations = 500;
    internal static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(180);

    internal static IReadOnlyList<ChatConversation> ApplyRetention(
        IEnumerable<ChatConversation> conversations,
        DateTimeOffset now,
        TimeSpan? retention = null) => conversations
        .Where(item => now - item.UpdatedUtc <= (retention ?? DefaultRetention))
        .OrderByDescending(item => item.UpdatedUtc)
        .Take(MaximumConversations)
        .ToArray();
}
