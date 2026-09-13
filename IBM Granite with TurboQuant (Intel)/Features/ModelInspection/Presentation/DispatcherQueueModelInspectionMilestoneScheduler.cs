using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal sealed class DispatcherQueueModelInspectionMilestoneScheduler :
    IModelInspectionMilestoneScheduler
{
    private readonly object gate = new();
    private readonly DispatcherQueue dispatcherQueue;
    private readonly HashSet<ScheduledCallback> scheduledCallbacks = [];
    private readonly long startTimestamp = Stopwatch.GetTimestamp();
    private bool disposed;

    internal DispatcherQueueModelInspectionMilestoneScheduler(
        DispatcherQueue dispatcherQueue)
    {
        this.dispatcherQueue = dispatcherQueue ??
            throw new ArgumentNullException(nameof(dispatcherQueue));
    }

    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(startTimestamp);

    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delay),
                delay,
                "A milestone delay must be positive.");
        }

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            DispatcherQueueTimer timer = dispatcherQueue.CreateTimer();
            timer.Interval = delay;
            timer.IsRepeating = false;
            var scheduled = new ScheduledCallback(this, timer, callback);
            scheduledCallbacks.Add(scheduled);
            try
            {
                scheduled.Start();
            }
            catch
            {
                scheduledCallbacks.Remove(scheduled);
                scheduled.Cancel();
                throw;
            }

            return scheduled;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (ScheduledCallback scheduled in scheduledCallbacks)
            {
                scheduled.Cancel();
            }

            scheduledCallbacks.Clear();
        }
    }

    private void Cancel(ScheduledCallback scheduled)
    {
        lock (gate)
        {
            if (!scheduledCallbacks.Remove(scheduled))
            {
                return;
            }

            scheduled.Cancel();
        }
    }

    private void Fire(ScheduledCallback scheduled)
    {
        lock (gate)
        {
            if (disposed || !scheduledCallbacks.Remove(scheduled))
            {
                return;
            }

            scheduled.Complete();
            scheduled.Callback();
        }
    }

    private sealed class ScheduledCallback : IDisposable
    {
        private readonly DispatcherQueueModelInspectionMilestoneScheduler owner;
        private readonly DispatcherQueueTimer timer;
        private bool finished;

        internal ScheduledCallback(
            DispatcherQueueModelInspectionMilestoneScheduler owner,
            DispatcherQueueTimer timer,
            Action callback)
        {
            this.owner = owner;
            this.timer = timer;
            Callback = callback;
        }

        internal Action Callback { get; }

        internal void Start()
        {
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        internal void Complete()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            timer.Stop();
            timer.Tick -= Timer_Tick;
        }

        internal void Cancel() => Complete();

        public void Dispose() => owner.Cancel(this);

        private void Timer_Tick(
            DispatcherQueueTimer sender,
            object arguments) => owner.Fire(this);
    }
}
