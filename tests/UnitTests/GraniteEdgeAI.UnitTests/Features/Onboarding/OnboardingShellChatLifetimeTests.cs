using System.Reflection;
using System.Runtime.CompilerServices;
using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.ApplicationFaults;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.Onboarding;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.Onboarding;

[TestClass]
public sealed class OnboardingShellChatLifetimeTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ImportModelAwaitsRetirementAndNavigatesExactlyOnce()
    {
        var reporter = new BoundedApplicationFaultReporter(4);
        var session = new ShellSession(
            new InvalidOperationException(@"private teardown C:\Users\private\model.gguf"));
        var shell = new OnboardingShellPage();
        var page = new ChatPage();
        ChatDemoController controller = await CreateControllerAsync(
            page, session, reporter);
        AttachChatOwnership(shell, page, controller);
        var frame = Assert.IsInstanceOfType<Frame>(shell.FindName("StageFrame"));
        int navigations = 0;
        frame.Navigated += (_, _) => navigations++;

        await InvokeTask(shell, "RetireChatAndNavigateToImportAsync", page);

        Assert.AreEqual(1, session.DisposeCount);
        Assert.AreEqual(1, navigations);
        Assert.HasCount(1, reporter.Capture());
        Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ShutdownAwaitsChatRetirementAndCompletesShellCleanup()
    {
        var session = new ShellSession();
        var shell = new OnboardingShellPage();
        var page = new ChatPage();
        ChatDemoController controller = await CreateControllerAsync(page, session);
        AttachChatOwnership(shell, page, controller);

        await shell.ShutdownAsync();

        Assert.AreEqual(1, session.DisposeCount);
        var frame = Assert.IsInstanceOfType<Frame>(shell.FindName("StageFrame"));
        Assert.IsFalse(frame.IsHitTestVisible);
        Assert.IsNull(Field(shell, "_attachedChatPage"));
        Assert.IsNull(Field(shell, "_chatController"));
    }

    private static Task<ChatDemoController> CreateControllerAsync(
        ChatPage page,
        IGgufChatSession session,
        IApplicationFaultReporter? reporter = null) =>
        ChatDemoController.CreateInitializedAsync(
            page,
            new ShellStore(),
            session,
            "model",
            "cpu",
            "Model",
            "Runtime",
            faultReporter: reporter,
            CancellationToken.None);

    private static void AttachChatOwnership(
        OnboardingShellPage shell,
        ChatPage page,
        ChatDemoController controller)
    {
        SetField(shell, "_attachedChatPage", page);
        SetField(shell, "_chatController", controller);
        var frame = Assert.IsInstanceOfType<Frame>(shell.FindName("StageFrame"));
        frame.Content = page;
    }

    private static async Task InvokeTask(
        OnboardingShellPage shell,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = typeof(OnboardingShellPage).GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{methodName} was not found.");
        await (Task)(method.Invoke(shell, arguments)
            ?? throw new InvalidOperationException($"{methodName} returned no task."));
    }

    private static object? Field(OnboardingShellPage shell, string name) =>
        typeof(OnboardingShellPage).GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(shell);

    private static void SetField(
        OnboardingShellPage shell,
        string name,
        object value)
    {
        FieldInfo field = typeof(OnboardingShellPage).GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{name} was not found.");
        field.SetValue(shell, value);
    }

    private sealed class ShellStore : IChatHistoryStore
    {
        public Task<ChatHistoryLoadResult> LoadAsync(CancellationToken token) =>
            Task.FromResult(new ChatHistoryLoadResult([], false));
        public Task SaveAsync(ChatConversation conversation, CancellationToken token) =>
            Task.CompletedTask;
        public Task DeleteAsync(Guid conversationId, CancellationToken token) =>
            Task.CompletedTask;
        public Task ClearAsync(CancellationToken token) => Task.CompletedTask;
    }

    private sealed class ShellSession(Exception? disposeFailure = null)
        : IGgufChatSession
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
            ValueTask.CompletedTask;
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return disposeFailure is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(disposeFailure);
        }
    }
}
