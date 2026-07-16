# Upstream llama.cpp Complete Measurement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Measure peak RAM, runtime KV allocation, and TTFT for UL-01 through UL-13; correct UL-01 quality coverage and UL-13 status; regenerate a fully evidenced WB-01.

**Architecture:** A focused Python launcher owns process-tree creation and 100 ms Windows memory sampling, while a loaded `llama-server` collector measures HTTP request initiation to the first streamed generated token. A separate Python parser validates raw events and computes medians. Existing llama binaries and pinned configurations remain unchanged; the harness adds observation without changing model behavior.

**Tech Stack:** PowerShell 5.1, Python 3.13 standard library, unittest, llama.cpp b9870, MSVC CPU/Vulkan/SYCL builds, Markdown-to-DOCX control scripts.

## Global Constraints

- One warm-up plus three recorded independent samples per row.
- Never estimate missing measurements.
- Preserve all raw evidence and failed attempts.
- UL-13 project inference and upstream edge-suite status are separate conclusions.
- No model, context, quantisation, seed, or backend substitutions.

---

### Task 1: Measurement parser and contract tests

**Files:**
- Create: `scripts/testing/parse_llama_measurement.py`
- Create: `scripts/testing/tests/test_parse_llama_measurement.py`

**Interfaces:**
- Consumes: JSONL event records containing timestamp, process memory, stdout bytes, stderr lines, and exit state.
- Produces: `summarize_measurement(events: list[dict]) -> dict` and `median_valid(samples: list[dict]) -> dict`.

- [ ] Write tests proving aggregate peak memory, KV-line parsing, first-response timing, median selection, and invalid-sample rejection.
- [ ] Run `python -m unittest scripts.testing.tests.test_parse_llama_measurement -v` and verify the tests fail because the parser does not exist.
- [ ] Implement only the parsing and validation needed by those tests.
- [ ] Rerun the unit tests and verify all pass.

### Task 2: Process-tree measurement launcher

**Files:**
- Create: `scripts/testing/measure_llama_run.py`
- Create: `scripts/testing/measure_llama_server.py`
- Modify: `scripts/testing/tests/test_parse_llama_measurement.py`

**Interfaces:**
- Consumes: executable, argument array, output directory, sample ID, backend environment, timeout.
- Produces: `events.jsonl`, `stdout.txt`, `stderr.txt`, `command.json`, and `measurement.json`; the server collector starts timing only after readiness and request initiation.

- [ ] Add failing integration tests using a deterministic child process for process-tree memory/KV collection and a fake streaming server for request-to-first-token timing.
- [ ] Run the integration test and verify failure because the launcher is absent.
- [ ] Implement process start, descendant discovery, 100 ms sampling, loaded-server readiness, streamed-token timing, timeout, process-tree termination, and exit-code recording.
- [ ] Verify the integration test and all parser tests pass.

### Task 3: Correct and complete UL-01 quality coverage

**Files:**
- Create evidence under: `experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-QUALITY-R003/`
- Modify: `experiments/granite_turboquant_intel/processed-results/upstream-llama-cpp/quality-scoring-2026-07-15.md`

- [ ] Run P1-P4 with the frozen Gemma Q4_K_M command and preserve raw outputs.
- [ ] Score each output against the repository rubric, applying exact caps.
- [ ] Replace the provisional P1-only comparison with a P1-P4 mean and explicitly label UL-01 as diagnostic Q4_K_M.

### Task 4: Complete and qualify UL-13

**Files:**
- Use evidence: `experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-R002/`
- Create evidence under: `experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-QUALITY-R001/`
- Modify: `experiments/granite_turboquant_intel/processed-results/upstream-llama-cpp/quality-scoring-2026-07-15.md`

- [ ] Validate R002 exit code, backend, device, layer count, context, and sample count.
- [ ] Run and score P1-P4 on corrected `ONEAPI_DEVICE_SELECTOR=level_zero:gpu`.
- [ ] Record project-workload pass separately from the retained 49/52 upstream edge-suite limitation.

### Task 5: Measure UL-01 through UL-13

**Files:**
- Create evidence under each: `experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-XX/UL-XX-SERVER-METRICS-R001/`
- Create: `experiments/granite_turboquant_intel/processed-results/upstream-llama-cpp/resource-metrics-2026-07-16.json`

- [ ] Run one warm-up and three recorded samples for every row using its frozen command.
- [ ] Validate model hash, backend/device identity, context, KV types, offload level, exit code, and required measurement fields after each row.
- [ ] Compute medians only from three valid samples and preserve raw values.

### Task 6: Update workbook controls

**Files:**
- Modify: `docs/testing/workbooks/text-templates/01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.md`
- Modify: `docs/testing/Workbook-Revision-Register.csv`
- Modify: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`
- Regenerate: `docs/testing/workbooks/generated/01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.docx`

- [ ] Replace all resource `Not measured` cells with validated medians and evidence paths.
- [ ] Correct UL-01 and UL-13 status, quality, failure log, and final decision text.
- [ ] Append a new WB-01 revision, regenerate DOCX, and refresh controlled hashes.
- [ ] Run parser tests, `Validate-Workbook-Revision-Control.py`, `Validate-Controlled-Testing-Workspace.ps1`, `git diff --check`, and a blank/missing-field scan.
- [ ] Render and inspect the DOCX; if the renderer remains unavailable, preserve the exact tooling failure without claiming visual QA passed.
