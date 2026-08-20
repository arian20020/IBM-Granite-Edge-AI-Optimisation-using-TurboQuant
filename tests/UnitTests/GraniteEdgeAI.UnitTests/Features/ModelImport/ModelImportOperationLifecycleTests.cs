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

    [UITestMethod]
    public async Task OpenVinoIntent_IsRaisedOnlyOnceWhenContinueIsRequested()
    {
        var page = CreatePage(new ImmediateClassifier(ModelSelectionRoute.OpenVinoDirectory));
        int requestCount = 0;
        page.OpenVinoInspectionRequested += (_, _) => requestCount++;

        await page.SubmitInputAsync(new ModelSelectionInput(@"C:\Models\openvino", "openvino", true));

        Assert.AreEqual(0, requestCount);
        Assert.IsTrue(page.TryRequestModelInspection());
        Assert.IsFalse(page.TryRequestModelInspection());
        Assert.AreEqual(1, requestCount);
    }

    [UITestMethod]
    public async Task Cancel_PreventsLateClassifierSuccessFromRestoringSelection()
    {
        var classifier = new ControllableClassifier();
        var page = CreatePage(classifier);
        Task pending = page.SubmitInputAsync(new ModelSelectionInput(@"C:\Models\first.gguf", "first.gguf", false));

        page.CancelSelection();
        classifier.CompleteFirst(ModelSelectionRoute.Gguf);
        await pending;

        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsNull(page.CurrentRoute);
    }

    [UITestMethod]
    public async Task NavigationAway_PreventsLateClassifierSuccessFromRestoringSelection()
    {
        var classifier = new ControllableClassifier();
        var page = CreatePage(classifier);
        Task pending = page.SubmitInputAsync(new ModelSelectionInput(@"C:\Models\first.gguf", "first.gguf", false));

        page.RetireSelectionForNavigation();
        classifier.CompleteFirst(ModelSelectionRoute.Gguf);
        await pending;

        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsNull(page.CurrentRoute);
    }

    [UITestMethod]
    public async Task WrongOperationId_IsIgnoredWithoutDispatchOrState()
    {
        var page = CreatePage(new WrongIdClassifier());
        int eventCount = 0;
        page.OpenVinoInspectionRequested += (_, _) => eventCount++;

        await page.SubmitInputAsync(new ModelSelectionInput(@"C:\Models\openvino", "openvino", true));

        Assert.IsNull(page.CurrentRoute);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.AreEqual(0, eventCount);
    }

    [UITestMethod]
    public async Task FolderSelection_ReplacesAcceptedGgufWithoutRetainingFilePath()
    {
        var page = CreatePage(new ShapeClassifier());
        await page.SubmitInputAsync(new ModelSelectionInput(@"C:\Models\first.gguf", "first.gguf", false));
        await page.SubmitInputAsync(new ModelSelectionInput(@"C:\Models\openvino", "openvino", true));

        Assert.AreEqual(ModelSelectionRoute.OpenVinoDirectory, page.CurrentRoute);
        Assert.IsNull(page.SelectedModelPath);
        Assert.IsNull(page.ValidatedScanResult);
    }

    [TestMethod]
    public void FolderIntentArgs_ExposeNoPathBearingProperty()
    {
        foreach (Type type in new[] { typeof(OpenVinoInspectionRequestedEventArgs), typeof(SourceModelInspectionRequestedEventArgs) })
        {
            Assert.IsFalse(type.GetProperties().Any(property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)));
        }
    }

    [UITestMethod]
    public async Task ReplacementDuringDelayedQuickScan_DoesNotDisposeTokenOrApplyStaleResult()
    {
        var scanStarted = new TaskCompletionSource<CancellationToken>();
        var releaseScan = new TaskCompletionSource<ModelQuickScanResult>();
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(null),
            async (_, _, token) =>
            {
                scanStarted.SetResult(token);
                return await releaseScan.Task;
            },
            classifier: new ShapeClassifier());

        Task first = page.SubmitInputAsync(new ModelSelectionInput(@"C:\Models\first.gguf", "first.gguf", false));
        CancellationToken token = await scanStarted.Task;
        Task replacement = page.SubmitInputAsync(new ModelSelectionInput(@"C:\Models\openvino", "openvino", true));
        await replacement;

        using CancellationTokenRegistration registration = token.Register(() => { });
        releaseScan.SetResult(ModelQuickScanResult.CreateSuccess("Granite", "granite", "3B", "Q4", 64, 4096, 3));
        await first;

        Assert.AreEqual(ModelSelectionRoute.OpenVinoDirectory, page.CurrentRoute);
        Assert.IsNull(page.ValidatedScanResult);
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

    private sealed class ImmediateClassifier(ModelSelectionRoute route) : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(ModelSelectionOperationId id, ModelSelectionInput input, CancellationToken token) =>
            Task.FromResult(ModelSelectionResult.Accepted(id, route, input.DisplayName));
    }

    private sealed class WrongIdClassifier : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(ModelSelectionOperationId id, ModelSelectionInput input, CancellationToken token) =>
            Task.FromResult(ModelSelectionResult.Accepted(ModelSelectionOperationId.CreateNew(), ModelSelectionRoute.OpenVinoDirectory, input.DisplayName));
    }

    private sealed class ShapeClassifier : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(ModelSelectionOperationId id, ModelSelectionInput input, CancellationToken token) =>
            Task.FromResult(ModelSelectionResult.Accepted(id, input.IsFolder ? ModelSelectionRoute.OpenVinoDirectory : ModelSelectionRoute.Gguf, input.DisplayName));
    }
}
