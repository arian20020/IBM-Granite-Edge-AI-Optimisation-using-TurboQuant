using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.DownloadedModels;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class DownloadedModelConsentTests
{
    [UITestMethod]
    public async Task DeclinedConsent_NeverEnumeratesAnyLocation()
    {
        var finder = new RecordingFinder();
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.None),
            () => Task.FromResult<string?>(null),
            (_, _, _) => Task.FromResult(ModelQuickScanResult.CreateCancelled()),
            downloadedModelFinder: finder);

        await page.FindDownloadedModelsAsync(consent: false);

        Assert.AreEqual(0, finder.FindCallCount);
        Assert.AreEqual(0, finder.EnumerateCallCount);
    }

    private sealed class RecordingFinder : IDownloadedModelFinder
    {
        internal int FindCallCount { get; private set; }
        internal int EnumerateCallCount { get; private set; }

        public Task<DownloadedModelSearchResult> FindAsync(
            DownloadedModelSearchPolicy policy,
            CancellationToken cancellationToken)
        {
            FindCallCount++;
            EnumerateCallCount++;
            return Task.FromResult(new DownloadedModelSearchResult(Array.Empty<string>()));
        }

        public void ClearResults() { }
    }
}
