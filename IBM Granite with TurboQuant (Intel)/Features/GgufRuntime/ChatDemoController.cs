using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private readonly ChatRenderScheduler renderScheduler;
    private List<HistoryRenderKey> renderedHistory = [];
    private bool followLatest;
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
        renderScheduler = new ChatRenderScheduler(
            callback => page.DispatcherQueue.TryEnqueue(() => callback()),
            Render);
        coordinator.ConversationChanged += Coordinator_ConversationChanged;
        page.NewChatRequested += Page_NewChatRequested;
        page.SendRequested += Page_SendRequested;
        page.StopRequested += Page_StopRequested;
        page.ConversationSelected += Page_ConversationSelected;
        page.SetModelHeader("Preview mode", "No model loaded");
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
        renderScheduler.Dispose();
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

        followLatest = true;
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

        followLatest = true;
        page.SetGenerating(true);
        try
        {
            await coordinator.SendAsync(prompt, CancellationToken.None);
        }
        finally
        {
            page.SetGenerating(false);
            renderScheduler.Request();
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

        followLatest = true;
        coordinator.Select(conversationId);
    }

    private void Coordinator_ConversationChanged(object? sender, EventArgs eventArguments) =>
        renderScheduler.Request();

    private void Render()
    {
        ChatCoordinatorSnapshot snapshot = coordinator.CaptureSnapshot();
        RenderHistoryIfChanged(snapshot);
        if (snapshot.SelectedConversation is not ChatConversation selected)
        {
            return;
        }

        bool consumeFollowLatest = followLatest;
        followLatest = false;
        page.SynchronizeTranscript(
            selected.Id,
            selected.Messages,
            consumeFollowLatest);
    }

    private void RenderHistoryIfChanged(ChatCoordinatorSnapshot snapshot)
    {
        Guid? selectedId = snapshot.SelectedConversation?.Id;
        var currentHistory = new List<HistoryRenderKey>();
        foreach (ChatHistoryGroup group in snapshot.Groups)
        {
            foreach (ChatConversation conversation in group.Conversations)
            {
                currentHistory.Add(new HistoryRenderKey(
                    group.Label,
                    conversation.Id,
                    conversation.Title,
                    conversation.Id == selectedId));
            }
        }

        if (renderedHistory.SequenceEqual(currentHistory))
        {
            return;
        }

        page.ClearHistory();
        string? renderedGroup = null;
        foreach (HistoryRenderKey item in currentHistory)
        {
            if (!string.Equals(renderedGroup, item.GroupLabel, StringComparison.Ordinal))
            {
                page.AddHistoryGroup(item.GroupLabel);
                renderedGroup = item.GroupLabel;
            }

            page.AddHistoryConversation(
                item.ConversationId,
                item.Title,
                item.IsSelected);
        }

        renderedHistory = currentHistory;
    }

    private readonly record struct HistoryRenderKey(
        string GroupLabel,
        Guid ConversationId,
        string Title,
        bool IsSelected);
}
