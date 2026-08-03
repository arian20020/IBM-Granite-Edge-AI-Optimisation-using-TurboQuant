# Model Inspection controls

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-03  
**Reviewed application baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`

[← Model Inspection architecture](../README.md) · [Presentation models](../Models/README.md)

## Purpose

This folder contains the reusable visual components that make up `ModelInspectionPage`.

Each control owns one stable visual responsibility and accepts a presentation object. The controls render state; they do not execute model inspection, classify runtime evidence, or decide onboarding navigation.

## Folder architecture

```text
ModelInspectionPage
    │
    ├── InspectionModelCard
    │       → selected model identity and inspected metadata
    │
    ├── InspectionContentCard
    │       → progress tracker or findings/diagnostics
    │       └── InspectionContentTemplateSelector
    │
    ├── InspectionOutcomeCard
    │       → final high-level outcome banner
    │
    └── InspectionActionCard
            → actions available in the current state
```

## Shared presentation pattern

The controls use a consistent boundary:

```text
parent assigns Presentation
        ↓
Presentation dependency property changes
        ↓
OnPresentationChanged callback
        ↓
control applies visibility, bindings, visual state, or template
        ↓
XAML renders the supplied data
```

The presentation contracts are documented in:

- [Model Inspection presentation models](../Models/README.md)

## File inventory and control contracts

# `InspectionModelCard`

## Purpose

Answers:

> Which model is being inspected, and what verified metadata is known about it?

## Files

- [`InspectionModelCard.xaml`](./InspectionModelCard.xaml)
- [`InspectionModelCard.xaml.cs`](./InspectionModelCard.xaml.cs)

## Presentation input

```csharp
InspectionModelCardPresentation Presentation
```

The code-behind exposes forwarding properties such as:

```text
ModelName
CompactSummary
FormatShortName
Publisher
FormatName
Quantisation
ParameterCount
ModelType
DeclaredContext
FileSize
InspectionChecks
```

Those forwarding properties keep compiled `x:Bind` expressions concise while the parent replaces one root presentation object.

## Structural modes

```text
CompactState
    → concise selected-model card and status badge

DetailedState
    → model overview and expandable inspection checks
```

`InspectionModelCardMode` selects the state. The code-behind calls `Bindings.Update()` and then `VisualStateManager.GoToState(...)`.

## Local responsibilities

- format icon and badge text;
- compact versus detailed layout;
- badge-state text, foreground, background, and border;
- inspection-check status icons and colors;
- expanded/collapsed inspection details;
- readable automation names for badge and expander state.

## Current limitation

The initial page supplies only path-derived filename and format data. The detailed layout exists, but no real inspection service currently supplies verified inspection checks.

---

# `InspectionContentCard`

## Purpose

Answers:

> What is happening now, or what did inspection find?

## Files

- [`InspectionContentCard.xaml`](./InspectionContentCard.xaml)
- [`InspectionContentCard.xaml.cs`](./InspectionContentCard.xaml.cs)
- [`InspectionContentTemplateSelector.cs`](./InspectionContentTemplateSelector.cs)

## Presentation input

```csharp
InspectionContentCardPresentation Presentation
```

The control also exposes an internally calculated:

```csharp
Visibility CardVisibility
```

`Hidden` mode collapses the complete card so it does not reserve layout space.

## Two actual XAML structures

The feature has many semantic content states but only two genuinely different layouts:

```text
ProgressTemplate
    → section heading
    → progress summary
    → five-stage tracker

FindingsTemplate
    → state-specific findings
    → optional supporting text
    → optional blocking statement
    → optional diagnostic code
    → optional expanded report
    → optional technical-details action
```

This prevents separate near-duplicate XAML controls for warnings, invalid models, unsupported models, incomplete packages, cancellation, and operational failure.

## Reusable row templates

`InspectionContentCard.xaml` defines:

```text
ProgressStageTemplate
    → active, passed, or waiting stage row

FindingRowTemplate
    → finding/warning/error row with status badge

ReportRowTemplate
    → compact row inside an expanded report
```

## Status helpers

The code-behind maps `InspectionContentStatus` to:

- background brush;
- foreground brush;
- WinUI symbol;
- passed/active/waiting visibility;
- connector visibility;
- disclosure label.

## Template selector

`InspectionContentTemplateSelector` maps:

```text
Hidden or Progress
    → ProgressTemplate

Warnings
ConversionRequired
IncompletePackage
Unsupported
Invalid
Cancelled
OperationalFailure
    → FindingsTemplate
```

### WinUI bootstrap handling

During `ContentControl` initialization, WinUI can request a template before the compiled `Content` binding supplies the final presentation. Depending on the route, the selector may receive:

- the presentation directly;
- `null`;
- a `ContentControl` containing the presentation;
- a `ContentPresenter` containing the presentation;
- a container whose content contains the presentation.

The selector resolves all of those forms. When no presentation is available yet, it returns `ProgressTemplate` as a deterministic bootstrap template because the surrounding card is still hidden.

This behavior fixes two earlier failures:

```text
presentation type name displayed as plain text
ArgumentException during normal ContentControl initialization
```

---

# `InspectionOutcomeCard`

## Purpose

Answers:

> What is the final high-level result of inspection?

## Files

- [`InspectionOutcomeCard.xaml`](./InspectionOutcomeCard.xaml)
- [`InspectionOutcomeCard.xaml.cs`](./InspectionOutcomeCard.xaml.cs)

## Presentation input

```csharp
InspectionOutcomePresentation Presentation
```

The control calculates `CardVisibility`. `Hidden` removes the entire banner from layout while inspection is running.

## Tone visual states

```text
SuccessTone
WarningTone
InformationTone
ErrorTone
NeutralTone
```

These five visual tones support the defined semantic outcomes without creating one control per result.

Conceptual mapping:

```text
Ready                         → Success
Ready with warnings           → Warning
Incomplete package            → Warning
Conversion required           → Information
Unsupported                    → Error
Invalid                        → Error
Operational failure           → Error
Cancelled                      → Neutral
```

The presentation supplies the exact icon, title, message, and accessible name. The control supplies tone-specific geometry and brushes.

## Current limitation

No classifier currently produces a real outcome, so the page initializes this card as Hidden.

---

# `InspectionActionCard`

## Purpose

Answers:

> What can the user do in the current inspection state?

## Files

- [`InspectionActionCard.xaml`](./InspectionActionCard.xaml)
- [`InspectionActionCard.xaml.cs`](./InspectionActionCard.xaml.cs)

## Presentation input

```csharp
InspectionActionCardPresentation Presentation
```

The control calculates `CardVisibility` and switches between two structures:

```text
InspectingState
    → centered Cancel action
    → safety/help message

ResultState
    → result-specific heading and message
    → up to two secondary actions
    → one primary action
```

`WideActionLayout` changes the result-button panel from vertical to horizontal on wider windows.

## Visual-state contract

The visual-state groups live inside `LayoutRoot`, in the same visual-tree scope as `InspectingView`, `ResultView`, and the result buttons they modify.

Code-behind checks the return value of:

```csharp
VisualStateManager.GoToState(this, stateName, false)
```

If the expected state is absent, it throws a clear `InvalidOperationException`. This prevents the control from silently leaving both views collapsed when XAML and C# names drift apart.

After applying the state, the control calls `Bindings.Update()` so action text, visibility, enabled state, commands, and automation names reflect the new presentation.

## Current limitation

The initial Cancel action is visible but disabled because no real asynchronous inspection task or cancellation token exists yet.

---

## Theme architecture

Each inspection XAML control defines local theme dictionaries with:

```text
Dark
Light
HighContrast
```

`ModelInspectionPage` currently requests the Light theme to match the approved prototype.

The controls use `ThemeResource` for card surfaces, borders, text, badges, and outcome tones. High Contrast dictionaries use system color resources.

### Known theming debt

Some check/status brushes in `InspectionModelCard.xaml.cs` and `InspectionContentCard.xaml.cs` are still created with fixed ARGB values. Those brushes do not automatically react to a theme change.

Future cleanup should move those status colors into shared or local theme resources while preserving the current status-to-semantic mapping.

## Accessibility architecture

The controls expose:

- card-level automation names from presentations;
- row-level complete descriptions;
- text status labels so color is not the only signal;
- raw accessibility view for decorative status shapes;
- polite live behavior on the outcome card;
- readable expander action names;
- minimum action sizes and keyboard order.

Future manual acceptance must still verify keyboard navigation, screen-reader announcements, high text scaling, and High Contrast behavior.

## Tests and evidence

### Current focused tests

Template selection and bootstrap routes:

- [`InspectionContentTemplateSelectorTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/InspectionContentTemplateSelectorTests.cs)

Page navigation and initial route:

- [`ModelInspectionPageNavigationTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs)
- [`OnboardingModelInspectionNavigationTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs)

### Missing focused coverage

Dedicated tests still need to prove:

- every model-card badge and display mode;
- every content-card semantic state;
- every outcome tone;
- every action-card result arrangement;
- theme-resource behavior;
- responsive layout and text scaling;
- accessibility output for final states.

## Implemented now

- four reusable cards;
- presentation dependency properties;
- safe hidden/default presentations;
- compact/detailed model modes;
- progress/findings content templates;
- template selector bootstrap handling;
- five outcome tones;
- inspecting/result action layouts;
- Light, Dark, and High Contrast resources;
- compiled-binding refresh and visual-state selection;
- focused selector and navigation tests.

## Not implemented and non-claims

- controls do not execute inspection;
- controls do not call llama.cpp or OpenVINO;
- action commands are not connected to a runtime workflow;
- no technical-report navigation or dialog is implemented here;
- no final result data is produced by a classifier;
- the presence of a visual state does not prove its runtime route is connected.

## Change checklist

When changing a control, review:

1. Its XAML and code-behind contract.
2. Its presentation class and enum values.
3. `x:Bind` paths and `Bindings.Update` timing.
4. Visual-state names and scope.
5. Light, Dark, and High Contrast resources.
6. Accessibility names and text status cues.
7. Dedicated tests and page-level tests.
8. This README, the Models README, and the parent feature README.

## Related documentation

- [Model Inspection architecture](../README.md)
- [Presentation models](../Models/README.md)
- [Onboarding architecture](../../Onboarding/README.md)
