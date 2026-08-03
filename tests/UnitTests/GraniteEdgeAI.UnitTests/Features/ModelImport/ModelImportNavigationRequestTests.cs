using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies the Model Import page's boundary for requesting model inspection.
/// </summary>
[TestClass]
public sealed class ModelImportNavigationRequestTests
{
    /// <summary>
    /// Proves that inspection cannot be requested before a model is validated.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_DisablesContinueToInspectionButton()
    {
        // Create the page in its initial state.
        var page = new ModelImportPage();

        // Locate the real button declared in ModelImportPage.xaml.
        var continueButton = (Button)page.FindName(
            "ContinueToModelInspectionButton");

        // The button must not allow progression before quick-scan success.
        Assert.IsFalse(continueButton.IsEnabled);
    }

    /// <summary>
    /// Proves that a successful quick scan enables the next onboarding action.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SuccessfulScan_EnablesContinueToInspectionButton()
    {
        // Arrange a deterministic valid model path and quick-scan result.
        const string selectedPath =
            @"C:\Models\granite-4.1-3b-instruct-q4_k_m.gguf";
        var page = CreateSuccessfulPage(selectedPath);

        // Run the same page operation used after the Browse button is selected.
        await page.BrowseFilesAsync();

        // Locate the real Continue button.
        var continueButton = (Button)page.FindName(
            "ContinueToModelInspectionButton");

        // A validated model is the only state that enables progression.
        Assert.IsTrue(page.HasValidatedModel);
        Assert.IsTrue(continueButton.IsEnabled);
    }

    /// <summary>
    /// Proves that clicking Continue raises exactly one request containing
    /// the authoritative validated model path.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ContinueButton_AfterSuccessfulScan_RaisesRequestWithValidatedPath()
    {
        // Arrange a deterministic successful import.
        const string selectedPath =
            @"C:\Models\granite-4.1-3b-instruct-q4_k_m.gguf";
        var page = CreateSuccessfulPage(selectedPath);

        // Capture the navigation request raised by the page.
        ModelInspectionRequestedEventArgs? capturedRequest = null;
        int requestCount = 0;
        page.ModelInspectionRequested += (_, eventArguments) =>
        {
            requestCount++;
            capturedRequest = eventArguments;
        };

        // Put the page into its validated-model state.
        await page.BrowseFilesAsync();

        // Invoke the real WinUI button through its automation peer.
        var continueButton = (Button)page.FindName(
            "ContinueToModelInspectionButton");
        InvokeButton(continueButton);

        // One click must produce one request with the original model path.
        Assert.AreEqual(1, requestCount);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(selectedPath, capturedRequest.ModelPath);
    }

    /// <summary>
    /// Proves that the request method rejects invalid state even if it is
    /// called independently of the disabled UI button.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void TryRequestModelInspection_WithoutValidatedModel_ReturnsFalse()
    {
        // Create an initial page with no validated model.
        var page = new ModelImportPage();
        int requestCount = 0;
        page.ModelInspectionRequested += (_, _) => requestCount++;

        // Call the guarded page action directly.
        bool requestAccepted = page.TryRequestModelInspection();

        // The page must not raise a request from an invalid state.
        Assert.IsFalse(requestAccepted);
        Assert.AreEqual(0, requestCount);
    }

    /// <summary>
    /// Creates a Model Import page whose picker and scanner always produce
    /// one valid GGUF model.
    /// </summary>
    private static ModelImportPage CreateSuccessfulPage(string selectedPath)
    {
        // Create the exact quick-scan success data used by the page state machine.
        ModelQuickScanResult successfulResult =
            ModelQuickScanResult.CreateSuccess(
                modelName: "Granite 4.1 3B Instruct",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantization: "Q4_K_M",
                fileSizeBytes: 2_000_000_000L,
                contextLength: 131_072UL,
                ggufVersion: 3);

        // Inject deterministic format selection, file picking and scanning.
        return new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPath),
            (format, path, cancellationToken) =>
                Task.FromResult(successfulResult));
    }

    /// <summary>
    /// Invokes a WinUI Button using the same accessibility action used by
    /// the existing model-import state-machine tests.
    /// </summary>
    private static void InvokeButton(Button button)
    {
        // Create an automation peer for the real button.
        var automationPeer = new ButtonAutomationPeer(button);

        // Obtain the standard invoke provider.
        var invokeProvider = (IInvokeProvider)automationPeer.GetPattern(
            PatternInterface.Invoke);

        // Perform one user-equivalent invocation.
        invokeProvider.Invoke();
    }
}
