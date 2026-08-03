using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.Onboarding.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies navigation owned by the onboarding shell.
/// </summary>
[TestClass]
public sealed class OnboardingModelInspectionNavigationTests
{
    /// <summary>
    /// Verifies that the shell responds to the Model Import page's request.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ModelImportRequest_NavigatesStageFrameToModelInspection()
    {
        // Arrange one valid model and the onboarding shell.
        const string selectedModelPath =
            @"C:\Models\granite-4.1-3b-instruct.gguf";

        var shell = new OnboardingShellPage();
        var modelImportPage =
            CreateSuccessfulModelImportPage(selectedModelPath);

        // Subscribe the supplied page using the shell's production helper.
        shell.AttachModelImportPage(modelImportPage);

        // Put the import page into its validated-model state.
        await modelImportPage.BrowseFilesAsync();

        // Click Continue.
        var continueButton = (Button)modelImportPage.FindName(
            "ContinueToModelInspectionButton");

        InvokeButton(continueButton);

        // Read the shell's real navigation Frame.
        var stageFrame =
            (Frame)shell.FindName("StageFrame");

        // The shell must display ModelInspectionPage.
        Assert.AreEqual(
            typeof(ModelInspectionPage),
            stageFrame.SourcePageType);

        Assert.IsInstanceOfType<ModelInspectionPage>(
            stageFrame.Content);
    }

    /// <summary>
    /// Verifies that navigation advances onboarding to stage two.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void NavigateToModelInspection_UpdatesCurrentStage()
    {
        // Arrange the shell in its initial stage.
        var shell = new OnboardingShellPage();

        // Ask the shell to navigate.
        bool navigationSucceeded =
            shell.NavigateToModelInspection(
                @"C:\Models\granite.gguf");

        // Navigation must advance the shell's state.
        Assert.IsTrue(navigationSucceeded);

        Assert.AreEqual(
            OnboardingStage.InspectModel,
            shell.CurrentStage);
    }

    /// <summary>
    /// Verifies that the persistent stage indicator stays synchronized.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void NavigateToModelInspection_UpdatesStageIndicator()
    {
        // Arrange the shell and locate the persistent indicator.
        var shell = new OnboardingShellPage();

        var stageIndicator =
            (OnboardingStageIndicator)shell.FindName(
                "StageIndicator");

        // Navigate to inspection.
        bool navigationSucceeded =
            shell.NavigateToModelInspection(
                @"C:\Models\granite.gguf");

        // The shell and indicator must report the same stage.
        Assert.IsTrue(navigationSucceeded);

        Assert.AreEqual(
            OnboardingStage.InspectModel,
            stageIndicator.CurrentStage);

        Assert.AreEqual(
            shell.CurrentStage,
            stageIndicator.CurrentStage);
    }

    /// <summary>
    /// Verifies that the shell passes the original model path unchanged.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void NavigateToModelInspection_PassesModelPathToDestination()
    {
        // Arrange one authoritative model path.
        const string selectedModelPath =
            @"C:\Models\granite.gguf";

        var shell = new OnboardingShellPage();

        // Navigate through the shell.
        bool navigationSucceeded =
            shell.NavigateToModelInspection(
                selectedModelPath);

        // Read the destination page.
        var stageFrame =
            (Frame)shell.FindName("StageFrame");

        var inspectionPage =
            stageFrame.Content as ModelInspectionPage;

        // The original path must reach the destination unchanged.
        Assert.IsTrue(navigationSucceeded);
        Assert.IsNotNull(inspectionPage);

        Assert.AreEqual(
            selectedModelPath,
            inspectionPage.SelectedModelPath);
    }

    /// <summary>
    /// Creates a deterministic Model Import page that always scans successfully.
    /// </summary>
    private static ModelImportPage CreateSuccessfulModelImportPage(
        string selectedModelPath)
    {
        ModelQuickScanResult successfulResult =
            ModelQuickScanResult.CreateSuccess(
                modelName: "Granite 4.1 3B Instruct",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantization: "Q4_K_M",
                fileSizeBytes: 2_000_000_000L,
                contextLength: 131_072UL,
                ggufVersion: 3);

        return new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedModelPath),
            (format, path, cancellationToken) =>
                Task.FromResult(successfulResult));
    }

    /// <summary>
    /// Invokes one real WinUI button.
    /// </summary>
    private static void InvokeButton(Button button)
    {
        var automationPeer =
            new ButtonAutomationPeer(button);

        var invokeProvider =
            (IInvokeProvider)automationPeer.GetPattern(
                PatternInterface.Invoke);

        invokeProvider.Invoke();
    }
}