# animehacker TQ3_0 Controlled Retest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute and fully reconcile WB-03 (`AH-B01`-`AH-B08`, `AH-01`-`AH-10`) with zero unresolved failures, no blank or literal `N/A` workbook cells, complete resource/utilization metrics, proven TQ3_0/device state, and harsh evidence-based quality scores.

**Architecture:** First finish and commit the pending WB-02 v1.7 boundary. Then use a route-specific matrix and crash-safe controller to acquire/build the pinned animehacker fork, execute build and runtime phases serially, capture immutable evidence, derive validated summaries, and update WB-03 immediately after each terminal row. Final reconciliation regenerates the DOCX and verifies every reported value from raw evidence.

**Tech Stack:** Git, CMake/MSVC/Ninja, oneAPI SYCL/Level Zero where supported, optional supplementary Vulkan, Python 3.11, PowerShell, llama.cpp tools/server, Windows process and GPU Engine counters, CSV/JSON, python-docx.

## Global Constraints

- Work in place on a dedicated WB-03 branch only after WB-02 v1.7 is installed, validated, committed, and the worktree is clean.
- Pin the latest default-branch commit at campaign start and record its exact SHA before modification.
- Run one model process and one controller at a time; use atomic checkpoints and complete process-tree cleanup.
- Use one pilot, one excluded warm-up, and three formal repetitions per runnable runtime row.
- Enforce a conservative available-RAM reserve and a hard emergency-stop floor for every launch; run 8B rows alone.
- Record CPU/GPU mean, median, peak, and raw samples for every runnable formal row.
- Use SYCL as the controlled GPU route; label Vulkan and WSL as supplementary.
- Never infer a measurement or treat flag acceptance as TQ3_0 activation.
- No completed WB-03 cell may be blank or contain the literal token `N/A`.
- No in-scope test may remain failed or unresolved at completion.

---

### Task 1: Close and preserve the WB-02 v1.7 boundary

**Files:**
- Modify: `docs/testing/workbooks/generated/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.docx`
- Modify: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`
- Verify: all currently modified AtomicBot registers, scripts, tests, and evidence.

**Interfaces:**
- Consumes: `.tmp-all-util-revised/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.docx` and all-row utilization evidence.
- Produces: one validated AtomicBot commit and a clean branch boundary.

- [ ] Confirm Word no longer locks the controlled WB-02 DOCX.
- [ ] Copy the generated v1.7 DOCX into `docs/testing/workbooks/generated/` and update its manifest SHA-256 with `update_manifest()` from `scripts/testing/reconcile_atomicbot_all_utilization.py`.
- [ ] Run `python scripts/testing/Validate-Workbook-Revision-Control.py`, the controlled-workspace validator, `python -m unittest discover -s scripts/testing/tests -p 'test_*.py' -q`, independent evidence/hash reconciliation, and `git diff --check`; require zero failures.
- [ ] Remove only verified WB-02 temporary generation folders and commit the complete WB-02 v1.7 change set with `git commit -m "test: complete AtomicBot all-row utilization"`.
- [ ] Create branch `testing/animehacker-tq3-formal-retest` from the completed boundary.

### Task 2: Freeze WB-03 matrix and failure-proof controller contracts

**Files:**
- Create: `experiments/manifests/animehacker-tq3-0/retest-matrix.json`
- Create: `scripts/testing/animehacker/matrix.py`
- Create: `scripts/testing/animehacker/state.py`
- Create: `scripts/testing/tests/test_animehacker_matrix.py`

**Interfaces:**
- Produces: `load_matrix(path: Path) -> list[TestCase]`, strict IDs/statuses, guarded-row flags, and atomic state writes.

- [ ] Write a failing test asserting the matrix contains each of `AH-B01`-`AH-B08` and `AH-01`-`AH-10` exactly once, rejects duplicates/unknown backends/statuses, and marks 8B rows as guarded.
- [ ] Run `python -m unittest scripts.testing.tests.test_animehacker_matrix -v` and confirm it fails because the module/matrix does not exist.
- [ ] Implement immutable `TestCase` parsing and atomic checkpoint replacement, and create the matrix with model, cache, backend, context, activation proof, required metrics, safety, and quality requirements.
- [ ] Rerun the test and require PASS.

### Task 3: Acquire and audit the pinned repository

**Files:**
- Create: `scripts/testing/acquire_animehacker_retest.ps1`
- Create: `scripts/testing/audit_animehacker_source.py`
- Create: `scripts/testing/tests/test_animehacker_source_audit.py`
- Create evidence under: `experiments/raw-results/animehacker-tq3-0/2026-07-18/acquisition/`

**Interfaces:**
- Produces: pinned repository metadata, source audit, TQ3_0/QJL/backend capability record, and exact build inputs.

- [ ] Query the remote default branch, clone to a campaign-specific external checkout, record remote/branch/commit/submodules/status, and verify the checkout is clean.
- [ ] Hash relevant source/build files and search for TQ3_0 types, flags, block layout, quantize/dequantize paths, runtime allocation logs, QJL residual correction, SYCL, Level Zero, Vulkan, CUDA-only assumptions, and upstream-base markers.
- [ ] Add tests that reject activation claims based only on README/flag strings and require implementation/runtime evidence categories.
- [ ] Run the audit tests and store a machine-readable source classification plus human-readable limitations report.
- [ ] Immediately fill WB-03 repository/environment and `AH-B01`, `AH-B06`, `AH-B08` fields from verified evidence.

### Task 4: Build CPU, tools, tests, and controlled SYCL backend

**Files:**
- Create: `scripts/testing/build_animehacker.ps1`
- Create: `scripts/testing/tests/test_animehacker_build_reconcile.py`
- Create evidence under: `experiments/raw-results/animehacker-tq3-0/2026-07-18/build-{cpu,sycl,vulkan}/`
- Modify: central build, failure, environment, repository, and run registers.

**Interfaces:**
- Produces: hashed binaries, complete configure/build/test logs, device inventory, and `AH-B02`-`AH-B07` classifications.

- [ ] Capture MSVC, CMake, Ninja, oneAPI, SYCL, Level Zero, Vulkan SDK, driver, CPU, RAM, GPU, power, and disk state.
- [ ] Configure and build a fresh Release CPU tree with repository tools/tests enabled; fix all build failures at root cause and rerun to PASS.
- [ ] Run every repository-provided test; diagnose and fix any in-scope failure, then rerun the complete suite with zero unresolved failures.
- [ ] Build and inventory benchmark/server/quantization tools and hash every required binary.
- [ ] Configure/build the controlled Windows SYCL route and enumerate actual SYCL/Level Zero devices. If a capability is absent before testing, record `Unsupported - <proof>` rather than a failed test.
- [ ] When source support exists, configure/build supplementary Vulkan without allowing it to replace SYCL results.
- [ ] Update all build IDs and failure records immediately after validation.

### Task 5: Implement crash-safe runtime, utilization, and activation capture

**Files:**
- Create: `scripts/testing/run_animehacker_retest.py`
- Create: `scripts/testing/measure_animehacker_server.py`
- Reuse: `scripts/testing/collect_process_utilization.ps1`
- Create: `scripts/testing/animehacker/activation.py`
- Create: `scripts/testing/tests/test_animehacker_runner.py`
- Create: `scripts/testing/tests/test_animehacker_activation.py`

**Interfaces:**
- Produces: pilot/warm-up/formal evidence, process-tree metrics, utilization summaries, backend placement, and activation decisions.

- [ ] Write failing tests for exact case selection, resume behavior, unique run IDs, process-tree cleanup, timeouts, emergency RAM stop, utilization bounds, and activation proof.
- [ ] Implement prelaunch command/environment evidence, a single-controller lock, atomic state, full process-tree termination, and emergency monitoring.
- [ ] Measure peak working set/private bytes, available RAM, KV allocation, TTFT, prompt/decode throughput, generation duration, CPU/GPU mean-median-peak, GPU memory, and cleanup result.
- [ ] Parse actual backend/device, layer placement, host/SYCL memory, KV device, and TQ3_0 allocation/type evidence from runtime logs.
- [ ] Require runtime or binary/source-linked activation proof; reject silent fallback and flag-only claims.
- [ ] Run the harness unit tests and require PASS before model execution.

### Task 6: Execute AH-01-AH-07 CPU and TQ3_0 rows

**Files:**
- Create evidence under: `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/<test-id>/`
- Modify: performance, device, run, traceability, failure, and completion registers plus WB-03 Markdown.

**Interfaces:**
- Consumes: validated CPU binaries, controlled models, frozen matrix.
- Produces: three valid formal repetitions and activation evidence per runnable CPU row.

- [ ] Verify model path, size, SHA-256, format, disk headroom, available RAM, and no existing model process before each row.
- [ ] Execute AH-01 through AH-07 serially with pilot, excluded warm-up, and three measured repetitions.
- [ ] For each terminal row, validate samples, reconcile metrics independently, hash evidence, update registers, and fill WB-03 immediately.
- [ ] Diagnose and fix every failed runnable row, then rerun it successfully; do not continue past an unresolved prerequisite failure that invalidates later rows.

### Task 7: Execute AH-08-AH-10 SYCL and supplementary GPU rows

**Files:**
- Create evidence under: `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/<test-id>/`
- Create supplementary evidence under: `experiments/raw-results/animehacker-tq3-0/2026-07-18/supplementary-vulkan/`

**Interfaces:**
- Produces: controlled SYCL placement/utilization results and separately labelled Vulkan evidence.

- [ ] Execute controlled SYCL partial rows only after device inventory and backend smoke tests pass.
- [ ] Capture actual layer offload, host/device buffers, host-versus-SYCL KV location, CPU fallback, and CPU/GPU mean-median-peak.
- [ ] Run AH-10 alone behind the 8B safety gate; preserve emergency-stop evidence and retry only with a documented safer hypothesis.
- [ ] If SYCL is source-level unsupported, populate every cell with specific `Unsupported - <proof>` classifications and run supplementary Vulkan only when genuinely supported.
- [ ] Fix and rerun any test that begins execution and fails; zero unresolved failed tests may remain.

### Task 8: Execute and score P1-P6 quality for every runnable configuration

**Files:**
- Create: `scripts/testing/run_animehacker_quality.py`
- Create: `scripts/testing/adjudicate_animehacker_quality.py`
- Create: `scripts/testing/tests/test_animehacker_quality.py`
- Create evidence under: `experiments/raw-results/animehacker-tq3-0/2026-07-18/quality/`
- Modify: `docs/testing/Quality-Evaluation-Register.csv`

**Interfaces:**
- Produces: terminal prompt records, response hashes, deterministic gate results, manual rubric dimensions, and per-row aggregate quality.

- [ ] Write tests proving score depends on response evidence rather than format label, structural failures cap scores, empty launched responses score zero, and prerequisite-blocked rows receive explicit text rather than invented scores.
- [ ] Implement one-controller locking, absolute deadlines, per-prompt checkpoints, request/response hashes, and safe process cleanup.
- [ ] Execute P1-P6 independently for every runnable AH row, including guarded long-context attempts only when RAM gates pass.
- [ ] Apply harsh deterministic gates and content adjudication, double-check each score against raw text, and update the quality register/workbook immediately.
- [ ] Rerun any harness/validator failure until the quality suite has zero unresolved failures.

### Task 9: Reconcile all controls and generate WB-03

**Files:**
- Create: `scripts/testing/reconcile_animehacker_workbook.py`
- Create: `scripts/testing/tests/test_animehacker_reconcile.py`
- Modify: all relevant central registers.
- Modify: `docs/testing/workbooks/text-templates/03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.md`
- Modify: `docs/testing/workbooks/generated/03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.docx`
- Modify: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`
- Modify: `docs/testing/Workbook-Revision-Register.csv`

**Interfaces:**
- Produces: a complete, source-backed WB-03 revision and controlled DOCX.

- [ ] Add reconciliation tests that require all 18 IDs, all runtime repetitions/classifications, all required measurements, quality evidence for every runnable row, source paths, unique evidence IDs, valid hashes, and no blank or literal `N/A` workbook cells.
- [ ] Independently recompute performance/utilization aggregates and quality summaries from raw evidence; fail on any mismatch.
- [ ] Populate implementation depth, QJL status, TQ3_0 activation, device support, memory benefit, integration difficulty, final status, and evidence summary.
- [ ] Append the next WB-03 revision, regenerate the DOCX, apply embedded revision history, and update template/DOCX manifest hashes.
- [ ] Render and inspect every DOCX page when a compatible renderer exists; otherwise record the renderer limitation and perform structural/semantic DOCX inspection.

### Task 10: Final zero-failure verification and branch handoff

**Files:**
- Verify the complete repository change set and evidence package.

**Interfaces:**
- Produces: verified completion evidence and a branch ready for review.

- [ ] Run the complete repository-provided animehacker test suite and require zero unresolved failures.
- [ ] Run `python -m unittest discover -s scripts/testing/tests -p 'test_*.py' -q` and require zero failures.
- [ ] Run `python scripts/testing/Validate-Workbook-Revision-Control.py` and the controlled-workspace validator; require PASS.
- [ ] Run independent no-blank/no-`N/A`, evidence-path/hash, metric-recomputation, activation, placement, and quality audits; require PASS.
- [ ] Run `git diff --check`, inspect the full diff for unrelated changes, remove only verified temporary/rejected artifacts, and commit the completed WB-03 campaign.
- [ ] Use `superpowers:verification-before-completion` before any completion claim and `superpowers:finishing-a-development-branch` for PR/merge handoff.
