# Model Import architecture

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-03  
**Reviewed implementation baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`  
**Current branch:** `feature/model-inspection`

[← Application feature architecture](../README.md)

## Purpose

Model Import is the first onboarding stage. It allows the user to select a local model package, performs a bounded quick scan, presents a controlled result, and exposes only a validated selection to the next stage.

The current working validation route is GGUF. The quick scan is intentionally lighter than full Model Inspection: it reads enough untrusted container metadata to classify the selection and populate the import card, but it does not load model tensors, execute the model, or prove runtime compatibility.

## Folder structure

```text
ModelImport/
├── README.md
├── ModelImportPage.xaml
├── ModelImportPage.xaml.cs
├── ModelInspectionRequestedEventArgs.cs
│
├── Controls/
│   ├── README.md
│   ├── ImportModelCard.xaml/.cs
│   ├── ImportModelCardState.cs
│   ├── ImportedModelCardData.cs
│   └── ImportedModelCardDataMapper.cs
│
├── FileImport/
│   ├── README.md
│   └── PickerRoute/
│       ├── README.md
│       ├── ModelFormatSelectionCard.xaml/.cs
│       ├── GgufModelFilePicker.cs
│       └── OpenVINOFolderPicker.cs
│
├── ModelDownload/
│   ├── README.md
│   ├── ModelDownloadCard.xaml/.cs
│   └── ModelPreferenceSlider.cs
│
└── QuickScan/
    ├── README.md
    ├── ModelQuickScanner.cs
    ├── GgufQuickScanner.cs
    ├── ModelQuickScanResult.cs
    ├── ModelQuickScanOutcome.cs
    └── ModelQuickScanFailureDiagnostic.cs
```

Nested documentation:

- [Imported-model controls](./Controls/README.md)
- [File-import boundary](./FileImport/README.md)
- [Native picker routes](./FileImport/PickerRoute/README.md)
- [Recommended-model download prototype](./ModelDownload/README.md)
- [Quick-scan architecture](./QuickScan/README.md)

## Responsibility boundary

### This feature owns

- model-format selection;
- native local-file selection;
- the Model Import page state;
- active quick-scan identity and cancellation;
- routing to the correct quick scanner;
- bounded GGUF header and metadata scanning;
- quick-scan result contracts;
- formatting data for the imported-model card;
- guarding the handoff to Model Inspection.

### This feature does not own

- onboarding `Frame` navigation;
- full llama.cpp or OpenVINO runtime inspection;
- hardware-fit analysis;
- model conversion;
- model configuration;
- inference or chat.

## Current architecture

```text
ImportModelCard
    → raises browse or cancel/remove intent

ModelImportPage
    → asks the user for model format
    → opens the native picker route
    → owns the selected path and validated state
    → owns the active scan identity
    → calls ModelQuickScanner

ModelQuickScanner
    → routes GGUF requests to GgufQuickScanner

GgufQuickScanner
    → validates the untrusted GGUF container structure
    → performs one bounded metadata pass
    → returns a controlled result

ModelQuickScanResult
    → Success
    → Failure
    → Cancelled

ImportedModelCardDataMapper
    → converts validated metadata into culture-aware display values

ModelImportPage
    → displays the matching ImportModelCard state
    → enables Continue only after success
    → raises ModelInspectionRequested when the user continues
```

## Root files

### `ModelImportPage.xaml`

Defines the complete first-stage page composition, including the imported-model card and Continue action.

[Open file](./ModelImportPage.xaml)

### `ModelImportPage.xaml.cs`

Coordinates format selection, file selection, quick scan, cancellation identity, stale-result suppression, card state, validated state, diagnostics, and the guarded handoff to Model Inspection.

It stores:

```csharp
SelectedModelPath
HasValidatedModel
ValidatedScanResult
```

[Open file](./ModelImportPage.xaml.cs)

### `ModelInspectionRequestedEventArgs.cs`

Defines the current cross-feature handoff message:

```csharp
ModelInspectionRequestedEventArgs
└── string ModelPath
```

The constructor rejects null, empty, or whitespace paths. The event argument carries intent and data; it does not navigate.

[Open file](./ModelInspectionRequestedEventArgs.cs)

## Main subcomponents

### Imported-model controls

The reusable card renders the visible Model Import state and raises browse or cancel/remove intent. It does not select files or parse models.

Implemented presentations:

1. `AwaitingSelection`.
2. `Scanning`.
3. `ScanFailed`.
4. `ScanSucceeded`.

Full file-by-file context:

- [Controls README](./Controls/README.md)

### File selection and picker routes

Format choice and native Windows picker construction live outside page orchestration and parser logic.

- [File-import boundary](./FileImport/README.md)
- [Picker-route implementation](./FileImport/PickerRoute/README.md)

The GGUF picker is connected. An OpenVINO folder picker exists, but the OpenVINO scan and validated import route are not connected.

### Quick scan

`ModelQuickScanner` routes the current GGUF request to `GgufQuickScanner`. The scanner performs bounded, asynchronous, read-only parsing for supported GGUF versions.

- [Quick-scan README](./QuickScan/README.md)

### Recommended-model download prototype

`ModelDownloadCard` and `ModelPreferenceSlider` currently provide a view-only preference prototype. They are not connected to a catalog, network download, integrity verification, or validated import route.

- [Model-download README](./ModelDownload/README.md)

## Successful import flow

```text
Choose GGUF
    → select a .gguf file
    → invalidate any previous validated selection
    → show Scanning
    → run ModelQuickScanner
    → receive Success
    → format approved display values
    → store ValidatedScanResult
    → set HasValidatedModel = true
    → show ScanSucceeded
    → enable Continue to model inspection
```

The visible success card currently shows only the approved values:

- model name;
- selected filename;
- GGUF badge;
- quantisation;
- parameter-size label;
- architecture;
- formatted file size;
- formatted declared context.

`GgufVersion` remains available in `ValidatedScanResult` for diagnostics and future decisions, but it is not displayed merely because the parser returns it.

## Continue-button invariant

The UI button is enabled only after a successful quick scan, but the page also protects the boundary in code.

`TryRequestModelInspection()` accepts the request only when:

```text
HasValidatedModel is true
AND ValidatedScanResult exists
AND SelectedModelPath is not null, empty, or whitespace
```

This prevents future callers or tests from bypassing the user-interface guard.

## Model Inspection handoff

```text
User clicks Continue to model inspection
    ↓
ModelImportPage.TryRequestModelInspection()
    ↓
validated-state invariant is checked again
    ↓
ModelInspectionRequested is raised
    ↓
ModelInspectionRequestedEventArgs carries ModelPath
    ↓
OnboardingShellPage performs navigation
```

`ModelImportPage` deliberately does not search for or manipulate a parent `Frame`. The onboarding shell owns navigation and stage synchronization.

Planned improvement: replace the path-only handoff with a project-owned request that also carries the selected format and validated quick-scan metadata. That richer request is not implemented yet.

## Cancellation and replacement safety

The page owns one active scan identity.

When a replacement scan starts:

```text
new scan identity is published
    ↓
previous token is cancelled
    ↓
old operation may still finish
    ↓
old result is recognised as stale
    ↓
only the current identity may update the page
```

This prevents:

- removal being undone by a late success;
- a replacement model being overwritten by an older result;
- a cancelled scan later displaying failure or success;
- two scans racing to control the same card.

A current `Cancelled` result resets the page to `AwaitingSelection`, clears selected and validated state, and keeps Continue disabled.

## Failure and diagnostic boundary

A controlled failure:

- clears validated state;
- keeps Continue disabled;
- shows only the stable failure code and user-facing message in the card;
- sends the technical message to the diagnostic seam;
- retains only the selected filename, not the full local path, in the default trace record.

Exceptions raised by the diagnostic sink are contained. Logging failure must not replace the controlled user-visible failure state or crash the import workflow.

## Automated tests and evidence

The Model Import slice is covered by tests for:

- GGUF scanner and result contracts;
- deterministic fixture integrity;
- supported, obsolete, future, malformed, and adversarial GGUF cases;
- format routing and picker contracts;
- awaiting, scanning, failure, and success card states;
- page-to-router-to-card handoff;
- optional metadata display fallbacks;
- culture-controlled formatting;
- cancellation and replacement races;
- late success and late failure suppression;
- diagnostic capture and diagnostic-sink failure containment;
- guarded Model Inspection navigation requests;
- the current model-download card prototype.

Relevant current navigation test:

- [`ModelImportNavigationRequestTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportNavigationRequestTests.cs)

Each nested README links its closest source tests.

Detailed evidence:

- [Model Import and GGUF quick scan current state](../../../docs/development/Model-Import-Quick-Scan-Current-State.md)
- [GGUF quick-scanner beginner guide](../../../docs/development/GGUF-Quick-Scanner-Beginner-Guide.md)
- [GGUF quick-scanner test report](../../../docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md)

## Implemented now

- local GGUF selection;
- bounded GGUF v2/v3 quick scan;
- controlled Success, Failure, and Cancelled outcomes;
- four imported-card presentations;
- active-scan cancellation identity;
- stale-result suppression;
- culture-aware presentation mapping;
- diagnostic minimisation and containment;
- validated-state storage;
- guarded event-driven handoff to Model Inspection;
- view-only recommended-model preference prototype.

## Not implemented and non-claims

- OpenVINO quick scanning and validated import flow;
- actual drag-and-drop handling;
- recommended-model catalog and downloads;
- downloaded models entering the existing validation contract;
- full runtime model loading;
- tensor validation;
- tokenizer/runtime compatibility validation;
- model conversion;
- inference or chat.

## Known limitations

- the handoff currently carries only `ModelPath`, not the validated scan result;
- only the GGUF validation route is connected;
- full responsive, high-text-scaling, keyboard, and screen-reader acceptance remains pending;
- quick scan proves bounded container readability, not runtime suitability;
- the model-download card contains development seed data and view-only slider logic.

## Related feature documentation

- [Onboarding architecture](../Onboarding/README.md)
- [Model Inspection architecture](../ModelInspection/README.md)
