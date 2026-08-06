# Model Inspection Complete Cleanup Programme Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** complete an inventory-driven, behaviour-preserving professional cleanup of every Model Inspection file introduced or changed through PRs #44, #45 and #47 without weakening protocol compatibility, security, privacy, process containment, cancellation, model integrity or verified user-visible behaviour

**Architecture:** run the cleanup as eight gated subsystem phases on `refactor/model-inspection-cleanup`, stacked on `feature/model-inspection-worker-host`. Phase 0 restores a trustworthy exact-head baseline and establishes the complete review ledger. Each later phase receives its own detailed plan after the preceding gate is green and after current code findings identify the exact refactorings that are justified.

**Tech Stack:** C# 12, repository-selected .NET SDK `10.0.301` with `latestPatch` roll-forward, `net8.0`, `net8.0-windows10.0.19041.0`, WinUI 3, Microsoft Testing Platform, MSTest, PowerShell, Windows x64, Win32 process APIs, LLamaSharp 0.27.0 feasibility tooling and GitHub Actions

## Global Constraints

- work only on `refactor/model-inspection-cleanup`, stacked on `feature/model-inspection-worker-host` at base commit `a4138a613dd643abe12858eec5d1c3beb09e95e7`
- preserve worker protocol version `1`, serialized JSON names, enum values, completion meanings and additive-field compatibility
- preserve all stable diagnostic codes and first-failure precedence
- preserve cooperative cancellation semantics and forced termination as `OperationalFailure`
- preserve startup, overall and cancellation-grace timeout meanings
- preserve creation-time Job Object containment and the exact inherited-handle allowlist
- preserve the absence of an uncontained process-launch fallback
- preserve executable containment, reparse-point rejection and x64 architecture verification
- preserve the child-environment allowlist and exclusion of paths, request data and secrets
- preserve bounded strict UTF-8 framing and bounded retained stderr with continued draining
- preserve model-path, chat-template, exception-chain and artifact privacy controls
- preserve production/test-fixture separation and forbid abnormal fixture switches in production
- preserve current WinUI presentation and navigation behaviour
- preserve real-model evidence rules and original-model byte integrity
- use no HTTP server, TCP listener, named-pipe service or required network access
- do not implement Gate 3 LLamaSharp extraction, classifier, service, ViewModel, packaging, OpenVINO, Hardware Fit, chat or TurboQuant
- internal renames and file moves are allowed only when behaviour is preserved and all consumers and tests are updated in the same task
- public contracts, protocol representations and security guarantees require a separate approved design and are not changed here
- comments created or revised by the cleanup use simple English, start with a lower-case letter and do not end with a full stop
- comments explain why, ownership, security, failure precedence or unusual platform behaviour and do not restate obvious code
- every complex refactoring starts from a passing characterization test or already-proven test boundary
- every defect fix begins with a focused failing regression test and is recorded separately from style refactoring
- every phase ends with focused tests, affected regressions, inventory updates, a manual review gate and a coherent commit
- no phase closes with unresolved critical or important findings unless the user explicitly accepts a documented deferral
- final completion requires complete exact-head CI, artifact hashes, privacy success, zero orphan processes and a fresh whole-diff review

---

## Programme Structure

The specification is implemented through the following child plans and gates

| Phase | Boundary | Detailed plan | Entry condition | Exit condition |
|---|---|---|---|---|
| 0 | baseline correction and inventory | `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-0-baseline-inventory.md` | approved cleanup specification | exact-head baseline green, complete ledger established, draft cleanup PR open |
| 1 | WinUI, navigation and application contracts | `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-1-winui-contracts.md` | Phase 0 accepted | focused UI/contract tests and packaged regression green, phase rows closed |
| 2 | shared contracts, protocol and transport | `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-2-protocol-transport.md` | Phase 1 accepted | protocol compatibility, transport, worker/client and process regressions green |
| 3 | WorkerClient and Windows process infrastructure | `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-3-worker-client-windows.md` | Phase 2 accepted | two-pass correctness/security review, complete process suite, repeat stability, privacy and orphan gates green |
| 4 | production worker host and abnormal fixture | `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-4-worker-fixture.md` | Phase 3 accepted | worker and process tests green, source separation proved |
| 5 | LLamaSharp feasibility code and test support | `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-5-llamasharp-feasibility.md` | Phase 4 accepted | deterministic and required native/real-model evidence green, Gate 3 dispositions recorded |
| 6 | test architecture and support cleanup | `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-6-tests.md` | Phase 5 accepted | every test layer readable, deterministic, zero-test protected and green |
| 7 | workflows, scripts and documentation | `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-7-automation-docs.md` | Phase 6 accepted | automation and documentation agree with final code and hosted checks are green |
| 8 | independent review and exact-head closure | `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-8-final-review.md` | Phase 7 accepted | ledger complete, no blocking findings, exact final head fully verified |

The child-plan path is fixed by this master plan. A child plan is written only after its entry condition is satisfied and its current subsystem findings have been collected. This avoids inventing refactorings before the code has been reviewed and keeps every implementation plan precise rather than speculative.

---

### Task 1: Execute Phase 0 Baseline and Inventory

**Files:**
- Follow: `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-0-baseline-inventory.md`
- Modify: `.github/workflows/build-and-test.yml`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`
- Create: `docs/reviews/model-inspection-cleanup-inventory.md`
- Create: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Create: `scripts/model-inspection/Verify-ModelInspectionCleanupInventory.ps1`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/CleanupInventoryContractTests.cs`
- Create: `docs/testing/evidence/2026-08-06-model-inspection-cleanup-baseline.md`

**Interfaces:**
- Consumes: approved specification `docs/superpowers/specs/2026-08-06-model-inspection-cleanup-design.md`
- Produces: green exact-head baseline, complete review ledger, repeatable completeness gate and evidence used by every later phase

- [ ] **Step 1: Execute every checkbox in the Phase 0 child plan in order**

Run the commands and preserve the red/green evidence exactly as written in the child plan

- [ ] **Step 2: Confirm the Phase 0 acceptance conditions**

Required result

```text
complete contract project discovered and passed
all Gate 2 unit and process suites passed
packaged WinUI regression passed
privacy scan passed
no worker or fixture process remained
artifacts retained with identifiers, sizes and hashes
inventory completeness test passed
all inventory rows began with a recorded risk and review state
baseline evidence matched the exact branch head
```

- [ ] **Step 3: Commit Phase 0 closure**

```powershell
git add .github/workflows/build-and-test.yml `
  tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests `
  scripts/model-inspection `
  docs/reviews `
  docs/testing/evidence

git commit -m "test(model-inspection): establish cleanup baseline and inventory"
```

---

### Task 2: Plan and Execute Phase 1 WinUI and Application Contracts

**Files:**
- Create plan: `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-1-winui-contracts.md`
- Review roots:
  - `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/`
  - relevant Model Inspection handoff files under `Features/ModelImport/`
  - relevant Model Inspection navigation files under `Features/Onboarding/`
  - affected tests under `tests/UnitTests/GraniteEdgeAI.UnitTests/`

**Interfaces:**
- Consumes: Phase 0 ledger and exact-head baseline
- Produces: reviewed presentation/navigation/application-contract boundary with unchanged visible behaviour

- [ ] **Step 1: Audit every Phase 1 inventory row before proposing changes**

Record for each file

```text
primary responsibility
current strengths
specific readability or design findings
behaviour that must remain unchanged
existing tests
required characterization tests
proposed disposition
```

- [ ] **Step 2: Write the Phase 1 detailed plan using `superpowers:writing-plans`**

The plan must name every changed file, exact tests, exact signatures and focused commands. It must not contain speculative refactorings or placeholders

- [ ] **Step 3: Review and approve the Phase 1 plan before implementation**

Do not change Phase 1 production code before this review gate

- [ ] **Step 4: Execute the approved Phase 1 plan**

Use `superpowers:subagent-driven-development` or `superpowers:executing-plans`

- [ ] **Step 5: Close every Phase 1 inventory row and run the packaged WinUI regression**

- [ ] **Step 6: Commit Phase 1 as one or more coherent reviewed changes**

---

### Task 3: Plan and Execute Phase 2 Shared Contracts, Protocol and Transport

**Files:**
- Create plan: `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-2-protocol-transport.md`
- Review roots:
  - `shared/GraniteEdgeAI.ModelInspection.Contracts/`
  - `shared/GraniteEdgeAI.ModelInspection.Transport/`
  - contract and transport test projects

**Interfaces:**
- Consumes: stable application contracts from Phase 1
- Produces: cleaner protocol/transport internals with byte-for-byte compatible protocol representations

- [ ] **Step 1: Audit all Phase 2 rows and map every serialized member, enum value and validator state**

- [ ] **Step 2: Add characterization coverage for any compatibility rule not already explicit**

- [ ] **Step 3: Write and approve the exact Phase 2 implementation plan**

- [ ] **Step 4: Execute in small red-green-refactor tasks**

- [ ] **Step 5: Run contract, transport, worker, WorkerClient and real-process regressions**

- [ ] **Step 6: Record protocol compatibility evidence and close Phase 2 rows**

---

### Task 4: Plan and Execute Phase 3 WorkerClient and Windows Infrastructure

**Files:**
- Create plan: `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-3-worker-client-windows.md`
- Review root: `infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/`
- Review affected WorkerClient and process tests

**Interfaces:**
- Consumes: stable protocol and transport from Phase 2
- Produces: clearer process-launch, ownership, conversation, cancellation and cleanup code with unchanged containment guarantees

- [ ] **Step 1: Build a native-resource ownership table for every handle, allocation, stream, registration and drain task**

- [ ] **Step 2: Audit partial-failure and concurrency paths before proposing refactorings**

- [ ] **Step 3: Write and approve the exact Phase 3 plan**

- [ ] **Step 4: Execute each refactoring behind characterization tests**

- [ ] **Step 5: Run the complete WorkerClient and process suite plus repeated timing-sensitive tests**

- [ ] **Step 6: Perform the separate security/privacy review**

- [ ] **Step 7: Confirm privacy success and zero orphan processes, then close Phase 3 rows**

---

### Task 5: Plan and Execute Phase 4 Worker Host and Fixture

**Files:**
- Create plan: `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-4-worker-fixture.md`
- Review roots:
  - `workers/GraniteEdgeAI.ModelInspection.Worker/`
  - `tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker/`
  - affected worker and process tests

**Interfaces:**
- Consumes: stable WorkerClient boundary from Phase 3
- Produces: clearer production state machine and isolated abnormal test fixture

- [ ] **Step 1: Audit worker states, terminal ownership, parent monitoring and exit rules**

- [ ] **Step 2: Audit every abnormal fixture scenario and prove it is absent from production**

- [ ] **Step 3: Write and approve the exact Phase 4 plan**

- [ ] **Step 4: Execute and verify worker, fixture-isolation and process tests**

- [ ] **Step 5: Close Phase 4 rows with source-separation evidence**

---

### Task 6: Plan and Execute Phase 5 LLamaSharp Feasibility Cleanup

**Files:**
- Create plan: `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-5-llamasharp-feasibility.md`
- Review roots:
  - `tools/ModelInspection.LlamaSharpSpike/`
  - `tools/ModelInspection.LlamaSharpSpike.TestSupport/`
  - native and real-model integration projects

**Interfaces:**
- Consumes: unchanged feasibility behaviour and controlled Granite evidence
- Produces: readable feasibility code and one Gate 3 disposition for every feasibility file

- [ ] **Step 1: Classify every file as extract, keep, replace after extraction or remove after migration**

- [ ] **Step 2: Audit runtime identity, file integrity, hashing, metadata, progress, cancellation, redaction and evidence writing**

- [ ] **Step 3: Write and approve the exact Phase 5 plan**

- [ ] **Step 4: Execute only justified behaviour-preserving cleanup**

- [ ] **Step 5: Run deterministic, contained native and required trusted real-model verification**

- [ ] **Step 6: Confirm original model hash, privacy and no-port evidence, then close Phase 5 rows**

---

### Task 7: Plan and Execute Phase 6 Test Architecture Cleanup

**Files:**
- Create plan: `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-6-tests.md`
- Review every Model Inspection test and test-support file in the ledger

**Interfaces:**
- Consumes: stable production and feasibility boundaries from Phases 1 through 5
- Produces: readable deterministic tests that preserve layer separation and zero-test protection

- [ ] **Step 1: Audit test names, arrange/act/assert visibility, helper transparency, cleanup and timing assumptions**

- [ ] **Step 2: Write and approve the exact Phase 6 plan**

- [ ] **Step 3: Simplify setup only where important scenario conditions remain visible**

- [ ] **Step 4: Run every affected suite and repeated process stability campaign**

- [ ] **Step 5: Close all test and test-support inventory rows**

---

### Task 8: Plan and Execute Phase 7 Automation and Documentation Cleanup

**Files:**
- Create plan: `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-7-automation-docs.md`
- Review Model Inspection workflows, scripts, READMEs, ADRs, specs, plans, runbooks, matrices, evidence indexes and PR descriptions

**Interfaces:**
- Consumes: final code and tests from Phases 1 through 6
- Produces: automation and documentation that accurately describe and verify the current implementation

- [ ] **Step 1: Audit workflow commands, triggers, permissions, test discovery, artifacts, privacy and orphan handling**

- [ ] **Step 2: Audit documentation against code and executable evidence**

- [ ] **Step 3: Write and approve the exact Phase 7 plan**

- [ ] **Step 4: Execute workflow/script cleanup with contract tests**

- [ ] **Step 5: Reconcile documentation without rewriting historical evidence claims**

- [ ] **Step 6: Run hosted workflows and close automation/documentation rows**

---

### Task 9: Plan and Execute Phase 8 Final Independent Review

**Files:**
- Create plan: `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-8-final-review.md`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`
- Create: final cleanup evidence and review report under `docs/testing/evidence/`
- Update: draft cleanup PR body

**Interfaces:**
- Consumes: completed Phases 0 through 7
- Produces: exact-head proof and final review decision

- [ ] **Step 1: Review the full branch diff without relying on earlier phase conclusions**

- [ ] **Step 2: Recheck every critical/high-risk ownership, security, privacy and failure path**

- [ ] **Step 3: Verify every ledger row has one allowed final disposition and executable evidence**

- [ ] **Step 4: Run complete exact-head CI and required trusted-model campaign**

- [ ] **Step 5: Record run IDs, job IDs, test counts, artifacts, sizes, digests, privacy and orphan outcomes**

- [ ] **Step 6: Resolve every critical and important finding or obtain an explicit documented deferral**

- [ ] **Step 7: Reconcile the PR body and final evidence with the exact final commit**

- [ ] **Step 8: Commit final closure documentation**

```powershell
git add docs/reviews docs/testing/evidence docs/superpowers/plans
git commit -m "docs(model-inspection): close complete cleanup review"
```

---

## Programme Acceptance Criteria

```text
[ ] Phase 0 exact-head baseline is trustworthy and green
[ ] complete review ledger is generated and completeness-checked
[ ] every relevant file appears exactly once in the ledger
[ ] every later phase has a detailed approved plan before code changes
[ ] every production type has one clear primary responsibility
[ ] important names and domain terms are consistent
[ ] all native and asynchronous resources have explicit proven ownership
[ ] failure and cleanup paths preserve the first proven cause
[ ] protocol and serialized compatibility remain unchanged
[ ] security, privacy, containment and cancellation guarantees remain unchanged
[ ] comments follow the approved simple style where comments are needed
[ ] test layers remain distinct, readable, deterministic and zero-test protected
[ ] LLamaSharp feasibility files have explicit Gate 3 dispositions
[ ] workflows and documentation agree with final code
[ ] no unresolved critical or important finding remains
[ ] exact final commit passes complete required verification
[ ] final artifacts and hashes are recorded
[ ] privacy scan passes
[ ] no worker or fixture process remains
[ ] final draft PR gives complete review context
```

## Engineering Basis

- **Refactoring: Improving the Design of Existing Code** — small behaviour-preserving transformations, characterization tests and frequent verification
- **Code Complete** — meaningful names, cohesive routines, consistent abstraction and defensive construction
- **Designing Secure Software** — explicit trust boundaries, least exposure, fail-closed behaviour and security-focused review
- **The Art of Unit Testing** — clear test intent and separation of unit, contract, process and trusted-runtime tests
- **Why Programs Fail** — reproducible baselines, first-failure preservation and cause-before-symptom debugging
- **Fundamentals of Software Architecture** — dependency fitness functions, cohesion, coupling and explicit trade-offs
- **Systems Engineering Principles and Practice** — traceability from requirement through implementation, verification and acceptance
- **windows-apps.pdf** — separation of WinUI presentation from application and native-process infrastructure
