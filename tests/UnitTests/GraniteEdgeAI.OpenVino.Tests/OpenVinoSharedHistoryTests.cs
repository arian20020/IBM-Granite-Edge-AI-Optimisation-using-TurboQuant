using System.Reflection;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
public sealed class OpenVinoSharedHistoryTests
{
    private static readonly string[] ReplayRoles = ["user", "assistant", "user"];
    private static readonly string[] ReplayContents = ["question", "partial but finalized", "continue exactly"];
    private static readonly string[] ExpectedDeltas = ["violet"];
    [TestMethod]
    public async Task LengthLimitedReplyAndContinuationReplayIdenticallyAcrossAdapters()
    {
        var conversation = ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow)
            .Append(ChatMessage.User("question", DateTimeOffset.UtcNow))
            .Append(ChatMessage.Assistant("partial but finalized", ChatCompletionStatus.LimitReached, DateTimeOffset.UtcNow))
            .Append(ChatMessage.Control("continue exactly", DateTimeOffset.UtcNow));
        IReadOnlyList<OpenVinoInitialTurn>? observed = null;
        await using var adapter = new GraniteEdgeAI.Features.OpenVinoRoute.OpenVinoSharedChatSessionAdapter(
            (turns, sink, token) => { observed = turns; return Task.FromResult<IPromptRouteSession>(new PromptSession()); });
        await adapter.PrepareConversationAsync(conversation, CancellationToken.None);
        var gguf = GgufChatSessionAdapter.CreateInitialTurns(conversation);
        Assert.IsNotNull(observed);
        CollectionAssert.AreEqual(ReplayRoles, observed.Select(turn => turn.Role).ToArray());
        CollectionAssert.AreEqual(ReplayContents, observed.Select(turn => turn.Content).ToArray());
        CollectionAssert.AreEqual(observed.Select(turn => turn.Content).ToArray(), gguf.Select(turn => turn.Content).ToArray());
        Assert.AreEqual(ChatCompletionStatus.LimitReached, conversation.Messages[1].Status);
    }
    [TestMethod]
    public async Task OversizedHistoryTurnRejectsBeforeStartingWorker()
    {
        bool started = false;
        await using var adapter = new GraniteEdgeAI.Features.OpenVinoRoute.OpenVinoSharedChatSessionAdapter(
            (turns, sink, token) => { started = true; return Task.FromResult<IPromptRouteSession>(new PromptSession()); });
        var conversation = ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow)
            .Append(ChatMessage.User(new string('x', 65537), DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await adapter.PrepareConversationAsync(conversation, CancellationToken.None));
        Assert.IsFalse(started);
        Assert.AreEqual(65537, conversation.Messages[0].Content.Length);
    }
    [TestMethod]
    public async Task DifferentTurnDeltaCannotEnterCurrentReply()
    {
        var session = new PromptSession { EmitWrongTurn = true };
        await using var adapter = new GraniteEdgeAI.Features.OpenVinoRoute.OpenVinoSharedChatSessionAdapter(
            (turns, sink, token) => { session.Sink = sink; return Task.FromResult<IPromptRouteSession>(session); });
        await adapter.PrepareConversationAsync(ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow), CancellationToken.None);
        var deltas = new List<string>();
        await foreach (var item in adapter.GenerateAsync("continue", CancellationToken.None))
            if (item is GgufChatDelta delta) deltas.Add(delta.Text);
        CollectionAssert.AreEqual(ExpectedDeltas, deltas);
    }
    [TestMethod]
    public async Task RetiredSessionDeltaCannotEnterNewConversation()
    {
        Action<PromptEvent>? retiredSink = null;
        var live = new PromptSession();
        int preparations = 0;
        Func<IReadOnlyList<OpenVinoInitialTurn>, Action<PromptEvent>, CancellationToken, Task<IPromptRouteSession>> factory = (turns, sink, token) =>
        {
            if (preparations++ == 0) { retiredSink = sink; return Task.FromResult<IPromptRouteSession>(new PromptSession()); }
            live.Sink = sink;
            live.BeforeGenerate = () => retiredSink!(new(PromptEventKind.TextDelta, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "stale", null, "CPU", ["CPU"]));
            return Task.FromResult<IPromptRouteSession>(live);
        };
        Type type = typeof(IGgufChatSession).Assembly.GetType("GraniteEdgeAI.Features.OpenVinoRoute.OpenVinoSharedChatSessionAdapter")!;
        await using var adapter = (IGgufChatSession)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [factory], null)!;
        var conversation = ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow);
        await adapter.PrepareConversationAsync(conversation, CancellationToken.None);
        await adapter.PrepareConversationAsync(conversation, CancellationToken.None);
        var deltas = new List<string>();
        await foreach (var item in adapter.GenerateAsync("continue", CancellationToken.None)) if (item is GgufChatDelta delta) deltas.Add(delta.Text);
        CollectionAssert.AreEqual(ExpectedDeltas, deltas);
    }
    [TestMethod]
    public async Task SharedAdapterReplaysRolesAndStreamsActualPromptEvents()
    {
        IReadOnlyList<OpenVinoInitialTurn>? observed = null;
        var session = new PromptSession();
        Func<IReadOnlyList<OpenVinoInitialTurn>, Action<PromptEvent>, CancellationToken, Task<IPromptRouteSession>> factory = (turns, sink, token) =>
        { observed = turns; session.Sink = sink; return Task.FromResult<IPromptRouteSession>(session); };
        Type? type = typeof(IGgufChatSession).Assembly.GetType("GraniteEdgeAI.Features.OpenVinoRoute.OpenVinoSharedChatSessionAdapter");
        Assert.IsNotNull(type, "OpenVINO must participate in the same conversation/session contract.");
        await using var adapter = (IGgufChatSession)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [factory], null)!;
        var conversation = ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow)
            .Append(ChatMessage.User("violet", DateTimeOffset.UtcNow))
            .Append(ChatMessage.Assistant("remembered", ChatCompletionStatus.Completed, DateTimeOffset.UtcNow));
        await adapter.PrepareConversationAsync(conversation, CancellationToken.None);
        Assert.AreEqual("assistant", observed![1].Role);
        Assert.AreEqual("remembered", observed[1].Content);
        var output = new List<GgufChatEvent>();
        await foreach (var item in adapter.GenerateAsync("continue", CancellationToken.None)) output.Add(item);
        Assert.AreEqual("continue", session.LastPrompt);
        Assert.AreEqual(512, session.LastTokens);
        Assert.AreEqual("violet", ((GgufChatDelta)output[0]).Text);
        Assert.IsInstanceOfType<GgufChatCompleted>(output[1]);
    }

    private sealed class PromptSession : IPromptRouteSession
    {
        internal Action<PromptEvent>? Sink;
        internal string? LastPrompt;
        internal int LastTokens;
        internal Action? BeforeGenerate;
        internal bool EmitWrongTurn;
        public PromptRouteCapability Capability => GraniteEdgeAI.Features.OpenVinoRoute.OpenVinoRouteCapability.PromptCapability;
        public Task<PromptTurnResult> GenerateAsync(string prompt, int tokens, CancellationToken token)
        {
            LastPrompt = prompt;
            LastTokens = tokens;
            BeforeGenerate?.Invoke();
            Guid operation = Guid.NewGuid(), session = Guid.NewGuid(), turn = Guid.NewGuid();
            Sink!(new(PromptEventKind.GeneratingTurn, operation, session, turn, null, null, "CPU", ["CPU"]));
            if (EmitWrongTurn)
                Sink!(new(PromptEventKind.TextDelta, operation, session, Guid.NewGuid(), "stale turn", null, "CPU", ["CPU"]));
            Sink!(new(PromptEventKind.TextDelta, operation, session, turn, "violet", null, "CPU", ["CPU"]));
            return Task.FromResult(new PromptTurnResult(PromptTurnStatus.Completed, "violet", 10, 1, null));
        }
        public Task StopAsync(CancellationToken token) => Task.CompletedTask;
        public Task CancelAsync(CancellationToken token) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken token) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
