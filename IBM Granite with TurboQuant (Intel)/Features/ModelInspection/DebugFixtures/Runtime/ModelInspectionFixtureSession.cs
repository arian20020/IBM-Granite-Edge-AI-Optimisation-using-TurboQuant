#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;

internal sealed class ModelInspectionFixtureSession : IDisposable
{
    private readonly object gate = new();
    private readonly Func<IModelInspectionAnimationDriver>
        animationDriverFactory;
    private readonly bool animationsEnabled;
    private readonly ModelInspectionFixtureDeferredEventRegistry deferredEvents;
    private readonly ModelInspectionFixtureOperationDomain lifetimeDomain;
    private readonly ModelInspectionFixtureOperationDrain deliveries;
    private readonly HashSet<PageDispatcherAudit> pageDispatcherAudits = [];

    private AuditedAnimationDriver? animationDriver;
    private AuditedRenderDispatcher? renderDispatcher;
    private FixedMotionSettings? motionSettings;
    private bool animationDriverClaimed;
    private bool animationDriverCreationInProgress;
    private bool motionSettingsClaimed;
    private bool renderDispatcherClaimed;
    private bool retired;
    private bool disposed;
    private bool disposalStarted;
    private bool disposalCompleted;
    private ExceptionDispatchInfo? disposalError;

    internal ModelInspectionFixtureSession(
        ValidatedModelInspectionFixtureInput input,
        bool animationsEnabled = true,
        Func<IModelInspectionAnimationDriver>? animationDriverFactory = null,
        Action<ModelInspectionFixtureDeferredEventKind>?
            beforeDeferredDelivery = null)
    {
        lifetimeDomain = new ModelInspectionFixtureOperationDomain();
        deliveries = new ModelInspectionFixtureOperationDrain(
            lifetimeDomain,
            waitForLifetimeDomainQuiescence: true);
        Plan = ModelInspectionFixtureAdapter.CreatePlan(
            input ?? throw new ArgumentNullException(nameof(input)));
        Evidence = new ModelInspectionFixtureSessionEvidence();
        deferredEvents = new ModelInspectionFixtureDeferredEventRegistry(
            Plan.DeferredEvents,
            Evidence,
            beforeDeferredDelivery);
        Service = new DebugModelInspectionService(
            Plan,
            Evidence,
            deferredEvents,
            lifetimeDomain: lifetimeDomain);
        this.animationsEnabled = animationsEnabled;
        this.animationDriverFactory = animationDriverFactory ??
            (() => new WinUiModelInspectionAnimationDriver(
                ModelInspectionMotionSpec.Approved));
    }

    internal ModelInspectionFixtureExecutionPlan Plan { get; }

    internal ModelInspectionRequest Request => Plan.Request;

    internal DebugModelInspectionService Service { get; }

    internal ModelInspectionFixtureSessionEvidence Evidence { get; }

    internal IReadOnlyList<ModelInspectionFixtureDeferredEvent>
        CapturedDeferredEvents => Evidence.CapturedDeferredEvents;

    internal IReadOnlyList<ModelInspectionFixtureStaleProgressHandle>
        StaleProgressHandles => deferredEvents.StaleProgressHandles;

    internal IReadOnlyList<ModelInspectionFixtureStaleResultSnapshotHandle>
        StaleResultSnapshotHandles =>
            deferredEvents.StaleResultSnapshotHandles;

    internal IReadOnlyList<ModelInspectionFixtureStaleMotionCallbackHandle>
        StaleMotionCallbackHandles =>
            deferredEvents.StaleMotionCallbackHandles;

    internal bool AnimationsEnabled => animationsEnabled;

    internal IReadOnlyList<ModelInspectionFixtureStaleAnnouncementCallbackHandle>
        StaleAnnouncementCallbackHandles =>
            deferredEvents.StaleAnnouncementCallbackHandles;

    internal bool ReleaseStaleProgress(
        int ownerAttempt,
        string releaseCheckpoint)
    {
        ModelInspectionFixtureOperationDrain.Lease? lease =
            TryEnterDeferredDelivery();
        if (lease is null)
        {
            return false;
        }

        using (lease)
        {
            return deferredEvents.ReleaseStaleProgress(
                ownerAttempt,
                releaseCheckpoint,
                Service.StartedAttemptCount);
        }
    }

    internal void CaptureStaleResultSnapshot(
        int ownerAttempt,
        string captureCheckpoint,
        Action callback)
    {
        using ModelInspectionFixtureOperationDrain.Lease lease =
            EnterDeferredOperation();
        deferredEvents.CaptureStaleResultSnapshot(
            ownerAttempt,
            captureCheckpoint,
            callback);
    }

    internal bool ReleaseStaleResultSnapshot(
        int ownerAttempt,
        string releaseCheckpoint)
    {
        ModelInspectionFixtureOperationDrain.Lease? lease =
            TryEnterDeferredDelivery();
        if (lease is null)
        {
            return false;
        }

        using (lease)
        {
            return deferredEvents.ReleaseStaleResultSnapshot(
                ownerAttempt,
                releaseCheckpoint,
                Service.StartedAttemptCount);
        }
    }

    internal void CaptureStaleMotionCallback(
        int ownerAttempt,
        string captureCheckpoint,
        Action callback)
    {
        using ModelInspectionFixtureOperationDrain.Lease lease =
            EnterDeferredOperation();
        deferredEvents.CaptureStaleMotionCallback(
            ownerAttempt,
            captureCheckpoint,
            callback);
    }

    internal bool ReleaseStaleMotionCallback(
        int ownerAttempt,
        string releaseCheckpoint)
    {
        ModelInspectionFixtureOperationDrain.Lease? lease =
            TryEnterDeferredDelivery();
        if (lease is null)
        {
            return false;
        }

        using (lease)
        {
            return deferredEvents.ReleaseStaleMotionCallback(
                ownerAttempt,
                releaseCheckpoint,
                Service.StartedAttemptCount);
        }
    }

    internal void CaptureStaleAnnouncementCallback(
        int ownerAttempt,
        string captureCheckpoint,
        Action callback)
    {
        using ModelInspectionFixtureOperationDrain.Lease lease =
            EnterDeferredOperation();
        deferredEvents.CaptureStaleAnnouncementCallback(
            ownerAttempt,
            captureCheckpoint,
            callback);
    }

    internal bool ReleaseStaleAnnouncementCallback(
        int ownerAttempt,
        string releaseCheckpoint)
    {
        ModelInspectionFixtureOperationDrain.Lease? lease =
            TryEnterDeferredDelivery();
        if (lease is null)
        {
            return false;
        }

        using (lease)
        {
            return deferredEvents.ReleaseStaleAnnouncementCallback(
                ownerAttempt,
                releaseCheckpoint,
                Service.StartedAttemptCount);
        }
    }

    internal IModelInspectionRenderDispatcher CreateRenderDispatcher()
    {
        lock (gate)
        {
            ThrowIfUnavailable();
            if (renderDispatcherClaimed)
            {
                throw new InvalidOperationException(
                    "The fixture render dispatcher has already been transferred.");
            }

            DispatcherQueue queue = DispatcherQueue.GetForCurrentThread() ??
                throw new InvalidOperationException(
                    "The fixture render dispatcher requires a UI thread.");
            renderDispatcher = new AuditedRenderDispatcher(
                new DispatcherQueueModelInspectionRenderDispatcher(queue),
                Evidence);
            renderDispatcherClaimed = true;
            return renderDispatcher;
        }
    }

    internal IDisposable? BeginPageDispatcherCallback()
    {
        lock (gate)
        {
            if (retired || disposed)
            {
                return null;
            }

            var registration = new PageDispatcherAudit(
                this,
                Evidence.BeginDispatcherCallback());
            pageDispatcherAudits.Add(registration);
            return registration;
        }
    }

    internal void ClosePageDispatcherCallbacks()
    {
        PageDispatcherAudit[] abandoned;
        lock (gate)
        {
            abandoned = [.. pageDispatcherAudits];
            pageDispatcherAudits.Clear();
        }

        foreach (PageDispatcherAudit registration in abandoned)
        {
            registration.CompleteEvidence();
        }
    }

    internal IModelInspectionAnimationDriver CreateAnimationDriver()
    {
        ModelInspectionFixtureOperationDrain.Lease lease;
        lock (gate)
        {
            ThrowIfUnavailable();
            if (animationDriverClaimed || animationDriverCreationInProgress)
            {
                throw new InvalidOperationException(
                    "The fixture animation driver is already being created or has been transferred.");
            }

            animationDriverCreationInProgress = true;
            try
            {
                lease = deliveries.Enter();
            }
            catch
            {
                animationDriverCreationInProgress = false;
                throw;
            }
        }

        try
        {
            IModelInspectionAnimationDriver inner =
                animationDriverFactory() ?? throw new InvalidOperationException(
                    "The fixture animation-driver factory returned null.");
            AuditedAnimationDriver candidate = new(inner, Evidence);
            bool publish;
            lock (gate)
            {
                animationDriverCreationInProgress = false;
                publish = !retired && !disposed;
                if (publish)
                {
                    animationDriver = candidate;
                    animationDriverClaimed = true;
                }
            }

            if (!publish)
            {
                candidate.Dispose();
                throw new ObjectDisposedException(
                    nameof(ModelInspectionFixtureSession));
            }

            return candidate;
        }
        catch
        {
            lock (gate)
            {
                animationDriverCreationInProgress = false;
            }

            throw;
        }
        finally
        {
            lease.Dispose();
        }
    }

    internal IModelInspectionMotionSettings CreateMotionSettings()
    {
        lock (gate)
        {
            ThrowIfUnavailable();
            if (motionSettingsClaimed)
            {
                throw new InvalidOperationException(
                    "The fixture motion settings have already been transferred.");
            }

            motionSettings = new FixedMotionSettings(
                animationsEnabled,
                Evidence);
            motionSettingsClaimed = true;
            return motionSettings;
        }
    }

    internal bool Retire()
    {
        bool first;
        lock (gate)
        {
            first = !retired;
            retired = true;
        }

        ExceptionDispatchInfo? error = null;
        AttemptCleanup(() => Service.Retire(), ref error);
        AttemptCleanup(
            () => deliveries.Close(FinalizeRetirement),
            ref error);
        error?.Throw();
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

    private ModelInspectionFixtureOperationDrain.Lease EnterDeferredOperation()
    {
        lock (gate)
        {
            ThrowIfUnavailable();
            return deliveries.Enter();
        }
    }

    private ModelInspectionFixtureOperationDrain.Lease?
        TryEnterDeferredDelivery()
    {
        lock (gate)
        {
            return retired || disposed ? null : deliveries.Enter();
        }
    }

    private void FinalizeRetirement()
    {
        AuditedAnimationDriver? driver;
        AuditedRenderDispatcher? dispatcher;
        FixedMotionSettings? settings;
        bool disposeRequested;
        lock (gate)
        {
            driver = animationDriver;
            dispatcher = renderDispatcher;
            settings = motionSettings;
            disposeRequested = disposed;
        }

        ExceptionDispatchInfo? error = null;
        AttemptCleanup(() => Service.Retire(), ref error);
        AttemptCleanup(deferredEvents.Retire, ref error);
        AttemptCleanup(ClosePageDispatcherCallbacks, ref error);
        if (dispatcher is not null)
        {
            AttemptCleanup(dispatcher.Retire, ref error);
        }
        if (driver is not null)
        {
            AttemptCleanup(driver.Dispose, ref error);
        }

        if (settings is not null)
        {
            AttemptCleanup(settings.Dispose, ref error);
        }

        AttemptCleanup(Evidence.RecordSessionRetirement, ref error);
        if (disposeRequested)
        {
            AttemptCleanup(CompleteDisposal, ref error);
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
            AttemptCleanup(Service.Dispose, ref error);
            AttemptCleanup(Evidence.RecordSessionDisposal, ref error);
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

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(retired || disposed, this);
    }

    private void CompletePageDispatcherCallback(
        PageDispatcherAudit registration)
    {
        lock (gate)
        {
            pageDispatcherAudits.Remove(registration);
        }

        registration.CompleteEvidence();
    }

    private sealed class PageDispatcherAudit : IDisposable
    {
        private readonly ModelInspectionFixtureSession owner;
        private readonly IDisposable evidenceAudit;
        private int completed;

        internal PageDispatcherAudit(
            ModelInspectionFixtureSession owner,
            IDisposable evidenceAudit)
        {
            this.owner = owner;
            this.evidenceAudit = evidenceAudit;
        }

        public void Dispose() => owner.CompletePageDispatcherCallback(this);

        internal void CompleteEvidence()
        {
            if (Interlocked.Exchange(ref completed, 1) == 0)
            {
                evidenceAudit.Dispose();
            }
        }
    }

    private sealed class FixedMotionSettings : IModelInspectionMotionSettings
    {
        private readonly ModelInspectionFixtureSessionEvidence evidence;
        private EventHandler? animationsEnabledChanged;
        private bool disposed;

        internal FixedMotionSettings(
            bool animationsEnabled,
            ModelInspectionFixtureSessionEvidence evidence)
        {
            AnimationsEnabled = animationsEnabled;
            this.evidence = evidence;
        }

        public bool AnimationsEnabled { get; }

        public event EventHandler? AnimationsEnabledChanged
        {
            add
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                animationsEnabledChanged += value;
            }
            remove
            {
                if (!disposed)
                {
                    animationsEnabledChanged -= value;
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            animationsEnabledChanged = null;
            evidence.RecordMotionSettingsDisposal();
        }
    }

    private sealed class AuditedAnimationDriver :
        IModelInspectionAnimationDriver
    {
        private readonly IModelInspectionAnimationDriver inner;
        private readonly ModelInspectionFixtureSessionEvidence evidence;
        private readonly object auditGate = new();
        private readonly HashSet<PendingMotionAudit> pendingAudits = [];
        private bool disposed;

        internal AuditedAnimationDriver(
            IModelInspectionAnimationDriver inner,
            ModelInspectionFixtureSessionEvidence evidence)
        {
            this.inner = inner;
            this.evidence = evidence;
        }

        public void StartStageStatus(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            ThrowIfDisposed();
            evidence.RecordAnimationStart();
            StartAudited(
                [new MotionSlot(
                    target,
                    ModelInspectionAnimatedProperty.Opacity)],
                callback => inner.StartStageStatus(target, key, callback),
                completed);
        }

        public void StartActiveDetail(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            ThrowIfDisposed();
            evidence.RecordAnimationStart();
            StartAudited(
                [
                    new MotionSlot(
                        target,
                        ModelInspectionAnimatedProperty.Opacity),
                    new MotionSlot(
                        target,
                        ModelInspectionAnimatedProperty.TranslationY)
                ],
                callback => inner.StartActiveDetail(target, key, callback),
                completed);
        }

        public void StartDisclosure(
            UIElement chevron,
            FrameworkElement viewport,
            IReadOnlyList<UIElement> followingElements,
            bool isExpanded,
            IReadOnlyList<double> previousTopOffsets,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            ThrowIfDisposed();
            evidence.RecordAnimationStart();
            var slots = new List<MotionSlot>
            {
                new(
                    chevron,
                    ModelInspectionAnimatedProperty.RotationInDegrees),
                new(viewport, ModelInspectionAnimatedProperty.Opacity),
                new(viewport, ModelInspectionAnimatedProperty.RevealProgress)
            };
            foreach (UIElement followingElement in followingElements)
            {
                slots.Add(new MotionSlot(
                    followingElement,
                    ModelInspectionAnimatedProperty.TranslationY));
            }

            StartAudited(slots, callback => inner.StartDisclosure(
                    chevron,
                    viewport,
                    followingElements,
                    isExpanded,
                    previousTopOffsets,
                    key,
                    callback),
                completed);
        }

        public void StartTerminal(
            UIElement outgoing,
            UIElement incoming,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            ThrowIfDisposed();
            evidence.RecordAnimationStart();
            StartAudited(
                [
                    new MotionSlot(
                        outgoing,
                        ModelInspectionAnimatedProperty.Opacity),
                    new MotionSlot(
                        incoming,
                        ModelInspectionAnimatedProperty.Opacity)
                ],
                callback => inner.StartTerminal(
                    outgoing, incoming, key, callback),
                completed);
        }

        public void CancelAll()
        {
            if (disposed)
            {
                return;
            }

            ExceptionDispatchInfo? error = null;
            try
            {
                AttemptCleanup(inner.CancelAll, ref error);
            }
            finally
            {
                AttemptCleanup(CompletePendingAudits, ref error);
                AttemptCleanup(
                    evidence.RecordAnimationCancellation,
                    ref error);
            }

            error?.Throw();
        }

        public void Dispose()
        {
            if (disposed)
            {
                CompletePendingAudits();
                return;
            }

            disposed = true;
            ExceptionDispatchInfo? error = null;
            try
            {
                AttemptCleanup(inner.CancelAll, ref error);
                AttemptCleanup(inner.Dispose, ref error);
            }
            finally
            {
                AttemptCleanup(CompletePendingAudits, ref error);
                AttemptCleanup(
                    evidence.RecordAnimationDriverDisposal,
                    ref error);
            }

            error?.Throw();
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private void StartAudited(
            IReadOnlyList<MotionSlot> slots,
            Action<Action<ModelInspectionVisualOperationKey>> start,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            var audit = new PendingMotionAudit(
                evidence.BeginMotionBatch(),
                slots);
            PendingMotionAudit[] superseded;
            lock (auditGate)
            {
                var matches = new List<PendingMotionAudit>();
                foreach (PendingMotionAudit pending in pendingAudits)
                {
                    if (pending.Overlaps(slots))
                    {
                        matches.Add(pending);
                    }
                }

                superseded = [.. matches];
                foreach (PendingMotionAudit pending in superseded)
                {
                    pendingAudits.Remove(pending);
                }

                pendingAudits.Add(audit);
            }

            foreach (PendingMotionAudit pending in superseded)
            {
                pending.Dispose();
            }

            int completionClaimed = 0;
            void Complete(ModelInspectionVisualOperationKey completedKey)
            {
                try
                {
                    completed(completedKey);
                }
                finally
                {
                    if (Interlocked.Exchange(ref completionClaimed, 1) == 0)
                    {
                        lock (auditGate)
                        {
                            pendingAudits.Remove(audit);
                        }

                        audit.Dispose();
                    }
                }
            }

            try
            {
                start(Complete);
            }
            catch
            {
                if (Interlocked.Exchange(ref completionClaimed, 1) == 0)
                {
                    lock (auditGate)
                    {
                        pendingAudits.Remove(audit);
                    }

                    audit.Dispose();
                }

                throw;
            }
        }

        private void CompletePendingAudits()
        {
            PendingMotionAudit[] pending;
            ExceptionDispatchInfo? error = null;
            lock (auditGate)
            {
                pending = [.. pendingAudits];
                pendingAudits.Clear();
            }

            foreach (PendingMotionAudit audit in pending)
            {
                AttemptCleanup(audit.Dispose, ref error);
            }

            error?.Throw();
        }

        private readonly record struct MotionSlot(
            UIElement Target,
            ModelInspectionAnimatedProperty Property);

        private sealed class PendingMotionAudit : IDisposable
        {
            private readonly IDisposable evidenceAudit;
            private readonly IReadOnlyList<MotionSlot> slots;
            private int completed;

            internal PendingMotionAudit(
                IDisposable evidenceAudit,
                IReadOnlyList<MotionSlot> slots)
            {
                this.evidenceAudit = evidenceAudit;
                this.slots = slots;
            }

            internal bool Overlaps(IReadOnlyList<MotionSlot> candidate)
            {
                foreach (MotionSlot current in slots)
                {
                    foreach (MotionSlot replacement in candidate)
                    {
                        if (ReferenceEquals(current.Target, replacement.Target) &&
                            current.Property == replacement.Property)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref completed, 1) == 0)
                {
                    evidenceAudit.Dispose();
                }
            }
        }
    }

    private sealed class AuditedRenderDispatcher :
        IModelInspectionRenderDispatcher
    {
        private readonly IModelInspectionRenderDispatcher inner;
        private readonly ModelInspectionFixtureSessionEvidence evidence;
        private readonly object gate = new();
        private readonly HashSet<IDisposable> pending = [];
        private bool retired;

        internal AuditedRenderDispatcher(
            IModelInspectionRenderDispatcher inner,
            ModelInspectionFixtureSessionEvidence evidence)
        {
            this.inner = inner;
            this.evidence = evidence;
        }

        public bool TryEnqueue(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            IDisposable audit;
            lock (gate)
            {
                if (retired)
                {
                    return false;
                }

                audit = evidence.BeginDispatcherCallback();
                pending.Add(audit);
            }

            bool enqueued;
            try
            {
                enqueued = inner.TryEnqueue(() =>
                {
                    try
                    {
                        callback();
                    }
                    finally
                    {
                        Complete(audit);
                    }
                });
            }
            catch
            {
                Complete(audit);
                throw;
            }

            if (!enqueued)
            {
                Complete(audit);
            }

            return enqueued;
        }

        internal void Retire()
        {
            IDisposable[] abandoned;
            lock (gate)
            {
                retired = true;
                abandoned = [.. pending];
                pending.Clear();
            }

            foreach (IDisposable audit in abandoned)
            {
                audit.Dispose();
            }
        }

        private void Complete(IDisposable audit)
        {
            lock (gate)
            {
                pending.Remove(audit);
            }

            audit.Dispose();
        }
    }
}

internal sealed class ModelInspectionFixtureOperationDomain
{
    private readonly object gate = new();
    private readonly Dictionary<int, int> operationsByThread = [];
    private readonly List<Action> quiescenceContinuations = [];
    private int activeOperationCount;

    internal bool CurrentThreadOwnsOperation
    {
        get
        {
            lock (gate)
            {
                return operationsByThread.ContainsKey(
                    Environment.CurrentManagedThreadId);
            }
        }
    }

    internal void Enter(int threadId)
    {
        lock (gate)
        {
            operationsByThread.TryGetValue(threadId, out int owned);
            int nextOwned = checked(owned + 1);
            int nextActive = checked(activeOperationCount + 1);
            operationsByThread[threadId] = nextOwned;
            activeOperationCount = nextActive;
        }
    }

    internal void Exit(int threadId)
    {
        Action[] ready = [];
        lock (gate)
        {
            if (!operationsByThread.TryGetValue(threadId, out int owned) ||
                owned <= 0 || activeOperationCount <= 0)
            {
                throw new InvalidOperationException(
                    "The fixture lifetime-domain operation audit is unbalanced.");
            }

            if (owned == 1)
            {
                operationsByThread.Remove(threadId);
            }
            else
            {
                operationsByThread[threadId] = owned - 1;
            }

            activeOperationCount--;
            if (activeOperationCount == 0 &&
                quiescenceContinuations.Count != 0)
            {
                ready = quiescenceContinuations.ToArray();
                quiescenceContinuations.Clear();
            }
        }

        ExceptionDispatchInfo? error = null;
        foreach (Action continuation in ready)
        {
            try
            {
                continuation();
            }
            catch (Exception exception)
            {
                error ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        error?.Throw();
    }

    internal void RunWhenQuiescent(Action continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        bool runNow;
        lock (gate)
        {
            runNow = activeOperationCount == 0;
            if (!runNow)
            {
                quiescenceContinuations.Add(continuation);
            }
        }

        if (runNow)
        {
            continuation();
        }
    }
}

internal sealed class ModelInspectionFixtureOperationDrain
{
    private readonly object gate = new();
    private readonly ModelInspectionFixtureOperationDomain lifetimeDomain;
    private readonly bool waitForLifetimeDomainQuiescence;
    private readonly Dictionary<int, int> leasesByThread = [];
    private int activeLeaseCount;
    private bool closed;
    private bool cleanupQueued;
    private bool cleanupStarted;
    private bool cleanupCompleted;
    private Action? cleanup;
    private ExceptionDispatchInfo? cleanupError;

    internal ModelInspectionFixtureOperationDrain() : this(
        new ModelInspectionFixtureOperationDomain(),
        waitForLifetimeDomainQuiescence: false)
    {
    }

    internal ModelInspectionFixtureOperationDrain(
        ModelInspectionFixtureOperationDomain lifetimeDomain,
        bool waitForLifetimeDomainQuiescence = false)
    {
        this.lifetimeDomain = lifetimeDomain ??
            throw new ArgumentNullException(nameof(lifetimeDomain));
        this.waitForLifetimeDomainQuiescence =
            waitForLifetimeDomainQuiescence;
    }

    internal bool CurrentThreadOwnsOperation =>
        lifetimeDomain.CurrentThreadOwnsOperation;

    internal Lease Enter()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            int threadId = Environment.CurrentManagedThreadId;
            activeLeaseCount = checked(activeLeaseCount + 1);
            leasesByThread.TryGetValue(threadId, out int owned);
            leasesByThread[threadId] = checked(owned + 1);
            lifetimeDomain.Enter(threadId);
            return new Lease(this, threadId);
        }
    }

    internal bool Close(Action cleanupAction)
    {
        ArgumentNullException.ThrowIfNull(cleanupAction);
        Action? cleanupToRun;
        bool first;
        bool queueDomainCleanup;
        bool waitForCompletion;
        lock (gate)
        {
            first = !closed;
            if (first)
            {
                closed = true;
                cleanup = cleanupAction;
            }

            queueDomainCleanup = waitForLifetimeDomainQuiescence &&
                !cleanupQueued;
            if (queueDomainCleanup)
            {
                cleanupQueued = true;
            }

            cleanupToRun = waitForLifetimeDomainQuiescence
                ? null
                : TakeCleanupLocked();
            waitForCompletion = !lifetimeDomain.CurrentThreadOwnsOperation;
        }

        if (queueDomainCleanup)
        {
            lifetimeDomain.RunWhenQuiescent(StartCleanup);
        }

        RunCleanup(cleanupToRun);
        if (waitForCompletion)
        {
            WaitForCompletion();
        }

        return first;
    }

    private void StartCleanup()
    {
        Action? cleanupToRun;
        lock (gate)
        {
            cleanupToRun = TakeCleanupLocked();
        }

        RunCleanup(cleanupToRun);
    }

    private Action? TakeCleanupLocked()
    {
        if (!closed || activeLeaseCount != 0 || cleanupStarted)
        {
            return null;
        }

        cleanupStarted = true;
        return cleanup ?? throw new InvalidOperationException(
            "The fixture operation drain closed without cleanup ownership.");
    }

    private void Exit(int entryThreadId)
    {
        Action? cleanupToRun;
        lock (gate)
        {
            if (!leasesByThread.TryGetValue(entryThreadId, out int owned) ||
                owned <= 0 || activeLeaseCount <= 0)
            {
                throw new InvalidOperationException(
                    "The fixture operation lease audit is unbalanced.");
            }

            if (owned == 1)
            {
                leasesByThread.Remove(entryThreadId);
            }
            else
            {
                leasesByThread[entryThreadId] = owned - 1;
            }

            activeLeaseCount--;
            cleanupToRun = waitForLifetimeDomainQuiescence
                ? null
                : TakeCleanupLocked();
        }

        ExceptionDispatchInfo? error = null;
        try
        {
            RunCleanup(cleanupToRun);
        }
        catch (Exception exception)
        {
            error = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            lifetimeDomain.Exit(entryThreadId);
        }
        catch (Exception exception)
        {
            error ??= ExceptionDispatchInfo.Capture(exception);
        }

        error?.Throw();
    }

    private void RunCleanup(Action? cleanupToRun)
    {
        if (cleanupToRun is null)
        {
            return;
        }

        ExceptionDispatchInfo? error = null;
        try
        {
            cleanupToRun();
        }
        catch (Exception exception)
        {
            error = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            lock (gate)
            {
                cleanupError = error;
                cleanupCompleted = true;
                Monitor.PulseAll(gate);
            }
        }

        error?.Throw();
    }

    private void WaitForCompletion()
    {
        ExceptionDispatchInfo? error;
        lock (gate)
        {
            while (!cleanupCompleted)
            {
                Monitor.Wait(gate);
            }

            error = cleanupError;
        }

        error?.Throw();
    }

    internal sealed class Lease : IDisposable
    {
        private ModelInspectionFixtureOperationDrain? owner;
        private readonly int entryThreadId;

        internal Lease(
            ModelInspectionFixtureOperationDrain owner,
            int entryThreadId)
        {
            this.owner = owner;
            this.entryThreadId = entryThreadId;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref owner, null)?.Exit(entryThreadId);
        }
    }
}

internal abstract class ModelInspectionFixtureDeferredEventHandle
{
    private readonly object gate = new();
    private readonly Action<ModelInspectionFixtureDeferredEventKind>?
        beforeDelivery;
    private Action? callback;
    private bool descriptorCaptured;
    private bool released;
    private bool retired;

    protected ModelInspectionFixtureDeferredEventHandle(
        ModelInspectionFixtureDeferredEvent descriptor,
        ModelInspectionFixtureDeferredEventKind expectedKind,
        Action<ModelInspectionFixtureDeferredEventKind>? beforeDelivery)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.Kind != expectedKind)
        {
            throw new ArgumentException(
                "The deferred fixture handle kind does not match its descriptor.",
                nameof(descriptor));
        }

        Kind = descriptor.Kind;
        OwnerAttempt = descriptor.OwnerAttempt;
        CaptureCheckpoint = descriptor.CaptureCheckpoint;
        ReleaseCheckpoint = descriptor.ReleaseCheckpoint;
        this.beforeDelivery = beforeDelivery;
    }

    public ModelInspectionFixtureDeferredEventKind Kind { get; }

    public int OwnerAttempt { get; }

    public string CaptureCheckpoint { get; }

    public string ReleaseCheckpoint { get; }

    internal bool IsCaptured
    {
        get
        {
            lock (gate)
            {
                return descriptorCaptured;
            }
        }
    }

    internal void CaptureDescriptor(
        ModelInspectionFixtureDeferredEvent descriptor,
        Action? capturedCallback)
    {
        RequireDescriptor(descriptor);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retired, this);
            if (descriptorCaptured)
            {
                throw new InvalidOperationException(
                    "The deferred fixture event was captured more than once.");
            }

            descriptorCaptured = true;
            callback = capturedCallback;
        }
    }

    internal void CaptureCallback(
        int ownerAttempt,
        string captureCheckpoint,
        Action capturedCallback)
    {
        ArgumentNullException.ThrowIfNull(capturedCallback);
        RequireCaptureIdentity(ownerAttempt, captureCheckpoint);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retired, this);
            if (!descriptorCaptured)
            {
                throw new InvalidOperationException(
                    "The deferred fixture event has not reached its capture checkpoint.");
            }

            if (released || callback is not null)
            {
                throw new InvalidOperationException(
                    "The deferred fixture callback has already been captured or released.");
            }

            callback = capturedCallback;
        }
    }

    internal bool Release(
        int ownerAttempt,
        string releaseCheckpoint,
        int startedAttemptCount)
    {
        Action prepared;
        lock (gate)
        {
            if (retired)
            {
                return false;
            }

            RequireReleaseIdentity(ownerAttempt, releaseCheckpoint);
            if (!descriptorCaptured)
            {
                throw new InvalidOperationException(
                    "The deferred fixture event has not been captured.");
            }

            if (startedAttemptCount <= OwnerAttempt)
            {
                throw new InvalidOperationException(
                    "A stale fixture event cannot be released before a later attempt starts.");
            }

            if (released)
            {
                throw new InvalidOperationException(
                    "The deferred fixture event has already been released.");
            }

            prepared = callback ?? throw new InvalidOperationException(
                "The deferred fixture callback has not been captured.");
            released = true;
            callback = null;
        }

        beforeDelivery?.Invoke(Kind);
        prepared();
        return true;
    }

    internal void Retire()
    {
        lock (gate)
        {
            if (retired)
            {
                return;
            }

            retired = true;
            callback = null;
        }
    }

    private void RequireDescriptor(
        ModelInspectionFixtureDeferredEvent descriptor)
    {
        if (descriptor.Kind != Kind ||
            descriptor.OwnerAttempt != OwnerAttempt ||
            !string.Equals(
                descriptor.CaptureCheckpoint,
                CaptureCheckpoint,
                StringComparison.Ordinal) ||
            !string.Equals(
                descriptor.ReleaseCheckpoint,
                ReleaseCheckpoint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The deferred fixture descriptor does not match its typed handle.");
        }
    }

    private void RequireCaptureIdentity(
        int ownerAttempt,
        string captureCheckpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(captureCheckpoint);
        if (ownerAttempt != OwnerAttempt ||
            !string.Equals(
                captureCheckpoint,
                CaptureCheckpoint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The deferred fixture capture identity is not exact.");
        }
    }

    private void RequireReleaseIdentity(
        int ownerAttempt,
        string releaseCheckpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseCheckpoint);
        if (ownerAttempt != OwnerAttempt ||
            !string.Equals(
                releaseCheckpoint,
                ReleaseCheckpoint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The deferred fixture release identity is not exact.");
        }
    }
}

internal sealed class ModelInspectionFixtureStaleProgressHandle :
    ModelInspectionFixtureDeferredEventHandle
{
    internal ModelInspectionFixtureStaleProgressHandle(
        ModelInspectionFixtureDeferredEvent descriptor,
        Action<ModelInspectionFixtureDeferredEventKind>? beforeDelivery) : base(
            descriptor,
            ModelInspectionFixtureDeferredEventKind.StaleProgress,
            beforeDelivery)
    {
    }
}

internal sealed class ModelInspectionFixtureStaleResultSnapshotHandle :
    ModelInspectionFixtureDeferredEventHandle
{
    internal ModelInspectionFixtureStaleResultSnapshotHandle(
        ModelInspectionFixtureDeferredEvent descriptor,
        Action<ModelInspectionFixtureDeferredEventKind>? beforeDelivery) : base(
            descriptor,
            ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot,
            beforeDelivery)
    {
    }
}

internal sealed class ModelInspectionFixtureStaleMotionCallbackHandle :
    ModelInspectionFixtureDeferredEventHandle
{
    internal ModelInspectionFixtureStaleMotionCallbackHandle(
        ModelInspectionFixtureDeferredEvent descriptor,
        Action<ModelInspectionFixtureDeferredEventKind>? beforeDelivery) : base(
            descriptor,
            ModelInspectionFixtureDeferredEventKind.StaleMotion,
            beforeDelivery)
    {
    }
}

internal sealed class ModelInspectionFixtureStaleAnnouncementCallbackHandle :
    ModelInspectionFixtureDeferredEventHandle
{
    internal ModelInspectionFixtureStaleAnnouncementCallbackHandle(
        ModelInspectionFixtureDeferredEvent descriptor,
        Action<ModelInspectionFixtureDeferredEventKind>? beforeDelivery) : base(
            descriptor,
            ModelInspectionFixtureDeferredEventKind.StaleAnnouncement,
            beforeDelivery)
    {
    }
}

internal sealed class ModelInspectionFixtureDeferredEventRegistry
{
    private static readonly ModelInspectionProgress StaleProgress = new(
        ModelInspectionStage.ReadModelConfiguration,
        ModelInspectionStageStatus.Active,
        completedStageCount: 1,
        totalStageCount: 5,
        stageFraction: null,
        userMessage: "Reading model configuration.");

    private readonly ModelInspectionFixtureSessionEvidence evidence;

    internal ModelInspectionFixtureDeferredEventRegistry(
        IReadOnlyList<ModelInspectionFixtureDeferredEvent> descriptors,
        ModelInspectionFixtureSessionEvidence evidence,
        Action<ModelInspectionFixtureDeferredEventKind>?
            beforeDelivery = null)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        this.evidence = evidence ??
            throw new ArgumentNullException(nameof(evidence));
        List<ModelInspectionFixtureStaleProgressHandle> progress = [];
        List<ModelInspectionFixtureStaleResultSnapshotHandle> results = [];
        List<ModelInspectionFixtureStaleMotionCallbackHandle> motion = [];
        List<ModelInspectionFixtureStaleAnnouncementCallbackHandle>
            announcements = [];
        foreach (ModelInspectionFixtureDeferredEvent descriptor in descriptors)
        {
            switch (descriptor.Kind)
            {
                case ModelInspectionFixtureDeferredEventKind.StaleProgress:
                    progress.Add(
                        new ModelInspectionFixtureStaleProgressHandle(
                            descriptor,
                            beforeDelivery));
                    break;
                case ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot:
                    results.Add(
                        new ModelInspectionFixtureStaleResultSnapshotHandle(
                            descriptor,
                            beforeDelivery));
                    break;
                case ModelInspectionFixtureDeferredEventKind.StaleMotion:
                    motion.Add(
                        new ModelInspectionFixtureStaleMotionCallbackHandle(
                            descriptor,
                            beforeDelivery));
                    break;
                case ModelInspectionFixtureDeferredEventKind.StaleAnnouncement:
                    announcements.Add(
                        new ModelInspectionFixtureStaleAnnouncementCallbackHandle(
                            descriptor,
                            beforeDelivery));
                    break;
                default:
                    throw new InvalidOperationException(
                        "The mapped deferred fixture kind has no typed handle.");
            }
        }

        StaleProgressHandles = progress.ToArray();
        StaleResultSnapshotHandles = results.ToArray();
        StaleMotionCallbackHandles = motion.ToArray();
        StaleAnnouncementCallbackHandles = announcements.ToArray();
        RequireUniqueHandles();
    }

    internal IReadOnlyList<ModelInspectionFixtureStaleProgressHandle>
        StaleProgressHandles { get; }

    internal IReadOnlyList<ModelInspectionFixtureStaleResultSnapshotHandle>
        StaleResultSnapshotHandles { get; }

    internal IReadOnlyList<ModelInspectionFixtureStaleMotionCallbackHandle>
        StaleMotionCallbackHandles { get; }

    internal IReadOnlyList<ModelInspectionFixtureStaleAnnouncementCallbackHandle>
        StaleAnnouncementCallbackHandles { get; }

    internal void Capture(
        ModelInspectionFixtureDeferredEvent descriptor,
        IProgress<ModelInspectionProgress>? progress)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        switch (descriptor.Kind)
        {
            case ModelInspectionFixtureDeferredEventKind.StaleProgress:
                IProgress<ModelInspectionProgress> reporter = progress ??
                    throw new InvalidOperationException(
                        "A declared stale-progress event requires a progress reporter.");
                RequireDescriptor(StaleProgressHandles, descriptor)
                    .CaptureDescriptor(
                        descriptor,
                        () => reporter.Report(StaleProgress));
                break;
            case ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot:
                RequireDescriptor(StaleResultSnapshotHandles, descriptor)
                    .CaptureDescriptor(descriptor, capturedCallback: null);
                break;
            case ModelInspectionFixtureDeferredEventKind.StaleMotion:
                RequireDescriptor(StaleMotionCallbackHandles, descriptor)
                    .CaptureDescriptor(descriptor, capturedCallback: null);
                break;
            case ModelInspectionFixtureDeferredEventKind.StaleAnnouncement:
                RequireDescriptor(StaleAnnouncementCallbackHandles, descriptor)
                    .CaptureDescriptor(descriptor, capturedCallback: null);
                break;
            default:
                throw new InvalidOperationException(
                    "The deferred fixture event has no typed capture path.");
        }
    }

    internal bool ReleaseStaleProgress(
        int ownerAttempt,
        string releaseCheckpoint,
        int startedAttemptCount)
    {
        bool released = RequireRelease(
                StaleProgressHandles,
                ownerAttempt,
                releaseCheckpoint)
            .Release(ownerAttempt, releaseCheckpoint, startedAttemptCount);
        if (released)
        {
            evidence.RecordDeferredProgressRelease();
        }

        return released;
    }

    internal void CaptureStaleResultSnapshot(
        int ownerAttempt,
        string captureCheckpoint,
        Action callback) => RequireCapture(
            StaleResultSnapshotHandles,
            ownerAttempt,
            captureCheckpoint).CaptureCallback(
                ownerAttempt,
                captureCheckpoint,
                callback);

    internal bool ReleaseStaleResultSnapshot(
        int ownerAttempt,
        string releaseCheckpoint,
        int startedAttemptCount) => ReleaseCallback(
            RequireRelease(
                StaleResultSnapshotHandles,
                ownerAttempt,
                releaseCheckpoint),
            ownerAttempt,
            releaseCheckpoint,
            startedAttemptCount,
            evidence.RecordDeferredResultSnapshotRelease);

    internal void CaptureStaleMotionCallback(
        int ownerAttempt,
        string captureCheckpoint,
        Action callback) => RequireCapture(
            StaleMotionCallbackHandles,
            ownerAttempt,
            captureCheckpoint).CaptureCallback(
                ownerAttempt,
                captureCheckpoint,
                callback);

    internal bool ReleaseStaleMotionCallback(
        int ownerAttempt,
        string releaseCheckpoint,
        int startedAttemptCount) => ReleaseCallback(
            RequireRelease(
                StaleMotionCallbackHandles,
                ownerAttempt,
                releaseCheckpoint),
            ownerAttempt,
            releaseCheckpoint,
            startedAttemptCount,
            evidence.RecordDeferredMotionRelease);

    internal void CaptureStaleAnnouncementCallback(
        int ownerAttempt,
        string captureCheckpoint,
        Action callback) => RequireCapture(
            StaleAnnouncementCallbackHandles,
            ownerAttempt,
            captureCheckpoint).CaptureCallback(
                ownerAttempt,
                captureCheckpoint,
                callback);

    internal bool ReleaseStaleAnnouncementCallback(
        int ownerAttempt,
        string releaseCheckpoint,
        int startedAttemptCount) => ReleaseCallback(
            RequireRelease(
                StaleAnnouncementCallbackHandles,
                ownerAttempt,
                releaseCheckpoint),
            ownerAttempt,
            releaseCheckpoint,
            startedAttemptCount,
            evidence.RecordDeferredAnnouncementRelease);

    internal void Retire()
    {
        foreach (ModelInspectionFixtureDeferredEventHandle handle in
                 AllHandles())
        {
            handle.Retire();
        }
    }

    private static bool ReleaseCallback(
        ModelInspectionFixtureDeferredEventHandle handle,
        int ownerAttempt,
        string releaseCheckpoint,
        int startedAttemptCount,
        Action recordRelease)
    {
        bool released = handle.Release(
            ownerAttempt,
            releaseCheckpoint,
            startedAttemptCount);
        if (released)
        {
            recordRelease();
        }

        return released;
    }

    private static THandle RequireDescriptor<THandle>(
        IReadOnlyList<THandle> handles,
        ModelInspectionFixtureDeferredEvent descriptor)
        where THandle : ModelInspectionFixtureDeferredEventHandle
    {
        foreach (THandle handle in handles)
        {
            if (handle.Kind == descriptor.Kind &&
                handle.OwnerAttempt == descriptor.OwnerAttempt &&
                string.Equals(
                    handle.CaptureCheckpoint,
                    descriptor.CaptureCheckpoint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    handle.ReleaseCheckpoint,
                    descriptor.ReleaseCheckpoint,
                    StringComparison.Ordinal))
            {
                return handle;
            }
        }

        throw new InvalidOperationException(
            "The deferred fixture descriptor has no exact typed handle.");
    }

    private static THandle RequireCapture<THandle>(
        IReadOnlyList<THandle> handles,
        int ownerAttempt,
        string captureCheckpoint)
        where THandle : ModelInspectionFixtureDeferredEventHandle
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(captureCheckpoint);
        foreach (THandle handle in handles)
        {
            if (handle.OwnerAttempt == ownerAttempt &&
                string.Equals(
                    handle.CaptureCheckpoint,
                    captureCheckpoint,
                    StringComparison.Ordinal))
            {
                return handle;
            }
        }

        throw new InvalidOperationException(
            "No declared typed stale-event handle owns that capture identity.");
    }

    private static THandle RequireRelease<THandle>(
        IReadOnlyList<THandle> handles,
        int ownerAttempt,
        string releaseCheckpoint)
        where THandle : ModelInspectionFixtureDeferredEventHandle
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseCheckpoint);
        foreach (THandle handle in handles)
        {
            if (handle.OwnerAttempt == ownerAttempt &&
                string.Equals(
                    handle.ReleaseCheckpoint,
                    releaseCheckpoint,
                    StringComparison.Ordinal))
            {
                return handle;
            }
        }

        throw new InvalidOperationException(
            "No declared typed stale-event handle owns that release identity.");
    }

    private IEnumerable<ModelInspectionFixtureDeferredEventHandle> AllHandles()
    {
        foreach (ModelInspectionFixtureDeferredEventHandle handle in
                 StaleProgressHandles)
        {
            yield return handle;
        }

        foreach (ModelInspectionFixtureDeferredEventHandle handle in
                 StaleResultSnapshotHandles)
        {
            yield return handle;
        }

        foreach (ModelInspectionFixtureDeferredEventHandle handle in
                 StaleMotionCallbackHandles)
        {
            yield return handle;
        }

        foreach (ModelInspectionFixtureDeferredEventHandle handle in
                 StaleAnnouncementCallbackHandles)
        {
            yield return handle;
        }
    }

    private void RequireUniqueHandles()
    {
        HashSet<string> identities = new(StringComparer.Ordinal);
        foreach (ModelInspectionFixtureDeferredEventHandle handle in
                 AllHandles())
        {
            string identity = string.Join(
                "|",
                handle.Kind,
                handle.OwnerAttempt,
                handle.CaptureCheckpoint,
                handle.ReleaseCheckpoint);
            if (!identities.Add(identity))
            {
                throw new InvalidOperationException(
                    "A deferred fixture handle identity is duplicated.");
            }
        }
    }
}
#endif
