# Onboarding architecture

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-03  
**Reviewed implementation baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`  
**Current branch:** `feature/model-inspection`

[← Application feature architecture](../README.md)

## Purpose

The Onboarding feature owns the multi-stage setup journey. It keeps one stage page visible inside `StageFrame` while the persistent `OnboardingStageIndicator` remains outside the frame and synchronized with the active stage.

This feature exists so individual pages can focus on their own workflow without also managing the complete application journey.

## Responsibility boundary

### This feature owns

- the five-stage onboarding enum;
- the `StageFrame` that hosts one stage page at a time;
- the current onboarding stage;
- synchronization of the persistent stage indicator;
- event subscriptions used to receive stage-completion intent;
- navigation from Model Import to Model Inspection;
- preservation of current stage when navigation fails.

### This feature does not own

- model selection or quick scanning;
- Model Inspection presentation internals;
- runtime model inspection;
- hardware-fit calculation;
- model configuration;
- chat behavior.

## Current composition

```text
OnboardingShellPage
└── Grid
    ├── StageFrame
    │   └── one current onboarding Page
    │
    └── OnboardingStageIndicator
        └── remains visible while StageFrame changes
```

The indicator is deliberately outside `StageFrame`, so navigation replaces only the current stage page rather than the complete onboarding shell.

## Five-stage model

`OnboardingStage` defines:

| Numeric stage | Enum value | User-facing stage | Current implementation |
|---:|---|---|---|
| 1 | `ImportModel` | Choose model | Implemented for local GGUF quick scan |
| 2 | `InspectModel` | Inspect model | Navigation and initial UI architecture implemented; runtime inspection not implemented |
| 3 | `CheckHardwareFit` | Check hardware fit | Not implemented |
| 4 | `ConfigureModel` | Configure model | Not implemented |
| 5 | `ReadyToChat` | Ready to chat | Not implemented |

Source:

- [`OnboardingStage.cs`](./OnboardingStage.cs)

## Main files

### `OnboardingShellPage.xaml`

Defines two permanent layout areas:

```text
Row 0: StageFrame uses remaining space
Row 1: StageIndicator sizes to its content
```

The page initially displays `ModelImportPage` inside `StageFrame`.

[Open file](./OnboardingShellPage.xaml)

### `OnboardingShellPage.xaml.cs`

Coordinates stage state and navigation.

Key responsibilities:

```text
constructor
    → InitializeComponent
    → CurrentStage = ImportModel
    → synchronize StageIndicator
    → navigate to initial ModelImportPage
    → subscribe to its inspection request

AttachModelImportPage(page)
    → reject null
    → avoid duplicate subscription
    → detach previous page
    → subscribe to ModelInspectionRequested

NavigateToModelInspection(path)
    → validate path
    → StageFrame.Navigate(ModelInspectionPage, path)
    → detach old import-page subscription
    → CurrentStage = InspectModel
    → StageIndicator.CurrentStage = InspectModel
```

[Open file](./OnboardingShellPage.xaml.cs)

### `Controls/`

Contains the persistent stage indicator.

- [Onboarding controls architecture](./Controls/README.md)

## Implemented navigation flow

```text
ModelImportPage
    → raises ModelInspectionRequested(ModelPath)

OnboardingShellPage
    → receives the event
    → calls NavigateToModelInspection(ModelPath)

StageFrame
    → creates ModelInspectionPage
    → supplies ModelPath as NavigationEventArgs.Parameter

OnboardingShellPage
    → records InspectModel
    → updates StageIndicator
```

## Navigation ownership rule

Stage pages report intent; the shell performs navigation.

```text
ModelImportPage
    ✓ knows that the user wants to continue
    ✗ does not know which Frame owns the journey

OnboardingShellPage
    ✓ owns StageFrame
    ✓ owns the stage enum
    ✓ owns the persistent indicator
```

This avoids brittle code that searches upward through the visual tree or depends on a page's current parent.

## Synchronization invariant

The following three values must always refer to the same conceptual stage:

```text
Page displayed in StageFrame
        =
OnboardingShellPage.CurrentStage
        =
StageIndicator.CurrentStage
```

The displayed page type is not literally equal to the enum. The invariant means that all three parts of the UI must represent the same user-journey stage.

## Event-subscription lifecycle

The shell stores the currently observed `ModelImportPage` so it can remove the event handler later.

```text
Attach new page
    → if same instance, do nothing
    → detach old page
    → store new page
    → subscribe

Successful navigation away
    → unsubscribe
    → release old page reference
```

This prevents duplicate navigation requests and avoids retaining an inactive page through an event subscription.

## Failure behavior

### Invalid path

`NavigateToModelInspection` rejects null, empty, or whitespace paths before calling `Frame.Navigate`.

### Initial navigation failure

If the shell cannot display `ModelImportPage`, it throws a clear `InvalidOperationException` rather than continuing with a shell whose stage content is missing.

### Unexpected initial page type

After navigation, the shell verifies that `StageFrame.Content` is the expected `ModelImportPage` before subscribing.

### Model Inspection navigation returns false

If `StageFrame.Navigate` returns false:

```text
CurrentStage remains unchanged
StageIndicator remains unchanged
old event subscription remains attached
```

The stage is advanced only after successful navigation.

## Tests and evidence

Navigation tests:

- [`OnboardingModelInspectionNavigationTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs)

They verify:

- an attached Model Import request navigates the real `StageFrame`;
- the destination is `ModelInspectionPage`;
- `CurrentStage` becomes `InspectModel`;
- the persistent indicator becomes `InspectModel`;
- the original selected path reaches the destination unchanged.

Indicator tests:

- [`OnboardingStageIndicatorTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/OnboardingStageIndicatorTests.cs)

## Implemented now

- shell page with persistent stage indicator;
- explicit five-stage enum;
- initial navigation to Model Import;
- event-driven Model Import-to-Inspection navigation;
- path validation;
- current-stage and indicator synchronization;
- event-subscription cleanup;
- focused navigation and indicator tests.

## Not implemented and non-claims

- navigation to Hardware Fit;
- navigation to Configure Model;
- navigation to Ready to Chat;
- back-navigation policy;
- restart or session-restoration behavior;
- preserving stage state across application restarts;
- route guards for stages three through five;
- global navigation service or navigation history abstraction.

## Known limitations and change hazards

- only the Model Import-to-Inspection transition is connected;
- the shell currently handles a page-specific event directly, which is proportionate now but may need a more general navigation contract when more transitions exist;
- adding a stage requires coordinated enum, indicator XAML/code, shell navigation, tests, and documentation changes;
- page navigation parameters should remain small project-owned contracts rather than passing page or ViewModel instances;
- `CurrentStage` must never be updated before `Frame.Navigate` succeeds.

## Child documentation

- [Onboarding controls](./Controls/README.md)

## Related feature documentation

- [Model Import architecture](../ModelImport/README.md)
- [Model Inspection architecture](../ModelInspection/README.md)
