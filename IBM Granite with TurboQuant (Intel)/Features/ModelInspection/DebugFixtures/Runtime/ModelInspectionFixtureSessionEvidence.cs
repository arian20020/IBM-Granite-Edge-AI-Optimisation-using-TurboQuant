#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;

internal sealed record ModelInspectionFixtureServiceCallEvidence(
    int Attempt,
    ModelInspectionRequest Request,
    CancellationToken CancellationToken);

internal sealed record ModelInspectionFixtureCheckpointEvidence(
    int Attempt,
    string Checkpoint);

internal sealed class ModelInspectionFixtureSessionEvidence
{
    private readonly object gate = new();
    private readonly List<ModelInspectionFixtureServiceCallEvidence> serviceCalls = [];
    private readonly List<ModelInspectionFixtureCheckpointEvidence>
        releasedServiceCheckpoints = [];
    private readonly List<int> cancellationObservedAttempts = [];
    private readonly List<ModelInspectionFixtureDeferredEvent>
        capturedDeferredEvents = [];
    private readonly List<int> terminalCompletionAttempts = [];
    private readonly List<string> invokedSetupInteractionIds = [];
    private readonly List<TaskCompletionSource<bool>> pendingZeroWaiters = [];
    private readonly List<ServiceCallWaiter> serviceCallWaiters = [];

    private int activeCancellationRegistrationCount;
    private int cancellationRegistrationCreatedCount;
    private int cancellationRegistrationReleasedCount;
    private int releasedDeferredProgressCount;
    private int releasedDeferredResultSnapshotCount;
    private int releasedDeferredMotionCount;
    private int releasedDeferredAnnouncementCount;
    private int retiredPendingCallCount;
    private int serviceRetirementCount;
    private int serviceDisposalCount;
    private int sessionRetirementCount;
    private int sessionDisposalCount;
    private int animationStartCount;
    private int animationCancellationCount;
    private int animationDriverDisposalCount;
    private int motionSettingsDisposalCount;
    private int hostActivationCount;
    private int pageRetirementCount;
    private int dispatcherCallbackCount;
    private int completedDispatcherCallbackCount;
    private int pendingDispatcherCallbackCount;
    private int motionBatchCount;
    private int completedMotionBatchCount;
    private int pendingMotionBatchCount;
    private int focusRequestCount;
    private int completedFocusRequestCount;
    private int pendingFocusRequestCount;
    private int disclosureOperationCount;
    private int completedDisclosureOperationCount;
    private int pendingDisclosureOperationCount;
    private int liveNotificationCount;
    private int completedLiveNotificationCount;
    private int pendingLiveNotificationCount;

    internal int ServiceCallCount
    {
        get
        {
            lock (gate)
            {
                return serviceCalls.Count;
            }
        }
    }

    internal IReadOnlyList<ModelInspectionFixtureServiceCallEvidence>
        ServiceCalls
    {
        get
        {
            lock (gate)
            {
                return serviceCalls.ToArray();
            }
        }
    }

    internal IReadOnlyList<ModelInspectionFixtureCheckpointEvidence>
        ReleasedServiceCheckpoints
    {
        get
        {
            lock (gate)
            {
                return releasedServiceCheckpoints.ToArray();
            }
        }
    }

    internal int CancellationObservationCount
    {
        get
        {
            lock (gate)
            {
                return cancellationObservedAttempts.Count;
            }
        }
    }

    internal IReadOnlyList<int> CancellationObservedAttempts
    {
        get
        {
            lock (gate)
            {
                return cancellationObservedAttempts.ToArray();
            }
        }
    }

    internal IReadOnlyList<ModelInspectionFixtureDeferredEvent>
        CapturedDeferredEvents
    {
        get
        {
            lock (gate)
            {
                return capturedDeferredEvents.ToArray();
            }
        }
    }

    internal int ActiveCancellationRegistrationCount
    {
        get
        {
            lock (gate)
            {
                return activeCancellationRegistrationCount;
            }
        }
    }

    internal int CancellationRegistrationCreatedCount => Read(
        ref cancellationRegistrationCreatedCount);

    internal int CancellationRegistrationReleasedCount => Read(
        ref cancellationRegistrationReleasedCount);

    internal int ReleasedDeferredProgressCount => Read(
        ref releasedDeferredProgressCount);

    internal int ReleasedDeferredResultSnapshotCount => Read(
        ref releasedDeferredResultSnapshotCount);

    internal int ReleasedDeferredMotionCount => Read(
        ref releasedDeferredMotionCount);

    internal int ReleasedDeferredAnnouncementCount => Read(
        ref releasedDeferredAnnouncementCount);

    internal int TerminalCompletionCount
    {
        get
        {
            lock (gate)
            {
                return terminalCompletionAttempts.Count;
            }
        }
    }

    internal IReadOnlyList<int> TerminalCompletionAttempts
    {
        get
        {
            lock (gate)
            {
                return terminalCompletionAttempts.ToArray();
            }
        }
    }

    internal int RetiredPendingCallCount => Read(ref retiredPendingCallCount);

    internal int ServiceRetirementCount => Read(ref serviceRetirementCount);

    internal int ServiceDisposalCount => Read(ref serviceDisposalCount);

    internal int SessionRetirementCount => Read(ref sessionRetirementCount);

    internal int SessionDisposalCount => Read(ref sessionDisposalCount);

    internal int AnimationStartCount => Read(ref animationStartCount);

    internal int AnimationCancellationCount => Read(
        ref animationCancellationCount);

    internal int AnimationDriverDisposalCount => Read(
        ref animationDriverDisposalCount);

    internal int MotionSettingsDisposalCount => Read(
        ref motionSettingsDisposalCount);

    internal int HostActivationCount => Read(ref hostActivationCount);

    internal int PageRetirementCount => Read(ref pageRetirementCount);

    internal int DispatcherCallbackCount => Read(ref dispatcherCallbackCount);
    internal int CompletedDispatcherCallbackCount => Read(
        ref completedDispatcherCallbackCount);
    internal int PendingDispatcherCallbackCount => Read(ref pendingDispatcherCallbackCount);
    internal int MotionBatchCount => Read(ref motionBatchCount);
    internal int CompletedMotionBatchCount => Read(
        ref completedMotionBatchCount);
    internal int PendingMotionBatchCount => Read(ref pendingMotionBatchCount);
    internal int FocusRequestCount => Read(ref focusRequestCount);
    internal int CompletedFocusRequestCount => Read(
        ref completedFocusRequestCount);
    internal int PendingFocusRequestCount => Read(ref pendingFocusRequestCount);
    internal int DisclosureOperationCount => Read(ref disclosureOperationCount);
    internal int CompletedDisclosureOperationCount => Read(
        ref completedDisclosureOperationCount);
    internal int PendingDisclosureOperationCount => Read(ref pendingDisclosureOperationCount);
    internal int LiveNotificationCount => Read(ref liveNotificationCount);
    internal int CompletedLiveNotificationCount => Read(
        ref completedLiveNotificationCount);
    internal int PendingLiveNotificationCount => Read(ref pendingLiveNotificationCount);

    internal IReadOnlyList<string> InvokedSetupInteractionIds
    {
        get
        {
            lock (gate)
            {
                return invokedSetupInteractionIds.ToArray();
            }
        }
    }

    internal void RecordServiceCall(
        int attempt,
        ModelInspectionRequest request,
        CancellationToken cancellationToken)
    {
        var recorded = new ModelInspectionFixtureServiceCallEvidence(
            attempt,
            request,
            cancellationToken);
        List<TaskCompletionSource<ModelInspectionFixtureServiceCallEvidence>>?
            completedWaiters = null;
        lock (gate)
        {
            int previousCount = serviceCalls.Count;
            serviceCalls.Add(recorded);
            for (int index = serviceCallWaiters.Count - 1; index >= 0; index--)
            {
                ServiceCallWaiter waiter = serviceCallWaiters[index];
                if (waiter.PreviousCount != previousCount)
                {
                    continue;
                }

                completedWaiters ??= [];
                completedWaiters.Add(waiter.Completion);
                serviceCallWaiters.RemoveAt(index);
            }
        }

        if (completedWaiters is null)
        {
            return;
        }

        foreach (TaskCompletionSource<ModelInspectionFixtureServiceCallEvidence>
            completion in completedWaiters)
        {
            completion.TrySetResult(recorded);
        }
    }

    internal Task<ModelInspectionFixtureServiceCallEvidence>
        WaitForNextServiceCallAsync(int previousCount)
    {
        lock (gate)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(previousCount);
            if (serviceCalls.Count == previousCount + 1)
            {
                return Task.FromResult(serviceCalls[^1]);
            }

            if (serviceCalls.Count != previousCount)
            {
                throw new InvalidOperationException(
                    "The fixture service-call audit did not advance by exactly one call.");
            }

            var completion = new TaskCompletionSource<
                ModelInspectionFixtureServiceCallEvidence>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            serviceCallWaiters.Add(new ServiceCallWaiter(
                previousCount,
                completion));
            return completion.Task;
        }
    }

    internal void RecordCancellationRegistrationCreated()
    {
        lock (gate)
        {
            activeCancellationRegistrationCount++;
            cancellationRegistrationCreatedCount++;
        }
    }

    internal void RecordCancellationRegistrationReleased()
    {
        lock (gate)
        {
            if (activeCancellationRegistrationCount <= 0)
            {
                throw new InvalidOperationException(
                    "The fixture cancellation-registration audit is unbalanced.");
            }

            activeCancellationRegistrationCount--;
            cancellationRegistrationReleasedCount++;
            CompletePendingZeroWaitersIfDrainedLocked();
        }
    }

    internal Task WaitForPendingZeroAsync(
        CancellationToken cancellationToken = default)
    {
        Task wait;
        lock (gate)
        {
            if (AllPendingCountsAreZeroLocked())
            {
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            pendingZeroWaiters.Add(completion);
            wait = completion.Task;
        }

        return cancellationToken.CanBeCanceled
            ? wait.WaitAsync(cancellationToken)
            : wait;
    }

    internal void RecordCancellationObserved(int attempt)
    {
        lock (gate)
        {
            if (!cancellationObservedAttempts.Contains(attempt))
            {
                cancellationObservedAttempts.Add(attempt);
            }
        }
    }

    internal void RecordServiceCheckpoint(int attempt, string checkpoint)
    {
        lock (gate)
        {
            releasedServiceCheckpoints.Add(
                new ModelInspectionFixtureCheckpointEvidence(
                    attempt,
                    checkpoint));
        }
    }

    internal void RecordDeferredCapture(
        ModelInspectionFixtureDeferredEvent deferredEvent)
    {
        lock (gate)
        {
            capturedDeferredEvents.Add(deferredEvent);
        }
    }

    internal void RecordDeferredProgressRelease() => Increment(
        ref releasedDeferredProgressCount);

    internal void RecordDeferredResultSnapshotRelease() => Increment(
        ref releasedDeferredResultSnapshotCount);

    internal void RecordDeferredMotionRelease() => Increment(
        ref releasedDeferredMotionCount);

    internal void RecordDeferredAnnouncementRelease() => Increment(
        ref releasedDeferredAnnouncementCount);

    internal void RecordTerminalCompletion(int attempt)
    {
        lock (gate)
        {
            if (terminalCompletionAttempts.Contains(attempt))
            {
                throw new InvalidOperationException(
                    "The fixture service completed one attempt more than once.");
            }

            terminalCompletionAttempts.Add(attempt);
        }
    }

    internal void RecordRetiredPendingCall() => Increment(
        ref retiredPendingCallCount);

    internal void RecordServiceRetirement() => Increment(
        ref serviceRetirementCount);

    internal void RecordServiceDisposal() => Increment(
        ref serviceDisposalCount);

    internal void RecordSessionRetirement() => Increment(
        ref sessionRetirementCount);

    internal void RecordSessionDisposal() => Increment(
        ref sessionDisposalCount);

    internal void RecordAnimationStart() => Increment(
        ref animationStartCount);

    internal void RecordAnimationCancellation() => Increment(
        ref animationCancellationCount);

    internal void RecordAnimationDriverDisposal() => Increment(
        ref animationDriverDisposalCount);

    internal void RecordMotionSettingsDisposal() => Increment(
        ref motionSettingsDisposalCount);

    internal void RecordHostActivation() => Increment(ref hostActivationCount);

    internal void RecordPageRetirement() => Increment(ref pageRetirementCount);

    internal void RecordInvokedSetupInteraction(string interactionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interactionId);
        lock (gate)
        {
            invokedSetupInteractionIds.Add(interactionId);
        }
    }

    internal IDisposable BeginDispatcherCallback() => Begin(AuditKind.Dispatcher);

    internal IDisposable BeginMotionBatch() => Begin(AuditKind.Motion);

    internal IDisposable BeginFocusRequest() => Begin(AuditKind.Focus);

    internal IDisposable BeginDisclosureOperation() => Begin(AuditKind.Disclosure);

    internal IDisposable BeginLiveNotification() => Begin(AuditKind.LiveNotification);

    private int Read(ref int value)
    {
        lock (gate)
        {
            return value;
        }
    }

    private void Increment(ref int value)
    {
        lock (gate)
        {
            value = checked(value + 1);
        }
    }

    private IDisposable Begin(AuditKind kind)
    {
        lock (gate)
        {
            IncrementPending(kind);
        }

        return new Completion(() =>
        {
            lock (gate)
            {
                DecrementPending(kind);
            }
        });
    }

    private void IncrementPending(AuditKind kind)
    {
        switch (kind)
        {
            case AuditKind.Dispatcher: dispatcherCallbackCount++; pendingDispatcherCallbackCount++; break;
            case AuditKind.Motion: motionBatchCount++; pendingMotionBatchCount++; break;
            case AuditKind.Focus: focusRequestCount++; pendingFocusRequestCount++; break;
            case AuditKind.Disclosure: disclosureOperationCount++; pendingDisclosureOperationCount++; break;
            case AuditKind.LiveNotification: liveNotificationCount++; pendingLiveNotificationCount++; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private void DecrementPending(AuditKind kind)
    {
        switch (kind)
        {
            case AuditKind.Dispatcher:
                Complete(
                    ref pendingDispatcherCallbackCount,
                    ref completedDispatcherCallbackCount);
                break;
            case AuditKind.Motion:
                Complete(
                    ref pendingMotionBatchCount,
                    ref completedMotionBatchCount);
                break;
            case AuditKind.Focus:
                Complete(
                    ref pendingFocusRequestCount,
                    ref completedFocusRequestCount);
                break;
            case AuditKind.Disclosure:
                Complete(
                    ref pendingDisclosureOperationCount,
                    ref completedDisclosureOperationCount);
                break;
            case AuditKind.LiveNotification:
                Complete(
                    ref pendingLiveNotificationCount,
                    ref completedLiveNotificationCount);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        CompletePendingZeroWaitersIfDrainedLocked();
    }

    private bool AllPendingCountsAreZeroLocked() =>
        activeCancellationRegistrationCount == 0 &&
        pendingDispatcherCallbackCount == 0 &&
        pendingMotionBatchCount == 0 &&
        pendingFocusRequestCount == 0 &&
        pendingDisclosureOperationCount == 0 &&
        pendingLiveNotificationCount == 0;

    private void CompletePendingZeroWaitersIfDrainedLocked()
    {
        if (!AllPendingCountsAreZeroLocked() || pendingZeroWaiters.Count == 0)
        {
            return;
        }

        TaskCompletionSource<bool>[] completed = [.. pendingZeroWaiters];
        pendingZeroWaiters.Clear();
        foreach (TaskCompletionSource<bool> waiter in completed)
        {
            waiter.TrySetResult(true);
        }
    }

    private static void Complete(ref int pending, ref int completed)
    {
        if (pending <= 0)
        {
            throw new InvalidOperationException(
                "The fixture pending-operation audit is unbalanced.");
        }

        pending--;
        completed = checked(completed + 1);
    }

    private sealed class Completion : IDisposable
    {
        private readonly Action complete;
        private int disposed;

        internal Completion(Action complete)
        {
            this.complete = complete;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                complete();
            }
        }
    }

    private sealed record ServiceCallWaiter(
        int PreviousCount,
        TaskCompletionSource<ModelInspectionFixtureServiceCallEvidence>
            Completion);

    private enum AuditKind
    {
        Dispatcher,
        Motion,
        Focus,
        Disclosure,
        LiveNotification
    }
}
#endif
