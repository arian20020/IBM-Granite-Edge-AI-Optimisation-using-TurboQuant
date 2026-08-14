"""Build and schema-validate one C1 dependency-preflight decision.

The PowerShell collector writes raw command observations as JSON. This module
turns those observations into the closed repository record, verifies the exact
hash-locked package/source relationships, validates the Draft 2020-12 schema,
and writes the decision atomically.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Iterable

from jsonschema import Draft202012Validator, FormatChecker

from scripts.testing.workbook05.phase3.dependency_lock import (
    build_dependency_preflight_record,
)


SCHEMA_NAME = "conversion-dependency-preflight.schema.json"


def _load_object(path: Path) -> dict[str, object]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object: {path}")
    return value


def build_and_validate(
    observation_path: Path,
    repository_root: Path,
) -> dict[str, object]:
    """Build one decision and reject any schema drift before writing it."""

    repository = repository_root.resolve(strict=True)
    observation = _load_object(observation_path.resolve(strict=True))
    record = build_dependency_preflight_record(observation)

    schema_path = (
        repository
        / "experiments"
        / "granite_turboquant_intel"
        / "schemas"
        / "workbook05"
        / SCHEMA_NAME
    )
    schema = _load_object(schema_path)
    errors = sorted(
        Draft202012Validator(
            schema,
            format_checker=FormatChecker(),
        ).iter_errors(record),
        key=lambda error: (
            tuple(str(part) for part in error.absolute_path),
            error.message,
        ),
    )
    if errors:
        details = "; ".join(
            f"$.{'/'.join(str(part) for part in error.absolute_path)}: "
            f"{error.message}"
            for error in errors
        )
        raise ValueError(f"Dependency-preflight decision is schema invalid: {details}")
    return record


def _write_atomic(path: Path, value: dict[str, object]) -> None:
    """Write UTF-8 JSON through a same-directory temporary file."""

    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    if path.exists() or temporary.exists():
        raise FileExistsError(f"Decision destination already exists: {path}")
    temporary.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    temporary.replace(path)


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--observation", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args(list(argv) if argv is not None else None)

    record = build_and_validate(
        arguments.observation,
        arguments.repository_root,
    )
    _write_atomic(arguments.output, record)
    print(
        f"Dependency preflight decision: {record['status']} -> {arguments.output}"
    )
    return 0 if record["status"] == "Passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
