"""Validate both reviewed source trees as data and write the ordinary input."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
from typing import Any, Mapping, Sequence

from scripts.testing.workbook05.phase3.dependency_source_contract import (
    validate_reviewed_source_contracts,
    write_reviewed_normal_requirement_input,
)


def _write_atomic_json(path: Path, value: Mapping[str, Any]) -> None:
    temporary = path.with_name(path.name + ".tmp")
    if path.exists():
        raise FileExistsError(f"Source-contract output already exists: {path}")
    if temporary.exists():
        raise FileExistsError(f"Temporary source-contract output exists: {temporary}")
    if not path.parent.is_dir():
        raise FileNotFoundError(f"Output parent does not exist: {path.parent}")
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
    parser.add_argument("--optimum-root", type=Path, required=True)
    parser.add_argument("--optimum-intel-root", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--requirements-output", type=Path, required=True)
    arguments = parser.parse_args(argv)

    report = validate_reviewed_source_contracts(
        arguments.optimum_root,
        arguments.optimum_intel_root,
    )
    write_reviewed_normal_requirement_input(arguments.requirements_output)
    _write_atomic_json(arguments.report, report)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
