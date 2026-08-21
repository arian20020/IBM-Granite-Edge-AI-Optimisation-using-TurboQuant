using System.Text;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
public sealed class OpenVinoPromptAdapterTests
{
    private static readonly string[] OrderedText = ["local ", "answer"];
    private static readonly string[] CpuExecutionDevices = ["CPU"];

    [TestMethod]
    public async Task StreamsOrderedTextAndPublishesCpuRequestedActualEvidence()
    {
        FakeChannel channel = new(async (command, progress, _) =>
        {
            progress?.Report(new TokenEvent(command.SessionId, command.TurnId, 0, "local "));
            progress?.Report(new TokenEvent(command.SessionId, command.TurnId, 1, "answer"));
            await Task.Yield();
            return new TurnCompletedEvent(
                command.SessionId,
                command.TurnId,
                7,
                2,
                OpenVinoTurnDisposition.Completed);
        });
        (OpenVinoRouteSession session, List<OpenVinoPromptEvent> events) =
            await StartSessionAsync(channel);

        OpenVinoTurnResult result = await session.GenerateAsync(
            "hello",
            requestedNewTokens: 8,
            CancellationToken.None);

        Assert.AreEqual("local answer", result.Text);
        CollectionAssert.AreEqual(
            OrderedText,
            events.Where(item => item.Kind == OpenVinoPromptEventKind.TextDelta)
                .Select(item => item.Text)
                .ToArray());
        OpenVinoPromptEvent ready = events.First(item =>
            item.Kind == OpenVinoPromptEventKind.SessionReady);
        Assert.AreEqual("CPU", ready.RequestedDevice);
        CollectionAssert.AreEqual(
            CpuExecutionDevices,
            ready.ActualExecutionDevices.ToArray());
    }

    [TestMethod]
    public async Task ProductPromptAndRequestedTokenLimitsFailBeforeTheChannel()
    {
        FakeChannel channel = new((command, _, _) => Task.FromResult<IOpenVinoEvent>(
            new TurnCompletedEvent(command.SessionId, command.TurnId, 1, 1,
                OpenVinoTurnDisposition.Completed)));
        (OpenVinoRouteSession session, _) = await StartSessionAsync(channel);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            session.GenerateAsync("hello", 129, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            session.GenerateAsync(
                new string('\u0800', OpenVinoRouteCapability.MaximumPromptUtf8Bytes / 2),
                1,
                CancellationToken.None));

        Assert.AreEqual(0, channel.PromptCount);
        OpenVinoConfigurationCandidate candidate =
            OpenVinoRouteCapability.Candidates.Single();
        Assert.AreEqual(4_096, candidate.MaximumContextTokens);
        Assert.AreEqual(128, candidate.DefaultRequestedNewTokens);
        Assert.AreEqual(128, candidate.MaximumRequestedNewTokens);
    }

    [TestMethod]
    public async Task StopReturnsSuccessfulPartialTurnAndTheSameSessionCanBeReused()
    {
        TaskCompletionSource firstFragment = NewSignal();
        TaskCompletionSource stopObserved = NewSignal();
        int promptSequence = 0;
        FakeChannel channel = new(async (command, progress, _) =>
        {
            int promptNumber = Interlocked.Increment(ref promptSequence);
            progress?.Report(new TokenEvent(command.SessionId, command.TurnId, 0,
                promptNumber == 1 ? "partial" : "second"));
            if (promptNumber == 1)
            {
                firstFragment.TrySetResult();
                await stopObserved.Task;
                return new TurnCompletedEvent(command.SessionId, command.TurnId, 2, 1,
                    OpenVinoTurnDisposition.Stopped);
            }

            return new TurnCompletedEvent(command.SessionId, command.TurnId, 2, 1,
                OpenVinoTurnDisposition.Completed);
        });
        channel.StopAction = () => stopObserved.TrySetResult();
        (OpenVinoRouteSession session, _) = await StartSessionAsync(channel);

        Task<OpenVinoTurnResult> first = session.GenerateAsync(
            "first", 8, CancellationToken.None);
        await firstFragment.Task;
        await session.StopAsync(CancellationToken.None);
        OpenVinoTurnResult stopped = await first;
        OpenVinoTurnResult second = await session.GenerateAsync(
            "second", 8, CancellationToken.None);

        Assert.AreEqual(OpenVinoTurnStatus.Stopped, stopped.Status);
        Assert.AreEqual("partial", stopped.Text);
        Assert.AreEqual(OpenVinoTurnStatus.Completed, second.Status);
        Assert.AreEqual("second", second.Text);
        Assert.AreEqual(2, channel.PromptCount);
    }

    [TestMethod]
    public async Task CancelSuppressesLateTextAndEndsTheSession()
    {
        TaskCompletionSource firstFragment = NewSignal();
        TaskCompletionSource cancelObserved = NewSignal();
        FakeChannel channel = new(async (command, progress, _) =>
        {
            progress?.Report(new TokenEvent(command.SessionId, command.TurnId, 0, "before"));
            firstFragment.TrySetResult();
            await cancelObserved.Task;
            progress?.Report(new TokenEvent(command.SessionId, command.TurnId, 1, "late"));
            return new SessionCancelledEvent(command.SessionId);
        });
        channel.CancelAction = () => cancelObserved.TrySetResult();
        (OpenVinoRouteSession session, List<OpenVinoPromptEvent> events) =
            await StartSessionAsync(channel);

        Task<OpenVinoTurnResult> generation = session.GenerateAsync(
            "cancel me", 8, CancellationToken.None);
        await firstFragment.Task;
        await session.CancelAsync(CancellationToken.None);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await generation);

        Assert.IsFalse(events.Any(item => item.Text == "late"));
        Assert.AreEqual(OpenVinoRouteState.Cancelled, session.Snapshot.State);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.GenerateAsync("again", 8, CancellationToken.None));
    }

    [TestMethod]
    public async Task CloseAwaitsChannelCleanupAndMakesLateOperationsInactionable()
    {
        FakeChannel channel = SuccessfulChannel();
        (OpenVinoRouteSession session, _) = await StartSessionAsync(channel);

        await session.CloseAsync(CancellationToken.None);

        Assert.IsTrue(channel.CloseCalled);
        Assert.IsTrue(channel.DisposeCalled);
        Assert.AreEqual(OpenVinoRouteState.SessionCompleted, session.Snapshot.State);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.GenerateAsync("late", 1, CancellationToken.None));
    }

    [TestMethod]
    public async Task FixedWorkerFailureMapsToBoundedRecoveryWithoutRawDiagnosticText()
    {
        FakeChannel channel = new((_, _, _) =>
            throw new OpenVinoRouteWorkerFailureException(
                OpenVinoSupportCode.RuntimeLoadFailed,
                "raw path C:\\secret\\model and native exception"));
        (OpenVinoRouteSession session, List<OpenVinoPromptEvent> events) =
            await StartSessionAsync(channel);

        OpenVinoTurnResult result = await session.GenerateAsync(
            "hello", 8, CancellationToken.None);

        Assert.AreEqual(OpenVinoTurnStatus.Failed, result.Status);
        Assert.AreEqual("runtime_load_failed", result.Failure!.SupportCode);
        Assert.AreEqual("Try loading the session again.", result.Failure.RecoveryAction);
        Assert.IsFalse(result.Failure.Message.Contains("secret", StringComparison.Ordinal));
        Assert.IsFalse(events.Any(item =>
            item.Failure?.Message.Contains("native", StringComparison.Ordinal) == true));
    }

    private static async Task<(OpenVinoRouteSession, List<OpenVinoPromptEvent>)>
        StartSessionAsync(FakeChannel channel)
    {
        OpenVinoRouteStateMachine machine = new();
        Guid operationId = machine.Snapshot.Identity.OperationId;
        Assert.IsTrue(machine.TryBeginInspection(operationId));
        Assert.IsTrue(machine.TryCompleteInspection(operationId,
            OpenVinoRouteInspectionOutcome.Ready));
        Assert.IsTrue(machine.TryAwaitConfiguration(operationId));
        List<OpenVinoPromptEvent> events = [];
        OpenVinoRouteSession session = await OpenVinoRouteSession.StartAsync(
            new FakeChannelFactory(channel),
            machine,
            Descriptor(),
            events.Add,
            CancellationToken.None);
        return (session, events);
    }

    private static OpenVinoSessionDescriptor Descriptor() => new(
        Guid.NewGuid(),
        "C:\\private\\package",
        new string('1', 64),
        new string('2', 64),
        88);

    private static FakeChannel SuccessfulChannel() => new((command, _, _) =>
        Task.FromResult<IOpenVinoEvent>(new TurnCompletedEvent(
            command.SessionId,
            command.TurnId,
            1,
            1,
            OpenVinoTurnDisposition.Completed)));

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class FakeChannelFactory(FakeChannel channel) :
        IOpenVinoPromptChannelFactory
    {
        public Task<IOpenVinoPromptChannel> StartAsync(
            StartSessionCommand command,
            CancellationToken cancellationToken)
        {
            command.Validate();
            channel.SessionId = command.SessionId;
            return Task.FromResult<IOpenVinoPromptChannel>(channel);
        }
    }

    private sealed class FakeChannel(
        Func<PromptCommand, IProgress<TokenEvent>?, CancellationToken,
            Task<IOpenVinoEvent>> prompt) : IOpenVinoPromptChannel
    {
        internal Action? StopAction { get; set; }
        internal Action? CancelAction { get; set; }
        internal Guid SessionId { get; set; }
        internal int PromptCount { get; private set; }
        internal bool CloseCalled { get; private set; }
        internal bool DisposeCalled { get; private set; }

        public async Task<IOpenVinoEvent> PromptAsync(
            PromptCommand command,
            IProgress<TokenEvent>? progress,
            CancellationToken cancellationToken)
        {
            PromptCount++;
            return await prompt(command, progress, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopAction?.Invoke();
            return Task.CompletedTask;
        }

        public Task CancelAsync(CancellationToken cancellationToken)
        {
            CancelAction?.Invoke();
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            CloseCalled = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            return ValueTask.CompletedTask;
        }
    }
}
