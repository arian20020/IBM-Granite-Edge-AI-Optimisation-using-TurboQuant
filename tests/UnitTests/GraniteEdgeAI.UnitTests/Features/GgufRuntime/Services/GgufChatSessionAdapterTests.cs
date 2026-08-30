using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Services;

[TestClass]
public sealed class GgufChatSessionAdapterTests
{
    [TestMethod]
    public async Task PreparingConversationRestartsRuntimeWithDurableTurns()
    {
        var startedTurns = new List<IReadOnlyList<GgufConversationTurn>>();
        var sessions = new List<FakeRuntimeSession>();
        var adapter = new GgufChatSessionAdapter((turns, cancellationToken) =>
        {
            var session = new FakeRuntimeSession();
            startedTurns.Add(turns);
            sessions.Add(session);
            return Task.FromResult<IGgufChatRuntimeSession>(session);
        });
        ChatConversation first = ConversationWithCompletedTurn("one", "first answer");
        ChatConversation second = ConversationWithCompletedTurn("two", "second answer");

        await adapter.PrepareConversationAsync(first, CancellationToken.None);
        await adapter.PrepareConversationAsync(second, CancellationToken.None);

        Assert.AreEqual(2, startedTurns.Count);
        Assert.AreEqual(GgufConversationRole.User, startedTurns[1][0].Role);
        Assert.AreEqual("two", startedTurns[1][0].Content);
        Assert.AreEqual(GgufConversationRole.Assistant, startedTurns[1][1].Role);
        Assert.AreEqual("second answer", startedTurns[1][1].Content);
        Assert.IsTrue(sessions[0].Disposed);
        Assert.IsFalse(sessions[1].Disposed);
    }

    [TestMethod]
    public async Task PreparingConversationExcludesUnfinishedEmptyAssistant()
    {
        ChatConversation conversation = ChatConversation.Create(
                Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow)
            .Append(ChatMessage.User("question", DateTimeOffset.UtcNow))
            .Append(ChatMessage.Assistant(
                string.Empty,
                ChatCompletionStatus.Pending,
                DateTimeOffset.UtcNow));
        IReadOnlyList<GgufConversationTurn>? captured = null;
        var adapter = new GgufChatSessionAdapter((turns, cancellationToken) =>
        {
            captured = turns;
            return Task.FromResult<IGgufChatRuntimeSession>(new FakeRuntimeSession());
        });

        await adapter.PrepareConversationAsync(conversation, CancellationToken.None);

        Assert.IsNotNull(captured);
        Assert.AreEqual(1, captured.Count);
        Assert.AreEqual("question", captured[0].Content);
    }

    [TestMethod]
    public async Task PreparingConversationReplaysHiddenControlAsRuntimeUser()
    {
        ChatConversation conversation = ChatConversation.Create(
                Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow)
            .Append(ChatMessage.User("question", DateTimeOffset.UtcNow))
            .Append(ChatMessage.Assistant(
                "partial",
                ChatCompletionStatus.LimitReached,
                DateTimeOffset.UtcNow))
            .Append(ChatMessage.Control(
                GgufChatCoordinator.ContinuationInstruction,
                DateTimeOffset.UtcNow));
        IReadOnlyList<GgufConversationTurn>? captured = null;
        var adapter = new GgufChatSessionAdapter((turns, cancellationToken) =>
        {
            captured = turns;
            return Task.FromResult<IGgufChatRuntimeSession>(new FakeRuntimeSession());
        });

        await adapter.PrepareConversationAsync(conversation, CancellationToken.None);

        Assert.IsNotNull(captured);
        Assert.AreEqual(3, captured.Count);
        Assert.AreEqual(GgufConversationRole.User, captured[2].Role);
        Assert.AreEqual(
            GgufChatCoordinator.ContinuationInstruction,
            captured[2].Content);
    }

    [TestMethod]
    public async Task GenerateMapsStopCompletionIntoChatDomain()
    {
        GgufChatEvent result = await GenerateCompletionAsync(GgufCompletionReason.Stop);

        Assert.AreEqual(
            GgufChatCompletionKind.Stop,
            Assert.IsInstanceOfType<GgufChatCompleted>(result).Kind);
    }

    [TestMethod]
    public async Task GenerateMapsLengthCompletionIntoChatDomain()
    {
        GgufChatEvent result = await GenerateCompletionAsync(GgufCompletionReason.Length);

        Assert.AreEqual(
            GgufChatCompletionKind.Length,
            Assert.IsInstanceOfType<GgufChatCompleted>(result).Kind);
    }

    [TestMethod]
    public async Task DisposeWaitsForActiveGenerationBeforeDisposingRuntimeSession()
    {
        var runtime = new BlockingRuntimeSession();
        var adapter = new GgufChatSessionAdapter((turns, cancellationToken) =>
            Task.FromResult<IGgufChatRuntimeSession>(runtime));
        await adapter.PrepareConversationAsync(
            ChatConversation.Create(
                Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow),
            CancellationToken.None);
        Task generation = ConsumeAsync(adapter);
        await runtime.Started.WaitAsync(TimeSpan.FromSeconds(5));

        Task disposal = adapter.DisposeAsync().AsTask();
        await Task.Delay(100);

        Assert.IsFalse(
            disposal.IsCompleted,
            "Disposal completed while runtime generation was active.");
        Assert.IsFalse(runtime.Disposed);

        runtime.AllowCompletion();
        await generation.WaitAsync(TimeSpan.FromSeconds(5));
        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(runtime.Disposed);
    }

    [TestMethod]
    public async Task StopCanInterruptGenerationWhileDisposalIsWaiting()
    {
        var runtime = new BlockingRuntimeSession();
        var adapter = new GgufChatSessionAdapter((turns, cancellationToken) =>
            Task.FromResult<IGgufChatRuntimeSession>(runtime));
        await adapter.PrepareConversationAsync(
            ChatConversation.Create(
                Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow),
            CancellationToken.None);
        Task generation = ConsumeAsync(adapter);
        await runtime.Started.WaitAsync(TimeSpan.FromSeconds(5));
        Task disposal = adapter.DisposeAsync().AsTask();

        await adapter.StopAsync(CancellationToken.None);

        await generation.WaitAsync(TimeSpan.FromSeconds(5));
        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(runtime.StopRequested);
        Assert.IsTrue(runtime.Disposed);
    }

    private static async Task<GgufChatEvent> GenerateCompletionAsync(
        GgufCompletionReason reason)
    {
        var runtime = new FakeRuntimeSession(
            new ResponseCompletedEvent(
                GgufProtocolVersion.Current,
                Guid.NewGuid(),
                new GgufSessionId(Guid.NewGuid()),
                0,
                reason));
        var adapter = new GgufChatSessionAdapter((turns, cancellationToken) =>
            Task.FromResult<IGgufChatRuntimeSession>(runtime));
        await adapter.PrepareConversationAsync(
            ChatConversation.Create(
                Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow),
            CancellationToken.None);
        var events = new List<GgufChatEvent>();
        await foreach (GgufChatEvent chatEvent in
            adapter.GenerateAsync("prompt", CancellationToken.None))
        {
            events.Add(chatEvent);
        }

        return events.Single();
    }

    private static ChatConversation ConversationWithCompletedTurn(
        string prompt,
        string response) => ChatConversation.Create(
            Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow)
        .Append(ChatMessage.User(prompt, DateTimeOffset.UtcNow))
        .Append(ChatMessage.Assistant(
            response,
            ChatCompletionStatus.Completed,
            DateTimeOffset.UtcNow));

    private static async Task ConsumeAsync(GgufChatSessionAdapter adapter)
    {
        await foreach (GgufChatEvent _ in
            adapter.GenerateAsync("prompt", CancellationToken.None))
        {
        }
    }

    private sealed class FakeRuntimeSession(params GgufRuntimeEvent[] events)
        : IGgufChatRuntimeSession
    {
        internal bool Disposed { get; private set; }

        public async IAsyncEnumerable<GgufRuntimeEvent> GenerateAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            foreach (GgufRuntimeEvent runtimeEvent in events)
            {
                yield return runtimeEvent;
                await Task.Yield();
            }
        }

        public ValueTask StopAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingRuntimeSession : IGgufChatRuntimeSession
    {
        private readonly TaskCompletionSource started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Started => started.Task;
        internal bool StopRequested { get; private set; }
        internal bool Disposed { get; private set; }

        internal void AllowCompletion() => completion.TrySetResult();

        public async IAsyncEnumerable<GgufRuntimeEvent> GenerateAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            started.TrySetResult();
            await completion.Task.WaitAsync(cancellationToken);
            yield return new ResponseCompletedEvent(
                GgufProtocolVersion.Current,
                Guid.NewGuid(),
                new GgufSessionId(Guid.NewGuid()),
                0,
                GgufCompletionReason.Stop);
        }

        public ValueTask StopAsync(CancellationToken cancellationToken)
        {
            StopRequested = true;
            completion.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            completion.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}
