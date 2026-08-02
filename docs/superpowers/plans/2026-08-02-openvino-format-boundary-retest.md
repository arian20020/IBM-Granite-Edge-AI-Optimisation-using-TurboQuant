# OpenVINO Format-Boundary Retest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and execute a small, serial OpenVINO format ladder that stops at this laptop's first safe CPU boundary and publishes a new WB-07 with complete performance, utilisation, memory, and quality evidence.

**Architecture:** Add a focused boundary manifest and controller around the existing crash-safe OpenVINO measurement and quality primitives. The controller owns only ordering, retry/stop decisions, resumable state, and human labels; existing workers continue to own subprocess isolation, RAM termination, telemetry, metric extraction, activation proof, and quality gates. A separate publisher validates accepted receipts, renders Markdown, generates DOCX through the existing converter, and rejects incomplete or misleading output.

**Tech Stack:** Python 3.13, pytest, OpenVINO 2026.2.1, OpenVINO GenAI 2026.2.1.0 plus project TurboQuant patch `00edae3b`, Windows Job Objects, PowerShell utilisation sampling, JSON evidence, Markdown, python-docx.

## Global Constraints

- CPU order is exactly U4+TBQ3, U4+TBQ4, U4+STANDARD-F16, U8+TBQ3, U8+TBQ4, U8+STANDARD-F16, F16+TBQ3, F16+TBQ4, F16+STANDARD-F16.
- All comparison rows use Granite 4.1 3B, deterministic generation, input context 512, CPU, one stream, and one inference thread.
- The U4+STANDARD-F16 GPU control is a separate lane; TBQ3/TBQ4 must never be requested on GPU.
- Start only with at least 4096 MiB available RAM; terminate a worker below 3072 MiB available RAM.
- Runtime role timeout is 180 seconds; each isolated quality prompt timeout is 90 seconds; whole-row elapsed time must not exceed 720 seconds.
- Allow one clean retry at most. Two matching failures close that lane. A hard RAM-floor breach may close it without retry.
- Run one model worker at a time and stop only campaign-owned processes.
- Every accepted row requires three measured samples plus complete quality, CPU/GPU, RAM, K/V, device, fallback, and cleanup evidence.
- Every accepted row records CPU utilisation mean, median, peak, and count plus GPU utilisation mean, median, peak, and count; measured zero is numeric `0`.
- Use the existing `quality-rubric-v1.json` 0-10 anchors; blind labels during adjudication and apply identical prompts and deductions.
- WB-04 and its evidence remain untouched. WB-07 uses human format labels in visible tables; internal IDs are evidence plumbing only.
- WB-07 contains no blank data cells, no `N/A` placeholders, and no GPU TurboQuant claim.
- Never synthesize a missing metric, quality score, device result, or failure reason.

---

## File Structure

- `experiments/manifests/official-openvino/format-boundary-matrix-v1.json`: ordered cases, human labels, model artifacts, backend lane, and runtime properties.
- `experiments/granite_turboquant_intel/prompts/compact-feasibility-prompt-set-v2.json`: bounded P1-P6 comparison prompt contract.
- `experiments/granite_turboquant_intel/prompts/fixtures/P5-compact-context-v2.txt`: tokenizer-verified sub-512-token retrieval fixture.
- `experiments/granite_turboquant_intel/prompts/rendered-v2/`: immutable rendered v2 prompt assets.
- `scripts/testing/official_openvino/quality_contracts.py`: allow-listed v1/v2 prompt-contract registry without changing historical hashes.
- `scripts/testing/official_openvino/format_boundary.py`: manifest validation, worker-spec creation, serial state machine, retry/stop policy, and immutable receipts.
- `scripts/testing/run_official_openvino_format_boundary.py`: thin CLI and dependency-path defaults.
- `scripts/testing/official_openvino/format_boundary_workbook.py`: evidence reconciliation, quality projection, Markdown rendering, workbook validation, and release manifest.
- `scripts/testing/publish_openvino_format_boundary_workbook.py`: thin publisher CLI and DOCX generation.
- `scripts/testing/tests/test_official_openvino_format_boundary.py`: manifest/spec/controller unit tests.
- `scripts/testing/tests/test_official_openvino_format_boundary_workbook.py`: publisher and workbook validation tests.
- `docs/testing/workbooks/text-templates/07_OpenVINO_Format_Boundary_Workbook.md`: generated canonical workbook source.
- `docs/testing/workbooks/generated/07_OpenVINO_Format_Boundary_Workbook.docx`: generated reviewable workbook.
- `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`: WB-07 hash and generation command.
- `docs/testing/Workbook-Revision-Register.csv`: one current WB-07 revision.
- `docs/testing/Workbook-Completion-Register.csv`: seven completed WB-07 sections.
- `scripts/testing/Generate-Controlled-Workbooks.py`: register the seventh Markdown template.
- `scripts/testing/Apply-Workbook-Revision-History.py`: register WB-07 for revision rendering.

### Task 1: Boundary manifest, compact prompts, and validated worker specs

**Files:**
- Create: `experiments/manifests/official-openvino/format-boundary-matrix-v1.json`
- Create: `experiments/granite_turboquant_intel/prompts/compact-feasibility-prompt-set-v2.json`
- Create: `experiments/granite_turboquant_intel/prompts/fixtures/P5-compact-context-v2.txt`
- Create: `experiments/granite_turboquant_intel/prompts/rendered-v2/P1.txt`
- Create: `experiments/granite_turboquant_intel/prompts/rendered-v2/P2.txt`
- Create: `experiments/granite_turboquant_intel/prompts/rendered-v2/P3.txt`
- Create: `experiments/granite_turboquant_intel/prompts/rendered-v2/P4.txt`
- Create: `experiments/granite_turboquant_intel/prompts/rendered-v2/P5-instruction.txt`
- Create: `experiments/granite_turboquant_intel/prompts/rendered-v2/P6-turn1.txt`
- Create: `experiments/granite_turboquant_intel/prompts/rendered-v2/P6-turn2-with-history.txt`
- Create: `scripts/testing/official_openvino/quality_contracts.py`
- Modify: `scripts/testing/run_official_openvino_quality.py`
- Modify: `scripts/testing/adjudicate_official_openvino_quality.py`
- Modify: `scripts/testing/adjudicate_official_openvino_adaptive_quality.py`
- Modify: `scripts/testing/official_openvino/quality.py`
- Create: `scripts/testing/official_openvino/format_boundary.py`
- Create: `scripts/testing/tests/test_official_openvino_format_boundary.py`

**Interfaces:**
- Consumes: `scripts.testing.official_openvino.matrix.load_matrix`, `scripts.testing.measure_official_openvino.build_runtime_property_spec`, the v1 prompt contract, Granite tokenizer, and existing U4/U8 artifact manifests.
- Produces: `BoundaryCase`, `BoundaryManifest`, `load_boundary_manifest(path: Path) -> BoundaryManifest`, `build_boundary_worker_spec(case: BoundaryCase, *, role: str, cache_dir: Path) -> dict[str, Any]`, and `load_quality_contract(prompt_set_path: Path) -> QualityContract`.

- [ ] **Step 1: Write failing manifest and spec tests**

```python
def test_manifest_has_exact_low_to_high_cpu_order_and_separate_gpu_lane(tmp_path):
    manifest = load_boundary_manifest(FIXTURE_MATRIX)
    assert [case.label for case in manifest.cpu_cases] == [
        "U4 weights + TBQ3 cache", "U4 weights + TBQ4 cache",
        "U4 weights + standard F16 cache", "U8 weights + TBQ3 cache",
        "U8 weights + TBQ4 cache", "U8 weights + standard F16 cache",
        "F16 weights + TBQ3 cache", "F16 weights + TBQ4 cache",
        "F16 weights + standard F16 cache",
    ]
    assert [case.label for case in manifest.gpu_cases] == [
        "U4 weights + standard F16 cache (GPU control)"
    ]

def test_worker_spec_activates_tbq3_without_visible_test_codes(tmp_path):
    case = load_boundary_manifest(FIXTURE_MATRIX).cpu_cases[0]
    spec = build_boundary_worker_spec(case, role="pilot", cache_dir=tmp_path)
    assert spec["device"] == "CPU"
    assert spec["context"] == 512
    assert spec["properties"]["TURBOQUANT_KEY_ALGORITHM"] == "TBQ3"
    assert spec["properties"]["TURBOQUANT_VALUE_ALGORITHM"] == "TBQ3"
    assert spec["properties"]["TURBOQUANT_NORM_CORRECTION"] is True
    assert spec["properties"]["INFERENCE_NUM_THREADS"] == 1
    assert spec["properties"]["NUM_STREAMS"] == 1
```

- [ ] **Step 2: Run the focused tests and confirm the import failure**

Run: `python -m pytest -q scripts/testing/tests/test_official_openvino_format_boundary.py`

Expected: FAIL because `scripts.testing.official_openvino.format_boundary` and the manifest do not exist.

- [ ] **Step 3: Add the exact manifest contract and dataclasses**

```python
@dataclass(frozen=True)
class BoundaryCase:
    internal_id: str
    label: str
    lane: Literal["cpu", "gpu-control"]
    order: int
    weight_precision: Literal["u4", "u8", "f16"]
    key_algorithm: Literal["STANDARD", "TBQ3", "TBQ4"]
    value_algorithm: Literal["STANDARD", "TBQ3", "TBQ4"]
    key_precision: Literal["f16", "u4", "u3"]
    value_precision: Literal["f16", "u4", "u3"]
    device: Literal["CPU", "GPU"]
    context: int
    artifact_manifest_path: Path | None

@dataclass(frozen=True)
class BoundaryManifest:
    cpu_cases: tuple[BoundaryCase, ...]
    gpu_cases: tuple[BoundaryCase, ...]
    source_path: Path
    sha256: str
```

Reject duplicate orders, any non-512 context, GPU TBQ, a CPU order different from the global constraint, or a non-null artifact path that does not remain under the repository root.

- [ ] **Step 4: Add the exact compact quality contract without changing v1 semantics**

Copy P1-P4 and P6 content/checks byte-for-byte after UTF-8 decoding from v1. Change only P5's fixture binding to `fixtures/P5-compact-context-v2.txt`. Keep its final marker `IXN-TQ-7319` and exact output `MARKER:IXN-TQ-7319`. Make the compact fixture roughly 250-350 Granite input tokens with the marker in its final paragraph. Add `maximum_input_tokens: 512` to the v2 contract and store the actual tokenizer count in its rendered-asset manifest.

- [ ] **Step 5: Add an allow-listed v1/v2 prompt-contract registry**

```python
@dataclass(frozen=True)
class QualityContract:
    prompt_set_id: str
    prompt_set_sha256: str
    rendered_root: Path
    maximum_input_tokens: int | None

QUALITY_CONTRACTS: Mapping[str, QualityContract]

def load_quality_contract(prompt_set_path: Path) -> QualityContract:
    raw = Path(prompt_set_path).read_bytes()
    document = json.loads(raw)
    if not isinstance(document, dict):
        raise ValueError("prompt contract must be an object")
    contract = QUALITY_CONTRACTS.get(document.get("prompt_set_id"))
    if contract is None:
        raise ValueError("prompt contract is not allow-listed")
    if hashlib.sha256(raw).hexdigest() != contract.prompt_set_sha256:
        raise ValueError("prompt contract hash is not allow-listed")
    return contract
```

Populate the private literal `QUALITY_CONTRACTS` mapping with the existing frozen v1 hash and the SHA-256 calculated from the final committed v2 bytes. Replace single-v1 checks in the four consumers with registry lookup. Preserve the existing v1 ID and exact SHA-256. Reject unknown IDs, mismatched hashes, substituted rendered assets, or v2 input counts above 512 before generation. Add regression assertions that v1 files and hashes remain unchanged.

- [ ] **Step 6: Implement role-specific worker specs**

STANDARD specs use `KEY_CACHE_PRECISION=f16` and `VALUE_CACHE_PRECISION=f16`. TBQ specs use `TURBOQUANT_KEY_ALGORITHM`, `TURBOQUANT_VALUE_ALGORITHM`, and `TURBOQUANT_NORM_CORRECTION=true`, without passing STANDARD precision properties. GPU STANDARD omits `NUM_STREAMS`, because the previous GPU calibration proved that property invalid for this plugin.

- [ ] **Step 7: Run focused and property-contract tests**

Run: `python -m pytest -q scripts/testing/tests/test_official_openvino_format_boundary.py scripts/testing/tests/test_official_openvino_worker_contract.py scripts/testing/tests/test_official_openvino_quality.py scripts/testing/tests/test_official_openvino_quality_worker.py scripts/testing/tests/test_official_openvino_adaptive_quality.py scripts/testing/tests/test_adjudicate_official_openvino_adaptive_quality.py`

Expected: PASS.

- [ ] **Step 8: Commit Task 1**

```powershell
git add -- experiments/manifests/official-openvino/format-boundary-matrix-v1.json experiments/granite_turboquant_intel/prompts/compact-feasibility-prompt-set-v2.json experiments/granite_turboquant_intel/prompts/fixtures/P5-compact-context-v2.txt experiments/granite_turboquant_intel/prompts/rendered-v2 scripts/testing/official_openvino/quality_contracts.py scripts/testing/run_official_openvino_quality.py scripts/testing/adjudicate_official_openvino_quality.py scripts/testing/adjudicate_official_openvino_adaptive_quality.py scripts/testing/official_openvino/quality.py scripts/testing/official_openvino/format_boundary.py scripts/testing/tests/test_official_openvino_format_boundary.py
git commit -m "test: define OpenVINO format boundary ladder"
```

### Task 2: Serial campaign controller with bounded retry and complete metrics

**Files:**
- Modify: `scripts/testing/official_openvino/format_boundary.py`
- Create: `scripts/testing/run_official_openvino_format_boundary.py`
- Modify: `scripts/testing/tests/test_official_openvino_format_boundary.py`

**Interfaces:**
- Consumes: `run_measurement_sequence(...)`, `capture_isolated_quality_campaign(...)`, `summarize_adaptive_runtime_samples(...)`, `KillOnCloseJob`, and `available_ram_bytes()`.
- Produces: `BoundaryCampaignConfig`, `run_boundary_campaign(config, *, run_measurement, run_quality, available_ram) -> dict[str, Any]`, `campaign-state.json`, one `accepted-row.json` per success, and one `terminal-boundary.json` per closed lane.

- [ ] **Step 1: Write failing serial, stop, retry, and resume tests**

```python
def test_second_matching_cpu_failure_stops_higher_formats(fake_config):
    calls = []
    def fail_twice(case, **kwargs):
        calls.append(case.label)
        raise RowFailure("minimum_available_ram", hard=False)
    state = run_boundary_campaign(
        fake_config, run_measurement=fail_twice,
        run_quality=lambda *a, **k: pytest.fail("quality must not run"),
        available_ram=lambda: 8 * 1024**3,
    )
    assert calls == ["U4 weights + TBQ3 cache"] * 2
    assert state["cpu_lane"]["status"] == "stopped"
    assert state["cpu_lane"]["reason_code"] == "minimum_available_ram"

def test_gpu_control_failure_does_not_close_cpu_lane(fake_config):
    state = run_boundary_campaign(fake_config, run_measurement=lane_aware_runner,
                                  run_quality=passing_quality,
                                  available_ram=lambda: 8 * 1024**3)
    assert state["gpu_lane"]["status"] == "stopped"
    assert state["cpu_lane"]["accepted_count"] > 3
```

Also test one hard RAM breach receives no retry, quality runs only after three accepted measured samples, state writes are atomic, and `--resume` never reruns an accepted row.

- [ ] **Step 2: Run tests and confirm missing controller behavior**

Run: `python -m pytest -q scripts/testing/tests/test_official_openvino_format_boundary.py`

Expected: FAIL on missing `run_boundary_campaign` behavior.

- [ ] **Step 3: Implement the controller types and injected boundaries**

```python
@dataclass(frozen=True)
class BoundaryCampaignConfig:
    repository_root: Path
    campaign_root: Path
    manifest_path: Path
    prompt_set_path: Path
    rubric_path: Path
    runtime_timeout_seconds: float = 180.0
    quality_timeout_seconds: float = 90.0
    row_timeout_seconds: float = 720.0
    launch_minimum_available_ram_mib: int = 4096
    emergency_minimum_available_ram_mib: int = 3072
    max_clean_retries: int = 1

class RowFailure(RuntimeError):
    def __init__(self, reason_code: str, *, hard: bool, receipt: Path | None = None):
        super().__init__(reason_code)
        self.reason_code = reason_code
        self.hard = hard
        self.receipt = receipt
```

Write state with canonical JSON to a same-directory temporary file, `fsync`, then `Path.replace`. Reject an existing output tree unless `resume=True` and every accepted receipt revalidates.

- [ ] **Step 4: Reuse the existing governed measurement sequence**

For each role, call `run_measurement_sequence` with `timeout_seconds=180`, `launch_minimum_available_ram_mib=4096`, and `emergency_minimum_available_ram_mib=3072`. Accept only summaries with exactly `sample-1`, `sample-2`, and `sample-3`, all valid, pilot/warmup excluded, complete timing/memory/utilisation metrics, matching activation, fallback false, and zero cleanup/residual counts.

- [ ] **Step 5: Reuse isolated quality capture and existing scoring gates**

Call `capture_isolated_quality_campaign` only after runtime acceptance, with the compact prompt set, the controlling rubric, and `timeout_seconds=90`. Require P1-P6 completion, exact prompt/output hashes, deterministic-gate records, and no terminal quality row before writing `accepted-row.json`.

- [ ] **Step 6: Implement concise terminal and skipped receipts**

Persist the exact failing role, fingerprint, RAM minimum, elapsed time, failure code, raw record path/hash, cleanup count, and the text `outside this laptop's configured safe RAM/time envelope` for resource failures. Mark higher CPU cases `not-attempted-after-boundary` without metric keys.

- [ ] **Step 7: Add the thin CLI**

The CLI resolves the current build provenance, U4/U8 artifacts, Python 3.13 environment, OpenVINO libraries, and sampler paths already recorded by WB-04. It prints one line per completed row and exits `0` for a clean reached-boundary result, `2` for preflight/configuration error, and `3` for an unsafe cleanup failure.

- [ ] **Step 8: Run controller and regression tests**

Run: `python -m pytest -q scripts/testing/tests/test_official_openvino_format_boundary.py scripts/testing/tests/test_measure_official_openvino_sequence.py scripts/testing/tests/test_official_openvino_measurement.py scripts/testing/tests/test_official_openvino_adaptive_metrics.py scripts/testing/tests/test_official_openvino_adaptive_quality.py scripts/testing/tests/test_official_openvino_quality.py scripts/testing/tests/test_measure_official_openvino_cli.py`

Expected: PASS.

- [ ] **Step 9: Commit Task 2**

```powershell
git add -- scripts/testing/official_openvino/format_boundary.py scripts/testing/run_official_openvino_format_boundary.py scripts/testing/tests/test_official_openvino_format_boundary.py
git commit -m "feat: add guarded OpenVINO format boundary runner"
```

### Task 3: Evidence reconciler and human-readable WB-07 publisher

**Files:**
- Create: `scripts/testing/official_openvino/format_boundary_workbook.py`
- Create: `scripts/testing/publish_openvino_format_boundary_workbook.py`
- Create: `scripts/testing/tests/test_official_openvino_format_boundary_workbook.py`
- Modify: `scripts/testing/Generate-Controlled-Workbooks.py`
- Modify: `scripts/testing/Apply-Workbook-Revision-History.py`

**Interfaces:**
- Consumes: final `campaign-state.json`, accepted rows, terminal receipts, raw sample receipts, compact quality records, and `quality-rubric-v1.json`.
- Produces: `reconcile_boundary_campaign(root: Path) -> BoundaryRelease`, `render_boundary_workbook(release: BoundaryRelease) -> str`, `validate_boundary_workbook(markdown: str, release: BoundaryRelease) -> None`, DOCX, and `release-manifest.json`.

- [ ] **Step 1: Write failing reconciliation and presentation tests**

```python
def test_release_recomputes_metrics_and_rejects_missing_quality(sample_release):
    release = reconcile_boundary_campaign(sample_release.root)
    assert release.rows[0].cpu.mean == statistics.fmean(sample_release.cpu_values)
    sample_release.remove_quality_score("P3")
    with pytest.raises(ValueError, match="complete P1-P6 quality"):
        reconcile_boundary_campaign(sample_release.root)

def test_workbook_uses_labels_not_internal_ids(sample_release):
    text = render_boundary_workbook(reconcile_boundary_campaign(sample_release.root))
    assert "U4 weights + TBQ3 cache" in text
    assert "FMT-U4-TBQ3" not in text
    assert "CPU mean / median / peak" in text
    assert "GPU mean / median / peak" in text
```

Also reject a blank table cell, `N/A`, accepted rows after the CPU boundary, GPU TurboQuant wording, fewer/more than three measured repetitions, digest drift, mismatched actual device, and a composite score outside 0-10.

- [ ] **Step 2: Run the publisher tests and confirm missing modules**

Run: `python -m pytest -q scripts/testing/tests/test_official_openvino_format_boundary_workbook.py`

Expected: FAIL because the publisher does not exist.

- [ ] **Step 3: Implement independent aggregate reconciliation**

Recompute scalar means and medians from the three raw sample receipts with `statistics.fmean` and `statistics.median`; pool every utilisation sample before computing mean, median, peak, and count. Compare each computed value with the controller summary using an absolute tolerance of `1e-9`. Rehash every referenced file from bytes. Never read display values back from Markdown.

- [ ] **Step 4: Implement blind, rubric-bound quality projection**

Create stable private labels from sorted output SHA-256 values, project outputs without format fields, apply deterministic gates first, and store prompt-level 0-10 scores plus terse deductions. The composite is the arithmetic mean of all six prompt scores, rounded only for display to two decimals; raw scores remain numeric in evidence. Any deterministic critical cap limits the prompt score exactly as defined in the controlling rubric.

- [ ] **Step 5: Render the seven-section Markdown workbook**

Successful CPU rows appear in one compact table. Put detailed sample-level values and evidence hashes in the evidence index rather than widening the main table. Present the GPU control separately. Show the first stopped case and primary reason as a short bullet, then list higher skipped labels in one sentence.

- [ ] **Step 6: Register and generate WB-07**

Append `07_OpenVINO_Format_Boundary_Workbook.md` to `WORKBOOK_TEMPLATES` and add its DOCX-to-`WB-07` mapping. Generate only WB-07 with:

```powershell
python .\scripts\testing\Generate-Controlled-Workbooks.py --workbook-id WB-07
python .\scripts\testing\Apply-Workbook-Revision-History.py --workbook-id WB-07
```

- [ ] **Step 7: Validate DOCX structurally and visually**

Open the DOCX as a ZIP, verify all package members, compare every Markdown table cell to Word table cells, ensure seven visible section headings, and render page images using the documents skill's `render_docx.py` workflow. Inspect every rendered page for clipping, split headers, unreadable columns, or blank pages.

- [ ] **Step 8: Run publisher and generator regressions**

Run: `python -m pytest -q scripts/testing/tests/test_official_openvino_format_boundary_workbook.py scripts/testing/tests/test_workbook_filter.py scripts/testing/tests/test_official_openvino_docx.py`

Expected: PASS.

- [ ] **Step 9: Commit Task 3**

```powershell
git add -- scripts/testing/official_openvino/format_boundary_workbook.py scripts/testing/publish_openvino_format_boundary_workbook.py scripts/testing/tests/test_official_openvino_format_boundary_workbook.py scripts/testing/Generate-Controlled-Workbooks.py scripts/testing/Apply-Workbook-Revision-History.py
git commit -m "feat: publish OpenVINO format boundary workbook"
```

### Task 4: Preflight and execute the bounded ladder

**Files:**
- Create: `experiments/raw-results/openvino-format-boundary/2026-08-02/`

**Interfaces:**
- Consumes: committed manifest/controller, build `00edae3b`, existing validated U4/U8 artifacts, compact prompts, and controlling rubric.
- Produces: fresh raw runtime, quality, state, terminal, and hash evidence only.

- [ ] **Step 1: Verify host readiness without closing user applications**

Run:

```powershell
Get-CimInstance Win32_OperatingSystem | Select-Object TotalVisibleMemorySize,FreePhysicalMemory
Get-Process | Where-Object { $_.ProcessName -match 'python|openvino|benchmark' } | Select-Object Id,ProcessName,Path
```

Proceed only when free RAM is at least 4096 MiB and no earlier campaign-owned model worker remains. Do not stop VS Code, Word, browsers, or unrelated Python processes.

- [ ] **Step 2: Run a dry preflight**

Run:

```powershell
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m scripts.testing.run_official_openvino_format_boundary --preflight
```

Expected: detected CPU and GPU, verified build/artifacts, available RAM at or above 4096 MiB, and zero owned workers.

- [ ] **Step 3: Start or resume the serial campaign**

Run:

```powershell
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m scripts.testing.run_official_openvino_format_boundary --resume
```

Allow the controller to stop itself. Do not rerun a closed row manually. If the command yields a running cell, wait in intervals shorter than 60 seconds and report each completed row or stop boundary to the user.

- [ ] **Step 4: Verify campaign termination and cleanup**

Run:

```powershell
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m scripts.testing.run_official_openvino_format_boundary --status
Get-Process | Where-Object { $_.ProcessName -match 'python|openvino' } | Select-Object Id,ProcessName,Path
```

Expected: CPU lane `complete` or `stopped`, GPU lane `complete` or `stopped`, no active owned PIDs, and no case still marked `running`.

### Task 5: Adjudicate quality, publish WB-07, and update controlled registers

**Files:**
- Create: `docs/testing/workbooks/text-templates/07_OpenVINO_Format_Boundary_Workbook.md`
- Create: `docs/testing/workbooks/generated/07_OpenVINO_Format_Boundary_Workbook.docx`
- Modify: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`
- Modify: `docs/testing/Workbook-Revision-Register.csv`
- Modify: `docs/testing/Workbook-Completion-Register.csv`
- Create: `experiments/raw-results/openvino-format-boundary/2026-08-02/release-manifest.json`

**Interfaces:**
- Consumes: Task 4 evidence and Task 3 publisher.
- Produces: final human-readable workbook, controlled hashes, revision/completion entries, and structural/visual QA receipts.

- [ ] **Step 1: Review blinded quality outputs and record exact deductions**

Score each private label independently using the existing 0, 2, 4, 6, 8, and 10 anchors. Reopen any critical-gate failure and any score difference greater than one point. Do not inspect the private-label mapping until all prompt scores and deductions are frozen.

- [ ] **Step 2: Publish into a staging directory and validate before replacement**

Run:

```powershell
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m scripts.testing.publish_openvino_format_boundary_workbook --campaign-root experiments/raw-results/openvino-format-boundary/2026-08-02
```

Expected: release validation PASS, Markdown validation PASS, DOCX structural validation PASS, and atomic destination replacement.

- [ ] **Step 3: Update WB-07 controlled metadata from final bytes**

Write one current WB-07 revision and seven completion rows. Calculate template and DOCX SHA-256 from disk. Keep prior WB-01 through WB-06 rows byte-for-byte unchanged except for necessary CSV newline normalization avoided by row-preserving writes.

- [ ] **Step 4: Render and visually inspect the final DOCX**

Use the documents skill render command, view every page image, and record the render output and inspection result under the campaign release directory. If local Word/LibreOffice rendering is unavailable, report that limitation and retain structural validation; never claim visual PASS without rendered pages.

- [ ] **Step 5: Run all focused validation**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_format_boundary.py scripts/testing/tests/test_official_openvino_format_boundary_workbook.py
python .\scripts\testing\Validate-Workbook-Revision-Control.py
git diff --check
```

Expected: all tests PASS, revision-control validator PASS, and no whitespace errors.

### Task 6: Independent final audit and scoped commit

**Files:**
- Verify: all Task 1-5 files and evidence.

- [ ] **Step 1: Recompute evidence and workbook values independently**

Use a separate audit process to recalculate every successful row from raw sample receipts, pool CPU/GPU samples, recompute prompt/composite quality, verify all SHA-256 values, and compare workbook cells. Require zero discrepancies.

- [ ] **Step 2: Run the full relevant regression set**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_format_boundary.py scripts/testing/tests/test_official_openvino_format_boundary_workbook.py scripts/testing/tests/test_measure_official_openvino_sequence.py scripts/testing/tests/test_official_openvino_measurement.py scripts/testing/tests/test_official_openvino_adaptive_metrics.py scripts/testing/tests/test_official_openvino_adaptive_quality.py scripts/testing/tests/test_official_openvino_quality.py scripts/testing/tests/test_workbook_filter.py scripts/testing/tests/test_official_openvino_docx.py
```

Expected: PASS.

- [ ] **Step 3: Inspect scope and commit only the new campaign plus intended prior WB-04 work**

Run `git status --short` and `git diff --stat`. Preserve unrelated historical raw directories. Stage explicit paths only; do not use `git add -A`.

```powershell
git add -- experiments/manifests/official-openvino/format-boundary-matrix-v1.json experiments/granite_turboquant_intel/prompts/compact-feasibility-prompt-set-v2.json experiments/granite_turboquant_intel/prompts/fixtures/P5-compact-context-v2.txt experiments/granite_turboquant_intel/prompts/rendered-v2 scripts/testing/official_openvino/quality_contracts.py scripts/testing/run_official_openvino_quality.py scripts/testing/adjudicate_official_openvino_quality.py scripts/testing/adjudicate_official_openvino_adaptive_quality.py scripts/testing/official_openvino/quality.py scripts/testing/official_openvino/format_boundary.py scripts/testing/official_openvino/format_boundary_workbook.py scripts/testing/run_official_openvino_format_boundary.py scripts/testing/publish_openvino_format_boundary_workbook.py scripts/testing/tests/test_official_openvino_format_boundary.py scripts/testing/tests/test_official_openvino_format_boundary_workbook.py scripts/testing/Generate-Controlled-Workbooks.py scripts/testing/Apply-Workbook-Revision-History.py docs/testing/workbooks/text-templates/07_OpenVINO_Format_Boundary_Workbook.md docs/testing/workbooks/generated/07_OpenVINO_Format_Boundary_Workbook.docx docs/testing/workbooks/Controlled-Workbook-Manifest.csv docs/testing/Workbook-Revision-Register.csv docs/testing/Workbook-Completion-Register.csv experiments/raw-results/openvino-format-boundary/2026-08-02
git commit -m "test: complete guarded OpenVINO format boundary retest"
```

- [ ] **Step 4: Apply verification-before-completion**

Reopen the final test logs, campaign status, audit receipt, and hashes. Report exact passing formats, exact first stopped format and reason, GPU-control outcome, quality scores, total test duration, and any visual-rendering limitation. Do not say the laptop cannot run a format unless the fresh terminal receipt proves that bounded claim.
