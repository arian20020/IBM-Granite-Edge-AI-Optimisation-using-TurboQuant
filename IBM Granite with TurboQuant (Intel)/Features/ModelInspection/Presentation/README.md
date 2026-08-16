# Model Inspection presentation construction

**Status:** Thirteen-state mapping, stable rendering, motion, and privacy-safe display projection implemented
**Last reviewed:** 2026-08-16

[Back to Model Inspection architecture](../README.md)

## Purpose

This folder converts application-owned inspection state into immutable page
presentations and changed-region deltas for four stable WinUI card controls.
Presentation construction remains above the service and classifier so runtime
code never depends on WinUI.

```text
ViewModel snapshot + commands
    -> presentation factory and exact Figma-state identity
    -> render coordinator (coalesce, reject stale keys, diff regions)
    -> stable model/content/outcome/action controls
    -> stable five-row progress owner
```

## Files

### `InitialInspectionProgressPresentationFactory.cs`

Creates the five-row state used before the first live progress event, with all
five stages waiting and `0 of 5 checks complete`. Its standalone/default call
keeps startup hidden; `ModelInspectionPresentationFactory` supplies the visible
`Starting secure inspection…` startup state for a live attempt until the
genuine Stage 1 event.

### `InspectionProgressPresentationFactory.cs`

Maps one truthful `ModelInspectionProgress` update into the fixed five rows.
Earlier stages are passed, the current stage uses its explicit application
status, and later stages remain waiting. At most one row is active. A nullable
stage fraction is carried only on the current row; it is not invented when the
worker did not report one.

### `ModelInspectionPagePresentation.cs`

Groups one immutable model/content/outcome/action/footer/announcement
presentation with its render key, exact `ModelInspectionFigmaState`, semantic
region keys, and progress-row update. It is input to delta calculation; it does
not require every control input to be reassigned.

### `ModelInspectionPresentationFactory.cs`

Creates:

- initial inspecting presentation with functional Cancel command state;
- live progress presentation;
- all six completed model-outcome presentations;
- cancelled and operational-failure recovery presentations.

It also projects all display text through `ModelInspectionDisplayTextPolicy`,
assigns exact region identities, maps the 13 approved state names, and keeps
future report/conversion/Hardware Fit actions visible but disabled with the
accessible `Coming later` explanation.

### Stable rendering and motion

- `ModelInspectionRenderCoordinator.cs` coalesces bursts, accepts only the
  newest truthful render key, preserves disclosure state for the same outcome,
  resets it for a new outcome/attempt, and emits changed-region deltas.
- `InspectionProgressRows.cs` owns exactly five observable row instances for
  one attempt and mutates only changed row values.
- `ModelInspectionMotion.cs` defines the exact 160/180/240 ms tokens and
  easing/endpoints; the driver rejects stale visual-operation keys and reduces
  duration to zero when Windows animations are disabled.
- `ModelInspectionRegionKeys.cs` and
  `ModelInspectionPresentationDelta.cs` make reassignment and live-region
  changes explicit and testable.

`ModelInspectionMilestoneSequencer.cs` sits between accepted semantic
snapshots and visual application. In normal motion it preserves each genuinely
reached Active stage for at least 550 ms from presentation, but adds no dwell
when genuine active time already satisfies the minimum. It queues genuine
bursts in order and coalesces Stage 2 fraction-only updates. Cancellation,
operational failure, retry/restart, choose-another, navigation/disposal,
stale-attempt invalidation, and reduced-motion changes flush immediately.

`ModelInspectionMotion` also defines the 1,050 ms linear Precision Orbit used
unchanged by every active stage. A genuine Stage 2 fraction updates restrained
trailing text only; it neither changes the indicator mode nor restarts motion.

## Terminal mapping

| Application state | Visible action policy |
|---|---|
| completed model outcome | choose another model; applicable future actions remain disabled with `Coming later` |
| cancelled | retry or choose another model |
| operational failure | retry or choose another model |

The current page executes no Hardware Fit, conversion, or report action.
`Ready` and `ReadyWithWarnings` eligibility remains in the domain contract for
a later downstream navigation slice.

All model outcomes have explicit semantic kind, tone, badge, content mode, and
accessible text. `Ready` hides the content/finding card because there is no
finding to show. Warning, conversion, incomplete, unsupported, and invalid
states show the relevant application findings.

## Privacy boundary

Presentation uses the request filename and safe quick-scan fields, never the
full model path. Operational failures display only the stable user message and
diagnostic code. Raw worker stderr, request IDs, protocol records, embedded
chat-template text, exception chains, and technical details are not projected
onto the page.

## Fixed five-stage wording

```text
1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm core runtime compatibility
```

These rows describe lightweight Model Inspection. They do not claim Vulkan or
GPU initialization, layer offload, TurboQuant activation, context creation,
full inference, Hardware Fit, or performance/quality benchmarking.

## Tests

- `InitialInspectionProgressPresentationTests`
- `InspectionProgressPresentationFactoryTests`
- `ModelInspectionPresentationFactoryTests`
- `ModelInspectionDisplayTextPolicyTests`
- `ModelInspectionFigmaStatePresentationTests`
- `InspectionProgressRowsTests`
- `ModelInspectionRenderCoordinatorTests`
- `ModelInspectionMotionTests`
- `PresentationTestData`

Historical pre-progress-polish evidence: the ordinary packaged candidate
executed these exact Task 12 class counts:
display policy 46, Figma states 77, progress rows 16, presentation factory 18,
progress factory 13, render coordinator 27, and motion 29. They cover ordering,
real nullable fractions, all outcome/execution mappings, stable identity and
delta behavior, stale-operation rejection, reduced-motion endpoints, action
policy, accessibility text, and privacy-sensitive exclusions. Exact Figma PNG
comparison and controlled OS execution remain open.

### Fixture preview boundary

The Debug x64 fixture gallery drives these production presentation controls
with the exact synthetic `MI-001` through `MI-050` catalogue. Declared Cancel,
Retry, Restart, Choose another, disclosure expand/collapse, and Reset routes
exercise real controls or commands; unavailable actions are omitted, and
future report, conversion, and Hardware Fit actions remain disabled with
`Coming later` and do not dispatch. The gallery can also switch fixtures and
close while retiring the previous synthetic session.

Gallery width, High Contrast, 200% text-scale, and reduced-motion presets are
controlled preview inputs to the existing presentation. They help inspect
layout, resources, reflow, focus, and motion deterministically, but they do not
claim strict comparison with approved Figma PNGs, actual OS High Contrast,
actual OS 200% text scale, or Narrator behavior. Synthetic terminal and failure
presentations do not claim real-worker outcome coverage; only the catalogue's
separate N-001 join covers the bounded real-worker Ready route.

Historical Task 10 evidence: the local Debug/x64 campaign passed all 220
fixture-category executions,
including the 6 interaction and 13 lifetime cases that drive the rendered
controls and audit retirement. The final local hosted-equivalent Release gate
passed 691/691, including all 39 protected page-navigation executions; the
focused real packaged N-001 journey passed 1/1, and Release isolation passed
with zero forbidden fixture/gallery path, token, or metadata hits. Strict Figma
pixels, controlled-OS/manual Narrator evidence, and hosted exact-head closure
remain open.

### Current behavior and pending final evidence

The presentation contract now includes shared vector status glyphs, the
Measured Checklist's 24/16 px page rhythm and five 48 px rows, Balanced Centre
terminal geometry, natural-height warning/failure states, and responsive
one-, two-, and three-action arrangements. The exact Debug catalogue remains
`MI-001` through `MI-050`. Hardware Inspection is not implemented here.

Fresh final-ladder totals and hashes are intentionally deferred until the
serialized evidence run completes. No strict Figma-pixel, real Narrator, or
controlled-OS claim is made without separate controlled evidence.

## Change hazards

Any stage, outcome, status, or action-policy change must be updated together in
the application contracts, presentation factories, controls, tests, and this
documentation. Do not add a downstream action merely because the corresponding
domain outcome enum exists.
