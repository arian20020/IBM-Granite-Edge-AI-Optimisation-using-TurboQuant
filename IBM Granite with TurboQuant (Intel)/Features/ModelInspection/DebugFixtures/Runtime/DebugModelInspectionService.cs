#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.ModelInspection.Fixtures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;

internal sealed class DebugModelInspectionService :
    IModelInspectionService,
    IDisposable
{
    private readonly object gate = new();
    private readonly ModelInspectionFixtureExecutionPlan plan;
    private readonly ModelInspectionFixtureSessionEvidence evidence;
    private readonly ModelInspectionFixtureDeferredEventRegistry deferredEvents;
    private readonly Action? beforeProgressDelivery;
    private readonly Action? beforeTerminalDelivery;
    private readonly Dictionary<int, CallState> calls = [];
    private readonly ModelInspectionFixtureOperationDrain deliveries;

    private int nextAttemptIndex;
    private CallState[] retirementPending = [];
    private bool retired;
    private bool disposed;
    private bool disposalStarted;
    private bool disposalCompleted;
    private ExceptionDispatchInfo? disposalError;

    internal DebugModelInspectionService(
        ModelInspectionFixtureExecutionPlan plan,
        ModelInspectionFixtureSessionEvidence evidence,
        Action? beforeProgressDelivery = null,
        Action? beforeTerminalDelivery = null) : this(
            plan,
            evidence,
            new ModelInspectionFixtureDeferredEventRegistry(
                plan?.DeferredEvents ?? throw new ArgumentNullException(
                    nameof(plan)),
                evidence ?? throw new ArgumentNullException(nameof(evidence))),
            beforeProgressDelivery,
            beforeTerminalDelivery)
    {
    }

    internal DebugModelInspectionService(
        ModelInspectionFixtureExecutionPlan plan,
        ModelInspectionFixtureSessionEvidence evidence,
        ModelInspectionFixtureDeferredEventRegistry deferredEvents,
        Action? beforeProgressDelivery = null,
        Action? beforeTerminalDelivery = null,
        ModelInspectionFixtureOperationDomain? lifetimeDomain = null)
    {
        this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
        this.evidence = evidence ??
            throw new ArgumentNullException(nameof(evidence));
        this.deferredEvents = deferredEvents ??
            throw new ArgumentNullException(nameof(deferredEvents));
        this.beforeProgressDelivery = beforeProgressDelivery;
        this.beforeTerminalDelivery = beforeTerminalDelivery;
        deliveries = new ModelInspectionFixtureOperationDrain(
            lifetimeDomain ?? new ModelInspectionFixtureOperationDomain());
    }

    internal int StartedAttemptCount
    {
        get
        {
            lock (gate)
            {
                return nextAttemptIndex;
            }
        }
    }

    internal int PendingCallCount
    {
        get
        {
            lock (gate)
            {
                return calls.Count;
            }
        }
    }

    public Task<ModelInspectionExecutionResult> InspectAsync(
        ModelInspectionRequest request,
        IProgress<ModelInspectionProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        CallState call;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retired || disposed, this);
            if (!ReferenceEquals(request, plan.Request))
            {
                throw new ArgumentException(
                    "The fixture service requires its exact planned request.",
                    nameof(request));
            }

            if (nextAttemptIndex >= plan.Attempts.Count)
            {
                throw new InvalidOperationException(
                    "The fixture service has no declared attempt remaining.");
            }

            ModelInspectionFixtureAttemptPlan attempt =
                plan.Attempts[nextAttemptIndex];
            int expectedAttempt = checked(nextAttemptIndex + 1);
            if (attempt.Attempt != expectedAttempt ||
                calls.ContainsKey(attempt.Attempt))
            {
                throw new InvalidOperationException(
                    "The fixture attempt sequence is not contiguous.");
            }

            call = new CallState(attempt, progress);
            calls.Add(attempt.Attempt, call);
            nextAttemptIndex++;
            if (cancellationToken.CanBeCanceled)
            {
                evidence.RecordCancellationRegistrationCreated();
                try
                {
                    call.SetCancellationRegistration(
                        cancellationToken.Register(
                            () => evidence.RecordCancellationObserved(
                                attempt.Attempt)),
                        evidence);
                }
                catch
                {
                    evidence.RecordCancellationRegistrationReleased();
                    calls.Remove(attempt.Attempt);
                    nextAttemptIndex--;
                    throw;
                }
            }

            evidence.RecordServiceCall(
                attempt.Attempt,
                request,
                cancellationToken);
        }

        ProcessAutomaticSteps(call.Attempt.Attempt);
        return call.Completion.Task;
    }

    internal void ReleaseServiceCheckpoint(int attempt, string checkpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpoint);
        PreparedEffect prepared;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retired || disposed, this);
            CallState call = GetPendingCallLocked(attempt);
            ModelInspectionFixtureServiceStepPlan step = NextStepLocked(call);
            if (step.TriggerKind !=
                    ModelInspectionFixtureServiceTriggerKind.Checkpoint ||
                !string.Equals(
                    step.Checkpoint,
                    checkpoint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The fixture service checkpoint is not the exact next step.");
            }

            call.NextStepIndex++;
            prepared = PrepareEffectLocked(call, step);
        }

        evidence.RecordServiceCheckpoint(attempt, checkpoint);
        Dispatch(prepared);
        ProcessAutomaticSteps(attempt);
    }

    internal bool Retire()
    {
        bool first;
        lock (gate)
        {
            first = !retired;
            if (first)
            {
                retired = true;
                retirementPending = calls.Values.ToArray();
                calls.Clear();
                foreach (CallState call in retirementPending)
                {
                    call.Settled = true;
                }
            }
        }

        deliveries.Close(FinalizeRetirement);
        return first;
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
        }

        ExceptionDispatchInfo? error = null;
        AttemptCleanup(() => Retire(), ref error);
        if (!deliveries.CurrentThreadOwnsOperation)
        {
            AttemptCleanup(CompleteDisposal, ref error);
        }

        error?.Throw();
    }

    private void ProcessAutomaticSteps(int attempt)
    {
        while (true)
        {
            PreparedEffect prepared;
            lock (gate)
            {
                if (retired || disposed ||
                    !calls.TryGetValue(attempt, out CallState? call) ||
                    call.Settled ||
                    call.TerminalPrepared)
                {
                    return;
                }

                if (call.NextStepIndex >= call.Attempt.ServiceSteps.Count)
                {
                    return;
                }

                ModelInspectionFixtureServiceStepPlan step =
                    call.Attempt.ServiceSteps[call.NextStepIndex];
                if (step.TriggerKind !=
                    ModelInspectionFixtureServiceTriggerKind.Automatic)
                {
                    return;
                }

                call.NextStepIndex++;
                prepared = PrepareEffectLocked(call, step);
            }

            Dispatch(prepared);
        }
    }

    private PreparedEffect PrepareEffectLocked(
        CallState call,
        ModelInspectionFixtureServiceStepPlan step)
    {
        int payloadCount =
            (step.Progress is null ? 0 : 1) +
            (step.TerminalResult is null ? 0 : 1) +
            (step.DeferredEvent is null ? 0 : 1);
        if (payloadCount != 1)
        {
            throw new InvalidOperationException(
                "The mapped fixture service step is not a closed effect.");
        }

        if (step.Progress is not null)
        {
            return PreparedEffect.ForProgress(
                call,
                step.Progress,
                deliveries.Enter());
        }

        if (step.TerminalResult is not null)
        {
            call.TerminalPrepared = true;
            return PreparedEffect.ForTerminal(
                call,
                step.TerminalResult,
                deliveries.Enter());
        }

        ModelInspectionFixtureDeferredEvent deferred = step.DeferredEvent!;
        if (deferred.OwnerAttempt != call.Attempt.Attempt)
        {
            throw new InvalidOperationException(
                "The deferred fixture event has the wrong attempt owner.");
        }

        IProgress<ModelInspectionProgress>? deferredProgress =
            deferred.Kind == ModelInspectionFixtureDeferredEventKind.StaleProgress
                ? call.Progress ?? throw new InvalidOperationException(
                    "A declared stale-progress event requires a progress reporter.")
                : null;
        return PreparedEffect.ForDeferred(
            deferred,
            deferredProgress,
            deliveries.Enter());
    }

    private void Dispatch(PreparedEffect prepared)
    {
        using (prepared.DeliveryLease)
        {
            if (prepared.Progress is not null)
            {
                beforeProgressDelivery?.Invoke();
                prepared.Call.Progress?.Report(prepared.Progress);
                return;
            }

            if (prepared.TerminalResult is not null)
            {
                beforeTerminalDelivery?.Invoke();
                CompleteTerminal(prepared.Call, prepared.TerminalResult);
                return;
            }

            ModelInspectionFixtureDeferredEvent deferred =
                prepared.DeferredEvent!;
            deferredEvents.Capture(deferred, prepared.DeferredProgressReporter);
            evidence.RecordDeferredCapture(deferred);
        }
    }

    private void FinalizeRetirement()
    {
        evidence.RecordServiceRetirement();
        deferredEvents.Retire();
        foreach (CallState call in retirementPending)
        {
            call.ReleaseCancellationRegistration();
            if (call.Completion.TrySetCanceled())
            {
                evidence.RecordRetiredPendingCall();
            }
        }

        bool disposeRequested;
        lock (gate)
        {
            disposeRequested = disposed;
        }

        if (disposeRequested)
        {
            CompleteDisposal();
        }
    }

    private void CompleteDisposal()
    {
        bool perform;
        lock (gate)
        {
            perform = !disposalStarted;
            if (perform)
            {
                disposalStarted = true;
            }
            else
            {
                while (!disposalCompleted)
                {
                    Monitor.Wait(gate);
                }

                disposalError?.Throw();
                return;
            }
        }

        ExceptionDispatchInfo? error = null;
        try
        {
            evidence.RecordServiceDisposal();
        }
        catch (Exception exception)
        {
            error = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            lock (gate)
            {
                disposalError = error;
                disposalCompleted = true;
                Monitor.PulseAll(gate);
            }
        }

        error?.Throw();
    }

    private static void AttemptCleanup(
        Action cleanupAction,
        ref ExceptionDispatchInfo? error)
    {
        try
        {
            cleanupAction();
        }
        catch (Exception exception)
        {
            error ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    private void CompleteTerminal(
        CallState call,
        ModelInspectionExecutionResult result)
    {
        bool deliver;
        lock (gate)
        {
            deliver = !retired &&
                !disposed &&
                !call.Settled &&
                calls.TryGetValue(call.Attempt.Attempt, out CallState? current) &&
                ReferenceEquals(call, current);
            if (deliver)
            {
                call.Settled = true;
                calls.Remove(call.Attempt.Attempt);
            }
        }

        if (!deliver)
        {
            return;
        }

        call.ReleaseCancellationRegistration();
        if (!call.Completion.TrySetResult(result))
        {
            throw new InvalidOperationException(
                "The fixture service attempted to complete one call more than once.");
        }

        evidence.RecordTerminalCompletion(call.Attempt.Attempt);
    }

    private CallState GetPendingCallLocked(int attempt)
    {
        if (!calls.TryGetValue(attempt, out CallState? call) ||
            call.Settled ||
            call.TerminalPrepared)
        {
            throw new InvalidOperationException(
                "The fixture attempt is not active.");
        }

        return call;
    }

    private static ModelInspectionFixtureServiceStepPlan NextStepLocked(
        CallState call)
    {
        if (call.NextStepIndex >= call.Attempt.ServiceSteps.Count)
        {
            throw new InvalidOperationException(
                "The fixture attempt has no service step remaining.");
        }

        return call.Attempt.ServiceSteps[call.NextStepIndex];
    }

    private sealed class CallState
    {
        private CancellationTokenRegistration cancellationRegistration;
        private ModelInspectionFixtureSessionEvidence? registrationEvidence;
        private int cancellationRegistrationReleased;

        internal CallState(
            ModelInspectionFixtureAttemptPlan attempt,
            IProgress<ModelInspectionProgress>? progress)
        {
            Attempt = attempt;
            Progress = progress;
            Completion = new TaskCompletionSource<ModelInspectionExecutionResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal ModelInspectionFixtureAttemptPlan Attempt { get; }

        internal IProgress<ModelInspectionProgress>? Progress { get; }

        internal TaskCompletionSource<ModelInspectionExecutionResult> Completion
            { get; }

        internal int NextStepIndex { get; set; }

        internal bool TerminalPrepared { get; set; }

        internal bool Settled { get; set; }

        internal void SetCancellationRegistration(
            CancellationTokenRegistration registration,
            ModelInspectionFixtureSessionEvidence evidence)
        {
            cancellationRegistration = registration;
            registrationEvidence = evidence;
        }

        internal void ReleaseCancellationRegistration()
        {
            if (registrationEvidence is null ||
                Interlocked.Exchange(
                    ref cancellationRegistrationReleased,
                    1) != 0)
            {
                return;
            }

            cancellationRegistration.Dispose();
            registrationEvidence.RecordCancellationRegistrationReleased();
        }
    }

    private sealed record PreparedEffect(
        CallState Call,
        ModelInspectionProgress? Progress,
        ModelInspectionExecutionResult? TerminalResult,
        ModelInspectionFixtureDeferredEvent? DeferredEvent,
        IProgress<ModelInspectionProgress>? DeferredProgressReporter,
        ModelInspectionFixtureOperationDrain.Lease DeliveryLease)
    {
        internal static PreparedEffect ForProgress(
            CallState call,
            ModelInspectionProgress progress,
            ModelInspectionFixtureOperationDrain.Lease deliveryLease) => new(
            call,
            progress,
            TerminalResult: null,
            DeferredEvent: null,
            DeferredProgressReporter: null,
            deliveryLease);

        internal static PreparedEffect ForTerminal(
            CallState call,
            ModelInspectionExecutionResult result,
            ModelInspectionFixtureOperationDrain.Lease deliveryLease) => new(
            call,
            Progress: null,
            result,
            DeferredEvent: null,
            DeferredProgressReporter: null,
            deliveryLease);

        internal static PreparedEffect ForDeferred(
            ModelInspectionFixtureDeferredEvent deferredEvent,
            IProgress<ModelInspectionProgress>? progressReporter,
            ModelInspectionFixtureOperationDrain.Lease deliveryLease) => new(
            Call: null!,
            Progress: null,
            TerminalResult: null,
            deferredEvent,
            progressReporter,
            deliveryLease);
    }
}
#endif
