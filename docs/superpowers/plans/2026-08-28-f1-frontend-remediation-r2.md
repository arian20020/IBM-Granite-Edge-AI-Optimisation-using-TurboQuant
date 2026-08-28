# F1 Frontend Remediation R2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make download and export frontend interactions cancellable, accessible, fail-closed, and bound only to explicit verified backend contracts.

**Architecture:** Pure controllers own generation-aware operation state and reject duplicate, stale, or disabled activation. WinUI controls render only bounded enum-derived copy and accept opaque verified offers/targets through consumer interfaces; Q1 supplies authoritative services and C0 wires them in shared composition. Existing picker/drop and route registration remain untouched.

**Tech Stack:** C# 12, .NET 8, WinUI 3, MSTest, Microsoft Testing Platform.

**Spec:** User-approved F1 R2 frontend remediation request dated 2026-08-28 (conversation authority; no repository spec file).

## Global Constraints

- Base commit/tree is `a5ef3558334e50587889140dafba194853938765` / `90c34ab009b744d7b00866fb93e8dbc86363f1b2`.
- Do not edit backend optimization/artifact semantics, worker protocols, shared application composition, `MainWindow`, project/solution/package registration, or onboarding route registration.
- Q1 owns authoritative download/export services; C0 owns their shared composition.
- Visible text is derived from bounded frontend states, never URLs, paths, provider output, or unapproved metadata.
- No production implementation precedes a failing test.
- No native/package UI launch occurs without validated H1, M1, and Q1 phase receipts and the native lock.

---

### Task 1: Download consumer contract and operation controller

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/RecommendedModelDownloadContract.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadController.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadControllerTests.cs`

**Interfaces:**
- Produces: `IRecommendedModelDownloadService.DownloadAsync(RecommendedModelDownloadRequest, IProgress<ModelDownloadProgress>, CancellationToken)`.
- Produces: opaque `RecommendedModelOffer`, verified `CompletedModelDownload`, bounded `ModelDownloadResult`, and generation-aware `ModelDownloadViewState`.
- Controller permits one active operation, cancellation, retry after terminal failure, and ignores late results from superseded generations.

- [ ] **Step 1: Write failing controller/contract tests**

Test literal outcomes for success identity preservation, cancellation, retry, duplicate start rejection, late-result rejection, canonical digest/length validation, and enum-only privacy-safe failure presentation.

- [ ] **Step 2: Run the focused tests and verify RED**

Run the WinUI unit-test build/filter from the neutral SDK runner. Expected: compilation fails because the contract/controller types do not exist.

- [ ] **Step 3: Implement the minimal contract and controller**

Use a lock-protected generation, one owned `CancellationTokenSource`, immutable state records, and no arbitrary sleeps. Service exceptions map to `PublicationFailure`; cancellation maps to `Cancelled`; no exception/provider message reaches state copy.

- [ ] **Step 4: Run focused and neighboring tests and verify GREEN**

Expected: new controller tests and existing model-download tests pass with zero failures.

### Task 2: Download control, accessibility, and import handoff

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportPageCompositionTests.cs`

**Interfaces:**
- Consumes: Task 1 service/controller and verified completed selection.
- Produces: `BindDownload(RecommendedModelOffer, IRecommendedModelDownloadService)` and a verified-completion event consumed by `ModelImportPage` through the existing `SubmitInputAsync` quick-scan route.

- [ ] **Step 1: Write failing UI/control tests**

Assert default fail-closed button state; explicit automation IDs/names; polite live status; progress and cancel visibility; focus after terminal state; duplicate click rejection; cancel/retry; and success entering the same quick-scan transition as picker/drop without exposing the internal path.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: assertions fail because the card has no binding/state controls or completion handoff.

- [ ] **Step 3: Implement minimal WinUI rendering and page handoff**

Bind the explicit offer/service, map controller states to fixed copy, disable the slider/start action while busy, show cancel/retry only in valid states, and route only a verified completion to existing quick scan. Every handler calls a guarded method so keyboard/automation/direct invocation shares the same authority check.

- [ ] **Step 4: Run focused and existing picker/drop tests and verify GREEN**

Expected: download and Model Import suites pass; picker/drop transitions remain unchanged.

### Task 3: Verified export consumer contract and stateful destination UI

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Export/OptimizationExportContract.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Export/OptimizationExportController.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Presentation/OptimizationPresentationFactory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationOutcomeCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationDestinationCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationDestinationCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/OptimizationPage.xaml.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationExportControllerTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationDestinationCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationAccessibilityTests.cs`

**Interfaces:**
- Produces: opaque `VerifiedPersistentExportTarget` carrying route, plan/config identity, output identity, manifest SHA-256, and nonzero length; no source path.
- Produces: `IOptimizationExportService.ExportAsync(target, progress, cancellationToken)` with bounded terminal results.
- Produces: `OptimizationPage.BindVerifiedExport(target, service)` which succeeds only when target identity matches the current persistent presentation.

- [ ] **Step 1: Write failing pure controller and command-state tests**

Cover exact identity validation, runtime-only rejection, missing/stale/mismatched target rejection, duplicate activation, cancellation, retry, integrity mismatch, insufficient space, publication/cleanup failure, and success digest/length equality.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: missing export contract/controller and currently enabled unbound Save action fail compilation/assertions.

- [ ] **Step 3: Implement minimal export contract/controller and fail-closed action state**

Persistent success may display Save only as disabled until an exact target/service is bound. Intercept Save in the destination card, use one operation generation, disable all conflicting actions while active, and derive status copy from result enums. Runtime-only presentation never contains Save.

- [ ] **Step 4: Add accessible export presentation and verify GREEN**

Add stable automation IDs, accessible names/help, a polite live region, bounded progress, Cancel/Retry controls, and deterministic focus transitions. Run controller, destination, presentation, and accessibility tests.

### Task 4: Integration proposal, regression verification, and handoff

**Files:**
- Create: `docs/audits/2026-08-28/F1-frontend-remediation-r2.md`
- Create: `docs/audits/2026-08-28/F1-c0-minimal-wiring-proposal.patch`
- External: atomically publish `C:\UCL-AUDIT-HANDOFFS\F1.json` after transport is verified.

**Interfaces:**
- Consumes: Q1 service adapters and C0 shared composition, without editing their owned files.
- Produces: exact minimal proposed C0 wiring diff, report, hashes, test totals, and schema-valid receipt with `evidenceManifest: null`.

- [ ] **Step 1: Add frontend-focused T1 contract coverage**

Link only pure frontend contract/controller sources into the existing T1 project if project registration changes are unnecessary; otherwise keep tests in the existing WinUI unit project and record the ownership blocker rather than modifying project registration.

- [ ] **Step 2: Run complete verification**

Run focused frontend tests, accessibility tests, T1 integration tests, Debug x64 compile/package construction where possible, `git diff --check`, privacy scans, duplicate route/control scans, and a changed-path ownership scan.

- [ ] **Step 3: Create report and exact C0 proposal**

Record RED/GREEN evidence, exact nonzero totals, skipped/blocked native rows, service-composition nonclaims, and the minimal shared diff C0 must apply once Q1 publishes its adapters.

- [ ] **Step 4: Review, commit, push, and publish receipt**

Verify the clean branch tip/tree and report hash/bytes, push only `audit/ucl-f1-frontend-remediation-r2`, validate the receipt against `10-HANDOFF-RECEIPT-SCHEMA.json`, and atomically replace only `C:\UCL-AUDIT-HANDOFFS\F1.json`.
