# T1 Cross-Feature Remediation R2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Delegation is disabled for this assignment, so execution remains inline.

**Goal:** Add independently derived, test-only regression coverage for every C0-unresolved cross-feature issue and document native UI-only boundaries for E1.

**Architecture:** Extend only the registered `GraniteEdgeAI.CrossFeature.IntegrationTests` executable with focused test fixtures and compile-linked frozen production seams. Route-equivalent cases are parameterized; route-specific parsing, runtime, artifact, download/export, packaging, and lifetime assertions remain distinct. Expected values are literals or hand-calculated contract values.

**Tech Stack:** C# 12, .NET 8 Windows x64, MSTest with Microsoft.Testing.Platform, PowerShell/Git verification.

**Spec:** User-approved T1 remediation prompt, `docs/audits/2026-08-28/C0-final-audit-reconciliation.md`, and the supplied `02-SCENARIO-MATRIX.md`.

## Global Constraints

- Base is exactly `a5ef3558334e50587889140dafba194853938765` / `90c34ab009b744d7b00866fb93e8dbc86363f1b2`, retaining frozen ancestor `4748fe04f19afdf6b27c4c12502b84db325e7294` / `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`.
- Modify tests, test fixtures, coverage maps, and test documentation only; never production.
- Keep expected values independent of production serializers/calculators.
- Preserve meaningful RED tests for unresolved owner defects; do not weaken them.
- Native/package UI behavior that cannot be observed without UI Automation receives an explicit E1 reason.

---

### Task 1: Cross-route ingress, handoff, compatibility, and memory contracts

**Files:**
- Modify: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests.csproj`
- Create: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/DropRouteTestDoubles.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/IngressRoutePrivacyTests.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/CrossRouteCompatibilityIntegrationTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/ModelInspectionHandoffIntegrationTests.cs`

**Interfaces:**
- Consumes the real selection normalizer/drop handler, Model Inspection v2 codec/projector contracts, and compatibility-core projection types.
- Produces parameterized GGUF/OpenVINO evidence for shared semantics and route-specific assertions.

- [ ] Compile-link the drop handler and quantizer environment policy, with only boundary doubles required to compile unused WinUI overloads.
- [ ] Replace the pseudo-drop test path with a fake `IModelImportDropRequest` through the real `ModelImportDropHandler` and prove picker/drop equality without exposing a path.
- [ ] Add independently constructed route cases for identical v2 wire shape, distinct total/available/reserve/budget values, current-fit/direct Chat, optional optimization, required optimization, no safe configuration, and unknown evidence.
- [ ] Run listing and the focused new tests; characterize already-correct behavior as GREEN and record any product-gap RED by exact name.
- [ ] Commit the task as test-only work.

### Task 2: Execution, artifact, recovery, UI composition, packaging, and fixed-regression contracts

**Files:**
- Create: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/RemediationGapIntegrationTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/PlanAndExecutionIntegrationTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/PersistentOutputRecoveryIntegrationTests.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/QuantizerEnvironmentIsolationIntegrationTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/COVERAGE-MAP.md`

**Interfaces:**
- Consumes exact v3 plans/results, the real output registry, route-specific OpenVINO/GGUF result semantics, app XAML/MSBuild composition artifacts, and the real quantizer environment policy.
- Produces executable regressions or explicit E1-only dispositions for all remaining requirements.

- [ ] Add plan/result mutation tests proving exact selected plan identity reaches execution and stale/duplicate/rollback/retry states cannot publish success.
- [ ] Add persistent/runtime-only consumer assertions: exact GGUF bytes reach Chat/export, OpenVINO runtime-only cannot become file export, and mutable-recency OpenVINO consumption remains RED until Q1/C0 integration lands.
- [ ] Add RED composition regressions for functional bounded/cancellable/integrity-checked/retryable download, bounded verified export, and the unresolved Model Inspection package-item expression.
- [ ] Add shell/footer and disabled-action coverage only where deterministic source/package composition is observable; document keyboard/UI Automation assertions as E1-only when no maintained UIA seam exists.
- [ ] Add quantizer closed-environment characterization and retain GGUF stop/dispose lifetime coverage.
- [ ] Run focused tests, record exact expected RED names, scan for duplicate tests/fixtures, and commit.

### Task 3: Verification, report, review, and handoff

**Files:**
- Modify: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/COVERAGE-MAP.md`
- Create: `docs/audits/2026-08-28/T1-cross-feature-integration-tests-r2.md`
- Replace atomically after completion: `C:/UCL-AUDIT-HANDOFFS/T1.json`

**Interfaces:**
- Consumes all final command results and exact Git/report identities.
- Produces the committed R2 report and schema-valid T1 receipt with `evidenceManifest: null`.

- [ ] Run non-zero discovery, complete cross-feature project, affected compatibility/OpenVINO/GGUF/Model Inspection contracts, privacy scans, duplicate scans, Debug x64 one-worker build, and `git diff --check`.
- [ ] Record exact counts and failed names, separating characterization passes, remediation-required RED, blockers, and E1-only reasons.
- [ ] Review the full base-to-head diff for ownership, oracle independence, privacy, and accidental production changes; correct Critical/Important findings.
- [ ] Commit the report, rerun final verification, push only the R2 branch, compute final tip/tree/report hash/bytes, atomically replace the preserved receipt, and validate it against the supplied schema plus external Git/file identities.
