using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Input;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;

[TestClass]
[TestCategory("WinUI")]
public sealed class ModelInspectionRenderCoordinatorTests
{
    private const ModelInspectionPresentationRegions AllRegions =
        ModelInspectionPresentationRegions.Outcome |
        ModelInspectionPresentationRegions.Model |
        ModelInspectionPresentationRegions.Content |
        ModelInspectionPresentationRegions.Actions |
        ModelInspectionPresentationRegions.Footer |
        ModelInspectionPresentationRegions.ProgressRows |
        ModelInspectionPresentationRegions.LiveRegions;

    private const ModelInspectionPresentationRegions AllTerminalRegions =
        AllRegions & ~ModelInspectionPresentationRegions.ProgressRows;

    [TestMethod]
    public void ApplyInitial_AppliesEveryRegionSynchronously()
    {
        using CoordinatorHarness harness = new();
        ModelInspectionViewSnapshot initial = RunningSnapshot(1, 0);

        harness.Coordinator.ApplyInitial(initial);

        Assert.HasCount(1, harness.AppliedDeltas);
        ModelInspectionPresentationDelta delta = harness.AppliedDeltas[0];
        Assert.AreEqual(initial.RenderKey, delta.RenderKey);
        Assert.AreEqual(AllRegions, delta.ChangedRegions);
        Assert.IsNotNull(delta.ProgressRowsUpdate);
        Assert.AreEqual(initial.RenderKey, delta.ProgressRowsUpdate.OwnerKey);
        Assert.AreEqual(initial.RenderKey, delta.VisualOperationKey.RenderKey);
        Assert.AreEqual(0L, delta.VisualOperationKey.InteractionRevision);
        Assert.AreSame(delta.Presentation, harness.Coordinator.CurrentPresentation);
        Assert.AreEqual(initial.RenderKey, harness.Coordinator.LatestAcceptedKey);
        Assert.IsFalse(harness.Coordinator.HasPendingRender);
        Assert.AreEqual(0, harness.Dispatcher.PendingCount);
    }

    [TestMethod]
    public void BurstNotifications_ScheduleOneNewestRender()
    {
        using CoordinatorHarness harness = CreateStartedHarness();
        harness.ClearObservations();
        ModelInspectionViewSnapshot active = ProgressSnapshot(
            1,
            1,
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completed: 0,
            "Checking the package.");
        ModelInspectionViewSnapshot completed = ProgressSnapshot(
            1,
            2,
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Completed,
            completed: 1,
            "Package check completed.");
        ModelInspectionViewSnapshot newest = ProgressSnapshot(
            1,
            3,
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completed: 1,
            "Reading model configuration.");

        harness.Coordinator.RequestRender(active);
        harness.Coordinator.RequestRender(completed);
        harness.Coordinator.RequestRender(newest);

        Assert.AreEqual(1, harness.Dispatcher.TryEnqueueCount);
        Assert.AreEqual(1, harness.Dispatcher.PendingCount);
        Assert.IsTrue(harness.Coordinator.HasPendingRender);
        Assert.HasCount(0, harness.CreatedSnapshots);
        Assert.HasCount(0, harness.AppliedDeltas);

        harness.Dispatcher.RunNext();

        Assert.HasCount(1, harness.CreatedSnapshots);
        Assert.AreSame(newest, harness.CreatedSnapshots[0]);
        Assert.HasCount(1, harness.AppliedDeltas);
        Assert.AreEqual(newest.RenderKey, harness.AppliedDeltas[0].RenderKey);
        Assert.AreEqual(newest.RenderKey, harness.Coordinator.LatestAcceptedKey);
        Assert.AreEqual(newest.RenderKey, harness.Coordinator.CurrentPresentation!.RenderKey);
        Assert.IsFalse(harness.Coordinator.HasPendingRender);
        InspectionProgressRows owner = harness.RowsPassedForAttempt(1);
        Assert.AreEqual(InspectionContentStatus.Passed, owner.Items[0].Status);
        Assert.AreEqual(InspectionContentStatus.Active, owner.Items[1].Status);
    }

    [TestMethod]
    public void OlderGenerationOrRevision_IsRejectedBeforeScheduling()
    {
        using CoordinatorHarness harness = new();
        harness.Coordinator.ApplyInitial(RunningSnapshot(2, 4));
        harness.ClearObservations();

        harness.Coordinator.RequestRender(RunningSnapshot(2, 3));
        harness.Coordinator.RequestRender(RunningSnapshot(1, 99));

        Assert.AreEqual(new ModelInspectionRenderKey(2, 4),
            harness.Coordinator.LatestAcceptedKey);
        Assert.AreEqual(0, harness.Dispatcher.TryEnqueueCount);
        Assert.AreEqual(0, harness.Dispatcher.PendingCount);
        Assert.HasCount(0, harness.CreatedSnapshots);
        Assert.HasCount(0, harness.AppliedDeltas);
    }

    [TestMethod]
    public void DispatcherRejection_ClearsPendingStateAndAllowsSameKeyRetry()
    {
        using CoordinatorHarness harness = CreateStartedHarness();
        ModelInspectionRenderKey initialKey =
            harness.Coordinator.CurrentPresentation!.RenderKey;
        ModelInspectionVisualOperationKey initialOperation = new(
            initialKey,
            harness.Coordinator.InteractionRevision);
        harness.ClearObservations();
        ModelInspectionViewSnapshot update = ProgressSnapshot(
            1,
            1,
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completed: 0,
            "Checking the package.");
        harness.Dispatcher.AcceptEnqueue = false;

        harness.Coordinator.RequestRender(update);

        Assert.AreEqual(1, harness.Dispatcher.TryEnqueueCount);
        Assert.IsFalse(harness.Coordinator.HasPendingRender);
        Assert.AreEqual(0, harness.Dispatcher.PendingCount);
        Assert.HasCount(0, harness.AppliedDeltas);
        Assert.AreEqual(update.RenderKey, harness.Coordinator.LatestAcceptedKey);
        Assert.AreEqual(initialKey,
            harness.Coordinator.CurrentPresentation!.RenderKey);
        Assert.IsFalse(harness.Coordinator.IsCurrent(initialOperation));

        harness.Dispatcher.AcceptEnqueue = true;
        harness.Coordinator.RequestRender(update);
        harness.Dispatcher.RunNext();

        Assert.AreEqual(2, harness.Dispatcher.TryEnqueueCount);
        Assert.HasCount(1, harness.AppliedDeltas);
        Assert.AreEqual(update.RenderKey, harness.AppliedDeltas[0].RenderKey);
    }

    [TestMethod]
    public void SameSemanticPresentation_DoesNotApplyOrReassignRegions()
    {
        using CoordinatorHarness harness = CreateStartedHarness();
        ModelInspectionPagePresentation current =
            harness.Coordinator.CurrentPresentation!;
        object appliedModel = harness.AppliedModel!;
        object appliedContent = harness.AppliedContent!;
        harness.ClearObservations();

        harness.Coordinator.RequestRender(RunningSnapshot(1, 0));
        harness.Dispatcher.RunNext();

        Assert.HasCount(1, harness.CreatedSnapshots);
        Assert.HasCount(0, harness.AppliedDeltas);
        Assert.AreSame(appliedModel, harness.AppliedModel);
        Assert.AreSame(appliedContent, harness.AppliedContent);
        Assert.AreNotSame(current, harness.Coordinator.CurrentPresentation);
        Assert.AreEqual(current.RegionKeys, harness.Coordinator.CurrentPresentation!.RegionKeys);
    }

    [TestMethod]
    public void CancellationRequested_ChangesActionsOnly()
    {
        using CoordinatorHarness harness = CreateStartedHarness();
        object appliedOutcome = harness.AppliedOutcome!;
        object appliedModel = harness.AppliedModel!;
        object appliedContent = harness.AppliedContent!;
        harness.ClearObservations();
        ModelInspectionViewSnapshot cancelled = new(
            new ModelInspectionRenderKey(1, 1),
            isRunActive: true,
            isCancellationRequested: true,
            progress: null,
            terminalResult: null);

        harness.Coordinator.RequestRender(cancelled);
        harness.Dispatcher.RunNext();

        ModelInspectionPresentationDelta delta = AssertSingleDelta(harness);
        Assert.AreEqual(ModelInspectionPresentationRegions.Actions,
            delta.ChangedRegions);
        Assert.IsNull(delta.ProgressRowsUpdate);
        Assert.AreSame(appliedOutcome, harness.AppliedOutcome);
        Assert.AreSame(appliedModel, harness.AppliedModel);
        Assert.AreSame(appliedContent, harness.AppliedContent);
        Assert.AreSame(delta.Presentation.ActionCard, harness.AppliedActions);
    }

    [TestMethod]
    public void SameKeyCommandChange_ChangesActionsOnly()
    {
        using CoordinatorHarness harness = CreateStartedHarness();
        harness.ClearObservations();
        harness.CancelCommand.CanExecuteValue = false;

        harness.Coordinator.RequestRender(RunningSnapshot(1, 0));
        harness.Dispatcher.RunNext();

        ModelInspectionPresentationDelta delta = AssertSingleDelta(harness);
        Assert.AreEqual(ModelInspectionPresentationRegions.Actions,
            delta.ChangedRegions);
        Assert.IsFalse(delta.Presentation.ActionCard.CancelAction.IsEnabled);
    }

    [TestMethod]
    public void ProgressChange_EndsStartupAndUpdatesRowsAndLiveRegion()
    {
        using CoordinatorHarness harness = CreateStartedHarness();
        InspectionProgressRows owner = harness.RowsPassedForAttempt(1);
        InspectionContentItemPresentation[] retained = owner.Items.ToArray();
        object appliedContent = harness.AppliedContent!;
        harness.ClearObservations();

        harness.Coordinator.RequestRender(ProgressSnapshot(
            1,
            1,
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completed: 0,
            "Checking the package."));
        harness.Dispatcher.RunNext();

        ModelInspectionPresentationDelta delta = AssertSingleDelta(harness);
        Assert.AreEqual(
            ModelInspectionPresentationRegions.Content |
            ModelInspectionPresentationRegions.ProgressRows |
            ModelInspectionPresentationRegions.LiveRegions,
            delta.ChangedRegions);
        Assert.IsNotNull(delta.ProgressRowsUpdate);
        Assert.AreNotSame(appliedContent, harness.AppliedContent);
        Assert.AreSame(delta.Presentation.ContentCard, harness.AppliedContent);
        for (int index = 0; index < retained.Length; index++)
        {
            Assert.AreSame(retained[index], owner.Items[index]);
        }

        Assert.AreEqual(InspectionContentStatus.Active, owner.Items[0].Status);
        Assert.AreEqual("Checking the package.", owner.Items[0].Detail);
        Assert.AreEqual("0 of 5 checks complete", owner.ProgressSummary);
    }

    [TestMethod]
    public void TerminalRender_IsOneAtomicDeltaAndDoesNotWipeOutgoingRows()
    {
        using CoordinatorHarness harness = CreateStartedHarness();
        harness.Coordinator.RequestRender(ProgressSnapshot(
            1,
            1,
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completed: 1,
            "Reading model configuration."));
        harness.Dispatcher.RunNext();
        harness.ClearObservations();
        ModelInspectionViewSnapshot terminal = TerminalSnapshot(
            1,
            2,
            ModelInspectionOutcome.Ready);

        harness.Coordinator.RequestRender(terminal);
        harness.Dispatcher.RunNext();

        ModelInspectionPresentationDelta delta = AssertSingleDelta(harness);
        Assert.AreEqual(AllTerminalRegions, delta.ChangedRegions);
        Assert.IsNull(delta.ProgressRowsUpdate);
        Assert.HasCount(1, harness.AppliedDeltas);
        InspectionProgressRows outgoing = harness.RowsPassedForAttempt(1);
        Assert.AreEqual(InspectionContentStatus.Passed, outgoing.Items[0].Status);
        Assert.AreEqual(InspectionContentStatus.Active, outgoing.Items[1].Status);
        Assert.AreEqual("Reading model configuration.", outgoing.Items[1].Detail);
        Assert.AreEqual(ModelInspectionFigmaState.ReadyCollapsed,
            harness.Coordinator.CurrentPresentation!.State);
        CollectionAssert.AreEqual(
            new[]
            {
                "RetireProgress",
                "Model",
                "Actions",
                "Footer",
                "Outcome",
                "LiveRegions"
            },
            harness.ApplicationPhases);
    }

    [TestMethod]
    public void Retry_UsesDistinctWaitingRowsForTheNewAttempt()
    {
        using CoordinatorHarness harness = CreateStartedHarness();
        harness.Coordinator.RequestRender(ProgressSnapshot(
            1,
            1,
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completed: 0,
            "Checking the package."));
        harness.Dispatcher.RunNext();
        InspectionProgressRows first = harness.RowsPassedForAttempt(1);
        InspectionContentItemPresentation[] firstItems = first.Items.ToArray();
        harness.ClearObservations();

        harness.Coordinator.RequestRender(RunningSnapshot(2, 0));
        harness.Dispatcher.RunNext();

        ModelInspectionPresentationDelta delta = AssertSingleDelta(harness);
        InspectionProgressRows second = harness.RowsPassedForAttempt(2);
        Assert.IsTrue(delta.ChangedRegions.HasFlag(
            ModelInspectionPresentationRegions.Content));
        Assert.IsTrue(delta.ChangedRegions.HasFlag(
            ModelInspectionPresentationRegions.ProgressRows));
        Assert.AreNotSame(first, second);
        for (int index = 0; index < firstItems.Length; index++)
        {
            Assert.AreNotSame(firstItems[index], second.Items[index]);
        }

        Assert.AreEqual(InspectionContentStatus.Active, first.Items[0].Status);
        Assert.IsTrue(second.Items.All(item =>
            item.Status == InspectionContentStatus.Waiting &&
            !item.IsActive &&
            item.Detail.Length == 0));
        Assert.AreEqual("ProgressRows", harness.ApplicationPhases[0]);
        Assert.AreEqual(ModelInspectionFigmaState.InspectionProgress,
            harness.Coordinator.CurrentPresentation!.State);
    }

    [TestMethod]
    public void ProgressToProgressAttemptReplacement_ForcesNewOwnerAttachment()
    {
        using CoordinatorHarness harness = CreateStartedHarness();
        InspectionProgressRows first = harness.RowsPassedForAttempt(1);
        object firstAppliedContent = harness.AppliedContent!;
        harness.ClearObservations();

        harness.Coordinator.RequestRender(RunningSnapshot(2, 0));
        harness.Dispatcher.RunNext();

        ModelInspectionPresentationDelta delta = AssertSingleDelta(harness);
        InspectionProgressRows second = harness.RowsPassedForAttempt(2);
        Assert.AreEqual(
            ModelInspectionPresentationRegions.Content |
            ModelInspectionPresentationRegions.ProgressRows |
            ModelInspectionPresentationRegions.LiveRegions,
            delta.ChangedRegions);
        Assert.AreNotSame(first, second);
        Assert.AreNotSame(firstAppliedContent, harness.AppliedContent);
        Assert.AreSame(second, delta.Presentation.ContentCard.ProgressRows);
        Assert.IsTrue(second.Items.All(item =>
            item.Status == InspectionContentStatus.Waiting));
    }

    [TestMethod]
    public void GenerationZeroOwner_PromotesOnceThenLaterAttemptGetsNewOwner()
    {
        using CoordinatorHarness harness = new();
        harness.Coordinator.ApplyInitial(RunningSnapshot(0, 0));
        InspectionProgressRows initial = harness.RowsPassedForAttempt(0);
        harness.ClearObservations();

        harness.Coordinator.RequestRender(RunningSnapshot(1, 0));
        harness.Dispatcher.RunNext();

        InspectionProgressRows promoted = harness.RowsPassedForAttempt(1);
        Assert.AreSame(initial, promoted);
        ModelInspectionPresentationDelta promotion = AssertSingleDelta(harness);
        Assert.AreEqual(
            ModelInspectionPresentationRegions.ProgressRows |
            ModelInspectionPresentationRegions.LiveRegions,
            promotion.ChangedRegions);
        harness.ClearObservations();

        harness.Coordinator.RequestRender(RunningSnapshot(2, 0));
        harness.Dispatcher.RunNext();

        InspectionProgressRows later = harness.RowsPassedForAttempt(2);
        Assert.AreNotSame(promoted, later);
        Assert.AreEqual(
            ModelInspectionPresentationRegions.Content |
            ModelInspectionPresentationRegions.ProgressRows |
            ModelInspectionPresentationRegions.LiveRegions,
            AssertSingleDelta(harness).ChangedRegions);
    }

    [TestMethod]
    public void DisclosureRequest_UsesSeparateRevisionAndChangesOnlyItsRegion()
    {
        using CoordinatorHarness harness = new();
        ModelInspectionViewSnapshot terminal = TerminalSnapshot(
            1,
            1,
            ModelInspectionOutcome.Ready);
        harness.Coordinator.ApplyInitial(terminal);
        harness.ClearObservations();

        bool accepted = harness.Coordinator.TryRequestDisclosure(
            terminal.RenderKey,
            isExpanded: true,
            out ModelInspectionVisualOperationKey operationKey);

        Assert.IsTrue(accepted);
        Assert.AreEqual(1L, harness.Coordinator.InteractionRevision);
        Assert.AreEqual(terminal.RenderKey, operationKey.RenderKey);
        Assert.AreEqual(1L, operationKey.InteractionRevision);
        Assert.IsTrue(harness.Coordinator.IsCurrent(operationKey));
        Assert.AreEqual(1, harness.Dispatcher.PendingCount);

        harness.Dispatcher.RunNext();

        ModelInspectionPresentationDelta delta = AssertSingleDelta(harness);
        Assert.AreEqual(ModelInspectionPresentationRegions.Model,
            delta.ChangedRegions);
        Assert.AreEqual(operationKey, delta.VisualOperationKey);
        Assert.AreEqual(ModelInspectionFigmaState.ReadyExpanded,
            delta.Presentation.State);
    }

    [TestMethod]
    public void DisclosureRequest_RejectsStaleUnsupportedAndNoOpTargets()
    {
        using CoordinatorHarness progressHarness = CreateStartedHarness();
        long progressRevision = progressHarness.Coordinator.InteractionRevision;
        Assert.IsFalse(progressHarness.Coordinator.TryRequestDisclosure(
            new ModelInspectionRenderKey(1, 0),
            isExpanded: true,
            out ModelInspectionVisualOperationKey unsupportedKey));
        Assert.AreEqual(default, unsupportedKey);
        Assert.AreEqual(progressRevision,
            progressHarness.Coordinator.InteractionRevision);
        Assert.AreEqual(0, progressHarness.Dispatcher.PendingCount);

        using CoordinatorHarness terminalHarness = new();
        ModelInspectionViewSnapshot terminal = TerminalSnapshot(
            2,
            1,
            ModelInspectionOutcome.Ready);
        terminalHarness.Coordinator.ApplyInitial(terminal);
        terminalHarness.ClearObservations();
        Assert.IsFalse(terminalHarness.Coordinator.TryRequestDisclosure(
            new ModelInspectionRenderKey(1, 99),
            isExpanded: true,
            out ModelInspectionVisualOperationKey staleKey));
        Assert.AreEqual(default, staleKey);
        Assert.IsFalse(terminalHarness.Coordinator.TryRequestDisclosure(
            terminal.RenderKey,
            isExpanded: false,
            out ModelInspectionVisualOperationKey noOpKey));
        Assert.AreEqual(default, noOpKey);
        Assert.AreEqual(0L, terminalHarness.Coordinator.InteractionRevision);
        Assert.AreEqual(0, terminalHarness.Dispatcher.PendingCount);
    }

    [TestMethod]
    public void RapidDisclosureReversal_LeavesOlderOperationStaleAndNewestTargetCurrent()
    {
        using CoordinatorHarness harness = new();
        ModelInspectionViewSnapshot terminal = TerminalSnapshot(
            1,
            1,
            ModelInspectionOutcome.Ready);
        harness.Coordinator.ApplyInitial(terminal);
        harness.ClearObservations();

        Assert.IsTrue(harness.Coordinator.TryRequestDisclosure(
            terminal.RenderKey,
            isExpanded: true,
            out ModelInspectionVisualOperationKey expandKey));
        Assert.IsTrue(harness.Coordinator.TryRequestDisclosure(
            terminal.RenderKey,
            isExpanded: false,
            out ModelInspectionVisualOperationKey collapseKey));

        Assert.AreEqual(1, harness.Dispatcher.TryEnqueueCount);
        Assert.AreEqual(1, harness.Dispatcher.PendingCount);
        Assert.IsFalse(harness.Coordinator.IsCurrent(expandKey));
        Assert.IsTrue(harness.Coordinator.IsCurrent(collapseKey));

        harness.Dispatcher.RunNext();

        Assert.AreEqual(ModelInspectionFigmaState.ReadyCollapsed,
            harness.Coordinator.CurrentPresentation!.State);
        Assert.HasCount(0, harness.AppliedDeltas);
        Assert.IsTrue(harness.Coordinator.IsCurrent(collapseKey));
    }

    [TestMethod]
    public void MotionPolicyChange_PreservesAcceptedTargetAndFlushesItSynchronously()
    {
        using CoordinatorHarness harness = new();
        ModelInspectionViewSnapshot ready = TerminalSnapshot(
            1,
            1,
            ModelInspectionOutcome.Ready);
        harness.Coordinator.ApplyInitial(ready);
        harness.ClearObservations();
        Assert.IsTrue(harness.Coordinator.TryRequestDisclosure(
            ready.RenderKey,
            isExpanded: true,
            out ModelInspectionVisualOperationKey staleMotionKey));

        harness.Coordinator.InvalidateInteractions(
            preserveDisclosureTarget: true);
        harness.Coordinator.FlushPendingRender();

        Assert.IsFalse(harness.Coordinator.IsCurrent(staleMotionKey));
        Assert.IsTrue(harness.Coordinator.DesiredDisclosureExpanded);
        Assert.AreEqual(
            ModelInspectionFigmaState.ReadyExpanded,
            harness.Coordinator.CurrentPresentation!.State);
        Assert.HasCount(1, harness.AppliedDeltas);
        Assert.IsFalse(harness.Coordinator.HasPendingRender);

        harness.Dispatcher.RunNext();
        Assert.HasCount(1, harness.AppliedDeltas,
            "The stale queued drain must not apply a second presentation.");
    }

    [TestMethod]
    public void NewAttempt_ClearsPendingDisclosureBeforeFactoryInvocation()
    {
        using CoordinatorHarness harness = new();
        ModelInspectionViewSnapshot terminal = TerminalSnapshot(
            1,
            1,
            ModelInspectionOutcome.Ready);
        harness.Coordinator.ApplyInitial(terminal);
        harness.ClearObservations();
        Assert.IsTrue(harness.Coordinator.TryRequestDisclosure(
            terminal.RenderKey,
            isExpanded: true,
            out ModelInspectionVisualOperationKey oldOperation));

        harness.Coordinator.RequestRender(RunningSnapshot(2, 0));
        harness.Dispatcher.RunNext();

        Assert.IsFalse(harness.Coordinator.IsCurrent(oldOperation));
        CollectionAssert.AreEqual(
            new[] { false },
            harness.CreatedExpansionTargets);
        Assert.AreEqual(ModelInspectionFigmaState.InspectionProgress,
            harness.Coordinator.CurrentPresentation!.State);
    }

    [TestMethod]
    public void SameOutcomeSemanticRerender_ProbesCollapsedBeforePreservingExpansion()
    {
        using CoordinatorHarness harness = new();
        ModelInspectionViewSnapshot first = TerminalSnapshot(
            1,
            1,
            ModelInspectionOutcome.Ready);
        harness.Coordinator.ApplyInitial(first);
        Assert.IsTrue(harness.Coordinator.TryRequestDisclosure(
            first.RenderKey,
            isExpanded: true,
            out ModelInspectionVisualOperationKey firstOperation));
        harness.Dispatcher.RunNext();
        harness.ClearObservations();

        ModelInspectionViewSnapshot sameOutcome = TerminalSnapshot(
            1,
            2,
            ModelInspectionOutcome.Ready);
        harness.Coordinator.RequestRender(sameOutcome);
        harness.Dispatcher.RunNext();

        CollectionAssert.AreEqual(
            new[] { false, true },
            harness.CreatedExpansionTargets);
        Assert.AreEqual(ModelInspectionFigmaState.ReadyExpanded,
            harness.Coordinator.CurrentPresentation!.State);
        Assert.AreEqual(sameOutcome.RenderKey,
            harness.Coordinator.CurrentPresentation.RenderKey);
        Assert.IsFalse(harness.Coordinator.IsCurrent(firstOperation));
        Assert.HasCount(0, harness.AppliedDeltas);
    }

    [TestMethod]
    public void NewOutcome_ResetsExpansionAndInvalidatesInteraction()
    {
        using CoordinatorHarness harness = new();
        ModelInspectionViewSnapshot ready = TerminalSnapshot(
            1,
            1,
            ModelInspectionOutcome.Ready);
        harness.Coordinator.ApplyInitial(ready);
        Assert.IsTrue(harness.Coordinator.TryRequestDisclosure(
            ready.RenderKey,
            isExpanded: true,
            out _));
        harness.Dispatcher.RunNext();
        long expandedRevision = harness.Coordinator.InteractionRevision;
        harness.ClearObservations();

        harness.Coordinator.RequestRender(TerminalSnapshot(
            1,
            2,
            ModelInspectionOutcome.ConversionRequired));
        harness.Dispatcher.RunNext();

        CollectionAssert.AreEqual(
            new[] { false },
            harness.CreatedExpansionTargets);
        Assert.IsGreaterThan(expandedRevision,
            harness.Coordinator.InteractionRevision);
        Assert.AreEqual(ModelInspectionFigmaState.ConversionRequiredCollapsed,
            harness.Coordinator.CurrentPresentation!.State);
    }

    [TestMethod]
    public void DisclosureEnqueueRejection_RollsBackTargetAndRevision()
    {
        using CoordinatorHarness harness = new();
        ModelInspectionViewSnapshot terminal = TerminalSnapshot(
            1,
            1,
            ModelInspectionOutcome.Ready);
        harness.Coordinator.ApplyInitial(terminal);
        harness.ClearObservations();
        harness.Dispatcher.AcceptEnqueue = false;

        bool accepted = harness.Coordinator.TryRequestDisclosure(
            terminal.RenderKey,
            isExpanded: true,
            out ModelInspectionVisualOperationKey operationKey);

        Assert.IsFalse(accepted);
        Assert.AreEqual(default, operationKey);
        Assert.AreEqual(0L, harness.Coordinator.InteractionRevision);
        Assert.IsFalse(harness.Coordinator.HasPendingRender);
        Assert.AreEqual(0, harness.Dispatcher.PendingCount);

        harness.Dispatcher.AcceptEnqueue = true;
        Assert.IsTrue(harness.Coordinator.TryRequestDisclosure(
            terminal.RenderKey,
            isExpanded: true,
            out _));
    }

    [TestMethod]
    public void NewerSemanticRenderAndExplicitInvalidation_RejectOldVisualOperations()
    {
        using CoordinatorHarness harness = new();
        ModelInspectionViewSnapshot terminal = TerminalSnapshot(
            1,
            1,
            ModelInspectionOutcome.Ready);
        harness.Coordinator.ApplyInitial(terminal);
        Assert.IsTrue(harness.Coordinator.TryRequestDisclosure(
            terminal.RenderKey,
            isExpanded: true,
            out ModelInspectionVisualOperationKey disclosureKey));

        harness.Coordinator.RequestRender(TerminalSnapshot(
            1,
            2,
            ModelInspectionOutcome.Ready));

        Assert.IsFalse(harness.Coordinator.IsCurrent(disclosureKey));
        ModelInspectionVisualOperationKey semanticKey = new(
            new ModelInspectionRenderKey(1, 2),
            harness.Coordinator.InteractionRevision);
        Assert.IsTrue(harness.Coordinator.IsCurrent(semanticKey));

        harness.Coordinator.InvalidateInteractions();

        Assert.IsFalse(harness.Coordinator.IsCurrent(semanticKey));
    }

    [TestMethod]
    public void FactoryReentrancy_NewerRequestPreventsOlderApply()
    {
        using CoordinatorHarness harness = CreateStartedHarness();
        harness.ClearObservations();
        ModelInspectionViewSnapshot older = ProgressSnapshot(
            1,
            1,
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completed: 0,
            "Checking the package.");
        ModelInspectionViewSnapshot newer = ProgressSnapshot(
            1,
            2,
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Completed,
            completed: 1,
            "Package check completed.");
        harness.OnCreatePresentation = snapshot =>
        {
            if (snapshot.RenderKey == older.RenderKey)
            {
                harness.Coordinator.RequestRender(newer);
            }
        };

        harness.Coordinator.RequestRender(older);
        harness.Dispatcher.RunNext();

        Assert.HasCount(0, harness.AppliedDeltas);
        Assert.AreEqual(1, harness.Dispatcher.PendingCount);
        Assert.AreEqual(newer.RenderKey, harness.Coordinator.LatestAcceptedKey);

        harness.Dispatcher.RunNext();

        Assert.HasCount(1, harness.AppliedDeltas);
        Assert.AreEqual(newer.RenderKey, harness.AppliedDeltas[0].RenderKey);
    }

    [TestMethod]
    public void ApplyReentrancy_SchedulesOneFinalDrain()
    {
        using CoordinatorHarness harness = CreateStartedHarness();
        harness.ClearObservations();
        ModelInspectionViewSnapshot first = ProgressSnapshot(
            1,
            1,
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completed: 0,
            "Checking the package.");
        ModelInspectionViewSnapshot newer = ProgressSnapshot(
            1,
            2,
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Completed,
            completed: 1,
            "Package check completed.");
        harness.OnApplyDelta = delta =>
        {
            if (delta.RenderKey == first.RenderKey)
            {
                harness.Coordinator.RequestRender(newer);
            }
        };

        harness.Coordinator.RequestRender(first);
        harness.Dispatcher.RunNext();

        Assert.HasCount(1, harness.AppliedDeltas);
        Assert.AreEqual(first.RenderKey, harness.AppliedDeltas[0].RenderKey);
        Assert.AreEqual(1, harness.Dispatcher.PendingCount);
        Assert.IsTrue(harness.Coordinator.HasPendingRender);

        harness.Dispatcher.RunNext();

        Assert.HasCount(2, harness.AppliedDeltas);
        Assert.AreEqual(newer.RenderKey, harness.AppliedDeltas[1].RenderKey);
        Assert.AreEqual(0, harness.Dispatcher.PendingCount);
        Assert.IsFalse(harness.Coordinator.HasPendingRender);
    }

    [TestMethod]
    public void InvalidateThenDispose_RejectsQueuedWorkAndInvalidatesKeys()
    {
        CoordinatorHarness harness = CreateStartedHarness();
        harness.ClearObservations();
        ModelInspectionViewSnapshot update = ProgressSnapshot(
            1,
            1,
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completed: 0,
            "Checking the package.");
        harness.Coordinator.RequestRender(update);
        ModelInspectionVisualOperationKey pendingKey = new(
            update.RenderKey,
            harness.Coordinator.InteractionRevision);

        harness.Coordinator.Invalidate();
        harness.Coordinator.Invalidate();
        harness.Coordinator.Dispose();
        harness.Coordinator.Dispose();
        harness.Dispatcher.RunNext();

        Assert.HasCount(0, harness.CreatedSnapshots);
        Assert.HasCount(0, harness.AppliedDeltas);
        Assert.IsFalse(harness.Coordinator.HasPendingRender);
        Assert.IsFalse(harness.Coordinator.IsCurrent(update.RenderKey));
        Assert.IsFalse(harness.Coordinator.IsCurrent(pendingKey));
    }

    [TestMethod]
    public void ValueTypesAndDelta_RejectInvalidConstruction()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelInspectionVisualOperationKey(
                new ModelInspectionRenderKey(0, 0),
                interactionRevision: -1));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new DispatcherQueueModelInspectionRenderDispatcher(null!));

        using CoordinatorHarness harness = new();
        ModelInspectionPagePresentation presentation =
            harness.CreateForTest(RunningSnapshot(1, 0), expanded: false);
        ModelInspectionVisualOperationKey operationKey = new(
            presentation.RenderKey,
            interactionRevision: 0);

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new ModelInspectionPresentationDelta(
                presentation.RenderKey,
                null!,
                ModelInspectionPresentationRegions.Actions,
                progressRowsUpdate: null,
                operationKey));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionPresentationDelta(
                presentation.RenderKey,
                presentation,
                ModelInspectionPresentationRegions.None,
                progressRowsUpdate: null,
                operationKey));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionPresentationDelta(
                presentation.RenderKey,
                presentation,
                ModelInspectionPresentationRegions.ProgressRows,
                progressRowsUpdate: null,
                operationKey));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionPresentationDelta(
                presentation.RenderKey,
                presentation,
                ModelInspectionPresentationRegions.Actions,
                presentation.ProgressRowsUpdate,
                operationKey));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionPresentationDelta(
                new ModelInspectionRenderKey(1, 1),
                presentation,
                ModelInspectionPresentationRegions.Actions,
                progressRowsUpdate: null,
                new ModelInspectionVisualOperationKey(
                    new ModelInspectionRenderKey(1, 1),
                    0)));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionPresentationDelta(
                presentation.RenderKey,
                presentation,
                ModelInspectionPresentationRegions.Actions,
                progressRowsUpdate: null,
                new ModelInspectionVisualOperationKey(
                    new ModelInspectionRenderKey(1, 1),
                    0)));
        using CoordinatorHarness otherHarness = new();
        ModelInspectionPagePresentation otherPresentation =
            otherHarness.CreateForTest(RunningSnapshot(1, 1), expanded: false);
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionPresentationDelta(
                presentation.RenderKey,
                presentation,
                ModelInspectionPresentationRegions.ProgressRows,
                otherPresentation.ProgressRowsUpdate,
                operationKey));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelInspectionPresentationDelta(
                presentation.RenderKey,
                presentation,
                (ModelInspectionPresentationRegions)128,
                progressRowsUpdate: null,
                operationKey));
    }

    [TestMethod]
    public void Delta_RejectsProgressPayloadForTerminalPresentation()
    {
        using CoordinatorHarness harness = new();
        ModelInspectionPagePresentation terminal = harness.CreateForTest(
            TerminalSnapshot(1, 1, ModelInspectionOutcome.Ready),
            expanded: false);
        ModelInspectionVisualOperationKey operationKey = new(
            terminal.RenderKey,
            interactionRevision: 0);

        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionPresentationDelta(
                terminal.RenderKey,
                terminal,
                ModelInspectionPresentationRegions.ProgressRows,
                terminal.ProgressRowsUpdate,
                operationKey));
    }

    [TestMethod]
    public void Delta_RejectsProgressPayloadThatDiffersFromPresentationUpdate()
    {
        using CoordinatorHarness harness = new();
        ModelInspectionPagePresentation presentation = harness.CreateForTest(
            RunningSnapshot(1, 0),
            expanded: false);
        ModelInspectionVisualOperationKey operationKey = new(
            presentation.RenderKey,
            interactionRevision: 0);
        InspectionProgressRowsUpdate mismatchedSummary = new(
            presentation.ProgressRowsUpdate.Key,
            presentation.RenderKey,
            "1 of 5 checks complete");
        InspectionProgressRowsUpdate mismatchedKey = new(
            new ModelInspectionProgressRegionKey(
                ModelInspectionStage.CheckModelPackage,
                ModelInspectionStageStatus.Active,
                completedStageCount: 0,
                stageCount: 5,
                stageFraction: 0,
                detail: "Checking the package."),
            presentation.RenderKey,
            presentation.ProgressRowsUpdate.ProgressSummary);

        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionPresentationDelta(
                presentation.RenderKey,
                presentation,
                ModelInspectionPresentationRegions.ProgressRows,
                mismatchedSummary,
                operationKey));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionPresentationDelta(
                presentation.RenderKey,
                presentation,
                ModelInspectionPresentationRegions.ProgressRows,
                mismatchedKey,
                operationKey));
    }

    private static CoordinatorHarness CreateStartedHarness()
    {
        CoordinatorHarness harness = new();
        harness.Coordinator.ApplyInitial(RunningSnapshot(1, 0));
        return harness;
    }

    private static ModelInspectionPresentationDelta AssertSingleDelta(
        CoordinatorHarness harness)
    {
        Assert.HasCount(1, harness.AppliedDeltas);
        return harness.AppliedDeltas[0];
    }

    private static ModelInspectionViewSnapshot RunningSnapshot(
        long generation,
        long revision) => new(
            new ModelInspectionRenderKey(generation, revision),
            isRunActive: true,
            isCancellationRequested: false,
            progress: null,
            terminalResult: null);

    private static ModelInspectionViewSnapshot ProgressSnapshot(
        long generation,
        long revision,
        ModelInspectionStage stage,
        ModelInspectionStageStatus status,
        int completed,
        string detail) => new(
            new ModelInspectionRenderKey(generation, revision),
            isRunActive: true,
            isCancellationRequested: false,
            new ModelInspectionProgress(
                stage,
                status,
                completed,
                totalStageCount: 5,
                stageFraction: status == ModelInspectionStageStatus.Active
                    ? 0.5
                    : 1,
                detail),
            terminalResult: null);

    private static ModelInspectionViewSnapshot TerminalSnapshot(
        long generation,
        long revision,
        ModelInspectionOutcome outcome) => new(
            new ModelInspectionRenderKey(generation, revision),
            isRunActive: false,
            isCancellationRequested: false,
            progress: null,
            terminalResult: ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(outcome)));

    private sealed class CoordinatorHarness : IDisposable
    {
        private readonly Dictionary<long, InspectionProgressRows> _passedRows = [];

        internal CoordinatorHarness()
        {
            Commands = new ModelInspectionPresentationCommands(
                CancelCommand,
                new MutableCommand(),
                new MutableCommand());
            Coordinator = new ModelInspectionRenderCoordinator(
                Dispatcher,
                CreatePresentation,
                ApplyDelta);
        }

        internal ManualDispatcher Dispatcher { get; } = new();

        internal MutableCommand CancelCommand { get; } = new();

        internal ModelInspectionPresentationCommands Commands { get; }

        internal ModelInspectionRenderCoordinator Coordinator { get; }

        internal List<ModelInspectionViewSnapshot> CreatedSnapshots { get; } = [];

        internal List<bool> CreatedExpansionTargets { get; } = [];

        internal List<ModelInspectionPresentationDelta> AppliedDeltas { get; } = [];

        internal List<string> ApplicationPhases { get; } = [];

        internal Action<ModelInspectionViewSnapshot>? OnCreatePresentation { get; set; }

        internal Action<ModelInspectionPresentationDelta>? OnApplyDelta { get; set; }

        internal object? AppliedOutcome { get; private set; }

        internal object? AppliedModel { get; private set; }

        internal object? AppliedContent { get; private set; }

        internal object? AppliedActions { get; private set; }

        internal InspectionFooterStatus? AppliedFooter { get; private set; }

        internal ModelInspectionPagePresentation CreateForTest(
            ModelInspectionViewSnapshot snapshot,
            bool expanded)
        {
            InspectionProgressRows rows = new();
            if (snapshot.RenderKey.AttemptGeneration > 0)
            {
                rows.Reset(new ModelInspectionRenderKey(
                    snapshot.RenderKey.AttemptGeneration,
                    0));
            }

            return CreatePresentation(snapshot, expanded, rows);
        }

        internal InspectionProgressRows RowsPassedForAttempt(long generation) =>
            _passedRows[generation];

        internal void ClearObservations()
        {
            CreatedSnapshots.Clear();
            CreatedExpansionTargets.Clear();
            AppliedDeltas.Clear();
            ApplicationPhases.Clear();
            Dispatcher.ResetTryEnqueueCount();
            OnCreatePresentation = null;
            OnApplyDelta = null;
        }

        public void Dispose() => Coordinator.Dispose();

        private ModelInspectionPagePresentation CreatePresentation(
            ModelInspectionViewSnapshot snapshot,
            bool expanded,
            InspectionProgressRows progressRows)
        {
            CreatedSnapshots.Add(snapshot);
            CreatedExpansionTargets.Add(expanded);
            if (_passedRows.TryGetValue(
                    snapshot.RenderKey.AttemptGeneration,
                    out InspectionProgressRows? retainedRows))
            {
                Assert.AreSame(retainedRows, progressRows);
            }
            else
            {
                _passedRows.Add(
                    snapshot.RenderKey.AttemptGeneration,
                    progressRows);
            }

            OnCreatePresentation?.Invoke(snapshot);
            return ModelInspectionPresentationFactory.Create(
                PresentationTestData.CreateRequest(),
                snapshot,
                Commands,
                expanded,
                progressRows);
        }

        private void ApplyDelta(ModelInspectionPresentationDelta delta)
        {
            AppliedDeltas.Add(delta);
            bool retiresProgress =
                delta.Presentation.State != ModelInspectionFigmaState.InspectionProgress &&
                delta.ChangedRegions.HasFlag(ModelInspectionPresentationRegions.Content);

            // Task 8 supplies one atomic delta. Its page consumer applies a
            // terminal delta in this observable order; the production page
            // wires the same phases when it adopts the coordinator in Tasks 9/10.
            if (retiresProgress)
            {
                ApplicationPhases.Add("RetireProgress");
                AppliedContent = delta.Presentation.ContentCard;
            }

            if (delta.ChangedRegions.HasFlag(
                    ModelInspectionPresentationRegions.ProgressRows))
            {
                ApplicationPhases.Add("ProgressRows");
                RowsPassedForAttempt(delta.RenderKey.AttemptGeneration).Apply(
                    delta.ProgressRowsUpdate!);
            }

            if (delta.ChangedRegions.HasFlag(ModelInspectionPresentationRegions.Model))
            {
                ApplicationPhases.Add("Model");
                AppliedModel = delta.Presentation.ModelCard;
            }

            if (!retiresProgress &&
                delta.ChangedRegions.HasFlag(ModelInspectionPresentationRegions.Content))
            {
                ApplicationPhases.Add("Content");
                AppliedContent = delta.Presentation.ContentCard;
            }

            if (delta.ChangedRegions.HasFlag(ModelInspectionPresentationRegions.Actions))
            {
                ApplicationPhases.Add("Actions");
                AppliedActions = delta.Presentation.ActionCard;
            }

            if (delta.ChangedRegions.HasFlag(ModelInspectionPresentationRegions.Footer))
            {
                ApplicationPhases.Add("Footer");
                AppliedFooter = delta.Presentation.FooterStatus;
            }

            if (delta.ChangedRegions.HasFlag(ModelInspectionPresentationRegions.Outcome))
            {
                ApplicationPhases.Add("Outcome");
                AppliedOutcome = delta.Presentation.OutcomeCard;
            }

            if (delta.ChangedRegions.HasFlag(ModelInspectionPresentationRegions.LiveRegions))
            {
                ApplicationPhases.Add("LiveRegions");
            }

            OnApplyDelta?.Invoke(delta);
        }
    }

    private sealed class ManualDispatcher : IModelInspectionRenderDispatcher
    {
        private readonly Queue<Action> _callbacks = [];

        internal bool AcceptEnqueue { get; set; } = true;

        internal int TryEnqueueCount { get; private set; }

        internal int PendingCount => _callbacks.Count;

        public bool TryEnqueue(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            TryEnqueueCount++;
            if (!AcceptEnqueue)
            {
                return false;
            }

            _callbacks.Enqueue(callback);
            return true;
        }

        internal void ResetTryEnqueueCount() => TryEnqueueCount = 0;

        internal void RunNext()
        {
            Assert.IsGreaterThan(0, _callbacks.Count);
            _callbacks.Dequeue()();
        }
    }

    private sealed class MutableCommand : ICommand
    {
        internal bool CanExecuteValue { get; set; } = true;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => CanExecuteValue;

        public void Execute(object? parameter)
        {
        }
    }
}
