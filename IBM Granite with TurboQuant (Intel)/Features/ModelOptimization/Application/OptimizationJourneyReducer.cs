using System;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Journey;

internal static class OptimizationJourneyReducer
{
    internal static OptimizationJourneyState Apply(
        OptimizationJourneyState state,
        OptimizationJourneyEvent @event)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(@event);
        if (state.IsTerminal)
        {
            return state;
        }

        return @event switch
        {
            OptimizationStarted started
                when state.Kind == OptimizationJourneyKind.Confirmation
                    && started.Generation > 0 =>
                state with
                {
                    Kind = OptimizationJourneyKind.Running,
                    Generation = started.Generation,
                    Stage = OptimizationProgressStage.Preflight,
                    Fraction = 0d
                },
            OptimizationProgressed progressed => Progress(state, progressed.Progress),
            OptimizationCancellationRequested cancelled
                when state.Kind == OptimizationJourneyKind.Running
                    && cancelled.Generation == state.Generation =>
                state with { Kind = OptimizationJourneyKind.Cancelling },
            OptimizationCancelled cancelled
                when state.Kind is OptimizationJourneyKind.Running
                    or OptimizationJourneyKind.Cancelling
                    && cancelled.Generation == state.Generation =>
                state with { Kind = OptimizationJourneyKind.Cancelled },
            OptimizationCompleted completed => Complete(state, completed),
            _ => state
        };
    }

    private static OptimizationJourneyState Progress(
        OptimizationJourneyState state,
        OptimizationProgress progress)
    {
        if (state.Kind != OptimizationJourneyKind.Running
            || progress.Generation != state.Generation
            || progress.OptimizationPlanId
                != state.Entry.OptimizationHandoff.OptimizationPlanId
            || !string.Equals(
                progress.ConfigurationSha256,
                state.Entry.OptimizationHandoff.ConfigurationSha256,
                StringComparison.Ordinal)
            || state.Stage is { } current && progress.Stage < current
            || progress.Stage == state.Stage && progress.Fraction < state.Fraction)
        {
            return state;
        }
        return state with
        {
            Stage = progress.Stage,
            Fraction = progress.Fraction
        };
    }

    private static OptimizationJourneyState Complete(
        OptimizationJourneyState state,
        OptimizationCompleted completed)
    {
        OptimizationExecutionResult result = completed.Result;
        OptimizationExecutionPlan plan =
            state.Entry.OptimizationHandoff.Plan;
        if (state.Kind != OptimizationJourneyKind.Running
            || completed.Generation != state.Generation
            || result.ExecutionId == Guid.Empty
            || result.OptimizationPlanId != plan.OptimizationPlanId
            || result.Route != plan.Route
            || !string.Equals(
                result.ConfigurationSha256,
                plan.ConfigurationSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                result.SourceSha256,
                plan.Binding.ModelSha256,
                StringComparison.Ordinal)
            || result.SourceLengthBytes != plan.Binding.ModelLengthBytes
            || !string.Equals(
                result.ModelInspectionRunId,
                plan.Binding.ModelInspectionRunId,
                StringComparison.Ordinal)
            || !string.Equals(
                result.ModelInspectionHandoffId,
                plan.Binding.ModelInspectionHandoffId,
                StringComparison.Ordinal)
            || !string.Equals(
                result.ProductHardwareRunId,
                plan.Binding.ProductHardwareRunId,
                StringComparison.Ordinal)
            || !string.Equals(
                result.HardwareSnapshotSha256,
                plan.Binding.HardwareSnapshotSha256,
                StringComparison.Ordinal))
        {
            return state;
        }

        OptimizationJourneyKind kind = result.Status switch
        {
            OptimizationExecutionStatus.SucceededPersistent =>
                OptimizationJourneyKind.SucceededPersistent,
            OptimizationExecutionStatus.SucceededRuntimeProfile =>
                OptimizationJourneyKind.SucceededRuntimeProfile,
            OptimizationExecutionStatus.Cancelled => OptimizationJourneyKind.Cancelled,
            OptimizationExecutionStatus.Failed => OptimizationJourneyKind.Failed,
            OptimizationExecutionStatus.ReplanRequired =>
                OptimizationJourneyKind.ReplanRequired,
            _ => state.Kind
        };
        return kind == state.Kind
            ? state
            : state with { Kind = kind, Result = result };
    }
}
