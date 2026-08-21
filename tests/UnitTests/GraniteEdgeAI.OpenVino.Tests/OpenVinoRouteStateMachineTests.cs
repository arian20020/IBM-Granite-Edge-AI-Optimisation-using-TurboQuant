using GraniteEdgeAI.Features.OpenVinoRoute;

namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
public sealed class OpenVinoRouteStateMachineTests
{
    [TestMethod]
    public void ApprovedLifecycleCarriesOneCurrentIdentityThroughEveryLegalState()
    {
        OpenVinoRouteStateMachine machine = new();
        Guid operationId = machine.Snapshot.Identity.OperationId;

        Assert.IsTrue(machine.TryBeginInspection(operationId));
        Assert.IsTrue(machine.TryCompleteInspection(
            operationId,
            OpenVinoRouteInspectionOutcome.Ready));
        Assert.IsTrue(machine.TryAwaitConfiguration(operationId));
        Assert.IsTrue(machine.TryBeginLoading(operationId));
        Assert.IsTrue(machine.TrySetSessionReady(operationId));
        Assert.IsTrue(machine.TryBeginTurn(operationId, out Guid turnId));
        Assert.IsTrue(machine.TryCompleteTurn(operationId, turnId));
        Assert.AreEqual(OpenVinoRouteState.TurnCompleted, machine.Snapshot.State);
        Assert.IsTrue(machine.TryReturnToSessionReady(operationId));
        Assert.IsTrue(machine.TryCompleteSession(operationId));

        Assert.AreEqual(OpenVinoRouteState.SessionCompleted, machine.Snapshot.State);
        Assert.AreEqual(operationId, machine.Snapshot.Identity.OperationId);
        Assert.IsNull(machine.Snapshot.ActiveTurnId);
    }

    [TestMethod]
    public void StopAndCancellationAreDistinctLegalBranches()
    {
        OpenVinoRouteStateMachine stopped = ReadyMachine();
        Guid operationId = stopped.Snapshot.Identity.OperationId;
        Assert.IsTrue(stopped.TryBeginTurn(operationId, out Guid turnId));
        Assert.IsTrue(stopped.TryBeginStopping(operationId, turnId));
        Assert.IsTrue(stopped.TryCompleteTurn(operationId, turnId));
        Assert.IsTrue(stopped.TryReturnToSessionReady(operationId));
        Assert.AreEqual(OpenVinoRouteState.SessionReady, stopped.Snapshot.State);

        OpenVinoRouteStateMachine cancelled = ReadyMachine();
        Guid cancelledOperation = cancelled.Snapshot.Identity.OperationId;
        Assert.IsTrue(cancelled.TryBeginTurn(cancelledOperation, out _));
        Assert.IsTrue(cancelled.TryBeginCancellation(cancelledOperation));
        Assert.IsTrue(cancelled.TryMarkCancelled(cancelledOperation));
        Assert.AreEqual(OpenVinoRouteState.Cancelled, cancelled.Snapshot.State);
    }

    [TestMethod]
    public void TerminalStateAndInspectionOutcomeAreImmutableAndIllegalTransitionsDoNotMutate()
    {
        OpenVinoRouteStateMachine machine = ReadyMachine();
        Guid operationId = machine.Snapshot.Identity.OperationId;
        Assert.IsTrue(machine.TryCompleteSession(operationId));
        OpenVinoRouteSnapshot terminal = machine.Snapshot;

        Assert.IsFalse(machine.TryBeginInspection(operationId));
        Assert.IsFalse(machine.TryFail(operationId, "runtime_load_failed"));
        Assert.AreEqual(terminal, machine.Snapshot);

        OpenVinoRouteStateMachine inspected = new();
        Guid inspectedOperation = inspected.Snapshot.Identity.OperationId;
        Assert.IsTrue(inspected.TryBeginInspection(inspectedOperation));
        Assert.IsTrue(inspected.TryCompleteInspection(
            inspectedOperation,
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings));
        Assert.IsFalse(inspected.TryCompleteInspection(
            inspectedOperation,
            OpenVinoRouteInspectionOutcome.Ready));
        Assert.AreEqual(
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings,
            inspected.Snapshot.InspectionOutcome);
    }

    [TestMethod]
    public void StaleOperationAndTurnEventsAreDiscardedWithoutMutation()
    {
        OpenVinoRouteStateMachine machine = ReadyMachine();
        Guid current = machine.Snapshot.Identity.OperationId;
        Assert.IsTrue(machine.TryBeginTurn(current, out Guid currentTurn));
        OpenVinoRouteSnapshot before = machine.Snapshot;

        Assert.IsFalse(machine.TryCompleteTurn(Guid.NewGuid(), currentTurn));
        Assert.IsFalse(machine.TryCompleteTurn(current, Guid.NewGuid()));
        Assert.AreEqual(before, machine.Snapshot);
    }

    [TestMethod]
    public void ResetCreatesFreshOperationWorkerProcessAndSessionIdentities()
    {
        OpenVinoRouteStateMachine machine = ReadyMachine();
        OpenVinoRouteIdentity before = machine.Snapshot.Identity;
        Assert.IsTrue(machine.TryCompleteSession(before.OperationId));

        OpenVinoRouteIdentity after = machine.Reset();

        Assert.AreEqual(OpenVinoRouteState.Idle, machine.Snapshot.State);
        Assert.AreNotEqual(before.OperationId, after.OperationId);
        Assert.AreNotEqual(before.WorkerId, after.WorkerId);
        Assert.AreNotEqual(before.ProcessId, after.ProcessId);
        Assert.AreNotEqual(before.SessionId, after.SessionId);
        Assert.IsFalse(new[]
        {
            after.OperationId,
            after.WorkerId,
            after.ProcessId,
            after.SessionId
        }.Contains(Guid.Empty));
    }

    [TestMethod]
    public void ASecondOperationOrTurnCannotBeginWhileOneIsActive()
    {
        OpenVinoRouteStateMachine machine = new();
        Guid operationId = machine.Snapshot.Identity.OperationId;
        Assert.IsTrue(machine.TryBeginInspection(operationId));
        Assert.IsFalse(machine.TryBeginInspection(operationId));

        machine = ReadyMachine();
        operationId = machine.Snapshot.Identity.OperationId;
        Assert.IsTrue(machine.TryBeginTurn(operationId, out _));
        Assert.IsFalse(machine.TryBeginTurn(operationId, out _));
    }

    [TestMethod]
    public void ConfirmedTurnGateGivesCancellationOrCompletionExactlyOneOwner()
    {
        OpenVinoRouteStateMachine cancellationFirst = ReadyMachine();
        Guid cancelledOperation =
            cancellationFirst.Snapshot.Identity.OperationId;
        Assert.IsTrue(cancellationFirst.TryBeginTurn(
            cancelledOperation,
            out Guid cancelledTurn));
        Assert.IsTrue(cancellationFirst.TryConfirmGeneration(
            cancelledOperation,
            cancelledTurn));

        Assert.IsTrue(cancellationFirst.TryBeginConfirmedTurnCancellation(
            cancelledOperation,
            cancelledTurn));
        Assert.IsTrue(cancellationFirst.TryMarkCancelled(cancelledOperation));
        Assert.AreEqual(
            OpenVinoTurnTerminalOwner.Cancellation,
            cancellationFirst.ResolveCompletedTurn(
                cancelledOperation,
                cancelledTurn));
        Assert.AreEqual(
            OpenVinoRouteState.Cancelled,
            cancellationFirst.Snapshot.State);

        OpenVinoRouteStateMachine completionFirst = ReadyMachine();
        Guid completedOperation = completionFirst.Snapshot.Identity.OperationId;
        Assert.IsTrue(completionFirst.TryBeginTurn(
            completedOperation,
            out Guid completedTurn));
        Assert.IsTrue(completionFirst.TryConfirmGeneration(
            completedOperation,
            completedTurn));

        Assert.AreEqual(
            OpenVinoTurnTerminalOwner.Prompt,
            completionFirst.ResolveCompletedTurn(
                completedOperation,
                completedTurn));
        Assert.AreEqual(
            OpenVinoRouteState.SessionReady,
            completionFirst.Snapshot.State);
        Assert.IsFalse(completionFirst.TryBeginConfirmedTurnCancellation(
            completedOperation,
            completedTurn));
        Assert.AreEqual(
            OpenVinoTurnTerminalOwner.None,
            completionFirst.ResolveCompletedTurn(
                completedOperation,
                completedTurn));
    }

    [TestMethod]
    public void ConfirmedTurnGateSuppressesPromptFailureAfterCancellationOwnership()
    {
        OpenVinoRouteStateMachine machine = ReadyMachine();
        Guid operationId = machine.Snapshot.Identity.OperationId;
        Assert.IsTrue(machine.TryBeginTurn(operationId, out Guid turnId));
        Assert.IsTrue(machine.TryConfirmGeneration(operationId, turnId));
        Assert.IsTrue(machine.TryBeginConfirmedTurnCancellation(
            operationId,
            turnId));

        Assert.AreEqual(
            OpenVinoTurnTerminalOwner.Cancellation,
            machine.ResolveFailedTurn(
                operationId,
                turnId,
                "runtime_protocol_failed"));
        Assert.AreEqual(
            OpenVinoRouteState.CancellingSession,
            machine.Snapshot.State);
        Assert.IsNull(machine.Snapshot.FailureCode);
    }

    private static OpenVinoRouteStateMachine ReadyMachine()
    {
        OpenVinoRouteStateMachine machine = new();
        Guid operationId = machine.Snapshot.Identity.OperationId;
        Assert.IsTrue(machine.TryBeginInspection(operationId));
        Assert.IsTrue(machine.TryCompleteInspection(
            operationId,
            OpenVinoRouteInspectionOutcome.Ready));
        Assert.IsTrue(machine.TryAwaitConfiguration(operationId));
        Assert.IsTrue(machine.TryBeginLoading(operationId));
        Assert.IsTrue(machine.TrySetSessionReady(operationId));
        return machine;
    }
}
