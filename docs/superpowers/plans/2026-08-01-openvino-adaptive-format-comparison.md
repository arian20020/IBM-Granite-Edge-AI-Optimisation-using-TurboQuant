# OpenVINO Adaptive Format Comparison Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and execute a guarded, resumable OpenVINO comparison campaign that measures U4 STANDARD, U8 STANDARD, FP16 STANDARD, U8+TBQ4, and U8+TBQ3 from context 512 upward, records every required runtime and P1–P6 quality metric, identifies this laptop's safe boundaries, and publishes an independently verified WB-04 v1.9/WR-037 release.

**Architecture:** Keep the historical WB-04 v1.8 matrix, evidence, and finalizer immutable. Add a comparison-specific matrix, artifact inventory, spec generator, adaptive breadth-first controller, isolated quality path, evidence reconciler, and atomic publisher; every published value is recomputed from immutable raw evidence rather than trusted from a summary. The controller launches one owned process at a time, uses separate 4,096 MiB launch and 2,048 MiB emergency floors, stops only the affected candidate at a confirmed boundary, and publishes a workbook/register checkpoint immediately after each completed or terminal configuration/context.

**Tech Stack:** Python 3.13, pytest, OpenVINO 2026.2.1, OpenVINO GenAI 2026.2.1.0, PowerShell telemetry sampling, psutil, Windows Job Objects, JSON/SHA-256 evidence receipts, Markdown/CSV/DOCX release artifacts, Git.

## Global Constraints

- Work only in the existing `testing/openvino-turboquant-recovery` worktree; preserve all unrelated and pre-existing untracked evidence.
- Do not modify `experiments/manifests/official-openvino/retest-matrix.json`, the 2026-07-30 accepted raw evidence, or the v1.8/WR-036 release contract.
- Use the new matrix `experiments/manifests/official-openvino/adaptive-format-comparison-matrix-v1.json` and raw root `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/`.
- Formal identities are exactly `OV-11`, `OV-12`, `OV-13`, `OV-TQ-21`, and `OV-TQ-22`; contexts are exactly `512`, `1024`, `2048`, `4096`, and `8192`.
- Run breadth-first by context and, within each context, in this exact order: `OV-11`, `OV-TQ-22`, `OV-TQ-21`, `OV-12`, `OV-13`.
- Use exactly the declared input context and exactly four generated tokens for every formal runtime sample.
- Run one guarded pilot, one excluded warmup, and exactly three accepted samples per successful configuration/context.
- Require at least 4,096 MiB available RAM before every process launch and retain the mandatory 2,048 MiB in-run emergency floor; neither threshold is configurable below its minimum.
- Run one owned model worker at a time. Never terminate VS Code, user applications, or unrelated processes.
- Retry a clean guarded failure once only after zero owned survivors and RAM recovery. Two matching guarded failures confirm a boundary. Do not retry after OS instability, incomplete cleanup, an emergency cleanup action, a failed RAM query, or failure to restore the launch reserve.
- Promote a candidate on runtime pass even when quality is `quality-blocked`; exclude that row from quality ranking until exactly P1–P6 have valid adjudications.
- Run P1–P5 in five separate fresh workers and both turns of P6 in one sixth fresh worker.
- Apply `GTQ-QUALITY-RUBRIC-v1` blindly and harshly: no format bonus, deterministic gates first, rubric caps enforced, both pairwise presentation orders reviewed, and manual adjudication required for critical-gate failures, judge disagreement greater than one point, or ranking reversal.
- Use MiB, not decimal MB. Record zero GPU utilisation only when a functioning sampler produced the zero observations.
- Successful tables contain no blank cells or `N/A`, `NA`, `TBD`, `TODO`, `TBC`, `-`, or em-dash placeholders. Terminal attempts appear only in a compact stage/reason/evidence list and never receive fabricated metrics.
- Report runtime-capable and fully-comparable boundaries separately. Treat FP16 artifact preparation failure separately from inference capability.
- Publish WB-04 as version `1.9`, revision `WR-037`, dated `2026-08-01`; update repository CSV text directly and atomically.
- Do not declare an overall winner without a shared context and complete P1–P6 quality evidence for every compared candidate.
- A checkpoint publication is authoritative only after raw evidence validation, independent aggregate recomputation, Markdown/DOCX generation, CSV reconciliation, and hash verification all succeed.
- Focused tests, the full Python suite, applicable .NET tests, workbook validation, DOCX structural audit, and `git diff --check` must pass before release. If page rendering is unavailable, record that limitation and do not claim visual QA.

## File Responsibility Map

| Path | Responsibility |
| --- | --- |
| `scripts/testing/official_openvino/artifact_inventory.py` | Validate U4/U8/FP16 artifact bindings and produce an explicit FP16 preparation terminal when needed. |
| `scripts/testing/prepare_official_openvino_adaptive_artifacts.py` | Guarded artifact-inventory/FP16-preparation CLI; never launches inference. |
| `experiments/manifests/official-openvino/adaptive-format-comparison-matrix-v1.json` | Immutable declaration of five comparison identities, contexts, artifact bindings or preparation terminal, and formal controls. |
| `scripts/testing/build_official_openvino_adaptive_matrix.py` | Deterministically materialize the immutable matrix from the validated artifact inventory before any formal launch. |
| `scripts/testing/official_openvino/matrix.py` | Historical loader plus additive comparison-matrix validator. |
| `scripts/testing/official_openvino/adaptive_campaign_spec.py` | Generate exactly 25 controlled specs when all artifacts exist and explicit preparation-terminal records otherwise. |
| `scripts/testing/generate_official_openvino_adaptive_specs.py` | Thin CLI for comparison spec generation. |
| `scripts/testing/official_openvino/adaptive_metrics.py` | Lossless runtime sample projection and independent three-sample aggregation. |
| `scripts/testing/official_openvino/runtime_process.py` | Separate launch reserve from in-run emergency floor and retain all raw process telemetry. |
| `scripts/testing/measure_official_openvino.py` | Propagate both RAM floors, revalidate identity before every role, and expose typed attempt failures. |
| `scripts/testing/official_openvino/adaptive_campaign.py` | Pure ladder/state/retry/boundary policy plus resumable orchestration. |
| `scripts/testing/run_official_openvino_adaptive_comparison.py` | Campaign CLI that wires runtime, quality, reconciliation, and checkpoint publication callbacks. |
| `scripts/testing/build_official_openvino_boundary_index.py` | Scan only an explicitly named historical evidence root, validate exact equivalence, and emit a hash-bound reusable-boundary index. |
| `scripts/testing/official_openvino/adaptive_quality.py` | Six-worker governed P1–P6 capture and resume validation, leaving the historical quality path intact. |
| `scripts/testing/run_official_openvino_adaptive_quality.py` | Single-row isolated quality CLI used by the controller and recovery runs. |
| `scripts/testing/adjudicate_official_openvino_adaptive_quality.py` | Blind scoring, two-order pairwise review, disagreement/reversal gates, and final unblinding. |
| `scripts/testing/official_openvino/comparison_reconcile.py` | Independently validate raw runtime/quality/terminal evidence and derive shared contexts and boundaries. |
| `scripts/testing/build_official_openvino_comparison_release_evidence.py` | Build an explicit hash-bound release input from controller state; never discover evidence by directory scan. |
| `scripts/testing/finalize_official_openvino_comparison_workbook.py` | Render and validate only the WB-04 v1.9 comparison sections. |
| `scripts/testing/publish_official_openvino_comparison.py` | Atomically synchronize Markdown, DOCX, registers, audit evidence, and publication state after each step. |
| `scripts/testing/official_openvino/docx_audit.py` | Preserve v1.8 audit behavior and add a v1.9 comparison profile. |
| `scripts/testing/audit_official_openvino_docx.py` | Select the comparison audit profile with `--comparison-release`. |

---

### Task 0: Restore a clean, trustworthy baseline

**Files:**
- Modify: `scripts/testing/adjudicate_official_openvino_quality.py:523-584`
- Modify: `scripts/testing/tests/test_adjudicate_official_openvino_quality.py:694-786`

**Interfaces:**
- Consumes: `_snapshot_lexical_raw_root(path: Path)` and `_require_governed_raw_root_unchanged(...)`.
- Produces: ancestry identity that detects replacement/aliasing without treating a legitimate sibling-directory metadata change as raw-evidence mutation.

- [ ] **Step 1: Record the baseline without touching evidence**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_metrics.py scripts/testing/tests/test_official_openvino_quality_worker.py scripts/testing/tests/test_official_openvino_quality_campaign.py scripts/testing/tests/test_adjudicate_official_openvino_quality.py
```

Expected: the current Windows baseline reproduces only these two failures, both reporting `raw quality root ancestry changed during validation`: `test_governed_capture_projects_six_blind_rows_without_private_identity` and `test_governed_capture_preserves_scoring_caps_and_unblinding`. If the failure set differs, stop implementation and save the exact output under `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/preflight/baseline-tests.txt` using the repository's evidence writer rather than a shell redirection.

- [ ] **Step 2: Add a failing regression that distinguishes sibling activity from path replacement**

Add this test:

```python
def test_governed_raw_root_allows_unrelated_sibling_creation_but_rejects_replacement(
    tmp_path: Path,
):
    raw_root = tmp_path / "raw" / "quality"
    raw_root.mkdir(parents=True)
    resolved, ancestry = module._snapshot_lexical_raw_root(raw_root)
    (tmp_path / "rendered").mkdir()
    assert module._snapshot_lexical_raw_root(raw_root)[0] == resolved
    assert module._same_lexical_ancestry(
        ancestry,
        module._snapshot_lexical_raw_root(raw_root)[1],
    )
    moved = tmp_path / "raw-moved"
    raw_root.parent.rename(moved)
    (tmp_path / "raw").mkdir()
    (tmp_path / "raw" / "quality").mkdir()
    assert not module._same_lexical_ancestry(
        ancestry,
        module._snapshot_lexical_raw_root(tmp_path / "raw" / "quality")[1],
    )
```

- [ ] **Step 3: Run the regression and verify the intended failure**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_adjudicate_official_openvino_quality.py::test_governed_raw_root_allows_unrelated_sibling_creation_but_rejects_replacement
```

Expected: FAIL because `_same_lexical_ancestry` does not exist.

- [ ] **Step 4: Implement the minimal stable ancestry comparison**

Keep device, inode/file ID, mode, creation identity, link count, Windows attributes, and reparse tag; intentionally ignore directory size and mtime for ancestors because creating a sibling legitimately changes them:

```python
def _stable_path_identity(value: tuple[int, ...]) -> tuple[int, ...]:
    dev, ino, mode, _size, _mtime, ctime, nlink, attrs, tag = value
    return dev, ino, mode, ctime, nlink, attrs, tag


def _same_lexical_ancestry(
    before: tuple[tuple[str, tuple[int, ...]], ...],
    after: tuple[tuple[str, tuple[int, ...]], ...],
) -> bool:
    return len(before) == len(after) and all(
        left_path == right_path
        and _stable_path_identity(left_identity)
        == _stable_path_identity(right_identity)
        for (left_path, left_identity), (right_path, right_identity)
        in zip(before, after, strict=True)
    )
```

Use `_same_lexical_ancestry` for the two ancestry comparisons in `_require_governed_raw_root_unchanged`; retain the exact raw-root child-set and capture-file snapshots unchanged.

- [ ] **Step 5: Verify the quality baseline and full starting suite**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_adjudicate_official_openvino_quality.py
python -m pytest -q
```

Expected: both commands PASS; the full-suite count is at least the previously recorded `787 passed` and contains zero failures.

- [ ] **Step 6: Commit only the baseline fix**

```powershell
git add scripts/testing/adjudicate_official_openvino_quality.py scripts/testing/tests/test_adjudicate_official_openvino_quality.py
git commit -m "fix(openvino): stabilize governed quality ancestry checks"
```

---

### Task 1: Add guarded artifact inventory and an explicit FP16 preparation boundary

**Files:**
- Create: `scripts/testing/official_openvino/artifact_inventory.py`
- Create: `scripts/testing/prepare_official_openvino_adaptive_artifacts.py`
- Create: `scripts/testing/tests/test_official_openvino_artifact_inventory.py`
- Reuse unchanged: `scripts/testing/official_openvino/conversion.py`

**Interfaces:**
- Consumes: `validate_artifact_manifest(manifest_path: Path, *, expected_precision: str | None = None)` from `conversion.py`, `available_ram_bytes() -> int | None`, and owned-process cleanup primitives from `owned_process_guard.py`.
- Produces: `ArtifactBinding`, `ArtifactPreparationOutcome`, `resolve_artifact_binding(...)`, and `prepare_adaptive_artifacts(...)` for matrix construction.

- [ ] **Step 1: Write failing artifact-inventory tests**

Add these exact public-contract assertions using temporary canonical manifests built with the existing conversion test helpers:

```python
def test_existing_u4_and_u8_manifests_are_hash_bound(tmp_path: Path) -> None:
    u4 = make_valid_manifest(tmp_path, precision="u4", artifact_id="u4-a")
    u8 = make_valid_manifest(tmp_path, precision="u8", artifact_id="u8-a")
    result = prepare_adaptive_artifacts(
        u4_manifest=u4,
        u8_manifest=u8,
        fp16_manifest=None,
        output_root=tmp_path / "inventory",
        prepare_fp16=lambda output: terminal_fp16(output, "not-present"),
    )
    assert result.bindings["u4"].manifest_sha256 == sha256_file(u4)
    assert result.bindings["u8"].manifest_sha256 == sha256_file(u8)


def test_fp16_preparation_failure_is_not_an_inference_failure(tmp_path: Path) -> None:
    result = prepared_inventory(tmp_path, fp16_status="memory-gate-not-run")
    fp16 = result.bindings["f16"]
    assert fp16.status == "artifact-preparation-terminal"
    assert fp16.terminal_stage == "artifact-preparation"
    assert fp16.terminal_receipt_sha256 == sha256_file(fp16.terminal_receipt_path)


def test_fp16_executor_requires_4096_mib_and_never_lowers_runtime_floor(
    tmp_path: Path,
) -> None:
    with pytest.raises(ValueError, match="launch reserve"):
        prepare_adaptive_artifacts(
            **base_inputs(tmp_path),
            launch_reserve_mib=4095,
            emergency_floor_mib=2048,
        )
    with pytest.raises(ValueError, match="emergency floor"):
        prepare_adaptive_artifacts(
            **base_inputs(tmp_path),
            launch_reserve_mib=4096,
            emergency_floor_mib=2047,
        )
```

- [ ] **Step 2: Run the new tests to prove the contract is absent**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_artifact_inventory.py
```

Expected: collection FAIL because `artifact_inventory` does not exist.

- [ ] **Step 3: Implement immutable artifact outcomes and canonical receipts**

Use these types and constants:

```python
MIB = 1024**2
MIN_LAUNCH_RESERVE_MIB = 4096
MIN_EMERGENCY_FLOOR_MIB = 2048


@dataclass(frozen=True)
class ArtifactBinding:
    precision: str
    status: str
    artifact_id: str | None
    model_root: Path | None
    manifest_path: Path | None
    manifest_sha256: str | None
    terminal_stage: str | None
    terminal_receipt_path: Path | None
    terminal_receipt_sha256: str | None


@dataclass(frozen=True)
class ArtifactPreparationOutcome:
    bindings: Mapping[str, ArtifactBinding]
    inventory_path: Path
    inventory_sha256: str


def prepare_adaptive_artifacts(
    *,
    u4_manifest: Path,
    u8_manifest: Path,
    fp16_manifest: Path | None,
    output_root: Path,
    launch_reserve_mib: int = MIN_LAUNCH_RESERVE_MIB,
    emergency_floor_mib: int = MIN_EMERGENCY_FLOOR_MIB,
    prepare_fp16: Callable[[Path], ArtifactBinding] = run_guarded_fp16_preparation,
) -> ArtifactPreparationOutcome:
    if launch_reserve_mib < MIN_LAUNCH_RESERVE_MIB:
        raise ValueError("launch reserve cannot be below 4096 MiB")
    if emergency_floor_mib < MIN_EMERGENCY_FLOOR_MIB:
        raise ValueError("emergency floor cannot be below 2048 MiB")
```

The FP16 preparation worker may either validate an already downloaded preconverted OpenVINO artifact or run the immutable-source download/export commands already recorded by `convert_official_openvino_models.py`. It must run as a guarded child process, write command/environment/stdout/stderr/memory/cleanup hashes, and return `artifact-preparation-terminal` rather than raising an inference failure when the resource gate prevents preparation. It may not run inside the controller process.

- [ ] **Step 4: Add a CLI that records, but never disguises, a missing FP16 artifact**

The parser must expose exactly these user-facing inputs:

```python
parser.add_argument("--u4-manifest", type=Path, required=True)
parser.add_argument("--u8-manifest", type=Path, required=True)
parser.add_argument("--fp16-manifest", type=Path)
parser.add_argument("--output-root", type=Path, required=True)
parser.add_argument("--launch-reserve-mib", type=int, default=4096)
parser.add_argument("--emergency-floor-mib", type=int, default=2048)
```

Exit `0` for a validated inventory even when FP16 has an explicit preparation terminal; exit nonzero for malformed U4/U8 evidence, hash drift, unsafe threshold requests, cleanup failure, or an uncategorized preparation crash.

- [ ] **Step 5: Verify focused and historical conversion behavior**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_artifact_inventory.py scripts/testing/tests/test_official_openvino_conversion.py
```

Expected: PASS with no modification to historical conversion fixtures.

- [ ] **Step 6: Commit the artifact boundary implementation**

```powershell
git add scripts/testing/official_openvino/artifact_inventory.py scripts/testing/prepare_official_openvino_adaptive_artifacts.py scripts/testing/tests/test_official_openvino_artifact_inventory.py
git commit -m "feat(openvino): add guarded adaptive artifact inventory"
```

---

### Task 2: Freeze the five-identity comparison matrix and generate exact workload specs

**Files:**
- Modify: `scripts/testing/official_openvino/matrix.py:13-276`
- Create: `scripts/testing/build_official_openvino_adaptive_matrix.py`
- Create: `scripts/testing/official_openvino/adaptive_campaign_spec.py`
- Create: `scripts/testing/generate_official_openvino_adaptive_specs.py`
- Create: `scripts/testing/tests/test_official_openvino_adaptive_matrix.py`
- Create: `scripts/testing/tests/test_official_openvino_adaptive_campaign_spec.py`
- Preserve unchanged: `scripts/testing/official_openvino/campaign_spec.py`
- Preserve unchanged: `scripts/testing/generate_official_openvino_specs.py`

**Interfaces:**
- Consumes: `ArtifactPreparationOutcome` from Task 1, `OpenVINOCase`, `load_matrix`, `execution_contract`, `build_context_workload`, and `build_runtime_property_spec`.
- Produces: `COMPARISON_IDS`, `COMPARISON_CONTEXTS`, `COMPARISON_RUN_ORDER`, `load_adaptive_comparison_matrix(path)`, and `generate_adaptive_format_comparison_specs(...)`.

- [ ] **Step 1: Add failing matrix tests for identity, fairness, contexts, and guards**

In this task, `MATRIX` is a pytest fixture produced under `tmp_path` by the new matrix builder from synthetic valid U4/U8/FP16 artifact receipts; the canonical repository matrix is deliberately frozen only in Task 10.

```python
def test_comparison_matrix_declares_exact_ids_contexts_and_run_order() -> None:
    cases = load_adaptive_comparison_matrix(MATRIX)
    assert tuple(case.test_id for case in cases) == COMPARISON_RUN_ORDER
    assert {case.test_id for case in cases} == COMPARISON_IDS
    assert all(case.contexts == COMPARISON_CONTEXTS for case in cases)


def test_comparison_matrix_binds_one_u8_artifact_to_standard_tbq4_and_tbq3() -> None:
    by_id = {case.test_id: case for case in load_adaptive_comparison_matrix(MATRIX)}
    identities = {
        (by_id[test_id].artifact_id, by_id[test_id].artifact_manifest_sha256)
        for test_id in ("OV-12", "OV-TQ-21", "OV-TQ-22")
    }
    assert len(identities) == 1


def test_comparison_matrix_separates_weight_precision_from_standard_kv_request() -> None:
    by_id = {case.test_id: case for case in load_adaptive_comparison_matrix(MATRIX)}
    assert by_id["OV-11"].weight_precision == "u4"
    assert by_id["OV-12"].weight_precision == "u8"
    assert by_id["OV-13"].weight_precision == "f16"
    assert all(
        by_id[test_id].runtime_key_algorithm == "STANDARD"
        and by_id[test_id].runtime_value_algorithm == "STANDARD"
        for test_id in ("OV-11", "OV-12", "OV-13")
    )


def test_historical_matrix_contract_is_unchanged() -> None:
    assert len(load_matrix(HISTORICAL_MATRIX)) == 60
    assert sha256_file(HISTORICAL_MATRIX) == HISTORICAL_MATRIX_SHA256
```

- [ ] **Step 2: Verify the comparison loader is missing**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_adaptive_matrix.py
```

Expected: FAIL because `load_adaptive_comparison_matrix` and the matrix builder do not exist.

- [ ] **Step 3: Extend the typed case additively and validate the exact comparison contract**

Add optional fields to `OpenVINOCase` so historical payloads remain valid:

```python
artifact_id: str | None = None
artifact_manifest_path: str | None = None
artifact_manifest_sha256: str | None = None
artifact_status: str | None = None
artifact_terminal_path: str | None = None
artifact_terminal_sha256: str | None = None
```

Add constants and an additive validator:

```python
COMPARISON_IDS = frozenset({"OV-11", "OV-12", "OV-13", "OV-TQ-21", "OV-TQ-22"})
COMPARISON_CONTEXTS = (512, 1024, 2048, 4096, 8192)
COMPARISON_RUN_ORDER = ("OV-11", "OV-TQ-22", "OV-TQ-21", "OV-12", "OV-13")


def load_adaptive_comparison_matrix(path: Path) -> tuple[OpenVINOCase, ...]:
    cases = tuple(load_matrix(path))
    if tuple(case.test_id for case in cases) != COMPARISON_RUN_ORDER:
        raise ValueError("adaptive comparison identities or run order are invalid")
    if any(case.contexts != COMPARISON_CONTEXTS for case in cases):
        raise ValueError("adaptive comparison contexts are invalid")
    if any(case.model != "granite-3b" or case.device != "cpu" for case in cases):
        raise ValueError("adaptive comparison must use Granite 3B on CPU")
    if any(case.guard != "ram-2048-mib" for case in cases):
        raise ValueError("adaptive comparison requires the 2048 MiB runtime guard")
    return _validate_comparison_artifact_bindings(cases)
```

Require a validated artifact manifest path/hash for U4 and U8. Allow only `OV-13` to carry `artifact_status == "artifact-preparation-terminal"`; require its terminal receipt path/hash and forbid runtime spec generation for that row. If FP16 exists, require its normal manifest binding. Reject every other missing binding.

- [ ] **Step 4: Implement deterministic matrix materialization from Task 1's inventory**

Create five `formal` cases in exact `COMPARISON_RUN_ORDER`. Set all contexts to `[512,1024,2048,4096,8192]`, `device`/`requested_device` to CPU, `guard` to `ram-2048-mib`, `quality_required` and numeric metrics to true, and `required_metrics` to the exact `FORMAL_METRICS` set. Use these execution identities:

```json
{
  "OV-11": {"weight_precision":"u4","k_algorithm":"standard","v_algorithm":"standard","k_precision":"f16","v_precision":"f16","runtime_key_algorithm":"STANDARD","runtime_value_algorithm":"STANDARD","execution_route":"stateful-standard","attention_path":"stateful_sdpa_standard"},
  "OV-TQ-22": {"weight_precision":"u8","k_algorithm":"tbq3","v_algorithm":"tbq3","k_precision":"u3","v_precision":"u3","runtime_key_algorithm":"TBQ3","runtime_value_algorithm":"TBQ3","execution_route":"patched-stateful","attention_path":"stateful_sdpa_reference_codec"},
  "OV-TQ-21": {"weight_precision":"u8","k_algorithm":"tbq4","v_algorithm":"tbq4","k_precision":"u4","v_precision":"u4","runtime_key_algorithm":"TBQ4","runtime_value_algorithm":"TBQ4","execution_route":"patched-stateful","attention_path":"stateful_sdpa_reference_codec"},
  "OV-12": {"weight_precision":"u8","k_algorithm":"standard","v_algorithm":"standard","k_precision":"f16","v_precision":"f16","runtime_key_algorithm":"STANDARD","runtime_value_algorithm":"STANDARD","execution_route":"stateful-standard","attention_path":"stateful_sdpa_standard"},
  "OV-13": {"weight_precision":"f16","k_algorithm":"standard","v_algorithm":"standard","k_precision":"f16","v_precision":"f16","runtime_key_algorithm":"STANDARD","runtime_value_algorithm":"STANDARD","execution_route":"stateful-standard","attention_path":"stateful_sdpa_standard"}
}
```

Copy the frozen source/build receipt objects from the historical matrix. Populate every artifact path and SHA-256 from Task 1's canonical inventory, never by hand. For `OV-12`, `OV-TQ-21`, and `OV-TQ-22`, copy the same U8 artifact ID, path, and manifest SHA-256 byte-for-byte. Expose this CLI so Task 10 can freeze the repository matrix only after the real artifact inventory exists:

```text
build_official_openvino_adaptive_matrix.py --artifact-inventory INVENTORY_PATH --historical-matrix HISTORICAL_MATRIX_PATH --output OUTPUT_PATH
```

The CLI refuses to overwrite an existing non-identical matrix. Unit tests materialize their matrix fixture under `tmp_path`; no live artifact preparation or inference occurs in this task.

- [ ] **Step 5: Write failing exact-spec tests**

```python
def test_generation_writes_all_five_identities_at_all_five_contexts(tmp_path: Path) -> None:
    result = generate_adaptive_format_comparison_specs(
        matrix_path=MATRIX,
        build_root=BUILD_ROOT,
        artifact_inventory_path=INVENTORY,
        cache_root=tmp_path / "cache",
        output_root=tmp_path / "specs",
    )
    assert result["runtime_spec_count"] == 25
    assert result["terminal_count"] == 0


def test_generation_keeps_fixed_four_token_workload_and_exact_context(tmp_path: Path) -> None:
    result = generate_specs(tmp_path)
    for path in result["runtime_spec_paths"]:
        spec = json.loads(Path(path).read_text(encoding="utf-8"))
        assert spec["max_new_tokens"] == 4
        assert spec["workload"]["actual_input_tokens"] == spec["context_tokens"]


def test_generation_records_missing_fp16_as_artifact_preparation_boundary(
    tmp_path: Path,
) -> None:
    result = generate_specs(tmp_path, fp16_terminal=True)
    assert result["runtime_spec_count"] == 20
    assert result["terminal_count"] == 1
    assert result["terminals"][0]["test_id"] == "OV-13"
    assert result["terminals"][0]["stage"] == "artifact-preparation"
```

- [ ] **Step 6: Implement the focused generator and thin CLI**

Use this public signature:

```python
def generate_adaptive_format_comparison_specs(
    *,
    matrix_path: Path,
    build_root: Path,
    artifact_inventory_path: Path,
    cache_root: Path,
    output_root: Path,
) -> dict[str, object]:
```

Write each runtime spec to `{output_root}/{test_id}/{context}/runtime-spec.json` and a canonical `spec-index.json` containing the matrix hash, artifact-inventory hash, every spec hash, and any FP16 preparation-terminal receipt. The CLI arguments must be `--matrix`, `--build-root`, `--artifact-inventory`, `--cache-root`, and `--output-root`; it generates specs only and must never import or launch OpenVINO inference.

- [ ] **Step 7: Run focused and historical regression tests**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_adaptive_matrix.py scripts/testing/tests/test_official_openvino_adaptive_campaign_spec.py scripts/testing/tests/test_official_openvino_matrix.py scripts/testing/tests/test_official_openvino_campaign_spec.py scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py
```

Expected: PASS, including the unchanged historical matrix hash/count assertion.

- [ ] **Step 8: Commit the matrix and spec generator**

```powershell
git add scripts/testing/official_openvino/matrix.py scripts/testing/build_official_openvino_adaptive_matrix.py scripts/testing/official_openvino/adaptive_campaign_spec.py scripts/testing/generate_official_openvino_adaptive_specs.py scripts/testing/tests/test_official_openvino_adaptive_matrix.py scripts/testing/tests/test_official_openvino_adaptive_campaign_spec.py
git commit -m "feat(openvino): freeze adaptive comparison matrix"
```

---

### Task 3: Separate RAM safety thresholds and retain every formal runtime metric

**Files:**
- Create: `scripts/testing/official_openvino/adaptive_metrics.py`
- Modify: `scripts/testing/official_openvino/runtime_process.py:177-690`
- Modify: `scripts/testing/measure_official_openvino.py:699-1270`
- Create: `scripts/testing/tests/test_official_openvino_adaptive_metrics.py`
- Modify: `scripts/testing/tests/test_official_openvino_measurement.py`
- Modify: `scripts/testing/tests/test_measure_official_openvino_sequence.py`

**Interfaces:**
- Consumes: raw governed attempt records, `measurement_sample(...)`, `summarize_samples(...)`, `build_campaign_identity(...)`, and owned-process cleanup evidence.
- Produces: `build_adaptive_runtime_sample(record, source_path)`, `summarize_adaptive_runtime_samples(samples)`, and typed `MeasurementSequenceFailure` records used by Task 4.

- [ ] **Step 1: Add failing tests for the two-threshold guard**

```python
def test_guard_uses_4096_launch_reserve_and_2048_emergency_floor() -> None:
    calls = governed_process_calls(
        launch_minimum_available_ram_bytes=4096 * MIB,
        emergency_minimum_available_ram_bytes=2048 * MIB,
    )
    assert calls.prelaunch_floor == 4096 * MIB
    assert calls.runtime_floor == 2048 * MIB


@pytest.mark.parametrize(
    ("launch_mib", "emergency_mib", "message"),
    [(4095, 2048, "launch"), (4096, 2047, "emergency")],
)
def test_guard_rejects_lower_configured_floors(
    launch_mib: int,
    emergency_mib: int,
    message: str,
) -> None:
    with pytest.raises(ValueError, match=message):
        run_governed_process(
            **governed_inputs(),
            launch_minimum_available_ram_bytes=launch_mib * MIB,
            emergency_minimum_available_ram_bytes=emergency_mib * MIB,
        )
```

- [ ] **Step 2: Run guard tests and confirm the old single-floor API fails**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_measurement.py -k "4096 or emergency or configured_floors"
```

Expected: FAIL because the separate parameters are not accepted.

- [ ] **Step 3: Split launch and emergency floors through the call chain**

Use these constants and signature in `runtime_process.py`:

```python
MIB = 1024**2
MIN_LAUNCH_AVAILABLE_RAM_BYTES = 4096 * MIB
MIN_EMERGENCY_AVAILABLE_RAM_BYTES = 2048 * MIB


def run_governed_process(
    *,
    command: Sequence[str],
    environment: Mapping[str, str],
    output_root: Path,
    timeout_seconds: float,
    launch_minimum_available_ram_bytes: int = MIN_LAUNCH_AVAILABLE_RAM_BYTES,
    emergency_minimum_available_ram_bytes: int = MIN_EMERGENCY_AVAILABLE_RAM_BYTES,
    sampler_script: Path,
) -> dict[str, Any]:
```

Validate both minimums before creating the output directory. Use the launch reserve only for prelaunch admission and the emergency floor only during/post execution. Propagate matching `launch_minimum_available_ram_mib=4096` and `emergency_minimum_available_ram_mib=2048` parameters through `run_single_measurement(...)`, `_run_measurement_sequence_locked(...)`, and `run_measurement_sequence(...)`.

- [ ] **Step 4: Add failing lossless-sample and aggregate tests**

```python
def test_adaptive_sample_retains_tokens_exit_cleanup_and_identity_hashes() -> None:
    sample = build_adaptive_runtime_sample(complete_record(), Path("sample-1.json"))
    assert sample["num_input_tokens"] == 512
    assert sample["num_generated_tokens"] == 4
    assert sample["exit_code"] == 0
    assert sample["fallback_count"] == 0
    assert sample["residual_owned_process_count"] == 0
    assert set(sample["identity_hashes"]) == {
        "artifact_manifest_sha256",
        "prompt_sha256",
        "matrix_sha256",
        "build_provenance_sha256",
        "command_sha256",
        "evidence_sha256",
    }


def test_adaptive_summary_reports_all_required_statistics() -> None:
    result = summarize_adaptive_runtime_samples(three_complete_samples())
    assert result["timing"]["ttft_ms"]["mean"] == pytest.approx(20.0)
    assert result["timing"]["ttft_ms"]["median"] == pytest.approx(20.0)
    assert result["memory"]["peak_working_set_mib"]["median"] == pytest.approx(3000.0)
    assert result["memory"]["peak_working_set_mib"]["worst_max"] == pytest.approx(3100.0)
    assert result["memory"]["available_ram_mib"]["global_min"] == pytest.approx(2200.0)
    assert result["utilisation"]["cpu_percent"]["count"] == 9
    assert result["utilisation"]["gpu_percent"]["count"] == 9
    assert result["utilisation"]["gpu_percent"]["mean"] == pytest.approx(0.0)


@pytest.mark.parametrize("field,value", [
    ("num_generated_tokens", 3),
    ("fallback_count", 1),
    ("exit_code", 1),
    ("residual_owned_process_count", 1),
])
def test_adaptive_summary_rejects_invalid_formal_samples(field: str, value: int) -> None:
    samples = three_complete_samples()
    samples[1][field] = value
    with pytest.raises(ValueError, match=field):
        summarize_adaptive_runtime_samples(samples)
```

- [ ] **Step 5: Run the adaptive metric tests and verify they fail before implementation**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_adaptive_metrics.py
```

Expected: collection FAIL because `adaptive_metrics` does not exist.

- [ ] **Step 6: Implement lossless projection and independent aggregation**

Expose exactly these metric groups:

```python
TIMING_FIELDS = (
    "load_ms",
    "ttft_ms",
    "prompt_tps",
    "tpot_ms",
    "decode_tps",
    "generation_duration_ms",
)
MEMORY_FIELDS = (
    "peak_working_set_mib",
    "peak_private_mib",
    "available_ram_mib",
    "kv_mib",
    "gpu_memory_peak_mib",
)
IDENTITY_HASH_FIELDS = (
    "artifact_manifest_sha256",
    "prompt_sha256",
    "matrix_sha256",
    "build_provenance_sha256",
    "command_sha256",
    "evidence_sha256",
)
RAW_MIB_FIELDS = {
    "peak_working_set_mb": "peak_working_set_mib",
    "peak_private_mb": "peak_private_mib",
    "available_ram_min_mb": "available_ram_mib",
    "kv_mb": "kv_mib",
    "gpu_memory_peak_mb": "gpu_memory_peak_mib",
}


def build_adaptive_runtime_sample(
    record: Mapping[str, Any],
    source_path: Path,
) -> dict[str, Any]:
    sample = measurement_sample(record, source_path)
    return _validate_and_enrich_sample(sample, record)


def summarize_adaptive_runtime_samples(
    samples: Sequence[Mapping[str, Any]],
) -> dict[str, Any]:
    if len(samples) != 3:
        raise ValueError("adaptive runtime summary requires exactly three samples")
    _require_consistent_formal_identity(samples)
    return {
        "schema": "official-openvino-adaptive-runtime-summary-v1",
        "samples": [dict(sample) for sample in samples],
        "timing": _summarize_run_scalars(samples, TIMING_FIELDS),
        "memory": _summarize_memory(samples),
        "utilisation": _summarize_pooled_utilisation(samples),
        "activation": _summarize_activation(samples),
        "identity_hashes": _require_consistent_hashes(samples),
    }
```

For timing, retain each of three values and calculate count/mean/median/min/max. For peak memory, retain each run, median, and worst maximum. For available RAM, retain each run minimum and the global minimum. Pool every timestamped CPU/GPU observation across all three runs for mean/median/peak/count. A zero GPU mean is valid only when `gpu_sampler_supported is True`, at least one observation exists in each accepted sample, and every pooled value is zero.

The historical raw keys end in `_mb`, but their collectors divide bytes by `1024**2`. Verify that unit provenance in each raw record, project them to the `_mib` names above without numerically rescaling, and label only the adaptive workbook columns as MiB. Reject a record whose collector/unit receipt does not prove binary MiB.

- [ ] **Step 7: Add typed measurement-sequence failures and role-by-role identity checks**

In `measure_official_openvino.py` add:

```python
@dataclass(frozen=True)
class MeasurementFailureRecord:
    role: str
    record_path: Path | None
    record: Mapping[str, Any] | None
    fingerprint: str


class MeasurementSequenceFailure(RuntimeError):
    def __init__(self, message: str, failure: MeasurementFailureRecord):
        super().__init__(message)
        self.failure = failure
```

Before each of `pilot`, `warmup`, `sample-1`, `sample-2`, and `sample-3`, rebuild the campaign identity and compare artifact, build, matrix, prompt, and runtime-property hashes with the sequence receipt. Raise `MeasurementSequenceFailure` with the canonical attempt record instead of requiring callers to parse a generic error string. Preserve the old message text for compatibility.

- [ ] **Step 8: Verify metrics, guard behavior, identity validation, and regressions**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_adaptive_metrics.py scripts/testing/tests/test_official_openvino_measurement.py scripts/testing/tests/test_measure_official_openvino_sequence.py scripts/testing/tests/test_official_openvino_metrics.py scripts/testing/tests/test_measure_official_openvino_cli.py
```

Expected: PASS. Confirm the existing pooled-utilisation regression still proves the campaign mean is not a median of run medians.

- [ ] **Step 9: Commit runtime safety and metric completeness together**

```powershell
git add scripts/testing/official_openvino/adaptive_metrics.py scripts/testing/official_openvino/runtime_process.py scripts/testing/measure_official_openvino.py scripts/testing/tests/test_official_openvino_adaptive_metrics.py scripts/testing/tests/test_official_openvino_measurement.py scripts/testing/tests/test_measure_official_openvino_sequence.py
git commit -m "feat(openvino): retain complete guarded runtime metrics"
```

---

### Task 4: Implement the resumable breadth-first adaptive controller

**Files:**
- Create: `scripts/testing/official_openvino/adaptive_campaign.py`
- Create: `scripts/testing/run_official_openvino_adaptive_comparison.py`
- Create: `scripts/testing/build_official_openvino_boundary_index.py`
- Create: `scripts/testing/tests/test_official_openvino_adaptive_campaign.py`

**Interfaces:**
- Consumes: comparison matrix/spec index from Task 2, `run_measurement_sequence(...)` and `MeasurementSequenceFailure` from Task 3, `capture_isolated_quality_campaign(...)` from Task 5 through an injected callback, and `publish_reconciled_checkpoint(...)` from Task 9 through an injected callback.
- Produces: canonical `adaptive-campaign-state.json`, immutable per-step controller receipts, candidate boundaries, and `run_adaptive_campaign(config, ...)`.

- [ ] **Step 1: Write failing pure-policy tests before orchestration code**

```python
def test_ladder_runs_breadth_first_in_expected_memory_order() -> None:
    assert build_ladder(valid_matrix())[:10] == (
        ("OV-11", 512), ("OV-TQ-22", 512), ("OV-TQ-21", 512),
        ("OV-12", 512), ("OV-13", 512),
        ("OV-11", 1024), ("OV-TQ-22", 1024), ("OV-TQ-21", 1024),
        ("OV-12", 1024), ("OV-13", 1024),
    )


def test_quality_blocked_does_not_stop_runtime_promotion() -> None:
    state = state_with("OV-11", 512, runtime="passed", quality="quality-blocked")
    assert ("OV-11", 1024) in eligible_steps(state)


def test_confirmed_boundary_prunes_only_that_candidate() -> None:
    state = state_with_confirmed_boundary("OV-12", 1024)
    remaining = eligible_steps(state)
    assert ("OV-12", 2048) not in remaining
    assert ("OV-11", 2048) in remaining


def test_no_intermediate_level_can_be_skipped() -> None:
    state = empty_state()
    state["steps"]["OV-11:512"] = passing_step()
    assert ("OV-11", 1024) in eligible_steps(state)
    assert ("OV-11", 2048) not in eligible_steps(state)
```

- [ ] **Step 2: Run policy tests and confirm the module is absent**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_adaptive_campaign.py -k "ladder or promotion or boundary or intermediate"
```

Expected: collection FAIL because `adaptive_campaign` does not exist.

- [ ] **Step 3: Implement immutable state types and pure eligibility rules**

Use these public names and exact policy constants:

```python
CONTEXTS = (512, 1024, 2048, 4096, 8192)
CANDIDATE_ORDER = ("OV-11", "OV-TQ-22", "OV-TQ-21", "OV-12", "OV-13")
START_RESERVE_MIB = 4096
RUNTIME_FLOOR_MIB = 2048
MAX_GUARDED_ATTEMPTS = 2


@dataclass(frozen=True)
class AdaptiveCampaignConfig:
    matrix_path: Path
    spec_root: Path
    campaign_root: Path
    build_root: Path
    build_provenance_path: Path
    python_executable: Path
    python_site_packages: Path
    openvino_libraries: Path
    sampler_script: Path
    reference_boundary_index: Path | None = None
    max_context: int = 8192


@dataclass(frozen=True)
class StepOutcome:
    test_id: str
    context_tokens: int
    runtime_status: str
    quality_status: str
    attempt_count: int
    failure_fingerprint: str | None
    evidence_path: Path
    evidence_sha256: str


def build_ladder(matrix_path: Path) -> tuple[tuple[str, int], ...]:
    load_adaptive_comparison_matrix(matrix_path)
    return tuple((test_id, context) for context in CONTEXTS for test_id in CANDIDATE_ORDER)


def eligible_steps(state: Mapping[str, Any]) -> tuple[tuple[str, int], ...]:
    return _eligible_breadth_first_steps(_validate_state(state))
```

Persist canonical JSON with atomic replace. The state must bind matrix/spec-index/artifact-inventory/build hashes and reject resume after any drift. A completed or failed attempt directory is immutable and new retries receive monotonically increasing attempt numbers.

- [ ] **Step 4: Add failing retry, recovery, and existing-boundary tests**

```python
def test_first_clean_guarded_failure_retries_same_context_once() -> None:
    runner = FakeRunner([clean_ram_failure("ram-floor"), runtime_pass()])
    result = run_campaign_with(runner)
    assert runner.calls == [("OV-11", 512), ("OV-11", 512)]
    assert result["steps"]["OV-11:512"]["runtime_status"] == "passed"


def test_two_matching_failures_confirm_boundary() -> None:
    runner = FakeRunner([clean_ram_failure("ram-floor"), clean_ram_failure("ram-floor")])
    result = run_campaign_with(runner)
    step = result["steps"]["OV-11:512"]
    assert step["runtime_status"] == "boundary-confirmed"
    assert step["attempt_count"] == 2


@pytest.mark.parametrize("failure", [
    cleanup_failure(),
    os_instability_failure(),
    emergency_cleanup_failure(),
    ram_query_failure(),
    launch_reserve_not_restored_failure(),
])
def test_unsafe_failure_prohibits_retry(failure: MeasurementFailureRecord) -> None:
    runner = FakeRunner([failure])
    result = run_campaign_with(runner)
    assert len(runner.calls) == 1
    assert result["steps"]["OV-11:512"]["runtime_status"] == "safety-boundary"


def test_existing_equivalent_boundary_skips_dangerous_relaunch() -> None:
    evidence = three_equivalent_u8_standard_4096_ram_floor_pilots()
    runner = FakeRunner([])
    result = run_campaign_with(runner, reference_boundary_index=evidence)
    assert ("OV-12", 4096) not in runner.calls
    assert result["steps"]["OV-12:4096"]["runtime_status"] == "boundary-confirmed"


def test_non_equivalent_boundary_evidence_is_rejected() -> None:
    evidence = three_u8_4096_pilots_with_different_artifact_hash()
    with pytest.raises(ValueError, match="equivalent boundary"):
        run_campaign_with(FakeRunner([]), reference_boundary_index=evidence)
```

- [ ] **Step 5: Implement strict failure classification and retry admission**

```python
def classify_runtime_failure(
    failure: MeasurementFailureRecord,
) -> StepOutcome:
    record = _require_failure_record(failure)
    clean = (
        record["cleanup_process_count"] == 0
        and record["residual_owned_process_count"] == 0
        and not record["emergency_actions"]
        and not record.get("os_instability", False)
    )
    category = _failure_category(record)
    retryable = clean and category in {"ram-floor", "functional"}
    return _failure_outcome(record, retryable=retryable)
```

Before any retry or next step, call `available_ram_bytes()` and require at least `4096 * 1024**2`. Require zero owned survivors using only the campaign Job Object. Match retry fingerprints over stage, category, artifact hash, route, context, safety floors, and normalized exit/failure code. Two nonmatching failures produce `inconclusive-safety-boundary`, never a fabricated pass.

- [ ] **Step 6: Implement orchestration with injection seams and immediate checkpoints**

```python
def run_adaptive_campaign(
    config: AdaptiveCampaignConfig,
    *,
    run_runtime: Callable[..., Mapping[str, Any]] = run_measurement_sequence,
    run_quality: Callable[..., Mapping[str, Any]] | None = None,
    publish_checkpoint: Callable[[Path, bool], Mapping[str, Any]] | None = None,
    available_ram: Callable[[], int] = available_ram_bytes,
) -> dict[str, Any]:
    state = load_or_create_state(config)
    for test_id, context in build_ladder(config.matrix_path):
        if context > config.max_context or not step_is_eligible(state, test_id, context):
            continue
        state = execute_or_classify_step(state, test_id, context, config, run_runtime, available_ram)
        if state["steps"][f"{test_id}:{context}"]["runtime_status"] == "passed":
            state = execute_quality_step(state, test_id, context, run_quality)
        save_state_atomically(config.campaign_root / "adaptive-campaign-state.json", state)
        if publish_checkpoint is not None:
            publish_checkpoint(config.campaign_root / "adaptive-campaign-state.json", False)
    return state
```

Hold a whole-campaign lock, but reuse the measurement sequence's finer lock and resume receipts. Publish after every successful or terminal configuration/context, including `quality-blocked`. Never publish a future/unattempted step.

- [ ] **Step 7: Implement the CLI without hidden safety overrides**

The CLI accepts:

```text
--matrix --spec-root --campaign-root --build-root --build-provenance
--python-executable --python-site-packages --openvino-libraries --sampler-script
--reference-boundary-index --max-context --resume --publish-checkpoints --preflight-only
```

`--max-context` must be one of `512,1024,2048,4096,8192`. `--preflight-only` validates every path/hash/spec/property, proves zero owned survivors, reads RAM, and prints the first eligible step without launching a worker or creating an attempt directory. Do not expose flags that lower RAM floors, skip cleanup, bypass identity validation, reuse a quality worker, or force a stopped candidate. Print one compact JSON status containing state path/hash, completed step count, terminal step count, and next eligible step.

Keep Task 4 independently testable: import the Task 5 quality callback and Task 9 publication callback lazily inside `main()` only when their respective operational modes are requested. The pure controller module must remain importable with injected `run_quality=None` and `publish_checkpoint=None` before those later tasks exist.

Add `build_official_openvino_boundary_index.py --matrix MATRIX_PATH --spec-root SPEC_ROOT --historical-root HISTORICAL_ROOT --output OUTPUT_PATH`. It may scan only `HISTORICAL_ROOT`, then must reopen and hash-validate every candidate attempt. Include a boundary only when at least two attempts have the same normalized failure fingerprint and exact artifact/model/device/route/context/launch-floor/emergency-floor identity as a new step. Emit a valid empty index when nothing qualifies. The adaptive controller trusts only the explicit resulting index/hash, never a directory scan.

- [ ] **Step 8: Verify controller policy, resume, and adjacent measurement tests**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_adaptive_campaign.py scripts/testing/tests/test_measure_official_openvino_sequence.py scripts/testing/tests/test_measure_official_openvino_cli.py
```

Expected: PASS, including no relaunch of completed steps and no overwrite of failed attempts.

- [ ] **Step 9: Commit the controller**

```powershell
git add scripts/testing/official_openvino/adaptive_campaign.py scripts/testing/run_official_openvino_adaptive_comparison.py scripts/testing/build_official_openvino_boundary_index.py scripts/testing/tests/test_official_openvino_adaptive_campaign.py
git commit -m "feat(openvino): add resumable adaptive comparison controller"
```

---

### Task 5: Capture P1–P6 in six fresh governed workers

**Files:**
- Modify: `scripts/testing/official_openvino/quality_worker.py:244-360`
- Create: `scripts/testing/official_openvino/adaptive_quality.py`
- Create: `scripts/testing/run_official_openvino_adaptive_quality.py`
- Create: `scripts/testing/tests/test_official_openvino_adaptive_quality.py`
- Preserve historical behavior in: `scripts/testing/official_openvino/quality_campaign.py`

**Interfaces:**
- Consumes: accepted runtime summary and identity, frozen prompt-set/rubric hashes, guarded command runner, OpenVINO model/build environment, and historical quality record validation primitives.
- Produces: `build_quality_prompt_worker_spec(...)`, `run_governed_quality_prompt(...)`, and `capture_isolated_quality_campaign(...)` returning a hash-bound six-receipt campaign summary.

- [ ] **Step 1: Write failing process-isolation tests**

```python
def test_capture_launches_six_fresh_workers_in_p1_p6_order(tmp_path: Path) -> None:
    runner = RecordingGuardRunner(successful_prompt_results())
    result = capture_isolated_quality_campaign(
        accepted_input(tmp_path),
        resume=False,
        run_command=runner,
    )
    assert [call.prompt_id for call in runner.calls] == ["P1", "P2", "P3", "P4", "P5", "P6"]
    assert len({call.process_identity for call in runner.calls}) == 6
    assert result["prompt_receipt_count"] == 6


def test_p6_runs_both_turns_only_inside_the_p6_worker(tmp_path: Path) -> None:
    specs = captured_specs(tmp_path)
    assert all(len(specs[prompt_id]["turns"]) == 1 for prompt_id in ("P1", "P2", "P3", "P4", "P5"))
    assert [turn["turn_id"] for turn in specs["P6"]["turns"]] == ["P6-turn-1", "P6-turn-2"]
    assert specs["P6"]["turns"][1]["history_source_sha256"] == sha256_text(
        specs["P6"]["expected_turn_one_output"]
    )


def test_failed_prompt_preserves_prior_evidence_and_blocks_score(tmp_path: Path) -> None:
    runner = RecordingGuardRunner(successes_then_failure("P4"))
    result = capture_isolated_quality_campaign(
        accepted_input(tmp_path),
        resume=False,
        run_command=runner,
    )
    assert result["status"] == "quality-blocked"
    assert result["completed_prompt_ids"] == ["P1", "P2", "P3"]
    assert "quality_mean" not in result
    assert all(path.exists() for path in result_paths(result, "P1", "P2", "P3", "P4"))


def test_resume_revalidates_six_workers_without_relaunch(tmp_path: Path) -> None:
    first = RecordingGuardRunner(successful_prompt_results())
    capture_isolated_quality_campaign(accepted_input(tmp_path), resume=False, run_command=first)
    second = RecordingGuardRunner([])
    capture_isolated_quality_campaign(accepted_input(tmp_path), resume=True, run_command=second)
    assert second.calls == []
```

- [ ] **Step 2: Run the isolation tests and verify the new path is absent**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_adaptive_quality.py
```

Expected: collection FAIL because `adaptive_quality` does not exist.

- [ ] **Step 3: Add an additive prompt-scoped worker contract**

Keep `execute_quality_worker(spec)` unchanged for historical evidence. Add:

```python
def execute_quality_prompt_worker(spec: Mapping[str, Any]) -> dict[str, Any]:
    prompt_id = require_prompt_id(spec["prompt_id"])
    expected_turn_ids = (
        ("P6-turn-1", "P6-turn-2")
        if prompt_id == "P6"
        else (prompt_id,)
    )
    turns = require_exact_turns(spec["turns"], expected_turn_ids)
    pipeline = load_pipeline_from_hash_bound_spec(spec)
    outcomes = execute_turns_in_order(pipeline, turns)
    return canonical_prompt_worker_result(spec, outcomes)
```

The prompt worker spec must include test ID/context only in the private controller file, not in the response record that enters blind scoring. Bind the runtime-summary, matrix, artifact, prompt-set, rubric, build, runtime-property, command, and worker-spec hashes. P6 turn two must include the actual P6 turn-one output in its in-process conversation history; no other prompt may share pipeline state.

- [ ] **Step 4: Implement per-prompt guarded directories and resume checks**

Use these public signatures:

```python
@dataclass(frozen=True)
class GovernedQualityPromptResult:
    prompt_id: str
    status: str
    prompt_root: Path
    worker_spec_sha256: str
    worker_result_sha256: str
    guard_evidence_sha256: str
    cleanup_process_count: int


def build_quality_prompt_worker_spec(
    campaign: AcceptedQualityCampaign,
    prompt_id: str,
) -> dict[str, Any]:


def run_governed_quality_prompt(
    campaign: AcceptedQualityCampaign,
    prompt_id: str,
    output_root: Path,
    timeout_seconds: float,
    *,
    run_command: Callable[..., Mapping[str, Any]] = run_guarded_command,
) -> GovernedQualityPromptResult:


def capture_isolated_quality_campaign(
    campaign_input: QualityCampaignInput,
    *,
    resume: bool,
    run_command: Callable[..., Mapping[str, Any]] = run_guarded_command,
) -> dict[str, Any]:
```

Each `{quality_root}/{prompt_id}/` contains canonical `worker-spec.json`, `worker-result.json`, `worker.log`, and `guard-evidence.json`. The campaign root contains `capture-summary.json`, which lists exactly six prompt receipt paths/hashes and has its own self-hash. Resume must reopen and fully validate each prior prompt; it may append from the first absent/failed prompt but never overwrite or silently trust it.

- [ ] **Step 5: Enforce the same RAM and cleanup safety as runtime**

Before every prompt worker, require 4,096 MiB available RAM and zero owned survivors. Use the 2,048 MiB emergency floor while it runs. A prompt failure retains its evidence, stops that row's quality sequence, emits `quality-blocked`, and returns control to the runtime ladder. It must not retry automatically; the controller can resume that prompt only in an explicit later recovery run after evidence review.

- [ ] **Step 6: Wire the dedicated quality CLI**

Accept:

```text
--runtime-summary --matrix --prompt-set --rubric --model-path --build-root
--python-executable --python-site-packages --openvino-libraries --sampler-script
--output-root --timeout-seconds --resume
--campaign-state --resume-quality-blocked
```

The explicit single-row arguments are used by tests and targeted diagnostics. In operational mode, `--campaign-state PATH --resume-quality-blocked` is mutually exclusive with `--runtime-summary`, `--model-path`, and `--output-root`; it validates the state and executes each stored `quality_recovery` argument object exactly, in ladder order. The CLI must reject a runtime summary that is not `passed`, is at a different context, has any fallback/survivor, or does not hash-match its raw samples. It prints the capture-summary path/hash and status; it never prints the private blind map or configuration labels with response text.

- [ ] **Step 7: Verify new and historical quality paths**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_adaptive_quality.py scripts/testing/tests/test_official_openvino_quality_worker.py scripts/testing/tests/test_official_openvino_quality_campaign.py
```

Expected: PASS. Existing single-worker historical fixtures remain readable, but new comparison capture tests prove six distinct guards/process identities.

- [ ] **Step 8: Commit isolated quality capture**

```powershell
git add scripts/testing/official_openvino/quality_worker.py scripts/testing/official_openvino/adaptive_quality.py scripts/testing/run_official_openvino_adaptive_quality.py scripts/testing/tests/test_official_openvino_adaptive_quality.py
git commit -m "feat(openvino): isolate adaptive quality prompts"
```

---

### Task 6: Add blinded, two-order, disagreement-aware quality adjudication

**Files:**
- Create: `scripts/testing/adjudicate_official_openvino_adaptive_quality.py`
- Create: `scripts/testing/tests/test_adjudicate_official_openvino_adaptive_quality.py`
- Reuse unchanged: `scripts/testing/adjudicate_official_openvino_quality.py`
- Reuse unchanged: `scripts/testing/official_openvino/quality.py`

**Interfaces:**
- Consumes: six-receipt capture summaries from Task 5, `GTQ-QUALITY-RUBRIC-v1`, frozen P1–P6 prompt set, two independent blind score sheets, two-order pairwise reviews, manual adjudication records, and a private blind map.
- Produces: `build_adaptive_blind_bundle(...)` and `adjudicate_adaptive_quality(...)`, with numeric P1–P6/mean/median/min/max only after every gate passes.

- [ ] **Step 1: Write failing privacy and completeness tests**

```python
def test_blind_bundle_contains_no_test_format_or_precision_identity() -> None:
    bundle = build_adaptive_blind_bundle(valid_capture_summaries())
    encoded = json.dumps(bundle, sort_keys=True)
    for forbidden in ("OV-11", "OV-12", "OV-13", "OV-TQ-21", "OV-TQ-22", "TBQ3", "TBQ4", "u4", "u8", "f16"):
        assert forbidden not in encoded


def test_incomplete_p1_p6_campaign_has_no_numeric_aggregates() -> None:
    with pytest.raises(ValueError, match="exactly P1 through P6"):
        adjudicate_adaptive_quality(**adjudication_inputs(missing_prompt="P5"))


def test_unblinding_occurs_only_after_scoring_validation() -> None:
    inputs = adjudication_inputs(tamper_score_sheet_hash=True)
    with pytest.raises(ValueError, match="score sheet hash"):
        adjudicate_adaptive_quality(**inputs)
    assert inputs["blind_map_reader"].call_count == 0
```

- [ ] **Step 2: Write failing pairwise and manual-adjudication tests**

```python
def test_every_pair_is_reviewed_in_both_presentation_orders() -> None:
    result = adjudicate_adaptive_quality(**complete_adjudication_inputs())
    assert all(pair["orders"] == ["AB", "BA"] for pair in result["pairwise_reviews"])


@pytest.mark.parametrize("condition", [
    "critical-gate",
    "judge-disagreement-over-one",
    "ranking-reversal",
])
def test_required_manual_adjudication_cannot_be_omitted(condition: str) -> None:
    with pytest.raises(ValueError, match="manual adjudication"):
        adjudicate_adaptive_quality(**inputs_triggering(condition, manual_record=None))


def test_complete_scores_publish_all_six_and_four_aggregates() -> None:
    result = adjudicate_adaptive_quality(**complete_adjudication_inputs())
    row = result["configurations"][0]
    assert set(row["prompt_scores"]) == {"P1", "P2", "P3", "P4", "P5", "P6"}
    assert set(row["aggregates"]) == {"mean", "median", "minimum", "maximum"}
```

- [ ] **Step 3: Run the tests and confirm the adaptive adjudicator is absent**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_adjudicate_official_openvino_adaptive_quality.py
```

Expected: collection FAIL because `adjudicate_official_openvino_adaptive_quality` does not exist.

- [ ] **Step 4: Implement the blind bundle as a one-way projection**

```python
def build_adaptive_blind_bundle(
    capture_summaries: Sequence[Path],
    *,
    prompt_set_path: Path,
    rubric_path: Path,
) -> tuple[dict[str, Any], dict[str, str]]:
    validated = validate_six_worker_captures(capture_summaries, prompt_set_path, rubric_path)
    blind_map = assign_random_blind_labels(validated)
    public = project_response_text_and_deterministic_gates(validated, blind_map)
    return canonical_hash_bound(public), canonical_hash_bound(blind_map)
```

The public bundle may contain blind label, prompt ID/hash, output/hash, P6 turn outputs, deterministic gates, and rubric criteria only. Keep test ID, format, precision, artifact, device, model, build, runtime, and codec identity solely in the separately stored private blind map.

- [ ] **Step 5: Implement strict scoring, pairwise, and adjudication validation**

Use this entry point:

```python
def adjudicate_adaptive_quality(
    *,
    scoring_input: Mapping[str, Any],
    judge_score_sheets: Sequence[Mapping[str, Any]],
    pairwise_reviews: Mapping[str, Any],
    manual_adjudications: Mapping[str, Any],
    blind_map_reader: Callable[[], Mapping[str, Any]],
    rubric_path: Path,
    prompt_set_path: Path,
) -> dict[str, Any]:
```

Require exactly two independently hashed judge sheets. For every blind-label pair at a shared context, require one AB and one BA review. Apply deterministic caps before subjective scores. Flag any per-prompt judge difference greater than `1.0`; detect a ranking reversal when AB and BA choose different winners after reversing order. Require a hash-bound manual record naming the evidence and final reason for every flagged prompt/pair. Only after all validation succeeds may the function invoke `blind_map_reader`, unblind, and calculate P1–P6 plus arithmetic mean, median, minimum, and maximum.

For each judge and prompt, compute the weighted score as `0.30*correctness + 0.25*instruction_adherence + 0.20*completeness + 0.15*relevance_clarity + 0.10*stability`, then apply the strictest deterministic cap. If no manual prompt adjudication is required, the final prompt score is the arithmetic mean of the two capped judge scores. If manual adjudication is required, its rubric-grounded capped score replaces that mean. Configuration mean/median/minimum/maximum are computed across the six final prompt scores only; pairwise preference is a consistency check and never adds bonus points.

- [ ] **Step 6: Add CLI modes for bundle construction and final adjudication**

Implement two mutually exclusive commands:

```text
build-bundle --capture-index --prompt-set --rubric --public-output --private-map-output
adjudicate --scoring-input --judge-score-sheet (exactly twice) --pairwise-reviews
           --manual-adjudications --private-map --prompt-set --rubric --output
```

`build-bundle` prints only public bundle path/hash and private-map path/hash, never response text beside identities. `adjudicate` refuses partial scoring rather than emitting numeric placeholders.

- [ ] **Step 7: Verify adaptive and historical adjudication**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_adjudicate_official_openvino_adaptive_quality.py scripts/testing/tests/test_adjudicate_official_openvino_quality.py scripts/testing/tests/test_official_openvino_quality.py
```

Expected: PASS, including deterministic caps and label-independent identical-content scoring.

- [ ] **Step 8: Commit the quality adjudicator**

```powershell
git add scripts/testing/adjudicate_official_openvino_adaptive_quality.py scripts/testing/tests/test_adjudicate_official_openvino_adaptive_quality.py
git commit -m "feat(openvino): add blind adaptive quality adjudication"
```

---

### Task 7: Reconcile release evidence independently from raw attempts

**Files:**
- Create: `scripts/testing/official_openvino/comparison_reconcile.py`
- Create: `scripts/testing/build_official_openvino_comparison_release_evidence.py`
- Create: `scripts/testing/examples/official-openvino-wb04-comparison-reconciliation-input.example.json`
- Create: `scripts/testing/tests/test_official_openvino_comparison_reconcile.py`

**Interfaces:**
- Consumes: comparison matrix, canonical controller state, explicit runtime attempt-sequence paths, six-prompt quality capture/adjudication paths, artifact-preparation terminals, and reference-boundary receipts.
- Produces: `ComparisonRelease`, `build_comparison_release_input(...)`, `build_quality_capture_index(...)`, `reconcile_comparison_release(...)`, `derive_shared_contexts(...)`, `derive_boundaries(...)`, `validate_closed_campaign(...)`, `validate_complete_release(...)`, and self-hashed reconciliation/capture indexes.

- [ ] **Step 1: Write failing runtime recomputation tests**

```python
def test_release_audit_recomputes_runtime_without_trusting_summary(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    corrupt_declared_summary(release_input, field="ttft_ms.mean", value=999999.0)
    result = reconcile_comparison_release(release_input)
    assert result.runtime[ComparisonKey("OV-11", 512)].timing["ttft_ms"]["mean"] == pytest.approx(20.0)


def test_runtime_requires_tokens_activation_cleanup_and_all_hashes(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    remove_raw_field(release_input, "sample-2", "command_sha256")
    with pytest.raises(ValueError, match="command_sha256"):
        reconcile_comparison_release(release_input)


def test_memory_uses_median_worst_case_and_global_ram_minimum(tmp_path: Path) -> None:
    row = reconcile_one_runtime(valid_release_input(tmp_path))
    assert row.memory["peak_private_mib"]["median"] == pytest.approx(2800.0)
    assert row.memory["peak_private_mib"]["worst_max"] == pytest.approx(2900.0)
    assert row.memory["available_ram_mib"]["global_min"] == pytest.approx(2200.0)


def test_utilisation_flattens_all_timestamped_samples(tmp_path: Path) -> None:
    row = reconcile_one_runtime(valid_release_input(tmp_path))
    assert row.utilisation["cpu_percent"]["count"] == 9
    assert row.utilisation["gpu_percent"]["count"] == 9
    assert row.utilisation["cpu_percent"]["mean"] != median_of_run_means(row)
```

- [ ] **Step 2: Write failing comparison, terminal, quality, and hash tests**

```python
def test_shared_cache_comparison_requires_identical_u8_artifact(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path, ids=("OV-12", "OV-TQ-21", "OV-TQ-22"))
    resign_sample_with_different_artifact(release_input, "OV-TQ-22")
    with pytest.raises(ValueError, match="identical U8 artifact"):
        reconcile_comparison_release(release_input)


def test_quality_requires_exactly_p1_through_p6(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    remove_quality_prompt(release_input, "P4")
    result = reconcile_comparison_release(release_input)
    quality = result.quality[ComparisonKey("OV-11", 512)]
    assert quality.status == "quality-blocked"
    assert quality.aggregates is None


def test_boundary_separates_runtime_and_fully_comparable_contexts(tmp_path: Path) -> None:
    result = reconcile_comparison_release(boundary_release_input(tmp_path))
    boundary = result.boundaries["OV-11"]
    assert boundary.highest_runtime_context == 2048
    assert boundary.highest_fully_comparable_context == 1024
    assert boundary.first_confirmed_blocked_context == 4096


def test_terminal_record_contains_no_fabricated_metrics(tmp_path: Path) -> None:
    terminal = reconcile_terminal(valid_terminal_input(tmp_path))
    assert set(terminal) == {"test_id", "context_tokens", "stage", "principal_reason", "evidence_path", "evidence_sha256"}


def test_hash_mismatch_refuses_reconciliation(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    tamper_raw_attempt_without_updating_index(release_input)
    with pytest.raises(ValueError, match="hash"):
        reconcile_comparison_release(release_input)
```

- [ ] **Step 3: Run reconciliation tests and confirm the new module is absent**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_comparison_reconcile.py
```

Expected: collection FAIL because `comparison_reconcile` does not exist.

- [ ] **Step 4: Implement typed comparison outcomes**

```python
@dataclass(frozen=True, order=True)
class ComparisonKey:
    test_id: str
    context_tokens: int


@dataclass(frozen=True)
class ComparisonRuntimeOutcome:
    key: ComparisonKey
    samples: tuple[Mapping[str, Any], Mapping[str, Any], Mapping[str, Any]]
    timing: Mapping[str, Mapping[str, float | int | tuple[float, ...]]]
    memory: Mapping[str, Mapping[str, float | int | tuple[float, ...]]]
    utilisation: Mapping[str, Mapping[str, float | int | tuple[float, ...]]]
    activation: Mapping[str, Any]
    identity_hashes: Mapping[str, str]
    evidence_path: Path
    evidence_sha256: str


@dataclass(frozen=True)
class ComparisonQualityOutcome:
    key: ComparisonKey
    status: str
    prompt_scores: Mapping[str, float] | None
    aggregates: Mapping[str, float] | None
    evidence_path: Path
    evidence_sha256: str


@dataclass(frozen=True)
class BoundaryOutcome:
    test_id: str
    highest_runtime_context: int | None
    highest_fully_comparable_context: int | None
    first_confirmed_blocked_context: int | None
    terminal_stage: str | None
    terminal_evidence_sha256: str | None
```

- [ ] **Step 5: Implement explicit release-input construction with no directory discovery**

```python
def build_comparison_release_input(
    matrix_path: Path,
    campaign_state_path: Path,
    output_path: Path,
) -> dict[str, Any]:
    state = load_and_validate_campaign_state(campaign_state_path, matrix_path)
    references = explicit_references_from_state(state)
    payload = {
        "schema": "official-openvino-comparison-release-input-v1",
        "matrix": file_reference(matrix_path),
        "campaign_state": file_reference(campaign_state_path),
        "steps": references,
    }
    return write_canonical_self_hashed_json(output_path, payload)


def build_quality_capture_index(
    campaign_state_path: Path,
    output_path: Path,
) -> dict[str, Any]:
    state = load_and_validate_campaign_state(campaign_state_path)
    captures = explicit_complete_capture_references(state)
    return write_canonical_self_hashed_json(
        output_path,
        {"schema": "official-openvino-quality-capture-index-v1", "captures": captures},
    )
```

Every step reference names one exact relative path and SHA-256. `build_official_openvino_comparison_release_evidence.py` accepts optional `--quality-capture-index OUTPUT_PATH` and writes the capture index from the same validated state. Do not glob, select newest attempts, or infer quality evidence from directory names. The example JSON must contain a complete synthetic passing runtime+quality step and one compact terminal step with valid 64-character demonstration hashes such as repeated hexadecimal digits; label it schema documentation, never executable evidence.

- [ ] **Step 6: Implement independent reconciliation and eligibility derivation**

```python
def reconcile_comparison_release(
    release_input: Mapping[str, Any] | Path,
) -> ComparisonRelease:
    source = load_comparison_release_input(release_input)
    matrix = load_adaptive_comparison_matrix(source.matrix.path)
    runtime = reconcile_all_runtime_steps(source, matrix)
    quality = reconcile_all_quality_steps(source, runtime)
    terminals = reconcile_all_terminal_steps(source, matrix)
    return ComparisonRelease(
        runtime=runtime,
        quality=quality,
        terminals=terminals,
        shared_cache_contexts=derive_shared_contexts(runtime, ("OV-12", "OV-TQ-21", "OV-TQ-22")),
        shared_standard_contexts=derive_shared_contexts(runtime, ("OV-11", "OV-12", "OV-13")),
        boundaries=derive_boundaries(matrix, runtime, quality, terminals),
    )
```

Reopen pilot/warmup/sample attempt files, verify every path/hash/self-hash and sequence binding, exclude pilot/warmup, and recompute Task 3's summary. Reopen all six quality workers and adjudication inputs, validate blind/unblind hashes, and recompute quality from six prompt scores. A runtime row with incomplete quality remains a valid runtime row and a `quality-blocked` quality outcome.

- [ ] **Step 7: Add separate ladder-closure and final-release validation**

`validate_closed_campaign(release)` must require every candidate to be closed by either: a contiguous pass chain ending in an 8192 runtime result; a first confirmed runtime/safety boundary after its pass chain; or, only for `OV-13`, an artifact-preparation terminal. It must require every runtime pass to have either a complete six-prompt capture awaiting adjudication or a precise quality terminal. `validate_complete_release(release)` calls that validator and then requires every complete capture to have a validated numeric adjudication. Both reject unattempted gaps, skipped contexts, duplicate evidence, or an unexplained missing row. The release-evidence CLI exposes mutually exclusive `--require-ladder-closed` and `--require-complete` modes.

- [ ] **Step 8: Verify reconciliation and historical release isolation**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_comparison_reconcile.py scripts/testing/tests/test_build_official_openvino_release_evidence.py scripts/testing/tests/test_official_openvino_reconcile.py
```

Expected: PASS. The historical v1.8 release builder continues to read only its frozen reconciliation input.

- [ ] **Step 9: Commit release reconciliation**

```powershell
git add scripts/testing/official_openvino/comparison_reconcile.py scripts/testing/build_official_openvino_comparison_release_evidence.py scripts/testing/examples/official-openvino-wb04-comparison-reconciliation-input.example.json scripts/testing/tests/test_official_openvino_comparison_reconcile.py
git commit -m "feat(openvino): reconcile adaptive comparison evidence"
```

---

### Task 8: Render a readable WB-04 v1.9 comparison workbook

**Files:**
- Create: `scripts/testing/finalize_official_openvino_comparison_workbook.py`
- Create: `scripts/testing/tests/test_finalize_official_openvino_comparison_workbook.py`
- Modify at publication time: `docs/testing/workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md`
- Preserve historical implementation: `scripts/testing/finalize_official_openvino_workbook.py`

**Interfaces:**
- Consumes: `ComparisonRelease` from Task 7 and the current WB-04 Markdown template.
- Produces: seven v1.9 comparison sections, `validate_comparison_workbook_text(text, release)`, and `finalize_release(..., require_complete)`.

- [ ] **Step 1: Write failing identity and successful-table validation tests**

```python
def test_v19_identity_is_wr037() -> None:
    text = render_complete_workbook()
    assert "Workbook version: 1.9" in text
    assert "Revision ID: WR-037" in text
    assert "Revision date: 2026-08-01" in text


@pytest.mark.parametrize("placeholder", ["", "N/A", "NA", "TBD", "TODO", "TBC", "-", "—"])
def test_success_tables_have_zero_blank_or_placeholder_cells(placeholder: str) -> None:
    text = render_complete_workbook()
    if placeholder:
        text = corrupt_first_success_cell(text, placeholder)
    else:
        text = corrupt_first_success_cell(text, "")
    with pytest.raises(ValueError, match="successful table"):
        validate_comparison_workbook_text(text, complete_release())


def test_final_write_is_fail_closed(tmp_path: Path) -> None:
    target = tmp_path / "workbook.md"
    target.write_text("known-good\n", encoding="utf-8")
    with pytest.raises(ValueError):
        finalize_release(incomplete_release(), target=target, require_complete=True)
    assert target.read_text(encoding="utf-8") == "known-good\n"
```

- [ ] **Step 2: Write failing comparison and metric-presentation tests**

```python
def test_cache_table_contains_only_shared_complete_contexts() -> None:
    tables = parse_tables(render_complete_workbook())
    rows = tables["U8 cache comparison"]
    assert {row["Test ID"] for row in rows} == {"OV-12", "OV-TQ-21", "OV-TQ-22"}
    assert len({row["U8 artifact SHA-256"] for row in rows}) == 1
    assert all(int(row["Context"]) in shared_cache_contexts() for row in rows)


def test_timing_tables_include_samples_mean_and_median() -> None:
    tables = parse_tables(render_complete_workbook())
    for metric in ("Load ms", "TTFT ms", "Prompt tok/s", "TPOT ms", "Decode tok/s", "Generation ms"):
        assert tables[f"{metric} timing"].headers == (
            "Test ID", "Context", "S1", "S2", "S3", "Mean", "Median",
        )


def test_memory_and_utilisation_tables_are_complete() -> None:
    tables = parse_tables(render_complete_workbook())
    assert tables["Peak process memory"].headers == (
        "Test ID", "Context", "WS S1 MiB", "WS S2 MiB", "WS S3 MiB", "WS median MiB", "WS worst MiB",
        "Private median MiB", "Private worst MiB",
    )
    assert tables["RAM, KV and GPU memory"].headers == (
        "Test ID", "Context", "Available RAM minimum MiB", "KV MiB", "GPU memory peak MiB",
    )
    assert tables["CPU and GPU utilisation"].headers == (
        "Test ID", "Context", "CPU mean %", "CPU median %", "CPU peak %", "CPU samples",
        "GPU mean %", "GPU median %", "GPU peak %", "GPU samples",
    )


def test_quality_table_requires_six_scores() -> None:
    row = parse_quality_row(render_complete_workbook(), "OV-11", 512)
    assert set(row) >= {"P1", "P2", "P3", "P4", "P5", "P6", "Mean", "Median", "Minimum", "Maximum"}
```

- [ ] **Step 3: Write failing boundary, terminal, and winner tests**

```python
def test_boundary_table_reports_both_boundaries() -> None:
    row = boundary_row(render_complete_workbook(), "OV-11")
    assert row["Highest runtime context"] == "2048"
    assert row["Highest fully comparable context"] == "1024"
    assert row["First confirmed blocked context"] == "4096"


def test_terminal_list_is_compact() -> None:
    item = terminal_item(render_complete_workbook(), "OV-12", 4096)
    assert set(item) == {"Identity/context", "Stage", "Principal reason", "Evidence"}
    assert "TTFT" not in item and "CPU" not in item and "Quality" not in item


def test_overall_winner_requires_shared_complete_quality() -> None:
    with pytest.raises(ValueError, match="overall winner"):
        validate_comparison_workbook_text(
            inject_overall_winner(render_runtime_only_workbook()),
            runtime_only_release(),
        )
```

- [ ] **Step 4: Run workbook tests and verify the comparison finalizer is absent**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_finalize_official_openvino_comparison_workbook.py
```

Expected: collection FAIL because the comparison finalizer does not exist.

- [ ] **Step 5: Implement exact v1.9 sections and formatting**

Use:

```python
TARGET_WORKBOOK_VERSION = "1.9"
TARGET_REVISION_ID = "WR-037"
TARGET_REVISION_DATE = "2026-08-01"
COMPARISON_SECTION_TITLES = (
    "U8 STANDARD, TBQ4 and TBQ3 shared-context comparison",
    "U4, U8 and FP16 STANDARD deployment comparison",
    "Complete timing results",
    "Complete memory and CPU/GPU results",
    "P1–P6 and aggregate quality results",
    "Laptop runtime-capable and fully-comparable boundaries",
    "Terminal attempts and hash-bound evidence",
)
```

Preserve the current workbook's project scope, environment, build/provenance, and method material; replace the comparison/result body with these seven sections. STANDARD route labels must use observed evidence, for example `CPU STANDARD; observed f32/f32 state`, never infer K/V state from U4/U8 weights.

- [ ] **Step 6: Implement successful-row and terminal rendering**

Expose:

```python
def render_cache_comparison(release: ComparisonRelease) -> str:
def render_standard_weight_comparison(release: ComparisonRelease) -> str:
def render_timing_results(release: ComparisonRelease) -> str:
def render_memory_utilisation_results(release: ComparisonRelease) -> str:
def render_quality_results(release: ComparisonRelease) -> str:
def render_laptop_boundaries(release: ComparisonRelease) -> str:
def render_terminal_attempts(release: ComparisonRelease) -> str:
```

Sections 1–5 include complete successful rows only. Omit future/unattempted contexts during checkpoints rather than creating empty cells. Runtime-only rows appear in runtime sections and not the numeric quality section. The terminal section is a short Markdown list or four-column table with only identity/context, stage, principal reason, and relative evidence link+SHA-256.

Keep tables narrow enough for the generated DOCX: render one seven-column timing table per timing metric, a compact peak-process-memory table, a compact RAM/KV/GPU-memory table, and a separate CPU/GPU-utilisation table. Do not combine all timing and resource columns into one unreadable table.

- [ ] **Step 7: Implement fail-closed validation and atomic writing**

```python
def finalize_release(
    release_input: Path,
    *,
    source_workbook: Path,
    target: Path,
    require_complete: bool,
) -> dict[str, Any]:
    release = reconcile_comparison_release(release_input)
    if require_complete:
        validate_complete_release(release)
    rendered = render_v19_workbook(source_workbook.read_text(encoding="utf-8-sig"), release)
    validate_comparison_workbook_text(rendered, release)
    atomic_write_text(target, rendered)
    return {"path": str(target), "sha256": sha256_file(target)}
```

Validation reparses every rendered table and compares every numeric/string cell with `ComparisonRelease`, including samples, aggregates, units, counts, activation labels, evidence hashes, and boundaries.

- [ ] **Step 8: Verify comparison and historical workbook paths**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_finalize_official_openvino_comparison_workbook.py scripts/testing/tests/test_finalize_official_openvino_workbook.py
```

Expected: PASS. Historical v1.8 fixture assertions remain on the historical finalizer; canonical WB-04 publication tests target v1.9 only after Task 9 publishes it.

- [ ] **Step 9: Commit the v1.9 renderer without publishing live data yet**

```powershell
git add scripts/testing/finalize_official_openvino_comparison_workbook.py scripts/testing/tests/test_finalize_official_openvino_comparison_workbook.py
git commit -m "feat(openvino): render WB-04 adaptive comparison"
```

---

### Task 9: Publish Markdown, DOCX, and every affected CSV register atomically

**Files:**
- Create: `scripts/testing/publish_official_openvino_comparison.py`
- Create: `scripts/testing/tests/test_publish_official_openvino_comparison.py`
- Modify: `scripts/testing/official_openvino/docx_audit.py`
- Modify: `scripts/testing/audit_official_openvino_docx.py`
- Modify: `scripts/testing/tests/test_official_openvino_docx.py`
- Update through publisher: `docs/testing/workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md`
- Update through publisher: `docs/testing/workbooks/generated/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx`
- Update through publisher: `docs/testing/OpenVINO-Codec-Traceability-Extension-v1.1.csv`
- Update through publisher: `docs/testing/Configuration-Register.csv`
- Update through publisher: `docs/testing/Test-Run-Register.csv`
- Update through publisher: `docs/testing/Performance-Measurement-Register.csv`
- Update through publisher: `docs/testing/Device-Verification-Register.csv`
- Update through publisher: `docs/testing/Quality-Evaluation-Register.csv`
- Update through publisher: `docs/testing/Failure-Register.csv`
- Update through publisher: `docs/testing/Evidence-Index.csv`
- Update through publisher: `docs/testing/Workbook-Completion-Register.csv`
- Update through publisher: `docs/testing/Workbook-Revision-Register.csv`
- Update last through publisher: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`

**Interfaces:**
- Consumes: validated release input and v1.9 Markdown from Tasks 7–8, current CSV files, existing workbook generator, and DOCX revision-history tooling.
- Produces: `register_rows_from_release(...)`, `rewrite_csv_slice_atomically(...)`, `publish_reconciled_checkpoint(...)`, a v1.9 DOCX audit profile, and authoritative `publication-state.json`.

- [ ] **Step 1: Write failing register mapping and idempotency tests**

```python
def test_accepted_runtime_writes_three_performance_rows_and_one_run_row(tmp_path: Path) -> None:
    bundle = build_publication_bundle(complete_runtime_release(), repo_root=fixture_repo(tmp_path))
    assert count_rows(bundle, "Performance-Measurement-Register.csv", key="OV-11@512") == 3
    assert count_rows(bundle, "Test-Run-Register.csv", key="OV-11@512") == 1


def test_complete_quality_writes_exactly_six_prompt_rows(tmp_path: Path) -> None:
    bundle = build_publication_bundle(complete_quality_release(), repo_root=fixture_repo(tmp_path))
    rows = rows_for(bundle, "Quality-Evaluation-Register.csv", "OV-11@512")
    assert [row["Prompt ID"] for row in rows] == ["P1", "P2", "P3", "P4", "P5", "P6"]


def test_terminal_checkpoint_does_not_create_performance_or_quality_rows(tmp_path: Path) -> None:
    bundle = build_publication_bundle(terminal_release(), repo_root=fixture_repo(tmp_path))
    assert rows_for(bundle, "Performance-Measurement-Register.csv", "OV-12@4096") == []
    assert rows_for(bundle, "Quality-Evaluation-Register.csv", "OV-12@4096") == []
    assert len(rows_for(bundle, "Failure-Register.csv", "OV-12@4096")) == 1


def test_checkpoint_publication_is_idempotent(tmp_path: Path) -> None:
    repo = fixture_repo(tmp_path)
    first = publish_reconciled_checkpoint(RELEASE_INPUT, repo_root=repo, require_complete=False)
    second = publish_reconciled_checkpoint(RELEASE_INPUT, repo_root=repo, require_complete=False)
    assert first["published_hashes"] == second["published_hashes"]
    assert all_register_keys_are_unique(repo)
```

- [ ] **Step 2: Write failing transactional and manifest-order tests**

```python
def test_failed_publication_leaves_previous_bundle_unchanged(tmp_path: Path) -> None:
    repo = fixture_repo(tmp_path)
    before = hash_publication_files(repo)
    with pytest.raises(ValueError, match="DOCX audit"):
        publish_reconciled_checkpoint(
            RELEASE_INPUT,
            repo_root=repo,
            require_complete=False,
            generate_docx=write_corrupt_docx,
        )
    assert hash_publication_files(repo) == before


def test_manifest_is_published_last_with_exact_hashes(tmp_path: Path) -> None:
    repo = fixture_repo(tmp_path)
    events: list[str] = []
    result = publish_reconciled_checkpoint(
        RELEASE_INPUT,
        repo_root=repo,
        require_complete=False,
        replace_observer=events.append,
    )
    assert events[-2:] == ["Controlled-Workbook-Manifest.csv", "publication-state.json"]
    assert manifest_hashes(repo) == result["published_hashes"]
```

- [ ] **Step 3: Run publisher tests and confirm the module is absent**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_publish_official_openvino_comparison.py
```

Expected: collection FAIL because `publish_official_openvino_comparison` does not exist.

- [ ] **Step 4: Implement deterministic register projections**

Use:

```python
REGISTER_PATHS = (
    "docs/testing/OpenVINO-Codec-Traceability-Extension-v1.1.csv",
    "docs/testing/Configuration-Register.csv",
    "docs/testing/Test-Run-Register.csv",
    "docs/testing/Performance-Measurement-Register.csv",
    "docs/testing/Device-Verification-Register.csv",
    "docs/testing/Quality-Evaluation-Register.csv",
    "docs/testing/Failure-Register.csv",
    "docs/testing/Evidence-Index.csv",
    "docs/testing/Workbook-Completion-Register.csv",
    "docs/testing/Workbook-Revision-Register.csv",
    "docs/testing/workbooks/Controlled-Workbook-Manifest.csv",
)


def register_rows_from_release(
    release: ComparisonRelease,
    *,
    evidence_commit: str,
    reconciliation_input_sha256: str,
) -> Mapping[str, Sequence[Mapping[str, str]]]:
```

Stable keys must include identity, context, sample/prompt where applicable, and evidence hash. Preserve unrelated rows and original header order. Accepted runtime creates three performance rows and one aggregate run row; performance rows include all timing/memory values and CPU/GPU mean/median/peak/count according to the actual register headers. Complete quality creates exactly six prompt rows. Runtime-only quality produces an explicit run/failure/evidence status but no numeric quality row. Terminal outcomes create run/failure/evidence rows only. Add five traceability identities once, one configuration row per declared identity/context, and a single WR-037 revision row superseding WR-036.

Construct the WR-037 row from this exact mapping, substituting only the validated reconciliation-input SHA-256 in code:

```python
revision_row = {
    "Record_ID": "WR-037",
    "Workbook_ID": "WB-04",
    "Version": "1.9",
    "Date": "2026-08-01",
    "Changed_By": "Student and Codex",
    "Change_Type": "Adaptive format comparison release",
    "Change_Summary": "Added guarded U4, U8, FP16, TBQ4 and TBQ3 comparison ladders with complete runtime, utilisation, quality and laptop-boundary evidence.",
    "Reason": "Provide a fair same-artifact TurboQuant comparison and establish the maximum safe format/context on the test laptop.",
    "Affected_Test_IDs": "OV-11; OV-12; OV-13; OV-TQ-21; OV-TQ-22; P1-P6",
    "Change_Reference": f"adaptive comparison reconciliation input SHA-256 {reconciliation_input_sha256}",
    "Status": "Current - pending PR",
    "Supersedes": "1.8",
}
```

- [ ] **Step 5: Implement byte-stable CSV replacement and a staged publication transaction**

```python
def rewrite_csv_slice_atomically(
    path: Path,
    *,
    owned_key: Callable[[Mapping[str, str]], str | None],
    replacement_rows: Sequence[Mapping[str, str]],
) -> bytes:
    header, existing = read_csv_strict(path)
    unrelated = [row for row in existing if owned_key(row) is None]
    validate_unique_owned_keys(replacement_rows, owned_key)
    return encode_csv(header, unrelated + sorted(replacement_rows, key=owned_key))


def publish_reconciled_checkpoint(
    release_input: Path,
    *,
    repo_root: Path,
    require_complete: bool,
    evidence_commit: str,
) -> dict[str, Any]:
    staged = build_publication_bundle(release_input, repo_root, require_complete, evidence_commit)
    validate_entire_staged_bundle(staged)
    replace_with_rollback(staged, manifest_last=True, publication_state_last=True)
    return validate_published_bundle(repo_root)
```

Build every new file in a staging directory outside the governed raw root. Back up all destination bytes before replacement and restore them if any replace fails. Publish the controlled manifest second-last and `publication-state.json` last. The publication state lists every released file and hash and is the only authoritative checkpoint marker.

- [ ] **Step 6: Add a comparison-specific DOCX audit profile without breaking v1.8**

In `docx_audit.py`, represent release rules as profiles:

```python
@dataclass(frozen=True)
class DocxAuditProfile:
    workbook_version: str
    revision_id: str
    revision_date: str
    expected_ids: frozenset[str]
    comparison_headings: tuple[str, ...]
    expected_matrix_sha256: str


COMPARISON_V19_PROFILE = DocxAuditProfile(
    workbook_version="1.9",
    revision_id="WR-037",
    revision_date="2026-08-01",
    expected_ids=frozenset({"OV-11", "OV-12", "OV-13", "OV-TQ-21", "OV-TQ-22"}),
    comparison_headings=COMPARISON_SECTION_TITLES,
    expected_matrix_sha256=load_frozen_comparison_matrix_sha256(),
)
```

`audit_official_openvino_docx.py --comparison-release` must select this profile and derive expected DOCX tables as finalized Markdown tables plus one revision-history table. Validate ZIP integrity, one revision-history table, seven headings, WR-037/version/date, matrix hash, successful-cell completeness, cache/deployment eligibility, compact terminal presentation, and no-winner rule.

- [ ] **Step 7: Generate the DOCX inside staging and validate it before publication**

Within `build_publication_bundle`, run:

```powershell
python scripts/testing/Generate-Controlled-Workbooks.py --workbook-id WB-04
python scripts/testing/Apply-Workbook-Revision-History.py --workbook-id WB-04
python scripts/testing/audit_official_openvino_docx.py --comparison-release --docx $stagedDocx --markdown $stagedMarkdown
```

If these scripts lack explicit output-path parameters, call their importable functions with staged paths rather than writing the live files and moving them. Do not relax the audit to accommodate generated output.

- [ ] **Step 8: Run focused publisher, DOCX, and workspace tests**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_publish_official_openvino_comparison.py scripts/testing/tests/test_official_openvino_docx.py scripts/testing/tests/test_finalize_official_openvino_comparison_workbook.py
python scripts/testing/Validate-Workbook-Revision-Control.py
powershell -File scripts/testing/Validate-Controlled-Testing-Workspace.ps1
```

Expected: PASS. No live canonical workbook/register is changed by fixture tests.

- [ ] **Step 9: Commit the publisher and DOCX profile**

```powershell
git add scripts/testing/publish_official_openvino_comparison.py scripts/testing/official_openvino/docx_audit.py scripts/testing/audit_official_openvino_docx.py scripts/testing/tests/test_publish_official_openvino_comparison.py scripts/testing/tests/test_official_openvino_docx.py
git commit -m "feat(openvino): publish WB-04 comparison checkpoints"
```

---

### Task 10: Freeze real inputs and prove the campaign is launch-ready without inference

**Files:**
- Create from validated evidence: `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/artifacts/artifact-inventory.json`
- Create from validated evidence: `experiments/manifests/official-openvino/adaptive-format-comparison-matrix-v1.json`
- Create from validated evidence: `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/specs/spec-index.json`
- Create: `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/preflight/preflight-report.json`
- Modify: `.gitignore` only if current evidence policy already tracks the corresponding immutable receipt class; do not blanket-unignore raw model binaries, caches, logs, or arbitrary attempt directories.

**Interfaces:**
- Consumes: all implementation from Tasks 0–9 plus the existing U4/U8 manifests, build provenance, build tree, Python environment, OpenVINO libraries, and sampler.
- Produces: exact frozen matrix/spec hashes and a zero-launch preflight receipt used by the first formal run.

- [ ] **Step 1: Establish paths and verify every required local dependency**

Run from the worktree:

```powershell
$wt = (Get-Location).Path
$campaign = Join-Path $wt 'experiments\raw-results\openvino-turboquant\2026-08-01\adaptive-format-comparison'
$u4Manifest = Join-Path $wt 'experiments\raw-results\openvino-turboquant\2026-07-30\artifacts\granite-4.1-3b-u4-ea0231a\artifact-manifest.json'
$u8Manifest = Join-Path $wt 'experiments\raw-results\openvino-turboquant\2026-07-30\artifacts\granite-4.1-3b-u8-f858c9e\artifact-manifest.json'
$buildProvenance = Join-Path $wt 'experiments\raw-results\openvino-turboquant\2026-07-30\provenance\build-00edae3b-attempt-001\build-provenance.json'
$buildRoot = 'C:\ov-wb04\2026-07-30\build-genai-tq-00edae3b'
$python = Join-Path $wt '.venv-official-openvino-turboquant-py313\Scripts\python.exe'
$sitePackages = Join-Path $wt '.venv-official-openvino-turboquant-py313\Lib\site-packages'
$openvinoLibraries = Join-Path $sitePackages 'openvino\libs'
$sampler = Join-Path $wt 'scripts\testing\collect_openvino_runtime_utilization.ps1'
$required = @($u4Manifest, $u8Manifest, $buildProvenance, $buildRoot, $python, $sitePackages, $openvinoLibraries, $sampler)
$missing = @($required | Where-Object { -not (Test-Path -LiteralPath $_) })
if ($missing.Count -ne 0) { throw "Missing required paths: $($missing -join ', ')" }
```

Expected: no exception. Do not substitute another build, venv, or artifact silently.

- [ ] **Step 2: Run all automated tests before creating campaign evidence**

Run:

```powershell
& $python -m pytest -q scripts/testing/tests
& $python -m pytest -q
```

Expected: both PASS with zero failures. If either fails, stop before artifact preparation; no formal evidence should share a commit with a failing harness.

- [ ] **Step 3: Build the real artifact inventory under the guard**

Run:

```powershell
& $python scripts/testing/prepare_official_openvino_adaptive_artifacts.py `
  --u4-manifest $u4Manifest `
  --u8-manifest $u8Manifest `
  --output-root (Join-Path $campaign 'artifacts') `
  --launch-reserve-mib 4096 `
  --emergency-floor-mib 2048
if ($LASTEXITCODE -ne 0) { throw 'Adaptive artifact inventory failed' }
```

Expected: validated U4/U8 bindings and either a validated FP16 binding or a canonical FP16 `artifact-preparation-terminal` receipt. A terminal receipt is acceptable; an uncategorized error, survivor, hash mismatch, or partially written artifact is not.

- [ ] **Step 4: Materialize and validate the immutable comparison matrix**

Run:

```powershell
$inventory = Join-Path $campaign 'artifacts\artifact-inventory.json'
$matrix = Join-Path $wt 'experiments\manifests\official-openvino\adaptive-format-comparison-matrix-v1.json'
& $python scripts/testing/build_official_openvino_adaptive_matrix.py `
  --artifact-inventory $inventory `
  --historical-matrix (Join-Path $wt 'experiments\manifests\official-openvino\retest-matrix.json') `
  --output $matrix
if ($LASTEXITCODE -ne 0) { throw 'Comparison matrix construction failed' }
& $python -m pytest -q scripts/testing/tests/test_official_openvino_adaptive_matrix.py
```

Expected: PASS and exactly five ordered identities with the three U8 rows bound to one identical manifest hash.

- [ ] **Step 5: Generate and audit every declared workload spec**

Run:

```powershell
$specRoot = Join-Path $campaign 'specs'
& $python scripts/testing/generate_official_openvino_adaptive_specs.py `
  --matrix $matrix `
  --build-root $buildRoot `
  --artifact-inventory $inventory `
  --cache-root (Join-Path $campaign 'cache') `
  --output-root $specRoot
if ($LASTEXITCODE -ne 0) { throw 'Adaptive spec generation failed' }
& $python -c "import json,pathlib; p=json.loads(pathlib.Path(r'$specRoot\spec-index.json').read_text(encoding='utf-8')); assert p['runtime_spec_count'] in (20,25); assert p['runtime_spec_count'] + 5*p['artifact_preparation_terminal_count'] == 25; assert all(s['max_new_tokens']==4 and s['actual_input_tokens']==s['context_tokens'] for s in p['runtime_specs'])"
```

Expected: 25 runtime specs when FP16 exists; otherwise 20 runtime specs plus one candidate-wide FP16 preparation terminal representing its five pruned runtime steps. Every runtime spec requests exactly four generated tokens and exact declared input tokens.

- [ ] **Step 6: Run controller preflight with zero launches**

Run:

```powershell
$boundaryIndex = Join-Path $campaign 'preflight\reference-boundary-index.json'
& $python scripts/testing/build_official_openvino_boundary_index.py `
  --matrix $matrix `
  --spec-root $specRoot `
  --historical-root (Join-Path $wt 'experiments\raw-results\openvino-turboquant\2026-07-30') `
  --output $boundaryIndex
if ($LASTEXITCODE -ne 0) { throw 'Historical boundary-index validation failed' }
$runtimeRoot = Join-Path $campaign 'runtime'
& $python scripts/testing/run_official_openvino_adaptive_comparison.py `
  --matrix $matrix `
  --spec-root $specRoot `
  --campaign-root $campaign `
  --build-root $buildRoot `
  --build-provenance $buildProvenance `
  --python-executable $python `
  --python-site-packages $sitePackages `
  --openvino-libraries $openvinoLibraries `
  --sampler-script $sampler `
  --reference-boundary-index $boundaryIndex `
  --max-context 512 `
  --preflight-only
if ($LASTEXITCODE -ne 0) { throw 'Adaptive campaign preflight failed' }
```

Expected: the boundary index records only exact reusable failures (or a valid zero-entry result); the preflight report names `OV-11@512` as the first eligible runtime step, shows no owned survivors, verifies all hashes, and records available RAM. It creates no `attempt-*` directory and launches no OpenVINO worker.

- [ ] **Step 7: Validate and commit only the frozen, reviewable control receipts**

Run:

```powershell
git diff --check
git status --short
git add experiments/manifests/official-openvino/adaptive-format-comparison-matrix-v1.json
git add experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/artifacts/artifact-inventory.json
git add experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/preflight/preflight-report.json
git add experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/preflight/reference-boundary-index.json
git add experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/specs/spec-index.json
git commit -m "test(openvino): freeze adaptive comparison inputs"
```

If evidence policy ignores one of these canonical receipts, use `git check-ignore -v PATH` and add only that exact receipt with `git add -f`; never add model binaries, cache content, bulk logs, or unrelated pre-existing evidence.

---

### Task 11: Execute the adaptive ladder safely and publish every checkpoint

**Files:**
- Append immutable evidence below: `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/`
- Update after each step through Task 9's publisher: WB-04 Markdown/DOCX and affected CSV registers.
- Update after each context checkpoint: `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/adaptive-campaign-state.json`

**Interfaces:**
- Consumes: frozen inputs from Task 10 and all controller/quality/publication code.
- Produces: a closed contiguous ladder for every candidate, three accepted samples and complete telemetry for each runtime pass, isolated quality captures for every runtime pass, compact terminal evidence, and authoritative checkpoint hashes.

- [ ] **Step 1: Record a fresh RAM/ownership preflight immediately before the first launch**

Run:

```powershell
& $python -c "import json,psutil; v=psutil.virtual_memory(); print(json.dumps({'available_mib':v.available/1024**2,'required_launch_mib':4096},sort_keys=True)); raise SystemExit(0 if v.available >= 4096*1024**2 else 2)"
if ($LASTEXITCODE -ne 0) { throw 'Less than 4096 MiB available; do not launch or terminate unrelated applications' }
& $python scripts/testing/run_official_openvino_adaptive_comparison.py `
  --matrix $matrix --spec-root $specRoot --campaign-root $campaign `
  --build-root $buildRoot --build-provenance $buildProvenance `
  --python-executable $python --python-site-packages $sitePackages `
  --openvino-libraries $openvinoLibraries --sampler-script $sampler `
  --reference-boundary-index $boundaryIndex `
  --max-context 512 --resume --publish-checkpoints --preflight-only
```

Expected: at least 4,096 MiB available, zero owned survivors, and a valid next step. If reserve is unavailable, stop; do not close or kill applications on the user's behalf.

- [ ] **Step 2: Run the complete context-512 breadth level**

Run the same controller without `--preflight-only`:

```powershell
& $python scripts/testing/run_official_openvino_adaptive_comparison.py `
  --matrix $matrix --spec-root $specRoot --campaign-root $campaign `
  --build-root $buildRoot --build-provenance $buildProvenance `
  --python-executable $python --python-site-packages $sitePackages `
  --openvino-libraries $openvinoLibraries --sampler-script $sampler `
  --reference-boundary-index $boundaryIndex `
  --max-context 512 --resume --publish-checkpoints
if ($LASTEXITCODE -ne 0) { throw 'Controller failed outside a governed terminal classification' }
```

Expected: eligible rows run in order `OV-11`, `OV-TQ-22`, `OV-TQ-21`, `OV-12`, `OV-13`. Each runtime pass has pilot, warmup, samples 1–3, then six fresh quality prompt workers. After each row, `publication-state.json` changes and the workbook/register checkpoint exactly matches the validated state. A governed terminal stops only that candidate.

- [ ] **Step 3: Audit the context-512 checkpoint before promotion**

Run:

```powershell
& $python scripts/testing/build_official_openvino_comparison_release_evidence.py `
  --matrix $matrix `
  --campaign-state (Join-Path $campaign 'adaptive-campaign-state.json') `
  --output (Join-Path $campaign 'reconciliation-input.json')
& $python scripts/testing/finalize_official_openvino_comparison_workbook.py `
  --release-input (Join-Path $campaign 'reconciliation-input.json') `
  --check-only
& $python scripts/testing/audit_official_openvino_docx.py --comparison-release
if ($LASTEXITCODE -ne 0) { throw 'Context-512 checkpoint audit failed' }
```

Expected: no blank successful cells; three run values plus means/medians; peak RAM, KV MiB, TTFT, CPU and GPU mean/median/peak/count; activation/tokens/hashes; quality scores only where exactly six prompts are adjudicated; compact terminal entries otherwise.

- [ ] **Step 4: Commit the sealed context-512 checkpoint**

```powershell
git diff --check
$publicationFiles = @(
  'docs/testing/workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md',
  'docs/testing/workbooks/generated/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx',
  'docs/testing/OpenVINO-Codec-Traceability-Extension-v1.1.csv',
  'docs/testing/Configuration-Register.csv',
  'docs/testing/Test-Run-Register.csv',
  'docs/testing/Performance-Measurement-Register.csv',
  'docs/testing/Device-Verification-Register.csv',
  'docs/testing/Quality-Evaluation-Register.csv',
  'docs/testing/Failure-Register.csv',
  'docs/testing/Evidence-Index.csv',
  'docs/testing/Workbook-Completion-Register.csv',
  'docs/testing/Workbook-Revision-Register.csv',
  'docs/testing/workbooks/Controlled-Workbook-Manifest.csv'
)
git add -- $publicationFiles
git add -f experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/adaptive-campaign-state.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/reconciliation-input.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/publication-state.json
git commit -m "test(openvino): record adaptive comparison at context 512"
```

Add only canonical state/index/release receipts and repository-policy-approved compact evidence. Do not accidentally stage caches, raw model files, unrelated retry directories, or pre-existing untracked evidence.

- [ ] **Step 5: Promote one context at a time in the fixed breadth-first order**

Run this loop; each invocation internally rechecks 4,096 MiB before every role/prompt and publishes after every completed/terminal row:

```powershell
foreach ($context in 1024, 2048, 4096, 8192) {
  & $python scripts/testing/run_official_openvino_adaptive_comparison.py `
    --matrix $matrix --spec-root $specRoot --campaign-root $campaign `
    --build-root $buildRoot --build-provenance $buildProvenance `
    --python-executable $python --python-site-packages $sitePackages `
    --openvino-libraries $openvinoLibraries --sampler-script $sampler `
    --reference-boundary-index $boundaryIndex `
    --max-context $context --resume --publish-checkpoints
  if ($LASTEXITCODE -ne 0) { throw "Ungoverned controller failure at context $context" }
  & $python scripts/testing/build_official_openvino_comparison_release_evidence.py `
    --matrix $matrix `
    --campaign-state (Join-Path $campaign 'adaptive-campaign-state.json') `
    --output (Join-Path $campaign 'reconciliation-input.json')
  & $python scripts/testing/finalize_official_openvino_comparison_workbook.py `
    --release-input (Join-Path $campaign 'reconciliation-input.json') `
    --check-only
  & $python scripts/testing/audit_official_openvino_docx.py --comparison-release
  if ($LASTEXITCODE -ne 0) { throw "Checkpoint validation failed at context $context" }
  git diff --check
  git add -- $publicationFiles
  git add -f experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/adaptive-campaign-state.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/reconciliation-input.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/publication-state.json
  git diff --cached --quiet
  if ($LASTEXITCODE -ne 0) {
    git commit -m "test(openvino): advance adaptive comparison through context $context"
  }
}
```

Expected: stopped candidates are skipped at higher contexts; viable candidates advance without gaps. If all candidates are already terminal, later invocations are idempotent and create no commit. Never use a prior boundary unless the controller validates exact artifact/model/device/route/context/floor/failure equivalence.

- [ ] **Step 6: Recover quality-blocked rows only under the explicit resume contract**

For each state entry whose runtime is `passed` and quality is `quality-blocked`, run:

```powershell
& $python scripts/testing/run_official_openvino_adaptive_quality.py `
  --campaign-state (Join-Path $campaign 'adaptive-campaign-state.json') `
  --resume-quality-blocked
```

The controller exposes a complete hash-bound `quality_recovery` argument object in every eligible step, and the quality CLI consumes it without path discovery or manually typed substitutions. Resume starts at the first absent/failed prompt, preserves earlier prompt evidence, and rechecks RAM/cleanup before every worker. One clean resumed failure becomes a precise quality boundary; do not loop indefinitely.

- [ ] **Step 7: Prove every candidate ladder is closed**

Run:

```powershell
& $python scripts/testing/build_official_openvino_comparison_release_evidence.py `
  --matrix $matrix `
  --campaign-state (Join-Path $campaign 'adaptive-campaign-state.json') `
  --output (Join-Path $campaign 'reconciliation-input.json') `
  --quality-capture-index (Join-Path $campaign 'quality\quality-capture-index.json') `
  --require-ladder-closed
if ($LASTEXITCODE -ne 0) { throw 'At least one candidate ladder is not closed' }
```

Expected: each candidate has a contiguous pass chain and either an 8192 result or first confirmed boundary; FP16 may instead have an explicit artifact-preparation terminal. Every runtime pass has a complete six-prompt capture awaiting blind adjudication or a precise governed quality boundary.

---

### Task 12: Score all complete quality campaigns and seal WB-04 v1.9/WR-037

**Files:**
- Create from evidence: `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/blind-scoring-input.json`
- Create from independent reviews: `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/judge-a-scores.json`
- Create from independent reviews: `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/judge-b-scores.json`
- Create from independent reviews: `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/pairwise-reviews.json`
- Create when gates require it: `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/manual-adjudications.json`
- Create: `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/adjudication.json`
- Finalize: WB-04 Markdown, DOCX, all comparison registers, release evidence, and publication state.

**Interfaces:**
- Consumes: closed runtime/quality-capture state from Task 11 and the adjudicator/publisher.
- Produces: harsh unbiased numeric quality for every complete P1–P6 campaign, final comparison/boundary findings, synchronized v1.9/WR-037 artifacts, and a zero-failure verification record.

- [ ] **Step 1: Build one public blind bundle and private map from explicit complete captures**

Run:

```powershell
$qualityRoot = Join-Path $campaign 'quality'
& $python scripts/testing/adjudicate_official_openvino_adaptive_quality.py build-bundle `
  --capture-index (Join-Path $qualityRoot 'quality-capture-index.json') `
  --prompt-set experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json `
  --rubric experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json `
  --public-output (Join-Path $qualityRoot 'blind-scoring-input.json') `
  --private-map-output (Join-Path $qualityRoot 'private-blind-map.json')
if ($LASTEXITCODE -ne 0) { throw 'Blind quality bundle construction failed' }
```

Expected: exactly six responses per complete campaign and no test ID, format, precision, device, artifact, model, build, or codec label in the public JSON.

- [ ] **Step 2: Obtain two independent harsh blind score sheets**

Dispatch two fresh reviewers separately. Give each only `blind-scoring-input.json`, the rubric, and prompt controls; do not give either reviewer the private map, workbook, runtime results, or the other review. Require each to score every P1–P6 response on the five weighted dimensions, cite the exact response evidence for deductions, apply deterministic caps, and write a canonical score sheet with a self-hash. Reject and redo any score sheet that mentions a test ID, precision, format, TurboQuant, or the competing review.

Expected: `judge-a-scores.json` and `judge-b-scores.json` cover the identical blind label/prompt set and contain no missing cells.

- [ ] **Step 3: Review every shared-context pair in both presentation orders**

Generate the pair list from the reconciler: cache pairs only among `OV-12`/`OV-TQ-21`/`OV-TQ-22` sharing a context and artifact hash; STANDARD deployment pairs only among `OV-11`/`OV-12`/`OV-13` sharing a context. Reviewer A receives AB order and Reviewer B receives BA order. Store both outcomes, reasons, and hashes in `pairwise-reviews.json` without unblinding either reviewer.

Expected: every eligible pair has exactly `AB` and `BA`; no nonshared context or unequal artifact enters the fair cache comparison.

- [ ] **Step 4: Resolve only mandated disagreements while still blind**

Run the adjudicator in validation mode. For every critical-gate event, per-prompt judge difference greater than 1.0, or AB/BA ranking reversal it reports, the primary adjudicator rereads the response, prompt controls, rubric, both score rationales, and both order reviews without the private map. Write one canonical manual record per flagged item with the selected score/outcome, exact rubric basis, evidence hashes, and self-hash. If there are no flags, write a canonical empty record `{ "schema": "official-openvino-manual-adjudications-v1", "records": [] }` and its self-hash wrapper.

- [ ] **Step 5: Validate, unblind once, and produce final numeric scores**

Run:

```powershell
& $python scripts/testing/adjudicate_official_openvino_adaptive_quality.py adjudicate `
  --scoring-input (Join-Path $qualityRoot 'blind-scoring-input.json') `
  --judge-score-sheet (Join-Path $qualityRoot 'judge-a-scores.json') `
  --judge-score-sheet (Join-Path $qualityRoot 'judge-b-scores.json') `
  --pairwise-reviews (Join-Path $qualityRoot 'pairwise-reviews.json') `
  --manual-adjudications (Join-Path $qualityRoot 'manual-adjudications.json') `
  --private-map (Join-Path $qualityRoot 'private-blind-map.json') `
  --prompt-set experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json `
  --rubric experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json `
  --output (Join-Path $qualityRoot 'adjudication.json')
if ($LASTEXITCODE -ne 0) { throw 'Quality adjudication failed' }
```

Expected: each complete campaign has P1–P6 plus mean, median, minimum, and maximum. Incomplete/blocked campaigns have no numeric score and cannot enter rankings.

- [ ] **Step 6: Rebuild release evidence and publish the final bundle**

Run:

```powershell
& $python scripts/testing/build_official_openvino_comparison_release_evidence.py `
  --matrix $matrix `
  --campaign-state (Join-Path $campaign 'adaptive-campaign-state.json') `
  --output (Join-Path $campaign 'reconciliation-input.json') `
  --quality-capture-index (Join-Path $qualityRoot 'quality-capture-index.json') `
  --quality-adjudication (Join-Path $qualityRoot 'adjudication.json') `
  --require-complete
& $python scripts/testing/publish_official_openvino_comparison.py `
  --release-input (Join-Path $campaign 'reconciliation-input.json') `
  --repo-root $wt `
  --evidence-commit (git rev-parse HEAD) `
  --require-complete
if ($LASTEXITCODE -ne 0) { throw 'Final comparison publication failed' }
```

Expected: Markdown and DOCX identify WB-04 v1.9/WR-037; every successful row is complete; all terminal rows are compact; registers are direct repository CSV text and synchronized; manifest hashes match generated files.

- [ ] **Step 7: Perform independent structural and rendered-page QA**

Run:

```powershell
& $python scripts/testing/audit_official_openvino_docx.py --comparison-release
& $python scripts/testing/Validate-Workbook-Revision-Control.py
powershell -File scripts/testing/Validate-Controlled-Testing-Workspace.ps1
& $python 'C:\Users\Student\.codex\plugins\cache\openai-primary-runtime\documents\26.709.11516\skills\documents\render_docx.py' `
  (Join-Path $wt 'docs\testing\workbooks\generated\04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx') `
  --output_dir (Join-Path $wt '.tmp\wb04-v19-render')
```

Inspect every rendered page for clipped tables, unreadable wrapping, duplicate headings, orphan rows, and inconsistent revision/footer text. If the renderer command is unavailable, add a release limitation stating exactly that rendered-page QA was not performed; still require the structural DOCX audit to pass.

- [ ] **Step 8: Run the final no-failure verification suite**

Run:

```powershell
& $python -m pytest -q scripts/testing/tests/test_official_openvino_adaptive_matrix.py scripts/testing/tests/test_official_openvino_adaptive_campaign_spec.py scripts/testing/tests/test_official_openvino_adaptive_metrics.py scripts/testing/tests/test_official_openvino_adaptive_campaign.py scripts/testing/tests/test_official_openvino_adaptive_quality.py scripts/testing/tests/test_adjudicate_official_openvino_adaptive_quality.py scripts/testing/tests/test_official_openvino_comparison_reconcile.py scripts/testing/tests/test_finalize_official_openvino_comparison_workbook.py scripts/testing/tests/test_publish_official_openvino_comparison.py scripts/testing/tests/test_official_openvino_docx.py
& $python -m pytest -q scripts/testing/tests
& $python -m pytest -q
dotnet test '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' --configuration Release
git diff --check
```

Expected: every applicable command PASS with zero failed tests. If the .NET project is absent, record the exact missing path as not applicable; do not call it passed.

- [ ] **Step 9: Run the final workbook completeness assertions directly**

Run:

```powershell
& $python scripts/testing/finalize_official_openvino_comparison_workbook.py `
  --release-input (Join-Path $campaign 'reconciliation-input.json') `
  --check-only `
  --require-complete
& $python scripts/testing/publish_official_openvino_comparison.py `
  --release-input (Join-Path $campaign 'reconciliation-input.json') `
  --repo-root $wt `
  --evidence-commit (git rev-parse HEAD) `
  --check-only `
  --require-complete
```

Expected: zero missing candidates, zero unexplained steps, zero blank/placeholder successful cells, zero duplicate register keys, exact Markdown/DOCX/register hashes, and separately stated runtime-capable and fully-comparable boundaries.

- [ ] **Step 10: Commit the sealed final comparison release**

```powershell
git status --short
git add -- $publicationFiles
git add -f experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/adaptive-campaign-state.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/reconciliation-input.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/publication-state.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/quality-capture-index.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/blind-scoring-input.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/private-blind-map.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/judge-a-scores.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/judge-b-scores.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/pairwise-reviews.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/manual-adjudications.json experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/quality/adjudication.json
git diff --cached --check
git commit -m "test(openvino): complete WB-04 adaptive format comparison"
```

Before committing, inspect `git diff --cached --name-status` and unstage any cache, model binary, unrelated retry evidence, or pre-existing user file. The commit is complete only when the final check-only commands still pass against the committed hashes.

---

## Completion Evidence Required in the Handoff

- Exact branch and final commit.
- Exact counts of runtime passes, runtime/safety/artifact boundaries, quality-complete rows, and quality-blocked rows.
- Highest runtime-capable and fully-comparable context for each of the five identities.
- Highest working STANDARD weight precision at every genuinely shared context, without converting an FP16 preparation terminal into an inference claim.
- Fair U8 STANDARD/TBQ4/TBQ3 comparison only where artifact hash and context match.
- Paths and hashes for matrix, artifact inventory, campaign state, reconciliation input/report, adjudication, Markdown workbook, DOCX, controlled manifest, and publication state.
- Focused/full test counts, .NET result or exact not-applicable reason, DOCX structural result, page-render result or exact renderer limitation, and `git diff --check` result.
- A clear statement that terminal rows have no invented metrics and successful tables have no blank or placeholder cells.
