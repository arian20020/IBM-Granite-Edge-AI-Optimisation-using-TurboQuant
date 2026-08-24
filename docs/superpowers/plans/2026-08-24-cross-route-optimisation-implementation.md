# Cross-Route Model Optimisation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver one PC-aware optimisation journey that plans consistently across GGUF and OpenVINO, executes exact validated route plans, preserves the source model, and ends with a modern Chat-or-Save choice.

**Architecture:** C1 owns the frozen shared planning contracts and selects an immutable complete candidate. G1 and O1 execute only their sealed route payloads, while UO1 owns every optimisation screen and I0 alone composes shared resources, navigation, and the final integrated test surface. Work is split into a short contract wave, a three-worker parallel implementation wave, and one serial integration wave.

**Tech Stack:** C# 13/.NET 8 and .NET 10 SDK orchestration, WinUI 3/Windows App SDK, MSTest/Microsoft.Testing.Platform, Visual Studio packaged VSTest for WinUI tests, PowerShell, Python-based OpenVINO converter, llama.cpp GGUF tools, SHA-256 manifests, Git worktrees.

**Spec:** `docs/superpowers/specs/2026-08-24-cross-route-optimisation-contract-design.md`

## Global Constraints

- `Automatic` is separate; manual slider labels and ranges are exactly `Maximum efficiency` 0-19, `Efficient` 20-39, `Balanced` 40-59, `High capability` 60-79, and `Maximum capability` 80-100.
- A mode selects a complete PC/model/workload/route candidate; no slider band is a fixed precision mapping.
- C1 selects; G1 and O1 execute the exact immutable plan or return `ReplanRequired`.
- The established Model Inspection fields remain exactly `schemaVersion`, `modelInspectionHandoffId`, `modelInspectionRunId`, `outcome`, `modelSha256`, and `modelLengthBytes`; Hardware uses `productHardwareRunId`.
- The original model/package is read-only and verified unchanged; failed or cancelled persistent work publishes nothing.
- Experimental capabilities are absent unless exact evidence admits the route/backend/device/model/cache/version combination.
- No route worker edits optimisation XAML, shared application navigation, shared project registration, or shared resource dictionaries.
- UO1 owns all optimisation XAML and presentation state on `feature/cross-route-optimisation-ui-v1`.
- I0 alone edits `App.xaml`, project files, `MainWindow`, `OnboardingShellPage`, shared dictionaries, and final navigation composition.
- Every code change follows red-green-refactor, uses bounded typed failures, and commits one independently reviewable task.
- No worker pushes, opens a PR, merges, acquires a candidate, contacts hardware, or changes external systems unless the user separately requests it.

## Frozen visual references

| Reference | Commit | Use |
|---|---|---|
| `feature/model-import-drag-drop` | `6c96f0203b9e38ac02639433d13b53c8dc2eecc8` | `ModelPreferenceSlider`, exact labels, interaction behavior |
| `feature/model-inspection-hardware-template-v1` | `ba4fd7bad5c473208248247fcba27e6f22c356ab` | Polished inspection spacing, equal rows, disclosures, status glyphs, 200% text behavior |
| `feature/hardware-inspection-functional-v1` | `f521e9eea81b59f5814fcf100e4f527391ee67d2` | Shared journey action palette and latest aligned detail geometry |
| `feature/hardware-inspection-page-v1` | `ed8bc75881b2637beda6fe5689611a46fb00bf2b` | Hardware card hierarchy, modern action styling, progress/recovery examples |

These references are read-only inputs. UO1 extracts reusable patterns into optimisation-owned controls first; I0 later decides whether a genuinely shared control belongs in a shared resource location.

## Branch and file ownership

| Worker | Required branch | Starts from | Exclusive paths |
|---|---|---|---|
| C1 | `feature/cross-route-optimisation-contracts-v1` | latest accepted `feature/model-hardware-compatibility`, audited at `699c4826...` | `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/**`, its standalone tests, contract handoff document |
| G1 | `feature/gguf-optimisation-executor-v1` | latest accepted `feature/gguf-cli-chat-production`, audited at `767d603a...`, plus frozen C1 contract commit | GGUF optimisation service/tool/manifest paths and component-local tests only |
| O1 | `feature/openvino-optimisation-adapter-v1` | latest accepted `feature/openvino-route`, audited at `1b2372f4...`, plus frozen C1 contract commit | OpenVINO optimisation paths and component-local tests only |
| UO1 | `feature/cross-route-optimisation-ui-v1` | frozen C1 contract integration base | `Features/ModelOptimization/**` and component-local visual/presentation tests only |
| I0 | `integration/cross-route-optimisation-v1` | current integration base chosen after worker commits freeze | shared app/project/resource/navigation files and cross-feature integration tests only |

Workers preserve user changes and stop on an overlapping dirty path. They never reset, checkout over, or delete another worker's changes.

## Execution graph

```text
C1 contract slice
       |
       +----------------+----------------+
       |                |                |
      G1               O1               UO1
       |                |                |
       +----------------+----------------+
                        |
                       I0
                        |
          packaged tests + visual QA
```

### Task 1: C1 freezes the route-neutral contract

**Files:**
- Plan: `docs/superpowers/plans/2026-08-24-c1-cross-route-planner.md`
- Produces: `OptimizationPreferenceSelection`, `OptimizationCapabilitySnapshot`, `OptimizationCandidate`, `OptimizationExecutionPlan`, `OptimizationExecutionResult`, sealed GGUF/OpenVINO payload records

**Interfaces:**
- Consumes: the exact approved design and existing `ModeSelectionRequest`, `GgufRouteConfiguration`, `CompatibilitySupportEntry`, fit/estimation policies
- Produces: a reviewed C1 commit and contract assembly usable by G1, O1, and UO1

- [ ] **Step 1: Implement the C1 plan through its contract and adapter tasks**
- [ ] **Step 2: Run the standalone planner suite** - expected: Microsoft.Testing.Platform executes a non-zero total with zero failures
- [ ] **Step 3: Publish a local contract handoff** - record the branch, base, commit, exact changed paths, test counts, and SHA-256 of the canonical contract document. Do not push or merge.

### Task 2: G1, O1, and UO1 execute in parallel

**Files:**
- G1 plan: `docs/superpowers/plans/2026-08-24-g1-gguf-optimisation-executor.md`
- O1 plan: `docs/superpowers/plans/2026-08-24-o1-openvino-optimisation-adapter.md`
- UO1 plan: `docs/superpowers/plans/2026-08-24-uo1-optimisation-ui.md`

**Interfaces:**
- Consumes: exact C1 contract commit and unchanged design document
- Produces: three clean local branches with non-overlapping paths and explicit handoffs

- [ ] **Step 1: Create all three branches from bases containing the exact C1 contract commit** - each worker verifies `git merge-base --is-ancestor feature/cross-route-optimisation-contracts-v1 HEAD` returns exit code 0 before editing, then records the resolved C1 SHA from `git rev-parse feature/cross-route-optimisation-contracts-v1`.
- [ ] **Step 2: Execute each worker plan independently** - G1 and O1 do not edit UI. UO1 uses fixture executors and does not invoke route tools. All three stop on contract drift rather than cloning shared types.
- [ ] **Step 3: Verify collision boundaries** - use the exact 40-character base recorded when each worktree was created and run `git diff --name-only $workerBase..HEAD`. Expected: every path belongs to that worker's exclusive ownership table; the handoff records the resolved value of `$workerBase` rather than the variable name.
- [ ] **Step 4: Produce three exact handoffs** - each handoff includes base, commits, changed paths, test commands/results, unresolved environmental tests, and zero claims about other branches.

### Task 3: I0 builds the integration branch

**Files:**
- Plan: `docs/superpowers/plans/2026-08-24-i0-optimisation-integration.md`
- Modify only in I0: app/project/resource/navigation files and integration tests named in that plan

**Interfaces:**
- Consumes: reviewed C1, G1, O1, and UO1 commit hashes
- Produces: one integrated route from compatibility result through optimisation to Chat or Save

- [ ] **Step 1: Create the integration branch and record its exact base**
- [ ] **Step 2: Integrate in dependency order** - integrate C1, then G1 and O1, then UO1. Resolve only shared project/resource registration in I0; send component conflicts back to the owning worker.
- [ ] **Step 3: Wire typed navigation and route composition** - do not pass paths, raw JSON, or arbitrary objects through navigation.
- [ ] **Step 4: Run cross-feature validation** - run standalone C1, G1, and OpenVINO test projects, build the WinUI project, then execute the packaged Visual Studio VSTest recipe. A plain `dotnet test` of the packaged WinUI project is not completion evidence.

### Task 4: Verify product behavior and visuals

- [ ] **Step 1: Exercise both routes and both result shapes** - cover GGUF persistent, GGUF runtime-only, OpenVINO persistent, OpenVINO runtime-only, replan-required, cancelled, failed, and successful Chat/Save destinations without using a private model.
- [ ] **Step 2: Capture the visual matrix** - selection, confirmation, all seven progress stages, cancellation, replan, failure, persistent success, and runtime-only success at compact, standard, wide, and 200% text.
- [ ] **Step 3: Compare against frozen references**
- [ ] **Step 4: Verify source and publication invariants**

### Task 5: Final review and merge preparation

- [ ] **Step 1: Run whitespace, encoding, and clean-worktree checks**
- [ ] **Step 2: Request independent code and visual review**
- [ ] **Step 3: Prepare the final handoff**
- [ ] **Step 4: Wait for explicit merge instruction** - do not merge, push, open a PR, or run external hardware workflows merely because local implementation is complete.
