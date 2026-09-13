using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
[TestCategory("HardwareInspectionGate8Acceptance")]
[DoNotParallelize]
public sealed class HardwareInspectionJourneyTests
{
    [TestMethod]
    public async Task DeterministicJourney_TraversesExactStagesAndEveryTerminalFamily()
    {
        var scenarios = new (Func<Guid, HardwareInspectionRunResult> Result,
            HardwareInspectionPresentationKind Expected)[]
        {
            (id => HardwareInspectionRunResult.CreateCompleted(
                    id,
                    HardwareInspectionOutcome.Completed,
                    HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(id)),
                HardwareInspectionPresentationKind.Completed),
            (id => HardwareInspectionRunResult.CreateCompleted(
                    id,
                    HardwareInspectionOutcome.CompletedWithWarnings,
                    HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(id)),
                HardwareInspectionPresentationKind.CompletedWithWarnings),
            (id => HardwareInspectionRunResult.CreateFailed(
                    id,
                    HardwareInspectionFailureKind.CriticalEvidence,
                    "HI-EVIDENCE-MISSING"),
                HardwareInspectionPresentationKind.FailedCriticalEvidence),
            (id => HardwareInspectionRunResult.CreateFailed(
                    id,
                    HardwareInspectionFailureKind.TransientOperation,
                    "HI-OPERATION-FAILED"),
                HardwareInspectionPresentationKind.FailedTransientOperation),
            (id => HardwareInspectionRunResult.CreateFailed(
                    id,
                    HardwareInspectionFailureKind.ApplicationRepairRequired,
                    "HI-INTEGRITY-FAILED"),
                HardwareInspectionPresentationKind.FailedApplicationRepairRequired),
            (HardwareInspectionRunResult.CreateCancelled,
                HardwareInspectionPresentationKind.Cancelled),
        };
        string[] expectedStages = Enum.GetValues<HardwareInspectionStage>()
            .Select(stage => HardwareInspectionCopyCatalog.Stage(stage).Title)
            .ToArray();

        foreach ((Func<Guid, HardwareInspectionRunResult> resultFactory,
                     HardwareInspectionPresentationKind expected) in scenarios)
        {
            DeterministicHardwareInspectionService service = new(resultFactory);
            CountingHardwareInspectionStagePacer pacer = new();
            HardwareInspectionViewModel viewModel = new(service, pacer);
            List<string> activeTitles = [];
            viewModel.SnapshotChanged += (_, _) =>
            {
                if (viewModel.Snapshot.Presentation.Kind
                    == HardwareInspectionPresentationKind.Active)
                {
                    string title = viewModel.Snapshot.Presentation.Title;
                    if (activeTitles.Count == 0 || activeTitles[^1] != title)
                    {
                        activeTitles.Add(title);
                    }
                }
            };

            await viewModel.ActivateAsync();

            CollectionAssert.AreEqual(expectedStages, activeTitles.ToArray());
            Assert.AreEqual(7, pacer.WaitCount);
            Assert.AreEqual(expected, viewModel.Snapshot.Presentation.Kind);
            Assert.IsFalse(viewModel.Snapshot.IsRunActive);
        }
    }

    [TestMethod]
    public async Task DeterministicJourney_ProducesOnlyFixedPrivacySafeDiagnostics()
    {
        DeterministicHardwareInspectionService service = new(id =>
            HardwareInspectionRunResult.CreateFailed(
                id,
                HardwareInspectionFailureKind.TransientOperation,
                "HI-OPERATION-FAILED"));
        HardwareInspectionViewModel viewModel = new(
            service,
            new CountingHardwareInspectionStagePacer());

        await viewModel.ActivateAsync();

        Assert.AreEqual("HI-OPERATION-FAILED", viewModel.Snapshot.SafeDiagnosticCode);
        Assert.IsFalse(viewModel.Snapshot.Details!.TechnicalGroups
            .SelectMany(group => group.Items)
            .Any(item => item.Value.Contains('\\') || item.Value.Contains('/')));
        Assert.IsNull(viewModel.Snapshot.Handoff);

        Type? productionType = typeof(IHardwareInspectionService).Assembly.GetType(
            "GraniteEdgeAI.Features.HardwareInspection.Orchestration.HardwareInspectionService",
            throwOnError: false);
        Assert.IsNotNull(productionType);
    }

    [TestMethod]
    public void PackagedTestBoundaryContainsNoCandidateOrGateEvidence()
    {
        string[] packagedFiles = Directory.GetFiles(
            AppContext.BaseDirectory,
            "*",
            SearchOption.AllDirectories);

        Assert.IsFalse(packagedFiles.Any(path =>
            Path.GetFileName(path).Equals("llmfit.exe", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(packagedFiles.Any(path =>
            Path.GetExtension(path).Equals(".trx", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(packagedFiles.Any(path =>
            Path.GetFileName(path).Contains(
                "hardware-inspection-gate1",
                StringComparison.OrdinalIgnoreCase)));
    }
}

internal sealed class DeterministicHardwareInspectionService
    : IHardwareInspectionService
{
    private readonly Func<Guid, HardwareInspectionRunResult> _resultFactory;

    internal DeterministicHardwareInspectionService(
        Func<Guid, HardwareInspectionRunResult> resultFactory) =>
        _resultFactory = resultFactory;

    public Task<HardwareInspectionRunResult> RunAsync(
        Guid inspectionId,
        IProgress<HardwareInspectionRunProgress> progress,
        CancellationToken cancellationToken)
    {
        long sequence = 0;
        foreach (HardwareInspectionRunStage stage in
                 Enum.GetValues<HardwareInspectionRunStage>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new HardwareInspectionRunProgress(
                inspectionId,
                ++sequence,
                stage));
        }

        return Task.FromResult(_resultFactory(inspectionId));
    }
}

internal sealed class CountingHardwareInspectionStagePacer
    : IHardwareInspectionStagePacer
{
    internal int WaitCount { get; private set; }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WaitCount++;
        return Task.CompletedTask;
    }
}
