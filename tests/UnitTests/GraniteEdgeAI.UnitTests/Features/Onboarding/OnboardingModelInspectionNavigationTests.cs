using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.Onboarding.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.IO;
using System.Reflection;

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
            ModelInspectionRequest? raisedRequest = null;
            modelImportPage.ModelInspectionRequested += (_, eventArguments) =>
                raisedRequest = eventArguments.Request;
            shell.AttachModelImportPage(modelImportPage);

            await modelImportPage.BrowseFilesAsync();

            var continueButton = (Button)modelImportPage.FindName(
                "ContinueToModelInspectionButton");
            InvokeButton(continueButton);

            var stageFrame = (Frame)shell.FindName("StageFrame");
            var inspectionPage = stageFrame.Content as ModelInspectionPage;

            Assert.AreEqual(typeof(ModelInspectionPage), stageFrame.SourcePageType);
            Assert.IsNotNull(inspectionPage);
            Assert.IsNotNull(raisedRequest);
            Assert.AreSame(raisedRequest, inspectionPage.Request);
            Assert.AreEqual(selectedModelPath, raisedRequest.ModelPath);
            Assert.AreEqual(
                Path.GetFileName(selectedModelPath),
                raisedRequest.FileName);
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
        Assert.HasCount(0, stageFrame.BackStack);
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

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ChooseAnother_FromActiveInspection_ResetsStageWithFreshImportPage()
    {
        var shell = new OnboardingShellPage();
        var stageFrame = (Frame)shell.FindName("StageFrame");
        var stageIndicator =
            (OnboardingStageIndicator)shell.FindName("StageIndicator");
        var initialImportPage = (ModelImportPage)stageFrame.Content;

        bool navigationSucceeded = shell.NavigateToModelInspection(
            CreateRequest(@"C:\Models\granite.gguf"));
        var inspectionPage = stageFrame.Content as ModelInspectionPage;

        Assert.IsTrue(navigationSucceeded);
        Assert.IsNotNull(inspectionPage);
        Assert.IsNotNull(inspectionPage.ViewModel);

        inspectionPage.ViewModel.ChooseAnotherCommand.Execute(null);

        Assert.AreEqual(typeof(ModelImportPage), stageFrame.SourcePageType);
        Assert.IsInstanceOfType<ModelImportPage>(stageFrame.Content);
        Assert.AreNotSame(initialImportPage, stageFrame.Content);
        Assert.HasCount(0, stageFrame.BackStack);
        Assert.IsFalse(stageFrame.CanGoBack);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
        Assert.AreEqual(shell.CurrentStage, stageIndicator.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ChooseAnother_AttachesFreshImportPageForNextExactRequest()
    {
        var shell = new OnboardingShellPage();
        var stageFrame = (Frame)shell.FindName("StageFrame");
        ModelInspectionRequest firstRequest = CreateRequest(
            @"C:\Models\first.gguf");
        ModelInspectionRequest secondRequest = CreateRequest(
            @"C:\Models\second.gguf");

        Assert.IsTrue(shell.NavigateToModelInspection(firstRequest));
        var inspectionPage = (ModelInspectionPage)stageFrame.Content;
        inspectionPage.ViewModel!.ChooseAnotherCommand.Execute(null);
        var freshImportPage = (ModelImportPage)stageFrame.Content;

        RaiseModelInspectionRequested(freshImportPage, secondRequest);

        var nextInspectionPage = stageFrame.Content as ModelInspectionPage;
        Assert.IsNotNull(nextInspectionPage);
        Assert.AreSame(secondRequest, nextInspectionPage.Request);
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void AttachModelInspectionPage_Repeatedly_DoesNotDuplicateChooseAnotherNavigation()
    {
        var shell = new OnboardingShellPage();
        var stageFrame = (Frame)shell.FindName("StageFrame");

        Assert.IsTrue(shell.NavigateToModelInspection(
            CreateRequest(@"C:\Models\granite.gguf")));
        var inspectionPage = (ModelInspectionPage)stageFrame.Content;
        int importNavigationCount = 0;
        stageFrame.Navigated += (_, eventArguments) =>
        {
            if (eventArguments.SourcePageType == typeof(ModelImportPage))
            {
                importNavigationCount++;
            }
        };

        shell.AttachModelInspectionPage(inspectionPage);
        shell.AttachModelInspectionPage(inspectionPage);
        inspectionPage.ViewModel!.ChooseAnotherCommand.Execute(null);

        Assert.AreEqual(1, importNavigationCount);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void AttachModelInspectionPage_SamplesFooterAndRejectsStaleSender()
    {
        var shell = new OnboardingShellPage();
        var indicator = (OnboardingStageIndicator)shell.FindName(
            "StageIndicator");
        var first = new ModelInspectionPage();
        var second = new ModelInspectionPage();
        int liveNotifications = indicator.LiveRegionChangeNotificationCount;

        shell.AttachModelInspectionPage(first);

        Assert.AreEqual(first.CurrentFooterStatus, indicator.InspectionStatus);

        shell.AttachModelInspectionPage(second);
        RaiseFooterStatusChanged(
            first,
            sender: first,
            InspectionFooterStatus.Complete);
        Assert.AreEqual(
            second.CurrentFooterStatus,
            indicator.InspectionStatus,
            "A detached page cannot mutate the shell footer.");

        RaiseFooterStatusChanged(
            second,
            sender: first,
            InspectionFooterStatus.Complete);
        Assert.AreEqual(
            second.CurrentFooterStatus,
            indicator.InspectionStatus,
            "The active event must still validate sender identity.");

        RaiseFooterStatusChanged(
            second,
            sender: second,
            InspectionFooterStatus.NotComplete);

        Assert.AreEqual(
            InspectionFooterStatus.NotComplete,
            indicator.InspectionStatus);
        Assert.AreEqual(
            liveNotifications,
            indicator.LiveRegionChangeNotificationCount,
            "Footer status is sampled without adding a duplicate polite live announcement.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void InactiveInspectionPage_CannotDriveShellNavigation()
    {
        var shell = new OnboardingShellPage();
        var stageFrame = (Frame)shell.FindName("StageFrame");

        Assert.IsTrue(shell.NavigateToModelInspection(
            CreateRequest(@"C:\Models\first.gguf")));
        var inactivePage = (ModelInspectionPage)stageFrame.Content;

        ModelInspectionRequest activeRequest = CreateRequest(
            @"C:\Models\second.gguf");
        Assert.IsTrue(shell.NavigateToModelInspection(activeRequest));
        var activePage = (ModelInspectionPage)stageFrame.Content;

        RaiseChooseAnotherModelRequestedIfSubscribed(inactivePage);

        Assert.AreSame(activePage, stageFrame.Content);
        Assert.AreSame(activeRequest, activePage.Request);
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void InactiveImportPage_CannotReplaceActiveInspection()
    {
        var shell = new OnboardingShellPage();
        var stageFrame = (Frame)shell.FindName("StageFrame");
        var inactiveImportPage = new ModelImportPage();
        shell.AttachModelImportPage(inactiveImportPage);

        ModelInspectionRequest activeRequest = CreateRequest(
            @"C:\Models\active.gguf");
        RaiseModelInspectionRequested(inactiveImportPage, activeRequest);
        var activeInspectionPage = (ModelInspectionPage)stageFrame.Content;

        RaiseModelInspectionRequestedIfSubscribed(
            inactiveImportPage,
            CreateRequest(@"C:\Models\stale.gguf"));

        Assert.AreSame(activeInspectionPage, stageFrame.Content);
        Assert.AreSame(activeRequest, activeInspectionPage.Request);
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task FailedInspectionNavigation_RetainsImportStageAndSubscription()
    {
        string selectedModelPath = CreateTemporaryModelFile(lengthBytes: 64);

        try
        {
            var shell = new OnboardingShellPage();
            var stageFrame = (Frame)shell.FindName("StageFrame");
            var modelImportPage = CreateSuccessfulModelImportPage(
                selectedModelPath,
                fileSizeBytes: 64);
            shell.AttachModelImportPage(modelImportPage);
            await modelImportPage.BrowseFilesAsync();

            NavigatingCancelEventHandler cancelInspectionNavigation =
                (_, eventArguments) =>
                {
                    if (eventArguments.SourcePageType ==
                        typeof(ModelInspectionPage))
                    {
                        eventArguments.Cancel = true;
                    }
                };
            stageFrame.Navigating += cancelInspectionNavigation;

            Assert.IsTrue(modelImportPage.TryRequestModelInspection());
            Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
            Assert.AreEqual(typeof(ModelImportPage), stageFrame.SourcePageType);

            stageFrame.Navigating -= cancelInspectionNavigation;

            Assert.IsTrue(modelImportPage.TryRequestModelInspection());
            Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
            Assert.AreEqual(
                typeof(ModelInspectionPage),
                stageFrame.SourcePageType);
        }
        finally
        {
            DeleteTemporaryModelFile(selectedModelPath);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void FailedChooseAnotherNavigation_RetainsInspectionStageAndSubscription()
    {
        var shell = new OnboardingShellPage();
        var stageFrame = (Frame)shell.FindName("StageFrame");
        Assert.IsTrue(shell.NavigateToModelInspection(
            CreateRequest(@"C:\Models\granite.gguf")));
        var inspectionPage = (ModelInspectionPage)stageFrame.Content;

        NavigatingCancelEventHandler cancelImportNavigation =
            (_, eventArguments) =>
            {
                if (eventArguments.SourcePageType == typeof(ModelImportPage))
                {
                    eventArguments.Cancel = true;
                }
            };
        stageFrame.Navigating += cancelImportNavigation;

        inspectionPage.ViewModel!.ChooseAnotherCommand.Execute(null);

        Assert.AreSame(inspectionPage, stageFrame.Content);
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);

        stageFrame.Navigating -= cancelImportNavigation;
        inspectionPage.ViewModel.ChooseAnotherCommand.Execute(null);

        Assert.AreEqual(typeof(ModelImportPage), stageFrame.SourcePageType);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
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
                ggufVersion: 3,
                fileLastWriteTimeUtc: new DateTimeOffset(
                    File.GetLastWriteTimeUtc(selectedModelPath)));

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

    private static void RaiseModelInspectionRequested(
        ModelImportPage modelImportPage,
        ModelInspectionRequest request)
    {
        FieldInfo? eventField = typeof(ModelImportPage).GetField(
            "ModelInspectionRequested",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(eventField);

        var eventHandler = eventField.GetValue(modelImportPage)
            as EventHandler<ModelInspectionRequestedEventArgs>;
        Assert.IsNotNull(eventHandler);

        eventHandler.Invoke(
            modelImportPage,
            new ModelInspectionRequestedEventArgs(request));
    }

    private static void RaiseModelInspectionRequestedIfSubscribed(
        ModelImportPage modelImportPage,
        ModelInspectionRequest request)
    {
        FieldInfo? eventField = typeof(ModelImportPage).GetField(
            "ModelInspectionRequested",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(eventField);

        var eventHandler = eventField.GetValue(modelImportPage)
            as EventHandler<ModelInspectionRequestedEventArgs>;
        eventHandler?.Invoke(
            modelImportPage,
            new ModelInspectionRequestedEventArgs(request));
    }

    private static void RaiseChooseAnotherModelRequestedIfSubscribed(
        ModelInspectionPage modelInspectionPage)
    {
        FieldInfo? eventField = typeof(ModelInspectionPage).GetField(
            "ChooseAnotherModelRequested",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(eventField);

        var eventHandler = eventField.GetValue(modelInspectionPage)
            as EventHandler;
        eventHandler?.Invoke(modelInspectionPage, EventArgs.Empty);
    }

    private static void RaiseFooterStatusChanged(
        ModelInspectionPage modelInspectionPage,
        object sender,
        InspectionFooterStatus status)
    {
        FieldInfo? eventField = typeof(ModelInspectionPage).GetField(
            "FooterStatusChanged",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(eventField);

        var eventHandler = eventField.GetValue(modelInspectionPage)
            as EventHandler<InspectionFooterStatusChangedEventArgs>;
        eventHandler?.Invoke(
            sender,
            new InspectionFooterStatusChangedEventArgs(status));
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
