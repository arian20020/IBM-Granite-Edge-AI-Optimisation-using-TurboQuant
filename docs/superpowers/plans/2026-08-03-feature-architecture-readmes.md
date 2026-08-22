# Hierarchical Feature Architecture READMEs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a central feature overview, feature-level READMEs, and full-context READMEs in every meaningful nested Model Import, Onboarding, and Model Inspection source folder, then reconcile the detailed Model Import current-state document.

**Architecture:** Documentation mirrors the application source hierarchy. Parent READMEs explain composition; leaf READMEs explain local inputs, outputs, file responsibilities, dependencies, tests, limitations, and change hazards. Source and executable tests remain the final truth.

**Tech Stack:** Markdown, GitHub text diagrams, WinUI 3/C# source links, existing MSTest paths.

## Global constraints

- Target branch: `feature/model-inspection`.
- Reviewed application baseline: `2c51bb0551cb5556e422e63c19888c1f3874d0e5`.
- Modify documentation only.
- Do not claim llama.cpp, LLamaSharp, OpenVINO inspection, dynamic progress, functional cancellation, model downloads, Hardware Fit, configuration, or chat are implemented.
- Preserve historical Model Import evidence.
- Use relative links and final newlines.
- Every leaf README must contain folder-specific context, not copied parent prose.

---

### Task 1: Cross-feature overview

**Files:**
- Create/update: `IBM Granite with TurboQuant (Intel)/Features/README.md`

- [x] Record the five-stage journey and implementation status.
- [x] Record shell ownership and the event-driven handoff.
- [x] Add the complete documented folder tree.
- [x] Link every feature and leaf README.
- [x] Record source-of-truth and update rules.

---

### Task 2: Model Import hierarchy

**Files:**
- Create/update: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Controls/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/FileImport/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/FileImport/PickerRoute/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/README.md`

- [x] Document root page orchestration and guarded Model Inspection handoff.
- [x] Document card states, data record, mapper, events, and tests.
- [x] Document file-selection grouping and child route.
- [x] Document format dialog, GGUF picker, OpenVINO folder picker, platform boundary, cancellation, and tests.
- [x] Document the reviewed branch's actual Model Download files: `ModelDownloadCard.xaml`, `ModelDownloadCard.xaml.cs`, and `ModelPreferenceSlider.cs`.
- [x] State that Model Download is a view-only prototype without a page, catalog, download service, integrity verification, or validated import integration on this branch.
- [x] Document quick-scan router, parser, result/outcome/diagnostic contracts, security boundary, tests, and non-claims.

---

### Task 3: Onboarding hierarchy

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/README.md`

- [x] Document shell composition, five stages, `StageFrame`, current-stage invariant, event subscriptions, failure behavior, tests, and deferred stages.
- [x] Document the indicator dependency property, stage validation/restoration, completed/active/future visuals, connector fills, accessibility, resources, tests, and limitations.

---

### Task 4: Model Inspection hierarchy

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/README.md`

- [x] Document page navigation, Loaded lifecycle, initial presentations, five stages, tests, and runtime non-claims.
- [x] Document every control and selector, dependency properties, templates, visual states, bootstrap handling, themes, accessibility, tests, and missing coverage.
- [x] Document every presentation class and enum, object relationships, defaults, mutation, WinUI dependencies, risks, and future domain separation.

---

### Task 5: Detailed Model Import reconciliation

**Files:**
- Update: `docs/development/Model-Import-Quick-Scan-Current-State.md`

- [x] Record that the foundation originated on the Model Import branch and is consumed by `feature/model-inspection`.
- [x] Add the guarded request, event, shell navigation, and stage-two transition.
- [x] Replace implemented navigation in the deferred list with the richer future request contract.
- [x] Preserve the historical 134-test evidence and distinguish later tests.
- [x] Record the next ViewModel/service/runtime-probe boundary.

---

### Task 6: Final verification

- [x] Confirm all 12 feature-tree READMEs exist.
- [x] Confirm parent/child links are bidirectional.
- [x] Confirm documented source and test paths against the current branch files and mirrored test paths.
- [x] Confirm file inventories match `feature/model-inspection` rather than newer `main` content.
- [x] Confirm no false runtime or download claims.
- [x] Confirm the detailed current-state document no longer lists implemented navigation as deferred.
- [x] Confirm the diff is documentation-only.
- [x] Record that no application build or test execution is claimed for this Markdown-only change.

## Verification evidence

`compare_commits` from application baseline
`2c51bb0551cb5556e422e63c19888c1f3874d0e5` to the feature branch showed
exactly 15 changed paths:

```text
12 feature-tree README files
1 detailed current-state Markdown update
1 design Markdown file
1 implementation-plan Markdown file
```

Every changed path is Markdown. No application source, test, fixture, project,
or workflow file appears in the diff.

Branch-specific file checks confirmed that the reviewed Model Download folder
contains `ModelDownloadCard.xaml`, `ModelDownloadCard.xaml.cs`, and
`ModelPreferenceSlider.cs`; the documents do not incorrectly copy the newer
main-branch `RecommendedModelDownloadPage` into this branch's inventory.

Source and test path checks were performed against the connected repository for
the picker files, quick-scan contracts, Model Import controls, Model Download
card test, onboarding shell and indicator, Model Inspection page/controls/models,
navigation tests, indicator tests, and selector tests.

No build, packaged test run, or manual WinUI run was executed for this
Markdown-only documentation change. Existing historical test totals are kept
explicitly attached to their previously verified implementation heads.
