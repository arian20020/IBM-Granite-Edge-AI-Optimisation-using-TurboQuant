import hashlib
import json
import subprocess
import sys
from pathlib import Path

import pytest

import scripts.testing.campaigns.openvino.campaign_spec as campaign_spec_module
from scripts.testing.campaigns.openvino.campaign_spec import (
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


def _clean_u4_model(tmp_path: Path) -> Path:
    model = tmp_path / "u4 model path"
    model.mkdir()
    return model


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
    assert len(spec_paths) == 19
    assert result["spec_count"] == 19
    assert result["expected_rejection_count"] == 2
    assert result["build_root"] == str(build.resolve())
    assert result["model_path"] == str(model.resolve())
    assert {path.parent.name for path in spec_paths} >= {
        "context-512",
        "context-2048",
        "context-4096",
        "context-8192",
    }
    assert not (output / "OV-TQ-01").exists()
    assert not (output / "OV-TQ-18").exists()

    rejection_path = output / "expected-rejections.json"
    rejection_bytes = rejection_path.read_bytes()
    rejection = json.loads(rejection_bytes)
    assert rejection["schema"] == "official-openvino-wb04-expected-rejections/v1"
    assert rejection["matrix_sha256"] == hashlib.sha256(MATRIX.read_bytes()).hexdigest()
    assert {
        item["matrix_case"]["test_id"] for item in rejection["rejections"]
    } == {"OV-TQ-01", "OV-TQ-18"}
    for item in rejection["rejections"]:
        contract = item["execution_contract"]
        assert contract["execution_route"] == "expected-rejection"
        assert contract["attention_path"] == "not-produced-by-expected-rejection"
        assert contract["numeric_generation_metrics_expected"] is False

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

    assert not (output / "OV-TQ-01").exists()

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


def test_explicit_u4_binding_records_tq02_semantic_rejection_without_a_spec(
    tmp_path,
):
    build, u8_model, cache = _clean_inputs(tmp_path)
    u4_model = _clean_u4_model(tmp_path)
    output = tmp_path / "u8 and u4 spec output"

    result = generate_formal_u8_granite3b_specs(
        matrix_path=MATRIX,
        build_root=build,
        model_path=u8_model,
        u4_model_path=u4_model,
        cache_root=cache,
        output_root=output,
    )

    spec_paths = sorted(output.glob("OV-TQ-*/context-*/spec.json"))
    assert len(spec_paths) == 19
    assert result["spec_count"] == 19
    assert (output / "OV-TQ-18").exists() is False
    assert (output / "OV-TQ-01").exists() is False
    assert (output / "OV-TQ-02").exists() is False

    rejection = json.loads((output / "expected-rejections.json").read_text())
    assert {
        item["matrix_case"]["test_id"] for item in rejection["rejections"]
    } == {"OV-TQ-01", "OV-TQ-02", "OV-TQ-18"}


def test_opt_in_baselines_adds_cpu_and_exact_gpu0_specs_and_rejections(
    tmp_path,
):
    build, u8_model, cache = _clean_inputs(tmp_path)
    u4_model = _clean_u4_model(tmp_path)
    output = tmp_path / "complete local 3b spec output"

    result = generate_formal_u8_granite3b_specs(
        matrix_path=MATRIX,
        build_root=build,
        model_path=u8_model,
        u4_model_path=u4_model,
        cache_root=cache,
        output_root=output,
        include_baselines=True,
    )

    assert result["spec_count"] == 21
    cpu = _read_spec(output, "OV-03", 4096)
    assert cpu["device"] == "CPU"
    assert cpu["properties"]["KEY_CACHE_PRECISION"] == "f16"
    assert cpu["properties"]["VALUE_CACHE_PRECISION"] == "f16"
    assert not any(key.startswith("TURBOQUANT_") for key in cpu["properties"])

    gpu = _read_spec(output, "OV-06", 4096)
    assert gpu["device"] == "GPU.0"
    assert gpu["properties"] == {
        "ATTENTION_BACKEND": "SDPA",
        "CACHE_DIR": str((cache / "OV-06" / "context-4096").resolve()),
        "NUM_STREAMS": "1",
        "PERFORMANCE_HINT": "LATENCY",
    }
    assert type(gpu["properties"]["NUM_STREAMS"]) is str

    rejection = json.loads((output / "expected-rejections.json").read_text())
    assert {
        item["matrix_case"]["test_id"] for item in rejection["rejections"]
    } == {"OV-04", "OV-05", "OV-TQ-01", "OV-TQ-02", "OV-TQ-18"}
    assert not (output / "OV-04").exists()
    assert not (output / "OV-05").exists()

    for test_id in ("OV-03", "OV-06"):
        template = _sequence_spec(
            output / test_id / "context-4096" / "spec.json"
        )
        for role in SEQUENCE_ROLES:
            role_spec = _role_spec(template, role, "b" * 64)
            assert role_spec["role"] == role


@pytest.mark.parametrize("overlap", ("build", "model", "cache", "ancestor"))
def test_generation_rejects_output_aliasing_controlled_input_roots(
    tmp_path,
    overlap,
):
    build, model, cache = _clean_inputs(tmp_path)
    output = {
        "build": build / "generated-specs",
        "model": model / "generated-specs",
        "cache": cache / "generated-specs",
        "ancestor": tmp_path,
    }[overlap]

    with pytest.raises(ValueError, match="overlap"):
        generate_formal_u8_granite3b_specs(
            matrix_path=MATRIX,
            build_root=build,
            model_path=model,
            cache_root=cache,
            output_root=output,
        )


def test_concurrent_output_creation_is_preserved_and_publication_rejects(
    tmp_path,
    monkeypatch,
):
    build, model, cache = _clean_inputs(tmp_path)
    output = tmp_path / "raced output"
    original = campaign_spec_module.atomic_write_json
    raced = False

    def create_competing_destination(path, value):
        nonlocal raced
        if not raced:
            raced = True
            output.mkdir()
            (output / "competitor.txt").write_text(
                "preserve me", encoding="utf-8"
            )
        original(path, value)

    monkeypatch.setattr(
        campaign_spec_module,
        "atomic_write_json",
        create_competing_destination,
    )

    with pytest.raises(FileExistsError, match="publish"):
        generate_formal_u8_granite3b_specs(
            matrix_path=MATRIX,
            build_root=build,
            model_path=model,
            cache_root=cache,
            output_root=output,
        )

    assert (output / "competitor.txt").read_text(encoding="utf-8") == (
        "preserve me"
    )
    assert not (output / "expected-rejections.json").exists()
    assert not list(output.glob("OV-*/context-*/spec.json"))


def test_explicit_u4_model_binding_fails_closed_when_u4_model_is_missing(
    tmp_path,
):
    build, model, cache = _clean_inputs(tmp_path)

    with pytest.raises(ValueError, match="U4 model"):
        generate_formal_u8_granite3b_specs(
            matrix_path=MATRIX,
            build_root=build,
            model_path=model,
            u4_model_path=tmp_path / "missing u4 model",
            cache_root=cache,
            output_root=tmp_path / "output",
        )


def test_explicit_u4_model_binding_rejects_the_u8_model_directory(
    tmp_path,
):
    build, model, cache = _clean_inputs(tmp_path)

    with pytest.raises(ValueError, match="must differ"):
        generate_formal_u8_granite3b_specs(
            matrix_path=MATRIX,
            build_root=build,
            model_path=model,
            u4_model_path=model,
            cache_root=cache,
            output_root=tmp_path / "output",
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
    assert summary["spec_count"] == 19
    assert summary["expected_rejection_count"] == 2
    assert not (output / "OV-TQ-01/context-4096/spec.json").exists()
    assert (output / "expected-rejections.json").is_file()


def test_cli_accepts_explicit_u4_model_binding_without_running_inference(
    tmp_path,
):
    build, model, cache = _clean_inputs(tmp_path)
    u4_model = _clean_u4_model(tmp_path)
    output = tmp_path / "u4 cli output"

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
            "--u4-model-path",
            str(u4_model),
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
    assert summary["spec_count"] == 19
    rejection = json.loads((output / "expected-rejections.json").read_text())
    assert {
        item["matrix_case"]["test_id"] for item in rejection["rejections"]
    } == {"OV-TQ-01", "OV-TQ-02", "OV-TQ-18"}


def test_cli_can_opt_in_complete_local_u8_baselines_without_inference(
    tmp_path,
):
    build, model, cache = _clean_inputs(tmp_path)
    u4_model = _clean_u4_model(tmp_path)
    output = tmp_path / "complete local cli output"

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
            "--u4-model-path",
            str(u4_model),
            "--include-baselines",
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
    assert summary["spec_count"] == 21
    assert summary["expected_rejection_count"] == 5
    assert (output / "OV-03/context-4096/spec.json").is_file()
    assert (output / "OV-06/context-4096/spec.json").is_file()
