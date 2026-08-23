"""Command-line validation for Workbook 05 dependency locks and pip reports."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any, Mapping, Sequence

from scripts.testing.workbook05.phase3.dependency_lock import (
    LockedDistribution,
    parse_hash_locked_requirements,
    parse_install_report_against_lock,
)
from scripts.testing.workbook05.phase3.dependency_preflight import DependencyPackage


def _required_versions(values: Sequence[str]) -> dict[str, str]:
    """Parse repeated exact `name==version` policy arguments."""

    result: dict[str, str] = {}
    for value in values:
        if value.count("==") != 1:
            raise ValueError(
                f"Required-direct value must use exact name==version syntax: {value}"
            )
        name, version = value.split("==", 1)
        name = name.strip()
        version = version.strip()
        if not name or not version:
            raise ValueError(
                f"Required-direct value must use exact name==version syntax: {value}"
            )
        if name in result:
            raise ValueError(f"Duplicate required-direct distribution: {name}")
        result[name] = version
    return result


def _read_text(path: Path, label: str) -> str:
    """Read one regular UTF-8 file without newline translation."""

    if not path.is_file() or path.is_symlink():
        raise ValueError(f"{label} must be one regular file: {path}")
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError as error:
        raise ValueError(f"{label} must contain UTF-8 text: {path}") from error


def _locked_package_records(
    packages: Sequence[LockedDistribution],
) -> list[dict[str, object]]:
    """Return deterministic text-only lock package evidence."""

    return [
        {
            "name": package.name,
            "version": package.version,
            "hashes": list(package.hashes),
            "marker": package.marker,
        }
        for package in packages
    ]


def _installed_package_records(
    packages: Sequence[DependencyPackage],
) -> list[dict[str, object]]:
    """Return deterministic text-only installed package evidence."""

    return [
        {
            "name": package.name,
            "version": package.version,
            "source_identity": package.source_identity,
            "direct": package.direct,
        }
        for package in packages
    ]


def _write_atomic_json(path: Path, value: Mapping[str, object]) -> None:
    """Write one new JSON file atomically and reject all prior state."""

    temporary = path.with_name(path.name + ".tmp")
    if path.exists():
        raise FileExistsError(f"Output already exists: {path}")
    if temporary.exists():
        raise FileExistsError(f"Temporary output already exists: {temporary}")
    if not path.parent.is_dir():
        raise FileNotFoundError(f"Output parent does not exist: {path.parent}")

    payload = json.dumps(value, indent=2, sort_keys=True) + "\n"
    try:
        with temporary.open("x", encoding="utf-8", newline="\n") as handle:
            handle.write(payload)
            handle.flush()
        temporary.replace(path)
    except Exception:
        # A failed write is negative evidence. Remove only the temporary file
        # created by this invocation; never overwrite a prior attempt.
        if temporary.exists() and not path.exists():
            temporary.unlink()
        raise


def _policy(arguments: argparse.Namespace) -> tuple[dict[str, str], frozenset[str]]:
    """Return the explicit package policy from parsed CLI arguments."""

    return (
        _required_versions(arguments.required_direct),
        frozenset(arguments.forbidden_name),
    )


def _validate_lock(arguments: argparse.Namespace) -> dict[str, object]:
    """Validate one lock and return its recomputed identity."""

    lock_path = Path(arguments.lock)
    lock_text = _read_text(lock_path, "Dependency lock")
    required, forbidden = _policy(arguments)
    packages = parse_hash_locked_requirements(
        lock_text,
        required_direct_versions=required,
        forbidden_names=forbidden,
    )
    return {
        "schema_version": "1.0",
        "record_type": "dependency-lock-validation",
        "lock_path": lock_path.name,
        "lock_sha256": hashlib.sha256(lock_text.encode("utf-8")).hexdigest(),
        "package_count": len(packages),
        "packages": _locked_package_records(packages),
    }


def _validate_report(arguments: argparse.Namespace) -> dict[str, object]:
    """Validate one pip report against one lock and bind actual artifacts."""

    lock_path = Path(arguments.lock)
    report_path = Path(arguments.report)
    lock_text = _read_text(lock_path, "Dependency lock")
    report_text = _read_text(report_path, "pip installation report")
    try:
        report = json.loads(report_text)
    except json.JSONDecodeError as error:
        raise ValueError(
            f"pip installation report must contain one JSON object: {report_path}"
        ) from error
    if not isinstance(report, Mapping):
        raise ValueError(
            f"pip installation report must contain one JSON object: {report_path}"
        )

    required, forbidden = _policy(arguments)
    lock = parse_hash_locked_requirements(
        lock_text,
        required_direct_versions=required,
        forbidden_names=forbidden,
    )
    packages = parse_install_report_against_lock(
        report,
        lock,
        forbidden_names=forbidden,
    )
    return {
        "schema_version": "1.0",
        "record_type": "dependency-install-report-validation",
        "lock_path": lock_path.name,
        "lock_sha256": hashlib.sha256(lock_text.encode("utf-8")).hexdigest(),
        "report_path": report_path.name,
        "report_sha256": hashlib.sha256(report_text.encode("utf-8")).hexdigest(),
        "package_count": len(packages),
        "packages": _installed_package_records(packages),
    }


def _add_policy_arguments(parser: argparse.ArgumentParser) -> None:
    """Add the common explicit package-policy arguments."""

    parser.add_argument(
        "--required-direct",
        action="append",
        default=[],
        metavar="NAME==VERSION",
        help="Require one exact distribution version; repeat as needed.",
    )
    parser.add_argument(
        "--forbidden-name",
        action="append",
        default=[],
        metavar="NAME",
        help="Reject one canonical distribution name; repeat as needed.",
    )


def _parser() -> argparse.ArgumentParser:
    """Build the deterministic two-command CLI."""

    parser = argparse.ArgumentParser(
        description="Validate Workbook 05 hash locks and pip installation reports."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    lock_parser = subparsers.add_parser(
        "validate-lock",
        help="Validate one exact hash lock.",
    )
    lock_parser.add_argument("--lock", required=True)
    lock_parser.add_argument("--output", required=True)
    _add_policy_arguments(lock_parser)
    lock_parser.set_defaults(handler=_validate_lock)

    report_parser = subparsers.add_parser(
        "validate-report",
        help="Validate one pip install report against one exact lock.",
    )
    report_parser.add_argument("--lock", required=True)
    report_parser.add_argument("--report", required=True)
    report_parser.add_argument("--output", required=True)
    _add_policy_arguments(report_parser)
    report_parser.set_defaults(handler=_validate_report)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    """Run one validation command and write its result atomically."""

    arguments = _parser().parse_args(argv)
    record = arguments.handler(arguments)
    _write_atomic_json(Path(arguments.output), record)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
