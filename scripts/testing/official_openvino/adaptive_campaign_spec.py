"""Generate runtime-free worker specs for the five-row adaptive comparison."""

from __future__ import annotations

import hashlib
import json
import shutil
import tempfile
from pathlib import Path
from typing import Any

from scripts.testing.build_official_openvino_adaptive_matrix import (
    load_adaptive_artifact_inventory,
)

from .artifact_inventory import sha256_file
from .matrix import OpenVINOCase, load_adaptive_comparison_matrix
from .runtime_measurement import atomic_write_json, build_runtime_property_spec
from .workload import build_context_workload


SPEC_SCHEMA = "official-openvino-adaptive-comparison-runtime-spec/v1"
SPEC_INDEX_SCHEMA = "official-openvino-adaptive-comparison-spec-index/v1"


def _directory(path: Path, field: str) -> Path:
    resolved = Path(path).resolve()
    if not resolved.is_dir():
        raise ValueError(f"{field} directory is missing: {resolved}")
    return resolved


def _build_root(path: Path) -> Path:
    root = _directory(path, "build root")
    package = _directory(root / "openvino_genai", "OpenVINO GenAI package")
    if not (package / "__init__.py").is_file():
        raise ValueError("OpenVINO GenAI package initializer is missing")
    modules = sorted(package.glob("py_openvino_genai*.pyd"))
    if len(modules) != 1 or modules[0].stat().st_size <= 0:
        raise ValueError("build root must contain one non-empty py_openvino_genai*.pyd")
    if not (package / "openvino_genai.dll").is_file():
        raise ValueError("openvino_genai.dll is missing")
    return root


def _paths_overlap(first: Path, second: Path) -> bool:
    return first == second or first in second.parents or second in first.parents


def _output_path(path: Path, protected: tuple[Path, ...]) -> Path:
    output = Path(path).resolve()
    if any(_paths_overlap(output, item.resolve()) for item in protected):
        raise ValueError("output root must not overlap an input path")
    if output.exists() and not output.is_dir():
        raise ValueError(f"existing output root is not a directory: {output}")
    output.parent.mkdir(parents=True, exist_ok=True)
    return output


def _json_bytes(value: object) -> bytes:
    return (
        json.dumps(value, indent=2, sort_keys=True, allow_nan=False) + "\n"
    ).encode("utf-8")


def _validate_existing_output(
    output: Path,
    expected_files: dict[Path, bytes],
) -> None:
    actual_files = {
        path.relative_to(output)
        for path in output.rglob("*")
        if path.is_file()
    }
    if actual_files != set(expected_files):
        raise ValueError("existing output is incomplete or non-identical")
    for relative, expected in expected_files.items():
        if (output / relative).read_bytes() != expected:
            raise ValueError("existing output is non-identical or hash-drifted")


def _bound_model_root(
    case: OpenVINOCase,
    bindings: dict[str, dict[str, Any]],
) -> Path | None:
    binding = bindings[case.weight_precision]
    if binding["status"] == "artifact-preparation-terminal":
        if case.test_id != "OV-13" or case.artifact_status != binding["status"]:
            raise ValueError(f"{case.test_id} artifact terminal binding does not match inventory")
        if (
            case.artifact_terminal_path != binding["terminal_receipt_path"]
            or case.artifact_terminal_sha256 != binding["terminal_receipt_sha256"]
        ):
            raise ValueError("OV-13 terminal receipt does not match artifact inventory")
        return None
    expected = (
        binding["artifact_id"],
        binding["manifest_path"],
        binding["manifest_sha256"],
        "available",
    )
    actual = (
        case.artifact_id,
        case.artifact_manifest_path,
        case.artifact_manifest_sha256,
        case.artifact_status,
    )
    if actual != expected:
        raise ValueError(f"{case.test_id} artifact binding does not match inventory")
    return _directory(Path(binding["model_root"]), f"{case.test_id} artifact model")


def generate_adaptive_format_comparison_specs(
    *,
    matrix_path: Path,
    build_root: Path,
    artifact_inventory_path: Path,
    cache_root: Path,
    output_root: Path,
) -> dict[str, object]:
    """Emit fixed four-token workload specs; this function never launches inference."""

    matrix = Path(matrix_path).resolve()
    if not matrix.is_file():
        raise ValueError(f"adaptive comparison matrix is missing: {matrix}")
    cases = load_adaptive_comparison_matrix(matrix)
    build = _build_root(build_root)
    inventory, bindings = load_adaptive_artifact_inventory(artifact_inventory_path)
    cache = _directory(cache_root, "cache root")
    output = _output_path(output_root, (matrix, build, inventory, cache))

    planned_specs: list[tuple[Path, dict[str, Any], str, int]] = []
    terminals: list[dict[str, str]] = []
    for case in cases:
        model_root = _bound_model_root(case, bindings)
        if model_root is None:
            terminals.append(
                {
                    "test_id": case.test_id,
                    "stage": "artifact-preparation",
                    "receipt_path": str(case.artifact_terminal_path),
                    "receipt_sha256": str(case.artifact_terminal_sha256),
                }
            )
            continue
        for context in case.contexts:
            workload = build_context_workload(context)
            runtime = build_runtime_property_spec(
                device=case.device,
                key_algorithm=str(case.runtime_key_algorithm),
                value_algorithm=str(case.runtime_value_algorithm),
                key_cache_precision=case.k_precision,
                value_cache_precision=case.v_precision,
                norm_correction=bool(case.norm_correction),
                cache_dir=str((cache / case.test_id / str(context)).resolve()),
            )
            spec = {
                "schema": SPEC_SCHEMA,
                "controlled_test_id": case.test_id,
                "context_tokens": context,
                "workload": {**workload, "actual_input_tokens": context},
                "model_path": str(model_root),
                "artifact_id": case.artifact_id,
                "artifact_manifest_path": case.artifact_manifest_path,
                "artifact_manifest_sha256": case.artifact_manifest_sha256,
                "device": runtime["device"],
                "properties": runtime["properties"],
                "max_new_tokens": 4,
                "ignore_eos": True,
                "seed": 42,
                "apply_chat_template": False,
            }
            planned_specs.append(
                (Path(case.test_id) / str(context) / "runtime-spec.json", spec, case.test_id, context)
            )

    index_specs = [
        {
            "test_id": test_id,
            "context_tokens": context,
            "path": str(relative).replace("\\", "/"),
            "sha256": hashlib.sha256(_json_bytes(spec)).hexdigest(),
        }
        for relative, spec, test_id, context in planned_specs
    ]
    index = {
        "schema": SPEC_INDEX_SCHEMA,
        "matrix_sha256": sha256_file(matrix),
        "artifact_inventory_sha256": sha256_file(inventory),
        "runtime_specs": index_specs,
        "terminals": terminals,
    }
    expected_files = {
        relative: _json_bytes(spec)
        for relative, spec, _, _ in planned_specs
    }
    expected_files[Path("spec-index.json")] = _json_bytes(index)
    result = {
        "build_root": str(build),
        "output_root": str(output),
        "runtime_spec_count": len(planned_specs),
        "terminal_count": len(terminals),
        "runtime_spec_paths": [str(output / relative) for relative, _, _, _ in planned_specs],
        "terminals": terminals,
        "spec_index_path": str(output / "spec-index.json"),
    }
    if output.exists():
        _validate_existing_output(output, expected_files)
        return result

    staging = Path(tempfile.mkdtemp(dir=output.parent, prefix=f".{output.name}."))
    try:
        for relative, spec, test_id, context in planned_specs:
            destination = staging / relative
            atomic_write_json(destination, spec)
        atomic_write_json(staging / "spec-index.json", index)
        staging.rename(output)
    except BaseException:
        if staging.exists():
            shutil.rmtree(staging)
        raise

    return result


__all__ = ["generate_adaptive_format_comparison_specs"]
