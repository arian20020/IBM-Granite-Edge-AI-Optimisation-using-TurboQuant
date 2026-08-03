# Hierarchical Feature Architecture READMEs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a central feature overview, feature-level READMEs, and full-context READMEs in every meaningful nested Model Import, Onboarding, and Model Inspection source folder, then reconcile the existing Model Import current-state document.

**Architecture:** Documentation mirrors the actual application source hierarchy. Parent READMEs explain cross-folder composition; leaf READMEs explain local inputs, outputs, file responsibilities, dependencies, tests, limitations, and change hazards. Detailed historical evidence remains under `docs/development/`, while source code and executable tests remain the final truth.

**Tech Stack:** Markdown, GitHub-rendered text diagrams, WinUI 3/C# source references, existing MSTest test paths.

## Global Constraints

- Target branch: `feature/model-inspection`.
- Reviewed application baseline: `2c51bb0551cb5556e422e63c19888c1f3874d0e5`.
- Do not modify application code, tests, fixtures, project files, or workflows.
- Do not claim that llama.cpp, LLamaSharp, OpenVINO inspection, runtime classification, dynamic stage progression, cancellation execution, or model downloads are implemented.
- Preserve historical Model Import test evidence and parser details.
- Use relative repository links rather than raw external URLs.
- Use Markdown text diagrams, not generated images.
- End every created Markdown file with a newline.
- Every nested README must explain the full local context rather than repeating only the parent summary.

---

### Task 1: Keep the cross-feature overview current

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/README.md`

**Interfaces:**
- Consumes: three feature-level READMEs.
- Produces: the documentation entry point for the application feature tree.

- [ ] Add a hierarchical directory map showing every nested README created by this plan.
- [ ] Link Model Import, Onboarding, and Model Inspection feature READMEs.
- [ ] Keep cross-feature ownership, current stage status, source-of-truth hierarchy, and update rules at overview level only.
- [ ] Confirm the overview does not duplicate leaf-folder file inventories.

---

### Task 2: Complete Model Import documentation hierarchy

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Controls/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/FileImport/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/FileImport/PickerRoute/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/README.md`

**Interfaces:**
- Consumes: current Model Import source, tests, and detailed implementation evidence.
- Produces: a navigable explanation from page orchestration down to each leaf responsibility.

- [ ] Update the Model Import root README with its child-folder map and links.
- [ ] Document `Controls` with the four card states, typed entry methods, data object, mapper, complete file inventory, and mirrored tests.
- [ ] Document `FileImport` as the selection and picker-routing boundary, including why it is separated from the page and how it delegates to `PickerRoute`.
- [ ] Document `PickerRoute` with `ModelFormatSelectionCard`, the format enum, GGUF picker, OpenVINO folder picker, native Windows dependencies, null/cancel behavior, tests, and current connection status.
- [ ] Document `ModelDownload` with `RecommendedModelDownloadPage`, `ModelDownloadCard`, `ModelPreferenceSlider`, current visual behavior, temporary cursor, tests, and explicit non-claims about download execution and validated import integration.
- [ ] Document `QuickScan` with router, GGUF parser, result/outcome/diagnostic contracts, bounded-input rules, security boundary, file inventory, tests, and distinction from full runtime inspection.
- [ ] Ensure every child README links back to Model Import and any child below it.

---

### Task 3: Complete Onboarding documentation hierarchy

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/README.md`

**Interfaces:**
- Consumes: `OnboardingShellPage`, `OnboardingStage`, `OnboardingStageIndicator`, navigation tests.
- Produces: the shell ownership contract and the full local indicator-control explanation.

- [ ] Document shell purpose, five stages, `StageFrame`, `CurrentStage`, stage synchronization, event-driven navigation, subscription lifecycle, failure behavior, tests, and deferred stages.
- [ ] Document `Controls` with the indicator XAML and code-behind, dependency property, stage validation, completed/active/future states, connector fills, accessibility live-region behavior, resource usage, tests, and limitations.
- [ ] Link the feature and control READMEs in both directions.

---

### Task 4: Complete Model Inspection documentation hierarchy

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/README.md`

**Interfaces:**
- Consumes: `ModelInspectionPage`, four controls, selector, presentation classes, state enums, navigation and selector tests.
- Produces: a clear current UI architecture and explicit boundary before runtime inspection.

- [ ] Document page navigation, Loaded lifecycle, one-time guard, four-card composition, initial presentation, five progress stages, tests, and non-claims.
- [ ] Document `Controls` with every XAML/control/code-behind file, dependency properties, visual states, template selection, bootstrap content handling, themes, accessibility, tests, and runtime-command boundary.
- [ ] Document `Models` with every presentation class and enum, default/hidden values, mutable versus immutable properties, WinUI dependencies, data relationships, and future domain-result separation.
- [ ] Link the feature, control, and model READMEs in both directions.

---

### Task 5: Reconcile detailed Model Import current-state evidence

**Files:**
- Modify: `docs/development/Model-Import-Quick-Scan-Current-State.md`

**Interfaces:**
- Consumes: the implemented Model Import-to-Inspection handoff.
- Produces: a detailed current-state record that no longer contradicts the application.

- [ ] Explain that the quick-scan foundation originated on `feature/winui-shell-model-import` and is now consumed by `feature/model-inspection`.
- [ ] Add `TryRequestModelInspection`, `ModelInspectionRequested`, shell navigation, and stage-two activation to the successful flow.
- [ ] Remove implemented navigation from the deferred list.
- [ ] Replace it with the richer future request contract carrying selected format and validated metadata.
- [ ] Preserve the recorded 134-test result and historical parser evidence.

---

### Task 6: Perform whole-document verification

**Files:**
- Verify every created or modified Markdown document.

**Interfaces:**
- Consumes: Tasks 1 through 5.
- Produces: one internally consistent hierarchical documentation set.

- [ ] Confirm every documented source and test path exists.
- [ ] Confirm every parent README links to all immediate child READMEs and each child links back.
- [ ] Confirm file inventories match the current source folders.
- [ ] Confirm terminology matches source: `StageFrame`, `CurrentStage`, `OnboardingStageIndicator`, `ModelInspectionRequested`, four inspection controls, selector, presentation classes, and enums.
- [ ] Search for false claims about llama.cpp, LLamaSharp, OpenVINO runtime inspection, dynamic stage progression, functional cancellation, model downloads, Hardware Fit, configuration, or chat.
- [ ] Confirm `Model-Import-Quick-Scan-Current-State.md` no longer lists implemented navigation as deferred.
- [ ] Check Markdown headings, tables, diagrams, relative links, and final newlines.
- [ ] Review the branch diff and confirm all changes are documentation-only.
- [ ] Record that no application build or test execution is claimed for this Markdown-only change.
