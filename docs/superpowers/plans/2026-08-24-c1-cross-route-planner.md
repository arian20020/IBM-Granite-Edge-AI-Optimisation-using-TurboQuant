# C1 Cross-Route Optimisation Planner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generalise C1's existing GGUF compatibility planner into the sole route-neutral selector and immutable-plan issuer for GGUF and OpenVINO.

**Architecture:** Keep route configurations sealed and strongly typed while comparing them through shared normalised metrics. Retain the pure generator/estimator/selector style, replace the old four-mode enum with Automatic plus the exact five manual bands, and expose immutable execution contracts from the existing compatibility core project.

**Tech Stack:** C# 13, .NET 8, MSTest 4/Microsoft.Testing.Platform, SHA-256 canonical descriptors, existing `GraniteEdgeAI.ModelHardwareCompatibility.Core` policies.

**Spec:** `docs/superpowers/specs/2026-08-24-cross-route-optimisation-contract-design.md`

## Global Constraints

- Work on `feature/cross-route-optimisation-contracts-v1` from the latest accepted `feature/model-hardware-compatibility`; preserve commits after audited `699c4826...`.
- Own only the compatibility core, its standalone tests, and the C1 handoff. Do not edit XAML, executors, navigation, or shared integration files.
- Preserve exact seam names: `modelInspectionRunId`, `modelInspectionHandoffId`, `modelSha256`, `modelLengthBytes`, and `productHardwareRunId`.
- Unknown capability, estimate, evidence, identity, or enum input fails closed.
- Selectors remain pure: no I/O, clock read, path inference, mutable aliases, or executor lookup.
- Every `*TestData` name in snippets is a private nested builder created in that task's named test file; it is not an additional production contract.

---

### Task 1: Implement the approved preference vocabulary

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationPreferenceSelection.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationPreferenceLabelPolicy.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/CompatibilityMode.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Optimization/OptimizationPreferenceSelectionTests.cs`

**Interfaces:**
- Consumes: separate Automatic choice or manual integer 0-100
- Produces: `OptimizationPreferenceSelection`, `OptimizationPreferenceBand`, and exact labels

- [ ] **Step 1: Write the failing boundary test**
- [ ] **Step 2: Run RED** - expected: missing preference types
- [ ] **Step 3: Implement the closed types** - retire `Quality` and `Efficiency` from new serialization. A legacy adapter may read existing in-memory values; it must not expose old labels.
- [ ] **Step 4: Run GREEN and commit** as `feat(compatibility): define optimisation preferences`

### Task 2: Add sealed OpenVINO route capabilities

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/OpenVino/OpenVinoWeightFormat.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/OpenVino/OpenVinoKvCacheFormat.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/OpenVino/OpenVinoRouteConfiguration.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Capabilities/OpenVinoCompatibilitySupportEntry.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationCapabilitySnapshot.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/OpenVino/OpenVinoRouteConfigurationTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Optimization/OptimizationCapabilitySnapshotTests.cs`

**Interfaces:**
- Consumes: explicitly admitted OpenVINO combinations
- Produces: exactly one sealed `GgufCapabilityPayload` or `OpenVinoCapabilityPayload`

- [ ] **Step 1: Write the failing union test**
- [ ] **Step 2: Run RED** - expected: missing types
- [ ] **Step 3: Implement route records** - `OpenVinoRouteConfiguration` derives from `RouteConfiguration` and includes weight, KV cache, device, context, compiled-cache policy, streams, and performance hint in its ordinal canonical descriptor. Factories reject unknown enums, empty evidence IDs, invalid context bounds, malformed lowercase SHA-256, and route/payload mismatch. Clone input collections.
- [ ] **Step 4: Run GREEN and commit** as `feat(compatibility): add OpenVINO capability model`

### Task 3: Generalise candidate generation and complete estimation

**Files:**
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/CandidateGenerationRequest.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/CandidateGenerator.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/CompatibilityCandidate.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/OpenVino/OpenVinoResourceEstimator.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationCandidateMetrics.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Candidates/CrossRouteCandidateGeneratorTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/OpenVino/OpenVinoResourceEstimatorTests.cs`

**Interfaces:**
- Consumes: capability snapshot, inspected facts, safe budget, workload, baseline, trusted source
- Produces: complete immutable candidates and normalised metrics

- [ ] **Step 1: Write the failing shared-memory test**
- [ ] **Step 2: Run RED** - expected: missing OpenVINO estimator/candidate metrics
- [ ] **Step 3: Implement exhaustive route dispatch** - extend `ResourcePhaseComposer`; do not estimate weights alone. Metrics contain evidence grade, quality, performance, stability, context, peak bytes, safe budget, headroom, working disk, output disk, and persistent-change flag. Unknown mandatory values create a typed exclusion, never zero.
- [ ] **Step 4: Run GREEN, invariants, and commit** as `feat(compatibility): generate cross-route candidates`

### Task 4: Select from one deterministic safe frontier

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/SafeCandidateFrontier.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/ModeSelector.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/ModeComparers.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ModeSelection/SafeCandidateFrontierTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/OptimizationPreferenceInvariantTests.cs`

**Interfaces:**
- Consumes: evaluated admitted candidates and preference
- Produces: winner fingerprint and truthful explanation; equal adjacent bands permitted

- [ ] **Step 1: Write failing frontier tests**
- [ ] **Step 2: Run RED** - expected: old selector has four legacy modes and no frontier
- [ ] **Step 3: Implement all six choices** - filter hard failures, remove dominated complete configurations, and rank with deterministic ties: evidence, headroom, no persistent conversion, stability, workload fit, canonical hash. Automatic uses this same frontier with the conversion penalty. Five manual bands use the approved semantics; no fabricated candidates.
- [ ] **Step 4: Run GREEN and property tests; commit** as `feat(compatibility): select safe optimisation frontier`

### Task 5: Issue immutable plans and typed terminal results

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationExecutionPlan.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationExecutionResult.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationPlanIssuer.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationCanonicalizer.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Optimization/OptimizationExecutionPlanTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/OptimizationPlanBindingTests.cs`

**Interfaces:**
- Consumes: current seam identities, capability snapshot, workload, selected candidate, preference
- Produces: exact immutable executor input and one of five terminal statuses

- [ ] **Step 1: Write the failing binding test**
- [ ] **Step 2: Run RED** - expected: plan/result types absent
- [ ] **Step 3: Implement canonical factories** - reject empty UUIDs, malformed hashes, nonpositive `ModelLengthBytes`, route/payload mismatch, unknown enums, and missing evidence. Canonical UTF-8 uses ordinal field ordering and invariant numbers; it excludes paths and free text. Define `OptimizationExecutionStatus` with exactly `SucceededPersistent`, `SucceededRuntimeProfile`, `Cancelled`, `Failed`, and `ReplanRequired`, and require every `OptimizationExecutionResult` to carry one defined member.
- [ ] **Step 4: Run full C1 GREEN and commit** as `feat(compatibility): issue bound optimisation plans`

### Task 6: Freeze the C1 handoff

**Files:**
- Create: `docs/handoffs/2026-08-24-c1-cross-route-optimisation-contracts.md`

**Interfaces:**
- Consumes: clean C1 branch and fresh test evidence
- Produces: exact commit required by G1, O1, and UO1

- [ ] **Step 1: Verify scope** - resolve the exact recorded starting SHA into `$c1Base`, then run `git diff --check $c1Base..HEAD`, `git status --short`, and `git diff --name-only $c1Base..HEAD`. Record the resolved 40-character SHA in the handoff. Expected: only C1 paths and a clean worktree.
- [ ] **Step 2: Record exact public contracts** - base/tip SHA, commits, paths, test counts, public types, serialized names, statuses, exact labels, and the non-claim that no executor or UI was implemented.
- [ ] **Step 3: Commit the handoff** as `docs(compatibility): hand off optimisation contracts`. Do not push or merge.
