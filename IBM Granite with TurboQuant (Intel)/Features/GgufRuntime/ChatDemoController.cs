using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;

namespace GraniteEdgeAI.Features.GgufRuntime;

internal sealed class ChatDemoController : IAsyncDisposable
{
    private const string DemoModelId = "granite-demo";
    private const string DemoProfileId = "local-preview";
    private readonly ChatPage page;
    private readonly GgufChatCoordinator coordinator;
    private bool disposed;

    internal ChatDemoController(ChatPage page)
    {
        this.page = page ?? throw new ArgumentNullException(nameof(page));
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GraniteEdgeAI",
            "ChatHistory");
        coordinator = new GgufChatCoordinator(
            new AtomicJsonChatHistoryStore(root),
            new DemoGgufChatSession(),
            TimeProvider.System,
            TimeZoneInfo.Local);
        coordinator.ConversationChanged += Coordinator_ConversationChanged;
        page.NewChatRequested += Page_NewChatRequested;
        page.SendRequested += Page_SendRequested;
        page.StopRequested += Page_StopRequested;
        page.ConversationSelected += Page_ConversationSelected;
        page.SetModelHeader("Granite local preview", "deterministic demo runtime");
    }

    internal async Task InitializeAsync()
    {
        await coordinator.InitializeAsync(CancellationToken.None);
        if (coordinator.SelectedConversation is null)
        {
            await coordinator.NewChatAsync(
                DemoModelId,
                DemoProfileId,
                CancellationToken.None);
        }

        Render();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        coordinator.ConversationChanged -= Coordinator_ConversationChanged;
        page.NewChatRequested -= Page_NewChatRequested;
        page.SendRequested -= Page_SendRequested;
        page.StopRequested -= Page_StopRequested;
        page.ConversationSelected -= Page_ConversationSelected;
        await coordinator.DisposeAsync();
    }

    private async void Page_NewChatRequested(object? sender, EventArgs eventArguments)
    {
        if (disposed)
        {
            return;
        }

        await coordinator.NewChatAsync(
            DemoModelId,
            DemoProfileId,
            CancellationToken.None);
    }

    private async void Page_SendRequested(object? sender, string prompt)
    {
        if (disposed)
        {
            return;
        }

        page.SetGenerating(true);
        try
        {
            await coordinator.SendAsync(prompt, CancellationToken.None);
        }
        finally
        {
            page.SetGenerating(false);
            Render();
        }
    }

    private async void Page_StopRequested(object? sender, EventArgs eventArguments)
    {
        if (!disposed)
        {
            await coordinator.StopAsync(CancellationToken.None);
        }
    }

    private void Page_ConversationSelected(object? sender, Guid conversationId)
    {
        if (disposed || coordinator.IsGenerating)
        {
            return;
        }

        coordinator.Select(conversationId);
    }

    private void Coordinator_ConversationChanged(object? sender, EventArgs eventArguments) =>
        page.DispatcherQueue.TryEnqueue(Render);

    private void Render()
    {
        page.ClearHistory();
        Guid? selectedId = coordinator.SelectedConversation?.Id;
        foreach (ChatHistoryGroup group in coordinator.Groups)
        {
            page.AddHistoryGroup(group.Label);
            foreach (ChatConversation conversation in group.Conversations)
            {
                page.AddHistoryConversation(
                    conversation.Id,
                    conversation.Title,
                    conversation.Id == selectedId);
            }
        }

        page.ClearTranscript();
        if (coordinator.SelectedConversation is not ChatConversation selected)
        {
            return;
        }

        foreach (ChatMessage message in selected.Messages)
        {
            page.AddMessage(
                message.Content,
                message.Role == ChatMessageRole.User,
                FormatStatus(message.Status));
        }
    }

    private static string FormatStatus(ChatCompletionStatus status) => status switch
    {
        ChatCompletionStatus.Pending => "Thinking…",
        ChatCompletionStatus.Streaming => "Generating…",
        ChatCompletionStatus.Stopped => "Stopped",
        ChatCompletionStatus.Incomplete => "Incomplete",
        ChatCompletionStatus.Failed => "Failed",
        _ => string.Empty,
    };
}
