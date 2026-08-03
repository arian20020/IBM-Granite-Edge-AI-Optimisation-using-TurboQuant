# Application feature architecture

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-03  
**Reviewed implementation baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`  
**Current branch:** `feature/model-inspection`

## Purpose

This folder is organised by **user-facing feature** rather than by technical file type. Each feature owns one understandable part of the application journey, while shared orchestration remains in the onboarding shell.

This README is the entry point for understanding how the implemented features communicate. It intentionally stays at cross-feature level; each feature folder contains a more detailed README describing its own controls, states, tests, and limitations.

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

The Model Inspection initial-screen fixes at the reviewed baseline still require a local cold-build and manual screen verification. This document does not claim that the current branch has completed llama.cpp or OpenVINO inspection.

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

ModelInspectionPage
    ├── depends on Model Inspection controls
    └── depends on Model Inspection presentation models

Model Inspection controls
    └── depend on presentation models
```

Dependencies do not flow back from the cards into the page, from the inspection page into Model Import, or from Model Import into the shell implementation.

## Feature documentation

- [Model Import architecture](./ModelImport/README.md)
- [Onboarding architecture](./Onboarding/README.md)
- [Model Inspection architecture](./ModelInspection/README.md)

Detailed implementation evidence remains under `docs/`, including:

- [Model Import and GGUF quick scan current state](../../docs/development/Model-Import-Quick-Scan-Current-State.md)
- [GGUF quick-scanner beginner guide](../../docs/development/GGUF-Quick-Scanner-Beginner-Guide.md)

## Source-of-truth hierarchy

When documents and implementation disagree, use this order:

```text
1. Source code and executable tests
2. Current-state feature README
3. Detailed development evidence under docs/development
4. Historical design documents and pull-request descriptions
```

READMEs document responsibilities and contracts. They should not contain complete copied XAML or C# files because duplicated source can become stale.

## Documentation update rules

Review the appropriate feature README whenever any of these changes:

- feature responsibility;
- navigation event or navigation parameter;
- visible state or state enum;
- page/control composition;
- cancellation or stale-result behavior;
- error or outcome classification;
- runtime service or adapter boundary;
- tests proving the architecture;
- implemented, deferred, or non-claim boundary.

## Current non-claims

The feature architecture currently does **not** provide:

- real llama.cpp or LLamaSharp model inspection;
- OpenVINO model inspection;
- dynamic progress-stage execution;
- runtime outcome classification;
- functional Model Inspection cancellation;
- Hardware Fit analysis;
- model configuration;
- a completed chat experience.

Those capabilities must be documented as implemented only after source, tests, and runtime evidence support the claim.
