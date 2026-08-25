using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
[TestCategory("HardwareInspectionGate8Acceptance")]
public sealed class HardwareInspectionPresentationContractTests
{
    private readonly HardwareInspectionPresentationFactory _factory = new();

    [TestMethod]
    [TestCategory("Unit")]
    public void ActiveStates_UseExactSevenStagesAndTruthfulCounts()
    {
        HardwareInspectionStage[] stages = Enum.GetValues<HardwareInspectionStage>();
        CollectionAssert.AreEqual(
            new[]
            {
                "Starting hardware inspection",
                "Reading processor information",
                "Reading system memory",
                "Detecting graphics hardware",
                "Checking local inference runtimes",
                "Normalising hardware information",
                "Creating the hardware report",
            },
            stages.Select(stage => HardwareInspectionCopyCatalog.Stage(stage).Title).ToArray());

        for (int index = 0; index < stages.Length; index++)
        {
            HardwareInspectionPresentationState state = _factory.CreateActive(stages[index]);
            Assert.AreEqual(HardwareInspectionPresentationKind.Active, state.Kind);
            Assert.AreEqual(index, state.CompletedStageCount);
            Assert.AreEqual(7, state.StageRows.Count);
            Assert.AreEqual(1, state.StageRows.Count(row => row.State == HardwareInspectionStageRowState.Active));
            Assert.AreEqual(index, state.StageRows.Count(row => row.State == HardwareInspectionStageRowState.Complete));
            Assert.AreEqual(6 - index, state.StageRows.Count(row => row.State == HardwareInspectionStageRowState.Waiting));
            Assert.AreEqual(stages[index], state.StageRows.Single(row => row.State == HardwareInspectionStageRowState.Active).Stage);
            Assert.IsFalse(state.DetailsAvailable);
            Assert.IsFalse(state.ReportCreated);
            AssertAction(state, HardwareInspectionActionKind.CancelInspection, visible: true, enabled: true);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void StageCopy_IsExactForActiveCompleteAndWaitingRows()
    {
        HardwareInspectionStageCopy memory = HardwareInspectionCopyCatalog.Stage(HardwareInspectionStage.ReadingSystemMemory);
        Assert.AreEqual(
            "Reading installed, Windows-usable, and currently available memory as separate values.",
            memory.ActiveExplanation);
        Assert.AreEqual(
            "Installed, usable, and currently available memory were collected separately.",
            memory.CompletedSentence);
        Assert.AreEqual("Starts after processor information is read.", memory.WaitingSentence);

        HardwareInspectionPresentationState graphics = _factory.CreateActive(HardwareInspectionStage.DetectingGraphicsHardware);
        HardwareInspectionStageRow active = graphics.StageRows.Single(row => row.State == HardwareInspectionStageRowState.Active);
        Assert.AreEqual(
            "Active — Detecting graphics hardware. Checking graphics devices and keeping dedicated and shared memory separate. State: Active.",
            active.AccessibleName);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void InvalidHandoff_IsBoundedAndStartsNoRunSurface()
    {
        HardwareInspectionPresentationState state = _factory.CreateInvalidHandoff();
        Assert.AreEqual(HardwareInspectionPresentationKind.InvalidHandoff, state.Kind);
        Assert.AreEqual("Hardware inspection could not open safely.", state.Subtitle);
        Assert.AreEqual("Navigation problem", state.Kicker);
        Assert.AreEqual("Hardware inspection could not open", state.Title);
        Assert.AreEqual(
            "The information needed to begin this page was missing or could not be read safely. No inspection was started. Return to model inspection and try the journey again.",
            state.Body);
        Assert.AreEqual(0, state.StageRows.Count);
        Assert.IsFalse(state.DetailsAvailable);
        Assert.IsFalse(state.ReportCreated);
        CollectionAssert.AreEqual(
            new[] { HardwareInspectionActionKind.BackToModelInspection },
            state.Actions.Select(action => action.Kind).ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Stopping_HasNoReportDetailsOrEnabledAction()
    {
        HardwareInspectionPresentationState state = _factory.CreateStopping();
        Assert.AreEqual("Your cancellation request was received.", state.Subtitle);
        Assert.AreEqual("Stopping inspection", state.Kicker);
        Assert.AreEqual("Closing the inspection safely...", state.Title);
        Assert.AreEqual("No hardware report will be created from this run.", state.Body);
        Assert.IsFalse(state.DetailsAvailable);
        Assert.IsFalse(state.ReportCreated);
        AssertAction(state, HardwareInspectionActionKind.Stopping, visible: true, enabled: false);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Completed_UsesExactCleanCopyAndTwoConditionContinueGate()
    {
        HardwareInspectionPresentationState blocked = _factory.CreateTerminal(
            HardwareInspectionOutcome.Completed,
            hasUsableHandoff: true,
            block3RouteRegistered: false);
        Assert.AreEqual("This computer’s hardware information is ready.", blocked.Subtitle);
        Assert.AreEqual("Inspection complete", blocked.Kicker);
        Assert.AreEqual("Your hardware information is ready", blocked.Title);
        Assert.AreEqual(
            "The hardware report was created from the reliable information collected on this device.",
            blocked.Body);
        Assert.IsTrue(blocked.DetailsAvailable);
        Assert.IsTrue(blocked.ReportCreated);
        AssertAction(blocked, HardwareInspectionActionKind.ContinueToCompatibility, visible: true, enabled: false);
        Assert.AreEqual(
            HardwareInspectionCopyCatalog.ContinueUnavailableHelp,
            blocked.Actions.Single(action => action.Kind == HardwareInspectionActionKind.ContinueToCompatibility).AccessibleHelp);

        HardwareInspectionPresentationState enabled = _factory.CreateTerminal(
            HardwareInspectionOutcome.Completed,
            hasUsableHandoff: true,
            block3RouteRegistered: true);
        AssertAction(enabled, HardwareInspectionActionKind.ContinueToCompatibility, visible: true, enabled: true);

        HardwareInspectionPresentationState missingHandoff = _factory.CreateTerminal(
            HardwareInspectionOutcome.Completed,
            hasUsableHandoff: false,
            block3RouteRegistered: true);
        AssertAction(missingHandoff, HardwareInspectionActionKind.ContinueToCompatibility, visible: true, enabled: false);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Warning_UsesOneReviewAndOneResolvedInformationItem()
    {
        HardwareInspectionPresentationState state = _factory.CreateTerminal(
            HardwareInspectionOutcome.CompletedWithWarnings,
            hasUsableHandoff: true,
            block3RouteRegistered: false);

        Assert.AreEqual(HardwareInspectionPresentationKind.CompletedWithWarnings, state.Kind);
        Assert.AreEqual("Your computer's hardware information is ready to review.", state.Subtitle);
        Assert.AreEqual("Inspection complete · Review recommended", state.Kicker);
        Assert.AreEqual(
            "The neural processor check could not be confirmed. A small memory-source difference was resolved safely. The hardware report was still created.",
            state.Body);
        Assert.AreEqual(1, state.UnresolvedReviewCount);
        Assert.AreEqual(1, state.ResolvedInformationCount);
        Assert.IsTrue(state.ReportCreated);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FailureClasses_UseExactRecoveryActionsAndNeverContinue()
    {
        HardwareInspectionPresentationState critical = _factory.CreateTerminal(
            HardwareInspectionOutcome.Failed,
            HardwareInspectionFailureClass.CriticalEvidence,
            criticalFailureRetryable: false);
        Assert.AreEqual("Essential hardware information is missing", critical.Title);
        CollectionAssert.AreEqual(
            new[] { HardwareInspectionActionKind.Back },
            critical.Actions.Select(action => action.Kind).ToArray());

        HardwareInspectionPresentationState retryableCritical = _factory.CreateTerminal(
            HardwareInspectionOutcome.Failed,
            HardwareInspectionFailureClass.CriticalEvidence,
            criticalFailureRetryable: true);
        CollectionAssert.AreEquivalent(
            new[] { HardwareInspectionActionKind.Back, HardwareInspectionActionKind.RunInspectionAgain },
            retryableCritical.Actions.Select(action => action.Kind).ToArray());

        HardwareInspectionPresentationState transient = _factory.CreateTerminal(
            HardwareInspectionOutcome.Failed,
            HardwareInspectionFailureClass.TransientOperation);
        Assert.AreEqual("The inspection could not start", transient.Title);
        CollectionAssert.AreEqual(
            new[] { HardwareInspectionActionKind.Back, HardwareInspectionActionKind.TryAgain },
            transient.Actions.Select(action => action.Kind).ToArray());

        HardwareInspectionPresentationState repair = _factory.CreateTerminal(
            HardwareInspectionOutcome.Failed,
            HardwareInspectionFailureClass.ApplicationRepairRequired);
        Assert.AreEqual("The inspection tool could not be verified", repair.Title);
        CollectionAssert.AreEqual(
            new[] { HardwareInspectionActionKind.BackToModelInspection },
            repair.Actions.Select(action => action.Kind).ToArray());

        foreach (HardwareInspectionPresentationState state in new[] { critical, transient, repair })
        {
            Assert.IsFalse(state.ReportCreated);
            Assert.IsFalse(state.Actions.Any(action => action.Kind == HardwareInspectionActionKind.ContinueToCompatibility));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Cancelled_IsNeutralAndRetryableOnlyAfterCleanupState()
    {
        HardwareInspectionPresentationState state = _factory.CreateTerminal(HardwareInspectionOutcome.Cancelled);
        Assert.AreEqual(HardwareInspectionPresentationKind.Cancelled, state.Kind);
        Assert.AreEqual("The inspection has stopped.", state.Subtitle);
        Assert.AreEqual("Inspection cancelled", state.Kicker);
        Assert.AreEqual("No hardware report was created", state.Title);
        Assert.AreEqual("You stopped the inspection. This does not indicate a problem with the computer.", state.Body);
        CollectionAssert.AreEqual(
            new[] { HardwareInspectionActionKind.Back, HardwareInspectionActionKind.RunInspectionAgain },
            state.Actions.Select(action => action.Kind).ToArray());
        Assert.IsFalse(state.ReportCreated);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void TerminalFactory_RejectsContradictoryFailureInputs()
    {
        Assert.Throws<ArgumentException>(() => _factory.CreateTerminal(
            HardwareInspectionOutcome.Completed,
            HardwareInspectionFailureClass.TransientOperation));
        Assert.Throws<ArgumentException>(() => _factory.CreateTerminal(HardwareInspectionOutcome.Failed));
        Assert.Throws<ArgumentException>(() => _factory.CreateTerminal(
            HardwareInspectionOutcome.Cancelled,
            HardwareInspectionFailureClass.CriticalEvidence));
    }

    private static void AssertAction(
        HardwareInspectionPresentationState state,
        HardwareInspectionActionKind kind,
        bool visible,
        bool enabled)
    {
        HardwareInspectionAction action = state.Actions.Single(candidate => candidate.Kind == kind);
        Assert.AreEqual(visible, action.IsVisible);
        Assert.AreEqual(enabled, action.IsEnabled);
    }
}
