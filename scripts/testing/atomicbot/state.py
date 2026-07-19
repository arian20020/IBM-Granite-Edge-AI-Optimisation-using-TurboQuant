"""Crash-safe AtomicBot run state persistence."""

from __future__ import annotations

import json
import os
import tempfile
from pathlib import Path


ROW_STATUSES = {"pending", "running", "pilot-complete", "complete", "passed", "failed", "blocked", "unsupported", "n/a"}


def _validate(state: dict) -> None:
    for test_id, status in state.get("rows", {}).items():
        if status not in ROW_STATUSES:
            raise ValueError(f"invalid row status for {test_id}: {status}")


def load_state(path: Path) -> dict:
    state = json.loads(path.read_text(encoding="utf-8"))
    _validate(state)
    return state


def checkpoint(path: Path, state: dict) -> None:
    _validate(state)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_name: str | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w", encoding="utf-8", suffix=".tmp", dir=path.parent, delete=False
        ) as temporary:
            temporary_name = temporary.name
            json.dump(state, temporary, indent=2, sort_keys=True)
            temporary.write("\n")
            temporary.flush()
            os.fsync(temporary.fileno())
        os.replace(temporary_name, path)
    finally:
        if temporary_name and os.path.exists(temporary_name):
            os.unlink(temporary_name)
