# Onboarding controls

**Status:** Five-stage indicator and live Model Inspection footer status implemented
**Last reviewed:** 2026-08-10
**Reviewed implementation baseline:** Task 12 candidate based on `5f90a5d9299363214f11454f548ff8571d98b1a5`

[← Onboarding architecture](../README.md)

## Purpose

This folder contains the persistent visual control that shows progress through the five-stage setup journey.

The control is separated from `OnboardingShellPage` because the shell owns navigation, while the indicator owns only visual and accessible representation of a supplied `OnboardingStage` and `InspectionFooterStatus`.

## Responsibility boundary

### This folder owns

- the five visible step boxes and labels;
- connector-line presentation;
- completed, active, and future step appearances;
- in-progress, complete, not-complete, and interrupted Model Inspection footer appearances;
- the current-step eyebrow text;
- validation of values assigned to the indicator;
- accessible stage descriptions and live-region notifications;
- local indicator brushes and styles.

### This folder does not own

- `StageFrame` navigation;
- deciding when a stage is complete;
- model import, inspection, hardware fit, configuration, or chat logic;
- persistence of onboarding state;
- route authorization.

## Local data flow

```text
OnboardingShellPage.CurrentStage
        │
        ▼
StageIndicator.CurrentStage dependency property
        │
        ▼
OnCurrentStageChanged(...)
        │
        ├── validate stage 1–5
        ├── restore previous value if invalid
        ├── ApplyStage(newStage)
        └── raise accessibility live-region event
                │
                ▼
step boxes, numbers/checkmarks, labels,
connectors, eyebrow, AutomationProperties.Name
```

While stage 2 is active, the shell also forwards the owned inspection page's
`FooterStatusChanged` value to `StageIndicator.InspectionStatus`. The indicator
updates the Inspect model box, eyebrow, and accessible status without advancing
the onboarding stage. Retired inspection pages are detached and cannot update
the persistent footer.

## File inventory

### `OnboardingStageIndicator.xaml`

Defines the complete persistent indicator layout.

The control contains:

- one top divider;
- an eyebrow such as `MODEL SETUP · STEP 2 OF 5`;
- five numbered stage boxes;
- four base connector lines;
- four blue connector overlays using `ScaleTransform`;
- five stage labels;
- local brushes and shared styles.

Ten equal columns keep each two-column stage span centered and allow connector spans to overlap adjacent stage centers.

The control is non-interactive:

```text
IsHitTestVisible = False
IsTabStop = False
```

It communicates progress rather than acting as a navigation control.

[Open file](./OnboardingStageIndicator.xaml)

### `OnboardingStageIndicator.xaml.cs`

Defines the `CurrentStage` and `InspectionStatus` dependency properties and applies the correct visual state to all five steps.

Main responsibilities:

```text
OnCurrentStageChanged
    → validate new enum value
    → prevent restoration recursion
    → ignore equivalent state
    → apply stage
    → notify accessibility clients

ApplyStage
    → update five step states
    → update connector fills
    → update eyebrow
    → update accessible description
```

[Open file](./OnboardingStageIndicator.xaml.cs)

## Dependency-property contract

```csharp
public OnboardingStage CurrentStage
public InspectionFooterStatus InspectionStatus
```

Default value:

```text
OnboardingStage.ImportModel
```

The dependency properties allow the shell to assign a stage and the current
inspection footer state through the standard WinUI property system.

## Stage validation and restoration

Only numeric values one through five are accepted.

When an invalid enum value is assigned:

```text
new value is rejected
    ↓
previous valid CurrentStage is restored
    ↓
ArgumentOutOfRangeException is thrown
```

`_isRestoringCurrentStage` prevents the corrective `SetValue` call from recursively entering the same validation route.

This behavior ensures the dependency-property store does not remain in an invalid state after a rejected assignment.

`InspectionStatus` applies the same restore-and-throw rule for undefined enum
values. Its approved values are `InProgress`, `Complete`, `NotComplete`, and
`Interrupted`.

## Step-state rules

For each step:

```text
step number < current step
→ Completed

step number == current step
→ Active

step number > current step
→ Future
```

### Completed

- blue surface and border;
- number replaced with a checkmark;
- white checkmark;
- blue semibold label.

### Active

- blue surface and border;
- current number remains visible;
- white number;
- primary semibold label.

### Future

- light inactive surface;
- inactive border;
- muted number;
- secondary normal-weight label.

## Connector rules

A connector fills when its destination stage has been reached:

```text
Choose → Inspect       fills at stage 2 or later
Inspect → Check Fit    fills at stage 3 or later
Check Fit → Configure  fills at stage 4 or later
Configure → Ready      fills at stage 5
```

The current code sets each overlay `ScaleX` directly to `0` or `1`.

Although the XAML structure is prepared for scale-based animation, animated progression is not implemented yet.

## Accessibility behavior

The indicator exposes:

- `AutomationProperties.HelpText` describing the five-stage setup process;
- `AutomationProperties.LiveSetting="Polite"`;
- an updated accessible name such as `Model setup progress. Step 2 of 5: Inspect model.`;
- an inspection suffix such as `Inspection complete.` while stage 2 is active;
- `LiveRegionChanged` after a valid stage transition.

The stage boxes are visual representations; the complete control provides the readable progress summary.

## Resource ownership

Local XAML resources define:

```text
surface
active blue
inactive surface and border
connector
primary, secondary, and muted text
```

Code-behind resolves these resources by stable string keys and throws a clear exception if a required resource is missing or is not a `SolidColorBrush`.

This creates a contract between XAML resource names and C# state application.

## Tests and evidence

Key tests:

- [`OnboardingStageIndicatorTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/OnboardingStageIndicatorTests.cs)

The tests cover:

- default stage;
- each valid current-stage presentation;
- completed, active, and future step values;
- connector fill state;
- eyebrow text;
- resource use;
- invalid stage rejection and restoration;
- all four inspection-footer values and invalid-value restoration;
- accessibility names and live-region behavior.

Shell synchronization is covered by:

- [`OnboardingModelInspectionNavigationTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs)

## Implemented now

- five-stage visual indicator;
- dependency-property input;
- completed, active, and future appearances;
- connector progression;
- live Model Inspection footer projection without stage advancement;
- invalid-value restoration;
- accessible stage summary;
- polite live-region notification;
- focused WinUI tests.

## Not implemented and non-claims

- the indicator does not navigate when clicked;
- connector animation is not implemented;
- stage persistence is not implemented;
- dark-theme resources are not currently defined in this control;
- the indicator does not decide whether a workflow stage passed.

The ordinary packaged tests cover the supplied semantic state and resource
projection. They do not prove an actual Windows High Contrast session, actual
200% text scale, Narrator output, or hosted exact-head execution.

## Known limitations and change hazards

- adding or reordering stages requires coordinated changes to `OnboardingStage`, XAML elements, connector transforms, `ApplyStage`, display names, shell transitions, tests, and documentation;
- C# resource-key strings must remain synchronized with XAML resource names;
- direct visual-property assignment is acceptable for the current focused control but may become harder to maintain if the state design grows significantly;
- accessibility announcements should occur only after real stage changes, not repeated equivalent assignments;
- the control is currently designed for exactly five stages.

## Related documentation

- [Onboarding architecture](../README.md)
- [Application feature architecture](../../README.md)
- [Model Inspection architecture](../../ModelInspection/README.md)
