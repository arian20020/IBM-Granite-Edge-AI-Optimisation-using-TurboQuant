#!/usr/bin/env python3
"""Plan, copy, and verify a copy-only external testing-history archive."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import shutil
import stat
import subprocess
import sys
import uuid
from collections import Counter
from dataclasses import dataclass
from enum import Enum
from pathlib import Path, PurePosixPath
from typing import Iterable, Mapping, Sequence


PLAN_FIELDS = (
    "source_root_id",
    "source_root",
    "source_branch",
    "source_head",
    "source_status_sha256",
    "source_path",
    "archive_path",
    "size_bytes",
    "sha256",
    "journal",
)
MIN_FREE_AFTER_COPY = 64 * 1024 * 1024


class ArchiveError(ValueError):
    """Raised when an archive operation cannot be completed safely."""


class JournalState(str, Enum):
    PLANNED = "planned"
    COPIED = "copied"
    VERIFIED = "verified"
    MIGRATED = "migrated"
    REMOVED = "removed"


@dataclass(slots=True)
class ArchiveEntry:
    source_root_id: str
    source_root: Path
    source_branch: str
    source_head: str
    source_status_sha256: str
    source_path: str
    archive_path: str
    size_bytes: int
    sha256: str
    journal: tuple[JournalState, ...] = (JournalState.PLANNED,)


@dataclass(slots=True)
class ArchivePlan:
    entries: tuple[ArchiveEntry, ...]


@dataclass(frozen=True, slots=True)
class VerificationResult:
    valid: bool
    verified_count: int
    unverified: tuple[str, ...]
    total_bytes: int
    archive_manifest_sha256: str


def _git_bytes(root: Path, *args: str) -> bytes:
    try:
        return subprocess.run(
            ["git", *args], cwd=root, check=True, capture_output=True
        ).stdout
    except (OSError, subprocess.CalledProcessError) as error:
        raise ArchiveError(f"source identity check failed for {root}") from error


def _git_text(root: Path, *args: str) -> str:
    return _git_bytes(root, *args).decode(
        "utf-8", errors="surrogateescape"
    ).strip()


def _status_hash(root: Path) -> str:
    raw = _git_bytes(root, "status", "--porcelain=v2", "-z", "--ignored")
    return hashlib.sha256(raw).hexdigest()


def _io_path(path: Path) -> Path:
    """Return a Windows extended-length spelling for absolute filesystem I/O."""

    candidate = Path(path)
    if os.name != "nt" or not candidate.is_absolute():
        return candidate
    value = str(candidate)
    if value.startswith("\\\\?\\"):
        return candidate
    if value.startswith("\\\\"):
        return Path("\\\\?\\UNC\\" + value[2:])
    return Path("\\\\?\\" + value)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    try:
        with _io_path(path).open("rb") as handle:
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
    except OSError as error:
        raise ArchiveError(f"cannot hash archive path: {path}") from error
    return digest.hexdigest()


def _is_reparse(path: Path) -> bool:
    candidate = _io_path(path)
    try:
        details = candidate.lstat()
    except OSError:
        return False
    attributes = getattr(details, "st_file_attributes", 0)
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return candidate.is_symlink() or bool(attributes & reparse_flag)


def _existing_components(path: Path) -> Iterable[Path]:
    current = Path(path.anchor) if path.anchor else Path()
    for part in path.parts[1:] if path.anchor else path.parts:
        current = current / part
        if os.path.lexists(_io_path(current)):
            yield current
        else:
            break


def _reject_reparse(path: Path) -> None:
    for component in _existing_components(path):
        if _is_reparse(component):
            raise ArchiveError(f"reparse point is not allowed: {component}")


def _safe_relative(value: object) -> str:
    raw = str(value or "").replace("\\", "/")
    candidate = PurePosixPath(raw)
    if (
        not raw
        or raw in {".", "/"}
        or candidate.is_absolute()
        or any(part in {"", ".", ".."} for part in candidate.parts)
        or (candidate.parts and ":" in candidate.parts[0])
    ):
        raise ArchiveError(f"unsafe relative path: {value}")
    return candidate.as_posix()


def _same_path(left: Path, right: Path) -> bool:
    return os.path.normcase(str(left)) == os.path.normcase(str(right))


def _validate_identity(entry: ArchiveEntry) -> None:
    raw_root = entry.source_root
    if not raw_root.is_absolute():
        raise ArchiveError(f"source identity root is not absolute: {raw_root}")
    _reject_reparse(raw_root)
    try:
        root = raw_root.resolve(strict=True)
    except OSError as error:
        raise ArchiveError(f"source identity root is unavailable: {raw_root}") from error
    if not root.is_dir():
        raise ArchiveError(f"source identity root is not a directory: {root}")
    top = Path(_git_text(root, "rev-parse", "--show-toplevel")).resolve()
    actual_head = _git_text(root, "rev-parse", "HEAD")
    expected_id = f"recovery-{entry.source_head[:12]}"
    actual = {
        "root": _same_path(top, root),
        "branch": _git_text(root, "branch", "--show-current") == entry.source_branch,
        "head": actual_head == entry.source_head,
        "status": _status_hash(root) == entry.source_status_sha256,
        "source_root_id": entry.source_root_id == expected_id,
    }
    failed = [name for name, valid in actual.items() if not valid]
    if failed:
        raise ArchiveError(
            f"source identity mismatch for {entry.source_root_id}: {', '.join(failed)}"
        )


def _validate_identities(plan: ArchivePlan) -> None:
    seen: dict[str, tuple[str, str, str, str]] = {}
    representatives: dict[str, ArchiveEntry] = {}
    for entry in plan.entries:
        metadata = (
            str(entry.source_root),
            entry.source_branch,
            entry.source_head,
            entry.source_status_sha256,
        )
        previous = seen.setdefault(entry.source_root_id, metadata)
        if previous != metadata:
            raise ArchiveError(
                f"conflicting immutable metadata for source_root_id {entry.source_root_id}"
            )
        representatives.setdefault(entry.source_root_id, entry)
    for source_id in sorted(representatives):
        _validate_identity(representatives[source_id])


def _source_file(entry: ArchiveEntry) -> Path:
    relative = _safe_relative(entry.source_path)
    raw = entry.source_root / Path(relative)
    _reject_reparse(raw)
    try:
        source = raw.resolve(strict=True)
        root = entry.source_root.resolve(strict=True)
    except OSError as error:
        raise ArchiveError(f"unresolved source path: {relative}") from error
    try:
        source.relative_to(root)
    except ValueError as error:
        raise ArchiveError(f"source path escapes approved root: {relative}") from error
    if not source.is_file():
        raise ArchiveError(f"source is not a regular file: {relative}")
    details = source.stat()
    if details.st_size != entry.size_bytes:
        raise ArchiveError(f"source size mismatch: {relative}")
    if _sha256(source) != entry.sha256:
        raise ArchiveError(f"source hash mismatch: {relative}")
    return source


def _destination_root(destination: Path) -> Path:
    root = Path(destination)
    if not root.is_absolute():
        raise ArchiveError(f"archive destination must be absolute: {root}")
    _reject_reparse(root)
    return root.resolve(strict=False)


def _destination_file(root: Path, entry: ArchiveEntry) -> Path:
    relative = _safe_relative(entry.archive_path)
    raw = root / Path(relative)
    _reject_reparse(raw)
    resolved = raw.resolve(strict=False)
    try:
        resolved.relative_to(root)
    except ValueError as error:
        raise ArchiveError(f"archive path escapes destination root: {relative}") from error
    return resolved


def _entry_from_record(record: Mapping[str, object]) -> ArchiveEntry:
    source_path = _safe_relative(record.get("source_path", record.get("path", "")))
    archive_path = _safe_relative(
        record.get("archive_path", record.get("destination", ""))
    )
    sha256 = str(record.get("sha256", "")).lower()
    if len(sha256) != 64 or any(char not in "0123456789abcdef" for char in sha256):
        raise ArchiveError(f"invalid SHA-256 for {source_path}")
    try:
        size_bytes = int(str(record.get("size_bytes", "")))
    except ValueError as error:
        raise ArchiveError(f"invalid size for {source_path}") from error
    if size_bytes < 0:
        raise ArchiveError(f"invalid size for {source_path}")
    source_head = str(record.get("source_head", ""))
    source_root_id = str(record.get("source_root_id", ""))
    if not source_head or source_root_id != f"recovery-{source_head[:12]}":
        raise ArchiveError(
            f"source_root_id is not bound to immutable source metadata: {source_root_id}"
        )
    journal_raw = str(record.get("journal", "planned"))
    try:
        journal = tuple(JournalState(value) for value in journal_raw.split("|") if value)
    except ValueError as error:
        raise ArchiveError(f"invalid journal for {source_path}") from error
    if not journal or journal[0] is not JournalState.PLANNED:
        raise ArchiveError(f"journal must begin with planned: {source_path}")
    return ArchiveEntry(
        source_root_id=source_root_id,
        source_root=Path(str(record.get("source_root", ""))),
        source_branch=str(record.get("source_branch", "")),
        source_head=source_head,
        source_status_sha256=str(record.get("source_status_sha256", "")),
        source_path=source_path,
        archive_path=archive_path,
        size_bytes=size_bytes,
        sha256=sha256,
        journal=journal,
    )


def _validate_unique_destinations(entries: Iterable[ArchiveEntry]) -> None:
    seen: dict[str, str] = {}
    for entry in entries:
        key = entry.archive_path.casefold()
        if key in seen:
            raise ArchiveError(
                "duplicate archive destination: "
                f"{seen[key]} and {entry.archive_path}"
            )
        seen[key] = entry.archive_path


def plan_archive(records: Iterable[Mapping[str, object]]) -> ArchivePlan:
    """Create a hash-bound plan for records classified ``archive_external``."""

    entries = tuple(
        _entry_from_record(record)
        for record in records
        if str(record.get("action", "")) == "archive_external"
    )
    _validate_unique_destinations(entries)
    plan = ArchivePlan(tuple(sorted(entries, key=lambda entry: entry.archive_path)))
    _validate_identities(plan)
    for entry in plan.entries:
        _source_file(entry)
    return plan


def _advance(entry: ArchiveEntry, state: JournalState) -> None:
    if state not in entry.journal:
        entry.journal = (*entry.journal, state)


def _nearest_existing(path: Path) -> Path:
    candidate = path
    while not candidate.exists():
        parent = candidate.parent
        if parent == candidate:
            raise ArchiveError(f"cannot locate destination volume: {path}")
        candidate = parent
    return candidate


def _copy_one(source: Path, target: Path, entry: ArchiveEntry) -> None:
    _reject_reparse(target)
    _io_path(target.parent).mkdir(parents=True, exist_ok=True)
    _reject_reparse(target.parent)
    temporary = target.with_name(f".{target.name}.archive-copy-{uuid.uuid4().hex}.tmp")
    io_source = _io_path(source)
    io_temporary = _io_path(temporary)
    io_target = _io_path(target)
    try:
        with io_source.open("rb") as source_handle, io_temporary.open("xb") as target_handle:
            shutil.copyfileobj(source_handle, target_handle, length=1024 * 1024)
            target_handle.flush()
            os.fsync(target_handle.fileno())
        if io_temporary.stat().st_size != entry.size_bytes or _sha256(temporary) != entry.sha256:
            raise ArchiveError(f"copied bytes failed verification: {entry.archive_path}")
        try:
            os.link(io_temporary, io_target)
        except FileExistsError as error:
            raise ArchiveError(f"destination collision: {entry.archive_path}") from error
        except OSError as error:
            raise ArchiveError(f"cannot publish archive copy: {entry.archive_path}") from error
    finally:
        try:
            io_temporary.unlink()
        except FileNotFoundError:
            pass


def copy_archive(plan: ArchivePlan, destination: Path) -> ArchivePlan:
    """Copy every planned entry without deleting or overwriting any source."""

    _validate_identities(plan)
    _validate_unique_destinations(plan.entries)
    root = _destination_root(Path(destination))
    preflight: list[tuple[ArchiveEntry, Path, Path, bool]] = []
    required_bytes = 0
    for entry in plan.entries:
        source = _source_file(entry)
        target = _destination_file(root, entry)
        io_target = _io_path(target)
        existing = io_target.exists()
        if existing:
            if not io_target.is_file() or io_target.stat().st_size != entry.size_bytes:
                raise ArchiveError(f"destination collision: {entry.archive_path}")
            if _sha256(target) != entry.sha256:
                raise ArchiveError(f"destination collision: {entry.archive_path}")
        else:
            required_bytes += entry.size_bytes
        preflight.append((entry, source, target, existing))

    free = shutil.disk_usage(_nearest_existing(root)).free
    if free < required_bytes + MIN_FREE_AFTER_COPY:
        raise ArchiveError(
            "insufficient free space: "
            f"need {required_bytes + MIN_FREE_AFTER_COPY} bytes, have {free}"
        )

    for entry, source, target, existing in preflight:
        if not existing:
            _source_file(entry)
            _copy_one(source, target, entry)
        io_target = _io_path(target)
        if io_target.stat().st_size != entry.size_bytes or _sha256(target) != entry.sha256:
            raise ArchiveError(f"copied bytes failed verification: {entry.archive_path}")
        _advance(entry, JournalState.COPIED)
    return plan


def _manifest_hash(entries: Iterable[ArchiveEntry]) -> str:
    digest = hashlib.sha256()
    for entry in sorted(entries, key=lambda item: item.archive_path):
        digest.update(
            f"{entry.sha256}  {entry.size_bytes}  {entry.archive_path}\n".encode("utf-8")
        )
    return digest.hexdigest()


def verify_archive(plan: ArchivePlan, destination: Path) -> VerificationResult:
    """Hash every source and archive copy, returning all unverified rows."""

    _validate_identities(plan)
    _validate_unique_destinations(plan.entries)
    root = _destination_root(Path(destination))
    unverified: list[str] = []
    verified_count = 0
    for entry in plan.entries:
        _source_file(entry)
        target = _destination_file(root, entry)
        io_target = _io_path(target)
        if (
            not io_target.is_file()
            or io_target.stat().st_size != entry.size_bytes
            or _sha256(target) != entry.sha256
        ):
            unverified.append(entry.archive_path)
            continue
        _advance(entry, JournalState.COPIED)
        _advance(entry, JournalState.VERIFIED)
        verified_count += 1
    return VerificationResult(
        valid=not unverified,
        verified_count=verified_count,
        unverified=tuple(unverified),
        total_bytes=sum(entry.size_bytes for entry in plan.entries),
        archive_manifest_sha256=_manifest_hash(plan.entries),
    )


def _write_plan(path: Path, plan: ArchivePlan) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    with temporary.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=PLAN_FIELDS, lineterminator="\n")
        writer.writeheader()
        for entry in plan.entries:
            writer.writerow(
                {
                    "source_root_id": entry.source_root_id,
                    "source_root": str(entry.source_root),
                    "source_branch": entry.source_branch,
                    "source_head": entry.source_head,
                    "source_status_sha256": entry.source_status_sha256,
                    "source_path": entry.source_path,
                    "archive_path": entry.archive_path,
                    "size_bytes": entry.size_bytes,
                    "sha256": entry.sha256,
                    "journal": "|".join(state.value for state in entry.journal),
                }
            )
    os.replace(temporary, path)


def _read_plan(path: Path) -> ArchivePlan:
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as handle:
            rows = tuple(dict(row) for row in csv.DictReader(handle))
    except (OSError, UnicodeError, csv.Error) as error:
        raise ArchiveError(f"cannot read archive plan: {path}") from error
    entries = tuple(_entry_from_record(row) for row in rows)
    _validate_unique_destinations(entries)
    return ArchivePlan(entries)


def _json_bytes(payload: Mapping[str, object]) -> bytes:
    return (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")


def _write_json(path: Path, payload: Mapping[str, object], *, collision_safe: bool) -> str:
    encoded = _json_bytes(payload)
    digest = hashlib.sha256(encoded).hexdigest()
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        if collision_safe and path.read_bytes() != encoded:
            raise ArchiveError(f"destination collision: {path}")
        if collision_safe:
            return digest
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    temporary.write_bytes(encoded)
    os.replace(temporary, path)
    return digest


def _receipt(plan: ArchivePlan, destination: Path, result: VerificationResult) -> dict[str, object]:
    counts = Counter(
        state.value for entry in plan.entries for state in entry.journal
    )
    source_roots = sorted(
        {
            (
                entry.source_root_id,
                str(entry.source_root),
                entry.source_branch,
                entry.source_head,
                entry.source_status_sha256,
            )
            for entry in plan.entries
        }
    )
    return {
        "schema": "testing-history-archive-receipt/v1",
        "archive_root": str(Path(destination).resolve()),
        "archive_manifest_sha256": result.archive_manifest_sha256,
        "entry_count": len(plan.entries),
        "total_bytes": result.total_bytes,
        "unverified_count": len(result.unverified),
        "journal_state_counts": dict(sorted(counts.items())),
        "source_roots": [
            {
                "source_root_id": row[0],
                "source_root": row[1],
                "source_branch": row[2],
                "source_head": row[3],
                "source_status_sha256": row[4],
            }
            for row in source_roots
        ],
        "entries": [
            {
                "source_root_id": entry.source_root_id,
                "source_path": entry.source_path,
                "archive_path": entry.archive_path,
                "size_bytes": entry.size_bytes,
                "sha256": entry.sha256,
                "journal": [state.value for state in entry.journal],
            }
            for entry in plan.entries
        ],
    }


def _load_inventory(path: Path) -> tuple[dict[str, object], ...]:
    metadata_path = path.with_name("inventory-metadata.json")
    try:
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        with path.open("r", encoding="utf-8-sig", newline="") as handle:
            rows = tuple(dict(row) for row in csv.DictReader(handle))
    except (OSError, UnicodeError, json.JSONDecodeError, csv.Error) as error:
        raise ArchiveError(f"cannot load inventory metadata: {path}") from error
    source_head = str(metadata.get("source_head", ""))
    source_root_id = f"recovery-{source_head[:12]}"
    enriched = []
    for row in rows:
        if row.get("action") != "archive_external":
            continue
        if row.get("source_root_id") != source_root_id:
            raise ArchiveError(
                f"inventory source_root_id does not match immutable metadata: {row.get('path')}"
            )
        enriched.append(
            {
                **row,
                "source_root": metadata.get("source_root", ""),
                "source_branch": metadata.get("source_branch", ""),
                "source_head": source_head,
                "source_status_sha256": metadata.get("source_status_sha256", ""),
            }
        )
    return tuple(enriched)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    plan = commands.add_parser("plan")
    plan.add_argument("--inventory", type=Path, required=True)
    plan.add_argument("--output", type=Path, required=True)
    copy = commands.add_parser("copy")
    copy.add_argument("--plan", type=Path, required=True)
    copy.add_argument("--destination", type=Path, required=True)
    verify = commands.add_parser("verify")
    verify.add_argument("--plan", type=Path, required=True)
    verify.add_argument("--destination", type=Path, required=True)
    verify.add_argument("--receipt", type=Path, required=True)
    verify.add_argument("--summary", type=Path)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        if args.command == "plan":
            plan = plan_archive(_load_inventory(args.inventory))
            _write_plan(args.output, plan)
            print(f"Planned {len(plan.entries)} archive entries.")
            return 0
        plan = _read_plan(args.plan)
        if args.command == "copy":
            copy_archive(plan, args.destination)
            _write_plan(args.plan, plan)
            print(f"Copied or resumed {len(plan.entries)} archive entries.")
            return 0
        result = verify_archive(plan, args.destination)
        if not result.valid:
            print(
                f"Archive verification failed for {len(result.unverified)} entries.",
                file=sys.stderr,
            )
            return 2
        _write_plan(args.plan, plan)
        receipt_payload = _receipt(plan, args.destination, result)
        receipt_sha256 = _write_json(args.receipt, receipt_payload, collision_safe=True)
        summary_path = args.summary or args.plan.with_name("archive-summary.json")
        summary = {
            "schema": "testing-history-archive-summary/v1",
            "archive_root": str(Path(args.destination).resolve()),
            "archive_manifest_sha256": result.archive_manifest_sha256,
            "entry_count": len(plan.entries),
            "total_bytes": result.total_bytes,
            "unverified_count": 0,
            "receipt_path": str(Path(args.receipt).resolve()),
            "receipt_sha256": receipt_sha256,
            "plan_sha256": _sha256(args.plan),
        }
        _write_json(summary_path, summary, collision_safe=False)
        print(
            f"Verified {result.verified_count} archive entries; "
            f"manifest SHA-256 {result.archive_manifest_sha256}."
        )
        return 0
    except ArchiveError as error:
        print(f"archive transaction refused: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
