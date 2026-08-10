# Model Inspection controls

**Status:** Four stable card controls and reusable disclosures implement the ordinary 13-state presentation
**Last reviewed:** 2026-08-10

[Back to Model Inspection architecture](../README.md)

## Purpose

This folder owns the reusable WinUI controls that render Model Inspection
presentation regions. Controls do not run inspection or interpret runtime
evidence.

```text
ModelInspectionPage
    -> InspectionModelCard
    -> InspectionContentCard
    -> InspectionOutcomeCard
    -> InspectionActionCard
```

Each control exposes one typed `Presentation` dependency property, installs a
safe non-null default during construction, and derives visual state from
semantic presentation values. The page creates each control once; the render
coordinator reapplies only regions whose semantic key changed. The content
control retains one `InspectionProgressRows` owner and the same five row
instances during an attempt.

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
| completed | Choose another model plus state-specific future actions that are visible, disabled, and expose `Coming later` help |
| cancelled or operational failure | Retry; Choose another model |

No Hardware Fit, conversion, technical-report, or separate technical-details
execution action is connected in this slice.

## Disclosure and motion

`InspectionDisclosure` is the single accessible disclosure primitive used by
the Ready model details and warning/conversion/invalid content details. It
exposes expanded/collapsed state and Enter/Space behavior, keeps the selected
button focused, and lets the page own the requested state. Page interaction
revisions prevent an older 240 ms disclosure completion from overwriting a
newer toggle, outcome, attempt, or navigation state. When Windows animations
are disabled, the same semantic result is applied immediately.

## Theme and accessibility

Controls use semantic theme resources with Light, Dark, and High Contrast
variants. They expose accessible names/status text and use non-color icons and
labels. Progress changes use one polite content-card live region; terminal
changes use one authoritative assertive outcome-card live region. Both controls
create automation peers, raise `LiveRegionChanged` only for changed semantic
announcements, and reset their dedupe state when hidden so a later equal result
is announced as a new journey.

Ordinary packaged tests cover keyboard invocation, focus retention, non-color
status, automation properties/events, 200%-equivalent layout simulation,
High-Contrast resource selection, responsive modes, and reduced-motion policy.
They do not prove an actual Windows 200% setting, actual High Contrast session,
or Narrator output. Those controlled/manual campaigns remain open.

## Tests

Focused packaged tests include:

- `InspectionContentTemplateSelectorTests`
- `InspectionVisualStateGuardTests`
- `InspectionActionCardTests`
- `InspectionContentCardTests`
- `InspectionModelCardTests`
- `InspectionOutcomeCardTests`
- `ModelInspectionDisclosureTests`
- `ModelInspectionPageDisclosureTests`
- `ModelInspectionRenderedStateTests`
- `ModelInspectionAccessibilityTests`
- `InspectionContentCardPresentationTests`
- `InitialInspectionProgressPresentationTests`
- `InspectionProgressPresentationFactoryTests`
- `ModelInspectionPresentationFactoryTests`
- `ModelInspectionPageNavigationTests`

The local hosted-equivalent candidate executed Action 4, Content 25, Model 9,
Outcome 7, disclosure 5, rendered-state 21, and accessibility 9 tests. Together
with page/disclosure coverage, they protect bootstrap selection, exact geometry
and typography, hidden-state isolation, nullable fraction conversion, fixed
stage semantics, all 13 mapped terminal variants, commands, automation
peer/event counts, focus/disclosure continuity, and stable control identity.
Strict pixel comparison and controlled OS evidence remain absent and open.

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
