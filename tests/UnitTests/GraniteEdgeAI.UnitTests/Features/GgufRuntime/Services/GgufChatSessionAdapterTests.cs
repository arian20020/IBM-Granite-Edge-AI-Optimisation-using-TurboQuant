using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;

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

    private static ChatConversation ConversationWithCompletedTurn(
        string prompt,
        string response) => ChatConversation.Create(
            Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow)
        .Append(ChatMessage.User(prompt, DateTimeOffset.UtcNow))
        .Append(ChatMessage.Assistant(
            response,
            ChatCompletionStatus.Completed,
            DateTimeOffset.UtcNow));

    private sealed class FakeRuntimeSession : IGgufChatRuntimeSession
    {
        internal bool Disposed { get; private set; }

        public async IAsyncEnumerable<GgufRuntimeEvent> GenerateAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
