using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal sealed class ModelInspectionMilestoneSequencer : IDisposable
{
    internal static readonly TimeSpan MinimumVisibleStage =
        TimeSpan.FromMilliseconds(550);

    private readonly IModelInspectionMilestoneScheduler scheduler;
    private readonly Action<ModelInspectionViewSnapshot> applySnapshot;
    private readonly StageSlot[] stages =
        [new(), new(), new(), new(), new()];

    private ModelInspectionViewSnapshot? latestSemanticSnapshot;
    private ModelInspectionRenderKey? latestAcceptedKey;
    private ModelInspectionViewSnapshot? currentActive;
    private ModelInspectionViewSnapshot? currentCompletion;
    private ModelInspectionViewSnapshot? normalTerminal;
    private IDisposable? scheduledDwell;
    private TimeSpan activePresentedAt;
    private long attemptGeneration = -1;
    private Guid acceptedRunId;
    private bool retryAwaitingStageOne;
    private bool retryBindingAwaitingPresentation;
    private long epoch;
    private long scheduleRevision;
    private int currentStage = -1;
    private int unpacedStage = -1;
    private PlaybackPhase phase;
    private bool animationsEnabled;
    private bool invalidated;
    private bool disposed;

    internal ModelInspectionMilestoneSequencer(
        IModelInspectionMilestoneScheduler scheduler,
        Action<ModelInspectionViewSnapshot> applySnapshot,
        bool animationsEnabled)
    {
        this.scheduler = scheduler ??
            throw new ArgumentNullException(nameof(scheduler));
        this.applySnapshot = applySnapshot ??
            throw new ArgumentNullException(nameof(applySnapshot));
        this.animationsEnabled = animationsEnabled;
    }

    internal bool HasPendingPlayback =>
        !invalidated &&
        animationsEnabled &&
        (currentStage >= 0 ||
         normalTerminal is not null ||
         HasRetainedStage());

    internal void Accept(ModelInspectionViewSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (invalidated)
        {
            return;
        }

        long generation = snapshot.RenderKey.AttemptGeneration;
        if (generation == attemptGeneration &&
            acceptedRunId != Guid.Empty &&
            snapshot.ModelInspectionRunId != acceptedRunId)
        {
            return;
        }
        if (generation < attemptGeneration ||
            (generation == attemptGeneration &&
             latestAcceptedKey is ModelInspectionRenderKey latest &&
             snapshot.RenderKey.PresentationRevision <=
                latest.PresentationRevision))
        {
            return;
        }

        bool retryFromCancelledAttempt = generation > attemptGeneration &&
            latestSemanticSnapshot?.TerminalResult?.Status ==
                ModelInspectionExecutionStatus.Cancelled;
        bool newerAttempt = generation > attemptGeneration;
        if (newerAttempt)
        {
            attemptGeneration = generation;
            acceptedRunId = snapshot.ModelInspectionRunId;
            ClearPlayback();
            unpacedStage = -1;
            retryAwaitingStageOne = retryFromCancelledAttempt;
        }

        if (acceptedRunId == Guid.Empty &&
            snapshot.ModelInspectionRunId != Guid.Empty)
        {
            acceptedRunId = snapshot.ModelInspectionRunId;
        }

        latestAcceptedKey = snapshot.RenderKey;
        latestSemanticSnapshot = snapshot;

        if (retryAwaitingStageOne)
        {
            if (!IsFreshStageOne(snapshot) &&
                !IsFreshStageOneCompletion(snapshot))
            {
                // A new attempt identity alone is not a rendered retry. Keep
                // the complete cancelled card until the worker has supplied
                // a same-run stage-one observation that can be attached to
                // the visual tree.
                return;
            }

            retryAwaitingStageOne = false;
            ModelInspectionViewSnapshot binding = CreateRetryBinding(snapshot);
            retryBindingAwaitingPresentation = true;
            AcceptActive(binding, binding.Progress!);
            if (snapshot.Progress!.StageStatus is
                ModelInspectionStageStatus.Completed or
                ModelInspectionStageStatus.Warning)
            {
                AcceptCompletion(snapshot, snapshot.Progress);
            }
            return;
        }

        if (!animationsEnabled)
        {
            TrackUnpacedStage(snapshot);
            applySnapshot(snapshot);
            return;
        }

        if (IsImmediateSafetySnapshot(snapshot))
        {
            ClearPlayback();
            unpacedStage = -1;
            applySnapshot(snapshot);
            return;
        }

        if (TryApplyUnpacedContinuation(snapshot))
        {
            return;
        }

        ModelInspectionProgress? progress = snapshot.Progress;
        if (progress is not null)
        {
            if (progress.StageStatus == ModelInspectionStageStatus.Active)
            {
                AcceptActive(snapshot, progress);
            }
            else if (progress.StageStatus is
                ModelInspectionStageStatus.Completed or
                ModelInspectionStageStatus.Warning)
            {
                AcceptCompletion(snapshot, progress);
            }

            return;
        }

        if (snapshot.TerminalResult?.Status ==
            ModelInspectionExecutionStatus.Completed)
        {
            AcceptNormalTerminal(snapshot);
            return;
        }

        applySnapshot(snapshot);
    }

    internal ModelInspectionViewSnapshot? NotifyPresented(
        ModelInspectionRenderKey renderKey)
    {
        if (invalidated)
        {
            return null;
        }

        if (currentStage < 0)
        {
            // Reduced-motion playback applies the authoritative snapshot
            // directly. It still needs the same exact final-frame handshake.
            return IsFinalCompletion(latestSemanticSnapshot) &&
                latestSemanticSnapshot!.RenderKey == renderKey
                    ? latestSemanticSnapshot
                    : null;
        }

        if (phase == PlaybackPhase.ActiveAwaitingPresentation &&
            currentActive?.RenderKey == renderKey)
        {
            retryBindingAwaitingPresentation = false;
            activePresentedAt = scheduler.Elapsed;
            if (animationsEnabled)
            {
                phase = PlaybackPhase.ActiveDwelling;
                ScheduleDwell(MinimumVisibleStage);
            }
            else
            {
                phase = PlaybackPhase.ActiveReady;
                TryReleaseCurrentCompletion();
            }
            return null;
        }

        if (phase == PlaybackPhase.CompletionAwaitingPresentation &&
            currentCompletion?.RenderKey == renderKey)
        {
            ModelInspectionViewSnapshot completed = currentCompletion;
            CompleteCurrentStage();
            return IsFinalCompletion(completed) &&
                currentStage < 0 &&
                !HasRetainedStage() &&
                normalTerminal is null
                    ? completed
                    : null;
        }

        return null;
    }

    internal void SetAnimationsEnabled(bool enabled)
    {
        if (invalidated || animationsEnabled == enabled)
        {
            return;
        }

        animationsEnabled = enabled;
        if (enabled)
        {
            return;
        }

        ClearPlayback();
        ModelInspectionViewSnapshot? latest = latestSemanticSnapshot;
        if (latest is null)
        {
            unpacedStage = -1;
            return;
        }

        TrackUnpacedStage(latest);
        applySnapshot(latest);
    }

    internal void Invalidate()
    {
        if (invalidated)
        {
            return;
        }

        invalidated = true;
        ClearPlayback();
        latestSemanticSnapshot = null;
        latestAcceptedKey = null;
        unpacedStage = -1;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Invalidate();
        scheduler.Dispose();
    }

    private void AcceptActive(
        ModelInspectionViewSnapshot snapshot,
        ModelInspectionProgress progress)
    {
        int stage = StageIndex(progress.Stage);
        if (currentStage == stage)
        {
            if (retryBindingAwaitingPresentation &&
                phase == PlaybackPhase.ActiveAwaitingPresentation)
            {
                // Do not replace the render key of a frame that has not yet
                // reached the visual tree. A fast same-stage update may be
                // retained, but it cannot supersede the zero frame and strand
                // NotifyPresented waiting for a key that was never rendered.
                stages[stage].Active = snapshot;
                return;
            }

            if (phase != PlaybackPhase.CompletionAwaitingPresentation &&
                currentActive is not null &&
                IsFractionOnlyChange(currentActive, snapshot))
            {
                currentActive = snapshot;
                stages[stage].Active = snapshot;
                applySnapshot(snapshot);
            }

            return;
        }

        if (currentStage > stage)
        {
            return;
        }

        StageSlot slot = stages[stage];
        if (slot.Active is null ||
            IsFractionOnlyChange(slot.Active, snapshot))
        {
            slot.Active = snapshot;
        }

        DrivePlayback();
    }

    private void AcceptCompletion(
        ModelInspectionViewSnapshot snapshot,
        ModelInspectionProgress progress)
    {
        int stage = StageIndex(progress.Stage);
        StageSlot slot = stages[stage];
        slot.Completion = snapshot;

        if (currentStage == stage)
        {
            currentCompletion = snapshot;
            if (phase == PlaybackPhase.CompletionAwaitingPresentation)
            {
                applySnapshot(snapshot);
            }
            else
            {
                TryReleaseCurrentCompletion();
            }

            return;
        }

        if (currentStage < 0 && slot.Active is null)
        {
            currentStage = stage;
            currentCompletion = snapshot;
            phase = PlaybackPhase.CompletionAwaitingPresentation;
            applySnapshot(snapshot);
            return;
        }

        DrivePlayback();
    }

    private void AcceptNormalTerminal(ModelInspectionViewSnapshot snapshot)
    {
        if (currentStage >= 0 || HasRetainedStage())
        {
            normalTerminal = snapshot;
            return;
        }

        unpacedStage = -1;
        applySnapshot(snapshot);
    }

    private void DrivePlayback()
    {
        if (currentStage >= 0)
        {
            return;
        }

        for (int stage = 0; stage < stages.Length; stage++)
        {
            StageSlot slot = stages[stage];
            if (slot.Active is not null)
            {
                currentStage = stage;
                currentActive = slot.Active;
                currentCompletion = slot.Completion;
                phase = PlaybackPhase.ActiveAwaitingPresentation;
                applySnapshot(currentActive);
                return;
            }

            if (slot.Completion is not null)
            {
                currentStage = stage;
                currentActive = null;
                currentCompletion = slot.Completion;
                phase = PlaybackPhase.CompletionAwaitingPresentation;
                applySnapshot(currentCompletion);
                return;
            }
        }

        if (normalTerminal is ModelInspectionViewSnapshot terminal)
        {
            normalTerminal = null;
            unpacedStage = -1;
            applySnapshot(terminal);
        }
    }

    private void TryReleaseCurrentCompletion()
    {
        if (currentCompletion is null)
        {
            return;
        }

        if (phase == PlaybackPhase.ActiveDwelling &&
            scheduler.Elapsed - activePresentedAt >= MinimumVisibleStage)
        {
            CancelDwell();
            phase = PlaybackPhase.ActiveReady;
        }

        if (phase != PlaybackPhase.ActiveReady)
        {
            return;
        }

        CancelDwell();
        phase = PlaybackPhase.CompletionAwaitingPresentation;
        applySnapshot(currentCompletion);
    }

    private void ScheduleDwell(TimeSpan delay)
    {
        long scheduledEpoch = epoch;
        long revision = ++scheduleRevision;
        bool invokedSynchronously = false;
        IDisposable registration = scheduler.Schedule(delay, () =>
        {
            invokedSynchronously = true;
            OnDwellElapsed(scheduledEpoch, revision);
        });
        if (invokedSynchronously ||
            invalidated ||
            epoch != scheduledEpoch ||
            scheduleRevision != revision ||
            phase != PlaybackPhase.ActiveDwelling)
        {
            registration.Dispose();
            return;
        }

        scheduledDwell = registration;
    }

    private void OnDwellElapsed(long scheduledEpoch, long revision)
    {
        if (invalidated ||
            epoch != scheduledEpoch ||
            scheduleRevision != revision ||
            phase != PlaybackPhase.ActiveDwelling)
        {
            return;
        }

        scheduledDwell = null;
        phase = PlaybackPhase.ActiveReady;
        TryReleaseCurrentCompletion();
    }

    private void CompleteCurrentStage()
    {
        int completedStage = currentStage;
        stages[completedStage].Clear();
        currentStage = -1;
        currentActive = null;
        currentCompletion = null;
        retryBindingAwaitingPresentation = false;
        phase = PlaybackPhase.None;
        DrivePlayback();
    }

    private bool TryApplyUnpacedContinuation(
        ModelInspectionViewSnapshot snapshot)
    {
        ModelInspectionProgress? progress = snapshot.Progress;
        if (unpacedStage < 0 || progress is null)
        {
            if (snapshot.TerminalResult is not null)
            {
                unpacedStage = -1;
            }

            return false;
        }

        int stage = StageIndex(progress.Stage);
        if (stage != unpacedStage)
        {
            unpacedStage = -1;
            return false;
        }

        if (progress.StageStatus is
            ModelInspectionStageStatus.Active or
            ModelInspectionStageStatus.Completed or
            ModelInspectionStageStatus.Warning)
        {
            applySnapshot(snapshot);
            if (progress.StageStatus != ModelInspectionStageStatus.Active)
            {
                unpacedStage = -1;
            }

            return true;
        }

        return false;
    }

    private void TrackUnpacedStage(ModelInspectionViewSnapshot snapshot)
    {
        ModelInspectionProgress? progress = snapshot.Progress;
        if (progress?.StageStatus == ModelInspectionStageStatus.Active)
        {
            unpacedStage = StageIndex(progress.Stage);
        }
        else if (progress is not null || snapshot.TerminalResult is not null)
        {
            unpacedStage = -1;
        }
    }

    private void ClearPlayback()
    {
        epoch = checked(epoch + 1);
        CancelDwell();
        foreach (StageSlot stage in stages)
        {
            stage.Clear();
        }

        currentStage = -1;
        currentActive = null;
        currentCompletion = null;
        normalTerminal = null;
        retryBindingAwaitingPresentation = false;
        phase = PlaybackPhase.None;
    }

    private void CancelDwell()
    {
        scheduleRevision = checked(scheduleRevision + 1);
        IDisposable? registration = scheduledDwell;
        scheduledDwell = null;
        registration?.Dispose();
    }

    private bool HasRetainedStage()
    {
        foreach (StageSlot stage in stages)
        {
            if (stage.Active is not null || stage.Completion is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsImmediateSafetySnapshot(
        ModelInspectionViewSnapshot snapshot) =>
        snapshot.IsCancellationRequested ||
        snapshot.Progress?.StageStatus is
            ModelInspectionStageStatus.Failed or
            ModelInspectionStageStatus.Cancelled ||
        snapshot.TerminalResult?.Status is
            ModelInspectionExecutionStatus.Cancelled or
            ModelInspectionExecutionStatus.OperationalFailure;

    private static bool IsFreshStageOne(
        ModelInspectionViewSnapshot snapshot) =>
        snapshot.IsRunActive &&
        !snapshot.IsCancellationRequested &&
        snapshot.Progress is
        {
            Stage: ModelInspectionStage.CheckModelPackage,
            StageStatus: ModelInspectionStageStatus.Active
        } &&
        snapshot.TerminalResult is null &&
        snapshot.ModelInspectionRunId != Guid.Empty;

    private static bool IsFreshStageOneCompletion(
        ModelInspectionViewSnapshot snapshot) =>
        snapshot.IsRunActive &&
        !snapshot.IsCancellationRequested &&
        snapshot.Progress is
        {
            Stage: ModelInspectionStage.CheckModelPackage,
            StageStatus: ModelInspectionStageStatus.Completed or
                ModelInspectionStageStatus.Warning
        } &&
        snapshot.TerminalResult is null &&
        snapshot.ModelInspectionRunId != Guid.Empty;

    private static bool IsFinalCompletion(
        ModelInspectionViewSnapshot? snapshot) =>
        snapshot?.Progress is
        {
            Stage: ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
            StageStatus: ModelInspectionStageStatus.Completed or
                ModelInspectionStageStatus.Warning
        };

    private static ModelInspectionViewSnapshot CreateRetryBinding(
        ModelInspectionViewSnapshot snapshot) => new(
        snapshot.RenderKey,
        isRunActive: true,
        isCancellationRequested: false,
        progress: new ModelInspectionProgress(
            snapshot.Progress!.Stage,
            ModelInspectionStageStatus.Active,
            completedStageCount: 0,
            totalStageCount: snapshot.Progress.TotalStageCount,
            stageFraction: 0d,
            userMessage: snapshot.Progress.UserMessage),
        terminalResult: null,
        modelInspectionRunId: snapshot.ModelInspectionRunId);

    private static bool IsFractionOnlyChange(
        ModelInspectionViewSnapshot previous,
        ModelInspectionViewSnapshot current)
    {
        ModelInspectionProgress? left = previous.Progress;
        ModelInspectionProgress? right = current.Progress;
        return left is not null &&
            right is not null &&
            previous.RenderKey.AttemptGeneration ==
                current.RenderKey.AttemptGeneration &&
            previous.IsRunActive == current.IsRunActive &&
            previous.IsCancellationRequested == current.IsCancellationRequested &&
            left.Stage == right.Stage &&
            left.StageStatus == ModelInspectionStageStatus.Active &&
            right.StageStatus == ModelInspectionStageStatus.Active &&
            left.CompletedStageCount == right.CompletedStageCount &&
            left.TotalStageCount == right.TotalStageCount &&
            (left.StageFraction != right.StageFraction ||
             !string.Equals(left.UserMessage, right.UserMessage, StringComparison.Ordinal));
    }

    private static int StageIndex(ModelInspectionStage stage) =>
        (int)stage - 1;

    private sealed class StageSlot
    {
        internal ModelInspectionViewSnapshot? Active { get; set; }

        internal ModelInspectionViewSnapshot? Completion { get; set; }

        internal void Clear()
        {
            Active = null;
            Completion = null;
        }
    }

    private enum PlaybackPhase
    {
        None,
        ActiveAwaitingPresentation,
        ActiveDwelling,
        ActiveReady,
        CompletionAwaitingPresentation
    }
}
