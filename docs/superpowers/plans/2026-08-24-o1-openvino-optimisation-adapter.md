# O1 OpenVINO Optimisation Adapter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing OpenVINO optimisation pipeline execute C1's exact capability-bound plan instead of a fixed per-mode registry.

**Architecture:** Preserve the proven OpenVINO transaction, package snapshot, validation, smoke, provenance, reinspection, rollback, and atomic-publication pipeline. Add a strict C1 payload adapter and capability projector, then remove executor-side mode interpretation. Standard released options remain available; TurboQuant TBQ4/TBQ3 enter capability evidence only when the exact route is independently admitted.

**Tech Stack:** C# 13/.NET 8, existing OpenVINO route/contracts, Python converter worker, NNCF/OpenVINO validation, MSTest/Microsoft.Testing.Platform, native worker evidence tests.

**Spec:** `docs/superpowers/specs/2026-08-24-cross-route-optimisation-contract-design.md`

## Global Constraints

- Branch: `feature/openvino-optimisation-adapter-v1`, based on accepted `feature/openvino-route` plus the exact C1 contract commit.
- Own OpenVINO optimisation paths and component tests only. Do not edit optimisation XAML, C1 types, GGUF, navigation, or shared resources.
- Preserve existing source snapshot, staging, validation, smoke test, provenance, rollback, reinspection, and atomic publication.
- Remove fixed objective lookup from accepting execution. Never remap a preference or substitute a candidate.
- Unknown or changed capability returns `ReplanRequired`; experimental absence is resolved by C1 replanning to released OpenVINO capability.
- `OpenVinoOptimizationAdaptation` is defined beside its adapter; every `*TestData`, `PackageFixture`, or fixture service used in snippets is a private helper in the named test file.

---

### Task 1: Project OpenVINO capability evidence for C1

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationCapabilityProjector.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationCandidate.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoOptimizationCapabilityProjectorTests.cs`

**Interfaces:**
- Consumes: installed OpenVINO/tool/device evidence and admitted route records
- Produces: `OpenVinoCapabilityPayload` for C1; no mode winners

- [ ] **Step 1: Write the failing capability projection test**

```csharp
[TestMethod]
public void ProjectorReportsCandidatesWithoutSelectingAMode()
{
    OpenVinoCapabilityPayload payload =
        OpenVinoOptimizationCapabilityProjector.Project(
            OpenVinoCapabilityTestData.StandardCpuEvidence());
    Assert.IsTrue(payload.Entries.Count >= 4);
    Assert.IsTrue(payload.Entries.All(static entry =>
        !string.IsNullOrWhiteSpace(entry.EvidenceId)));
    Assert.IsFalse(payload.Entries.Any(static entry => entry.Preference is not null));
}
```

- [ ] **Step 2: Run RED**

Run the OpenVINO unit project filtered to the new class. Expected: projector absent.

- [ ] **Step 3: Implement evidence-only projection**

Project proven original/FP16/INT8/INT4 weights, released default/F16/BF16/U8/U4 cache values only where supported, exact devices/runtime controls, maturity, versions, and evidence IDs. Do not claim unavailable enum members merely because the design lists possible coverage. Extend enums only with combinations backed by current code/evidence and tests.

- [ ] **Step 4: Run GREEN and commit**

Commit as `feat(openvino): project optimisation capabilities`.

### Task 2: Replace fixed mode lookup with an exact plan adapter

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationPlanAdapter.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationCandidate.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationService.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoOptimizationPlanAdapterTests.cs`

**Interfaces:**
- Consumes: `OptimizationExecutionPlan` containing the sealed OpenVINO payload
- Produces: exact `OpenVinoOptimizationCandidate`; drift is typed `ReplanRequired`

- [ ] **Step 1: Write the failing no-substitution test**

```csharp
[TestMethod]
public void AdapterRejectsCapabilityDigestDrift()
{
    OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
    OpenVinoCapabilityPayload current =
        OpenVinoOptimizationTestData.CapabilitiesWithDifferentDigest();
    OpenVinoOptimizationAdaptation result =
        OpenVinoOptimizationPlanAdapter.Adapt(plan, current);
    Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.ReplanRequired,
        result.Status);
    Assert.IsNull(result.Candidate);
}
```

- [ ] **Step 2: Run RED**

Expected: service still accepts only `OpenVinoOptimizationRegistry` members by legacy objective.

- [ ] **Step 3: Implement strict mapping**

Map weight, cache, device, compiled-cache, context, streams, and performance hint exhaustively. Validate plan/configuration/capability hashes and evidence ID. Remove `Objective` as an accepting execution input; retain a versioned legacy adapter only for existing migration tests. No `GetRequired(objective)` call remains on the new execution path.

- [ ] **Step 4: Run GREEN and commit**

Commit as `feat(openvino): execute exact C1 plans`.

### Task 3: Bind the existing transaction to plan identity

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationService.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationProvenance.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/SealedOpenVinoOptimizationPipeline.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoOptimizationTests.cs`

**Interfaces:**
- Consumes: confirmed immutable plan and exact adapted candidate
- Produces: plan-bound OpenVINO result and provenance

- [ ] **Step 1: Write the failing provenance binding test**

```csharp
[TestMethod]
public async Task PublishedProvenanceBindsPlanAndConfiguration()
{
    using PackageFixture package = PackageFixture.Create();
    OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
    OptimizationExecutionResult result = await package.Service.ExecuteAsync(
        plan, null, CancellationToken.None);
    OpenVinoOptimizationProvenance provenance =
        OpenVinoOptimizationProvenance.Read(result.PublishedDirectory!);
    Assert.AreEqual(plan.OptimizationPlanId, provenance.OptimizationPlanId);
    Assert.AreEqual(plan.ConfigurationSha256, provenance.ConfigurationSha256);
}
```

- [ ] **Step 2: Run RED**

Expected: provenance has operation/configuration data but not the C1 plan binding.

- [ ] **Step 3: Extend, do not replace, the proven lifecycle**

Add plan/capability/model/hardware identity digests to provenance and revalidate them immediately before staging and publish. Preserve call order and cleanup. Runtime-only candidates store a bound profile and skip persistent conversion. Return C1 terminal statuses without exposing paths or raw tool output.

- [ ] **Step 4: Run GREEN and commit**

Run existing `OpenVinoOptimizationTests`, conversion tests, inspection tests, and end-to-end optimisation tests. Commit as `feat(openvino): bind optimisation provenance`.

### Task 4: Gate experimental TurboQuant and preserve official fallback planning

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/TurboQuant/TurboQuantActivationState.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationCapabilityProjector.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/TurboQuant/TurboQuantRouteAdapterTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoExperimentalCapabilityTests.cs`

**Interfaces:**
- Consumes: exact native activation evidence and admitted hashes/versions
- Produces: TBQ capability entry or no entry; never a post-confirmation silent fallback

- [ ] **Step 1: Write the failing evidence-absence test**

```csharp
[TestMethod]
public void MissingActivationEvidencePublishesNoTurboQuantCandidate()
{
    OpenVinoCapabilityPayload payload =
        OpenVinoOptimizationCapabilityProjector.Project(
            OpenVinoCapabilityTestData.WithoutTurboQuantEvidence());
    Assert.IsFalse(payload.Entries.Any(static entry => entry.IsExperimental));
}
```

- [ ] **Step 2: Run RED**

Expected: projector does not yet make this absence invariant explicit.

- [ ] **Step 3: Implement exact experimental admission**

Require the worker, source, binary, runtime, device, format, activation, and evidence identities expected by existing TurboQuant controls. If absent, omit TBQ entries so C1 selects official OpenVINO candidates. If evidence changes after planning, return `ReplanRequired`.

- [ ] **Step 4: Run GREEN, native evidence scripts, and commit**

Run TurboQuant unit/native contract tests and `Test-OpenVinoTurboQuantActivation.ps1`. Commit as `fix(openvino): gate experimental optimisation capabilities`.

### Task 5: Run full route regression and hand off

**Files:**
- Create: `docs/handoffs/2026-08-24-o1-openvino-optimisation-adapter.md`

**Interfaces:**
- Consumes: clean O1 branch and fresh route test evidence
- Produces: service factory/result/capability handoff; no UI

- [ ] **Step 1: Run route verification**

Run OpenVINO contracts, unit, worker-client, integration, converter-isolation, stable-route, protocol-containment, and applicable PowerShell closure tests. Record exact totals and environmental skips.

- [ ] **Step 2: Verify fixed lookup is absent from the accepting path**

Use `rg -n "GetRequired\(|OpenVinoOptimizationObjective"` over production optimisation files. Any remaining occurrence must be legacy migration code unreachable from the new plan execution path and covered by a rejection test.

- [ ] **Step 3: Record and commit handoff**

Record base/tip, C1 contract commit, paths, tests, exact capability/result types, preserved lifecycle calls, experimental nonclaims, and no-XAML scope. Commit as `docs(openvino): hand off optimisation adapter`. Do not push or merge.
