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
public sealed class ModelImportInputParityTests
{
    [UITestMethod]
    public async Task PickerAndDropEquivalentGguf_UseTheSameAcceptedRouteAndState()
    {
        ModelSelectionInput input = new(@"C:\Models\granite.gguf", "granite.gguf", false);
        var pickerPage = CreatePage();
        var dropPage = CreatePage();

        await pickerPage.BrowseFilesAsync();
        await dropPage.SubmitInputAsync(input);

        Assert.AreEqual(ModelSelectionRoute.Gguf, pickerPage.CurrentRoute);
        Assert.AreEqual(pickerPage.CurrentRoute, dropPage.CurrentRoute);
        Assert.AreEqual(pickerPage.HasValidatedModel, dropPage.HasValidatedModel);
    }

    private static ModelImportPage CreatePage() => new(
        () => Task.FromResult(ModelFormatSelection.Gguf),
        () => Task.FromResult<string?>(@"C:\Models\granite.gguf"),
        (_, _, _) => Task.FromResult(Success()),
        classifier: new StubClassifier(ModelSelectionRoute.Gguf));

    private static ModelQuickScanResult Success() => ModelQuickScanResult.CreateSuccess(
        "Granite", "granite", "3B", "Q4", 64, 4096, 3);

    private sealed class StubClassifier(ModelSelectionRoute route) : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(ModelSelectionOperationId id, ModelSelectionInput input, CancellationToken token) =>
            Task.FromResult(ModelSelectionResult.Accepted(id, route, input.DisplayName));
    }
}
