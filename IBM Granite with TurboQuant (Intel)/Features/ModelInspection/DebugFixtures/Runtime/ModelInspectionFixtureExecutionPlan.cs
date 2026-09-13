#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.Fixtures;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;

internal sealed record ModelInspectionFixtureExecutionPlan(
    ModelInspectionRequest Request,
    IReadOnlyList<ModelInspectionFixtureAttemptPlan> Attempts,
    IReadOnlyList<ModelInspectionFixtureDeferredEvent> DeferredEvents);

internal sealed record ModelInspectionFixtureAttemptPlan(
    int Attempt,
    IReadOnlyList<ModelInspectionFixtureServiceStepPlan> ServiceSteps);

internal sealed record ModelInspectionFixtureServiceStepPlan(
    ModelInspectionFixtureServiceTriggerKind TriggerKind,
    string? Checkpoint,
    ModelInspectionProgress? Progress,
    ModelInspectionExecutionResult? TerminalResult,
    ModelInspectionFixtureDeferredEvent? DeferredEvent);

internal enum ModelInspectionFixtureDeferredEventKind
{
    StaleProgress,
    StaleResultSnapshot,
    StaleMotion,
    StaleAnnouncement
}

internal sealed record ModelInspectionFixtureDeferredEvent(
    ModelInspectionFixtureDeferredEventKind Kind,
    int OwnerAttempt,
    string CaptureCheckpoint,
    string ReleaseCheckpoint);
#endif
