# Formal U8 Granite 3B Campaign Spec Generation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate deterministic, runner-valid worker specs for every runnable formal U8 Granite 3B matrix row/context and an explicit deterministic manifest for selected expected-rejection rows.

**Architecture:** A focused `campaign_spec` module loads the existing typed matrix and composes the existing execution-contract, runtime-property, and deterministic-workload helpers. A thin CLI validates the supplied clean build/model/cache/output paths and writes one pilot template per runnable row/context; the existing sequence runner replaces the role for warm-up and three formal samples. Expected-rejection rows never become worker specs and are instead recorded in `expected-rejections.json`.

**Tech Stack:** Python 3, `argparse`, `pathlib`, existing WB-04 typed matrix/runtime/workload helpers, pytest.

## Global Constraints

- Select only rows with `phase=formal`, `model=granite-3b`, and `weight_precision=u8`.
- Use `max_new_tokens=4`, `ignore_eos=true`, `seed=42`, and `apply_chat_template=false`.
- Generate worker specs only when `execution_contract(case).expected_outcome == "pass"`.
- Keep `OV-TQ-01` runnable; its cache-precision acceptance depends on runtime telemetry.
- Record every selected expected-rejection row, including `OV-TQ-18`, in a deterministic `expected-rejections.json`; do not create its worker spec.
- Reuse existing helpers for matrix translation, supported runtime properties, and exact-context workloads.
- Do not run inference and do not modify `scripts/testing/official_openvino/conversion.py`.
- Worker specs contain only fields consumed by the existing worker/sequence contract.

---

### Task 1: Add the fail-closed campaign generator and CLI

**Files:**

- Create: `scripts/testing/official_openvino/campaign_spec.py`
- Create: `scripts/testing/generate_official_openvino_specs.py`
- Test: `scripts/testing/tests/test_official_openvino_campaign_spec.py`

**Interfaces:**

- Consumes: `load_matrix(Path) -> list[OpenVINOCase]`, `execution_contract(OpenVINOCase) -> OpenVINOExecutionContract`, `build_runtime_property_spec(...) -> dict`, and `build_context_workload(int) -> dict`.
- Produces: `generate_formal_u8_granite3b_specs(*, matrix_path: Path, build_root: Path, model_path: Path, cache_root: Path, output_root: Path) -> dict[str, object]`.

- [ ] **Step 1: Write failing generation-contract tests**

Create real-behavior tests using the frozen matrix and temporary clean build/model/cache directories. Require 20 runnable row/context specs, one expected-rejection record for `OV-TQ-18`, exact `OV-TQ-01` scalar U8 properties, exact asymmetric and norm-ablation properties, context-specific deterministic prompts, the fixed generation settings, and an exact allowlist of worker-spec fields.

- [ ] **Step 2: Run the targeted tests and verify RED**

Run:

```powershell
python -m pytest scripts/testing/tests/test_official_openvino_campaign_spec.py -q
```

Expected: collection fails because `scripts.testing.official_openvino.campaign_spec` does not exist.

- [ ] **Step 3: Implement the minimal pure generator and writer**

Validate that the build root contains exactly one `py_openvino_genai*.pyd` and `openvino_genai.dll`, that the supplied model/cache roots exist, and that the output root is absent or empty. Build every spec in memory before writing. Use the typed matrix contract, supported property builder, exact-context workload, resolved model/cache paths, and atomic JSON writes. Reject an empty target selection, unsafe test IDs, invalid paths, unexpected runnable routes, or any selected expected-rejection row accidentally reaching the worker-spec path.

- [ ] **Step 4: Add and test the thin CLI**

Require `--matrix`, `--build-root`, `--model-path`, `--cache-root`, and `--output-root`. Print a sorted JSON summary and return zero after generation. A subprocess test must prove `--help` works from the repository root without importing OpenVINO or running inference.

- [ ] **Step 5: Run targeted and adjacent tests**

Run:

```powershell
python -m pytest scripts/testing/tests/test_official_openvino_campaign_spec.py scripts/testing/tests/test_official_openvino_workload.py scripts/testing/tests/test_official_openvino_matrix.py scripts/testing/tests/test_official_openvino_measurement.py scripts/testing/tests/test_measure_official_openvino_sequence.py -q
```

Expected: all selected tests pass.

- [ ] **Step 6: Verify the exact diff and commit**

Run the full Python testing suite, inspect `git diff --check`, confirm `conversion.py` is absent from this task's diff, then commit only the new plan, generator, CLI, and tests:

```powershell
git add docs/superpowers/plans/2026-07-30-openvino-formal-u8-campaign-spec-generation.md scripts/testing/official_openvino/campaign_spec.py scripts/testing/generate_official_openvino_specs.py scripts/testing/tests/test_official_openvino_campaign_spec.py
git commit -m "test(openvino): generate formal runtime specs"
```
