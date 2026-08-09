# Model Inspection presentation models

**Status:** Complete presentation shapes for initial, live-progress, and terminal UI states
**Last reviewed:** 2026-08-09

[Back to Model Inspection architecture](../README.md)

## Naming clarification

`Models` here means WinUI presentation data, not machine-learning model files
or runtime model objects. Application domain contracts live in `Contracts` and
worker/runtime implementation remains outside this folder.

## Purpose

These types define the complete data supplied to the four Model Inspection
controls:

```text
ModelInspectionViewModel state
    -> Presentation factories
    -> Models (this folder)
    -> Controls
```

The page replaces root presentation snapshots when request, progress, result,
or command state changes.

## Root presentation types

- `InspectionModelCardPresentation` supplies filename, quick-scan display facts,
  badge, compact/detailed mode, and completed-check details.
- `InspectionContentCardPresentation` supplies progress or finding mode, rows,
  supporting text, diagnostics, disclosure, and optional technical action.
- `InspectionOutcomePresentation` supplies semantic result kind, visual tone,
  icon, title, message, and accessible name.
- `InspectionActionCardPresentation` supplies inspecting/result layout and
  fixed Cancel/secondary/primary action slots.

Safe defaults keep every binding non-null:

```text
InspectionModelCardPresentation.Empty
InspectionContentCardPresentation.Hidden
InspectionOutcomePresentation.Hidden
InspectionActionCardPresentation.Hidden
InspectionActionPresentation.Hidden
```

`InspectionContentCardPresentation.Hidden` returns a fresh instance because its
`IsExpanded` property is mutable and notifies bindings.

## Child presentation types

`InspectionContentItemPresentation` represents progress, finding, warning,
error, or report rows. Progress rows include:

```text
StageNumber
Title
Detail
Status / StatusText
IsActive
StageFraction (nullable)
ShowConnector
AutomationName
```

`StageFraction` is real progress only. `null` selects an indeterminate active
ring; determinate values are converted to percentage by the control helper.

`InspectionCheckPresentation` represents compact completed checks without
progress-only state. `InspectionActionPresentation` represents one command
slot with text, command, parameter, enabled/visible state, automation name, and
minimum width.

## Semantic enums

- model card: compact/detailed mode, badge state, check status;
- content card: hidden/progress and seven terminal finding modes, plus explicit
  neutral/waiting/active/passed/warning/error/information row status;
- outcome card: hidden, all six model outcomes, cancelled, and operational
  failure with a separate tone enum;
- action card: hidden, inspecting, or result.

Semantic identity remains separate from color/tone so accessibility and tests
do not infer meaning from styling.

## Ownership boundary

These presentation types may use WinUI `Visibility`, control `Symbol`, and
`ICommand`. They must not cross into runtime adapters, services, classifiers,
or worker projects. They contain no full model path, native handle, worker
record, raw failure payload, or chat-template content.

## Tests

Presentation shape and mutation behavior are covered by:

- `InspectionContentCardPresentationTests`
- `InitialInspectionProgressPresentationTests`
- `InspectionProgressPresentationFactoryTests`
- `ModelInspectionPresentationFactoryTests`
- `InspectionVisualStateGuardTests`

## Scope and non-claims

Enum values describe display states; they do not prove every result is produced
by the current classifier. The live classifier currently produces `Ready` or
`ReadyWithWarnings` from approved GGUF CPU/VocabOnly evidence. No presentation
type proves OpenVINO, TurboQuant, Vulkan/GPU, context creation, inference,
benchmarking, conversion execution, Hardware Fit, or chat execution.

## Related documentation

- [Presentation construction](../Presentation/README.md)
- [Inspection controls](../Controls/README.md)
- [Application contracts](../Contracts/README.md)
