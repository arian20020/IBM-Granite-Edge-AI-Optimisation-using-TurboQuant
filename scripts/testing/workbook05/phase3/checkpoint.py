"""Atomic, identity-bound Workbook 05 Phase 3 checkpoints."""

from __future__ import annotations

import hashlib
import json
import os
import re
import time
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Mapping


_HEX_40 = re.compile(r"^[0-9a-f]{40}$")
_HEX_64 = re.compile(r"^[0-9a-f]{64}$")


class CheckpointIdentityError(ValueError):
    """Raised when checkpoint identity or retained evidence has drifted."""


class CheckpointGenerationError(ValueError):
    """Raised when an update is based on a stale checkpoint generation."""


@dataclass(frozen=True, slots=True)
class CheckpointIdentity:
    """Every immutable input that defines a resumable execution."""

    repository_head: str
    prerequisite_proof_sha256: str
    asset_lock_sha256: str
    executable_sha256: str
    request_sha256: str
    configuration_sha256: str
    prompt_sha256: str
    rubric_sha256: str

    def __post_init__(self) -> None:
        if not _HEX_40.fullmatch(self.repository_head):
            raise ValueError("repository_head must be 40 lowercase hexadecimal characters")
        for field_name, value in self.as_dict().items():
            if field_name == "repository_head":
                continue
            if not _HEX_64.fullmatch(value):
                raise ValueError(
                    f"{field_name} must be 64 lowercase hexadecimal characters"
                )

    def as_dict(self) -> dict[str, str]:
        """Return a stable JSON-ready identity mapping."""

        return asdict(self)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _atomic_json_write(path: Path, payload: Mapping[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    data = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")
    try:
        with temporary.open("xb") as handle:
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def _acquire_lock(lock_path: Path, timeout_seconds: float = 10.0) -> int:
    deadline = time.monotonic() + timeout_seconds
    while True:
        try:
            return os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
        except FileExistsError:
            if time.monotonic() >= deadline:
                raise TimeoutError(f"Timed out waiting for checkpoint lock: {lock_path}")
            time.sleep(0.05)


def _load_json(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise CheckpointIdentityError(f"Checkpoint is unreadable: {path}") from error
    if not isinstance(payload, dict):
        raise CheckpointIdentityError("Checkpoint root must be one JSON object")
    return payload


def _verify_identity(payload: Mapping[str, Any], identity: CheckpointIdentity) -> None:
    recorded = payload.get("identity")
    if not isinstance(recorded, dict):
        raise CheckpointIdentityError("Checkpoint identity is missing")
    expected = identity.as_dict()
    for field_name, expected_value in expected.items():
        if recorded.get(field_name) != expected_value:
            raise CheckpointIdentityError(
                f"Checkpoint identity mismatch: {field_name}"
            )


def _verify_step_evidence(payload: Mapping[str, Any]) -> None:
    steps = payload.get("steps")
    if not isinstance(steps, list):
        raise CheckpointIdentityError("Checkpoint steps must be an array")
    for step in steps:
        if not isinstance(step, dict):
            raise CheckpointIdentityError("Checkpoint step must be an object")
        if step.get("status") != "Passed":
            continue
        evidence_text = step.get("evidence_path")
        expected_digest = step.get("evidence_sha256")
        if not isinstance(evidence_text, str) or not _HEX_64.fullmatch(
            str(expected_digest or "")
        ):
            raise CheckpointIdentityError(
                f"Passed step {step.get('step_id')} has incomplete evidence"
            )
        evidence_path = Path(evidence_text)
        if not evidence_path.is_file():
            raise CheckpointIdentityError(
                f"Passed step evidence is missing: {evidence_path}"
            )
        if _sha256(evidence_path) != expected_digest:
            raise CheckpointIdentityError(
                f"Passed step evidence SHA-256 mismatch: {evidence_path}"
            )


def load_and_verify_checkpoint(
    path: Path,
    identity: CheckpointIdentity,
) -> dict[str, Any]:
    """Load a checkpoint and re-verify identity plus every passed step."""

    payload = _load_json(path)
    _verify_identity(payload, identity)
    _verify_step_evidence(payload)
    return payload


def record_checkpoint_step(
    path: Path,
    expected_generation: int,
    identity: CheckpointIdentity,
    step_id: str,
    status: str,
    evidence_path: Path | None,
    evidence_sha256: str | None,
) -> dict[str, Any]:
    """Append one immutable step using compare-and-swap generation semantics."""

    if expected_generation < 0:
        raise ValueError("expected_generation cannot be negative")
    if not step_id.strip():
        raise ValueError("step_id is required")
    if status not in {"Pending", "Passed", "Failed", "Blocked", "Skipped"}:
        raise ValueError(f"Unsupported checkpoint status: {status}")

    if status == "Passed":
        if evidence_path is None or not evidence_path.is_file():
            raise ValueError("Passed checkpoint steps require an evidence file")
        if not _HEX_64.fullmatch(str(evidence_sha256 or "")):
            raise ValueError("Passed checkpoint steps require a SHA-256 digest")
        if _sha256(evidence_path) != evidence_sha256:
            raise CheckpointIdentityError("Supplied evidence SHA-256 does not match")

    lock_path = path.with_suffix(path.suffix + ".lock")
    lock_path.parent.mkdir(parents=True, exist_ok=True)
    descriptor = _acquire_lock(lock_path)
    try:
        if path.exists():
            current = load_and_verify_checkpoint(path, identity)
        else:
            current = {
                "record_type": "phase3-checkpoint",
                "schema_version": "1.0",
                "generation": 0,
                "identity": identity.as_dict(),
                "steps": [],
            }

        generation = current.get("generation")
        if generation != expected_generation:
            raise CheckpointGenerationError(
                f"Expected generation {expected_generation}; observed {generation}"
            )

        steps = current.get("steps")
        if not isinstance(steps, list):
            raise CheckpointIdentityError("Checkpoint steps must be an array")
        if any(step.get("step_id") == step_id for step in steps if isinstance(step, dict)):
            raise ValueError(f"Checkpoint step already exists: {step_id}")

        step: dict[str, Any] = {
            "step_id": step_id,
            "status": status,
            "evidence_path": str(evidence_path) if evidence_path is not None else None,
            "evidence_sha256": evidence_sha256,
        }
        updated = {
            **current,
            "generation": expected_generation + 1,
            "steps": [*steps, step],
        }
        _atomic_json_write(path, updated)
        return updated
    finally:
        os.close(descriptor)
        lock_path.unlink(missing_ok=True)


def first_incomplete_step(checkpoint: Mapping[str, Any]) -> str | None:
    """Return the first non-passed step identifier, or ``None`` when complete."""

    steps = checkpoint.get("steps", [])
    if not isinstance(steps, list):
        raise ValueError("checkpoint.steps must be an array")
    for step in steps:
        if not isinstance(step, Mapping):
            raise ValueError("checkpoint step must be an object")
        if step.get("status") != "Passed":
            value = step.get("step_id")
            return str(value) if value is not None else None
    return None
