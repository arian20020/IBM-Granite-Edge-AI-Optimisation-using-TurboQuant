using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportDownloadedModelIntegrationTests
{
    [UITestMethod]
    public async Task VerifiedDownload_UsesExistingSelectionPipelineAndRequestsInspectionOnce()
    {
        string root = Path.Combine(Path.GetTempPath(), "GraniteEdgeAI-DownloadHandoff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "granite.gguf");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
        DateTimeOffset timestamp = new(File.GetLastWriteTimeUtc(path));
        try
        {
            var service = new CompletedDownloadService(path);
            var coordinator = new ModelDownloadCoordinator(service, new UnrestrictedPolicy());
            var page = new ModelImportPage(
                () => Task.FromResult(ModelFormatSelection.None),
                () => Task.FromResult<string?>(null),
                (_, _, _) => Task.FromResult(ModelQuickScanResult.CreateSuccess(
                    "Granite", "granite", "3B", "Q4_K_M", 4, 4096, 3, timestamp)),
                classifier: new AcceptedGgufClassifier(),
                modelDownloadCoordinator: coordinator);
            VerifiedDownloadInspectionReadyEventArgs? ready = null;
            page.VerifiedDownloadInspectionReady += (_, value) => ready = value;

            await coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);

            Assert.IsNotNull(ready);
            Assert.IsFalse(ready.GetType().GetProperties().Any(property =>
                property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)
                || property.PropertyType == typeof(ModelInspectionRequest)));
            Assert.IsFalse(page.TryClaimVerifiedDownloadInspection(ModelDownloadOperationId.CreateNew(), out _));
            using var claimGate = new ManualResetEventSlim(false);
            Task<(bool Claimed, ModelInspectionRequest? Request)> firstClaim = Task.Run(() =>
            {
                claimGate.Wait();
                bool claimed = page.TryClaimVerifiedDownloadInspection(ready.OperationId, out ModelInspectionRequest? value);
                return (claimed, value);
            });
            Task<(bool Claimed, ModelInspectionRequest? Request)> secondClaim = Task.Run(() =>
            {
                claimGate.Wait();
                bool claimed = page.TryClaimVerifiedDownloadInspection(ready.OperationId, out ModelInspectionRequest? value);
                return (claimed, value);
            });
            claimGate.Set();
            (bool Claimed, ModelInspectionRequest? Request)[] claims = await Task.WhenAll(firstClaim, secondClaim);
            Assert.AreEqual(1, claims.Count(value => value.Claimed));
            ModelInspectionRequest? request = claims.Single(value => value.Claimed).Request;
            Assert.IsNotNull(request);
            Assert.AreEqual(path, request.ModelPath);
            Assert.AreEqual(4, request.ExpectedFileIdentity.LengthBytes);
            Assert.IsFalse(page.TryClaimVerifiedDownloadInspection(ready.OperationId, out _));
            Assert.IsTrue(page.HasValidatedModel);
            Assert.AreEqual(ModelSelectionRoute.Gguf, page.CurrentRoute);
            Assert.IsFalse(coordinator.TryClaimVerifiedModel(coordinator.State.OperationId!.Value, out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [UITestMethod]
    public async Task PickerReplacementAndRetirementRevokeAutomaticInspectionAuthority()
    {
        string root = Path.Combine(Path.GetTempPath(), "GraniteEdgeAI-DownloadHandoff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "granite.gguf");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
        DateTimeOffset timestamp = new(File.GetLastWriteTimeUtc(path));
        try
        {
            var coordinator = new ModelDownloadCoordinator(new CompletedDownloadService(path), new UnrestrictedPolicy());
            var page = new ModelImportPage(
                () => Task.FromResult(ModelFormatSelection.None),
                () => Task.FromResult<string?>(null),
                (_, _, _) => Task.FromResult(ModelQuickScanResult.CreateSuccess(
                    "Granite", "granite", "3B", "Q4_K_M", 4, 4096, 3, timestamp)),
                classifier: new AcceptedGgufClassifier(),
                modelDownloadCoordinator: coordinator);
            VerifiedDownloadInspectionReadyEventArgs? ready = null;
            page.VerifiedDownloadInspectionReady += (_, value) => ready = value;

            await coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
            Assert.IsNotNull(ready);
            ModelDownloadOperationId replacedByDownload = ready.OperationId;
            ready = null;
            await coordinator.StartAsync(65, allowMetered: false, CancellationToken.None);
            Assert.IsNotNull(ready);
            Assert.IsFalse(page.TryClaimVerifiedDownloadInspection(replacedByDownload, out _));
            ModelDownloadOperationId pickerRevoked = ready.OperationId;
            await page.BrowseFilesAsync();
            Assert.IsFalse(page.TryClaimVerifiedDownloadInspection(pickerRevoked, out _));

            ready = null;
            await coordinator.StartAsync(80, allowMetered: false, CancellationToken.None);
            Assert.IsNotNull(ready);
            ModelDownloadOperationId retirementRevoked = ready.OperationId;
            await page.RetireForNavigationAsync().WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsFalse(page.TryClaimVerifiedDownloadInspection(retirementRevoked, out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [UITestMethod]
    public async Task RetirementDuringAutomaticSubmitWaitsAndSuppressesLateInspectionReady()
    {
        string root = Path.Combine(Path.GetTempPath(), "GraniteEdgeAI-DownloadHandoff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "granite.gguf");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
        try
        {
            var classifier = new DeferredAcceptedClassifier();
            var coordinator = new ModelDownloadCoordinator(new CompletedDownloadService(path), new UnrestrictedPolicy());
            int scans = 0;
            int readyEvents = 0;
            var page = new ModelImportPage(
                () => Task.FromResult(ModelFormatSelection.None),
                () => Task.FromResult<string?>(null),
                (_, _, _) =>
                {
                    scans++;
                    throw new InvalidOperationException("A retired automatic submission must not scan.");
                },
                classifier: classifier,
                modelDownloadCoordinator: coordinator,
                automaticHandoffRetirementTimeout: TimeSpan.FromMilliseconds(50));
            page.VerifiedDownloadInspectionReady += (_, _) => readyEvents++;

            await coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
            CancellationToken submitToken = await classifier.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Task retirement = page.RetireForNavigationAsync();

            Assert.IsTrue(submitToken.IsCancellationRequested);
            await retirement.WaitAsync(TimeSpan.FromSeconds(2));
            classifier.Release();

            Assert.AreEqual(0, scans);
            Assert.AreEqual(0, readyEvents);
            Assert.IsFalse(page.TryClaimVerifiedDownloadInspection(ModelDownloadOperationId.CreateNew(), out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [UITestMethod]
    public async Task ReentrantRetirementFromReadyNotificationRevokesClaimWithoutDeadlock()
    {
        string root = Path.Combine(Path.GetTempPath(), "GraniteEdgeAI-DownloadHandoff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "granite.gguf");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
        DateTimeOffset timestamp = new(File.GetLastWriteTimeUtc(path));
        try
        {
            var coordinator = new ModelDownloadCoordinator(new CompletedDownloadService(path), new UnrestrictedPolicy());
            var page = new ModelImportPage(
                () => Task.FromResult(ModelFormatSelection.None),
                () => Task.FromResult<string?>(null),
                (_, _, _) => Task.FromResult(ModelQuickScanResult.CreateSuccess(
                    "Granite", "granite", "3B", "Q4_K_M", 4, 4096, 3, timestamp)),
                classifier: new AcceptedGgufClassifier(),
                modelDownloadCoordinator: coordinator);
            Task? retirement = null;
            ModelDownloadOperationId? operationId = null;
            page.VerifiedDownloadInspectionReady += (_, value) =>
            {
                operationId = value.OperationId;
                retirement = page.RetireForNavigationAsync();
            };

            await coordinator.StartAsync(50, allowMetered: false, CancellationToken.None);
            Assert.IsNotNull(retirement);
            await retirement.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.IsTrue(operationId.HasValue);
            Assert.IsFalse(page.TryClaimVerifiedDownloadInspection(operationId.Value, out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class UnrestrictedPolicy : IModelDownloadNetworkPolicy
    {
        public ModelDownloadConnectionKind GetCurrentConnectionKind() => ModelDownloadConnectionKind.Unrestricted;
    }

    private sealed class AcceptedGgufClassifier : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(ModelSelectionOperationId id, ModelSelectionInput input, CancellationToken token) =>
            Task.FromResult(ModelSelectionResult.Accepted(id, ModelSelectionRoute.Gguf, input.DisplayName));
    }

    private sealed class DeferredAcceptedClassifier : IModelSelectionClassifier
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<CancellationToken> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ModelSelectionResult> ClassifyAsync(ModelSelectionOperationId id, ModelSelectionInput input, CancellationToken token)
        {
            Started.TrySetResult(token);
            await _release.Task;
            return ModelSelectionResult.Accepted(id, ModelSelectionRoute.Gguf, input.DisplayName);
        }

        internal void Release() => _release.TrySetResult();
    }

    private sealed class CompletedDownloadService(string path) : IModelDownloadService
    {
        public Task<ModelDownloadResult> DownloadAsync(ModelDownloadCatalogEntry entry, IProgress<ModelDownloadProgress> progress, CancellationToken cancellationToken) =>
            Task.FromResult(new ModelDownloadResult(
                ModelDownloadResultKind.Completed,
                new VerifiedDownloadedModel(path, "granite.gguf", entry.Id, 4, entry.ExpectedSha256),
                null));

        public Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken) =>
            Task.FromResult<ModelDownloadResumeInfo?>(null);

        public Task DiscardPartialAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
