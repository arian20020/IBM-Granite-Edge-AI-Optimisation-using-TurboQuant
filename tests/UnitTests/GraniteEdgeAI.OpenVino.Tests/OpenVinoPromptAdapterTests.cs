using System.Text;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.Prompting;
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
        (OpenVinoRouteSession session, List<PromptEvent> events) =
            await StartSessionAsync(channel);

        PromptTurnResult result = await session.GenerateAsync(
            "hello",
            requestedNewTokens: 8,
            CancellationToken.None);

        Assert.AreEqual("local answer", result.Text);
        CollectionAssert.AreEqual(
            OrderedText,
            events.Where(item => item.Kind == PromptEventKind.TextDelta)
                .Select(item => item.Text)
                .ToArray());
        PromptEvent ready = events.First(item =>
            item.Kind == PromptEventKind.SessionReady);
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

        Task<PromptTurnResult> first = session.GenerateAsync(
            "first", 8, CancellationToken.None);
        await firstFragment.Task;
        await session.StopAsync(CancellationToken.None);
        PromptTurnResult stopped = await first;
        PromptTurnResult second = await session.GenerateAsync(
            "second", 8, CancellationToken.None);

        Assert.AreEqual(PromptTurnStatus.Stopped, stopped.Status);
        Assert.AreEqual("partial", stopped.Text);
        Assert.AreEqual(PromptTurnStatus.Completed, second.Status);
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
        (OpenVinoRouteSession session, List<PromptEvent> events) =
            await StartSessionAsync(channel);

        Task<PromptTurnResult> generation = session.GenerateAsync(
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
    public async Task ActiveCancellationOutcomeCannotRaceSessionIntoFailedState()
    {
        TaskCompletionSource promptStarted = NewSignal();
        TaskCompletionSource cancellationReleased = NewSignal();
        FakeChannel channel = new(async (_, _, _) =>
        {
            promptStarted.TrySetResult();
            await cancellationReleased.Task;
            throw new OpenVinoRouteWorkerFailureException(
                OpenVinoSupportCode.OperationCancelled,
                "active native turn cancelled");
        });
        channel.CancelAction = () => cancellationReleased.TrySetResult();
        (OpenVinoRouteSession session, List<PromptEvent> events) =
            await StartSessionAsync(channel);

        Task<PromptTurnResult> generation = session.GenerateAsync(
            "cancel me", 8, CancellationToken.None);
        await promptStarted.Task;
        await session.CancelAsync(CancellationToken.None);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await generation);
        await session.DisposeAsync();

        Assert.AreEqual(OpenVinoRouteState.Cancelled, session.Snapshot.State);
        Assert.IsFalse(events.Any(item => item.Kind == PromptEventKind.Failed));
        Assert.HasCount(1, events.Where(item =>
            item.Kind == PromptEventKind.Cancelled));
        Assert.AreEqual(1, channel.CancelCount);
        Assert.AreEqual(1, channel.DisposeCount);
    }

    [TestMethod]
    [DataRow(OpenVinoSupportCode.RuntimeTimedOut, "runtime_timed_out")]
    [DataRow(OpenVinoSupportCode.RuntimeProtocolFailed, "runtime_protocol_failed")]
    public async Task CancellationFailureTerminalizesAndDisposesExactlyOnce(
        OpenVinoSupportCode supportCode,
        string expectedCode)
    {
        FakeChannel channel = SuccessfulChannel();
        channel.CancelFailure = new OpenVinoRouteWorkerFailureException(
            supportCode,
            "private cancellation failure C:\\secret\\package");
        (OpenVinoRouteSession session, List<PromptEvent> events) =
            await StartSessionAsync(channel);

        await session.CancelAsync(CancellationToken.None);
        await session.CancelAsync(CancellationToken.None);
        await session.DisposeAsync();

        Assert.AreEqual(OpenVinoRouteState.Failed, session.Snapshot.State);
        Assert.AreEqual(expectedCode, session.Snapshot.FailureCode);
        PromptEvent failure = events.Single(item =>
            item.Kind == PromptEventKind.Failed);
        Assert.AreEqual(expectedCode, failure.Failure!.SupportCode);
        Assert.IsFalse(failure.Failure.Message.Contains(
            "secret", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(1, channel.CancelCount);
        Assert.AreEqual(1, channel.DisposeCount);
    }

    [TestMethod]
    [DataRow(PromptEventKind.CancellingSession)]
    [DataRow(PromptEventKind.Cancelled)]
    public async Task ThrowingCancellationObserverCannotPreemptTerminalTeardown(
        PromptEventKind throwingKind)
    {
        FakeChannel channel = SuccessfulChannel();
        List<PromptEvent> events = [];
        OpenVinoRouteSession session = await StartSessionAsync(
            channel,
            promptEvent =>
            {
                events.Add(promptEvent);
                if (promptEvent.Kind == throwingKind)
                {
                    throw new InvalidOperationException("observer failed");
                }
            });

        await session.CancelAsync(CancellationToken.None);
        await session.CancelAsync(CancellationToken.None);
        await session.DisposeAsync();

        Assert.AreEqual(OpenVinoRouteState.Cancelled, session.Snapshot.State);
        Assert.IsTrue(events.Any(item =>
            item.Kind == PromptEventKind.CancellingSession));
        Assert.IsTrue(events.Any(item => item.Kind == PromptEventKind.Cancelled));
        Assert.AreEqual(1, channel.CancelCount);
        Assert.AreEqual(1, channel.DisposeCount);
    }

    [TestMethod]
    [DataRow(OpenVinoSupportCode.RuntimeTimedOut, "runtime_timed_out")]
    [DataRow(OpenVinoSupportCode.RuntimeProtocolFailed, "runtime_protocol_failed")]
    public async Task ThrowingFailureObserverCannotPreemptFailedCancellationTeardown(
        OpenVinoSupportCode supportCode,
        string expectedCode)
    {
        FakeChannel channel = SuccessfulChannel();
        channel.CancelFailure = new OpenVinoRouteWorkerFailureException(
            supportCode,
            "private cancellation failure C:\\secret\\package");
        OpenVinoRouteSession session = await StartSessionAsync(
            channel,
            promptEvent =>
            {
                if (promptEvent.Kind == PromptEventKind.Failed)
                {
                    throw new InvalidOperationException("terminal observer failed");
                }
            });

        await session.CancelAsync(CancellationToken.None);
        await session.CancelAsync(CancellationToken.None);
        await session.DisposeAsync();

        Assert.AreEqual(OpenVinoRouteState.Failed, session.Snapshot.State);
        Assert.AreEqual(expectedCode, session.Snapshot.FailureCode);
        Assert.AreEqual(1, channel.CancelCount);
        Assert.AreEqual(1, channel.DisposeCount);
    }

    [TestMethod]
    public async Task ConcurrentCancelAndDisposeBothJoinOneSlowChannelDisposal()
    {
        TaskCompletionSource disposalStarted = NewSignal();
        TaskCompletionSource releaseDisposal = NewSignal();
        FakeChannel channel = SuccessfulChannel();
        channel.DisposeAction = async () =>
        {
            disposalStarted.TrySetResult();
            await releaseDisposal.Task;
        };
        List<PromptEvent> events = [];
        OpenVinoRouteSession session = await StartSessionAsync(
            channel,
            promptEvent =>
            {
                events.Add(promptEvent);
                if (promptEvent.Kind == PromptEventKind.Cancelled)
                {
                    throw new InvalidOperationException("terminal observer failed");
                }
            });

        Task cancellation = session.CancelAsync(CancellationToken.None);
        await disposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task disposal = session.DisposeAsync().AsTask();

        Assert.IsFalse(cancellation.IsCompleted);
        Assert.IsFalse(disposal.IsCompleted,
            "Every disposer must join the in-flight channel disposal task.");
        Assert.AreEqual(1, channel.DisposeCount);

        releaseDisposal.SetResult();
        await Task.WhenAll(cancellation, disposal)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(OpenVinoRouteState.Cancelled, session.Snapshot.State);
        Assert.HasCount(1, events.Where(item =>
            item.Kind == PromptEventKind.Cancelled));
        Assert.AreEqual(1, channel.CancelCount);
        Assert.AreEqual(1, channel.DisposeCount);
    }

    [TestMethod]
    public async Task CancellingObserverReentrantDisposeJoinsPublishedSlowTeardown()
    {
        TaskCompletionSource disposalStarted = NewSignal();
        TaskCompletionSource releaseDisposal = NewSignal();
        FakeChannel channel = SuccessfulChannel();
        channel.DisposeAction = async () =>
        {
            disposalStarted.TrySetResult();
            await releaseDisposal.Task;
        };
        List<PromptEvent> events = [];
        Task? reentrantDisposal = null;
        OpenVinoRouteSession? session = null;
        session = await StartSessionAsync(
            channel,
            promptEvent =>
            {
                events.Add(promptEvent);
                if (promptEvent.Kind == PromptEventKind.CancellingSession)
                {
                    reentrantDisposal = session!.DisposeAsync().AsTask();
                }
            });

        Task cancellation = session.CancelAsync(CancellationToken.None);
        await disposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsNotNull(reentrantDisposal);
        Assert.AreSame(cancellation, reentrantDisposal,
            "Reentrant disposal must observe the exact published teardown task.");
        Assert.IsFalse(cancellation.IsCompleted);
        Assert.IsFalse(reentrantDisposal.IsCompleted,
            "Reentrant disposal must join the already-published teardown task.");
        Assert.AreEqual(1, channel.CancelCount);
        Assert.AreEqual(1, channel.DisposeCount);

        releaseDisposal.SetResult();
        await Task.WhenAll(cancellation, reentrantDisposal)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(OpenVinoRouteState.Cancelled, session.Snapshot.State);
        Assert.HasCount(1, events.Where(item =>
            item.Kind == PromptEventKind.CancellingSession));
        Assert.HasCount(1, events.Where(item =>
            item.Kind == PromptEventKind.Cancelled));
        Assert.AreEqual(1, channel.CancelCount);
        Assert.AreEqual(1, channel.DisposeCount);
    }

    [TestMethod]
    public async Task WorkerConfirmationOwnsTheExactTurnBeforeActiveCancellationIsEnabled()
    {
        TaskCompletionSource promptStarted = NewSignal();
        TaskCompletionSource releasePrompt = NewSignal();
        FakeChannel channel = new(async (command, _, _) =>
        {
            promptStarted.TrySetResult();
            await releasePrompt.Task;
            return new TurnCompletedEvent(
                command.SessionId,
                command.TurnId,
                1,
                1,
                OpenVinoTurnDisposition.Completed);
        })
        {
            AutoConfirmGeneration = false
        };
        (OpenVinoRouteSession session, List<PromptEvent> events) =
            await StartSessionAsync(channel);

        Task<PromptTurnResult> generation = session.GenerateAsync(
            "confirm me", 8, CancellationToken.None);
        await promptStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        PromptEvent pending = events.Last();

        Assert.AreEqual(PromptEventKind.GeneratingTurn, pending.Kind);
        Assert.IsFalse(events.Any(item =>
            item.Kind == PromptEventKind.GenerationConfirmed));

        channel.ConfirmGeneration();
        PromptEvent confirmed = events.Single(item =>
            item.Kind == PromptEventKind.GenerationConfirmed);
        Assert.AreEqual(pending.TurnId, confirmed.TurnId);

        releasePrompt.SetResult();
        Assert.AreEqual(
            PromptTurnStatus.Completed,
            (await generation.WaitAsync(TimeSpan.FromSeconds(5))).Status);
    }

    [TestMethod]
    public async Task StopBeforeWorkerConfirmationFailsBeforeTheChannel()
    {
        TaskCompletionSource promptStarted = NewSignal();
        TaskCompletionSource releasePrompt = NewSignal();
        FakeChannel channel = new(async (command, _, _) =>
        {
            promptStarted.TrySetResult();
            await releasePrompt.Task;
            return new TurnCompletedEvent(
                command.SessionId,
                command.TurnId,
                1,
                1,
                OpenVinoTurnDisposition.Completed);
        })
        {
            AutoConfirmGeneration = false
        };
        (OpenVinoRouteSession session, _) = await StartSessionAsync(channel);

        Task<PromptTurnResult> generation = session.GenerateAsync(
            "not confirmed", 8, CancellationToken.None);
        await promptStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.StopAsync(CancellationToken.None));
        Assert.AreEqual(0, channel.StopCount);

        channel.ConfirmGeneration();
        releasePrompt.SetResult();
        Assert.AreEqual(
            PromptTurnStatus.Completed,
            (await generation.WaitAsync(TimeSpan.FromSeconds(5))).Status);
    }

    [TestMethod]
    public async Task ActiveStopAtomicallyRequiresTheWorkerConfirmedTurnId()
    {
        TaskCompletionSource promptStarted = NewSignal();
        TaskCompletionSource stopObserved = NewSignal();
        FakeChannel channel = new(async (command, _, _) =>
        {
            promptStarted.TrySetResult();
            await stopObserved.Task;
            return new TurnCompletedEvent(
                command.SessionId,
                command.TurnId,
                1,
                1,
                OpenVinoTurnDisposition.Stopped);
        });
        channel.StopAction = () => stopObserved.TrySetResult();
        (OpenVinoRouteSession session, List<PromptEvent> events) =
            await StartSessionAsync(channel);

        Task<PromptTurnResult> generation = session.GenerateAsync(
            "stop confirmed", 8, CancellationToken.None);
        await promptStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Guid confirmedTurnId = events.Single(item =>
            item.Kind == PromptEventKind.GenerationConfirmed).TurnId!.Value;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.StopActiveTurnAsync(
                Guid.NewGuid(),
                CancellationToken.None));
        Assert.AreEqual(0, channel.StopCount);

        await session.StopActiveTurnAsync(
            confirmedTurnId,
            CancellationToken.None);
        Assert.AreEqual(
            PromptTurnStatus.Stopped,
            (await generation.WaitAsync(TimeSpan.FromSeconds(5))).Status);
        Assert.AreEqual(1, channel.StopCount);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.StopActiveTurnAsync(
                confirmedTurnId,
                CancellationToken.None));
        Assert.AreEqual(1, channel.StopCount);
    }

    [TestMethod]
    public async Task ActiveCancelAtomicallyRequiresTheWorkerConfirmedTurnId()
    {
        TaskCompletionSource promptStarted = NewSignal();
        TaskCompletionSource cancellationReleased = NewSignal();
        FakeChannel channel = new(async (_, _, _) =>
        {
            promptStarted.TrySetResult();
            await cancellationReleased.Task;
            throw new OpenVinoRouteWorkerFailureException(
                OpenVinoSupportCode.OperationCancelled,
                "confirmed active turn cancelled");
        });
        channel.CancelAction = () => cancellationReleased.TrySetResult();
        (OpenVinoRouteSession session, List<PromptEvent> events) =
            await StartSessionAsync(channel);

        Task<PromptTurnResult> generation = session.GenerateAsync(
            "cancel confirmed", 8, CancellationToken.None);
        await promptStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Guid confirmedTurnId = events.Single(item =>
            item.Kind == PromptEventKind.GenerationConfirmed).TurnId!.Value;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            session.CancelActiveTurnAsync(
                Guid.NewGuid(),
                CancellationToken.None));
        Assert.AreEqual(0, channel.CancelCount);

        await session.CancelActiveTurnAsync(
            confirmedTurnId,
            CancellationToken.None);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await generation);

        Assert.AreEqual(OpenVinoRouteState.Cancelled, session.Snapshot.State);
        Assert.AreEqual(1, channel.CancelCount);
        Assert.AreEqual(1, channel.DisposeCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancellationClaimAtPromptTerminalBoundarySuppressesPromptTerminal(
        bool workerReturnsFailure)
    {
        TaskCompletionSource terminalBoundaryReached = NewSignal();
        TaskCompletionSource releaseTerminal = NewSignal();
        FakeChannel channel = new(async (command, progress, _) =>
        {
            terminalBoundaryReached.TrySetResult();
            await releaseTerminal.Task;
            progress?.Report(new TokenEvent(
                command.SessionId,
                command.TurnId,
                0,
                "late"));
            return workerReturnsFailure
                ? new TurnFailedEvent(
                    command.SessionId,
                    command.TurnId,
                    OpenVinoSupportCode.RuntimeProtocolFailed)
                : new TurnCompletedEvent(
                    command.SessionId,
                    command.TurnId,
                    1,
                    1,
                    OpenVinoTurnDisposition.Completed);
        });
        channel.CancelAction = () => releaseTerminal.TrySetResult();
        (OpenVinoRouteSession session, List<PromptEvent> events) =
            await StartSessionAsync(channel);

        Task<PromptTurnResult> generation = session.GenerateAsync(
            "terminal race", 8, CancellationToken.None);
        await terminalBoundaryReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Guid confirmedTurnId = events.Single(item =>
            item.Kind == PromptEventKind.GenerationConfirmed).TurnId!.Value;

        await session.CancelActiveTurnAsync(
            confirmedTurnId,
            CancellationToken.None);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await generation);
        int eventCountAtCancellation = events.Count;
        await session.DisposeAsync();

        Assert.AreEqual(eventCountAtCancellation, events.Count,
            "No event revision may follow cancellation ownership.");
        Assert.HasCount(1, events.Where(item =>
            item.Kind == PromptEventKind.Cancelled));
        Assert.IsFalse(events.Any(item =>
            item.Kind is PromptEventKind.TurnCompleted or PromptEventKind.Failed));
        Assert.IsFalse(events.Any(item => item.Text == "late"));
        Assert.AreEqual(OpenVinoRouteState.Cancelled, session.Snapshot.State);
        Assert.AreEqual(1, channel.CancelCount);
        Assert.AreEqual(1, channel.DisposeCount);
    }

    [TestMethod]
    public async Task PromptTerminalClaimFirstRejectsStaleActiveCancellationBeforeChannel()
    {
        TaskCompletionSource terminalBoundaryReached = NewSignal();
        TaskCompletionSource releaseTerminal = NewSignal();
        FakeChannel channel = new(async (command, _, _) =>
        {
            terminalBoundaryReached.TrySetResult();
            await releaseTerminal.Task;
            return new TurnCompletedEvent(
                command.SessionId,
                command.TurnId,
                1,
                1,
                OpenVinoTurnDisposition.Completed);
        });
        List<PromptEvent> events = [];
        OpenVinoRouteSession? session = null;
        Exception? reentrantCancellationError = null;
        session = await StartSessionAsync(
            channel,
            promptEvent =>
            {
                events.Add(promptEvent);
                if (promptEvent.Kind == PromptEventKind.TurnCompleted)
                {
                    try
                    {
                        _ = session!.CancelActiveTurnAsync(
                            promptEvent.TurnId!.Value,
                            CancellationToken.None);
                    }
                    catch (Exception error)
                    {
                        reentrantCancellationError = error;
                    }
                }
            });

        Task<PromptTurnResult> generation = session.GenerateAsync(
            "prompt wins", 8, CancellationToken.None);
        await terminalBoundaryReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Guid confirmedTurnId = events.Single(item =>
            item.Kind == PromptEventKind.GenerationConfirmed).TurnId!.Value;
        releaseTerminal.SetResult();
        Assert.AreEqual(
            PromptTurnStatus.Completed,
            (await generation.WaitAsync(TimeSpan.FromSeconds(5))).Status);

        Assert.IsInstanceOfType<InvalidOperationException>(
            reentrantCancellationError,
            "The prompt-owned terminal boundary must reject active cancellation " +
            "before the terminal observer returns.");
        PromptEvent completion = events.Single(item =>
            item.Kind == PromptEventKind.TurnCompleted);
        Assert.AreEqual(confirmedTurnId, completion.TurnId);
        Assert.IsFalse(events.Any(item =>
            item.Kind is PromptEventKind.Cancelled or PromptEventKind.Failed));
        Assert.AreEqual(OpenVinoRouteState.SessionReady, session.Snapshot.State);
        Assert.AreEqual(0, channel.CancelCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WrongOrStaleTurnFailureCannotClaimTheCurrentWorkerFailure(
        bool wrongSession)
    {
        Guid firstTurnId = Guid.Empty;
        int promptNumber = 0;
        FakeChannel channel = new((command, _, _) =>
        {
            if (Interlocked.Increment(ref promptNumber) == 1)
            {
                firstTurnId = command.TurnId;
                return Task.FromResult<IOpenVinoEvent>(new TurnCompletedEvent(
                    command.SessionId,
                    command.TurnId,
                    1,
                    1,
                    OpenVinoTurnDisposition.Completed));
            }

            return Task.FromResult<IOpenVinoEvent>(new TurnFailedEvent(
                wrongSession ? Guid.NewGuid() : command.SessionId,
                wrongSession ? command.TurnId : firstTurnId,
                OpenVinoSupportCode.RuntimeLoadFailed));
        });
        (OpenVinoRouteSession session, List<PromptEvent> events) =
            await StartSessionAsync(channel);

        Assert.AreEqual(
            PromptTurnStatus.Completed,
            (await session.GenerateAsync(
                "establish stale identity",
                8,
                CancellationToken.None)).Status);
        PromptTurnResult result = await session.GenerateAsync(
            "reject wrong terminal",
            8,
            CancellationToken.None);

        Assert.AreEqual(PromptTurnStatus.Failed, result.Status);
        Assert.AreEqual(
            OpenVinoSupportCode.RuntimeProtocolFailed.ToProtocolValue(),
            result.Failure!.SupportCode,
            "A mismatched terminal event must fail closed as a protocol error, " +
            "not claim the current turn with the attacker's support code.");
        PromptEvent failure = events.Single(item =>
            item.Kind == PromptEventKind.Failed);
        Assert.AreEqual(result.Failure.SupportCode, failure.Failure!.SupportCode);
        Assert.AreNotEqual(firstTurnId, failure.TurnId,
            "A stale turn identity must never be published as the current terminal.");
        Assert.IsFalse(events.Any(item =>
            item.Failure?.SupportCode ==
                OpenVinoSupportCode.RuntimeLoadFailed.ToProtocolValue()));
    }

    [TestMethod]
    public async Task StopDispatchOwnsExactTurnUntilPromptTerminalSettles()
    {
        TaskCompletionSource firstPromptStarted = NewSignal();
        TaskCompletionSource<IOpenVinoEvent> firstTerminal = new();
        TaskCompletionSource stopDispatchEntered = NewSignal();
        TaskCompletionSource releaseStopDispatch = NewSignal();
        IProgress<TokenEvent>? firstProgress = null;
        int promptNumber = 0;
        FakeChannel channel = new(async (command, progress, _) =>
        {
            if (Interlocked.Increment(ref promptNumber) == 1)
            {
                firstProgress = progress;
                firstPromptStarted.TrySetResult();
                return await firstTerminal.Task.ConfigureAwait(false);
            }

            return new TurnCompletedEvent(
                command.SessionId,
                command.TurnId,
                1,
                1,
                OpenVinoTurnDisposition.Completed);
        });
        channel.StopAsyncAction = async (_, _) =>
        {
            stopDispatchEntered.TrySetResult();
            await releaseStopDispatch.Task;
        };
        (OpenVinoRouteSession session, List<PromptEvent> events) =
            await StartSessionAsync(channel);

        Task<PromptTurnResult> first = session.GenerateAsync(
            "stop exact turn",
            8,
            CancellationToken.None);
        await firstPromptStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Guid firstTurnId = events.Single(item =>
            item.Kind == PromptEventKind.GenerationConfirmed).TurnId!.Value;

        Task stop = session.StopActiveTurnAsync(
            firstTurnId,
            CancellationToken.None);
        await stopDispatchEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        firstTerminal.TrySetResult(new TurnCompletedEvent(
            session.Snapshot.Identity.SessionId,
            firstTurnId,
            1,
            0,
            OpenVinoTurnDisposition.Stopped));

        bool firstCompletedBeforeStopDispatch = first.IsCompleted;
        Exception? overlappingPromptError = null;
        try
        {
            _ = await session.GenerateAsync(
                "must not start during stop dispatch",
                8,
                CancellationToken.None);
        }
        catch (Exception error)
        {
            overlappingPromptError = error;
        }
        int promptCountBeforeStopDispatch = channel.PromptCount;

        releaseStopDispatch.TrySetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        PromptTurnResult stopped = await first.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsFalse(firstCompletedBeforeStopDispatch,
            "Prompt completion cannot release turn ownership while its STOP " +
            "dispatch is still paused.");
        Assert.IsInstanceOfType<InvalidOperationException>(
            overlappingPromptError,
            "A next prompt cannot start until the original STOP dispatch and " +
            "terminal arbitration have settled.");
        Assert.AreEqual(1, promptCountBeforeStopDispatch);
        Assert.AreEqual(PromptTurnStatus.Stopped, stopped.Status);
        CollectionAssert.AreEqual(
            new[] { firstTurnId },
            channel.StopTurnIds.ToArray(),
            "The expected worker-confirmed turn must cross the channel boundary.");
        Assert.HasCount(1, events.Where(item =>
            item.Kind is PromptEventKind.TurnCompleted or
                PromptEventKind.Failed or
                PromptEventKind.Cancelled));

        int eventCountAfterTerminal = events.Count;
        firstProgress!.Report(new TokenEvent(
            session.Snapshot.Identity.SessionId,
            firstTurnId,
            1,
            "late"));
        Assert.AreEqual(eventCountAfterTerminal, events.Count,
            "A late fragment from the settled turn must remain inactionable.");

        PromptTurnResult reused = await session.GenerateAsync(
            "next turn",
            8,
            CancellationToken.None);
        Assert.AreEqual(PromptTurnStatus.Completed, reused.Status);
        Assert.AreEqual(2, channel.PromptCount);
        Assert.AreEqual(1, channel.StopCount);
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
        (OpenVinoRouteSession session, List<PromptEvent> events) =
            await StartSessionAsync(channel);

        PromptTurnResult result = await session.GenerateAsync(
            "hello", 8, CancellationToken.None);

        Assert.AreEqual(PromptTurnStatus.Failed, result.Status);
        Assert.AreEqual("runtime_load_failed", result.Failure!.SupportCode);
        Assert.AreEqual("Try loading the session again.", result.Failure.RecoveryAction);
        Assert.IsFalse(result.Failure.Message.Contains("secret", StringComparison.Ordinal));
        Assert.IsFalse(events.Any(item =>
            item.Failure?.Message.Contains("native", StringComparison.Ordinal) == true));
    }

    private static async Task<(OpenVinoRouteSession, List<PromptEvent>)>
        StartSessionAsync(FakeChannel channel)
    {
        OpenVinoRouteStateMachine machine = new();
        Guid operationId = machine.Snapshot.Identity.OperationId;
        Assert.IsTrue(machine.TryBeginInspection(operationId));
        Assert.IsTrue(machine.TryCompleteInspection(operationId,
            OpenVinoRouteInspectionOutcome.Ready));
        Assert.IsTrue(machine.TryAwaitConfiguration(operationId));
        List<PromptEvent> events = [];
        OpenVinoRouteSession session = await OpenVinoRouteSession.StartAsync(
            new FakeChannelFactory(channel),
            machine,
            Descriptor(),
            events.Add,
            CancellationToken.None);
        return (session, events);
    }

    private static async Task<OpenVinoRouteSession> StartSessionAsync(
        FakeChannel channel,
        Action<PromptEvent> eventSink)
    {
        OpenVinoRouteStateMachine machine = new();
        Guid operationId = machine.Snapshot.Identity.OperationId;
        Assert.IsTrue(machine.TryBeginInspection(operationId));
        Assert.IsTrue(machine.TryCompleteInspection(operationId,
            OpenVinoRouteInspectionOutcome.Ready));
        Assert.IsTrue(machine.TryAwaitConfiguration(operationId));
        return await OpenVinoRouteSession.StartAsync(
            new FakeChannelFactory(channel),
            machine,
            Descriptor(),
            eventSink,
            CancellationToken.None);
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
        internal Func<Guid, CancellationToken, Task>? StopAsyncAction { get; set; }
        internal Action? CancelAction { get; set; }
        internal Exception? CancelFailure { get; set; }
        internal Func<ValueTask>? DisposeAction { get; set; }
        internal bool AutoConfirmGeneration { get; set; } = true;
        internal Guid SessionId { get; set; }
        internal int PromptCount { get; private set; }
        internal bool CloseCalled { get; private set; }
        internal bool DisposeCalled => DisposeCount != 0;
        internal int CancelCount { get; private set; }
        internal int StopCount { get; private set; }
        internal List<Guid> StopTurnIds { get; } = [];
        internal int DisposeCount { get; private set; }
        private Action<GenerationStartedEvent>? generationStarted;
        private Guid activeTurnId;

        public async Task<IOpenVinoEvent> PromptAsync(
            PromptCommand command,
            IProgress<TokenEvent>? progress,
            Action<GenerationStartedEvent>? generationStarted,
            CancellationToken cancellationToken)
        {
            PromptCount++;
            this.generationStarted = generationStarted;
            activeTurnId = command.TurnId;
            if (AutoConfirmGeneration)
            {
                ConfirmGeneration();
            }
            return await prompt(command, progress, cancellationToken);
        }

        internal void ConfirmGeneration() => generationStarted?.Invoke(
            new GenerationStartedEvent(SessionId, activeTurnId));

        public Task StopAsync(
            Guid expectedTurnId,
            CancellationToken cancellationToken)
        {
            StopCount++;
            StopTurnIds.Add(expectedTurnId);
            StopAction?.Invoke();
            return StopAsyncAction?.Invoke(expectedTurnId, cancellationToken) ??
                Task.CompletedTask;
        }

        public Task CancelAsync(CancellationToken cancellationToken)
        {
            CancelCount++;
            CancelAction?.Invoke();
            if (CancelFailure is not null)
            {
                throw CancelFailure;
            }
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            CloseCalled = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return DisposeAction?.Invoke() ?? ValueTask.CompletedTask;
        }
    }
}
