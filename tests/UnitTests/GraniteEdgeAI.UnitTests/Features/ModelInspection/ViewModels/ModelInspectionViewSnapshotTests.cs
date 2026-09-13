using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelInspectionViewSnapshotTests
{
    [TestMethod]
    public void RenderKey_RejectsNegativeComponents()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ModelInspectionRenderKey(-1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ModelInspectionRenderKey(0, -1));
    }

    [TestMethod]
    public void Initial_IsInactiveAtZeroGenerationAndRevision()
    {
        ModelInspectionViewSnapshot snapshot =
            ModelInspectionViewSnapshot.Initial;

        Assert.AreEqual(new ModelInspectionRenderKey(0, 0), snapshot.RenderKey);
        Assert.IsFalse(snapshot.IsRunActive);
        Assert.IsFalse(snapshot.IsCancellationRequested);
        Assert.IsNull(snapshot.Progress);
        Assert.IsNull(snapshot.TerminalResult);
    }

    [TestMethod]
    public void Snapshot_RejectsActiveTerminalState()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(1, 1),
                isRunActive: true,
                isCancellationRequested: false,
                progress: null,
                terminalResult: CreateFailureResult()));
    }

    [TestMethod]
    public void Snapshot_RejectsProgressAndTerminalState()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(1, 1),
                isRunActive: false,
                isCancellationRequested: false,
                progress: CreateProgress(),
                terminalResult: CreateFailureResult()));
    }

    [TestMethod]
    public void Snapshot_RejectsCancellationWithoutActiveRun()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(1, 1),
                isRunActive: false,
                isCancellationRequested: true,
                progress: null,
                terminalResult: null));
    }

    [TestMethod]
    public void Snapshot_RejectsProgressWithoutActiveRun()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(1, 1),
                isRunActive: false,
                isCancellationRequested: false,
                progress: CreateProgress(),
                terminalResult: null));
    }

    private static ModelInspectionProgress CreateProgress() =>
        new(
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completedStageCount: 1,
            totalStageCount: 5,
            stageFraction: null,
            userMessage: "Inspection is running.");

    private static ModelInspectionExecutionResult CreateFailureResult() =>
        ModelInspectionExecutionResult.OperationalFailure(
            new ModelInspectionOperationalFailure(
                code: "MI-OP-SNAPSHOT",
                userMessage: "Model inspection could not be completed.",
                technicalDetail: "Controlled test failure."));
}
