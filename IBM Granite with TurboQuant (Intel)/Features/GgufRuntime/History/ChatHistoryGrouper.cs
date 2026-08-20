using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.Features.GgufRuntime.History;

internal static class ChatHistoryGrouper
{
    private static readonly string[] Labels =
        ["Today", "Yesterday", "Previous 7 Days", "Older"];

    internal static IReadOnlyList<ChatHistoryGroup> Group(
        IEnumerable<ChatConversation> conversations,
        DateTimeOffset now,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(conversations);
        ArgumentNullException.ThrowIfNull(timeZone);
        DateOnly today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
        return conversations
            .OrderByDescending(item => item.UpdatedUtc)
            .GroupBy(item => LabelFor(item.UpdatedUtc, today, timeZone))
            .OrderBy(group => Array.IndexOf(Labels, group.Key))
            .Select(group => new ChatHistoryGroup(group.Key, group.ToArray()))
            .ToArray();
    }

    private static string LabelFor(
        DateTimeOffset value,
        DateOnly today,
        TimeZoneInfo timeZone)
    {
        DateOnly local = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(value, timeZone).DateTime);
        int age = today.DayNumber - local.DayNumber;
        return age switch
        {
            <= 0 => Labels[0],
            1 => Labels[1],
            <= 7 => Labels[2],
            _ => Labels[3],
        };
    }
}
