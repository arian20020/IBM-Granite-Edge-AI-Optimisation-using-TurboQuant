"""Hash-bound inventory and preparation boundary for WB-04 artifacts."""

from __future__ import annotations

import hashlib
import json
import os
import subprocess
import sys
import tempfile
from dataclasses import asdict, dataclass
from pathlib import Path
from types import MappingProxyType
from typing import Any, Callable, Mapping

from .conversion import validate_artifact_manifest
from .owned_process_guard import (
    CREATE_SUSPENDED,
    KillOnCloseJob,
    _append_error,
    _cleanup_job,
    _close_run_resources,
    _resume_suspended_process,
    _wait_process,
    available_ram_bytes,
)

MIB = 1024**2
MIN_LAUNCH_RESERVE_MIB = 4096
MIN_EMERGENCY_FLOOR_MIB = 2048
_TERMINAL_STAGE = "artifact-preparation"


@dataclass(frozen=True)
class ArtifactBinding:
    precision: str
    status: str
    artifact_id: str | None
    model_root: Path | None
    manifest_path: Path | None
    manifest_sha256: str | None
    terminal_stage: str | None
    terminal_receipt_path: Path | None
    terminal_receipt_sha256: str | None


@dataclass(frozen=True)
class ArtifactPreparationOutcome:
    bindings: Mapping[str, ArtifactBinding]
    inventory_path: Path
    inventory_sha256: str


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _canonical_bytes(value: object) -> bytes:
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8")


def _write_canonical_json(path: Path, value: object) -> None:
    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="wb", prefix=".wb04-", dir=destination.parent, delete=False
        ) as handle:
            temporary = Path(handle.name)
            handle.write(_canonical_bytes(value))
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, destination)
        temporary = None
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def _binding_record(binding: ArtifactBinding) -> dict[str, object | None]:
    value = asdict(binding)
    for key in ("model_root", "manifest_path", "terminal_receipt_path"):
        if value[key] is not None:
            value[key] = str(value[key])
    return value


def resolve_artifact_binding(
    manifest_path: Path,
    *,
    expected_precision: str,
) -> ArtifactBinding:
    """Validate a supplied immutable artifact and bind its exact manifest bytes."""

    path = Path(manifest_path).resolve()
    before = sha256_file(path)
    validated = validate_artifact_manifest(path, expected_precision=expected_precision)
    after = sha256_file(path)
    if before != after:
        raise ValueError("artifact manifest changed while it was being validated")
    return ArtifactBinding(
        precision=expected_precision,
        status="available",
        artifact_id=str(validated["artifact_id"]),
        model_root=Path(str(validated["artifact_root"])).resolve(),
        manifest_path=path,
        manifest_sha256=after,
        terminal_stage=None,
        terminal_receipt_path=None,
        terminal_receipt_sha256=None,
    )


def _terminal_binding(receipt_path: Path) -> ArtifactBinding:
    return ArtifactBinding(
        precision="f16",
        status="artifact-preparation-terminal",
        artifact_id=None,
        model_root=None,
        manifest_path=None,
        manifest_sha256=None,
        terminal_stage=_TERMINAL_STAGE,
        terminal_receipt_path=receipt_path.resolve(),
        terminal_receipt_sha256=sha256_file(receipt_path),
    )


def _write_terminal_receipt(
    output_root: Path,
    *,
    classification: str,
    launch_reserve_mib: int,
    emergency_floor_mib: int,
    memory_before: int | None,
    command: list[str],
    environment: Mapping[str, str],
    stdout: bytes = b"",
    stderr: bytes = b"",
    cleanup: Mapping[str, object] | None = None,
) -> ArtifactBinding:
    root = output_root.resolve()
    root.mkdir(parents=True, exist_ok=True)
    command_path = root / "command.json"
    environment_path = root / "environment.json"
    stdout_path = root / "stdout.txt"
    stderr_path = root / "stderr.txt"
    memory_path = root / "memory.json"
    cleanup_path = root / "cleanup.json"
    _write_canonical_json(command_path, {"command": command})
    _write_canonical_json(environment_path, dict(sorted(environment.items())))
    stdout_path.write_bytes(stdout)
    stderr_path.write_bytes(stderr)
    _write_canonical_json(
        memory_path,
        {
            "available_ram_bytes_before": memory_before,
            "launch_reserve_bytes": launch_reserve_mib * MIB,
            "emergency_floor_bytes": emergency_floor_mib * MIB,
        },
    )
    _write_canonical_json(cleanup_path, dict(cleanup or {"cleanup_process_count": 0}))
    receipt_path = root / "terminal-receipt.json"
    _write_canonical_json(
        receipt_path,
        {
            "schema": "official-openvino-adaptive-artifact-preparation/v1",
            "precision": "f16",
            "status": "artifact-preparation-terminal",
            "terminal_stage": _TERMINAL_STAGE,
            "classification": classification,
            "command_sha256": sha256_file(command_path),
            "environment_sha256": sha256_file(environment_path),
            "stdout_sha256": sha256_file(stdout_path),
            "stderr_sha256": sha256_file(stderr_path),
            "memory_sha256": sha256_file(memory_path),
            "cleanup_sha256": sha256_file(cleanup_path),
        },
    )
    return _terminal_binding(receipt_path)


def _worker_main(result_path: Path) -> int:
    _write_canonical_json(result_path, {"classification": "fp16-manifest-not-provided"})
    return 0


def run_guarded_fp16_preparation(
    output_root: Path,
    *,
    launch_reserve_mib: int = MIN_LAUNCH_RESERVE_MIB,
    emergency_floor_mib: int = MIN_EMERGENCY_FLOOR_MIB,
) -> ArtifactBinding:
    """Create an explicit FP16 terminal through a guarded, owned child process.

    This boundary deliberately does not attempt inference or invent an FP16 model.
    A caller that already has an FP16 artifact supplies its validated manifest instead.
    """

    root = Path(output_root).resolve()
    reserve = launch_reserve_mib * MIB
    before = available_ram_bytes()
    environment = {"PYTHONPATH": os.environ.get("PYTHONPATH", "")}
    command = [
        sys.executable,
        "-m",
        "scripts.testing.official_openvino.artifact_inventory",
        "--fp16-preparation-worker",
    ]
    if before is None or before < reserve:
        return _write_terminal_receipt(
            root,
            classification="memory-gate-not-run",
            launch_reserve_mib=launch_reserve_mib,
            emergency_floor_mib=emergency_floor_mib,
            memory_before=before,
            command=command,
            environment=environment,
        )

    result_path = root / "worker-result.json"
    command.extend(["--result", str(result_path)])
    stdout_path = root / "worker.stdout.txt"
    stderr_path = root / "worker.stderr.txt"
    root.mkdir(parents=True, exist_ok=True)
    errors: list[str] = []
    emergency_actions: list[dict[str, object]] = []
    job: KillOnCloseJob | None = None
    process: subprocess.Popen[bytes] | None = None
    handles: list[object] = []
    try:
        job = KillOnCloseJob("WB04-fp16-preparation")
        stdout_handle = stdout_path.open("wb")
        stderr_handle = stderr_path.open("wb")
        handles.extend([stdout_handle, stderr_handle])
        process = subprocess.Popen(
            command,
            stdin=subprocess.DEVNULL,
            stdout=stdout_handle,
            stderr=stderr_handle,
            cwd=Path(__file__).resolve().parents[3],
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP | CREATE_SUSPENDED,
        )
        job.assign_pid(process.pid)
        _resume_suspended_process(process.pid)
        _wait_process(process, 60.0, "FP16 preparation", errors)
    except Exception as error:
        _append_error(errors, f"FP16 preparation launch failed: {type(error).__name__}: {error}")
    finally:
        cleanup, _ = _cleanup_job(job, process, "FP16 preparation", errors, emergency_actions)
        _close_run_resources(handles, [job], errors)
    after = available_ram_bytes()
    cleanup = {
        **cleanup,
        "emergency_actions": emergency_actions,
        "available_ram_bytes_after": after,
    }
    if errors or process is None or process.returncode != 0 or not result_path.is_file():
        raise RuntimeError("uncategorized FP16 preparation crash: " + "; ".join(errors))
    try:
        worker_result = json.loads(result_path.read_text(encoding="utf-8"))
        classification = worker_result["classification"]
    except (OSError, ValueError, KeyError, TypeError) as error:
        raise RuntimeError("uncategorized FP16 preparation crash") from error
    if classification != "fp16-manifest-not-provided":
        raise RuntimeError("uncategorized FP16 preparation crash")
    return _write_terminal_receipt(
        root,
        classification=classification,
        launch_reserve_mib=launch_reserve_mib,
        emergency_floor_mib=emergency_floor_mib,
        memory_before=before,
        command=command,
        environment=environment,
        stdout=stdout_path.read_bytes(),
        stderr=stderr_path.read_bytes(),
        cleanup=cleanup,
    )


def _validate_terminal_binding(binding: ArtifactBinding) -> ArtifactBinding:
    if (
        binding.precision != "f16"
        or binding.status != "artifact-preparation-terminal"
        or binding.terminal_stage != _TERMINAL_STAGE
        or binding.terminal_receipt_path is None
        or binding.terminal_receipt_sha256 is None
    ):
        raise ValueError("FP16 preparation must return an artifact-preparation terminal")
    if binding.artifact_id is not None or binding.model_root is not None:
        raise ValueError("FP16 preparation terminal cannot claim an artifact")
    if sha256_file(binding.terminal_receipt_path) != binding.terminal_receipt_sha256:
        raise ValueError("FP16 preparation terminal receipt hash mismatch")
    return binding


def prepare_adaptive_artifacts(
    *,
    u4_manifest: Path,
    u8_manifest: Path,
    fp16_manifest: Path | None,
    output_root: Path,
    launch_reserve_mib: int = MIN_LAUNCH_RESERVE_MIB,
    emergency_floor_mib: int = MIN_EMERGENCY_FLOOR_MIB,
    prepare_fp16: Callable[[Path], ArtifactBinding] = run_guarded_fp16_preparation,
) -> ArtifactPreparationOutcome:
    """Bind U4/U8 artifacts and record FP16 availability without running inference."""

    if launch_reserve_mib < MIN_LAUNCH_RESERVE_MIB:
        raise ValueError("launch reserve cannot be below 4096 MiB")
    if emergency_floor_mib < MIN_EMERGENCY_FLOOR_MIB:
        raise ValueError("emergency floor cannot be below 2048 MiB")
    root = Path(output_root).resolve()
    u4 = resolve_artifact_binding(u4_manifest, expected_precision="u4")
    u8 = resolve_artifact_binding(u8_manifest, expected_precision="u8")
    f16 = (
        resolve_artifact_binding(fp16_manifest, expected_precision="f16")
        if fp16_manifest is not None
        else _validate_terminal_binding(
            run_guarded_fp16_preparation(
                root / "f16-preparation",
                launch_reserve_mib=launch_reserve_mib,
                emergency_floor_mib=emergency_floor_mib,
            )
            if prepare_fp16 is run_guarded_fp16_preparation
            else prepare_fp16(root / "f16-preparation")
        )
    )
    bindings: Mapping[str, ArtifactBinding] = MappingProxyType({"f16": f16, "u4": u4, "u8": u8})
    inventory_path = root / "artifact-inventory.json"
    _write_canonical_json(
        inventory_path,
        {
            "schema": "official-openvino-adaptive-artifact-inventory/v1",
            "launch_reserve_mib": launch_reserve_mib,
            "emergency_floor_mib": emergency_floor_mib,
            "bindings": {precision: _binding_record(binding) for precision, binding in bindings.items()},
        },
    )
    return ArtifactPreparationOutcome(
        bindings=bindings,
        inventory_path=inventory_path,
        inventory_sha256=sha256_file(inventory_path),
    )


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser()
    parser.add_argument("--fp16-preparation-worker", action="store_true")
    parser.add_argument("--result", type=Path)
    args = parser.parse_args()
    if args.fp16_preparation_worker and args.result is not None:
        raise SystemExit(_worker_main(args.result))
    raise SystemExit("worker arguments are required")
