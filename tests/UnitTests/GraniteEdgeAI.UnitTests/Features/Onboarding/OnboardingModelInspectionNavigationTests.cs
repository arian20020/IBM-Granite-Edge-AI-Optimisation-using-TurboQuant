using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.Onboarding.Controls;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

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
    public async Task ChooseAnotherOwnerAwaitsRetirementBeforeChangingFrameContent()
    {
        TaskCompletionSource retirementStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseRetirement = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ModelInspectionPage page = new();
        page.NavigationRetirementOverride = async () =>
        {
            retirementStarted.SetResult();
            await releaseRetirement.Task;
        };
        ControlledInspectionFrameNavigator navigator = new(page, new());
        OnboardingShellPage shell = new(navigator.Navigate);
        Frame frame = (Frame)shell.FindName("StageFrame");
        Assert.IsTrue(shell.NavigateToModelInspection(
            CreateRequest(@"C:\Models\owned-cleanup.gguf")));

        page.ViewModel!.ChooseAnotherCommand.Execute(null);
        await retirementStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
        Assert.IsNotNull(shell.CurrentNavigationTask);
        Assert.IsFalse(shell.CurrentNavigationTask.IsCompleted);

        releaseRetirement.SetResult();
        await shell.CurrentNavigationTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task NavigationAndShutdownShareOneRetirementAndCommitDestinationOwnershipAtomically()
    {
        TaskCompletionSource retirementStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseRetirement = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int retirementCount = 0;
        ModelInspectionPage page = new();
        page.NavigationRetirementOverride = async () =>
        {
            Interlocked.Increment(ref retirementCount);
            retirementStarted.TrySetResult();
            await releaseRetirement.Task;
        };
        ControlledInspectionFrameNavigator navigator = new(page, new());
        OnboardingShellPage shell = new(navigator.Navigate);
        Frame frame = (Frame)shell.FindName("StageFrame");
        Assert.IsTrue(shell.NavigateToModelInspection(
            CreateRequest(@"C:\Models\atomic-cleanup.gguf")));

        Task<bool> navigation = shell.ReturnToModelImportAsync();
        await retirementStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ModelImportPage destination =
            Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
        Task shutdown = shell.ShutdownAsync();
        await Task.Yield();

        Assert.AreEqual(1, Volatile.Read(ref retirementCount),
            "Navigation and app close must await the same retirement owner.");
        Assert.IsFalse(frame.IsHitTestVisible,
            "The destination frame cannot accept input before ownership commits.");
        Assert.IsFalse(destination.IsEnabled,
            "The fresh import page cannot accept input before subscriptions attach.");
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);

        releaseRetirement.SetResult();
        Assert.IsTrue(await navigation.WaitAsync(TimeSpan.FromSeconds(5)));
        await shutdown.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(1, Volatile.Read(ref retirementCount));
        Assert.IsTrue(frame.IsHitTestVisible);
        Assert.IsTrue(destination.IsEnabled);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
        Assert.IsNull(shell.ActiveInspectionPageForTesting);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ThrowingRetirementCommitsInteractiveOwnedRecoveryDestination()
    {
        TaskCompletionSource retirementStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseRetirement = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int retirementCount = 0;
        ModelInspectionPage page = new();
        page.NavigationRetirementOverride = async () =>
        {
            Interlocked.Increment(ref retirementCount);
            retirementStarted.TrySetResult();
            await releaseRetirement.Task;
            throw new InvalidOperationException("controlled retirement failure");
        };
        ControlledInspectionFrameNavigator navigator = new(page, new());
        OnboardingShellPage shell = new(navigator.Navigate);
        Frame frame = (Frame)shell.FindName("StageFrame");
        Assert.IsTrue(shell.NavigateToModelInspection(
            CreateRequest(@"C:\Models\retirement-failure.gguf")));

        Task<bool> navigation = shell.ReturnToModelImportAsync();
        await retirementStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ModelImportPage destination =
            Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
        Assert.IsFalse(destination.IsEnabled);
        releaseRetirement.SetResult();

        Assert.IsFalse(await navigation.WaitAsync(TimeSpan.FromSeconds(5)),
            "A controlled recovery destination must report retirement failure.");
        Assert.AreEqual(1, Volatile.Read(ref retirementCount));
        Assert.AreSame(destination, frame.Content);
        Assert.IsTrue(frame.IsHitTestVisible);
        Assert.IsTrue(destination.IsEnabled);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
        Assert.IsNull(shell.ActiveInspectionPageForTesting);

        ModelInspectionRequest next = CreateRequest(
            @"C:\Models\recovered-next.gguf");
        RaiseModelInspectionRequested(destination, next);
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage,
            "The visible recovery destination must own the live subscription.");
        Assert.AreSame(
            next,
            Assert.IsInstanceOfType<ModelInspectionPage>(frame.Content).Request);

        await shell.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, Volatile.Read(ref retirementCount));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ShutdownOwnerAwaitsActiveInspectionRetirement()
    {
        TaskCompletionSource releaseRetirement = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ModelInspectionPage page = new();
        page.NavigationRetirementOverride = () => releaseRetirement.Task;
        ControlledInspectionFrameNavigator navigator = new(page, new());
        OnboardingShellPage shell = new(navigator.Navigate);
        Assert.IsTrue(shell.NavigateToModelInspection(
            CreateRequest(@"C:\Models\close-cleanup.gguf")));

        Task shutdown = shell.ShutdownAsync();

        Assert.IsFalse(shutdown.IsCompleted);
        releaseRetirement.SetResult();
        await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsNull(shell.ActiveInspectionPageForTesting);
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
        OnboardingStage navigationStage = shell.CurrentStage;

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
        Assert.AreEqual(
            navigationStage,
            shell.CurrentStage,
            "Footer status cannot advance the onboarding navigation stage.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task LiveFooterPipeline_DoesNotAdvanceStageOrPermitRetiredPageUpdates()
    {
        var service = new ControlledInspectionService();
        service.QueueCall(ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(ModelInspectionOutcome.Ready)));
        var page = new ModelInspectionPage(service);
        var replacement = new ModelInspectionPage(
            new ControlledInspectionService());
        var navigator = new ControlledInspectionFrameNavigator(
            page,
            replacement);
        var shell = new OnboardingShellPage(navigator.Navigate);
        var stageFrame = (Frame)shell.FindName("StageFrame");
        var indicator = (OnboardingStageIndicator)shell.FindName(
            "StageIndicator");

        try
        {
            Assert.IsTrue(shell.NavigateToModelInspection(
                CreateRequest(@"C:\Models\controlled-footer.gguf")));
            Assert.AreSame(
                page,
                stageFrame.Content,
                "The controlled page must participate in the Frame navigation lifetime under test.");

            Task completeApplied = ObserveInspectionStatusAsync(
                indicator,
                InspectionFooterStatus.Complete);
            await page.StartInspectionIfReadyAsync()!;
            await completeApplied;

            Assert.AreEqual(
                InspectionFooterStatus.Complete,
                indicator.InspectionStatus);
            Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);

            ControlledCall staleCall = service.QueueCall();
            Task retryApplied = ObserveInspectionStatusAsync(
                indicator,
                InspectionFooterStatus.InProgress);
            ModelInspectionViewModel retiredViewModel = page.ViewModel!;
            Task retryRun = retiredViewModel.StartAsync();
            await retryApplied;
            Assert.AreEqual(
                InspectionFooterStatus.InProgress,
                indicator.InspectionStatus);
            Assert.IsTrue(staleCall.CancellationToken.CanBeCanceled);

            ModelInspectionPagePresentation retainedPresentation =
                page.CurrentPresentation!;
            InspectionFooterStatus retainedFooter = page.CurrentFooterStatus;

            EventHandler capturedChooseAnother =
                CaptureChooseAnotherModelRequested(page);
            EventHandler<InspectionFooterStatusChangedEventArgs>
                capturedFooter = CaptureFooterStatusChanged(page);
            ModelInspectionRequest replacementRequest = CreateRequest(
                @"C:\Models\shell-second.gguf");
            Assert.IsTrue(shell.NavigateToModelInspection(replacementRequest));
            Assert.AreSame(replacement, stageFrame.Content);
            InspectionFooterStatus replacementStatus =
                replacement.CurrentFooterStatus;
            Assert.IsNull(
                page.ViewModel,
                "Frame replacement must retire the exact controlled page that supplied the live footer.");
            Assert.IsTrue(staleCall.CancellationToken.IsCancellationRequested);
            ModelInspectionViewSnapshot retiredSnapshot =
                retiredViewModel.Snapshot;

            staleCall.Complete(CreateFailureResult("retired-shell-page"));
            await retryRun.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreSame(retiredSnapshot, retiredViewModel.Snapshot);
            Assert.AreSame(retainedPresentation, page.CurrentPresentation);
            Assert.AreEqual(retainedFooter, page.CurrentFooterStatus);

            capturedFooter.Invoke(
                page,
                new InspectionFooterStatusChangedEventArgs(
                    InspectionFooterStatus.Complete));
            capturedChooseAnother.Invoke(page, EventArgs.Empty);

            Assert.AreEqual(replacementStatus, indicator.InspectionStatus);
            Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
            Assert.AreSame(replacement, stageFrame.Content);
            Assert.AreSame(replacementRequest, replacement.Request);
            Assert.HasCount(0, stageFrame.BackStack);
            Assert.IsFalse(stageFrame.CanGoBack);
        }
        finally
        {
            ModelInspectionPage? framePage =
                stageFrame.Content as ModelInspectionPage;
            if (framePage is not null)
            {
                InvokeNavigation(
                    framePage,
                    "OnNavigatedFrom",
                    parameter: null);
            }

            stageFrame.Content = null;
            if (!ReferenceEquals(page, framePage) && page.ViewModel is not null)
            {
                InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
            }

            if (!ReferenceEquals(replacement, framePage) &&
                replacement.ViewModel is not null)
            {
                InvokeNavigation(
                    replacement,
                    "OnNavigatedFrom",
                    parameter: null);
            }
        }
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
        int retirementCount = 0;
        inspectionPage.NavigationRetirementOverride = () =>
        {
            retirementCount++;
            return Task.CompletedTask;
        };

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
        Assert.AreEqual(0, retirementCount);
        Assert.IsTrue(stageFrame.IsHitTestVisible);
        Assert.IsTrue(inspectionPage.IsEnabled);

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

    private static EventHandler CaptureChooseAnotherModelRequested(
        ModelInspectionPage modelInspectionPage)
    {
        FieldInfo? eventField = typeof(ModelInspectionPage).GetField(
            "ChooseAnotherModelRequested",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(eventField);
        var eventHandler = eventField.GetValue(modelInspectionPage) as
            EventHandler;
        Assert.IsNotNull(eventHandler);
        return eventHandler;
    }

    private static EventHandler<InspectionFooterStatusChangedEventArgs>
        CaptureFooterStatusChanged(ModelInspectionPage modelInspectionPage)
    {
        FieldInfo? eventField = typeof(ModelInspectionPage).GetField(
            "FooterStatusChanged",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(eventField);
        var eventHandler = eventField.GetValue(modelInspectionPage) as
            EventHandler<InspectionFooterStatusChangedEventArgs>;
        Assert.IsNotNull(eventHandler);
        return eventHandler;
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

    private static ModelInspectionExecutionResult CreateFailureResult(
        string discriminator) =>
        ModelInspectionExecutionResult.OperationalFailure(
            new ModelInspectionOperationalFailure(
                $"MI-OP-{discriminator}",
                "Model inspection could not be completed.",
                "Controlled shell test failure."));

    private static async Task ObserveInspectionStatusAsync(
        OnboardingStageIndicator indicator,
        InspectionFooterStatus expectedStatus)
    {
        if (indicator.InspectionStatus == expectedStatus)
        {
            return;
        }

        var observed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        long callbackToken = indicator.RegisterPropertyChangedCallback(
            OnboardingStageIndicator.InspectionStatusProperty,
            (_, _) =>
            {
                if (indicator.InspectionStatus == expectedStatus)
                {
                    observed.TrySetResult(true);
                }
            });

        try
        {
            await observed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException error)
        {
            throw new AssertFailedException(
                $"Expected inspection footer {expectedStatus}; " +
                $"current footer is {indicator.InspectionStatus}.",
                error);
        }
        finally
        {
            indicator.UnregisterPropertyChangedCallback(
                OnboardingStageIndicator.InspectionStatusProperty,
                callbackToken);
        }
    }

    private static void InvokeNavigation(
        ModelInspectionPage page,
        string methodName,
        object? parameter)
    {
        NavigationEventArgs navigation = CreateNavigationEventArgs(parameter);
        MethodInfo? method = typeof(ModelInspectionPage).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        try
        {
            method.Invoke(page, [navigation]);
        }
        catch (TargetInvocationException error)
            when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
        }
    }

    private static NavigationEventArgs CreateNavigationEventArgs(
        object? parameter)
    {
        NavigationEventArgs? captured = null;
        var frame = new Frame();
        frame.Navigated += (_, eventArguments) => captured = eventArguments;
        Assert.IsTrue(frame.Navigate(typeof(Page), parameter));
        Assert.IsNotNull(captured);
        return captured;
    }

    private sealed class ControlledInspectionFrameNavigator
    {
        private readonly Queue<ModelInspectionPage> pages;

        internal ControlledInspectionFrameNavigator(
            params ModelInspectionPage[] pages)
        {
            this.pages = new Queue<ModelInspectionPage>(pages);
        }

        internal bool Navigate(
            Frame frame,
            ModelInspectionRequest request)
        {
            if (pages.Count == 0)
            {
                return false;
            }

            if (frame.Content is ModelInspectionPage current)
            {
                InvokeNavigation(
                    current,
                    "OnNavigatedFrom",
                    parameter: null);
            }

            ModelInspectionPage next = pages.Dequeue();
            frame.Content = next;
            InvokeNavigation(next, "OnNavigatedTo", request);
            return true;
        }
    }

    private sealed class ControlledInspectionService : IModelInspectionService
    {
        private readonly Queue<ControlledCall> calls = new();

        internal ControlledCall QueueCall(
            ModelInspectionExecutionResult? completedResult = null)
        {
            var call = new ControlledCall();
            if (completedResult is not null)
            {
                call.Complete(completedResult);
            }

            calls.Enqueue(call);
            return call;
        }

        public Task<ModelInspectionExecutionResult> InspectAsync(
            ModelInspectionRequest request,
            IProgress<ModelInspectionProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (calls.Count == 0)
            {
                throw new InvalidOperationException(
                    "No controlled shell inspection call was queued.");
            }

            ControlledCall call = calls.Dequeue();
            call.Bind(progress, cancellationToken);
            return call.Completion.Task;
        }
    }

    private sealed class ControlledCall
    {
        internal TaskCompletionSource<ModelInspectionExecutionResult> Completion
            { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal IProgress<ModelInspectionProgress>? Progress { get; private set; }

        internal CancellationToken CancellationToken { get; private set; }

        internal void Bind(
            IProgress<ModelInspectionProgress>? progress,
            CancellationToken cancellationToken)
        {
            Progress = progress;
            CancellationToken = cancellationToken;
        }

        internal void Complete(ModelInspectionExecutionResult result) =>
            Completion.SetResult(result);
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
