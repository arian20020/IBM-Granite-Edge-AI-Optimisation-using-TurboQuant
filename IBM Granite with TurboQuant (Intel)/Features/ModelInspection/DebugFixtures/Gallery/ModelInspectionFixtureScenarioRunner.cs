#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;

internal interface IModelInspectionFixtureScenarioRunner
{
    Task<string> RunAsync(
        ValidatedModelInspectionFixtureInput input,
        ModelInspectionPage page,
        ModelInspectionFixtureSession session,
        CancellationToken cancellationToken);
}

internal sealed class ModelInspectionFixtureScenarioRunner :
    IModelInspectionFixtureScenarioRunner
{
    public async Task<string> RunAsync(
        ValidatedModelInspectionFixtureInput input,
        ModelInspectionPage page,
        ModelInspectionFixtureSession session,
        CancellationToken cancellationToken)
        => await RunCoreAsync(
            input,
            page,
            session,
            stopCheckpoint: null,
            continueUntilNextInteraction: false,
            captureStaleResultSnapshots: true,
            cancellationToken);

    internal Task<string> RunToCheckpointAsync(
        ValidatedModelInspectionFixtureInput input,
        ModelInspectionPage page,
        ModelInspectionFixtureSession session,
        string stopCheckpoint,
        bool continueUntilNextInteraction,
        bool captureStaleResultSnapshots,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stopCheckpoint);
        return RunCoreAsync(
            input,
            page,
            session,
            stopCheckpoint,
            continueUntilNextInteraction,
            captureStaleResultSnapshots,
            cancellationToken);
    }

    private async Task<string> RunCoreAsync(
        ValidatedModelInspectionFixtureInput input,
        ModelInspectionPage page,
        ModelInspectionFixtureSession session,
        string? stopCheckpoint,
        bool continueUntilNextInteraction,
        bool captureStaleResultSnapshots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(session);
        RequireClosedObservationSequence(input);
        await WaitForLoadedAsync(page, cancellationToken);

        if (input.Attempts.Count == 0)
        {
            RequireServiceCallCount(session, expectedCount: 0);
        }
        else
        {
            await session.Evidence
                .WaitForNextServiceCallAsync(previousCount: 0)
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            await DrainDispatcherAsync(page, cancellationToken);
            RequireServiceCallCount(session, expectedCount: 1);
        }

        var pendingResultCaptures = new Dictionary<int, string>();
        bool stopReached = false;
        foreach (ModelInspectionFixtureSetupStepDescriptor step in
                 input.SetupSteps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stopReached && continueUntilNextInteraction &&
                !string.IsNullOrWhiteSpace(step.InteractionId))
            {
                return stopCheckpoint!;
            }

            bool invokedInteraction = false;
            switch (step.Kind)
            {
                case ModelInspectionFixtureSetupStepKind
                    .ReleaseServiceCheckpoint:
                    await ReleaseServiceCheckpointAsync(
                        page,
                        session,
                        step,
                        pendingResultCaptures,
                        captureStaleResultSnapshots,
                        cancellationToken);
                    break;
                case ModelInspectionFixtureSetupStepKind.InvokeDisclosure:
                    InvokeDisclosure(page);
                    invokedInteraction = true;
                    await session.Evidence.WaitForPendingZeroAsync(
                            cancellationToken)
                        .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    await DrainDispatcherAsync(page, cancellationToken);
                    break;
                case ModelInspectionFixtureSetupStepKind.InvokeCancel:
                    ExecuteRequiredCommand(
                        RequireViewModel(page).CancelCommand,
                        "Cancel");
                    invokedInteraction = true;
                    await DrainDispatcherAsync(page, cancellationToken);
                    break;
                case ModelInspectionFixtureSetupStepKind.InvokeRetry:
                case ModelInspectionFixtureSetupStepKind.InvokeRestart:
                    int previousCallCount = session.Evidence.ServiceCallCount;
                    int nextCall = checked(previousCallCount + 1);
                    ExecuteRequiredCommand(
                        RequireViewModel(page).RetryCommand,
                        step.Kind == ModelInspectionFixtureSetupStepKind
                            .InvokeRestart
                                ? "Restart"
                                : "Retry");
                    invokedInteraction = true;
                    await session.Evidence
                        .WaitForNextServiceCallAsync(previousCallCount)
                        .WaitAsync(
                            TimeSpan.FromSeconds(5),
                            cancellationToken);
                    await DrainDispatcherAsync(page, cancellationToken);
                    RequireServiceCallCount(session, nextCall);
                    break;
                case ModelInspectionFixtureSetupStepKind.InvokeChooseAnother:
                    ExecuteRequiredCommand(
                        RequireViewModel(page).ChooseAnotherCommand,
                        "Choose another");
                    invokedInteraction = true;
                    await DrainDispatcherAsync(page, cancellationToken);
                    break;
                case ModelInspectionFixtureSetupStepKind.ReleaseStaleProgress:
                    RequireIdentity(step);
                    if (!session.ReleaseStaleProgress(
                            step.Attempt!.Value,
                            step.Checkpoint!))
                    {
                        throw new InvalidOperationException(
                            "The declared stale progress was not released.");
                    }

                    await DrainDispatcherAsync(page, cancellationToken);
                    break;
                case ModelInspectionFixtureSetupStepKind
                    .SubmitStaleResultSnapshot:
                    RequireIdentity(step);
                    if (!page.SubmitStaleResultSnapshotForFixture(
                            step.Attempt!.Value,
                            step.Checkpoint!))
                    {
                        throw new InvalidOperationException(
                            "The declared stale result was not submitted.");
                    }

                    await DrainDispatcherAsync(page, cancellationToken);
                    break;
                case ModelInspectionFixtureSetupStepKind.ReleaseStaleMotion:
                    RequireIdentity(step);
                    if (!session.AnimationsEnabled)
                    {
                        await DrainDispatcherAsync(page, cancellationToken);
                        break;
                    }

                    if (!page.ReleaseStaleMotionForFixture(
                            step.Attempt!.Value,
                            step.Checkpoint!))
                    {
                        throw new InvalidOperationException(
                            "The declared stale motion was not released.");
                    }

                    await DrainDispatcherAsync(page, cancellationToken);
                    break;
                case ModelInspectionFixtureSetupStepKind
                    .ReleaseStaleAnnouncement:
                    RequireIdentity(step);
                    if (!page.ReleaseStaleAnnouncementForFixture(
                            step.Attempt!.Value,
                            step.Checkpoint!))
                    {
                        throw new InvalidOperationException(
                            "The declared stale announcement was not released.");
                    }

                    await DrainDispatcherAsync(page, cancellationToken);
                    break;
                case ModelInspectionFixtureSetupStepKind.Observe:
                    if (pendingResultCaptures.Count != 0)
                    {
                        throw new InvalidOperationException(
                            "The fixture reached observation with an uncaptured stale result.");
                    }

                    await DrainDispatcherAsync(page, cancellationToken);
                    return step.Checkpoint!;
                default:
                    throw new InvalidOperationException(
                        "The fixture setup step has no runner route.");
            }

            if (invokedInteraction && step.InteractionId is not null)
            {
                session.Evidence.RecordInvokedSetupInteraction(
                    step.InteractionId);
            }

            if (stopCheckpoint is not null && string.Equals(
                    step.Checkpoint,
                    stopCheckpoint,
                    StringComparison.Ordinal))
            {
                if (!continueUntilNextInteraction)
                {
                    return stopCheckpoint;
                }

                stopReached = true;
            }
        }

        throw new InvalidOperationException(
            "The fixture scenario did not reach its observation checkpoint.");
    }

    private static async Task ReleaseServiceCheckpointAsync(
        ModelInspectionPage page,
        ModelInspectionFixtureSession session,
        ModelInspectionFixtureSetupStepDescriptor step,
        IDictionary<int, string> pendingResultCaptures,
        bool captureStaleResultSnapshots,
        CancellationToken cancellationToken)
    {
        RequireIdentity(step);
        int attempt = step.Attempt!.Value;
        string checkpoint = step.Checkpoint!;
        ModelInspectionFixtureServiceStepPlan planStep = session.Plan.Attempts
            .Single(candidate => candidate.Attempt == attempt)
            .ServiceSteps.Single(candidate => string.Equals(
                candidate.Checkpoint,
                checkpoint,
                StringComparison.Ordinal));
        ModelInspectionViewSnapshot? changed = null;
        if (planStep.Progress is not null || planStep.TerminalResult is not null)
        {
            ModelInspectionViewModel viewModel = RequireViewModel(page);
            changed = await ReleaseAndWaitForSnapshotAsync(
                viewModel,
                snapshot =>
                    snapshot.RenderKey.AttemptGeneration == attempt &&
                    (planStep.Progress is not null
                        ? ReferenceEquals(snapshot.Progress, planStep.Progress)
                        : ReferenceEquals(
                            snapshot.TerminalResult,
                            planStep.TerminalResult)),
                () => session.Service.ReleaseServiceCheckpoint(
                    attempt,
                    checkpoint),
                cancellationToken);
        }
        else
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.Service.ReleaseServiceCheckpoint(attempt, checkpoint);
        }

        if (planStep.DeferredEvent?.Kind ==
            ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot)
        {
            pendingResultCaptures.Add(
                attempt,
                planStep.DeferredEvent.CaptureCheckpoint);
        }

        if (changed is not null)
        {
            await DrainDispatcherAsync(page, cancellationToken);
            if (page.CurrentPresentation?.RenderKey != changed.RenderKey)
            {
                throw new InvalidOperationException(
                    "The fixture render did not reach the released checkpoint.");
            }

            if (captureStaleResultSnapshots &&
                changed.TerminalResult is not null &&
                pendingResultCaptures.Remove(
                    attempt,
                    out string? captureCheckpoint))
            {
                page.CaptureStaleResultSnapshotForFixture(
                    attempt,
                    captureCheckpoint);
            }
        }
        else
        {
            await DrainDispatcherAsync(page, cancellationToken);
        }
    }

    internal static async Task<ModelInspectionViewSnapshot>
        ReleaseAndWaitForSnapshotAsync(
            ModelInspectionViewModel viewModel,
            Predicate<ModelInspectionViewSnapshot> predicate,
            Action release,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(release);
        cancellationToken.ThrowIfCancellationRequested();
        using var waiterCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<ModelInspectionViewSnapshot> changed = WaitForSnapshotAsync(
            viewModel,
            predicate,
            waiterCancellation.Token);
        try
        {
            release();
        }
        catch (Exception error)
        {
            waiterCancellation.Cancel();
            try
            {
                await changed;
            }
            catch (OperationCanceledException) when (
                waiterCancellation.IsCancellationRequested)
            {
            }

            ExceptionDispatchInfo.Capture(error).Throw();
            throw;
        }

        return await changed;
    }

    private static void InvokeDisclosure(ModelInspectionPage page)
    {
        var model = (InspectionModelCard)page.FindName(
            "InspectionModelCardControl");
        var content = (InspectionContentCard)page.FindName(
            "InspectionContentCardControl");
        InspectionDisclosure[] active =
        [
            .. new[] { model.ActiveDisclosure, content.ActiveDisclosure }
                .OfType<InspectionDisclosure>()
        ];
        if (active.Length != 1)
        {
            throw new InvalidOperationException(
                "The fixture disclosure route is not uniquely active.");
        }

        var toggle = (Button)active[0].FindName("DisclosureToggleButton");
        var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(
            toggle);
        if (peer.GetPattern(
                Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke) is not
            Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider invoke)
        {
            throw new InvalidOperationException(
                "The fixture disclosure control is not invokable.");
        }

        invoke.Invoke();
    }

    private static ModelInspectionViewModel RequireViewModel(
        ModelInspectionPage page) => page.ViewModel ??
        throw new InvalidOperationException(
            "The fixture page has no active view model.");

    private static void ExecuteRequiredCommand(
        System.Windows.Input.ICommand command,
        string name)
    {
        if (!command.CanExecute(null))
        {
            throw new InvalidOperationException(
                $"The real {name} command is not available at its declared checkpoint.");
        }

        command.Execute(null);
    }

    private static void RequireIdentity(
        ModelInspectionFixtureSetupStepDescriptor step)
    {
        if (step.Attempt is null || string.IsNullOrWhiteSpace(step.Checkpoint))
        {
            throw new InvalidOperationException(
                "The fixture setup step has no exact attempt/checkpoint identity.");
        }
    }

    private static void RequireClosedObservationSequence(
        ValidatedModelInspectionFixtureInput input)
    {
        if (input.SetupSteps.Count == 0 ||
            input.SetupSteps[^1].Kind !=
                ModelInspectionFixtureSetupStepKind.Observe ||
            !string.Equals(
                input.SetupSteps[^1].Checkpoint,
                input.ObservationCheckpoint,
                StringComparison.Ordinal) ||
            input.SetupSteps.Count(step => step.Kind ==
                ModelInspectionFixtureSetupStepKind.Observe) != 1)
        {
            throw new InvalidOperationException(
                "The fixture scenario must end at exactly one named observation.");
        }
    }

    private static async Task WaitForLoadedAsync(
        FrameworkElement element,
        CancellationToken cancellationToken)
    {
        if (element.IsLoaded)
        {
            return;
        }

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler? handler = null;
        handler = (_, _) =>
        {
            element.Loaded -= handler;
            completion.TrySetResult(true);
        };
        element.Loaded += handler;
        try
        {
            if (element.IsLoaded)
            {
                completion.TrySetResult(true);
            }

            using CancellationTokenRegistration registration =
                cancellationToken.Register(() => completion.TrySetCanceled(
                    cancellationToken));
            await completion.Task;
        }
        finally
        {
            element.Loaded -= handler;
        }
    }

    private static void RequireServiceCallCount(
        ModelInspectionFixtureSession session,
        int expectedCount)
    {
        if (session.Evidence.ServiceCallCount != expectedCount)
        {
            throw new InvalidOperationException(
                "The fixture service did not reach the exact declared attempt boundary.");
        }
    }

    private static async Task<ModelInspectionViewSnapshot> WaitForSnapshotAsync(
        ModelInspectionViewModel viewModel,
        Predicate<ModelInspectionViewSnapshot> predicate,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<
            ModelInspectionViewSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler? handler = null;
        CancellationTokenRegistration registration = default;
        handler = (_, arguments) =>
        {
            if (arguments.PropertyName != nameof(
                    ModelInspectionViewModel.Snapshot))
            {
                return;
            }

            ModelInspectionViewSnapshot snapshot = viewModel.Snapshot;
            if (!predicate(snapshot))
            {
                return;
            }

            completion.TrySetResult(snapshot);
        };
        viewModel.PropertyChanged += handler;
        try
        {
            ModelInspectionViewSnapshot current = viewModel.Snapshot;
            if (predicate(current))
            {
                completion.TrySetResult(current);
            }

            registration = cancellationToken.Register(() =>
                completion.TrySetCanceled(cancellationToken));
            return await completion.Task;
        }
        finally
        {
            registration.Dispose();
            viewModel.PropertyChanged -= handler;
        }
    }

    private static async Task DrainDispatcherAsync(
        FrameworkElement element,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration =
            cancellationToken.Register(() => completion.TrySetCanceled(
                cancellationToken));
        if (!element.DispatcherQueue.TryEnqueue(
                () => completion.TrySetResult(true)))
        {
            throw new InvalidOperationException(
                "The fixture dispatcher rejected its deterministic barrier.");
        }

        await completion.Task;
        element.UpdateLayout();
    }
}
#endif
