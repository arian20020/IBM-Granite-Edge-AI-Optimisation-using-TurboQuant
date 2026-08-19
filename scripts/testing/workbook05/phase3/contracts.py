"""Validate Workbook 05 Phase 3 evidence records against closed schemas."""

from __future__ import annotations

import json
from pathlib import Path
from typing import TYPE_CHECKING, Any, Mapping

if TYPE_CHECKING:
    # Type-only imports keep editor and checker support without making live C1
    # acquisition depend on the repository-only schema-validation environment.
    from jsonschema.exceptions import ValidationError

    from scripts.testing.workbook05.schema_validation import ValidationIssue


SCHEMA_NAMES: dict[str, str] = {
    "prerequisite-proof": "phase3-prerequisite-proof.schema.json",
    "model-asset-lock": "model-asset-lock.schema.json",
    "model-conversion-record": "model-conversion-record.schema.json",
}


def _json_path(parts: list[object]) -> str:
    """Convert a jsonschema path into a deterministic, readable JSON path."""

    value = "$"
    for part in parts:
        value += f"[{part}]" if isinstance(part, int) else f".{part}"
    return value


def _stable_error_key(error: ValidationError) -> tuple[tuple[str, ...], str]:
    """Sort validation failures independently of dictionary traversal order."""

    return (tuple(str(part) for part in error.absolute_path), error.message)


def _schema_path(record_type: str, repository_root: Path) -> Path:
    """Resolve the reviewed schema for one supported Phase 3 record type."""

    schema_name = SCHEMA_NAMES.get(record_type)
    if schema_name is None:
        raise ValueError(f"Unsupported Phase 3 record type: {record_type}")

    return (
        repository_root
        / "experiments"
        / "granite_turboquant_intel"
        / "schemas"
        / "workbook05"
        / schema_name
    )


def validate_phase3_record(
    record_type: str,
    payload: Mapping[str, Any],
    repository_root: Path,
) -> list[ValidationIssue]:
    """Return every schema problem in stable path/message order."""

    # Schema validation belongs to the repository-validation environment. Delay
    # both the jsonschema package and its ValidationIssue adapter so importing a
    # live acquisition subcommand does not pull jsonschema transitively into the
    # independently accepted conversion environment.
    try:
        from jsonschema import Draft202012Validator, FormatChecker

        from scripts.testing.workbook05.schema_validation import ValidationIssue
    except ImportError as error:
        raise ValueError(
            "Phase 3 schema validation requires the repository validator "
            "environment with jsonschema installed."
        ) from error

    schema_path = _schema_path(record_type, repository_root)
    schema = json.loads(schema_path.read_text(encoding="utf-8-sig"))
    Draft202012Validator.check_schema(schema)
    validator = Draft202012Validator(schema, format_checker=FormatChecker())
    errors = sorted(validator.iter_errors(payload), key=_stable_error_key)
    return [
        ValidationIssue(_json_path(list(error.absolute_path)), error.message)
        for error in errors
    ]


def assert_phase3_record(
    record_type: str,
    payload: Mapping[str, Any],
    repository_root: Path,
) -> None:
    """Raise one display-ready exception containing all schema failures."""

    issues = validate_phase3_record(record_type, payload, repository_root)
    if not issues:
        return

    details = "\n".join(
        f"- {issue.json_path}: {issue.message}" for issue in issues
    )
    raise ValueError(
        f"Phase 3 JSON validation failed for {record_type}:\n{details}"
    )
