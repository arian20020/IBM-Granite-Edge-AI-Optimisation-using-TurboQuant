using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.GgufRuntime.Contracts;

namespace GraniteEdgeAI.UnitTests.Features.Onboarding;

[TestClass]
public sealed class SharedChatContinuityTests
{
    [TestMethod]
    public async Task FailedHistoryPreparationKeepsExistingRuntimeUsable()
    {
        var live = new RetainedRuntime();
        int starts = 0;
        await using var adapter = new GgufChatSessionAdapter((turns, token) =>
            ++starts == 1 ? Task.FromResult<IGgufChatRuntimeSession>(live)
                : Task.FromException<IGgufChatRuntimeSession>(new InvalidOperationException("context rejected")));
        var conversation = ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow);
        await adapter.PrepareConversationAsync(conversation, CancellationToken.None);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await adapter.PrepareConversationAsync(conversation, CancellationToken.None));
        Assert.AreEqual(0, live.Disposals, "Rejected history must not retire the usable runtime.");
    }

    private sealed class RetainedRuntime : IGgufChatRuntimeSession
    {
        internal int Disposals;
        public async IAsyncEnumerable<GraniteEdgeAI.GgufRuntime.Contracts.Events.GgufRuntimeEvent> GenerateAsync(string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token) { await Task.CompletedTask; yield break; }
        public ValueTask StopAsync(CancellationToken token) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
    [TestMethod]
    public void IncompleteAssistantIsNotSilentlyOmittedOrReplayed()
    {
        var conversation = ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow)
            .Append(ChatMessage.User("question", DateTimeOffset.UtcNow))
            .Append(ChatMessage.Assistant("unfinished answer", ChatCompletionStatus.Stopped, DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(() => GgufChatSessionAdapter.CreateInitialTurns(conversation));
        Assert.AreEqual("unfinished answer", conversation.Messages[1].Content);
    }
    [TestMethod]
    public void OversizedReplayRejectsWithoutDiscardingSavedTurns()
    {
        var conversation = ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow);
        for (int index = 0; index <= GgufProtocolLimits.MaxInitialTurns; index++)
            conversation = conversation.Append(ChatMessage.User($"turn {index}", DateTimeOffset.UtcNow));
        Assert.ThrowsExactly<GgufChatRuntimeUnavailableException>(() => GgufChatSessionAdapter.CreateInitialTurns(conversation));
        Assert.AreEqual(GgufProtocolLimits.MaxInitialTurns + 1, conversation.Messages.Count);
        Assert.AreEqual("turn 0", conversation.Messages[0].Content);
    }
}
