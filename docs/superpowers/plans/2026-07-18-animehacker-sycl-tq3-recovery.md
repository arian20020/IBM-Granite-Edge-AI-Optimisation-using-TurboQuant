# animehacker SYCL TQ3 Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Recover formal AH-09 SYCL TQ3 execution and safely attempt AH-10, then replace the current terminal classifications with complete evidence-backed workbook results wherever the unchanged memory gate permits execution.

**Architecture:** Put the recovered Level Zero command and Granite prompt protocol in the existing `animehacker.runner` boundary so runtime and quality controllers share one definition. Execute runtime rows through the existing crash-safe collector, extend quality selection to the newly runnable rows, then regenerate and reconcile WB-03 from retained evidence.

**Tech Stack:** Python 3.11, `unittest`, PowerShell/cmd oneAPI environment setup, llama.cpp server/HTTP collector, JSON/CSV/Markdown/DOCX control scripts.

## Global Constraints

- Pinned fork commit remains `5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc` plus the already documented controlled test/backend corrections.
- SYCL TQ3 uses `ONEAPI_DEVICE_SELECTOR=level_zero:0`, `-ngl 1 -sm none -mg 0`.
- Do not use forced `output.*=SYCL0` placement or explicit `-fa off` for the recovered route.
- Treat TQ3 K/V as host-resident unless runtime evidence proves otherwise; one-layer allocation and utilization prove only partial SYCL use.
- AH-10 retains a 2,048 MiB minimum available-physical-RAM emergency floor; never lower or bypass it.
- Accept only valid multi-token output; reject empty EOS and 1,000,000 tok/s sentinel measurements.
- Every runnable row requires pilot, excluded warm-up, three formal samples, full CPU/GPU/resource metrics, and P1-P6 harsh format-neutral quality.
- Preserve rejected diagnostics and exact commands; never overwrite raw evidence.

---

### Task 1: Encode the recovered SYCL TQ3 command and prompt protocol

**Files:**
- Modify: `scripts/testing/tests/test_animehacker_runner.py`
- Modify: `scripts/testing/animehacker/runner.py`
- Modify: `scripts/testing/run_animehacker_retest.py`

**Interfaces:**
- Produces: `format_runtime_prompt(case: TestCase, prompt: str) -> str`
- Produces: `build_server_command(...) -> list[str]` with the recovered SYCL TQ3 placement.
- Consumed by: runtime controller and Task 2 quality controller.

- [ ] **Step 1: Replace the obsolete SYCL expectations with failing recovery tests**

Add tests equivalent to:

```python
def test_tq_sycl_uses_proven_level_zero_partial_offload_flags(self):
    from scripts.testing.animehacker.runner import build_server_command
    command = build_server_command(case(backend="sycl-partial", cache="tq3_0"),
                                   Path("server.exe"), Path("model.gguf"), 19003)
    pairs = [command[i:i + 2] for i in range(len(command) - 1)]
    self.assertIn(["-ngl", "1"], pairs)
    self.assertIn(["-sm", "none"], pairs)
    self.assertIn(["-mg", "0"], pairs)
    self.assertNotIn(["-ot", "output.*=SYCL0"], pairs)
    self.assertNotIn(["-fa", "off"], pairs)

def test_tq_sycl_runtime_prompt_uses_granite_role_tokens(self):
    from scripts.testing.animehacker.runner import format_runtime_prompt
    item = case(backend="sycl-partial", cache="tq3_0")
    self.assertEqual(format_runtime_prompt(item, "Question"),
        "<|start_of_role|>user<|end_of_role|>Question<|end_of_text|>\n"
        "<|start_of_role|>assistant<|end_of_role|>")

def test_cpu_runtime_prompt_is_unchanged(self):
    from scripts.testing.animehacker.runner import format_runtime_prompt
    self.assertEqual(format_runtime_prompt(case(), "Question"), "Question")
```

- [ ] **Step 2: Run the targeted test and observe the intended RED state**

Run:

```powershell
python -m unittest scripts.testing.tests.test_animehacker_runner -v
```

Expected: failures because the current command uses `-ngl 0`, `-ot`, and `-fa off`, and `format_runtime_prompt` does not exist.

- [ ] **Step 3: Implement the minimal shared recovery behavior**

In `runner.py`, make SYCL TQ3 use one layer and append the proven split/main-GPU flags; remove the special output-tensor and flash-off flags. Add:

```python
def format_runtime_prompt(case: TestCase, prompt: str) -> str:
    if case.backend == "sycl-partial" and case.cache == "tq3_0":
        return ("<|start_of_role|>user<|end_of_role|>" + prompt +
                "<|end_of_text|>\n<|start_of_role|>assistant<|end_of_role|>")
    return prompt
```

In `run_animehacker_retest.py`, import the helper, set the SYCL environment to `{"ONEAPI_DEVICE_SELECTOR": "level_zero:0"}`, and pass `format_runtime_prompt(case, fixed_prompt)` to `--prompt`.

- [ ] **Step 4: Run targeted and complete unit suites**

Run:

```powershell
python -m unittest scripts.testing.tests.test_animehacker_runner -v
python -m unittest discover -s scripts/testing/tests -p 'test_*.py'
```

Expected: all tests pass, with no obsolete forced-flash/output-placement expectation remaining.

- [ ] **Step 5: Commit the harness correction**

```powershell
git add scripts/testing/animehacker/runner.py scripts/testing/run_animehacker_retest.py scripts/testing/tests/test_animehacker_runner.py
git commit -m "fix: recover animehacker SYCL TQ3 route"
```

---

### Task 2: Extend quality execution to recovered rows

**Files:**
- Modify: `scripts/testing/tests/test_animehacker_runner.py`
- Modify: `scripts/testing/run_animehacker_quality.py`

**Interfaces:**
- Consumes: `build_server_command` from Task 1.
- Produces: selectable AH-09/AH-10 quality execution with Granite 8B model input.

- [ ] **Step 1: Add a failing selection/model-coverage test**

Extract and test a small pure helper such as:

```python
def test_quality_selection_accepts_recovered_rows(self):
    from scripts.testing.run_animehacker_quality import quality_case_ids
    self.assertIn("AH-09", quality_case_ids())
    self.assertIn("AH-10", quality_case_ids())
```

- [ ] **Step 2: Verify the targeted test fails**

Run:

```powershell
python -m unittest scripts.testing.tests.test_animehacker_runner -v
```

Expected: FAIL because the current quality allowlist ends at AH-08 and the helper is absent.

- [ ] **Step 3: Add only the required quality inputs and selection**

Add `--granite8-model`, include `"granite-8b-q8-0"` in the model map, and expose:

```python
def quality_case_ids() -> frozenset[str]:
    return frozenset({"AH-01", "AH-02", "AH-03", "AH-04", "AH-05",
                      "AH-08", "AH-09", "AH-10"})
```

Use that helper for selection. Invoke the quality controller only after the corresponding runtime summary exists; keep the existing P1-P6 request, timeout, hash, and lock behavior.

- [ ] **Step 4: Verify targeted and complete suites**

Run the two commands from Task 1 Step 4. Expected: all pass.

- [ ] **Step 5: Commit quality recovery support**

```powershell
git add scripts/testing/run_animehacker_quality.py scripts/testing/tests/test_animehacker_runner.py
git commit -m "test: enable quality for recovered SYCL TQ3 rows"
```

---

### Task 3: Execute and reconcile AH-09 runtime and quality

**Files:**
- Create: `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime-recovery/AH-09/`
- Create: `experiments/raw-results/animehacker-tq3-0/2026-07-18/quality-recovery/AH-09/`
- Modify: `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/state.json`

**Interfaces:**
- Consumes: corrected runtime and quality controllers.
- Produces: accepted AH-09 summary and six hashed quality responses.

- [ ] **Step 1: Preserve recovery diagnostics and choose a non-overwriting formal root**

Retain `runtime/AH-09-sycl-recovery/` as diagnostic evidence. Use `runtime-recovery/` for formal reruns; do not delete prior rejected attempts.

- [ ] **Step 2: Run AH-09 pilot under the oneAPI environment**

Invoke `setvars.bat`, select only AH-09, set the current Q4_K_M Granite 3B path, use `--pilot-only --measurement-tokens 128 --minimum-available-ram-mb 3072`, and require a valid multi-token measurement plus activation proof.

- [ ] **Step 3: Run AH-09 warm-up and three formal samples**

Rerun the same controller without `--pilot-only`. Expected: `summary.json` with exactly three samples and complete RAM/KV/TTFT/throughput/CPU/GPU/GPU-memory aggregates.

- [ ] **Step 4: Validate AH-09 summary before quality**

Reject the row if any sample has `predicted_n <= 1`, `decode_tps >= 1000000`, missing utilization, absent 70 MiB TQ3 allocation evidence, or no genuine SYCL allocation/offload proof.

- [ ] **Step 5: Run AH-09 P1-P6 quality**

Run `run_animehacker_quality.py --only AH-09` in the oneAPI Level Zero environment using a new quality-recovery directory and the frozen prompt set. Adjudicate with the existing harsh content-keyed rubric; never reuse the earlier capability-classified score.

- [ ] **Step 6: Commit immutable AH-09 evidence**

Stage only the new runtime/quality evidence and the reconciled state, then commit:

```powershell
git commit -m "test: recover AH-09 SYCL TQ3 evidence"
```

---

### Task 4: Attempt AH-10 with unchanged safety controls

**Files:**
- Create: `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime-recovery/AH-10/`
- Conditionally create: `experiments/raw-results/animehacker-tq3-0/2026-07-18/quality-recovery/AH-10/`
- Modify: `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/state.json`

**Interfaces:**
- Consumes: Task 1 harness and Granite 8B Q8_0 model.
- Produces: full runnable evidence or a fresh terminal 2 GiB safety classification.

- [ ] **Step 1: Verify no competing model processes and record available RAM**

Confirm no `llama-*` process tree is active. Record physical available RAM and do not begin unless the controlled pilot can preserve the 2,048 MiB floor.

- [ ] **Step 2: Run AH-10 pilot alone**

Invoke the runtime controller with `--only AH-10 --include-guarded --pilot-only --minimum-available-ram-mb 2048 --measurement-tokens 128` under Level Zero.

- [ ] **Step 3: Follow the terminal branch proved by the pilot**

If the pilot crosses the floor, verify process-tree cleanup, retain its events/measurement/logs, and stop AH-10 execution. If valid, run warm-up and three formal samples with the same floor.

- [ ] **Step 4: Run quality only for a runnable AH-10**

If and only if `summary.json` has three valid samples, run P1-P6 in isolation with the same Level Zero route and adjudicate it. Do not manufacture quality for a safety-stopped row.

- [ ] **Step 5: Commit AH-10 terminal evidence**

Commit either the complete runtime/quality result or fresh safety evidence with an honest message describing the outcome.

---

### Task 5: Update quality registers and WB-03

**Files:**
- Modify: `docs/testing/Quality-Evaluation-Register.csv`
- Modify: `docs/testing/Workbook-Revision-Register.csv`
- Modify: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`
- Modify: `docs/testing/workbooks/text-templates/03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.md`
- Modify: `docs/testing/workbooks/generated/03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.docx`
- Modify: `scripts/testing/finalize_animehacker_runtime_state.py`
- Modify: `scripts/testing/reconcile_animehacker_workbook.py`

**Interfaces:**
- Consumes: terminal AH-09 and AH-10 evidence.
- Produces: synchronized WB-03 Markdown/DOCX/register/manifest revision.

- [ ] **Step 1: Add failing reconciliation coverage for the recovered classification**

Extend reconciliation tests so a runnable AH-09 requires three complete samples and six quality records, while AH-10 accepts only either the same complete set or a sourced memory-gate terminal record.

- [ ] **Step 2: Observe RED, then minimally update reconciliation/state logic**

Run the targeted reconciliation test, confirm the old forced capability classification fails, then update the finalizer/reconciler to consume the new terminal evidence paths.

- [ ] **Step 3: Update quality rows idempotently**

Replace WB-03 AH-09 quality records with the new P1-P6 hashes and scores. Add AH-10 records only if it ran quality. Preserve the UTF-8 BOM and all unrelated register rows.

- [ ] **Step 4: Fill every affected workbook field from evidence**

Replace AH-09 unsupported wording with measured runtime, placement, resource, utilization, quality, and recovery explanation. Populate AH-10 from formal results or the fresh 2 GiB safety stop. Update failure resolution and final recommendation without claiming full-GPU TQ3 cache acceleration.

- [ ] **Step 5: Append a new WB-03 revision and regenerate deterministic DOCX**

Supersede WR-029, append the next record ID/version, generate all workbooks in temporary directories, apply revision history, copy only WB-03, and update its template/DOCX hashes and revision in the manifest.

- [ ] **Step 6: Commit the synchronized workbook revision**

Commit registers, Markdown, DOCX, state/reconciliation logic, and generated reconciliation evidence together.

---

### Task 6: Final verification and cleanup

**Files:**
- Verify: all files changed by Tasks 1-5
- Create or update: `experiments/raw-results/animehacker-tq3-0/2026-07-18/docx-structural-qa.json`

**Interfaces:**
- Consumes: completed implementation and evidence.
- Produces: review-ready branch with no uncommitted files or unresolved test failures.

- [ ] **Step 1: Run WB-03 evidence reconciliation**

Require runtime/quality terminal evidence, response hashes, no blank table cells, no literal `N/A`, and accurate AH-09/AH-10 classifications.

- [ ] **Step 2: Run DOCX, revision-control, and workspace validators**

Run `audit_animehacker_docx.py`, `Validate-Workbook-Revision-Control.py`, and `Validate-Controlled-Testing-Workspace.ps1` with process-scoped execution-policy bypass.

- [ ] **Step 3: Run the complete unit suite**

```powershell
python -m unittest discover -s scripts/testing/tests -p 'test_*.py'
```

Expected: zero failures.

- [ ] **Step 4: Audit final diff and evidence exceptions**

Run `git diff --check`, inspect every register/workbook change, preserve raw model-output whitespace where hashes require it, remove only verified in-repository temporary generation folders, and confirm no transient locks are staged.

- [ ] **Step 5: Commit final verification artefacts and confirm clean status**

Commit any regenerated QA/reconciliation artefacts, then verify `git status --short` is empty and record the final commit IDs for handoff.
