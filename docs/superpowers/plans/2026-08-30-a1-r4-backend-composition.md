# A1 R4 Backend Composition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish A1's backend composition cleanup with unique production seams, cancellation-correct compatibility behavior, and one awaitable idempotent GGUF Chat retirement owner.

**Architecture:** Preserve the R3 route authorities and production callers. Narrow the compatibility boundary so only known resource-acquisition failures become the existing fail-closed screen while cancellation and programming defects escape; make `ChatDemoController` own a single cached retirement task that every navigation/shutdown caller awaits. Strengthen fitness tests with behavioral entry-point coverage and a whole-production registration scan, without changing the shared shell, schemas, XAML, projects, package manifests, H1 facts, or Q1 results.

**Tech Stack:** C# 13, .NET 10 pinned SDK, WinUI 3, MSTest/Microsoft Testing Platform, PowerShell, Git.

---

## Requirement-to-evidence map

| R4 requirement | Production owner / live caller | Baseline or RED | Final evidence |
|---|---|---|---|
| Unique compatibility orchestration | `CompatibilityEvaluationOrchestrator`; shell compatibility navigation | R3 has one source registration | Whole-production structural scan plus composed behavioral tests |
| Unique route-exact optimization factory | `OptimizationBackendCompositionFactory`; shell optimization navigation | R3 factory behavior passes | Factory route/missing/duplicate tests plus whole-production scan |
| Unique official OpenVINO worker authority | `OpenVinoOfficialWorkerAuthority`; `ModelInspectionServiceComposition` | R3 authority tests pass | WorkerClient suite, composition filter, whole-production scan |
| Cancellation and programming-fault correctness | `CompatibilityEvaluationOrchestrator`, `CompatibilityFallbackPolicy` | cancellation passes; programming fault is currently swallowed | RED tests for evaluator/core programming fault propagation; GREEN focused and full compatibility suites |
| One GGUF Chat lifetime owner | `ChatDemoController`; two production launchers and shell teardown | second concurrent disposal returns early | RED concurrency test; repeated GREEN lifetime suite; cross-feature suite |
| Exact C0 seams and collision rules | existing orchestrator/factory/service composition/controller | caller inventory from R3 | report method-level retain/replace instructions and changed-path scan |
| Warning inventory and managed/package gates | all A1-changed paths | clean managed baseline; package stage externally absent | Debug/Release x64 builds, package fail-closed result, warning ledger |
| Runtime/screenshot gate | exact built app | non-authoritative host; prerequisites must be checked | bounded launch/smoke/screenshots if activatable; otherwise exact managed/package blocker and non-claims |

### Task 1: Make compatibility failure ownership explicit

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CompatibilityEvaluationOrchestratorTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Presentation/CompatibilityFallbackPolicyTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/CompatibilityEvaluationOrchestrator.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityFallbackPolicy.cs`

- [ ] **Step 1: Write behavioral RED tests**

Add a test whose evaluator throws `InvalidOperationException` and assert it escapes, while preserving the existing capture-unavailability and cancellation cases. Change the core fallback policy test to assert that programming faults propagate:

```csharp
await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
    orchestrator.EvaluateAuthorityAsync(optedIn, ThrowProgrammingFault, CancellationToken.None));

Assert.ThrowsExactly<InvalidOperationException>(() =>
    CompatibilityFallbackPolicy.Execute(
        () => throw new InvalidOperationException("programming defect"),
        CompatibilityFallbackPolicy.NotEstablished));
```

- [ ] **Step 2: Run the exact filters and verify genuine RED**

Run the direct compatibility executable and packaged unit filter. Expected: the new programming-fault assertions fail because R3 returns fallback; discovery must be non-zero and compilation must succeed.

- [ ] **Step 3: Apply the minimal boundary correction**

Split fresh-resource capture from evaluation. Convert only known capture unavailability (`IOException`, `UnauthorizedAccessException`, and the current H1 source's `InvalidOperationException`) to fallback, rethrow `OperationCanceledException`, and invoke evaluators outside the catch. Change `CompatibilityFallbackPolicy.Execute` to stop catching arbitrary exceptions so typed compatibility results remain the operational-failure mechanism.

```csharp
private async Task<CompatibilityFreshResourcesInput?> CaptureFreshAsync(
    CancellationToken cancellationToken)
{
    try { return await _freshResourcesSource.CaptureAsync(cancellationToken); }
    catch (OperationCanceledException) { throw; }
    catch (Exception error) when (error is IOException
        or UnauthorizedAccessException
        or InvalidOperationException) { return null; }
}
```

- [ ] **Step 4: Run GREEN and the complete compatibility suite**

Expected: focused tests pass; direct executable discovers at least the new total and reports zero failures/skips.

- [ ] **Step 5: Mutation-check and commit**

Temporarily restore the broad evaluator catch, prove the programming-fault regression fails, restore GREEN, then commit the tests and minimal production changes.

### Task 2: Make GGUF Chat retirement one awaitable task

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/GgufChatSessionLifecycleIntegrationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/A1BackendProductionReachabilityTests.cs`

- [ ] **Step 1: Write the lifecycle RED**

Create a controller-level or extracted lifetime-owner test with a blocked operation, call retirement twice, and prove both returned tasks stay incomplete until the operation releases and the session is disposed exactly once. Add a structural assertion that both production GGUF launch paths return the same `ChatDemoController` lifetime owner and that no alternate controller factory exists.

- [ ] **Step 2: Run and verify RED**

Expected: the second retirement completes early on R3 while the first remains blocked; non-zero discovery and successful compilation are mandatory.

- [ ] **Step 3: Cache a single retirement task**

Replace the early-return `disposed` pattern with a lock-protected cached task. Every `DisposeAsync` call returns the same retirement operation; the operation cancels controller work, detaches events, requests stop when required, awaits all tracked tasks, disposes the render scheduler/session/token source once, and does not swallow programming defects.

```csharp
public ValueTask DisposeAsync()
{
    lock (operationSync)
    {
        retirementTask ??= RetireCoreAsync();
        return new ValueTask(retirementTask);
    }
}
```

- [ ] **Step 4: Run GREEN three times without sleeps**

Run the focused lifecycle filter three times, then the complete cross-feature executable. Expected: identical pass counts, zero failures/skips, and exact once-only disposal.

- [ ] **Step 5: Mutation-check and commit**

Temporarily restore early return for the second call, verify RED, restore the cached task, rerun GREEN, and commit.

### Task 3: Strengthen production reachability and C0 seams

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/A1BackendProductionReachabilityTests.cs`
- Modify only if behavioral proof reveals a gap: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Services/ModelInspectionServiceComposition.cs`
- Modify only if behavioral proof reveals a gap: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/OptimizationBackendCompositionFactory.cs`

- [ ] **Step 1: Expand the production scan**

Enumerate all production `.cs` files under the app/infrastructure/shared roots and assert exactly one registration for the compatibility orchestrator, optimization factory, official worker installation authority, and production Chat controller factory. Assert zero direct official installation construction and zero ambient `LastPublishedDirectory` use in A1-owned composition types; retain the known C0-owned shell calls only in the handoff ledger.

- [ ] **Step 2: Pair structural checks with real seams**

Run existing orchestrator, route-factory, official worker authority, model-inspection composition, and Chat lifecycle behaviors so source counts are never the sole oracle.

- [ ] **Step 3: Run focused GREEN and commit**

Expected: all A1 reachability/cancellation filters pass with non-zero discovery; no production change unless a real composition gap was demonstrated.

### Task 4: Managed build, package, runtime, and visual verification

**Files:**
- Create only local ignored artifacts under `TestResults/R4-A1/`

- [ ] **Step 1: Run all required managed suites**

Run direct Microsoft Testing Platform executables for complete compatibility, CrossFeature, OpenVINO WorkerClient, and Model Inspection contracts; run the packaged WinUI recipe filters for A1 reachability/cancellation, Model Inspection composition, and GGUF lifetime. Record discovered/executed/passed/failed/skipped arithmetic and TRX SHA-256 values.

- [ ] **Step 2: Build Debug and Release x64**

Use the pinned SDK and repository-supported package-independent settings. Record warnings by file and separate A1-owned warnings from inherited warnings; use no suppression.

- [ ] **Step 3: Run the strongest production package gate**

Keep all external producer requirements enabled. Treat the known absent validated native stage as a fail-closed external block, not acceptance.

- [ ] **Step 4: Attempt the exact built-app smoke and screenshot gate**

Check package activation prerequisites first. If activation is permitted, launch one owned app instance, execute the bounded import-to-compatibility-to-Chat/optimization journey, capture privacy-safe normal/reduced/scaled states, inspect full-resolution images, and close only that instance. If blocked, record the exact prerequisite and make no visual/native/performance claim.

- [ ] **Step 5: Run hygiene scans**

Run `git diff --check`, duplicate-definition/registration scans, sensitive-data/private-path scans, process/listener/temp cleanup checks, and confirm only authorized paths changed.

### Task 5: Review, durable handoff, and remote proof

**Files:**
- Create: `docs/audits/2026-08-30/A1-backend-composition-r4.md`
- Create: `docs/audits/2026-08-30/handoffs/R4-A1.json`

- [ ] **Step 1: Run requirements and adversarial reviews**

Review every matrix row, then independently inspect the complete R4 diff for duplicate authority, races, swallowed cancellation/programming faults, unbounded I/O, privacy leaks, misleading evidence, and ownership collisions. Fix all Critical/Important findings and rerun affected gates.

- [ ] **Step 2: Commit the immutable implementation subject**

Record its commit/tree and do not amend it after evidence collection. A1's evidence manifest is explicitly `null` under schema v2.

- [ ] **Step 3: Write and hash the final report**

Include exact base/subject identities, commit list, changed paths, live callers/registrations, test arithmetic, build/package/runtime disposition, screenshot ledger or blocker, inherited warnings, C0 method-level merge rules, and Intel-native/performance non-claims.

- [ ] **Step 4: Create and validate `R4-A1.json`**

Use schema version 2, bind the immutable implementation subject, hash the finalized report, set `evidenceManifest` to `null`, use exact test totals, and set the honest native disposition. Validate against the supplied v2 schema without committing a schema copy.

- [ ] **Step 5: Commit, push, and independently prove remote equality**

Push `audit/ucl-a1-remediation-r4`; fetch its exact remote ref; prove the remote tip contains the receipt/report blobs, descends from the implementation subject, and matches the local final tip/tree. Record this external proof in the final handoff response rather than making the receipt self-referential.
