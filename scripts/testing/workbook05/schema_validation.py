"""Validate Workbook 05 JSON controls against versioned JSON Schemas."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from jsonschema import Draft202012Validator


@dataclass(frozen=True)
class ValidationIssue:
    """One deterministic, display-ready schema validation problem."""

    json_path: str
    message: str


def _json_path(parts: list[object]) -> str:
    """Convert jsonschema's path deque into a beginner-readable JSON path."""

    value = "$"
    for part in parts:
        value += f"[{part}]" if isinstance(part, int) else f".{part}"
    return value


def validate_json_file(instance_path: Path, schema_path: Path) -> list[ValidationIssue]:
    """Return every schema issue in stable path/message order."""

    instance = json.loads(instance_path.read_text(encoding="utf-8-sig"))
    schema = json.loads(schema_path.read_text(encoding="utf-8-sig"))
    validator = Draft202012Validator(schema)
    errors = sorted(
        validator.iter_errors(instance),
        key=lambda error: (tuple(str(part) for part in error.absolute_path), error.message),
    )
    return [
        ValidationIssue(_json_path(list(error.absolute_path)), error.message)
        for error in errors
    ]


def assert_valid_json_file(instance_path: Path, schema_path: Path) -> None:
    """Raise one exception containing all problems when validation fails."""

    issues = validate_json_file(instance_path, schema_path)
    if not issues:
        return
    details = "\n".join(f"- {issue.json_path}: {issue.message}" for issue in issues)
    raise ValueError(f"JSON validation failed for {instance_path}:\n{details}")
