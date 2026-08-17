"""Atomically materialise a live dependency decision and normal inventory."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
from typing import Any, Mapping, Sequence

from scripts.testing.workbook05.phase3.dependency_decision import (
    build_live_dependency_preflight_record,
)
from scripts.testing.workbook05.phase3.dependency_lock import (
    DIRECT_NORMAL_VERSIONS,
    VCS_PACKAGE_NAMES,
    parse_hash_locked_requirements,
    parse_normal_install_report,
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
        raise FileExistsError(f"Final JSON evidence already exists: {path}")
    if temporary.exists():
        raise FileExistsError(f"Temporary JSON evidence already exists: {temporary}")
    if not path.parent.is_dir():
        raise FileNotFoundError(f"JSON evidence parent does not exist: {path.parent}")

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


def _normal_package_inventory(observation: Mapping[str, Any]) -> dict[str, Any]:
    """Recompute the ordinary package set from the retained lock and pip report."""

    lock_text = observation.get("lock_text")
    report = observation.get("normal_install_report")
    if not isinstance(lock_text, str):
        raise ValueError("Observation lock_text must be UTF-8 text.")
    if not isinstance(report, Mapping):
        raise ValueError("Observation normal_install_report must be one object.")

    lock = parse_hash_locked_requirements(
        lock_text,
        required_direct_versions=DIRECT_NORMAL_VERSIONS,
        forbidden_names=VCS_PACKAGE_NAMES,
    )
    packages = parse_normal_install_report(report, lock)
    return {
        "schema_version": "1.0",
        "record_type": "dependency-normal-package-inventory",
        "packages": [
            {"name": package.name, "version": package.version}
            for package in sorted(packages, key=lambda value: value.name)
        ],
    }


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--observation", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args(argv)

    observation = _load_object(arguments.observation)
    decision = build_live_dependency_preflight_record(observation)
    inventory = _normal_package_inventory(observation)
    inventory_path = arguments.output.parent / "reports" / "normal-packages.json"

    # Check both final locations before publishing either record. If the second
    # write fails unexpectedly, remove only the decision created by this call so
    # an incomplete evidence pair cannot be mistaken for a completed boundary.
    if arguments.output.exists() or arguments.output.with_name(
        arguments.output.name + ".tmp"
    ).exists():
        raise FileExistsError(
            f"Decision or temporary decision already exists: {arguments.output}"
        )
    if inventory_path.exists() or inventory_path.with_name(
        inventory_path.name + ".tmp"
    ).exists():
        raise FileExistsError(
            f"Normal package inventory already exists: {inventory_path}"
        )

    _write_atomic(inventory_path, inventory)
    try:
        _write_atomic(arguments.output, decision)
    except Exception:
        # This process created the inventory and no decision was published, so
        # remove only that incomplete companion record before propagating failure.
        if inventory_path.exists():
            inventory_path.unlink()
        raise
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
