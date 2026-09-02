"""Create a complete text-only manifest for one reviewed Git source tree."""

from __future__ import annotations

import argparse
import csv
import json
import re
from pathlib import Path, PurePosixPath
from typing import Iterable

from scripts.testing.workbook05.phase3.hashing import sha256_file, sha256_tree


_FULL_COMMIT = re.compile(r"^[0-9a-f]{40}$")


def _tracked_paths(root: Path, file_list: Path) -> list[Path]:
    """Resolve a newline-delimited `git ls-files` list without path escape."""

    resolved_root = root.resolve(strict=True)
    if not resolved_root.is_dir() or root.is_symlink():
        raise ValueError(f"Source root must be one normal directory: {root}")

    values: list[Path] = []
    seen: set[str] = set()
    for line_number, raw_line in enumerate(
        file_list.read_text(encoding="utf-8-sig").splitlines(),
        start=1,
    ):
        if not raw_line or raw_line != raw_line.strip() or "\\" in raw_line:
            raise ValueError(f"Unsafe tracked path at line {line_number}.")
        parsed = PurePosixPath(raw_line)
        if (
            parsed.is_absolute()
            or parsed.as_posix() != raw_line
            or any(part in {"", ".", ".."} for part in parsed.parts)
        ):
            raise ValueError(f"Unsafe tracked path at line {line_number}.")
        key = raw_line.casefold()
        if key in seen:
            raise ValueError(f"Duplicate canonical tracked path: {raw_line}")
        seen.add(key)
        candidate = resolved_root.joinpath(*parsed.parts)
        if not candidate.is_file() or candidate.is_symlink():
            raise ValueError(f"Tracked source is not a normal file: {raw_line}")
        values.append(candidate)

    if not values:
        raise ValueError("The tracked source-file list is empty.")
    return values


def create_source_manifest(
    *,
    name: str,
    repository: str,
    origin: str,
    commit: str,
    root: Path,
    file_list: Path,
    csv_output: Path,
    json_output: Path,
) -> dict[str, object]:
    """Write complete per-file and aggregate source-tree identities."""

    if not all(isinstance(value, str) and value for value in (name, repository, origin)):
        raise ValueError("Source name, repository, and origin must be non-empty.")
    if not _FULL_COMMIT.fullmatch(commit):
        raise ValueError("Source commit must be a full lowercase Git commit.")

    resolved_root = root.resolve(strict=True)
    files = _tracked_paths(root, file_list)
    rows: list[dict[str, object]] = []
    for path in sorted(
        files,
        key=lambda value: (
            value.relative_to(resolved_root).as_posix().casefold(),
            value.relative_to(resolved_root).as_posix(),
        ),
    ):
        rows.append(
            {
                "relative_path": path.relative_to(resolved_root).as_posix(),
                "size_bytes": path.stat().st_size,
                "sha256": sha256_file(path),
            }
        )

    csv_output.parent.mkdir(parents=True, exist_ok=True)
    if csv_output.exists() or json_output.exists():
        raise FileExistsError("Source-manifest output already exists.")
    with csv_output.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(
            stream,
            fieldnames=["relative_path", "size_bytes", "sha256"],
            lineterminator="\n",
        )
        writer.writeheader()
        writer.writerows(rows)

    record: dict[str, object] = {
        "schema_version": "1.0",
        "name": name,
        "repository": repository,
        "origin": origin,
        "commit": commit,
        "clean": True,
        "file_count": len(rows),
        "aggregate_sha256": sha256_tree(resolved_root, files),
        "file_manifest_path": csv_output.name,
    }
    json_output.parent.mkdir(parents=True, exist_ok=True)
    json_output.write_text(
        json.dumps(record, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    return record


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--name", required=True)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--origin", required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--file-list", type=Path, required=True)
    parser.add_argument("--csv-output", type=Path, required=True)
    parser.add_argument("--json-output", type=Path, required=True)
    arguments = parser.parse_args(list(argv) if argv is not None else None)

    record = create_source_manifest(
        name=arguments.name,
        repository=arguments.repository,
        origin=arguments.origin,
        commit=arguments.commit,
        root=arguments.root,
        file_list=arguments.file_list,
        csv_output=arguments.csv_output,
        json_output=arguments.json_output,
    )
    print(
        f"Recorded {record['file_count']} files for {record['name']} at "
        f"{record['commit']}."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
