using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.Onboarding.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.IO;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies request-based navigation owned by the onboarding shell.
/// </summary>
[TestClass]
public sealed class OnboardingModelInspectionNavigationTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ModelImportRequest_NavigatesStageFrameWithSameRequest()
    {
        string selectedModelPath = CreateTemporaryModelFile(lengthBytes: 64);

        try
        {
            var shell = new OnboardingShellPage();
            var modelImportPage =
                CreateSuccessfulModelImportPage(
                    selectedModelPath,
                    fileSizeBytes: 64);
            shell.AttachModelImportPage(modelImportPage);

            await modelImportPage.BrowseFilesAsync();

            var continueButton = (Button)modelImportPage.FindName(
                "ContinueToModelInspectionButton");
            InvokeButton(continueButton);

            var stageFrame = (Frame)shell.FindName("StageFrame");
            var inspectionPage = stageFrame.Content as ModelInspectionPage;

            Assert.AreEqual(typeof(ModelInspectionPage), stageFrame.SourcePageType);
            Assert.IsNotNull(inspectionPage);
            Assert.IsNotNull(inspectionPage.Request);
            Assert.AreEqual(selectedModelPath, inspectionPage.Request.ModelPath);
            Assert.AreEqual(
                Path.GetFileName(selectedModelPath),
                inspectionPage.Request.FileName);
        }
        finally
        {
            DeleteTemporaryModelFile(selectedModelPath);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void NavigateToModelInspection_UpdatesCurrentStage()
    {
        var shell = new OnboardingShellPage();
        ModelInspectionRequest request = CreateRequest(
            @"C:\Models\granite.gguf");

        bool navigationSucceeded =
            shell.NavigateToModelInspection(request);

        Assert.IsTrue(navigationSucceeded);
        Assert.AreEqual(
            OnboardingStage.InspectModel,
            shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void NavigateToModelInspection_UpdatesStageIndicator()
    {
        var shell = new OnboardingShellPage();
        var stageIndicator =
            (OnboardingStageIndicator)shell.FindName("StageIndicator");
        ModelInspectionRequest request = CreateRequest(
            @"C:\Models\granite.gguf");

        bool navigationSucceeded =
            shell.NavigateToModelInspection(request);

        Assert.IsTrue(navigationSucceeded);
        Assert.AreEqual(
            OnboardingStage.InspectModel,
            stageIndicator.CurrentStage);
        Assert.AreEqual(
            shell.CurrentStage,
            stageIndicator.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void NavigateToModelInspection_PassesSameRequestToDestination()
    {
        var shell = new OnboardingShellPage();
        ModelInspectionRequest request = CreateRequest(
            @"C:\Models\granite.gguf");

        bool navigationSucceeded =
            shell.NavigateToModelInspection(request);

        var stageFrame = (Frame)shell.FindName("StageFrame");
        var inspectionPage = stageFrame.Content as ModelInspectionPage;

        Assert.IsTrue(navigationSucceeded);
        Assert.IsNotNull(inspectionPage);
        Assert.AreSame(request, inspectionPage.Request);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void NavigateToModelInspection_WithNullRequest_Throws()
    {
        var shell = new OnboardingShellPage();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            shell.NavigateToModelInspection(null!));

        Assert.AreEqual(
            OnboardingStage.ImportModel,
            shell.CurrentStage);
    }

    private static ModelImportPage CreateSuccessfulModelImportPage(
        string selectedModelPath,
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
            () => Task.FromResult<string?>(selectedModelPath),
            (format, path, cancellationToken) =>
                Task.FromResult(successfulResult));
    }

    private static ModelInspectionRequest CreateRequest(string modelPath)
    {
        return new ModelInspectionRequest(
            modelPath,
            Path.GetFileName(modelPath),
            new ExpectedModelFileIdentity(
                lengthBytes: 64,
                lastWriteTimeUtc: new DateTimeOffset(
                    2026,
                    8,
                    5,
                    12,
                    0,
                    0,
                    TimeSpan.Zero)),
            ValidatedQuickScanSnapshot.CreateGguf(
                modelName: "Granite 4.1 3B Instruct",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantisation: "Q4_K_M",
                fileSizeBytes: 64,
                declaredContextLength: 131_072,
                ggufVersion: 3));
    }

    private static string CreateTemporaryModelFile(int lengthBytes)
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"granite-edge-ai-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);

        string modelPath = Path.Combine(
            directoryPath,
            "granite-4.1-3b-instruct.gguf");
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
