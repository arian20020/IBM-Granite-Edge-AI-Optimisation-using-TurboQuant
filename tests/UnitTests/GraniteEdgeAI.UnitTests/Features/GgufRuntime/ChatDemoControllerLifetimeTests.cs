using System.Reflection;
using System.Runtime.CompilerServices;
using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.ApplicationFaults;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;
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

        Assert.AreSame(firstRetirement, secondRetirement);
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
    public async Task StopProgrammingFaultIsReportedOnceAndRetirementStillCompletes()
    {
        var session = new ThrowingStopSession();
        var reporter = new BoundedApplicationFaultReporter(4);
        ChatDemoController controller = CreateController(session, reporter);
        SetCoordinatorGenerating(controller);

        await controller.DisposeAsync();

        Assert.AreEqual(1, session.DisposeCount);
        IReadOnlyList<ApplicationFault> faults = reporter.Capture();
        Assert.HasCount(1, faults);
        Assert.AreEqual(ApplicationFaultCode.GgufChatRetirementUnexpected, faults[0].Code);
        Assert.AreEqual(ApplicationFaultClassification.InvalidOperation, faults[0].Classification);
        Assert.IsFalse(faults[0].ToString().Contains("Injected", StringComparison.Ordinal));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task EventProgrammingFaultIsReportedOnceAtTheOperationBoundary()
    {
        var session = new ThrowingStopSession();
        var reporter = new BoundedApplicationFaultReporter(4);
        ChatDemoController controller = CreateController(session, reporter);
        MethodInfo runOperation = typeof(ChatDemoController).GetMethod(
            "RunOperationAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "The GGUF Chat operation boundary was not found.");
        var operation = (Func<CancellationToken, Task>)(_ => Task.FromException(
            new InvalidOperationException("Injected event programming fault.")));

        for (int attempt = 0; attempt < 2; attempt++)
        {
            await (Task)(runOperation.Invoke(
                controller,
                [operation, CancellationToken.None])
                ?? throw new InvalidOperationException("The operation task was not returned."));
        }
        await controller.DisposeAsync();

        Assert.AreEqual(1, session.DisposeCount);
        IReadOnlyList<ApplicationFault> faults = reporter.Capture();
        Assert.HasCount(1, faults);
        Assert.AreEqual(ApplicationFaultCode.GgufChatOperationUnexpected, faults[0].Code);
        Assert.AreEqual(ApplicationFaultClassification.InvalidOperation, faults[0].Classification);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ExpectedHistoryIoFailureBecomesBoundedSupportState()
    {
        var reporter = new BoundedApplicationFaultReporter(4);
        ChatDemoController controller = CreateController(
            new BlockingDisposalSession(),
            reporter);
        MethodInfo runOperation = typeof(ChatDemoController).GetMethod(
            "RunOperationAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The operation boundary was not found.");
        var operation = (Func<CancellationToken, Task>)(_ => Task.FromException(
            new IOException(@"private path C:\Users\private\history.json")));

        await (Task)(runOperation.Invoke(controller, [operation, CancellationToken.None])
            ?? throw new InvalidOperationException("The operation task was not returned."));

        Assert.AreEqual(ChatOperationSupportCode.HistoryUnavailable, controller.LastSupportCode);
        Assert.HasCount(0, reporter.Capture());
        ((BlockingDisposalSession)GetSession(controller)).AllowDisposal();
        await controller.DisposeAsync();
        Assert.IsFalse(typeof(ChatDemoController).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic).Any(field =>
                typeof(Exception).IsAssignableFrom(field.FieldType)
                || typeof(IEnumerable<Exception>).IsAssignableFrom(field.FieldType)));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task TypedRuntimeFailureBecomesBoundedSupportState()
    {
        var session = new BlockingDisposalSession();
        var reporter = new BoundedApplicationFaultReporter(4);
        ChatDemoController controller = CreateController(session, reporter);
        MethodInfo runOperation = typeof(ChatDemoController).GetMethod(
            "RunOperationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var operation = (Func<CancellationToken, Task>)(_ => Task.FromException(
            new GgufRuntimeTrustException("runtime-unavailable")));

        await (Task)runOperation.Invoke(
            controller, [operation, CancellationToken.None])!;

        Assert.AreEqual(ChatOperationSupportCode.RuntimeUnavailable, controller.LastSupportCode);
        Assert.HasCount(0, reporter.Capture());
        session.AllowDisposal();
        await controller.DisposeAsync();
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RetirementRunsLaterCleanupAfterAnEarlierPhaseFault()
    {
        var session = new MultiFailureSession();
        var reporter = new BoundedApplicationFaultReporter(4);
        ChatDemoController controller = CreateController(session, reporter);
        SetCoordinatorGenerating(controller);

        await controller.DisposeAsync();

        Assert.AreEqual(1, session.StopCount);
        Assert.AreEqual(1, session.DisposeCount);
        Assert.HasCount(1, reporter.Capture());
    }

    private static ChatDemoController CreateController(
        IGgufChatSession session,
        IApplicationFaultReporter? reporter = null)
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
                typeof(string),
                typeof(IApplicationFaultReporter)
            ],
            modifiers: null) ?? throw new InvalidOperationException(
                "The GGUF Chat lifetime owner constructor was not found.");
        return (ChatDemoController)constructor.Invoke(
            [new ChatPage(), session, "model", "cpu", "Model", "Runtime", reporter]);
    }

    private static IGgufChatSession GetSession(ChatDemoController controller)
    {
        object coordinator = typeof(ChatDemoController).GetField(
            "coordinator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
        return (IGgufChatSession)coordinator.GetType().GetField(
            "session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(coordinator)!;
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

    private sealed class MultiFailureSession : IGgufChatSession
    {
        internal int StopCount { get; private set; }
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
        public ValueTask StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return ValueTask.FromException(
                new InvalidOperationException("first private fault"));
        }
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.FromException(
                new NullReferenceException("second private fault"));
        }
    }
}
