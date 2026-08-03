"""Atomically record and resume Workbook 05 campaign phases."""

from __future__ import annotations

import argparse
import json
import os
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable, Mapping


ALLOWED_STATUSES = {"Not started", "In progress", "Passed", "Failed", "Blocked"}
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")


class CheckpointConflictError(RuntimeError):
    """Raised when another writer or stale caller tries to update state."""


def load_checkpoint(path: Path) -> dict[str, Any]:
    """Read one checkpoint using BOM-tolerant UTF-8."""

    return json.loads(path.read_text(encoding="utf-8-sig"))


def first_incomplete_step(checkpoint: Mapping[str, Any]) -> str | None:
    """Return the first phase that has not passed, or None when complete."""

    status_by_id = {step["step_id"]: step["status"] for step in checkpoint["steps"]}
    for step_id in checkpoint["phase_order"]:
        if status_by_id[step_id] != "Passed":
            return step_id
    return None


def record_step(
    path: Path,
    *,
    expected_generation: int,
    step_id: str,
    status: str,
    evidence_sha256: str,
) -> dict[str, Any]:
    """Update one phase under an exclusive lock and atomically replace the file."""

    if status not in ALLOWED_STATUSES:
        raise ValueError(f"Unsupported checkpoint status: {status}")
    if status == "Passed" and not SHA256_PATTERN.fullmatch(evidence_sha256):
        raise ValueError("Passed checkpoint steps require a lowercase SHA-256 evidence hash")
    if evidence_sha256 and not SHA256_PATTERN.fullmatch(evidence_sha256):
        raise ValueError("evidence_sha256 must be empty or a lowercase SHA-256 value")

    lock_path = path.with_suffix(path.suffix + ".lock")
    temporary_path = path.with_suffix(path.suffix + ".tmp")
    try:
        descriptor = os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError as error:
        raise CheckpointConflictError(f"Checkpoint is already locked: {lock_path}") from error

    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as lock_file:
            lock_file.write(str(os.getpid()))
            lock_file.flush()
            os.fsync(lock_file.fileno())

        checkpoint = load_checkpoint(path)
        if checkpoint["generation"] != expected_generation:
            raise CheckpointConflictError(
                f"Expected generation {expected_generation}, found {checkpoint['generation']}"
            )
        matching = [step for step in checkpoint["steps"] if step["step_id"] == step_id]
        if len(matching) != 1:
            raise ValueError(f"Unknown or duplicate checkpoint step: {step_id}")

        matching[0]["status"] = status
        matching[0]["evidence_sha256"] = evidence_sha256
        checkpoint["generation"] += 1
        checkpoint["updated_at_utc"] = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")

        with temporary_path.open("w", encoding="utf-8", newline="\n") as handle:
            json.dump(checkpoint, handle, indent=2)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary_path, path)
        return checkpoint
    finally:
        temporary_path.unlink(missing_ok=True)
        lock_path.unlink(missing_ok=True)


def main(argv: Iterable[str] | None = None) -> int:
    """Update a runtime checkpoint copy from PowerShell orchestration."""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--path", type=Path, required=True)
    parser.add_argument("--expected-generation", type=int, required=True)
    parser.add_argument("--step-id", required=True)
    parser.add_argument("--status", choices=sorted(ALLOWED_STATUSES), required=True)
    parser.add_argument("--evidence-sha256", default="")
    arguments = parser.parse_args(list(argv) if argv is not None else None)
    updated = record_step(
        arguments.path,
        expected_generation=arguments.expected_generation,
        step_id=arguments.step_id,
        status=arguments.status,
        evidence_sha256=arguments.evidence_sha256,
    )
    print(json.dumps(updated, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
