#!/usr/bin/env python3
"""Plan, copy, and verify a copy-only external testing-history archive."""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import os
import shutil
import stat
import subprocess
import sys
import uuid
from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone
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
REMOVAL_ACTIONS = frozenset({"archive_external", "remove_regenerable"})
INVENTORY_ACTIONS = frozenset(
    {
        "retain_active",
        "move_active",
        "archive_code",
        "archive_external",
        "remove_regenerable",
        "retain_ambiguous",
    }
)
APPROVED_SCOPES = (
    "docs/testing/final-results",
    "experiments/raw-results",
    "scripts/testing",
)
ARCHIVE_CLASSIFICATION_REASON = "exact duplicate with no active evidence binding"
REGENERABLE_CLASSIFICATION_REASON = "proven interpreter cache"
REGENERABLE_RULE = "python bytecode cache: scripts/testing/**/__pycache__/*.pyc"


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


@dataclass(frozen=True, slots=True)
class SourceRootBinding:
    source_root_id: str
    source_root: Path
    source_branch: str
    source_head: str
    source_status_sha256: str


@dataclass(frozen=True, slots=True)
class RemovalTarget:
    source_root_id: str
    source_root: Path
    original_path: str
    action: str
    size_bytes: int
    sha256: str
    archive_path: str | None
    archive_destination: Path | None
    regenerable_rule: str | None


@dataclass(frozen=True, slots=True)
class RemovalTransaction:
    inventory_path: Path
    inventory_sha256: str
    inventory_metadata_path: Path
    inventory_metadata_sha256: str
    archive_plan_path: Path
    archive_plan_sha256: str
    archive_summary_path: Path
    archive_summary_sha256: str
    archive_receipt_path: Path
    archive_receipt_sha256: str
    archive_root: Path
    archive_manifest_sha256: str
    archive_entry_count: int
    canonical_root: Path
    source: SourceRootBinding
    targets: tuple[RemovalTarget, ...]


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


def _utc_now() -> str:
    return (
        datetime.now(timezone.utc)
        .isoformat(timespec="milliseconds")
        .replace("+00:00", "Z")
    )


def _read_json(path: Path, label: str) -> dict[str, object]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ArchiveError(f"cannot read {label}: {path}") from error
    if not isinstance(payload, dict):
        raise ArchiveError(f"{label} must be a JSON object: {path}")
    return payload


def _read_csv_records(
    path: Path, label: str
) -> tuple[tuple[dict[str, str], ...], tuple[str, ...]]:
    try:
        text = path.read_bytes().decode("utf-8-sig")
        reader = csv.DictReader(io.StringIO(text, newline=""))
        rows = tuple(dict(row) for row in reader)
        fields = tuple(reader.fieldnames or ())
    except (OSError, UnicodeError, csv.Error) as error:
        raise ArchiveError(f"cannot read {label}: {path}") from error
    if not fields:
        raise ArchiveError(f"{label} has no CSV header: {path}")
    if any(None in row for row in rows):
        raise ArchiveError(f"{label} contains malformed CSV rows: {path}")
    return rows, fields


def _required_sha256(value: object, label: str) -> str:
    digest = str(value or "").lower()
    if len(digest) != 64 or any(char not in "0123456789abcdef" for char in digest):
        raise ArchiveError(f"invalid {label} SHA-256")
    return digest


def _required_git_oid(value: object, label: str) -> str:
    oid = str(value or "").lower()
    if len(oid) not in {40, 64} or any(
        character not in "0123456789abcdef" for character in oid
    ):
        raise ArchiveError(f"invalid {label} Git object ID")
    return oid


def _required_nonnegative_integer(value: object, label: str) -> int:
    try:
        parsed = int(str(value))
    except (TypeError, ValueError) as error:
        raise ArchiveError(f"invalid {label}") from error
    if parsed < 0:
        raise ArchiveError(f"invalid {label}")
    return parsed


def _safe_removal_relative(value: object) -> str:
    relative = _safe_relative(value)
    if any(character in relative for character in "*?[]"):
        raise ArchiveError(f"wildcard is not allowed in removal path: {value}")
    return relative


def _path_in_scope(path: str, scopes: Iterable[str]) -> bool:
    candidate = PurePosixPath(path)
    return any(
        candidate == PurePosixPath(scope)
        or PurePosixPath(scope) in candidate.parents
        for scope in scopes
    )


def _control_root(path: Path) -> Path:
    candidate = Path(path)
    _reject_reparse(candidate)
    try:
        if not candidate.is_file():
            raise ArchiveError(f"inventory is not a regular file: {candidate}")
        root = Path(_git_text(candidate.parent, "rev-parse", "--show-toplevel"))
        _reject_reparse(root)
        root = root.resolve(strict=True)
    except OSError as error:
        raise ArchiveError(f"cannot resolve inventory repository: {candidate}") from error
    if not root.is_dir():
        raise ArchiveError(f"inventory repository is unavailable: {root}")
    return root


def _immutable_head_file(path: Path, root: Path, label: str) -> str:
    candidate = Path(path)
    _reject_reparse(candidate)
    try:
        resolved = candidate.resolve(strict=True)
        relative = resolved.relative_to(root).as_posix()
    except (OSError, ValueError) as error:
        raise ArchiveError(f"{label} is outside the implementation root: {candidate}") from error
    if not resolved.is_file():
        raise ArchiveError(f"{label} is not a regular file: {candidate}")
    actual = resolved.read_bytes()
    expected = _git_bytes(root, "show", f"HEAD:{relative}")
    if actual != expected:
        raise ArchiveError(f"{label} differs from immutable inventory HEAD bytes")
    return hashlib.sha256(actual).hexdigest()


def _validate_canonical_identity(root: Path, metadata: Mapping[str, object]) -> None:
    expected_root = Path(str(metadata.get("canonical_root", "")))
    if not expected_root.is_absolute():
        raise ArchiveError("canonical root in inventory metadata is not absolute")
    _reject_reparse(expected_root)
    try:
        expected_root = expected_root.resolve(strict=True)
    except OSError as error:
        raise ArchiveError("canonical root in inventory metadata is unavailable") from error
    if not _same_path(root, expected_root):
        raise ArchiveError("inventory is not under its approved implementation root")
    if _git_text(root, "branch", "--show-current") != str(
        metadata.get("canonical_branch", "")
    ):
        raise ArchiveError("implementation root branch identity mismatch")
    canonical_head = _required_git_oid(
        metadata.get("canonical_head", ""), "canonical head"
    )
    ancestor = subprocess.run(
        ["git", "merge-base", "--is-ancestor", canonical_head, "HEAD"],
        cwd=root,
        capture_output=True,
        check=False,
    )
    if ancestor.returncode != 0:
        raise ArchiveError("implementation HEAD is not descended from inventory metadata")


def _source_binding(metadata: Mapping[str, object]) -> SourceRootBinding:
    head = _required_git_oid(metadata.get("source_head", ""), "source head")
    source_root_id = f"recovery-{head[:12]}"
    root = Path(str(metadata.get("source_root", "")))
    if not root.is_absolute():
        raise ArchiveError("source root in inventory metadata is not absolute")
    return SourceRootBinding(
        source_root_id=source_root_id,
        source_root=root,
        source_branch=str(metadata.get("source_branch", "")),
        source_head=head,
        source_status_sha256=_required_sha256(
            metadata.get("source_status_sha256", ""), "source status"
        ),
    )


def _validate_source_binding(
    binding: SourceRootBinding, *, include_status: bool
) -> Path:
    raw_root = binding.source_root
    _reject_reparse(raw_root)
    try:
        root = raw_root.resolve(strict=True)
    except OSError as error:
        raise ArchiveError(f"source identity root is unavailable: {raw_root}") from error
    if not root.is_dir():
        raise ArchiveError(f"source identity root is not a directory: {root}")
    identity = _git_text(
        root,
        "rev-parse",
        "--show-toplevel",
        "HEAD",
        "--abbrev-ref",
        "HEAD",
    ).splitlines()
    if len(identity) != 3:
        raise ArchiveError(f"source identity response is invalid for {root}")
    actual_root, actual_head, actual_branch = identity
    failed = []
    if not _same_path(Path(actual_root).resolve(), root):
        failed.append("root")
    if actual_branch != binding.source_branch:
        failed.append("branch")
    if actual_head != binding.source_head:
        failed.append("head")
    if binding.source_root_id != f"recovery-{binding.source_head[:12]}":
        failed.append("source_root_id")
    if include_status and _status_hash(root) != binding.source_status_sha256:
        failed.append("status")
    if failed:
        raise ArchiveError(
            f"source identity mismatch for {binding.source_root_id}: "
            + ", ".join(failed)
        )
    return root


def _inventory_evidence_is_empty(row: Mapping[str, str], path: str) -> None:
    referenced = str(row.get("referenced_by_final_results", "")).casefold()
    if referenced not in {"true", "false"}:
        raise ArchiveError(f"ambiguous citation flag for removal row: {path}")
    raw_ids = str(row.get("evidence_ids", ""))
    try:
        evidence_ids = json.loads(raw_ids)
    except json.JSONDecodeError as error:
        raise ArchiveError(f"ambiguous evidence IDs for removal row: {path}") from error
    if not isinstance(evidence_ids, list) or any(
        not isinstance(value, str) for value in evidence_ids
    ):
        raise ArchiveError(f"ambiguous evidence IDs for removal row: {path}")
    if referenced == "true" or evidence_ids or any(
        str(row.get(field, ""))
        for field in ("test_case_id", "attempt_id", "terminal_status")
    ):
        raise ArchiveError(f"cited file cannot be removed: {path}")


def _validate_candidate_classification(
    row: Mapping[str, str], path: str, scopes: tuple[str, ...]
) -> str:
    action = str(row.get("action", ""))
    if action not in REMOVAL_ACTIONS:
        raise ArchiveError(f"ambiguous record is not an exact removal action: {path}")
    if not _path_in_scope(path, scopes):
        raise ArchiveError(f"removal path is outside approved inventory scopes: {path}")
    destination = str(row.get("destination", ""))
    if action == "archive_external" and destination != path:
        raise ArchiveError(f"inventory destination mismatch for removal row: {path}")
    if action == "remove_regenerable" and destination not in {"", path}:
        raise ArchiveError(f"inventory destination mismatch for removal row: {path}")
    if str(row.get("tracked_status", "")) not in {"tracked", "untracked", "ignored"}:
        raise ArchiveError(f"ambiguous tracked status for removal row: {path}")
    _inventory_evidence_is_empty(row, path)

    parts = PurePosixPath(path).parts
    if action == "remove_regenerable":
        valid_cache = (
            path.startswith("scripts/testing/")
            and "__pycache__" in parts
            and PurePosixPath(path).suffix.casefold() == ".pyc"
            and str(row.get("tracked_status", "")) == "ignored"
            and str(row.get("reason", "")) == REGENERABLE_CLASSIFICATION_REASON
        )
        if not valid_cache:
            raise ArchiveError(
                f"regenerable classification is not independently proven: {path}"
            )
        return action

    duplicate_group = str(row.get("duplicate_group", ""))
    valid_archive = (
        not path.startswith("scripts/testing/")
        and (
            path.startswith("experiments/raw-results/")
            or path.startswith("docs/testing/final-results/")
        )
        and duplicate_group.startswith("DUP-")
        and len(duplicate_group) == 20
        and all(character in "0123456789abcdef" for character in duplicate_group[4:])
        and str(row.get("reason", "")) == ARCHIVE_CLASSIFICATION_REASON
    )
    if not valid_archive:
        raise ArchiveError(f"ambiguous archive classification cannot be removed: {path}")
    return action


def _removal_source_file(
    binding: SourceRootBinding,
    relative: str,
    size_bytes: int,
    sha256: str,
) -> Path:
    safe = _safe_removal_relative(relative)
    root = binding.source_root
    raw = root / Path(safe)
    _reject_reparse(raw)
    try:
        source = raw.resolve(strict=True)
        resolved_root = root.resolve(strict=True)
        source.relative_to(resolved_root)
    except (OSError, ValueError) as error:
        raise ArchiveError(f"source path escapes approved root: {safe}") from error
    candidate = _io_path(source)
    try:
        details = candidate.lstat()
    except OSError as error:
        raise ArchiveError(f"unresolved source path: {safe}") from error
    if not stat.S_ISREG(details.st_mode):
        raise ArchiveError(f"source is not a regular file: {safe}")
    if details.st_size != size_bytes:
        raise ArchiveError(f"source size mismatch: {safe}")
    if _sha256(source) != sha256:
        raise ArchiveError(f"source hash mismatch: {safe}")
    return source


def _receipt_entry_index(
    receipt: Mapping[str, object], binding: SourceRootBinding
) -> dict[str, dict[str, object]]:
    entries = receipt.get("entries")
    if not isinstance(entries, list):
        raise ArchiveError("archive receipt entries are invalid")
    indexed: dict[str, dict[str, object]] = {}
    for raw in entries:
        if not isinstance(raw, dict):
            raise ArchiveError("archive receipt entry is invalid")
        path = _safe_removal_relative(raw.get("source_path", ""))
        key = path.casefold()
        if key in indexed:
            raise ArchiveError(f"duplicate archive receipt source path: {path}")
        if str(raw.get("source_root_id", "")) != binding.source_root_id:
            raise ArchiveError(f"archive receipt source root mismatch: {path}")
        journal = raw.get("journal")
        if not isinstance(journal, list) or "verified" not in journal:
            raise ArchiveError(f"unverified archive row: {path}")
        _required_sha256(raw.get("sha256", ""), f"archive row {path}")
        _required_nonnegative_integer(raw.get("size_bytes", ""), f"archive row {path} size")
        _safe_removal_relative(raw.get("archive_path", ""))
        indexed[key] = raw
    return indexed


def _validate_archive_receipt(
    receipt: Mapping[str, object],
    summary: Mapping[str, object],
    binding: SourceRootBinding,
    receipt_entries: Mapping[str, Mapping[str, object]],
) -> Path:
    if receipt.get("schema") != "testing-history-archive-receipt/v1":
        raise ArchiveError("archive receipt schema mismatch")
    if summary.get("schema") != "testing-history-archive-summary/v1":
        raise ArchiveError("archive summary schema mismatch")
    entry_count = _required_nonnegative_integer(
        receipt.get("entry_count", ""), "archive receipt entry count"
    )
    if entry_count != len(receipt_entries) or entry_count != _required_nonnegative_integer(
        summary.get("entry_count", ""), "archive summary entry count"
    ):
        raise ArchiveError("archive receipt entry count mismatch")
    if _required_nonnegative_integer(
        receipt.get("unverified_count", ""), "archive receipt unverified count"
    ) != 0 or _required_nonnegative_integer(
        summary.get("unverified_count", ""), "archive summary unverified count"
    ) != 0:
        raise ArchiveError("archive receipt contains unverified rows")
    roots = receipt.get("source_roots")
    if not isinstance(roots, list) or len(roots) != 1 or not isinstance(roots[0], dict):
        raise ArchiveError("archive receipt source roots are invalid")
    expected_root = {
        "source_root_id": binding.source_root_id,
        "source_root": str(binding.source_root),
        "source_branch": binding.source_branch,
        "source_head": binding.source_head,
        "source_status_sha256": binding.source_status_sha256,
    }
    if roots[0] != expected_root:
        raise ArchiveError("archive receipt source root identity mismatch")
    manifest = _required_sha256(
        receipt.get("archive_manifest_sha256", ""), "archive manifest"
    )
    if manifest != _required_sha256(
        summary.get("archive_manifest_sha256", ""), "archive summary manifest"
    ):
        raise ArchiveError("archive manifest mismatch")
    digest = hashlib.sha256()
    total_bytes = 0
    for entry in sorted(
        receipt_entries.values(), key=lambda row: str(row["archive_path"])
    ):
        path = str(entry["archive_path"])
        size = int(str(entry["size_bytes"]))
        sha256 = str(entry["sha256"])
        digest.update(f"{sha256}  {size}  {path}\n".encode("utf-8"))
        total_bytes += size
    if digest.hexdigest() != manifest:
        raise ArchiveError("archive manifest does not match receipt entries")
    if total_bytes != _required_nonnegative_integer(
        receipt.get("total_bytes", ""), "archive receipt total bytes"
    ) or total_bytes != _required_nonnegative_integer(
        summary.get("total_bytes", ""), "archive summary total bytes"
    ):
        raise ArchiveError("archive receipt total byte count mismatch")
    archive_root = _destination_root(Path(str(receipt.get("archive_root", ""))))
    summary_root = _destination_root(Path(str(summary.get("archive_root", ""))))
    if not _same_path(archive_root, summary_root) or not archive_root.is_dir():
        raise ArchiveError("archive root mismatch or unavailable")
    return archive_root


def _prepare_removal_transaction(
    inventory_path: Path, archive_receipt_path: Path
) -> RemovalTransaction:
    inventory = Path(inventory_path)
    canonical_root = _control_root(inventory)
    metadata_path = inventory.with_name("inventory-metadata.json")
    archive_plan_path = inventory.with_name("archive-plan.csv")
    archive_summary_path = inventory.with_name("archive-summary.json")
    inventory_sha256 = _immutable_head_file(inventory, canonical_root, "inventory")
    metadata_sha256 = _immutable_head_file(
        metadata_path, canonical_root, "inventory metadata"
    )
    plan_sha256 = _immutable_head_file(
        archive_plan_path, canonical_root, "archive plan"
    )
    summary_sha256 = _immutable_head_file(
        archive_summary_path, canonical_root, "archive summary"
    )
    metadata = _read_json(metadata_path, "inventory metadata")
    if metadata.get("schema") != "testing-cleanup-inventory/v1":
        raise ArchiveError("inventory metadata schema mismatch")
    _validate_canonical_identity(canonical_root, metadata)
    binding = _source_binding(metadata)
    if _same_path(binding.source_root.resolve(strict=False), canonical_root):
        raise ArchiveError("recovery and implementation roots must be distinct")
    source_root = _validate_source_binding(binding, include_status=True)
    binding = SourceRootBinding(
        source_root_id=binding.source_root_id,
        source_root=source_root,
        source_branch=binding.source_branch,
        source_head=binding.source_head,
        source_status_sha256=binding.source_status_sha256,
    )

    scopes_raw = metadata.get("scopes")
    if not isinstance(scopes_raw, list):
        raise ArchiveError("approved inventory scopes are invalid")
    scopes = tuple(sorted(_safe_removal_relative(value) for value in scopes_raw))
    if scopes != tuple(sorted(APPROVED_SCOPES)):
        raise ArchiveError("approved inventory scopes do not match cleanup contract")
    inventory_rows, inventory_fields = _read_csv_records(inventory, "inventory")
    required_fields = {
        "source_root_id",
        "path",
        "tracked_status",
        "size_bytes",
        "sha256",
        "test_case_id",
        "attempt_id",
        "terminal_status",
        "evidence_ids",
        "referenced_by_final_results",
        "duplicate_group",
        "action",
        "destination",
        "reason",
    }
    if not required_fields <= set(inventory_fields):
        raise ArchiveError("inventory header is missing required removal fields")
    if len(inventory_rows) != _required_nonnegative_integer(
        metadata.get("record_count", ""), "inventory record count"
    ):
        raise ArchiveError("inventory record count mismatch")

    selected: list[tuple[dict[str, str], str, int, str]] = []
    seen_paths: set[str] = set()
    for row in inventory_rows:
        path = _safe_removal_relative(row.get("path", ""))
        key = path.casefold()
        if key in seen_paths:
            raise ArchiveError(f"duplicate inventory path: {path}")
        seen_paths.add(key)
        if str(row.get("source_root_id", "")) != binding.source_root_id:
            raise ArchiveError(f"inventory source root identity mismatch: {path}")
        action = str(row.get("action", ""))
        if action not in INVENTORY_ACTIONS:
            raise ArchiveError(f"unsupported inventory action: {action}")
        if action not in REMOVAL_ACTIONS:
            continue
        _validate_candidate_classification(row, path, scopes)
        size_bytes = _required_nonnegative_integer(
            row.get("size_bytes", ""), f"source size for {path}"
        )
        sha256 = _required_sha256(row.get("sha256", ""), f"source {path}")
        selected.append((row, path, size_bytes, sha256))
    if not selected:
        raise ArchiveError("inventory contains no exact removal actions")

    plan = _read_plan(archive_plan_path)
    plan_by_source: dict[str, ArchiveEntry] = {}
    for entry in plan.entries:
        path = _safe_removal_relative(entry.source_path)
        key = path.casefold()
        if key in plan_by_source:
            raise ArchiveError(f"duplicate archive plan source path: {path}")
        plan_by_source[key] = entry

    summary = _read_json(archive_summary_path, "archive summary")
    supplied_receipt = Path(archive_receipt_path)
    _reject_reparse(supplied_receipt)
    try:
        supplied_receipt = supplied_receipt.resolve(strict=True)
    except OSError as error:
        raise ArchiveError(f"archive receipt is unavailable: {archive_receipt_path}") from error
    if not supplied_receipt.is_file():
        raise ArchiveError(f"archive receipt is not a regular file: {supplied_receipt}")
    expected_receipt = Path(str(summary.get("receipt_path", "")))
    if not expected_receipt.is_absolute() or not _same_path(
        supplied_receipt, expected_receipt.resolve(strict=False)
    ):
        raise ArchiveError("archive receipt path does not match immutable summary")
    receipt_sha256 = _sha256(supplied_receipt)
    if receipt_sha256 != _required_sha256(
        summary.get("receipt_sha256", ""), "archive receipt"
    ):
        raise ArchiveError("archive receipt SHA-256 does not match immutable summary")
    if plan_sha256 != _required_sha256(
        summary.get("plan_sha256", ""), "archive plan"
    ):
        raise ArchiveError("archive plan SHA-256 does not match immutable summary")
    receipt = _read_json(supplied_receipt, "archive receipt")
    receipt_entries = _receipt_entry_index(receipt, binding)
    archive_root = _validate_archive_receipt(
        receipt, summary, binding, receipt_entries
    )

    targets: list[RemovalTarget] = []
    archive_selected = 0
    for row, path, size_bytes, sha256 in selected:
        action = row["action"]
        archive_path: str | None = None
        archive_destination: Path | None = None
        regenerable_rule: str | None = None
        if action == "archive_external":
            archive_selected += 1
            plan_entry = plan_by_source.get(path.casefold())
            receipt_entry = receipt_entries.get(path.casefold())
            if plan_entry is None or receipt_entry is None:
                raise ArchiveError(f"unverified archive row: {path}")
            archive_path = _safe_removal_relative(row["destination"])
            matches = (
                plan_entry.source_root_id == binding.source_root_id
                and _same_path(plan_entry.source_root, binding.source_root)
                and plan_entry.source_branch == binding.source_branch
                and plan_entry.source_head == binding.source_head
                and plan_entry.source_status_sha256 == binding.source_status_sha256
                and plan_entry.source_path == path
                and plan_entry.archive_path == archive_path
                and plan_entry.size_bytes == size_bytes
                and plan_entry.sha256 == sha256
                and JournalState.VERIFIED in plan_entry.journal
                and str(receipt_entry.get("source_path", "")) == path
                and str(receipt_entry.get("archive_path", "")) == archive_path
                and int(str(receipt_entry.get("size_bytes", "-1"))) == size_bytes
                and str(receipt_entry.get("sha256", "")) == sha256
            )
            if not matches:
                raise ArchiveError(f"archive destination or receipt mismatch: {path}")
            archive_destination = _destination_file(archive_root, plan_entry)
            io_destination = _io_path(archive_destination)
            if (
                not io_destination.is_file()
                or io_destination.stat().st_size != size_bytes
                or _sha256(archive_destination) != sha256
            ):
                raise ArchiveError(f"archive destination verification failed: {path}")
        else:
            regenerable_rule = REGENERABLE_RULE
        _removal_source_file(binding, path, size_bytes, sha256)
        targets.append(
            RemovalTarget(
                source_root_id=binding.source_root_id,
                source_root=binding.source_root,
                original_path=path,
                action=action,
                size_bytes=size_bytes,
                sha256=sha256,
                archive_path=archive_path,
                archive_destination=archive_destination,
                regenerable_rule=regenerable_rule,
            )
        )
    if archive_selected != len(plan_by_source) or archive_selected != len(
        receipt_entries
    ):
        raise ArchiveError("archive plan/receipt does not exactly match inventory actions")

    return RemovalTransaction(
        inventory_path=inventory.resolve(strict=True),
        inventory_sha256=inventory_sha256,
        inventory_metadata_path=metadata_path.resolve(strict=True),
        inventory_metadata_sha256=metadata_sha256,
        archive_plan_path=archive_plan_path.resolve(strict=True),
        archive_plan_sha256=plan_sha256,
        archive_summary_path=archive_summary_path.resolve(strict=True),
        archive_summary_sha256=summary_sha256,
        archive_receipt_path=supplied_receipt,
        archive_receipt_sha256=receipt_sha256,
        archive_root=archive_root,
        archive_manifest_sha256=str(receipt["archive_manifest_sha256"]),
        archive_entry_count=len(receipt_entries),
        canonical_root=canonical_root,
        source=binding,
        targets=tuple(sorted(targets, key=lambda item: item.original_path)),
    )


def _selection_entry(target: RemovalTarget) -> dict[str, object]:
    return {
        "source_root_id": target.source_root_id,
        "source_root": str(target.source_root),
        "original_path": target.original_path,
        "action": target.action,
        "archive_path": target.archive_path,
        "archive_destination": (
            str(target.archive_destination) if target.archive_destination else None
        ),
        "regenerable_rule": target.regenerable_rule,
        "size_bytes": target.size_bytes,
        "sha256": target.sha256,
    }


def _selection_sha256(targets: Iterable[RemovalTarget]) -> str:
    selected = [_selection_entry(target) for target in targets]
    encoded = json.dumps(
        selected, sort_keys=True, separators=(",", ":"), ensure_ascii=False
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _removal_receipt(
    transaction: RemovalTransaction,
    *,
    status: str,
    created_at_utc: str,
    removed_at: Mapping[str, str] | None = None,
) -> dict[str, object]:
    removed_at = removed_at or {}
    action_counts = Counter(target.action for target in transaction.targets)
    removed_counts = Counter(
        target.action
        for target in transaction.targets
        if target.original_path in removed_at
    )
    removed_count = sum(removed_counts.values())
    entries = []
    for target in transaction.targets:
        entry = _selection_entry(target)
        timestamp = removed_at.get(target.original_path)
        entry.update(
            {
                "state": "removed" if timestamp else "planned",
                "removed_at_utc": timestamp,
            }
        )
        entries.append(entry)
    return {
        "schema": "testing-cleanup-removal-receipt/v1",
        "status": status,
        "created_at_utc": created_at_utc,
        "completed_at_utc": _utc_now() if status == "removed" else None,
        "inventory_path": str(transaction.inventory_path),
        "inventory_sha256": transaction.inventory_sha256,
        "inventory_metadata_path": str(transaction.inventory_metadata_path),
        "inventory_metadata_sha256": transaction.inventory_metadata_sha256,
        "archive_plan_path": str(transaction.archive_plan_path),
        "archive_plan_sha256": transaction.archive_plan_sha256,
        "archive_summary_path": str(transaction.archive_summary_path),
        "archive_summary_sha256": transaction.archive_summary_sha256,
        "archive_receipt_path": str(transaction.archive_receipt_path),
        "archive_receipt_sha256": transaction.archive_receipt_sha256,
        "archive_root": str(transaction.archive_root),
        "archive_manifest_sha256": transaction.archive_manifest_sha256,
        "archive_entry_count": transaction.archive_entry_count,
        "selection_sha256": _selection_sha256(transaction.targets),
        "planned_count": len(transaction.targets),
        "removed_count": removed_count,
        "action_counts": {
            action: {
                "planned": action_counts.get(action, 0),
                "removed": removed_counts.get(action, 0),
            }
            for action in sorted(REMOVAL_ACTIONS)
        },
        "root_counts": [
            {
                "source_root_id": transaction.source.source_root_id,
                "role": "recovery",
                "root": str(transaction.source.source_root),
                "planned_count": len(transaction.targets),
                "removed_count": removed_count,
                "action_counts": {
                    action: action_counts.get(action, 0)
                    for action in sorted(REMOVAL_ACTIONS)
                },
            },
            {
                "source_root_id": None,
                "role": "implementation",
                "root": str(transaction.canonical_root),
                "planned_count": 0,
                "removed_count": 0,
                "action_counts": {
                    action: 0 for action in sorted(REMOVAL_ACTIONS)
                },
            },
        ],
        "entries": entries,
    }


def _validate_output_path(output: Path, transaction: RemovalTransaction) -> Path:
    expected = transaction.inventory_path.with_name("removal-receipt.json")
    candidate = Path(output).resolve(strict=False)
    _reject_reparse(candidate)
    if not _same_path(candidate, expected):
        raise ArchiveError(
            f"removal receipt output must be the inventory-bound path: {expected}"
        )
    if candidate.exists() and not candidate.is_file():
        raise ArchiveError(f"removal receipt output is not a regular file: {candidate}")
    return candidate


def _bindings_unchanged(transaction: RemovalTransaction) -> None:
    checks = (
        (transaction.inventory_path, transaction.inventory_sha256, "inventory"),
        (
            transaction.inventory_metadata_path,
            transaction.inventory_metadata_sha256,
            "inventory metadata",
        ),
        (transaction.archive_plan_path, transaction.archive_plan_sha256, "archive plan"),
        (
            transaction.archive_summary_path,
            transaction.archive_summary_sha256,
            "archive summary",
        ),
        (
            transaction.archive_receipt_path,
            transaction.archive_receipt_sha256,
            "archive receipt",
        ),
    )
    for path, expected, label in checks:
        _reject_reparse(path)
        if not path.is_file() or _sha256(path) != expected:
            raise ArchiveError(f"{label} changed after removal preflight")


def _validate_planned_receipt(
    planned: Mapping[str, object], expected: Mapping[str, object]
) -> None:
    if planned.get("schema") != "testing-cleanup-removal-receipt/v1":
        raise ArchiveError("real removal requires the exact dry-run receipt")
    if planned.get("status") != "planned":
        raise ArchiveError("real removal requires a planned dry-run receipt")
    stable_fields = (
        "inventory_sha256",
        "inventory_metadata_sha256",
        "archive_plan_sha256",
        "archive_summary_sha256",
        "archive_receipt_sha256",
        "archive_manifest_sha256",
        "selection_sha256",
        "planned_count",
        "removed_count",
        "action_counts",
        "root_counts",
        "entries",
    )
    if any(planned.get(field) != expected.get(field) for field in stable_fields):
        raise ArchiveError("dry-run receipt does not match the exact current removal plan")


def remove_verified(
    inventory: Path,
    archive_receipt: Path,
    output: Path,
    *,
    dry_run: bool,
) -> dict[str, object]:
    """Plan or execute exact source removals bound to a verified archive."""

    transaction = _prepare_removal_transaction(inventory, archive_receipt)
    output_path = _validate_output_path(output, transaction)
    created_at = _utc_now()
    planned_payload = _removal_receipt(
        transaction, status="planned", created_at_utc=created_at
    )
    if dry_run:
        if output_path.exists():
            existing = _read_json(output_path, "removal receipt output")
            if existing.get("status") == "removed":
                raise ArchiveError("refusing to overwrite a completed removal receipt")
        _write_json(output_path, planned_payload, collision_safe=False)
        for target in transaction.targets:
            print(
                f"{target.source_root_id}\t{target.action}\t{target.original_path}"
            )
        noun = "file" if len(transaction.targets) == 1 else "files"
        print(
            f"Planned {len(transaction.targets)} exact source {noun}; "
            "implementation-root targets: 0."
        )
        return planned_payload

    if not output_path.is_file():
        raise ArchiveError("real removal requires a reviewed dry-run receipt")
    reviewed = _read_json(output_path, "dry-run removal receipt")
    _validate_planned_receipt(reviewed, planned_payload)
    _bindings_unchanged(transaction)
    _validate_source_binding(transaction.source, include_status=True)
    removed_at: dict[str, str] = {}
    for target in transaction.targets:
        _validate_source_binding(transaction.source, include_status=False)
        source = _removal_source_file(
            transaction.source,
            target.original_path,
            target.size_bytes,
            target.sha256,
        )
        if target.action == "archive_external":
            destination = target.archive_destination
            if destination is None:
                raise ArchiveError(
                    f"archive destination is missing before removal: {target.original_path}"
                )
            _reject_reparse(destination)
            io_destination = _io_path(destination)
            if (
                not io_destination.is_file()
                or io_destination.stat().st_size != target.size_bytes
                or _sha256(destination) != target.sha256
            ):
                raise ArchiveError(
                    f"archive destination changed before removal: {target.original_path}"
                )
        _io_path(source).unlink()
        removed_at[target.original_path] = _utc_now()

    _bindings_unchanged(transaction)
    for target in transaction.targets:
        raw = transaction.source.source_root / Path(target.original_path)
        if os.path.lexists(_io_path(raw)):
            raise ArchiveError(f"source still exists after exact removal: {target.original_path}")
    completed = _removal_receipt(
        transaction,
        status="removed",
        created_at_utc=str(reviewed.get("created_at_utc", created_at)),
        removed_at=removed_at,
    )
    _write_json(output_path, completed, collision_safe=False)
    noun = "file" if len(transaction.targets) == 1 else "files"
    print(f"Removed {len(transaction.targets)} exact source {noun}.")
    return completed


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
    remove = commands.add_parser("remove-verified")
    remove.add_argument("--inventory", type=Path, required=True)
    remove.add_argument("--archive-receipt", type=Path, required=True)
    remove.add_argument("--output", type=Path, required=True)
    remove.add_argument("--dry-run", action="store_true")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        if args.command == "plan":
            plan = plan_archive(_load_inventory(args.inventory))
            _write_plan(args.output, plan)
            print(f"Planned {len(plan.entries)} archive entries.")
            return 0
        if args.command == "remove-verified":
            remove_verified(
                args.inventory,
                args.archive_receipt,
                args.output,
                dry_run=args.dry_run,
            )
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
