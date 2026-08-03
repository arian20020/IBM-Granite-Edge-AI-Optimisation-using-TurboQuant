# Feature Architecture README Design

**Status:** Approved design

**Date:** 2026-08-03

**Target branch:** `feature/model-inspection`

**Reviewed implementation baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`

## Purpose

The WinUI application now has three connected feature areas that a new contributor must understand together:

1. Model Import and bounded GGUF quick scan.
2. Onboarding-shell navigation and stage ownership.
3. Model Inspection page composition and presentation-driven controls.

The repository already contains detailed implementation evidence for Model Import, but it does not contain a beginner-friendly documentation entry point beside the feature code. Model Inspection has also grown enough that its implemented UI architecture, deferred runtime boundary, and known limitations need to be recorded before llama.cpp and OpenVINO integration begins.

This design introduces living architecture documentation close to the source code without replacing detailed evidence under `docs/`.

## Selected approach

Use one central feature overview plus one README per implemented feature:

```text
IBM Granite with TurboQuant (Intel)/
└── Features/
    ├── README.md
    ├── ModelImport/
    │   └── README.md
    ├── Onboarding/
    │   └── README.md
    └── ModelInspection/
        └── README.md
```

Also update:

```text
docs/development/Model-Import-Quick-Scan-Current-State.md
```

so that its current-state and deferred-work sections agree with the newly implemented Model Inspection navigation boundary.

## Why this approach was selected

A single large README would be easy to find but would mix unrelated implementation details and become difficult to maintain as Hardware Fit, model configuration, and chat are added.

Only separate feature READMEs would preserve cohesion but would not give a beginner one place to understand how the pages communicate.

The selected structure therefore uses:

- `Features/README.md` for the complete user journey and cross-feature contracts;
- a feature README for each feature's internal responsibilities, state model, tests, limitations, and links;
- `docs/development/` for deeper implementation evidence and historical context;
- source code and tests as the final implementation truth.

## Documentation ownership

### `Features/README.md`

Owns only cross-feature information:

- the five-stage onboarding journey;
- current implementation status by stage;
- `OnboardingShellPage` ownership of `StageFrame`, `CurrentStage`, and the persistent indicator;
- the event-driven handoff from Model Import to Model Inspection;
- links to each feature README;
- repository-wide rules for keeping feature documentation current.

It must not duplicate detailed parser rules, complete card-state tables, or runtime plans.

### `Features/ModelImport/README.md`

Owns the current Model Import architecture:

- format selection and native file picking;
- `ImportModelCard` presentations;
- `ModelImportPage` orchestration;
- `ModelQuickScanner` routing;
- bounded `GgufQuickScanner` parsing;
- selected and validated model state;
- cancellation identity and stale-result suppression;
- the guarded `ModelInspectionRequested` handoff;
- tests, evidence links, non-claims, and deferred work.

### `Features/Onboarding/README.md`

Owns the onboarding-shell architecture:

- stage definitions;
- `StageFrame` ownership;
- `CurrentStage` and stage-indicator synchronization;
- event subscription and detachment;
- navigation failure behavior;
- current navigation tests;
- the boundary for future Hardware Fit, Configure Model, and Ready to Chat stages.

### `Features/ModelInspection/README.md`

Owns the current Model Inspection architecture:

- page lifecycle and composition;
- the four reusable cards;
- presentation models and dependency-property binding flow;
- card modes, outcome kinds, and visual states;
- the five initial progress stages;
- template selection and WinUI bootstrap behavior;
- current tests;
- explicit separation between implemented UI architecture and unimplemented llama.cpp/OpenVINO runtime inspection;
- the recommended next service and ViewModel layer.

### `docs/development/Model-Import-Quick-Scan-Current-State.md`

Remains the detailed Model Import implementation record. It must be updated rather than replaced. Historical test evidence and parser details remain intact, while outdated statements about Model Inspection navigation being deferred are corrected.

## Required README structure

Each feature README will use the following order where applicable:

1. Status and reviewed implementation baseline.
2. Purpose.
3. Responsibility boundary.
4. Architecture or data-flow diagram.
5. Implemented states and invariants.
6. Failure, cancellation, or lifecycle behavior.
7. Tests and evidence.
8. Implemented scope.
9. Not implemented and non-claims.
10. Known limitations or technical debt.
11. Related documentation and source files.

This repeated structure makes the files easier to compare without forcing every feature into identical technical details.

## Source-of-truth hierarchy

When documentation and implementation disagree, use this order:

```text
1. Source code and executable tests
2. Current-state feature README
3. Detailed development evidence under docs/development
4. Historical design documents and pull-request descriptions
```

READMEs describe contracts and responsibilities. They should not contain full duplicated XAML or C# files because copied implementation can become stale.

## Implemented versus planned content

Every README must visibly distinguish:

- `Implemented now`;
- `Planned next`;
- `Not implemented / non-claims`;
- `Known limitations`.

The Model Inspection README must state plainly that the progress screen is currently presentation-only. It must not imply that llama.cpp, LLamaSharp, OpenVINO, tensor validation, tokenizer validation, result classification, or cancellation execution are already working.

## Architecture diagrams

Use Markdown text diagrams rather than generated images. Text diagrams:

- remain reviewable in diffs;
- render consistently on GitHub;
- are accessible without external design tools;
- are easier to update with source changes.

The overview README will show the cross-feature route. Feature READMEs will show only their internal flow.

## Documentation update triggers

A feature README must be reviewed when any of the following changes:

- feature responsibility;
- navigation event or parameter contract;
- state enum or visible state transition;
- page/control composition;
- cancellation or stale-result behavior;
- error or outcome classification;
- runtime service or adapter boundary;
- test coverage that proves the architecture;
- implemented/deferred boundary.

## Validation

Because this change is documentation-only, validation consists of:

- confirming every linked repository path exists;
- confirming architecture names match the current branch source;
- confirming no document claims an unimplemented runtime capability;
- confirming the existing Model Import current-state document no longer contradicts the implemented navigation flow;
- reviewing Markdown structure and code fences;
- checking the final branch diff for unintended application-code changes.

## Non-goals

This documentation change does not:

- alter WinUI behavior;
- change navigation contracts;
- implement model inspection services;
- add LLamaSharp, llama.cpp, or OpenVINO dependencies;
- change tests or fixture data;
- replace formal architecture decision records;
- claim that planned components are complete.
