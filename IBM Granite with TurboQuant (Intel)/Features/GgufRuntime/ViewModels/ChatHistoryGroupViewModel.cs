using System.Collections.ObjectModel;
using System.Linq;
using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.Features.GgufRuntime.ViewModels;

internal sealed class ChatHistoryGroupViewModel
{
    internal ChatHistoryGroupViewModel(ChatHistoryGroup group)
    {
        Label = group.Label;
        Conversations = new ObservableCollection<ChatConversationViewModel>(
            group.Conversations.Select(item => new ChatConversationViewModel(item)));
    }

    internal string Label { get; }
    internal ObservableCollection<ChatConversationViewModel> Conversations { get; }
}
