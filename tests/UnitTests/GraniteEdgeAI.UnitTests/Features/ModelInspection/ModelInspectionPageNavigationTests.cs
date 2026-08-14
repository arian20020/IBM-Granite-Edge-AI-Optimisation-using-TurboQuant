using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        var page = new ModelInspectionPage(service);
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
            var modelHeading = (InspectionModelCard)page.FindName(
                "InspectionModelCardControl");
            Assert.AreSame(
                modelHeading,
                FocusManager.GetFocusedElement(page.XamlRoot),
                "Rendering startup must retain the selected-model heading focus.");

            // A subsequent layout pass must not create a second attempt.
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
            var modelControl = (InspectionModelCard)page.FindName(
                "InspectionModelCardControl");
            var contentControl = (InspectionContentCard)page.FindName(
                "InspectionContentCardControl");
            var outcomeControl = (InspectionOutcomeCard)page.FindName(
                "InspectionOutcomeCardControl");
            var actionControl = (InspectionActionCard)page.FindName(
                "InspectionActionCardControl");
            var outgoingControl = (InspectionContentCard)page.FindName(
                "OutgoingProgressContentCard");
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
            Assert.AreSame(modelControl, page.FindName("InspectionModelCardControl"));
            Assert.AreSame(contentControl, page.FindName("InspectionContentCardControl"));
            Assert.AreSame(outcomeControl, page.FindName("InspectionOutcomeCardControl"));
            Assert.AreSame(actionControl, page.FindName("InspectionActionCardControl"));
            Assert.AreSame(retainedModel, modelControl.Presentation);
            Assert.AreSame(retainedContent, contentControl.Presentation);
            Assert.AreSame(retainedOutcome, outcomeControl.Presentation);
            Assert.AreSame(retainedActions, actionControl.Presentation);
            CollectionAssert.AreEqual(
                retainedRows,
                progressRows.Items.Cast<object>().ToArray());
            Assert.AreEqual(
                InspectionContentStatus.Active,
                progressRows.Items[2].Status);

            int modelAssignments = 0;
            int contentAssignments = 0;
            int outcomeAssignments = 0;
            int actionAssignments = 0;
            long modelToken = modelControl.RegisterPropertyChangedCallback(
                InspectionModelCard.PresentationProperty,
                (_, _) => modelAssignments++);
            long contentToken = contentControl.RegisterPropertyChangedCallback(
                InspectionContentCard.PresentationProperty,
                (_, _) => contentAssignments++);
            long outcomeToken = outcomeControl.RegisterPropertyChangedCallback(
                InspectionOutcomeCard.PresentationProperty,
                (_, _) => outcomeAssignments++);
            long actionToken = actionControl.RegisterPropertyChangedCallback(
                InspectionActionCard.PresentationProperty,
                (_, _) => actionAssignments++);

            latest.ActionCard.CancelAction.Command!.Execute(null);
            Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
            Assert.AreEqual(1, dispatcher.PendingCount);
            dispatcher.RunAll();

            Assert.AreEqual(0, modelAssignments);
            Assert.AreEqual(0, contentAssignments);
            Assert.AreEqual(0, outcomeAssignments);
            Assert.AreEqual(1, actionAssignments);
            Assert.AreSame(retainedModel, modelControl.Presentation);
            Assert.AreSame(retainedContent, contentControl.Presentation);
            Assert.AreSame(retainedOutcome, outcomeControl.Presentation);
            Assert.AreNotSame(retainedActions, actionControl.Presentation);
            Assert.IsFalse(
                page.CurrentPresentation!.ActionCard.CancelAction.IsEnabled);
            modelControl.UnregisterPropertyChangedCallback(
                InspectionModelCard.PresentationProperty,
                modelToken);
            contentControl.UnregisterPropertyChangedCallback(
                InspectionContentCard.PresentationProperty,
                contentToken);
            outcomeControl.UnregisterPropertyChangedCallback(
                InspectionOutcomeCard.PresentationProperty,
                outcomeToken);
            actionControl.UnregisterPropertyChangedCallback(
                InspectionActionCard.PresentationProperty,
                actionToken);

            bool progressRetiredBeforeOutcome = false;
            long visibilityToken = outcomeControl.RegisterPropertyChangedCallback(
                InspectionOutcomeCard.CardVisibilityProperty,
                (_, _) =>
                {
                    if (outcomeControl.CardVisibility != Visibility.Visible)
                    {
                        return;
                    }

                    progressRetiredBeforeOutcome =
                        contentControl.Presentation.Mode !=
                            InspectionContentCardMode.Progress &&
                        AutomationProperties.GetLiveSetting(contentControl) ==
                            AutomationLiveSetting.Off &&
                        outgoingControl.Visibility == Visibility.Visible &&
                        !outgoingControl.IsHitTestVisible &&
                        AutomationProperties.GetAccessibilityView(outgoingControl) ==
                            AccessibilityView.Raw &&
                        AutomationProperties.GetLiveSetting(outgoingControl) ==
                            AutomationLiveSetting.Off;
                });

            call.Complete(CreateFailureResult("terminal"));
            await run;
            dispatcher.RunAll();
            outcomeControl.UnregisterPropertyChangedCallback(
                InspectionOutcomeCard.CardVisibilityProperty,
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
        await run;
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
        await run;
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
        await run;

        Assert.AreSame(lastVisibleSnapshot, page.CurrentPresentation);
        AssertCompleteSnapshotApplied(page);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_ConfiguresProgressAndOutcomeLiveRegions()
    {
        var service = new ControlledInspectionService();
        var page = new ModelInspectionPage(service);
        var explanation = (TextBlock)page.FindName(
            "ModelInspectionExplanation");
        var content = (InspectionContentCard)page.FindName(
            "InspectionContentCardControl");
        var outcome = (InspectionOutcomeCard)page.FindName(
            "InspectionOutcomeCardControl");

        Assert.AreEqual(
            "We are checking that the model package, tokenizer, structure, " +
            "and runtime are supported before checking hardware fit.",
            explanation.Text);
        Assert.AreEqual(
            AutomationLiveSetting.Off,
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
        int dispatcherFactoryCalls = 0;
        int driverFactoryCalls = 0;
        int settingsFactoryCalls = 0;
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

        Assert.AreEqual(1, dispatcherFactoryCalls);
        Assert.AreEqual(1, driverFactoryCalls);
        Assert.AreEqual(1, settingsFactoryCalls);

        InvokeNavigation(page, "OnNavigatedFrom", parameter: null);

        CollectionAssert.AreEqual(
            new[] { "cancel:True", "driver:True", "settings:True" },
            retirement);
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
            });
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
            () => settings);

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
            await run;
            dispatcher.RunAll();
            Assert.HasCount(1, driver.TerminalStarts);
            var outgoing = (InspectionContentCard)page.FindName(
                "OutgoingProgressContentCard");
            Assert.AreEqual(Visibility.Visible, outgoing.Visibility);
            InspectionContentCardPresentation retained = outgoing.Presentation;
            bool completionWasRejected = false;
            driver.CancelAllCallback = () =>
            {
                driver.CompleteTerminal(index: 0);
                completionWasRejected =
                    outgoing.Visibility == Visibility.Visible &&
                    ReferenceEquals(retained, outgoing.Presentation);
            };

            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
            retired = true;

            Assert.IsTrue(
                completionWasRejected,
                "A synchronous driver completion cannot mutate controls after navigation retirement begins.");
            Assert.AreEqual(Visibility.Collapsed, outgoing.Visibility);
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
                "InspectionPageScrollViewer");
            pageScroll.IsTabStop = true;
            Assert.IsTrue(pageScroll.Focus(FocusState.Programmatic));
            object? retainedFocus = FocusManager.GetFocusedElement(
                page.XamlRoot);
            Assert.AreSame(pageScroll, retainedFocus);
            ModelInspectionPagePresentation retainedPresentation =
                page.CurrentPresentation!;
            InspectionFooterStatus retainedFooter = page.CurrentFooterStatus;
            var content = (InspectionContentCard)page.FindName(
                "InspectionContentCardControl");
            var outcome = (InspectionOutcomeCard)page.FindName(
                "InspectionOutcomeCardControl");
            int retainedProgressAnnouncements =
                content.LiveRegionChangeNotificationCount;
            int retainedOutcomeAnnouncements =
                outcome.LiveRegionChangeNotificationCount;
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
                content.LiveRegionChangeNotificationCount);
            Assert.AreEqual(
                retainedOutcomeAnnouncements,
                outcome.LiveRegionChangeNotificationCount);
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
            () => settings[settingsIndex++]);

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
        service.QueueCall(ModelInspectionExecutionResult.Completed(
            CreateReadyResult()));
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
            () => settings[settingsIndex++]);
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
            await run;
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
        service.QueueCall(ModelInspectionExecutionResult.Completed(
            CreateReadyResult()));
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
            await run;
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
        var content = (InspectionContentCard)page.FindName(
            "InspectionContentCardControl");
        InspectionContentCardPresentation progressPresentation =
            content.Presentation;
        InspectionProgressRows owner = progressPresentation.ProgressRows;
        object[] rowReferences = owner.Items.Cast<object>().ToArray();

        call.Report(CreateProgress(
            ModelInspectionStage.CheckModelPackage,
            completedStageCount: 0));
        dispatcher.RunAll();

        Assert.AreNotSame(progressPresentation, content.Presentation,
            "The first counted stage must retire the dedicated startup DTO.");
        progressPresentation = content.Presentation;
        CollectionAssert.AreEqual(
            rowReferences,
            owner.Items.Cast<object>().ToArray());
        Assert.AreEqual(InspectionContentStatus.Active, owner.Items[0].Status);

        call.Complete(CreateFailureResult("terminal-retained"));
        await run;
        dispatcher.RunAll();

        var outgoing = (InspectionContentCard)page.FindName(
            "OutgoingProgressContentCard");
        Assert.AreSame(progressPresentation, outgoing.Presentation);
        Assert.AreEqual(Visibility.Visible, outgoing.Visibility);
        Assert.IsFalse(outgoing.IsHitTestVisible);
        Assert.AreEqual(
            AccessibilityView.Raw,
            AutomationProperties.GetAccessibilityView(outgoing));
        Assert.AreEqual(InspectionContentCardMode.OperationalFailure,
            content.Presentation.Mode);
        Assert.AreEqual(1, driver.TerminalStarts.Count);

        driver.CompleteLastTerminal();

        Assert.AreEqual(Visibility.Collapsed, outgoing.Visibility);
        Assert.AreEqual(
            InspectionContentCardMode.Hidden,
            outgoing.Presentation.Mode);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task TerminalCrossfade_DisclosureRevisionStillRetiresOutgoingLayer()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingPageAnimationDriver();
        var page = CreateInjectedPage(
            service,
            CreateRequest(),
            dispatcher,
            driver,
            new RecordingMotionSettings(animationsEnabled: true));
        Task run = page.StartInspectionIfReadyAsync()!;
        dispatcher.RunAll();
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = page };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            page.UpdateLayout();

            call.Complete(CreateCompletedResult(
                ModelInspectionOutcome.ReadyWithWarnings));
            await run;
            dispatcher.RunAll();
            page.UpdateLayout();

            var outgoing = (InspectionContentCard)page.FindName(
                "OutgoingProgressContentCard");
            var content = (InspectionContentCard)page.FindName(
                "InspectionContentCardControl");
            var actions = (InspectionActionCard)page.FindName(
                "InspectionActionCardControl");
            Assert.AreEqual(Visibility.Visible, outgoing.Visibility);
            Assert.IsGreaterThan(0d, outgoing.ActualHeight);
            Assert.AreEqual(
                AutomationLiveSetting.Off,
                AutomationProperties.GetLiveSetting(outgoing));
            Assert.AreEqual(
                AutomationLiveSetting.Off,
                AutomationProperties.GetLiveSetting(content));
            Assert.HasCount(1, driver.TerminalStarts);
            Assert.IsNotNull(content.ActiveDisclosure);

            content.ActiveDisclosure!.RequestTargetState(isExpanded: true);
            dispatcher.RunAll();
            page.UpdateLayout();
            double actionTopWithOutgoing = GetAbsoluteTop(actions);
            Assert.HasCount(1, driver.DisclosureStarts);
            Assert.AreEqual(
                driver.TerminalStarts[0].Key.RenderKey,
                driver.DisclosureStarts[0].Key.RenderKey);
            Assert.AreNotEqual(
                driver.TerminalStarts[0].Key.InteractionRevision,
                driver.DisclosureStarts[0].Key.InteractionRevision);

            driver.CompleteTerminal(index: 0);
            page.UpdateLayout();

            Assert.AreEqual(Visibility.Collapsed, outgoing.Visibility);
            Assert.AreEqual(
                InspectionContentCardMode.Hidden,
                outgoing.Presentation.Mode);

            driver.CompleteDisclosure(index: 0);
            content.ActiveDisclosure.RequestTargetState(isExpanded: false);
            dispatcher.RunAll();
            Assert.HasCount(2, driver.DisclosureStarts);
            driver.CompleteDisclosure(index: 1);
            page.UpdateLayout();
            Assert.IsLessThan(
                actionTopWithOutgoing,
                GetAbsoluteTop(actions),
                "Retiring the taller progress overlay must release its Grid-row height.");
        }
        finally
        {
            RetireLoadedPage(page, window);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task TerminalOutcome_RetiresProgressLiveRegionAndStaleAnnouncement()
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
        Task run = page.StartInspectionIfReadyAsync()!;
        dispatcher.RunAll();
        var content = (InspectionContentCard)page.FindName(
            "InspectionContentCardControl");
        var outcome = (InspectionOutcomeCard)page.FindName(
            "InspectionOutcomeCardControl");

        call.Report(CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            completedStageCount: 1));
        dispatcher.RunAll();
        string progressName = AutomationProperties.GetName(content);
        int progressNotifications = content.LiveRegionChangeNotificationCount;
        Assert.AreEqual(
            AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(content));
        Assert.IsGreaterThan(0, progressNotifications);

        call.Complete(CreateFailureResult("live-region-retirement"));
        await run;
        dispatcher.RunAll();

        string terminalName = AutomationProperties.GetName(content);
        Assert.AreEqual(
            AutomationLiveSetting.Off,
            AutomationProperties.GetLiveSetting(content));
        Assert.AreEqual(
            page.CurrentPresentation!.ContentCard.SectionTitle,
            terminalName);
        Assert.AreNotEqual(progressName, terminalName);
        Assert.AreEqual(
            progressNotifications,
            content.LiveRegionChangeNotificationCount,
            "Terminal assignment must not raise the retired polite region.");
        Assert.AreEqual(
            AutomationLiveSetting.Assertive,
            AutomationProperties.GetLiveSetting(outcome));
        Assert.AreEqual(1, outcome.LiveRegionChangeNotificationCount);

        content.AnnounceProgress("Stale progress must not be announced.");
        Assert.AreEqual(terminalName, AutomationProperties.GetName(content));
        Assert.AreEqual(
            progressNotifications,
            content.LiveRegionChangeNotificationCount);
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

        var outgoing = (InspectionContentCard)page.FindName(
            "OutgoingProgressContentCard");
        Assert.HasCount(2, driver.TerminalStarts);
        Assert.AreEqual(Visibility.Visible, outgoing.Visibility);
        InspectionContentCardPresentation replacement = outgoing.Presentation;

        driver.CompleteTerminal(index: 0);

        Assert.AreEqual(Visibility.Visible, outgoing.Visibility);
        Assert.AreSame(replacement, outgoing.Presentation);

        driver.CompleteTerminal(index: 1);
        Assert.AreEqual(Visibility.Collapsed, outgoing.Visibility);
        Assert.AreEqual(
            InspectionContentCardMode.Hidden,
            outgoing.Presentation.Mode);
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
            () => new RecordingMotionSettings(animationsEnabled: true));

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
        var outgoing = (InspectionContentCard)page.FindName(
            "OutgoingProgressContentCard");
        Assert.AreEqual(Visibility.Visible, outgoing.Visibility);
        InspectionContentCardPresentation replacement = outgoing.Presentation;

        oldDriver.CompleteTerminal(index: 0);

        Assert.AreEqual(Visibility.Visible, outgoing.Visibility);
        Assert.AreSame(replacement, outgoing.Presentation);

        currentDriver.CompleteTerminal(index: 0);
        Assert.AreEqual(Visibility.Collapsed, outgoing.Visibility);
        Assert.AreEqual(
            InspectionContentCardMode.Hidden,
            outgoing.Presentation.Mode);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ReducedMotion_AppliesSameTerminalStateWithoutDriverStarts()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingPageAnimationDriver();
        var settings = new RecordingMotionSettings(animationsEnabled: false);
        var page = CreateInjectedPage(
            service,
            CreateRequest(),
            dispatcher,
            driver,
            settings);
        Task run = page.StartInspectionIfReadyAsync()!;
        dispatcher.RunAll();

        call.Complete(CreateFailureResult("reduced"));
        await run;
        dispatcher.RunAll();

        Assert.AreEqual(0, driver.TotalStartCount);
        Assert.AreEqual(
            InspectionOutcomePresentationKind.OperationalFailure,
            page.CurrentPresentation!.OutcomeCard.Kind);
        var outgoing = (InspectionContentCard)page.FindName(
            "OutgoingProgressContentCard");
        Assert.AreEqual(Visibility.Collapsed, outgoing.Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task FailureFocus_MovesOnlyWhenRetiringProgressOwnedFocus()
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
        var window = new Window { Content = page };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            dispatcher.RunAll();
            var progressCard = (InspectionContentCard)page.FindName(
                "InspectionContentCardControl");
            progressCard.IsTabStop = true;
            Assert.IsTrue(progressCard.Focus(FocusState.Programmatic));

            first.Complete(CreateFailureResult("focused-progress"));
            await page.CurrentInspectionTask!.WaitAsync(TimeSpan.FromSeconds(10));
            dispatcher.RunAll();
            var outcome = (InspectionOutcomeCard)page.FindName(
                "InspectionOutcomeCardControl");
            Assert.AreSame(
                outcome.FocusTarget,
                FocusManager.GetFocusedElement(page.XamlRoot));
            Assert.IsFalse(((Control)outcome.FocusTarget).IsTabStop);

            service.QueueCall(CreateFailureResult("stable-focus"));
            var pageScroll = (ScrollViewer)page.FindName(
                "InspectionPageScrollViewer");
            pageScroll.IsTabStop = true;
            Assert.IsTrue(pageScroll.Focus(FocusState.Programmatic));
            page.ViewModel!.RetryCommand.Execute(null);
            dispatcher.RunAll();

            Assert.AreSame(
                pageScroll,
                FocusManager.GetFocusedElement(page.XamlRoot));

            ControlledCall focusRecoveryCall = service.QueueCall();
            var actions = (InspectionActionCard)page.FindName(
                "InspectionActionCardControl");
            var retry = (Button)actions.FindName("PrimaryActionButton");
            Assert.AreEqual(Visibility.Visible, retry.Visibility);
            Assert.IsTrue(retry.IsEnabled);
            Assert.IsTrue(retry.Focus(FocusState.Programmatic));

            page.ViewModel.RetryCommand.Execute(null);
            dispatcher.RunNext();

            var model = (InspectionModelCard)page.FindName(
                "InspectionModelCardControl");
            object? focusedAfterRetry = FocusManager.GetFocusedElement(
                page.XamlRoot);
            bool collapsedRetryRecoveredToModel = ReferenceEquals(
                model,
                focusedAfterRetry);
            Assert.AreEqual(Visibility.Collapsed, retry.Visibility);
            AssertStartupPresentation(
                AssertCompleteSnapshotApplied(page).ContentCard);

            dispatcher.RunNext();
            Assert.AreEqual(3, service.CallCount);

            var cancel = (Button)actions.FindName("CancelActionButton");
            Assert.AreEqual(Visibility.Visible, cancel.Visibility);
            Assert.IsTrue(cancel.IsEnabled);
            Assert.IsTrue(cancel.Focus(FocusState.Programmatic));
            page.ViewModel.CancelCommand.Execute(null);
            Assert.IsTrue(
                focusRecoveryCall.CancellationToken.IsCancellationRequested);
            dispatcher.RunNext();

            bool disabledCancelRecoveredToModel = ReferenceEquals(
                model,
                FocusManager.GetFocusedElement(page.XamlRoot));
            AssertStartupPresentation(
                AssertCompleteSnapshotApplied(page).ContentCard);
            Assert.AreEqual(Visibility.Visible, cancel.Visibility);
            Assert.IsFalse(cancel.IsEnabled);
            Assert.AreEqual(
                "Cancel model inspection",
                AutomationProperties.GetName(cancel));
            focusRecoveryCall.Complete(
                ModelInspectionExecutionResult.Cancelled(cooperative: true));
            await page.CurrentInspectionTask!.WaitAsync(
                TimeSpan.FromSeconds(10));
            await DrainDispatcherAsync(page);
            dispatcher.RunAll();

            Assert.IsTrue(
                collapsedRetryRecoveredToModel,
                $"Startup must recover focus from the collapsed retry action to the selected-model heading. Actual: {focusedAfterRetry?.GetType().Name ?? "null"}.");
            Assert.IsTrue(
                disabledCancelRecoveredToModel,
                "Startup must recover focus from the disabled cancel action to the selected-model heading.");
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
        var outcome = (InspectionOutcomeCard)page.FindName(
            "InspectionOutcomeCardControl");
        Assert.AreEqual(1, outcome.LiveRegionChangeNotificationCount);

        outcome.Visibility = Visibility.Collapsed;
        outcome.Visibility = Visibility.Visible;
        Assert.AreEqual(1, outcome.LiveRegionChangeNotificationCount);

        page.ViewModel!.RetryCommand.Execute(null);
        await Task.Yield();
        dispatcher.RunAll();

        Assert.AreEqual(2, outcome.LiveRegionChangeNotificationCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ReadyDisclosure_RetainsRowsAndScrollAcrossPageOwnedRevisions()
    {
        var fixture = await CreateLoadedTerminalPageAsync(
            ModelInspectionOutcome.Ready);

        try
        {
            var model = (InspectionModelCard)fixture.Page.FindName(
                "InspectionModelCardControl");
            Assert.IsNotNull(
                model.ActiveDisclosure,
                "The initial Ready presentation must expose its disclosure.");
            InspectionDisclosure disclosure = model.ActiveDisclosure!;
            await CompleteDisclosureRequestAsync(
                fixture.Page,
                disclosure,
                fixture.Dispatcher,
                fixture.Driver,
                isExpanded: true);

            var items = (ItemsControl)model.FindName(
                "InspectionChecksItemsControl");
            var scroll = (ScrollViewer)model.FindName(
                "InspectionChecksScrollViewer");
            Assert.IsNotNull(items.ItemsSource);
            object source = items.ItemsSource;
            object[] rows = items.Items.Cast<object>().ToArray();
            object[] containers = CaptureItemContainers(items);
            double offset = await SetNonzeroScrollOffsetAsync(
                fixture.Page,
                scroll);

            await CompleteDisclosureRequestAsync(
                fixture.Page,
                disclosure,
                fixture.Dispatcher,
                fixture.Driver,
                isExpanded: false);
            await CompleteDisclosureRequestAsync(
                fixture.Page,
                disclosure,
                fixture.Dispatcher,
                fixture.Driver,
                isExpanded: true);

            Assert.AreSame(source, items.ItemsSource);
            CollectionAssert.AreEqual(rows, items.Items.Cast<object>().ToArray());
            CollectionAssert.AreEqual(
                containers,
                CaptureItemContainers(items));
            Assert.AreEqual(offset, scroll.VerticalOffset, 0.01d);

            int terminalStartIndex = fixture.Driver.TerminalStarts.Count;
            ModelInspectionRenderKey retiredKey =
                fixture.Page.CurrentPresentation!.RenderKey;
            var outcome = (InspectionOutcomeCard)fixture.Page.FindName(
                "InspectionOutcomeCardControl");
            int retiredAnnouncementCount =
                outcome.LiveRegionChangeNotificationCount;
            ControlledCall retry = fixture.Service.QueueCall();
            fixture.Page.ViewModel!.RetryCommand.Execute(null);
            Assert.AreEqual(2, fixture.Dispatcher.PendingCount);
            fixture.Dispatcher.RunAll();
            ModelInspectionPagePresentation retryProgress =
                fixture.Page.CurrentPresentation!;
            Assert.AreEqual(
                ModelInspectionFigmaState.InspectionProgress,
                retryProgress.State);
            Assert.AreEqual(
                InspectionModelCardMode.Compact,
                model.Presentation.DisplayMode);
            Assert.IsNull(
                model.ActiveDisclosure,
                "Retry must reset the prior attempt's expanded disclosure.");

            fixture.InitialCall.Report(CreateProgress(
                ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
                completedStageCount: 4));
            GetCoordinator(fixture.Page).RequestRender(
                new ModelInspectionViewSnapshot(
                    retiredKey,
                    isRunActive: false,
                    isCancellationRequested: false,
                    progress: null,
                    terminalResult: ModelInspectionExecutionResult.Completed(
                        CreateReadyResult())));
            fixture.Driver.CompleteTerminal(index: 0);
            fixture.Dispatcher.RunAll();

            Assert.AreSame(retryProgress, fixture.Page.CurrentPresentation);
            Assert.AreEqual(
                retiredAnnouncementCount,
                outcome.LiveRegionChangeNotificationCount,
                "Retired progress/result/animation work cannot repeat the prior announcement.");

            retry.Complete(CreateCompletedResult(ModelInspectionOutcome.Ready));
            await DrainDispatcherAsync(fixture.Page);
            fixture.Dispatcher.RunAll();
            Assert.AreEqual(
                terminalStartIndex + 1,
                fixture.Driver.TerminalStarts.Count);
            fixture.Driver.CompleteLastTerminal();
            fixture.Page.UpdateLayout();
            Assert.AreEqual(
                ModelInspectionFigmaState.ReadyCollapsed,
                fixture.Page.CurrentPresentation!.State);
            Assert.AreEqual(
                retiredAnnouncementCount + 1,
                outcome.LiveRegionChangeNotificationCount);

            Assert.IsNotNull(
                model.ActiveDisclosure,
                "The replacement Ready presentation must expose its disclosure.");
            disclosure = model.ActiveDisclosure!;
            await CompleteDisclosureRequestAsync(
                fixture.Page,
                disclosure,
                fixture.Dispatcher,
                fixture.Driver,
                isExpanded: true);

            Assert.AreNotSame(source, items.ItemsSource,
                "A new attempt must install one new disclosure row owner.");
            Assert.IsNotNull(items.ItemsSource);
            object replacement = items.ItemsSource;
            await CompleteDisclosureRequestAsync(
                fixture.Page,
                disclosure,
                fixture.Dispatcher,
                fixture.Driver,
                isExpanded: false);
            await CompleteDisclosureRequestAsync(
                fixture.Page,
                disclosure,
                fixture.Dispatcher,
                fixture.Driver,
                isExpanded: true);
            Assert.AreSame(replacement, items.ItemsSource,
                "The replacement owner must then survive expansion-only renders.");
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
        var fixture = await CreateLoadedTerminalPageAsync(
            ModelInspectionOutcome.Ready);

        try
        {
            var model = (InspectionModelCard)fixture.Page.FindName(
                "InspectionModelCardControl");
            InspectionDisclosure disclosure = model.ActiveDisclosure!;
            await CompleteDisclosureRequestAsync(
                fixture.Page,
                disclosure,
                fixture.Dispatcher,
                fixture.Driver,
                isExpanded: true);
            var checks = (ScrollViewer)model.FindName(
                "InspectionChecksScrollViewer");
            checks.IsTabStop = true;
            Assert.IsTrue(checks.Focus(FocusState.Programmatic));
            Assert.AreSame(
                checks,
                FocusManager.GetFocusedElement(fixture.Page.XamlRoot));

            ModelInspectionRenderKey currentKey =
                fixture.Page.CurrentPresentation!.RenderKey;
            var updatedSnapshot = new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(
                    currentKey.AttemptGeneration,
                    currentKey.PresentationRevision + 1),
                isRunActive: false,
                isCancellationRequested: false,
                progress: null,
                terminalResult: ModelInspectionExecutionResult.Completed(
                    CreateReadyResult(TimeSpan.FromSeconds(5))));

            GetCoordinator(fixture.Page).RequestRender(updatedSnapshot);
            fixture.Dispatcher.RunAll();

            Assert.AreEqual(
                ModelInspectionFigmaState.ReadyExpanded,
                fixture.Page.CurrentPresentation.State);
            Assert.IsTrue(disclosure.IsExpanded);
            Assert.AreSame(
                checks,
                FocusManager.GetFocusedElement(fixture.Page.XamlRoot));
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
            var content = (InspectionContentCard)fixture.Page.FindName(
                "InspectionContentCardControl");
            Assert.IsNotNull(
                content.ActiveDisclosure,
                "The initial terminal presentation must expose its disclosure.");
            InspectionDisclosure disclosure = content.ActiveDisclosure!;
            await CompleteDisclosureRequestAsync(
                fixture.Page,
                disclosure,
                fixture.Dispatcher,
                fixture.Driver,
                isExpanded: true);

            var items = (ItemsControl)content.FindName(
                "ExpandedReportItemsControl");
            var scroll = (ScrollViewer)content.FindName(
                "ExpandedReportScrollViewer");
            scroll.Height = 24d;
            fixture.Page.UpdateLayout();
            Assert.IsNotNull(items.ItemsSource);
            object source = items.ItemsSource;
            object[] rows = items.Items.Cast<object>().ToArray();
            object[] containers = CaptureItemContainers(items);
            double offset = await SetNonzeroScrollOffsetAsync(
                fixture.Page,
                scroll);

            await CompleteDisclosureRequestAsync(
                fixture.Page,
                disclosure,
                fixture.Dispatcher,
                fixture.Driver,
                isExpanded: false);
            await CompleteDisclosureRequestAsync(
                fixture.Page,
                disclosure,
                fixture.Dispatcher,
                fixture.Driver,
                isExpanded: true);

            Assert.AreSame(source, items.ItemsSource);
            CollectionAssert.AreEqual(rows, items.Items.Cast<object>().ToArray());
            CollectionAssert.AreEqual(
                containers,
                CaptureItemContainers(items));
            Assert.AreEqual(offset, scroll.VerticalOffset, 0.01d);

            int terminalStartIndex = fixture.Driver.TerminalStarts.Count;
            ControlledCall retry = fixture.Service.QueueCall();
            fixture.Page.ViewModel!.RetryCommand.Execute(null);
            await Task.Yield();
            fixture.Dispatcher.RunAll();
            Assert.AreEqual(
                InspectionContentCardMode.Progress,
                content.Presentation.Mode);

            ModelInspectionOutcome replacementOutcome = outcome switch
            {
                ModelInspectionOutcome.ReadyWithWarnings =>
                    ModelInspectionOutcome.ConversionRequired,
                ModelInspectionOutcome.ConversionRequired =>
                    ModelInspectionOutcome.Invalid,
                ModelInspectionOutcome.Invalid =>
                    ModelInspectionOutcome.ReadyWithWarnings,
                _ => throw new ArgumentOutOfRangeException(nameof(outcome))
            };
            retry.Complete(CreateCompletedResult(replacementOutcome));
            await DrainDispatcherAsync(fixture.Page);
            fixture.Dispatcher.RunAll();
            Assert.AreEqual(
                terminalStartIndex + 1,
                fixture.Driver.TerminalStarts.Count);
            fixture.Driver.CompleteLastTerminal();
            fixture.Page.UpdateLayout();

            Assert.IsNotNull(
                content.ActiveDisclosure,
                "The replacement terminal presentation must expose its disclosure.");
            disclosure = content.ActiveDisclosure!;
            await CompleteDisclosureRequestAsync(
                fixture.Page,
                disclosure,
                fixture.Dispatcher,
                fixture.Driver,
                isExpanded: true);
            Assert.AreNotSame(source, items.ItemsSource,
                "A new attempt/outcome must install one new report owner.");
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
        var fixture = await CreateLoadedTerminalPageAsync(
            ModelInspectionOutcome.Ready);

        try
        {
            var pageScroll = (ScrollViewer)fixture.Page.FindName(
                "InspectionPageScrollViewer");
            var host = (FrameworkElement)fixture.Page.FindName(
                "InspectionContentHost");
            var model = (InspectionModelCard)fixture.Page.FindName(
                "InspectionModelCardControl");
            var content = (InspectionContentCard)fixture.Page.FindName(
                "InspectionContentCardControl");
            var actions = (InspectionActionCard)fixture.Page.FindName(
                "InspectionActionCardControl");
            Assert.IsGreaterThan(0d, host.Margin.Top);
            pageScroll.Height = 320d;
            fixture.Page.UpdateLayout();
            await SetNonzeroScrollOffsetAsync(fixture.Page, pageScroll);
            InspectionDisclosure disclosure = model.ActiveDisclosure!;

            double[] collapsedAbsoluteTops =
                [GetAbsoluteTop(content), GetAbsoluteTop(actions)];
            disclosure.RequestTargetState(isExpanded: true);
            fixture.Dispatcher.RunAll();
            fixture.Page.UpdateLayout();

            Assert.HasCount(1, fixture.Driver.DisclosureStarts);
            RecordingPageAnimationDriver.DisclosureCall expansion =
                fixture.Driver.DisclosureStarts[0];
            CollectionAssert.AreEqual(
                collapsedAbsoluteTops,
                expansion.PreviousTopOffsets.ToArray());
            CollectionAssert.AreEqual(
                new[] { GetAbsoluteTop(content), GetAbsoluteTop(actions) },
                expansion.CurrentTopOffsets.ToArray());
            for (int index = 0; index < collapsedAbsoluteTops.Length; index++)
            {
                Assert.AreEqual(
                    collapsedAbsoluteTops[index] -
                        expansion.CurrentTopOffsets[index],
                    expansion.PreviousTopOffsets[index] -
                        expansion.CurrentTopOffsets[index],
                    0.01d);
            }

            double[] expandedAbsoluteTops =
                [GetAbsoluteTop(content), GetAbsoluteTop(actions)];
            disclosure.RequestTargetState(isExpanded: false);
            fixture.Dispatcher.RunAll();
            fixture.Page.UpdateLayout();
            Assert.HasCount(2, fixture.Driver.DisclosureStarts);
            RecordingPageAnimationDriver.DisclosureCall collapse =
                fixture.Driver.DisclosureStarts[1];
            CollectionAssert.AreEqual(
                expandedAbsoluteTops,
                collapse.PreviousTopOffsets.ToArray());
            CollectionAssert.AreEqual(
                new[] { GetAbsoluteTop(content), GetAbsoluteTop(actions) },
                collapse.CurrentTopOffsets.ToArray());

            fixture.Driver.CompleteDisclosure(index: 0);
            Assert.IsFalse(disclosure.IsExpanded,
                "The stale expansion completion must not win after reversal.");
            fixture.Driver.CompleteDisclosure(index: 1);
            Assert.IsFalse(disclosure.IsExpanded);
            Assert.AreEqual(
                ModelInspectionFigmaState.ReadyCollapsed,
                fixture.Page.CurrentPresentation!.State);
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
        var service = new ControlledInspectionService();
        service.QueueCall(ModelInspectionExecutionResult.Completed(
            CreateReadyResult()));
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
        await run;
        dispatcher.RunAll();
        InspectionDisclosure disclosure = ((InspectionModelCard)page.FindName(
            "InspectionModelCardControl")).ActiveDisclosure!;

        disclosure.RequestTargetState(isExpanded: true);
        settings.SetAnimationsEnabled(value: false);
        dispatcher.RunAll();

        Assert.AreEqual(0, driver.DisclosureStarts.Count);
        Assert.IsTrue(disclosure.IsExpanded);
        Assert.AreEqual(
            ModelInspectionFigmaState.ReadyExpanded,
            page.CurrentPresentation!.State);
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
        InspectionContentCard? observedContentControl = null;
        long startupToken = 0;

        try
        {
            Assert.IsTrue(frame.Navigate(typeof(ModelInspectionPage), request));
            page = frame.Content as ModelInspectionPage;
            Assert.IsNotNull(page);
            Assert.AreSame(request, page.Request);
            Assert.IsNotNull(page.ViewModel);
            observedContentControl = (InspectionContentCard)page.FindName(
                "InspectionContentCardControl");
            List<(
                string Summary,
                int WaitingCount,
                int ActiveCount)> observedStartupPresentations = [];
            List<string> observedSemanticOrder = [];
            startupToken = observedContentControl.RegisterPropertyChangedCallback(
                InspectionContentCard.PresentationProperty,
                (_, _) =>
                {
                    InspectionContentCardPresentation candidate =
                        observedContentControl.Presentation;
                    InspectionStartupPresentation startup = candidate.Startup;
                    if (startup.Visibility != Visibility.Visible)
                    {
                        return;
                    }

                    observedStartupPresentations.Add((
                        startup.Summary,
                        candidate.Items.Count(row =>
                            row.Status == InspectionContentStatus.Waiting),
                        candidate.Items.Count(row => row.IsActive)));
                    observedSemanticOrder.Add("Startup");
                });
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
            await DrainDispatcherAsync(page);

            Assert.HasCount(1, observedStartupPresentations);
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

            var model = (InspectionModelCard)page.FindName(
                "InspectionModelCardControl");
            InspectionDisclosure? disclosure = model.ActiveDisclosure;
            Assert.IsNotNull(disclosure);
            disclosure.RequestTargetState(isExpanded: true);
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

            disclosure.RequestTargetState(isExpanded: false);
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
                if (observedContentControl is not null && startupToken != 0)
                {
                    observedContentControl.UnregisterPropertyChangedCallback(
                        InspectionContentCard.PresentationProperty,
                        startupToken);
                }
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
        var page = new ModelInspectionPage(service);
        InvokeNavigation(page, "OnNavigatedTo", request);
        return page;
    }

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
            () => settings);
        InvokeNavigation(page, "OnNavigatedTo", request);
        return page;
    }

    private static ModelInspectionPagePresentation AssertCompleteSnapshotApplied(
        ModelInspectionPage page)
    {
        ModelInspectionPagePresentation? snapshot = page.CurrentPresentation;
        Assert.IsNotNull(snapshot);
        var model = (InspectionModelCard)page.FindName(
            "InspectionModelCardControl");
        var content = (InspectionContentCard)page.FindName(
            "InspectionContentCardControl");
        var outcome = (InspectionOutcomeCard)page.FindName(
            "InspectionOutcomeCardControl");
        var actions = (InspectionActionCard)page.FindName(
            "InspectionActionCardControl");

        AssertModelPresentation(snapshot.ModelCard, model.Presentation);
        AssertContentPresentation(snapshot.ContentCard, content.Presentation);
        AssertOutcomePresentation(snapshot.OutcomeCard, outcome.Presentation);
        AssertActionPresentation(snapshot.ActionCard, actions.Presentation);
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

    private static async Task DrainDispatcherAsync(FrameworkElement element)
    {
        var drained = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(element.DispatcherQueue.TryEnqueue(
            () => drained.TrySetResult(true)));
        await drained.Task.WaitAsync(TimeSpan.FromSeconds(10));
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
        await run;
        dispatcher.RunAll();
        driver.CompleteLastTerminal();

        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = page };
        window.Activate();
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
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
        InspectionDisclosure disclosure,
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
        }

        internal void Report(ModelInspectionProgress progress)
        {
            Assert.IsNotNull(Progress);
            Progress.Report(progress);
        }

        internal void Complete(ModelInspectionExecutionResult result)
        {
            Completion.SetResult(result);
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

    private sealed class RecordingPageAnimationDriver :
        IModelInspectionAnimationDriver
    {
        private readonly List<(
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
            terminal.Add((key, completed));
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
