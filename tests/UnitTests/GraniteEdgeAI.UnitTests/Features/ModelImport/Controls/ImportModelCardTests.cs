using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ImportModelCardTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void ScanFailed_DisplaysFailureViewAndScannerValues()
    {
        const string expectedFileName = "broken-model.gguf";
        const string expectedFailureCode = "GGUF_INVALID_MAGIC";
        const string expectedFailureMessage =
            "The selected file does not contain a valid GGUF header.";
        var card = new ImportModelCard();

        card.SetState(
            ImportModelCardState.ScanFailed,
            expectedFileName,
            expectedFailureCode,
            expectedFailureMessage);

        Assert.AreEqual(ImportModelCardState.ScanFailed, card.CurrentState);
        Assert.AreEqual(
            Visibility.Collapsed,
            GetElement(card, "AwaitingSelectionView").Visibility);
        Assert.AreEqual(
            Visibility.Collapsed,
            GetElement(card, "ScanningView").Visibility);
        Assert.AreEqual(
            Visibility.Visible,
            GetElement(card, "FailureView").Visibility);
        Assert.AreEqual(
            expectedFileName,
            GetTextBlock(card, "FailureFileNameTextBlock").Text);
        Assert.AreEqual(
            expectedFailureCode,
            GetTextBlock(card, "FailureCodeTextBlock").Text);
        Assert.AreEqual(
            expectedFailureMessage,
            GetTextBlock(card, "FailureMessageTextBlock").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void AwaitingSelection_AfterScanFailed_ClearsHiddenFailureValues()
    {
        var card = new ImportModelCard();
        card.SetState(
            ImportModelCardState.ScanFailed,
            "broken-model.gguf",
            "GGUF_INVALID_MAGIC",
            "The selected file does not contain a valid GGUF header.");

        card.SetState(ImportModelCardState.AwaitingSelection);

        Assert.AreEqual(
            ImportModelCardState.AwaitingSelection,
            card.CurrentState);
        Assert.AreEqual(
            string.Empty,
            GetTextBlock(card, "FailureCodeTextBlock").Text);
        Assert.AreEqual(
            string.Empty,
            GetTextBlock(card, "FailureMessageTextBlock").Text);
        Assert.AreEqual(
            string.Empty,
            GetTextBlock(card, "FailureFileNameTextBlock").Text);
        Assert.AreEqual(
            Visibility.Collapsed,
            GetElement(card, "FailureView").Visibility);
        Assert.AreEqual(
            Visibility.Visible,
            GetElement(card, "AwaitingSelectionView").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ScanFailed_BlankDetails_UsesSafeFallbacks()
    {
        string?[] blankValues = [null, string.Empty, "   "];
        var card = new ImportModelCard();

        foreach (string? blankValue in blankValues)
        {
            card.SetState(
                ImportModelCardState.ScanFailed,
                "broken-model.gguf",
                blankValue,
                blankValue);

            Assert.AreEqual(
                "model-scan-failed",
                GetTextBlock(card, "FailureCodeTextBlock").Text);
            Assert.AreEqual(
                "The selected model could not be scanned.",
                GetTextBlock(card, "FailureMessageTextBlock").Text);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task FailedGgufScan_DisplaysUserFacingFailureAndKeepsSelection()
    {
        const string selectedPath = @"C:\Models\broken-model.gguf";
        const string failureCode = "GGUF_INVALID_MAGIC";
        const string userMessage =
            "The selected file does not contain a valid GGUF header.";
        const string technicalMessage =
            "Unexpected bytes 00-00-00-00 at offset zero.";
        var scanInteractionCount = 0;
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPath),
            (format, path, cancellationToken) =>
            {
                scanInteractionCount++;
                Assert.AreEqual(ModelFormatSelection.Gguf, format);
                Assert.AreEqual(selectedPath, path);
                Assert.IsFalse(cancellationToken.IsCancellationRequested);

                return Task.FromResult(
                    ModelQuickScanResult.CreateFailure(
                        failureCode,
                        userMessage,
                        technicalMessage));
            });

        await page.BrowseFilesAsync();

        var card = (ImportModelCard)page.FindName(
            "ImportModelCardControl");
        Assert.AreEqual(1, scanInteractionCount);
        Assert.AreEqual(selectedPath, page.SelectedModelPath);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(
            ((Button)page.FindName(
                "ContinueToModelInspectionButton")).IsEnabled);
        Assert.AreEqual(ImportModelCardState.ScanFailed, card.CurrentState);
        Assert.AreEqual(
            Visibility.Visible,
            GetElement(card, "FailureView").Visibility);
        Assert.AreEqual(
            failureCode,
            GetTextBlock(card, "FailureCodeTextBlock").Text);
        Assert.AreEqual(
            userMessage,
            GetTextBlock(card, "FailureMessageTextBlock").Text);
        Assert.AreNotEqual(
            technicalMessage,
            GetTextBlock(card, "FailureMessageTextBlock").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RemoveFailedModel_Click_RestoresAwaitingSelection()
    {
        const string selectedPath = @"C:\Models\broken-model.gguf";
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPath),
            (format, path, cancellationToken) =>
                Task.FromResult(
                    ModelQuickScanResult.CreateFailure(
                        "GGUF_INVALID_MAGIC",
                        "The selected file does not contain a valid GGUF header.",
                        "Unexpected bytes at offset zero.")));
        await page.BrowseFilesAsync();
        var card = (ImportModelCard)page.FindName(
            "ImportModelCardControl");
        var removeButton = (Button)card.FindName(
            "RemoveFailedModelButton");

        InvokeButton(removeButton);

        Assert.IsNull(page.SelectedModelPath);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(
            ((Button)page.FindName(
                "ContinueToModelInspectionButton")).IsEnabled);
        Assert.AreEqual(
            ImportModelCardState.AwaitingSelection,
            card.CurrentState);
        Assert.AreEqual(
            Visibility.Visible,
            GetElement(card, "AwaitingSelectionView").Visibility);
        Assert.AreEqual(
            Visibility.Collapsed,
            GetElement(card, "FailureView").Visibility);
        Assert.AreEqual(
            string.Empty,
            GetTextBlock(card, "FailureCodeTextBlock").Text);
        Assert.AreEqual(
            string.Empty,
            GetTextBlock(card, "FailureMessageTextBlock").Text);
        Assert.AreEqual(
            string.Empty,
            GetTextBlock(card, "FailureFileNameTextBlock").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CancelScan_DuringAwait_IgnoresLateFailureResult()
    {
        const string selectedPath = @"C:\Models\slow-model.gguf";
        CancellationToken scanCancellationToken = default;
        var scanCompletion =
            new TaskCompletionSource<ModelQuickScanResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPath),
            (format, path, cancellationToken) =>
            {
                scanCancellationToken = cancellationToken;
                return scanCompletion.Task;
            });

        Task browseTask = page.BrowseFilesAsync();
        var card = (ImportModelCard)page.FindName(
            "ImportModelCardControl");
        var cancelButton = (Button)card.FindName("CancelScanButton");

        InvokeButton(cancelButton);
        scanCompletion.SetResult(
            ModelQuickScanResult.CreateFailure(
                "GGUF_INVALID_MAGIC",
                "This late result must not replace the reset card.",
                "The test scan completed after cancellation."));
        await browseTask;

        Assert.IsTrue(scanCancellationToken.IsCancellationRequested);
        Assert.IsNull(page.SelectedModelPath);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(
            ((Button)page.FindName(
                "ContinueToModelInspectionButton")).IsEnabled);
        Assert.AreEqual(
            ImportModelCardState.AwaitingSelection,
            card.CurrentState);
        Assert.AreEqual(
            Visibility.Visible,
            GetElement(card, "AwaitingSelectionView").Visibility);
        Assert.AreEqual(
            Visibility.Collapsed,
            GetElement(card, "FailureView").Visibility);
        Assert.AreEqual(
            string.Empty,
            GetTextBlock(card, "FailureCodeTextBlock").Text);
        Assert.AreEqual(
            string.Empty,
            GetTextBlock(card, "FailureMessageTextBlock").Text);
        Assert.AreEqual(
            string.Empty,
            GetTextBlock(card, "FailureFileNameTextBlock").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ScanSucceeded_DisplaysImportedCardValues()
    {
        var card = new ImportModelCard();

        card.ShowSuccess(
            new ImportedModelCardData(
                FileName:
                    "granite-3.3-8b-instruct-Q4_K_M.gguf",
                ModelName:
                    "IBM Granite 3.3 8B Instruct",
                Parameters:
                    "8B",
                Architecture:
                    "Granite",
                Quantization:
                    "Q4_K_M",
                FileSize:
                    "5.1 GB",
                DeclaredContext:
                    "128K tokens"));

        Assert.AreEqual(
            ImportModelCardState.ScanSucceeded,
            card.CurrentState);

        Assert.AreEqual(
            Visibility.Collapsed,
            GetElement(
                card,
                "AwaitingSelectionView").Visibility);

        Assert.AreEqual(
            Visibility.Collapsed,
            GetElement(
                card,
                "ScanningView").Visibility);

        Assert.AreEqual(
            Visibility.Collapsed,
            GetElement(
                card,
                "FailureView").Visibility);

        Assert.AreEqual(
            Visibility.Visible,
            GetElement(
                card,
                "SuccessView").Visibility);

        Assert.AreEqual(
            "IBM Granite 3.3 8B Instruct",
            GetTextBlock(
                card,
                "SuccessModelNameTextBlock").Text);

        Assert.AreEqual(
            "granite-3.3-8b-instruct-Q4_K_M.gguf",
            GetTextBlock(
                card,
                "SuccessFileNameTextBlock").Text);

        Assert.AreEqual(
            "Q4_K_M",
            GetTextBlock(
                card,
                "SuccessQuantizationTextBlock").Text);

        Assert.AreEqual(
            "8B",
            GetTextBlock(
                card,
                "SuccessParametersTextBlock").Text);

        Assert.AreEqual(
            "Granite",
            GetTextBlock(
                card,
                "SuccessArchitectureTextBlock").Text);

        Assert.AreEqual(
            "5.1 GB",
            GetTextBlock(
                card,
                "SuccessFileSizeTextBlock").Text);

        Assert.AreEqual(
            "128K tokens",
            GetTextBlock(
                card,
                "SuccessDeclaredContextTextBlock").Text);

        Button browse = (Button)card.FindName("BrowseFilesButton");
        Assert.AreEqual(46d, browse.Height);
        Assert.AreEqual(new CornerRadius(11), browse.CornerRadius);
        Assert.AreEqual(FontWeights.SemiBold.Weight, browse.FontWeight.Weight);
        Assert.AreEqual(
            Colors.White,
            ((SolidColorBrush)browse.Foreground).Color);
        Assert.AreEqual(
            ColorHelper.FromArgb(0xFF, 0x25, 0x63, 0xEB),
            ((SolidColorBrush)browse.Resources["ButtonBackground"]).Color);
        Assert.AreEqual(
            ColorHelper.FromArgb(0xFF, 0x1D, 0x4E, 0xD8),
            ((SolidColorBrush)browse.Resources["ButtonBackgroundPointerOver"]).Color);
        Assert.AreEqual(
            ColorHelper.FromArgb(0xFF, 0x1E, 0x40, 0xAF),
            ((SolidColorBrush)browse.Resources["ButtonBackgroundPressed"]).Color);
        Assert.AreEqual(
            ColorHelper.FromArgb(0xFF, 0xE5, 0xE7, 0xEB),
            ((SolidColorBrush)browse.Resources["ButtonBackgroundDisabled"]).Color);
        Assert.AreEqual(
            ColorHelper.FromArgb(0xFF, 0x9C, 0xA3, 0xAF),
            ((SolidColorBrush)browse.Resources["ButtonForegroundDisabled"]).Color);
        Assert.AreEqual(
            ColorHelper.FromArgb(0xFF, 0xD1, 0xD5, 0xDB),
            ((SolidColorBrush)browse.Resources["ButtonBorderBrushDisabled"]).Color);
        Assert.IsTrue(browse.UseSystemFocusVisuals);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SuccessfulGgufScan_ShowsImportedCardAndEnablesContinue()
    {
        const string selectedPath =
            @"C:\Models\granite-3.3-8b-instruct-Q4_K_M.gguf";

        ModelQuickScanResult successfulResult =
            ModelQuickScanResult.CreateSuccess(
                modelName:
                    "IBM Granite 3.3 8B Instruct",
                architecture:
                    "granite",
                parameterSizeLabel:
                    "8B",
                quantization:
                    "Q4_K_M",
                fileSizeBytes:
                    5_100_000_000L,
                contextLength:
                    131_072UL,
                ggufVersion:
                    3);

        var page = new ModelImportPage(
            () => Task.FromResult(
                ModelFormatSelection.Gguf),

            () => Task.FromResult<string?>(
                selectedPath),

            (format, path, cancellationToken) =>
            {
                Assert.AreEqual(
                    ModelFormatSelection.Gguf,
                    format);

                Assert.AreEqual(
                    selectedPath,
                    path);

                Assert.IsFalse(
                    cancellationToken.IsCancellationRequested);

                return Task.FromResult(
                    successfulResult);
            });

        await page.BrowseFilesAsync();

        var card =
            (ImportModelCard)page.FindName(
                "ImportModelCardControl");

        var continueButton =
            (Button)page.FindName(
                "ContinueToModelInspectionButton");

        Assert.AreEqual(
            selectedPath,
            page.SelectedModelPath);

        Assert.AreSame(
            successfulResult,
            page.ValidatedScanResult);

        Assert.IsTrue(
            page.HasValidatedModel);

        Assert.IsTrue(
            continueButton.IsEnabled);

        Assert.AreEqual(
            ImportModelCardState.ScanSucceeded,
            card.CurrentState);

        Assert.AreEqual(
            Visibility.Visible,
            GetElement(
                card,
                "SuccessView").Visibility);

        Assert.AreEqual(
            "5.1 GB",
            GetTextBlock(
                card,
                "SuccessFileSizeTextBlock").Text);

        Assert.AreEqual(
            "128K tokens",
            GetTextBlock(
                card,
                "SuccessDeclaredContextTextBlock").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RemoveSuccessfulModel_ClearsValidatedState()
    {
        const string selectedPath =
            @"C:\Models\granite.gguf";

        var page = new ModelImportPage(
            () => Task.FromResult(
                ModelFormatSelection.Gguf),

            () => Task.FromResult<string?>(
                selectedPath),

            (format, path, cancellationToken) =>
                Task.FromResult(
                    ModelQuickScanResult.CreateSuccess(
                        modelName:
                            "IBM Granite",
                        architecture:
                            "granite",
                        parameterSizeLabel:
                            "8B",
                        quantization:
                            "Q4_K_M",
                        fileSizeBytes:
                            5_100_000_000L,
                        contextLength:
                            131_072UL,
                        ggufVersion:
                            3)));

        await page.BrowseFilesAsync();

        var card =
            (ImportModelCard)page.FindName(
                "ImportModelCardControl");

        var removeButton =
            (Button)card.FindName(
                "RemoveSucceededModelButton");

        InvokeButton(removeButton);

        Assert.IsNull(
            page.SelectedModelPath);

        Assert.IsNull(
            page.ValidatedScanResult);

        Assert.IsFalse(
            page.HasValidatedModel);

        Assert.IsFalse(
            ((Button)page.FindName(
                "ContinueToModelInspectionButton")).IsEnabled);

        Assert.AreEqual(
            ImportModelCardState.AwaitingSelection,
            card.CurrentState);

        Assert.AreEqual(
            Visibility.Visible,
            GetElement(
                card,
                "AwaitingSelectionView").Visibility);

        Assert.AreEqual(
            Visibility.Collapsed,
            GetElement(
                card,
                "SuccessView").Visibility);

        Assert.AreEqual(
            string.Empty,
            GetTextBlock(
                card,
                "SuccessModelNameTextBlock").Text);
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
}
