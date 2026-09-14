using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using GraniteEdgeAI.Features.HardwareInspection.Orchestration;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
[TestCategory("HardwareInspectionGate8Acceptance")]
[DoNotParallelize]
public sealed class HardwareInspectionViewModelTests
{
    [TestMethod]
    [TestCategory("ProgressCompletion")]
    public async Task ProductionServiceStartedGroupsAreVisibleWithoutQueuedStageLag()
    {
        var watch = Stopwatch.StartNew();
        var trace = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var hidden = new List<HardwareInspectionRunStage>();
        var missingGroups = new List<string>();
        HardwareInspectionViewModel? model = null;
        int grouped = 0;
        var service = new ObservedProductionService(HardwareInspectionComposition.CreateProduction(), progress =>
        {
            var presentation = model!.Snapshot.Presentation;
            trace.Enqueue($"{watch.Elapsed.TotalMilliseconds:F1}ms received {progress.Sequence}/{progress.Stage} group={progress.CheckGroup}:{progress.GroupCompleted}/{progress.GroupTotal}; displayed={presentation.Title}; rows={string.Join(',', presentation.StageRows.Select(row => row.State))}");
            if (progress.CheckGroup is HardwareInspectionRunStage group)
            {
                grouped++;
                var key = Enum.Parse<HardwareInspectionStage>(group.ToString());
                if (!presentation.GroupChecks.TryGetValue(key, out var visible)
                    || visible.Completed != progress.GroupCompleted || visible.Total != progress.GroupTotal)
                    missingGroups.Add($"{progress.Sequence}/{group}:{progress.GroupCompleted}/{progress.GroupTotal}");
            }
            if ((int)progress.Stage is >= 1 and <= 4 && progress.CompletedChecks is null &&
                presentation.StageRows[(int)progress.Stage].State == HardwareInspectionStageRowState.Waiting)
                hidden.Add(progress.Stage);
        });
        model = new HardwareInspectionViewModel(service);
        model.SnapshotChanged += (_, _) => trace.Enqueue($"{watch.Elapsed.TotalMilliseconds:F1}ms displayed {model.Snapshot.Presentation.Title}");
        try
        {
            await model.ActivateAsync().WaitAsync(TimeSpan.FromSeconds(60));
            Assert.AreEqual(7, grouped, "Real service must emit all seven completed probe samples for this trace.");
            Assert.AreEqual(0, missingGroups.Count, "Accepted group counts must be visible immediately: " + string.Join(',', missingGroups));
            Assert.AreEqual(0, hidden.Count, "Already-started groups were hidden by presentation pacing: " + string.Join(',', hidden));
        }
        finally
        {
            model.Deactivate();
            foreach (string line in trace) Console.WriteLine(line);
        }
    }

    private sealed class ObservedProductionService(IHardwareInspectionService service, Action<HardwareInspectionRunProgress> observed) : IHardwareInspectionService
    {
        public Task<HardwareInspectionRunResult> RunAsync(Guid id, IProgress<HardwareInspectionRunProgress> progress, CancellationToken token) =>
            service.RunAsync(id, new InlineObservedProgress(value => { progress.Report(value); observed(value); }), token);
    }
    private sealed class InlineObservedProgress(Action<HardwareInspectionRunProgress> report) : IProgress<HardwareInspectionRunProgress>
    {
        public void Report(HardwareInspectionRunProgress value) => report(value);
    }
    [TestMethod]
    [TestCategory("EstimatedProgress")]
    public async Task RejectedGroupRegressionDoesNotConsumeSequenceOrAggregate()
    {
        var service = new ManualHardwareInspectionService();
        var model = Create(service);
        Task run = model.ActivateAsync();
        var call = service.Calls[0];
        try
        {
            call.Report(1, HardwareInspectionRunStage.StartingHardwareInspection);
            call.Report(2, HardwareInspectionRunStage.ReadingProcessorInformation);
            call.Report(3, HardwareInspectionRunStage.ReadingSystemMemory);
            call.Report(4, HardwareInspectionRunStage.DetectingGraphicsHardware);
            call.Report(5, HardwareInspectionRunStage.CheckingLocalInferenceRuntimes);
            call.ReportGroup(6, 1, HardwareInspectionRunStage.ReadingSystemMemory, 1, 2);
            long revision = model.Snapshot.Revision;
            call.ReportGroup(7, 2, HardwareInspectionRunStage.ReadingSystemMemory, 0, 2);
            Assert.AreEqual(revision, model.Snapshot.Revision);
            call.ReportGroup(7, 2, HardwareInspectionRunStage.ReadingSystemMemory, 2, 2);
            Assert.AreEqual(2, model.Snapshot.Presentation.CompletedChecks);
        }
        finally { call.Complete(HardwareInspectionRunResult.CreateCancelled(call.InspectionId)); await run; }
    }
    [TestMethod]
    [TestCategory("ChatTextInteractionsMeasured")]
    public async Task MeasuredChecksRejectStaleRegressionsWithoutAdvancingStage()
    {
        var service = new ManualHardwareInspectionService();
        var model = Create(service);
        Task run = model.ActivateAsync();
        ManualRun call = service.Calls[0];
        call.Report(1, HardwareInspectionRunStage.StartingHardwareInspection);
        await WaitForAsync(() => model.Snapshot.Presentation.Kind == HardwareInspectionPresentationKind.Active);
        call.ReportMeasured(call.InspectionId, 2, HardwareInspectionRunStage.StartingHardwareInspection, 3, 7);
        Assert.AreEqual(3, model.Snapshot.Presentation.CompletedChecks);
        Assert.AreEqual(0, model.Snapshot.Presentation.CompletedStageCount);
        long revision = model.Snapshot.Revision;
        call.ReportMeasured(Guid.NewGuid(), 3, HardwareInspectionRunStage.StartingHardwareInspection, 4, 7);
        call.ReportMeasured(call.InspectionId, 2, HardwareInspectionRunStage.StartingHardwareInspection, 4, 7);
        call.ReportMeasured(call.InspectionId, 3, HardwareInspectionRunStage.StartingHardwareInspection, 2, 7);
        call.ReportMeasured(call.InspectionId, 3, HardwareInspectionRunStage.StartingHardwareInspection, 4, 8);
        Assert.AreEqual(revision, model.Snapshot.Revision);
        call.ReportMeasured(call.InspectionId, 3, HardwareInspectionRunStage.StartingHardwareInspection, 4, 7);
        Assert.AreEqual(4, model.Snapshot.Presentation.CompletedChecks);
        call.Complete(HardwareInspectionRunResult.CreateCancelled(call.InspectionId));
        await run;
    }
    [TestMethod]
    public async Task Activate_StartsExactlyOnceAndPublishesStartingImmediately()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = Create(service);

        Task first = viewModel.ActivateAsync();
        Task second = viewModel.ActivateAsync();

        Assert.AreEqual(1, service.Calls.Count);
        Assert.IsTrue(viewModel.Snapshot.IsRunActive);
        Assert.AreEqual(service.Calls[0].InspectionId, viewModel.Snapshot.InspectionId);
        Assert.AreEqual(HardwareInspectionPresentationKind.Active,
            viewModel.Snapshot.Presentation.Kind);
        Assert.AreEqual("Starting hardware inspection",
            viewModel.Snapshot.Presentation.Title);

        service.Calls[0].Complete(HardwareInspectionRunResult.CreateCancelled(
            service.Calls[0].InspectionId));
        await Task.WhenAll(first, second);
        Assert.AreEqual(HardwareInspectionPresentationKind.Cancelled,
            viewModel.Snapshot.Presentation.Kind);
    }

    [TestMethod]
    public async Task Progress_AcceptsOnlyMatchingStrictlyOrderedEvents()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = Create(service);
        Task run = viewModel.ActivateAsync();
        ManualRun call = service.Calls[0];

        call.Report(1, HardwareInspectionRunStage.StartingHardwareInspection);
        call.Report(2, HardwareInspectionRunStage.ReadingProcessorInformation);
        await WaitForAsync(() => viewModel.Snapshot.Presentation.Title
            == "Reading processor information");
        long acceptedRevision = viewModel.Snapshot.Revision;

        call.Report(2, HardwareInspectionRunStage.ReadingSystemMemory);
        call.Report(1, HardwareInspectionRunStage.ReadingSystemMemory);
        call.ReportFor(Guid.NewGuid(), 3, HardwareInspectionRunStage.ReadingSystemMemory);
        call.Report(4, HardwareInspectionRunStage.DetectingGraphicsHardware);
        await Task.Delay(30);
        Assert.AreEqual(acceptedRevision, viewModel.Snapshot.Revision);

        call.Report(3, HardwareInspectionRunStage.ReadingSystemMemory);
        await WaitForAsync(() => viewModel.Snapshot.Presentation.Title
            == "Reading system memory");
        Assert.IsTrue(viewModel.Snapshot.Revision > acceptedRevision);

        call.Complete(HardwareInspectionRunResult.CreateCancelled(call.InspectionId));
        await run;
    }

    [TestMethod]
    public async Task CompletedRun_PublishesSummaryDetailsAndSameRunHandoff()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = Create(service);
        Task run = viewModel.ActivateAsync();
        ManualRun call = service.Calls[0];
        ReportAllStages(call);
        call.Complete(HardwareInspectionRunResult.CreateCompleted(
            call.InspectionId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(call.InspectionId)));

        await run;

        Assert.IsFalse(viewModel.Snapshot.IsRunActive);
        Assert.AreEqual(HardwareInspectionPresentationKind.Completed,
            viewModel.Snapshot.Presentation.Kind);
        Assert.IsNotNull(viewModel.Snapshot.Summary);
        Assert.IsNotNull(viewModel.Snapshot.Details);
        Assert.IsNotNull(viewModel.Snapshot.Handoff);
        Assert.AreEqual(call.InspectionId, viewModel.Snapshot.Handoff.InspectionId);
    }

    [TestMethod]
    public async Task CompletedResultWithoutAllStages_FailsClosedWithoutHandoff()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = Create(service);
        Task run = viewModel.ActivateAsync();
        ManualRun call = service.Calls[0];
        call.Report(1, HardwareInspectionRunStage.StartingHardwareInspection);
        call.Complete(HardwareInspectionRunResult.CreateCompleted(
            call.InspectionId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(call.InspectionId)));

        await run;

        Assert.AreEqual(
            HardwareInspectionPresentationKind.FailedApplicationRepairRequired,
            viewModel.Snapshot.Presentation.Kind);
        Assert.AreEqual(
            "HI-STAGE-SEQUENCE-INCOMPLETE",
            viewModel.Snapshot.SafeDiagnosticCode);
        Assert.IsNull(viewModel.Snapshot.Handoff);
        Assert.IsNull(viewModel.Snapshot.Summary);
    }

    [TestMethod]
    public async Task FailureAndServiceException_AreSafeAndNeverExposeHandoff()
    {
        foreach ((HardwareInspectionFailureKind failure,
                     HardwareInspectionPresentationKind expected) in new[]
                 {
                     (HardwareInspectionFailureKind.CriticalEvidence,
                         HardwareInspectionPresentationKind.FailedCriticalEvidence),
                     (HardwareInspectionFailureKind.TransientOperation,
                         HardwareInspectionPresentationKind.FailedTransientOperation),
                     (HardwareInspectionFailureKind.ApplicationRepairRequired,
                         HardwareInspectionPresentationKind.FailedApplicationRepairRequired),
                 })
        {
            ManualHardwareInspectionService service = new();
            HardwareInspectionViewModel viewModel = Create(service);
            Task run = viewModel.ActivateAsync();
            ManualRun call = service.Calls[0];
            call.Complete(HardwareInspectionRunResult.CreateFailed(
                call.InspectionId,
                failure,
                "HI-SAFE-FAILURE"));
            await run;

            Assert.AreEqual(expected, viewModel.Snapshot.Presentation.Kind);
            Assert.IsNull(viewModel.Snapshot.Handoff);
            Assert.IsNotNull(viewModel.Snapshot.Details);
        }

        ManualHardwareInspectionService throwingService = new();
        HardwareInspectionViewModel throwing = Create(throwingService);
        Task throwingRun = throwing.ActivateAsync();
        throwingService.Calls[0].Fail(new InvalidOperationException(
            @"C:\private\raw-tool-output"));
        await throwingRun;
        Assert.AreEqual(HardwareInspectionPresentationKind.FailedTransientOperation,
            throwing.Snapshot.Presentation.Kind);
        Assert.AreEqual("HI-OPERATION-FAILED", throwing.Snapshot.SafeDiagnosticCode);
        string safeDiagnosticCode = throwing.Snapshot.SafeDiagnosticCode!;
        Assert.IsFalse(safeDiagnosticCode.Contains("private",
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Cancel_IsIdempotentAndStoppingWinsUntilCancelledTerminal()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = Create(service);
        Task run = viewModel.ActivateAsync();
        ManualRun call = service.Calls[0];

        viewModel.Cancel();
        long stoppingRevision = viewModel.Snapshot.Revision;
        viewModel.Cancel();

        await WaitForAsync(() => call.CancellationToken.IsCancellationRequested);
        Assert.AreEqual(HardwareInspectionPresentationKind.Stopping,
            viewModel.Snapshot.Presentation.Kind);
        Assert.AreEqual(stoppingRevision, viewModel.Snapshot.Revision);
        call.Report(1, HardwareInspectionRunStage.StartingHardwareInspection);
        await Task.Delay(20);
        Assert.AreEqual(stoppingRevision, viewModel.Snapshot.Revision);

        call.Complete(HardwareInspectionRunResult.CreateCancelled(call.InspectionId));
        await run;
        Assert.AreEqual(HardwareInspectionPresentationKind.Cancelled,
            viewModel.Snapshot.Presentation.Kind);
        Assert.IsNull(viewModel.Snapshot.Handoff);
    }

    [TestMethod]
    public async Task AcceptedCancel_WinsOverAConcurrentSuccessfulServiceReturn()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = Create(service);
        Task run = viewModel.ActivateAsync();
        ManualRun call = service.Calls[0];

        viewModel.Cancel();
        Assert.AreEqual(HardwareInspectionPresentationKind.Stopping,
            viewModel.Snapshot.Presentation.Kind);

        call.Complete(HardwareInspectionRunResult.CreateCompleted(
            call.InspectionId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                call.InspectionId)));
        await run;

        Assert.AreEqual(HardwareInspectionPresentationKind.Cancelled,
            viewModel.Snapshot.Presentation.Kind);
        Assert.IsFalse(viewModel.Snapshot.IsRunActive);
        Assert.IsNull(viewModel.Snapshot.Handoff);
        Assert.IsNull(viewModel.Snapshot.Summary);
    }

    [TestMethod]
    public async Task AcceptedCancellation_ReturnsBeforeExactlyOneWorkerSignal()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = Create(service);
        Task run = viewModel.ActivateAsync();
        ManualRun call = service.Calls[0];
        int cancellationCallbacks = 0;
        using CancellationTokenRegistration registration =
            call.CancellationToken.Register(
                () => Interlocked.Increment(ref cancellationCallbacks));

        long generation = viewModel.Snapshot.AttemptGeneration;
        Assert.IsTrue(viewModel.TryCancelAttempt(generation, out var stopping));
        Assert.IsNotNull(stopping);
        Assert.AreEqual(
            HardwareInspectionPresentationKind.Stopping,
            stopping.Presentation.Kind);
        Assert.AreEqual(
            0,
            Volatile.Read(ref cancellationCallbacks),
            "Attempt acceptance must return before token callbacks are dispatched.");
        Assert.IsFalse(call.CancellationToken.IsCancellationRequested);

        viewModel.DispatchAcceptedCancellation(generation);
        viewModel.DispatchAcceptedCancellation(generation);
        await WaitForAsync(() => Volatile.Read(ref cancellationCallbacks) == 1);
        await Task.Delay(30);
        Assert.AreEqual(1, Volatile.Read(ref cancellationCallbacks));

        call.Complete(HardwareInspectionRunResult.CreateCompleted(
            call.InspectionId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                call.InspectionId)));
        await run;

        Assert.AreEqual(
            HardwareInspectionPresentationKind.Cancelled,
            viewModel.Snapshot.Presentation.Kind);
        Assert.IsNull(viewModel.Snapshot.Handoff);
        Assert.IsNull(viewModel.Snapshot.Summary);
    }

    [TestMethod]
    public async Task VisibleAttemptMaySuppressSuccessUntilItsTerminalIsPresented()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = Create(service);
        Task run = viewModel.ActivateAsync();
        ManualRun call = service.Calls[0];
        ReportAllStages(call);
        call.Complete(HardwareInspectionRunResult.CreateCompleted(
            call.InspectionId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                call.InspectionId)));
        await run;
        long generation = viewModel.Snapshot.AttemptGeneration;
        Assert.AreEqual(HardwareInspectionPresentationKind.Completed,
            viewModel.Snapshot.Presentation.Kind);

        Assert.IsTrue(viewModel.TryCancelAttempt(generation));
        Assert.AreEqual(HardwareInspectionPresentationKind.Stopping,
            viewModel.Snapshot.Presentation.Kind);
        viewModel.CompletePendingSuccessfulPresentationCancellation(generation);

        Assert.AreEqual(HardwareInspectionPresentationKind.Cancelled,
            viewModel.Snapshot.Presentation.Kind);
        Assert.IsNull(viewModel.Snapshot.Handoff);
        Assert.IsNull(viewModel.Snapshot.Summary);
    }

    [TestMethod]
    public async Task Retry_CreatesNewIdentityAndOldRunCannotMutateWinner()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = Create(service);
        Task first = viewModel.ActivateAsync();
        ManualRun oldRun = service.Calls[0];

        Task retry = viewModel.RetryAsync();
        Assert.AreEqual(2, service.Calls.Count);
        ManualRun winner = service.Calls[1];
        Assert.AreNotEqual(oldRun.InspectionId, winner.InspectionId);
        long winnerRevision = viewModel.Snapshot.Revision;

        oldRun.Report(1, HardwareInspectionRunStage.StartingHardwareInspection);
        oldRun.Complete(HardwareInspectionRunResult.CreateCompleted(
            oldRun.InspectionId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(oldRun.InspectionId)));
        await first;
        Assert.AreEqual(winner.InspectionId, viewModel.Snapshot.InspectionId);
        Assert.AreEqual(winnerRevision, viewModel.Snapshot.Revision);

        winner.Complete(HardwareInspectionRunResult.CreateCancelled(winner.InspectionId));
        await retry;
    }

    [TestMethod]
    public async Task Deactivate_CancelsAndRejectsEveryLaterMutation()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = Create(service);
        Task run = viewModel.ActivateAsync();
        ManualRun call = service.Calls[0];

        viewModel.Deactivate();
        long retiredRevision = viewModel.Snapshot.Revision;
        await WaitForAsync(() => call.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(viewModel.Snapshot.IsRunActive);

        call.Report(1, HardwareInspectionRunStage.StartingHardwareInspection);
        call.Complete(HardwareInspectionRunResult.CreateCancelled(call.InspectionId));
        await run;
        Assert.AreEqual(retiredRevision, viewModel.Snapshot.Revision);
    }

    private static HardwareInspectionViewModel Create(
        ManualHardwareInspectionService service) => new(
            service,
            new ImmediateHardwareInspectionStagePacer());

    private static void ReportAllStages(ManualRun call)
    {
        long sequence = 0;
        foreach (HardwareInspectionRunStage stage in
                 Enum.GetValues<HardwareInspectionRunStage>())
        {
            call.Report(++sequence, stage);
        }
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
        while (!predicate())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}

internal sealed class ImmediateHardwareInspectionStagePacer
    : IHardwareInspectionStagePacer
{
    public Task WaitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class ManualHardwareInspectionService : IHardwareInspectionService
{
    internal List<ManualRun> Calls { get; } = [];

    public Task<HardwareInspectionRunResult> RunAsync(
        Guid inspectionId,
        IProgress<HardwareInspectionRunProgress> progress,
        CancellationToken cancellationToken)
    {
        ManualRun call = new(inspectionId, progress, cancellationToken);
        Calls.Add(call);
        return call.Completion.Task;
    }
}

internal sealed class ManualRun
{
    internal void ReportGroup(long sequence, int allCompleted, HardwareInspectionRunStage group, int completed, int total) =>
        _progress.Report(new HardwareInspectionRunProgress(InspectionId, sequence, HardwareInspectionRunStage.CheckingLocalInferenceRuntimes, allCompleted, 7)
            .WithGroup(group, completed, total));
    private readonly IProgress<HardwareInspectionRunProgress> _progress;

    internal ManualRun(
        Guid inspectionId,
        IProgress<HardwareInspectionRunProgress> progress,
        CancellationToken cancellationToken)
    {
        InspectionId = inspectionId;
        _progress = progress;
        CancellationToken = cancellationToken;
    }

    internal Guid InspectionId { get; }
    internal CancellationToken CancellationToken { get; }
    internal TaskCompletionSource<HardwareInspectionRunResult> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void Report(long sequence, HardwareInspectionRunStage stage) =>
        ReportFor(InspectionId, sequence, stage);

    internal void ReportMeasured(Guid id, long sequence, HardwareInspectionRunStage stage, int completed, int total) =>
        _progress.Report(new HardwareInspectionRunProgress(id, sequence, stage, completed, total));

    internal void ReportFor(
        Guid inspectionId,
        long sequence,
        HardwareInspectionRunStage stage) =>
        _progress.Report(new HardwareInspectionRunProgress(
            inspectionId,
            sequence,
            stage));

    internal void Complete(HardwareInspectionRunResult result) =>
        Completion.TrySetResult(result);

    internal void Fail(Exception exception) => Completion.TrySetException(exception);
}
