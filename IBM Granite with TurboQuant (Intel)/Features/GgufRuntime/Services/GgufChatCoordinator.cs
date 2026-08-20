using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.Features.GgufRuntime.Services;

internal sealed class GgufChatCoordinator : IAsyncDisposable
{
    private readonly IChatHistoryStore store;
    private readonly IGgufChatSession session;
    private readonly TimeProvider clock;
    private readonly TimeZoneInfo timeZone;
    private readonly List<ChatConversation> conversations = [];

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

    internal IReadOnlyList<ChatConversation> Conversations => conversations;

    internal IReadOnlyList<ChatHistoryGroup> Groups => ChatHistoryGrouper.Group(
        conversations,
        clock.GetUtcNow(),
        timeZone);

    internal ChatConversation? SelectedConversation { get; private set; }

    internal bool IsGenerating { get; private set; }

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ChatConversation> loaded = await store.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        conversations.Clear();
        conversations.AddRange(ChatHistoryPolicy.ApplyRetention(loaded, clock.GetUtcNow()));
        SelectedConversation = conversations.FirstOrDefault();
    }

    internal async Task<ChatConversation> NewChatAsync(
        string modelId,
        string profileId,
        CancellationToken cancellationToken)
    {
        if (IsGenerating)
        {
            throw new InvalidOperationException("A new chat cannot replace an active generation.");
        }

        ChatConversation conversation = ChatConversation.Create(
            Guid.NewGuid(),
            modelId,
            profileId,
            clock.GetUtcNow());
        conversations.Insert(0, conversation);
        SelectedConversation = conversation;
        await store.SaveAsync(conversation, cancellationToken).ConfigureAwait(false);
        return conversation;
    }

    internal void Select(Guid conversationId)
    {
        if (IsGenerating)
        {
            throw new InvalidOperationException("History cannot change during generation.");
        }

        SelectedConversation = conversations.Single(item => item.Id == conversationId);
    }

    internal async Task SendAsync(string prompt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        if (IsGenerating || SelectedConversation is null)
        {
            throw new InvalidOperationException("The selected chat is not ready.");
        }

        IsGenerating = true;
        try
        {
            ChatConversation current = SelectedConversation.Append(
                ChatMessage.User(prompt, clock.GetUtcNow()));
            await PublishAsync(current, cancellationToken).ConfigureAwait(false);
            ChatMessage assistant = ChatMessage.Assistant(
                string.Empty,
                ChatCompletionStatus.Pending,
                clock.GetUtcNow());
            current = current.Append(assistant);
            await PublishAsync(current, cancellationToken).ConfigureAwait(false);

            await foreach (GgufChatEvent runtimeEvent in
                session.GenerateAsync(prompt, cancellationToken).ConfigureAwait(false))
            {
                assistant = runtimeEvent switch
                {
                    GgufChatDelta delta => assistant.WithContent(
                        assistant.Content + delta.Text,
                        ChatCompletionStatus.Streaming),
                    GgufChatCompleted => assistant.WithContent(
                        assistant.Content,
                        ChatCompletionStatus.Completed),
                    GgufChatStopped stopped => assistant.WithContent(
                        assistant.Content,
                        stopped.NeedsReload
                            ? ChatCompletionStatus.Incomplete
                            : ChatCompletionStatus.Stopped),
                    GgufChatFailed => assistant.WithContent(
                        assistant.Content,
                        ChatCompletionStatus.Failed),
                    _ => throw new InvalidOperationException("The chat event is unsupported."),
                };
                current = current.ReplaceMessage(assistant);
                await PublishAsync(current, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            IsGenerating = false;
        }
    }

    internal ValueTask StopAsync(CancellationToken cancellationToken) =>
        IsGenerating
            ? session.StopAsync(cancellationToken)
            : ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => session.DisposeAsync();

    private async Task PublishAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken)
    {
        int index = conversations.FindIndex(item => item.Id == conversation.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("The conversation is not active.");
        }

        conversations[index] = conversation;
        SelectedConversation = conversation;
        await store.SaveAsync(conversation, cancellationToken).ConfigureAwait(false);
    }
}
