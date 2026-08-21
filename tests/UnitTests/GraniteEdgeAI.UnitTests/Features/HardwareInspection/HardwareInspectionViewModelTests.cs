using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
[DoNotParallelize]
public sealed class HardwareInspectionViewModelTests
{
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
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation()));

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

        Assert.IsTrue(call.CancellationToken.IsCancellationRequested);
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
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation()));
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
