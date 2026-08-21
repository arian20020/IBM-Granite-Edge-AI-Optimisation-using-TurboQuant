using System;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

public enum OpenVinoRouteState
{
    Idle,
    Inspecting,
    TerminalInspectionOutcome,
    AwaitingConfiguration,
    Loading,
    SessionReady,
    GeneratingTurn,
    StoppingTurn,
    CancellingSession,
    TurnCompleted,
    SessionCompleted,
    Failed,
    Cancelled
}

public enum OpenVinoRouteInspectionOutcome
{
    Ready,
    ReadyWithWarnings,
    ConversionRequired,
    IncompletePackage,
    Unsupported,
    Invalid
}

public sealed record OpenVinoRouteIdentity(
    Guid OperationId,
    Guid WorkerId,
    Guid ProcessId,
    Guid SessionId);

public sealed record OpenVinoRouteSnapshot(
    OpenVinoRouteState State,
    OpenVinoRouteIdentity Identity,
    OpenVinoRouteInspectionOutcome? InspectionOutcome,
    Guid? ActiveTurnId,
    string? FailureCode);

/// <summary>
/// Owns the route-local identity and legal lifecycle independently of the
/// worker protocol state machine.
/// </summary>
public sealed class OpenVinoRouteStateMachine
{
    private readonly object stateLock = new();
    private readonly Func<Guid> identityFactory;
    private OpenVinoRouteSnapshot snapshot;

    public OpenVinoRouteStateMachine()
        : this(Guid.NewGuid)
    {
    }

    internal OpenVinoRouteStateMachine(Func<Guid> identityFactory)
    {
        this.identityFactory = identityFactory ??
            throw new ArgumentNullException(nameof(identityFactory));
        snapshot = FreshSnapshot();
    }

    public OpenVinoRouteSnapshot Snapshot
    {
        get
        {
            lock (stateLock)
            {
                return snapshot;
            }
        }
    }

    public bool TryBeginInspection(Guid operationId) =>
        TryMove(operationId, OpenVinoRouteState.Idle,
            OpenVinoRouteState.Inspecting);

    public bool TryCompleteInspection(
        Guid operationId,
        OpenVinoRouteInspectionOutcome outcome)
    {
        lock (stateLock)
        {
            if (!IsCurrent(operationId) ||
                snapshot.State != OpenVinoRouteState.Inspecting ||
                snapshot.InspectionOutcome is not null ||
                !Enum.IsDefined(outcome))
            {
                return false;
            }

            snapshot = snapshot with
            {
                State = OpenVinoRouteState.TerminalInspectionOutcome,
                InspectionOutcome = outcome
            };
            return true;
        }
    }

    public bool TryAwaitConfiguration(Guid operationId)
    {
        lock (stateLock)
        {
            if (!IsCurrent(operationId) ||
                snapshot.State != OpenVinoRouteState.TerminalInspectionOutcome ||
                snapshot.InspectionOutcome is not (
                    OpenVinoRouteInspectionOutcome.Ready or
                    OpenVinoRouteInspectionOutcome.ReadyWithWarnings))
            {
                return false;
            }

            snapshot = snapshot with
            {
                State = OpenVinoRouteState.AwaitingConfiguration
            };
            return true;
        }
    }

    public bool TryBeginLoading(Guid operationId) =>
        TryMove(operationId, OpenVinoRouteState.AwaitingConfiguration,
            OpenVinoRouteState.Loading);

    public bool TrySetSessionReady(Guid operationId) =>
        TryMove(operationId, OpenVinoRouteState.Loading,
            OpenVinoRouteState.SessionReady);

    public bool TryBeginTurn(Guid operationId, out Guid turnId)
    {
        lock (stateLock)
        {
            turnId = Guid.Empty;
            if (!IsCurrent(operationId) ||
                snapshot.State != OpenVinoRouteState.SessionReady)
            {
                return false;
            }

            turnId = NextIdentity();
            snapshot = snapshot with
            {
                State = OpenVinoRouteState.GeneratingTurn,
                ActiveTurnId = turnId
            };
            return true;
        }
    }

    public bool TryBeginStopping(Guid operationId, Guid turnId)
    {
        lock (stateLock)
        {
            if (!IsCurrentTurn(operationId, turnId) ||
                snapshot.State != OpenVinoRouteState.GeneratingTurn)
            {
                return false;
            }

            snapshot = snapshot with { State = OpenVinoRouteState.StoppingTurn };
            return true;
        }
    }

    public bool TryCompleteTurn(Guid operationId, Guid turnId)
    {
        lock (stateLock)
        {
            if (!IsCurrentTurn(operationId, turnId) ||
                snapshot.State is not (
                    OpenVinoRouteState.GeneratingTurn or
                    OpenVinoRouteState.StoppingTurn))
            {
                return false;
            }

            snapshot = snapshot with
            {
                State = OpenVinoRouteState.TurnCompleted,
                ActiveTurnId = null
            };
            return true;
        }
    }

    public bool TryReturnToSessionReady(Guid operationId) =>
        TryMove(operationId, OpenVinoRouteState.TurnCompleted,
            OpenVinoRouteState.SessionReady);

    public bool TryBeginCancellation(Guid operationId)
    {
        lock (stateLock)
        {
            if (!IsCurrent(operationId) || snapshot.State is not (
                    OpenVinoRouteState.Loading or
                    OpenVinoRouteState.SessionReady or
                    OpenVinoRouteState.GeneratingTurn or
                    OpenVinoRouteState.StoppingTurn or
                    OpenVinoRouteState.TurnCompleted))
            {
                return false;
            }

            snapshot = snapshot with
            {
                State = OpenVinoRouteState.CancellingSession,
                ActiveTurnId = null
            };
            return true;
        }
    }

    public bool TryMarkCancelled(Guid operationId) =>
        TryMove(operationId, OpenVinoRouteState.CancellingSession,
            OpenVinoRouteState.Cancelled);

    public bool TryCompleteSession(Guid operationId) =>
        TryMove(operationId, OpenVinoRouteState.SessionReady,
            OpenVinoRouteState.SessionCompleted);

    public bool TryFail(Guid operationId, string failureCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        lock (stateLock)
        {
            if (!IsCurrent(operationId) || IsTerminal(snapshot.State))
            {
                return false;
            }

            snapshot = snapshot with
            {
                State = OpenVinoRouteState.Failed,
                ActiveTurnId = null,
                FailureCode = failureCode
            };
            return true;
        }
    }

    public OpenVinoRouteIdentity Reset()
    {
        lock (stateLock)
        {
            if (snapshot.State is not (
                    OpenVinoRouteState.TerminalInspectionOutcome or
                    OpenVinoRouteState.SessionCompleted or
                    OpenVinoRouteState.Failed or
                    OpenVinoRouteState.Cancelled))
            {
                throw new InvalidOperationException(
                    "Only a terminal route operation can be reset.");
            }

            snapshot = FreshSnapshot();
            return snapshot.Identity;
        }
    }

    private bool TryMove(
        Guid operationId,
        OpenVinoRouteState from,
        OpenVinoRouteState to)
    {
        lock (stateLock)
        {
            if (!IsCurrent(operationId) || snapshot.State != from)
            {
                return false;
            }

            snapshot = snapshot with { State = to };
            return true;
        }
    }

    private bool IsCurrent(Guid operationId) =>
        operationId != Guid.Empty && snapshot.Identity.OperationId == operationId;

    private bool IsCurrentTurn(Guid operationId, Guid turnId) =>
        IsCurrent(operationId) &&
        turnId != Guid.Empty &&
        snapshot.ActiveTurnId == turnId;

    private OpenVinoRouteSnapshot FreshSnapshot()
    {
        Guid operationId = NextIdentity();
        Guid workerId = NextDistinctIdentity(operationId);
        Guid processId = NextDistinctIdentity(operationId, workerId);
        Guid sessionId = NextDistinctIdentity(operationId, workerId, processId);
        return new OpenVinoRouteSnapshot(
            OpenVinoRouteState.Idle,
            new OpenVinoRouteIdentity(
                operationId,
                workerId,
                processId,
                sessionId),
            InspectionOutcome: null,
            ActiveTurnId: null,
            FailureCode: null);
    }

    private Guid NextDistinctIdentity(params Guid[] existing)
    {
        Guid value;
        do
        {
            value = NextIdentity();
        }
        while (existing.Contains(value));

        return value;
    }

    private Guid NextIdentity()
    {
        Guid value = identityFactory();
        if (value == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The route identity source returned an empty identity.");
        }

        return value;
    }

    private static bool IsTerminal(OpenVinoRouteState state) => state is
        OpenVinoRouteState.SessionCompleted or
        OpenVinoRouteState.Failed or
        OpenVinoRouteState.Cancelled;
}
