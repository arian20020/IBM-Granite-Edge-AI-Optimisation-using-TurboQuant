using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.IO;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies the Model Import page's boundary for requesting model inspection.
/// </summary>
[TestClass]
public sealed class ModelImportNavigationRequestTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_DisablesContinueToInspectionButton()
    {
        var page = new ModelImportPage();
        var continueButton = (Button)page.FindName(
            "ContinueToModelInspectionButton");

        Assert.IsFalse(continueButton.IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SuccessfulScan_EnablesContinueToInspectionButton()
    {
        string selectedPath = CreateTemporaryModelFile(lengthBytes: 64);

        try
        {
            var page = CreateSuccessfulPage(selectedPath, fileSizeBytes: 64);

            await page.BrowseFilesAsync();

            var continueButton = (Button)page.FindName(
                "ContinueToModelInspectionButton");

            Assert.IsTrue(page.HasValidatedModel);
            Assert.IsTrue(continueButton.IsEnabled);
        }
        finally
        {
            DeleteTemporaryModelFile(selectedPath);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ContinueButton_AfterSuccessfulScan_RaisesImmutableRequest()
    {
        string selectedPath = CreateTemporaryModelFile(lengthBytes: 64);

        try
        {
            var page = CreateSuccessfulPage(selectedPath, fileSizeBytes: 64);
            ModelInspectionRequestedEventArgs? capturedRequest = null;
            int requestCount = 0;
            page.ModelInspectionRequested += (_, eventArguments) =>
            {
                requestCount++;
                capturedRequest = eventArguments;
            };

            await page.BrowseFilesAsync();

            var continueButton = (Button)page.FindName(
                "ContinueToModelInspectionButton");
            InvokeButton(continueButton);

            Assert.AreEqual(1, requestCount);
            Assert.IsNotNull(capturedRequest);
            Assert.AreEqual(selectedPath, capturedRequest.Request.ModelPath);
            Assert.AreEqual(
                Path.GetFileName(selectedPath),
                capturedRequest.Request.FileName);
            Assert.AreEqual(
                64L,
                capturedRequest.Request.ExpectedFileIdentity.LengthBytes);
            Assert.AreEqual(
                "Granite 4.1 3B Instruct",
                capturedRequest.Request.QuickScan.ModelName);
        }
        finally
        {
            DeleteTemporaryModelFile(selectedPath);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ContinueButton_AfterFileChanges_InvalidatesSelectionWithoutNavigation()
    {
        string selectedPath = CreateTemporaryModelFile(lengthBytes: 64);

        try
        {
            var page = CreateSuccessfulPage(selectedPath, fileSizeBytes: 64);
            int requestCount = 0;
            page.ModelInspectionRequested += (_, _) => requestCount++;

            await page.BrowseFilesAsync();

            // Change the selected file after quick-scan success but before the
            // navigation boundary captures its expected identity.
            using (FileStream stream = new(
                selectedPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.None))
            {
                stream.WriteByte(0);
            }

            var continueButton = (Button)page.FindName(
                "ContinueToModelInspectionButton");
            InvokeButton(continueButton);

            AssertChangedSelectionFailure(
                page,
                continueButton,
                selectedPath,
                requestCount);
        }
        finally
        {
            DeleteTemporaryModelFile(selectedPath);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ContinueButton_AfterFileIsDeleted_InvalidatesSelectionWithoutNavigation()
    {
        string selectedPath = CreateTemporaryModelFile(lengthBytes: 64);

        try
        {
            var page = CreateSuccessfulPage(selectedPath, fileSizeBytes: 64);
            int requestCount = 0;
            page.ModelInspectionRequested += (_, _) => requestCount++;

            await page.BrowseFilesAsync();

            // Remove the file after quick-scan success. Model Import must not
            // navigate using stale metadata for a model that no longer exists.
            File.Delete(selectedPath);

            var continueButton = (Button)page.FindName(
                "ContinueToModelInspectionButton");
            InvokeButton(continueButton);

            AssertChangedSelectionFailure(
                page,
                continueButton,
                selectedPath,
                requestCount);
        }
        finally
        {
            DeleteTemporaryModelFile(selectedPath);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void TryRequestModelInspection_WithoutValidatedModel_ReturnsFalse()
    {
        var page = new ModelImportPage();
        int requestCount = 0;
        page.ModelInspectionRequested += (_, _) => requestCount++;

        bool requestAccepted = page.TryRequestModelInspection();

        Assert.IsFalse(requestAccepted);
        Assert.AreEqual(0, requestCount);
    }

    private static ModelImportPage CreateSuccessfulPage(
        string selectedPath,
        long fileSizeBytes)
    {
        ModelQuickScanResult successfulResult =
            ModelQuickScanResult.CreateSuccess(
                modelName: "Granite 4.1 3B Instruct",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantization: "Q4_K_M",
                fileSizeBytes: fileSizeBytes,
                contextLength: 131_072UL,
                ggufVersion: 3);

        return new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPath),
            (format, path, cancellationToken) =>
                Task.FromResult(successfulResult));
    }

    private static void AssertChangedSelectionFailure(
        ModelImportPage page,
        Button continueButton,
        string selectedPath,
        int requestCount)
    {
        var importCard = (ImportModelCard)page.FindName(
            "ImportModelCardControl");
        var failureCode = (TextBlock)importCard.FindName(
            "FailureCodeTextBlock");
        var failureMessage = (TextBlock)importCard.FindName(
            "FailureMessageTextBlock");
        var failureFileName = (TextBlock)importCard.FindName(
            "FailureFileNameTextBlock");

        Assert.AreEqual(0, requestCount);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsNull(page.ValidatedScanResult);
        Assert.IsNull(page.SelectedModelPath);
        Assert.IsFalse(continueButton.IsEnabled);
        Assert.AreEqual(ImportModelCardState.ScanFailed, importCard.CurrentState);
        Assert.AreEqual("model-selection-changed", failureCode.Text);
        Assert.AreEqual(
            "The selected model changed after validation. Choose the model again.",
            failureMessage.Text);
        Assert.AreEqual(Path.GetFileName(selectedPath), failureFileName.Text);
        Assert.IsFalse(failureMessage.Text.Contains(
            selectedPath,
            StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateTemporaryModelFile(int lengthBytes)
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"granite-edge-ai-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);

        string modelPath = Path.Combine(
            directoryPath,
            "granite-4.1-3b-instruct-q4_k_m.gguf");
        File.WriteAllBytes(modelPath, new byte[lengthBytes]);
        return modelPath;
    }

    private static void DeleteTemporaryModelFile(string modelPath)
    {
        string? directoryPath = Path.GetDirectoryName(modelPath);
        if (directoryPath is not null && Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static void InvokeButton(Button button)
    {
        var automationPeer = new ButtonAutomationPeer(button);
        var invokeProvider = (IInvokeProvider)automationPeer.GetPattern(
            PatternInterface.Invoke);
        invokeProvider.Invoke();
    }
}
