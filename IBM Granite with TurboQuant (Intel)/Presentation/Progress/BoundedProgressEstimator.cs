using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Presentation.Progress;

/// <summary>successful row-only catch-up. never delays an operation or its terminal view</summary>
internal sealed class BoundedProgressCompletion
{
    private readonly TimeProvider clock;
    private object? owner;
    private double fraction;
    private double origin;
    private long started;
    private bool sawActive;
    private bool completing;
    private bool finalFramePending;

    internal BoundedProgressCompletion(TimeProvider? clock = null) => this.clock = clock ?? TimeProvider.System;

    internal void Observe(object owner, bool active, bool completed, double fraction, bool motion)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!double.IsFinite(fraction) || fraction < 0 || fraction > 1) throw new ArgumentOutOfRangeException(nameof(fraction));
        if (!ReferenceEquals(this.owner, owner))
        {
            Reset();
            this.owner = owner;
        }
        if (active)
        {
            this.fraction = fraction;
            sawActive = true;
            completing = false;
            finalFramePending = false;
        }
        else if (!completed)
        {
            this.fraction = fraction;
            sawActive = completing = false;
            finalFramePending = false;
        }
        else if (!motion || !sawActive)
        {
            this.fraction = 1;
            completing = false;
            finalFramePending = false;
        }
        else if (!completing && this.fraction < 1)
        {
            origin = this.fraction;
            started = clock.GetTimestamp();
            completing = true;
        }
    }

    internal double GetFraction()
    {
        if (!completing) return fraction;
        double elapsed = Math.Clamp(clock.GetElapsedTime(started).TotalMilliseconds / 500, 0, 1);
        fraction = origin + (1 - origin) * elapsed;
        if (elapsed == 1) { completing = false; finalFramePending = true; }
        return fraction;
    }
    internal bool IsCompleting() { GetFraction(); return completing; }
    internal bool TryGetCompletionFrame(out double value)
    {
        value = GetFraction();
        if (completing) return true;
        if (!finalFramePending) return false;
        finalFramePending = false;
        return true;
    }
    internal void Reset() { owner = null; fraction = origin = 0; sawActive = completing = finalFramePending = false; }
}

internal sealed class BoundedProgressInterpolation
{
    private readonly TimeProvider clock;
    private long started;
    private double origin;
    private double target;
    internal BoundedProgressInterpolation(TimeProvider? clock = null) => this.clock = clock ?? TimeProvider.System;
    internal void SetTarget(double value, bool motion)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        double current = GetFraction();
        if (!motion || value < current) { origin = target = value; return; }
        if (value == target) return;
        if (current == target)
        {
            origin = current;
            started = clock.GetTimestamp();
        }
        target = value;
    }
    internal double GetFraction()
    {
        double elapsed = Math.Clamp(clock.GetElapsedTime(started).TotalMilliseconds / 180, 0, 1);
        return origin + (target - origin) * elapsed;
    }
}

/// <summary>presentation-only, explicitly estimated progress for an opaque phase</summary>
internal sealed class BoundedProgressEstimator
{
    private readonly TimeProvider clock;
    private object? owner;
    private object? stage;
    private long revision;
    private long started;
    private double origin;
    private double? measured;
    private bool complete;
    private bool frozen;
    private bool motion;
    private double held;

    internal BoundedProgressEstimator(TimeProvider? clock = null) => this.clock = clock ?? TimeProvider.System;

    internal void Update(object owner, long revision, object stage, double? measured, bool complete, bool motion)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(stage);
        if (measured is double value && (!double.IsFinite(value) || value < 0 || value > 1))
            throw new ArgumentOutOfRangeException(nameof(measured));
        bool newOwner = !ReferenceEquals(this.owner, owner);
        if (!newOwner && (revision < this.revision || frozen)) return;
        bool newStage = newOwner || !Equals(this.stage, stage);
        double previous = newStage ? 0 : GetFraction();
        if (newStage || (this.measured.HasValue && !measured.HasValue) || this.motion != motion)
        {
            origin = Math.Min(.95, previous);
            started = clock.GetTimestamp();
        }
        this.owner = owner;
        this.stage = stage;
        this.revision = revision;
        this.measured = measured is double measuredValue
            ? Math.Max(previous, measuredValue)
            : null;
        this.complete = complete;
        this.motion = motion;
        frozen = false;
    }

    internal double GetFraction()
    {
        if (frozen) return held;
        if (complete) return 1;
        if (measured is double fraction) return fraction;
        if (owner is null || !motion) return origin;
        double seconds = Math.Max(0, clock.GetElapsedTime(started).TotalSeconds);
        return Math.Min(.95, origin + (.95 - origin) * (1 - Math.Exp(-seconds / 120)));
    }

    internal bool IsEstimated() => !complete && measured is null;

    internal void Freeze()
    {
        held = GetFraction();
        frozen = true;
    }

    internal void Reset()
    {
        owner = stage = null;
        measured = null;
        revision = 0;
        origin = held = 0;
        frozen = complete = motion = false;
    }
}

internal enum SerializedProgressStageState
{
    Waiting,
    Active,
    Completed,
    NotNeeded,
}

internal readonly record struct SerializedProgressStageObservation(
    bool IsActive,
    bool IsCompleted,
    bool IsNotNeeded,
    double? MeasuredFraction = null);

internal sealed class SerializedProgressFrame
{
    internal SerializedProgressFrame(
        IReadOnlyList<SerializedProgressStageState> stages,
        int activeIndex,
        double activeFraction,
        double overallValue,
        bool needsTicks)
    {
        Stages = stages;
        ActiveIndex = activeIndex;
        ActiveFraction = activeFraction;
        OverallValue = overallValue;
        NeedsTicks = needsTicks;
    }

    internal IReadOnlyList<SerializedProgressStageState> Stages { get; }
    internal int ActiveIndex { get; }
    internal double ActiveFraction { get; }
    internal double OverallValue { get; }
    internal bool NeedsTicks { get; }
    internal bool IsFinished => ActiveIndex < 0;
}

/// <summary>
/// serializes already-observed stage results without delaying or influencing the operation
/// </summary>
internal sealed class SerializedProgressSequence
{
    private static readonly TimeSpan CompletionDuration = TimeSpan.FromMilliseconds(1800);
    private static readonly TimeSpan FullFrameDwell = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan TerminalDwell = TimeSpan.FromMilliseconds(160);
    private readonly TimeProvider clock;
    private readonly BoundedProgressEstimator estimate;
    private object? owner;
    private long revision;
    private long epoch;
    private SerializedProgressStageObservation[] observations = [];
    private SerializedProgressStageState[] stages = [];
    private int activeIndex = -1;
    private double activeFraction;
    private double completionOrigin;
    private long phaseStarted;
    private bool motion;
    private Phase phase;

    internal SerializedProgressSequence(TimeProvider? clock = null)
    {
        this.clock = clock ?? TimeProvider.System;
        estimate = new BoundedProgressEstimator(this.clock);
    }

    internal long Epoch => epoch;

    internal void Observe(
        object owner,
        long revision,
        IReadOnlyList<SerializedProgressStageObservation> observations,
        bool motion)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(observations);
        if (observations.Count == 0)
            throw new ArgumentException("At least one progress stage is required.", nameof(observations));
        for (int index = 0; index < observations.Count; index++)
        {
            double? measured = observations[index].MeasuredFraction;
            if (measured is double value && (!double.IsFinite(value) || value < 0 || value > 1))
                throw new ArgumentOutOfRangeException(nameof(observations));
        }

        bool newOwner = !Equals(this.owner, owner) || this.observations.Length != observations.Count;
        if (!newOwner && revision < this.revision) return;
        if (newOwner) Reset(owner, observations.Count);

        this.revision = revision;
        this.motion = motion;
        for (int index = 0; index < observations.Count; index++)
            this.observations[index] = observations[index];

        if (activeIndex < 0) return;
        if (phase == Phase.AwaitingAuthority)
        {
            if (HasAuthoritativeNextStage()) PromoteNextStage();
            return;
        }
        if (phase == Phase.Active)
        {
            SerializedProgressStageObservation observation = this.observations[activeIndex];
            if (observation.IsCompleted || observation.IsNotNeeded)
            {
                BeginCompletion();
            }
            else
            {
                double? measured = observation.IsActive
                    ? observation.MeasuredFraction is double value ? Math.Min(.99, value) : null
                    : null;
                estimate.Update(this.owner!, revision, activeIndex, measured, false, motion);
            }
        }
    }

    internal SerializedProgressFrame GetFrame()
    {
        if (stages.Length == 0)
            return new SerializedProgressFrame([], -1, 0, 0, false);
        AdvancePhase();
        if (activeIndex < 0)
        {
            return new SerializedProgressFrame(
                (SerializedProgressStageState[])stages.Clone(), -1, 1, stages.Length, false);
        }

        if (phase == Phase.Active)
        {
            double target = Math.Min(.99, estimate.GetFraction());
            activeFraction = motion
                ? Math.Max(activeFraction, Math.Min(target, activeFraction + .01))
                : Math.Max(activeFraction, target);
        }
        else if (phase == Phase.Completing)
        {
            double completionMilliseconds = CompletionDuration.TotalMilliseconds
                * Math.Max(.08, 1 - completionOrigin);
            double elapsed = motion
                ? Math.Clamp(
                    clock.GetElapsedTime(phaseStarted).TotalMilliseconds / completionMilliseconds,
                    0,
                    1)
                : 1;
            double desired = completionOrigin + (1 - completionOrigin) * elapsed;
            activeFraction = motion
                ? Math.Min(desired, activeFraction + .01)
                : 1;
            if (elapsed >= 1 && activeFraction >= .999999)
            {
                activeFraction = 1;
                phase = Phase.FullFrame;
                phaseStarted = clock.GetTimestamp();
            }
        }

        int settled = 0;
        for (int index = 0; index < stages.Length; index++)
            if (stages[index] is SerializedProgressStageState.Completed or SerializedProgressStageState.NotNeeded)
                settled++;
        double overall = settled + (stages[activeIndex] == SerializedProgressStageState.Active
            ? activeFraction
            : 0);
        bool needsTicks = phase is not Phase.Active and not Phase.AwaitingAuthority
            || (motion && (estimate.IsEstimated()
                || activeFraction + double.Epsilon < Math.Min(.99, estimate.GetFraction())));
        return new SerializedProgressFrame(
            (SerializedProgressStageState[])stages.Clone(), activeIndex, activeFraction, overall, needsTicks);
    }

    internal void Abort()
    {
        checked { epoch++; }
        owner = null;
        revision = 0;
        observations = [];
        stages = [];
        activeIndex = -1;
        activeFraction = completionOrigin = 0;
        phaseStarted = 0;
        motion = false;
        phase = Phase.Active;
        estimate.Reset();
    }

    private void Reset(object owner, int stageCount)
    {
        checked { epoch++; }
        this.owner = owner;
        revision = 0;
        observations = new SerializedProgressStageObservation[stageCount];
        stages = new SerializedProgressStageState[stageCount];
        activeIndex = 0;
        stages[0] = SerializedProgressStageState.Active;
        activeFraction = completionOrigin = 0;
        phaseStarted = clock.GetTimestamp();
        phase = Phase.Active;
        estimate.Reset();
        estimate.Update(owner, 0, 0, null, false, motion);
    }

    private void BeginCompletion()
    {
        activeFraction = Math.Clamp(activeFraction, 0, .99);
        completionOrigin = activeFraction;
        phaseStarted = clock.GetTimestamp();
        phase = Phase.Completing;
    }

    private void AdvancePhase()
    {
        if (activeIndex < 0) return;
        if (phase == Phase.FullFrame)
        {
            if (clock.GetElapsedTime(phaseStarted) < (motion ? FullFrameDwell : TimeSpan.Zero))
                return;
            SerializedProgressStageObservation observation = observations[activeIndex];
            stages[activeIndex] = observation.IsNotNeeded
                ? SerializedProgressStageState.NotNeeded
                : SerializedProgressStageState.Completed;
            phase = Phase.TerminalFrame;
            phaseStarted = clock.GetTimestamp();
            return;
        }
        if (phase != Phase.TerminalFrame
            || clock.GetElapsedTime(phaseStarted) < (motion ? TerminalDwell : TimeSpan.Zero))
            return;

        if (activeIndex + 1 >= stages.Length)
        {
            activeIndex = -1;
            activeFraction = 1;
            return;
        }

        if (!HasAuthoritativeNextStage())
        {
            phase = Phase.AwaitingAuthority;
            return;
        }

        PromoteNextStage();
    }

    private bool HasAuthoritativeNextStage()
    {
        int nextIndex = activeIndex + 1;
        if (nextIndex < 0 || nextIndex >= observations.Length) return false;
        SerializedProgressStageObservation next = observations[nextIndex];
        return next.IsActive || next.IsCompleted || next.IsNotNeeded;
    }

    private void PromoteNextStage()
    {
        activeIndex++;

        stages[activeIndex] = SerializedProgressStageState.Active;
        activeFraction = completionOrigin = 0;
        phase = Phase.Active;
        phaseStarted = clock.GetTimestamp();
        estimate.Reset();
        estimate.Update(owner!, revision, activeIndex, null, false, motion);
        SerializedProgressStageObservation next = observations[activeIndex];
        if (next.IsCompleted || next.IsNotNeeded) BeginCompletion();
        else if (next.IsActive)
            estimate.Update(owner!, revision, activeIndex,
                next.MeasuredFraction is double value ? Math.Min(.99, value) : null,
                false, motion);
    }

    private enum Phase
    {
        Active,
        Completing,
        FullFrame,
        TerminalFrame,
        AwaitingAuthority,
    }
}
