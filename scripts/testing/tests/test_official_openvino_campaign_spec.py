import hashlib
import json
import subprocess
import sys
from pathlib import Path

import pytest

from scripts.testing.official_openvino.campaign_spec import (
    generate_formal_u8_granite3b_specs,
)
from scripts.testing.measure_official_openvino import (
    SEQUENCE_ROLES,
    _role_spec,
    _sequence_spec,
)


ROOT = Path(__file__).resolve().parents[3]
MATRIX = ROOT / "experiments/manifests/official-openvino/retest-matrix.json"
SCRIPT = ROOT / "scripts/testing/generate_official_openvino_specs.py"
WORKER_FIELDS = {
    "apply_chat_template",
    "context",
    "controlled_test_id",
    "device",
    "expected_input_tokens",
    "ignore_eos",
    "max_new_tokens",
    "model_path",
    "prompt",
    "properties",
    "role",
    "schema",
    "seed",
}


def _clean_inputs(tmp_path: Path) -> tuple[Path, Path, Path]:
    build = tmp_path / "clean build"
    package = build / "openvino_genai"
    package.mkdir(parents=True)
    (package / "__init__.py").write_text("", encoding="utf-8")
    (package / "py_openvino_genai.cp313-win_amd64.pyd").write_bytes(b"pyd")
    (package / "openvino_genai.dll").write_bytes(b"dll")
    model = tmp_path / "model path"
    model.mkdir()
    cache = tmp_path / "cache root"
    cache.mkdir()
    return build, model, cache


def _generate(tmp_path: Path, *, output_name: str = "spec output"):
    build, model, cache = _clean_inputs(tmp_path)
    output = tmp_path / output_name
    result = generate_formal_u8_granite3b_specs(
        matrix_path=MATRIX,
        build_root=build,
        model_path=model,
        cache_root=cache,
        output_root=output,
    )
    return result, build, model, cache, output


def _read_spec(output: Path, test_id: str, context: int) -> dict:
    return json.loads(
        (
            output
            / test_id
            / f"context-{context}"
            / "spec.json"
        ).read_text(encoding="utf-8")
    )


def test_generation_expands_every_runnable_context_and_records_gpu_rejection(
    tmp_path,
):
    result, build, model, _, output = _generate(tmp_path)

    spec_paths = sorted(output.glob("OV-TQ-*/context-*/spec.json"))
    assert len(spec_paths) == 20
    assert result["spec_count"] == 20
    assert result["expected_rejection_count"] == 1
    assert result["build_root"] == str(build.resolve())
    assert result["model_path"] == str(model.resolve())
    assert {path.parent.name for path in spec_paths} >= {
        "context-512",
        "context-2048",
        "context-4096",
        "context-8192",
    }
    assert not (output / "OV-TQ-18").exists()

    rejection_path = output / "expected-rejections.json"
    rejection_bytes = rejection_path.read_bytes()
    rejection = json.loads(rejection_bytes)
    assert rejection == {
        "matrix_sha256": hashlib.sha256(MATRIX.read_bytes()).hexdigest(),
        "rejections": [
            {
                    "execution_contract": {
                    "attention_path": "not-produced-by-expected-rejection",
                    "controlled_test_id": "OV-TQ-18",
                    "execution_route": "expected-rejection",
                    "expected_outcome": "expected-rejection",
                    "norm_correction": True,
                    "numeric_generation_metrics_expected": False,
                    "requires_actual_cache_precision_proof": False,
                    "runtime_key_algorithm": "TBQ4",
                    "runtime_value_algorithm": "TBQ4",
                    "suitable_host_required": False,
                },
                    "matrix_case": {
                    "attention_path": "not-produced-by-expected-rejection",
                    "contexts": [1024],
                    "description": "GPU TurboQuant gate",
                        "device": "gpu",
                    "execution_route": "expected-rejection",
                    "expected_outcome": "expected-rejection",
                    "guard": "none",
                    "k_algorithm": "tbq4",
                        "k_precision": "u4",
                    "key_cache_precision": "u4",
                    "model": "granite-3b",
                    "norm_correction": True,
                    "numeric_generation_metrics_expected": False,
                    "phase": "formal",
                        "quality_required": True,
                    "required_metrics": [
                        "available_ram_min_mb",
                        "cpu_percent",
                        "decode_tps",
                        "generation_duration_ms",
                        "gpu_memory_peak_mb",
                        "gpu_percent",
                        "kv_mb",
                        "load_ms",
                        "peak_private_mb",
                        "peak_working_set_mb",
                        "prompt_tps",
                        "tpot_ms",
                        "ttft_ms",
                    ],
                    "requested_device": "GPU",
                    "runtime_key_algorithm": "TBQ4",
                    "runtime_value_algorithm": "TBQ4",
                    "suitable_host_required": False,
                    "test_id": "OV-TQ-18",
                    "v_algorithm": "tbq4",
                        "v_precision": "u4",
                    "value_cache_precision": "u4",
                    "weight_precision": "u8",
                },
                "reason": (
                    "matrix execution contract declares expected-rejection; "
                    "worker spec generation is prohibited"
                ),
            }
        ],
        "schema": "official-openvino-wb04-expected-rejections/v1",
    }

    second_output = tmp_path / "second output"
    generate_formal_u8_granite3b_specs(
        matrix_path=MATRIX,
        build_root=build,
        model_path=model,
        cache_root=tmp_path / "cache root",
        output_root=second_output,
    )
    assert (
        second_output / "expected-rejections.json"
    ).read_bytes() == rejection_bytes


def test_worker_specs_bind_exact_matrix_runtime_contract(tmp_path):
    _, _, model, cache, output = _generate(tmp_path)

    scalar = _read_spec(output, "OV-TQ-01", 4096)
    assert set(scalar) == WORKER_FIELDS
    assert scalar == {
        "apply_chat_template": False,
        "context": 4096,
        "controlled_test_id": "OV-TQ-01",
        "device": "CPU",
        "expected_input_tokens": 4096,
        "ignore_eos": True,
        "max_new_tokens": 4,
        "model_path": str(model.resolve()),
        "prompt": " test" * 4096,
        "properties": {
            "ATTENTION_BACKEND": "SDPA",
            "CACHE_DIR": str(
                (cache / "OV-TQ-01" / "context-4096").resolve()
            ),
            "ENABLE_CPU_PINNING": False,
            "INFERENCE_NUM_THREADS": 1,
            "KEY_CACHE_PRECISION": "u8",
            "NUM_STREAMS": 1,
            "PERFORMANCE_HINT": "LATENCY",
            "VALUE_CACHE_PRECISION": "u8",
        },
        "role": "pilot",
        "schema": "official-openvino-wb04-worker-spec/v1",
        "seed": 42,
    }

    key_only = _read_spec(output, "OV-TQ-07", 4096)
    assert key_only["properties"]["TURBOQUANT_KEY_ALGORITHM"] == "TBQ4"
    assert key_only["properties"]["TURBOQUANT_VALUE_ALGORITHM"] == "STANDARD"
    assert key_only["properties"]["VALUE_CACHE_PRECISION"] == "f16"
    assert "KEY_CACHE_PRECISION" not in key_only["properties"]

    value_only = _read_spec(output, "OV-TQ-08", 4096)
    assert value_only["properties"]["TURBOQUANT_KEY_ALGORITHM"] == "STANDARD"
    assert value_only["properties"]["TURBOQUANT_VALUE_ALGORITHM"] == "TBQ4"
    assert value_only["properties"]["KEY_CACHE_PRECISION"] == "f16"
    assert "VALUE_CACHE_PRECISION" not in value_only["properties"]

    assert (
        _read_spec(output, "OV-TQ-03", 4096)["properties"][
            "TURBOQUANT_NORM_CORRECTION"
        ]
        is True
    )
    assert (
        _read_spec(output, "OV-TQ-11", 4096)["properties"][
            "TURBOQUANT_NORM_CORRECTION"
        ]
        is False
    )
    assert (
        _read_spec(output, "OV-TQ-12", 4096)["properties"][
            "TURBOQUANT_NORM_CORRECTION"
        ]
        is False
    )


def test_every_generated_spec_is_accepted_by_sequence_contract_for_all_roles(
    tmp_path,
):
    _, _, _, _, output = _generate(tmp_path)

    for spec_path in output.glob("OV-TQ-*/context-*/spec.json"):
        template = _sequence_spec(spec_path)
        for role in SEQUENCE_ROLES:
            role_spec = _role_spec(template, role, "a" * 64)
            assert role_spec["role"] == role
            assert role_spec["campaign_identity_sha256"] == "a" * 64


@pytest.mark.parametrize(
    ("break_input", "message"),
    [
        ("package-initializer", "initializer"),
        ("python-module", "py_openvino_genai"),
        ("empty-python-module", "non-empty"),
        ("runtime-dll", "openvino_genai.dll"),
        ("model", "model"),
        ("cache", "cache"),
        ("output", "empty"),
    ],
)
def test_generation_fails_closed_on_invalid_supplied_paths(
    tmp_path,
    break_input,
    message,
):
    build, model, cache = _clean_inputs(tmp_path)
    output = tmp_path / "output"
    if break_input == "package-initializer":
        (build / "openvino_genai" / "__init__.py").unlink()
    elif break_input == "python-module":
        next((build / "openvino_genai").glob("py_openvino_genai*.pyd")).unlink()
    elif break_input == "empty-python-module":
        next((build / "openvino_genai").glob("py_openvino_genai*.pyd")).write_bytes(
            b""
        )
    elif break_input == "runtime-dll":
        (build / "openvino_genai" / "openvino_genai.dll").unlink()
    elif break_input == "model":
        model.rmdir()
    elif break_input == "cache":
        cache.rmdir()
    else:
        output.mkdir()
        (output / "existing.json").write_text("{}", encoding="utf-8")

    with pytest.raises(ValueError, match=message):
        generate_formal_u8_granite3b_specs(
            matrix_path=MATRIX,
            build_root=build,
            model_path=model,
            cache_root=cache,
            output_root=output,
        )


def test_cli_generates_specs_from_explicit_paths_without_running_inference(
    tmp_path,
):
    build, model, cache = _clean_inputs(tmp_path)
    output = tmp_path / "cli output"

    result = subprocess.run(
        [
            sys.executable,
            str(SCRIPT),
            "--matrix",
            str(MATRIX),
            "--build-root",
            str(build),
            "--model-path",
            str(model),
            "--cache-root",
            str(cache),
            "--output-root",
            str(output),
        ],
        cwd=ROOT,
        capture_output=True,
        text=True,
    )

    assert result.returncode == 0, result.stderr
    summary = json.loads(result.stdout)
    assert summary["spec_count"] == 20
    assert summary["expected_rejection_count"] == 1
    assert (output / "OV-TQ-01/context-4096/spec.json").is_file()
    assert (output / "expected-rejections.json").is_file()
