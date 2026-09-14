using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;

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
    public void IncompleteAssistantRemainsSavedButIsNotReplayed()
    {
        var conversation = ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow)
            .Append(ChatMessage.User("question", DateTimeOffset.UtcNow))
            .Append(ChatMessage.Assistant("unfinished answer", ChatCompletionStatus.Incomplete, DateTimeOffset.UtcNow));
        Assert.ThrowsExactly<GraniteEdgeAI.Features.ChatModels.ChatHistoryReplayException>(
            () => GgufChatSessionAdapter.CreateInitialTurns(conversation));
        Assert.AreEqual("unfinished answer", conversation.Messages[1].Content);
    }

    [TestMethod]
    public void DeliberatelyStoppedAssistantRetainsItsTextInReplay()
    {
        var conversation = ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow)
            .Append(ChatMessage.User("question", DateTimeOffset.UtcNow))
            .Append(ChatMessage.Assistant("stopped answer", ChatCompletionStatus.Stopped, DateTimeOffset.UtcNow));
        IReadOnlyList<GgufConversationTurn> turns = GgufChatSessionAdapter.CreateInitialTurns(conversation);
        Assert.HasCount(2, turns);
        Assert.AreEqual(GgufConversationRole.User, turns[0].Role);
        Assert.AreEqual("question", turns[0].Content);
        Assert.AreEqual(GgufConversationRole.Assistant, turns[1].Role);
        Assert.AreEqual("stopped answer", turns[1].Content);
        Assert.AreEqual(ChatCompletionStatus.Stopped, conversation.Messages[1].Status);
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
