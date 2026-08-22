using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Services;

[TestClass]
public sealed class GgufChatCoordinatorTests
{
    [TestMethod]
    public async Task NewChatAndEveryTurnArePersistedInDisplayOrder()
    {
        var store = new MemoryStore();
        var session = new FakeSession(
            new GgufChatDelta("Hello "),
            new GgufChatDelta("there"),
            new GgufChatCompleted());
        var coordinator = new GgufChatCoordinator(
            store,
            session,
            TimeProvider.System,
            TimeZoneInfo.Utc);

        await coordinator.InitializeAsync(CancellationToken.None);
        ChatConversation created = await coordinator.NewChatAsync(
            "granite-test",
            "cpu",
            CancellationToken.None);
        Assert.AreEqual(1, store.SaveCount);
        Assert.AreEqual("New chat", created.Title);

        await coordinator.SendAsync("Say hello", CancellationToken.None);

        Assert.AreEqual(2, coordinator.SelectedConversation!.Messages.Count);
        Assert.AreEqual("Say hello", coordinator.SelectedConversation.Messages[0].Content);
        Assert.AreEqual("Hello there", coordinator.SelectedConversation.Messages[1].Content);
        Assert.AreEqual(
            ChatCompletionStatus.Completed,
            coordinator.SelectedConversation.Messages[1].Status);
        Assert.AreEqual(4, store.SaveCount);
        Assert.IsFalse(coordinator.IsGenerating);
    }

    [TestMethod]
    public async Task UnexpectedSessionFailurePersistsLatestPartialResponseOnce()
    {
        var store = new MemoryStore();
        var coordinator = new GgufChatCoordinator(
            store,
            new ThrowingSession(),
            TimeProvider.System,
            TimeZoneInfo.Utc);
        await coordinator.NewChatAsync("model", "cpu", CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.SendAsync("long answer", CancellationToken.None));

        Assert.AreEqual(4, store.SaveCount);
        Assert.IsNotNull(store.LastSaved);
        Assert.AreEqual("partial", store.LastSaved.Messages[1].Content);
        Assert.AreEqual(
            ChatCompletionStatus.Streaming,
            store.LastSaved.Messages[1].Status);
    }

    [TestMethod]
    public async Task StopRetainsPartialAssistantResponse()
    {
        var store = new MemoryStore();
        var session = new FakeSession(
            new GgufChatDelta("partial"),
            new GgufChatStopped(NeedsReload: true));
        var coordinator = new GgufChatCoordinator(
            store,
            session,
            TimeProvider.System,
            TimeZoneInfo.Utc);
        await coordinator.NewChatAsync("model", "cpu", CancellationToken.None);

        await coordinator.SendAsync("long answer", CancellationToken.None);

        ChatMessage assistant = coordinator.SelectedConversation!.Messages[1];
        Assert.AreEqual("partial", assistant.Content);
        Assert.AreEqual(ChatCompletionStatus.Incomplete, assistant.Status);
        Assert.AreEqual(
            2,
            session.PreparedConversations.Count,
            "A stop that invalidates the CLI must rebuild it from the persisted transcript.");
        Assert.AreEqual(
            ChatCompletionStatus.Incomplete,
            session.PreparedConversations[1].Messages[1].Status);
    }

    [TestMethod]
    public async Task SnapshotKeepsSelectedConversationAndHistoryCoherent()
    {
        var coordinator = new GgufChatCoordinator(
            new MemoryStore(),
            new FakeSession(),
            TimeProvider.System,
            TimeZoneInfo.Utc);
        ChatConversation first = await coordinator.NewChatAsync(
            "first",
            "cpu",
            CancellationToken.None);
        ChatConversation second = await coordinator.NewChatAsync(
            "second",
            "cpu",
            CancellationToken.None);
        await coordinator.SelectAsync(first.Id, CancellationToken.None);

        ChatCoordinatorSnapshot snapshot = coordinator.CaptureSnapshot();

        Assert.AreEqual(first.Id, snapshot.SelectedConversation!.Id);
        CollectionAssert.AreEquivalent(
            new[] { first.Id, second.Id },
            snapshot.Groups
                .SelectMany(group => group.Conversations)
                .Select(conversation => conversation.Id)
                .ToArray());
    }

    [TestMethod]
    public async Task ConversationChangesPrepareAnIsolatedRuntimeContext()
    {
        var store = new MemoryStore();
        ChatConversation restored = ChatConversation.Create(
                Guid.NewGuid(),
                "granite-test",
                "cpu",
                DateTimeOffset.UtcNow)
            .Append(ChatMessage.User("first", DateTimeOffset.UtcNow))
            .Append(ChatMessage.Assistant(
                "answer",
                ChatCompletionStatus.Completed,
                DateTimeOffset.UtcNow));
        await store.SaveAsync(restored, CancellationToken.None);
        var session = new FakeSession();
        var coordinator = new GgufChatCoordinator(
            store,
            session,
            TimeProvider.System,
            TimeZoneInfo.Utc);

        await coordinator.InitializeAsync(CancellationToken.None);
        ChatConversation fresh = await coordinator.NewChatAsync(
            "granite-test",
            "cpu",
            CancellationToken.None);
        await coordinator.SelectAsync(restored.Id, CancellationToken.None);

        Assert.AreEqual(3, session.PreparedConversations.Count);
        Assert.AreEqual(restored.Id, session.PreparedConversations[0].Id);
        Assert.AreEqual(fresh.Id, session.PreparedConversations[1].Id);
        Assert.AreEqual(restored.Id, session.PreparedConversations[2].Id);
        Assert.AreEqual(2, session.PreparedConversations[2].Messages.Count);
    }

    [TestMethod]
    public async Task InitializationLoadsOnlyTheRequestedModelProfile()
    {
        var store = new MemoryStore();
        ChatConversation matching = ChatConversation.Create(
            Guid.NewGuid(), "granite", "cpu", DateTimeOffset.UtcNow);
        ChatConversation other = ChatConversation.Create(
            Guid.NewGuid(), "preview", "demo", DateTimeOffset.UtcNow);
        await store.SaveAsync(matching, CancellationToken.None);
        await store.SaveAsync(other, CancellationToken.None);
        var session = new FakeSession();
        var coordinator = new GgufChatCoordinator(
            store,
            session,
            TimeProvider.System,
            TimeZoneInfo.Utc);

        await coordinator.InitializeAsync(
            "granite",
            "cpu",
            CancellationToken.None);

        Assert.AreEqual(1, coordinator.Conversations.Count);
        Assert.AreEqual(matching.Id, coordinator.SelectedConversation!.Id);
        Assert.AreEqual(matching.Id, session.PreparedConversations.Single().Id);
    }

    [TestMethod]
    public async Task PreviewSessionExplainsItsBoundaryWithoutDeveloperTerminology()
    {
        await using var session = new DemoGgufChatSession();
        var output = new System.Text.StringBuilder();

        await foreach (GgufChatEvent runtimeEvent in session.GenerateAsync(
                           "Explain this model",
                           CancellationToken.None))
        {
            if (runtimeEvent is GgufChatDelta delta)
            {
                output.Append(delta.Text);
            }
        }

        string response = output.ToString();
        StringAssert.Contains(response, "Preview mode is active");
        StringAssert.Contains(response, "Explain this model");
        StringAssert.Contains(
            response,
            "Import a compatible GGUF model to run local generation");
        Assert.IsFalse(response.Contains("deterministic demo runtime", StringComparison.Ordinal));
        Assert.IsFalse(response.Contains("production path uses", StringComparison.Ordinal));
        Assert.IsFalse(response.Contains("protected GGUF CLI supervisor", StringComparison.Ordinal));
    }

    private sealed class FakeSession(params GgufChatEvent[] events) : IGgufChatSession
    {
        internal List<ChatConversation> PreparedConversations { get; } = [];

        public ValueTask PrepareConversationAsync(
            ChatConversation conversation,
            CancellationToken cancellationToken)
        {
            PreparedConversations.Add(conversation);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            foreach (GgufChatEvent runtimeEvent in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return runtimeEvent;
                await Task.Yield();
            }
        }

        public ValueTask StopAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingSession : IGgufChatSession
    {
        public ValueTask PrepareConversationAsync(
            ChatConversation conversation,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            yield return new GgufChatDelta("partial");
            await Task.Yield();
            throw new InvalidOperationException("runtime failed unexpectedly");
        }

        public ValueTask StopAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class MemoryStore : IChatHistoryStore
    {
        private readonly Dictionary<Guid, ChatConversation> records = [];
        internal int SaveCount { get; private set; }
        internal ChatConversation? LastSaved { get; private set; }

        public Task<IReadOnlyList<ChatConversation>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ChatConversation>>(records.Values.ToArray());

        public Task SaveAsync(ChatConversation conversation, CancellationToken cancellationToken)
        {
            records[conversation.Id] = conversation;
            LastSaved = conversation;
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken)
        {
            records.Remove(conversationId);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            records.Clear();
            return Task.CompletedTask;
        }
    }
}
