using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Threading;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportPageStateMachineTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CancelledScan_RestoresAwaitingSelection()
    {
        const string selectedPath = @"C:\Models\cancelled-model.gguf";
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPath),
            (format, path, cancellationToken) =>
                Task.FromResult(ModelQuickScanResult.CreateCancelled()));

        await page.BrowseFilesAsync();

        var card = (ImportModelCard)page.FindName("ImportModelCardControl");
        var continueButton = (Button)page.FindName(
            "ContinueToModelInspectionButton");

        Assert.IsNull(page.SelectedModelPath);
        Assert.IsNull(page.ValidatedScanResult);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(continueButton.IsEnabled);
        Assert.AreEqual(
            ImportModelCardState.AwaitingSelection,
            card.CurrentState);
        Assert.AreEqual(
            Visibility.Visible,
            GetElement(card, "AwaitingSelectionView").Visibility);
        Assert.AreEqual(
            Visibility.Collapsed,
            GetElement(card, "ScanningView").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task StartingSecondScan_CancelsFirstAndKeepsSecondResult()
    {
        const string firstPath = @"C:\Models\first-model.gguf";
        const string secondPath = @"C:\Models\second-model.gguf";
        string[] selectedPaths = [firstPath, secondPath];
        int pickerIndex = 0;
        CancellationToken firstScanToken = default;
        var firstScanStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstScanCompletion =
            new TaskCompletionSource<ModelQuickScanResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        ModelQuickScanResult secondScanResult =
            ModelQuickScanResult.CreateSuccess(
                modelName: "Second Granite Model",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantization: "Q4_K_M",
                fileSizeBytes: 2_000_000_000L,
                contextLength: 131_072UL,
                ggufVersion: 3);

        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPaths[pickerIndex++]),
            (format, path, cancellationToken) =>
            {
                if (path == firstPath)
                {
                    firstScanToken = cancellationToken;
                    firstScanStarted.TrySetResult(true);
                    return firstScanCompletion.Task;
                }

                Assert.AreEqual(secondPath, path);
                return Task.FromResult(secondScanResult);
            });

        Task firstBrowse = page.BrowseFilesAsync();
        await firstScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await page.BrowseFilesAsync();

        firstScanCompletion.SetResult(
            ModelQuickScanResult.CreateFailure(
                failureCode: "late-first-result",
                userMessage: "The first scan completed too late.",
                technicalMessage: "Late result used only by the test."));
        await firstBrowse;

        var card = (ImportModelCard)page.FindName("ImportModelCardControl");
        var continueButton = (Button)page.FindName(
            "ContinueToModelInspectionButton");

        Assert.IsTrue(firstScanToken.IsCancellationRequested);
        Assert.AreEqual(secondPath, page.SelectedModelPath);
        Assert.AreSame(secondScanResult, page.ValidatedScanResult);
        Assert.IsTrue(page.HasValidatedModel);
        Assert.IsTrue(continueButton.IsEnabled);
        Assert.AreEqual(
            ImportModelCardState.ScanSucceeded,
            card.CurrentState);
        Assert.AreEqual(
            "Second Granite Model",
            GetTextBlock(card, "SuccessModelNameTextBlock").Text);
    }

    private static FrameworkElement GetElement(
        ImportModelCard card,
        string elementName)
    {
        return (FrameworkElement)card.FindName(elementName);
    }

    private static TextBlock GetTextBlock(
        ImportModelCard card,
        string elementName)
    {
        return (TextBlock)card.FindName(elementName);
    }
}
