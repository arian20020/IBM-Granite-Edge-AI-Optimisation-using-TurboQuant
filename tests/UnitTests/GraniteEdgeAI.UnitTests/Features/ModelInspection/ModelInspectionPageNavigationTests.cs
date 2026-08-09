using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
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
/// Verifies the page-owned lifecycle between onboarding navigation, the
/// application service, and the four replaceable presentation cards.
/// </summary>
[TestClass]
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
        Assert.IsTrue(navigationSucceeded);
        Assert.IsNotNull(page);
        Assert.AreSame(request, page.Request);
        Assert.IsNotNull(page.ViewModel);
        Assert.AreSame(request, page.ViewModel.Request);
        Assert.IsNull(page.CurrentInspectionTask);

        ModelInspectionPagePresentation snapshot =
            AssertCompleteSnapshotApplied(page);
        Assert.AreEqual(
            InspectionContentCardMode.Progress,
            snapshot.ContentCard.Mode);
        Assert.AreEqual(
            InspectionOutcomePresentationKind.Hidden,
            snapshot.OutcomeCard.Kind);
        Assert.AreEqual(
            "granite-4.1-3b-instruct.gguf",
            snapshot.ModelCard.ModelName);
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

            Assert.AreEqual(1, service.CallCount);
            Task? currentInspectionTask = page.CurrentInspectionTask;
            Assert.IsNotNull(currentInspectionTask);
            Assert.IsTrue(page.ViewModel!.IsRunActive);

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
    public async Task ProgressAndTerminalEvents_ReplaceTheCompleteFourCardSnapshot()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var page = CreatePage(service, CreateRequest());
        ModelInspectionPagePresentation initial =
            AssertCompleteSnapshotApplied(page);
        Task run = page.StartInspectionIfReadyAsync()!;
        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            completedStageCount: 1);

        call.Report(progress);

        ModelInspectionPagePresentation active =
            AssertCompleteSnapshotApplied(page);
        Assert.AreNotSame(initial, active);
        Assert.AreEqual(
            "1 of 5 checks complete",
            active.ContentCard.ProgressSummary);
        Assert.AreEqual(
            InspectionOutcomePresentationKind.Hidden,
            active.OutcomeCard.Kind);
        Assert.IsTrue(active.ActionCard.CancelAction.IsEnabled);

        call.Complete(CreateFailureResult("terminal"));
        await run;

        ModelInspectionPagePresentation terminal =
            AssertCompleteSnapshotApplied(page);
        Assert.AreNotSame(active, terminal);
        Assert.AreEqual(
            InspectionOutcomePresentationKind.OperationalFailure,
            terminal.OutcomeCard.Kind);
        Assert.AreEqual(
            InspectionContentCardMode.OperationalFailure,
            terminal.ContentCard.Mode);
        Assert.IsTrue(terminal.ActionCard.PrimaryAction.IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CancelCommand_DisablesImmediatelyThenShowsConfirmedCancellation()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var page = CreatePage(service, CreateRequest());
        Task run = page.StartInspectionIfReadyAsync()!;
        InspectionActionPresentation cancel =
            AssertCompleteSnapshotApplied(page).ActionCard.CancelAction;

        cancel.Command!.Execute(null);

        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(
            AssertCompleteSnapshotApplied(page)
                .ActionCard.CancelAction.IsEnabled);
        Assert.IsTrue(page.ViewModel!.IsRunActive);

        call.Complete(ModelInspectionExecutionResult.Cancelled(cooperative: true));
        await run;

        ModelInspectionPagePresentation terminal =
            AssertCompleteSnapshotApplied(page);
        Assert.AreEqual(
            InspectionOutcomePresentationKind.Cancelled,
            terminal.OutcomeCard.Kind);
        Assert.AreEqual(
            InspectionContentCardMode.Cancelled,
            terminal.ContentCard.Mode);
        Assert.IsFalse(page.ViewModel.CancelCommand.CanExecute(null));
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
        InspectionActionPresentation retry =
            AssertCompleteSnapshotApplied(page).ActionCard.PrimaryAction;

        retry.Command!.Execute(null);

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
        Assert.AreEqual("replacement.gguf", replacementSnapshot.ModelCard.ModelName);

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
        call.Report(CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            completedStageCount: 1));
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
        var page = new ModelInspectionPage(new ControlledInspectionService());
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
            AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(content));
        Assert.AreEqual(
            AutomationLiveSetting.Assertive,
            AutomationProperties.GetLiveSetting(outcome));
    }

    [UITestMethod]
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
        Assert.IsTrue(frame.Navigate(typeof(ModelInspectionPage), request));
        var page = frame.Content as ModelInspectionPage;
        Assert.IsNotNull(page);
        Assert.AreSame(request, page.Request);
        Assert.IsNotNull(page.ViewModel);
        List<ModelInspectionProgress> observedProgress = [];
        page.ViewModel.PropertyChanged += (_, eventArguments) =>
        {
            if (eventArguments.PropertyName ==
                    nameof(ModelInspectionViewModel.Progress) &&
                page.ViewModel?.Progress is ModelInspectionProgress progress)
            {
                observedProgress.Add(progress);
            }
        };

        Task run = page.StartInspectionIfReadyAsync()!;
        await run.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.IsNotNull(page.ViewModel.Progress);
        Assert.AreEqual(5, page.ViewModel.Progress.CompletedStageCount);
        Assert.AreEqual(
            ModelInspectionStageStatus.Completed,
            page.ViewModel.Progress.StageStatus);
        Assert.AreEqual(
            ModelInspectionExecutionStatus.Completed,
            page.ViewModel.Result?.Status);
        Assert.AreEqual(
            ModelInspectionOutcome.Ready,
            page.ViewModel.Result?.Result?.Outcome);
        Assert.IsFalse(page.ViewModel.CancelCommand.CanExecute(null));
        CollectionAssert.AreEqual(
            Enum.GetValues<ModelInspectionStage>(),
            observedProgress
                .Where(progress =>
                    progress.StageStatus ==
                    ModelInspectionStageStatus.Completed)
                .Select(progress => progress.Stage)
                .ToArray());
        CollectionAssert.AreEqual(
            Enum.GetValues<ModelInspectionStage>(),
            observedProgress
                .Where(progress =>
                    progress.StageStatus == ModelInspectionStageStatus.Active)
                .Select(progress => progress.Stage)
                .Distinct()
                .ToArray());

        ModelInspectionPagePresentation terminal =
            AssertCompleteSnapshotApplied(page);
        Assert.AreEqual(
            InspectionOutcomePresentationKind.Ready,
            terminal.OutcomeCard.Kind);
        Assert.AreEqual(
            Visibility.Collapsed,
            terminal.ActionCard.CancelAction.Visibility);
    }

    private static ModelInspectionPage CreatePage(
        IModelInspectionService service,
        ModelInspectionRequest request)
    {
        var page = new ModelInspectionPage(service);
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

        Assert.AreSame(snapshot.ModelCard, model.Presentation);
        Assert.AreSame(snapshot.ContentCard, content.Presentation);
        Assert.AreSame(snapshot.OutcomeCard, outcome.Presentation);
        Assert.AreSame(snapshot.ActionCard, actions.Presentation);
        return snapshot;
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

    private sealed class ControlledInspectionService : IModelInspectionService
    {
        private readonly Queue<ControlledCall> calls = new();

        internal int CallCount { get; private set; }

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
}
