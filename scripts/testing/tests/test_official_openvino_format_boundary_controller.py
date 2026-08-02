import hashlib
import json
import shutil
import sys
import tempfile
from dataclasses import replace
from pathlib import Path
from types import SimpleNamespace

import pytest

from scripts.testing.official_openvino.adaptive_quality import (
    AdaptiveQualityCampaignInput,
    capture_isolated_quality_campaign,
)
from scripts.testing.tests.test_official_openvino_adaptive_quality import (
    RecordingGuardRunner,
)
from scripts.testing.tests.test_official_openvino_quality_campaign import (
    _accepted_input,
)
from scripts.testing.tests.test_measure_official_openvino_sequence import (
    ROLES,
    _record,
    _setup_campaign,
)


ROOT = Path(__file__).resolve().parents[3]
PROMPT_SET = (
    ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "prompts"
    / "compact-feasibility-prompt-set-v2.json"
)
RENDERED = PROMPT_SET.parent / "rendered-v2"
RUBRIC = (
    ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "rubrics"
    / "quality-rubric-v1.json"
)
BOUNDARY_MATRIX = (
    ROOT
    / "experiments"
    / "manifests"
    / "official-openvino"
    / "format-boundary-matrix-v1.json"
)


@pytest.fixture(autouse=True)
def _controlled_tokenizer_dependency(monkeypatch):
    class Tokenizer:
        @classmethod
        def from_file(cls, _path):
            return cls()

        def encode(self, _prompt):
            return SimpleNamespace(ids=list(range(357)))

    monkeypatch.setitem(sys.modules, "tokenizers", SimpleNamespace(Tokenizer=Tokenizer))


def _write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def _adaptive_input(tmp_path: Path) -> AdaptiveQualityCampaignInput:
    source = _accepted_input(tmp_path)
    sequence_path = source.campaign_root / "attempt-sequence.json"
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    pilot_path = source.campaign_root / sequence["pilot"]["spec_path"]
    spec_index = tmp_path / "projection-index.json"
    _write_json(spec_index, {"schema": "test-projection-index/v1"})
    return AdaptiveQualityCampaignInput(
        campaign_root=source.campaign_root,
        spec_path=source.spec_path,
        matrix_path=source.matrix_path,
        artifact_manifest_path=source.artifact_manifest_path,
        build_provenance_path=source.build_provenance_path,
        build_root=source.build_root,
        repo_root=source.repo_root,
        python_executable=source.python_executable,
        python_site_packages=source.python_site_packages,
        openvino_libraries=source.openvino_libraries,
        sampler_script=source.sampler_script,
        prompt_set_path=PROMPT_SET,
        rendered_root=RENDERED,
        rubric_path=RUBRIC,
        output_root=tmp_path / "quality",
        timeout_seconds=90.0,
        attempt_sequence_path=sequence_path,
        adaptive_runtime_spec_path=source.spec_path,
        pilot_spec_path=pilot_path,
        spec_index_path=spec_index,
        artifact_inventory_path=source.artifact_manifest_path,
    )


def test_runtime_reconciliation_reopens_and_hashes_all_canonical_evidence(tmp_path):
    from scripts.testing.official_openvino.format_boundary import (
        reconcile_runtime_evidence,
    )

    source = _adaptive_input(tmp_path)
    evidence = reconcile_runtime_evidence(source)

    assert evidence["measurement_summary"]["path"] == str(
        (source.campaign_root / "measurement-summary.json").resolve()
    )
    assert evidence["attempt_sequence"]["path"] == str(
        source.attempt_sequence_path.resolve()
    )
    assert evidence["adaptive_runtime_summary"]["path"] == str(
        (source.campaign_root / "adaptive-runtime-summary.json").resolve()
    )
    assert len(evidence["sample_sources"]) == 3
    assert len({row["path"] for row in evidence["sample_sources"]}) == 3
    for binding in (
        evidence["measurement_summary"],
        evidence["attempt_sequence"],
        evidence["adaptive_runtime_summary"],
        *evidence["sample_sources"],
    ):
        path = Path(binding["path"])
        assert binding["sha256"] == hashlib.sha256(path.read_bytes()).hexdigest()
    utilisation = evidence["adaptive_summary"]["utilisation"]
    for field in ("cpu_percent", "gpu_percent"):
        assert set(("mean", "median", "peak", "count")) <= set(utilisation[field])
    assert evidence["adaptive_summary"]["activation"]["device"]["requested"] == "CPU"


@pytest.mark.parametrize(
    "mutation",
    (
        "arbitrary-sequence-hash",
        "fewer-than-three",
        "changed-sample",
        "missing-adaptive-metric",
        "fallback",
        "cleanup-drift",
    ),
)
def test_runtime_reconciliation_rejects_drift_and_incomplete_metrics(
    tmp_path, mutation,
):
    from scripts.testing.official_openvino.format_boundary import (
        reconcile_runtime_evidence,
    )

    source = _adaptive_input(tmp_path)
    summary_path = source.campaign_root / "measurement-summary.json"
    sequence_path = source.campaign_root / "attempt-sequence.json"
    adaptive_path = source.campaign_root / "adaptive-runtime-summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    adaptive = json.loads(adaptive_path.read_text(encoding="utf-8"))
    if mutation == "arbitrary-sequence-hash":
        sequence["measurement_summary_sha256"] = "f" * 64
        _write_json(sequence_path, sequence)
    elif mutation == "fewer-than-three":
        sequence["accepted_samples"].pop()
        sequence["accepted_sample_count"] = 2
        _write_json(sequence_path, sequence)
    elif mutation == "changed-sample":
        Path(summary["sources"][0]["path"]).write_bytes(b"{}\n")
    elif mutation == "missing-adaptive-metric":
        del adaptive["utilisation"]["cpu_percent"]["median"]
        _write_json(adaptive_path, adaptive)
    elif mutation == "fallback":
        summary["activation"]["fallback"] = True
        _write_json(summary_path, summary)
    else:
        summary["cleanup_process_count"] = 1
        _write_json(summary_path, summary)

    with pytest.raises(ValueError):
        reconcile_runtime_evidence(source)


def test_quality_reconciliation_reopens_six_prompt_receipts_and_hashes(tmp_path):
    from scripts.testing.adjudicate_official_openvino_quality import (
        _prompt_controls,
        deterministic_gate,
    )
    from scripts.testing.official_openvino.format_boundary import (
        reconcile_quality_evidence,
    )

    source = _adaptive_input(tmp_path)
    capture_isolated_quality_campaign(
        source,
        resume=False,
        run_command=RecordingGuardRunner(),
    )

    evidence = reconcile_quality_evidence(source)

    assert evidence["completed_prompt_ids"] == [
        "P1", "P2", "P3", "P4", "P5", "P6",
    ]
    assert len(evidence["prompt_receipts"]) == 6
    assert evidence["capture_summary"]["sha256"] == hashlib.sha256(
        Path(evidence["capture_summary"]["path"]).read_bytes()
    ).hexdigest()
    for receipt in evidence["prompt_receipts"]:
        assert receipt["cleanup_process_count"] == 0
        assert receipt["active_pids_after_cleanup"] == []
        assert set(receipt["evidence"]) == {
            "worker_spec", "worker_result", "worker_log", "guard_evidence",
        }
    controls = _prompt_controls(PROMPT_SET)
    assert len(evidence["deterministic_gate_records"]) == 6
    for record, receipt in zip(
        evidence["deterministic_gate_records"],
        evidence["prompt_receipts"],
        strict=True,
    ):
        prompt_id = record["prompt_id"]
        result = json.loads(
            Path(receipt["evidence"]["worker_result"]["path"]).read_text()
        )
        turns = [
            {
                "turn_id": outcome["turn_id"],
                "output": outcome["raw_output"],
                "output_sha256": outcome["raw_output_sha256"],
            }
            for outcome in result["outcomes"]
        ]
        expected = deterministic_gate(
            prompt_id,
            turns[-1]["output"],
            turns,
            controls[prompt_id],
        )
        assert record["deterministic_gate"] == expected
        assert record["deterministic_gate_sha256"] == hashlib.sha256(
            json.dumps(
                expected,
                sort_keys=True,
                separators=(",", ":"),
                ensure_ascii=True,
                allow_nan=False,
            ).encode("utf-8")
        ).hexdigest()
    p5 = evidence["deterministic_gate_records"][4]
    assert p5["deterministic_gate"]["passed"] is False
    assert p5["deterministic_gate"]["critical_caps"] == [4.0]
    assert p5["deterministic_gate"]["reasons"] == [
        "P5 exact marker output failed",
    ]


@pytest.mark.parametrize(
    "mutation",
    (
        "missing-prompt",
        "output-hash",
        "nonzero-cleanup",
        "missing-active-pids",
    ),
)
def test_quality_reconciliation_rejects_gaps_hash_or_cleanup_drift(
    tmp_path, mutation,
):
    from scripts.testing.official_openvino.format_boundary import (
        reconcile_quality_evidence,
    )

    source = _adaptive_input(tmp_path)
    capture_isolated_quality_campaign(
        source,
        resume=False,
        run_command=RecordingGuardRunner(),
    )
    if mutation == "missing-prompt":
        summary_path = source.output_root / "capture-summary.json"
        summary = json.loads(summary_path.read_text(encoding="utf-8"))
        summary["completed_prompt_ids"].pop()
        _write_json(summary_path, summary)
    elif mutation == "output-hash":
        result_path = source.output_root / "P1" / "worker-result.json"
        result = json.loads(result_path.read_text(encoding="utf-8"))
        result["outcomes"][0]["raw_output_sha256"] = "0" * 64
        _write_json(result_path, result)
    else:
        guard_path = source.output_root / "P1" / "guard-evidence.json"
        guard = json.loads(guard_path.read_text(encoding="utf-8"))
        if mutation == "nonzero-cleanup":
            guard["cleanup_process_count"] = 1
        else:
            guard["containing_job_assignment"]["active_pids_after_cleanup"] = None
        _write_json(guard_path, guard)

    with pytest.raises(ValueError):
        reconcile_quality_evidence(source)


def test_campaign_configuration_safety_values_are_not_caller_configurable(tmp_path):
    from scripts.testing.official_openvino.format_boundary import (
        BoundaryCampaignConfig,
    )

    required = {
        "repository_root": ROOT,
        "campaign_root": tmp_path / "campaign",
        "manifest_path": tmp_path / "boundary.json",
        "comparison_matrix_path": tmp_path / "matrix.json",
        "build_root": tmp_path / "build",
        "python_executable": tmp_path / "python.exe",
        "python_site_packages": tmp_path / "site-packages",
        "openvino_libraries": tmp_path / "libraries",
        "sampler_script": tmp_path / "sampler.ps1",
        "prompt_set_path": PROMPT_SET,
        "rendered_root": RENDERED,
        "rubric_path": RUBRIC,
    }
    config = BoundaryCampaignConfig(**required)

    assert (
        config.runtime_timeout_seconds,
        config.quality_timeout_seconds,
        config.row_timeout_seconds,
        config.launch_minimum_available_ram_mib,
        config.emergency_minimum_available_ram_mib,
        config.max_clean_retries,
    ) == (180.0, 90.0, 720.0, 4096, 3072, 1)
    with pytest.raises(TypeError):
        BoundaryCampaignConfig(**required, runtime_timeout_seconds=1.0)


def test_failure_fingerprint_binds_the_runtime_build_identity(tmp_path, monkeypatch):
    from scripts.testing.official_openvino import format_boundary

    manifest_path = tmp_path / "artifact-manifest.json"
    _write_json(
        manifest_path,
        {
            "load_probe": {
                "runtime_build_manifest_sha256": "b" * 64,
                "runtime_build_commit": "c" * 40,
            }
        },
    )
    case = format_boundary.BoundaryCase(
        internal_id="cpu-u4-tbq3",
        label="U4 weights + TBQ3 cache",
        lane="cpu",
        order=1,
        weight_precision="u4",
        key_algorithm="TBQ3",
        value_algorithm="TBQ3",
        key_precision="u3",
        value_precision="u3",
        device="CPU",
        context=512,
        artifact_manifest_path=manifest_path,
    )
    captured = {}

    def canonical(value):
        captured.update(value)
        return b"fingerprint-input\n"

    monkeypatch.setattr(format_boundary, "_canonical_bytes", canonical)
    format_boundary._failure_fingerprint(
        case,
        stage="measurement",
        category="ram-floor",
        reason_code="launch-ram-reserve",
    )

    assert captured["build_identity"] == {
        "runtime_build_manifest_sha256": "b" * 64,
        "runtime_build_commit": "c" * 40,
    }
    assert captured["execution_route"] == "patched-stateful"


def _binding(path: Path) -> dict[str, str]:
    return {
        "path": str(path.resolve()),
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
    }


def _controller_projection(tmp_path: Path):
    from scripts.testing.official_openvino.format_boundary import (
        BoundaryEvidenceProjection,
        ExecutableBoundaryInput,
        ProjectionFile,
        TerminalBoundaryInput,
        load_boundary_manifest,
    )

    campaign = tmp_path / "campaign"
    execution = campaign / "execution-inputs"
    runtime_specs = []
    terminals = []
    manifest = load_boundary_manifest(BOUNDARY_MATRIX)
    for case in (*manifest.cpu_cases, *manifest.gpu_cases):
        if case.artifact_manifest_path is None:
            path = execution / "terminal-prerequisites" / f"{case.internal_id}.json"
            _write_json(
                path,
                {
                    "schema": "official-openvino-format-boundary-terminal-prerequisite/v1",
                    "role": "terminal-prerequisite",
                    "reason": "artifact-unavailable",
                    "case": {"internal_id": case.internal_id},
                },
            )
            terminals.append(
                TerminalBoundaryInput(case.internal_id, ProjectionFile(path.resolve(), _binding(path)["sha256"]))
            )
        else:
            path = execution / "runtime-specs" / case.internal_id / "512" / "runtime-spec.json"
            _write_json(
                path,
                {
                    "schema": "official-openvino-adaptive-comparison-runtime-spec/v1",
                    "controlled_test_id": case.internal_id,
                },
            )
            runtime_specs.append(
                ExecutableBoundaryInput(case.internal_id, ProjectionFile(path.resolve(), _binding(path)["sha256"]))
            )
    matrix = execution / "comparison-matrix.json"
    index = execution / "projection-index.json"
    _write_json(matrix, {"schema": "test-projected-matrix/v1"})
    _write_json(
        index,
        {
            "schema": "official-openvino-format-boundary-projection-index/v1",
            "build": {
                "provenance": {
                    "path": str((tmp_path / "build-provenance.json").resolve()),
                    "sha256": "a" * 64,
                },
            },
        },
    )
    return BoundaryEvidenceProjection(
        repository_root=ROOT,
        campaign_root=campaign,
        build_root=tmp_path / "build",
        boundary_manifest=ProjectionFile(
            BOUNDARY_MATRIX.resolve(), hashlib.sha256(BOUNDARY_MATRIX.read_bytes()).hexdigest()
        ),
        comparison_matrix=ProjectionFile(matrix.resolve(), _binding(matrix)["sha256"]),
        runtime_specs=tuple(runtime_specs),
        terminal_prerequisites=tuple(terminals),
        projection_index=ProjectionFile(index.resolve(), _binding(index)["sha256"]),
    )


def _controller_config(tmp_path: Path):
    from scripts.testing.official_openvino.format_boundary import BoundaryCampaignConfig

    for path in (
        tmp_path / "build",
        tmp_path / "site-packages",
        tmp_path / "libraries",
    ):
        path.mkdir(exist_ok=True)
    for path in (
        tmp_path / "python.exe",
        tmp_path / "sampler.ps1",
        tmp_path / "matrix.json",
    ):
        path.write_bytes(b"fixture\n")
    for path in (
        tmp_path / "site-packages" / "openvino" / "__init__.py",
        tmp_path / "build" / "openvino_genai" / "__init__.py",
    ):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b"__version__ = 'fixture'\n")
    return BoundaryCampaignConfig(
        repository_root=ROOT,
        campaign_root=tmp_path / "campaign",
        manifest_path=BOUNDARY_MATRIX,
        comparison_matrix_path=tmp_path / "matrix.json",
        build_root=tmp_path / "build",
        python_executable=tmp_path / "python.exe",
        python_site_packages=tmp_path / "site-packages",
        openvino_libraries=tmp_path / "libraries",
        sampler_script=tmp_path / "sampler.ps1",
        prompt_set_path=PROMPT_SET,
        rendered_root=RENDERED,
        rubric_path=RUBRIC,
    )


def _install_controller_boundaries(monkeypatch, tmp_path):
    from scripts.testing.official_openvino import format_boundary

    projection = _controller_projection(tmp_path)
    monkeypatch.setattr(
        format_boundary,
        "project_boundary_evidence_inputs",
        lambda **_kwargs: projection,
    )

    def runtime(input_value):
        root = Path(input_value.campaign_root)
        files = []
        for name in (
            "measurement-summary.json",
            "attempt-sequence.json",
            "adaptive-runtime-summary.json",
            "sample-1.json",
            "sample-2.json",
            "sample-3.json",
        ):
            path = root / name
            _write_json(path, {"name": name})
            files.append(path)
        return {
            "measurement_summary": _binding(files[0]),
            "attempt_sequence": _binding(files[1]),
            "adaptive_runtime_summary": _binding(files[2]),
            "sample_sources": [_binding(path) for path in files[3:]],
            "adaptive_summary": {
                "timing": {"ttft_ms": {"mean": 1.0}},
                "memory": {"available_ram_mib": {"global_min": 5000.0}},
                "utilisation": {
                    "cpu_percent": {"mean": 1.0, "median": 1.0, "peak": 1.0, "count": 3},
                    "gpu_percent": {"mean": 0.0, "median": 0.0, "peak": 0.0, "count": 3},
                },
                "activation": {"fallback": False, "device": {"requested": "CPU", "actual": "CPU"}},
                "identity_hashes": {"evidence_sha256": "e" * 64},
            },
            "campaign_identity_sha256": "c" * 64,
            "runtime_config_sha256": "d" * 64,
            "cleanup_proof": {
                "safe": True,
                "source": "validated-attempt-sequence",
                "attempt_sequence": _binding(files[1]),
                "cleanup_process_count": 0,
                "residual_owned_process_count": 0,
                "emergency_actions": [],
                "active_pids_after_cleanup": [],
            },
        }

    def quality(input_value):
        root = Path(input_value.output_root)
        summary = root / "capture-summary.json"
        _write_json(summary, {"status": "passed"})
        receipts = []
        for prompt_id in ("P1", "P2", "P3", "P4", "P5", "P6"):
            prompt_root = root / prompt_id
            evidence = {}
            for label in ("worker_spec", "worker_result", "worker_log", "guard_evidence"):
                path = prompt_root / f"{label}.txt"
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(label, encoding="utf-8")
                evidence[label] = _binding(path)
            receipts.append(
                {
                    "prompt_id": prompt_id,
                    "cleanup_process_count": 0,
                    "active_pids_after_cleanup": [],
                    "evidence": evidence,
                }
            )
        return {
            "capture_summary": _binding(summary),
            "completed_prompt_ids": ["P1", "P2", "P3", "P4", "P5", "P6"],
            "prompt_receipts": receipts,
            "deterministic_gate_records": [
                {"prompt_id": prompt_id, "outcomes": [{"status": "complete"}]}
                for prompt_id in ("P1", "P2", "P3", "P4", "P5", "P6")
            ],
        }

    monkeypatch.setattr(format_boundary, "reconcile_runtime_evidence", runtime)
    monkeypatch.setattr(format_boundary, "reconcile_quality_evidence", quality)
    return projection


def _safe_failure_record(case_id: str, code: str, *, safe: bool = True) -> dict:
    empty = [] if safe else None
    return {
        "role": "sample-1",
        "failure_category": "functional",
        "failure_code": code,
        "controlled_test_id": case_id,
        "context_tokens": 512,
        "artifact_manifest_sha256": "a" * 64,
        "execution_route": "patched-stateful",
        "launch_minimum_available_ram_mib": 4096,
        "emergency_minimum_available_ram_mib": 3072,
        "cleanup_process_count": 0 if safe else -1,
        "residual_owned_process_count": 0 if safe else -1,
        "emergency_actions": [],
        "job_object": {
            "setup_ok": True,
            "query_ok": safe,
            "terminate_job_called": False,
            "queried_active_process_count_after_cleanup": 0 if safe else None,
            "survivor_pids_after_cleanup": empty,
        },
        "containing_job_assignment": {
            "requested": True,
            "assigned_before_fine_job": True,
            "assigned_pid": 123,
            "query_ok_after_cleanup": safe,
            "active_pids_after_cleanup": empty,
        },
    }


def _sequence_failure(tmp_path: Path, case_id: str, code: str, *, safe: bool = True):
    from scripts.testing.measure_official_openvino import (
        MeasurementFailureRecord,
        MeasurementSequenceFailure,
    )

    path = tmp_path / f"{case_id}-{code}.json"
    record = _safe_failure_record(case_id, code, safe=safe)
    _write_json(path, record)
    return MeasurementSequenceFailure(
        code,
        MeasurementFailureRecord(
            role="sample-1",
            record_path=path,
            record=record,
            fingerprint="untrusted-callback-fingerprint",
        ),
    )


def test_terminal_f16_prerequisite_stops_cpu_but_gpu_control_still_runs(
    tmp_path, monkeypatch,
):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    measured = []
    quality_inputs = []

    def measure(**kwargs):
        measured.append(json.loads(Path(kwargs["spec_path"]).read_text())["controlled_test_id"])
        return {"untrusted": True}

    def quality(input_value, *, resume, **_kwargs):
        assert isinstance(input_value, AdaptiveQualityCampaignInput)
        assert resume is False
        quality_inputs.append(input_value)
        return {"untrusted": True}

    state = run_boundary_campaign(
        config,
        run_measurement=measure,
        run_quality=quality,
        available_ram=lambda: 8 * 1024**3,
    )

    assert measured == [
        "cpu-u4-tbq3", "cpu-u4-tbq4", "cpu-u4-standard",
        "cpu-u8-tbq3", "cpu-u8-tbq4", "cpu-u8-standard",
        "gpu-u4-standard-control",
    ]
    assert all(input_value.timeout_seconds == 90.0 for input_value in quality_inputs)
    assert state["cpu_lane"]["terminal_status"] == "artifact-unavailable"
    assert state["cpu_lane"]["rows"]["cpu-f16-tbq4"] == "not-attempted-after-boundary"
    assert state["gpu_lane"]["status"] == "complete"
    assert state["gpu_lane"]["accepted_count"] == 1


@pytest.mark.parametrize(
    ("codes", "expected"),
    [
        (("OOM", "OOM"), "confirmed-format-boundary"),
        (("OOM", "TIMEOUT"), "inconclusive-safety-boundary"),
    ],
)
def test_retry_requires_safe_cleanup_and_compares_canonical_failures(
    tmp_path, monkeypatch, codes, expected,
):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    calls = []

    def measure(**kwargs):
        case_id = json.loads(Path(kwargs["spec_path"]).read_text())["controlled_test_id"]
        calls.append(case_id)
        if case_id == "gpu-u4-standard-control":
            return {"untrusted": True}
        raise _sequence_failure(tmp_path, case_id, codes[len(calls) - 1])

    state = run_boundary_campaign(
        config,
        run_measurement=measure,
        run_quality=lambda *_a, **_k: {"untrusted": True},
        available_ram=lambda: 8 * 1024**3,
    )

    assert calls == [
        "cpu-u4-tbq3", "cpu-u4-tbq3", "gpu-u4-standard-control",
    ]
    assert state["cpu_lane"]["terminal_status"] == expected
    assert state["cpu_lane"]["attempt_count"] == 2
    terminal = json.loads(
        (config.campaign_root / "cpu" / "cpu-u4-tbq3" / "terminal-boundary.json").read_text()
    )
    assert terminal["attempt_count"] == 2
    assert terminal["attempts"][0]["cleanup_proof"]["safe"] is True
    if expected == "confirmed-format-boundary":
        assert terminal["attempts"][0]["fingerprint"] == terminal["attempts"][1]["fingerprint"]
    else:
        assert terminal["attempts"][0]["fingerprint"] != terminal["attempts"][1]["fingerprint"]


def test_unsafe_cleanup_gets_no_retry_sets_global_halt_and_skips_gpu(
    tmp_path, monkeypatch,
):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    calls = []

    def measure(**kwargs):
        case_id = json.loads(Path(kwargs["spec_path"]).read_text())["controlled_test_id"]
        calls.append(case_id)
        raise _sequence_failure(tmp_path, case_id, "CLEANUP", safe=False)

    state = run_boundary_campaign(
        config,
        run_measurement=measure,
        run_quality=lambda *_a, **_k: pytest.fail("quality must not run"),
        available_ram=lambda: 8 * 1024**3,
    )

    assert calls == ["cpu-u4-tbq3"]
    assert state["global_halt"]["active"] is True
    assert state["cpu_lane"]["terminal_status"] == "unsafe-cleanup"
    assert state["gpu_lane"]["status"] == "not-run-global-halt"


def test_hard_emergency_ram_breach_has_one_attempt_and_no_launch(
    tmp_path, monkeypatch,
):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    calls = []
    state = run_boundary_campaign(
        config,
        run_measurement=lambda **kwargs: calls.append(kwargs),
        run_quality=lambda *_a, **_k: pytest.fail("quality must not run"),
        available_ram=lambda: 3000 * 1024**2,
    )

    assert calls == []
    assert state["cpu_lane"]["attempt_count"] == 1
    assert state["cpu_lane"]["terminal_status"] == "emergency-ram-floor"


def test_governed_runtime_emergency_ram_record_is_hard_and_never_retried(
    tmp_path, monkeypatch,
):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    calls = []
    minimum = 3000 * 1024**2

    def measure(**kwargs):
        case_id = json.loads(Path(kwargs["spec_path"]).read_text())["controlled_test_id"]
        calls.append(case_id)
        if case_id == "gpu-u4-standard-control":
            return {"untrusted": True}
        failure = _sequence_failure(tmp_path, case_id, "minimum_available_ram")
        failure.failure.record["available_ram_bytes"] = {
            "before": 5000 * 1024**2,
            "minimum": minimum,
            "after": 3500 * 1024**2,
        }
        raise failure

    state = run_boundary_campaign(
        config,
        run_measurement=measure,
        run_quality=lambda *_a, **_k: {"untrusted": True},
        available_ram=lambda: 8 * 1024**3,
    )

    assert calls == ["cpu-u4-tbq3", "gpu-u4-standard-control"]
    terminal = json.loads(
        (config.campaign_root / "cpu" / "cpu-u4-tbq3" / "terminal-boundary.json").read_text()
    )
    assert terminal["attempt_count"] == 1
    assert terminal["attempts"][0]["hard"] is True
    assert terminal["attempts"][0]["retryable"] is False
    assert terminal["observed_available_ram_bytes"] == minimum


def test_cleanup_safe_quality_failure_retries_in_a_fresh_attempt_root(
    tmp_path, monkeypatch,
):
    from scripts.testing.official_openvino import format_boundary

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    successful_reconcile = format_boundary.reconcile_quality_evidence
    runtime_roots = []
    quality_roots = []

    def measure(**kwargs):
        runtime_roots.append(Path(kwargs["campaign_root"]).resolve())
        return {"untrusted": True}

    def quality(input_value, *, resume, **_kwargs):
        quality_roots.append(Path(input_value.output_root).resolve())
        if (
            "cpu-u4-tbq3" in input_value.output_root.parts
            and "attempt-001" in input_value.output_root.parts
        ):
            guard_path = input_value.output_root / "P1" / "guard-evidence.json"
            guard = _safe_failure_record("cpu-u4-tbq3", "quality-worker-failed")
            _write_json(guard_path, guard)
            summary = {
                "schema": "official-openvino-adaptive-quality-capture/v1",
                "status": "quality-blocked",
                "prompt_receipts": [
                    {
                        "prompt_id": "P1",
                        "status": "failed",
                        "guard_evidence_path": str(guard_path.resolve()),
                        "guard_evidence_sha256": _binding(guard_path)["sha256"],
                        "worker_result_path": str(
                            (input_value.output_root / "P1" / "worker-result.json").resolve()
                        ),
                    }
                ],
            }
            _write_json(input_value.output_root / "capture-summary.json", summary)
        return {"untrusted": True}

    def reconcile(input_value):
        if (
            "cpu-u4-tbq3" in input_value.output_root.parts
            and "attempt-001" in input_value.output_root.parts
        ):
            raise ValueError("P1 governed quality worker failed")
        return successful_reconcile(input_value)

    monkeypatch.setattr(format_boundary, "reconcile_quality_evidence", reconcile)
    state = format_boundary.run_boundary_campaign(
        config,
        run_measurement=measure,
        run_quality=quality,
        available_ram=lambda: 8 * 1024**3,
    )

    first = config.campaign_root / "cpu" / "cpu-u4-tbq3"
    assert runtime_roots[:2] == [
        (first / "attempt-001" / "runtime").resolve(),
        (first / "attempt-002" / "runtime").resolve(),
    ]
    assert quality_roots[:2] == [
        (first / "attempt-001" / "quality").resolve(),
        (first / "attempt-002" / "quality").resolve(),
    ]
    accepted = json.loads((first / "accepted-row.json").read_text())
    assert accepted["selected_attempt_number"] == 2
    assert accepted["execution_root"] == str(
        (first / "attempt-002").resolve()
    )
    assert state["cpu_lane"]["rows"]["cpu-u4-tbq3"] == "accepted"


def test_accepted_receipt_binds_reduced_quality_timeout_and_absolute_deadline(
    tmp_path, monkeypatch,
):
    from scripts.testing.official_openvino import format_boundary

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    clock = SimpleNamespace(now=0.0)

    def measure(**_kwargs):
        clock.now = 650.0
        return {"untrusted": True}

    state = format_boundary.run_boundary_campaign(
        config,
        run_measurement=measure,
        run_quality=lambda *_a, **_k: {"untrusted": True},
        available_ram=lambda: 8 * 1024**3,
        monotonic=lambda: clock.now,
    )

    accepted_path = config.campaign_root / "cpu" / "cpu-u4-tbq3" / "accepted-row.json"
    accepted = json.loads(accepted_path.read_text())
    assert accepted["selected_attempt_number"] == 1
    assert accepted["stage_timeouts"] == {"measurement": 180.0, "quality": 70.0}
    assert accepted["monotonic_deadline"] == 720.0
    assert state["cpu_lane"]["rows"]["cpu-u4-tbq3"] == "accepted"

    observed_timeouts = []
    original = format_boundary.reconcile_runtime_evidence

    def reconcile(input_value):
        observed_timeouts.append(input_value.timeout_seconds)
        return original(input_value)

    monkeypatch.setattr(format_boundary, "reconcile_runtime_evidence", reconcile)
    format_boundary.run_boundary_campaign(
        replace(config, resume=True),
        run_measurement=lambda **_kwargs: pytest.fail("accepted runtime reran"),
        run_quality=lambda *_args, **_kwargs: pytest.fail("accepted quality reran"),
        available_ram=lambda: 8 * 1024**3,
        monotonic=lambda: clock.now,
    )
    assert observed_timeouts == [70.0] + [90.0] * 6


def test_after_runtime_deadline_uses_validated_sequence_cleanup_proof(
    tmp_path, monkeypatch,
):
    from scripts.testing.official_openvino import format_boundary

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    clock = SimpleNamespace(now=0.0)
    original = format_boundary.reconcile_runtime_evidence

    def reconcile(input_value):
        value = original(input_value)
        value["cleanup_proof"] = {
            "safe": True,
            "source": "validated-attempt-sequence",
            "attempt_sequence": value["attempt_sequence"],
            "cleanup_process_count": 0,
            "residual_owned_process_count": 0,
            "emergency_actions": [],
            "active_pids_after_cleanup": [],
        }
        return value

    monkeypatch.setattr(format_boundary, "reconcile_runtime_evidence", reconcile)

    def measure(**_kwargs):
        clock.now = 721.0
        return {"untrusted": True}

    format_boundary.run_boundary_campaign(
        config,
        run_measurement=measure,
        run_quality=lambda *_a, **_k: {"untrusted": True},
        available_ram=lambda: 8 * 1024**3,
        monotonic=lambda: clock.now,
    )

    terminal = json.loads(
        (config.campaign_root / "cpu" / "cpu-u4-tbq3" / "terminal-boundary.json").read_text()
    )
    assert terminal["cleanup_proof"]["source"] == "validated-attempt-sequence"
    assert terminal["cleanup_proof"]["attempt_sequence"]["sha256"]
    assert terminal["cleanup_proof"].get("job_active_pid_evidence") != "not-launched"


def test_deadline_passes_remaining_budgets_and_rejects_late_quality(
    tmp_path, monkeypatch,
):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    clock = SimpleNamespace(now=0.0)
    measurement_timeouts = []
    quality_timeouts = []

    def measure(**kwargs):
        measurement_timeouts.append(kwargs["timeout_seconds"])
        if len(measurement_timeouts) == 1:
            clock.now = 650.0
        return {"untrusted": True}

    def quality(input_value, *, resume, **_kwargs):
        quality_timeouts.append(input_value.timeout_seconds)
        if len(quality_timeouts) == 1:
            clock.now = 721.0
        return {"untrusted": True}

    state = run_boundary_campaign(
        config,
        run_measurement=measure,
        run_quality=quality,
        available_ram=lambda: 8 * 1024**3,
        monotonic=lambda: clock.now,
    )

    assert measurement_timeouts == [180.0, 180.0]
    assert quality_timeouts == [70.0, 90.0]
    assert state["cpu_lane"]["terminal_status"] == "row-deadline-exceeded"
    terminal = json.loads(
        (config.campaign_root / "cpu" / "cpu-u4-tbq3" / "terminal-boundary.json").read_text()
    )
    assert terminal["role"] == "quality"
    assert terminal["deadline_outcome"] == "exceeded-after-quality"
    assert terminal["stage_timeouts"] == {"measurement": 180.0, "quality": 70.0}


def test_resume_rehashes_accepted_evidence_and_never_reruns_rows(
    tmp_path, monkeypatch,
):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    run_boundary_campaign(
        config,
        run_measurement=lambda **_kwargs: {"untrusted": True},
        run_quality=lambda *_args, **_kwargs: {"untrusted": True},
        available_ram=lambda: 8 * 1024**3,
    )
    resumed = run_boundary_campaign(
        replace(config, resume=True),
        run_measurement=lambda **_kwargs: pytest.fail("accepted runtime reran"),
        run_quality=lambda *_args, **_kwargs: pytest.fail("accepted quality reran"),
        available_ram=lambda: 8 * 1024**3,
    )

    assert resumed["cpu_lane"]["accepted_count"] == 6
    assert resumed["gpu_lane"]["accepted_count"] == 1
    target = (
        config.campaign_root
        / "cpu"
        / "cpu-u4-tbq3"
        / "attempt-001"
        / "runtime"
        / "measurement-summary.json"
    )
    target.write_bytes(target.read_bytes() + b" ")
    with pytest.raises(ValueError, match="hash drift"):
        run_boundary_campaign(
            replace(config, resume=True),
            run_measurement=lambda **_kwargs: pytest.fail("tampered runtime reran"),
            run_quality=lambda *_args, **_kwargs: pytest.fail("tampered quality reran"),
            available_ram=lambda: 8 * 1024**3,
        )


def test_resume_rejects_terminal_prerequisite_tampering(tmp_path, monkeypatch):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    projection = _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    run_boundary_campaign(
        config,
        run_measurement=lambda **_kwargs: {"untrusted": True},
        run_quality=lambda *_args, **_kwargs: {"untrusted": True},
        available_ram=lambda: 8 * 1024**3,
    )
    descriptor = next(
        item.descriptor.path
        for item in projection.terminal_prerequisites
        if item.case_internal_id == "cpu-f16-tbq3"
    )
    descriptor.write_bytes(descriptor.read_bytes() + b" ")

    with pytest.raises(ValueError, match="hash drift"):
        run_boundary_campaign(
            replace(config, resume=True),
            run_measurement=lambda **_kwargs: pytest.fail("terminal row reran"),
            run_quality=lambda *_args, **_kwargs: pytest.fail("terminal quality ran"),
            available_ram=lambda: 8 * 1024**3,
        )


def test_resume_admits_the_projection_bound_cache_tree(tmp_path, monkeypatch):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    run_boundary_campaign(
        config,
        run_measurement=lambda **_kwargs: {"untrusted": True},
        run_quality=lambda *_args, **_kwargs: {"untrusted": True},
        available_ram=lambda: 8 * 1024**3,
    )
    cache_file = config.campaign_root / "cache" / "cpu-u4-tbq3" / "512" / "blob.bin"
    cache_file.parent.mkdir(parents=True)
    cache_file.write_bytes(b"runtime cache")

    resumed = run_boundary_campaign(
        replace(config, resume=True),
        run_measurement=lambda **_kwargs: pytest.fail("cache resume reran runtime"),
        run_quality=lambda *_args, **_kwargs: pytest.fail("cache resume reran quality"),
        available_ram=lambda: 8 * 1024**3,
    )
    assert resumed["cpu_lane"]["accepted_count"] == 6


def test_resume_rejects_state_claim_without_its_durable_receipt(tmp_path, monkeypatch):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    run_boundary_campaign(
        config,
        run_measurement=lambda **_kwargs: {"untrusted": True},
        run_quality=lambda *_args, **_kwargs: {"untrusted": True},
        available_ram=lambda: 8 * 1024**3,
    )
    (config.campaign_root / "cpu" / "cpu-u4-tbq3" / "accepted-row.json").unlink()

    with pytest.raises(ValueError, match="state.*receipt|receipt.*state"):
        run_boundary_campaign(
            replace(config, resume=True),
            run_measurement=lambda **_kwargs: pytest.fail("missing receipt reran"),
            run_quality=lambda *_args, **_kwargs: pytest.fail("missing receipt quality ran"),
            available_ram=lambda: 8 * 1024**3,
        )


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("accepted_count", 5),
        ("status", "running"),
        ("rows", {"cpu-u4-tbq3": "unsafe-cleanup"}),
    ],
)
def test_resume_rejects_mutable_lane_state_drift(
    tmp_path, monkeypatch, field, value,
):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    run_boundary_campaign(
        config,
        run_measurement=lambda **_kwargs: {"untrusted": True},
        run_quality=lambda *_args, **_kwargs: {"untrusted": True},
        available_ram=lambda: 8 * 1024**3,
    )
    state_path = config.campaign_root / "campaign-state.json"
    state = json.loads(state_path.read_text())
    state["cpu_lane"][field] = value
    _write_json(state_path, state)

    with pytest.raises(ValueError, match="state.*receipt|receipt.*state"):
        run_boundary_campaign(
            replace(config, resume=True),
            run_measurement=lambda **_kwargs: pytest.fail("drifted state reran"),
            run_quality=lambda *_args, **_kwargs: pytest.fail("drifted state quality ran"),
            available_ram=lambda: 8 * 1024**3,
        )


def test_resume_rejects_malformed_terminal_and_skipped_receipts(
    tmp_path, monkeypatch,
):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    run_boundary_campaign(
        config,
        run_measurement=lambda **_kwargs: {"untrusted": True},
        run_quality=lambda *_args, **_kwargs: {"untrusted": True},
        available_ram=lambda: 8 * 1024**3,
    )
    terminal_path = (
        config.campaign_root / "cpu" / "cpu-f16-tbq3" / "terminal-boundary.json"
    )
    skipped_path = config.campaign_root / "cpu" / "cpu-f16-tbq4" / "skipped-row.json"
    originals = {
        terminal_path: json.loads(terminal_path.read_text()),
        skipped_path: json.loads(skipped_path.read_text()),
    }
    mutations = [
        (terminal_path, "schema", "wrong/v1"),
        (terminal_path, "status", "accepted"),
        (terminal_path, "attempt_count", 0),
        (terminal_path, "limits", {}),
        (terminal_path, "cleanup_proof", {"safe": False}),
        (skipped_path, "schema", "wrong/v1"),
        (skipped_path, "status", "accepted"),
        (skipped_path, "attempt_count", 1),
        (skipped_path, "limits", {}),
    ]
    for path, field, value in mutations:
        receipt = dict(originals[path])
        receipt[field] = value
        _write_json(path, receipt)
        with pytest.raises(ValueError, match="receipt.*invalid"):
            run_boundary_campaign(
                replace(config, resume=True),
                run_measurement=lambda **_kwargs: pytest.fail("malformed receipt reran"),
                run_quality=lambda *_args, **_kwargs: pytest.fail("malformed quality ran"),
                available_ram=lambda: 8 * 1024**3,
            )
        _write_json(path, originals[path])


def test_durable_row_output_is_one_canonical_line_per_case(tmp_path, monkeypatch):
    from scripts.testing.official_openvino.format_boundary import (
        durable_row_lines,
        run_boundary_campaign,
    )

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    state = run_boundary_campaign(
        config,
        run_measurement=lambda **_kwargs: {"untrusted": True},
        run_quality=lambda *_args, **_kwargs: {"untrusted": True},
        available_ram=lambda: 8 * 1024**3,
    )
    lines = durable_row_lines(config, state)

    assert len(lines) == 10
    decoded = [json.loads(line) for line in lines]
    assert [row["case"]["internal_id"] for row in decoded] == [
        "cpu-u4-tbq3", "cpu-u4-tbq4", "cpu-u4-standard",
        "cpu-u8-tbq3", "cpu-u8-tbq4", "cpu-u8-standard",
        "cpu-f16-tbq3", "cpu-f16-tbq4", "cpu-f16-standard",
        "gpu-u4-standard-control",
    ]
    accepted = decoded[0]
    assert accepted["status"] == "accepted"
    assert set(accepted) == {
        "attempt_count", "case", "lane", "quality_summary",
        "runtime_summary", "status",
    }
    terminal = decoded[6]
    assert terminal["status"] == "artifact-unavailable"
    assert set(("reason_code", "fingerprint")) <= set(terminal)
    assert decoded[7]["status"] == "not-attempted-after-boundary"


def test_cli_contract_has_projection_modes_and_no_manual_spec_or_recovery():
    from scripts.testing.run_official_openvino_format_boundary import _parser

    parser = _parser()
    destinations = {action.dest for action in parser._actions}

    assert {"preflight", "resume", "status"} <= destinations
    assert "spec_root" not in destinations
    assert "quality_recovery" not in destinations
    assert "build_provenance" not in destinations


def _passing_python_probe(config):
    return {
        "python_executable": str(config.python_executable.resolve()),
        "python_version": "3.13.7",
        "openvino": {
            "path": str((config.python_site_packages / "openvino" / "__init__.py").resolve()),
            "version": "2026.2.0",
        },
        "openvino_genai": {
            "path": str((config.build_root / "openvino_genai" / "__init__.py").resolve()),
            "version": "2026.2.0",
        },
        "available_devices": ["CPU", "GPU.0"],
    }


def test_no_model_preflight_persists_ram_devices_imports_and_zero_owned_pids(
    tmp_path, monkeypatch,
):
    from scripts.testing.official_openvino.format_boundary import (
        prepare_boundary_projection,
        run_boundary_preflight,
    )

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    receipt = run_boundary_preflight(
        config,
        available_ram=lambda: 5 * 1024**3,
        python_probe=_passing_python_probe,
        owned_pid_probe=lambda _root: {"query_ok": True, "active_pids": []},
    )

    assert receipt["status"] == "passed"
    assert receipt["available_ram_bytes"] == 5 * 1024**3
    assert receipt["detected_devices"] == ["CPU", "GPU.0"]
    assert receipt["owned_process_probe"] == {
        "query_ok": True,
        "active_pids": [],
    }
    assert receipt["python_probe"]["python_executable"] == str(
        config.python_executable.resolve()
    )
    assert receipt["input_bindings"]["cache_root"] == str(
        (config.campaign_root / "cache").resolve()
    )
    persisted = json.loads(
        (config.campaign_root / "preflight-receipt.json").read_text()
    )
    assert persisted == receipt
    persisted["input_bindings"]["sampler_script"]["sha256"] = "0" * 64
    _write_json(config.campaign_root / "preflight-receipt.json", persisted)
    with pytest.raises(ValueError, match="preflight receipt is invalid"):
        prepare_boundary_projection(config)


@pytest.mark.parametrize("failure", ("ram", "cpu", "gpu", "owned", "python"))
def test_no_model_preflight_rejects_unsafe_or_mismatched_host(
    tmp_path, monkeypatch, failure,
):
    from scripts.testing.official_openvino.format_boundary import run_boundary_preflight

    _install_controller_boundaries(monkeypatch, tmp_path)
    config = _controller_config(tmp_path)
    probe = _passing_python_probe(config)
    if failure == "cpu":
        probe["available_devices"] = ["GPU.0"]
    elif failure == "gpu":
        probe["available_devices"] = ["CPU"]
    elif failure == "python":
        probe["python_executable"] = str(tmp_path / "other-python.exe")
    owned = {
        "query_ok": True,
        "active_pids": [1234] if failure == "owned" else [],
    }
    ram = 3 * 1024**3 if failure == "ram" else 5 * 1024**3

    with pytest.raises(RuntimeError if failure in {"ram", "owned"} else ValueError):
        run_boundary_preflight(
            config,
            available_ram=lambda: ram,
            python_probe=lambda _config: probe,
            owned_pid_probe=lambda _root: owned,
        )


def test_cli_no_argument_defaults_come_from_committed_repository_provenance():
    from scripts.testing import run_official_openvino_format_boundary as cli

    args = cli._parser().parse_args([])
    config = cli._config(args)

    assert config.manifest_path == BOUNDARY_MATRIX
    assert config.comparison_matrix_path == (
        ROOT / "experiments/manifests/official-openvino/adaptive-format-comparison-matrix-v1.json"
    )
    assert config.campaign_root == (
        ROOT / "experiments/raw-results/openvino-format-boundary/2026-08-02"
    )
    assert config.build_root == Path(r"C:\ov-wb04\2026-07-30\build-genai-tq-00edae3b")
    assert config.python_executable == (
        ROOT / ".venv-official-openvino-turboquant-py313/Scripts/python.exe"
    )
    assert config.resume is False


def _cli_args(tmp_path: Path, mode: str) -> list[str]:
    config = _controller_config(tmp_path)
    return [
        "--manifest", str(config.manifest_path),
        "--campaign-root", str(config.campaign_root),
        "--matrix", str(config.comparison_matrix_path),
        "--build-root", str(config.build_root),
        "--python-executable", str(config.python_executable),
        "--python-site-packages", str(config.python_site_packages),
        "--openvino-libraries", str(config.openvino_libraries),
        "--sampler-script", str(config.sampler_script),
        "--prompt-set", str(config.prompt_set_path),
        "--rendered-root", str(config.rendered_root),
        "--rubric", str(config.rubric_path),
        mode,
    ]


def test_cli_preflight_and_status_do_not_launch_and_use_exit_contract(
    tmp_path, monkeypatch, capsys,
):
    from scripts.testing import run_official_openvino_format_boundary as cli

    monkeypatch.setattr(
        cli,
        "run_boundary_preflight",
        lambda _config: {"schema": "preflight/v1", "status": "passed"},
    )
    monkeypatch.setattr(
        cli,
        "run_boundary_campaign",
        lambda *_a, **_k: pytest.fail("preflight/status launched campaign"),
    )
    assert cli.main(_cli_args(tmp_path, "--preflight")) == 0
    assert json.loads(capsys.readouterr().out)["status"] == "passed"

    state = {
        "global_halt": {"active": True, "reason_code": "unsafe-cleanup"},
        "cpu_lane": {"rows": {}},
        "gpu_lane": {"rows": {}},
    }
    monkeypatch.setattr(cli, "load_boundary_status", lambda _config: state)
    monkeypatch.setattr(cli, "durable_row_lines", lambda *_a: ["{\"status\":\"unsafe-cleanup\"}"])
    assert cli.main(_cli_args(tmp_path, "--status")) == 3
    assert capsys.readouterr().out.strip() == '{"status":"unsafe-cleanup"}'


def test_cli_execute_returns_zero_or_three_from_persisted_cleanup_state(
    tmp_path, monkeypatch, capsys,
):
    from scripts.testing import run_official_openvino_format_boundary as cli

    clean = {
        "global_halt": {"active": False, "reason_code": None},
        "cpu_lane": {"rows": {}},
        "gpu_lane": {"rows": {}},
    }
    unsafe = {
        "global_halt": {"active": True, "reason_code": "unsafe-cleanup"},
        "cpu_lane": {"rows": {}},
        "gpu_lane": {"rows": {}},
    }
    monkeypatch.setattr(cli, "durable_row_lines", lambda *_a: ["row"])
    monkeypatch.setattr(cli, "run_boundary_preflight", lambda _config: {"status": "passed"})
    observed_resume = []

    def clean_campaign(config, **_kwargs):
        observed_resume.append(config.resume)
        return clean

    monkeypatch.setattr(cli, "run_boundary_campaign", clean_campaign)
    assert cli.main(_cli_args(tmp_path, "--resume")) == 0
    assert capsys.readouterr().out.strip() == "row"
    assert observed_resume == [False]

    config = _controller_config(tmp_path)
    _write_json(config.campaign_root / "campaign-state.json", {"existing": True})
    assert cli.main(_cli_args(tmp_path, "--resume")) == 0
    capsys.readouterr()
    assert observed_resume == [False, True]

    monkeypatch.setattr(cli, "run_boundary_campaign", lambda *_a, **_k: unsafe)
    assert cli.main(_cli_args(tmp_path, "--resume")) == 3
    assert capsys.readouterr().out.strip() == "row"


def test_cli_configuration_failure_is_exit_two(tmp_path, capsys):
    from scripts.testing import run_official_openvino_format_boundary as cli

    arguments = _cli_args(tmp_path, "--preflight")
    Path(arguments[arguments.index("--matrix") + 1]).unlink()

    assert cli.main(arguments) == 2
    assert "configuration error" in capsys.readouterr().err


def _rewrite_precision_manifest(path: Path, precision: str) -> None:
    manifest = json.loads(path.read_text(encoding="utf-8"))
    model_root = Path(manifest["artifact_root"])
    xml = model_root / "openvino_model.xml"
    xml.write_text(
        f'<model><data element_type="{"i4" if precision == "u4" else "u8"}"/></model>',
        encoding="utf-8",
    )
    (model_root / "openvino_config.json").write_text(
        json.dumps({"weight_format": "int4" if precision == "u4" else "int8"}),
        encoding="utf-8",
    )
    inventory = [
        {
            "path": source.name,
            "size_bytes": source.stat().st_size,
            "sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
        }
        for source in sorted(model_root.iterdir())
        if source.is_file()
    ]
    inventory.sort(key=lambda row: row["path"])
    inventory_sha256 = hashlib.sha256(
        json.dumps(inventory, sort_keys=True, separators=(",", ":")).encode("utf-8")
    ).hexdigest()
    xml_row = next(row for row in inventory if row["path"] == "openvino_model.xml")
    manifest["artifact_id"] = f"controlled-granite-{precision}"
    manifest["model"]["precision"] = precision
    manifest["model"]["artifact_repository"] = f"publisher/granite-{precision}-ov"
    manifest["conversion"]["command"][-1] = "int4" if precision == "u4" else "int8"
    manifest["files"] = inventory
    manifest["inventory_sha256"] = inventory_sha256
    manifest["precision_proof"] = {
        "path": "openvino_model.xml",
        "sha256": xml_row["sha256"],
        "element_type": "i4" if precision == "u4" else "u8",
        "element_type_count": 1,
    }
    manifest["load_probe"]["artifact_inventory_sha256"] = inventory_sha256
    manifest["load_probe"]["runtime_build_commit"] = "a" * 40
    _write_json(path, manifest)


def _real_projection_workspace():
    workspace = Path(tempfile.mkdtemp(prefix=".boundary-controller-", dir=ROOT))
    u4 = _setup_campaign(workspace / "u4")
    u8 = _setup_campaign(workspace / "u8")
    _rewrite_precision_manifest(u4["artifact_manifest_path"], "u4")
    _rewrite_precision_manifest(u8["artifact_manifest_path"], "u8")
    manifests = {"u4": u4["artifact_manifest_path"], "u8": u8["artifact_manifest_path"]}
    boundary = json.loads(BOUNDARY_MATRIX.read_text(encoding="utf-8"))
    for row in boundary["cases"]:
        precision = row["weight_precision"]
        if precision in manifests:
            row["artifact_manifest_path"] = manifests[precision].relative_to(ROOT).as_posix()
    boundary_path = workspace / "format-boundary.json"
    _write_json(boundary_path, boundary)
    authoritative_path = workspace / "authoritative-matrix.json"
    authoritative = json.loads(
        (
            ROOT
            / "experiments"
            / "manifests"
            / "official-openvino"
            / "adaptive-format-comparison-matrix-v1.json"
        ).read_text(encoding="utf-8")
    )
    for row in authoritative["cases"]:
        precision = row["weight_precision"]
        if precision not in manifests:
            continue
        manifest_path = manifests[precision]
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        row.update(
            artifact_id=manifest["artifact_id"],
            artifact_manifest_path=str(manifest_path.resolve()),
            artifact_manifest_sha256=hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
            artifact_status="available",
            artifact_terminal_path=None,
            artifact_terminal_sha256=None,
        )
    _write_json(authoritative_path, authoritative)
    return workspace, u4, boundary_path, authoritative_path


def _boundary_record(role: str, spec: dict) -> dict:
    record = _record(role, ROLES.index(role), spec)
    record["available_ram_bytes"] = {
        "before": 6000 * 1024**2,
        "minimum": 5000 * 1024**2,
        "after": 5500 * 1024**2,
    }
    activation = record["activation"]
    properties = spec["properties"]
    algorithm = properties.get("TURBOQUANT_KEY_ALGORITHM", "STANDARD")
    precision = {"TBQ3": "u3", "TBQ4": "u4", "STANDARD": "f16"}[algorithm]
    device = spec["device"]
    activation.update(
        requested_key_algorithm=algorithm,
        requested_value_algorithm=algorithm,
        activated_key_algorithm=algorithm,
        activated_value_algorithm=algorithm,
        requested_key_cache_precision=precision,
        requested_value_cache_precision=precision,
        activated_key_cache_precision=precision,
        activated_value_cache_precision=precision,
        device=device,
        actual_device=device,
    )
    if algorithm == "STANDARD":
        standard = 100 * 1024**2
        activation.update(
            status="not_requested",
            observed_key_state_precision="f16",
            observed_value_state_precision="f16",
            norm_correction=False,
            attention_path="stateful_sdpa_standard",
            expected_bytes=standard,
            actual_bytes=standard,
            expected_persistent_standard_bytes=standard,
            actual_persistent_standard_bytes=standard,
            expected_persistent_payload_bytes=0,
            actual_persistent_payload_bytes=0,
            expected_persistent_norm_bytes=0,
            actual_persistent_norm_bytes=0,
            expected_persistent_metadata_bytes=0,
            actual_persistent_metadata_bytes=0,
            decoded_scratch_bytes=0,
            full_precision_equivalent_bytes=standard,
            operation_type="not_requested",
            operation_count=0,
            matched_state_count=0,
            transformed_model_hash="not_requested",
            runtime_layer_type="not_requested",
        )
    if device == "GPU":
        record["gpu_engine_count"] = {
            "values": [1.0, 1.0],
            "mean": 1.0,
            "median": 1.0,
            "peak": 1.0,
            "count": 2,
            "query_succeeded": True,
        }
    return record


def test_cli_flows_projected_u4_through_real_runtime_and_quality_validators(
    monkeypatch, capsys,
):
    from scripts.testing import run_official_openvino_format_boundary as cli
    from scripts.testing.measure_official_openvino import (
        run_measurement_sequence as real_sequence,
    )

    workspace, source, boundary, authoritative = _real_projection_workspace()
    try:
        measured = []
        quality_inputs = []

        def sequence(**kwargs):
            def worker(**worker_kwargs):
                spec = json.loads(worker_kwargs["spec_path"].read_text(encoding="utf-8"))
                record = _boundary_record(worker_kwargs["role"], spec)
                _write_json(worker_kwargs["output_dir"] / "attempt.json", record)
                return record

            measured.append(json.loads(kwargs["spec_path"].read_text())["controlled_test_id"])
            return real_sequence(**kwargs, run_measurement=worker)

        def quality(input_value, *, resume, **deadline):
            quality_inputs.append(input_value)
            return capture_isolated_quality_campaign(
                input_value,
                resume=resume,
                run_command=RecordingGuardRunner(),
                **deadline,
            )

        monkeypatch.setattr(cli, "run_measurement_sequence", sequence)
        monkeypatch.setattr(cli, "capture_isolated_quality_campaign", quality)
        monkeypatch.setattr(cli, "available_ram_bytes", lambda: 8 * 1024**3)
        monkeypatch.setattr(
            cli,
            "run_boundary_preflight",
            lambda _config: {"status": "passed"},
        )
        arguments = [
            "--manifest", str(boundary),
            "--campaign-root", str(workspace / "campaign"),
            "--matrix", str(authoritative),
            "--build-root", str(source["build_root"]),
            "--python-executable", str(source["python_executable"]),
            "--python-site-packages", str(source["python_site_packages"]),
            "--openvino-libraries", str(source["openvino_libraries"]),
            "--sampler-script", str(source["sampler_script"]),
            "--prompt-set", str(PROMPT_SET),
            "--rendered-root", str(RENDERED),
            "--rubric", str(RUBRIC),
        ]

        assert cli.main(arguments) == 0
        lines = [json.loads(line) for line in capsys.readouterr().out.splitlines()]
        assert len(lines) == 10
        assert measured[0] == "cpu-u4-tbq3"
        assert all(isinstance(value, AdaptiveQualityCampaignInput) for value in quality_inputs)
        assert all(value.timeout_seconds == 90.0 for value in quality_inputs)
        assert all(value.prompt_set_path == PROMPT_SET for value in quality_inputs)
        assert not list(workspace.rglob("quality-recovery.json"))
        assert lines[6]["status"] == "artifact-unavailable"
        gpu_summary = json.loads(
            (
                workspace
                / "campaign"
                / "gpu-control"
                / "gpu-u4-standard-control"
                / "attempt-001"
                / "runtime"
                / "adaptive-runtime-summary.json"
            ).read_text(encoding="utf-8")
        )
        assert gpu_summary["utilisation"]["gpu_percent"] == {
            "values": [22.0, 24.0, 23.0, 25.0, 24.0, 26.0],
            "mean": 24.0,
            "median": 24.0,
            "peak": 26.0,
            "count": 6,
        }
    finally:
        shutil.rmtree(workspace)
