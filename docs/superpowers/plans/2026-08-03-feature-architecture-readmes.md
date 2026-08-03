# Feature Architecture READMEs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a central feature-architecture overview, focused READMEs for Model Import, Onboarding, and Model Inspection, and reconcile the existing Model Import current-state document with the implemented navigation flow.

**Architecture:** Documentation follows a two-level structure. `Features/README.md` owns cross-feature flow and contracts; each feature README owns its internal responsibilities, states, tests, limitations, and links. Detailed historical and implementation evidence remains under `docs/development/`, with source code and tests as the final truth.

**Tech Stack:** Markdown, GitHub-rendered text diagrams, WinUI 3/C# source references, existing MSTest test names.

## Global Constraints

- Target branch: `feature/model-inspection`.
- Reviewed application baseline: `2c51bb0551cb5556e422e63c19888c1f3874d0e5`.
- Do not modify application code, tests, fixtures, project files, or workflows.
- Do not claim that llama.cpp, LLamaSharp, OpenVINO inspection, runtime classification, stage progression, or cancellation execution are implemented.
- Preserve historical Model Import test evidence and parser details.
- Use relative repository links rather than raw external URLs.
- Keep architecture diagrams as reviewable Markdown text.
- End every created Markdown file with a newline.

---

### Task 1: Add the cross-feature architecture overview

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/README.md`

**Interfaces:**
- Consumes: `OnboardingShellPage`, `ModelImportPage`, `ModelInspectionPage`, and the five `OnboardingStage` values.
- Produces: the documentation entry point linking to the three feature READMEs.

- [ ] **Step 1: Create the overview status block**

Add:

```markdown
# Application feature architecture

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-03  
**Reviewed implementation baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`
```

- [ ] **Step 2: Record the five-stage user journey**

Document:

```text
Choose model
    -> Inspect model
    -> Check hardware fit
    -> Configure model
    -> Ready to chat
```

Mark Model Import implemented, Model Inspection UI/navigation foundation implemented and under verification, and the remaining three stages not implemented.

- [ ] **Step 3: Record cross-feature ownership**

Explain that `OnboardingShellPage` owns `StageFrame`, `CurrentStage`, and `OnboardingStageIndicator`, while stage pages own their own workflows and raise intent rather than manipulating the shell.

- [ ] **Step 4: Record the implemented handoff**

Document:

```text
ModelImportPage
    -> validates current selection
    -> raises ModelInspectionRequested
OnboardingShellPage
    -> navigates StageFrame
    -> updates CurrentStage and StageIndicator
ModelInspectionPage
    -> receives the selected model path
    -> applies the initial presentation after Loaded
```

- [ ] **Step 5: Link the feature READMEs and document update rules**

Add relative links to Model Import, Onboarding, and Model Inspection. Include the source-of-truth hierarchy and documentation update triggers from the approved design.

- [ ] **Step 6: Review the file**

Confirm that the overview does not duplicate parser internals or imply that later stages exist.

- [ ] **Step 7: Commit**

```bash
git add "IBM Granite with TurboQuant (Intel)/Features/README.md"
git commit -m "docs(features): add architecture overview"
```

---

### Task 2: Add the Model Import feature README

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/README.md`

**Interfaces:**
- Consumes: `ImportModelCard`, `ModelImportPage`, `ModelQuickScanner`, `GgufQuickScanner`, `ModelQuickScanResult`, `ImportedModelCardDataMapper`, and `ModelInspectionRequestedEventArgs`.
- Produces: the current Model Import architecture and handoff contract.

- [ ] **Step 1: Record purpose and responsibility boundary**

Explain that the feature selects a local model, performs a bounded quick scan, presents controlled states, and exposes only a validated selection to the next stage.

- [ ] **Step 2: Add the architecture flow**

Document:

```text
ImportModelCard
    -> browse/remove intent
ModelImportPage
    -> format and path selection
    -> active scan identity and state
ModelQuickScanner
    -> GGUF route
GgufQuickScanner
    -> bounded metadata validation
ModelQuickScanResult
    -> Success / Failure / Cancelled
ImportedModelCardDataMapper
    -> culture-aware display values
ModelImportPage
    -> card state and guarded inspection request
```

- [ ] **Step 3: Record UI states and invariants**

List `AwaitingSelection`, `Scanning`, `ScanFailed`, and `ScanSucceeded`. State that Continue is accepted only when `HasValidatedModel` is true, `ValidatedScanResult` exists, and `SelectedModelPath` is non-empty.

- [ ] **Step 4: Record cancellation, diagnostics, and stale-result safety**

Explain active-scan identity, replacement cancellation, late-result suppression, selected-path handling, diagnostic minimisation, and diagnostic-sink containment.

- [ ] **Step 5: Record the Model Inspection handoff**

Explain `TryRequestModelInspection()`, `ModelInspectionRequested`, and the current path-only event contract. Explicitly state that navigation is owned by the onboarding shell.

- [ ] **Step 6: Record tests, implemented scope, and non-claims**

Link the detailed current-state document and list the relevant scanner, card, fixture, cancellation, diagnostic, and navigation tests. Record that OpenVINO import, drag-and-drop, recommended downloads, runtime loading, and inference remain unimplemented.

- [ ] **Step 7: Review the file**

Confirm it preserves the distinction between quick scan and full runtime inspection.

- [ ] **Step 8: Commit**

```bash
git add "IBM Granite with TurboQuant (Intel)/Features/ModelImport/README.md"
git commit -m "docs(model-import): record current architecture"
```

---

### Task 3: Add the Onboarding feature README

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/README.md`

**Interfaces:**
- Consumes: `OnboardingShellPage`, `OnboardingStage`, `OnboardingStageIndicator`, `ModelInspectionRequested`, and `ModelInspectionPage`.
- Produces: the navigation and stage-ownership contract for all onboarding features.

- [ ] **Step 1: Record purpose and owned state**

Explain that the shell hosts stage pages and owns `StageFrame`, `CurrentStage`, and the persistent stage indicator.

- [ ] **Step 2: Record the five stage values**

List Import Model, Inspect Model, Check Hardware Fit, Configure Model, and Ready to Chat, with current implementation status.

- [ ] **Step 3: Record event-driven navigation**

Document the request-to-navigation sequence and explain why stage pages do not find or manipulate a parent Frame.

- [ ] **Step 4: Record the synchronization invariant**

Add:

```text
StageFrame.Content
    == CurrentStage
    == StageIndicator.CurrentStage
```

Clarify that the equality is conceptual: the displayed page, recorded enum, and visible indicator must refer to the same stage.

- [ ] **Step 5: Record subscription lifecycle and failure behavior**

Explain duplicate-subscription prevention, detachment of the old import page, invalid-path rejection, and preservation of the current stage when `Frame.Navigate` returns false.

- [ ] **Step 6: Record tests and deferred stages**

Link `OnboardingModelInspectionNavigationTests` and record that Hardware Fit, Configure Model, Ready to Chat, back-navigation policy, and session restoration are not implemented.

- [ ] **Step 7: Commit**

```bash
git add "IBM Granite with TurboQuant (Intel)/Features/Onboarding/README.md"
git commit -m "docs(onboarding): record navigation architecture"
```

---

### Task 4: Add the Model Inspection feature README

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`

**Interfaces:**
- Consumes: `ModelInspectionPage`, four inspection controls, presentation classes, mode/status enums, selector, and navigation/selector tests.
- Produces: the current UI architecture and the explicit boundary before runtime inspection.

- [ ] **Step 1: Record current status and non-claim**

State that navigation, page composition, reusable cards, presentation contracts, and the initial visual state are implemented, while real GGUF/OpenVINO inspection is not.

- [ ] **Step 2: Record page lifecycle and composition**

Document `OnNavigatedTo` for path receipt, `Loaded` for presentation assignment, the one-time guard, and the four-card stack.

- [ ] **Step 3: Record each card responsibility**

Describe:

```text
InspectionModelCard   -> selected model identity and metadata
InspectionContentCard -> progress or findings
InspectionOutcomeCard -> final high-level result
InspectionActionCard  -> actions available in the current state
```

- [ ] **Step 4: Record presentation and binding flow**

Document:

```text
Presentation object
    -> UserControl dependency property
    -> property-change callback
    -> Bindings.Update / visual state / template selection
    -> visible XAML
```

List the four root presentation classes and explain that `Models` currently means UI presentation models, not AI model files.

- [ ] **Step 5: Record state and template mappings**

List model-card modes, content-card modes, outcome kinds, action-card modes, visual-state names, and the Progress/Findings template mapping. Explain null/bootstrap handling in `InspectionContentTemplateSelector`.

- [ ] **Step 6: Record the initial five stages**

List the five stage names and state that stage one begins Active while stages two through five begin Waiting. State plainly that the sequence is static presentation data at present.

- [ ] **Step 7: Record tests, technical debt, and next architecture layer**

Link navigation and selector tests. Record path-only navigation, code-behind presentation construction, hard-coded status brushes, missing central state machine, and the recommended `ModelInspectionViewModel -> IModelInspectionService -> GGUF/OpenVINO adapter` boundary.

- [ ] **Step 8: Commit**

```bash
git add "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md"
git commit -m "docs(model-inspection): record current architecture"
```

---

### Task 5: Reconcile the detailed Model Import current-state document

**Files:**
- Modify: `docs/development/Model-Import-Quick-Scan-Current-State.md`

**Interfaces:**
- Consumes: the newly documented Model Import-to-Inspection handoff.
- Produces: a detailed current-state record that no longer contradicts the application.

- [ ] **Step 1: Correct the document context**

Replace the statement that the document records only `feature/winui-shell-model-import` with wording that explains the quick-scan foundation originated there and is now consumed by `feature/model-inspection`.

- [ ] **Step 2: Add the implemented handoff**

After the successful import flow, add:

```text
Continue to model inspection
    -> TryRequestModelInspection validates the stored state
    -> ModelInspectionRequested carries the selected path
    -> OnboardingShellPage navigates StageFrame
    -> stage 2 becomes active
```

- [ ] **Step 3: Reconcile deferred work**

Remove `ModelInspectionPage and Continue-button navigation` from the deferred list. Replace it with the richer future request contract carrying selected format and validated quick-scan metadata.

- [ ] **Step 4: Correct the next-slice statement**

State that navigation and the initial Model Inspection presentation now exist, while the next runtime slice must consume the selected path and validated metadata without duplicating quick-scan parsing in the page.

- [ ] **Step 5: Preserve evidence**

Do not change the recorded 134-test result or parser compatibility details because they describe the verified Model Import slice.

- [ ] **Step 6: Commit**

```bash
git add docs/development/Model-Import-Quick-Scan-Current-State.md
git commit -m "docs(model-import): reconcile inspection handoff"
```

---

### Task 6: Perform whole-document verification

**Files:**
- Verify all five created or modified architecture documents.

**Interfaces:**
- Consumes: Tasks 1 through 5.
- Produces: one internally consistent documentation set.

- [ ] **Step 1: Verify paths and links**

Confirm that every relative link points to an existing path on `feature/model-inspection`.

- [ ] **Step 2: Verify terminology**

Search the documents for these exact implementation names:

```text
OnboardingShellPage
StageFrame
CurrentStage
OnboardingStageIndicator
ModelInspectionRequested
ModelInspectionPage
InspectionModelCard
InspectionContentCard
InspectionOutcomeCard
InspectionActionCard
InspectionContentTemplateSelector
```

Confirm spelling and ownership agree with source.

- [ ] **Step 3: Scan for false claims**

Confirm none of the documents claims that llama.cpp, LLamaSharp, OpenVINO inspection, runtime classification, dynamic stage progression, or cancellation execution is working.

- [ ] **Step 4: Scan for contradictions**

Confirm the detailed Model Import document no longer lists implemented navigation as deferred.

- [ ] **Step 5: Review the branch diff**

Expected changed paths are documentation only:

```text
docs/superpowers/specs/2026-08-03-feature-architecture-readmes-design.md
docs/superpowers/plans/2026-08-03-feature-architecture-readmes.md
IBM Granite with TurboQuant (Intel)/Features/README.md
IBM Granite with TurboQuant (Intel)/Features/ModelImport/README.md
IBM Granite with TurboQuant (Intel)/Features/Onboarding/README.md
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md
docs/development/Model-Import-Quick-Scan-Current-State.md
```

- [ ] **Step 6: Record verification**

No application build is required for Markdown-only changes. Record that repository-path, terminology, scope, and contradiction checks were performed, and make no claim about application tests running for this documentation change.
