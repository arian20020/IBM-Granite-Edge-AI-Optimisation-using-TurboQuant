# Official OpenVINO WB-04 Formal Retest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute and fully reconcile WB-04 against official OpenVINO `2026.2.1` and OpenVINO GenAI `2026.2.1.0`, with complete activation, performance, memory, utilization, quality, safety, and negative-capability evidence.

**Architecture:** A typed Python package under `scripts/testing/official_openvino/` owns the frozen matrix, source/package audits, activation parsing, measurements, safety, quality validation, state, and reconciliation. Thin command-line controllers perform acquisition, conversion, formal execution, quality adjudication, and workbook finalization. Evidence is append-only under a campaign-date root; controlled files change only after row-level validation.

**Tech Stack:** Python 3.11, OpenVINO 2026.2.1, OpenVINO GenAI 2026.2.1.0, official tagged Git sources, PowerShell, Windows CIM/PDH/GPU counters, `unittest`, python-docx.

## Global Constraints

- Pin OpenVINO `2026.2.1` and OpenVINO GenAI `2026.2.1.0` exactly.
- Preserve the frozen WB-04 IDs and official-versus-experimental capability boundary.
- Use one pilot, one excluded warm-up, and exactly three accepted measured repetitions per runnable formal configuration.
- Capture load/compile time, TTFT, prompt tok/s, TPOT, decode tok/s, generation duration, peak working set/private RAM, minimum available RAM, KV MiB, GPU memory, and CPU/GPU mean-median-peak-count.
- Require runtime activation and device/fallback proof; accepted properties alone are insufficient.
- Apply the frozen harsh P1-P6 rubric without format or precision bonuses.
- Keep a 2,048 MiB available-physical-RAM floor for guarded 8B rows.
- Never overwrite evidence; resume only validated checkpoints.
- Leave no blank workbook cells or bare `N/A`; unsupported and blocked rows require sourced terminal classifications.

---

### Task 1: Freeze and validate the complete WB-04 matrix

**Files:**
- Create: `experiments/manifests/official-openvino/retest-matrix.json`
- Create: `scripts/testing/official_openvino/__init__.py`
- Create: `scripts/testing/official_openvino/matrix.py`
- Create: `scripts/testing/tests/test_official_openvino_matrix.py`

**Interfaces:**
- Produces: `OpenVINOCase`, `load_matrix(path: Path) -> list[OpenVINOCase]`, `select_cases(...)`, and exact phase/metric/quality/safety requirements for every controlled ID.

- [ ] **Step 1: Write the failing matrix tests**

```python
def test_matrix_contains_every_wb04_id_once(self):
    cases = load_matrix(MATRIX)
    expected = ({f"OV-B{i:02d}" for i in range(1, 13)} |
                {f"OV-C{i:02d}" for i in range(1, 7)} |
                {f"OV-{i:02d}" for i in range(1, 11)} |
                {f"OV-TQS-{i:02d}" for i in range(1, 13)} |
                {f"OV-TQ-{i:02d}" for i in range(1, 21)})
    self.assertEqual({case.test_id for case in cases}, expected)
    self.assertEqual(len(cases), len(expected))
```

Also test duplicate/unknown IDs, exact contexts and K/V combinations, required P1-P6 rows, context series, norm ablations, and `OV-TQ-16/17` guarded status.

- [ ] **Step 2: Run RED**

Run: `python -m unittest scripts.testing.tests.test_official_openvino_matrix -v`

Expected: FAIL because the module and matrix do not exist.

- [ ] **Step 3: Implement the typed loader and frozen JSON matrix**

Validate enums for phase, device, weight precision, K/V algorithm, K/V precision, status, and guard. Require the complete formal metric set and quality flag where applicable.

- [ ] **Step 4: Run GREEN**

Run: `python -m unittest scripts.testing.tests.test_official_openvino_matrix -v`

Expected: PASS.

- [ ] **Step 5: Commit**

Commit message: `test(openvino): freeze WB-04 formal matrix`.

### Task 2: Acquire official releases and create the isolated environment

**Files:**
- Create: `scripts/testing/acquire_official_openvino.ps1`
- Create: `scripts/testing/official_openvino/acquisition.py`
- Create: `scripts/testing/tests/test_official_openvino_acquisition.py`
- Modify: `experiments/manifests/official-openvino/README.md`

**Interfaces:**
- Produces: `audit_checkout(...)`, `audit_environment(...)`, and acquisition evidence containing exact tags, full SHAs, remotes, clean status, submodules, source hashes, installed packages, wheel metadata, devices, drivers, Python, CMake/compiler, RAM, and OS.

- [ ] **Step 1: Write failing audit tests**

```python
def test_audit_rejects_wrong_tag_or_dirty_checkout(self):
    with self.assertRaisesRegex(ValueError, "expected tag 2026.2.1"):
        audit_checkout(fixture(tag="master"), expected_tag="2026.2.1")
    with self.assertRaisesRegex(ValueError, "dirty checkout"):
        audit_checkout(fixture(tag="2026.2.1", dirty=True), expected_tag="2026.2.1")
```

Test OpenVINO GenAI `2026.2.1.0`, package-version equality, missing CPU/GPU inventory, BOM-safe JSON, and secret-free environment capture.

- [ ] **Step 2: Run RED**

Run: `python -m unittest scripts.testing.tests.test_official_openvino_acquisition -v`

Expected: FAIL because acquisition interfaces are missing.

- [ ] **Step 3: Implement acquisition and run it**

Create `.venv-official-openvino-2026.2.1`, install exact release packages and conversion dependencies, clone both signed tags into campaign-specific external directories, and write evidence under `experiments/raw-results/official-openvino/2026-07-19/acquisition/` and `environment/`. Do not record tokens or unrelated environment values.

- [ ] **Step 4: Verify package imports and device enumeration**

Run the venv Python to import `openvino` and `openvino_genai`, print versions, enumerate `Core().available_devices`, and verify recorded package/source hashes.

- [ ] **Step 5: Commit**

Commit message: `test(openvino): pin official 2026.2.1 environment`.

### Task 3: Prove official codec and diagnostic boundaries

**Files:**
- Create: `scripts/testing/official_openvino/source_audit.py`
- Create: `scripts/testing/run_official_openvino_diagnostics.py`
- Create: `scripts/testing/tests/test_official_openvino_source_audit.py`
- Create: `scripts/testing/tests/test_official_openvino_diagnostics.py`

**Interfaces:**
- Produces: `CodecBoundary`, `audit_codec_boundary(source_roots)`, `classify_diagnostic(...)`, and reconciled results for `OV-B01`-`OV-B12` and `OV-TQS-01`-`OV-TQS-12`.

- [ ] **Step 1: Write failing source/diagnostic tests**

```python
def test_turbo_claim_requires_enum_properties_and_runtime_allocation(self):
    evidence = fixture_source(algorithm="TURBO", precisions=("u3", "u4"))
    runtime = fixture_runtime(accepted=True, allocation_proven=False)
    self.assertFalse(classify_activation(evidence, runtime).activated)
```

Test independent K/V properties, norm switch, packed bytes, CPU SDPA preconditions, QJL/Polar absence, GPU fallback, official test exit codes, and zero failed diagnostic tests.

- [ ] **Step 2: Run RED**

Run: `python -m unittest scripts.testing.tests.test_official_openvino_source_audit scripts.testing.tests.test_official_openvino_diagnostics -v`

Expected: FAIL because audit/diagnostic modules are absent.

- [ ] **Step 3: Implement and execute source/API audit plus official diagnostics**

Search the exact tagged source and package runtime, preserve matched files/lines/hashes, run available official CPU/GPU and cache diagnostics, and execute all 12 short capability combinations. Each row records accepted, activated, expected/actual bytes, fallback, actual device, and terminal classification.

- [ ] **Step 4: Verify reconciliation**

Run focused tests and a diagnostic reconciler that rejects a nonzero official-test failure count or an activation claim lacking allocation evidence.

- [ ] **Step 5: Commit**

Commit message: `test(openvino): audit official codec capability`.

### Task 4: Convert and validate Granite model variants

**Files:**
- Create: `scripts/testing/official_openvino/conversion.py`
- Create: `scripts/testing/convert_official_openvino_models.py`
- Create: `scripts/testing/tests/test_official_openvino_conversion.py`

**Interfaces:**
- Produces: `ConversionSpec`, `validate_conversion(spec, output)`, manifests for `OV-C01`-`OV-C06`, and resolved model paths consumed by runtime execution.

- [ ] **Step 1: Write failing conversion tests**

```python
def test_conversion_requires_model_tokenizer_config_hashes_and_load_probe(self):
    result = fixture_conversion(tokenizer_hash=None)
    with self.assertRaisesRegex(ValueError, "tokenizer hash"):
        validate_conversion(result)
```

Test exact source revision, command/version, IR XML/BIN presence, tokenizer/config validation, load probe, precision identity, and safe 8B classification.

- [ ] **Step 2: Run RED**

Run: `python -m unittest scripts.testing.tests.test_official_openvino_conversion -v`

Expected: FAIL because conversion support is missing.

- [ ] **Step 3: Implement conversion controller and execute OV-C01-OV-C06 serially**

Use official Optimum Intel/OpenVINO conversion commands pinned in the manifest. Preserve stdout/stderr, commands, durations, output inventories, hashes, and a CPU load/generation probe. Guard 8B conversion/load with the 2,048 MiB floor.

- [ ] **Step 4: Verify all conversion rows**

Run focused tests and validate that every conversion row is either load-proven or carries a sourced terminal classification without substitution.

- [ ] **Step 5: Commit**

Commit message: `test(openvino): validate Granite model conversions`.

### Task 5: Build complete OpenVINO measurement and safety collection

**Files:**
- Create: `scripts/testing/official_openvino/metrics.py`
- Create: `scripts/testing/measure_official_openvino.py`
- Create: `scripts/testing/tests/fixtures/fake_openvino_runtime.py`
- Create: `scripts/testing/tests/test_official_openvino_metrics.py`

**Interfaces:**
- Produces: `Measurement`, `summarize_samples(samples)`, request-window utilization sampler, GPU memory collector, process-tree cleanup proof, and atomic `measurement.json`.

- [ ] **Step 1: Write failing metric tests**

```python
def test_summary_requires_three_samples_and_all_utilization_statistics(self):
    samples = [complete_sample() for _ in range(2)]
    with self.assertRaisesRegex(ValueError, "exactly three"):
        summarize_samples(samples)
    del samples[0]["gpu_percent"]["mean"]
```

Cover TTFT boundary, load time, prompt/decode/TPOT math, memory peaks, KV bytes, GPU dedicated/shared memory, CPU/GPU mean-median-peak-count, invalid rows, emergency stop, timeout, descendant cleanup, and atomic evidence writes.

- [ ] **Step 2: Run RED**

Run: `python -m unittest scripts.testing.tests.test_official_openvino_metrics -v`

Expected: FAIL because the measurement module is missing.

- [ ] **Step 3: Implement collector against the fake runtime**

Use monotonic clocks for inference timing, CIM/psutil-compatible process memory, Windows GPU engine/memory counters, absolute-deadline polling, a 2,048 MiB guarded floor, and request-window markers. Preserve raw utilization samples.

- [ ] **Step 4: Run GREEN and a diagnostic IR smoke measurement**

Require every metric and utilization statistic to be present or precisely classified before a sample is valid.

- [ ] **Step 5: Commit**

Commit message: `test(openvino): collect complete WB-04 statistics`.

### Task 6: Execute baselines, formal TurboQuant rows, and harsh quality

**Files:**
- Create: `scripts/testing/official_openvino/runner.py`
- Create: `scripts/testing/official_openvino/state.py`
- Create: `scripts/testing/run_official_openvino_retest.py`
- Create: `scripts/testing/run_official_openvino_quality.py`
- Create: `scripts/testing/adjudicate_official_openvino_quality.py`
- Create: `scripts/testing/tests/test_official_openvino_runner.py`
- Create: `scripts/testing/tests/test_official_openvino_quality.py`

**Interfaces:**
- Produces: exact per-case runtime configs, serial/resumable execution, summaries for `OV-01`-`OV-10` and `OV-TQ-01`-`OV-TQ-20`, six quality records for required rows, and harsh adjudication summaries.

- [ ] **Step 1: Write failing runner/quality tests**

```python
def test_quality_cannot_run_before_complete_runtime(self):
    with self.assertRaisesRegex(RuntimeError, "complete runtime evidence"):
        require_runtime_summary("OV-TQ-03", None)

def test_precision_label_never_changes_quality_score(self):
    self.assertEqual(score("same output", format="TBQ3"),
                     score("same output", format="F16"))
```

Test exact properties/env, selection/resume, unique attempts, serial order, norm OFF/ON, context series, GPU/QJL/Polar gates, three samples, six prompt hashes, deterministic caps, and no overwrite.

- [ ] **Step 2: Run RED**

Run: `python -m unittest scripts.testing.tests.test_official_openvino_runner scripts.testing.tests.test_official_openvino_quality -v`

Expected: FAIL because runner and quality controllers are absent.

- [ ] **Step 3: Implement and execute the matrix serially**

Run each row through pilot, warm-up, samples 1-3, activation validation, summary, cleanup, and checkpoint. Update the workbook staging data immediately after each reconciled row. Run P1-P6 only for complete required configurations and adjudicate with the frozen rubric.

- [ ] **Step 4: Audit terminal execution state**

Require every controlled runtime ID to be complete or accurately terminal, no active OpenVINO controller remains, and no quality-required runnable row lacks six scores.

- [ ] **Step 5: Commit**

Commit message: `test(openvino): execute official WB-04 matrix`.

### Task 7: Reconcile evidence into controlled registers and WB-04

**Files:**
- Create: `scripts/testing/reconcile_official_openvino_workbook.py`
- Create: `scripts/testing/finalize_official_openvino_workbook.py`
- Create: `scripts/testing/tests/test_official_openvino_reconcile.py`
- Modify: `docs/testing/workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md`
- Modify: relevant `docs/testing/*.csv` operational registers
- Modify: `docs/testing/Workbook-Revision-Register.csv`
- Modify: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`

**Interfaces:**
- Consumes: terminal acquisition, diagnostics, conversion, runtime, quality, cleanup, and adjudication evidence.
- Produces: fully populated WB-04 Markdown, synchronized registers, append-only revision, controlled manifest hashes, and `reconciliation.json`.

- [ ] **Step 1: Write failing reconciliation tests**

```python
def test_workbook_rejects_blank_bare_na_missing_metric_or_quality(self):
    for mutation in (blank_cell, bare_na, missing_gpu_mean, missing_ttft, missing_p6):
        with self.subTest(mutation=mutation.__name__):
            with self.assertRaises(ValueError):
                validate_workbook(mutation(valid_workbook()))
```

Also reject unsourced unsupported claims, activation/property mismatch, failed official diagnostics, nonzero cleanup counts, stale manifest hashes, and non-append-only revisions.

- [ ] **Step 2: Run RED**

Run: `python -m unittest scripts.testing.tests.test_official_openvino_reconcile -v`

Expected: FAIL because WB-04 reconciliation does not exist.

- [ ] **Step 3: Implement validation-first finalization**

Validate all evidence before editing controlled files. Render every workbook cell from validated records, append the revision row, update registers and hashes, and preserve UTF-8 BOM requirements. Unsupported/blocked fields receive explicit evidence-linked prose.

- [ ] **Step 4: Run GREEN and evidence reconciliation**

Run focused tests and the reconciler against the real campaign root. Require zero unresolved failures and complete P1-P6 for every runnable required row.

- [ ] **Step 5: Commit**

Commit message: `docs(testing): complete official OpenVINO workbook`.

### Task 8: Generate WB-04 DOCX and perform final independent verification

**Files:**
- Create: `scripts/testing/audit_official_openvino_docx.py`
- Create: `scripts/testing/tests/test_official_openvino_docx.py`
- Modify: `docs/testing/workbooks/generated/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx`
- Create: `experiments/raw-results/official-openvino/2026-07-19/docx-structural-qa.json`

**Interfaces:**
- Produces: regenerated controlled DOCX plus structural and campaign verification reports.

- [ ] **Step 1: Write failing DOCX audit tests**

```python
def test_docx_requires_all_ids_and_zero_blank_cells(self):
    report = audit_docx(FIXTURE)
    self.assertEqual(report["blank_table_cells"], 0)
    self.assertEqual(report["missing_controlled_ids"], [])
```

Test ZIP integrity, table/cell counts, required IDs, version/revision text, terminal decision fields, and manifest hash synchronization.

- [ ] **Step 2: Run RED**

Run: `python -m unittest scripts.testing.tests.test_official_openvino_docx -v`

Expected: FAIL because the audit module does not exist.

- [ ] **Step 3: Generate and audit the controlled artifact**

Run `Generate-Controlled-Workbooks.py`, apply revision history, and execute the new structural audit. If Word/LibreOffice rendering is available, render every page and inspect layout; otherwise record the unavailable visual-render boundary without weakening structural checks.

- [ ] **Step 4: Run all final gates independently**

Run:

```powershell
python -m unittest discover -s scripts/testing/tests -p "test_*.py" -v
python scripts/testing/reconcile_official_openvino_workbook.py --campaign-root experiments/raw-results/official-openvino/2026-07-19
python scripts/testing/audit_official_openvino_docx.py --docx docs/testing/workbooks/generated/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx --output experiments/raw-results/official-openvino/2026-07-19/docx-structural-qa.json
python scripts/testing/Validate-Workbook-Revision-Control.py
powershell -ExecutionPolicy Bypass -File scripts/testing/Validate-OpenVINO-Codec-Extension.ps1
powershell -ExecutionPolicy Bypass -File scripts/testing/Validate-Controlled-Testing-Workspace.ps1
```

Expected: every command exits 0, every controlled ID is terminal, every required statistic and quality score is present, and no workbook table cell is blank.

- [ ] **Step 5: Commit**

Commit message: `test(openvino): verify completed WB-04 artifact`.
