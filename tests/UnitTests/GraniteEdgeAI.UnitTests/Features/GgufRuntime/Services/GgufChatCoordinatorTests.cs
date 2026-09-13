using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Services;

[TestClass]
public sealed class GgufChatCoordinatorTests
{
    [TestMethod]
    public async Task NewDraftIsVisibleWhilePreparationIsPendingButCannotSend()
    {
        var session = new PendingPreparationSession();
        var store = new MemoryStore();
        await using var coordinator = new GgufChatCoordinator(store, session, TimeProvider.System, TimeZoneInfo.Utc);
        Task<ChatConversation> pending = coordinator.NewChatAsync("model", "cpu", CancellationToken.None);
        try
        {
            Assert.IsNotNull(coordinator.SelectedConversation, "The draft must be published before preparation finishes.");
            Assert.AreEqual(0, coordinator.SelectedConversation.Messages.Count);
            Assert.AreEqual(0, store.SaveCount);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => coordinator.SendAsync("not yet", CancellationToken.None));
        }
        finally { session.Ready.TrySetResult(); await pending; }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PendingTitleCanSummarizeFirstExchangeAfterLaterMessages(bool limited)
    {
        var now = DateTimeOffset.UtcNow;
        var original = ChatConversation.Create(Guid.NewGuid(), "model", "cpu", now)
            .Append(ChatMessage.User("Explain caching", now))
            .Append(ChatMessage.Assistant("Caching reuses results.", limited ? ChatCompletionStatus.LimitReached : ChatCompletionStatus.Completed, now))
            .Append(ChatMessage.User("And memory?", now))
            .Append(ChatMessage.Assistant("Memory stores them.", ChatCompletionStatus.Completed, now));
        var store = new MemoryStore();
        await store.SaveAsync(original, CancellationToken.None);
        var session = new RetryingTitleSession("Caching Explained");
        await using var coordinator = new GgufChatCoordinator(store, session, TimeProvider.System, TimeZoneInfo.Utc);
        await coordinator.InitializeAsync(CancellationToken.None);
        await coordinator.GenerateTitleForCurrentConversationAsync(CancellationToken.None);
        Assert.AreEqual("Caching Explained", coordinator.SelectedConversation!.Title);
        CollectionAssert.AreEqual(original.Messages.ToArray(), coordinator.SelectedConversation.Messages.ToArray());
    }

    [TestMethod]
    public async Task SkippedTitleMayRetryWithoutOverwritingManualNames()
    {
        var now = DateTimeOffset.UtcNow;
        var original = ChatConversation.Create(Guid.NewGuid(), "model", "cpu", now)
            .Append(ChatMessage.User("Explain caching", now))
            .Append(ChatMessage.Assistant("Caching reuses results.", ChatCompletionStatus.Completed, now));
        var store = new MemoryStore();
        await store.SaveAsync(original, CancellationToken.None);
        var session = new RetryingTitleSession(null, "Caching Explained");
        await using var coordinator = new GgufChatCoordinator(store, session, TimeProvider.System, TimeZoneInfo.Utc);
        await coordinator.InitializeAsync(CancellationToken.None);
        await coordinator.GenerateTitleForCurrentConversationAsync(CancellationToken.None);
        Assert.AreEqual(ChatTitleOwnership.Pending, coordinator.SelectedConversation!.TitleOwnership);
        await coordinator.GenerateTitleForCurrentConversationAsync(CancellationToken.None);
        Assert.AreEqual("Caching Explained", coordinator.SelectedConversation!.Title);
        await coordinator.RenameAsync(original.Id, "My title", CancellationToken.None);
        await coordinator.GenerateTitleForCurrentConversationAsync(CancellationToken.None);
        Assert.AreEqual("My title", coordinator.SelectedConversation!.Title);
    }

    private sealed class PendingPreparationSession : IGgufChatSession
    {
        internal TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask PrepareConversationAsync(ChatConversation conversation, CancellationToken token) => await Ready.Task.WaitAsync(token);
        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token) { await Task.CompletedTask; yield break; }
        public ValueTask StopAsync(CancellationToken token) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [TestMethod]
    public async Task RestoredPendingTitleRetriesAtIdleWithoutAnotherPrompt()
    {
        var now = DateTimeOffset.UtcNow;
        var original = ChatConversation.Create(Guid.NewGuid(), "model", "cpu", now)
            .Append(ChatMessage.User("Explain caching", now))
            .Append(ChatMessage.Assistant("Caching reuses results.", ChatCompletionStatus.Completed, now));
        var store = new MemoryStore();
        await store.SaveAsync(original, CancellationToken.None);
        await using var coordinator = new GgufChatCoordinator(store, new RetryingTitleSession(null, "Caching Explained"), TimeProvider.System, TimeZoneInfo.Utc);
        var titled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.ConversationChanged += (_, _) =>
        {
            if (coordinator.SelectedConversation?.Title == "Caching Explained") titled.TrySetResult();
        };
        await coordinator.InitializeAsync(CancellationToken.None);
        await titled.Task.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.AreEqual(2, coordinator.SelectedConversation!.Messages.Count);
    }

    private sealed class RetryingTitleSession(params string?[] results) : IGgufChatSession, IBackgroundChatTitleSession
    {
        private int attempt;
        public Task<string?> TryGenerateTitleAsync(string prompt, CancellationToken token) => Task.FromResult(results[Math.Min(attempt++, results.Length - 1)]);
        public ValueTask PrepareConversationAsync(ChatConversation conversation, CancellationToken token) => ValueTask.CompletedTask;
        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token) { await Task.CompletedTask; yield break; }
        public ValueTask StopAsync(CancellationToken token) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [TestMethod]
    public async Task NewChatIsTemporaryUntilFirstPromptThenTurnsArePersistedInDisplayOrder()
    {
        var store = new MemoryStore();
        var session = new FakeSession(
            new GgufChatDelta("Hello "),
            new GgufChatDelta("there"),
            new GgufChatCompleted(GgufChatCompletionKind.Stop));
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
        Assert.AreEqual(0, store.SaveCount);
        Assert.AreEqual("New chat", created.Title);

        await coordinator.SendAsync("Say hello", CancellationToken.None);

        Assert.AreEqual(2, coordinator.SelectedConversation!.Messages.Count);
        Assert.AreEqual("Say hello", coordinator.SelectedConversation.Messages[0].Content);
        Assert.AreEqual("Hello there", coordinator.SelectedConversation.Messages[1].Content);
        Assert.AreEqual(
            ChatCompletionStatus.Completed,
            coordinator.SelectedConversation.Messages[1].Status);
        Assert.AreEqual(3, store.SaveCount);
        Assert.IsFalse(coordinator.IsGenerating);
    }

    [TestMethod]
    public async Task RepeatedNewChatRetainsDraftsButHidesThemFromHistory()
    {
        var coordinator = new GgufChatCoordinator(
            new MemoryStore(),
            new FakeSession(),
            TimeProvider.System,
            TimeZoneInfo.Utc);

        ChatConversation first = await coordinator.NewChatAsync(
            "granite", "cpu", CancellationToken.None);
        ChatConversation replacement = await coordinator.NewChatAsync(
            "granite", "cpu", CancellationToken.None);
        ChatConversation otherProfile = await coordinator.NewChatAsync(
            "granite", "vulkan", CancellationToken.None);

        CollectionAssert.AreEquivalent(
            new[] { first.Id, replacement.Id, otherProfile.Id },
            coordinator.Conversations.Select(item => item.Id).ToArray());
        Assert.AreEqual(0, coordinator.Groups.SelectMany(group => group.Conversations).Count());
        Assert.AreEqual(otherProfile.Id, coordinator.SelectedConversation!.Id);
    }

    [TestMethod]
    public async Task PersistedEmptyChatIsPreservedAfterReplacementIsPrepared()
    {
        var store = new MemoryStore();
        ChatConversation persisted = ChatConversation.Create(
            Guid.NewGuid(), "granite", "cpu", DateTimeOffset.UtcNow);
        await store.SaveAsync(persisted, CancellationToken.None);
        var coordinator = new GgufChatCoordinator(
            store,
            new FakeSession(),
            TimeProvider.System,
            TimeZoneInfo.Utc);
        await coordinator.InitializeAsync("granite", "cpu", CancellationToken.None);

        ChatConversation replacement = await coordinator.NewChatAsync(
            "granite", "cpu", CancellationToken.None);

        Assert.AreEqual(0, store.DeleteCount);
        Assert.IsTrue(store.Contains(persisted.Id));
        Assert.IsFalse(store.Contains(replacement.Id));
        Assert.AreEqual(replacement.Id, coordinator.SelectedConversation!.Id);
    }

    [TestMethod]
    public async Task FailedNewChatPreparationKeepsDraftGatedAndPreservesSavedHistory()
    {
        var store = new MemoryStore();
        ChatConversation restored = ChatConversation.Create(
                Guid.NewGuid(), "granite", "cpu", DateTimeOffset.UtcNow)
            .Append(ChatMessage.User("existing", DateTimeOffset.UtcNow));
        await store.SaveAsync(restored, CancellationToken.None);
        var coordinator = new GgufChatCoordinator(
            store,
            new FailOnSecondPrepareSession(),
            TimeProvider.System,
            TimeZoneInfo.Utc);
        await coordinator.InitializeAsync("granite", "cpu", CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.NewChatAsync("granite", "cpu", CancellationToken.None));

        Assert.AreNotEqual(restored.Id, coordinator.SelectedConversation!.Id);
        Assert.AreEqual(0, coordinator.SelectedConversation.Messages.Count);
        Assert.IsTrue(coordinator.Conversations.Any(item => item.Id == restored.Id));
        Assert.IsTrue(store.Contains(restored.Id));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => coordinator.SendAsync("not ready", CancellationToken.None));
        Assert.AreEqual(0, store.DeleteCount);
    }

    [TestMethod]
    public async Task AssistantReplacementsUseTheInjectedClock()
    {
        DateTimeOffset expected = new(2032, 4, 5, 6, 7, 8, TimeSpan.Zero);
        var coordinator = new GgufChatCoordinator(
            new MemoryStore(),
            new FakeSession(
                new GgufChatDelta("deterministic"),
                new GgufChatCompleted(GgufChatCompletionKind.Stop)),
            new FixedTimeProvider(expected),
            TimeZoneInfo.Utc);
        await coordinator.NewChatAsync("model", "cpu", CancellationToken.None);

        await coordinator.SendAsync("question", CancellationToken.None);

        Assert.AreEqual(expected, coordinator.SelectedConversation!.UpdatedUtc);
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

        Assert.AreEqual(3, store.SaveCount);
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
        Assert.AreEqual(0, snapshot.Groups.SelectMany(group => group.Conversations).Count());
        CollectionAssert.AreEquivalent(
            new[] { first.Id, second.Id },
            coordinator.Conversations
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
    public async Task LengthCompletionStoresLimitReached()
    {
        var coordinator = new GgufChatCoordinator(
            new MemoryStore(),
            new FakeSession(
                new GgufChatDelta("partial"),
                new GgufChatCompleted(GgufChatCompletionKind.Length)),
            TimeProvider.System,
            TimeZoneInfo.Utc);
        await coordinator.NewChatAsync("model", "cpu", CancellationToken.None);

        await coordinator.SendAsync("question", CancellationToken.None);

        Assert.AreEqual(
            ChatCompletionStatus.LimitReached,
            coordinator.SelectedConversation!.Messages[1].Status);
    }

    [TestMethod]
    public async Task ContinueAddsHiddenControlAndAppendsToSameAssistant()
    {
        var session = new SequencedSession(
        [
            [new GgufChatDelta("first "),
                new GgufChatCompleted(GgufChatCompletionKind.Length)],
            [new GgufChatDelta("second"),
                new GgufChatCompleted(GgufChatCompletionKind.Stop)],
        ]);
        var coordinator = new GgufChatCoordinator(
            new MemoryStore(), session, TimeProvider.System, TimeZoneInfo.Utc);
        await coordinator.NewChatAsync("model", "cpu", CancellationToken.None);
        await coordinator.SendAsync("question", CancellationToken.None);
        Guid assistantId = coordinator.SelectedConversation!.Messages[1].Id;

        await coordinator.ContinueAsync(assistantId, CancellationToken.None);

        ChatConversation result = coordinator.SelectedConversation!;
        Assert.AreEqual(3, result.Messages.Count);
        Assert.AreEqual(assistantId, result.Messages[1].Id);
        Assert.AreEqual("first second", result.Messages[1].Content);
        Assert.AreEqual(ChatCompletionStatus.Completed, result.Messages[1].Status);
        Assert.AreEqual(ChatMessageRole.Control, result.Messages[2].Role);
        Assert.AreEqual(GgufChatCoordinator.ContinuationInstruction, session.Prompts[1]);
    }

    [TestMethod]
    public async Task RepeatedLengthPermitsAnotherContinuation()
    {
        var session = new SequencedSession(
        [
            [new GgufChatDelta("one"),
                new GgufChatCompleted(GgufChatCompletionKind.Length)],
            [new GgufChatDelta(" two"),
                new GgufChatCompleted(GgufChatCompletionKind.Length)],
            [new GgufChatDelta(" three"),
                new GgufChatCompleted(GgufChatCompletionKind.Stop)],
        ]);
        var coordinator = new GgufChatCoordinator(
            new MemoryStore(), session, TimeProvider.System, TimeZoneInfo.Utc);
        await coordinator.NewChatAsync("model", "cpu", CancellationToken.None);
        await coordinator.SendAsync("question", CancellationToken.None);
        Guid assistantId = coordinator.SelectedConversation!.Messages[1].Id;

        await coordinator.ContinueAsync(assistantId, CancellationToken.None);
        Assert.AreEqual(
            ChatCompletionStatus.LimitReached,
            coordinator.SelectedConversation!.Messages[1].Status);
        await coordinator.ContinueAsync(assistantId, CancellationToken.None);

        Assert.AreEqual("one two three", coordinator.SelectedConversation!.Messages[1].Content);
        Assert.AreEqual(ChatCompletionStatus.Completed, coordinator.SelectedConversation.Messages[1].Status);
        Assert.AreEqual(2, coordinator.SelectedConversation.Messages.Count(
            message => message.Role == ChatMessageRole.Control));
    }

    [TestMethod]
    public async Task ContinuationFailureRetainsOriginalAndAppendedPartialText()
    {
        GgufChatCoordinator beforeDelta = await CreateLimitedCoordinatorAsync(
            new ContinuationFailureSession(emitContinuationDelta: false));
        Guid beforeId = beforeDelta.SelectedConversation!.Messages[1].Id;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            beforeDelta.ContinueAsync(beforeId, CancellationToken.None));
        Assert.AreEqual("original", beforeDelta.SelectedConversation.Messages[1].Content);

        GgufChatCoordinator afterDelta = await CreateLimitedCoordinatorAsync(
            new ContinuationFailureSession(emitContinuationDelta: true));
        Guid afterId = afterDelta.SelectedConversation!.Messages[1].Id;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            afterDelta.ContinueAsync(afterId, CancellationToken.None));
        Assert.AreEqual("original appended", afterDelta.SelectedConversation.Messages[1].Content);
    }

    [TestMethod]
    public async Task ContinueRejectsIneligibleTargetsBeforeInvokingRuntime()
    {
        var session = new SequencedSession(
        [
            [new GgufChatDelta("limited"),
                new GgufChatCompleted(GgufChatCompletionKind.Length)],
            [new GgufChatDelta("complete"),
                new GgufChatCompleted(GgufChatCompletionKind.Stop)],
        ]);
        var coordinator = new GgufChatCoordinator(
            new MemoryStore(), session, TimeProvider.System, TimeZoneInfo.Utc);
        await coordinator.NewChatAsync("model", "cpu", CancellationToken.None);
        await coordinator.SendAsync("first", CancellationToken.None);
        Guid nonLatest = coordinator.SelectedConversation!.Messages[1].Id;
        await coordinator.SendAsync("second", CancellationToken.None);
        ChatConversation current = coordinator.SelectedConversation!;
        Guid user = current.Messages[2].Id;
        Guid completed = current.Messages[3].Id;
        int generationCount = session.GenerationCount;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.ContinueAsync(nonLatest, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.ContinueAsync(user, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.ContinueAsync(completed, CancellationToken.None));

        Assert.AreEqual(generationCount, session.GenerationCount);
    }

    private static async Task<GgufChatCoordinator> CreateLimitedCoordinatorAsync(
        IGgufChatSession session)
    {
        var coordinator = new GgufChatCoordinator(
            new MemoryStore(), session, TimeProvider.System, TimeZoneInfo.Utc);
        await coordinator.NewChatAsync("model", "cpu", CancellationToken.None);
        await coordinator.SendAsync("question", CancellationToken.None);
        return coordinator;
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

    private sealed class SequencedSession(
        IReadOnlyList<IReadOnlyList<GgufChatEvent>> generations)
        : IGgufChatSession
    {
        internal List<string> Prompts { get; } = [];
        internal int GenerationCount { get; private set; }

        public ValueTask PrepareConversationAsync(
            ChatConversation conversation,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            IReadOnlyList<GgufChatEvent> events = generations[GenerationCount++];
            foreach (GgufChatEvent runtimeEvent in events)
            {
                yield return runtimeEvent;
                await Task.Yield();
            }
        }

        public ValueTask StopAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ContinuationFailureSession(bool emitContinuationDelta)
        : IGgufChatSession
    {
        private int generation;

        public ValueTask PrepareConversationAsync(
            ChatConversation conversation,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            if (generation++ == 0)
            {
                yield return new GgufChatDelta("original");
                yield return new GgufChatCompleted(GgufChatCompletionKind.Length);
                yield break;
            }

            if (emitContinuationDelta)
            {
                yield return new GgufChatDelta(" appended");
            }

            await Task.Yield();
            throw new InvalidOperationException("continuation failed");
        }

        public ValueTask StopAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailOnSecondPrepareSession : IGgufChatSession
    {
        private int prepareCount;

        public ValueTask PrepareConversationAsync(
            ChatConversation conversation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++prepareCount == 2)
            {
                throw new InvalidOperationException("prepare failed");
            }

            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class MemoryStore : IChatHistoryStore
    {
        private readonly Dictionary<Guid, ChatConversation> records = [];
        internal int SaveCount { get; private set; }
        internal int DeleteCount { get; private set; }
        internal ChatConversation? LastSaved { get; private set; }
        internal List<Guid> DeletedIds { get; } = [];
        internal bool Contains(Guid conversationId) => records.ContainsKey(conversationId);

        public Task<ChatHistoryLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ChatHistoryLoadResult(records.Values.ToArray(), false));

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
            DeletedIds.Add(conversationId);
            DeleteCount++;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            records.Clear();
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
