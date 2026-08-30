using System.Reflection;
using System.Runtime.CompilerServices;
using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime;

[TestClass]
public sealed class ChatDemoControllerLifetimeTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ConcurrentRetirementCallersAwaitTheSameSessionDisposal()
    {
        var session = new BlockingDisposalSession();
        ChatDemoController controller = CreateController(session);

        Task firstRetirement = controller.DisposeAsync().AsTask();
        await session.DisposalStarted.WaitAsync(TimeSpan.FromSeconds(5));
        Task secondRetirement = controller.DisposeAsync().AsTask();

        Assert.IsFalse(firstRetirement.IsCompleted);
        Assert.IsFalse(
            secondRetirement.IsCompleted,
            "Every teardown caller must await the one in-progress retirement task.");

        session.AllowDisposal();
        await Task.WhenAll(firstRetirement, secondRetirement)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, session.DisposeCount);
    }

    private static ChatDemoController CreateController(IGgufChatSession session)
    {
        ConstructorInfo constructor = typeof(ChatDemoController).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [
                typeof(ChatPage),
                typeof(IGgufChatSession),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string)
            ],
            modifiers: null) ?? throw new InvalidOperationException(
                "The GGUF Chat lifetime owner constructor was not found.");
        return (ChatDemoController)constructor.Invoke(
            [new ChatPage(), session, "model", "cpu", "Model", "Runtime"]);
    }

    private sealed class BlockingDisposalSession : IGgufChatSession
    {
        private readonly TaskCompletionSource disposalStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource disposalAllowed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task DisposalStarted => disposalStarted.Task;
        internal int DisposeCount { get; private set; }

        internal void AllowDisposal() => disposalAllowed.TrySetResult();

        public ValueTask PrepareConversationAsync(
            ChatConversation conversation,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(
            string prompt,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            disposalStarted.TrySetResult();
            await disposalAllowed.Task;
        }
    }
}
