using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.History;

[TestClass]
public sealed class ChatHistoryStoreTests
{
    [TestMethod]
    public async Task SavesAndReloadsEveryOrderedMessageAcrossStoreRestart()
    {
        using var root = new TemporaryDirectory();
        var store = new AtomicJsonChatHistoryStore(root.Path);
        DateTimeOffset now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        ChatConversation conversation = ChatConversation.Create(
            Guid.NewGuid(),
            "model-id",
            "cpu-profile",
            now)
            .Append(ChatMessage.User("Explain TurboQuant", now))
            .Append(ChatMessage.Assistant(
                "A quantization technique.",
                ChatCompletionStatus.Completed,
                now.AddSeconds(1)));

        await store.SaveAsync(conversation, CancellationToken.None);
        var restartedStore = new AtomicJsonChatHistoryStore(root.Path);
        ChatHistoryLoadResult load = await restartedStore.LoadAsync(
            CancellationToken.None);
        IReadOnlyList<ChatConversation> loaded = load.Conversations;

        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual("Explain TurboQuant", loaded[0].Title);
        Assert.AreEqual(2, loaded[0].Messages.Count);
        Assert.AreEqual("Explain TurboQuant", loaded[0].Messages[0].Content);
        Assert.AreEqual("A quantization technique.", loaded[0].Messages[1].Content);
        Assert.AreEqual(ChatCompletionStatus.Completed, loaded[0].Messages[1].Status);
    }

    [TestMethod]
    public async Task CorruptRecordDoesNotBlockHealthyRecordsAndDeleteIsImmediate()
    {
        using var root = new TemporaryDirectory();
        var store = new AtomicJsonChatHistoryStore(root.Path);
        ChatConversation healthy = ChatConversation.Create(
            Guid.NewGuid(),
            "model",
            "profile",
            DateTimeOffset.UtcNow);
        await store.SaveAsync(healthy, CancellationToken.None);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(root.Path, $"{Guid.NewGuid():N}.json"),
            "{broken",
            CancellationToken.None);

        ChatHistoryLoadResult load = await store.LoadAsync(CancellationToken.None);
        IReadOnlyList<ChatConversation> loaded = load.Conversations;
        Assert.AreEqual(1, loaded.Count);
        Assert.IsTrue(load.HasUnavailableRecords);

        await store.DeleteAsync(healthy.Id, CancellationToken.None);
        Assert.AreEqual(
            0,
            (await store.LoadAsync(CancellationToken.None)).Conversations.Count);
    }

    [TestMethod]
    public async Task EmptyOversizedNullAndIdentityMismatchRecordsSetBoundedSupportFact()
    {
        using var root = new TemporaryDirectory();
        var store = new AtomicJsonChatHistoryStore(root.Path);
        await File.WriteAllBytesAsync(
            System.IO.Path.Combine(root.Path, $"{Guid.NewGuid():N}.json"),
            []);
        await File.WriteAllBytesAsync(
            System.IO.Path.Combine(root.Path, $"{Guid.NewGuid():N}.json"),
            new byte[ChatHistoryPolicy.MaximumRecordBytes + 1]);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(root.Path, $"{Guid.NewGuid():N}.json"),
            "null");
        ChatConversation mismatch = ChatConversation.Create(
            Guid.NewGuid(), "model", "profile", DateTimeOffset.UtcNow);
        await store.SaveAsync(mismatch, CancellationToken.None);
        File.Move(
            System.IO.Path.Combine(root.Path, $"{mismatch.Id:N}.json"),
            System.IO.Path.Combine(root.Path, $"{Guid.NewGuid():N}.json"));

        ChatHistoryLoadResult result = await store.LoadAsync(CancellationToken.None);

        Assert.IsTrue(result.HasUnavailableRecords);
        Assert.HasCount(0, result.Conversations);
    }

    [TestMethod]
    public void SaveCleanupFailureCannotReplacePrimaryAndIsTypedWhenAlone()
    {
        var primary = new InvalidOperationException("primary-private-detail");

        InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() =>
            AtomicJsonChatHistoryStore.CompleteSave(
                primary,
                new IOException("cleanup-private-path")));
        Assert.AreSame(primary, actual);

        Assert.ThrowsExactly<ChatHistoryUnavailableException>(() =>
            AtomicJsonChatHistoryStore.CompleteSave(
                null,
                new IOException("cleanup-private-path")));
    }

    [TestMethod]
    public async Task HiddenControlAndLimitStatusRoundTripWithoutChangingTitle()
    {
        using var root = new TemporaryDirectory();
        var store = new AtomicJsonChatHistoryStore(root.Path);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ChatConversation conversation = ChatConversation.Create(
                Guid.NewGuid(), "model", "cpu", now)
            .Append(ChatMessage.User("Question", now))
            .Append(ChatMessage.Assistant(
                "Partial",
                ChatCompletionStatus.LimitReached,
                now.AddSeconds(1)))
            .Append(ChatMessage.Control(
                GgufChatCoordinator.ContinuationInstruction,
                now.AddSeconds(2)));

        await store.SaveAsync(conversation, CancellationToken.None);
        ChatConversation loaded = (await new AtomicJsonChatHistoryStore(root.Path)
            .LoadAsync(CancellationToken.None)).Conversations.Single();

        Assert.AreEqual("Question", loaded.Title);
        Assert.AreEqual(3, loaded.Messages.Count);
        Assert.AreEqual(ChatCompletionStatus.LimitReached, loaded.Messages[1].Status);
        Assert.AreEqual(ChatMessageRole.Control, loaded.Messages[2].Role);
        Assert.IsFalse(loaded.Messages[2].IsVisible);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"g1-chat-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
