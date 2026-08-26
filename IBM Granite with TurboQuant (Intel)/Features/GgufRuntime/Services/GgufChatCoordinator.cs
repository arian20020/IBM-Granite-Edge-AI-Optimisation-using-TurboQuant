using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.Features.GgufRuntime.Services;

internal sealed class GgufChatCoordinator : IAsyncDisposable
{
    internal const string ContinuationInstruction =
        "Continue from exactly where the preceding response ended. " +
        "Do not repeat text already given. Complete the answer concisely.";

    private readonly object stateSync = new();
    private readonly IChatHistoryStore store;
    private readonly IGgufChatSession session;
    private readonly TimeProvider clock;
    private readonly TimeZoneInfo timeZone;
    private readonly List<ChatConversation> conversations = [];
    private ChatConversation? selectedConversation;
    private bool isGenerating;

    internal GgufChatCoordinator(
        IChatHistoryStore store,
        IGgufChatSession session,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.timeZone = timeZone ?? throw new ArgumentNullException(nameof(timeZone));
    }

    internal IReadOnlyList<ChatConversation> Conversations
    {
        get
        {
            lock (stateSync)
            {
                return conversations.ToArray();
            }
        }
    }

    internal event EventHandler? ConversationChanged;

    internal IReadOnlyList<ChatHistoryGroup> Groups => CaptureSnapshot().Groups;

    internal ChatConversation? SelectedConversation
    {
        get
        {
            lock (stateSync)
            {
                return selectedConversation;
            }
        }
    }

    internal bool IsGenerating
    {
        get
        {
            lock (stateSync)
            {
                return isGenerating;
            }
        }
    }

    internal ChatCoordinatorSnapshot CaptureSnapshot()
    {
        lock (stateSync)
        {
            return new ChatCoordinatorSnapshot(
                ChatHistoryGrouper.Group(
                    conversations.ToArray(),
                    clock.GetUtcNow(),
                    timeZone),
                selectedConversation,
                isGenerating);
        }
    }

    internal Task InitializeAsync(CancellationToken cancellationToken) =>
        InitializeAsync(null, null, cancellationToken);

    internal async Task InitializeAsync(
        string? modelId,
        string? profileId,
        CancellationToken cancellationToken)
    {
        if ((modelId is null) != (profileId is null))
        {
            throw new ArgumentException(
                "Model and profile filters must be supplied together.");
        }

        IReadOnlyList<ChatConversation> loaded = await store.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        IEnumerable<ChatConversation> applicable = modelId is null
            ? loaded
            : loaded.Where(conversation =>
                string.Equals(conversation.ModelId, modelId, StringComparison.Ordinal) &&
                string.Equals(conversation.ProfileId, profileId, StringComparison.Ordinal));
        lock (stateSync)
        {
            conversations.Clear();
            conversations.AddRange(ChatHistoryPolicy.ApplyRetention(
                applicable,
                clock.GetUtcNow()));
            selectedConversation = conversations.FirstOrDefault();
        }

        if (selectedConversation is not null)
        {
            await session.PrepareConversationAsync(
                selectedConversation,
                cancellationToken).ConfigureAwait(false);
        }

        ConversationChanged?.Invoke(this, EventArgs.Empty);
    }

    internal async Task<ChatConversation> NewChatAsync(
        string modelId,
        string profileId,
        CancellationToken cancellationToken)
    {
        lock (stateSync)
        {
            if (isGenerating)
            {
                throw new InvalidOperationException("A new chat cannot replace an active generation.");
            }
        }

        ChatConversation conversation = ChatConversation.Create(
            Guid.NewGuid(),
            modelId,
            profileId,
            clock.GetUtcNow());
        lock (stateSync)
        {
            conversations.Insert(0, conversation);
            selectedConversation = conversation;
        }

        await store.SaveAsync(conversation, cancellationToken).ConfigureAwait(false);
        await session.PrepareConversationAsync(
            conversation,
            cancellationToken).ConfigureAwait(false);
        ConversationChanged?.Invoke(this, EventArgs.Empty);
        return conversation;
    }

    internal async Task SelectAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        ChatConversation conversation;
        lock (stateSync)
        {
            if (isGenerating)
            {
                throw new InvalidOperationException("History cannot change during generation.");
            }

            conversation = conversations.Single(item => item.Id == conversationId);
        }

        await session.PrepareConversationAsync(
            conversation,
            cancellationToken).ConfigureAwait(false);

        lock (stateSync)
        {
            selectedConversation = conversation;
        }

        ConversationChanged?.Invoke(this, EventArgs.Empty);
    }

    internal async Task SendAsync(string prompt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ChatConversation current;
        lock (stateSync)
        {
            if (isGenerating || selectedConversation is null)
            {
                throw new InvalidOperationException("The selected chat is not ready.");
            }

            isGenerating = true;
            current = selectedConversation;
        }

        try
        {
            current = current.Append(
                ChatMessage.User(prompt, clock.GetUtcNow()));
            await PublishAsync(current, persist: true, cancellationToken).ConfigureAwait(false);
            ChatMessage assistant = ChatMessage.Assistant(
                string.Empty,
                ChatCompletionStatus.Pending,
                clock.GetUtcNow());
            current = current.Append(assistant);
            await PublishAsync(current, persist: true, cancellationToken).ConfigureAwait(false);
            await GenerateIntoAssistantAsync(
                current,
                assistant,
                prompt,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (stateSync)
            {
                isGenerating = false;
            }
        }
    }

    internal async Task ContinueAsync(
        Guid assistantMessageId,
        CancellationToken cancellationToken)
    {
        ChatConversation current;
        ChatMessage assistant;
        lock (stateSync)
        {
            if (isGenerating || selectedConversation is null)
            {
                throw new InvalidOperationException("The selected chat is not ready.");
            }

            current = selectedConversation;
            assistant = current.Messages.SingleOrDefault(
                    message => message.Id == assistantMessageId)
                ?? throw new InvalidOperationException(
                    "The response does not belong to the selected chat.");
            ChatMessage? lastVisible = current.Messages.LastOrDefault(
                message => message.IsVisible);
            if (lastVisible?.Id != assistant.Id ||
                assistant.Role != ChatMessageRole.Assistant ||
                assistant.Status != ChatCompletionStatus.LimitReached)
            {
                throw new InvalidOperationException(
                    "Only the latest limited response can continue.");
            }

            isGenerating = true;
        }

        try
        {
            current = current.Append(ChatMessage.Control(
                ContinuationInstruction,
                clock.GetUtcNow()));
            await PublishAsync(current, persist: true, cancellationToken)
                .ConfigureAwait(false);
            assistant = assistant.WithContent(
                assistant.Content,
                ChatCompletionStatus.Pending);
            current = current.ReplaceMessage(assistant);
            await PublishAsync(current, persist: true, cancellationToken)
                .ConfigureAwait(false);
            await GenerateIntoAssistantAsync(
                current,
                assistant,
                ContinuationInstruction,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (stateSync)
            {
                isGenerating = false;
            }
        }
    }

    internal ValueTask StopAsync(CancellationToken cancellationToken) =>
        IsGenerating
            ? session.StopAsync(cancellationToken)
            : ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => session.DisposeAsync();

    private async Task GenerateIntoAssistantAsync(
        ChatConversation current,
        ChatMessage assistant,
        string prompt,
        CancellationToken cancellationToken)
    {
        ChatConversation? unpersistedConversation = null;
        bool requiresSessionReload = false;
        try
        {
            await foreach (GgufChatEvent runtimeEvent in
                session.GenerateAsync(prompt, cancellationToken).ConfigureAwait(false))
            {
                assistant = runtimeEvent switch
                {
                    GgufChatDelta delta => assistant.WithContent(
                        assistant.Content + delta.Text,
                        ChatCompletionStatus.Streaming),
                    GgufChatCompleted
                    {
                        Kind: GgufChatCompletionKind.Stop
                    } => assistant.WithContent(
                        assistant.Content,
                        ChatCompletionStatus.Completed),
                    GgufChatCompleted
                    {
                        Kind: GgufChatCompletionKind.Length
                    } => assistant.WithContent(
                        assistant.Content,
                        ChatCompletionStatus.LimitReached),
                    GgufChatStopped stopped => assistant.WithContent(
                        assistant.Content,
                        stopped.NeedsReload
                            ? ChatCompletionStatus.Incomplete
                            : ChatCompletionStatus.Stopped),
                    GgufChatFailed => assistant.WithContent(
                        assistant.Content,
                        ChatCompletionStatus.Failed),
                    _ => throw new InvalidOperationException(
                        "The chat event is unsupported."),
                };
                current = current.ReplaceMessage(assistant);
                bool persist = runtimeEvent is GgufChatCompleted or
                    GgufChatStopped or GgufChatFailed;
                requiresSessionReload |= runtimeEvent is GgufChatStopped
                {
                    NeedsReload: true
                };
                await PublishAsync(current, persist, cancellationToken)
                    .ConfigureAwait(false);
                unpersistedConversation = persist ? null : current;
            }

            if (requiresSessionReload)
            {
                await session.PrepareConversationAsync(
                    current,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (unpersistedConversation is not null)
            {
                await store.SaveAsync(
                    unpersistedConversation,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private async Task PublishAsync(
        ChatConversation conversation,
        bool persist,
        CancellationToken cancellationToken)
    {
        lock (stateSync)
        {
            int index = conversations.FindIndex(item => item.Id == conversation.Id);
            if (index < 0)
            {
                throw new InvalidOperationException("The conversation is not active.");
            }

            conversations[index] = conversation;
            selectedConversation = conversation;
        }

        if (persist)
        {
            await store.SaveAsync(conversation, cancellationToken).ConfigureAwait(false);
        }

        ConversationChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed record ChatCoordinatorSnapshot(
    IReadOnlyList<ChatHistoryGroup> Groups,
    ChatConversation? SelectedConversation,
    bool IsGenerating);
