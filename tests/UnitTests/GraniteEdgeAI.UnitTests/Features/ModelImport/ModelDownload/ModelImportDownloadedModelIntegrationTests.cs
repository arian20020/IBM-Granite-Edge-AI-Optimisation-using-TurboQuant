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
            Assert.IsTrue(page.TryClaimVerifiedDownloadInspection(ready.OperationId, out ModelInspectionRequest? request));
            Assert.IsNotNull(request);
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

    private sealed class UnrestrictedPolicy : IModelDownloadNetworkPolicy
    {
        public ModelDownloadConnectionKind GetCurrentConnectionKind() => ModelDownloadConnectionKind.Unrestricted;
    }

    private sealed class AcceptedGgufClassifier : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(ModelSelectionOperationId id, ModelSelectionInput input, CancellationToken token) =>
            Task.FromResult(ModelSelectionResult.Accepted(id, ModelSelectionRoute.Gguf, input.DisplayName));
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
