using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
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
                Task.FromResult(ModelQuickScanResult.CreateCancelled()),
            classifier: new GgufSelectionTestClassifier());

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
            },
            classifier: new GgufSelectionTestClassifier());

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

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CancelButton_IgnoresLateSuccessResult()
    {
        const string selectedPath = @"C:\Models\slow-success.gguf";
        CancellationToken scanToken = default;
        var scanCompletion =
            new TaskCompletionSource<ModelQuickScanResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPath),
            (format, path, cancellationToken) =>
            {
                scanToken = cancellationToken;
                return scanCompletion.Task;
            },
            classifier: new GgufSelectionTestClassifier());

        Task browseTask = page.BrowseFilesAsync();
        var card = (ImportModelCard)page.FindName("ImportModelCardControl");
        var cancelButton = (Button)card.FindName("CancelScanButton");

        InvokeButton(cancelButton);
        scanCompletion.SetResult(
            ModelQuickScanResult.CreateSuccess(
                modelName: "Late Granite Model",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantization: "Q4_K_M",
                fileSizeBytes: 2_000_000_000L,
                contextLength: 131_072UL,
                ggufVersion: 3));
        await browseTask;

        Assert.IsTrue(scanToken.IsCancellationRequested);
        Assert.IsNull(page.SelectedModelPath);
        Assert.IsNull(page.ValidatedScanResult);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(
            ((Button)page.FindName(
                "ContinueToModelInspectionButton")).IsEnabled);
        Assert.AreEqual(
            ImportModelCardState.AwaitingSelection,
            card.CurrentState);
        Assert.AreEqual(
            string.Empty,
            GetTextBlock(card, "SuccessModelNameTextBlock").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task FailedScan_RecordsTechnicalDiagnosticAndShowsUserMessage()
    {
        const string selectedPath = @"C:\Models\broken-model.gguf";
        const string failureCode = "invalid-magic";
        const string userMessage =
            "The selected file is not a valid GGUF model.";
        const string technicalMessage =
            "Expected GGUF magic at file offset zero.";
        ModelQuickScanFailureDiagnostic? capturedDiagnostic = null;
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPath),
            (format, path, cancellationToken) =>
                Task.FromResult(
                    ModelQuickScanResult.CreateFailure(
                        failureCode,
                        userMessage,
                        technicalMessage)),
            recordScanFailure: diagnostic =>
                capturedDiagnostic = diagnostic,
            classifier: new GgufSelectionTestClassifier());

        await page.BrowseFilesAsync();

        var card = (ImportModelCard)page.FindName("ImportModelCardControl");
        Assert.IsNotNull(capturedDiagnostic);
        Assert.AreEqual("broken-model.gguf", capturedDiagnostic.SelectedFileName);
        Assert.AreEqual(failureCode, capturedDiagnostic.FailureCode);
        Assert.AreEqual(technicalMessage, capturedDiagnostic.TechnicalMessage);
        Assert.AreEqual(
            userMessage,
            GetTextBlock(card, "FailureMessageTextBlock").Text);
        Assert.AreNotEqual(
            technicalMessage,
            GetTextBlock(card, "FailureMessageTextBlock").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task DiagnosticSinkFailure_DoesNotReplaceControlledFailureCard()
    {
        const string selectedPath = @"C:\Models\broken-model.gguf";
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPath),
            (format, path, cancellationToken) =>
                Task.FromResult(
                    ModelQuickScanResult.CreateFailure(
                        "invalid-magic",
                        "The selected file is not a valid GGUF model.",
                        "Expected GGUF magic at file offset zero.")),
            recordScanFailure: diagnostic =>
                throw new InvalidOperationException("Test diagnostic failure."),
            classifier: new GgufSelectionTestClassifier());

        await page.BrowseFilesAsync();

        var card = (ImportModelCard)page.FindName("ImportModelCardControl");
        Assert.AreEqual(
            ImportModelCardState.ScanFailed,
            card.CurrentState);
        Assert.AreEqual(
            Visibility.Visible,
            GetElement(card, "FailureView").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RealFixture_ThroughRealRouter_DisplaysSuccessCard()
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "GGUF",
            "V-001-complete-metadata-v3.gguf");
        Assert.IsTrue(File.Exists(fixturePath));
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(fixturePath));

        await page.BrowseFilesAsync();

        var card = (ImportModelCard)page.FindName("ImportModelCardControl");
        Assert.IsTrue(page.HasValidatedModel);
        Assert.IsNotNull(page.ValidatedScanResult);
        Assert.AreEqual(3u, page.ValidatedScanResult.GgufVersion);
        Assert.AreEqual(
            ImportModelCardState.ScanSucceeded,
            card.CurrentState);
        Assert.AreEqual(
            "IBM Granite Fixture Model",
            GetTextBlock(card, "SuccessModelNameTextBlock").Text);
        Assert.AreEqual(
            "3B",
            GetTextBlock(card, "SuccessParametersTextBlock").Text);
        Assert.AreEqual(
            "Q4_K_M",
            GetTextBlock(card, "SuccessQuantizationTextBlock").Text);
        Assert.AreEqual(
            "320 B",
            GetTextBlock(card, "SuccessFileSizeTextBlock").Text);
        Assert.AreEqual(
            "128K tokens",
            GetTextBlock(card, "SuccessDeclaredContextTextBlock").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task BrowseFilesAsync_WithInjectedAcceptedClassifier_RunsInjectedScannerForSyntheticPath()
    {
        const string selectedPath = @"C:\Models\injected-scanner.gguf";
        int scannerCalls = 0;
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPath),
            (_, path, _) =>
            {
                scannerCalls++;
                Assert.AreEqual(selectedPath, path);
                return Task.FromResult(ModelQuickScanResult.CreateCancelled());
            },
            classifier: new AcceptedGgufClassifier());

        await page.BrowseFilesAsync();

        Assert.AreEqual(1, scannerCalls);
        Assert.AreEqual(
            ImportModelCardState.AwaitingSelection,
            ((ImportModelCard)page.FindName("ImportModelCardControl")).CurrentState);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ScanSucceeded_WithoutCardData_IsRejected()
    {
        var card = new ImportModelCard();

        Assert.Throws<InvalidOperationException>(
            () => card.SetState(
                ImportModelCardState.ScanSucceeded,
                "model.gguf"));
    }

    private static void InvokeButton(Button button)
    {
        var automationPeer = new ButtonAutomationPeer(button);
        var invokeProvider = (IInvokeProvider)automationPeer.GetPattern(
            PatternInterface.Invoke);

        invokeProvider.Invoke();
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

    private sealed class AcceptedGgufClassifier : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(
            ModelSelectionOperationId id,
            ModelSelectionInput input,
            CancellationToken token) =>
            Task.FromResult(
                ModelSelectionResult.Accepted(
                    id,
                    ModelSelectionRoute.Gguf,
                    input.DisplayName));
    }
}
