# Hierarchical Feature Architecture README Design

**Status:** Approved and implemented documentation design

**Date:** 2026-08-03

**Target branch:** `feature/model-inspection`

**Reviewed application baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`

## Purpose

The WinUI application has three connected feature areas that must be understandable both as one journey and as independent folders:

1. Model Import and bounded GGUF quick scan.
2. Onboarding-shell navigation and stage ownership.
3. Model Inspection page composition and presentation-driven controls.

A top-level README alone does not give enough context when a contributor opens a leaf folder such as `QuickScan`, `PickerRoute`, `Controls`, or `Models`. The documentation therefore mirrors every meaningful responsibility boundary in the application source tree.

## Selected hierarchy

```text
IBM Granite with TurboQuant (Intel)/
└── Features/
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

Also reconcile:

```text
docs/development/Model-Import-Quick-Scan-Current-State.md
```

with the implemented Model Import-to-Inspection handoff.

## Documentation levels

### Cross-feature overview

`Features/README.md` owns:

- the five-stage journey;
- current implementation status;
- cross-feature dependencies;
- shell ownership;
- the Model Import-to-Inspection handoff;
- links to every feature and leaf README;
- source-of-truth and update rules.

### Feature READMEs

The Model Import, Onboarding, and Model Inspection READMEs own:

- feature purpose and boundary;
- complete feature flow;
- root-file responsibilities;
- child-folder map;
- state and lifecycle invariants;
- tests and evidence;
- implemented, deferred, and non-claim boundaries;
- known architectural debt.

### Leaf-folder READMEs

Each nested README must explain:

1. Why the folder exists.
2. What its parent delegates to it.
3. Inputs consumed.
4. Outputs, events, or results produced.
5. Local data flow.
6. Every source file and its single responsibility.
7. Dependencies and platform boundaries.
8. Tests and evidence.
9. Implemented behavior.
10. Not implemented and non-claims.
11. Limitations and change hazards.
12. Parent and child links.

## Folder ownership

### Model Import controls

Documents `ImportModelCard`, its four state presentations, `ImportModelCardState`, `ImportedModelCardData`, the mapper, typed entry methods, events, and card/mapper tests.

### File Import

Documents the format-selection and native-picker boundary and delegates detailed implementation to `PickerRoute`.

### Picker Route

Documents `ModelFormatSelectionCard`, `ModelFormatSelection`, `GgufModelFilePicker`, `OpenVINOFolderPicker`, cancellation/null behavior, native Windows dependencies, tests, and the incomplete OpenVINO route.

### Model Download

At the reviewed branch baseline this folder contains:

```text
ModelDownloadCard.xaml
ModelDownloadCard.xaml.cs
ModelPreferenceSlider.cs
```

It is a view-only recommended-model preference prototype. The documentation must not claim that a recommended-model page, catalog, download service, integrity verification, or validated import integration exists on this branch.

### Quick Scan

Documents the router, bounded GGUF parser, result/outcome/diagnostic contracts, parser security boundary, compatibility, cancellation, tests, and distinction from full runtime inspection.

### Onboarding controls

Documents `OnboardingStageIndicator`, its dependency property, completed/active/future states, connector fills, accessibility live-region behavior, validation/restoration, resources, and tests.

### Model Inspection controls

Documents all four cards, the selector, dependency properties, XAML templates, visual states, themes, accessibility, bootstrap content handling, tests, and the boundary before runtime commands.

### Model Inspection models

Documents UI presentation classes and enums, safe defaults, object relationships, mutable versus immutable properties, WinUI dependencies, and future separation from framework-neutral domain/runtime results.

## Test documentation boundary

Source-folder READMEs link to mirrored tests. This change does not create duplicate READMEs throughout the test tree because that would create two competing explanations of the same contract.

## Source-of-truth hierarchy

```text
1. Source code and executable tests
2. Nearest README beside the code
3. Parent feature README
4. Detailed current-state evidence under docs/development
5. Historical design documents and pull-request descriptions
```

READMEs explain contracts and responsibilities. They do not copy complete implementation files.

## Implemented versus planned language

Every document must distinguish:

- Implemented now.
- Planned next.
- Not implemented / non-claims.
- Known limitations.

Model Inspection documents must not imply that llama.cpp, LLamaSharp, OpenVINO runtime inspection, tensor validation, tokenizer validation, dynamic stage progression, result classification, or cancellation execution are working.

Model Download documents must not imply that a catalog, network download, integrity verification, or validated import route is working.

## Update triggers

Review the nearest README and its parent when:

- a file is added, moved, or removed;
- folder responsibility changes;
- an event, request, result, or navigation contract changes;
- a state enum or visible transition changes;
- picker, parser, cancellation, error, or outcome behavior changes;
- a runtime service or adapter is added;
- tests proving the architecture change;
- the implemented/deferred boundary changes.

## Validation

For this documentation-only change:

- confirm every documented path exists on the branch;
- confirm parent and child links are bidirectional;
- confirm file inventories match the branch;
- confirm terminology matches source;
- confirm no false runtime or download claims;
- reconcile the detailed Model Import current-state document;
- check Markdown structure and final newlines;
- confirm the branch diff contains documentation only.

No application build or test result is claimed by the documentation change itself.

## Non-goals

This work does not alter WinUI behavior, tests, fixtures, project files, runtime dependencies, navigation contracts, downloads, inspection services, or formal ADRs.
