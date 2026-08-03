# Hierarchical Feature Architecture README Design

**Status:** Approved design, expanded after implementation review

**Date:** 2026-08-03

**Target branch:** `feature/model-inspection`

**Reviewed application baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`

## Purpose

The WinUI application now has three connected feature areas that a new contributor must understand together:

1. Model Import and bounded GGUF quick scan.
2. Onboarding-shell navigation and stage ownership.
3. Model Inspection page composition and presentation-driven controls.

A README only at each top-level feature folder is not enough for a beginner who opens a nested folder such as `QuickScan`, `Controls`, or `Models`. The documentation must continue down the real source tree so that each meaningful folder explains why it exists, the files it contains, how data enters and leaves, which tests prove it, and which capabilities remain unimplemented.

This design introduces living architecture documentation beside the source without replacing detailed evidence under `docs/`.

## Selected approach

Use a central overview, feature-level READMEs, and focused READMEs in every meaningful nested application folder:

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

Also update:

```text
docs/development/Model-Import-Quick-Scan-Current-State.md
```

so its current-state and deferred-work sections agree with the implemented Model Inspection navigation boundary.

The mirrored test folders will be linked from the relevant source-folder READMEs. They will not receive duplicate READMEs in this change because the source-folder documents already explain which tests prove each boundary, and duplicating that explanation in both trees would create two maintenance points.

## Why this approach was selected

A single large README would be easy to find but would mix unrelated details and become difficult to maintain as Hardware Fit, configuration, and chat are added.

Only top-level feature READMEs would preserve feature cohesion but would still leave nested folders unexplained.

A README in every directory regardless of meaning would create low-value repetition. The selected structure therefore documents every **meaningful responsibility boundary** while avoiding READMEs in generated output, assets, or folders that exist only because of build tooling.

## Documentation levels

### Level 1: `Features/README.md`

Owns only cross-feature information:

- the five-stage onboarding journey;
- implementation status by stage;
- `OnboardingShellPage` ownership of `StageFrame`, `CurrentStage`, and the persistent indicator;
- the event-driven handoff from Model Import to Model Inspection;
- links to each feature README;
- source-of-truth and update rules.

It must not duplicate parser limits, complete card-state tables, or full file inventories.

### Level 2: feature READMEs

`ModelImport/README.md`, `Onboarding/README.md`, and `ModelInspection/README.md` own:

- the feature's purpose and boundary;
- its complete internal flow;
- current state model and invariants;
- links to nested folder READMEs;
- feature-level tests and evidence;
- implemented, planned, and non-claim boundaries;
- known architectural debt.

### Level 3: nested folder READMEs

Every nested README owns the detailed local context needed to understand that folder without opening every file first.

Each nested README must include:

1. Folder purpose.
2. Why the folder is separated from its parent.
3. Inputs consumed.
4. Outputs or events produced.
5. Local data flow.
6. File inventory with one responsibility per file.
7. Dependencies on parent, sibling, framework, or platform components.
8. Tests and evidence paths.
9. Implemented behavior.
10. Not implemented and non-claims.
11. Known limitations or change hazards.
12. Parent and child documentation links.

## Folder-specific ownership

### `ModelImport/Controls/README.md`

Documents:

- `ImportModelCard.xaml` and code-behind;
- `ImportModelCardState`;
- `ImportedModelCardData`;
- `ImportedModelCardDataMapper`;
- four visible card states;
- typed state-entry methods;
- presentation-only responsibility;
- card and mapper tests.

### `ModelImport/FileImport/README.md`

Documents the model-selection boundary as a grouping layer:

- why format selection and native picker routing are kept outside page orchestration;
- how a model-format choice leads to a route;
- current GGUF connection;
- current OpenVINO picker existence versus unimplemented OpenVINO import workflow;
- the child `PickerRoute` folder.

### `ModelImport/FileImport/PickerRoute/README.md`

Documents:

- `ModelFormatSelectionCard.xaml` and code-behind;
- `ModelFormatSelection` values;
- `GgufModelFilePicker`;
- `OpenVINOFolderPicker`;
- native Windows picker dependencies;
- cancellation/null-return behavior;
- picker tests;
- the fact that OpenVINO selection is not yet connected to scanning.

### `ModelImport/ModelDownload/README.md`

Documents:

- `RecommendedModelDownloadPage`;
- `ModelDownloadCard`;
- `ModelPreferenceSlider`;
- current visual/interactivity role;
- temporary cursor behavior;
- current separation from the validated import/quick-scan contract;
- tests and deferred download execution.

### `ModelImport/QuickScan/README.md`

Documents:

- `ModelQuickScanner` routing;
- `GgufQuickScanner` bounded parsing;
- `ModelQuickScanResult`;
- `ModelQuickScanOutcome`;
- `ModelQuickScanFailureDiagnostic`;
- parser limits and compatibility;
- Success, Failure, and Cancelled contracts;
- security and untrusted-input boundary;
- scanner, result, fixture, and router tests;
- the difference between quick scan and full runtime inspection.

### `Onboarding/Controls/README.md`

Documents:

- `OnboardingStageIndicator.xaml` and code-behind;
- the `CurrentStage` dependency property;
- completed, active, and future visual states;
- connector-fill logic;
- accessible live-region updates;
- invalid-stage restoration and failure behavior;
- indicator tests.

### `ModelInspection/Controls/README.md`

Documents:

- all four reusable cards;
- `InspectionContentTemplateSelector`;
- each control's presentation dependency property;
- visual-state ownership;
- Progress and Findings template selection;
- WinUI bootstrap content behavior;
- theme dictionaries and accessibility;
- navigation and selector tests currently proving the connected slice;
- the boundary before runtime commands and services.

### `ModelInspection/Models/README.md`

Documents that `Models` currently means UI presentation contracts, not AI model files. It inventories:

- root presentation classes;
- child row/action/check presentations;
- mode, status, tone, badge, and outcome enums;
- default/hidden objects;
- mutable versus immutable presentation values;
- WinUI-specific dependencies such as `Visibility`, `Symbol`, and `ICommand`;
- the future separation from runtime/domain inspection results.

## Existing detailed document

`docs/development/Model-Import-Quick-Scan-Current-State.md` remains the detailed Model Import implementation record. It must be updated rather than replaced. Historical test evidence and parser details remain intact, while outdated statements about Model Inspection navigation being deferred are corrected.

## Required README structure

Each README will use this order where applicable:

1. Status and reviewed implementation baseline.
2. Purpose.
3. Parent context.
4. Responsibility boundary.
5. Architecture or data-flow diagram.
6. File inventory.
7. Implemented states and invariants.
8. Failure, cancellation, lifecycle, or security behavior.
9. Tests and evidence.
10. Implemented scope.
11. Not implemented and non-claims.
12. Known limitations or technical debt.
13. Related parent, child, source, and test links.

The structure is repeated for predictability, but content must remain specific to the folder rather than copying parent text.

## Source-of-truth hierarchy

When documentation and implementation disagree, use this order:

```text
1. Source code and executable tests
2. Nearest current-state README beside the code
3. Parent feature README
4. Detailed development evidence under docs/development
5. Historical design documents and pull-request descriptions
```

READMEs describe contracts and responsibilities. They must not contain complete duplicated XAML or C# files because copied implementation can become stale.

## Implemented versus planned content

Every README must visibly distinguish:

- `Implemented now`;
- `Planned next`;
- `Not implemented / non-claims`;
- `Known limitations`.

The Model Inspection documentation must state plainly that the progress screen is currently presentation-only. It must not imply that llama.cpp, LLamaSharp, OpenVINO, tensor validation, tokenizer validation, result classification, dynamic progress, or cancellation execution are working.

The Model Download documentation must not imply that downloads, integrity verification, or entry into the validated import route are complete unless the source and tests prove those behaviors.

## Architecture diagrams

Use Markdown text diagrams rather than generated images. Text diagrams:

- remain reviewable in diffs;
- render consistently on GitHub;
- work without external design tools;
- are easy to update with source changes.

Each diagram must remain local to the folder's abstraction level.

## Documentation update triggers

Review the nearest README and its parent when any of these changes:

- a file is added, moved, or removed from the folder;
- folder responsibility changes;
- navigation event or parameter contract changes;
- state enum or visible transition changes;
- control composition changes;
- cancellation or stale-result behavior changes;
- parser limit, error, or outcome classification changes;
- platform picker behavior changes;
- runtime service or adapter boundary changes;
- tests proving the folder's behavior change;
- implemented/deferred boundary changes.

## Validation

Because this change is documentation-only, validation consists of:

- confirming every documented file and linked test path exists;
- confirming architecture names match current branch source;
- confirming parent and child READMEs link to each other;
- confirming no document claims an unimplemented runtime or download capability;
- confirming the existing Model Import current-state document no longer contradicts the implemented navigation flow;
- reviewing Markdown headings, tables, diagrams, and code fences;
- checking the final branch diff for unintended application-code changes.

## Non-goals

This documentation change does not:

- alter WinUI behavior;
- change navigation contracts;
- implement model inspection services;
- add LLamaSharp, llama.cpp, or OpenVINO dependencies;
- implement model downloads;
- change tests or fixture data;
- create duplicate documentation throughout the test tree;
- replace formal architecture decision records;
- claim that planned components are complete.
