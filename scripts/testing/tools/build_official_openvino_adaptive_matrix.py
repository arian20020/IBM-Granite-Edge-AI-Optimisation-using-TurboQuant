"""Materialize the isolated adaptive format comparison matrix from receipts."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import tempfile
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[3]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.campaigns.openvino.artifact_inventory import sha256_file
from scripts.testing.campaigns.openvino.matrix import (
    COMPARISON_CONTEXTS,
    COMPARISON_RUN_ORDER,
    FORMAL_METRICS,
    load_adaptive_comparison_matrix,
    load_matrix,
)


_INVENTORY_SCHEMA = "official-openvino-adaptive-artifact-inventory/v1"
_SHA256_LENGTH = 64
_IDENTITIES: dict[str, dict[str, str]] = {
    "OV-11": {"weight_precision": "u4", "k_algorithm": "standard", "v_algorithm": "standard", "k_precision": "f16", "v_precision": "f16", "runtime_key_algorithm": "STANDARD", "runtime_value_algorithm": "STANDARD", "execution_route": "stateful-standard", "attention_path": "stateful_sdpa_standard"},
    "OV-TQ-22": {"weight_precision": "u8", "k_algorithm": "tbq3", "v_algorithm": "tbq3", "k_precision": "u3", "v_precision": "u3", "runtime_key_algorithm": "TBQ3", "runtime_value_algorithm": "TBQ3", "execution_route": "patched-stateful", "attention_path": "stateful_sdpa_reference_codec"},
    "OV-TQ-21": {"weight_precision": "u8", "k_algorithm": "tbq4", "v_algorithm": "tbq4", "k_precision": "u4", "v_precision": "u4", "runtime_key_algorithm": "TBQ4", "runtime_value_algorithm": "TBQ4", "execution_route": "patched-stateful", "attention_path": "stateful_sdpa_reference_codec"},
    "OV-12": {"weight_precision": "u8", "k_algorithm": "standard", "v_algorithm": "standard", "k_precision": "f16", "v_precision": "f16", "runtime_key_algorithm": "STANDARD", "runtime_value_algorithm": "STANDARD", "execution_route": "stateful-standard", "attention_path": "stateful_sdpa_standard"},
    "OV-13": {"weight_precision": "f16", "k_algorithm": "standard", "v_algorithm": "standard", "k_precision": "f16", "v_precision": "f16", "runtime_key_algorithm": "STANDARD", "runtime_value_algorithm": "STANDARD", "execution_route": "stateful-standard", "attention_path": "stateful_sdpa_standard"},
}


def _canonical_bytes(value: object) -> bytes:
    return (
        json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False)
        + "\n"
    ).encode("utf-8")


def load_adaptive_artifact_inventory(path: Path) -> tuple[Path, dict[str, dict[str, Any]]]:
    inventory = Path(path).resolve()
    if not inventory.is_file():
        raise ValueError(f"artifact inventory file is missing: {inventory}")
    try:
        payload = json.loads(inventory.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as error:
        raise ValueError("artifact inventory is not valid JSON") from error
    if not isinstance(payload, dict) or payload.get("schema") != _INVENTORY_SCHEMA:
        raise ValueError("artifact inventory schema is invalid")
    bindings = payload.get("bindings")
    if not isinstance(bindings, dict) or set(bindings) != {"u4", "u8", "f16"}:
        raise ValueError("artifact inventory must bind exactly U4, U8, and FP16")
    typed: dict[str, dict[str, Any]] = {}
    for precision in ("u4", "u8", "f16"):
        binding = bindings[precision]
        if not isinstance(binding, dict) or binding.get("precision") != precision:
            raise ValueError(f"artifact inventory {precision} binding is invalid")
        status = binding.get("status")
        if status == "available":
            for field in ("artifact_id", "model_root", "manifest_path", "manifest_sha256"):
                if not isinstance(binding.get(field), str) or not binding[field]:
                    raise ValueError(f"artifact inventory {precision} {field} is required")
            manifest = Path(binding["manifest_path"])
            root = Path(binding["model_root"])
            if not manifest.is_file() or not root.is_dir():
                raise ValueError(f"artifact inventory {precision} artifact path is unavailable")
            if len(binding["manifest_sha256"]) != _SHA256_LENGTH or sha256_file(manifest) != binding["manifest_sha256"]:
                raise ValueError(f"artifact inventory {precision} manifest hash mismatch")
            if binding.get("terminal_receipt_path") is not None or binding.get("terminal_receipt_sha256") is not None:
                raise ValueError(f"artifact inventory {precision} available binding carries terminal data")
        elif precision == "f16" and status == "artifact-preparation-terminal":
            if binding.get("terminal_stage") != "artifact-preparation":
                raise ValueError("FP16 terminal stage must be artifact-preparation")
            for field in ("terminal_receipt_path", "terminal_receipt_sha256"):
                if not isinstance(binding.get(field), str) or not binding[field]:
                    raise ValueError(f"FP16 terminal {field} is required")
            receipt = Path(binding["terminal_receipt_path"])
            if not receipt.is_file() or len(binding["terminal_receipt_sha256"]) != _SHA256_LENGTH or sha256_file(receipt) != binding["terminal_receipt_sha256"]:
                raise ValueError("FP16 terminal receipt hash mismatch")
            if any(binding.get(field) is not None for field in ("artifact_id", "model_root", "manifest_path", "manifest_sha256")):
                raise ValueError("FP16 terminal cannot claim an artifact")
        else:
            raise ValueError(f"artifact inventory {precision} status is invalid")
        typed[precision] = binding
    return inventory, typed


def _artifact_fields(binding: dict[str, Any]) -> dict[str, str | None]:
    if binding["status"] == "available":
        return {
            "artifact_id": binding["artifact_id"],
            "artifact_manifest_path": binding["manifest_path"],
            "artifact_manifest_sha256": binding["manifest_sha256"],
            "artifact_status": "available",
            "artifact_terminal_path": None,
            "artifact_terminal_sha256": None,
        }
    return {
        "artifact_id": None,
        "artifact_manifest_path": None,
        "artifact_manifest_sha256": None,
        "artifact_status": "artifact-preparation-terminal",
        "artifact_terminal_path": binding["terminal_receipt_path"],
        "artifact_terminal_sha256": binding["terminal_receipt_sha256"],
    }


def _case(test_id: str, binding: dict[str, Any]) -> dict[str, object]:
    identity = _IDENTITIES[test_id]
    return {
        "test_id": test_id,
        "phase": "formal",
        "description": f"Adaptive format comparison {test_id}",
        "model": "granite-3b",
        **identity,
        "device": "cpu",
        "contexts": list(COMPARISON_CONTEXTS),
        "guard": "ram-2048-mib",
        "quality_required": True,
        "required_metrics": sorted(FORMAL_METRICS),
        "key_cache_precision": identity["k_precision"],
        "value_cache_precision": identity["v_precision"],
        "requested_device": "CPU",
        "norm_correction": identity["k_algorithm"] in {"tbq3", "tbq4"},
        "expected_outcome": "pass",
        "suitable_host_required": False,
        "numeric_generation_metrics_expected": True,
        **_artifact_fields(binding),
    }


def build_adaptive_comparison_matrix(
    *,
    artifact_inventory_path: Path,
    historical_matrix_path: Path,
    output_path: Path,
) -> Path:
    """Write the exact five-row matrix, refusing non-identical replacement."""

    _, bindings = load_adaptive_artifact_inventory(artifact_inventory_path)
    historical = Path(historical_matrix_path).resolve()
    # Loading is intentional: this proves the frozen historical contract before
    # copying only its source/build receipt objects into the new matrix.
    load_matrix(historical)
    payload = json.loads(historical.read_text(encoding="utf-8-sig"))
    matrix = {
        "source_identity": payload["source_identity"],
        "build_identity": payload["build_identity"],
        "cases": [
            _case(test_id, bindings[_IDENTITIES[test_id]["weight_precision"]])
            for test_id in COMPARISON_RUN_ORDER
        ],
    }
    encoded = _canonical_bytes(matrix)
    output = Path(output_path).resolve()
    if output.exists():
        if not output.is_file() or output.read_bytes() != encoded:
            raise FileExistsError("refusing to overwrite a non-identical adaptive comparison matrix")
        return output
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(dir=output.parent, prefix=f".{output.name}.", delete=False) as handle:
            temporary = Path(handle.name)
            handle.write(encoded)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, output)
        temporary = None
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)
    load_adaptive_comparison_matrix(output)
    return output


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--artifact-inventory", type=Path, required=True)
    parser.add_argument("--historical-matrix", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        output = build_adaptive_comparison_matrix(
            artifact_inventory_path=args.artifact_inventory,
            historical_matrix_path=args.historical_matrix,
            output_path=args.output,
        )
    except (ValueError, FileExistsError) as error:
        _parser().error(str(error))
    print(json.dumps({"matrix_path": str(output), "matrix_sha256": sha256_file(output)}, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
