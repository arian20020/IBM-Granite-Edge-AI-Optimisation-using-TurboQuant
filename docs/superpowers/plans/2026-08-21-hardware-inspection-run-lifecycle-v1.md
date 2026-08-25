# Hardware Inspection Run Lifecycle v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to execute this plan task-by-task.

**Goal:** Turn the approved static Hardware Inspection presentation into a real, candidate-free application lifecycle with exactly one active run, seven ordered progress stages, cancellation, retry, stale-event rejection, terminal result mapping, and handoff creation for usable completed results.

**Architecture:** Keep dependency direction `Presentation -> Application -> Domain`. Application defines immutable run/progress/result contracts and the sole `IHardwareInspectionService` boundary. A Hardware-owned ViewModel coordinates one active run and maps accepted application events into the existing presentation factories. The page binds only to that ViewModel and never calls providers, native APIs, processes, parsers, or compatibility logic. A deterministic in-memory service is test-only and proves behavior without claiming hardware evidence.

**Tech Stack:** C# 12, .NET 8, WinUI 3 / Windows App SDK 2.2, MSTest 4.3.2 packaged AppContainer tests, Python contract tests, Git.

**Authorities:**

- `docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md`
- `docs/superpowers/specs/2026-08-19-hardware-inspection-visual-contract-v1.md`
- Current combined baseline `8a8540040884fbc51b3cb4af908081cc586e00e2`

## Global Constraints

- Work only in `C:\hi-functional` on `feature/hardware-inspection-functional-v1`.
- Preserve Model Inspection, Model Import, Onboarding, App resources, project files, workflows, Stage A controls, and shared protocols byte-for-byte.
- Do not invoke hardware, external executables, candidates, the UCL laptop, network changes, GitHub workflows, or compatibility calculations.
- Gate 1 remains Blocked and Gate 2 remains prohibited.
- Production Presentation depends only on `IHardwareInspectionService`, immutable Application contracts, Domain contracts, and Hardware-owned presentation factories.
- Hardware providers must never receive model content. This plan introduces no model request or Block 3 data.
- A run has one non-empty `InspectionId`, one cancellation source, and strictly increasing positive progress sequence numbers.
- Retry creates a new `InspectionId`. Cancellation is terminal and distinct from failure.
- Reject wrong-run, duplicate, decreasing, post-terminal, and post-deactivation progress/result events.
- Keep user-visible stages in the exact approved seven-stage order. Do not fabricate percentages.
- Only `Completed` and `CompletedWithWarnings` with a usable snapshot can create `HardwareInspectionHandoff`.
- All new behavior must be introduced test-first. Keep existing test identities unchanged; add new Hardware-owned identities only.
- Test output belongs under ignored `TestResults/HardwareInspection/RunLifecycleV1/`.

## Files and Ownership

### Application contracts

- Create `Features/HardwareInspection/Application/HardwareInspectionRunStage.cs`.
- Create `Features/HardwareInspection/Application/HardwareInspectionRunProgress.cs`.
- Create `Features/HardwareInspection/Application/HardwareInspectionFailureKind.cs`.
- Create `Features/HardwareInspection/Application/HardwareInspectionRunResult.cs`.
- Create `Features/HardwareInspection/Application/IHardwareInspectionService.cs`.

### Presentation lifecycle

- Create `Features/HardwareInspection/ViewModels/HardwareInspectionViewModel.cs`.
- Modify only as required: `HardwareInspectionPage.xaml.cs`.
- Reuse unchanged: `HardwareInspectionPresentationFactory`, `HardwareSummaryPresentationFactory`, all existing cards and visual resources.

### Tests

- Create `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionRunContractTests.cs`.
- Create `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionViewModelTests.cs`.
- Test-only deterministic service and controllable stage pacer live inside the new test files, not production.

---

## Task 1: Immutable Run Contracts

- [ ] Write failing tests that require:
  - exactly seven Application stages in approved order;
  - non-empty run identity and positive sequence;
  - completed results to carry one usable snapshot;
  - warnings/completed results to create a handoff with the same inspection identity;
  - failed results to carry one closed failure kind and no actionable handoff;
  - cancelled results to carry neither snapshot nor failure kind;
  - impossible outcome/snapshot/failure combinations to throw before state is published;
  - `IHardwareInspectionService.RunAsync(Guid, IProgress<HardwareInspectionRunProgress>, CancellationToken)` as the sole collection call.
- [ ] Run the exact new contract test class and capture the missing-type RED.
- [ ] Implement the smallest immutable contracts that satisfy those tests.
- [ ] Run the class to GREEN and verify no Presentation/WinUI reference exists in Domain or Application.
- [ ] Commit only contracts and their tests with `feat(hardware-inspection): add run lifecycle contracts`.

## Task 2: One-Run ViewModel Coordinator

- [ ] Write failing ViewModel tests for:
  - exactly one auto-start per activation;
  - immediate Starting presentation before awaiting the service;
  - strictly increasing progress accepted for the matching run;
  - wrong-run, duplicate, decreasing, post-terminal, and post-deactivation events ignored;
  - completed and warning results mapped to summary/details and an actionable handoff;
  - failed and cancelled results never expose a handoff;
  - cancellation immediately maps to Stopping, requests the run token once, then accepts only Cancelled;
  - retry retires the old run and starts a new non-empty identity;
  - service exceptions map to a safe transient operational failure without raw exception text;
  - deactivation requests cancellation and prevents later mutation;
  - all accepted UI state changes are exposed through one immutable snapshot/property-change seam.
- [ ] Capture a valid RED before production ViewModel code exists.
- [ ] Implement the Hardware-owned ViewModel with one private run-generation owner, linked cancellation, last-sequence tracking, terminal latch, and injectable minimum-stage pacer.
- [ ] Map Application stages to the existing Presentation stage enum explicitly and exhaustively.
- [ ] Map results through existing factories; build summary from the canonical snapshot and build privacy-safe details without raw output or paths.
- [ ] Run the exact ViewModel class to GREEN.
- [ ] Commit ViewModel and tests with `feat(hardware-inspection): coordinate one inspection run`.

## Task 3: Page Lifecycle and Typed Actions

- [ ] Extend existing page tests first to require:
  - injected ViewModel activation starts once after the page is loaded;
  - presentation changes call the existing `Apply` seam without replacing cards;
  - Cancel invokes ViewModel cancellation;
  - Run again/Try again invokes ViewModel retry;
  - navigation/deactivation retires the run;
  - Back and Continue are raised as typed navigation requests only; the page does not perform navigation itself;
  - Continue stays visible-disabled unless a usable Hardware handoff exists and a compatibility route is explicitly registered.
- [ ] Capture RED with production page unchanged.
- [ ] Add the minimal page-owned binding/lifecycle adapter while preserving all approved XAML and visual structure.
- [ ] Run existing plus new Hardware page tests to GREEN.
- [ ] Commit page integration with `feat(hardware-inspection): connect page run lifecycle`.

## Task 4: Candidate-Free Journey Fixture

- [ ] Add a deterministic test service that emits the exact seven stages and controlled terminal results without reading the machine or launching anything.
- [ ] Add packaged journey tests for Completed, CompletedWithWarnings, Failed critical, Failed transient, repair-required, Cancelled, retry, and stale old-run emissions.
- [ ] Prove the real page reaches the existing fifteen observable presentation states and preserves disclosure state within a run while resetting it for retry.
- [ ] Prove every terminal action ID, enabled state, announcement, focus target, summary, and details surface comes from the accepted ViewModel snapshot.
- [ ] Run the exact Hardware filter and retain its TRX.
- [ ] Commit fixture tests and any Hardware-only fixture adapter with `test(hardware-inspection): prove run lifecycle journeys`.

## Task 5: Verification and Handoff

- [ ] Run both Python Hardware contract modules.
- [ ] Build app and packaged tests serially with zero errors.
- [ ] Run the exact Hardware packaged filter; require zero non-passing results.
- [ ] Run the complete unfiltered packaged suite; require zero source regressions. If a known Model fixture race recurs, reproduce it three times in isolation and report it separately; do not weaken Hardware or Model assertions.
- [ ] Verify all protected baseline files remain byte-identical to `8a854004` except the explicitly authorized Hardware paths and new plan.
- [ ] Run `git diff --check` and require a clean worktree.
- [ ] Request independent spec and quality review before calling the lifecycle slice complete.

## Completion Boundary

This plan completes the in-app lifecycle and a candidate-free functional journey. It does **not** claim real hardware evidence. Real Windows/DXGI/storage/NPU providers, trusted LLM Fit and llama.cpp process boundaries, evidence resolution/normalization, outcome policy, app-shell navigation, Model handoff integration, and Block 3 compatibility remain subsequent controlled plans. No operational action is authorized by this repository implementation.
