# Model Inspection presentation construction

**Status:** Initial, live-progress, and terminal presentation mapping implemented
**Last reviewed:** 2026-08-09

[Back to Model Inspection architecture](../README.md)

## Purpose

This folder converts application-owned inspection state into complete,
replaceable snapshots for the four WinUI cards. Presentation construction is
kept above the service and classifier so runtime code never depends on WinUI.

```text
application request/progress/result + commands
    -> presentation factory
    -> model card
    -> content card
    -> outcome card
    -> action card
```

## Files

### `InitialInspectionProgressPresentationFactory.cs`

Creates the five-row state used before the first live progress event: stage 1
active, stages 2-5 waiting, and `0 of 5 checks complete`.

### `InspectionProgressPresentationFactory.cs`

Maps one truthful `ModelInspectionProgress` update into the fixed five rows.
Earlier stages are passed, the current stage uses its explicit application
status, and later stages remain waiting. At most one row is active. A nullable
stage fraction is carried only on the current row; it is not invented when the
worker did not report one.

### `ModelInspectionPagePresentation.cs`

Groups one complete model/content/outcome/action snapshot. The page replaces
all four card inputs together when state changes.

### `ModelInspectionPresentationFactory.cs`

Creates:

- initial inspecting presentation with functional Cancel command state;
- live progress presentation;
- all six completed model-outcome presentations;
- cancelled and operational-failure recovery presentations.

## Terminal mapping

| Application state | Visible action policy |
|---|---|
| completed model outcome | choose another model |
| cancelled | retry or choose another model |
| operational failure | retry or choose another model |

The current page deliberately exposes no Hardware Fit or conversion execution
action. `Ready` and `ReadyWithWarnings` eligibility remains in the domain
contract for a later downstream navigation slice.

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
- `PresentationTestData`

The focused tests cover ordering, explicit stage statuses, real nullable
fractions, one-spinner behavior, all outcome/execution mappings, command state,
action policy, accessibility text, and privacy-sensitive exclusions.

## Change hazards

Any stage, outcome, status, or action-policy change must be updated together in
the application contracts, presentation factories, controls, tests, and this
documentation. Do not add a downstream action merely because the corresponding
domain outcome enum exists.
