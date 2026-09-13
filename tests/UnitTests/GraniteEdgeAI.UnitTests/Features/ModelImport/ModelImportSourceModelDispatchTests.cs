using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportSourceModelDispatchTests
{
    [UITestMethod]
    public async Task ValidSourceFolder_RaisesConversionRequiredIntentExactlyOnce()
    {
        var page = CreatePage(new ImmediateSourceClassifier());
        SourceModelConversionRequestedEventArgs? captured = null;
        int requestCount = 0;
        page.SourceModelConversionRequested += (_, eventArguments) =>
        {
            requestCount++;
            captured = eventArguments;
        };

        await page.SubmitInputAsync(new ModelSelectionInput(
            @"C:\Models\private-source-package",
            "private-source-package",
            isFolder: true));

        Assert.IsTrue(page.TryRequestModelInspection());
        Assert.IsFalse(page.TryRequestModelInspection());
        Assert.AreEqual(1, requestCount);
        Assert.IsNotNull(captured);
        Assert.AreEqual(ModelSelectionRoute.SourceModelDirectory, captured.Selection.Route);
        Assert.AreEqual("private-source-package", captured.Selection.DisplayName);
    }

    [UITestMethod]
    public async Task ReplacedAcceptedSourceFolder_DoesNotDispatchTheRetiredOperation()
    {
        var classifier = new ControllableClassifier();
        var page = CreatePage(classifier);
        int requestCount = 0;
        page.SourceModelConversionRequested += (_, _) => requestCount++;

        Task first = page.SubmitInputAsync(new ModelSelectionInput(
            @"C:\Models\first-source", "first-source", isFolder: true));
        Task second = page.SubmitInputAsync(new ModelSelectionInput(
            @"C:\Models\second-source", "second-source", isFolder: true));
        classifier.CompleteSecond();
        await second;
        classifier.CompleteFirst();
        await first;

        Assert.IsTrue(page.TryRequestModelInspection());
        Assert.AreEqual(1, requestCount);
    }

    [UITestMethod]
    public async Task CancelledAcceptedSourceFolder_DoesNotDispatchConversionIntent()
    {
        var page = CreatePage(new ImmediateSourceClassifier());
        int requestCount = 0;
        page.SourceModelConversionRequested += (_, _) => requestCount++;

        await page.SubmitInputAsync(new ModelSelectionInput(
            @"C:\Models\private-source-package",
            "private-source-package",
            isFolder: true));
        page.CancelSelection();

        Assert.IsFalse(page.TryRequestModelInspection());
        Assert.AreEqual(0, requestCount);
    }

    [TestMethod]
    public void ConversionIntent_ExposesNoPathOrExecutionCapability()
    {
        Assert.IsFalse(typeof(SourceModelConversionRequestedEventArgs).GetProperties()
            .Any(property => property.Name.Contains("path", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(typeof(SourceModelConversionRequestedEventArgs).GetMethods()
            .Any(method => method.Name.Contains("process", StringComparison.OrdinalIgnoreCase) ||
                           method.Name.Contains("convert", StringComparison.OrdinalIgnoreCase)));
    }

    private static ModelImportPage CreatePage(IModelSelectionClassifier classifier) => new(
        () => Task.FromResult(ModelFormatSelection.Gguf),
        () => Task.FromResult<string?>(null),
        (_, _, _) => Task.FromResult(ModelQuickScanResult.CreateCancelled()),
        classifier: classifier);

    private sealed class ImmediateSourceClassifier : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(
            ModelSelectionOperationId id,
            ModelSelectionInput input,
            CancellationToken cancellationToken) =>
            Task.FromResult(ModelSelectionResult.Accepted(
                id,
                ModelSelectionRoute.SourceModelDirectory,
                input.DisplayName));
    }

    private sealed class ControllableClassifier : IModelSelectionClassifier
    {
        private readonly TaskCompletionSource<ModelSelectionResult> first = new();
        private readonly TaskCompletionSource<ModelSelectionResult> second = new();
        private ModelSelectionOperationId firstId;
        private ModelSelectionOperationId secondId;
        private int calls;

        public Task<ModelSelectionResult> ClassifyAsync(
            ModelSelectionOperationId id,
            ModelSelectionInput input,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstId = id;
                return first.Task;
            }

            secondId = id;
            return second.Task;
        }

        internal void CompleteFirst() => first.SetResult(ModelSelectionResult.Accepted(
            firstId, ModelSelectionRoute.SourceModelDirectory, "first-source"));
        internal void CompleteSecond() => second.SetResult(ModelSelectionResult.Accepted(
            secondId, ModelSelectionRoute.SourceModelDirectory, "second-source"));
    }
}
