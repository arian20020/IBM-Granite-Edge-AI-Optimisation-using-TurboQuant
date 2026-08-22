using System;
using System.Collections.Generic;
using System.Linq;
using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.Features.GgufRuntime.Clipboard;

internal static class ChatTranscriptFormatter
{
    internal static string Format(IReadOnlyList<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            messages
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .Select(message =>
                    $"{RoleLabel(message.Role)}:{Environment.NewLine}{message.Content}"));
    }

    private static string RoleLabel(ChatMessageRole role) => role switch
    {
        ChatMessageRole.User => "You",
        ChatMessageRole.Assistant => "Granite Edge AI",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}
