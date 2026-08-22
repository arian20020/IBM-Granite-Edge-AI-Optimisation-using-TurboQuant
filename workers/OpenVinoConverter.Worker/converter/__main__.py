"""Entry point for the isolated, stdin-only converter process."""

from __future__ import annotations

import os
import socket
import sys
from pathlib import Path

from . import PROTOCOL, PROTOCOL_VERSION
from .export import export_model
from .protocol import ProtocolError, read_request, write_event
from .provenance import fixed_options, package_versions

_FORBIDDEN_ENVIRONMENT = frozenset(
    {
        "HF_TOKEN",
        "HUGGING_FACE_HUB_TOKEN",
        "HTTP_PROXY",
        "HTTPS_PROXY",
        "ALL_PROXY",
        "NO_PROXY",
        "PIP_CONFIG_FILE",
        "PYTHONHOME",
        "PYTHONPATH",
        "VIRTUAL_ENV",
    }
)

_SCRATCH_ENVIRONMENT = (
    "HOME",
    "USERPROFILE",
    "APPDATA",
    "LOCALAPPDATA",
    "HF_HOME",
    "XDG_CACHE_HOME",
    "TORCH_HOME",
    "TEMP",
    "TMP",
)


def install_network_guard() -> None:
    def deny_network(event: str, _arguments: tuple[object, ...]) -> None:
        if event == "socket.__new__" or event.startswith("socket.get") or event.startswith("socket.connect"):
            raise PermissionError("network_disabled")

    sys.addaudithook(deny_network)
    # Retain an explicit reference so static and runtime review show the guarded API.
    _ = socket.socket


def _suppress_stderr() -> None:
    descriptor = os.open(os.devnull, os.O_WRONLY)
    try:
        os.dup2(descriptor, sys.stderr.fileno())
    finally:
        os.close(descriptor)


def assert_closed_module_paths(runtime_root: Path) -> None:
    root = runtime_root.resolve(strict=True)
    for item in sys.path:
        if not item:
            raise RuntimeError("runtime_integrity_failed")
        candidate = Path(item).resolve(strict=True)
        if candidate != root and root not in candidate.parents:
            raise RuntimeError("runtime_integrity_failed")


def _assert_source_not_importable(source_path: str) -> None:
    source = Path(source_path).resolve(strict=True)
    for item in sys.path:
        candidate = Path(item).resolve(strict=True)
        if candidate == source or source in candidate.parents or candidate in source.parents:
            raise RuntimeError("runtime_integrity_failed")


def _validate_runtime() -> Path:
    if not (
        sys.flags.isolated
        and sys.flags.no_site
        and sys.flags.ignore_environment
        and sys.flags.dont_write_bytecode
    ):
        raise RuntimeError("runtime_integrity_failed")
    if os.environ.get("HF_HUB_OFFLINE") != "1" or os.environ.get("TRANSFORMERS_OFFLINE") != "1":
        raise RuntimeError("runtime_integrity_failed")
    if any(name in os.environ for name in _FORBIDDEN_ENVIRONMENT):
        raise RuntimeError("runtime_integrity_failed")
    configured = os.environ.get("GRANITE_CONVERTER_ROOT")
    if not configured:
        raise RuntimeError("runtime_integrity_failed")
    runtime_root = Path(configured)
    assert_closed_module_paths(runtime_root)
    scratch_value = os.environ.get("GRANITE_CONVERTER_SCRATCH")
    if not scratch_value:
        raise RuntimeError("runtime_integrity_failed")
    scratch = Path(scratch_value).resolve(strict=True)
    for name in _SCRATCH_ENVIRONMENT:
        value = os.environ.get(name)
        if not value:
            raise RuntimeError("runtime_integrity_failed")
        candidate = Path(value).resolve(strict=True)
        if candidate != scratch and scratch not in candidate.parents:
            raise RuntimeError("runtime_integrity_failed")
    consent = scratch / "local" / "Intel Corporation" / "openvino_telemetry"
    if consent.read_text(encoding="ascii") != "0":
        raise RuntimeError("runtime_integrity_failed")
    return runtime_root


def main() -> int:
    operation_id = "00000000-0000-0000-0000-000000000000"
    try:
        _validate_runtime()
        install_network_guard()
        _suppress_stderr()
        write_event(
            {
                "type": "hello",
                "protocol": PROTOCOL,
                "version": PROTOCOL_VERSION,
                "isolated": True,
                "offline": True,
                "networkDenied": True,
            }
        )
        request = read_request()
        operation_id = request.operation_id
        _assert_source_not_importable(request.source_path)
        write_event({"type": "started", "operationId": operation_id})
        export_model(request.source_path, request.destination_path)
        write_event(
            {
                "type": "completed",
                "operationId": operation_id,
                "sourceManifestSha256": request.source_manifest_sha256,
                "options": fixed_options(),
                "versions": package_versions(),
            }
        )
        return 0
    except ProtocolError:
        support_code = "runtime_protocol_failed"
    except Exception as error:  # The boundary deliberately suppresses raw diagnostics.
        support_code = str(error) if str(error) in {
            "conversion_preflight_failed",
            "conversion_output_invalid",
            "runtime_integrity_failed",
        } else "conversion_failed"
    try:
        write_event(
            {"type": "failed", "operationId": operation_id, "supportCode": support_code}
        )
    except Exception:
        pass
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
