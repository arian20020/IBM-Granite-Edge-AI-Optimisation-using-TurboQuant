# H1 Hardware Remediation R2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the C0 H1 hardware findings with typed memory roles, deterministic safety budgeting, stable snapshot identity, controlled probe packaging and schema-valid evidence.

**Architecture:** Keep capture and process containment intact. Add role-specific values at the Hardware Inspection and compatibility boundaries, centralize budget arithmetic, bind downstream authority to a canonical snapshot digest, and convert probe package inclusion to resolved MSBuild task outputs.

**Tech Stack:** C# 13, .NET 8/10 SDK tooling, MSTest/Microsoft.Testing.Platform, MSBuild, PowerShell, JSON Schema, Git.

**Spec:** `docs/superpowers/specs/2026-08-28-h1-hardware-remediation-r2-design.md`

## Global Constraints

- Base commit/tree: `a5ef3558334e50587889140dafba194853938765` / `90c34ab009b744d7b00866fb93e8dbc86363f1b2`.
- Frozen ancestor/tree: `4748fe04f19afdf6b27c4c12502b84db325e7294` / `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`.
- Do not edit shared onboarding/navigation/composition or Model Inspection/OpenVINO behavior.
- Do not download assets, change machine policy, install certificates or weaken hashes.
- Every production behavior change begins with an observed failing test.
- Raw native output, usernames, hostnames and local paths remain outside Git.

---

### Task 1: Type and centralize system-memory budgeting

**Files:**
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEvaluation.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityProductionInput.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEngine.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/FitAssessment/FitPolicy.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/FitAssessment/SystemMemoryBudgetCalculator.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/FitAssessment/SystemMemoryBudgetCalculatorTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Presentation/CompatibilityProductionInputTests.cs`

**Interfaces:**
- Consumes: installed and fresh available byte observations.
- Produces: `TotalPhysicalMemory`, `CurrentlyAvailableMemory`, `ProportionalSafetyReserve`, `ExecutableModelBudget`, and one coherent budget result.

- [ ] Add literal boundary tests for zero, floor, proportional, exact division, `ulong.MaxValue`, reserve bounding and role substitution.
- [ ] Run focused tests and record failures caused by the missing types/calculator.
- [ ] Implement immutable role types and integer-only calculator.
- [ ] Route fit, evaluation and issuance through the calculator; remove duplicated reserve arithmetic.
- [ ] Run focused tests and the complete compatibility suite.

### Task 2: Bind the canonical hardware snapshot identity

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Domain/HardwareSnapshotIdentity.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Domain/HardwareSnapshot.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Application/HardwareInspectionHandoff.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/GgufCompatibilityInputProjector.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/OpenVinoCompatibilityInputProjector.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionContractTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/GgufCompatibilityInputProjectorTests.cs`

**Interfaces:**
- Consumes: normalized `HardwareSnapshot` facts and safe evidence provenance.
- Produces: `HardwareSnapshot.Identity.Sha256`, retained verbatim by prepared compatibility inputs.

- [ ] Add tests proving deterministic digesting, mutation sensitivity, no canonical/raw payload exposure, UUID binding and exact downstream propagation.
- [ ] Run focused tests and record missing-identity/mismatched-digest failures.
- [ ] Implement length-prefixed canonical hashing and handoff UUID validation.
- [ ] Replace partial downstream snapshot digest reconstruction with the H1 digest.
- [ ] Build the WinUI test project and run every executable boundary test available on the host.

### Task 3: Resolve and constrain hardware package items

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/HardwareInspection.LlamaCppProbePackaging.targets`
- Modify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Create: `tests/PowerShell/HardwareInspectionPackaging.Tests.ps1`
- Modify: `tests/PowerShell/HardwareInspectionTrust.Tests.ps1`

**Interfaces:**
- Consumes: verified, resolved probe files and manifest.
- Produces: flat controlled `Content` items with fixed package target paths.

- [ ] Add Pester/static-evaluation tests that reject unresolved package-relevant expressions and H1 audit/reference/debug/private content.
- [ ] Run the focused Pester tests and record the current unresolved expression and debug-gallery failures.
- [ ] Emit package content through resolved MSBuild task outputs and remove H1 debug fixture/gallery inclusion from app/test packages.
- [ ] Evaluate Debug x64, Release x64, x86 and AnyCPU graphs and scan resolved package items.
- [ ] Build Debug x64 packaging targets without supplying unrelated unavailable OpenVINO stages; report fail-closed external gates separately.

### Task 4: Preserve bounded process and manifest behavior

**Files:**
- Test only unless a defect is reproduced: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/**`
- Test only unless a defect is reproduced: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests/**`
- Test only unless a defect is reproduced: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Processes/**`

**Interfaces:**
- Consumes: existing trusted manifests and bounded process runner.
- Produces: non-zero verification of timeout, cancellation, cleanup and fail-closed behavior.

- [ ] Run focused timeout/cancellation/cleanup and manifest-tamper tests.
- [ ] Run Foundation, probe, fake-tool and relevant boundary suites.
- [ ] If a defect appears, add one failing regression before the minimal owned fix.

### Task 5: Freeze the evidence subject and verify

**Files:**
- No evidence/report files in the implementation commit.

**Interfaces:**
- Produces: exact `evidenceSubjectCommit` and `evidenceSubjectTree`.

- [ ] Run focused RED/GREEN suites, full Foundation, probe, compatibility and relevant contract-boundary tests.
- [ ] Run package/static evaluation, privacy/content/duplication scans and `git diff --check`.
- [ ] Commit implementation and tests; record the exact commit/tree.
- [ ] Re-run every scoped gate from the committed tree and record command arithmetic and result hashes.
- [ ] Acquire the native lock only if the protocol and host permit it; otherwise record the exact blocker and verify no orphan H1 process.

### Task 6: Publish evidence, report and receipt

**Files:**
- Create: `docs/audits/2026-08-28/H1-hardware-remediation-r2.md`
- Create: `docs/audits/2026-08-28/evidence/H1-hardware-evidence-v1.json`
- Publish atomically: `C:\UCL-AUDIT-HANDOFFS\H1.json`

**Interfaces:**
- Consumes: evidence-subject identities and verified command results.
- Produces: schema-v1 manifest, report, pushed branch and schema-v1 handoff receipt.

- [ ] Write the report with exact results, blockers and nonclaims.
- [ ] Write the evidence manifest with stable output kinds `hardwareSnapshot`, `availableMemory` and `safetyBudget`.
- [ ] Validate manifest JSON Schema and command arithmetic.
- [ ] Commit report and manifest separately; verify final clean tip/tree.
- [ ] Push only `audit/ucl-h1-hardware-remediation-r2`.
- [ ] Hash exact report/manifest bytes, validate the receipt schema, atomically publish `H1.json`, then independently re-read and verify it.
