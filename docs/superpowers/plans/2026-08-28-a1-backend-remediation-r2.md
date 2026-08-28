# A1 Backend Architecture Remediation R2 Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Correct the backend-owned A1 architecture findings on the exact C0 base while preserving shared UI, OpenVINO result, and inspection ownership boundaries.

**Architecture:** Move backend decisions into small, testable authorities that existing C0 composition can call. Keep route implementations unchanged, retain fail-closed validation, inject existing clocks into state mutations, and provide an exact C0 wiring proposal instead of editing the shared onboarding or inspection composition files.

**Tech Stack:** C#/.NET 8 and Windows App SDK, MSTest/Microsoft.Testing.Platform, PowerShell, Git.

---

### Task 1: Establish deterministic conversation mutation time

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Services/GgufChatCoordinatorTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/ChatConversation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/GgufChatCoordinator.cs`

1. Add a coordinator test proving streamed and terminal assistant replacements use the injected `TimeProvider` value.
2. Run the focused test against the unmodified implementation and retain the failing result as RED evidence.
3. Require an explicit UTC update time in `ChatConversation.ReplaceMessage` and pass `clock.GetUtcNow()` from every coordinator call site.
4. Run the focused test and the GGUF chat/coordinator coverage GREEN.

### Task 2: Consolidate compatibility fallback decisions

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityFallbackPolicy.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEngine.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Presentation/CompatibilityFallbackPolicyTests.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/CompatibilityEvaluationOrchestrator.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CompatibilityEvaluationOrchestratorTests.cs`

1. Add tests proving non-cancellation faults become the fail-closed projection at an injected time and cancellation is never swallowed.
2. Run the focused tests before the policy exists and retain the compile/test failure as RED evidence.
3. Implement one generic policy boundary and route both production and adapter fallback paths through it.
4. Extract the shell's repeated capture/evaluate/fallback sequence into one backend orchestrator with an injected clock and cancellation-preserving fallback.
5. Remove the ambient clock from fallback-result creation and run focused plus full compatibility tests GREEN.

### Task 3: Centralize official OpenVINO worker authority

**Files:**
- Create: `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/OpenVinoOfficialWorkerAuthority.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/OpenVinoOfficialWorkerAuthorityTests.cs`

1. Add tests for exact protocol, executable, build evidence, closed binary inventory, validation, immutable caller exposure, and invalid manifest-digest rejection.
2. Run the focused tests before the authority exists and retain RED evidence.
3. Implement a single factory that constructs and validates the official installation without duplicating resolver, hashing, or PE-verification behavior.
4. Run focused and full worker-client tests GREEN.
5. Document the exact C0-owned `ModelInspectionServiceComposition` replacement; do not edit it.

### Task 4: Extract backend optimization route composition

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/OptimizationBackendCompositionFactory.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationBackendCompositionFactoryTests.cs`

1. Add tests proving exact GGUF/OpenVINO route selection, missing-authority refusal, no cross-route fallback, and dependency validation.
2. Run focused tests against the unmodified C0 base and retain RED evidence.
3. Implement the smallest factory that assembles existing executors, revalidators, context factories, output registry, and journey coordinator; do not duplicate route implementations.
4. Run focused optimization tests GREEN.
5. Document the exact C0-owned onboarding wiring replacement; do not edit the onboarding shell.

### Task 5: Verification, commits, report, and receipt

**Files:**
- Create: `docs/audits/2026-08-28/A1-backend-architecture-clean-code-r2.md`
- Replace externally and atomically at the end: `C:/UCL-AUDIT-HANDOFFS/A1.json`

1. Run all affected standalone unit, contract, and cross-feature projects with TRX output and non-zero discovered/executed totals.
2. Run the WinUI unit host where the available SDK and packaging environment permit; record any independently reproduced infrastructure limitation without unsupported pass claims.
3. Run `git diff --check`, warning-as-error builds, duplicate-symbol/registration scans, and private-path/secret scans.
4. Self-review the final diff because agent delegation is not authorized in this session; correct confirmed issues and rerun affected checks.
5. Create focused implementation/test commits and a final report commit, then verify a clean branch.
6. Push only `audit/ucl-a1-remediation-r2`; if unavailable, create and verify the prescribed bundle.
7. Compute report bytes/hash and final commit/tree externally, construct the schema-compliant receipt with `evidenceManifest: null`, validate it, and atomically replace `A1.json` only after every handoff fact is final.
