#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
[DoNotParallelize]
[TestCategory("ModelInspectionFixtureGallery")]
public sealed class ModelInspectionFixtureViewModelIntegrationTests
{
    [TestMethod]
    public async Task Cancel_UsesRealCommandAndWaitsForCooperativeServiceResult()
    {
        using ModelInspectionFixtureSession session = Session("MI-012");
        using ModelInspectionViewModel viewModel = CreateViewModel(session);
        Task run = viewModel.StartAsync();
        session.Service.ReleaseServiceCheckpoint(1, "progress");

        viewModel.CancelCommand.Execute(null);

        Assert.AreEqual(new ModelInspectionRenderKey(1, 2),
            viewModel.Snapshot.RenderKey);
        Assert.IsTrue(viewModel.Snapshot.IsRunActive);
        Assert.IsTrue(viewModel.Snapshot.IsCancellationRequested);
        Assert.IsFalse(viewModel.CancelCommand.CanExecute(null));
        Assert.IsNull(viewModel.Result);
        Assert.AreEqual(1, session.Evidence.ServiceCallCount);
        Assert.AreEqual(1, session.Evidence.CancellationObservationCount);

        session.Service.ReleaseServiceCheckpoint(1, "cancelled");
        await run;

        Assert.AreEqual(ModelInspectionExecutionStatus.Cancelled,
            viewModel.Result?.Status);
        Assert.AreEqual(true, viewModel.Result?.CancellationWasCooperative);
        Assert.IsTrue(viewModel.RetryCommand.CanExecute(null));
        Assert.AreEqual(1, session.Evidence.ServiceCallCount);
    }

    [DataTestMethod]
    [DataRow("MI-031", "cancelled", "ready",
        (int)ModelInspectionExecutionStatus.Cancelled,
        nameof(ModelInspectionFixtureSetupStepKind.InvokeRestart))]
    [DataRow("MI-032", "failure", "ready",
        (int)ModelInspectionExecutionStatus.OperationalFailure,
        nameof(ModelInspectionFixtureSetupStepKind.InvokeRetry))]
    public async Task RetryAndRestart_UseRealRetryCommandAndFreshGeneration(
        string id,
        string firstCheckpoint,
        string secondCheckpoint,
        int firstStatus,
        string expectedSetupKind)
    {
        ValidatedModelInspectionFixture fixture =
            ModelInspectionFixtureTestCatalogue.Get(id);
        ModelInspectionFixtureSetupStepDescriptor recoverySetup = fixture.Input
            .SetupSteps
            .Single(step => step.Kind is
                ModelInspectionFixtureSetupStepKind.InvokeRetry or
                ModelInspectionFixtureSetupStepKind.InvokeRestart);
        Assert.AreEqual(expectedSetupKind, recoverySetup.Kind.ToString());
        Assert.IsNull(typeof(ModelInspectionViewModel).GetProperty(
            "RestartCommand",
            BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic));
        using ModelInspectionFixtureSession session = new(fixture.Input);
        using ModelInspectionViewModel viewModel = CreateViewModel(session);

        Task first = viewModel.StartAsync();
        session.Service.ReleaseServiceCheckpoint(1, firstCheckpoint);
        await first;

        Assert.AreEqual((ModelInspectionExecutionStatus)firstStatus,
            viewModel.Result?.Status);
        Assert.AreEqual(1L,
            viewModel.Snapshot.RenderKey.AttemptGeneration);
        Assert.IsTrue(viewModel.RetryCommand.CanExecute(null));
        Task<ModelInspectionViewSnapshot> secondTerminal = WaitForSnapshot(
            viewModel,
            snapshot =>
                snapshot.RenderKey.AttemptGeneration == 2 &&
                snapshot.TerminalResult is not null);

        viewModel.RetryCommand.Execute(null);

        Assert.AreEqual(2, session.Evidence.ServiceCallCount);
        Assert.AreEqual(new ModelInspectionRenderKey(2, 0),
            viewModel.Snapshot.RenderKey);
        Assert.IsTrue(viewModel.IsRunActive);
        Assert.IsNull(viewModel.Result);
        session.Service.ReleaseServiceCheckpoint(2, secondCheckpoint);

        ModelInspectionViewSnapshot terminal = await secondTerminal;
        Assert.AreEqual(2L, terminal.RenderKey.AttemptGeneration);
        Assert.AreEqual(
            ModelInspectionExecutionStatus.Completed,
            terminal.TerminalResult?.Status);
        Assert.AreEqual(
            ModelInspectionOutcome.Ready,
            terminal.TerminalResult?.Result?.Outcome);
        CollectionAssert.AreEqual(
            new[] { 1, 2 },
            session.Evidence.ServiceCalls
                .Select(call => call.Attempt)
                .ToArray());
        Assert.IsTrue(session.Evidence.ServiceCalls.All(call =>
            ReferenceEquals(session.Request, call.Request)));
        Assert.AreNotEqual(
            session.Evidence.ServiceCalls[0].CancellationToken,
            session.Evidence.ServiceCalls[1].CancellationToken);
    }

    [TestMethod]
    public async Task ChooseAnother_UsesRealCommandAndInvalidatesBeforeRetirement()
    {
        using ModelInspectionFixtureSession session = Session("MI-014");
        using ModelInspectionViewModel viewModel = CreateViewModel(session);
        bool eventObservedInvalidatedState = false;
        viewModel.ChooseAnotherRequested += (_, _) =>
            eventObservedInvalidatedState =
                !viewModel.IsRunActive &&
                viewModel.Progress is null &&
                viewModel.Result is null;
        Task run = viewModel.StartAsync();
        session.Service.ReleaseServiceCheckpoint(1, "progress");
        Assert.IsNotNull(viewModel.Progress);

        viewModel.ChooseAnotherCommand.Execute(null);

        Assert.IsTrue(eventObservedInvalidatedState);
        Assert.AreEqual(new ModelInspectionRenderKey(2, 0),
            viewModel.Snapshot.RenderKey);
        Assert.IsFalse(viewModel.IsRunActive);
        Assert.IsNull(viewModel.Progress);
        Assert.IsNull(viewModel.Result);
        Assert.AreEqual(1, session.Evidence.CancellationObservationCount);

        session.Retire();
        await run;
        Assert.IsNull(viewModel.Result);
        Assert.AreEqual(1, session.Evidence.ServiceCallCount);
    }

    [TestMethod]
    public async Task UnsupportedCommandStates_DoNotCreateServiceAttempts()
    {
        using ModelInspectionFixtureSession session = Session("MI-002");
        using ModelInspectionViewModel viewModel = CreateViewModel(session);
        Task run = viewModel.StartAsync();

        Assert.IsFalse(viewModel.RetryCommand.CanExecute(null));
        viewModel.RetryCommand.Execute(null);
        Assert.AreEqual(1, session.Evidence.ServiceCallCount);

        session.Service.ReleaseServiceCheckpoint(1, "terminal");
        await run;
        Assert.IsFalse(viewModel.CancelCommand.CanExecute(null));
        viewModel.CancelCommand.Execute(null);
        Assert.AreEqual(1, session.Evidence.ServiceCallCount);
        Assert.AreEqual(0, session.Evidence.CancellationObservationCount);
    }

    [TestMethod]
    public void ServiceContract_DoesNotAcquireViewModelCommands()
    {
        CollectionAssert.AreEqual(
            new[] { nameof(IModelInspectionService.InspectAsync) },
            typeof(IModelInspectionService)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());

        string[] forbidden = ["Cancel", "Retry", "Restart", "ChooseAnother"];
        string[] debugServiceMethods = typeof(DebugModelInspectionService)
            .GetMethods(BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();
        foreach (string command in forbidden)
        {
            Assert.IsFalse(
                debugServiceMethods.Any(name => name.Contains(
                    command,
                    StringComparison.Ordinal)),
                $"The deterministic service must not own {command}.");
        }
    }

    private static ModelInspectionFixtureSession Session(string id) => new(
        ModelInspectionFixtureTestCatalogue.Get(id).Input);

    private static ModelInspectionViewModel CreateViewModel(
        ModelInspectionFixtureSession session)
    {
        SynchronizationContext? original = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            return new ModelInspectionViewModel(session.Service, session.Request);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }
    }

    private static Task<ModelInspectionViewSnapshot> WaitForSnapshot(
        ModelInspectionViewModel viewModel,
        Predicate<ModelInspectionViewSnapshot> predicate)
    {
        if (predicate(viewModel.Snapshot))
        {
            return Task.FromResult(viewModel.Snapshot);
        }

        TaskCompletionSource<ModelInspectionViewSnapshot> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler? handler = null;
        handler = (_, arguments) =>
        {
            if (arguments.PropertyName != nameof(ModelInspectionViewModel.Snapshot))
            {
                return;
            }

            ModelInspectionViewSnapshot snapshot = viewModel.Snapshot;
            if (!predicate(snapshot))
            {
                return;
            }

            viewModel.PropertyChanged -= handler;
            completion.TrySetResult(snapshot);
        };
        viewModel.PropertyChanged += handler;
        return completion.Task;
    }
}
#endif
