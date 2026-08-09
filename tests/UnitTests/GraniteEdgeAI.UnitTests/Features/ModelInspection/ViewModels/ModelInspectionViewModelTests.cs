using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel;

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
        Assert.AreSame(progress, viewModel.Progress);
        Assert.AreSame(result, viewModel.Result);
        Assert.IsFalse(viewModel.CancelCommand.CanExecute(null));
        Assert.IsTrue(viewModel.RetryCommand.CanExecute(null));
        CollectionAssert.Contains(
            propertyNotifications,
            nameof(ModelInspectionViewModel.IsRunActive));
        CollectionAssert.Contains(
            propertyNotifications,
            nameof(ModelInspectionViewModel.Progress));
        CollectionAssert.Contains(
            propertyNotifications,
            nameof(ModelInspectionViewModel.Result));
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

        Assert.AreSame(currentProgress, viewModel.Progress);
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
            if (args.PropertyName == nameof(ModelInspectionViewModel.Result))
            {
                terminalObserverSawRetiredAttempt = !viewModel.IsRunActive;
                call.Report(queuedProgress);
            }
        };

        Task run = viewModel.StartAsync();
        call.Report(acceptedProgress);
        call.Complete(CreateFailureResult("terminal"));
        await run;

        Assert.IsTrue(terminalObserverSawRetiredAttempt);
        Assert.AreSame(acceptedProgress, viewModel.Progress);
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
        Assert.AreSame(acceptedProgress, viewModel.Progress);
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
    }

    private static ModelInspectionViewModel CreateViewModel(
        IModelInspectionService service,
        ModelInspectionRequest request)
    {
        SynchronizationContext? originalContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            return new ModelInspectionViewModel(service, request);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
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
}
