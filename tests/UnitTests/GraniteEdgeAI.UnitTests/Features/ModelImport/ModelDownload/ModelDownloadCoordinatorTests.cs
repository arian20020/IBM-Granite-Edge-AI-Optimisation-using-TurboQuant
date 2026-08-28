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
        internal bool WaitForCancellation { get; init; }
        internal TaskCompletionSource DownloadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        public Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken) =>
            Task.FromResult<ModelDownloadResumeInfo?>(entry.Quantisation == ResumeQuantisation
                ? new ModelDownloadResumeInfo(entry.Id, 2, entry.ExpectedByteLength, "\"v1\"")
                : null);

        public Task DiscardPartialAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken)
        {
            DiscardCalls++;
            return Task.CompletedTask;
        }
    }
}
