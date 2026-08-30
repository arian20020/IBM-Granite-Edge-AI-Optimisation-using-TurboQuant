using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ApplicationFaults;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;
using GraniteEdgeAI.GgufRuntime.WorkerClient;

namespace GraniteEdgeAI.Features.GgufRuntime;

internal enum ChatOperationSupportCode
{
    HistoryUnavailable,
    RuntimeUnavailable,
}

internal sealed class ChatDemoController : IAsyncDisposable
{
    private readonly ChatPage page;
    private readonly GgufChatCoordinator coordinator;
    private readonly ChatRenderScheduler renderScheduler;
    private readonly string modelId;
    private readonly string profileId;
    private readonly IApplicationFaultReporter faultReporter;
    private readonly object operationSync = new();
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly HashSet<Task> activeOperations = [];
    private List<HistoryRenderKey> renderedHistory = [];
    private bool followLatest;
    private bool disposed;
    private Task? retirementTask;
    private int operationFaultReported;
    private int retirementFaultReported;
    private ChatOperationSupportCode? lastSupportCode;

    private ChatDemoController(
        ChatPage page,
        IGgufChatSession session,
        string modelId,
        string profileId,
        string displayName,
        string runtimeDescription,
        IApplicationFaultReporter? faultReporter = null)
        : this(
            page,
            CreateHistoryStore(),
            session,
            modelId,
            profileId,
            displayName,
            runtimeDescription,
            faultReporter)
    {
    }

    private ChatDemoController(
        ChatPage page,
        IChatHistoryStore store,
        IGgufChatSession session,
        string modelId,
        string profileId,
        string displayName,
        string runtimeDescription,
        IApplicationFaultReporter? faultReporter)
    {
        this.page = page ?? throw new ArgumentNullException(nameof(page));
        this.modelId = modelId;
        this.profileId = profileId;
        coordinator = new GgufChatCoordinator(
            store ?? throw new ArgumentNullException(nameof(store)),
            session,
            TimeProvider.System,
            TimeZoneInfo.Local);
        this.faultReporter = faultReporter ?? BoundedApplicationFaultReporter.Shared;
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

    internal ChatOperationSupportCode? LastSupportCode => lastSupportCode;

    internal static async Task<ChatDemoController> CreateInitializedProductionAsync(
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
        ChatDemoController? controller = null;
        try
        {
            controller = new ChatDemoController(
                page,
                session,
                request.Configuration.ModelId,
                request.Configuration.ProfileId,
                request.DisplayName,
                $"{request.Configuration.Backend} - {request.Configuration.RuntimeBuildId}");
            await controller.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return controller;
        }
        catch
        {
            if (controller is not null)
            {
                await controller.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                await DisposeUnownedSessionAsync(session, faultReporter: null)
                    .ConfigureAwait(false);
            }
            throw;
        }
    }

    internal static async Task<ChatDemoController> CreateInitializedAsync(
        ChatPage page,
        IChatHistoryStore store,
        IGgufChatSession session,
        string modelId,
        string profileId,
        string displayName,
        string runtimeDescription,
        IApplicationFaultReporter? faultReporter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ChatDemoController? controller = null;
        try
        {
            controller = new ChatDemoController(
                page,
                store,
                session,
                modelId,
                profileId,
                displayName,
                runtimeDescription,
                faultReporter);
            await controller.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return controller;
        }
        catch
        {
            if (controller is not null)
            {
                await controller.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                await DisposeUnownedSessionAsync(session, faultReporter)
                    .ConfigureAwait(false);
            }

            throw;
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await coordinator.InitializeAsync(
            modelId,
            profileId,
            cancellationToken);
        if (coordinator.SelectedConversation is null)
        {
            await coordinator.NewChatAsync(
                modelId,
                profileId,
                cancellationToken);
        }

        Render();
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource? retirementStarter = null;
        Task retirement;
        lock (operationSync)
        {
            if (retirementTask is null)
            {
                retirementStarter = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                retirementTask = retirementStarter.Task;
            }

            retirement = retirementTask;
        }

        if (retirementStarter is not null)
        {
            _ = CompleteRetirementAsync(retirementStarter);
        }

        return new ValueTask(retirement);
    }

    private async Task CompleteRetirementAsync(TaskCompletionSource completion)
    {
        try
        {
            await RetireCoreAsync();
        }
        catch (Exception exception)
        {
            ReportRetirementFault(exception);
        }
        finally
        {
            completion.TrySetResult();
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
        try
        {
            lifetimeCancellation.Cancel();
        }
        catch (Exception exception)
        {
            HandleRetirementFailure(exception);
        }

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
            catch (Exception exception)
            {
                HandleRetirementFailure(exception);
            }
        }

        try
        {
            await Task.WhenAll(pending);
        }
        catch (Exception exception)
        {
            HandleRetirementFailure(exception);
        }

        try
        {
            renderScheduler.Dispose();
        }
        catch (Exception exception)
        {
            HandleRetirementFailure(exception);
        }

        try
        {
            await coordinator.DisposeAsync();
        }
        catch (Exception exception)
        {
            HandleRetirementFailure(exception);
        }

        try
        {
            lifetimeCancellation.Dispose();
        }
        catch (Exception exception)
        {
            HandleRetirementFailure(exception);
        }
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
        catch (Exception exception)
        {
            if (TryClassifyOperationalFailure(exception, out ChatOperationSupportCode code))
            {
                lastSupportCode = code;
            }
            else if (Interlocked.Exchange(ref operationFaultReported, 1) == 0)
            {
                faultReporter.Report(ApplicationFault.FromException(
                    ApplicationFaultCode.GgufChatOperationUnexpected,
                    exception));
            }

            TryRequestRender();
        }
    }

    private async Task RemoveOperationAsync(Task operation)
    {
        try
        {
            await operation;
        }
        catch (Exception exception)
        {
            if (Interlocked.Exchange(ref operationFaultReported, 1) == 0)
            {
                faultReporter.Report(ApplicationFault.FromException(
                    ApplicationFaultCode.GgufChatOperationUnexpected,
                    exception));
            }
        }
        finally
        {
            lock (operationSync)
            {
                activeOperations.Remove(operation);
            }
        }
    }

    private void TryRequestRender()
    {
        try
        {
            renderScheduler.Request();
        }
        catch (Exception exception)
        {
            if (Interlocked.Exchange(ref operationFaultReported, 1) == 0)
            {
                faultReporter.Report(ApplicationFault.FromException(
                    ApplicationFaultCode.GgufChatOperationUnexpected,
                    exception));
            }
        }
    }

    private void HandleRetirementFailure(Exception exception)
    {
        if (TryClassifyOperationalFailure(exception, out ChatOperationSupportCode code))
        {
            lastSupportCode = code;
            return;
        }

        ReportRetirementFault(exception);
    }

    private void ReportRetirementFault(Exception exception)
    {
        if (Interlocked.Exchange(ref retirementFaultReported, 1) == 0)
        {
            faultReporter.Report(ApplicationFault.FromException(
                ApplicationFaultCode.GgufChatRetirementUnexpected,
                exception));
        }
    }

    internal static bool TryClassifyOperationalFailure(
        Exception exception,
        out ChatOperationSupportCode code)
    {
        if (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            code = ChatOperationSupportCode.HistoryUnavailable;
            return true;
        }

        if (exception is GgufRuntimeStartupException
            or GgufWorkerPolicyException
            or GgufRuntimeTrustException
            or GgufChatLaunchException)
        {
            code = ChatOperationSupportCode.RuntimeUnavailable;
            return true;
        }

        code = default;
        return false;
    }

    private static IChatHistoryStore CreateHistoryStore()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GraniteEdgeAI",
            "ChatHistory");
        return new AtomicJsonChatHistoryStore(root);
    }

    private static async Task DisposeUnownedSessionAsync(
        IGgufChatSession session,
        IApplicationFaultReporter? faultReporter)
    {
        try
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            (faultReporter ?? BoundedApplicationFaultReporter.Shared).Report(
                ApplicationFault.FromException(
                    ApplicationFaultCode.GgufChatInitializationCleanupUnexpected,
                    exception));
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
