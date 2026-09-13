using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.Features.ChatModels;

internal sealed class ChatHistoryReplayException() : InvalidOperationException(
    "This conversation contains an unfinished or failed reply that cannot be replayed. Its saved text is preserved.");

internal sealed class ChatHistoryCapacityException() : InvalidOperationException(
    "This conversation exceeds the history capacity of the selected model. Start a new chat to use this model.");

internal static class ChatHistoryReplayPolicy
{
    internal static IReadOnlyList<ChatMessage> GetCompletedTurns(ChatConversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ChatMessage[] messages = conversation.Messages.Where(message => !string.IsNullOrWhiteSpace(message.Content)).ToArray();
        if (messages.Any(message => message.Role == ChatMessageRole.Assistant &&
            message.Status is not (ChatCompletionStatus.Completed or ChatCompletionStatus.LimitReached or ChatCompletionStatus.Stopped)))
            throw new ChatHistoryReplayException();
        return messages;
    }
}
