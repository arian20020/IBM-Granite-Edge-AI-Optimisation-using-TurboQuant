using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.Features.GgufRuntime.ViewModels;

internal sealed class ChatMessageViewModel
{
    internal ChatMessageViewModel(ChatMessage message)
    {
        Content = message.Content;
        IsUser = message.Role == ChatMessageRole.User;
        Status = message.Status;
    }

    internal string Content { get; }
    internal bool IsUser { get; }
    internal ChatCompletionStatus Status { get; }
}
