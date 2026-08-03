# Recommended-model download UI

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-03  
**Reviewed implementation baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`

[← Model Import architecture](../README.md)

## Purpose

This folder contains the current prototype UI for expressing a user's preferred balance between efficiency and capability when choosing a recommended downloadable model.

At the reviewed branch baseline, this is a **view and interaction prototype**, not a complete model-catalog or download subsystem.

## Responsibility boundary

### This folder currently owns

- the visual recommended-model card;
- the continuous preference slider;
- mapping the slider value to one of five readable preference labels;
- pointer and drag cursor behavior for the slider thumb;
- responsive text behavior tested for the card.

### This folder does not currently own

- a model catalog;
- network downloads;
- model identity or version selection;
- file integrity verification;
- download progress, pause, resume, or cancellation;
- writing models to disk;
- routing a downloaded model through quick scan;
- enabling Model Inspection.

## Current local architecture

```text
ModelDownloadCard.xaml
    → displays temporary recommended-model seed information
    → contains ModelPreferenceSlider

ModelPreferenceSlider
    → locates Slider Thumb controls after template creation
    → tracks pointer-over and dragging state
    → applies a temporary interaction cursor

ModelDownloadCard.xaml.cs
    → observes slider value changes
    → maps 0–100 to a readable preference label
```

There is no service or ViewModel behind this card yet.

## File inventory

### `ModelDownloadCard.xaml`

Defines the current recommended-model card and the preference slider layout.

The XAML contains temporary development seed data and explicit comments describing the planned direction:

```text
Model catalog or seed data
    → future ModelImportViewModel.SelectedModel
    → card bindings
```

The current hard-coded card values are not an authoritative model recommendation and must not be treated as a live catalog result.

The XAML also records future theming work for shared colors, spacing, corner radii, and dark mode.

[Open file](./ModelDownloadCard.xaml)

### `ModelDownloadCard.xaml.cs`

Initializes the card and keeps the visible preference label synchronized with the slider value.

Current label mapping:

```text
0 to <20    → Maximum efficiency
20 to <40   → Efficient
40 to <60   → Balanced
60 to <80   → High capability
80 to 100   → Maximum capability
```

The slider remains continuous; it does not currently snap to five discrete stops.

This code is intentionally view-only. The file comments identify a future `ModelImportViewModel` as the likely owner of model-selection logic.

[Open file](./ModelDownloadCard.xaml.cs)

### `ModelPreferenceSlider.cs`

Extends the WinUI `Slider` control to manage pointer and drag cursor behavior on generated `Thumb` elements.

It:

- removes handlers from an old template before reapplying;
- recursively finds `Thumb` controls in the generated visual tree;
- tracks pointer entry, pointer exit, drag start, drag completion, and capture loss;
- keeps the interaction cursor stable while dragging;
- resets state when the template changes.

The current cursor is the built-in Hand cursor. The source records this as temporary until an original Open Palm cursor asset is added.

[Open file](./ModelPreferenceSlider.cs)

## Current interaction flow

```text
Card is created
    ↓
slider starts at 50
    ↓
visible label becomes “Balanced”
    ↓
user moves slider
    ↓
ValueChanged supplies the new continuous value
    ↓
GetModelScaleLabel(...) selects one of five labels
    ↓
TextBlock is updated
```

The value does not currently select, download, or validate a real model.

## Tests and evidence

Current UI-thread tests:

- [`ModelDownloadCardTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadCardTests.cs)

The tests cover at least:

- the initial value of `50`;
- the initial `Balanced` label;
- text wrapping and layout behavior for narrow presentation;
- slider-to-label transitions.

## Implemented now

- recommended-model card prototype;
- continuous 0–100 preference slider;
- five readable preference categories;
- initial Balanced state;
- custom slider-thumb interaction behavior;
- event-handler cleanup when the slider template is recreated;
- focused WinUI tests.

## Not implemented and non-claims

- no recommended-model page exists on the reviewed `feature/model-inspection` baseline;
- no catalog or ranking service is connected;
- no model download occurs;
- no hash, signature, or provenance check occurs;
- no downloaded file enters `ModelQuickScanner`;
- no selected preference is persisted;
- no ViewModel owns the current state;
- the displayed model information is development seed data, not a recommendation result.

## Known limitations and change hazards

- slider logic is view code and will need a stable ViewModel contract before it controls model choice;
- the continuous slider and five textual categories may need a deliberate product decision about snapping;
- `FindThumbs` depends on the generated Slider template structure and must be retested when Windows App SDK styling changes;
- custom cursor work must use a project-owned asset rather than shipping an external font or cursor without provenance;
- adding download functionality requires separate service, storage, integrity, cancellation, retry, and evidence boundaries rather than placing network code in this control.

## Recommended future architecture

```text
ModelCatalogService
    → verified catalog entries

ModelImportViewModel
    → selected preference
    → selected recommended model
    → download state

IModelDownloadService
    → network transfer
    → cancellation and retry
    → checksum/provenance verification

Validated downloaded package
    → existing quick-scan route
```

## Related documentation

- [Model Import architecture](../README.md)
- [Model file-import boundary](../FileImport/README.md)
- [Quick-scan architecture](../QuickScan/README.md)
