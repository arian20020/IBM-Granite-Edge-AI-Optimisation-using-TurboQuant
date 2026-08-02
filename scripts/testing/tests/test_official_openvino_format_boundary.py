import hashlib
import json
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
FIXTURE_MATRIX = (
    ROOT
    / "experiments"
    / "manifests"
    / "official-openvino"
    / "format-boundary-matrix-v1.json"
)
ADAPTIVE_MATRIX = (
    ROOT
    / "experiments"
    / "manifests"
    / "official-openvino"
    / "adaptive-format-comparison-matrix-v1.json"
)
PROMPT_ROOT = ROOT / "experiments" / "granite_turboquant_intel" / "prompts"
V1_PROMPT_SET = PROMPT_ROOT / "fixed-feasibility-prompt-set-v1.json"
V2_PROMPT_SET = PROMPT_ROOT / "compact-feasibility-prompt-set-v2.json"
FROZEN_V1_SHA256 = "9ba512818e81e0ba8da3ddc89cf040dd3b779d1edc41db23d3a896d778de807f"


def _controlled_build_root(tmp_path):
    build_root = tmp_path / "build"
    package = build_root / "openvino_genai"
    package.mkdir(parents=True)
    (package / "__init__.py").write_bytes(b"# controlled package\n")
    (package / "py_openvino_genai.pyd").write_bytes(b"module-a")
    (package / "openvino_genai.dll").write_bytes(b"runtime-a")
    return build_root


def _stub_committed_model_hashes(monkeypatch, calls=None):
    from scripts.testing.official_openvino import format_boundary
    from scripts.testing.official_openvino.artifact_inventory import sha256_file

    expected = {}
    manifest_payload = json.loads(FIXTURE_MATRIX.read_text())
    for row in manifest_payload["cases"]:
        relative = row["artifact_manifest_path"]
        if relative is None:
            continue
        manifest = json.loads((ROOT / relative).read_text())
        model_root = Path(manifest["artifact_root"])
        for file_row in manifest["files"]:
            if file_row["path"] in {"openvino_model.bin", "openvino_model.xml"}:
                expected[(model_root / file_row["path"]).resolve()] = file_row["sha256"]

    def controlled_sha256(path):
        source = Path(path).resolve()
        if source in expected:
            if calls is not None:
                calls.append(source)
            return expected[source]
        return sha256_file(source)

    monkeypatch.setattr(format_boundary, "sha256_file", controlled_sha256, raising=False)


@pytest.fixture
def fake_config(tmp_path):
    from scripts.testing.official_openvino.format_boundary import BoundaryCampaignConfig

    return BoundaryCampaignConfig(
        repository_root=ROOT,
        campaign_root=tmp_path / "campaign",
        manifest_path=FIXTURE_MATRIX,
        prompt_set_path=V2_PROMPT_SET,
        rubric_path=ROOT / "experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json",
    )


@pytest.fixture
def projected_inputs(tmp_path, monkeypatch):
    from scripts.testing.official_openvino.format_boundary import (
        project_boundary_evidence_inputs,
    )

    _stub_committed_model_hashes(monkeypatch)
    build_root = _controlled_build_root(tmp_path)
    return project_boundary_evidence_inputs(
        repository_root=ROOT,
        campaign_root=tmp_path / "campaign",
        build_root=build_root,
        manifest_path=FIXTURE_MATRIX,
        comparison_matrix_path=ADAPTIVE_MATRIX,
    )


def test_projection_rejects_non_executable_build_before_emitting_inputs(tmp_path):
    from scripts.testing.official_openvino.format_boundary import (
        project_boundary_evidence_inputs,
    )

    build_root = tmp_path / "empty-build"
    build_root.mkdir()
    campaign_root = tmp_path / "campaign"
    with pytest.raises(ValueError, match="OpenVINO GenAI package"):
        project_boundary_evidence_inputs(
            repository_root=ROOT,
            campaign_root=campaign_root,
            build_root=build_root,
            manifest_path=FIXTURE_MATRIX,
            comparison_matrix_path=ADAPTIVE_MATRIX,
        )
    assert not (campaign_root / "execution-inputs").exists()


@pytest.mark.parametrize(
    "relationship",
    ("equal", "campaign-inside-build", "build-inside-campaign"),
)
def test_projection_rejects_campaign_build_overlap_before_emitting_inputs(
    tmp_path, monkeypatch, relationship,
):
    from scripts.testing.official_openvino.format_boundary import (
        project_boundary_evidence_inputs,
    )

    _stub_committed_model_hashes(monkeypatch)
    if relationship == "equal":
        build_root = _controlled_build_root(tmp_path)
        campaign_root = build_root
    elif relationship == "campaign-inside-build":
        build_root = _controlled_build_root(tmp_path)
        campaign_root = build_root / "campaign"
    else:
        campaign_root = tmp_path / "campaign"
        build_root = _controlled_build_root(campaign_root)
    with pytest.raises(ValueError, match="campaign root must not overlap build root"):
        project_boundary_evidence_inputs(
            repository_root=ROOT,
            campaign_root=campaign_root,
            build_root=build_root,
            manifest_path=FIXTURE_MATRIX,
            comparison_matrix_path=ADAPTIVE_MATRIX,
        )
    assert not (campaign_root / "execution-inputs").exists()


def test_projection_binds_executable_build_and_detects_same_size_drift(
    tmp_path, monkeypatch,
):
    from scripts.testing.official_openvino.artifact_inventory import sha256_file
    from scripts.testing.official_openvino.format_boundary import (
        project_boundary_evidence_inputs,
    )

    _stub_committed_model_hashes(monkeypatch)
    build_root = _controlled_build_root(tmp_path)
    kwargs = {
        "repository_root": ROOT,
        "campaign_root": tmp_path / "campaign",
        "build_root": build_root,
        "manifest_path": FIXTURE_MATRIX,
        "comparison_matrix_path": ADAPTIVE_MATRIX,
    }
    projection = project_boundary_evidence_inputs(**kwargs)
    index = json.loads(projection.projection_index.path.read_text())
    package = build_root / "openvino_genai"
    provenance = ROOT / index["build_identity"]["path"]
    assert index["build"]["root"] == str(build_root.resolve())
    assert index["build"]["python_module"] == {
        "path": str((package / "py_openvino_genai.pyd").resolve()),
        "sha256": sha256_file(package / "py_openvino_genai.pyd"),
    }
    assert index["build"]["runtime_dll"] == {
        "path": str((package / "openvino_genai.dll").resolve()),
        "sha256": sha256_file(package / "openvino_genai.dll"),
    }
    assert index["build"]["provenance"] == {
        "path": str(provenance.resolve()),
        "sha256": sha256_file(provenance),
    }
    (package / "py_openvino_genai.pyd").write_bytes(b"module-b")
    with pytest.raises(ValueError, match="immutable projection drift"):
        project_boundary_evidence_inputs(**kwargs)


def test_model_binding_hashes_bytes_and_rejects_same_size_tampering(tmp_path):
    from scripts.testing.official_openvino.format_boundary import (
        _model_file_bindings,
    )

    model_root = tmp_path / "model"
    model_root.mkdir()
    contents = {
        "openvino_model.bin": b"binary-a",
        "openvino_model.xml": b"<model-a/>",
    }
    files = []
    for name, content in contents.items():
        (model_root / name).write_bytes(content)
        files.append({
            "path": name,
            "size_bytes": len(content),
            "sha256": hashlib.sha256(content).hexdigest(),
        })
    manifest = {"files": files}
    assert _model_file_bindings(manifest, model_root) == sorted(
        files, key=lambda row: row["path"]
    )
    (model_root / "openvino_model.bin").write_bytes(b"binary-b")
    with pytest.raises(ValueError, match="hash drift"):
        _model_file_bindings(manifest, model_root)


def test_projection_hashes_each_shared_model_file_once(tmp_path, monkeypatch):
    from scripts.testing.official_openvino.format_boundary import (
        project_boundary_evidence_inputs,
    )

    calls = []
    _stub_committed_model_hashes(monkeypatch, calls)
    project_boundary_evidence_inputs(
        repository_root=ROOT,
        campaign_root=tmp_path / "campaign",
        build_root=_controlled_build_root(tmp_path),
        manifest_path=FIXTURE_MATRIX,
        comparison_matrix_path=ADAPTIVE_MATRIX,
    )
    assert len(calls) == 4
    assert len(set(calls)) == 4


def test_manifest_rejects_null_artifact_for_executable_format(tmp_path):
    from scripts.testing.official_openvino.format_boundary import (
        load_boundary_manifest,
    )

    payload = json.loads(FIXTURE_MATRIX.read_text())
    payload["cases"][0]["artifact_manifest_path"] = None
    invalid = tmp_path / "format-boundary.json"
    invalid.write_text(json.dumps(payload), encoding="utf-8")
    with pytest.raises(ValueError, match="only the three F16"):
        load_boundary_manifest(invalid)


def test_projection_matrix_loads_in_boundary_order_with_independent_gpu_control(
    projected_inputs,
):
    from scripts.testing.official_openvino.matrix import load_matrix

    cases = load_matrix(projected_inputs.comparison_matrix.path)
    assert [case.test_id for case in cases] == [
        "cpu-u4-tbq3", "cpu-u4-tbq4", "cpu-u4-standard",
        "cpu-u8-tbq3", "cpu-u8-tbq4", "cpu-u8-standard",
        "cpu-f16-tbq3", "cpu-f16-tbq4", "cpu-f16-standard",
        "gpu-u4-standard-control",
    ]
    assert all(case.contexts == (512,) for case in cases)
    matrix = json.loads(projected_inputs.comparison_matrix.path.read_text())
    authoritative = json.loads(ADAPTIVE_MATRIX.read_text())
    assert matrix["source_identity"] == authoritative["source_identity"]
    assert matrix["build_identity"] == authoritative["build_identity"]
    gpu = matrix["cases"][-1]
    assert gpu["device"] == "gpu"
    assert gpu["requested_device"] == "GPU"
    gpu_spec = json.loads(
        next(
            item.runtime_spec.path.read_text()
            for item in projected_inputs.runtime_specs
            if item.case_internal_id == "gpu-u4-standard-control"
        )
    )
    assert gpu_spec["properties"] == {
        "ATTENTION_BACKEND": "SDPA",
        "CACHE_DIR": str(
            projected_inputs.campaign_root
            / "cache"
            / "gpu-u4-standard-control"
            / "512"
        ),
        "KEY_CACHE_PRECISION": "f16",
        "PERFORMANCE_HINT": "LATENCY",
        "VALUE_CACHE_PRECISION": "f16",
    }
    assert "NUM_STREAMS" not in gpu_spec["properties"]


def test_executable_specs_pass_real_sequence_projection_and_matrix_reconciliation(
    projected_inputs,
):
    from scripts.testing.measure_official_openvino import (
        _matrix_case,
        _sequence_spec,
        validate_worker_spec_against_matrix_case,
    )
    from scripts.testing.official_openvino.workload import build_context_workload

    assert len(projected_inputs.runtime_specs) == 7
    for item in projected_inputs.runtime_specs:
        raw = json.loads(item.runtime_spec.path.read_text())
        projected = _sequence_spec(item.runtime_spec.path)
        matrix = _matrix_case(
            projected_inputs.comparison_matrix.path,
            item.case_internal_id,
            512,
        )
        validate_worker_spec_against_matrix_case(projected, matrix["case"])
        assert raw["workload"] == {
            **build_context_workload(512),
            "actual_input_tokens": 512,
        }
        assert (
            raw["max_new_tokens"], raw["ignore_eos"], raw["seed"],
            raw["apply_chat_template"], raw["device"],
        ) == (4, True, 42, False, matrix["case"]["requested_device"])
        if raw["device"] == "CPU":
            assert raw["properties"]["INFERENCE_NUM_THREADS"] == 1
            assert raw["properties"]["NUM_STREAMS"] == 1


def test_runtime_artifact_bindings_come_from_committed_manifests_and_models(
    projected_inputs,
):
    index = json.loads(projected_inputs.projection_index.path.read_text())
    indexed = {row["case_internal_id"]: row for row in index["runtime_specs"]}
    for item in projected_inputs.runtime_specs:
        raw = json.loads(item.runtime_spec.path.read_text())
        manifest_path = Path(raw["artifact_manifest_path"])
        manifest = json.loads(manifest_path.read_text())
        assert raw["artifact_id"] == manifest["artifact_id"]
        assert raw["artifact_manifest_sha256"] == hashlib.sha256(
            manifest_path.read_bytes()
        ).hexdigest()
        assert raw["model_path"] == manifest["artifact_root"]
        assert Path(raw["model_path"]).is_dir()
        binding = indexed[item.case_internal_id]["artifact_binding"]
        assert binding["artifact_inventory_sha256"] == manifest["inventory_sha256"]
        assert {row["path"] for row in binding["model_files"]} == {
            "openvino_model.bin", "openvino_model.xml",
        }
        for model_file in binding["model_files"]:
            assert len(model_file["sha256"]) == 64
            assert (Path(raw["model_path"]) / model_file["path"]).is_file()


def test_f16_null_artifacts_become_terminal_prerequisites_without_specs(
    projected_inputs,
):
    assert {item.case_internal_id for item in projected_inputs.terminal_prerequisites} == {
        "cpu-f16-tbq3", "cpu-f16-tbq4", "cpu-f16-standard",
    }
    assert not {
        item.case_internal_id for item in projected_inputs.runtime_specs
    } & {item.case_internal_id for item in projected_inputs.terminal_prerequisites}
    for item in projected_inputs.terminal_prerequisites:
        descriptor = json.loads(item.descriptor.path.read_text())
        assert descriptor["reason"] == "artifact-unavailable"
        assert descriptor["role"] == "terminal-prerequisite"
        assert descriptor["case"]["lane"] == "cpu"
        assert descriptor["boundary_manifest"]["sha256"] == hashlib.sha256(
            FIXTURE_MATRIX.read_bytes()
        ).hexdigest()


@pytest.mark.parametrize(
    "tamper_target",
    ("runtime-spec", "comparison-matrix", "terminal-descriptor", "projection-index"),
)
def test_projection_is_byte_stable_and_rejects_tampering(
    tmp_path, monkeypatch, tamper_target,
):
    from scripts.testing.official_openvino.format_boundary import (
        project_boundary_evidence_inputs,
    )

    _stub_committed_model_hashes(monkeypatch)
    build_root = _controlled_build_root(tmp_path)
    kwargs = {
        "repository_root": ROOT,
        "campaign_root": tmp_path / "campaign",
        "build_root": build_root,
        "manifest_path": FIXTURE_MATRIX,
        "comparison_matrix_path": ADAPTIVE_MATRIX,
    }
    first = project_boundary_evidence_inputs(**kwargs)
    emitted = [
        first.comparison_matrix.path,
        *(item.runtime_spec.path for item in first.runtime_specs),
        *(item.descriptor.path for item in first.terminal_prerequisites),
        first.projection_index.path,
    ]
    before = {path: path.read_bytes() for path in emitted}
    second = project_boundary_evidence_inputs(**kwargs)
    assert first == second
    assert before == {path: path.read_bytes() for path in emitted}
    target = {
        "runtime-spec": first.runtime_specs[0].runtime_spec.path,
        "comparison-matrix": first.comparison_matrix.path,
        "terminal-descriptor": first.terminal_prerequisites[0].descriptor.path,
        "projection-index": first.projection_index.path,
    }[tamper_target]
    target.write_bytes(target.read_bytes() + b" ")
    with pytest.raises(ValueError, match="immutable projection drift"):
        project_boundary_evidence_inputs(**kwargs)


def test_projection_creates_executable_paths_without_historical_spec_tree(
    projected_inputs,
):
    assert projected_inputs.campaign_root.name == "campaign"
    assert all(
        item.runtime_spec.path.is_relative_to(projected_inputs.campaign_root)
        and item.runtime_spec.path.is_file()
        for item in projected_inputs.runtime_specs
    )
    index = json.loads(projected_inputs.projection_index.path.read_text())
    assert index["roots"] == {
        "build": str(projected_inputs.build_root),
        "campaign": str(projected_inputs.campaign_root),
        "repository": str(projected_inputs.repository_root),
    }
    assert "quality-recovery" not in projected_inputs.projection_index.path.read_text()


def _accepted_runtime(case, **kwargs):
    return {
        "accepted": True,
        "accepted_sample_count": 3,
        "pilot_passed": True,
        "warmup_excluded": True,
        "cleanup_process_count": 0,
        "measurement_summary_path": "measurement-summary.json",
        "measurement_summary_sha256": "a" * 64,
        "case": case.internal_id,
    }


def _passing_quality(*args, **kwargs):
    return {
        "status": "passed",
        "completed_prompt_ids": ["P1", "P2", "P3", "P4", "P5", "P6"],
        "prompt_receipt_count": 6,
        "cleanup_process_count": 0,
    }


def test_second_matching_cpu_failure_stops_higher_formats(fake_config):
    from scripts.testing.official_openvino.format_boundary import RowFailure, run_boundary_campaign

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
    assert state["cpu_lane"]["skipped_count"] == 8


def test_gpu_control_failure_does_not_close_cpu_lane(fake_config):
    from scripts.testing.official_openvino.format_boundary import RowFailure, run_boundary_campaign

    def lane_aware_runner(case, **kwargs):
        if case.lane == "gpu-control":
            raise RowFailure("gpu_control_failed", hard=False)
        return _accepted_runtime(case)

    state = run_boundary_campaign(
        fake_config, run_measurement=lane_aware_runner,
        run_quality=_passing_quality,
        available_ram=lambda: 8 * 1024**3,
    )
    assert state["gpu_lane"]["status"] == "stopped"
    assert state["cpu_lane"]["accepted_count"] > 3


def test_hard_ram_breach_is_not_retried(fake_config):
    from scripts.testing.official_openvino.format_boundary import RowFailure, run_boundary_campaign

    calls = []

    def hard_failure(case, **kwargs):
        calls.append(case.internal_id)
        raise RowFailure("emergency_minimum_available_ram", hard=True)

    state = run_boundary_campaign(
        fake_config, run_measurement=hard_failure, run_quality=_passing_quality,
        available_ram=lambda: 8 * 1024**3,
    )
    assert calls == ["cpu-u4-tbq3"]
    assert state["cpu_lane"]["attempt_count"] == 1
    terminal = json.loads((fake_config.campaign_root / "cpu" / "terminal-boundary.json").read_text())
    assert terminal["reason_code"] == "emergency_minimum_available_ram"
    assert terminal["envelope"] == "outside this laptop's configured safe RAM/time envelope"


def test_quality_only_runs_after_runtime_acceptance_and_atomic_state(fake_config):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    quality_calls = []

    def quality(case, runtime, **kwargs):
        quality_calls.append((case.internal_id, runtime["accepted_sample_count"]))
        assert (fake_config.campaign_root / "campaign-state.json").is_file()
        return _passing_quality()

    state = run_boundary_campaign(
        fake_config, run_measurement=_accepted_runtime, run_quality=quality,
        available_ram=lambda: 8 * 1024**3,
    )
    assert len(quality_calls) == 10
    assert state["cpu_lane"]["accepted_count"] == 9
    assert not list(fake_config.campaign_root.glob("*.tmp"))
    accepted = fake_config.campaign_root / "cpu" / "cpu-u4-tbq3" / "accepted-row.json"
    assert json.loads(accepted.read_text())["runtime"]["accepted_sample_count"] == 3


def test_resume_revalidates_receipts_and_never_reruns_accepted_work(fake_config):
    from dataclasses import replace
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    initial_calls = []

    def initial(case, **kwargs):
        initial_calls.append(case.internal_id)
        return _accepted_runtime(case)

    run_boundary_campaign(
        fake_config, run_measurement=initial, run_quality=_passing_quality,
        available_ram=lambda: 8 * 1024**3,
    )
    resumed_calls = []
    state = run_boundary_campaign(
        replace(fake_config, resume=True),
        run_measurement=lambda case, **kwargs: resumed_calls.append(case.internal_id),
        run_quality=lambda *a, **k: pytest.fail("accepted quality must not rerun"),
        available_ram=lambda: 8 * 1024**3,
    )
    assert len(initial_calls) == 10
    assert resumed_calls == []
    assert state["cpu_lane"]["accepted_count"] == 9


def test_manifest_has_exact_low_to_high_cpu_order_and_separate_gpu_lane(tmp_path):
    from scripts.testing.official_openvino.format_boundary import (
        load_boundary_manifest,
    )

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
    from scripts.testing.official_openvino.format_boundary import (
        build_boundary_worker_spec,
        load_boundary_manifest,
    )

    case = load_boundary_manifest(FIXTURE_MATRIX).cpu_cases[0]
    spec = build_boundary_worker_spec(case, role="pilot", cache_dir=tmp_path)
    assert spec["device"] == "CPU"
    assert spec["context"] == 512
    assert spec["properties"]["TURBOQUANT_KEY_ALGORITHM"] == "TBQ3"
    assert spec["properties"]["TURBOQUANT_VALUE_ALGORITHM"] == "TBQ3"
    assert spec["properties"]["TURBOQUANT_NORM_CORRECTION"] is True
    assert spec["properties"]["INFERENCE_NUM_THREADS"] == 1
    assert spec["properties"]["NUM_STREAMS"] == 1


def test_quality_contract_allow_lists_frozen_v1_and_compact_v2():
    from scripts.testing.official_openvino.quality_contracts import (
        load_quality_contract,
    )

    assert hashlib.sha256(V1_PROMPT_SET.read_bytes()).hexdigest() == FROZEN_V1_SHA256
    v1 = load_quality_contract(V1_PROMPT_SET)
    v2 = load_quality_contract(V2_PROMPT_SET)
    assert v1.prompt_set_id == "GTQ-PROMPTS-v1"
    assert v1.maximum_input_tokens is None
    assert v2.prompt_set_id == "GTQ-PROMPTS-v2"
    assert v2.rendered_root == PROMPT_ROOT / "rendered-v2"
    assert v2.maximum_input_tokens == 512


def test_compact_prompt_contract_rejects_substituted_rendered_root(tmp_path):
    from scripts.testing.run_official_openvino_quality import load_prompt_contract

    with pytest.raises(ValueError, match="rendered root"):
        load_prompt_contract(V2_PROMPT_SET, PROMPT_ROOT / "rendered")


def test_v2_is_accepted_by_quality_and_both_adjudicator_prompt_controls():
    from scripts.testing.adjudicate_official_openvino_adaptive_quality import (
        _prompt_controls as adaptive_prompt_controls,
    )
    from scripts.testing.adjudicate_official_openvino_quality import _prompt_controls
    from scripts.testing.official_openvino.quality import validate_response_record
    from scripts.testing.official_openvino.quality_contracts import (
        load_quality_contract,
    )

    contract = load_quality_contract(V2_PROMPT_SET)
    output = "MARKER:IXN-TQ-7319"
    record = {
        "schema_version": 1,
        "status": "complete",
        "test_id": "boundary-u4-tbq3",
        "context_tokens": 512,
        "prompt_id": "P5",
        "prompt_set_id": contract.prompt_set_id,
        "prompt_set_sha256": contract.prompt_set_sha256,
        "prompt_sha256": "a" * 64,
        "rubric_id": "GTQ-QUALITY-RUBRIC-v1",
        "runtime_summary_sha256": "b" * 64,
        "runtime_config_sha256": "c" * 64,
        "generation_settings": {
            "temperature": 0.0, "top_p": 1.0, "seed": 42,
            "max_output_tokens": 256,
        },
        "output": output,
        "output_sha256": hashlib.sha256(output.encode("utf-8")).hexdigest(),
    }
    assert validate_response_record(
        record,
        expected_runtime={
            "test_id": "boundary-u4-tbq3", "context_tokens": 512,
            "runtime_summary_sha256": "b" * 64,
            "runtime_config_sha256": "c" * 64,
        },
        expected_prompt_set_sha256=contract.prompt_set_sha256,
        expected_prompt_sha256="a" * 64,
    )["prompt_set_id"] == "GTQ-PROMPTS-v2"
    assert _prompt_controls(V2_PROMPT_SET)["P5"]["exact_output"] == output
    assert adaptive_prompt_controls(V2_PROMPT_SET)["P5"]["exact_output"] == output


class _FakeGraniteTokenizer:
    def __init__(self, token_count):
        self.token_count = token_count
        self.seen = []

    def encode(self, text):
        self.seen.append(text)
        return list(range(self.token_count))


def test_compact_prompt_uses_injected_tokenizer_and_persists_observed_count():
    from scripts.testing.run_official_openvino_quality import load_prompt_contract

    tokenizer = _FakeGraniteTokenizer(357)
    contract = load_prompt_contract(
        V2_PROMPT_SET,
        PROMPT_ROOT / "rendered-v2",
        tokenizer_loader=lambda path: tokenizer,
    )
    assert tokenizer.seen == [contract["prompts"]["P5"]["execution"]["prompt"]]
    assert contract["observed_input_tokens"]["P5"] == 357


def test_compact_prompt_rejects_513_tokens_before_generation():
    from scripts.testing.run_official_openvino_quality import load_prompt_contract

    with pytest.raises(ValueError, match="maximum input tokens"):
        load_prompt_contract(
            V2_PROMPT_SET,
            PROMPT_ROOT / "rendered-v2",
            tokenizer_loader=lambda path: _FakeGraniteTokenizer(513),
        )


def test_registry_binds_prompt_set_id_to_its_exact_registered_hash():
    from scripts.testing.official_openvino.quality_contracts import (
        require_quality_contract_identity,
    )

    v1 = require_quality_contract_identity("GTQ-PROMPTS-v1", FROZEN_V1_SHA256)
    v2 = require_quality_contract_identity(
        "GTQ-PROMPTS-v2",
        hashlib.sha256(V2_PROMPT_SET.read_bytes()).hexdigest(),
    )
    assert v1.prompt_set_id == "GTQ-PROMPTS-v1"
    assert v2.prompt_set_id == "GTQ-PROMPTS-v2"
    with pytest.raises(ValueError, match="hash"):
        require_quality_contract_identity("GTQ-PROMPTS-v1", v2.prompt_set_sha256)


def _temporary_v2_contract_with_p5_count(tmp_path, monkeypatch, token_count):
    from dataclasses import replace

    from scripts.testing.official_openvino.quality_contracts import (
        QUALITY_CONTRACTS,
    )

    payload = json.loads(V2_PROMPT_SET.read_text(encoding="utf-8"))
    payload["rendered_asset_manifest"]["P5_input_tokens"] = token_count
    path = tmp_path / "compact-feasibility-prompt-set-v2.json"
    fixture = tmp_path / "fixtures" / "P5-compact-context-v2.txt"
    fixture.parent.mkdir()
    fixture.write_bytes(
        (PROMPT_ROOT / "fixtures" / "P5-compact-context-v2.txt").read_bytes()
    )
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    raw = path.read_bytes()
    monkeypatch.setitem(
        QUALITY_CONTRACTS,
        "GTQ-PROMPTS-v2",
        replace(
            QUALITY_CONTRACTS["GTQ-PROMPTS-v2"],
            prompt_set_sha256=hashlib.sha256(raw).hexdigest(),
        ),
    )
    return path


def test_compact_prompt_accepts_exactly_512_tokens(tmp_path, monkeypatch):
    from scripts.testing.run_official_openvino_quality import load_prompt_contract

    contract = load_prompt_contract(
        _temporary_v2_contract_with_p5_count(tmp_path, monkeypatch, 512),
        PROMPT_ROOT / "rendered-v2",
        tokenizer_loader=lambda path: _FakeGraniteTokenizer(512),
    )
    assert contract["observed_input_tokens"] == {"P5": 512}


def test_compact_prompt_rejects_observed_count_that_differs_from_signed_manifest():
    from scripts.testing.run_official_openvino_quality import load_prompt_contract

    with pytest.raises(ValueError, match="observed token count"):
        load_prompt_contract(
            V2_PROMPT_SET,
            PROMPT_ROOT / "rendered-v2",
            tokenizer_loader=lambda path: _FakeGraniteTokenizer(512),
        )


def test_v1_prompt_contract_keeps_its_prior_shape_without_tokenizer_loading():
    from scripts.testing.run_official_openvino_quality import load_prompt_contract

    def forbidden_tokenizer_loader(path):
        pytest.fail(f"v1 must not load a tokenizer: {path}")

    contract = load_prompt_contract(
        V1_PROMPT_SET,
        PROMPT_ROOT / "rendered",
        tokenizer_loader=forbidden_tokenizer_loader,
    )
    assert set(contract) == {
        "prompt_set_id", "prompt_set_sha256", "generation_settings", "prompts",
    }
    assert contract["prompt_set_id"] == "GTQ-PROMPTS-v1"
    assert contract["prompt_set_sha256"] == FROZEN_V1_SHA256


def test_both_adjudicators_bind_v2_scoring_input_identity_to_registry_hash():
    from scripts.testing import adjudicate_official_openvino_adaptive_quality as adaptive
    from scripts.testing import adjudicate_official_openvino_quality as standard
    from scripts.testing.official_openvino.quality_contracts import (
        load_quality_contract,
    )

    v2 = load_quality_contract(V2_PROMPT_SET)
    assert standard._scoring_contract(v2.prompt_set_id, v2.prompt_set_sha256) == v2
    assert adaptive._scoring_contract(v2.prompt_set_id, v2.prompt_set_sha256) == v2
    with pytest.raises(ValueError, match="hash"):
        standard._scoring_contract("GTQ-PROMPTS-v1", v2.prompt_set_sha256)
    with pytest.raises(ValueError, match="hash"):
        adaptive._scoring_contract("GTQ-PROMPTS-v1", v2.prompt_set_sha256)
