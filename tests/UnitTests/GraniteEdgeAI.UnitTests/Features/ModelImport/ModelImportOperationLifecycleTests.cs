using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportOperationLifecycleTests
{
    [UITestMethod]
    public async Task SecondSelection_PreventsLateFirstResultFromReplacingCurrentSelection()
    {
        var classifier = new ControllableClassifier();
        var page = CreatePage(classifier);
        Task first = page.SubmitInputAsync(new ModelSelectionInput(@"C:\Models\first.gguf", "first.gguf", false));
        Task second = page.SubmitInputAsync(new ModelSelectionInput(@"C:\Models\openvino", "openvino", true));

        classifier.CompleteSecond(ModelSelectionRoute.OpenVinoDirectory);
        await second;
        classifier.CompleteFirst(ModelSelectionRoute.Gguf);
        await first;

        Assert.AreEqual(ModelSelectionRoute.OpenVinoDirectory, page.CurrentRoute);
        Assert.IsTrue(page.HasValidatedModel);
        Assert.IsNull(page.SelectedModelPath);
    }

    private static ModelImportPage CreatePage(IModelSelectionClassifier classifier) => new(
        () => Task.FromResult(ModelFormatSelection.Gguf),
        () => Task.FromResult<string?>(null),
        (_, _, _) => Task.FromResult(ModelQuickScanResult.CreateCancelled()),
        classifier: classifier);

    private sealed class ControllableClassifier : IModelSelectionClassifier
    {
        private readonly TaskCompletionSource<ModelSelectionResult> first = new();
        private readonly TaskCompletionSource<ModelSelectionResult> second = new();
        private ModelSelectionOperationId firstId;
        private ModelSelectionOperationId secondId;
        private int callCount;

        public Task<ModelSelectionResult> ClassifyAsync(ModelSelectionOperationId id, ModelSelectionInput input, CancellationToken token)
        {
            if (Interlocked.Increment(ref callCount) == 1) { firstId = id; return first.Task; }
            secondId = id;
            return second.Task;
        }

        internal void CompleteFirst(ModelSelectionRoute route) => first.SetResult(ModelSelectionResult.Accepted(firstId, route, "first.gguf"));
        internal void CompleteSecond(ModelSelectionRoute route) => second.SetResult(ModelSelectionResult.Accepted(secondId, route, "folder"));
    }
}
