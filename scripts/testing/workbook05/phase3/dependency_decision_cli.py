"""Atomically materialise a live dependency-preflight decision from observation JSON."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
from typing import Any, Mapping, Sequence

from scripts.testing.workbook05.phase3.dependency_decision import (
    build_live_dependency_preflight_record,
)


def _load_object(path: Path) -> dict[str, Any]:
    if not path.is_file() or path.is_symlink():
        raise ValueError(f"Observation must be one regular file: {path}")
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError("Observation must contain one JSON object.")
    return value


def _write_atomic(path: Path, value: Mapping[str, Any]) -> None:
    temporary = path.with_name(path.name + ".tmp")
    if path.exists():
        raise FileExistsError(f"Decision already exists: {path}")
    if temporary.exists():
        raise FileExistsError(f"Temporary decision already exists: {temporary}")
    if not path.parent.is_dir():
        raise FileNotFoundError(f"Decision parent does not exist: {path.parent}")

    payload = json.dumps(value, indent=2, sort_keys=True) + "\n"
    created = False
    try:
        with temporary.open("x", encoding="utf-8", newline="\n") as handle:
            created = True
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())
        os.rename(temporary, path)
    except Exception:
        if created and temporary.exists():
            temporary.unlink()
        raise


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--observation", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args(argv)

    observation = _load_object(arguments.observation)
    decision = build_live_dependency_preflight_record(observation)
    _write_atomic(arguments.output, decision)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
