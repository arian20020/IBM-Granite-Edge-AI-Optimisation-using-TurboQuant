"""Exact workload spec tests for the adaptive comparison contract."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import pytest

from scripts.testing.build_official_openvino_adaptive_matrix import (
    build_adaptive_comparison_matrix,
)
from scripts.testing.official_openvino.adaptive_campaign_spec import (
    generate_adaptive_format_comparison_specs,
)


ROOT = Path(__file__).resolve().parents[3]
HISTORICAL_MATRIX = ROOT / "experiments/manifests/official-openvino/retest-matrix.json"


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8")


def _available_binding(tmp_path: Path, precision: str) -> dict[str, object | None]:
    root = tmp_path / f"{precision}-model"
    root.mkdir()
    manifest = tmp_path / f"{precision}-manifest.json"
    manifest.write_text(f"{precision} manifest\n", encoding="utf-8")
    return {
        "precision": precision,
        "status": "available",
        "artifact_id": f"artifact-{precision}",
        "model_root": str(root),
        "manifest_path": str(manifest),
        "manifest_sha256": _sha256(manifest),
        "terminal_stage": None,
        "terminal_receipt_path": None,
        "terminal_receipt_sha256": None,
    }


def _terminal_binding(tmp_path: Path) -> dict[str, object | None]:
    receipt = tmp_path / "fp16-terminal.json"
    _write_json(receipt, {"classification": "not-present"})
    return {
        "precision": "f16",
        "status": "artifact-preparation-terminal",
        "artifact_id": None,
        "model_root": None,
        "manifest_path": None,
        "manifest_sha256": None,
        "terminal_stage": "artifact-preparation",
        "terminal_receipt_path": str(receipt),
        "terminal_receipt_sha256": _sha256(receipt),
    }


def _inputs(tmp_path: Path, *, fp16_terminal: bool = False) -> tuple[Path, Path, Path]:
    inventory = tmp_path / "artifact-inventory.json"
    _write_json(
        inventory,
        {
            "schema": "official-openvino-adaptive-artifact-inventory/v1",
            "launch_reserve_mib": 4096,
            "emergency_floor_mib": 2048,
            "bindings": {
                "u4": _available_binding(tmp_path, "u4"),
                "u8": _available_binding(tmp_path, "u8"),
                "f16": _terminal_binding(tmp_path) if fp16_terminal else _available_binding(tmp_path, "f16"),
            },
        },
    )
    matrix = tmp_path / "matrix.json"
    build_adaptive_comparison_matrix(
        artifact_inventory_path=inventory,
        historical_matrix_path=HISTORICAL_MATRIX,
        output_path=matrix,
    )
    build = tmp_path / "build"
    package = build / "openvino_genai"
    package.mkdir(parents=True)
    (package / "__init__.py").write_text("", encoding="utf-8")
    (package / "py_openvino_genai.pyd").write_bytes(b"extension")
    (package / "openvino_genai.dll").write_bytes(b"runtime")
    return matrix, inventory, build


def _generate_specs(tmp_path: Path, *, fp16_terminal: bool = False) -> dict[str, object]:
    matrix, inventory, build = _inputs(tmp_path, fp16_terminal=fp16_terminal)
    cache = tmp_path / "cache"
    cache.mkdir()
    return generate_adaptive_format_comparison_specs(
        matrix_path=matrix,
        build_root=build,
        artifact_inventory_path=inventory,
        cache_root=cache,
        output_root=tmp_path / "specs",
    )


def test_generation_writes_all_five_identities_at_all_five_contexts(tmp_path: Path) -> None:
    result = _generate_specs(tmp_path)
    assert result["runtime_spec_count"] == 25
    assert result["terminal_count"] == 0
    assert len(result["runtime_spec_paths"]) == 25


def test_generation_keeps_fixed_four_token_workload_and_exact_context(tmp_path: Path) -> None:
    result = _generate_specs(tmp_path)
    for path in result["runtime_spec_paths"]:
        spec = json.loads(Path(path).read_text(encoding="utf-8"))
        assert spec["max_new_tokens"] == 4
        assert spec["workload"]["actual_input_tokens"] == spec["context_tokens"]


def test_generation_records_missing_fp16_as_artifact_preparation_boundary(tmp_path: Path) -> None:
    result = _generate_specs(tmp_path, fp16_terminal=True)
    assert result["runtime_spec_count"] == 20
    assert result["terminal_count"] == 1
    assert result["terminals"][0]["test_id"] == "OV-13"
    assert result["terminals"][0]["stage"] == "artifact-preparation"
    index = json.loads((tmp_path / "specs" / "spec-index.json").read_text(encoding="utf-8"))
    assert index["matrix_sha256"] == _sha256(tmp_path / "matrix.json")
    assert index["artifact_inventory_sha256"] == _sha256(tmp_path / "artifact-inventory.json")


def test_generation_is_idempotent_for_an_identical_complete_output(tmp_path: Path) -> None:
    first = _generate_specs(tmp_path)
    matrix, inventory, build = tmp_path / "matrix.json", tmp_path / "artifact-inventory.json", tmp_path / "build"
    repeated = generate_adaptive_format_comparison_specs(
        matrix_path=matrix,
        build_root=build,
        artifact_inventory_path=inventory,
        cache_root=tmp_path / "cache",
        output_root=tmp_path / "specs",
    )

    assert repeated == first


def test_generation_refuses_tampered_existing_output_without_overwriting(tmp_path: Path) -> None:
    _generate_specs(tmp_path)
    matrix, inventory, build = tmp_path / "matrix.json", tmp_path / "artifact-inventory.json", tmp_path / "build"
    tampered = tmp_path / "specs" / "OV-11" / "512" / "runtime-spec.json"
    tampered.write_text("tampered\n", encoding="utf-8")

    with pytest.raises(ValueError, match="existing output"):
        generate_adaptive_format_comparison_specs(
            matrix_path=matrix,
            build_root=build,
            artifact_inventory_path=inventory,
            cache_root=tmp_path / "cache",
            output_root=tmp_path / "specs",
        )
    assert tampered.read_text(encoding="utf-8") == "tampered\n"
