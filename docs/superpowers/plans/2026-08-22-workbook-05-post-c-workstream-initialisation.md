# Workbook 05 Post-C Workstream Initialisation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Initialise the repository-only RED boundaries for Workbook 05 Stages D, E, and F so development can continue on GitHub-hosted runners while the Lenovo is unavailable, without authorising any live experiment.

**Architecture:** All post-C workstreams fan out from the frozen C2 contract head `fa6606f7c8fa03350fc780f13b5c801c9e0d91dd` during RED initialization. Each branch contains only its own contracts/fixtures/planning until shared predecessor interfaces are stable; later integration retargets them into the final C1→C2→C3→C4→C5→D→E→F dependency chain. Live and self-hosted execution remains absent or fail-closed.

**Tech Stack:** Python 3.12.10, JSON Schema Draft 2020-12, Windows PowerShell 5.1, GitHub Actions hosted Windows runners, existing Workbook 05 controls and manifests.

**Spec:** `docs/superpowers/specs/2026-08-03-workbook-05-two-route-memory-frontier-design.md` and `docs/superpowers/specs/2026-08-13-workbook-05-phase3-measured-run-foundations-design.md`, with execution order governed by `docs/superpowers/plans/2026-08-05-workbook-05-completion-checkpoints.md`.

## Global Constraints

- No post-C initialization branch may contact the Lenovo self-hosted runner.
- No model may be downloaded, converted, loaded, or executed from these RED boundaries.
- Synthetic/fixture evidence must never be promoted to a live scientific claim.
- Route A and Route B remain distinct; QJL/PolarQuant labels are never translated into merged OpenVINO TurboQuant labels.
- D/E/F branches may define schemas, controls, ordering, frontier logic, metric aggregation, validators, and hosted repository contracts only.
- Every live D/E result remains blocked until predecessor acceptance and a separately reviewed dispatch boundary.
- Granite 30B is a bounded feasibility package, not a promised successful benchmark; IBM's official Granite 4.1 family includes a 30B instruct model, but laptop execution remains unproven.

---

### Task 1: Initialise D1 codec-conformance RED boundary

**Files:**
- Create: `tests/testing/workbook05/test_stage_d1_codec_conformance_contract.py`

**Interfaces:**
- Produces future record types `codec-conformance-result` and `codec-storage-reconciliation`.
- Requires request/property/dispatch/no-fallback/storage evidence before `Passed`.

- [ ] Write the failing contracts.
- [ ] Run the focused suite and retain the intended missing-schema/type failure.
- [ ] Open a draft PR against `feature/workbook-05-c2-process-harness`.

### Task 2: Initialise D2 diagnostic K/V sweep RED boundary

**Files:**
- Create: `tests/testing/workbook05/test_stage_d2_diagnostic_kv_sweep_contract.py`

**Interfaces:**
- Produces future record types `diagnostic-kv-sweep-row` and `diagnostic-kv-sweep-summary`.
- Requires low-memory-to-high-memory ordering and explicit blocker propagation.

- [ ] Write the failing contracts.
- [ ] Run/retain the intended RED boundary.
- [ ] Open the draft PR.

### Task 3: Initialise E1 Granite 3B frontier/formal RED boundary

**Files:**
- Create: `tests/testing/workbook05/test_stage_e1_granite3b_frontier_contract.py`

**Interfaces:**
- Produces future record types `frontier-point`, `frontier-summary`, and `formal-comparison-summary`.
- Requires last-stable/first-repeated-failure semantics and matched baseline identity.

- [ ] Write the failing contracts.
- [ ] Retain RED evidence.
- [ ] Open the draft PR.

### Task 4: Initialise E2 Granite 8B safety/feasibility RED boundary

**Files:**
- Create: `tests/testing/workbook05/test_stage_e2_granite8b_feasibility_contract.py`

**Interfaces:**
- Produces future record type `model-feasibility-decision` for the exact 8B gate.
- Requires watchdog/frontier evidence and prevents discovery-only output from becoming formal quality evidence.

- [ ] Write the failing contracts.
- [ ] Retain RED evidence.
- [ ] Open the draft PR.

### Task 5: Initialise E3 Granite 30B bounded-feasibility RED boundary

**Files:**
- Create: `tests/testing/workbook05/test_stage_e3_granite30b_feasibility_contract.py`

**Interfaces:**
- Produces future record type `bounded-large-model-feasibility`.
- Requires lowest-weight-first ordering, hard resource stops, explicit `Feasible|Bounded|Blocked` outcome, and forbids conversion of feasibility into a formal performance claim.

- [ ] Write the failing contracts.
- [ ] Retain RED evidence.
- [ ] Open the draft PR.

### Task 6: Initialise E4 cross-family/repeatability RED boundary

**Files:**
- Create: `tests/testing/workbook05/test_stage_e4_cross_family_repeatability_contract.py`

**Interfaces:**
- Produces future record types `cross-family-result` and `repeatability-result`.
- Requires valid constituent codec decisions, matched identities, and explicit blocked rows when a codec is unavailable.

- [ ] Write the failing contracts.
- [ ] Retain RED evidence.
- [ ] Open the draft PR.

### Task 7: Initialise F evidence/workbook-closure RED boundary

**Files:**
- Create: `tests/testing/workbook05/test_stage_f_workbook_closure_contract.py`

**Interfaces:**
- Produces future record type `workbook-closure-decision`.
- Requires independently validated artifact identities for every formal row and forbids unresolved controlled fields at closure.

- [ ] Write the failing contracts.
- [ ] Retain RED evidence.
- [ ] Open the draft PR.

## Verification

After all seven branches exist:

- confirm each PR is Draft, open, and based on the frozen C2 RED contract branch;
- confirm each branch changes only its intended initialization files;
- confirm no branch adds a `self-hosted` execution job or a model acquisition command;
- confirm no branch claims D/E/F acceptance;
- record exact branch heads in the PR descriptions.
