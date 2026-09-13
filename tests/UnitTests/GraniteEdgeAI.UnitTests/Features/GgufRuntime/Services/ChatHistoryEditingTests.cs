using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Services;

[TestClass]
public sealed class ChatHistoryEditingTests
{
    [TestMethod]
    public async Task AtomicStoreRoundTripsRenameAndDeletion()
    {
        string directory = Path.Combine(Path.GetTempPath(), "granite-history-edit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new AtomicJsonChatHistoryStore(directory);
            await using var coordinator = new GgufChatCoordinator(store, new Session(), TimeProvider.System, TimeZoneInfo.Utc);
            var chat = await coordinator.NewChatAsync("model", "profile", CancellationToken.None);
            await coordinator.RenameAsync(chat.Id, "Persisted name", CancellationToken.None);
            var reloaded = await new AtomicJsonChatHistoryStore(directory).LoadAsync(CancellationToken.None);
            Assert.AreEqual("Persisted name", reloaded.Conversations.Single().Title);
            await coordinator.DeleteAsync(chat.Id, "model", "profile", CancellationToken.None);
            Assert.AreEqual(0, (await new AtomicJsonChatHistoryStore(directory).LoadAsync(CancellationToken.None)).Conversations.Count);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
    [TestMethod]
    public async Task RenamePersistsTrimmedTitleAndSurvivesNewChatAndReload()
    {
        var store = new Store();
        await using var coordinator = Create(store);
        var chat = await coordinator.NewChatAsync("model", "profile", CancellationToken.None);
        await coordinator.RenameAsync(chat.Id, "  Saved notes  ", CancellationToken.None);
        await coordinator.NewChatAsync("model", "profile", CancellationToken.None);
        Assert.AreEqual("Saved notes", coordinator.Conversations.Single(item => item.Id == chat.Id).Title);
        await using var reloaded = Create(store);
        await reloaded.InitializeAsync(CancellationToken.None);
        Assert.AreEqual("Saved notes", reloaded.SelectedConversation!.Title);
    }

    [TestMethod]
    public async Task RenameFailureLeavesOriginalTitleAndMessages()
    {
        var store = new Store();
        await using var coordinator = Create(store);
        var chat = await coordinator.NewChatAsync("model", "profile", CancellationToken.None);
        store.Fail = true;
        await Assert.ThrowsExactlyAsync<IOException>(() => coordinator.RenameAsync(chat.Id, "Changed", CancellationToken.None));
        Assert.AreSame(chat, coordinator.SelectedConversation);
        await Assert.ThrowsAsync<ArgumentException>(() => coordinator.RenameAsync(chat.Id, "  ", CancellationToken.None));
    }

    [TestMethod]
    public async Task DeleteSelectedFailureRestoresSessionAndSuccessfulDeleteUsesActiveIdentity()
    {
        var store = new Store();
        var session = new Session();
        await using var coordinator = Create(store, session);
        var chat = await coordinator.NewChatAsync("old", "old-profile", CancellationToken.None);
        await coordinator.RenameAsync(chat.Id, "Saved", CancellationToken.None);
        store.Fail = true;
        await Assert.ThrowsExactlyAsync<IOException>(() => coordinator.DeleteAsync(chat.Id, "new-model", "new-profile", CancellationToken.None));
        Assert.AreEqual(chat.Id, coordinator.SelectedConversation!.Id);
        Assert.AreEqual(chat.Id, session.Prepared!.Id);
        store.Fail = false;
        await coordinator.DeleteAsync(chat.Id, "new-model", "new-profile", CancellationToken.None);
        Assert.AreEqual("new-model", coordinator.SelectedConversation!.ModelId);
        Assert.AreEqual("new-profile", coordinator.SelectedConversation.ProfileId);
        Assert.AreEqual(0, (await store.LoadAsync(CancellationToken.None)).Conversations.Count);
    }

    [TestMethod]
    public async Task DeleteUnselectedLeavesCurrentSessionAndConversationUntouched()
    {
        var store = new Store(); var session = new Session();
        await using var coordinator = Create(store, session);
        var first = await coordinator.NewChatAsync("one", "profile", CancellationToken.None);
        await coordinator.RenameAsync(first.Id, "First", CancellationToken.None);
        var second = await coordinator.NewChatAsync("two", "profile", CancellationToken.None);
        int preparations = session.Preparations;
        await coordinator.DeleteAsync(first.Id, "two", "profile", CancellationToken.None);
        Assert.AreEqual(second.Id, coordinator.SelectedConversation!.Id);
        Assert.AreEqual(preparations, session.Preparations);
    }

    [TestMethod]
    public async Task PendingHistoryWritePreventsGenerationAndConcurrentDeletion()
    {
        var store = new Store(); await using var coordinator = Create(store);
        var chat = await coordinator.NewChatAsync("model", "profile", CancellationToken.None);
        store.Pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task rename = coordinator.RenameAsync(chat.Id, "Name", CancellationToken.None);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => coordinator.SendAsync("Hi", CancellationToken.None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => coordinator.DeleteAsync(chat.Id, "model", "profile", CancellationToken.None));
        store.Pending.SetResult(); await rename;
        Assert.AreEqual("Name", coordinator.SelectedConversation!.Title);
    }

    private static GgufChatCoordinator Create(Store store, Session? session = null) => new(store, session ?? new Session(), TimeProvider.System, TimeZoneInfo.Utc);
    private sealed class Store : IChatHistoryStore
    {
        private readonly Dictionary<Guid, ChatConversation> records = [];
        internal bool Fail; internal TaskCompletionSource? Pending;
        public Task<ChatHistoryLoadResult> LoadAsync(CancellationToken token) => Task.FromResult(new ChatHistoryLoadResult(records.Values.ToArray(), false));
        public async Task SaveAsync(ChatConversation conversation, CancellationToken token)
        { if (Fail) throw new IOException(); if (Pending is { } pending) await pending.Task; records[conversation.Id] = conversation; }
        public Task DeleteAsync(Guid id, CancellationToken token) { if (Fail) throw new IOException(); records.Remove(id); return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken token) { records.Clear(); return Task.CompletedTask; }
    }
    private sealed class Session : IGgufChatSession
    {
        internal ChatConversation? Prepared; internal int Preparations;
        public ValueTask PrepareConversationAsync(ChatConversation chat, CancellationToken token) { Prepared = chat; Preparations++; return ValueTask.CompletedTask; }
        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        { await Task.CompletedTask; yield return new GgufChatCompleted(GgufChatCompletionKind.Stop); }
        public ValueTask StopAsync(CancellationToken token) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
