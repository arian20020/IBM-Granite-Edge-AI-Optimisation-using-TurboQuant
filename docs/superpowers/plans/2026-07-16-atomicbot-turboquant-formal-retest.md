# AtomicBot TurboQuant Formal Retest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a resumable AtomicBot test harness, execute every WB-02 test against the pinned current repository tip, and publish a completely reconciled controlled workbook and evidence set.

**Architecture:** A small Python package owns the matrix, collectors, checkpoint state, aggregation, and workbook reconciliation while reusing the existing llama measurement collectors. Raw evidence remains immutable under the AtomicBot experiment route; normalized JSON is the calculation source for the Markdown/DOCX workbook and testing registers.

**Tech Stack:** Python 3 standard library, `unittest`, PowerShell, CMake/MSVC, llama.cpp/AtomicBot CLI and server binaries, Vulkan SDK, Pandoc/LibreOffice-compatible DOCX generation already used by the repository.

## Global Constraints

- Target `https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant.git`, branch `feature/turboquant-kv-cache`, commit `519f0c594a8e31467d2e2f2cf17054c9e7e11536`.
- Every workbook field must contain a fresh verified value or an evidence-backed `N/A`, `Unsupported`, or `Blocked`; unexplained blanks are forbidden.
- Preserve raw evidence; derive processed results with version-controlled code.
- Run one pilot, one excluded warm-up, and three measured repetitions per applicable configuration.
- TTFT is request submission to the first streamed generated token from an already loaded server.
- Peak RAM is complete process-tree physical working set sampled at no more than 100 ms intervals.
- KV allocation comes from runtime output/property only and duplicate reports are deduplicated.
- Quality uses `GTQ-QUALITY-RUBRIC-v1` without a precision-based bonus.
- Perplexity requires a named, versioned dataset and hash; otherwise report `Blocked` rather than substitute a proxy.
- Execute only one model process at a time; apply timeouts, process-tree cleanup, atomic checkpoints, and high-memory safety gates.
- Do not commit model weights, build trees, caches, secrets, personal data, or unlicensed datasets.

---

### Task 1: AtomicBot matrix and state contracts

**Files:**
- Create: `scripts/testing/atomicbot/__init__.py`
- Create: `scripts/testing/atomicbot/matrix.py`
- Create: `scripts/testing/atomicbot/state.py`
- Create: `scripts/testing/tests/test_atomicbot_matrix.py`
- Create: `experiments/manifests/atomicbot-turboquant/retest-matrix.json`
- Modify: `experiments/manifests/atomicbot-turboquant/README.md`

**Interfaces:**
- Produces: `load_matrix(path: Path) -> list[TestCase]`, `validate_matrix(cases: list[TestCase]) -> None`, `load_state(path: Path) -> dict`, and `checkpoint(path: Path, state: dict) -> None`.
- `TestCase` fields: `test_id`, `model_id`, `model_path_env`, `format`, `backend`, `context`, `guard`, `turbo_type`, `flash_attention`, and `required_metrics`.

- [ ] **Step 1: Write failing contract tests**

Add tests that load `retest-matrix.json`, assert all workbook IDs `AB-B01` through `AB-B08`, `AB-01` through `AB-15`, `AB-KV3-F16-4K`, `AB-KV8-F16-4K`, `AB-08F`, `AB-08Q`, and `AB-15M` occur exactly once, reject duplicate IDs, reject unknown status values, and verify an interrupted checkpoint remains valid JSON by replacing the target atomically.

- [ ] **Step 2: Verify the tests fail**

Run: `python -m unittest scripts.testing.tests.test_atomicbot_matrix -v`  
Expected: import failure for `scripts.testing.atomicbot.matrix`.

- [ ] **Step 3: Implement the typed matrix and atomic state writer**

Use a frozen dataclass for `TestCase`, strict required-key and enum validation, and `tempfile.NamedTemporaryFile(delete=False, dir=path.parent)` followed by `os.replace(temp_name, path)` for checkpoints. Populate the JSON directly from the current WB-02 matrix; use `guard: "memory"` only for the two guarded rows and `guard: "none"` elsewhere.

- [ ] **Step 4: Run the focused tests**

Run: `python -m unittest scripts.testing.tests.test_atomicbot_matrix -v`  
Expected: all matrix and state tests pass.

- [ ] **Step 5: Commit the matrix contract**

Run: `git add scripts/testing/atomicbot scripts/testing/tests/test_atomicbot_matrix.py experiments/manifests/atomicbot-turboquant && git commit -m "test: define AtomicBot retest matrix"`

### Task 2: Accurate AtomicBot measurement parsing and safety gates

**Files:**
- Create: `scripts/testing/atomicbot/metrics.py`
- Create: `scripts/testing/atomicbot/safety.py`
- Create: `scripts/testing/tests/test_atomicbot_metrics.py`
- Create: `scripts/testing/tests/fixtures/atomicbot_compact_timing.txt`
- Modify: `scripts/testing/measure_llama_server.py`
- Modify: `scripts/testing/parse_llama_measurement.py`

**Interfaces:**
- Consumes: raw `events.jsonl`, runtime stdout/stderr, and `measurement.json` from existing collectors.
- Produces: `parse_runtime_metrics(events: list[dict]) -> RuntimeMetrics`, `aggregate_samples(samples: list[RuntimeMetrics]) -> AggregateMetrics`, and `evaluate_memory_gate(required_bytes: int, available_bytes: int, commit_headroom_bytes: int, reserve_bytes: int) -> GateResult`.

- [ ] **Step 1: Write failing parser and gate tests**

Cover `[ Prompt: 28.8 t/s | Generation: 9.4 t/s ]`, verbose llama timing, duplicated KV groups, distinct K/V allocations, missing GPU collectors, three-sample medians plus min/max ranges, request-to-first-token TTFT, process-tree peak working set, insufficient physical memory, and insufficient commit headroom. Assert ambiguous multiple timing matches invalidate the sample instead of selecting one silently.

- [ ] **Step 2: Verify the tests fail**

Run: `python -m unittest scripts.testing.tests.test_atomicbot_metrics -v`  
Expected: missing `scripts.testing.atomicbot.metrics`.

- [ ] **Step 3: Implement parsing, sampling corrections, and gates**

Represent unavailable GPU values as `{value: null, status: "N/A", reason: "collector-unavailable"}`. Extend the server collector to record process-tree working set and private bytes as separate event fields, minimum available RAM, request start, first token, completion, and cleanup outcome. Preserve current `peak_ram_mb`, `kv_mb`, and `ttft_ms` keys for compatibility.

- [ ] **Step 4: Run old and new measurement tests**

Run: `python -m unittest scripts.testing.tests.test_parse_llama_measurement scripts.testing.tests.test_atomicbot_metrics -v`  
Expected: all legacy and AtomicBot tests pass.

- [ ] **Step 5: Commit measurement correctness**

Run: `git add scripts/testing/atomicbot scripts/testing/measure_llama_server.py scripts/testing/parse_llama_measurement.py scripts/testing/tests && git commit -m "test: add accurate AtomicBot metric collection"`

### Task 3: Resumable build and execution runner

**Files:**
- Create: `scripts/testing/run_atomicbot_retest.py`
- Create: `scripts/testing/atomicbot/runner.py`
- Create: `scripts/testing/tests/test_atomicbot_runner.py`
- Create: `scripts/testing/tests/fixtures/fake_atomicbot_runtime.py`
- Modify: `experiments/scripts/atomicbot-turboquant/README.md`

**Interfaces:**
- Consumes: `TestCase`, `GateResult`, metric collectors, environment variables for external source/build/model paths.
- Produces: CLI switches `--dry-run`, `--only`, `--from`, `--skip`, `--resume`, `--replace-attempt`, `--pilot`, and `--matrix`; `execute_case(case: TestCase, context: RunContext) -> CaseResult`.

- [ ] **Step 1: Write failing runner tests**

Use the fake runtime to prove dry-run launches nothing, resume skips only fully reconciled rows, a timeout kills descendants, a simulated interruption leaves the previous state readable, `--only AB-01` cannot run AB-02, completed attempts cannot be overwritten without `--replace-attempt`, and guarded rows produce a persisted blocked record when the memory gate fails.

- [ ] **Step 2: Verify the tests fail**

Run: `python -m unittest scripts.testing.tests.test_atomicbot_runner -v`  
Expected: missing runner module.

- [ ] **Step 3: Implement the runner**

Build command records must include executable, argument array, selected environment, cwd, source commit, timestamps, and exit code. Each row writes `pilot/`, `warmup/`, and `sample-1/` through `sample-3/`; logs stream directly to files. Cleanup uses Windows process-tree termination and records whether descendants remain. Run IDs follow `AB-<ID>-20260716-RNNN` with collision-safe incrementing.

- [ ] **Step 4: Run runner and complete test suites**

Run: `python -m unittest scripts.testing.tests.test_atomicbot_runner -v`  
Run: `python -m unittest discover -s scripts/testing/tests -v`  
Expected: all tests pass and no fake child process remains.

- [ ] **Step 5: Commit the resumable runner**

Run: `git add scripts/testing/run_atomicbot_retest.py scripts/testing/atomicbot/runner.py scripts/testing/tests experiments/scripts/atomicbot-turboquant/README.md && git commit -m "test: add resumable AtomicBot retest runner"`

### Task 4: Quality, perplexity, and workbook reconciliation

**Files:**
- Create: `scripts/testing/atomicbot/quality.py`
- Create: `scripts/testing/atomicbot/reconcile.py`
- Create: `scripts/testing/tests/test_atomicbot_quality.py`
- Create: `scripts/testing/tests/test_atomicbot_reconcile.py`
- Create: `experiments/protocols/atomicbot-turboquant/quality-prompts.json`
- Modify: `experiments/protocols/atomicbot-turboquant/README.md`

**Interfaces:**
- Produces: `score_response(prompt_id: str, output: str, adjudication: dict) -> QualityResult`, `run_perplexity_gate(dataset: Path | None, expected_sha256: str | None) -> GateResult`, and `reconcile_workbook(template: Path, results: dict) -> ReconciliationReport`.

- [ ] **Step 1: Write failing quality and reconciliation tests**

Assert identical evidence receives identical scores regardless of format label; deterministic failures and critical caps lower the score; component totals recalculate to the final 0-10 score; absent/mismatched perplexity fixtures block execution; every WB-02 result cell maps to a source field or allowed status; and blank, `TBD`, `TODO`, or unresolved placeholder cells fail reconciliation.

- [ ] **Step 2: Verify the tests fail**

Run: `python -m unittest scripts.testing.tests.test_atomicbot_quality scripts.testing.tests.test_atomicbot_reconcile -v`  
Expected: missing quality and reconciliation modules.

- [ ] **Step 3: Implement conservative scoring and source mapping**

Store deterministic gate results, adjudicated rubric components, cap reasons, raw-output path, and recalculated total separately. The reconciler must emit one record per workbook field containing `field`, `value`, `source_path`, `source_key`, `status`, and `validated_at`; reject a measured value without a source path.

- [ ] **Step 4: Run quality and reconciliation tests**

Run: `python -m unittest scripts.testing.tests.test_atomicbot_quality scripts.testing.tests.test_atomicbot_reconcile -v`  
Expected: all tests pass.

- [ ] **Step 5: Commit quality controls**

Run: `git add scripts/testing/atomicbot scripts/testing/tests experiments/protocols/atomicbot-turboquant && git commit -m "test: enforce unbiased AtomicBot quality scoring"`

### Task 5: Pin, configure, build, and smoke-test AtomicBot

**Files:**
- Create during execution: `experiments/raw-results/atomicbot-turboquant/2026-07-16/repository-pin.json`
- Create during execution: `experiments/raw-results/atomicbot-turboquant/2026-07-16/build-cpu/`
- Create during execution: `experiments/raw-results/atomicbot-turboquant/2026-07-16/build-vulkan/`
- Modify: `docs/testing/Build-Register.csv`
- Modify: `docs/testing/Repository-Register.csv`
- Modify: `docs/testing/Environment-Register.csv`

**Interfaces:**
- Consumes: external clean clone and build paths supplied to the runner.
- Produces: verified CPU and Vulkan binaries plus build/smoke evidence for `AB-B01` through `AB-B08`.

- [ ] **Step 1: Run preflight and dry-run**

Run: `python scripts/testing/run_atomicbot_retest.py --matrix experiments/manifests/atomicbot-turboquant/retest-matrix.json --dry-run`  
Expected: the exact WB-02 matrix, pin, required model variables, memory gates, and build commands print without launching a model.

- [ ] **Step 2: Clone/fetch and verify the exact source pin**

Use a clean external clone, fetch `feature/turboquant-kv-cache`, checkout detached `519f0c594a8e31467d2e2f2cf17054c9e7e11536`, run `git status --porcelain`, and persist URL, branch, commit, submodule commits, and dirty state. Expected: empty dirty state and exact HEAD match.

- [ ] **Step 3: Configure and build CPU**

Configure a clean Release CPU build with the repository-documented TurboQuant options, capture CMake cache and logs, build required CLI/server/perplexity targets, and run `--version` plus a minimal model smoke test. Expected: every CPU build gate either passes or receives a failure code and corrected rerun with preserved attempts.

- [ ] **Step 4: Configure and build Vulkan safely**

Set the verified SDK path, configure a separate Release build with Vulkan and TurboQuant enabled and optional UI embedding disabled, capture shader-generation/backend evidence, build required targets, and run placement smoke tests. Expected: Vulkan backend loads and runtime placement is visible; otherwise diagnose before the full matrix.

- [ ] **Step 5: Reconcile and commit build evidence**

Cross-check all build statuses against raw logs, update the three registers, run the reconciliation audit for build rows, then commit only compact licensed evidence and register changes with `git commit -m "test: record AtomicBot controlled builds"`.

### Task 6: Execute and reconcile the CPU matrix

**Files:**
- Create during execution: `experiments/raw-results/atomicbot-turboquant/2026-07-16/cpu/`
- Create during execution: `experiments/processed-results/atomicbot-turboquant/2026-07-16/cpu-results.json`
- Modify after each row: `docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md`
- Modify after each row: `docs/testing/Performance-Measurement-Register.csv`
- Modify after each row: `docs/testing/Test-Run-Register.csv`

**Interfaces:**
- Produces: fresh pilot, warm-up, three-sample evidence, aggregate metrics, placement proof, and row reconciliation for all CPU cases.

- [ ] **Step 1: Execute the CPU matrix with resume enabled**

Run the CPU subset through `run_atomicbot_retest.py --resume`, one row at a time. After each row, require a valid state checkpoint and no remaining runtime process before continuing.

- [ ] **Step 2: Cross-check each row immediately**

For each sample, compare parsed prompt/decode speeds, TTFT, RAM, KV, exit status, model/hash, context, format, and backend against raw logs/events. Reject ambiguous or missing signals and rerun only after correcting the harness or command.

- [ ] **Step 3: Execute CPU quality and perplexity supplements**

Capture raw P1-P6 responses under identical controls and score them without inspecting the format label during adjudication. Run perplexity only after fixture name/version/hash verification; otherwise persist the blocked record.

- [ ] **Step 4: Reconcile CPU workbook fields**

Run the reconciler and require zero blanks, zero source-less measurements, correct median/range calculations, and explicit reasons for unavailable GPU-only fields. Expected: every CPU field passes reconciliation.

- [ ] **Step 5: Commit the CPU checkpoint**

Run the testing unit suite and structural checks, then commit compact evidence, processed results, workbook updates, and registers with `git commit -m "test: complete AtomicBot CPU retest"`.

### Task 7: Execute and reconcile the Vulkan and guarded matrix

**Files:**
- Create during execution: `experiments/raw-results/atomicbot-turboquant/2026-07-16/vulkan/`
- Create during execution: `experiments/processed-results/atomicbot-turboquant/2026-07-16/vulkan-results.json`
- Modify after each row: `docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md`
- Modify after each row: `docs/testing/Device-Verification-Register.csv`
- Modify after each row: `docs/testing/Failure-Register.csv`

**Interfaces:**
- Produces: Vulkan placement, dedicated/shared GPU memory disposition, performance, quality, stability, safety-gate, and failure evidence.

- [ ] **Step 1: Execute Vulkan smoke and formal rows**

Run each Vulkan row with explicit layer/offload controls and resume enabled. Require runtime placement evidence, not only backend enumeration, and verify that no unexplained CPU fallback occurred.

- [ ] **Step 2: Apply guarded-row safety checks**

Evaluate `AB-KV8-F16-4K` and `AB-15M` immediately before launch using current available RAM, commit headroom, reserve, expected model/KV requirements, and disk space. Execute only on a passing gate; otherwise record `Blocked` with all gate inputs.

- [ ] **Step 3: Diagnose and rerun genuine failures**

For every failure, preserve the attempt, assign a catalogue code, inspect logs and process cleanup, correct only evidence-backed command/harness/product causes, and rerun under a new attempt ID. Unsupported repository paths receive source/runtime evidence rather than repeated unsafe retries.

- [ ] **Step 4: Reconcile Vulkan workbook fields**

Cross-check all metrics and quality scores, verify dedicated/shared GPU memory labels, require an explicit status for unavailable utilization counters, and run the no-blank/source mapping audit.

- [ ] **Step 5: Commit the Vulkan checkpoint**

Run focused tests and reconciliation, then commit compact evidence, processed results, workbook updates, and failure/device registers with `git commit -m "test: complete AtomicBot Vulkan retest"`.

### Task 8: Final workbook generation, independent verification, and route guidance

**Files:**
- Modify: `docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md`
- Modify: `docs/testing/workbooks/generated/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.docx`
- Modify: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`
- Modify: `docs/testing/Workbook-Revision-Register.csv`
- Modify: `docs/testing/Workbook-Completion-Register.csv`
- Modify: `docs/testing/Evidence-Index.csv`
- Modify: `experiments/processed-results/atomicbot-turboquant/README.md`
- Modify: `docs/testing/README.md`

**Interfaces:**
- Consumes: all reconciled CPU/Vulkan/build/quality/failure outputs.
- Produces: final WB-02 Markdown and DOCX, hashes, revision/completion entries, validation reports, and rerun guidance.

- [ ] **Step 1: Run the final reconciliation audit**

Require every workbook cell to map to raw/processed evidence or an allowed status, recalculate every median/range/quality total independently, compare result identities and units, and fail on blanks, placeholders, stale run IDs, or historical-only values.

- [ ] **Step 2: Regenerate the DOCX and manifests**

Use the repository workbook generator, update SHA-256 hashes and revision metadata, and preserve deterministic inputs. Expected: Markdown and DOCX identify the same revision, target pin, and result set.

- [ ] **Step 3: Render and visually inspect the generated workbook**

Render every DOCX page, inspect tables for clipping, overflow, missing rows, broken headings, and stale fields, correct the source template, regenerate, and repeat until clean.

- [ ] **Step 4: Run all repository validation**

Run `python -m unittest discover -s scripts/testing/tests -v`, workbook structural validation, manifest/hash validation, `git diff --check`, and the repository's documented complete test command. Expected: all checks pass with no orphan runtime process.

- [ ] **Step 5: Commit the completed controlled retest**

Review `git diff --stat` and `git status --short`, confirm no model/build/cache artifacts are staged, then commit with `git commit -m "test: complete AtomicBot controlled retest workbook"`.
