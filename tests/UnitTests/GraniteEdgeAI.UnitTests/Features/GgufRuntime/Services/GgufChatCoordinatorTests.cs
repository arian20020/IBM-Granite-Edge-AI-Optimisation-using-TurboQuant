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
