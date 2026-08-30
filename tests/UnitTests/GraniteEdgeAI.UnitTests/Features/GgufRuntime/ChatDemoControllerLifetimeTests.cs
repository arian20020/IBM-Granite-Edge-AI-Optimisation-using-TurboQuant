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

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task LifetimeCancellationReentrancyCannotStartSecondRetirement()
    {
        var session = new BlockingDisposalSession();
        ChatDemoController controller = CreateController(session);
        CancellationTokenSource lifetime = GetLifetimeCancellation(controller);
        Task? reentrantRetirement = null;
        using CancellationTokenRegistration registration = lifetime.Token.Register(
            () => reentrantRetirement = controller.DisposeAsync().AsTask());

        Task retirement = controller.DisposeAsync().AsTask();
        await session.DisposalStarted.WaitAsync(TimeSpan.FromSeconds(5));
        session.AllowDisposal();
        await retirement.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsNotNull(reentrantRetirement);
        await reentrantRetirement.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(1, session.DisposeCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task StopProgrammingFaultIsNotSwallowedDuringRetirement()
    {
        var session = new ThrowingStopSession();
        ChatDemoController controller = CreateController(session);
        SetCoordinatorGenerating(controller);

        InvalidOperationException exception =
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => controller.DisposeAsync().AsTask());

        Assert.AreEqual("Injected stop programming fault.", exception.Message);
        Assert.AreEqual(1, session.DisposeCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task EventProgrammingFaultIsReportedByRetirement()
    {
        var session = new ThrowingStopSession();
        ChatDemoController controller = CreateController(session);
        MethodInfo runOperation = typeof(ChatDemoController).GetMethod(
            "RunOperationAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "The GGUF Chat operation boundary was not found.");
        var operation = (Func<CancellationToken, Task>)(_ => Task.FromException(
            new InvalidOperationException("Injected event programming fault.")));

        await (Task)(runOperation.Invoke(
            controller,
            [operation, CancellationToken.None])
            ?? throw new InvalidOperationException("The operation task was not returned."));
        InvalidOperationException exception =
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => controller.DisposeAsync().AsTask());

        Assert.AreEqual("Injected event programming fault.", exception.Message);
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

    private static CancellationTokenSource GetLifetimeCancellation(
        ChatDemoController controller) =>
        (CancellationTokenSource)(typeof(ChatDemoController).GetField(
            "lifetimeCancellation",
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(controller)
            ?? throw new InvalidOperationException(
                "The GGUF Chat lifetime cancellation owner was not found."));

    private static void SetCoordinatorGenerating(ChatDemoController controller)
    {
        object coordinator = typeof(ChatDemoController).GetField(
            "coordinator",
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(controller)
            ?? throw new InvalidOperationException(
                "The GGUF Chat coordinator owner was not found.");
        FieldInfo isGenerating = coordinator.GetType().GetField(
            "isGenerating",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "The GGUF Chat generation state was not found.");
        isGenerating.SetValue(coordinator, true);
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

    private sealed class ThrowingStopSession : IGgufChatSession
    {
        internal int DisposeCount { get; private set; }

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
            ValueTask.FromException(
                new InvalidOperationException("Injected stop programming fault."));

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
