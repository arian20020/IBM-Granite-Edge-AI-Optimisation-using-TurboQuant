"""Validation helpers for reproducible Official OpenVINO acquisition evidence."""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any


EXPECTED_PACKAGES = {
    "openvino": "2026.2.1",
    "openvino-genai": "2026.2.1.0",
}

_SECRET_MARKERS = (
    "API_KEY",
    "AUTH",
    "CREDENTIAL",
    "PASSWORD",
    "PRIVATE_KEY",
    "SECRET",
    "TOKEN",
)


def audit_checkout(
    evidence: Mapping[str, Any], *, expected_tag: str, expected_remote: str
) -> dict[str, Any]:
    """Validate that source evidence identifies the exact clean release checkout."""
    actual_tag = evidence.get("tag")
    if actual_tag != expected_tag:
        raise ValueError(f"expected tag {expected_tag}, found {actual_tag!r}")

    actual_remote = evidence.get("remote")
    if actual_remote != expected_remote:
        raise ValueError(f"expected remote {expected_remote}, found {actual_remote!r}")

    if evidence.get("dirty") is not False:
        raise ValueError("dirty checkout is not valid acquisition evidence")

    commit = evidence.get("commit")
    if not isinstance(commit, str) or len(commit) != 40:
        raise ValueError("checkout commit must be a 40-character SHA")

    tree_hash = evidence.get("tree_sha256")
    if not isinstance(tree_hash, str) or len(tree_hash) != 64:
        raise ValueError("checkout tree_sha256 must be a 64-character digest")

    return dict(evidence)


def audit_environment(evidence: Mapping[str, Any]) -> dict[str, Any]:
    """Validate package pins and the CPU/GPU inventory required by the matrix."""
    packages = evidence.get("packages")
    if not isinstance(packages, Mapping):
        raise ValueError("environment packages inventory is missing")
    for package, expected_version in EXPECTED_PACKAGES.items():
        actual_version = packages.get(package)
        if actual_version != expected_version:
            raise ValueError(
                f"{package} must be {expected_version}, found {actual_version!r}"
            )

    devices = evidence.get("devices")
    if not isinstance(devices, list):
        raise ValueError("OpenVINO device inventory is missing")
    normalized_devices = [str(device).upper() for device in devices]
    if not any(device == "CPU" or device.startswith("CPU.") for device in normalized_devices):
        raise ValueError("CPU device was not detected")
    if not any(device == "GPU" or device.startswith("GPU.") for device in normalized_devices):
        raise ValueError("GPU device was not detected")

    installed_ram = evidence.get("installed_ram_bytes")
    if not isinstance(installed_ram, int) or installed_ram <= 0:
        raise ValueError("installed RAM inventory is missing")
    if not evidence.get("python_version") or not evidence.get("os"):
        raise ValueError("Python and OS inventory are required")

    audited = dict(evidence)
    audited["accepted"] = True
    return audited


def redact_environment(environment: Mapping[str, str]) -> dict[str, str]:
    """Remove variables whose names indicate credentials or secret material."""
    return {
        key: value
        for key, value in environment.items()
        if not any(marker in key.upper() for marker in _SECRET_MARKERS)
    }
