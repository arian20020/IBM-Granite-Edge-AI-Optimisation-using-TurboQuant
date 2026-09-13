# Model Import controls

**Status:** Living current-state documentation
**Last reviewed:** 2026-08-03
**Reviewed implementation baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`

[← Model Import architecture](../README.md)

## Purpose

This folder contains the reusable visible card used by `ModelImportPage` and the small presentation types that supply its successful state.

The folder exists so the page can coordinate selection and scanning without also containing the internal XAML geometry, state-specific text fields, and display formatting rules of the imported-model card.

## Responsibility boundary

### This folder owns

- the four visible Model Import card presentations;
- browse and cancel/remove intent events;
- visibility switching between card states;
- validation of data required by each visible state;
- the successful-card display record;
- mapping validated quick-scan metadata into formatted card text.

### This folder does not own

- native file selection;
- quick-scan execution;
- active scan cancellation tokens;
- stale-result suppression;
- onboarding navigation;
- full runtime model inspection.

## Local data flow

```text
ModelImportPage
    │
    ├── ShowAwaitingSelection()
    ├── ShowScanning(fileName)
    ├── ShowFailure(fileName, code, message)
    └── ShowSuccess(ImportedModelCardData)
            │
            ▼
ImportModelCard
    → displays exactly one visual state

ImportModelCard
    ├── BrowseFilesRequested
    └── CancelScanRequested
            │
            ▼
ModelImportPage handles workflow action
```

Successful data follows a separate mapping route:

```text
ModelQuickScanResult
    + selected filename
    + display culture
            │
            ▼
ImportedModelCardDataMapper
            │
            ▼
ImportedModelCardData
            │
            ▼
ImportModelCard.ShowSuccess(...)
```

## File inventory

### `ImportModelCard.xaml`

Defines the internal layout for:

- awaiting selection;
- scanning;
- scan succeeded;
- scan failed.

It owns the card's typography, badges, metadata arrangement, progress indication, buttons, and accessibility labels. The parent page owns where the card is placed on the screen.

[Open file](./ImportModelCard.xaml)

### `ImportModelCard.xaml.cs`

Owns the card state transition methods and user-intent events.

Public or internal interaction surface:

```csharp
BrowseFilesRequested
CancelScanRequested
CurrentState
ShowAwaitingSelection()
ShowScanning(selectedFileName)
ShowFailure(selectedFileName, failureCode, failureMessage)
ShowSuccess(importedModelCardData)
```

`ShowSuccess` requires every displayed value to be non-empty. The generic `SetState` test seam deliberately rejects `ScanSucceeded`; callers must use `ShowSuccess` so an incomplete success card cannot be displayed.

Every state transition clears values belonging to the other states. This prevents old model names, error codes, or metadata remaining visible after a later transition.

[Open file](./ImportModelCard.xaml.cs)

### `ImportModelCardState.cs`

Defines the four semantic card states:

```text
AwaitingSelection
Scanning
ScanSucceeded
ScanFailed
```

The enum describes card presentation only. It does not represent the complete page workflow or scanner outcome contract.

[Open file](./ImportModelCardState.cs)

### `ImportedModelCardData.cs`

Defines the complete set of **formatted values** required by the successful card:

```text
FileName
ModelName
Parameters
Architecture
Quantization
FileSize
DeclaredContext
```

This record is intentionally display-specific. It is not the authoritative scanner result and must not be used as the future Model Inspection navigation contract.

[Open file](./ImportedModelCardData.cs)

### `ImportedModelCardDataMapper.cs`

Converts `ModelQuickScanResult` values into the strings shown by the successful card.

Its separation from the page makes these rules independently testable:

- culture-aware numeric formatting;
- file-size formatting;
- context-length formatting;
- fallback text for optional metadata;
- preservation of the approved visible field set.

The mapper must not invent runtime inspection results or expose technical diagnostics as user-facing metadata.

[Open file](./ImportedModelCardDataMapper.cs)

## Card-state invariants

### Awaiting selection

```text
No selected filename is displayed
No validated metadata is displayed
Browse action is available
```

### Scanning

```text
A non-empty selected filename is required
Progress presentation is visible
Success and failure values are cleared
```

### Scan failed

```text
A non-empty selected filename is required
A stable fallback failure code is supplied when needed
A controlled fallback user message is supplied when needed
No success metadata remains visible
```

### Scan succeeded

```text
ImportedModelCardData must exist
Every displayed field must be non-empty
Success values are assigned together
Scanning and failure values are cleared
```

## Event ownership

The card raises intent only:

```text
BrowseFilesRequested
→ the page decides which dialog and picker to use

CancelScanRequested
→ the page cancels the active scan and resets state
```

The card does not open Windows pickers, create cancellation tokens, or navigate to another page.

## Tests and evidence

Key tests:

- [`ImportModelCardTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Controls/ImportModelCardTests.cs)
- [`ImportedModelCardDataMapperTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Controls/ImportedModelCardDataMapperTests.cs)
- [`ModelImportPageStateMachineTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Controls/ModelImportPageStateMachineTests.cs)

These tests cover card states, required data, fallback formatting, culture-controlled formatting, page-to-card transitions, cancellation, replacement, and stale-result behavior.

Detailed evidence:

- [Model Import and GGUF quick scan current state](../../../../docs/development/Model-Import-Quick-Scan-Current-State.md)

## Implemented now

- four explicit card states;
- typed state-entry methods;
- event-driven browse and cancel/remove intent;
- complete success presentation record;
- culture-aware successful-card mapping;
- clearing of stale state-specific values;
- defensive validation of visible data.

## Not implemented and non-claims

- the card does not perform drag-and-drop handling yet;
- the card does not scan or load models;
- the card does not classify runtime compatibility;
- the card does not own navigation;
- the successful display record is not a domain model or Model Inspection request.

## Known limitations and change hazards

- adding a visible metadata field requires coordinated changes to XAML, `ImportedModelCardData`, the mapper, and tests;
- adding a new card state requires changes to the enum, visibility switching, state cleanup, XAML, and tests;
- moving workflow logic into the card would break the current separation between presentation and orchestration;
- technical failure details must remain outside the user-facing card unless explicitly approved for display.

## Related documentation

- [Model Import architecture](../README.md)
- [File-import boundary](../FileImport/README.md)
- [Quick-scan architecture](../QuickScan/README.md)
