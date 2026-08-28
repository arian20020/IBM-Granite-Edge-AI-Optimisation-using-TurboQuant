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

    private sealed class FakeNetworkPolicy(ModelDownloadConnectionKind kind) : IModelDownloadNetworkPolicy
    {
        public ModelDownloadConnectionKind GetCurrentConnectionKind() => kind;
    }

    private sealed class FakeDownloadService : IModelDownloadService
    {
        internal VerifiedDownloadedModel Model { get; } = new(
            @"C:\private\model.gguf", "Granite 4.0 H-Micro", "catalog", 4, new string('a', 64));

        internal int DownloadCalls { get; private set; }

        public Task<ModelDownloadResult> DownloadAsync(ModelDownloadCatalogEntry entry, IProgress<ModelDownloadProgress> progress, CancellationToken cancellationToken)
        {
            DownloadCalls++;
            progress.Report(new ModelDownloadProgress(ModelDownloadStage.Downloading, 2, 4));
            return Task.FromResult(new ModelDownloadResult(ModelDownloadResultKind.Completed, Model, null));
        }

        public Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken) =>
            Task.FromResult<ModelDownloadResumeInfo?>(null);

        public Task DiscardPartialAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
