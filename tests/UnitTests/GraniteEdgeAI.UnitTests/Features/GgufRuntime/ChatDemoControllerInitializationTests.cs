using System.Runtime.CompilerServices;
using GraniteEdgeAI.Features.ApplicationFaults;
using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime;

[TestClass]
public sealed class ChatDemoControllerInitializationTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task LoadFailureBeforePreparationDisposesSessionOnceAndPreservesPrimary()
    {
        var primary = new IOException("primary private load failure");
        var session = new InitializationSession();

        IOException actual = await Assert.ThrowsExactlyAsync<IOException>(() =>
            CreateAsync(new InitializationStore(loadFailure: primary), session));

        Assert.AreSame(primary, actual);
        Assert.AreEqual(0, session.PrepareCount);
        Assert.AreEqual(1, session.DisposeCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task FailureAfterPreparationDisposesSessionOnceAndPreservesPrimary()
    {
        var primary = new InvalidOperationException("primary private prepare failure");
        var session = new InitializationSession(prepareFailure: primary);

        InvalidOperationException actual =
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                CreateAsync(new InitializationStore([Conversation()]), session));

        Assert.AreSame(primary, actual);
        Assert.AreEqual(1, session.PrepareCount);
        Assert.AreEqual(1, session.DisposeCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task InitializationCancellationPreservesExactTokenAndDisposesOnce()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var session = new InitializationSession();

        OperationCanceledException actual =
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                CreateAsync(
                    new InitializationStore(
                        loadFailure: new OperationCanceledException(cancellation.Token)),
                    session,
                    cancellationToken: cancellation.Token));

        Assert.AreEqual(cancellation.Token, actual.CancellationToken);
        Assert.AreEqual(1, session.DisposeCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CleanupFaultIsBoundedAndCannotReplaceInitializationFailure()
    {
        const string privateText = @"cleanup C:\Users\private\model.gguf";
        var primary = new InvalidOperationException("primary initialization failure");
        var reporter = new BoundedApplicationFaultReporter(4);
        var session = new InitializationSession(
            prepareFailure: primary,
            disposeFailure: new InvalidOperationException(privateText));

        InvalidOperationException actual =
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                CreateAsync(
                    new InitializationStore([Conversation()]),
                    session,
                    reporter));

        Assert.AreSame(primary, actual);
        Assert.AreEqual(1, session.DisposeCount);
        IReadOnlyList<ApplicationFault> faults = reporter.Capture();
        Assert.HasCount(1, faults);
        Assert.AreEqual(ApplicationFaultCode.GgufChatRetirementUnexpected, faults[0].Code);
        Assert.IsFalse(faults[0].ToString().Contains(privateText, StringComparison.Ordinal));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SuccessfulFactoryReturnsInitializedOwnerAndRetiresOnce()
    {
        var session = new InitializationSession();
        ChatDemoController controller = await CreateAsync(
            new InitializationStore(), session);

        Assert.AreEqual(1, session.PrepareCount);
        Assert.AreEqual(0, session.DisposeCount);
        await Task.WhenAll(
            controller.DisposeAsync().AsTask(),
            controller.DisposeAsync().AsTask());
        Assert.AreEqual(1, session.DisposeCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task DamagedHistoryRecordsBecomeBoundedSupportState()
    {
        var session = new InitializationSession();
        ChatDemoController controller = await CreateAsync(
            new InitializationStore(hasUnavailableRecords: true),
            session);

        Assert.AreEqual(
            ChatOperationSupportCode.HistoryUnavailable,
            controller.LastSupportCode);
        await controller.DisposeAsync();
    }

    private static Task<ChatDemoController> CreateAsync(
        IChatHistoryStore store,
        IGgufChatSession session,
        IApplicationFaultReporter? reporter = null,
        CancellationToken cancellationToken = default) =>
        ChatDemoController.CreateInitializedAsync(
            new ChatPage(),
            store,
            session,
            "model",
            "cpu",
            "Model",
            "Runtime",
            reporter,
            cancellationToken);

    private static ChatConversation Conversation() => ChatConversation.Create(
        Guid.NewGuid(), "model", "cpu", DateTimeOffset.UtcNow);

    private sealed class InitializationStore(
        IReadOnlyList<ChatConversation>? conversations = null,
        Exception? loadFailure = null,
        bool hasUnavailableRecords = false) : IChatHistoryStore
    {
        public Task<ChatHistoryLoadResult> LoadAsync(CancellationToken token) =>
            loadFailure is null
                ? Task.FromResult(new ChatHistoryLoadResult(
                    conversations ?? (IReadOnlyList<ChatConversation>)[],
                    hasUnavailableRecords))
                : Task.FromException<ChatHistoryLoadResult>(loadFailure);
        public Task SaveAsync(ChatConversation conversation, CancellationToken token) =>
            Task.CompletedTask;
        public Task DeleteAsync(Guid conversationId, CancellationToken token) =>
            Task.CompletedTask;
        public Task ClearAsync(CancellationToken token) => Task.CompletedTask;
    }

    private sealed class InitializationSession(
        Exception? prepareFailure = null,
        Exception? disposeFailure = null) : IGgufChatSession
    {
        internal int PrepareCount { get; private set; }
        internal int DisposeCount { get; private set; }

        public ValueTask PrepareConversationAsync(
            ChatConversation conversation,
            CancellationToken cancellationToken)
        {
            PrepareCount++;
            return prepareFailure is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(prepareFailure);
        }

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
