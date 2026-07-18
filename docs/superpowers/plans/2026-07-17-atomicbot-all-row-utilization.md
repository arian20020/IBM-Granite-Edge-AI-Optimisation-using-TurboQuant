# AtomicBot All-Row Utilization Completion Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Capture auditable CPU and GPU mean, median, and peak utilization for every WB-02 runtime test, including actual GPU use for every Vulkan-partial configuration.

**Architecture:** Reuse the validated per-process Windows utilization sampler and the existing AtomicBot server-metrics harness. Execute each runtime row serially with one pilot, one excluded warm-up, and three formal 64-token loaded-request repetitions; reconcile raw samples, aggregates, device placement, registers, workbook v1.7, evidence hashes, and the generated DOCX.

**Tech Stack:** Python 3.11, PowerShell, Windows GPU Engine performance counters, llama-server CPU/Vulkan builds, CSV/JSON, python-docx.

## Global Constraints

- Run only one llama-server process at a time.
- Preserve the 256 MiB emergency available-RAM floor for all cases and include guarded rows explicitly.
- Attribute GPU utilization only to GPU Engine instances containing the active llama-server PID.
- Define GPU utilization as the busiest process GPU engine per timestamp; never sum engines or infer missing values.
- Record per-repetition and aggregate CPU/GPU mean, median, and peak percentages.
- Retain pilot and warm-up evidence but exclude them from formal statistics.
- Update the workbook and registers only from valid measured samples and verified hashes.

---

### Task 1: Validate all-row utilization harness

**Files:**
- Modify: `scripts/testing/run_atomicbot_server_metrics.py`
- Modify: `scripts/testing/atomicbot/utilization.py`
- Test: `scripts/testing/tests/test_atomicbot_utilization.py`

**Interfaces:**
- Consumes: `summarize_utilization(samples: list[dict]) -> dict`
- Produces: one `server-metrics-summary.json` per test with three formal utilization series.

- [ ] Add tests proving aggregate mean, median, peak and sample counts remain bounded and source-backed.
- [ ] Run `python -m unittest scripts.testing.tests.test_atomicbot_utilization -v` and confirm all tests pass.
- [ ] Run a dry selection audit and confirm exactly 19 non-build runtime rows are selected.

### Task 2: Execute CPU runtime rows

**Files:**
- Create: `experiments/raw-results/atomicbot-turboquant/2026-07-17/all-row-utilization-v1/<CPU test ID>/`

**Interfaces:**
- Consumes: CPU llama-server build and frozen retest matrix.
- Produces: pilot, warm-up, three formal samples, raw CSVs, and summaries for AB-01 through AB-10 plus guarded CPU rows.

- [ ] Execute CPU rows serially with `--measurement-tokens 64 --ignore-eos --minimum-available-ram-mb 256`.
- [ ] Verify every CPU summary contains three valid formal samples and non-null CPU/GPU statistics.
- [ ] Confirm CPU-only process GPU mean is measured from attributable counters and is not inferred.

### Task 3: Execute Vulkan runtime rows

**Files:**
- Create: `experiments/raw-results/atomicbot-turboquant/2026-07-17/all-row-utilization-v1/<Vulkan test ID>/`

**Interfaces:**
- Consumes: Vulkan llama-server build and frozen retest matrix.
- Produces: utilization evidence for AB-11 through AB-15M.

- [ ] Execute Vulkan rows serially with the same formal protocol and emergency RAM floor.
- [ ] Verify GPU Engine samples belong to the active llama-server PID and remain within 0–100%.
- [ ] Retain Vulkan-partial GPU mean, median, and peak together with actual layer placement.

### Task 4: Reconcile workbook and registers

**Files:**
- Create: `scripts/testing/reconcile_atomicbot_all_utilization.py`
- Modify: `docs/testing/Performance-Measurement-Register.csv`
- Modify: `docs/testing/Device-Verification-Register.csv`
- Modify: `docs/testing/Evidence-Index.csv`
- Modify: `docs/testing/Workbook-Completion-Register.csv`
- Modify: `docs/testing/Workbook-Revision-Register.csv`
- Modify: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`
- Modify: `docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md`
- Modify: `docs/testing/workbooks/generated/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.docx`

**Interfaces:**
- Consumes: valid all-row summaries and raw utilization CSVs.
- Produces: WB-02 v1.7 with complete per-run and aggregate CPU/GPU utilization.

- [ ] Add a failing reconciliation test that rejects any runtime row without CPU/GPU mean, median, peak, source path, or three formal repetitions.
- [ ] Populate all 57 formal performance rows from their matching summaries.
- [ ] Add device-register rows for CPU configurations and update all Vulkan aggregate rows.
- [ ] Update the workbook device table so Vulkan-partial rows visibly state GPU mean, median, and peak.
- [ ] Index and hash every new evidence file, append revision v1.7, regenerate the DOCX, and update manifest hashes.

### Task 5: Independent verification and cleanup

**Files:**
- Verify all modified controls and generated evidence.

**Interfaces:**
- Consumes: reconciled WB-02 v1.7 repository state.
- Produces: evidence-backed completion status.

- [ ] Independently recompute every register value from raw CSV/JSON evidence and reject mismatches.
- [ ] Run `python scripts/testing/Validate-Workbook-Revision-Control.py` and require PASS.
- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Validate-Controlled-Testing-Workspace.ps1` and require PASS.
- [ ] Run `python -m unittest discover -s scripts/testing/tests -p 'test_*.py' -q` and require zero failures.
- [ ] Run `git diff --check`, remove only verified temporary/rejected artifacts, and report any visual-render limitation explicitly.
