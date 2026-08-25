using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelInspectionViewModelTests
{
    private static readonly DateTimeOffset FixedUtc = new(
        2026,
        8,
        9,
        8,
        0,
        0,
        TimeSpan.Zero);

    [TestMethod]
    public void Constructor_PublishesInitialSnapshot()
    {
        ModelInspectionViewModel viewModel = CreateViewModel(
            new ControlledInspectionService(),
            CreateRequest());

        Assert.AreSame(ModelInspectionViewSnapshot.Initial, viewModel.Snapshot);
        Type? barrierType = typeof(ModelInspectionViewModel).Assembly.GetType(
            "GraniteEdgeAI.Features.ModelInspection.ViewModels." +
            "IModelInspectionStartupPresentationBarrier");
        Assert.IsNotNull(
            barrierType,
            "Startup must have an explicit presentation-barrier seam.");
        Assert.IsNotNull(typeof(ModelInspectionViewModel).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [
                typeof(IModelInspectionService),
                typeof(ModelInspectionRequest),
                barrierType!
            ],
            modifiers: null));
    }

    [TestMethod]
    public async Task Snapshot_AdvancesExactKeysForStartProgressCancelAndTerminal()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        List<ModelInspectionViewSnapshot> published = [];
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ModelInspectionViewModel.Snapshot))
            {
                published.Add(viewModel.Snapshot);
            }
        };

        Task run = viewModel.StartAsync();
        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            completedStageCount: 1);
        call.Report(progress);
        viewModel.CancelCommand.Execute(null);
        ModelInspectionExecutionResult terminal = CreateFailureResult("terminal");
        call.Complete(terminal);
        await run;

        Assert.AreEqual(4, published.Count);
        Guid modelRunId = published[0].ModelInspectionRunId;
        Assert.IsTrue(
            GraniteEdgeAI.Features.ModelInspection.Handoff
                .ModelInspectionHandoff.IsUuidV4(modelRunId));
        Assert.IsTrue(published.All(item =>
            item.ModelInspectionRunId == modelRunId));
        AssertSnapshot(
            published[0],
            attemptGeneration: 1,
            presentationRevision: 0,
            isRunActive: true,
            isCancellationRequested: false,
            progress: null,
            terminalResult: null);
        AssertSnapshot(
            published[1],
            attemptGeneration: 1,
            presentationRevision: 1,
            isRunActive: true,
            isCancellationRequested: false,
            progress,
            terminalResult: null);
        AssertSnapshot(
            published[2],
            attemptGeneration: 1,
            presentationRevision: 2,
            isRunActive: true,
            isCancellationRequested: true,
            progress,
            terminalResult: null);
        AssertSnapshot(
            published[3],
            attemptGeneration: 1,
            presentationRevision: 3,
            isRunActive: false,
            isCancellationRequested: false,
            progress: null,
            terminal);
    }

    [TestMethod]
    public async Task StartAsync_ReplacesActiveAttemptWithNewGenerationBeforeCancellation()
    {
        ControlledInspectionService service = new();
        ControlledCall first = service.QueueCall();
        ControlledCall second = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task firstRun = viewModel.StartAsync();
        Guid firstModelRunId = viewModel.Snapshot.ModelInspectionRunId;
        ModelInspectionRenderKey cancellationObservedKey = default;
        using CancellationTokenRegistration registration =
            first.CancellationToken.Register(() =>
                cancellationObservedKey = viewModel.Snapshot.RenderKey);

        Task secondRun = viewModel.StartAsync();

        Assert.AreNotEqual(
            firstModelRunId,
            viewModel.Snapshot.ModelInspectionRunId);
        Assert.AreEqual(
            new ModelInspectionRenderKey(2, 0),
            cancellationObservedKey);
        Assert.IsTrue(viewModel.Snapshot.IsRunActive);
        first.Complete(CreateFailureResult("stale"));
        second.Complete(CreateFailureResult("current"));
        await Task.WhenAll(firstRun, secondRun);
    }

    [TestMethod]
    public async Task Retry_PublishesNewGenerationAtRevisionZero()
    {
        ControlledInspectionService service = new();
        ControlledCall first = service.QueueCall();
        ControlledCall second = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        first.Complete(CreateFailureResult("first"));
        await viewModel.StartAsync();
        ModelInspectionViewSnapshot? retryStart = null;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ModelInspectionViewModel.Snapshot) &&
                viewModel.Snapshot.IsRunActive)
            {
                retryStart = viewModel.Snapshot;
            }
        };
        second.Complete(CreateFailureResult("second"));

        viewModel.RetryCommand.Execute(null);

        Assert.IsNotNull(retryStart);
        Assert.AreEqual(new ModelInspectionRenderKey(2, 0), retryStart.RenderKey);
        Assert.IsNull(retryStart.Progress);
        Assert.IsNull(retryStart.TerminalResult);
        Assert.AreEqual(
            new ModelInspectionRenderKey(2, 1),
            viewModel.Snapshot.RenderKey);
        Assert.IsNotNull(viewModel.Snapshot.TerminalResult);
    }

    [TestMethod]
    public async Task ChooseAnother_AdvancesGenerationAndClearsActiveStateBeforeNavigation()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();
        call.Report(CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            completedStageCount: 1));
        ModelInspectionViewSnapshot? navigationSnapshot = null;
        viewModel.ChooseAnotherRequested += (_, _) =>
            navigationSnapshot = viewModel.Snapshot;

        viewModel.ChooseAnotherCommand.Execute(null);

        Assert.IsNotNull(navigationSnapshot);
        AssertSnapshot(
            navigationSnapshot,
            attemptGeneration: 2,
            presentationRevision: 0,
            isRunActive: false,
            isCancellationRequested: false,
            progress: null,
            terminalResult: null);
        call.Complete(CreateFailureResult("stale"));
        await run;
    }

    [TestMethod]
    public async Task ChooseAnother_AfterTerminalAdvancesGenerationAndClearsTerminalBeforeNavigation()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        call.Complete(CreateFailureResult("terminal"));
        await viewModel.StartAsync();
        ModelInspectionViewSnapshot? navigationSnapshot = null;
        viewModel.ChooseAnotherRequested += (_, _) =>
            navigationSnapshot = viewModel.Snapshot;

        viewModel.ChooseAnotherCommand.Execute(null);

        Assert.IsNotNull(navigationSnapshot);
        AssertSnapshot(
            navigationSnapshot,
            attemptGeneration: 2,
            presentationRevision: 0,
            isRunActive: false,
            isCancellationRequested: false,
            progress: null,
            terminalResult: null);
    }

    [TestMethod]
    public async Task DeactivateThenDispose_InvalidatesLifecycleOnlyOnce()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();
        int snapshotNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ModelInspectionViewModel.Snapshot))
            {
                snapshotNotifications++;
            }
        };

        viewModel.Deactivate();
        ModelInspectionViewSnapshot afterDeactivate = viewModel.Snapshot;
        viewModel.Dispose();

        Assert.AreEqual(new ModelInspectionRenderKey(2, 0), afterDeactivate.RenderKey);
        Assert.AreSame(afterDeactivate, viewModel.Snapshot);
        Assert.AreEqual(1, snapshotNotifications);
        call.Complete(CreateFailureResult("stale"));
        await run;
        Assert.AreSame(afterDeactivate, viewModel.Snapshot);
        Assert.AreEqual(1, snapshotNotifications);
    }

    [TestMethod]
    public async Task TerminalRevisionExhaustion_DoesNotRetireAttemptOrChangeSnapshot()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();
        ModelInspectionViewSnapshot saturated = SaturateRevision(viewModel);

        call.Complete(CreateFailureResult("terminal"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => run);
        Assert.AreSame(saturated, viewModel.Snapshot);
        Assert.IsTrue(viewModel.IsRunActive);
        Assert.IsTrue(viewModel.CancelCommand.CanExecute(null));
        viewModel.Dispose();
    }

    [TestMethod]
    public async Task CancellationRevisionExhaustion_DoesNotPoisonCancellationState()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();
        ModelInspectionViewSnapshot saturated = SaturateRevision(viewModel);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => viewModel.CancelCommand.Execute(null));

        Assert.AreSame(saturated, viewModel.Snapshot);
        Assert.IsFalse(call.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(viewModel.CancelCommand.CanExecute(null));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => viewModel.CancelCommand.Execute(null));

        viewModel.Deactivate();
        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        call.Complete(CreateFailureResult("stale"));
        await run;
    }

    [TestMethod]
    public async Task ChooseAnotherGenerationExhaustion_DoesNotDetachAttemptOrRaiseNavigation()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();
        ModelInspectionViewSnapshot before = viewModel.Snapshot;
        bool navigationRaised = false;
        viewModel.ChooseAnotherRequested += (_, _) => navigationRaised = true;
        SetPrivateField(viewModel, "nextAttemptGeneration", long.MaxValue);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => viewModel.ChooseAnotherCommand.Execute(null));

        Assert.AreSame(before, viewModel.Snapshot);
        Assert.IsFalse(navigationRaised);
        Assert.IsFalse(call.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(viewModel.CancelCommand.CanExecute(null));

        SetPrivateField(
            viewModel,
            "nextAttemptGeneration",
            before.RenderKey.AttemptGeneration);
        viewModel.ChooseAnotherCommand.Execute(null);
        Assert.IsTrue(navigationRaised);
        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        call.Complete(CreateFailureResult("stale"));
        await run;
    }

    [TestMethod]
    public async Task LifecycleGenerationExhaustion_DoesNotDetachOrMarkInvalidated()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();
        ModelInspectionViewSnapshot before = viewModel.Snapshot;
        SetPrivateField(viewModel, "nextAttemptGeneration", long.MaxValue);

        Assert.ThrowsExactly<InvalidOperationException>(viewModel.Deactivate);

        Assert.AreSame(before, viewModel.Snapshot);
        Assert.IsFalse(call.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(viewModel.CancelCommand.CanExecute(null));

        SetPrivateField(
            viewModel,
            "nextAttemptGeneration",
            before.RenderKey.AttemptGeneration);
        viewModel.Deactivate();
        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        call.Complete(CreateFailureResult("stale"));
        await run;
    }

    [TestMethod]
    public async Task DisposeGenerationExhaustion_DoesNotDisposeOrDetachAttempt()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();
        ModelInspectionViewSnapshot before = viewModel.Snapshot;
        SetPrivateField(viewModel, "nextAttemptGeneration", long.MaxValue);

        Assert.ThrowsExactly<InvalidOperationException>(viewModel.Dispose);

        Assert.AreSame(before, viewModel.Snapshot);
        Assert.IsTrue(viewModel.IsRunActive);
        Assert.IsTrue(viewModel.ChooseAnotherCommand.CanExecute(null));
        Assert.IsFalse(call.CancellationToken.IsCancellationRequested);

        SetPrivateField(
            viewModel,
            "nextAttemptGeneration",
            before.RenderKey.AttemptGeneration);
        viewModel.Dispose();
        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(viewModel.ChooseAnotherCommand.CanExecute(null));
        call.Complete(CreateFailureResult("stale"));
        await run;
    }

    [TestMethod]
    public async Task DuplicateEqualProgress_DoesNotAdvanceRevisionOrNotify()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();
        ModelInspectionProgress first = CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            completedStageCount: 1);
        ModelInspectionProgress equal = CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            completedStageCount: 1);
        call.Report(first);
        int snapshotNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ModelInspectionViewModel.Snapshot))
            {
                snapshotNotifications++;
            }
        };

        call.Report(equal);

        Assert.AreEqual(new ModelInspectionRenderKey(1, 1), viewModel.Snapshot.RenderKey);
        Assert.AreEqual(0, snapshotNotifications);
        call.Complete(CreateFailureResult("terminal"));
        await run;
    }

    [TestMethod]
    public async Task StartAsync_ForwardsExactRequestAndPublishesProgressAndResult()
    {
        ModelInspectionRequest request = CreateRequest();
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(service, request);
        List<string?> propertyNotifications = [];
        viewModel.PropertyChanged += (_, args) =>
            propertyNotifications.Add(args.PropertyName);

        Task run = viewModel.StartAsync();

        Assert.AreSame(request, call.Request);
        Assert.AreSame(request, viewModel.Request);
        Assert.IsTrue(viewModel.IsRunActive);
        Assert.IsNull(viewModel.Progress);
        Assert.IsNull(viewModel.Result);
        Assert.IsTrue(viewModel.CancelCommand.CanExecute(null));
        Assert.IsFalse(viewModel.RetryCommand.CanExecute(null));

        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            completedStageCount: 1);
        call.Report(progress);
        Assert.AreSame(progress, viewModel.Progress);

        ModelInspectionExecutionResult result = CreateFailureResult("first");
        call.Complete(result);
        await run;

        Assert.IsFalse(viewModel.IsRunActive);
        Assert.IsNull(viewModel.Progress);
        Assert.AreSame(result, viewModel.Result);
        Assert.IsFalse(viewModel.CancelCommand.CanExecute(null));
        Assert.IsTrue(viewModel.RetryCommand.CanExecute(null));
        CollectionAssert.AreEqual(
            new[]
            {
                nameof(ModelInspectionViewModel.Snapshot),
                nameof(ModelInspectionViewModel.Snapshot),
                nameof(ModelInspectionViewModel.Snapshot)
            },
            propertyNotifications);
    }

    [TestMethod]
    public async Task StartAsync_ReplacesAttemptBeforeCancellingAndIgnoresStaleUpdates()
    {
        ControlledInspectionService service = new();
        ControlledCall first = service.QueueCall();
        ControlledCall second = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        ModelInspectionProgress staleProgress = CreateProgress(
            ModelInspectionStage.ValidateTokenizerAndChatSetup,
            completedStageCount: 2);
        Task firstRun = viewModel.StartAsync();
        using CancellationTokenRegistration registration =
            first.CancellationToken.Register(() => first.Report(staleProgress));
        Task secondRun = viewModel.StartAsync();

        Assert.IsTrue(first.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(viewModel.IsRunActive);
        Assert.IsNull(viewModel.Progress);
        Assert.IsNull(viewModel.Result);

        first.Report(staleProgress);
        first.Complete(CreateFailureResult("stale"));
        await firstRun;
        Assert.IsTrue(viewModel.IsRunActive);
        Assert.IsNull(viewModel.Progress);
        Assert.IsNull(viewModel.Result);

        ModelInspectionProgress currentProgress = CreateProgress(
            ModelInspectionStage.ValidateModelStructure,
            completedStageCount: 3);
        second.Report(currentProgress);
        ModelInspectionExecutionResult currentResult =
            CreateFailureResult("current");
        second.Complete(currentResult);
        await secondRun;

        Assert.IsNull(viewModel.Progress);
        Assert.AreSame(currentResult, viewModel.Result);
        Assert.IsFalse(viewModel.IsRunActive);
    }

    [TestMethod]
    public async Task StartAsync_RetiresAttemptBeforeTerminalNotification()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        ModelInspectionProgress acceptedProgress = CreateProgress(
            ModelInspectionStage.ValidateModelStructure,
            completedStageCount: 3);
        ModelInspectionProgress queuedProgress = CreateProgress(
            ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
            completedStageCount: 4);
        bool terminalObserverSawRetiredAttempt = false;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ModelInspectionViewModel.Snapshot) &&
                viewModel.Snapshot.TerminalResult is not null)
            {
                terminalObserverSawRetiredAttempt =
                    !viewModel.Snapshot.IsRunActive &&
                    viewModel.Snapshot.Progress is null;
                call.Report(queuedProgress);
            }
        };

        Task run = viewModel.StartAsync();
        call.Report(acceptedProgress);
        call.Complete(CreateFailureResult("terminal"));
        await run;

        Assert.IsTrue(terminalObserverSawRetiredAttempt);
        Assert.IsNull(viewModel.Progress);
    }

    [TestMethod]
    public async Task CancelCommand_DisablesImmediatelyButPreservesReturnedFailure()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();

        viewModel.CancelCommand.Execute(null);

        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(viewModel.IsRunActive);
        Assert.IsFalse(viewModel.CancelCommand.CanExecute(null));
        Assert.IsNull(viewModel.Result);

        ModelInspectionExecutionResult forcedFailure =
            CreateFailureResult("forced-timeout");
        call.Complete(forcedFailure);
        await run;

        ModelInspectionExecutionResult? actualResult = viewModel.Result;
        Assert.AreSame(forcedFailure, actualResult);
        Assert.IsNotNull(actualResult);
        Assert.AreEqual(
            ModelInspectionExecutionStatus.OperationalFailure,
            actualResult.Status);

        ControlledInspectionService preStartService = new();
        preStartService.QueueCall();
        var heldBarrier = new HeldStartupPresentationBarrier();
        ModelInspectionViewModel preStartViewModel = CreateViewModel(
            preStartService,
            CreateRequest(),
            heldBarrier);
        Task preStartRun = preStartViewModel.StartAsync();

        preStartViewModel.CancelCommand.Execute(null);

        await preStartRun.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(
            0,
            preStartService.CallCount,
            "Cancel before startup presentation completes must not invoke the service.");
        heldBarrier.Release();
        Assert.AreEqual(
            ModelInspectionExecutionStatus.Cancelled,
            preStartViewModel.Result?.Status);
        Assert.IsTrue(
            preStartViewModel.Result?.CancellationWasCooperative is true);
        Assert.AreNotEqual(
            "MI-OP-CANCELLATION-UNCONFIRMED",
            preStartViewModel.Result?.Failure?.Code);
    }

    [TestMethod]
    public async Task CancelCommand_PublishesOnlyReturnedCooperativeCancellation()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();

        viewModel.CancelCommand.Execute(null);
        ModelInspectionExecutionResult cancelled =
            ModelInspectionExecutionResult.Cancelled(cooperative: true);
        call.Complete(cancelled);
        await run;

        ModelInspectionExecutionResult? actualResult = viewModel.Result;
        Assert.AreSame(cancelled, actualResult);
        Assert.IsNotNull(actualResult);
        Assert.AreEqual(
            ModelInspectionExecutionStatus.Cancelled,
            actualResult.Status);
        Assert.IsTrue(actualResult.CancellationWasCooperative is true);
    }

    [TestMethod]
    public async Task CancelCommand_UnconfirmedCancellationBecomesOperationalFailure()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();

        viewModel.CancelCommand.Execute(null);
        call.Fail(new OperationCanceledException(call.CancellationToken));
        await run;

        Assert.IsNotNull(viewModel.Result);
        Assert.AreEqual(
            ModelInspectionExecutionStatus.OperationalFailure,
            viewModel.Result.Status);
        Assert.AreEqual(
            "MI-OP-CANCELLATION-UNCONFIRMED",
            viewModel.Result.Failure?.Code);
    }

    [TestMethod]
    public async Task UnexpectedServiceFailure_UsesFixedPrivacySafeDiagnostic()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();

        call.Fail(new SensitiveInspectionSentinelException(
            @"C:\Private\Models\secret.gguf"));
        await run;

        ModelInspectionOperationalFailure? failure = viewModel.Result?.Failure;
        Assert.IsNotNull(failure);
        Assert.AreEqual("MI-OP-SERVICE-UNEXPECTED", failure.Code);
        Assert.AreEqual(
            "The inspection service ended unexpectedly.",
            failure.TechnicalDetail);
        StringAssert.DoesNotContain(
            failure.TechnicalDetail,
            nameof(SensitiveInspectionSentinelException),
            StringComparison.Ordinal);
        StringAssert.DoesNotContain(
            failure.TechnicalDetail,
            "secret.gguf",
            StringComparison.Ordinal);

        ControlledInspectionService startupService = new();
        startupService.QueueCall();
        ModelInspectionViewModel startupViewModel = CreateViewModel(
            startupService,
            CreateRequest(),
            new ThrowingStartupPresentationBarrier(
                new SensitiveInspectionSentinelException(
                    @"C:\Private\Startup\secret.xaml")));

        await startupViewModel.StartAsync();

        Assert.AreEqual(0, startupService.CallCount);
        ModelInspectionOperationalFailure? startupFailure =
            startupViewModel.Result?.Failure;
        Assert.IsNotNull(startupFailure);
        Assert.AreEqual(
            "MI-OP-STARTUP-PRESENTATION",
            startupFailure.Code);
        Assert.AreEqual(
            "Model inspection could not be started.",
            startupFailure.UserMessage);
        Assert.AreEqual(
            "The secure inspection startup presentation could not be confirmed.",
            startupFailure.TechnicalDetail);
        StringAssert.DoesNotContain(
            startupFailure.TechnicalDetail,
            "secret.xaml",
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RetryCommand_UsesSameRequestWithNewAttempt()
    {
        ModelInspectionRequest request = CreateRequest();
        ControlledInspectionService service = new();
        ControlledCall first = service.QueueCall();
        ControlledCall second = service.QueueCall();
        ModelInspectionExecutionResult firstResult = CreateFailureResult("first");
        ModelInspectionExecutionResult secondResult = CreateFailureResult("second");
        first.Complete(firstResult);
        second.Complete(secondResult);
        ModelInspectionViewModel viewModel = CreateViewModel(service, request);

        await viewModel.StartAsync();
        Assert.AreSame(firstResult, viewModel.Result);

        viewModel.RetryCommand.Execute(null);

        Assert.AreEqual(2, service.CallCount);
        Assert.AreSame(request, first.Request);
        Assert.AreSame(request, second.Request);
        Assert.AreNotEqual(first.CancellationToken, second.CancellationToken);
        Assert.AreSame(secondResult, viewModel.Result);
        Assert.IsFalse(viewModel.IsRunActive);
    }

    [TestMethod]
    public async Task ChooseAnother_InvalidatesBeforeCancellationAndEvent()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        ModelInspectionProgress acceptedProgress = CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            completedStageCount: 1);
        ModelInspectionProgress staleProgress = CreateProgress(
            ModelInspectionStage.ValidateModelStructure,
            completedStageCount: 3);
        bool cancellationSawInvalidatedAttempt = false;
        bool eventSawInvalidatedAttempt = false;
        viewModel.ChooseAnotherRequested += (_, _) =>
            eventSawInvalidatedAttempt = !viewModel.IsRunActive;

        Task run = viewModel.StartAsync();
        using CancellationTokenRegistration registration =
            call.CancellationToken.Register(() =>
            {
                cancellationSawInvalidatedAttempt = !viewModel.IsRunActive;
                call.Report(staleProgress);
            });
        call.Report(acceptedProgress);
        viewModel.ChooseAnotherCommand.Execute(null);

        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(cancellationSawInvalidatedAttempt);
        Assert.IsTrue(eventSawInvalidatedAttempt);
        Assert.IsNull(viewModel.Progress);
        Assert.IsNull(viewModel.Result);

        call.Complete(CreateFailureResult("stale"));
        await run;
        Assert.IsNull(viewModel.Result);
    }

    [TestMethod]
    public async Task Deactivate_InvalidatesBeforeCancellationAndIgnoresTerminalResult()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        bool cancellationSawInvalidatedAttempt = false;
        Task run = viewModel.StartAsync();
        using CancellationTokenRegistration registration =
            call.CancellationToken.Register(() =>
                cancellationSawInvalidatedAttempt = !viewModel.IsRunActive);

        viewModel.Deactivate();

        Assert.IsTrue(cancellationSawInvalidatedAttempt);
        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(viewModel.IsRunActive);
        call.Complete(CreateFailureResult("stale"));
        await run;
        Assert.IsNull(viewModel.Result);

        ControlledInspectionService preStartService = new();
        preStartService.QueueCall();
        var heldBarrier = new HeldStartupPresentationBarrier();
        ModelInspectionViewModel preStartViewModel = CreateViewModel(
            preStartService,
            CreateRequest(),
            heldBarrier);
        Task preStartRun = preStartViewModel.StartAsync();

        preStartViewModel.Deactivate();

        await preStartRun.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(
            0,
            preStartService.CallCount,
            "A lifecycle-retired pre-start attempt must not invoke the service.");
        heldBarrier.Release();
        Assert.IsFalse(preStartViewModel.IsRunActive);
        Assert.IsNull(preStartViewModel.Result);
    }

    [TestMethod]
    public async Task Dispose_InvalidatesCancelsAndPreventsFutureRuns()
    {
        ControlledInspectionService service = new();
        ControlledCall call = service.QueueCall();
        ModelInspectionViewModel viewModel = CreateViewModel(
            service,
            CreateRequest());
        Task run = viewModel.StartAsync();

        viewModel.Dispose();

        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(viewModel.IsRunActive);
        Assert.IsFalse(viewModel.CancelCommand.CanExecute(null));
        Assert.IsFalse(viewModel.RetryCommand.CanExecute(null));
        Assert.IsFalse(viewModel.ChooseAnotherCommand.CanExecute(null));

        call.Complete(CreateFailureResult("stale"));
        await run;
        Assert.IsNull(viewModel.Result);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            viewModel.StartAsync);

        ControlledInspectionService preStartService = new();
        preStartService.QueueCall();
        var heldBarrier = new HeldStartupPresentationBarrier();
        ModelInspectionViewModel preStartViewModel = CreateViewModel(
            preStartService,
            CreateRequest(),
            heldBarrier);
        Task preStartRun = preStartViewModel.StartAsync();

        preStartViewModel.Dispose();

        await preStartRun.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(
            0,
            preStartService.CallCount,
            "A disposed pre-start attempt must not invoke the service.");
        heldBarrier.Release();
        Assert.IsFalse(preStartViewModel.IsRunActive);
        Assert.IsNull(preStartViewModel.Result);
    }

    private static ModelInspectionViewModel CreateViewModel(
        IModelInspectionService service,
        ModelInspectionRequest request) => CreateViewModel(
            service,
            request,
            ImmediateStartupPresentationBarrier.Instance);

    private static ModelInspectionViewModel CreateViewModel(
        IModelInspectionService service,
        ModelInspectionRequest request,
        IModelInspectionStartupPresentationBarrier startupBarrier)
    {
        SynchronizationContext? originalContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            return new ModelInspectionViewModel(
                service,
                request,
                startupBarrier);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
    }

    private static ModelInspectionViewSnapshot SaturateRevision(
        ModelInspectionViewModel viewModel)
    {
        ModelInspectionViewSnapshot current = viewModel.Snapshot;
        ModelInspectionViewSnapshot saturated = new(
            new ModelInspectionRenderKey(
                current.RenderKey.AttemptGeneration,
                long.MaxValue),
            current.IsRunActive,
            current.IsCancellationRequested,
            current.Progress,
            current.TerminalResult);
        SetPrivateField(viewModel, "snapshot", saturated);
        return saturated;
    }

    private static void SetPrivateField<T>(
        ModelInspectionViewModel viewModel,
        string fieldName,
        T value)
    {
        FieldInfo field = typeof(ModelInspectionViewModel).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                $"ModelInspectionViewModel field '{fieldName}' was not found.");
        field.SetValue(viewModel, value);
    }

    private static void AssertSnapshot(
        ModelInspectionViewSnapshot snapshot,
        long attemptGeneration,
        long presentationRevision,
        bool isRunActive,
        bool isCancellationRequested,
        ModelInspectionProgress? progress,
        ModelInspectionExecutionResult? terminalResult)
    {
        Assert.AreEqual(
            new ModelInspectionRenderKey(
                attemptGeneration,
                presentationRevision),
            snapshot.RenderKey);
        Assert.AreEqual(isRunActive, snapshot.IsRunActive);
        Assert.AreEqual(
            isCancellationRequested,
            snapshot.IsCancellationRequested);
        Assert.AreSame(progress, snapshot.Progress);
        Assert.AreSame(terminalResult, snapshot.TerminalResult);
    }

    private static ModelInspectionRequest CreateRequest()
    {
        return new ModelInspectionRequest(
            modelPath: @"C:\Models\granite.gguf",
            fileName: "granite.gguf",
            expectedFileIdentity: new ExpectedModelFileIdentity(
                lengthBytes: 4_096,
                lastWriteTimeUtc: FixedUtc),
            quickScan: ValidatedQuickScanSnapshot.CreateGguf(
                modelName: "Granite",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantisation: "Q4_K_M",
                fileSizeBytes: 4_096,
                declaredContextLength: 4_096,
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

        internal ControlledCall QueueCall()
        {
            ControlledCall call = new();
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
                    "No controlled inspection call was queued.");
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

        internal void Fail(Exception exception)
        {
            Completion.SetException(exception);
        }
    }

    private sealed class SensitiveInspectionSentinelException : Exception
    {
        internal SensitiveInspectionSentinelException(string message)
            : base(message)
        {
        }
    }

    private sealed class ImmediateStartupPresentationBarrier :
        IModelInspectionStartupPresentationBarrier
    {
        internal static ImmediateStartupPresentationBarrier Instance { get; } =
            new();

        public ValueTask WaitForPresentationAsync(
            CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested
                ? ValueTask.FromCanceled(cancellationToken)
                : ValueTask.CompletedTask;
    }

    private sealed class HeldStartupPresentationBarrier :
        IModelInspectionStartupPresentationBarrier
    {
        private readonly TaskCompletionSource<bool> completion = new();

        public ValueTask WaitForPresentationAsync(
            CancellationToken cancellationToken) =>
            new(completion.Task.WaitAsync(cancellationToken));

        internal void Release() => completion.TrySetResult(true);
    }

    private sealed class ThrowingStartupPresentationBarrier :
        IModelInspectionStartupPresentationBarrier
    {
        private readonly Exception error;

        internal ThrowingStartupPresentationBarrier(Exception error)
        {
            this.error = error;
        }

        public ValueTask WaitForPresentationAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromException(error);
    }
}
