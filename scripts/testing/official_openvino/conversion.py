"""Validation contract for official OpenVINO model conversion evidence."""

from __future__ import annotations

import hashlib
import json
import os
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class ConversionSpec:
    test_id: str
    source_model: str
    source_revision: str
    precision: str
    command: list[str]
    tool_versions: dict[str, str]
    files: dict[str, str]
    load_probe: dict[str, Any]
    terminal_classification: str | None = None
    terminal_evidence: dict[str, Any] = field(default_factory=dict)


def validate_conversion(spec: ConversionSpec) -> dict[str, Any]:
    if not spec.test_id.startswith("OV-C") or not spec.source_model:
        raise ValueError("conversion identity is missing")
    if len(spec.source_revision) != 40:
        raise ValueError("exact source revision is required")
    if spec.precision not in {"f16", "u8", "u4"}:
        raise ValueError("recognized precision is required")

    if spec.terminal_classification:
        if spec.terminal_classification != "memory-gate-not-run":
            raise ValueError("unknown terminal classification")
        evidence = spec.terminal_evidence
        available = evidence.get("available_ram_bytes")
        floor = evidence.get("required_floor_bytes")
        estimated = evidence.get("estimated_peak_bytes", 0)
        if (not isinstance(available, int) or not isinstance(floor, int)
                or not isinstance(estimated, int)
                or available >= floor + estimated):
            raise ValueError("terminal evidence must prove a binding memory gate")
        return {"accepted": True, "status": spec.terminal_classification,
                "test_id": spec.test_id}

    if not spec.command:
        raise ValueError("conversion command is required")
    if not isinstance(spec.tool_versions, dict):
        raise ValueError("conversion tool versions are required")
    for package in ("optimum-intel", "openvino"):
        if not spec.tool_versions.get(package):
            raise ValueError(f"{package} tool version is required")
    required_files = {
        "openvino_model.xml": "model XML hash",
        "openvino_model.bin": "model BIN hash",
        "tokenizer.json": "tokenizer hash",
        "config.json": "config hash",
    }
    for filename, label in required_files.items():
        digest = spec.files.get(filename)
        if not isinstance(digest, str) or len(digest) != 64:
            raise ValueError(f"{label} is required")
    if not spec.load_probe.get("loaded") or spec.load_probe.get("device") != "CPU":
        raise ValueError("CPU load probe is required")
    if not isinstance(spec.load_probe.get("generated_tokens"), int) or spec.load_probe["generated_tokens"] <= 0:
        raise ValueError("load probe must generate tokens")
    return {"accepted": True, "status": "load-proven", "test_id": spec.test_id}


REQUIRED_ARTIFACT_FILES = frozenset({
    "openvino_model.xml",
    "openvino_model.bin",
    "openvino_tokenizer.xml",
    "openvino_tokenizer.bin",
    "openvino_detokenizer.xml",
    "openvino_detokenizer.bin",
    "tokenizer.json",
    "tokenizer_config.json",
    "config.json",
    "generation_config.json",
    "openvino_config.json",
    "README.md",
})
PRECISION_ELEMENT_TYPE = {"f16": "f16", "u8": "i8", "u4": "i4"}
_SHA256 = re.compile(r"^[0-9a-f]{64}$")
_REVISION = re.compile(r"^[0-9a-f]{40}$")


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _require_hash(value: Any, field_name: str) -> str:
    if not isinstance(value, str) or _SHA256.fullmatch(value) is None:
        raise ValueError(f"{field_name} must be a lowercase SHA256")
    return value


def _require_text(value: Any, field_name: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field_name} must be non-blank")
    return value


def _same_path(left: Path, right: Path) -> bool:
    return os.path.normcase(str(left.resolve())) == os.path.normcase(str(right.resolve()))


def _external_hashed_file(
    record: dict[str, Any],
    manifest_path: Path,
    *,
    path_field: str,
    hash_field: str,
    kind: str,
) -> Path:
    raw_path = Path(_require_text(record.get(path_field), f"{kind} path"))
    path = raw_path if raw_path.is_absolute() else manifest_path.parent / raw_path
    path = path.resolve()
    if not path.is_file():
        raise ValueError(f"{kind} file missing: {path}")
    expected = _require_hash(record.get(hash_field), f"{kind} hash")
    if _sha256(path) != expected:
        raise ValueError(f"{kind} hash mismatch")
    return path


def validate_artifact_manifest(
    manifest_path: Path,
    *,
    expected_precision: str | None = None,
) -> dict[str, Any]:
    """Validate a real, immutable OpenVINO model and its CPU generation proof."""

    manifest_path = Path(manifest_path).resolve()
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"artifact manifest is unreadable: {manifest_path}") from exc
    if not isinstance(manifest, dict) or manifest.get("schema_version") != 1:
        raise ValueError("artifact manifest schema version must be 1")
    if manifest.get("status") != "load-proven":
        raise ValueError("artifact status must be load-proven")
    _require_text(manifest.get("artifact_id"), "artifact id")
    artifact_root = Path(
        _require_text(manifest.get("artifact_root"), "artifact root")
    ).resolve()
    if not artifact_root.is_dir():
        raise ValueError(f"artifact root is missing: {artifact_root}")

    model = manifest.get("model")
    if not isinstance(model, dict):
        raise ValueError("model provenance is required")
    for field_name in (
        "family",
        "parameter_scale",
        "source_repository",
        "artifact_repository",
    ):
        _require_text(model.get(field_name), f"model {field_name}")
    for field_name in ("source_revision", "artifact_revision"):
        revision = model.get(field_name)
        if not isinstance(revision, str) or _REVISION.fullmatch(revision) is None:
            raise ValueError(f"{field_name.replace('_', ' ')} must be an exact revision")
    precision = model.get("precision")
    if precision not in PRECISION_ELEMENT_TYPE:
        raise ValueError("recognized model precision is required")
    if expected_precision is not None and precision != expected_precision:
        raise ValueError(
            f"expected precision {expected_precision}, found {precision}"
        )

    conversion = manifest.get("conversion")
    if not isinstance(conversion, dict):
        raise ValueError("conversion provenance is required")
    if conversion.get("kind") not in {"published-preconverted", "local-conversion"}:
        raise ValueError("conversion provenance kind is unsupported")
    command = conversion.get("command")
    if (
        not isinstance(command, list)
        or not command
        or any(not isinstance(item, str) or not item.strip() for item in command)
    ):
        raise ValueError("exact conversion command is required")
    tool_versions = conversion.get("tool_versions")
    if (
        not isinstance(tool_versions, dict)
        or len(tool_versions) < 2
        or any(not isinstance(value, str) or not value.strip()
               for value in tool_versions.values())
    ):
        raise ValueError("at least two exact conversion tool versions are required")

    raw_files = manifest.get("files")
    if not isinstance(raw_files, list) or not raw_files:
        raise ValueError("artifact file inventory is required")
    file_records: dict[str, dict[str, Any]] = {}
    normalized_inventory: list[dict[str, Any]] = []
    for raw_record in raw_files:
        if not isinstance(raw_record, dict):
            raise ValueError("artifact file record must be an object")
        raw_relative = _require_text(raw_record.get("path"), "artifact file path")
        relative = Path(raw_relative)
        if relative.is_absolute() or ".." in relative.parts:
            raise ValueError("artifact files must remain within artifact root")
        path = (artifact_root / relative).resolve()
        try:
            path.relative_to(artifact_root)
        except ValueError as exc:
            raise ValueError("artifact files must remain within artifact root") from exc
        normalized_name = relative.as_posix()
        if normalized_name in file_records:
            raise ValueError(f"duplicate artifact file: {normalized_name}")
        if not path.is_file():
            raise ValueError(f"artifact file missing: {normalized_name}")
        expected_hash = _require_hash(raw_record.get("sha256"), "artifact hash")
        if _sha256(path) != expected_hash:
            raise ValueError(f"artifact hash mismatch: {normalized_name}")
        size = raw_record.get("size_bytes")
        if isinstance(size, bool) or not isinstance(size, int) or size <= 0:
            raise ValueError(f"artifact size is invalid: {normalized_name}")
        if path.stat().st_size != size:
            raise ValueError(f"artifact size mismatch: {normalized_name}")
        normalized = {
            "path": normalized_name,
            "size_bytes": size,
            "sha256": expected_hash,
        }
        normalized_inventory.append(normalized)
        file_records[normalized_name] = normalized
    missing_files = sorted(REQUIRED_ARTIFACT_FILES - set(file_records))
    if missing_files:
        raise ValueError(f"required artifact files missing: {missing_files}")
    normalized_inventory.sort(key=lambda item: item["path"])
    canonical_inventory = json.dumps(
        normalized_inventory, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    inventory_sha256 = hashlib.sha256(canonical_inventory).hexdigest()
    if manifest.get("inventory_sha256") != inventory_sha256:
        raise ValueError("artifact inventory hash mismatch")

    provenance_path = conversion.get("provenance_path")
    if provenance_path not in file_records:
        raise ValueError("conversion provenance path is not in the artifact inventory")
    if conversion.get("provenance_sha256") != file_records[provenance_path]["sha256"]:
        raise ValueError("conversion provenance hash mismatch")

    precision_proof = manifest.get("precision_proof")
    if not isinstance(precision_proof, dict):
        raise ValueError("precision proof is required")
    expected_element_type = PRECISION_ELEMENT_TYPE[precision]
    if precision_proof.get("element_type") != expected_element_type:
        raise ValueError("precision proof element type does not match the precision label")
    proof_path = precision_proof.get("path")
    if proof_path != "openvino_model.xml" or proof_path not in file_records:
        raise ValueError("precision proof must use the inventoried model XML")
    if precision_proof.get("sha256") != file_records[proof_path]["sha256"]:
        raise ValueError("precision proof hash mismatch")
    xml = (artifact_root / proof_path).read_text(encoding="utf-8", errors="strict")
    actual_count = len(re.findall(
        rf'\belement_type="{re.escape(expected_element_type)}"', xml
    ))
    recorded_count = precision_proof.get("element_type_count")
    if (
        isinstance(recorded_count, bool)
        or not isinstance(recorded_count, int)
        or recorded_count <= 0
        or recorded_count != actual_count
    ):
        raise ValueError(
            f"precision proof element type count mismatch: "
            f"recorded {recorded_count}, actual {actual_count}"
        )

    license_record = manifest.get("license")
    if not isinstance(license_record, dict):
        raise ValueError("license evidence is required")
    if license_record.get("spdx") != "Apache-2.0":
        raise ValueError("license must be exact Apache-2.0 evidence")
    license_path = license_record.get("path")
    if license_path not in file_records:
        raise ValueError("license path is not in the artifact inventory")
    if license_record.get("sha256") != file_records[license_path]["sha256"]:
        raise ValueError("license hash mismatch")
    license_text = (artifact_root / license_path).read_text(
        encoding="utf-8", errors="replace"
    ).lower()
    if (
        "license: apache-2.0" not in license_text
        and "apache license" not in license_text
    ):
        raise ValueError("license file does not contain Apache-2.0 evidence")

    probe = manifest.get("load_probe")
    if not isinstance(probe, dict) or probe.get("status") != "passed":
        raise ValueError("successful CPU load probe is required")
    if probe.get("device_requested") != "CPU" or probe.get("device_actual") != "CPU":
        raise ValueError("load probe must prove actual CPU execution")
    if probe.get("fallback") is not False:
        raise ValueError("load probe fallback is not permitted")
    probe_model_path = Path(
        _require_text(probe.get("model_path"), "load probe model path")
    ).resolve()
    if not _same_path(probe_model_path, artifact_root):
        raise ValueError("load probe model path does not match the artifact")
    probe_command = probe.get("command")
    if (
        not isinstance(probe_command, list)
        or not probe_command
        or str(artifact_root) not in probe_command
    ):
        raise ValueError("load probe command is not bound to the artifact path")
    generated_tokens = probe.get("generated_tokens")
    if (
        isinstance(generated_tokens, bool)
        or not isinstance(generated_tokens, int)
        or generated_tokens <= 0
    ):
        raise ValueError("load probe generated tokens must be positive")
    output = probe.get("output")
    if not isinstance(output, str) or not output.strip():
        raise ValueError("load probe output is empty")
    expected_output_hash = _require_hash(
        probe.get("output_sha256"), "load probe output hash"
    )
    if hashlib.sha256(output.encode("utf-8")).hexdigest() != expected_output_hash:
        raise ValueError("load probe output hash mismatch")
    if probe.get("artifact_inventory_sha256") != inventory_sha256:
        raise ValueError("load probe artifact inventory binding mismatch")
    _require_hash(
        probe.get("runtime_build_manifest_sha256"),
        "load probe runtime build manifest hash",
    )
    if probe.get("exit_code") != 0:
        raise ValueError("load probe exit code is not zero")
    if probe.get("cleanup_process_count") != 0:
        raise ValueError("load probe cleanup process count is not zero")
    _external_hashed_file(
        probe,
        manifest_path,
        path_field="log_path",
        hash_field="log_sha256",
        kind="load probe log",
    )

    return {
        "accepted": True,
        "status": "load-proven",
        "artifact_id": manifest["artifact_id"],
        "artifact_root": str(artifact_root),
        "precision": precision,
        "inventory_sha256": inventory_sha256,
        "generated_tokens": generated_tokens,
    }
