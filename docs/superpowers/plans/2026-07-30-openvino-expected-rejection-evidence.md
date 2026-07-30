# OpenVINO WB-04 Expected-Rejection Evidence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce deterministic, validated fail-closed evidence for every WB-04 expected-rejection configuration without launching generation or inventing metrics.

**Architecture:** A focused library loads the frozen matrix, translates each selected case through the existing execution contract, and proves the precise runtime-property rejection. The same library validates canonical per-probe and aggregate hashes; a thin CLI writes one atomic JSON artifact.

**Tech Stack:** Python 3.13, standard library, existing WB-04 matrix/runtime modules, `unittest`.

## Global Constraints

- Cover `OV-TQS-05..12`, `OV-TQ-18..20`, and separate QJL/Polar probes for `OV-B11`.
- Invoke the real `build_runtime_property_spec`; never launch model generation.
- Record exact case, contract, input, exception, matrix/controller hashes, and cleanup proof.
- Reject missing, duplicate, altered, successful, metric-bearing, or hash-tampered evidence.
- Do not edit campaign-spec, conversion, model manifests, or run heavy inference.

---

### Task 1: Exact Unsupported-Algorithm Errors

**Files:**
- Modify: `scripts/testing/official_openvino/runtime_measurement.py`
- Modify: `scripts/testing/tests/test_official_openvino_measurement.py`

**Interfaces:**
- Consumes: `build_runtime_property_spec(...)`
- Produces: field/value-specific `ValueError` messages for unsupported algorithms.

- [ ] **Step 1: Write failing key/value-label tests**

Add literal assertions for unsupported key `SCALAR`, unsupported value
`POLAR`, and the unchanged GPU TurboQuant CPU-only message.

- [ ] **Step 2: Run RED**

Run:
`python -m unittest scripts.testing.tests.test_official_openvino_measurement.OfficialOpenVINOMeasurementTests.test_unsupported_runtime_algorithms_name_field_and_value -v`

Expected: failure because the current error is generic.

- [ ] **Step 3: Implement minimal field/value validation**

Validate key and value separately and raise:
`unsupported runtime <key|value> algorithm: <LABEL>; expected exact uppercase STANDARD, TBQ3, or TBQ4`.

- [ ] **Step 4: Run GREEN**

Run the focused measurement test module and require zero failures.

### Task 2: Evidence Generator and Validator

**Files:**
- Create: `scripts/testing/official_openvino/expected_rejections.py`
- Create: `scripts/testing/tests/test_official_openvino_expected_rejections.py`

**Interfaces:**
- Produces: `generate_expected_rejection_evidence(matrix_path: Path) -> dict`
- Produces: `validate_expected_rejection_evidence(payload: Mapping, matrix_path: Path) -> dict`
- Produces: `write_expected_rejection_evidence(path: Path, payload: Mapping) -> None`

- [ ] **Step 1: Write failing generation and tamper tests**

Assert the literal 13-probe set, exact exceptions, complete case/contract/input
records, absent metrics, deterministic hashes, and rejection of missing,
duplicate, altered, extra-field, launched-generation, nonzero-cleanup, and
hash-tampered payloads.

- [ ] **Step 2: Run RED**

Run the new module and confirm import failure because the production module
does not exist.

- [ ] **Step 3: Implement minimal canonical controller**

Use sorted compact UTF-8 JSON with a trailing newline. Hash each probe without
its `probe_sha256`, then hash the aggregate without its `aggregate_sha256`.
Validation regenerates the exact expected records and checks strict key sets
and both hash layers.

- [ ] **Step 4: Run GREEN**

Run the new tests and existing matrix/measurement tests with zero failures.

### Task 3: Atomic CLI and Dated Evidence

**Files:**
- Create: `scripts/testing/generate_official_openvino_expected_rejections.py`
- Extend: `scripts/testing/tests/test_official_openvino_expected_rejections.py`
- Create: `experiments/raw-results/openvino-turboquant/2026-07-30/expected-rejections/expected-rejections.json`

**Interfaces:**
- CLI: `--matrix PATH --output PATH`

- [ ] **Step 1: Write failing CLI integration test**

Run the CLI in a temporary directory, require exit zero, validate the written
artifact, and assert a second run is byte-identical.

- [ ] **Step 2: Run RED**

Confirm failure because the CLI does not exist.

- [ ] **Step 3: Implement the thin CLI**

Generate, atomically write, reload, validate, and print a compact validation
summary.

- [ ] **Step 4: Run GREEN and produce evidence**

Run focused and broader official OpenVINO tests, invoke the CLI for the dated
path, validate the result, and confirm byte-identical regeneration.

- [ ] **Step 5: Commit**

Stage only the design, plan, controller, CLI, tests, precise error change, and
dated evidence. Commit with a focused message after fresh verification.
