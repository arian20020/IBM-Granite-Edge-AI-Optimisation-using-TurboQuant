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
OFFLINE_FIXTURE_REASON = (
    "Offline fixture evidence only; no source clone, package resolution, "
    "installation, import, CLI, or model operation was executed."
)


def _load_object(path: Path) -> dict[str, object]:
    """Load one UTF-8 JSON object and reject non-object roots."""

    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object: {path}")
    return value


def _validate_record(
    record: dict[str, object],
    repository_root: Path,
) -> None:
    """Reject any decision that drifts from the closed C1 schema."""

    schema_path = (
        repository_root
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


def build_and_validate(
    observation_path: Path,
    repository_root: Path,
    *,
    offline_fixture: bool = False,
) -> dict[str, object]:
    """Build one decision and validate its final truthful classification.

    A repository rehearsal may prove that the orchestration and evidence shape
    work, but it cannot prove that packages were actually resolved or installed.
    In that explicit mode, the otherwise complete synthetic observation is
    therefore classified ``Blocked`` while every later authorisation stays
    false. The underlying identities remain unchanged so an independent
    validator can recompute and compare the record.
    """

    repository = repository_root.resolve(strict=True)
    observation = _load_object(observation_path.resolve(strict=True))
    record = build_dependency_preflight_record(observation)

    # Preserve all calculated identities, checks, hashes, and non-claims while
    # preventing synthetic fixture evidence from becoming a live acceptance.
    if offline_fixture:
        record["status"] = "Blocked"
        record["reasons"] = [OFFLINE_FIXTURE_REASON]
        for key in (
            "model_download_authorised",
            "granite_model_test_authorised",
            "activation_claim_authorised",
            "packed_storage_claim_authorised",
            "performance_claim_authorised",
            "quality_claim_authorised",
        ):
            record[key] = False

    _validate_record(record, repository)
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
    """Build the decision and return process success only for an honest result."""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--observation", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument(
        "--offline-fixture",
        action="store_true",
        help=(
            "Classify a synthetic repository rehearsal as Blocked while "
            "returning zero after its schema-valid evidence is written."
        ),
    )
    arguments = parser.parse_args(list(argv) if argv is not None else None)

    record = build_and_validate(
        arguments.observation,
        arguments.repository_root,
        offline_fixture=arguments.offline_fixture,
    )
    _write_atomic(arguments.output, record)
    print(
        f"Dependency preflight decision: {record['status']} -> {arguments.output}"
    )

    # A blocked offline rehearsal is a successful test execution, not accepted
    # live evidence. Any other non-Passed decision remains a nonzero process.
    if record["status"] == "Passed":
        return 0
    if arguments.offline_fixture and record["status"] == "Blocked":
        return 0
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
