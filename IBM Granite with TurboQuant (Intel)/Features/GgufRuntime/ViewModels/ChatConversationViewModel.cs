using System;
using System.Collections.ObjectModel;
using System.Linq;
using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.Features.GgufRuntime.ViewModels;

internal sealed class ChatConversationViewModel
{
    internal ChatConversationViewModel(ChatConversation conversation)
    {
        Id = conversation.Id;
        Title = conversation.Title;
        Messages = new ObservableCollection<ChatMessageViewModel>(
            conversation.Messages.Select(message => new ChatMessageViewModel(message)));
    }

    internal Guid Id { get; }
    internal string Title { get; }
    internal ObservableCollection<ChatMessageViewModel> Messages { get; }
}
