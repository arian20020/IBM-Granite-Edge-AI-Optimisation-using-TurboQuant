# Application feature architecture

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-03  
**Reviewed implementation baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`  
**Current branch:** `feature/model-inspection`

## Purpose

This folder is organised by **user-facing feature** rather than by technical file type. Each feature owns one understandable part of the application journey, while shared orchestration remains in the onboarding shell.

This README is the entry point for understanding how the implemented features communicate. Documentation continues down to each meaningful nested source folder so a contributor can understand the local architecture without first reading every implementation file.

## Documented source hierarchy

```text
Features/
├── README.md
│
├── ModelImport/
│   ├── README.md
│   ├── Controls/
│   │   └── README.md
│   ├── FileImport/
│   │   ├── README.md
│   │   └── PickerRoute/
│   │       └── README.md
│   ├── ModelDownload/
│   │   └── README.md
│   └── QuickScan/
│       └── README.md
│
├── Onboarding/
│   ├── README.md
│   └── Controls/
│       └── README.md
│
└── ModelInspection/
    ├── README.md
    ├── Controls/
    │   └── README.md
    └── Models/
        └── README.md
```

The nearest README explains the folder's local responsibility, file inventory, inputs, outputs, tests, limitations, and change hazards. Parent READMEs explain how those folders compose into a larger feature.

## Onboarding journey

```text
1. Choose model
       ↓
2. Inspect model
       ↓
3. Check hardware fit
       ↓
4. Configure model
       ↓
5. Ready to chat
```

### Current implementation status

| Stage | Feature | Current status |
|---|---|---|
| 1 | Model Import | Implemented for local GGUF selection and bounded quick scan |
| 2 | Model Inspection | Navigation, page composition, reusable controls, and initial presentation implemented; real runtime inspection is not implemented |
| 3 | Hardware Fit | Not implemented |
| 4 | Configure Model | Not implemented |
| 5 | Ready to Chat | Not implemented |

The Model Inspection initial-screen fixes at the reviewed baseline still require a local cold build and manual screen verification. This documentation does not claim that the current branch has completed llama.cpp or OpenVINO inspection.

## Cross-feature ownership

```text
OnboardingShellPage
│
├── owns StageFrame
├── owns CurrentStage
├── owns OnboardingStageIndicator synchronization
│
├── hosts ModelImportPage
└── hosts ModelInspectionPage
```

The shell owns the journey. Individual stage pages own their internal workflow and report user intent through explicit contracts.

### Important separation

```text
ModelImportPage
    does not navigate StageFrame directly

ModelInspectionPage
    does not know how Model Import selected the file

Reusable cards
    do not know about the onboarding shell

Picker and runtime adapters
    do not own visible page state
```

This prevents a page or control from reaching upward through the visual tree to manipulate unrelated application state.

## Implemented Model Import to Model Inspection handoff

```text
ModelImportPage
    → selects and quick-scans a local GGUF file
    → stores SelectedModelPath and ValidatedScanResult
    → enables Continue only after quick-scan success
    → revalidates the state in TryRequestModelInspection()
    → raises ModelInspectionRequested

OnboardingShellPage
    → receives ModelInspectionRequested
    → navigates StageFrame to ModelInspectionPage
    → updates CurrentStage to InspectModel
    → updates the persistent stage indicator

ModelInspectionPage
    → receives the selected path in OnNavigatedTo
    → waits for Loaded before changing child controls
    → applies the initial model, progress, outcome, and action presentations
```

The current navigation contract carries the selected model path. Passing the selected format and complete validated quick-scan result is a planned improvement, not current behavior.

## Current high-level dependency direction

```text
OnboardingShellPage
    ├── depends on ModelImportPage
    └── depends on ModelInspectionPage

ModelImportPage
    ├── depends on import controls
    ├── depends on picker routes
    └── depends on quick-scan contracts

ModelInspectionPage
    ├── depends on inspection controls
    └── depends on inspection presentation models

Reusable controls
    └── depend on their presentation contracts
```

Dependencies do not flow back from cards into pages, from the inspection page into Model Import, or from Model Import into the shell implementation.

## Feature and nested documentation

### Model Import

- [Model Import architecture](./ModelImport/README.md)
- [Imported-model controls](./ModelImport/Controls/README.md)
- [File-import boundary](./ModelImport/FileImport/README.md)
- [Native picker routes](./ModelImport/FileImport/PickerRoute/README.md)
- [Recommended-model download prototype](./ModelImport/ModelDownload/README.md)
- [Quick-scan architecture](./ModelImport/QuickScan/README.md)

### Onboarding

- [Onboarding architecture](./Onboarding/README.md)
- [Onboarding stage-indicator control](./Onboarding/Controls/README.md)

### Model Inspection

- [Model Inspection architecture](./ModelInspection/README.md)
- [Inspection controls](./ModelInspection/Controls/README.md)
- [Inspection presentation models](./ModelInspection/Models/README.md)

Detailed implementation evidence remains under `docs/`, including:

- [Model Import and GGUF quick scan current state](../../docs/development/Model-Import-Quick-Scan-Current-State.md)
- [GGUF quick-scanner beginner guide](../../docs/development/GGUF-Quick-Scanner-Beginner-Guide.md)

## Source-of-truth hierarchy

When documents and implementation disagree, use this order:

```text
1. Source code and executable tests
2. Nearest README beside the affected code
3. Parent feature README
4. Detailed development evidence under docs/development
5. Historical design documents and pull-request descriptions
```

READMEs document responsibilities and contracts. They should not contain complete copied XAML or C# files because duplicated source can become stale.

## Documentation update rules

Review the nearest README and its parent whenever any of these changes:

- a file is added, moved, or removed;
- folder or feature responsibility changes;
- navigation event or navigation parameter changes;
- visible state or state enum changes;
- page/control composition changes;
- cancellation or stale-result behavior changes;
- picker, parser, error, or outcome classification changes;
- runtime service or adapter boundary changes;
- tests proving the architecture change;
- implemented, deferred, or non-claim boundary changes.

## Current non-claims

The feature architecture currently does **not** provide:

- real llama.cpp or LLamaSharp model inspection;
- OpenVINO quick scanning or runtime inspection;
- dynamic Model Inspection progress execution;
- runtime outcome classification;
- functional Model Inspection cancellation;
- a working recommended-model catalog or download service;
- Hardware Fit analysis;
- model configuration;
- a completed chat experience.

Those capabilities must be documented as implemented only after source, tests, and runtime evidence support the claim.
