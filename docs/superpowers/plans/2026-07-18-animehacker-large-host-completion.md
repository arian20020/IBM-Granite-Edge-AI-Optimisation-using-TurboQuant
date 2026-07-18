# Animehacker Large-Host Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a portable, guarded workflow that completes frozen WB-03 rows AH-06, AH-07, and AH-10 on a qualifying 32 GiB+ Windows host and imports only fully validated evidence.

**Architecture:** A focused Python module owns host preflight, immutable manifests, run-directory allocation, command construction, and terminal evidence validation. A thin Python CLI and PowerShell wrapper orchestrate the existing runtime and quality controllers without changing the frozen matrix or 2,048 MiB safety floor. Workbook import is a separate validation-first step so incomplete host runs cannot modify controlled artifacts.

**Tech Stack:** Python 3.11 standard library, PowerShell 7/Windows PowerShell, `unittest`, existing WB-03 controllers and controlled-workbook scripts.

## Global Constraints

- Preserve the frozen AH-06, AH-07, and AH-10 model, cache, context, backend, prompt, and quality definitions.
- Require at least 32 GiB installed physical RAM; expose no production bypass flag.
- Retain the 2,048 MiB minimum available-physical-RAM emergency floor.
- Run rows serially and terminate the complete process tree on failure.
- Never overwrite laptop evidence or an unrelated large-host run.
- Import only three valid formal samples, complete CPU/GPU aggregates, six hashed quality records, proven activation, and zero residual processes.

---

### Task 1: Large-host preflight and immutable run manifest

**Files:**
- Create: `scripts/testing/animehacker/large_host.py`
- Create: `scripts/testing/tests/test_animehacker_large_host.py`

**Interfaces:**
- Produces: `HostInputs`, `PreflightResult`, `preflight(inputs, installed_ram_bytes=None, active_process_names=None)`, `allocate_run_root(parent, date)`, and `write_manifest(run_root, result)`.

- [ ] **Step 1: Write failing preflight tests**

Test that 31.99 GiB is rejected, 32 GiB is accepted when all paths exist, missing models/builds are named, active llama/controller processes are rejected, AH-10 requires Level Zero evidence, run IDs do not collide, and manifests cannot be overwritten. Use temporary files and injected RAM/process/device values so tests never launch a model.

- [ ] **Step 2: Verify the tests fail for the missing module**

Run: `python -m unittest scripts.testing.tests.test_animehacker_large_host -v`

Expected: FAIL with `ModuleNotFoundError` for `scripts.testing.animehacker.large_host`.

- [ ] **Step 3: Implement the minimal preflight module**

Use frozen allowed IDs `frozenset({"AH-06", "AH-07", "AH-10"})`, `MINIMUM_INSTALLED_RAM_BYTES = 32 * 1024**3`, resolved-path checks, injectable probes, SHA-256 model records, an exclusive `manifest.json` create, and `AH-LH-<date>-R####` allocation.

- [ ] **Step 4: Verify focused and existing matrix tests pass**

Run: `python -m unittest scripts.testing.tests.test_animehacker_large_host scripts.testing.tests.test_animehacker_matrix -v`

Expected: PASS.

- [ ] **Step 5: Commit**

Run: `git add scripts/testing/animehacker/large_host.py scripts/testing/tests/test_animehacker_large_host.py && git commit -m "feat(testing): add WB-03 large-host preflight"`.

### Task 2: Portable serial runtime and quality launcher

**Files:**
- Create: `scripts/testing/run_animehacker_large_host.py`
- Create: `scripts/testing/Run-Animehacker-LargeHost.ps1`
- Modify: `scripts/testing/tests/test_animehacker_large_host.py`
- Modify: `scripts/testing/run_animehacker_quality.py`
- Modify: `scripts/testing/tests/test_animehacker_runner.py`

**Interfaces:**
- Consumes: Task 1 `HostInputs`, preflight, run allocation, and manifest writing.
- Produces: `build_runtime_command(config, test_id, runtime_root)`, `build_quality_command(config, test_id, runtime_root, quality_root)`, and `execute(config, runner=subprocess.run) -> Path`.

- [ ] **Step 1: Write failing orchestration tests**

Assert selection rejects all IDs except AH-06/AH-07/AH-10; commands preserve `--include-guarded`, `--minimum-available-ram-mb 2048`, frozen matrix/model/build paths, `--only`, and per-run roots; execution is serial; resume skips only validated phases; nonzero subprocess results stop later phases. Add a failing test requiring complete runtime summaries for AH-06 and AH-07 as well as AH-09/AH-10 before quality.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest scripts.testing.tests.test_animehacker_large_host scripts.testing.tests.test_animehacker_runner -v`

Expected: FAIL because the launcher and expanded runtime-summary gate do not exist.

- [ ] **Step 3: Implement minimal launcher and wrapper**

Build argument arrays rather than shell strings. For each selected row, invoke the existing runtime controller for pilot/formal execution, validate `summary.json`, then invoke quality. Persist phase state and captured stdout/stderr beneath the allocated run. Make the PowerShell wrapper pass explicit paths to the Python CLI and stop on any nonzero exit.

- [ ] **Step 4: Verify GREEN and CLI help**

Run: `python -m unittest scripts.testing.tests.test_animehacker_large_host scripts.testing.tests.test_animehacker_runner -v`

Run: `python scripts/testing/run_animehacker_large_host.py --help`

Expected: all tests PASS and help exits 0 without launching workloads.

- [ ] **Step 5: Commit**

Run: `git add scripts/testing/run_animehacker_large_host.py scripts/testing/Run-Animehacker-LargeHost.ps1 scripts/testing/run_animehacker_quality.py scripts/testing/tests/test_animehacker_large_host.py scripts/testing/tests/test_animehacker_runner.py && git commit -m "feat(testing): orchestrate WB-03 large-host completion"`.

### Task 3: Terminal evidence validator and validation-first importer

**Files:**
- Create: `scripts/testing/import_animehacker_large_host.py`
- Modify: `scripts/testing/animehacker/large_host.py`
- Modify: `scripts/testing/tests/test_animehacker_large_host.py`

**Interfaces:**
- Produces: `validate_terminal_row(test_id, runtime_summary, quality_records, manifest, cleanup) -> ValidatedRow` and `render_workbook_updates(markdown, rows) -> str`.

- [ ] **Step 1: Write failing terminal-validation tests**

Build valid temporary evidence fixtures and independently remove a formal sample, activation record, CPU/GPU mean/median/peak field, formal metric, quality prompt, output hash, model-hash match, or cleanup proof. Assert each mutation is rejected. Assert invalid input leaves a sentinel workbook byte-for-byte unchanged; assert valid input produces complete AH-row text without `Not measured`, `Not scored`, blank table cells, or `N/A`.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest scripts.testing.tests.test_animehacker_large_host -v`

Expected: FAIL because terminal validation/import interfaces are missing.

- [ ] **Step 3: Implement validation and staged workbook rendering**

Parse evidence using UTF-8 with BOM tolerance. Validate all evidence before writing. Render to memory, run row completeness checks, then use a same-directory temporary file and `Path.replace()` for the Markdown update. Append a new revision record through an explicit import command only after evidence validation. Do not regenerate controlled files automatically during unit tests.

- [ ] **Step 4: Verify GREEN**

Run: `python -m unittest scripts.testing.tests.test_animehacker_large_host -v`

Expected: PASS.

- [ ] **Step 5: Commit**

Run: `git add scripts/testing/import_animehacker_large_host.py scripts/testing/animehacker/large_host.py scripts/testing/tests/test_animehacker_large_host.py && git commit -m "feat(testing): validate WB-03 large-host evidence import"`.

### Task 4: Operator guidance and full verification

**Files:**
- Create: `docs/testing/Animehacker-Large-Host-Completion-Guide.md`
- Modify: `docs/testing/workbooks/text-templates/03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.md`
- Modify: `scripts/testing/tests/test_animehacker_large_host.py`

**Interfaces:**
- Consumes: Tasks 1-3 CLI contracts.
- Produces: copy/run/resume/import instructions with exact PowerShell commands and evidence locations.

- [ ] **Step 1: Write a failing documentation-contract test**

Assert the guide names AH-06/AH-07/AH-10, 32 GiB, 2,048 MiB, preflight, resume, evidence root, import command, and explicitly prohibits bypassing the gates or overwriting laptop evidence.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest scripts.testing.tests.test_animehacker_large_host -v`

Expected: FAIL because the guide does not exist.

- [ ] **Step 3: Write the operator guide and workbook handoff note**

Document prerequisites, model/build path arguments, preflight-only invocation, full serial invocation, interruption recovery, evidence transfer, importer invocation, controlled-workbook regeneration, and failure interpretation. Add only a concise handoff reference to WB-03; do not replace current safety classifications before real host evidence exists.

- [ ] **Step 4: Run all verification gates**

Run: `python -m unittest discover -s scripts/testing/tests -p "test_*.py" -v`

Run: `python scripts/testing/reconcile_animehacker_workbook.py`

Run: `python scripts/testing/audit_animehacker_docx.py`

Run: `python scripts/testing/Validate-Workbook-Revision-Control.py`

Run: `powershell -ExecutionPolicy Bypass -File scripts/testing/Validate-Controlled-Testing-Workspace.ps1`

Expected: every command exits 0; no controlled workbook row is falsely marked complete.

- [ ] **Step 5: Commit**

Run: `git add docs/testing/Animehacker-Large-Host-Completion-Guide.md docs/testing/workbooks/text-templates/03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.md scripts/testing/tests/test_animehacker_large_host.py && git commit -m "docs(testing): guide WB-03 large-host completion"`.
