using GraniteEdgeAI.Features.ModelImport.DownloadedModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class DownloadedModelFinderTests
{
    [TestMethod]
    public async Task FindAsync_StopsAtDeclaredItemBoundAndReturnsSafeNamesOnly()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            for (int index = 0; index < 4; index++)
            {
                File.WriteAllText(Path.Combine(root, $"model-{index}.gguf"), string.Empty);
            }

            var finder = new BoundedDownloadedModelFinder(
                _ => root,
                maximumChildDepth: 1);
            var policy = new DownloadedModelSearchPolicy(
                [KnownFolderId.Downloads], 1, 2, TimeSpan.FromSeconds(10));

            DownloadedModelSearchResult result = await finder.FindAsync(
                policy, CancellationToken.None);

            Assert.IsTrue(finder.EnumerateCallCount <= 1);
            Assert.IsTrue(finder.VisitedItemCount <= 2);
            Assert.IsTrue(result.DisplayNames.Count <= 2);
            Assert.IsFalse(string.Join("|", result.DisplayNames).Contains(root, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task FindAsync_CancellationClearsRetainedResults()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "model.gguf"), string.Empty);
            var finder = new BoundedDownloadedModelFinder(_ => root);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            try
            {
                await finder.FindAsync(DownloadedModelSearchPolicy.Default, cancellation.Token);
                Assert.Fail("A cancelled search must not complete successfully.");
            }
            catch (OperationCanceledException)
            {
                // TaskCanceledException is the expected Task-based cancellation form.
            }

            Assert.AreEqual(0, finder.LastSafeResults.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void SearchResult_ExposesNoPathBearingProperty()
    {
        Assert.IsFalse(typeof(DownloadedModelSearchResult).GetProperties()
            .Any(property => property.Name.Contains("path", StringComparison.OrdinalIgnoreCase)));
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
