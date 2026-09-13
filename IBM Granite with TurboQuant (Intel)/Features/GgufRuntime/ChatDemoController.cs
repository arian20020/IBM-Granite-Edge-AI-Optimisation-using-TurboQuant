using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ApplicationFaults;
using GraniteEdgeAI.Features.ApplicationComposition;
using GraniteEdgeAI.Features.ChatModels;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;
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
    private readonly SharedChatSessionRouter sessionRouter;
    private bool switchingModel;
    private readonly ChatRenderScheduler renderScheduler;
    private string modelId;
    private string profileId;
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
        sessionRouter = new SharedChatSessionRouter(session);
        coordinator = new GgufChatCoordinator(
            store ?? throw new ArgumentNullException(nameof(store)),
            sessionRouter,
            TimeProvider.System,
            TimeZoneInfo.Local);
        this.faultReporter = faultReporter ?? BoundedApplicationFaultReporter.Shared;
        renderScheduler = new ChatRenderScheduler(
            callback => page.DispatcherQueue.TryEnqueue(() => callback()),
            Render,
            ReportOperationFault);
        coordinator.ConversationChanged += Coordinator_ConversationChanged;
        page.NewChatRequested += Page_NewChatRequested;
        page.SendRequested += Page_SendRequested;
        page.StopRequested += Page_StopRequested;
        page.ModelSelectionPreparation = PrepareModelSelectionAsync;
        page.ContinuationRequested += Page_ContinuationRequested;
        page.ConversationSelected += Page_ConversationSelected;
        page.ConversationRenameRequested += Page_ConversationRenameRequested;
        page.ConversationDeleteRequested += Page_ConversationDeleteRequested;
        page.SetModelHeader(displayName, runtimeDescription);
    }

    internal ChatOperationSupportCode? LastSupportCode => lastSupportCode;

    private async Task<bool> PrepareModelSelectionAsync()
    {
        Task[] pending;
        lock (operationSync)
        {
            if (disposed || switchingModel) return false;
            pending = activeOperations.ToArray();
        }
        if (coordinator.IsGenerating)
        {
            if (!await page.ConfirmStopForModelSwitchAsync(lifetimeCancellation.Token)) return false;
            try { await coordinator.StopAsync(lifetimeCancellation.Token); }
              catch (InvalidOperationException stopFailure)
              {
                  // native completion can precede delivery of its terminal event to this coordinator
                  // wait briefly for that same operation; do not cancel it merely to classify Stop
                  try
                  {
                      await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(2), lifetimeCancellation.Token);
                  }
                  catch (TimeoutException)
                  {
                      System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(stopFailure).Throw();
                  }
                  if (coordinator.IsGenerating || coordinator.CaptureSnapshot().SelectedConversation?.Messages.LastOrDefault()?.Status
                      is not (ChatCompletionStatus.Completed or ChatCompletionStatus.LimitReached or ChatCompletionStatus.Stopped))
                      throw;
              }
            await Task.WhenAll(pending);
        }
        lock (operationSync)
        {
            foreach (Task operation in pending.Where(task => task.IsCompleted)) activeOperations.Remove(operation);
            return !disposed && !switchingModel && activeOperations.Count == 0 && !coordinator.IsGenerating;
        }
    }

    internal async Task SwitchModelAsync(IGgufChatSession candidate, string displayName,
        string runtimeDescription, CancellationToken cancellationToken,
        string? activeModelId = null, string? activeProfileId = null)
    {
        ChatConversation? conversation;
        lock (operationSync)
        {
            conversation = coordinator.SelectedConversation;
            if (disposed || switchingModel || activeOperations.Count != 0 || conversation is null)
                conversation = null;
            else switchingModel = true;
        }
        if (conversation is null)
        {
            await candidate.DisposeAsync();
            throw new InvalidOperationException("Finish the current chat operation before changing models.");
        }
        page.SetSessionPreparing(true);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeCancellation.Token);
        try
        {
            await sessionRouter.SwitchAsync(candidate, conversation, linked.Token);
            coordinator.AcknowledgePreparedConversation();
            coordinator.ClearFailureNotice();
            coordinator.NotifyActiveModelChanged();
            modelId = activeModelId ?? modelId;
            profileId = activeProfileId ?? profileId;
            page.SetModelHeader(displayName, runtimeDescription);
        }
        finally
        {
            lock (operationSync) switchingModel = false;
            page.SetSessionPreparing(false);
        }
    }

    internal static async Task<IGgufChatSession> CreateVerifiedSessionAsync(object authorityToken,
        GgufChatLaunchRequest request, CancellationToken cancellationToken)
    {
        A1BackendProductionAuthorities.AssertAuthorityToken(authorityToken);
        GgufRuntimeClient client = await Task.Run(() => GgufRuntimeClient.CreateFromPackage(
            request.PackageRoot, request.TrustedManifest.Span, request.ModelFile), cancellationToken);
        await request.VerifyModelAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new GgufChatSessionAdapter(client, request.Configuration);
    }

    internal static async Task<ChatDemoController> CreateInitializedSharedAsync(object authorityToken,
        ChatPage page, IGgufChatSession session, string modelId, string profileId,
        string displayName, string runtimeDescription, CancellationToken cancellationToken)
    {
        A1BackendProductionAuthorities.AssertAuthorityToken(authorityToken);
        var controller = new ChatDemoController(page, session, modelId, profileId, displayName, runtimeDescription);
        try { await controller.InitializeAsync(cancellationToken); return controller; }
        catch { await controller.DisposeAsync(); throw; }
    }

    internal static async Task<ChatDemoController> CreateInitializedProductionAsync(
        object authorityToken,
        ChatPage page,
        GgufChatLaunchRequest request,
        CancellationToken cancellationToken)
    {
        A1BackendProductionAuthorities.AssertAuthorityToken(authorityToken);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        // Package verification hashes native payloads; keep this pure work off
        // the UI thread while resuming here before creating any XAML controller.
        GgufRuntimeClient client = await Task.Run(() =>
            GgufRuntimeClient.CreateFromPackage(
                request.PackageRoot,
                request.TrustedManifest.Span,
                request.ModelFile), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await request.VerifyModelAsync(cancellationToken);
        return await CreateInitializedProductionAsync(
            page,
            request,
            () => new GgufChatSessionAdapter(client, request.Configuration),
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<ChatDemoController> CreateInitializedProductionAsync(
        ChatPage page,
        GgufChatLaunchRequest request,
        Func<IGgufChatSession> createSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(createSession);
        IGgufChatSession session = createSession()
            ?? throw new InvalidOperationException("The production chat session factory returned no session.");
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
        catch (GgufChatRuntimeUnavailableException exception)
            when (request.Configuration.Backend
                    == GraniteEdgeAI.GgufRuntime.Contracts.Configuration.GgufRuntimeBackend.Vulkan
                && exception.StartupFailure is not null
                && GgufRuntimeFailureResultMapper.IsVulkanRuntimeUnavailable(
                    exception.StartupFailure))
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
            throw new GgufChatLaunchException("vulkan-replan-required");
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
        Exception? recoveredHistory = null;
        try { await coordinator.InitializeAsync(cancellationToken); }
        catch (Exception error) when (IsHistoryReplayFailure(error))
        {
            await coordinator.NewChatAsync(modelId, profileId, cancellationToken);
            recoveredHistory = error;
        }
        if (coordinator.HistoryHadUnavailableRecords)
        {
            lastSupportCode = ChatOperationSupportCode.HistoryUnavailable;
        }
        if (coordinator.SelectedConversation is null)
        {
            await coordinator.NewChatAsync(
                modelId,
                profileId,
                cancellationToken);
        }

        Render();
        if (recoveredHistory is not null) page.ShowHistoryResumeNotice(HistoryFailureReason(recoveredHistory));
    }

    private static bool IsHistoryReplayFailure(Exception error) =>
        error is ChatHistoryReplayException or ChatHistoryCapacityException
        || error is GgufChatRuntimeUnavailableException
            { StartupFailure.Category: GraniteEdgeAI.GgufRuntime.Contracts.Failures.GgufRuntimeFailureCategory.ContextLimitReached }
        || error is GraniteEdgeAI.Features.OpenVinoRoute.OpenVinoRouteWorkerFailureException
            { SupportCode: GraniteEdgeAI.OpenVino.Contracts.OpenVinoSupportCode.RuntimeContextExceeded };

    private static string HistoryFailureReason(Exception error) => error is ChatHistoryReplayException
        ? "This conversation contains an unfinished or failed reply that cannot be replayed. Start a new chat; the saved text is preserved."
        : IsHistoryReplayFailure(error)
            ? "This conversation is too long for the active model's context. Choose a shorter conversation or start a new chat."
            : "The model could not load this conversation. Try again, or select another model.";

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
        page.ModelSelectionPreparation = null;
        page.ContinuationRequested -= Page_ContinuationRequested;
        page.ConversationSelected -= Page_ConversationSelected;
        page.ConversationRenameRequested -= Page_ConversationRenameRequested;
        page.ConversationDeleteRequested -= Page_ConversationDeleteRequested;
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
            page.SetHistoryLoading(true);
            try
            {
                await coordinator.NewChatAsync(modelId, profileId, cancellationToken);
                if (!disposed) page.CompleteConversationNavigation();
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                if (!disposed) Render();
                ReportOperationFault(error);
            }
            finally { if (!disposed) page.SetHistoryLoading(false); }
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

    private void Page_StopRequested(object? sender, EventArgs eventArguments)
    {
        page.SetStopping(true);
        TrackOperation(async cancellationToken =>
        {
            try
            {
                await coordinator.StopAsync(cancellationToken);
            }
            catch
            {
                if (!disposed) page.ShowStopFailure();
                throw;
            }
        });
    }

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

        ChatConversation? requestedConversation = coordinator.Conversations.FirstOrDefault(
            conversation => conversation.Id == conversationId);
        if (requestedConversation is null)
        {
            return;
        }

        TrackOperation(async cancellationToken =>
        {
            followLatest = true;
            renderedHistory = [];
            page.PresentPendingConversation(
                requestedConversation.Id,
                requestedConversation.Title,
                requestedConversation.Messages);
            long pendingStatusRevision = page.BeginConversationLoading();
            try
            {
                await coordinator.SelectAsync(conversationId, cancellationToken);
                if (!disposed) page.CompleteConversationNavigation();
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                if (!disposed)
                {
                    Render();
                    page.ShowHistorySelectionFailure(HistoryFailureReason(error));
                }
                if (!IsHistoryReplayFailure(error)) ReportOperationFault(error);
            }
            finally
            {
                if (!disposed)
                {
                    page.CompletePendingHistoryStatus(pendingStatusRevision);
                    page.SetHistoryEditing(false);
                }
            }
        });
    }

    private void Page_ConversationRenameRequested(object? sender, Guid id) => EditHistory(id, delete: false);

    private void Page_ConversationDeleteRequested(object? sender, Guid id) => EditHistory(id, delete: true);

    private void EditHistory(Guid id, bool delete)
    {
        lock (operationSync)
        {
            if (disposed || switchingModel || activeOperations.Count != 0 || coordinator.IsGenerating) return;
            TrackOperation(async token =>
            {
                ChatConversation? conversation = coordinator.Conversations.FirstOrDefault(item => item.Id == id);
                if (conversation is null) return;
                page.SetHistoryEditing(true);
                long pendingStatusRevision = 0;
                try
                {
                    if (delete)
                    {
                        if (await page.ConfirmDeleteConversationAsync(conversation.Title, token))
                        {
                            ChatConversation? selected = coordinator.SelectedConversation;
                            bool deletingSelected = selected?.Id == id;
                            ChatConversation? replacement = deletingSelected
                                ? coordinator.Conversations
                                    .Where(item => item.Id != id)
                                    .OrderByDescending(item => item.UpdatedUtc)
                                    .FirstOrDefault()
                                : null;
                            renderedHistory = [];
                            page.PresentPendingConversationDeletion(
                                id,
                                replacement,
                                deletingSelected);
                            pendingStatusRevision = page.BeginConversationDeletion();
                            await coordinator.DeleteAsync(id, modelId, profileId, token);
                        }
                    }
                    else if (await page.RequestConversationTitleAsync(conversation.Title, token) is { } title)
                        await coordinator.RenameAsync(id, title, token);
                    Render();
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    Render();
                    page.ShowHistoryEditFailure();
                    ReportOperationFault(error);
                }
                finally
                {
                    if (!disposed)
                    {
                        if (pendingStatusRevision != 0)
                            page.CompletePendingHistoryStatus(pendingStatusRevision);
                        page.SetHistoryEditing(false);
                        page.RestoreHistoryFocus(id);
                    }
                }
            });
        }
    }

    private void TrackOperation(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Task tracked;
        lock (operationSync)
        {
            if (disposed || switchingModel)
            {
                return;
            }

            page.SetHistoryCommandsEnabled(false);
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
            else
            {
                ReportOperationFault(exception);
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
            ReportOperationFault(exception);
        }
        finally
        {
            lock (operationSync)
            {
                activeOperations.Remove(operation);
                if (!disposed && activeOperations.Count == 0) page.SetHistoryCommandsEnabled(true);
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
            ReportOperationFault(exception);
        }
    }

    private void ReportOperationFault(Exception exception)
    {
        if (Interlocked.Exchange(ref operationFaultReported, 1) == 0)
        {
            faultReporter.Report(ApplicationFault.FromException(
                ApplicationFaultCode.GgufChatOperationUnexpected,
                exception));
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
        if (exception is ChatHistoryUnavailableException)
        {
            code = ChatOperationSupportCode.HistoryUnavailable;
            return true;
        }

        if (exception is GgufRuntimeStartupException
            or GgufWorkerPolicyException
            or GgufRuntimeTrustException
            or GgufChatRuntimeUnavailableException
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
        page.SetConversationReady(snapshot.IsConversationReady);
        page.ShowGenerationFailure(snapshot.LastFailureMessage);
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
        page.SetConversationTitle(selected.Title);
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
