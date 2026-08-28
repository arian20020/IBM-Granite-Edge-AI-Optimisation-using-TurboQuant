# Q1 Optimisation and Artifact Identity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the exact selected optimisation plan control execution and make its exact verified result control Chat and export, with a strict distinction between persistent artifacts and runtime-only OpenVINO configuration.

**Architecture:** Extend the immutable execution result with attempt/execution and hardware identity, then place all persistent outputs behind a receipt-backed registry and all consumer actions behind typed target resolvers. OpenVINO activation exposes bounded private failure categories; all large-file publication/export operations use cancellable bounded streams and atomic promotion.

**Tech Stack:** C# 13/.NET 8 Windows, WinUI application services, MSTest with Microsoft.Testing.Platform, PowerShell packaging/evidence validators, Git object identity.

**Spec:** `docs/superpowers/specs/2026-08-28-q1-optimisation-artifact-identity-design.md`

## Global Constraints

- Base commit/tree must remain `a5ef3558334e50587889140dafba194853938765` / `90c34ab009b744d7b00866fb93e8dbc86363f1b2`.
- Do not edit shared onboarding/navigation/composition or frontend presentation.
- Do not download models/tools or weaken App Control, package trust, hashes, bounds, or manifest verification.
- Native work requires valid H1 and M1 receipts and H1 to M1 to Q1 lock ordering.
- Every new production behavior follows an observed RED then GREEN cycle.
- Commit phase one is implementation plus tests; phase two is report/evidence bound to the phase-one commit/tree.

---

### Task 1: Exact execution and output contracts

**Files:**
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationExecutionResult.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/OptimizationAttemptContext.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/OptimizationJourneyReducer.cs`
- Test: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/OptimizationExecutionIdentityTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationJourneyReducerTests.cs`

**Interfaces:**
- Consumes: immutable `OptimizationExecutionPlan` and attempt generation.
- Produces: a non-empty `ExecutionId` plus plan/source/hardware/configuration identities on every result, and reducer rejection of any mismatched terminal result.

- [ ] Add tests proving a substituted plan, changed source, changed hardware, wrong execution ID, duplicate result, and late generation cannot become terminal success.
- [ ] Run the focused test executable and observe failures caused by absent execution identity validation.
- [ ] Add the smallest immutable identity fields and validators needed by those tests; thread the execution ID from attempt creation through executors and result factories.
- [ ] Rerun focused tests to GREEN and commit only after the later Q1 tasks are also complete, preserving the required two-phase commit layout.

### Task 2: Typed OpenVINO activation and exact Chat target

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Targets/OptimizationChatTarget.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/OpenVinoActivationResult.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.OpenVino.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/OpenVinoActivationResultTests.cs`
- Test: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/OptimizationTargetIdentityTests.cs`

**Interfaces:**
- Consumes: the exact `OptimizationExecutionResult` and a registry-issued target.
- Produces: `OpenVinoActivationResult` with `Succeeded`, `Cancelled`, or one bounded failure category; persistent and runtime-only typed Chat targets.

- [ ] Add tests proving exceptions are sanitized, cancellation is distinct, stale persistent targets reject, runtime-only configuration reaches session activation, and no ambient directory/configuration is used.
- [ ] Observe RED for the existing boolean/catch-all API and missing typed targets.
- [ ] Implement the bounded result and target types; keep all raw paths/exceptions internal and dispose partial sessions on failure.
- [ ] Rerun the OpenVINO and target tests to GREEN.

### Task 3: Receipt-backed persistent output registry

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/OptimizationCommitReceipt.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/OptimizationOutputLease.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/OptimizationOutputRegistry.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/BoundedFileTransfer.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/Gguf/GgufOptimizationExecutor.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/OpenVino/OpenVinoOptimizationJourneyInfrastructure.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationOutputRegistryTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/BoundedFileTransferTests.cs`

**Interfaces:**
- Consumes: plan, execution ID, generation, immutable source snapshot, bounded output lease.
- Produces: verified GGUF file/OpenVINO package targets keyed by route, plan, execution, configuration, source, hardware, output digest, and byte count.

- [ ] Add tests for same-length mutation, digest/byte/source/plan/execution mismatch, duplicate/late admission, reparse files/directories, aggregate/per-file limits, sparse files, cancellation, rollback, restart recovery, and cleanup.
- [ ] Observe RED against the current plan-only receipt and unbounded resolution APIs.
- [ ] Implement streaming manifest/hash/copy helpers with a fixed buffer; reject reparses before and during traversal; use operation-owned temporary paths and same-volume atomic moves.
- [ ] Remove `LastPublishedDirectory` and register both persistent OpenVINO and GGUF outputs through the same result-bound authority; runtime-only results register no filesystem artifact.
- [ ] Rerun unit and T1 recovery suites to GREEN.

### Task 4: Exact export target and atomic export

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Targets/OptimizationExportTarget.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/OptimizationExporter.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationExporterTests.cs`
- Test: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/OptimizationTargetIdentityTests.cs`

**Interfaces:**
- Consumes: an exact registry-issued persistent target and user-selected destination.
- Produces: verified atomically promoted exported file/package; runtime-only target resolution always fails.

- [ ] Add tests for runtime-only rejection, stale/mutated result rejection, bounded sparse-file copy, cancellation cleanup, destination reparse rejection, digest/length mismatch, and preservation of any existing destination.
- [ ] Observe RED because no result-bound exporter exists.
- [ ] Implement temporary sibling export, streaming verification, atomic promotion without overwrite, and exact cleanup.
- [ ] Rerun export and cross-feature tests to GREEN.

### Task 5: OpenVINO staging and worker/package verification

**Files:**
- Modify only Q1-owned OpenVINO optimization/conversion or packaging files identified by failing tests.
- Test: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoOptimizationTests.cs`
- Test: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/PackagingContractTests.cs`
- Test: `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/**`

**Interfaces:**
- Consumes: exact plan-bound source and an authorized validated worker closure.
- Produces: one operation-owned staging tree, atomic publication/rollback, and a manifest-valid Debug x64 worker stage when prerequisites exist.

- [ ] Add or refine tests proving no redundant source/output copy, source remains read-only, reinspection uses the promoted exact output, and failed/cancelled work leaves no publishable directory.
- [ ] Observe RED before production changes.
- [ ] Collapse redundant copies while retaining a single atomic publication boundary and rollback; do not weaken worker manifest validation.
- [ ] Recheck H1/M1 receipts. If valid, acquire the native lock and run stage/package tests in order; otherwise record the exact blocked prerequisite without fabricating a stage.

### Task 6: Full verification and two-phase evidence

**Files:**
- Create: `docs/audits/2026-08-28/Q1-optimisation-artifact-remediation-r2.md`
- Create: `docs/audits/2026-08-28/evidence/Q1-optimisation-artifact-evidence-v1.json`
- Create or reuse: repository evidence-schema validation command from `10-EVIDENCE-MANIFEST-SCHEMA.json`.

**Interfaces:**
- Consumes: fresh command logs and the exact clean phase-one commit/tree.
- Produces: schema-valid Q1 evidence whose inputs/outputs collectively include `optimizationPlan`, `executionResult`, `chatTarget`, and `exportTarget`, plus a hash/byte-bound handoff receipt.

- [ ] Run OpenVINO contracts/unit/client/process, affected GGUF, T1, target, sparse streaming, cancellation/timeout/stale/rollback/reparse/cleanup/package tests and Debug x64 construction when authorized.
- [ ] Run privacy, duplication, package-content, `git diff --check`, and changed-path scans; independently review every requirement against the committed diff.
- [ ] Commit implementation/tests as phase one and record its exact commit/tree as `evidenceSubjectCommit`/`evidenceSubjectTree`.
- [ ] Generate the report and evidence JSON, validate schema, kind coverage, arithmetic, hashes, byte counts, and privacy; commit them as phase two.
- [ ] Re-run final identity and clean-state verification, push only `audit/ucl-q1-optimisation-remediation-r2`, then atomically replace `C:\UCL-AUDIT-HANDOFFS\Q1.json` with the validated receipt.
