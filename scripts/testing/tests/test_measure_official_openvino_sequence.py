import hashlib
import json
import statistics
import sys
from pathlib import Path

import pytest

from scripts.testing.measure_official_openvino import (
    CampaignLock,
    build_campaign_identity,
    run_measurement_sequence,
)


MIB = 1024**2
ROLES = ("pilot", "warmup", "sample-1", "sample-2", "sample-3")


def _write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def _setup_campaign(tmp_path: Path) -> dict:
    build = tmp_path / "build"
    package = build / "openvino_genai"
    package.mkdir(parents=True)
    (package / "__init__.py").write_text("", encoding="utf-8")
    (package / "py_openvino_genai.cp313-win_amd64.pyd").write_bytes(b"pyd")
    (package / "openvino_genai.dll").write_bytes(b"dll")
    repo = tmp_path / "repo"
    repo.mkdir()
    runtime_source = (
        repo
        / "scripts"
        / "testing"
        / "official_openvino"
        / "measurement_worker.py"
    )
    runtime_source.parent.mkdir(parents=True)
    runtime_source.write_text("RUNTIME = 1\n", encoding="utf-8")
    site_packages = tmp_path / "site-packages"
    (site_packages / "openvino").mkdir(parents=True)
    (site_packages / "openvino" / "__init__.py").write_text(
        "__version__ = 'test'\n",
        encoding="utf-8",
    )
    libraries = tmp_path / "openvino-libs"
    libraries.mkdir()
    (libraries / "openvino.dll").write_bytes(b"openvino")
    model = tmp_path / "model"
    model.mkdir()
    (model / "openvino_model.xml").write_text("<model/>", encoding="utf-8")
    (model / "openvino_model.bin").write_bytes(b"weights")
    sampler = tmp_path / "sampler.ps1"
    sampler.write_text("# sampler\n", encoding="utf-8")
    build_provenance = tmp_path / "build-provenance.json"
    _write_json(
        build_provenance,
        {
            "schema": "openvino-turboquant-build-provenance/v1",
            "status": "passed",
            "source": {"patch_commit": "a" * 40},
        },
    )
    matrix = tmp_path / "matrix.json"
    _write_json(
        matrix,
        {
            "schema_version": 1,
            "cases": [
                {
                    "test_id": "OV-TQ-03",
                    "phase": "formal",
                    "model": "granite-3b",
                    "weight_precision": "u8",
                    "device": "cpu",
                    "contexts": [4096],
                    "k_algorithm": "tbq4",
                    "v_algorithm": "tbq4",
                    "k_precision": "u4",
                    "v_precision": "u4",
                }
            ],
        },
    )
    spec = tmp_path / "spec.json"
    _write_json(
        spec,
        {
            "schema": "official-openvino-wb04-worker-spec/v1",
            "role": "pilot",
            "controlled_test_id": "OV-TQ-03",
            "context": 4096,
            "model_path": str(model),
            "device": "CPU",
            "prompt": "Reply with one word.",
            "max_new_tokens": 4,
            "expected_input_tokens": 4096,
            "ignore_eos": True,
            "seed": 42,
            "apply_chat_template": False,
            "properties": {
                "ATTENTION_BACKEND": "SDPA",
                "TURBOQUANT_KEY_ALGORITHM": "TBQ4",
                "TURBOQUANT_VALUE_ALGORITHM": "TBQ4",
                "TURBOQUANT_NORM_CORRECTION": True,
            },
        },
    )
    return {
        "spec_path": spec,
        "campaign_root": tmp_path / "campaign",
        "matrix_path": matrix,
        "build_provenance_path": build_provenance,
        "build_root": build,
        "repo_root": repo,
        "python_executable": Path(sys.executable),
        "python_site_packages": site_packages,
        "openvino_libraries": libraries,
        "sampler_script": sampler,
        "timeout_seconds": 60,
        "minimum_available_ram_mib": 2048,
    }


def _identity_kwargs(kwargs: dict) -> dict:
    return {
        field: kwargs[field]
        for field in (
            "spec_path",
            "matrix_path",
            "build_provenance_path",
            "build_root",
            "repo_root",
            "python_executable",
            "python_site_packages",
            "openvino_libraries",
        )
    }


def _utilization(values: list[float]) -> dict:
    return {
        "values": values,
        "mean": statistics.fmean(values),
        "median": statistics.median(values),
        "peak": max(values),
        "count": len(values),
        "query_succeeded": True,
    }


def _record(role: str, ordinal: int, spec: dict) -> dict:
    payload = 96 * MIB
    norm = 2 * MIB
    metadata = 2 * MIB
    actual = payload + norm + metadata
    return {
        "role": role,
        "valid": True,
        "cleanup_process_count": 0,
        "peak_working_set_bytes": (1000 + ordinal) * MIB,
        "peak_private_bytes": (900 + ordinal) * MIB,
        "available_ram_bytes": {
            "before": 3500 * MIB,
            "minimum": 3000 * MIB,
            "after": 3300 * MIB,
        },
        "gpu_dedicated_memory_peak_mb": 48 + ordinal,
        "gpu_shared_memory_peak_mb": 16 + ordinal,
        "gpu_memory_peak_mb": 64 + (2 * ordinal),
        "cpu_percent": _utilization([40 + ordinal, 42 + ordinal]),
        "gpu_percent": _utilization([20 + ordinal, 22 + ordinal]),
        "worker": {
            "load_ms": 100 + ordinal,
            "ttft_ms": 50 + ordinal,
            "prompt_tps": 20 + ordinal,
            "tpot_ms": 25 + ordinal,
            "decode_tps": 40 + ordinal,
            "generation_duration_ms": 500 + ordinal,
            "output_valid": True,
            "model_path": str(Path(spec["model_path"]).resolve()),
            "device": spec["device"],
            "controlled_test_id": spec["controlled_test_id"],
            "context": spec["context"],
            "expected_input_tokens": spec["expected_input_tokens"],
            "role": role,
        },
        "activation": {
            "status": "activated",
            "requested_key_algorithm": "TBQ4",
            "requested_value_algorithm": "TBQ4",
            "activated_key_algorithm": "TBQ4",
            "activated_value_algorithm": "TBQ4",
            "requested_key_cache_precision": "u4",
            "requested_value_cache_precision": "u4",
            "activated_key_cache_precision": "u4",
            "activated_value_cache_precision": "u4",
            "observed_key_state_precision": "u8+f32+i32",
            "observed_value_state_precision": "u8+f32+i32",
            "norm_correction": True,
            "attention_path": "stateful_sdpa_reference_codec",
            "device": "CPU",
            "actual_device": "CPU",
            "fallback": False,
            "expected_bytes": actual,
            "actual_bytes": actual,
            "expected_persistent_standard_bytes": 0,
            "actual_persistent_standard_bytes": 0,
            "expected_persistent_payload_bytes": payload,
            "actual_persistent_payload_bytes": payload,
            "expected_persistent_norm_bytes": norm,
            "actual_persistent_norm_bytes": norm,
            "expected_persistent_metadata_bytes": metadata,
            "actual_persistent_metadata_bytes": metadata,
            "decoded_scratch_bytes": 8 * MIB,
            "full_precision_equivalent_bytes": 800 * MIB,
            "operation_type": "TurboQuantStateUpdateDecode",
            "operation_count": 80,
            "matched_state_count": 80,
            "transformed_model_hash": "transformed-model-hash",
            "runtime_layer_type": "Reference",
            "build_commit": "a" * 40,
            "model_hash": "model-hash",
        },
        "output_sha256": hashlib.sha256(
            f"output-{role}".encode("utf-8")
        ).hexdigest(),
        "telemetry_sha256": hashlib.sha256(b"telemetry").hexdigest(),
    }


def test_sequence_runs_five_roles_once_and_atomically_summarizes_only_samples(
    tmp_path,
):
    kwargs = _setup_campaign(tmp_path)
    calls: list[tuple[str, Path]] = []

    def fake_measurement(**run_kwargs):
        role = run_kwargs["role"]
        output = run_kwargs["output_dir"]
        calls.append((role, output))
        assert json.loads(
            run_kwargs["spec_path"].read_text(encoding="utf-8")
        )["role"] == role
        ordinal = ROLES.index(role)
        spec = json.loads(
            run_kwargs["spec_path"].read_text(encoding="utf-8")
        )
        record = _record(role, ordinal, spec)
        _write_json(output / "attempt.json", record)
        return record

    first = run_measurement_sequence(
        **kwargs,
        run_measurement=fake_measurement,
    )
    first_attempt_bytes = (
        kwargs["campaign_root"]
        / "attempts"
        / "sample-1"
        / "attempt-001"
        / "run"
        / "attempt.json"
    ).read_bytes()
    second = run_measurement_sequence(
        **kwargs,
        run_measurement=fake_measurement,
    )

    assert [role for role, _ in calls] == list(ROLES)
    assert all(path.name == "run" for _, path in calls)
    assert first == second
    assert first["pilot_passed"] is True
    assert first["warmup_excluded"] is True
    assert first["accepted_sample_count"] == 3
    assert [row["role"] for row in first["accepted_samples"]] == [
        "sample-1",
        "sample-2",
        "sample-3",
    ]
    assert (
        kwargs["campaign_root"]
        / "attempts"
        / "warmup"
        / "attempt-001"
        / "sequence-receipt.json"
    ).is_file()
    assert (
        kwargs["campaign_root"]
        / "attempts"
        / "sample-1"
        / "attempt-001"
        / "run"
        / "attempt.json"
    ).read_bytes() == first_attempt_bytes

    metrics = json.loads(
        (kwargs["campaign_root"] / "measurement-summary.json").read_text(
            encoding="utf-8"
        )
    )
    assert metrics["status"] == "measured"
    assert metrics["sample_count"] == 3
    assert metrics["ttft_ms"]["mean"] == 53
    assert metrics["cpu_percent"]["mean"] == 44
    assert metrics["gpu_percent"]["mean"] == 24
    assert len(metrics["campaign_identity_sha256"]) == 64


def test_sequence_refuses_a_concurrent_controller_before_launching_a_role(
    tmp_path,
):
    kwargs = _setup_campaign(tmp_path)
    launches = 0

    def fake_measurement(**_):
        nonlocal launches
        launches += 1
        raise AssertionError("locked campaign must not launch")

    with CampaignLock(kwargs["campaign_root"]):
        with pytest.raises(RuntimeError, match="locked"):
            run_measurement_sequence(
                **kwargs,
                run_measurement=fake_measurement,
            )

    assert launches == 0


def test_invalid_attempt_is_retained_and_next_run_uses_a_new_attempt_number(
    tmp_path,
):
    kwargs = _setup_campaign(tmp_path)
    pilot_launches = 0

    def fail_first_pilot(**run_kwargs):
        nonlocal pilot_launches
        role = run_kwargs["role"]
        pilot_launches += 1
        assert role == "pilot"
        record = {
            "role": role,
            "valid": False,
            "cleanup_process_count": 0,
            "validation_errors": ["low memory stop"],
        }
        _write_json(run_kwargs["output_dir"] / "attempt.json", record)
        return record

    with pytest.raises(RuntimeError, match="pilot"):
        run_measurement_sequence(
            **kwargs,
            run_measurement=fail_first_pilot,
        )

    invalid_path = (
        kwargs["campaign_root"]
        / "attempts"
        / "pilot"
        / "attempt-001"
        / "run"
        / "attempt.json"
    )
    invalid_bytes = invalid_path.read_bytes()
    invalid_receipt = json.loads(
        (
            kwargs["campaign_root"]
            / "attempts"
            / "pilot"
            / "attempt-001"
            / "sequence-receipt.json"
        ).read_text(encoding="utf-8")
    )
    assert invalid_receipt["accepted"] is False

    def pass_measurement(**run_kwargs):
        role = run_kwargs["role"]
        ordinal = ROLES.index(role)
        spec = json.loads(
            run_kwargs["spec_path"].read_text(encoding="utf-8")
        )
        record = _record(role, ordinal, spec)
        _write_json(run_kwargs["output_dir"] / "attempt.json", record)
        return record

    result = run_measurement_sequence(
        **kwargs,
        run_measurement=pass_measurement,
    )

    assert pilot_launches == 1
    assert invalid_path.read_bytes() == invalid_bytes
    assert result["pilot"]["attempt_number"] == 2
    assert (
        kwargs["campaign_root"]
        / "attempts"
        / "pilot"
        / "attempt-002"
        / "run"
        / "attempt.json"
    ).is_file()


def test_controller_refuses_to_construct_a_missing_runtime_record(tmp_path):
    kwargs = _setup_campaign(tmp_path)

    def returns_without_persisting(**run_kwargs):
        spec = json.loads(
            run_kwargs["spec_path"].read_text(encoding="utf-8")
        )
        return _record(run_kwargs["role"], 0, spec)

    with pytest.raises(RuntimeError, match="pilot"):
        run_measurement_sequence(
            **kwargs,
            run_measurement=returns_without_persisting,
        )

    attempt = (
        kwargs["campaign_root"]
        / "attempts"
        / "pilot"
        / "attempt-001"
    )
    assert not (attempt / "run" / "attempt.json").exists()
    receipt = json.loads(
        (attempt / "sequence-receipt.json").read_text(encoding="utf-8")
    )
    assert receipt["accepted"] is False
    assert receipt["runtime_record_path"] is None
    assert receipt["runtime_record_sha256"] is None


def test_controller_rejects_runtime_record_from_a_different_spec(tmp_path):
    kwargs = _setup_campaign(tmp_path)

    def wrong_runtime_identity(**run_kwargs):
        role = run_kwargs["role"]
        spec = json.loads(
            run_kwargs["spec_path"].read_text(encoding="utf-8")
        )
        record = _record(role, ROLES.index(role), spec)
        record["worker"]["controlled_test_id"] = "OV-TQ-04"
        _write_json(run_kwargs["output_dir"] / "attempt.json", record)
        return record

    with pytest.raises(RuntimeError, match="pilot"):
        run_measurement_sequence(
            **kwargs,
            run_measurement=wrong_runtime_identity,
        )

    receipt = json.loads(
        (
            kwargs["campaign_root"]
            / "attempts"
            / "pilot"
            / "attempt-001"
            / "sequence-receipt.json"
        ).read_text(encoding="utf-8")
    )
    assert receipt["accepted"] is False


def test_resume_rejects_changed_identity_before_launching_more_work(tmp_path):
    kwargs = _setup_campaign(tmp_path)

    def pass_measurement(**run_kwargs):
        role = run_kwargs["role"]
        spec = json.loads(
            run_kwargs["spec_path"].read_text(encoding="utf-8")
        )
        record = _record(role, ROLES.index(role), spec)
        _write_json(run_kwargs["output_dir"] / "attempt.json", record)
        return record

    run_measurement_sequence(
        **kwargs,
        run_measurement=pass_measurement,
    )
    _mutate_json(
        kwargs["spec_path"],
        lambda value: value.update(prompt="Changed after the campaign."),
    )
    launches = 0

    def must_not_launch(**_):
        nonlocal launches
        launches += 1
        raise AssertionError("identity mismatch must stop before launch")

    with pytest.raises(RuntimeError, match="different canonical identity"):
        run_measurement_sequence(
            **kwargs,
            run_measurement=must_not_launch,
        )

    assert launches == 0


@pytest.mark.parametrize(
    ("domain", "mutate"),
    [
        (
            "config",
            lambda kwargs: _mutate_json(
                kwargs["spec_path"],
                lambda value: value.update(max_new_tokens=5),
            ),
        ),
        (
            "context",
            lambda kwargs: (
                _mutate_json(
                    kwargs["matrix_path"],
                    lambda value: value["cases"][0].update(
                        contexts=[2048, 4096]
                    ),
                ),
                _mutate_json(
                    kwargs["spec_path"],
                    lambda value: value.update(
                        context=2048,
                        expected_input_tokens=2048,
                    ),
                ),
            ),
        ),
        (
            "matrix",
            lambda kwargs: _mutate_json(
                kwargs["matrix_path"],
                lambda value: value["cases"][0].update(
                    description="changed matrix contract"
                ),
            ),
        ),
        (
            "build",
            lambda kwargs: _mutate_json(
                kwargs["build_provenance_path"],
                lambda value: value["source"].update(patch_commit="b" * 40),
            ),
        ),
        (
            "model",
            lambda kwargs: (
                Path(
                    json.loads(
                        kwargs["spec_path"].read_text(encoding="utf-8")
                    )["model_path"]
                )
                / "openvino_model.bin"
            ).write_bytes(b"different weights"),
        ),
        (
            "prompt",
            lambda kwargs: _mutate_json(
                kwargs["spec_path"],
                lambda value: value.update(prompt="Different prompt."),
            ),
        ),
        (
            "runtime",
            lambda kwargs: (
                kwargs["repo_root"]
                / "scripts"
                / "testing"
                / "official_openvino"
                / "measurement_worker.py"
            ).write_text("RUNTIME = 2\n", encoding="utf-8"),
        ),
    ],
)
def test_canonical_identity_changes_for_every_resume_boundary(
    tmp_path,
    domain,
    mutate,
):
    kwargs = _setup_campaign(tmp_path / domain)
    before = build_campaign_identity(**_identity_kwargs(kwargs))

    mutate(kwargs)
    after = build_campaign_identity(**_identity_kwargs(kwargs))

    assert before["campaign_identity_sha256"] != after[
        "campaign_identity_sha256"
    ]


def test_canonical_identity_ignores_runtime_generated_python_bytecode(tmp_path):
    kwargs = _setup_campaign(tmp_path)
    before = build_campaign_identity(**_identity_kwargs(kwargs))
    runtime_cache = (
        kwargs["repo_root"]
        / "scripts"
        / "testing"
        / "official_openvino"
        / "__pycache__"
        / "measurement_worker.cpython-313.pyc"
    )
    runtime_cache.parent.mkdir()
    runtime_cache.write_bytes(b"transient runtime cache")
    package_cache = (
        kwargs["python_site_packages"]
        / "openvino"
        / "__pycache__"
        / "__init__.cpython-313.pyc"
    )
    package_cache.parent.mkdir()
    package_cache.write_bytes(b"transient package cache")

    after = build_campaign_identity(**_identity_kwargs(kwargs))

    assert after["campaign_identity_sha256"] == before[
        "campaign_identity_sha256"
    ]


def _mutate_json(path: Path, mutate) -> None:
    value = json.loads(path.read_text(encoding="utf-8"))
    mutate(value)
    _write_json(path, value)
