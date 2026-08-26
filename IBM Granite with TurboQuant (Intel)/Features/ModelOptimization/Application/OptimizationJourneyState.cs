using System;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Journey;

internal enum OptimizationJourneyKind
{
    Confirmation = 0,
    Running,
    Cancelling,
    Cancelled,
    Failed,
    ReplanRequired,
    SucceededPersistent,
    SucceededRuntimeProfile
}

internal sealed record OptimizationJourneyState(
    OptimizationJourneyKind Kind,
    OptimizationJourneyEntryContext Entry,
    long Generation,
    OptimizationProgressStage? Stage,
    double Fraction,
    OptimizationExecutionResult? Result)
{
    internal static OptimizationJourneyState Initial(
        OptimizationJourneyEntryContext entry) =>
        new(OptimizationJourneyKind.Confirmation,
            entry ?? throw new ArgumentNullException(nameof(entry)),
            0,
            null,
            0d,
            null);

    internal bool IsTerminal => Kind is OptimizationJourneyKind.Cancelled
        or OptimizationJourneyKind.Failed
        or OptimizationJourneyKind.ReplanRequired
        or OptimizationJourneyKind.SucceededPersistent
        or OptimizationJourneyKind.SucceededRuntimeProfile;
}

internal abstract record OptimizationJourneyEvent(long Generation);
internal sealed record OptimizationStarted(long AttemptGeneration)
    : OptimizationJourneyEvent(AttemptGeneration);
internal sealed record OptimizationProgressed(OptimizationProgress Progress)
    : OptimizationJourneyEvent(Progress.Generation);
internal sealed record OptimizationCancellationRequested(long AttemptGeneration)
    : OptimizationJourneyEvent(AttemptGeneration);
internal sealed record OptimizationCancelled(long AttemptGeneration)
    : OptimizationJourneyEvent(AttemptGeneration);
internal sealed record OptimizationCompleted(
    long AttemptGeneration,
    OptimizationExecutionResult Result)
    : OptimizationJourneyEvent(AttemptGeneration);
