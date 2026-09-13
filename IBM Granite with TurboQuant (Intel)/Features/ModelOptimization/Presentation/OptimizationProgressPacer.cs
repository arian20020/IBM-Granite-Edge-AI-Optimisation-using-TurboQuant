using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.ModelOptimization.Presentation;

internal sealed class OptimizationProgressPacer
{
    private static readonly TimeSpan MinimumDwell = TimeSpan.FromMilliseconds(550);
    private readonly List<OptimizationPresentationState> _pending = new();
    private OptimizationPresentationState? _displayed;
    private TimeSpan _displayedAt;

    internal long Epoch { get; private set; }

    // Only observed running snapshots are retained. Domain state and actions never wait here.
    internal OptimizationPresentationState? Offer(
        OptimizationPresentationState state, TimeSpan now, bool motionEnabled)
    {
        int active = ActiveIndex(state);
        if (state.Kind != OptimizationPageStateKind.Running || !state.CanCancel
            || !motionEnabled || active < 0)
        {
            Reset();
            return state;
        }

        if (_displayed is null
            || _displayed.OptimizationPlanId != state.OptimizationPlanId
            || _displayed.ConfigurationSha256 != state.ConfigurationSha256
            || active < ActiveIndex(_pending.Count > 0 ? _pending[^1] : _displayed))
        {
            Reset();
            return Display(state, now);
        }

        if (_pending.Count == 0 && ActiveIndex(_displayed) == active)
        {
            _displayed = state;
            return state; // same-stage detail changes must not restart its dwell
        }

        if (_pending.Count > 0 && ActiveIndex(_pending[^1]) == active)
            _pending[^1] = state;
        else
            _pending.Add(state);
        return Advance(now);
    }

    internal OptimizationPresentationState? Advance(TimeSpan now)
    {
        if (_pending.Count == 0 || now - _displayedAt < MinimumDwell) return null;
        OptimizationPresentationState next = _pending[0];
        _pending.RemoveAt(0);
        return Display(next, now);
    }

    internal TimeSpan? Remaining(TimeSpan now) => _pending.Count == 0
        ? null : TimeSpan.FromTicks(Math.Max(1, (MinimumDwell - (now - _displayedAt)).Ticks));

    internal void Reset()
    {
        Epoch++;
        _pending.Clear();
        _displayed = null;
    }

    internal static int ActiveIndex(OptimizationPresentationState state)
    {
        if (state.ProgressRows.Count != 7) return -1;
        int active = -1;
        for (int i = 0; i < state.ProgressRows.Count; i++)
        {
            if (state.ProgressRows[i].Status != OptimizationStageStatus.Active) continue;
            if (active >= 0) return -1;
            active = i;
        }
        return active;
    }

    private OptimizationPresentationState Display(OptimizationPresentationState state, TimeSpan now)
    {
        _displayed = state;
        _displayedAt = now;
        return state;
    }
}
