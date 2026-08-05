# Onboarding architecture

**Status:** Five-stage shell and immutable Model Import-to-Inspection navigation implemented  
**Last reviewed:** 2026-08-05  
**Current branch:** `feature/model-inspection-runtime-integration`

[← Application feature architecture](../README.md)

## Purpose

Onboarding owns the multi-stage setup journey. It keeps one stage page visible inside `StageFrame` while the persistent `OnboardingStageIndicator` remains outside the frame and synchronized with the active stage.

Individual stage pages report intent and carry small project-owned data contracts. They do not locate or manipulate the shell's frame themselves.

## Responsibility boundary

### This feature owns

- the five-stage onboarding enum;
- `StageFrame` and current-stage state;
- synchronization of the persistent stage indicator;
- stage-page event subscription and cleanup;
- navigation from Model Import to Model Inspection;
- forwarding the exact immutable `ModelInspectionRequest`;
- preserving stage state when navigation fails.

### This feature does not own

- model selection or quick scanning;
- request construction or file identity capture;
- Model Inspection presentation or runtime work;
- worker protocol, classification, hardware fit, configuration, or chat.

## Five-stage model

| Stage | Enum | Current implementation |
|---:|---|---|
| 1 | `ImportModel` | Local GGUF selection and immutable inspection request implemented |
| 2 | `InspectModel` | Request navigation and initial UI implemented; runtime execution deferred |
| 3 | `CheckHardwareFit` | Not implemented |
| 4 | `ConfigureModel` | Not implemented |
| 5 | `ReadyToChat` | Not implemented |

## Composition

```text
OnboardingShellPage
└── Grid
    ├── StageFrame
    │   └── one active onboarding page
    └── OnboardingStageIndicator
        └── persistent five-stage progress
```

The indicator is outside `StageFrame`, so stage navigation replaces only the active page.

## Immutable navigation flow

```text
ModelImportPage
    → raises ModelInspectionRequested(Request)

OnboardingShellPage
    → receives the event
    → NavigateToModelInspection(Request)

StageFrame
    → creates ModelInspectionPage
    → supplies the same Request as NavigationEventArgs.Parameter

OnboardingShellPage
    → records InspectModel only after navigation succeeds
    → updates StageIndicator
    → detaches the inactive ModelImportPage subscription
```

The shell does not reconstruct the request, re-read the file, or reduce the handoff back to a path. Object identity is preserved from the event through the destination page.

## Navigation ownership rule

```text
ModelImportPage
    ✓ reports intent and validated request
    ✗ does not own StageFrame

OnboardingShellPage
    ✓ owns navigation and stage synchronization
    ✗ does not inspect or classify the model

ModelInspectionPage
    ✓ receives the request
    ✗ does not navigate itself
```

This avoids visual-tree searches and keeps the user journey independent from page implementation details.

## Synchronization invariant

After successful navigation, these must describe the same conceptual stage:

```text
Page displayed by StageFrame
        =
OnboardingShellPage.CurrentStage
        =
StageIndicator.CurrentStage
```

`CurrentStage` is updated only after `Frame.Navigate` returns `true`.

## Event-subscription lifecycle

```text
Attach page
    → reject null
    → ignore duplicate attachment
    → detach previous page
    → subscribe to ModelInspectionRequested

Successful navigation away
    → unsubscribe
    → release the page reference
```

This prevents duplicate requests and avoids retaining an inactive page through an event handler.

## Controlled failures

- A null inspection request is rejected before navigation and the stage remains `ImportModel`.
- Failure to display the initial Model Import page raises a clear `InvalidOperationException`.
- An unexpected initial page type is rejected rather than silently continuing.
- When `Frame.Navigate` returns `false`, current stage, indicator, and event subscription remain unchanged.

## Tests

- [`OnboardingModelInspectionNavigationTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs)
- [`OnboardingStageIndicatorTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/OnboardingStageIndicatorTests.cs)

The navigation tests verify request forwarding, destination type, same-object preservation, stage synchronization, and null-request rejection.

## Implemented now

- persistent onboarding shell;
- explicit five-stage enum;
- initial Model Import navigation;
- event-driven Model Import-to-Inspection transition;
- immutable request forwarding;
- current-stage and indicator synchronization;
- event-subscription cleanup;
- focused UI-thread tests.

## Non-claims and deferred work

- Hardware Fit, Configure Model, or Ready to Chat navigation;
- back-navigation and restart/session restoration;
- global navigation service or history abstraction;
- production model inspection execution;
- worker process, classification, service, ViewModel, or functional Cancel action.

## Related documentation

- [Onboarding controls](./Controls/README.md)
- [Model Import architecture](../ModelImport/README.md)
- [Model Inspection architecture](../ModelInspection/README.md)
