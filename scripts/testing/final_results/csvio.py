"""Deterministic CSV and JSON output for final-results artifacts."""

from __future__ import annotations

import csv
import json
from collections.abc import Iterable, Mapping, Sequence
from pathlib import Path

from jsonschema import Draft202012Validator


def _csv_value(value: object) -> object:
    if value is None:
        return ""
    if isinstance(value, bool):
        return str(value).lower()
    return value


def write_csv(
    path: Path,
    rows: Iterable[Mapping[str, object]],
    fieldnames: Sequence[str],
) -> None:
    """Write UTF-8 CSV with declared columns and POSIX newlines."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=fieldnames,
            extrasaction="ignore",
            lineterminator="\n",
        )
        writer.writeheader()
        for row in rows:
            writer.writerow({name: _csv_value(row.get(name)) for name in fieldnames})


def write_json(path: Path, payload: object) -> None:
    """Write stable UTF-8 JSON with sorted keys and a terminating newline."""
    path.parent.mkdir(parents=True, exist_ok=True)
    serialized = json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True)
    path.write_text(f"{serialized}\n", encoding="utf-8", newline="\n")


def _json_pointer(path: Iterable[object]) -> str:
    parts = (str(part).replace("~", "~0").replace("/", "~1") for part in path)
    return "/" + "/".join(parts)


def validate_json(instance: object, schema_path: Path) -> list[str]:
    """Return stable JSON-pointer-like paths for Draft 2020-12 validation errors."""
    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    validator = Draft202012Validator(schema)
    return sorted({_json_pointer(error.absolute_path) for error in validator.iter_errors(instance)})
