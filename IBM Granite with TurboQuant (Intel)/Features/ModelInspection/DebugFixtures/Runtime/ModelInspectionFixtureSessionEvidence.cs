#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;
using System.Collections.Generic;
using System.Threading;

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

    private int activeCancellationRegistrationCount;
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

    internal void RecordServiceCall(
        int attempt,
        ModelInspectionRequest request,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            serviceCalls.Add(new ModelInspectionFixtureServiceCallEvidence(
                attempt,
                request,
                cancellationToken));
        }
    }

    internal void RecordCancellationRegistrationCreated()
    {
        lock (gate)
        {
            activeCancellationRegistrationCount++;
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
        }
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
}
#endif
