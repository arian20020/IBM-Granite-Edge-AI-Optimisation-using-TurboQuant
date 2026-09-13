using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
public sealed class HardwareInspectionRunContractTests
{
    [TestMethod]
    [TestCategory("ChatTextInteractionsMeasured")]
    public void MeasuredProgressRequiresValidCountsAndKeepsPublicUnknownDefault()
    {
        Guid id = Guid.NewGuid();
        var unknown = new HardwareInspectionRunProgress(id, 1, HardwareInspectionRunStage.CheckingLocalInferenceRuntimes);
        Assert.IsNull(unknown.CompletedChecks);
        var measured = new HardwareInspectionRunProgress(id, 2, unknown.Stage, 3, 7);
        Assert.AreEqual(3, measured.CompletedChecks);
        Assert.AreEqual(7, measured.TotalChecks);
        Assert.Throws<ArgumentOutOfRangeException>(() => new HardwareInspectionRunProgress(id, 3, unknown.Stage, -1, 7));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HardwareInspectionRunProgress(id, 3, unknown.Stage, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HardwareInspectionRunProgress(id, 3, unknown.Stage, 8, 7));
    }
    [TestMethod]
    public void RunStages_AreExactlyTheApprovedSevenInOrder()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "StartingHardwareInspection",
                "ReadingProcessorInformation",
                "ReadingSystemMemory",
                "DetectingGraphicsHardware",
                "CheckingLocalInferenceRuntimes",
                "NormalisingHardwareInformation",
                "CreatingHardwareReport",
            },
            Enum.GetNames<HardwareInspectionRunStage>());
    }

    [TestMethod]
    public void Progress_RequiresRunIdentityPositiveSequenceAndDeclaredStage()
    {
        Guid runId = Guid.NewGuid();
        HardwareInspectionRunProgress progress = new(
            runId,
            7,
            HardwareInspectionRunStage.DetectingGraphicsHardware);

        Assert.AreEqual(runId, progress.InspectionId);
        Assert.AreEqual(7L, progress.Sequence);
        Assert.AreEqual(
            HardwareInspectionRunStage.DetectingGraphicsHardware,
            progress.Stage);
        Assert.Throws<ArgumentException>(() => new HardwareInspectionRunProgress(
            Guid.Empty,
            1,
            HardwareInspectionRunStage.StartingHardwareInspection));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HardwareInspectionRunProgress(
            runId,
            0,
            HardwareInspectionRunStage.StartingHardwareInspection));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HardwareInspectionRunProgress(
            runId,
            1,
            (HardwareInspectionRunStage)99));
    }

    [TestMethod]
    public void CompletedResults_CreateOnlyUsableSameRunHandoffs()
    {
        foreach (HardwareInspectionOutcome outcome in new[]
                 {
                     HardwareInspectionOutcome.Completed,
                     HardwareInspectionOutcome.CompletedWithWarnings,
                 })
        {
            Guid runId = Guid.NewGuid();
            HardwareSnapshot snapshot = HardwareInspectionContractTests
                .CreateUsableSnapshotForPresentation(runId);
            HardwareInspectionRunResult result =
                HardwareInspectionRunResult.CreateCompleted(runId, outcome, snapshot);

            Assert.AreEqual(runId, result.InspectionId);
            Assert.AreEqual(outcome, result.Outcome);
            Assert.AreSame(snapshot, result.Snapshot);
            Assert.IsNull(result.FailureKind);
            Assert.IsNull(result.SafeDiagnosticCode);
            Assert.IsNotNull(result.Handoff);
            Assert.AreEqual(runId, result.Handoff.InspectionId);
            Assert.AreSame(snapshot, result.Handoff.Snapshot);
        }

        Guid invalidRunId = Guid.NewGuid();
        HardwareSnapshot invalidOutcomeSnapshot = HardwareInspectionContractTests
            .CreateUsableSnapshotForPresentation(invalidRunId);
        Assert.Throws<ArgumentException>(() =>
            HardwareInspectionRunResult.CreateCompleted(
                invalidRunId,
                HardwareInspectionOutcome.Failed,
                invalidOutcomeSnapshot));
    }

    [TestMethod]
    public void DisplayOnlyCompletedResult_PreservesFactsWithoutActionableHandoff()
    {
        Guid runId = Guid.NewGuid();
        HardwareSnapshot usable = HardwareInspectionContractTests
            .CreateUsableSnapshotForPresentation(runId);
        HardwareSnapshot snapshot = new(
            usable.SnapshotId,
            usable.CapturedAtUtc,
            usable.SchemaVersion,
            usable.PolicyVersion,
            usable.Processor,
            usable.Memory,
            usable.GraphicsAdapters,
            usable.NeuralProcessor,
            usable.Storage,
            usable.OperatingSystem,
            usable.LocalRuntime,
            usable.Evidence,
            HardwareSnapshotUsability.DisplayOnly);

        HardwareInspectionRunResult result = HardwareInspectionRunResult.CreateCompleted(
            runId,
            HardwareInspectionOutcome.CompletedWithWarnings,
            snapshot);

        Assert.AreSame(snapshot, result.Snapshot);
        Assert.AreEqual(HardwareSnapshotUsability.DisplayOnly, result.Snapshot!.Usability);
        Assert.IsNull(result.Handoff);

        Assert.Throws<ArgumentException>(() => HardwareInspectionRunResult.CreateCompleted(
            runId,
            HardwareInspectionOutcome.Completed,
            snapshot));

        HardwareSnapshot undefined = new(
            usable.SnapshotId,
            usable.CapturedAtUtc,
            usable.SchemaVersion,
            usable.PolicyVersion,
            usable.Processor,
            usable.Memory,
            usable.GraphicsAdapters,
            usable.NeuralProcessor,
            usable.Storage,
            usable.OperatingSystem,
            usable.LocalRuntime,
            usable.Evidence,
            (HardwareSnapshotUsability)99);
        Assert.Throws<ArgumentException>(() => HardwareInspectionRunResult.CreateCompleted(
            runId,
            HardwareInspectionOutcome.CompletedWithWarnings,
            undefined));
    }

    [TestMethod]
    public void FailedAndCancelledResults_NeverCreateActionableHandoffs()
    {
        Guid failedId = Guid.NewGuid();
        HardwareInspectionRunResult failed = HardwareInspectionRunResult.CreateFailed(
            failedId,
            HardwareInspectionFailureKind.TransientOperation,
            "HI-OPERATION-FAILED");

        Assert.AreEqual(failedId, failed.InspectionId);
        Assert.AreEqual(HardwareInspectionOutcome.Failed, failed.Outcome);
        Assert.AreEqual(HardwareInspectionFailureKind.TransientOperation, failed.FailureKind);
        Assert.AreEqual("HI-OPERATION-FAILED", failed.SafeDiagnosticCode);
        Assert.IsNull(failed.Snapshot);
        Assert.IsNull(failed.Handoff);

        Guid cancelledId = Guid.NewGuid();
        HardwareInspectionRunResult cancelled =
            HardwareInspectionRunResult.CreateCancelled(cancelledId);
        Assert.AreEqual(cancelledId, cancelled.InspectionId);
        Assert.AreEqual(HardwareInspectionOutcome.Cancelled, cancelled.Outcome);
        Assert.IsNull(cancelled.Snapshot);
        Assert.IsNull(cancelled.FailureKind);
        Assert.IsNull(cancelled.SafeDiagnosticCode);
        Assert.IsNull(cancelled.Handoff);

        Assert.Throws<ArgumentException>(() => HardwareInspectionRunResult.CreateFailed(
            Guid.Empty,
            HardwareInspectionFailureKind.CriticalEvidence,
            "HI-EVIDENCE-MISSING"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HardwareInspectionRunResult.CreateFailed(
                Guid.NewGuid(),
                (HardwareInspectionFailureKind)99,
                "HI-EVIDENCE-MISSING"));
        Assert.Throws<ArgumentException>(() => HardwareInspectionRunResult.CreateFailed(
            Guid.NewGuid(),
            HardwareInspectionFailureKind.CriticalEvidence,
            " "));
        Assert.Throws<ArgumentException>(() =>
            HardwareInspectionRunResult.CreateCancelled(Guid.Empty));
    }

    [TestMethod]
    public void Service_ExposesOnlyTheTypedOneRunBoundary()
    {
        MethodInfo[] methods = typeof(IHardwareInspectionService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert.AreEqual(1, methods.Length);
        MethodInfo method = methods.Single();
        Assert.AreEqual("RunAsync", method.Name);
        Assert.AreEqual(typeof(Task<HardwareInspectionRunResult>), method.ReturnType);
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(Guid),
                typeof(IProgress<HardwareInspectionRunProgress>),
                typeof(CancellationToken),
            },
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }
}
