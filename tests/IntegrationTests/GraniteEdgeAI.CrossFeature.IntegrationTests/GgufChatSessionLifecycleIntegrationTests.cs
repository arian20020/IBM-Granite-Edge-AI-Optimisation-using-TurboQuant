using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class GgufChatSessionLifecycleIntegrationTests
{
    [TestMethod]
    public async Task DisposalCannotReleaseRuntimeDuringActiveGeneration()
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

        Assert.IsFalse(disposal.IsCompleted);
        Assert.IsFalse(runtime.Disposed);

        runtime.AllowCompletion();
        await generation.WaitAsync(TimeSpan.FromSeconds(5));
        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(runtime.Disposed);
    }

    [TestMethod]
    public async Task StopRemainsAvailableWhileDisposalWaitsForGeneration()
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

    private static async Task ConsumeAsync(GgufChatSessionAdapter adapter)
    {
        await foreach (GgufChatEvent _ in
            adapter.GenerateAsync("prompt", CancellationToken.None))
        {
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
