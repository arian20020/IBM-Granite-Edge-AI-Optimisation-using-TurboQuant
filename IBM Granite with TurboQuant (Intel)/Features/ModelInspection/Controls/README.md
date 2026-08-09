# Model Inspection controls

**Status:** Reusable four-card UI supports initial, live-progress, and terminal snapshots
**Last reviewed:** 2026-08-09

[Back to Model Inspection architecture](../README.md)

## Purpose

This folder owns the reusable WinUI controls that render Model Inspection
presentation snapshots. Controls do not run inspection or interpret runtime
evidence.

```text
ModelInspectionPage
    -> InspectionModelCard
    -> InspectionContentCard
    -> InspectionOutcomeCard
    -> InspectionActionCard
```

Each control exposes one typed `Presentation` dependency property, installs a
safe non-null default during construction, refreshes compiled bindings when the
root snapshot changes, and derives visual state from semantic presentation
values.

## `InspectionModelCard`

Renders the selected filename and safe quick-scan facts in compact mode, or a
detailed inspected-model view when supplied. It owns model-card layout, badge
state, icon/brush selection, and card-specific accessibility labels. It never
receives or displays the full model path.

## `InspectionContentCard`

Renders one of two actual structures:

```text
ProgressTemplate
    -> progress summary
    -> fixed five-stage tracker

FindingsTemplate
    -> findings/warnings/errors
    -> optional supporting text
    -> optional diagnostic code/report/action
```

`InspectionContentTemplateSelector` selects the progress template for hidden or
progress states and the findings template for warning, conversion, incomplete,
unsupported, invalid, cancelled, and operational-failure states. It also
handles the normal WinUI initialization shapes where a presentation arrives
directly, through a control/presenter, or after an initial null bootstrap.

### Progress ring semantics

An active row uses `InspectionContentItemPresentation.StageFraction`:

- `null` means an indeterminate ring;
- a real fraction in `0..1` means a determinate ring with value `0..100`;
- inactive rows show their explicit passed/warning/error/information marker.

`IsProgressIndeterminate(double?)` and `GetProgressPercent(double?)` implement
that narrow conversion without inventing progress. The presentation factory
ensures at most one row is active.

## `InspectionOutcomeCard`

Renders the mutually exclusive semantic outcome using an explicit kind, tone,
symbol, title, message, and automation name. Hidden is a real collapsed state;
visual tone alone is never the only communication of status.

## `InspectionActionCard`

Renders either the inspecting action layout or a terminal result/recovery
layout. Buttons bind to presentation commands and command-derived enabled
state. The page reapplies snapshots on `CanExecuteChanged`, so Cancel disables
immediately and Retry/Choose another reflect the active ViewModel lifecycle.

The current action policy is:

| State | Actions |
|---|---|
| inspecting | Cancel |
| completed | Choose another model |
| cancelled or operational failure | Retry; Choose another model |

No Hardware Fit or conversion execution action is connected in this slice.

## Theme and accessibility

Controls use semantic theme resources with Light, Dark, and High Contrast
variants. They expose accessible names/status text and use non-color icons and
labels. Progress changes use one polite content-card live region; terminal
changes use one authoritative assertive outcome-card live region. Both controls
create automation peers, raise `LiveRegionChanged` only for changed semantic
announcements, and reset their dedupe state when hidden so a later equal result
is announced as a new journey. Manual Windows acceptance is still required for
keyboard navigation, Narrator behavior, high text scaling, responsive layout,
and High Contrast rendering.

## Tests

Focused packaged tests include:

- `InspectionContentTemplateSelectorTests`
- `InspectionVisualStateGuardTests`
- `InspectionContentCardPresentationTests`
- `InitialInspectionProgressPresentationTests`
- `InspectionProgressPresentationFactoryTests`
- `ModelInspectionPresentationFactoryTests`
- `ModelInspectionPageNavigationTests`

They protect bootstrap selection, hidden-state isolation, fail-fast visual
states, nullable fraction conversion, fixed stage semantics, terminal mappings,
commands, automation peer/event counts, hidden-state reannouncement, and page
snapshot replacement.

## Ownership boundary and non-claims

Controls do not open models, launch processes, reference LLamaSharp or worker
protocol types, classify evidence, or own navigation. Their visual states do
not by themselves prove a live route. The connected route is limited to local
GGUF Windows x64 CPU `VocabOnly` inspection; OpenVINO, TurboQuant, Vulkan/GPU,
context creation, inference, benchmarking, conversion, Hardware Fit, and chat
remain downstream.

## Related documentation

- [Presentation models](../Models/README.md)
- [Presentation construction](../Presentation/README.md)
- [Model Inspection ViewModel](../ViewModels/README.md)
