using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.Features.ModelInspection.Views;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies the page-owned lifecycle between onboarding navigation, the
/// application service, and the four replaceable presentation cards.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ModelInspectionPageNavigationTests
{
    private static readonly DateTimeOffset FixedUtc = new(
        2026,
        8,
        5,
        12,
        0,
        0,
        TimeSpan.Zero);

    [UITestMethod]
    [TestCategory("WinUI")]
    public void FrameNavigation_WithRequest_CreatesInitialPageForExactRequest()
    {
        ModelInspectionRequest request = CreateRequest(
            @"C:\Models\granite-4.1-3b-instruct.gguf");
        var frame = new Frame();

        bool navigationSucceeded = frame.Navigate(
            typeof(ModelInspectionPage),
            request);

        var page = frame.Content as ModelInspectionPage;
        try
        {
            Assert.IsTrue(navigationSucceeded);
            Assert.IsNotNull(page);
            Assert.AreSame(request, page.Request);
            Assert.IsNotNull(page.ViewModel);
            Assert.AreSame(request, page.ViewModel.Request);
            Assert.IsNull(page.CurrentInspectionTask);

            ModelInspectionPagePresentation snapshot =
                AssertCompleteSnapshotApplied(page);
            Assert.AreEqual(
                new ModelInspectionRenderKey(0, 0),
                snapshot.RenderKey);
            Assert.AreEqual(
                ModelInspectionFigmaState.InspectionProgress,
                snapshot.State);
            Assert.AreEqual(
                InspectionContentCardMode.Progress,
                snapshot.ContentCard.Mode);
            Assert.AreEqual(
                "0 of 5 checks complete",
                snapshot.ContentCard.ProgressSummary);
            Assert.HasCount(5, snapshot.ContentCard.Items);
            Assert.IsTrue(snapshot.ContentCard.Items.All(item =>
                item.Status == InspectionContentStatus.Waiting &&
                item.StatusText == "Waiting"));
            Assert.AreEqual(
                InspectionOutcomePresentationKind.Hidden,
                snapshot.OutcomeCard.Kind);
            Assert.AreEqual(
                "Granite 4.1 3B Instruct",
                snapshot.ModelCard.ModelName);
            Assert.IsFalse(snapshot.ActionCard.CancelAction.IsEnabled);
        }
        finally
        {
            if (page is not null)
            {
                InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
            }

            frame.Content = null;
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OnNavigatedTo_WithInvalidParameter_RejectsWithoutRetiringCurrentPage()
    {
        var service = new ControlledInspectionService();
        ModelInspectionPage page = CreateUnactivatedPage(service);
        ModelInspectionRequest request = CreateRequest();
        InvokeNavigation(page, "OnNavigatedTo", request);
        ModelInspectionViewModel originalViewModel = page.ViewModel!;
        ModelInspectionPagePresentation originalSnapshot =
            AssertCompleteSnapshotApplied(page);

        Assert.ThrowsExactly<ArgumentException>(() =>
            InvokeNavigation(page, "OnNavigatedTo", parameter: null));
        Assert.ThrowsExactly<ArgumentException>(() =>
            InvokeNavigation(page, "OnNavigatedTo", new object()));

        Assert.AreSame(request, page.Request);
        Assert.AreSame(originalViewModel, page.ViewModel);
        Assert.AreSame(originalSnapshot, page.CurrentPresentation);
        Assert.IsTrue(originalViewModel.ChooseAnotherCommand.CanExecute(null));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task StartInspectionIfReadyAsync_StartsOnceForEachNavigation()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var page = CreatePage(service, CreateRequest());

        Task? first = page.StartInspectionIfReadyAsync();
        Task? reloaded = page.StartInspectionIfReadyAsync();

        Assert.IsNotNull(first);
        Assert.AreSame(first, reloaded);
        Assert.AreSame(first, page.CurrentInspectionTask);
        await DrainDispatcherAsync(page);
        Assert.AreEqual(1, service.CallCount);
        Assert.IsTrue(page.ViewModel!.IsRunActive);
        Assert.IsTrue(
            AssertCompleteSnapshotApplied(page)
                .ActionCard.CancelAction.IsEnabled);

        call.Complete(CreateFailureResult("first-navigation"));
        await first;

        ModelInspectionRequest replacement = CreateRequest(
            @"C:\Models\replacement.gguf");
        ControlledCall replacementCall = service.QueueCall();
        InvokeNavigation(page, "OnNavigatedTo", replacement);

        Task? replacementTask = page.StartInspectionIfReadyAsync();

        Assert.IsNotNull(replacementTask);
        Assert.AreNotSame(first, replacementTask);
        await DrainDispatcherAsync(page);
        Assert.AreEqual(2, service.CallCount);
        Assert.AreSame(replacement, replacementCall.Request);
        replacementCall.Complete(CreateFailureResult("replacement"));
        await replacementTask;
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task LoadedInVisualTree_AutomaticallyStartsExactlyOneInspection()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var page = CreatePage(service, CreateRequest());
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window
        {
            Content = page
        };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await DrainDispatcherAsync(page);

            Assert.AreEqual(1, service.CallCount);
            Task? currentInspectionTask = page.CurrentInspectionTask;
            Assert.IsNotNull(currentInspectionTask);
            Assert.IsTrue(page.ViewModel!.IsRunActive);
            ModelInspectionPagePresentation startup =
                AssertCompleteSnapshotApplied(page);
            AssertStartupPresentation(startup.ContentCard);
            Assert.IsTrue(startup.ActionCard.CancelAction.IsEnabled);
            Assert.HasCount(5, startup.ContentCard.Items);
            Assert.IsTrue(startup.ContentCard.Items.All(row =>
                row.Status == InspectionContentStatus.Waiting && !row.IsActive));
            var previewHost = (ContentControl)page.FindName("InspectionPreviewHost");
            Assert.AreSame(
                previewHost,
                FocusManager.GetFocusedElement(page.XamlRoot),
                "Rendering startup must retain focus in the current preview host.");

            // a subsequent layout pass must not create a second attempt
            page.InvalidateMeasure();
            page.UpdateLayout();
            Assert.AreEqual(1, service.CallCount);

            call.Complete(CreateFailureResult("loaded-lifecycle"));
            await currentInspectionTask.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ProgressBurst_CoalescesAndRetainsControlIdentity()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var dispatcher = new ManualRenderDispatcher();
        var page = CreateInjectedPage(
            service,
            CreateRequest(),
            dispatcher,
            new RecordingPageAnimationDriver(),
            new RecordingMotionSettings(animationsEnabled: false));
        try
        {
            ModelInspectionPreviewProjection preview = Preview(page);
            var modelControl = preview.ModelCard;
            var contentControl = preview.ContentCard;
            var outcomeControl = preview.OutcomeCard;
            var actionControl = preview.ActionCard;
            UserControl retainedView = preview.Element;
            InspectionProgressRows progressRows =
                contentControl.Presentation.ProgressRows;
            object[] retainedRows = progressRows.Items.Cast<object>().ToArray();
            Task run = page.StartInspectionIfReadyAsync()!;
            Assert.AreEqual(0, service.CallCount);
            Assert.AreEqual(2, dispatcher.PendingCount);
            dispatcher.RunNext();
            ModelInspectionPagePresentation active =
                AssertCompleteSnapshotApplied(page);
            AssertStartupPresentation(active.ContentCard);
            Assert.IsTrue(active.ActionCard.CancelAction.IsEnabled);
            Assert.AreEqual(0, service.CallCount);
            dispatcher.RunNext();
            await service.FirstCallStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(10));
            Assert.AreEqual(1, service.CallCount);
            InspectionModelCardPresentation retainedModel = modelControl.Presentation;
            InspectionContentCardPresentation startupContent =
                contentControl.Presentation;
            InspectionOutcomePresentation retainedOutcome =
                outcomeControl.Presentation;
            InspectionActionCardPresentation retainedActions =
                actionControl.Presentation;

            call.Report(CreateProgress(
                ModelInspectionStage.CheckModelPackage,
                completedStageCount: 0));
            call.Report(CreateProgress(
                ModelInspectionStage.ReadModelConfiguration,
                completedStageCount: 1));
            call.Report(CreateProgress(
                ModelInspectionStage.ValidateTokenizerAndChatSetup,
                completedStageCount: 2));

            Assert.AreEqual(
                1,
                dispatcher.PendingCount,
                "A burst must own exactly one queued page render.");
            Assert.AreSame(active, page.CurrentPresentation);
            dispatcher.RunAll();

            ModelInspectionPagePresentation latest =
                AssertCompleteSnapshotApplied(page);
            Assert.AreNotSame(startupContent, contentControl.Presentation);
            InspectionContentCardPresentation retainedContent =
                contentControl.Presentation;
            Assert.AreEqual(
                "2 of 5 checks complete",
                latest.ContentCard.ProgressSummary);
            Assert.AreSame(retainedView, Preview(page).Element);
            Assert.AreSame(modelControl, Preview(page).ModelCard);
            Assert.AreSame(contentControl, Preview(page).ContentCard);
            Assert.AreSame(outcomeControl, Preview(page).OutcomeCard);
            Assert.AreSame(actionControl, Preview(page).ActionCard);
            AssertModelPresentation(retainedModel, modelControl.Presentation);
            AssertContentPresentation(retainedContent, contentControl.Presentation);
            AssertOutcomePresentation(retainedOutcome, outcomeControl.Presentation);
            AssertActionPresentation(retainedActions, actionControl.Presentation);
            CollectionAssert.AreEqual(
                retainedRows,
                progressRows.Items.Cast<object>().ToArray());
            Assert.AreEqual(
                InspectionContentStatus.Active,
                progressRows.Items[2].Status);

            latest.ActionCard.CancelAction.Command!.Execute(null);
            Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
            Assert.AreEqual(1, dispatcher.PendingCount);
            dispatcher.RunAll();

            // The current preview uses presentation adapters rather than
            // dependency properties. Only the action snapshot should change.
            AssertModelPresentation(retainedModel, modelControl.Presentation);
            AssertContentPresentation(retainedContent, contentControl.Presentation);
            AssertOutcomePresentation(retainedOutcome, outcomeControl.Presentation);
            Assert.AreNotSame(retainedActions, actionControl.Presentation);
            Assert.IsFalse(
                page.CurrentPresentation!.ActionCard.CancelAction.IsEnabled);
            bool progressRetiredBeforeOutcome = false;
            var failurePanel = (FrameworkElement)preview.FindElement("InspectionFailurePanel")!;
            long visibilityToken = failurePanel.RegisterPropertyChangedCallback(
                UIElement.VisibilityProperty,
                (_, _) =>
                {
                    if (failurePanel.Visibility != Visibility.Visible)
                    {
                        return;
                    }

                    progressRetiredBeforeOutcome =
                        contentControl.Presentation.Mode !=
                            InspectionContentCardMode.Progress &&
                        preview.ContentSurface.Visibility == Visibility.Collapsed;
                });

            call.Complete(CreateFailureResult("terminal"));
            await AwaitRunAndRenderAsync(page, run);
            dispatcher.RunAll();
            failurePanel.UnregisterPropertyChangedCallback(
                UIElement.VisibilityProperty,
                visibilityToken);

            ModelInspectionPagePresentation terminal =
                AssertCompleteSnapshotApplied(page);
            Assert.IsTrue(
                progressRetiredBeforeOutcome,
                "Progress semantics must retire before the terminal banner becomes visible.");
            Assert.AreEqual(
                InspectionOutcomePresentationKind.OperationalFailure,
                terminal.OutcomeCard.Kind);
            Assert.AreEqual(
                InspectionContentCardMode.OperationalFailure,
                terminal.ContentCard.Mode);
            Assert.IsTrue(terminal.ActionCard.PrimaryAction.IsEnabled);
        }
        finally
        {
            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        }

        var rejectedService = new ControlledInspectionService();
        rejectedService.QueueCall();
        var rejectedDispatcher = new ManualRenderDispatcher
        {
            SuccessfulEnqueueLimit = 1
        };
        var rejectedPage = CreateInjectedPage(
            rejectedService,
            CreateRequest(),
            rejectedDispatcher,
            new RecordingPageAnimationDriver(),
            new RecordingMotionSettings(animationsEnabled: false));
        try
        {
            Task rejectedRun = rejectedPage.StartInspectionIfReadyAsync()!;

            Assert.AreEqual(0, rejectedService.CallCount);
            await rejectedRun;
            rejectedDispatcher.RunAll();
            Assert.AreEqual(0, rejectedService.CallCount);
            Assert.AreEqual(
                ModelInspectionExecutionStatus.OperationalFailure,
                rejectedPage.ViewModel!.Result?.Status);
            Assert.AreEqual(
                "MI-OP-STARTUP-PRESENTATION",
                rejectedPage.ViewModel.Result?.Failure?.Code);
            Assert.AreEqual(
                "Model inspection could not be started.",
                rejectedPage.ViewModel.Result?.Failure?.UserMessage);
            Assert.AreEqual(
                "The secure inspection startup presentation could not be confirmed.",
                rejectedPage.ViewModel.Result?.Failure?.TechnicalDetail);
        }
        finally
        {
            InvokeNavigation(rejectedPage, "OnNavigatedFrom", parameter: null);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CancelCommand_DisablesImmediatelyThenShowsConfirmedCancellation()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var page = CreatePage(service, CreateRequest());
        Task run = page.StartInspectionIfReadyAsync()!;
        await DrainDispatcherAsync(page);
        InspectionActionPresentation cancel =
            AssertCompleteSnapshotApplied(page).ActionCard.CancelAction;

        cancel.Command!.Execute(null);
        await DrainDispatcherAsync(page);

        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(
            AssertCompleteSnapshotApplied(page)
                .ActionCard.CancelAction.IsEnabled);
        Assert.IsTrue(page.ViewModel!.IsRunActive);

        call.Complete(ModelInspectionExecutionResult.Cancelled(cooperative: true));
        await AwaitRunAndRenderAsync(page, run);
        await DrainDispatcherAsync(page);

        ModelInspectionPagePresentation terminal =
            AssertCompleteSnapshotApplied(page);
        Assert.AreEqual(
            InspectionOutcomePresentationKind.Cancelled,
            terminal.OutcomeCard.Kind);
        Assert.AreEqual(
            InspectionContentCardMode.Cancelled,
            terminal.ContentCard.Mode);
        Assert.IsFalse(page.ViewModel.CancelCommand.CanExecute(null));

        var preStartService = new ControlledInspectionService();
        preStartService.QueueCall();
        var preStartDispatcher = new ManualRenderDispatcher();
        var preStartPage = CreateInjectedPage(
            preStartService,
            CreateRequest(@"C:\Models\cancel-before-preflight.gguf"),
            preStartDispatcher,
            new RecordingPageAnimationDriver(),
            new RecordingMotionSettings(animationsEnabled: false));
        try
        {
            Task preStartRun = preStartPage.StartInspectionIfReadyAsync()!;
            Assert.AreSame(preStartRun, preStartPage.CurrentInspectionTask);
            preStartDispatcher.RunNext();
            AssertStartupPresentation(
                AssertCompleteSnapshotApplied(preStartPage).ContentCard);
            Assert.AreEqual(0, preStartService.CallCount);

            preStartPage.ViewModel!.CancelCommand.Execute(null);
            await Task.Yield();

            Assert.IsTrue(
                preStartRun.IsCompleted,
                "Cancel must settle CurrentInspectionTask while startup presentation is held.");
            await preStartRun;
            preStartDispatcher.RunAll();
            Assert.AreEqual(0, preStartService.CallCount);
            Assert.AreEqual(
                ModelInspectionExecutionStatus.Cancelled,
                preStartPage.ViewModel.Result?.Status);
            Assert.AreNotEqual(
                "MI-OP-CANCELLATION-UNCONFIRMED",
                preStartPage.ViewModel.Result?.Failure?.Code);
        }
        finally
        {
            InvokeNavigation(preStartPage, "OnNavigatedFrom", parameter: null);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RetryCommand_RunsSameRequestAsANewAttempt()
    {
        var service = new ControlledInspectionService();
        ControlledCall first = service.QueueCall(
            CreateFailureResult("first"));
        ControlledCall second = service.QueueCall(
            CreateFailureResult("second"));
        ModelInspectionRequest request = CreateRequest();
        var page = CreatePage(service, request);

        await page.StartInspectionIfReadyAsync()!;
        await DrainDispatcherAsync(page);
        InspectionActionPresentation retry =
            AssertCompleteSnapshotApplied(page).ActionCard.PrimaryAction;

        retry.Command!.Execute(null);
        await DrainDispatcherAsync(page);

        Assert.AreEqual(2, service.CallCount);
        Assert.AreSame(request, first.Request);
        Assert.AreSame(request, second.Request);
        Assert.AreEqual(
            "MI-OP-second",
            AssertCompleteSnapshotApplied(page).ContentCard.DiagnosticCode);
        Assert.IsFalse(page.ViewModel!.IsRunActive);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ChooseAnotherCommand_ForwardsOnceAndInvalidatesTheRun()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var page = CreatePage(service, CreateRequest());
        int eventCount = 0;
        page.ChooseAnotherModelRequested += (_, _) => eventCount++;
        Task run = page.StartInspectionIfReadyAsync()!;
        await DrainDispatcherAsync(page);

        page.ViewModel!.ChooseAnotherCommand.Execute(null);

        Assert.AreEqual(1, eventCount);
        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(page.ViewModel.IsRunActive);
        call.Complete(CreateFailureResult("stale-after-choose"));
        await AwaitRunAndRenderAsync(page, run);
        Assert.IsNull(page.ViewModel.Result);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task NewNavigation_RetiresPriorViewModelBeforeStaleCallbacks()
    {
        var service = new ControlledInspectionService();
        ControlledCall oldCall = service.QueueCall();
        ModelInspectionRequest oldRequest = CreateRequest();
        var page = CreatePage(service, oldRequest);
        ModelInspectionViewModel oldViewModel = page.ViewModel!;
        Task oldRun = page.StartInspectionIfReadyAsync()!;
        await DrainDispatcherAsync(page);
        oldCall.Report(CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            completedStageCount: 1));
        ModelInspectionRequest replacement = CreateRequest(
            @"C:\Models\replacement.gguf");

        InvokeNavigation(page, "OnNavigatedTo", replacement);

        Assert.IsTrue(oldCall.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(oldViewModel.ChooseAnotherCommand.CanExecute(null));
        Assert.AreSame(replacement, page.Request);
        Assert.AreNotSame(oldViewModel, page.ViewModel);
        Assert.IsNull(page.CurrentInspectionTask);
        ModelInspectionPagePresentation replacementSnapshot =
            AssertCompleteSnapshotApplied(page);
        Assert.AreEqual(
            "Granite 4.1 3B Instruct",
            replacementSnapshot.ModelCard.ModelName);

        oldCall.Report(CreateProgress(
            ModelInspectionStage.ValidateModelStructure,
            completedStageCount: 3));
        oldCall.Complete(CreateFailureResult("retired"));
        await oldRun;

        Assert.AreSame(replacementSnapshot, page.CurrentPresentation);
        Assert.IsNull(page.ViewModel!.Progress);
        Assert.IsNull(page.ViewModel.Result);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OnNavigatedFrom_ClearsOwnershipAndSuppressesStaleCallbacks()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var page = CreatePage(service, CreateRequest());
        ModelInspectionViewModel retiredViewModel = page.ViewModel!;
        Task run = page.StartInspectionIfReadyAsync()!;
        await DrainDispatcherAsync(page);
        call.Report(CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            completedStageCount: 1));
        await DrainDispatcherAsync(page);
        ModelInspectionPagePresentation lastVisibleSnapshot =
            AssertCompleteSnapshotApplied(page);

        InvokeNavigation(page, "OnNavigatedFrom", parameter: null);

        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        Assert.IsNull(page.Request);
        Assert.IsNull(page.ViewModel);
        Assert.IsNull(page.CurrentInspectionTask);
        Assert.IsFalse(retiredViewModel.ChooseAnotherCommand.CanExecute(null));

        call.Report(CreateProgress(
            ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
            completedStageCount: 4));
        call.Complete(CreateFailureResult("stale-after-navigation"));
        await AwaitRunAndRenderAsync(page, run);

        Assert.AreSame(lastVisibleSnapshot, page.CurrentPresentation);
        AssertCompleteSnapshotApplied(page);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_ConfiguresProgressAndOutcomeLiveRegions()
    {
        var service = new ControlledInspectionService();
        ModelInspectionPage page = CreateUnactivatedPage(service);
        var stack = (StackPanel)page.FindName("InspectionContentStack");
        var heading = (StackPanel)stack.Children[0];
        var explanation = (TextBlock)heading.Children[1];
        ModelInspectionPreviewProjection preview = Preview(page);
        FrameworkElement content = preview.ContentSurface;
        var outcome = (FrameworkElement)preview.FindElement("InspectionReadyPanel")!;

        Assert.AreEqual(
            "We are checking that the model package, tokenizer, structure, " +
            "and runtime are supported before checking hardware fit.",
            explanation.Text);
        Assert.AreEqual(
            AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(content));
        Assert.AreEqual(
            AutomationLiveSetting.Assertive,
            AutomationProperties.GetLiveSetting(outcome));

        InvokeNavigation(page, "OnNavigatedTo", CreateRequest());
        Assert.AreEqual(
            AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(content));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void InjectedNavigationLifetime_CreatesFactoriesOnceAndRetiresMotionFirst()
    {
        var service = new ControlledInspectionService();
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingPageAnimationDriver();
        var settings = new RecordingMotionSettings(animationsEnabled: true);
        var scheduler = new ImmediateMilestoneScheduler();
        int dispatcherFactoryCalls = 0;
        int driverFactoryCalls = 0;
        int settingsFactoryCalls = 0;
        int schedulerFactoryCalls = 0;
        var page = new ModelInspectionPage(
            service,
            () =>
            {
                dispatcherFactoryCalls++;
                return dispatcher;
            },
            () =>
            {
                driverFactoryCalls++;
                return driver;
            },
            () =>
            {
                settingsFactoryCalls++;
                return settings;
            },
            () =>
            {
                schedulerFactoryCalls++;
                return scheduler;
            });
        InvokeNavigation(page, "OnNavigatedTo", CreateRequest());
        ModelInspectionViewModel viewModel = page.ViewModel!;
        List<string> retirement = [];
        driver.CancelAllCallback = () => retirement.Add(
            $"cancel:{viewModel.ChooseAnotherCommand.CanExecute(null)}");
        driver.DisposeCallback = () => retirement.Add(
            $"driver:{viewModel.ChooseAnotherCommand.CanExecute(null)}");
        settings.DisposeCallback = () =>
        {
            retirement.Add(
                $"settings:{viewModel.ChooseAnotherCommand.CanExecute(null)}");
            settings.RaiseChanged();
        };
        scheduler.DisposeCallback = () => retirement.Add(
            $"scheduler:{viewModel.ChooseAnotherCommand.CanExecute(null)}");

        Assert.AreEqual(1, dispatcherFactoryCalls);
        Assert.AreEqual(1, driverFactoryCalls);
        Assert.AreEqual(1, settingsFactoryCalls);
        Assert.AreEqual(1, schedulerFactoryCalls);

        InvokeNavigation(page, "OnNavigatedFrom", parameter: null);

        CollectionAssert.AreEqual(
            new[]
            {
                "scheduler:True",
                "cancel:True",
                "driver:True",
                "settings:True"
            },
            retirement);
        Assert.AreEqual(1, scheduler.DisposeCount);
        Assert.AreEqual(1, driver.CancelAllCount);
        Assert.IsTrue(driver.IsDisposed);
        Assert.IsTrue(settings.IsDisposed);
        Assert.IsFalse(viewModel.ChooseAnotherCommand.CanExecute(null));
        Assert.IsNull(page.ViewModel);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OnNavigatedFrom_ReentrantDriverRetirementClaimsLifetimeBeforeCallbacks()
    {
        var service = new ControlledInspectionService();
        var driver = new RecordingPageAnimationDriver();
        var settings = new RecordingMotionSettings(animationsEnabled: true);
        ModelInspectionPage page = CreateInjectedPage(
            service,
            CreateRequest(),
            new ManualRenderDispatcher(),
            driver,
            settings);
        int reentrantCalls = 0;
        driver.CancelAllCallback = () =>
        {
            reentrantCalls++;
            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        };

        InvokeNavigation(page, "OnNavigatedFrom", parameter: null);

        Assert.AreEqual(1, reentrantCalls);
        Assert.AreEqual(1, driver.CancelAllCount);
        Assert.AreEqual(1, driver.DisposeCount);
        Assert.AreEqual(1, settings.DisposeCount);
        Assert.IsNull(page.Request);
        Assert.IsNull(page.ViewModel);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OnNavigatedFrom_ReentrantActivationIsRejectedDuringCleanup()
    {
        var service = new ControlledInspectionService();
        var driver = new RecordingPageAnimationDriver();
        var settings = new RecordingMotionSettings(animationsEnabled: true);
        int dispatcherFactoryCalls = 0;
        int driverFactoryCalls = 0;
        int settingsFactoryCalls = 0;
        var page = new ModelInspectionPage(
            service,
            () =>
            {
                dispatcherFactoryCalls++;
                return new ManualRenderDispatcher();
            },
            () =>
            {
                driverFactoryCalls++;
                return driver;
            },
            () =>
            {
                settingsFactoryCalls++;
                return settings;
            },
            () => new ImmediateMilestoneScheduler());
        InvokeNavigation(page, "OnNavigatedTo", CreateRequest());
        Exception? reentrantError = null;
        driver.CancelAllCallback = () =>
        {
            try
            {
                InvokeNavigation(
                    page,
                    "OnNavigatedTo",
                    CreateRequest(@"C:\Models\reentrant.gguf"));
            }
            catch (Exception error)
            {
                reentrantError = error;
                throw;
            }
        };

        InvalidOperationException thrown =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                InvokeNavigation(page, "OnNavigatedFrom", parameter: null));

        Assert.AreSame(reentrantError, thrown);
        Assert.AreEqual(1, dispatcherFactoryCalls);
        Assert.AreEqual(1, driverFactoryCalls);
        Assert.AreEqual(1, settingsFactoryCalls);
        Assert.AreEqual(1, driver.CancelAllCount);
        Assert.AreEqual(1, driver.DisposeCount);
        Assert.AreEqual(1, settings.DisposeCount);
        Assert.IsNull(page.Request);
        Assert.IsNull(page.ViewModel);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OnNavigatedFrom_AttemptsEveryOwnerAndRethrowsFirstCleanupError()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var driver = new RecordingPageAnimationDriver();
        var settings = new RecordingMotionSettings(animationsEnabled: true);
        var dispatcher = new ManualRenderDispatcher();
        ModelInspectionPage page = CreateInjectedPage(
            service,
            CreateRequest(),
            dispatcher,
            driver,
            settings);
        _ = page.StartInspectionIfReadyAsync();
        dispatcher.RunAll();
        ModelInspectionRenderCoordinator coordinator = GetCoordinator(page);
        ModelInspectionRenderKey acceptedKey =
            page.CurrentPresentation!.RenderKey;
        ModelInspectionViewModel retiredViewModel = page.ViewModel!;
        var first = new InvalidOperationException("settings-remove-first");
        var second = new InvalidOperationException("cancel-all-second");
        var third = new InvalidOperationException("driver-dispose-third");
        var fourth = new InvalidOperationException("settings-dispose-fourth");
        int cancellationCallbacks = 0;
        using CancellationTokenRegistration registration =
            call.CancellationToken.Register(() => cancellationCallbacks++);
        settings.RemoveHandlerError = first;
        driver.CancelAllCallback = () => throw second;
        driver.DisposeCallback = () => throw third;
        settings.DisposeCallback = () => throw fourth;

        InvalidOperationException thrown =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                InvokeNavigation(page, "OnNavigatedFrom", parameter: null));

        Assert.AreSame(first, thrown);
        Assert.AreEqual(1, settings.RemoveHandlerCount);
        Assert.AreEqual(
            2,
            driver.CancelAllCount,
            "Startup generation replacement and retirement each cancel owned motion once.");
        Assert.AreEqual(1, driver.DisposeCount);
        Assert.AreEqual(1, settings.DisposeCount);
        Assert.AreEqual(1, cancellationCallbacks);
        Assert.IsFalse(coordinator.IsCurrent(acceptedKey));
        Assert.IsFalse(retiredViewModel.ChooseAnotherCommand.CanExecute(null));
        Assert.IsNull(page.Request);
        Assert.IsNull(page.ViewModel);
        InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        Assert.AreEqual(2, driver.CancelAllCount);
        Assert.AreEqual(1, driver.DisposeCount);
        Assert.AreEqual(1, settings.DisposeCount);
        Assert.AreEqual(1, settings.RemoveHandlerCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OnNavigatedTo_ApplyInitialFailureRollsBackPublishedOwnershipExactlyOnce()
    {
        var service = new ControlledInspectionService();
        var driver = new RecordingPageAnimationDriver();
        var settings = new RecordingMotionSettings(animationsEnabled: true)
        {
            AnimationsEnabledError = new InvalidOperationException(
                "apply-initial")
        };
        var page = new ModelInspectionPage(
            service,
            () => new ManualRenderDispatcher(),
            () => driver,
            () => settings,
            () => new ImmediateMilestoneScheduler());

        InvalidOperationException error =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                InvokeNavigation(page, "OnNavigatedTo", CreateRequest()));

        Assert.AreSame(settings.AnimationsEnabledError, error);
        Assert.IsNull(page.Request);
        Assert.IsNull(page.ViewModel);
        Assert.AreEqual(1, driver.CancelAllCount);
        Assert.AreEqual(1, driver.DisposeCount);
        Assert.AreEqual(1, settings.DisposeCount);
        InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        Assert.AreEqual(1, driver.CancelAllCount);
        Assert.AreEqual(1, driver.DisposeCount);
        Assert.AreEqual(1, settings.DisposeCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void RetirePageLifetime_LifetimeOverflowLeavesOwnershipActive()
    {
        var service = new ControlledInspectionService();
        var driver = new RecordingPageAnimationDriver();
        var settings = new RecordingMotionSettings(animationsEnabled: true);
        ModelInspectionPage page = CreateInjectedPage(
            service,
            CreateRequest(),
            new ManualRenderDispatcher(),
            driver,
            settings);
        FieldInfo? lifetime = typeof(ModelInspectionPage).GetField(
            "_navigationLifetime",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(lifetime);
        lifetime.SetValue(page, long.MaxValue);

        Assert.ThrowsExactly<OverflowException>(() =>
            InvokeNavigation(page, "OnNavigatedFrom", parameter: null));

        Assert.IsNotNull(page.ViewModel);
        Assert.AreEqual(0, driver.CancelAllCount);
        Assert.AreEqual(0, driver.DisposeCount);
        Assert.AreEqual(0, settings.DisposeCount);
        lifetime.SetValue(page, 41L);
        InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        Assert.AreEqual(1, driver.CancelAllCount);
        Assert.AreEqual(1, driver.DisposeCount);
        Assert.AreEqual(1, settings.DisposeCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OnNavigatedFrom_RejectsTerminalCompletionDuringDriverCancellation()
    {
        var service = new ControlledInspectionService();
        service.QueueCall(CreateFailureResult("retirement-completion"));
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingPageAnimationDriver();
        var page = CreateInjectedPage(
            service,
            CreateRequest(),
            dispatcher,
            driver,
            new RecordingMotionSettings(animationsEnabled: true));
        bool retired = false;

        try
        {
            Task run = page.StartInspectionIfReadyAsync()!;
            dispatcher.RunAll();
            await AwaitRunAndRenderAsync(page, run);
            dispatcher.RunAll();
            Assert.HasCount(1, driver.TerminalStarts);
            ModelInspectionPreviewProjection preview = Preview(page);
            FrameworkElement terminalPanel = preview.OutcomeSurface;
            Assert.AreEqual(Visibility.Visible, terminalPanel.Visibility);
            InspectionContentCardPresentation retained = preview.ContentPresentation;
            int announcements = preview.OutcomeAnnouncementCount;
            bool completionWasRejected = false;
            driver.CancelAllCallback = () =>
            {
                driver.CompleteTerminal(index: 0);
                completionWasRejected =
                    terminalPanel.Visibility == Visibility.Visible &&
                    ReferenceEquals(retained, preview.ContentPresentation) &&
                    announcements == preview.OutcomeAnnouncementCount;
            };

            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
            retired = true;

            Assert.IsTrue(
                completionWasRejected,
                "A synchronous driver completion cannot mutate controls after navigation retirement begins.");
            Assert.AreSame(retained, preview.ContentPresentation);
            Assert.IsNull(page.ViewModel);
            Assert.IsTrue(driver.IsDisposed);
        }
        finally
        {
            if (!retired)
            {
                InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
            }
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OnNavigatedFrom_RetiresCoordinatorBeforeSynchronousCancellationCallback()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingPageAnimationDriver();
        var settings = new RecordingMotionSettings(animationsEnabled: true);
        var page = CreateInjectedPage(
            service,
            CreateRequest(),
            dispatcher,
            driver,
            settings);
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = page };
        bool retired = false;

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Task? run = page.CurrentInspectionTask;
            Assert.IsNotNull(run);
            dispatcher.RunAll();
            page.UpdateLayout();
            var pageScroll = (ScrollViewer)page.FindName(
                "InspectionScrollViewer");
            pageScroll.IsTabStop = true;
            Assert.IsTrue(pageScroll.Focus(FocusState.Programmatic));
            object? retainedFocus = FocusManager.GetFocusedElement(
                page.XamlRoot);
            Assert.AreSame(pageScroll, retainedFocus);
            ModelInspectionPagePresentation retainedPresentation =
                page.CurrentPresentation!;
            InspectionFooterStatus retainedFooter = page.CurrentFooterStatus;
            ModelInspectionPreviewProjection preview = Preview(page);
            int retainedProgressAnnouncements =
                preview.ProgressAnnouncementCount;
            int retainedOutcomeAnnouncements =
                preview.OutcomeAnnouncementCount;
            int retainedMotionStarts = driver.TotalStartCount;
            ModelInspectionRenderCoordinator coordinator =
                GetCoordinator(page);
            ModelInspectionRenderKey visibleKey =
                retainedPresentation.RenderKey;
            bool cancellationObserved = false;
            bool retiredBeforeCallback = false;
            int reentrantRetirementCount = 0;
            using CancellationTokenRegistration registration =
                call.CancellationToken.Register(() =>
                {
                    cancellationObserved = true;
                    InvokeNavigation(
                        page,
                        "OnNavigatedFrom",
                        parameter: null);
                    reentrantRetirementCount++;
                    retiredBeforeCallback =
                        !coordinator.IsCurrent(visibleKey) &&
                        driver.IsDisposed &&
                        settings.IsDisposed &&
                        page.ViewModel is null &&
                        page.CurrentInspectionTask is null;
                    call.Report(CreateProgress(
                        ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
                        completedStageCount: 4));
                    settings.RaiseChanged();
                    call.Complete(ModelInspectionExecutionResult.Cancelled(
                        cooperative: true));
                });

            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
            retired = true;
            await run.WaitAsync(TimeSpan.FromSeconds(10));
            dispatcher.RunAll();

            Assert.IsTrue(cancellationObserved);
            Assert.AreEqual(1, reentrantRetirementCount);
            Assert.IsTrue(
                retiredBeforeCallback,
                "Coordinator, animation, settings, and page ownership must be retired before service cancellation callbacks run.");
            Assert.AreSame(retainedPresentation, page.CurrentPresentation);
            Assert.AreEqual(retainedFooter, page.CurrentFooterStatus);
            Assert.AreEqual(
                retainedProgressAnnouncements,
                preview.ProgressAnnouncementCount);
            Assert.AreEqual(
                retainedOutcomeAnnouncements,
                preview.OutcomeAnnouncementCount);
            Assert.AreSame(
                retainedFocus,
                FocusManager.GetFocusedElement(page.XamlRoot));
            Assert.AreEqual(retainedMotionStarts, driver.TotalStartCount);
            Assert.AreEqual(0, dispatcher.PendingCount);
        }
        finally
        {
            if (!retired)
            {
                InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
            }

            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void RetiredMotionSettingsCallback_CannotClearNewLifetimePendingChange()
    {
        var service = new ControlledInspectionService();
        var oldDispatcher = new ManualRenderDispatcher();
        var newDispatcher = new ManualRenderDispatcher();
        var oldSettings = new RecordingMotionSettings(animationsEnabled: true);
        var newSettings = new RecordingMotionSettings(animationsEnabled: true);
        IModelInspectionRenderDispatcher[] dispatchers =
            [oldDispatcher, newDispatcher];
        IModelInspectionMotionSettings[] settings =
            [oldSettings, newSettings];
        int dispatcherIndex = 0;
        int settingsIndex = 0;
        var page = new ModelInspectionPage(
            service,
            () => dispatchers[dispatcherIndex++],
            () => new RecordingPageAnimationDriver(),
            () => settings[settingsIndex++],
            () => new ImmediateMilestoneScheduler());

        InvokeNavigation(page, "OnNavigatedTo", CreateRequest());
        oldSettings.RaiseChanged();
        InvokeNavigation(
            page,
            "OnNavigatedTo",
            CreateRequest(@"C:\Models\second-lifetime.gguf"));
        newSettings.RaiseChanged();

        Assert.IsTrue(GetMotionSettingsChangePending(page));
        oldDispatcher.RunAll();
        Assert.IsTrue(
            GetMotionSettingsChangePending(page),
            "A stale retired-lifetime callback must not clear the current pending change.");

        newDispatcher.RunAll();
        Assert.IsFalse(GetMotionSettingsChangePending(page));
        InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task PausedRetiredSettingsHandler_CannotPoisonNewLifetimeMotion()
    {
        var service = new ControlledInspectionService();
        // This test isolates motion ownership, not successful progress draining.
        service.QueueCall(CreateFailureResult("motion-lifetime"));
        var oldDispatcher = new ManualRenderDispatcher();
        var newDispatcher = new ManualRenderDispatcher();
        var oldDriver = new RecordingPageAnimationDriver();
        var newDriver = new RecordingPageAnimationDriver();
        var oldSettings = new RecordingMotionSettings(animationsEnabled: true);
        var newSettings = new RecordingMotionSettings(animationsEnabled: true);
        IModelInspectionRenderDispatcher[] dispatchers =
            [oldDispatcher, newDispatcher];
        IModelInspectionAnimationDriver[] drivers = [oldDriver, newDriver];
        IModelInspectionMotionSettings[] settings =
            [oldSettings, newSettings];
        int dispatcherIndex = 0;
        int driverIndex = 0;
        int settingsIndex = 0;
        var page = new ModelInspectionPage(
            service,
            () => dispatchers[dispatcherIndex++],
            () => drivers[driverIndex++],
            () => settings[settingsIndex++],
            () => new ImmediateMilestoneScheduler());
        using var validated = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();

        try
        {
            InvokeNavigation(page, "OnNavigatedTo", CreateRequest());
            page.MotionSettingsChangeValidatedForTesting = () =>
            {
                validated.Set();
                if (!resume.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException(
                        "The paused settings handler was not resumed.");
                }
            };

            Task staleChange = Task.Run(oldSettings.RaiseChanged);
            Assert.IsTrue(
                validated.Wait(TimeSpan.FromSeconds(10)),
                "The retired handler did not reach the deterministic pause.");

            InvokeNavigation(
                page,
                "OnNavigatedTo",
                CreateRequest(@"C:\Models\replacement-lifetime.gguf"));
            page.MotionSettingsChangeValidatedForTesting = null;
            resume.Set();
            await staleChange.WaitAsync(TimeSpan.FromSeconds(10));
            oldDispatcher.RunAll();

            Assert.IsFalse(
                GetMotionSettingsChangePending(page),
                "A retired off-thread handler must not poison the new lifetime.");

            Task run = page.StartInspectionIfReadyAsync()!;
            newDispatcher.RunAll();
            await run.WaitAsync(TimeSpan.FromSeconds(10));
            newDispatcher.RunAll();
            Assert.HasCount(
                1,
                newDriver.TerminalStarts,
                "The new lifetime must retain motion after the stale handler drains.");
        }
        finally
        {
            resume.Set();
            page.MotionSettingsChangeValidatedForTesting = null;
            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OlderSameLifetimeSettingsHandler_CannotReassertPending()
    {
        var service = new ControlledInspectionService();
        // Any terminal exercises this settings race; success needs progress evidence.
        service.QueueCall(CreateFailureResult("motion-settings-revision"));
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingPageAnimationDriver();
        var settings = new RecordingMotionSettings(animationsEnabled: true);
        var page = CreateInjectedPage(
            service,
            CreateRequest(),
            dispatcher,
            driver,
            settings);
        using var firstRevisionClaimed = new ManualResetEventSlim();
        using var resumeFirstHandler = new ManualResetEventSlim();

        try
        {
            page.MotionSettingsChangeRevisionClaimedForTesting = revision =>
            {
                if (revision != 1)
                {
                    return;
                }

                firstRevisionClaimed.Set();
                if (!resumeFirstHandler.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException(
                        "The first settings handler was not resumed.");
                }
            };

            Task firstChange = Task.Run(settings.RaiseChanged);
            Assert.IsTrue(
                firstRevisionClaimed.Wait(TimeSpan.FromSeconds(10)),
                "The first handler did not pause after claiming revision one.");

            settings.RaiseChanged();
            dispatcher.RunAll();
            Assert.IsFalse(
                GetMotionSettingsChangePending(page),
                "The newer settings callback must complete revision two.");

            page.MotionSettingsChangeRevisionClaimedForTesting = null;
            resumeFirstHandler.Set();
            await firstChange.WaitAsync(TimeSpan.FromSeconds(10));
            dispatcher.RunAll();

            Assert.IsFalse(
                GetMotionSettingsChangePending(page),
                "A stale revision-one callback must not reassert pending work.");

            Task run = page.StartInspectionIfReadyAsync()!;
            dispatcher.RunAll();
            await AwaitRunAndRenderAsync(page, run);
            dispatcher.RunAll();
            Assert.HasCount(
                1,
                driver.TerminalStarts,
                "Motion must remain enabled after the stale callback drains.");
        }
        finally
        {
            resumeFirstHandler.Set();
            page.MotionSettingsChangeRevisionClaimedForTesting = null;
            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CoordinatorDelta_RetainsProgressRowsAndOneOutgoingTerminalLayer()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingPageAnimationDriver();
        var settings = new RecordingMotionSettings(animationsEnabled: true);
        var page = CreateInjectedPage(
            service,
            CreateRequest(),
            dispatcher,
            driver,
            settings);
        Task run = page.StartInspectionIfReadyAsync()!;
        dispatcher.RunAll();
        ModelInspectionPreviewProjection preview = Preview(page);
        InspectionContentCardPresentation progressPresentation =
            preview.ContentPresentation;
        InspectionProgressRows owner = progressPresentation.ProgressRows;
        object[] rowReferences = owner.Items.Cast<object>().ToArray();

        call.Report(CreateProgress(
            ModelInspectionStage.CheckModelPackage,
            completedStageCount: 0));
        dispatcher.RunAll();

        Assert.AreNotSame(progressPresentation, preview.ContentPresentation,
            "The first counted stage must retire the dedicated startup DTO.");
        progressPresentation = preview.ContentPresentation;
        CollectionAssert.AreEqual(
            rowReferences,
            owner.Items.Cast<object>().ToArray());
        Assert.AreEqual(InspectionContentStatus.Active, owner.Items[0].Status);
        Assert.AreEqual(
            "Checking\n0%",
            Assert.IsInstanceOfType<TextBlock>(
                preview.FindElement("InspectionStage1Status")).Text);

        call.Complete(CreateFailureResult("terminal-retained"));
        await AwaitRunAndRenderAsync(page, run);
        dispatcher.RunAll();

        Assert.AreEqual(InspectionContentCardMode.OperationalFailure,
            preview.ContentPresentation.Mode);
        Assert.AreEqual(
            Visibility.Visible,
            Assert.IsInstanceOfType<FrameworkElement>(
                preview.FindElement("InspectionFailurePanel")).Visibility);
        Assert.AreEqual(1, driver.TerminalStarts.Count);
        Assert.AreNotSame(
            driver.TerminalStarts[0].Outgoing,
            driver.TerminalStarts[0].Incoming,
            "Terminal motion must animate between distinct visual surfaces.");
        Assert.AreEqual(
            "InspectionProgressPanel",
            ((FrameworkElement)driver.TerminalStarts[0].Outgoing).Name);
        Assert.AreEqual(
            "InspectionFailurePanel",
            ((FrameworkElement)driver.TerminalStarts[0].Incoming).Name);

        driver.CompleteLastTerminal();
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ExactGgufTerminalPanelsProjectTheSuppliedOutcomeMessage()
    {
        ModelInspectionPreviewProjection preview =
            new ModelInspectionGgufPreviewView().PreviewProjection;
        await using var host = await WinUiRenderHost.ShowAsync(preview.Element, 1000, 700);

        preview.ApplyOutcome(new InspectionOutcomePresentation
        {
            Kind = InspectionOutcomePresentationKind.Ready,
            Tone = InspectionOutcomeTone.Success,
            Title = "Ready title",
            Message = "Measured ready detail"
        });
        Assert.AreEqual(
            "Measured ready detail",
            AdjacentOutcomeDetail(preview, "InspectionReadyHeading").Text);

        preview.ApplyContent(new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Warnings
        });
        preview.ApplyOutcome(new InspectionOutcomePresentation
        {
            Kind = InspectionOutcomePresentationKind.ReadyWithWarnings,
            Tone = InspectionOutcomeTone.Warning,
            Title = "Warning title",
            Message = "Measured warning detail"
        });
        Assert.AreEqual(
            "Measured warning detail",
            AdjacentOutcomeDetail(preview, "InspectionWarningHeading").Text);

        preview.ApplyContent(new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Invalid
        });
        preview.ApplyOutcome(new InspectionOutcomePresentation
        {
            Kind = InspectionOutcomePresentationKind.Invalid,
            Tone = InspectionOutcomeTone.Error,
            Title = "Failure title",
            Message = "Measured failure detail"
        });
        Assert.AreEqual(
            "Measured failure detail",
            AdjacentOutcomeDetail(preview, "InspectionFailureHeading").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task TerminalCrossfade_DisclosureRevisionStillRetiresOutgoingLayer()
    {
        var fixture = await CreateLoadedTerminalPageAsync(ModelInspectionOutcome.ReadyWithWarnings);
        try
        {
            var preview = Preview(fixture.Page);
            var disclosure = Assert.IsInstanceOfType<Expander>(preview.ActiveDisclosure);
            var terminal = fixture.Page.CurrentPresentation!;
            FrameworkElement panel = preview.OutcomeSurface;
            Assert.AreEqual(Visibility.Collapsed, preview.ContentSurface.Visibility);
            Assert.AreEqual(Visibility.Visible, panel.Visibility);
            Assert.HasCount(1, fixture.Driver.TerminalStarts);
            Assert.AreSame(preview.ContentSurface, fixture.Driver.TerminalStarts[0].Outgoing);
            Assert.AreSame(panel, fixture.Driver.TerminalStarts[0].Incoming);
            disclosure.IsExpanded = true;
            await DrainDispatcherAsync(fixture.Page);
            fixture.Dispatcher.RunAll();
            Assert.AreEqual(ModelInspectionFigmaState.ReadyWithWarningsExpanded,
                fixture.Page.CurrentPresentation!.State);
            Assert.AreEqual(terminal.RenderKey, fixture.Page.CurrentPresentation.RenderKey);
            Assert.HasCount(0, fixture.Driver.DisclosureStarts);
            fixture.Driver.CompleteTerminal(0);
            Assert.IsTrue(disclosure.IsExpanded);
            Assert.AreSame(panel, preview.OutcomeSurface);
            Assert.AreEqual(Visibility.Visible, panel.Visibility);
            Assert.AreEqual(Visibility.Collapsed, preview.ContentSurface.Visibility);
            disclosure.IsExpanded = false;
            await DrainDispatcherAsync(fixture.Page);
            fixture.Dispatcher.RunAll();
            Assert.AreEqual(ModelInspectionFigmaState.ReadyWithWarningsCollapsed,
                fixture.Page.CurrentPresentation!.State);
        }
        finally
        {
            RetireLoadedPage(fixture.Page, fixture.Window);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [TestCategory("DeferredInspectionPresentation")]
    public async Task TerminalOutcome_RetiresProgressLiveRegionAndStaleAnnouncement()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var dispatcher = new ManualRenderDispatcher();
        var page = CreateInjectedPage(service, CreateRequest(), dispatcher,
            new RecordingPageAnimationDriver(),
            new RecordingMotionSettings(animationsEnabled: false));
        try
        {
            Task run = page.StartInspectionIfReadyAsync()!;
            dispatcher.RunAll();
            var preview = Preview(page);
            call.Report(CreateProgress(ModelInspectionStage.ReadModelConfiguration, 1));
            dispatcher.RunAll();
            await DrainDispatcherAsync(page);
            Assert.AreEqual(Visibility.Visible, preview.ContentSurface.Visibility);
            call.Complete(CreateFailureResult("live-region-retirement"));
            await AwaitRunAndRenderAsync(page, run);
            dispatcher.RunAll();
            await DrainDispatcherAsync(page);
            Assert.AreEqual(Visibility.Collapsed, preview.ContentSurface.Visibility);
            Assert.AreEqual(Visibility.Visible, preview.OutcomeSurface.Visibility);
            Assert.AreEqual(1, preview.OutcomeAnnouncementCount);
            int notifications = preview.ProgressAnnouncementCount;
            string terminalName = AutomationProperties.GetName(preview.ContentSurface);
            preview.AnnounceProgress("Stale progress must not be announced.");
            Assert.AreEqual(notifications, preview.ProgressAnnouncementCount,
                "A retired progress region must reject stale announcements.");
            Assert.AreEqual(terminalName, AutomationProperties.GetName(preview.ContentSurface));
        }
        finally
        {
            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task StaleTerminalCompletion_CannotRetireNewAttemptOverlay()
    {
        var service = new ControlledInspectionService();
        service.QueueCall(CreateFailureResult("first-terminal"));
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingPageAnimationDriver();
        var page = CreateInjectedPage(
            service,
            CreateRequest(),
            dispatcher,
            driver,
            new RecordingMotionSettings(animationsEnabled: true));

        Task firstRun = page.StartInspectionIfReadyAsync()!;
        dispatcher.RunAll();
        await firstRun;
        dispatcher.RunAll();
        Assert.HasCount(1, driver.TerminalStarts);

        ControlledCall retry = service.QueueCall();
        page.ViewModel!.RetryCommand.Execute(null);
        await Task.Yield();
        dispatcher.RunAll();
        retry.Complete(CreateFailureResult("replacement-terminal"));
        await DrainDispatcherAsync(page);
        dispatcher.RunAll();

        var preview = Preview(page);
        FrameworkElement outgoing = preview.ContentSurface;
        Assert.HasCount(2, driver.TerminalStarts);
        Assert.AreEqual(Visibility.Collapsed, outgoing.Visibility);
        Assert.AreEqual(Visibility.Visible, preview.OutcomeSurface.Visibility);
        ModelInspectionPagePresentation replacement = page.CurrentPresentation!;

        driver.CompleteTerminal(index: 0);

        Assert.AreEqual(Visibility.Collapsed, outgoing.Visibility);
        Assert.AreEqual(Visibility.Visible, preview.OutcomeSurface.Visibility);
        Assert.AreSame(replacement, page.CurrentPresentation);

        driver.CompleteTerminal(index: 1);
        Assert.AreEqual(Visibility.Collapsed, outgoing.Visibility);
        Assert.AreSame(replacement, page.CurrentPresentation);
        Assert.AreEqual(Visibility.Visible, preview.OutcomeSurface.Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RetiredNavigationTerminalCompletion_CannotRetireCurrentOverlay()
    {
        var service = new ControlledInspectionService();
        service.QueueCall(CreateFailureResult("retired-navigation"));
        service.QueueCall(CreateFailureResult("current-navigation"));
        var oldDispatcher = new ManualRenderDispatcher();
        var currentDispatcher = new ManualRenderDispatcher();
        var oldDriver = new RecordingPageAnimationDriver();
        var currentDriver = new RecordingPageAnimationDriver();
        var dispatchers = new Queue<ManualRenderDispatcher>(
            [oldDispatcher, currentDispatcher]);
        var drivers = new Queue<RecordingPageAnimationDriver>(
            [oldDriver, currentDriver]);
        var page = new ModelInspectionPage(
            service,
            () => dispatchers.Dequeue(),
            () => drivers.Dequeue(),
            () => new RecordingMotionSettings(animationsEnabled: true),
            () => new ImmediateMilestoneScheduler());

        InvokeNavigation(page, "OnNavigatedTo", CreateRequest());
        Task retiredRun = page.StartInspectionIfReadyAsync()!;
        oldDispatcher.RunAll();
        await retiredRun;
        oldDispatcher.RunAll();
        Assert.HasCount(1, oldDriver.TerminalStarts);

        InvokeNavigation(
            page,
            "OnNavigatedTo",
            CreateRequest(@"C:\Models\replacement-terminal.gguf"));
        Task currentRun = page.StartInspectionIfReadyAsync()!;
        currentDispatcher.RunAll();
        await currentRun;
        currentDispatcher.RunAll();
        Assert.HasCount(1, currentDriver.TerminalStarts);
        var preview = Preview(page);
        FrameworkElement outgoing = preview.ContentSurface;
        Assert.AreEqual(Visibility.Collapsed, outgoing.Visibility);
        Assert.AreEqual(Visibility.Visible, preview.OutcomeSurface.Visibility);
        ModelInspectionPagePresentation replacement = page.CurrentPresentation!;

        oldDriver.CompleteTerminal(index: 0);

        Assert.AreEqual(Visibility.Collapsed, outgoing.Visibility);
        Assert.AreEqual(Visibility.Visible, preview.OutcomeSurface.Visibility);
        Assert.AreSame(replacement, page.CurrentPresentation);

        currentDriver.CompleteTerminal(index: 0);
        Assert.AreEqual(Visibility.Collapsed, outgoing.Visibility);
        Assert.AreSame(replacement, page.CurrentPresentation);
        Assert.AreEqual(Visibility.Visible, preview.OutcomeSurface.Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ReducedMotion_AppliesSameTerminalStateWithoutDriverStarts()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingPageAnimationDriver();
        var page = CreateInjectedPage(service, CreateRequest(), dispatcher, driver,
            new RecordingMotionSettings(animationsEnabled: false));
        try
        {
            Task run = page.StartInspectionIfReadyAsync()!;
            dispatcher.RunAll();
            var preview = Preview(page);
            Assert.AreEqual(Visibility.Visible, preview.ContentSurface.Visibility);
            call.Complete(CreateFailureResult("reduced"));
            await AwaitRunAndRenderAsync(page, run);
            dispatcher.RunAll();
            Assert.AreEqual(0, driver.TotalStartCount);
            Assert.AreEqual(InspectionOutcomePresentationKind.OperationalFailure,
                page.CurrentPresentation!.OutcomeCard.Kind);
            Assert.AreEqual(Visibility.Collapsed, preview.ContentSurface.Visibility);
            Assert.AreEqual(Visibility.Visible, preview.OutcomeSurface.Visibility);
            foreach (ProgressRing ring in PreviewDescendants<ProgressRing>(preview.Element))
                Assert.IsFalse(ring.IsActive, "Terminal presentation must retire progress motion.");
        }
        finally
        {
            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [TestCategory("DeferredInspectionPresentation")]
    public async Task FailureFocus_MovesOnlyWhenFocusedElementBecomesIneffective()
    {
        var service = new ControlledInspectionService();
        ControlledCall first = service.QueueCall();
        var dispatcher = new ManualRenderDispatcher();
        var page = CreateInjectedPage(
            service,
            CreateRequest(),
            dispatcher,
            new RecordingPageAnimationDriver(),
            new RecordingMotionSettings(animationsEnabled: false));
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var outsidePageFocus = new Button
        {
            Content = "Outside page focus",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Visibility = Visibility.Collapsed
        };
        var root = new Grid();
        root.Children.Add(page);
        root.Children.Add(outsidePageFocus);
        var window = new Window { Content = root };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            dispatcher.RunAll();
            var preview = Preview(page);
            var progressCard = preview.CancelActionButton;
            progressCard.IsTabStop = true;
            Assert.IsTrue(progressCard.Focus(FocusState.Programmatic));

            first.Complete(CreateFailureResult("focused-progress"));
            await page.CurrentInspectionTask!.WaitAsync(TimeSpan.FromSeconds(10));
            dispatcher.RunAll();
            var outcome = preview.OutcomeCard;
            Assert.AreSame(
                outcome.FocusTarget,
                FocusManager.GetFocusedElement(page.XamlRoot));
            Assert.IsFalse(outcome.FocusTarget.IsTabStop);

            service.QueueCall(CreateFailureResult("stable-focus"));
            var pageScroll = (ScrollViewer)page.FindName(
                "InspectionScrollViewer");
            pageScroll.IsTabStop = true;
            Assert.IsTrue(pageScroll.Focus(FocusState.Programmatic));
            page.ViewModel!.RetryCommand.Execute(null);
            dispatcher.RunAll();

            Assert.AreSame(
                pageScroll,
                FocusManager.GetFocusedElement(page.XamlRoot));

            ControlledCall focusRecoveryCall = service.QueueCall();
            var actions = preview.ActionCard;
            var retry = (Button)actions.FindName("PrimaryActionButton");
            Assert.AreEqual(Visibility.Visible, retry.Visibility);
            Assert.IsTrue(retry.IsEnabled);
            Assert.IsTrue(retry.Focus(FocusState.Programmatic));

            page.ViewModel.RetryCommand.Execute(null);
            dispatcher.RunNext();

            var pageHeading = (FrameworkElement)preview.FindElement("CompactModelName")!;
            object? focusedAfterRetry = FocusManager.GetFocusedElement(
                page.XamlRoot);
            bool collapsedRetryRecoveredToHeading = ReferenceEquals(
                pageHeading,
                focusedAfterRetry);
            Assert.AreEqual(Visibility.Collapsed, retry.Visibility);
            AssertStartupPresentation(
                AssertCompleteSnapshotApplied(page).ContentCard);

            dispatcher.RunNext();
            Assert.AreEqual(3, service.CallCount);

            var cancel = (Button)actions.FindName("CancelActionButton");
            Assert.AreEqual(Visibility.Visible, cancel.Visibility);
            Assert.IsTrue(cancel.IsEnabled);
            Assert.IsGreaterThanOrEqualTo(44d, cancel.MinHeight);

            Assert.IsTrue(pageScroll.Focus(FocusState.Programmatic));
            page.ViewModel.CancelCommand.Execute(null);
            Assert.IsTrue(
                focusRecoveryCall.CancellationToken.IsCancellationRequested);
            Assert.IsFalse(cancel.IsEnabled);
            dispatcher.RunNext();
            bool disabledCancelPreservedUnrelatedPageFocus = ReferenceEquals(
                pageScroll,
                FocusManager.GetFocusedElement(page.XamlRoot));
            focusRecoveryCall.Complete(
                ModelInspectionExecutionResult.Cancelled(cooperative: true));
            await DrainDispatcherAsync(page);
            dispatcher.RunAll();

            ControlledCall ownedCancelFocusRecoveryCall = service.QueueCall();
            Assert.AreEqual(Visibility.Visible, retry.Visibility);
            Assert.IsTrue(retry.IsEnabled);
            page.ViewModel.RetryCommand.Execute(null);
            dispatcher.RunNext();
            AssertStartupPresentation(
                AssertCompleteSnapshotApplied(page).ContentCard);
            dispatcher.RunNext();
            Assert.AreEqual(4, service.CallCount);
            Assert.AreEqual(Visibility.Visible, cancel.Visibility);
            Assert.IsTrue(cancel.IsEnabled);
            Assert.IsTrue(cancel.Focus(FocusState.Programmatic));
            page.ViewModel.CancelCommand.Execute(null);
            Assert.IsTrue(
                ownedCancelFocusRecoveryCall.CancellationToken
                    .IsCancellationRequested);
            Assert.IsFalse(cancel.IsEnabled);
            outsidePageFocus.Visibility = Visibility.Visible;
            Assert.IsTrue(outsidePageFocus.Focus(FocusState.Programmatic));
            Assert.AreSame(
                outsidePageFocus,
                FocusManager.GetFocusedElement(page.XamlRoot));
            dispatcher.RunNext();

            bool disabledCancelRecoveredToHeading = ReferenceEquals(
                pageHeading,
                FocusManager.GetFocusedElement(page.XamlRoot));
            AssertStartupPresentation(
                AssertCompleteSnapshotApplied(page).ContentCard);
            Assert.AreEqual(Visibility.Visible, cancel.Visibility);
            Assert.IsFalse(cancel.IsEnabled);
            Assert.AreEqual(
                "Stopping model inspection",
                AutomationProperties.GetName(cancel));
            ownedCancelFocusRecoveryCall.Report(CreateProgress(
                ModelInspectionStage.CheckModelPackage,
                completedStageCount: 0));
            dispatcher.RunAll();
            ModelInspectionPagePresentation cancelledProgress =
                AssertCompleteSnapshotApplied(page);
            Assert.AreEqual(
                Visibility.Collapsed,
                cancelledProgress.ContentCard.Startup.Visibility);
            bool disabledCancelPreservedEffectiveHeading = ReferenceEquals(
                pageHeading,
                FocusManager.GetFocusedElement(page.XamlRoot));
            ownedCancelFocusRecoveryCall.Complete(
                ModelInspectionExecutionResult.Cancelled(cooperative: true));
            await page.CurrentInspectionTask!.WaitAsync(
                TimeSpan.FromSeconds(10));
            await DrainDispatcherAsync(page);
            dispatcher.RunAll();

            Assert.IsTrue(
                collapsedRetryRecoveredToHeading,
                $"Startup must recover focus from the collapsed retry action to the page heading. Actual: {focusedAfterRetry?.GetType().Name ?? "null"}.");
            Assert.IsTrue(
                disabledCancelPreservedUnrelatedPageFocus,
                "Cancellation must preserve unrelated effective in-page focus when Cancel did not own focus.");
            Assert.IsTrue(
                disabledCancelRecoveredToHeading,
                "Startup must recover focus from the disabled cancel action to the page heading.");
            Assert.IsTrue(
                disabledCancelPreservedEffectiveHeading,
                "A later disabled-cancel progress delta must preserve the effective page heading focus.");
            Assert.IsFalse(page.ViewModel.IsRunActive);
            Assert.AreEqual(
                ModelInspectionExecutionStatus.Cancelled,
                page.ViewModel.Result?.Status);
            Assert.AreEqual(0, dispatcher.PendingCount);
        }
        finally
        {
            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OutcomeAnnouncements_DedupePerAttemptAndRepeatForRetryGeneration()
    {
        var service = new ControlledInspectionService();
        service.QueueCall(CreateFailureResult("first-announcement"));
        service.QueueCall(CreateFailureResult("second-announcement"));
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingPageAnimationDriver();
        var page = CreateInjectedPage(
            service,
            CreateRequest(),
            dispatcher,
            driver,
            new RecordingMotionSettings(animationsEnabled: false));

        Task firstRun = page.StartInspectionIfReadyAsync()!;
        dispatcher.RunAll();
        await firstRun;
        dispatcher.RunAll();
        ModelInspectionPreviewProjection preview = Preview(page);
        FrameworkElement outcome = preview.OutcomeSurface;
        Assert.AreEqual(1, preview.OutcomeAnnouncementCount);

        outcome.Visibility = Visibility.Collapsed;
        outcome.Visibility = Visibility.Visible;
        Assert.AreEqual(1, preview.OutcomeAnnouncementCount);

        page.ViewModel!.RetryCommand.Execute(null);
        await Task.Yield();
        dispatcher.RunAll();

        Assert.AreEqual(2, preview.OutcomeAnnouncementCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ReadyDisclosure_RetainsRowsAndScrollAcrossPageOwnedRevisions()
    {
        var fixture = await CreateLoadedTerminalPageAsync(ModelInspectionOutcome.Ready);
        try
        {
            var preview = Preview(fixture.Page);
            var disclosure = Assert.IsInstanceOfType<Expander>(preview.ActiveDisclosure);
            var evidence = Assert.IsInstanceOfType<StackPanel>(disclosure.Content);
            var rows = evidence.Children.ToArray();
            Assert.HasCount(5, rows);
            async Task SetExpandedAsync(bool expanded)
            {
                disclosure.IsExpanded = expanded;
                await DrainDispatcherAsync(fixture.Page);
                fixture.Dispatcher.RunAll();
                fixture.Page.UpdateLayout();
                Assert.AreEqual(expanded, disclosure.IsExpanded);
                Assert.AreEqual(expanded ? ModelInspectionFigmaState.ReadyExpanded :
                    ModelInspectionFigmaState.ReadyCollapsed, fixture.Page.CurrentPresentation!.State);
            }
            await SetExpandedAsync(true);
            var scroll = Assert.IsInstanceOfType<ScrollViewer>(
                fixture.Page.FindName("InspectionScrollViewer"));
            scroll.Height = 240d;
            fixture.Page.UpdateLayout();
            double offset = await SetNonzeroScrollOffsetAsync(fixture.Page, scroll);
            await SetExpandedAsync(false);
            await SetExpandedAsync(true);
            Assert.AreSame(evidence, disclosure.Content);
            CollectionAssert.AreEqual(rows, evidence.Children.ToArray());
            Assert.AreEqual(offset, scroll.VerticalOffset, 1d);

            var retiredKey = fixture.Page.CurrentPresentation!.RenderKey;
            int announcements = preview.OutcomeAnnouncementCount;
            var retry = fixture.Service.QueueCall();
            fixture.Page.ViewModel!.RetryCommand.Execute(null);
            fixture.Dispatcher.RunAll();
            var progress = fixture.Page.CurrentPresentation!;
            Assert.AreEqual(ModelInspectionFigmaState.InspectionProgress, progress.State);
            Assert.IsNull(preview.ActiveDisclosure);
            fixture.InitialCall.Report(CreateProgress(
                ModelInspectionStage.ConfirmCoreRuntimeCompatibility, completedStageCount: 4));
            GetCoordinator(fixture.Page).RequestRender(new ModelInspectionViewSnapshot(
                retiredKey, isRunActive: false, isCancellationRequested: false, progress: null,
                terminalResult: ModelInspectionExecutionResult.Completed(CreateReadyResult())));
            fixture.Dispatcher.RunAll();
            Assert.AreSame(progress, fixture.Page.CurrentPresentation);
            Assert.AreEqual(announcements, preview.OutcomeAnnouncementCount);

            retry.Complete(CreateCompletedResult(ModelInspectionOutcome.Ready));
            DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
            while (fixture.Page.CurrentPresentation!.State == ModelInspectionFigmaState.InspectionProgress
                && DateTimeOffset.UtcNow < deadline)
            {
                fixture.Dispatcher.RunAll();
                await Task.Delay(1);
            }
            Assert.AreEqual(ModelInspectionFigmaState.ReadyCollapsed,
                fixture.Page.CurrentPresentation!.State);
            Assert.AreEqual(announcements + 1, preview.OutcomeAnnouncementCount);
            Assert.IsGreaterThan(retiredKey.AttemptGeneration,
                fixture.Page.CurrentPresentation.RenderKey.AttemptGeneration);
            Assert.AreSame(disclosure, preview.ActiveDisclosure,
                "The native evidence template is reused across attempts.");
            await SetExpandedAsync(true);
            Assert.AreSame(evidence, disclosure.Content);
            CollectionAssert.AreEqual(rows, evidence.Children.ToArray());
        }
        finally
        {
            RetireLoadedPage(fixture.Page, fixture.Window);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SameOutcomeRevision_PreservesExpandedDisclosureAndFocusedElement()
    {
        var fixture = await CreateLoadedTerminalPageAsync(ModelInspectionOutcome.Ready);
        try
        {
            var preview = Preview(fixture.Page);
            var disclosure = Assert.IsInstanceOfType<Expander>(preview.ActiveDisclosure);
            Assert.IsTrue(disclosure.Focus(FocusState.Keyboard));
            object focused = FocusManager.GetFocusedElement(fixture.Page.XamlRoot);
            var expandCollapse = Assert.IsInstanceOfType<IExpandCollapseProvider>(
                new ExpanderAutomationPeer(disclosure).GetPattern(PatternInterface.ExpandCollapse));
            expandCollapse.Expand();
            await DrainDispatcherAsync(fixture.Page);
            fixture.Dispatcher.RunAll();
            fixture.Page.UpdateLayout();
            Assert.IsTrue(disclosure.IsExpanded);
            Assert.AreSame(focused, FocusManager.GetFocusedElement(fixture.Page.XamlRoot));
            Assert.HasCount(0, fixture.Driver.DisclosureStarts,
                "The native expander owns disclosure motion; the retired overlay driver is not used.");
            var evidence = disclosure.Content;
            var currentKey = fixture.Page.CurrentPresentation!.RenderKey;
            GetCoordinator(fixture.Page).RequestRender(new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(currentKey.AttemptGeneration,
                    currentKey.PresentationRevision + 1),
                isRunActive: false, isCancellationRequested: false, progress: null,
                terminalResult: ModelInspectionExecutionResult.Completed(
                    CreateReadyResult(TimeSpan.FromSeconds(5)))));
            fixture.Dispatcher.RunAll();
            Assert.AreEqual(ModelInspectionFigmaState.ReadyExpanded,
                fixture.Page.CurrentPresentation.State);
            Assert.AreSame(disclosure, preview.ActiveDisclosure);
            Assert.AreSame(evidence, disclosure.Content);
            Assert.IsTrue(disclosure.IsExpanded);
            Assert.AreSame(focused, FocusManager.GetFocusedElement(fixture.Page.XamlRoot));
        }
        finally
        {
            RetireLoadedPage(fixture.Page, fixture.Window);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [DataRow((int)ModelInspectionOutcome.ReadyWithWarnings)]
    [DataRow((int)ModelInspectionOutcome.ConversionRequired)]
    [DataRow((int)ModelInspectionOutcome.Invalid)]
    public async Task ContentDisclosure_RetainsRowsAndScrollAcrossPageOwnedRevisions(
        int outcomeValue)
    {
        ModelInspectionOutcome outcome = (ModelInspectionOutcome)outcomeValue;
        var fixture = await CreateLoadedTerminalPageAsync(outcome);
        try
        {
            var preview = Preview(fixture.Page);
            if (outcome == ModelInspectionOutcome.ReadyWithWarnings)
            {
                var disclosure = Assert.IsInstanceOfType<Expander>(preview.ActiveDisclosure);
                var evidence = Assert.IsInstanceOfType<Border>(disclosure.Content);
                Assert.AreSame(preview.FindElement("WarningEvidenceBody"), evidence);
                var warningLayout = Assert.IsInstanceOfType<Grid>(evidence.Child);
                var rows = warningLayout.Children.ToArray();
                var warningText = warningLayout.Children.OfType<StackPanel>().Single();
                Assert.HasCount(2, warningText.Children.OfType<TextBlock>().ToArray());
                disclosure.IsExpanded = true;
                await DrainDispatcherAsync(fixture.Page);
                fixture.Dispatcher.RunAll();
                Assert.AreEqual(ModelInspectionFigmaState.ReadyWithWarningsExpanded,
                    fixture.Page.CurrentPresentation!.State);
                var scroll = Assert.IsInstanceOfType<ScrollViewer>(
                    fixture.Page.FindName("InspectionScrollViewer"));
                scroll.Height = 240d;
                fixture.Page.UpdateLayout();
                await SetNonzeroScrollOffsetAsync(fixture.Page, scroll);
                disclosure.IsExpanded = false;
                await DrainDispatcherAsync(fixture.Page);
                fixture.Dispatcher.RunAll();
                disclosure.IsExpanded = true;
                await DrainDispatcherAsync(fixture.Page);
                fixture.Dispatcher.RunAll();
                Assert.AreSame(evidence, disclosure.Content);
                Assert.AreSame(warningLayout, evidence.Child);
                CollectionAssert.AreEqual(rows, warningLayout.Children.ToArray());
                Assert.IsTrue(double.IsFinite(scroll.VerticalOffset));
                Assert.IsTrue(scroll.VerticalOffset >= 0d &&
                    scroll.VerticalOffset <= scroll.ScrollableHeight + 1d);
            }
            else
            {
                Assert.IsNull(preview.ActiveDisclosure,
                    "Conversion and invalid native panels expose their report directly.");
                Assert.AreEqual(Visibility.Visible, preview.OutcomeSurface.Visibility);
            }

            ControlledCall retry = fixture.Service.QueueCall();
            fixture.Page.ViewModel!.RetryCommand.Execute(null);
            await Task.Yield();
            fixture.Dispatcher.RunAll();
            Assert.AreEqual(InspectionContentCardMode.Progress, preview.ContentPresentation.Mode);
            Assert.IsNull(preview.ActiveDisclosure, "Retry must retire the terminal disclosure.");
            ModelInspectionOutcome replacementOutcome = outcome == ModelInspectionOutcome.ReadyWithWarnings
                ? ModelInspectionOutcome.Invalid : ModelInspectionOutcome.ReadyWithWarnings;
            retry.Complete(CreateCompletedResult(replacementOutcome));
            DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
            while ((fixture.Page.ViewModel.IsRunActive ||
                    preview.ContentPresentation.Mode == InspectionContentCardMode.Progress) &&
                DateTimeOffset.UtcNow < deadline)
            {
                fixture.Dispatcher.RunAll();
                await Task.Delay(1);
            }
            Assert.IsFalse(fixture.Page.ViewModel.IsRunActive,
                "The retry must finish its ordered presentation drain.");
            fixture.Dispatcher.RunAll();
            fixture.Driver.CompleteLastTerminal();
            Assert.AreEqual(replacementOutcome, fixture.Page.ViewModel.Result!.Result!.Outcome);
            if (replacementOutcome == ModelInspectionOutcome.ReadyWithWarnings)
            {
                var replacement = Assert.IsInstanceOfType<Expander>(preview.ActiveDisclosure);
                Assert.IsFalse(replacement.IsExpanded,
                    "A replacement result must start with collapsed evidence.");
            }
            else
            {
                Assert.IsNull(preview.ActiveDisclosure);
            }
            Assert.AreEqual(Visibility.Collapsed, preview.ContentSurface.Visibility);
        }
        finally
        {
            RetireLoadedPage(fixture.Page, fixture.Window);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task DisclosureRapidReverse_UsesOneAbsoluteCoordinateSpace()
    {
        var fixture = await CreateLoadedTerminalPageAsync(ModelInspectionOutcome.Ready);
        try
        {
            var preview = Preview(fixture.Page);
            var disclosure = Assert.IsInstanceOfType<Expander>(preview.ActiveDisclosure);
            var scroll = Assert.IsInstanceOfType<ScrollViewer>(
                fixture.Page.FindName("InspectionScrollViewer"));
            scroll.Height = 240d;
            fixture.Page.UpdateLayout();
            await SetNonzeroScrollOffsetAsync(fixture.Page, scroll);
            var evidence = disclosure.Content;
            disclosure.IsExpanded = true;
            fixture.Dispatcher.RunAll();
            Assert.AreEqual(ModelInspectionFigmaState.ReadyExpanded,
                fixture.Page.CurrentPresentation!.State);
            disclosure.IsExpanded = false;
            await DrainDispatcherAsync(fixture.Page);
            fixture.Dispatcher.RunAll();
            Assert.IsFalse(disclosure.IsExpanded);
            Assert.AreEqual(ModelInspectionFigmaState.ReadyCollapsed,
                fixture.Page.CurrentPresentation!.State);
            Assert.HasCount(0, fixture.Driver.DisclosureStarts,
                "Native expanders no longer submit absolute-coordinate overlay animations.");
            Assert.AreSame(evidence, disclosure.Content);
            await DrainDispatcherAsync(fixture.Page);
            fixture.Dispatcher.RunAll();
            Assert.IsFalse(disclosure.IsExpanded,
                "Queued expansion work must not win after reversal.");
            Assert.IsTrue(double.IsFinite(scroll.VerticalOffset));
            Assert.IsTrue(scroll.VerticalOffset >= 0d &&
                scroll.VerticalOffset <= scroll.ScrollableHeight + 1d);
        }
        finally
        {
            RetireLoadedPage(fixture.Page, fixture.Window);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task MotionSettingChange_PreservesAcceptedTargetWithoutReplacementMotion()
    {
        var fixture = await CreateLoadedTerminalPageAsync(ModelInspectionOutcome.Ready);
        try
        {
            var preview = Preview(fixture.Page);
            var disclosure = Assert.IsInstanceOfType<Expander>(preview.ActiveDisclosure);
            // Exercise the page-owned policy source, not a detached legacy card.
            FieldInfo settingsField = typeof(ModelInspectionPage).GetField(
                "_motionSettings", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var settings = Assert.IsInstanceOfType<RecordingMotionSettings>(
                settingsField.GetValue(fixture.Page));
            bool stoppedBeforeFlush = false;
            fixture.Page.MotionSettingsChangeBeforeFlushForTesting = () =>
                stoppedBeforeFlush = PreviewDescendants<ProgressRing>(preview.Element)
                    .All(ring => !ring.IsActive);
            disclosure.IsExpanded = true;
            settings.SetAnimationsEnabled(false);
            fixture.Dispatcher.RunAll();
            Assert.IsTrue(stoppedBeforeFlush);
            Assert.HasCount(0, fixture.Driver.DisclosureStarts);
            Assert.IsTrue(disclosure.IsExpanded);
            Assert.AreEqual(ModelInspectionFigmaState.ReadyExpanded,
                fixture.Page.CurrentPresentation!.State);
            ModelInspectionPagePresentation accepted = fixture.Page.CurrentPresentation;
            fixture.Page.MotionSettingsChangeBeforeFlushForTesting = null;
            settings.SetAnimationsEnabled(true);
            fixture.Dispatcher.RunAll();
            Assert.IsTrue(disclosure.IsExpanded);
            Assert.AreEqual(accepted.RenderKey, fixture.Page.CurrentPresentation!.RenderKey);
            Assert.HasCount(0, fixture.Driver.DisclosureStarts);
            Assert.AreEqual(Visibility.Visible, preview.OutcomeSurface.Visibility);
        }
        finally
        {
            RetireLoadedPage(fixture.Page, fixture.Window);
        }
    }

    [UITestMethod]
    [DoNotParallelize]
    [TestCategory("WinUI")]
    [TestCategory("ModelInspectionPackagedIntegration")]
    public async Task PackagedN001_PageJourneyCompletesAllFiveStagesAsReady()
    {
        string applicationRoot = ModelInspectionWorkerComposition
            .ResolveApprovedApplicationRoot();
        string fixturePath = Path.Combine(
            applicationRoot,
            "TestFixtures",
            "GGUF",
            "N-001-vocab-only-spm.gguf");
        Assert.IsTrue(File.Exists(fixturePath));
        FileInfo fixture = new(fixturePath);
        fixture.Refresh();
        ModelInspectionRequest request = CreateRequest(
            fixturePath,
            fixture.Length,
            new DateTimeOffset(fixture.LastWriteTimeUtc, TimeSpan.Zero));
        var frame = new Frame();
        ModelInspectionPage? page = null;
        Window? window = null;


        try
        {
            page = CreateUnactivatedPage(
                ModelInspectionServiceComposition.CreateDefault());
            InvokeNavigation(page, "OnNavigatedTo", request);
            frame.Content = page;
            Assert.AreSame(request, page.Request);
            Assert.IsNotNull(page.ViewModel);
            List<(
                string Summary,
                int WaitingCount,
                int ActiveCount)> observedStartupPresentations = [];
            Visibility? observedStartupVisibility = null;
            List<string> observedSemanticOrder = [];
            ModelInspectionPreviewProjection observedPreview = Preview(page);
            List<(
                ModelInspectionStage Stage,
                ModelInspectionStageStatus Status,
                int CompletedStageCount)> observedTransitions = [];
            page.ViewModel.PropertyChanged += (_, eventArguments) =>
            {
                if (eventArguments.PropertyName !=
                        nameof(ModelInspectionViewModel.Snapshot) ||
                    page.ViewModel?.Snapshot.Progress is not
                        ModelInspectionProgress progress)
                {
                    return;
                }

                if (observedStartupPresentations.Count == 0)
                {
                    // The first worker progress callback follows the production
                    // startup barrier, before its queued page render.
                    InspectionContentCardPresentation candidate =
                        observedPreview.ContentPresentation;
                    observedStartupVisibility = candidate.Startup.Visibility;
                    observedStartupPresentations.Add((
                        candidate.Startup.Summary,
                        candidate.Items.Count(row => row.Status == InspectionContentStatus.Waiting),
                        candidate.Items.Count(row => row.IsActive)));
                    observedSemanticOrder.Add("Startup");
                }

                RecordCoreProgressObservation(
                    observedTransitions,
                    progress);
                if (progress.StageFraction is null)
                {
                    observedSemanticOrder.Add(
                        $"{progress.Stage}:{progress.StageStatus}");
                }
            };
            var loaded = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            page.Loaded += (_, _) => loaded.TrySetResult(true);
            window = new Window { Content = frame };

            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Task? run = page.CurrentInspectionTask;
            Assert.IsNotNull(run,
                "The production Loaded path must start the inspection.");
            page.InvalidateMeasure();
            page.UpdateLayout();
            Assert.AreSame(run, page.CurrentInspectionTask,
                "A later layout must not start another inspection.");

            await run.WaitAsync(TimeSpan.FromSeconds(30));
            await WaitForPresentationStateAsync(
                page,
                ModelInspectionFigmaState.ReadyCollapsed);

            Assert.HasCount(1, observedStartupPresentations);
            Assert.AreEqual(Visibility.Visible, observedStartupVisibility);
            Assert.AreEqual(
                "Starting secure inspection…",
                observedStartupPresentations[0].Summary);
            Assert.AreEqual(5, observedStartupPresentations[0].WaitingCount);
            Assert.AreEqual(0, observedStartupPresentations[0].ActiveCount);
            Assert.AreEqual("Startup", observedSemanticOrder[0]);

            Assert.IsNull(page.ViewModel.Progress,
                "Terminal snapshots retire mutable progress evidence.");
            var expectedTransitions = Enum.GetValues<ModelInspectionStage>()
                .SelectMany(stage => new[]
                {
                    (
                        stage,
                        ModelInspectionStageStatus.Active,
                        (int)stage - 1),
                    (
                        stage,
                        ModelInspectionStageStatus.Completed,
                        (int)stage)
                })
                .ToArray();
            CollectionAssert.AreEqual(
                expectedTransitions,
                observedTransitions.ToArray(),
                "Every stage must become Active and then Completed in order.");
            Assert.AreEqual(
                ModelInspectionExecutionStatus.Completed,
                page.ViewModel.Result?.Status);
            Assert.AreEqual(
                ModelInspectionOutcome.Ready,
                page.ViewModel.Result?.Result?.Outcome);
            Assert.IsFalse(page.ViewModel.CancelCommand.CanExecute(null));

            ModelInspectionPagePresentation terminal =
                AssertCompleteSnapshotApplied(page);
            Assert.AreEqual(
                InspectionOutcomePresentationKind.Ready,
                terminal.OutcomeCard.Kind);
            Assert.AreEqual(
                Visibility.Collapsed,
                terminal.ActionCard.CancelAction.Visibility);

            Expander? disclosure = Preview(page).ActiveDisclosure;
            Assert.IsNotNull(disclosure);
            disclosure.IsExpanded = true;
            await WaitForDisclosureStateAsync(
                page,
                disclosure,
                isExpanded: true);
            ModelInspectionPagePresentation expanded =
                AssertCompleteSnapshotApplied(page);
            Assert.AreEqual(
                ModelInspectionFigmaState.ReadyExpanded,
                expanded.State);
            string visibleTerminalText = FlattenVisibleText(expanded);
            StringAssert.DoesNotContain(
                visibleTerminalText,
                fixturePath,
                StringComparison.OrdinalIgnoreCase);
            StringAssert.DoesNotContain(
                visibleTerminalText,
                applicationRoot,
                StringComparison.OrdinalIgnoreCase);
            StringAssert.DoesNotContain(
                visibleTerminalText,
                "{% for message in messages %}{{ message['content'] }}{% endfor %}",
                StringComparison.Ordinal);

            disclosure.IsExpanded = false;
            await WaitForDisclosureStateAsync(
                page,
                disclosure,
                isExpanded: false);
            Assert.AreEqual(
                ModelInspectionFigmaState.ReadyCollapsed,
                page.CurrentPresentation?.State);
            Assert.HasCount(0, frame.BackStack);
            Assert.IsFalse(frame.CanGoBack);
        }
        finally
        {
            if (page is not null)
            {
                InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
            }

            if (window is not null)
            {
                window.Content = null;
                window.Close();
            }

            await AssertNoProcessesRemainAsync(
                "GraniteEdgeAI.ModelInspection.Worker",
                "GraniteEdgeAI.ModelInspection.ProtocolTestWorker");
        }
    }

    [TestMethod]
    [TestCategory("WinUI")]
    public void CoreProgressObserver_RecordsDuplicatesAndIgnoresFractionalNoise()
    {
        List<(
            ModelInspectionStage Stage,
            ModelInspectionStageStatus Status,
            int CompletedStageCount)> observedTransitions = [];
        var active = new ModelInspectionProgress(
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completedStageCount: 0,
            totalStageCount: 5,
            stageFraction: null,
            userMessage: "Checking package.");
        var fractionalActive = new ModelInspectionProgress(
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completedStageCount: 0,
            totalStageCount: 5,
            stageFraction: 0.5,
            userMessage: "Checking package.");
        var completed = new ModelInspectionProgress(
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Completed,
            completedStageCount: 1,
            totalStageCount: 5,
            stageFraction: null,
            userMessage: "Package checked.");
        var fractionalCompleted = new ModelInspectionProgress(
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Completed,
            completedStageCount: 1,
            totalStageCount: 5,
            stageFraction: 1d,
            userMessage: "Package checked.");

        RecordCoreProgressObservation(observedTransitions, active);
        RecordCoreProgressObservation(observedTransitions, active);
        RecordCoreProgressObservation(observedTransitions, fractionalActive);
        RecordCoreProgressObservation(observedTransitions, completed);
        RecordCoreProgressObservation(observedTransitions, completed);
        RecordCoreProgressObservation(observedTransitions, fractionalCompleted);

        CollectionAssert.AreEqual(
            new[]
            {
                (
                    ModelInspectionStage.CheckModelPackage,
                    ModelInspectionStageStatus.Active,
                    0),
                (
                    ModelInspectionStage.CheckModelPackage,
                    ModelInspectionStageStatus.Active,
                    0),
                (
                    ModelInspectionStage.CheckModelPackage,
                    ModelInspectionStageStatus.Completed,
                    1),
                (
                    ModelInspectionStage.CheckModelPackage,
                    ModelInspectionStageStatus.Completed,
                    1)
            },
            observedTransitions.ToArray(),
            "Duplicate fractionless stage boundaries must remain visible to the exact N-001 sequence assertion, while fractional noise is ignored.");

        Assert.IsFalse(
            observedTransitions.SequenceEqual(
                new[]
                {
                    (
                        ModelInspectionStage.CheckModelPackage,
                        ModelInspectionStageStatus.Active,
                        0),
                    (
                        ModelInspectionStage.CheckModelPackage,
                        ModelInspectionStageStatus.Completed,
                        1)
                }),
            "A duplicate fractionless boundary must make the exact one-Active/one-Completed sequence mismatch instead of being hidden.");
    }

    private static void RecordCoreProgressObservation(
        ICollection<(
            ModelInspectionStage Stage,
            ModelInspectionStageStatus Status,
            int CompletedStageCount)> observedTransitions,
        ModelInspectionProgress progress)
    {
        if (progress.StageFraction is not null)
        {
            return;
        }

        var transition = (
            progress.Stage,
            progress.StageStatus,
            progress.CompletedStageCount);
        observedTransitions.Add(transition);
    }

    private static ModelInspectionPage CreatePage(
        IModelInspectionService service,
        ModelInspectionRequest request)
    {
        ModelInspectionPage page = CreateUnactivatedPage(service);
        InvokeNavigation(page, "OnNavigatedTo", request);
        return page;
    }

    private static ModelInspectionPage CreateUnactivatedPage(
        IModelInspectionService service) => new(
        service,
        () => new DispatcherQueueModelInspectionRenderDispatcher(
            DispatcherQueue.GetForCurrentThread() ??
            throw new InvalidOperationException(
                "The packaged page test requires a UI DispatcherQueue.")),
        () => new WinUiModelInspectionAnimationDriver(
            ModelInspectionMotionSpec.Approved),
        () => new UiSettingsModelInspectionMotionSettings(),
        () => new ImmediateMilestoneScheduler());

    private static ModelInspectionPage CreateInjectedPage(
        IModelInspectionService service,
        ModelInspectionRequest request,
        ManualRenderDispatcher dispatcher,
        RecordingPageAnimationDriver driver,
        RecordingMotionSettings settings)
    {
        var page = new ModelInspectionPage(
            service,
            () => dispatcher,
            () => driver,
            () => settings,
            () => new ImmediateMilestoneScheduler());
        InvokeNavigation(page, "OnNavigatedTo", request);
        return page;
    }

    private static ModelInspectionPagePresentation AssertCompleteSnapshotApplied(
        ModelInspectionPage page)
    {
        ModelInspectionPagePresentation? snapshot = page.CurrentPresentation;
        Assert.IsNotNull(snapshot);
        ModelInspectionPreviewProjection preview = Preview(page);

        AssertModelPresentation(snapshot.ModelCard, preview.ModelPresentation);
        AssertContentPresentation(snapshot.ContentCard, preview.ContentPresentation);
        AssertOutcomePresentation(snapshot.OutcomeCard, preview.OutcomePresentation);
        AssertActionPresentation(snapshot.ActionCard, preview.ActionPresentation);
        return snapshot;
    }

    private static void AssertStartupPresentation(
        InspectionContentCardPresentation content)
    {
        InspectionStartupPresentation startup = content.Startup;
        Assert.AreEqual(Visibility.Visible, startup.Visibility);
        Assert.AreEqual("Starting secure inspection…", startup.Summary);
        Assert.AreEqual(
            "Model inspection is starting.",
            startup.AutomationName);
    }

    private static async Task AwaitRunAndRenderAsync(ModelInspectionPage page, Task run)
    {
        // Five animated completion stages take roughly ten seconds (1.8s per
        // stage plus frame dwell). Allow layout overhead without waiting forever.
        DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
        while (!run.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            if (typeof(ModelInspectionPage).GetField("_renderDispatcher",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)
                is ManualRenderDispatcher dispatcher)
            {
                dispatcher.RunAll();
            }
            await Task.Delay(1);
        }
        await run.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private static async Task DrainDispatcherAsync(FrameworkElement element)
    {
        var drained = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(element.DispatcherQueue.TryEnqueue(
            () => drained.TrySetResult(true)));
        await drained.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static async Task WaitForPresentationStateAsync(
        ModelInspectionPage page,
        ModelInspectionFigmaState expectedState)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow +
            TimeSpan.FromSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await DrainDispatcherAsync(page);
            page.UpdateLayout();
            if (page.CurrentPresentation?.State == expectedState)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        Assert.Fail(
            $"The page did not reach {expectedState} before timeout. Actual: " +
            $"{page.CurrentPresentation?.State.ToString() ?? "null"}.");
    }

    private static void AssertModelPresentation(
        InspectionModelCardPresentation expected,
        InspectionModelCardPresentation actual)
    {
        Assert.AreEqual(expected.DisplayMode, actual.DisplayMode);
        Assert.AreEqual(expected.BadgeState, actual.BadgeState);
        Assert.AreEqual(expected.ModelName, actual.ModelName);
        Assert.AreEqual(expected.CompactSummary, actual.CompactSummary);
        Assert.AreEqual(expected.FormatShortName, actual.FormatShortName);
        Assert.AreEqual(
            expected.InspectionDetailsVisibility,
            actual.InspectionDetailsVisibility);
        Assert.AreEqual(
            expected.IsInspectionDetailsExpanded,
            actual.IsInspectionDetailsExpanded);
    }

    private static void AssertContentPresentation(
        InspectionContentCardPresentation expected,
        InspectionContentCardPresentation actual)
    {
        Assert.AreEqual(expected.Mode, actual.Mode);
        Assert.AreEqual(expected.SectionTitle, actual.SectionTitle);
        Assert.AreEqual(expected.ProgressSummary, actual.ProgressSummary);
        Assert.AreEqual(
            expected.Startup.Visibility,
            actual.Startup.Visibility);
        Assert.AreEqual(expected.Startup.Summary, actual.Startup.Summary);
        Assert.AreEqual(
            expected.Startup.AutomationName,
            actual.Startup.AutomationName);
        Assert.AreEqual(expected.SupportingText, actual.SupportingText);
        Assert.AreEqual(
            expected.SupportingTextVisibility,
            actual.SupportingTextVisibility);
        Assert.AreEqual(expected.TertiaryText, actual.TertiaryText);
        Assert.AreEqual(
            expected.TertiaryTextVisibility,
            actual.TertiaryTextVisibility);
        Assert.AreEqual(expected.DiagnosticCode, actual.DiagnosticCode);
        Assert.AreEqual(
            expected.DiagnosticCodeVisibility,
            actual.DiagnosticCodeVisibility);
        Assert.AreEqual(expected.DisclosureSummary, actual.DisclosureSummary);
        Assert.AreEqual(
            expected.DisclosureVisibility,
            actual.DisclosureVisibility);
        Assert.AreEqual(expected.IsExpanded, actual.IsExpanded);
        if (expected.Mode == InspectionContentCardMode.Progress)
        {
            Assert.AreSame(expected.ProgressRows, actual.ProgressRows);
        }
    }

    private static void AssertOutcomePresentation(
        InspectionOutcomePresentation expected,
        InspectionOutcomePresentation actual)
    {
        Assert.AreEqual(expected.Kind, actual.Kind);
        Assert.AreEqual(expected.Tone, actual.Tone);
        Assert.AreEqual(expected.Title, actual.Title);
        Assert.AreEqual(expected.Message, actual.Message);
        Assert.AreEqual(expected.AutomationName, actual.AutomationName);
    }

    private static void AssertActionPresentation(
        InspectionActionCardPresentation expected,
        InspectionActionCardPresentation actual)
    {
        Assert.AreEqual(expected.Mode, actual.Mode);
        Assert.AreEqual(expected.Title, actual.Title);
        Assert.AreEqual(expected.Message, actual.Message);
        Assert.AreEqual(expected.AutomationName, actual.AutomationName);
        AssertActionSlot(expected.CancelAction, actual.CancelAction);
        AssertActionSlot(expected.SecondaryActionOne, actual.SecondaryActionOne);
        AssertActionSlot(expected.SecondaryActionTwo, actual.SecondaryActionTwo);
        AssertActionSlot(expected.PrimaryAction, actual.PrimaryAction);
    }

    private static void AssertActionSlot(
        InspectionActionPresentation expected,
        InspectionActionPresentation actual)
    {
        Assert.AreEqual(expected.Text, actual.Text);
        Assert.AreEqual(expected.Visibility, actual.Visibility);
        Assert.AreEqual(expected.IsEnabled, actual.IsEnabled);
        Assert.AreEqual(expected.AutomationName, actual.AutomationName);
        Assert.AreEqual(expected.AutomationHelpText, actual.AutomationHelpText);
        Assert.AreEqual(expected.CommandParameter, actual.CommandParameter);
        Assert.AreEqual(expected.MinimumWidth, actual.MinimumWidth);
        Assert.AreSame(expected.Command, actual.Command);
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

    private static bool GetMotionSettingsChangePending(
        ModelInspectionPage page) =>
        page.MotionSettingsChangePendingForTesting;

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

    private static ModelInspectionRequest CreateRequest(
        string modelPath = @"C:\Models\granite.gguf",
        long lengthBytes = 64,
        DateTimeOffset? lastWriteTimeUtc = null)
    {
        return new ModelInspectionRequest(
            modelPath,
            Path.GetFileName(modelPath),
            new ExpectedModelFileIdentity(
                lengthBytes,
                lastWriteTimeUtc ?? FixedUtc),
            ValidatedQuickScanSnapshot.CreateGguf(
                modelName: "Granite 4.1 3B Instruct",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantisation: "Q4_K_M",
                fileSizeBytes: lengthBytes,
                declaredContextLength: 131_072,
                ggufVersion: 3));
    }

    private static ModelInspectionProgress CreateProgress(
        ModelInspectionStage stage,
        int completedStageCount)
    {
        return new ModelInspectionProgress(
            stage,
            ModelInspectionStageStatus.Active,
            completedStageCount,
            totalStageCount: 5,
            stageFraction: null,
            userMessage: "Inspection is running.");
    }

    private static ModelInspectionExecutionResult CreateFailureResult(
        string discriminator)
    {
        return ModelInspectionExecutionResult.OperationalFailure(
            new ModelInspectionOperationalFailure(
                code: $"MI-OP-{discriminator}",
                userMessage: "Model inspection could not be completed.",
                technicalDetail: "Controlled test failure."));
    }

    private static ModelInspectionResult CreateReadyResult(
        TimeSpan? duration = null)
    {
        return new ModelInspectionResult(
            ModelInspectionOutcome.Ready,
            PresentationTestData.CreateEvidence(),
            Array.Empty<ModelInspectionFinding>(),
            summary: "All checks passed.",
            recommendedAction: "Continue.",
            verifiedConversionRouteId: null,
            startedAtUtc: FixedUtc,
            completedAtUtc: FixedUtc +
                (duration ?? TimeSpan.FromSeconds(2)));
    }

    private static ModelInspectionExecutionResult CreateCompletedResult(
        ModelInspectionOutcome outcome) =>
        ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(outcome));

    private static async Task<(
        ControlledInspectionService Service,
        ControlledCall InitialCall,
        ModelInspectionPage Page,
        ManualRenderDispatcher Dispatcher,
        RecordingPageAnimationDriver Driver,
        Window Window)> CreateLoadedTerminalPageAsync(
            ModelInspectionOutcome outcome)
    {
        var service = new ControlledInspectionService();
        ControlledCall initialCall = service.QueueCall(
            CreateCompletedResult(outcome));
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingPageAnimationDriver();
        var page = CreateInjectedPage(
            service,
            PresentationTestData.CreateRequest(),
            dispatcher,
            driver,
            new RecordingMotionSettings(animationsEnabled: true));
        Task run = page.StartInspectionIfReadyAsync()!;
        dispatcher.RunAll();
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = page };
        window.Activate();
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await AwaitRunAndRenderAsync(page, run);
        dispatcher.RunAll();
        driver.CompleteLastTerminal();
        await DrainDispatcherAsync(page);
        page.UpdateLayout();
        return (service, initialCall, page, dispatcher, driver, window);
    }

    private static ModelInspectionRenderCoordinator GetCoordinator(
        ModelInspectionPage page)
    {
        FieldInfo? field = typeof(ModelInspectionPage).GetField(
            "_coordinator",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        var coordinator = field.GetValue(page) as
            ModelInspectionRenderCoordinator;
        Assert.IsNotNull(coordinator);
        return coordinator;
    }

    private static async Task CompleteDisclosureRequestAsync(
        ModelInspectionPage page,
        InspectionDisclosure disclosure,
        ManualRenderDispatcher dispatcher,
        RecordingPageAnimationDriver driver,
        bool isExpanded)
    {
        Assert.IsNotNull(page);
        Assert.IsNotNull(disclosure);
        Assert.IsNotNull(dispatcher);
        Assert.IsNotNull(driver);
        int startIndex = driver.DisclosureStarts.Count;
        disclosure.RequestTargetState(isExpanded);
        dispatcher.RunAll();
        Assert.AreEqual(startIndex + 1, driver.DisclosureStarts.Count);
        page.UpdateLayout();
        driver.CompleteDisclosure(startIndex);
        await DrainDispatcherAsync(page);
        page.UpdateLayout();
        Assert.AreEqual(isExpanded, disclosure.IsExpanded);
    }

    private static object[] CaptureItemContainers(ItemsControl items)
    {
        items.UpdateLayout();
        var containers = new object[items.Items.Count];
        for (int index = 0; index < containers.Length; index++)
        {
            object? container = items.ContainerFromIndex(index);
            Assert.IsNotNull(container);
            containers[index] = container;
        }

        return containers;
    }

    private static async Task<double> SetNonzeroScrollOffsetAsync(
        FrameworkElement dispatcherOwner,
        ScrollViewer scroll)
    {
        scroll.UpdateLayout();
        Assert.IsGreaterThan(0d, scroll.ScrollableHeight);
        double target = Math.Min(16d, scroll.ScrollableHeight);
        Assert.IsTrue(scroll.ChangeView(
            horizontalOffset: null,
            verticalOffset: target,
            zoomFactor: null,
            disableAnimation: true));
        await DrainDispatcherAsync(dispatcherOwner);
        scroll.UpdateLayout();
        Assert.IsGreaterThan(0d, scroll.VerticalOffset);
        return scroll.VerticalOffset;
    }

    private static double GetAbsoluteTop(UIElement element)
    {
        Assert.IsNotNull(element.XamlRoot);
        var root = element.XamlRoot.Content as UIElement;
        Assert.IsNotNull(root);
        return element.TransformToVisual(root).TransformPoint(default).Y;
    }

    private static async Task WaitForDisclosureStateAsync(
        ModelInspectionPage page,
        Expander disclosure,
        bool isExpanded)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow +
            TimeSpan.FromSeconds(10);
        ModelInspectionFigmaState expectedState = isExpanded
            ? ModelInspectionFigmaState.ReadyExpanded
            : ModelInspectionFigmaState.ReadyCollapsed;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await DrainDispatcherAsync(page);
            page.UpdateLayout();
            if (disclosure.IsExpanded == isExpanded &&
                page.CurrentPresentation?.State == expectedState)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        Assert.Fail(
            $"The Ready disclosure did not reach {expectedState} before timeout.");
    }

    private static string FlattenVisibleText(
        ModelInspectionPagePresentation presentation)
    {
        IEnumerable<string> modelText =
        [
            presentation.ModelCard.ModelName,
            presentation.ModelCard.CompactSummary,
            presentation.ModelCard.FormatShortName,
            presentation.ModelCard.OverviewFormatBadgeText,
            presentation.ModelCard.Publisher,
            presentation.ModelCard.FormatName,
            presentation.ModelCard.Quantisation,
            presentation.ModelCard.ParameterCount,
            presentation.ModelCard.ModelType,
            presentation.ModelCard.DeclaredContext,
            presentation.ModelCard.FileSize,
            presentation.ModelCard.InspectionChecksSummary
        ];
        IEnumerable<string> checkText = presentation.ModelCard.InspectionChecks
            .SelectMany(check => new[]
            {
                check.Title,
                check.Detail,
                check.StatusText,
                check.AutomationName
            });
        IEnumerable<string> contentText =
        [
            presentation.ContentCard.SectionTitle,
            presentation.ContentCard.ProgressSummary,
            presentation.ContentCard.SupportingText,
            presentation.ContentCard.TertiaryText,
            presentation.ContentCard.DiagnosticCode,
            presentation.ContentCard.DisclosureSummary,
            presentation.ContentCard.TechnicalDetailsActionText,
            presentation.ContentCard.TechnicalDetailsAutomationName,
            presentation.ContentCard.TechnicalDetailsAutomationHelpText
        ];
        IEnumerable<string> itemText = presentation.ContentCard.Items
            .Concat(presentation.ContentCard.ExpandedItems)
            .SelectMany(item => new[]
            {
                item.StageNumber,
                item.Title,
                item.Detail,
                item.StatusText,
                item.AutomationName
            });
        IEnumerable<string> outcomeText =
        [
            presentation.OutcomeCard.Title,
            presentation.OutcomeCard.Message,
            presentation.OutcomeCard.AutomationName
        ];
        IEnumerable<string> actionText =
        [
            presentation.ActionCard.Title,
            presentation.ActionCard.Message,
            presentation.ActionCard.AutomationName,
            presentation.ActionCard.CancelAction.Text,
            presentation.ActionCard.SecondaryActionOne.Text,
            presentation.ActionCard.SecondaryActionTwo.Text,
            presentation.ActionCard.PrimaryAction.Text,
            presentation.ActionCard.CancelAction.AutomationHelpText,
            presentation.ActionCard.SecondaryActionOne.AutomationHelpText,
            presentation.ActionCard.SecondaryActionTwo.AutomationHelpText,
            presentation.ActionCard.PrimaryAction.AutomationHelpText,
            presentation.ProgressAnnouncement,
            presentation.OutcomeAnnouncement
        ];

        return string.Join(
            "\n",
            modelText
                .Concat(checkText)
                .Concat(contentText)
                .Concat(itemText)
                .Concat(outcomeText)
                .Concat(actionText));
    }

    private static async Task AssertNoProcessesRemainAsync(
        params string[] processNames)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow +
            TimeSpan.FromSeconds(5);
        while (true)
        {
            List<int> processIds = [];
            foreach (string processName in processNames)
            {
                Process[] processes = Process.GetProcessesByName(processName);
                try
                {
                    processIds.AddRange(
                        processes.Select(process => process.Id));
                }
                finally
                {
                    foreach (Process process in processes)
                    {
                        process.Dispose();
                    }
                }
            }

            if (processIds.Count == 0)
            {
                return;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                Assert.Fail(
                    $"Inspection worker processes remained after cleanup: " +
                    string.Join(", ", processIds));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }

    private static void RetireLoadedPage(
        ModelInspectionPage page,
        Window window)
    {
        InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        window.Content = null;
        window.Close();
    }

    private sealed class ControlledInspectionService : IModelInspectionService
    {
        private readonly Queue<ControlledCall> calls = new();

        internal int CallCount { get; private set; }

        internal TaskCompletionSource<bool> FirstCallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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
                    "No controlled Model Inspection call was queued.");
            }

            ControlledCall call = calls.Dequeue();
            call.Bind(request, progress, cancellationToken);
            CallCount++;
            FirstCallStarted.TrySetResult(true);
            return call.Completion.Task;
        }
    }

    private sealed class ControlledCall
    {
        internal TaskCompletionSource<ModelInspectionExecutionResult> Completion
        { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ModelInspectionRequest? Request { get; private set; }

        internal IProgress<ModelInspectionProgress>? Progress { get; private set; }

        internal CancellationToken CancellationToken { get; private set; }

        internal void Bind(
            ModelInspectionRequest request,
            IProgress<ModelInspectionProgress>? progress,
            CancellationToken cancellationToken)
        {
            Request = request;
            Progress = progress;
            CancellationToken = cancellationToken;
            if (Completion.Task.IsCompletedSuccessfully)
            {
                ReportCompletedStages(Completion.Task.Result);
            }
        }

        internal void Report(ModelInspectionProgress progress)
        {
            Assert.IsNotNull(Progress);
            Progress.Report(progress);
        }

        internal void Complete(ModelInspectionExecutionResult result)
        {
            ReportCompletedStages(result);
            Completion.SetResult(result);
        }

        private void ReportCompletedStages(ModelInspectionExecutionResult result)
        {
            if (Progress is null || result.Status != ModelInspectionExecutionStatus.Completed)
            {
                return;
            }

            // A successful fake service must obey the same final-progress
            // contract as the real service, so the page can drain its stages.
            foreach (ModelInspectionStage stage in Enum.GetValues<ModelInspectionStage>())
            {
                Progress.Report(new ModelInspectionProgress(
                    stage, ModelInspectionStageStatus.Completed, (int)stage,
                    5, 1d, "Completed inspection check."));
            }
        }
    }

    private sealed class ImmediateMilestoneScheduler :
        IModelInspectionMilestoneScheduler
    {
        private bool disposed;

        public TimeSpan Elapsed => TimeSpan.Zero;

        internal Action? DisposeCallback { get; set; }

        internal int DisposeCount { get; private set; }

        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            if (delay <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            ObjectDisposedException.ThrowIf(disposed, this);
            callback();
            return EmptyRegistration.Instance;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisposeCount++;
            DisposeCallback?.Invoke();
        }

        private sealed class EmptyRegistration : IDisposable
        {
            internal static EmptyRegistration Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class ManualRenderDispatcher : IModelInspectionRenderDispatcher
    {
        private readonly Queue<Action> callbacks = new();
        private int successfulEnqueueCount;

        internal int PendingCount => callbacks.Count;

        internal int? SuccessfulEnqueueLimit { get; init; }

        public bool TryEnqueue(Action callback)
        {
            if (SuccessfulEnqueueLimit is int limit &&
                successfulEnqueueCount >= limit)
            {
                return false;
            }

            callbacks.Enqueue(callback);
            successfulEnqueueCount++;
            return true;
        }

        internal void RunAll()
        {
            while (callbacks.Count > 0)
            {
                callbacks.Dequeue()();
            }
        }

        internal void RunNext()
        {
            if (callbacks.Count == 0)
            {
                throw new InvalidOperationException(
                    "No queued dispatcher callback is available.");
            }

            callbacks.Dequeue()();
        }
    }

    private sealed class RecordingMotionSettings : IModelInspectionMotionSettings
    {
        private bool animationsEnabled;
        private EventHandler? animationsEnabledChanged;

        internal RecordingMotionSettings(bool animationsEnabled)
        {
            this.animationsEnabled = animationsEnabled;
        }

        public bool AnimationsEnabled => AnimationsEnabledError is Exception error
            ? throw error
            : animationsEnabled;

        public event EventHandler? AnimationsEnabledChanged
        {
            add => animationsEnabledChanged += value;
            remove
            {
                RemoveHandlerCount++;
                if (RemoveHandlerError is Exception error)
                {
                    throw error;
                }

                animationsEnabledChanged -= value;
            }
        }

        internal Exception? AnimationsEnabledError { get; init; }

        internal Action? DisposeCallback { get; set; }

        internal Exception? RemoveHandlerError { get; set; }

        internal int RemoveHandlerCount { get; private set; }

        internal bool IsDisposed { get; private set; }

        internal int DisposeCount { get; private set; }

        internal void SetAnimationsEnabled(bool value)
        {
            animationsEnabled = value;
            RaiseChanged();
        }

        internal void RaiseChanged() =>
            animationsEnabledChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            DisposeCount++;
            IsDisposed = true;
            DisposeCallback?.Invoke();
        }
    }

    private static TextBlock AdjacentOutcomeDetail(
        ModelInspectionPreviewProjection preview,
        string headingName)
    {
        TextBlock heading = Assert.IsInstanceOfType<TextBlock>(
            preview.FindElement(headingName));
        Panel parent = Assert.IsInstanceOfType<Panel>(
            VisualTreeHelper.GetParent(heading));
        return Assert.IsInstanceOfType<TextBlock>(parent.Children
            .OfType<TextBlock>()
            .First(item => !ReferenceEquals(item, heading)));
    }

    private static IEnumerable<T> PreviewDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (T nested in PreviewDescendants<T>(child)) yield return nested;
        }
    }

    private static ModelInspectionPreviewProjection Preview(
        ModelInspectionPage page)
    {
        FieldInfo? field = typeof(ModelInspectionPage).GetField(
            "_activePreview",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return Assert.IsInstanceOfType<ModelInspectionPreviewProjection>(
            field.GetValue(page));
    }

    private sealed class RecordingPageAnimationDriver :
        IModelInspectionAnimationDriver
    {
        private readonly List<(
            UIElement Outgoing,
            UIElement Incoming,
            ModelInspectionVisualOperationKey Key,
            Action<ModelInspectionVisualOperationKey> Completed)> terminal = [];
        private readonly List<DisclosureCall> disclosure = [];

        internal sealed record DisclosureCall(
            ModelInspectionVisualOperationKey Key,
            Action<ModelInspectionVisualOperationKey> Completed,
            IReadOnlyList<double> PreviousTopOffsets,
            IReadOnlyList<double> CurrentTopOffsets);

        internal Action? CancelAllCallback { get; set; }

        internal Action? DisposeCallback { get; set; }

        internal int CancelAllCount { get; private set; }

        internal bool IsDisposed { get; private set; }

        internal int DisposeCount { get; private set; }

        internal int StageStatusStartCount { get; private set; }

        internal int ActiveDetailStartCount { get; private set; }

        internal int DisclosureStartCount { get; private set; }

        internal IReadOnlyList<DisclosureCall> DisclosureStarts => disclosure;

        internal IReadOnlyList<(
            UIElement Outgoing,
            UIElement Incoming,
            ModelInspectionVisualOperationKey Key,
            Action<ModelInspectionVisualOperationKey> Completed)> TerminalStarts =>
            terminal;

        internal int TotalStartCount =>
            StageStatusStartCount +
            ActiveDetailStartCount +
            DisclosureStartCount +
            terminal.Count;

        public void StartStageStatus(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            StageStatusStartCount++;
        }

        public void StartActiveDetail(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            ActiveDetailStartCount++;
        }

        public void StartDisclosure(
            UIElement chevron,
            FrameworkElement viewport,
            IReadOnlyList<UIElement> followingElements,
            bool isExpanded,
            IReadOnlyList<double> previousTopOffsets,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            DisclosureStartCount++;
            double[] currentTopOffsets = new double[followingElements.Count];
            for (int index = 0; index < followingElements.Count; index++)
            {
                UIElement element = followingElements[index];
                currentTopOffsets[index] = element.XamlRoot?.Content is UIElement root
                    ? element.TransformToVisual(root).TransformPoint(default).Y
                    : double.NaN;
            }

            disclosure.Add(new DisclosureCall(
                key,
                completed,
                previousTopOffsets.ToArray(),
                currentTopOffsets));
        }

        public void StartTerminal(
            UIElement outgoing,
            UIElement incoming,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            terminal.Add((outgoing, incoming, key, completed));
        }

        internal void CompleteLastTerminal()
        {
            var call = terminal[^1];
            call.Completed(call.Key);
        }

        internal void CompleteTerminal(int index)
        {
            var call = terminal[index];
            call.Completed(call.Key);
        }

        internal void CompleteDisclosure(int index)
        {
            var call = disclosure[index];
            call.Completed(call.Key);
        }

        public void CancelAll()
        {
            CancelAllCount++;
            CancelAllCallback?.Invoke();
        }

        public void Dispose()
        {
            DisposeCount++;
            IsDisposed = true;
            DisposeCallback?.Invoke();
        }
    }
}
