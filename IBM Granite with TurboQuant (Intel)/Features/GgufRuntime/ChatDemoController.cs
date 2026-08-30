using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.GgufRuntime.WorkerClient;

namespace GraniteEdgeAI.Features.GgufRuntime;

internal sealed class ChatDemoController : IAsyncDisposable
{
    private readonly ChatPage page;
    private readonly GgufChatCoordinator coordinator;
    private readonly ChatRenderScheduler renderScheduler;
    private readonly string modelId;
    private readonly string profileId;
    private readonly object operationSync = new();
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly HashSet<Task> activeOperations = [];
    private List<HistoryRenderKey> renderedHistory = [];
    private bool followLatest;
    private bool disposed;
    private Task? retirementTask;

    internal ChatDemoController(ChatPage page)
        : this(
            page,
            new DemoGgufChatSession(),
            "granite-demo",
            "local-preview",
            "Preview mode",
            "No model loaded")
    {
    }

    private ChatDemoController(
        ChatPage page,
        IGgufChatSession session,
        string modelId,
        string profileId,
        string displayName,
        string runtimeDescription)
    {
        this.page = page ?? throw new ArgumentNullException(nameof(page));
        this.modelId = modelId;
        this.profileId = profileId;
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GraniteEdgeAI",
            "ChatHistory");
        coordinator = new GgufChatCoordinator(
            new AtomicJsonChatHistoryStore(root),
            session,
            TimeProvider.System,
            TimeZoneInfo.Local);
        renderScheduler = new ChatRenderScheduler(
            callback => page.DispatcherQueue.TryEnqueue(() => callback()),
            Render);
        coordinator.ConversationChanged += Coordinator_ConversationChanged;
        page.NewChatRequested += Page_NewChatRequested;
        page.SendRequested += Page_SendRequested;
        page.StopRequested += Page_StopRequested;
        page.ContinuationRequested += Page_ContinuationRequested;
        page.ConversationSelected += Page_ConversationSelected;
        page.SetModelHeader(displayName, runtimeDescription);
    }

    internal static async Task<ChatDemoController> CreateProductionAsync(
        ChatPage page,
        GgufChatLaunchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        GgufRuntimeClient client = GgufRuntimeClient.CreateFromPackage(
            request.PackageRoot,
            request.TrustedManifest.Span,
            request.ModelFile);
        await request.VerifyModelAsync(cancellationToken);
        var session = new GgufChatSessionAdapter(
            client,
            request.Configuration);
        return new ChatDemoController(
            page,
            session,
            request.Configuration.ModelId,
            request.Configuration.ProfileId,
            request.DisplayName,
            $"{request.Configuration.Backend} Â· {request.Configuration.RuntimeBuildId}");
    }

    internal async Task InitializeAsync()
    {
        await coordinator.InitializeAsync(
            modelId,
            profileId,
            lifetimeCancellation.Token);
        if (coordinator.SelectedConversation is null)
        {
            await coordinator.NewChatAsync(
                modelId,
                profileId,
                lifetimeCancellation.Token);
        }

        Render();
    }

    public ValueTask DisposeAsync()
    {
        lock (operationSync)
        {
            retirementTask ??= RetireCoreAsync();
            return new ValueTask(retirementTask);
        }
    }

    private async Task RetireCoreAsync()
    {
        Task[] pending;
        lock (operationSync)
        {
            disposed = true;
            pending = activeOperations.ToArray();
        }
        lifetimeCancellation.Cancel();

        coordinator.ConversationChanged -= Coordinator_ConversationChanged;
        page.NewChatRequested -= Page_NewChatRequested;
        page.SendRequested -= Page_SendRequested;
        page.StopRequested -= Page_StopRequested;
        page.ContinuationRequested -= Page_ContinuationRequested;
        page.ConversationSelected -= Page_ConversationSelected;
        if (coordinator.IsGenerating)
        {
            try
            {
                await coordinator.StopAsync(CancellationToken.None);
            }
            catch
            {
                // Lifetime cancellation remains the fail-closed fallback.
            }
        }
        await Task.WhenAll(pending);
        renderScheduler.Dispose();
        await coordinator.DisposeAsync();
        lifetimeCancellation.Dispose();
    }

    private void Page_NewChatRequested(object? sender, EventArgs eventArguments) =>
        TrackOperation(async cancellationToken =>
        {
            followLatest = true;
            await coordinator.NewChatAsync(
                modelId,
                profileId,
                cancellationToken);
        });

    private void Page_SendRequested(object? sender, string prompt) =>
        TrackOperation(async cancellationToken =>
        {
            followLatest = true;
            page.SetGenerating(true);
            try
            {
                await coordinator.SendAsync(prompt, cancellationToken);
            }
            finally
            {
                page.SetGenerating(false);
                renderScheduler.Request();
            }
        });

    private void Page_StopRequested(object? sender, EventArgs eventArguments) =>
        TrackOperation(async cancellationToken =>
        {
            await coordinator.StopAsync(cancellationToken);
        });

    private void Page_ContinuationRequested(object? sender, Guid messageId)
    {
        if (disposed || coordinator.IsGenerating)
        {
            return;
        }

        TrackOperation(async cancellationToken =>
        {
            followLatest = true;
            page.SetGenerating(true);
            try
            {
                await coordinator.ContinueAsync(messageId, cancellationToken);
            }
            finally
            {
                page.SetGenerating(false);
                renderScheduler.Request();
            }
        });
    }

    private void Page_ConversationSelected(object? sender, Guid conversationId)
    {
        if (disposed || coordinator.IsGenerating)
        {
            return;
        }

        TrackOperation(async cancellationToken =>
        {
            followLatest = true;
            await coordinator.SelectAsync(conversationId, cancellationToken);
        });
    }

    private void TrackOperation(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Task tracked;
        lock (operationSync)
        {
            if (disposed)
            {
                return;
            }

            tracked = RunOperationAsync(operation, lifetimeCancellation.Token);
            activeOperations.Add(tracked);
        }

        _ = RemoveOperationAsync(tracked);
    }

    private async Task RunOperationAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Controller disposal owns this lifetime cancellation.
        }
        catch
        {
            // Coordinators map expected failures into state. Unexpected event
            // failures remain bounded to this surface and request a safe redraw.
            renderScheduler.Request();
        }
    }

    private async Task RemoveOperationAsync(Task operation)
    {
        await operation;
        lock (operationSync)
        {
            activeOperations.Remove(operation);
        }
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
