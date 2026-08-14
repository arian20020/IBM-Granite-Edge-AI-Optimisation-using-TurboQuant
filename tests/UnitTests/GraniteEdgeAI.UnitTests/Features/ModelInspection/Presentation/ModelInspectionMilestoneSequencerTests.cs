using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;

[TestClass]
public sealed class ModelInspectionMilestoneSequencerTests
{
    [TestMethod]
    public void FastStage_WaitsForExactPresentedDwellAndCompletionAcknowledgement()
    {
        var scheduler = new ManualMilestoneScheduler();
        List<ModelInspectionViewSnapshot> applied = [];
        using var sequencer = new ModelInspectionMilestoneSequencer(
            scheduler,
            applied.Add,
            animationsEnabled: true);
        ModelInspectionViewSnapshot stage1Active = Active(1, 1, 1);
        ModelInspectionViewSnapshot stage1Completed = Completed(1, 2, 1);
        ModelInspectionViewSnapshot stage2Active = Active(1, 3, 2);

        sequencer.Accept(stage1Active);
        sequencer.Accept(stage1Completed);
        sequencer.Accept(stage2Active);

        CollectionAssert.AreEqual(new[] { stage1Active }, applied);
        sequencer.NotifyPresented(stage1Completed.RenderKey);
        Assert.AreEqual(0, scheduler.PendingCount);
        sequencer.NotifyPresented(stage1Active.RenderKey);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(549));
        CollectionAssert.AreEqual(new[] { stage1Active }, applied);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1));
        CollectionAssert.AreEqual(
            new[] { stage1Active, stage1Completed },
            applied);
        sequencer.NotifyPresented(stage1Completed.RenderKey);
        CollectionAssert.AreEqual(
            new[] { stage1Active, stage1Completed, stage2Active },
            applied);
    }

    [TestMethod]
    public void NaturallyLongActive_ReleasesCompletionWithoutAddedDwell()
    {
        var scheduler = new ManualMilestoneScheduler();
        List<ModelInspectionViewSnapshot> applied = [];
        using var sequencer = new ModelInspectionMilestoneSequencer(
            scheduler,
            applied.Add,
            animationsEnabled: true);
        ModelInspectionViewSnapshot active = Active(1, 1, 1);
        ModelInspectionViewSnapshot completed = Completed(1, 2, 1);

        sequencer.Accept(active);
        sequencer.NotifyPresented(active.RenderKey);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(700));
        sequencer.Accept(completed);

        CollectionAssert.AreEqual(new[] { active, completed }, applied);
        Assert.AreEqual(0, scheduler.PendingCount);
    }

    [TestMethod]
    public void FiveStageBurst_PreservesEveryGenuineMilestoneInOrder()
    {
        var scheduler = new ManualMilestoneScheduler();
        List<ModelInspectionViewSnapshot> applied = [];
        using var sequencer = new ModelInspectionMilestoneSequencer(
            scheduler,
            applied.Add,
            animationsEnabled: true);
        var expected = new List<ModelInspectionViewSnapshot>();
        long revision = 0;
        for (int stage = 1; stage <= 5; stage++)
        {
            ModelInspectionViewSnapshot active = Active(
                1,
                ++revision,
                stage);
            ModelInspectionViewSnapshot completed = Completed(
                1,
                ++revision,
                stage);
            expected.Add(active);
            expected.Add(completed);
            sequencer.Accept(active);
            sequencer.Accept(completed);
        }

        CollectionAssert.AreEqual(new[] { expected[0] }, applied);
        for (int stage = 0; stage < 5; stage++)
        {
            ModelInspectionViewSnapshot active = expected[stage * 2];
            ModelInspectionViewSnapshot completed = expected[(stage * 2) + 1];
            sequencer.NotifyPresented(active.RenderKey);
            scheduler.AdvanceBy(ModelInspectionMilestoneSequencer.MinimumVisibleStage);
            Assert.AreSame(completed, applied[^1]);
            sequencer.NotifyPresented(completed.RenderKey);
        }

        CollectionAssert.AreEqual(expected, applied);
        Assert.IsFalse(sequencer.HasPendingPlayback);

        var completionScheduler = new ManualMilestoneScheduler();
        List<ModelInspectionViewSnapshot> completionApplied = [];
        using var completionSequencer = new ModelInspectionMilestoneSequencer(
            completionScheduler,
            completionApplied.Add,
            animationsEnabled: true);
        ModelInspectionViewSnapshot warning = Progress(
            2,
            1,
            stage: 3,
            ModelInspectionStageStatus.Warning,
            fraction: null);
        ModelInspectionViewSnapshot stage4Completed = Completed(2, 2, 4);
        ModelInspectionViewSnapshot stage5Completed = Completed(2, 3, 5);
        ModelInspectionViewSnapshot terminal = NormalTerminal(2, 4);

        completionSequencer.Accept(warning);
        completionSequencer.Accept(stage4Completed);
        completionSequencer.Accept(stage5Completed);

        CollectionAssert.AreEqual(new[] { warning }, completionApplied);
        completionSequencer.NotifyPresented(warning.RenderKey);
        CollectionAssert.AreEqual(
            new[] { warning, stage4Completed },
            completionApplied);
        completionSequencer.Accept(terminal);
        completionSequencer.NotifyPresented(stage4Completed.RenderKey);
        completionSequencer.NotifyPresented(stage5Completed.RenderKey);
        CollectionAssert.AreEqual(
            new[] { warning, stage4Completed, stage5Completed, terminal },
            completionApplied);
        Assert.IsFalse(completionSequencer.HasPendingPlayback);
    }

    [TestMethod]
    public void FractionOnlyActiveUpdates_CoalesceAndNeverResetFirstPresentedTime()
    {
        var scheduler = new ManualMilestoneScheduler();
        List<ModelInspectionViewSnapshot> applied = [];
        using var sequencer = new ModelInspectionMilestoneSequencer(
            scheduler,
            applied.Add,
            animationsEnabled: true);
        ModelInspectionViewSnapshot stage1Active = Active(1, 1, 1);
        ModelInspectionViewSnapshot stage1Completed = Completed(1, 2, 1);
        ModelInspectionViewSnapshot discardedStage2 = Active(1, 3, 2, 0.1);
        ModelInspectionViewSnapshot replacedStage2 = Active(1, 4, 2, 0.4);
        ModelInspectionViewSnapshot latestStage2 = Active(1, 5, 2, 0.9);
        ModelInspectionViewSnapshot presentedFraction = Active(1, 6, 2, 0.95);
        ModelInspectionViewSnapshot stage2Completed = Completed(1, 7, 2);

        sequencer.Accept(stage1Active);
        sequencer.Accept(stage1Completed);
        sequencer.Accept(discardedStage2);
        sequencer.Accept(replacedStage2);
        sequencer.Accept(latestStage2);
        sequencer.NotifyPresented(stage1Active.RenderKey);
        scheduler.AdvanceBy(ModelInspectionMilestoneSequencer.MinimumVisibleStage);
        sequencer.NotifyPresented(stage1Completed.RenderKey);

        Assert.AreSame(latestStage2, applied[^1]);
        CollectionAssert.DoesNotContain(applied, discardedStage2);
        CollectionAssert.DoesNotContain(applied, replacedStage2);
        sequencer.NotifyPresented(latestStage2.RenderKey);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(300));
        sequencer.Accept(presentedFraction);
        sequencer.NotifyPresented(presentedFraction.RenderKey);
        sequencer.Accept(stage2Completed);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(249));
        Assert.AreSame(presentedFraction, applied[^1]);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1));
        Assert.AreSame(stage2Completed, applied[^1]);
    }

    [TestMethod]
    public void NormalCompletedTerminal_WaitsBehindAcknowledgedStageFiveCompletion()
    {
        var scheduler = new ManualMilestoneScheduler();
        List<ModelInspectionViewSnapshot> applied = [];
        using var sequencer = new ModelInspectionMilestoneSequencer(
            scheduler,
            applied.Add,
            animationsEnabled: true);
        ModelInspectionViewSnapshot active = Active(1, 1, 5);
        ModelInspectionViewSnapshot completed = Completed(1, 2, 5);
        ModelInspectionViewSnapshot terminal = NormalTerminal(1, 3);

        sequencer.Accept(active);
        sequencer.Accept(completed);
        sequencer.Accept(terminal);
        sequencer.NotifyPresented(active.RenderKey);
        scheduler.AdvanceBy(ModelInspectionMilestoneSequencer.MinimumVisibleStage);

        CollectionAssert.AreEqual(new[] { active, completed }, applied);
        Assert.IsTrue(sequencer.HasPendingPlayback);
        sequencer.NotifyPresented(completed.RenderKey);
        CollectionAssert.AreEqual(new[] { active, completed, terminal }, applied);
        Assert.IsFalse(sequencer.HasPendingPlayback);
    }

    [TestMethod]
    public void SafetySnapshots_CancelQueuedPlaybackAndPublishImmediately()
    {
        AssertImmediateSafetyFlush(CancellationRequested(1, 2));
        AssertImmediateSafetyFlush(Failed(1, 2));
        AssertImmediateSafetyFlush(CancelledProgress(1, 2));
        AssertImmediateSafetyFlush(CancelledTerminal(1, 2));
        AssertImmediateSafetyFlush(OperationalFailure(1, 2));
    }

    [TestMethod]
    public void RetryNavigationAndDisposal_InvalidatePriorPlaybackSilently()
    {
        var scheduler = new ManualMilestoneScheduler();
        List<ModelInspectionViewSnapshot> applied = [];
        var sequencer = new ModelInspectionMilestoneSequencer(
            scheduler,
            applied.Add,
            animationsEnabled: true);
        ModelInspectionViewSnapshot oldActive = Active(1, 1, 1);
        ModelInspectionViewSnapshot oldCompleted = Completed(1, 2, 1);
        ModelInspectionViewSnapshot retryStartup = Startup(2, 0);
        ModelInspectionViewSnapshot retryActive = Active(2, 1, 1);

        sequencer.Accept(oldActive);
        sequencer.NotifyPresented(oldActive.RenderKey);
        sequencer.Accept(oldCompleted);
        sequencer.Accept(retryStartup);

        CollectionAssert.AreEqual(new[] { oldActive, retryStartup }, applied);
        Assert.AreEqual(1, scheduler.CancelledCount);
        scheduler.InvokeLastCancelled();
        CollectionAssert.AreEqual(new[] { oldActive, retryStartup }, applied);

        sequencer.Accept(retryActive);
        sequencer.NotifyPresented(retryActive.RenderKey);
        sequencer.Invalidate();
        scheduler.InvokeLastCancelled();
        sequencer.Accept(Completed(2, 2, 1));
        sequencer.Dispose();
        sequencer.Dispose();

        CollectionAssert.AreEqual(
            new[] { oldActive, retryStartup, retryActive },
            applied);
        Assert.IsFalse(sequencer.HasPendingPlayback);
        Assert.IsTrue(scheduler.IsDisposed);
    }

    [TestMethod]
    public void ReducedMotion_FlushesLatestAndReenablePacesOnlyFutureActivation()
    {
        var scheduler = new ManualMilestoneScheduler();
        List<ModelInspectionViewSnapshot> applied = [];
        using var sequencer = new ModelInspectionMilestoneSequencer(
            scheduler,
            applied.Add,
            animationsEnabled: true);
        ModelInspectionViewSnapshot stage1Active = Active(1, 1, 1);
        ModelInspectionViewSnapshot stage1Completed = Completed(1, 2, 1);
        ModelInspectionViewSnapshot stage2Active = Active(1, 3, 2);
        ModelInspectionViewSnapshot stage2Completed = Completed(1, 4, 2);
        ModelInspectionViewSnapshot stage3Active = Active(1, 5, 3);
        ModelInspectionViewSnapshot stage3Completed = Completed(1, 6, 3);

        sequencer.Accept(stage1Active);
        sequencer.NotifyPresented(stage1Active.RenderKey);
        sequencer.Accept(stage1Completed);
        sequencer.Accept(stage2Active);
        sequencer.SetAnimationsEnabled(false);

        CollectionAssert.AreEqual(new[] { stage1Active, stage2Active }, applied);
        Assert.AreEqual(1, scheduler.CancelledCount);
        Assert.IsFalse(sequencer.HasPendingPlayback);
        sequencer.SetAnimationsEnabled(true);
        sequencer.Accept(stage2Completed);
        sequencer.Accept(stage3Active);
        sequencer.Accept(stage3Completed);

        CollectionAssert.AreEqual(
            new[] { stage1Active, stage2Active, stage2Completed, stage3Active },
            applied);
        sequencer.NotifyPresented(stage3Active.RenderKey);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(549));
        Assert.AreSame(stage3Active, applied[^1]);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1));
        Assert.AreSame(stage3Completed, applied[^1]);
    }

    [TestMethod]
    public void StalePresentationAndTimerCallbacks_CannotAdvanceCurrentEpoch()
    {
        var scheduler = new ManualMilestoneScheduler();
        List<ModelInspectionViewSnapshot> applied = [];
        using var sequencer = new ModelInspectionMilestoneSequencer(
            scheduler,
            applied.Add,
            animationsEnabled: true);
        ModelInspectionViewSnapshot original = Active(1, 1, 1, 0.1);
        ModelInspectionViewSnapshot replacement = Active(1, 2, 1, 0.5);
        ModelInspectionViewSnapshot completed = Completed(1, 3, 1);
        ModelInspectionViewSnapshot newerAttempt = Startup(2, 0);

        sequencer.Accept(original);
        sequencer.Accept(replacement);
        sequencer.NotifyPresented(original.RenderKey);
        Assert.AreEqual(0, scheduler.PendingCount);
        sequencer.NotifyPresented(replacement.RenderKey);
        sequencer.Accept(completed);
        sequencer.Accept(newerAttempt);
        scheduler.InvokeLastCancelled();
        sequencer.NotifyPresented(completed.RenderKey);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1));

        CollectionAssert.AreEqual(
            new[] { original, replacement, newerAttempt },
            applied);
    }

    [TestMethod]
    public void BurstReplacement_RetainsFiveTypedStageSlotsAndOneNormalTerminal()
    {
        var scheduler = new ManualMilestoneScheduler();
        List<ModelInspectionViewSnapshot> applied = [];
        using var sequencer = new ModelInspectionMilestoneSequencer(
            scheduler,
            applied.Add,
            animationsEnabled: true);
        var expected = new List<ModelInspectionViewSnapshot>();
        long revision = 0;
        ModelInspectionViewSnapshot stage1Active = Active(1, ++revision, 1);
        expected.Add(stage1Active);
        sequencer.Accept(stage1Active);
        for (int stage = 1; stage <= 5; stage++)
        {
            if (stage > 1)
            {
                ModelInspectionViewSnapshot latest = null!;
                for (int update = 0; update < 100; update++)
                {
                    latest = Active(
                        1,
                        ++revision,
                        stage,
                        update / 100d);
                    sequencer.Accept(latest);
                }

                expected.Add(latest);
            }

            ModelInspectionViewSnapshot completed = Completed(
                1,
                ++revision,
                stage);
            expected.Add(completed);
            sequencer.Accept(completed);
        }

        ModelInspectionViewSnapshot latestTerminal = null!;
        for (int update = 0; update < 100; update++)
        {
            latestTerminal = NormalTerminal(1, ++revision);
            sequencer.Accept(latestTerminal);
        }

        for (int stage = 0; stage < 5; stage++)
        {
            ModelInspectionViewSnapshot active = expected[stage * 2];
            ModelInspectionViewSnapshot completed = expected[(stage * 2) + 1];
            Assert.AreSame(active, applied[^1]);
            sequencer.NotifyPresented(active.RenderKey);
            scheduler.AdvanceBy(ModelInspectionMilestoneSequencer.MinimumVisibleStage);
            Assert.AreSame(completed, applied[^1]);
            sequencer.NotifyPresented(completed.RenderKey);
        }

        expected.Add(latestTerminal);
        CollectionAssert.AreEqual(expected, applied);
        Assert.IsFalse(sequencer.HasPendingPlayback);
    }

    private static void AssertImmediateSafetyFlush(
        ModelInspectionViewSnapshot safety)
    {
        var scheduler = new ManualMilestoneScheduler();
        List<ModelInspectionViewSnapshot> applied = [];
        using var sequencer = new ModelInspectionMilestoneSequencer(
            scheduler,
            applied.Add,
            animationsEnabled: true);
        ModelInspectionViewSnapshot active = Active(1, 1, 1);

        sequencer.Accept(active);
        sequencer.NotifyPresented(active.RenderKey);
        sequencer.Accept(safety);

        CollectionAssert.AreEqual(new[] { active, safety }, applied);
        Assert.AreEqual(1, scheduler.CancelledCount);
        Assert.IsFalse(sequencer.HasPendingPlayback);
        scheduler.InvokeLastCancelled();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
        CollectionAssert.AreEqual(new[] { active, safety }, applied);
    }

    private static ModelInspectionViewSnapshot Startup(
        long generation,
        long revision) => new(
        new ModelInspectionRenderKey(generation, revision),
        isRunActive: true,
        isCancellationRequested: false,
        progress: null,
        terminalResult: null);

    private static ModelInspectionViewSnapshot Active(
        long generation,
        long revision,
        int stage,
        double? fraction = null) => Progress(
        generation,
        revision,
        stage,
        ModelInspectionStageStatus.Active,
        fraction);

    private static ModelInspectionViewSnapshot Completed(
        long generation,
        long revision,
        int stage) => Progress(
        generation,
        revision,
        stage,
        ModelInspectionStageStatus.Completed,
        fraction: null);

    private static ModelInspectionViewSnapshot Failed(
        long generation,
        long revision) => Progress(
        generation,
        revision,
        stage: 1,
        ModelInspectionStageStatus.Failed,
        fraction: null);

    private static ModelInspectionViewSnapshot CancelledProgress(
        long generation,
        long revision) => Progress(
        generation,
        revision,
        stage: 1,
        ModelInspectionStageStatus.Cancelled,
        fraction: null);

    private static ModelInspectionViewSnapshot Progress(
        long generation,
        long revision,
        int stage,
        ModelInspectionStageStatus status,
        double? fraction)
    {
        var typedStage = (ModelInspectionStage)stage;
        int completed = status is ModelInspectionStageStatus.Completed or
            ModelInspectionStageStatus.Warning
                ? stage
                : stage - 1;
        return new ModelInspectionViewSnapshot(
            new ModelInspectionRenderKey(generation, revision),
            isRunActive: true,
            isCancellationRequested: false,
            new ModelInspectionProgress(
                typedStage,
                status,
                completed,
                totalStageCount: 5,
                fraction,
                $"Stage {stage} {status}."),
            terminalResult: null);
    }

    private static ModelInspectionViewSnapshot CancellationRequested(
        long generation,
        long revision)
    {
        ModelInspectionProgress progress = Active(
            generation,
            revision,
            stage: 1).Progress!;
        return new ModelInspectionViewSnapshot(
            new ModelInspectionRenderKey(generation, revision),
            isRunActive: true,
            isCancellationRequested: true,
            progress,
            terminalResult: null);
    }

    private static ModelInspectionViewSnapshot NormalTerminal(
        long generation,
        long revision) => new(
        new ModelInspectionRenderKey(generation, revision),
        isRunActive: false,
        isCancellationRequested: false,
        progress: null,
        ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(ModelInspectionOutcome.Ready)));

    private static ModelInspectionViewSnapshot CancelledTerminal(
        long generation,
        long revision) => new(
        new ModelInspectionRenderKey(generation, revision),
        isRunActive: false,
        isCancellationRequested: false,
        progress: null,
        ModelInspectionExecutionResult.Cancelled(cooperative: true));

    private static ModelInspectionViewSnapshot OperationalFailure(
        long generation,
        long revision) => new(
        new ModelInspectionRenderKey(generation, revision),
        isRunActive: false,
        isCancellationRequested: false,
        progress: null,
        ModelInspectionExecutionResult.OperationalFailure(
            PresentationTestData.CreateFailure()));

    private sealed class ManualMilestoneScheduler :
        IModelInspectionMilestoneScheduler
    {
        private readonly List<ScheduledCallback> callbacks = [];
        private long nextSequence;
        private Action? lastCancelled;
        private bool disposed;

        public TimeSpan Elapsed { get; private set; }

        internal int PendingCount => callbacks.Count(value =>
            !value.Cancelled && !value.Executed);

        internal int CancelledCount { get; private set; }

        internal bool IsDisposed => disposed;

        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            if (delay <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            ObjectDisposedException.ThrowIf(disposed, this);
            var scheduled = new ScheduledCallback(
                Elapsed + delay,
                nextSequence++,
                callback);
            callbacks.Add(scheduled);
            return new Cancellation(this, scheduled);
        }

        internal void AdvanceBy(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            TimeSpan target = Elapsed + duration;
            while (callbacks
                .Where(value =>
                    !value.Cancelled &&
                    !value.Executed &&
                    value.Due <= target)
                .OrderBy(value => value.Due)
                .ThenBy(value => value.Sequence)
                .FirstOrDefault() is ScheduledCallback next)
            {
                Elapsed = next.Due;
                next.Executed = true;
                next.Callback();
            }

            Elapsed = target;
        }

        internal void InvokeLastCancelled()
        {
            lastCancelled?.Invoke();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (ScheduledCallback callback in callbacks)
            {
                Cancel(callback);
            }
        }

        private void Cancel(ScheduledCallback callback)
        {
            if (callback.Cancelled || callback.Executed)
            {
                return;
            }

            callback.Cancelled = true;
            CancelledCount++;
            lastCancelled = callback.Callback;
        }

        private sealed class Cancellation : IDisposable
        {
            private ManualMilestoneScheduler? owner;
            private ScheduledCallback? callback;

            internal Cancellation(
                ManualMilestoneScheduler owner,
                ScheduledCallback callback)
            {
                this.owner = owner;
                this.callback = callback;
            }

            public void Dispose()
            {
                ManualMilestoneScheduler? capturedOwner = owner;
                ScheduledCallback? capturedCallback = callback;
                owner = null;
                callback = null;
                if (capturedOwner is not null && capturedCallback is not null)
                {
                    capturedOwner.Cancel(capturedCallback);
                }
            }
        }

        private sealed class ScheduledCallback
        {
            internal ScheduledCallback(
                TimeSpan due,
                long sequence,
                Action callback)
            {
                Due = due;
                Sequence = sequence;
                Callback = callback;
            }

            internal TimeSpan Due { get; }

            internal long Sequence { get; }

            internal Action Callback { get; }

            internal bool Cancelled { get; set; }

            internal bool Executed { get; set; }
        }
    }
}
