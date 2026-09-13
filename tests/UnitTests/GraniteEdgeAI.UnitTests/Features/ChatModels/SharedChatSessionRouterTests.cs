using System.Reflection;
using GraniteEdgeAI.Features.ChatModels;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;

namespace GraniteEdgeAI.UnitTests.Features.ChatModels;

[TestClass]
public sealed class SharedChatSessionRouterTests
{
    [TestMethod]
    [TestCategory("StopContinuation")]
    public async Task QueuedForegroundStopWaitsForTitleRestorationWithoutStoppingWrongTurn()
    {
        var session = new RestoringTitleSession();
        await using var router = new SharedChatSessionRouter(session);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Task<string?> title = router.TryGenerateTitleAsync("title", timeout.Token);
        await session.Entered.Task.WaitAsync(timeout.Token);
        var received = new List<GgufChatEvent>();
        async Task Drain()
        {
            await foreach (var item in router.GenerateAsync("not yet sent", timeout.Token)) received.Add(item);
        }
        Task generation = Drain();
        await session.StopRequested.Task.WaitAsync(timeout.Token);
        Task stopping = router.StopAsync(timeout.Token).AsTask();
        try
        {
            Assert.IsFalse(stopping.IsCompleted);
            Assert.AreEqual(0, session.NormalStops);
            Assert.AreEqual(0, session.Generations);
        }
        finally { session.Restored.TrySetResult(); }
        await Task.WhenAll(title, generation, stopping);
        Assert.HasCount(1, received);
        Assert.IsInstanceOfType<GgufChatStopped>(received[0]);
        Assert.AreEqual(0, session.Generations);
        Assert.AreEqual(0, session.NormalStops);
    }

    private sealed class RestoringTitleSession : IGgufChatSession, IGgufChatTitleSession
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource StopRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Restored = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int NormalStops;
        internal int Generations;
        public async Task<string?> GenerateTitleAsync(string prompt, Task stop, CancellationToken token)
        { Entered.TrySetResult(); await stop.WaitAsync(token); StopRequested.TrySetResult(); await Restored.Task.WaitAsync(token); return null; }
        public ValueTask PrepareConversationAsync(ChatConversation conversation, CancellationToken token) => ValueTask.CompletedTask;
        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        { Generations++; await Task.CompletedTask; yield return new GgufChatCompleted(GgufChatCompletionKind.Stop); }
        public ValueTask StopAsync(CancellationToken token) { NormalStops++; return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [TestMethod]
    public async Task LateCancellationCannotReportRollbackAfterCommittedActivation()
    {
        using var cancellation = new CancellationTokenSource();
        await using var library = new ChatModelLibrary();
        library.Register(ChatModelDescriptor.Create("model", "Model", ChatModelRoute.Gguf,
            "GGUF", "CPU", ChatModelReadiness.Ready), new CommittingTarget(cancellation));
        ChatModelSwitchResult result = await library.SwitchAsync("model", cancellation.Token);
        Assert.AreEqual(ChatModelSwitchDisposition.Activated, result.Disposition);
        Assert.AreEqual("model", library.ActiveModelId);
    }

    private sealed class CommittingTarget(CancellationTokenSource cancellation) : IChatModelActivationTarget
    {
        public ChatModelRoute Route => ChatModelRoute.Gguf;
        public ValueTask<ChatModelActivationDisposition> ActivateAsync(CancellationToken token)
        { cancellation.Cancel(); return ValueTask.FromResult(ChatModelActivationDisposition.Committed); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    [TestMethod]
    public async Task RepeatedCandidateCannotDisposeNewlyActiveSession()
    {
        await using IGgufChatSession router = CreateRouter(new RecordingSession("old"));
        var candidate = new RecordingSession("new") { PreparationRelease = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        Task first = Switch(router, candidate, Conversation());
        await candidate.Preparing.Task;
        Task second = Switch(router, candidate, Conversation());
        candidate.PreparationRelease.TrySetResult();
        await first;
        await Assert.ThrowsAsync<ArgumentException>(() => second);
        Assert.AreEqual(0, candidate.Disposals);
    }
    [TestMethod]
    public async Task DisposalCancelsPendingCandidateWithoutCommittingIt()
    {
        var old = new RecordingSession("old");
        IGgufChatSession router = CreateRouter(old);
        var candidate = new RecordingSession("new") { WaitForCancellation = true };
        Task switching = Switch(router, candidate, Conversation());
        await candidate.Preparing.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await router.DisposeAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => switching);
        Assert.AreEqual(1, old.Disposals);
        Assert.AreEqual(1, candidate.Disposals);
        await router.DisposeAsync();
        Assert.AreEqual(1, old.Disposals);
    }

    [TestMethod]
    public async Task FailedCandidateLeavesOldSessionUsableAndDisposesOnlyCandidate()
    {
        var old = new RecordingSession("old");
        await using IGgufChatSession router = CreateRouter(old);
        ChatConversation conversation = Conversation();
        await router.PrepareConversationAsync(conversation, CancellationToken.None);
        var candidate = new RecordingSession("new") { FailPreparation = true };
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Switch(router, candidate, conversation));
        var events = new List<GgufChatEvent>();
        await foreach (var item in router.GenerateAsync("continue", CancellationToken.None)) events.Add(item);
        Assert.AreEqual("old", ((GgufChatDelta)events[0]).Text);
        Assert.AreEqual(0, old.Disposals);
        Assert.AreEqual(1, candidate.Disposals);
        Assert.AreEqual(2, conversation.Messages.Count);
    }

    [TestMethod]
    public async Task SuccessfulCandidateReceivesSameOrderedHistoryBeforeOldSessionRetires()
    {
        var old = new RecordingSession("old");
        await using IGgufChatSession router = CreateRouter(old);
        ChatConversation conversation = Conversation();
        await router.PrepareConversationAsync(conversation, CancellationToken.None);
        var candidate = new RecordingSession("new");
        await Switch(router, candidate, conversation);
        Assert.AreEqual(conversation.Id, candidate.Prepared!.Id);
        CollectionAssert.AreEqual(new[] { "Remember violet", "I will remember violet" }, candidate.Prepared.Messages.Select(m => m.Content).ToArray());
        Assert.AreEqual(1, old.Disposals);
        await foreach (var item in router.GenerateAsync("continue", CancellationToken.None))
            if (item is GgufChatDelta delta) Assert.AreEqual("new", delta.Text);
    }

    private static IGgufChatSession CreateRouter(IGgufChatSession session)
    {
        Type? type = typeof(IGgufChatSession).Assembly.GetType("GraniteEdgeAI.Features.ChatModels.SharedChatSessionRouter");
        Assert.IsNotNull(type, "A shared session router must preserve chat during model changes.");
        return (IGgufChatSession)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [session], null)!;
    }

    private static Task Switch(IGgufChatSession router, IGgufChatSession candidate, ChatConversation conversation) =>
        (Task)router.GetType().GetMethod("SwitchAsync", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(router, [candidate, conversation, CancellationToken.None])!;

    private static ChatConversation Conversation() => ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow)
        .Append(ChatMessage.User("Remember violet", DateTimeOffset.UtcNow))
        .Append(ChatMessage.Assistant("I will remember violet", ChatCompletionStatus.Completed, DateTimeOffset.UtcNow));

    private sealed class RecordingSession(string answer) : IGgufChatSession
    {
        internal bool FailPreparation;
        internal bool WaitForCancellation;
        internal TaskCompletionSource Preparing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource? PreparationRelease;
        internal int Disposals;
        internal ChatConversation? Prepared;
        public async ValueTask PrepareConversationAsync(ChatConversation conversation, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (FailPreparation) throw new InvalidOperationException("Candidate unavailable.");
            Preparing.TrySetResult();
            if (PreparationRelease is not null) await PreparationRelease.Task.WaitAsync(token);
            if (WaitForCancellation) await Task.Delay(Timeout.InfiniteTimeSpan, token);
            Prepared = conversation;
        }
        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            yield return new GgufChatDelta(answer);
        }
        public ValueTask StopAsync(CancellationToken token) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
}
