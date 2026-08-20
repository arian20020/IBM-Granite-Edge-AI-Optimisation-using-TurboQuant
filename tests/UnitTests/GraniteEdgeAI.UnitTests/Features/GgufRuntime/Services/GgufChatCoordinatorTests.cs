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
        Assert.IsTrue(store.SaveCount >= 5);
        Assert.IsFalse(coordinator.IsGenerating);
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
    }

    private sealed class FakeSession(params GgufChatEvent[] events) : IGgufChatSession
    {
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

    private sealed class MemoryStore : IChatHistoryStore
    {
        private readonly Dictionary<Guid, ChatConversation> records = [];
        internal int SaveCount { get; private set; }

        public Task<IReadOnlyList<ChatConversation>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ChatConversation>>(records.Values.ToArray());

        public Task SaveAsync(ChatConversation conversation, CancellationToken cancellationToken)
        {
            records[conversation.Id] = conversation;
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
