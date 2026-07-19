"""Validation contract for official OpenVINO model conversion evidence."""

from __future__ import annotations

from dataclasses import dataclass, field
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
