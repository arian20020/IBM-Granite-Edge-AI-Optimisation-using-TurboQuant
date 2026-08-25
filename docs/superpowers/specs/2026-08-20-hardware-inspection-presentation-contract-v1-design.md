# Hardware Inspection Presentation Contract v1 Design

**Status:** Approved for implementation by the user on 20 August 2026.

## Goal

Translate the approved Hardware Inspection Visual Contract v1 into immutable, UI-framework-neutral state and copy objects. XAML controls and the page consume this contract; they do not reinterpret service outcomes or invent copy.

## Observable-state model

The factory produces exactly the approved 15 fixtures:

- invalid or missing handoff;
- seven active states, one per canonical stage;
- stopping;
- clean completed;
- completed with warnings;
- failed because critical evidence is missing;
- failed because of a transient operation problem;
- failed because application repair is required;
- cancelled.

`Stopping` remains non-terminal. The three failure presentations remain classifications beneath the single `Failed` service outcome.

## Types

- `HardwareInspectionStage`: the seven canonical ordered stages.
- `HardwareInspectionStageRowState`: `Waiting`, `Active`, `Complete`.
- `HardwareInspectionPresentationKind`: the nine structural screen kinds; `Active` is parameterised by stage.
- `HardwareInspectionFailureClass`: critical evidence, transient operation, application repair.
- `HardwareInspectionActionKind`: the exact approved action vocabulary.
- `HardwareInspectionAction`: label, kind, visibility, enabled state, optional accessible help.
- `HardwareInspectionStageRow`: stage, state, title, sentence, accessible name.
- `HardwareInspectionPresentationState`: immutable page copy, stage rows, actions, details/report flags, review counts, announcement, and default-focus key.
- `HardwareInspectionPresentationFactory`: the only creator of approved state combinations.

## Factory inputs

The factory accepts only bounded semantic inputs:

- active stage;
- terminal `HardwareInspectionOutcome`;
- failure classification and explicit critical-failure retryability;
- whether a usable Hardware handoff exists;
- whether the Block 3 route is registered.

It accepts no raw provider output, paths, native messages, model metadata, or compatibility result.

## Invariants

- Initial active is stage 1 with 0/7 complete.
- Active states contain all seven ordered rows with exactly one `Active`; preceding rows are `Complete`, later rows are `Waiting`.
- Progress is a completed count, never a fabricated percentage.
- Invalid, active, and stopping expose no details or report.
- Stopping has only a disabled `Stopping...` action.
- Completed outcomes expose details and a report; warning counts are derived as one review item and one resolved informational note for the canonical warning fixture.
- Continue is visible only on completed outcomes and enabled only when both a usable handoff and registered Block 3 route exist. Its disabled help is exact.
- Failed and cancelled states never expose Continue or a report.
- Repair-required failure has no retry.
- Retry creates a new run later; this contract only declares action availability.
- All user-facing text is taken verbatim from V0. No worker may substitute generic copy.

## Non-goals

No WinUI dependency, XAML, ViewModel lifecycle, disclosure persistence, navigation, hardware provider, model interpretation, Block 3 calculation, or fixture-gallery registration is included. Those remain downstream slices.
