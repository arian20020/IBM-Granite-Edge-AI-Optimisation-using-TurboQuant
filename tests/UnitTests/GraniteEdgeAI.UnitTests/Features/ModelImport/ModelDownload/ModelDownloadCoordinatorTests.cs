using GraniteEdgeAI.Features.ModelImport.ModelDownload;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelDownloadCoordinatorTests
{
    [TestMethod]
    public async Task StartAsync_SnapshotsSliderAndPublishesPathFreeCompletion()
    {
        var service = new FakeDownloadService();
        var coordinator = new ModelDownloadCoordinator(service, new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        VerifiedModelAvailableEventArgs? notification = null;
        coordinator.VerifiedModelAvailable += (_, value) => notification = value;

        await coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);

        Assert.AreEqual("Q4_K_M", coordinator.State.Quantisation);
        Assert.AreEqual(ModelDownloadStage.Completed, coordinator.State.Stage);
        Assert.IsNotNull(notification);
        Assert.IsFalse(notification.ToString()!.Contains(service.Model.LocalPath, StringComparison.Ordinal));
        Assert.IsFalse(coordinator.State.ToString()!.Contains(service.Model.LocalPath, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TryClaimVerifiedModel_IsAtomicAndOneTime()
    {
        var service = new FakeDownloadService();
        var coordinator = new ModelDownloadCoordinator(service, new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        await coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
        ModelDownloadOperationId operationId = coordinator.State.OperationId!.Value;

        Assert.IsFalse(coordinator.TryClaimVerifiedModel(ModelDownloadOperationId.CreateNew(), out _));
        Assert.IsTrue(coordinator.TryClaimVerifiedModel(operationId, out VerifiedDownloadedModel? first));
        Assert.AreSame(service.Model, first);
        Assert.IsFalse(coordinator.TryClaimVerifiedModel(operationId, out _));
    }

    [TestMethod]
    public async Task StartAsync_ConfirmationRequired_DoesNotUseNetworkWithoutConsent()
    {
        var service = new FakeDownloadService();
        var coordinator = new ModelDownloadCoordinator(service, new FakeNetworkPolicy(ModelDownloadConnectionKind.ConfirmationRequired));

        await coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);

        Assert.AreEqual(ModelDownloadStage.Interrupted, coordinator.State.Stage);
        Assert.AreEqual("download-network-confirmation-required", coordinator.State.ErrorCode);
        Assert.AreEqual(0, service.DownloadCalls);
    }

    [TestMethod]
    public async Task StartAsync_Offline_ReportsBoundedStateWithoutUsingNetwork()
    {
        var service = new FakeDownloadService();
        var coordinator = new ModelDownloadCoordinator(service, new FakeNetworkPolicy(ModelDownloadConnectionKind.Offline));

        await coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);

        Assert.AreEqual("download-offline", coordinator.State.ErrorCode);
        Assert.AreEqual(0, service.DownloadCalls);
    }

    [TestMethod]
    public async Task RetireAutomaticHandoff_PreventsClaimWithoutDeletingModel()
    {
        var service = new FakeDownloadService();
        var coordinator = new ModelDownloadCoordinator(service, new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        await coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
        ModelDownloadOperationId operationId = coordinator.State.OperationId!.Value;

        coordinator.RetireAutomaticHandoff();

        Assert.IsFalse(coordinator.TryClaimVerifiedModel(operationId, out _));
        Assert.AreEqual(ModelDownloadStage.Completed, coordinator.State.Stage);
    }

    [TestMethod]
    public async Task RecoverAsync_UnrestrictedConnection_ResumesDurablePartial()
    {
        var service = new FakeDownloadService { ResumeQuantisation = "Q4_K_M" };
        var coordinator = new ModelDownloadCoordinator(service, new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));

        await coordinator.RecoverAsync(CancellationToken.None);

        Assert.AreEqual(1, service.DownloadCalls);
        Assert.AreEqual("Q4_K_M", coordinator.State.Quantisation);
        Assert.AreEqual(ModelDownloadStage.Completed, coordinator.State.Stage);
    }

    [TestMethod]
    public async Task CancelAsync_AwaitsQuiescenceThenDiscardsPartial()
    {
        var service = new FakeDownloadService { WaitForCancellation = true };
        var coordinator = new ModelDownloadCoordinator(service, new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        Task running = coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
        await service.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await coordinator.CancelAsync(discardPartial: true, CancellationToken.None);
        await running;

        Assert.AreEqual(1, service.DiscardCalls);
        Assert.AreEqual("download-cancelled-discarded", coordinator.State.ErrorCode);
        Assert.AreEqual(0, coordinator.State.DownloadedBytes);
    }

    [TestMethod]
    public async Task StartingNewDownload_RevokesPriorAutomaticHandoffAuthorization()
    {
        var service = new FakeDownloadService();
        var coordinator = new ModelDownloadCoordinator(service, new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        await coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
        ModelDownloadOperationId first = coordinator.State.OperationId!.Value;
        Assert.IsTrue(coordinator.TryClaimVerifiedModel(first, out _));
        Assert.IsTrue(coordinator.IsAutomaticHandoffAuthorized(first));

        await coordinator.StartAsync(80, allowMetered: false, CancellationToken.None);

        Assert.IsFalse(coordinator.IsAutomaticHandoffAuthorized(first));
    }

    [TestMethod]
    public async Task RecoverAsync_DoesNotReplaceAUserStartedOperation()
    {
        var service = new FakeDownloadService
        {
            ResumeQuantisation = "Q4_K_M",
            DelayResumeProbe = true,
            WaitForCancellation = true
        };
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Offline));
        Task recovery = coordinator.RecoverAsync(CancellationToken.None);
        await service.ResumeProbeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Task userStart = coordinator.StartAsync(80, allowMetered: false, CancellationToken.None);
        service.ReleaseResumeProbe.TrySetResult();
        await recovery;
        await userStart;

        Assert.AreEqual("Q8_0", coordinator.State.Quantisation);
        Assert.AreEqual("download-offline", coordinator.State.ErrorCode);
    }

    [TestMethod]
    public async Task CancelAsync_DiscardsSettledInterruptedPartial()
    {
        var service = new FakeDownloadService();
        var coordinator = new ModelDownloadCoordinator(service, new FakeNetworkPolicy(ModelDownloadConnectionKind.Offline));
        await coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);

        await coordinator.CancelAsync(discardPartial: true, CancellationToken.None);

        Assert.AreEqual(1, service.DiscardCalls);
        Assert.AreEqual("download-cancelled-discarded", coordinator.State.ErrorCode);
        Assert.AreEqual(0, coordinator.State.DownloadedBytes);
    }

    [TestMethod]
    public async Task CancelAsync_NonCooperativeSuccessCannotWinAfterCancellation()
    {
        var service = new NonCooperativeDownloadService();
        var coordinator = new ModelDownloadCoordinator(service, new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        int handoffs = 0;
        coordinator.VerifiedModelAvailable += (_, _) => handoffs++;
        Task running = coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Task cancellation = coordinator.CancelAsync(false, CancellationToken.None);
        service.CompleteSuccessfully();
        await Task.WhenAll(running, cancellation).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(ModelDownloadStage.Interrupted, coordinator.State.Stage);
        Assert.AreEqual("download-cancelled", coordinator.State.ErrorCode);
        Assert.AreEqual(0, handoffs);
        Assert.IsFalse(coordinator.TryClaimVerifiedModel(coordinator.State.OperationId!.Value, out _));
    }

    [TestMethod]
    public async Task Dispose_PreventsLateProgressAndCompletionPublication()
    {
        var service = new NonCooperativeDownloadService();
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        int completedNotifications = 0;
        coordinator.VerifiedModelAvailable += (_, _) => completedNotifications++;
        Task operation = coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        coordinator.Dispose();
        service.ReportProgress(ModelDownloadStage.Downloading, 3);
        service.CompleteSuccessfully();
        await operation.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(0, completedNotifications);
        Assert.AreNotEqual(ModelDownloadStage.Completed, coordinator.State.Stage);
    }

    [TestMethod]
    public async Task StateObserverFailure_DoesNotPreventOperationOrOtherObservers()
    {
        var service = new FakeDownloadService();
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        int observedStates = 0;
        coordinator.StateChanged += (_, _) => throw new InvalidOperationException("observer failure");
        coordinator.StateChanged += (_, _) => observedStates++;

        await coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);

        Assert.AreEqual(ModelDownloadStage.Completed, coordinator.State.Stage);
        Assert.IsTrue(observedStates > 0);
    }

    [TestMethod]
    public async Task CancelAsync_PublishesImmediateCancellingStateBeforeProviderSettles()
    {
        var service = new NonCooperativeDownloadService();
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        Task operation = coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Task cancellation = coordinator.CancelAsync(
            discardPartial: true,
            CancellationToken.None);

        Assert.AreEqual("download-cancelling", coordinator.State.ErrorCode);
        Assert.IsFalse(cancellation.IsCompleted);
        service.CompleteInterrupted();
        await cancellation.WaitAsync(TimeSpan.FromSeconds(2));
        await operation.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task CancelAsync_ThrowingCallbackDoesNotEscapeOrLoseCleanupFailure()
    {
        var service = new ThrowingCancellationDownloadService();
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        Task operation = coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await coordinator.CancelAsync(discardPartial: true, CancellationToken.None);
        await operation.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual("download-cancellation-cleanup-failed", coordinator.State.ErrorCode);
    }

    [TestMethod]
    public async Task RetireAsync_IsIdempotentAndWaitsForNonCooperativeProvider()
    {
        var service = new NonCooperativeDownloadService();
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        Task operation = coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Task first = coordinator.RetireAsync();
        Task second = coordinator.RetireAsync();

        Assert.AreSame(first, second);
        Assert.IsFalse(first.IsCompleted);
        service.CompleteInterrupted();
        await first.WaitAsync(TimeSpan.FromSeconds(2));
        await operation.WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            coordinator.StartAsync(50, allowMetered: false, CancellationToken.None));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancelAsync_BoundsAProviderWhoseCancellationCallbackNeverReturns(bool discardPartial)
    {
        using var release = new ManualResetEventSlim(false);
        var service = new BlockingCancellationDownloadService(release);
        var disposing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reconciled = new TaskCompletionSource<ModelDownloadCoordinatorState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted),
            TimeSpan.FromMilliseconds(50),
            _ => disposing.TrySetResult());
        coordinator.StateChanged += (_, state) =>
        {
            if (state.Stage == ModelDownloadStage.Interrupted) reconciled.TrySetResult(state);
        };
        Task operation = coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        try
        {
            Task cancellation = coordinator.CancelAsync(discardPartial, CancellationToken.None);
            await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            service.CompleteInterrupted();
            await cancellation.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(ModelDownloadStage.Failed, coordinator.State.Stage);
            Assert.AreEqual("download-cancellation-cleanup-pending", coordinator.State.ErrorCode);
            Assert.AreEqual(0, service.DiscardCalls);
            await operation.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.IsFalse(disposing.Task.IsCompleted);
            service.ReportProgress();
            Assert.AreEqual("download-cancellation-cleanup-pending", coordinator.State.ErrorCode);
        }
        finally { release.Set(); }
        await service.CallbackFinished.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await disposing.Task.WaitAsync(TimeSpan.FromSeconds(1));
        ModelDownloadCoordinatorState terminal = await reconciled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.AreEqual(discardPartial ? "download-cancelled-discarded" : "download-cancelled", terminal.ErrorCode);
    }

    [TestMethod]
    public async Task DeferredCancellationReconciliationBlocksReplacementUntilQuiescent()
    {
        using var release = new ManualResetEventSlim(false);
        var service = new BlockingCancellationDownloadService(release);
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted),
            TimeSpan.FromMilliseconds(50));
        Task first = coordinator.StartAsync(50, false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Task cancellation = coordinator.CancelAsync(false, CancellationToken.None);
        await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        service.CompleteInterrupted();
        await Task.WhenAll(first, cancellation).WaitAsync(TimeSpan.FromSeconds(1));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.StartAsync(65, false, CancellationToken.None));
        release.Set();
        await coordinator.CancellationReconciliation.WaitAsync(TimeSpan.FromSeconds(1));

        ModelDownloadOperationId old = coordinator.State.OperationId!.Value;
        await coordinator.StartAsync(65, false, CancellationToken.None);
        Assert.AreNotEqual(old, coordinator.State.OperationId);
    }

    [TestMethod]
    public async Task CancellationAuthorityWinsWhenCallbackCompletesProviderWithSuccess()
    {
        var service = new SuccessOnCancellationDownloadService();
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        int verifiedEvents = 0;
        coordinator.VerifiedModelAvailable += (_, _) => verifiedEvents++;
        Task operation = coordinator.StartAsync(50, false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await coordinator.CancelAsync(false, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
        await operation.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.AreEqual(ModelDownloadStage.Interrupted, coordinator.State.Stage);
        Assert.AreEqual("download-cancelled", coordinator.State.ErrorCode);
        Assert.AreEqual(0, verifiedEvents);
    }

    [TestMethod]
    public async Task QuiescedDiscardFailureSettlesAndDoesNotStrandReplacementAuthority()
    {
        var service = new FakeDownloadService
        {
            WaitForCancellation = true,
            DiscardException = new IOException("synthetic cleanup failure")
        };
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        Task operation = coordinator.StartAsync(50, false, CancellationToken.None);
        await service.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await coordinator.CancelAsync(true, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
        await operation.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.AreEqual(ModelDownloadStage.Failed, coordinator.State.Stage);
        Assert.AreEqual("download-cancellation-cleanup-failed", coordinator.State.ErrorCode);
        service.WaitForCancellation = false;
        service.DiscardException = null;
        await coordinator.StartAsync(65, false, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
        Assert.AreEqual(ModelDownloadStage.Completed, coordinator.State.Stage);
    }

    [TestMethod]
    public async Task DeferredCancellationReconciliationCannotPublishAfterRetirement()
    {
        using var release = new ManualResetEventSlim(false);
        var service = new BlockingCancellationDownloadService(release);
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted),
            TimeSpan.FromMilliseconds(50));
        int publications = 0;
        coordinator.StateChanged += (_, _) => publications++;
        Task operation = coordinator.StartAsync(50, false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Task cancellation = coordinator.CancelAsync(false, CancellationToken.None);
        await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        service.CompleteInterrupted();
        await Task.WhenAll(operation, cancellation).WaitAsync(TimeSpan.FromSeconds(1));
        await coordinator.RetireAsync().WaitAsync(TimeSpan.FromSeconds(1));
        ModelDownloadCoordinatorState retiredState = coordinator.State;
        int retiredPublications = publications;

        release.Set();
        await coordinator.CancellationReconciliation.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.AreEqual(retiredState, coordinator.State);
        Assert.AreEqual(retiredPublications, publications);
    }

    [TestMethod]
    public async Task CallerCancelledWaitPreservesCleanupAuthorityUntilProviderQuiesces()
    {
        var service = new NonCooperativeDownloadService();
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted),
            TimeSpan.FromSeconds(1));
        using var waitCancellation = new CancellationTokenSource();
        Task operation = coordinator.StartAsync(50, false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Task cancellation = coordinator.CancelAsync(false, waitCancellation.Token);
        waitCancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await cancellation);
        Assert.AreEqual("download-cancellation-cleanup-pending", coordinator.State.ErrorCode);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.StartAsync(65, false, CancellationToken.None));

        service.CompleteInterrupted();
        await operation.WaitAsync(TimeSpan.FromSeconds(1));
        await coordinator.CancellationReconciliation.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.AreEqual(ModelDownloadStage.Interrupted, coordinator.State.Stage);
        Assert.AreEqual("download-cancelled", coordinator.State.ErrorCode);
    }

    [TestMethod]
    public async Task CompletionAtCancellationClaimCannotBeatAuthorityOrLeakCancellationTask()
    {
        var service = new NonCooperativeDownloadService();
        var disposing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted),
            cancellationDisposing: _ => disposing.TrySetResult(),
            cancellationAuthorityClaimed: service.CompleteSuccessfully);
        Task operation = coordinator.StartAsync(50, false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await coordinator.CancelAsync(false, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
        await operation.WaitAsync(TimeSpan.FromSeconds(1));
        await disposing.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.AreEqual(ModelDownloadStage.Interrupted, coordinator.State.Stage);
        Assert.AreEqual("download-cancelled", coordinator.State.ErrorCode);
        Assert.AreEqual(0, coordinator.CancellationTaskCount);
    }

    [TestMethod]
    public async Task CompletedObserverCancellationCannotRevokeCommittedHandoff()
    {
        var service = new FakeDownloadService();
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        Task? cancellation = null;
        coordinator.StateChanged += (_, state) =>
        {
            if (state.Stage == ModelDownloadStage.Completed)
                cancellation = coordinator.CancelAsync(true, CancellationToken.None);
        };

        await coordinator.StartAsync(50, false, CancellationToken.None);
        await (cancellation ?? Task.CompletedTask);

        Assert.AreEqual(ModelDownloadStage.Completed, coordinator.State.Stage);
        Assert.IsTrue(coordinator.TryClaimVerifiedModel(coordinator.State.OperationId!.Value, out _));
        Assert.AreEqual(0, service.DiscardCalls);
    }

    [TestMethod]
    public async Task RetirementDuringQuiescedDiscardSuppressesTerminalPublication()
    {
        var service = new BlockingDiscardDownloadService();
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        int publications = 0;
        coordinator.StateChanged += (_, _) => publications++;
        Task operation = coordinator.StartAsync(50, false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Task cancellation = coordinator.CancelAsync(true, CancellationToken.None);
        service.CompleteInterrupted();
        await service.DiscardStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await operation.WaitAsync(TimeSpan.FromSeconds(1));
        await coordinator.RetireAsync().WaitAsync(TimeSpan.FromSeconds(1));
        ModelDownloadCoordinatorState retiredState = coordinator.State;
        int retiredPublications = publications;

        service.ReleaseDiscard.TrySetResult();
        await cancellation.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.AreEqual(retiredState, coordinator.State);
        Assert.AreEqual(retiredPublications, publications);
    }

    [TestMethod]
    public async Task ReplacementRemainsBlockedUntilDiscardTerminalCommit()
    {
        var service = new BlockingDiscardDownloadService();
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Unrestricted));
        Task operation = coordinator.StartAsync(50, false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Task cancellation = coordinator.CancelAsync(true, CancellationToken.None);
        service.CompleteInterrupted();
        await service.DiscardStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await operation.WaitAsync(TimeSpan.FromSeconds(1));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.StartAsync(65, false, CancellationToken.None));
        service.ReleaseDiscard.TrySetResult();
        await cancellation.WaitAsync(TimeSpan.FromSeconds(1));
        ModelDownloadOperationId cancelledOperation = coordinator.State.OperationId!.Value;

        await coordinator.StartAsync(65, false, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
        Assert.AreNotEqual(cancelledOperation, coordinator.State.OperationId);
    }

    [TestMethod]
    public async Task InactiveInterruptedDiscardBlocksConcurrentResumeAndStart()
    {
        var service = new BlockingDiscardDownloadService();
        var coordinator = new ModelDownloadCoordinator(
            service,
            new FakeNetworkPolicy(ModelDownloadConnectionKind.Offline));
        await coordinator.StartAsync(50, false, CancellationToken.None);
        Assert.AreEqual(ModelDownloadStage.Interrupted, coordinator.State.Stage);
        Task discard = coordinator.CancelAsync(true, CancellationToken.None);
        await service.DiscardStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.ResumeAsync(false, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.StartAsync(65, false, CancellationToken.None));

        service.ReleaseDiscard.TrySetResult();
        await discard.WaitAsync(TimeSpan.FromSeconds(1));
        ModelDownloadOperationId discardedOperation = coordinator.State.OperationId!.Value;
        await coordinator.StartAsync(65, false, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
        Assert.AreNotEqual(discardedOperation, coordinator.State.OperationId);
    }

    private sealed class FakeNetworkPolicy(ModelDownloadConnectionKind kind) : IModelDownloadNetworkPolicy
    {
        public ModelDownloadConnectionKind GetCurrentConnectionKind() => kind;
    }

    private sealed class FakeDownloadService : IModelDownloadService
    {
        internal VerifiedDownloadedModel Model { get; } = new(
            @"C:\private\model.gguf", "Granite 4.0 H-Micro", "catalog", 4, new string('a', 64));

        internal int DownloadCalls { get; private set; }
        internal int DiscardCalls { get; private set; }
        internal string? ResumeQuantisation { get; init; }
        internal bool WaitForCancellation { get; set; }
        internal bool DelayResumeProbe { get; init; }
        internal Exception? DiscardException { get; set; }
        internal TaskCompletionSource DownloadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ResumeProbeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseResumeProbe { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ModelDownloadResult> DownloadAsync(ModelDownloadCatalogEntry entry, IProgress<ModelDownloadProgress> progress, CancellationToken cancellationToken)
        {
            DownloadCalls++;
            DownloadStarted.TrySetResult();
            progress.Report(new ModelDownloadProgress(ModelDownloadStage.Downloading, 2, 4));
            if (WaitForCancellation)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return new ModelDownloadResult(ModelDownloadResultKind.Interrupted, null, "download-cancelled");
                }
            }
            return new ModelDownloadResult(ModelDownloadResultKind.Completed, Model, null);
        }

        public async Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken)
        {
            if (DelayResumeProbe)
            {
                ResumeProbeStarted.TrySetResult();
                await ReleaseResumeProbe.Task.WaitAsync(cancellationToken);
            }
            return entry.Quantisation == ResumeQuantisation
                ? new ModelDownloadResumeInfo(entry.Id, 2, entry.ExpectedByteLength, "\"v1\"")
                : null;
        }

        public Task DiscardPartialAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken)
        {
            DiscardCalls++;
            if (DiscardException is not null) return Task.FromException(DiscardException);
            return Task.CompletedTask;
        }
    }

    private sealed class NonCooperativeDownloadService : IModelDownloadService
    {
        private readonly TaskCompletionSource<ModelDownloadResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IProgress<ModelDownloadProgress>? _progress;
        private ModelDownloadCatalogEntry? _entry;

        internal TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ModelDownloadResult> DownloadAsync(
            ModelDownloadCatalogEntry entry,
            IProgress<ModelDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            _entry = entry;
            _progress = progress;
            Started.TrySetResult();
            return _completion.Task;
        }

        internal void ReportProgress(ModelDownloadStage stage, long downloadedBytes) =>
            _progress!.Report(new ModelDownloadProgress(
                stage,
                downloadedBytes,
                _entry!.ExpectedByteLength));

        internal void CompleteSuccessfully() =>
            _completion.TrySetResult(new ModelDownloadResult(
                ModelDownloadResultKind.Completed,
                new VerifiedDownloadedModel(
                    @"C:\private\late.gguf",
                    "late.gguf",
                    _entry!.Id,
                    _entry.ExpectedByteLength,
                    _entry.ExpectedSha256),
                null));

        internal void CompleteInterrupted() =>
            _completion.TrySetResult(new ModelDownloadResult(
                ModelDownloadResultKind.Interrupted,
                null,
                "download-interrupted"));

        public Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(
            ModelDownloadCatalogEntry entry,
            CancellationToken cancellationToken) =>
            Task.FromResult<ModelDownloadResumeInfo?>(null);

        public Task DiscardPartialAsync(
            ModelDownloadCatalogEntry entry,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class BlockingCancellationDownloadService(ManualResetEventSlim release) : IModelDownloadService
    {
        private readonly TaskCompletionSource<ModelDownloadResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IProgress<ModelDownloadProgress>? _progress;
        private ModelDownloadCatalogEntry? _entry;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CallbackStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CallbackFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int DiscardCalls { get; private set; }

        public Task<ModelDownloadResult> DownloadAsync(ModelDownloadCatalogEntry entry, IProgress<ModelDownloadProgress> progress, CancellationToken cancellationToken)
        {
            _progress = progress;
            _entry = entry;
            cancellationToken.Register(() =>
            {
                CallbackStarted.TrySetResult();
                release.Wait();
                CallbackFinished.TrySetResult();
            });
            Started.TrySetResult();
            return _completion.Task;
        }

        internal void CompleteInterrupted() => _completion.TrySetResult(new ModelDownloadResult(
            ModelDownloadResultKind.Interrupted, null, "download-cancelled"));
        internal void ReportProgress() => _progress!.Report(new ModelDownloadProgress(
            ModelDownloadStage.Downloading, 1, _entry!.ExpectedByteLength));

        public Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken) =>
            Task.FromResult<ModelDownloadResumeInfo?>(null);

        public Task DiscardPartialAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken)
        {
            DiscardCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingDiscardDownloadService : IModelDownloadService
    {
        private readonly TaskCompletionSource<ModelDownloadResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource DiscardStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseDiscard { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ModelDownloadResult> DownloadAsync(
            ModelDownloadCatalogEntry entry,
            IProgress<ModelDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return _completion.Task;
        }

        internal void CompleteInterrupted() => _completion.TrySetResult(new ModelDownloadResult(
            ModelDownloadResultKind.Interrupted, null, "download-cancelled"));

        public Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(
            ModelDownloadCatalogEntry entry,
            CancellationToken cancellationToken) => Task.FromResult<ModelDownloadResumeInfo?>(null);

        public async Task DiscardPartialAsync(
            ModelDownloadCatalogEntry entry,
            CancellationToken cancellationToken)
        {
            DiscardStarted.TrySetResult();
            await ReleaseDiscard.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class SuccessOnCancellationDownloadService : IModelDownloadService
    {
        private readonly TaskCompletionSource<ModelDownloadResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ModelDownloadResult> DownloadAsync(ModelDownloadCatalogEntry entry, IProgress<ModelDownloadProgress> progress, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => _completion.TrySetResult(new ModelDownloadResult(
                ModelDownloadResultKind.Completed,
                new VerifiedDownloadedModel(@"C:\private\late.gguf", "late.gguf", entry.Id, entry.ExpectedByteLength, entry.ExpectedSha256),
                null)));
            Started.TrySetResult();
            return _completion.Task;
        }

        public Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken) => Task.FromResult<ModelDownloadResumeInfo?>(null);
        public Task DiscardPartialAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ThrowingCancellationDownloadService : IModelDownloadService
    {
        private readonly TaskCompletionSource<ModelDownloadResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ModelDownloadResult> DownloadAsync(
            ModelDownloadCatalogEntry entry,
            IProgress<ModelDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.Register(() =>
            {
                _completion.TrySetResult(new ModelDownloadResult(
                    ModelDownloadResultKind.Interrupted,
                    null,
                    "download-cancelled"));
                throw new InvalidOperationException("callback failure");
            });
            Started.TrySetResult();
            return _completion.Task;
        }

        public Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(
            ModelDownloadCatalogEntry entry,
            CancellationToken cancellationToken) =>
            Task.FromResult<ModelDownloadResumeInfo?>(null);

        public Task DiscardPartialAsync(
            ModelDownloadCatalogEntry entry,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
