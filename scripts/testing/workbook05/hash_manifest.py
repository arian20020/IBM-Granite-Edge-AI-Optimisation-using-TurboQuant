"""Create and verify deterministic SHA-256 manifests for Workbook 05 evidence."""

from __future__ import annotations

import argparse
import hashlib
import re
from pathlib import Path, PurePosixPath
from typing import Iterable


LINE_PATTERN = re.compile(r"^([0-9a-f]{64})  (.+)$")


def _files(root: Path, destination: Path) -> list[Path]:
    """Return regular evidence files in stable POSIX-relative order."""

    resolved_destination = destination.resolve()
    return sorted(
        (
            candidate
            for candidate in root.rglob("*")
            if candidate.is_file() and candidate.resolve() != resolved_destination
        ),
        key=lambda candidate: candidate.relative_to(root).as_posix(),
    )


def _sha256(path: Path) -> str:
    """Hash one file without loading the entire artifact into memory."""

    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_hash_manifest(root: Path, destination: Path) -> list[tuple[str, str]]:
    """Hash every regular file except the manifest itself and write stable rows."""

    root = root.resolve()
    destination = destination.resolve()
    destination.parent.mkdir(parents=True, exist_ok=True)
    records = [
        (_sha256(candidate), candidate.relative_to(root).as_posix())
        for candidate in _files(root, destination)
    ]
    destination.write_text(
        "".join(f"{digest}  {relative}\n" for digest, relative in records),
        encoding="utf-8",
        newline="\n",
    )
    return records


def verify_hash_manifest(root: Path, manifest_path: Path) -> list[str]:
    """Return every malformed, missing, added, duplicate, or changed-file problem."""

    root = root.resolve()
    manifest_path = manifest_path.resolve()
    issues: list[str] = []
    expected: dict[str, str] = {}
    if not manifest_path.is_file():
        return [f"missing hash manifest: {manifest_path}"]

    for line_number, raw_line in enumerate(manifest_path.read_text(encoding="utf-8-sig").splitlines(), start=1):
        match = LINE_PATTERN.fullmatch(raw_line)
        if not match:
            issues.append(f"malformed hash row {line_number}")
            continue
        digest, relative = match.groups()
        parsed = PurePosixPath(relative)
        if parsed.is_absolute() or ".." in parsed.parts or "." in parsed.parts or "\\" in relative:
            issues.append(f"path traversal in hash row {line_number}: {relative}")
            continue
        if relative in expected:
            issues.append(f"duplicate hash entry: {relative}")
            continue
        expected[relative] = digest

    actual_paths = {
        candidate.relative_to(root).as_posix(): candidate
        for candidate in _files(root, manifest_path)
    }
    for relative, digest in sorted(expected.items()):
        candidate = actual_paths.get(relative)
        if candidate is None:
            issues.append(f"missing file: {relative}")
        elif _sha256(candidate) != digest:
            issues.append(f"changed file: {relative}")
    for relative in sorted(set(actual_paths) - set(expected)):
        issues.append(f"added file: {relative}")
    return issues


def main(argv: Iterable[str] | None = None) -> int:
    """CLI used by the Windows preflight orchestrator."""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args(list(argv) if argv is not None else None)
    records = write_hash_manifest(arguments.root, arguments.output)
    print(f"Hashed {len(records)} evidence files into {arguments.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
