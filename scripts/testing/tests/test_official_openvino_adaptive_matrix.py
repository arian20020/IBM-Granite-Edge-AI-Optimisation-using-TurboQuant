"""Contract tests for the isolated adaptive format comparison matrix."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import pytest

from scripts.testing.build_official_openvino_adaptive_matrix import (
    build_adaptive_comparison_matrix,
)
from scripts.testing.official_openvino.matrix import (
    COMPARISON_CONTEXTS,
    COMPARISON_IDS,
    COMPARISON_RUN_ORDER,
    FORMAL_METRICS,
    load_adaptive_comparison_matrix,
    load_matrix,
)


ROOT = Path(__file__).resolve().parents[3]
HISTORICAL_MATRIX = ROOT / "experiments/manifests/official-openvino/retest-matrix.json"
HISTORICAL_MATRIX_SHA256 = hashlib.sha256(HISTORICAL_MATRIX.read_bytes()).hexdigest()


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8")


def _binding(tmp_path: Path, precision: str) -> dict[str, object | None]:
    manifest = tmp_path / f"{precision}-manifest.json"
    manifest.write_text(f"{precision} manifest\n", encoding="utf-8")
    model_root = tmp_path / f"{precision}-model"
    model_root.mkdir()
    return {
        "precision": precision,
        "status": "available",
        "artifact_id": f"artifact-{precision}",
        "model_root": str(model_root),
        "manifest_path": str(manifest),
        "manifest_sha256": sha256_file(manifest),
        "terminal_stage": None,
        "terminal_receipt_path": None,
        "terminal_receipt_sha256": None,
    }


def _terminal_binding(tmp_path: Path) -> dict[str, object | None]:
    receipt = tmp_path / "fp16-terminal.json"
    write_json(receipt, {"classification": "not-present"})
    return {
        "precision": "f16",
        "status": "artifact-preparation-terminal",
        "artifact_id": None,
        "model_root": None,
        "manifest_path": None,
        "manifest_sha256": None,
        "terminal_stage": "artifact-preparation",
        "terminal_receipt_path": str(receipt),
        "terminal_receipt_sha256": sha256_file(receipt),
    }


@pytest.fixture
def inventory(tmp_path: Path) -> Path:
    path = tmp_path / "inventory.json"
    write_json(
        path,
        {
            "schema": "official-openvino-adaptive-artifact-inventory/v1",
            "launch_reserve_mib": 4096,
            "emergency_floor_mib": 2048,
            "bindings": {
                "u4": _binding(tmp_path, "u4"),
                "u8": _binding(tmp_path, "u8"),
                "f16": _binding(tmp_path, "f16"),
            },
        },
    )
    return path


@pytest.fixture
def matrix(tmp_path: Path, inventory: Path) -> Path:
    output = tmp_path / "adaptive-comparison-matrix.json"
    build_adaptive_comparison_matrix(
        artifact_inventory_path=inventory,
        historical_matrix_path=HISTORICAL_MATRIX,
        output_path=output,
    )
    return output


def test_comparison_matrix_declares_exact_ids_contexts_and_run_order(matrix: Path) -> None:
    cases = load_adaptive_comparison_matrix(matrix)
    assert tuple(case.test_id for case in cases) == COMPARISON_RUN_ORDER
    assert {case.test_id for case in cases} == COMPARISON_IDS
    assert all(case.contexts == COMPARISON_CONTEXTS for case in cases)


def test_comparison_matrix_binds_one_u8_artifact_to_standard_tbq4_and_tbq3(matrix: Path) -> None:
    by_id = {case.test_id: case for case in load_adaptive_comparison_matrix(matrix)}
    identities = {
        (by_id[test_id].artifact_id, by_id[test_id].artifact_manifest_sha256)
        for test_id in ("OV-12", "OV-TQ-21", "OV-TQ-22")
    }
    assert len(identities) == 1


def test_comparison_matrix_separates_weight_precision_from_standard_kv_request(matrix: Path) -> None:
    by_id = {case.test_id: case for case in load_adaptive_comparison_matrix(matrix)}
    assert by_id["OV-11"].weight_precision == "u4"
    assert by_id["OV-12"].weight_precision == "u8"
    assert by_id["OV-13"].weight_precision == "f16"
    assert all(
        by_id[test_id].runtime_key_algorithm == "STANDARD"
        and by_id[test_id].runtime_value_algorithm == "STANDARD"
        for test_id in ("OV-11", "OV-12", "OV-13")
    )


def test_comparison_matrix_is_formal_cpu_fair_and_metric_complete(matrix: Path) -> None:
    for case in load_adaptive_comparison_matrix(matrix):
        assert case.phase == "formal"
        assert case.model == "granite-3b"
        assert case.device == "cpu"
        assert case.requested_device == "CPU"
        assert case.guard == "ram-2048-mib"
        assert case.quality_required is True
        assert case.numeric_generation_metrics_expected is True
        assert case.required_metrics == FORMAL_METRICS


def test_comparison_matrix_allows_only_fp16_preparation_terminal(tmp_path: Path, inventory: Path) -> None:
    payload = json.loads(inventory.read_text(encoding="utf-8"))
    payload["bindings"]["f16"] = _terminal_binding(tmp_path)
    write_json(inventory, payload)
    output = tmp_path / "terminal-matrix.json"
    build_adaptive_comparison_matrix(
        artifact_inventory_path=inventory,
        historical_matrix_path=HISTORICAL_MATRIX,
        output_path=output,
    )
    cases = load_adaptive_comparison_matrix(output)
    assert next(case for case in cases if case.test_id == "OV-13").artifact_status == "artifact-preparation-terminal"


def test_historical_matrix_contract_is_unchanged() -> None:
    assert len(load_matrix(HISTORICAL_MATRIX)) == 60
    assert sha256_file(HISTORICAL_MATRIX) == HISTORICAL_MATRIX_SHA256
