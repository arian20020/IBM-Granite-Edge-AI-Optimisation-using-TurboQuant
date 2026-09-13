using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.Prompting;

namespace GraniteEdgeAI.UnitTests.Features.OpenVinoRoute.Presentation;

[TestClass]
public sealed class OpenVinoSharedChatSessionAdapterTests
{
    [TestMethod]
    [TestCategory("StopConfirmation")]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public async Task TerminalBeforeConfirmationReleasesStopWithoutSendingControl(bool failed, bool dispose)
    {
        var runtime = new ConfirmingSession();
        await using var adapter = new OpenVinoSharedChatSessionAdapter((_, sink, _) =>
        { runtime.Sink = sink; return Task.FromResult<IPromptRouteSession>(runtime); });
        await adapter.PrepareConversationAsync(ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow), CancellationToken.None);
        async Task Drain() { await foreach (var item in adapter.GenerateAsync("prompt", CancellationToken.None)) { } }
        Task generation = Drain();
        Task stopping = adapter.StopAsync(CancellationToken.None).AsTask();
        if (dispose) await adapter.DisposeAsync();
        else runtime.Complete(failed);
        await Task.WhenAll(generation, stopping).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(0, runtime.Stops);
        Assert.AreEqual(0, runtime.Cancels);
    }

    [TestMethod]
    [TestCategory("StopConfirmation")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StopWaitsForConfirmedTurnAndNeverUsesCancel(bool terminalWins)
    {
        var runtime = new ConfirmingSession { TerminalWins = terminalWins };
        await using var adapter = new OpenVinoSharedChatSessionAdapter((_, sink, _) =>
        { runtime.Sink = sink; return Task.FromResult<IPromptRouteSession>(runtime); });
        await adapter.PrepareConversationAsync(ChatConversation.Create(Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow), CancellationToken.None);
        async Task Drain() { await foreach (var item in adapter.GenerateAsync("prompt", CancellationToken.None)) { } }
        Task generation = Drain();
        Task stopping = adapter.StopAsync(CancellationToken.None).AsTask();
        try
        {
            Assert.AreEqual(0, runtime.Stops);
            Assert.IsFalse(stopping.IsCompleted);
        }
        finally { runtime.Confirm(); }
        await stopping.WaitAsync(TimeSpan.FromSeconds(2));
        await generation.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(runtime.Turn, runtime.StoppedTurn);
        Assert.AreEqual(1, runtime.Stops);
        Assert.AreEqual(0, runtime.Cancels);
    }

    private sealed class ConfirmingSession : IPromptRouteSession
    {
        internal Action<PromptEvent> Sink = null!;
        internal readonly Guid Turn = Guid.NewGuid();
        private readonly Guid session = Guid.NewGuid();
        private readonly Guid operation = Guid.NewGuid();
        private readonly TaskCompletionSource<PromptTurnResult> terminal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Stops;
        internal int Cancels;
        internal Guid? StoppedTurn;
        internal bool TerminalWins;
        public PromptRouteCapability Capability => new(PromptRouteKind.OpenVino, "ov", "profile", "OpenVINO", "CPU", "test", 4096, 32, 4096);
        public Task<PromptTurnResult> GenerateAsync(string prompt, int requested, CancellationToken token)
        { Sink(new(PromptEventKind.GeneratingTurn, operation, session, Turn, null, null, "CPU", ["CPU"])); return terminal.Task; }
        internal void Confirm() => Sink(new(PromptEventKind.GenerationConfirmed, operation, session, Turn, null, null, "CPU", ["CPU"]));
        internal void Complete(bool failed = false) => terminal.TrySetResult(new(
            failed ? PromptTurnStatus.Failed : PromptTurnStatus.Completed, "done", 1, 1,
            failed ? new PromptFailure("runtime_failed", "failed", "reload") : null));
        public Task StopActiveTurnAsync(Guid turn, CancellationToken token)
        {
            StoppedTurn = turn;
            if (TerminalWins)
            {
                Stops++;
                Complete();
                throw new InvalidOperationException("The confirmed turn completed before Stop.");
            }
            return StopAsync(token);
        }
        public Task StopAsync(CancellationToken token)
        { Stops++; terminal.TrySetResult(new(PromptTurnStatus.Stopped, "partial", 1, 1, null)); return Task.CompletedTask; }
        public Task CancelAsync(CancellationToken token) { Cancels++; return Task.CompletedTask; }
        public Task CloseAsync(CancellationToken token) => Task.CompletedTask;
        public ValueTask DisposeAsync() { terminal.TrySetResult(new(PromptTurnStatus.Stopped, "", 1, 0, null)); return ValueTask.CompletedTask; }
    }
}
