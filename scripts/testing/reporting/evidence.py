"""Portable evidence provenance and checksum-manifest helpers."""

from __future__ import annotations

import csv
import hashlib
import os
import re
import tempfile
from collections.abc import Collection, Sequence
from functools import lru_cache
from pathlib import Path

from .models import EvidenceRecord


_HASH_BLOCK_SIZE = 1024 * 1024
_EVIDENCE_ID_PREFIX_LENGTH = 12


def _resolved_contained_path(repo_root: Path, path: Path) -> tuple[Path, Path]:
    """Resolve root and target, returning both and enforcing containment."""
    root = Path(repo_root).resolve(strict=True)
    target = Path(path).resolve(strict=False)
    try:
        target.relative_to(root)
    except ValueError as error:
        raise ValueError("path must resolve inside the repository") from error
    return root, target


def _portable_relative(relative_path: str) -> str:
    """Validate the serialized path boundary and normalize separators."""
    if (
        not relative_path
        or relative_path in {".", ".."}
        or relative_path.startswith(("/", "\\"))
        or "\\" in relative_path
        or any(ord(character) < 32 or ord(character) == 127 for character in relative_path)
    ):
        raise ValueError("path must be a repository-relative POSIX-style path")
    parts = relative_path.split("/")
    if any(part in {"", ".", ".."} for part in parts):
        raise ValueError("path must be a repository-relative POSIX-style path")
    if re.match(r"^[A-Za-z]:", relative_path):
        raise ValueError("path must be a repository-relative POSIX-style path")
    return relative_path


def repo_relative(repo_root: Path, path: Path) -> str:
    """Return a stable POSIX repository-relative path.

    Both paths are resolved before the containment check so a symlink cannot
    make an outside file appear to be part of the repository.
    """
    root, target = _resolved_contained_path(repo_root, path)
    relative = target.relative_to(root).as_posix()
    return _portable_relative(relative)


@lru_cache(maxsize=8)
def _path_migrations(
    ledger_path: str, modified_ns: int, size_bytes: int
) -> dict[str, str]:
    """Load an immutable snapshot of the path ledger for one repository state."""
    del modified_ns, size_bytes
    migrations: dict[str, str] = {}
    with Path(ledger_path).open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None or not {"old_path", "new_path"}.issubset(
            reader.fieldnames
        ):
            raise ValueError("PATH-MIGRATION.csv is missing required path columns")
        for row in reader:
            old_path = _portable_relative(row["old_path"].replace("\\", "/"))
            new_path = _portable_relative(row["new_path"].replace("\\", "/"))
            existing = migrations.get(old_path)
            if existing is not None and existing != new_path:
                raise ValueError(
                    f"PATH-MIGRATION.csv has conflicting mappings for {old_path}"
                )
            migrations[old_path] = new_path
    return migrations


def resolve_repository_path(repo_root: Path, relative_path: str | Path) -> Path:
    """Resolve a live repository path through the exact cleanup ledger.

    Frozen registers retain their historical path strings. Existing paths win,
    while a missing legacy path is translated only by an exact ledger row.
    """
    root = Path(repo_root).resolve(strict=True)
    relative = _portable_relative(Path(relative_path).as_posix())
    _, direct = _resolved_contained_path(root, root / relative)
    if direct.exists() and not direct.is_dir():
        return direct

    ledger = root / "docs/testing/cleanup/PATH-MIGRATION.csv"
    if not ledger.is_file():
        return direct
    ledger_stat = ledger.stat()
    migrations = _path_migrations(
        str(ledger.resolve()), ledger_stat.st_mtime_ns, ledger_stat.st_size
    )
    migrated_relative = migrations.get(relative)
    if migrated_relative is None:
        prefix = f"{relative}/"
        migrated_bases: set[str] = set()
        for old_path, new_path in migrations.items():
            if not old_path.startswith(prefix):
                continue
            suffix = old_path[len(prefix) :]
            suffix_marker = f"/{suffix}"
            if not new_path.endswith(suffix_marker):
                raise ValueError(
                    f"PATH-MIGRATION.csv cannot derive a directory mapping for {relative}"
                )
            migrated_bases.add(new_path[: -len(suffix_marker)])
        if not migrated_bases:
            return direct
        if len(migrated_bases) != 1:
            raise ValueError(
                f"PATH-MIGRATION.csv has conflicting directory mappings for {relative}"
            )
        migrated_relative = migrated_bases.pop()
    _, migrated = _resolved_contained_path(root, root / migrated_relative)
    return migrated


def migrate_repository_references(repo_root: Path, value: str) -> str:
    """Translate complete repository-path tokens while preserving surrounding text."""
    root = Path(repo_root).resolve(strict=True)
    ledger = root / "docs/testing/cleanup/PATH-MIGRATION.csv"
    if not ledger.is_file() or not value:
        return value
    ledger_stat = ledger.stat()
    migrations = _path_migrations(
        str(ledger.resolve()), ledger_stat.st_mtime_ns, ledger_stat.st_size
    )
    migrated = value
    for old_path, new_path in sorted(
        migrations.items(), key=lambda item: len(item[0]), reverse=True
    ):
        pattern = rf"(?<![A-Za-z0-9._/\\-]){re.escape(old_path)}(?![A-Za-z0-9._/\\-])"
        migrated = re.sub(pattern, lambda _match: new_path, migrated)
    return migrated


def hash_file(path: Path) -> str:
    """Hash a file with SHA-256 using bounded memory."""
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for block in iter(lambda: handle.read(_HASH_BLOCK_SIZE), b""):
            digest.update(block)
    return digest.hexdigest()


def build_evidence_record(
    repo_root: Path,
    path: Path,
    route_id: str,
    campaign_id: str,
    role: str,
    source_label: str | None = None,
    derived: bool = False,
    input_evidence_ids: Sequence[str] = (),
    existing_evidence_ids: Collection[str] = (),
) -> EvidenceRecord:
    """Build an evidence record from a verified repository file.

    IDs use the route and the first twelve digest characters in the common
    case. If that compact ID is already in use, the full digest disambiguates
    the record; a second collision is rejected instead of silently replacing
    an existing provenance record.
    """
    relative_path = repo_relative(repo_root, path)
    file_path = Path(path)
    if not file_path.is_file():
        raise FileNotFoundError(file_path)
    digest = hash_file(file_path)
    compact_id = f"{route_id}-{digest[:_EVIDENCE_ID_PREFIX_LENGTH]}"
    evidence_id = compact_id
    if evidence_id in existing_evidence_ids:
        evidence_id = f"{route_id}-{digest}"
        if evidence_id in existing_evidence_ids:
            raise ValueError(f"duplicate evidence ID: {evidence_id}")

    return EvidenceRecord(
        route_id=route_id,
        campaign_id=campaign_id,
        evidence_id=evidence_id,
        role=role,
        relative_path=relative_path,
        sha256=digest,
        size_bytes=file_path.stat().st_size,
        source_label=source_label,
        derived=derived,
        input_evidence_ids=tuple(input_evidence_ids),
    )


def write_sha256_manifest(
    root: Path, paths: Sequence[Path], output: Path
) -> None:
    """Write sorted ``sha256  relative/path`` entries with POSIX newlines."""
    root_path = Path(root).resolve(strict=True)
    output_path = Path(output)
    _, output_resolved = _resolved_contained_path(root_path, output_path)
    resolved_inputs: list[tuple[str, Path]] = []
    entries: list[tuple[str, str]] = []
    seen_paths: set[str] = set()
    for path in paths:
        _, input_resolved = _resolved_contained_path(root_path, Path(path))
        same_file = (
            output_resolved.exists()
            and input_resolved.exists()
            and os.path.samefile(output_resolved, input_resolved)
        )
        if input_resolved == output_resolved or same_file:
            raise ValueError("manifest output aliases an input evidence file")
        relative_path = repo_relative(root_path, input_resolved)
        if relative_path in seen_paths:
            raise ValueError(f"duplicate manifest path: {relative_path}")
        seen_paths.add(relative_path)
        resolved_inputs.append((relative_path, input_resolved))

    for relative_path, input_path in resolved_inputs:
        entries.append((relative_path, hash_file(input_path)))

    entries.sort(key=lambda entry: entry[0])
    content = "".join(
        f"{digest}  {relative_path}\n" for relative_path, digest in entries
    ).encode("utf-8")

    if output_resolved.exists():
        if not output_resolved.is_file():
            raise FileExistsError(output_resolved)
        if output_resolved.read_bytes() == content:
            return
        raise FileExistsError(output_resolved)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="wb",
            dir=output_path.parent,
            prefix=f".{output_path.name}.",
            suffix=".tmp",
            delete=False,
        ) as handle:
            temporary_path = Path(handle.name)
            handle.write(content)
            handle.flush()
            os.fsync(handle.fileno())
        # Recheck after creating the temporary file so a concurrently-created
        # output is never silently replaced.
        if output_resolved.exists():
            if output_resolved.is_file() and output_resolved.read_bytes() == content:
                return
            raise FileExistsError(output_resolved)
        os.replace(temporary_path, output_path)
        temporary_path = None
    finally:
        if temporary_path is not None:
            try:
                temporary_path.unlink()
            except FileNotFoundError:
                pass


def _manifest_error(line_number: int, message: str) -> str:
    return f"line {line_number}: {message}"


def validate_sha256_manifest(root: Path, manifest: Path) -> list[str]:
    """Validate checksum-manifest syntax, paths, file presence, and hashes."""
    manifest_path = Path(manifest)
    if not manifest_path.is_file():
        return ["manifest: file not found"]

    errors: list[str] = []
    seen_paths: set[str] = set()
    try:
        raw = manifest_path.read_bytes()
    except OSError as error:
        return [f"manifest: {error}"]
    if b"\r\n" in raw:
        errors.append("manifest: CRLF line endings are not canonical")
    elif b"\r" in raw:
        errors.append("manifest: carriage-return line endings are not canonical")
    if raw and not raw.endswith(b"\n"):
        errors.append("manifest: missing final newline")
    try:
        text = raw.decode("utf-8")
    except (OSError, UnicodeError) as error:
        return [f"manifest: {error}"]
    # Normalize only for parsing. The original bytes were already checked so
    # non-canonical line endings remain visible in ``errors``.
    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    if lines and lines[-1] == "":
        lines.pop()

    parsed_paths: list[str] = []

    for line_number, line in enumerate(lines, start=1):
        match = re.fullmatch(r"([0-9a-fA-F]{64})  (.+)", line)
        if match is None:
            errors.append(_manifest_error(line_number, "malformed checksum entry"))
            continue
        digest, relative_path = match.groups()
        try:
            relative_path = _portable_relative(relative_path)
        except ValueError:
            errors.append(_manifest_error(line_number, "non-portable path"))
            continue
        if relative_path in seen_paths:
            errors.append(_manifest_error(line_number, "duplicate path"))
            continue
        seen_paths.add(relative_path)
        parsed_paths.append(relative_path)

        try:
            _, target = _resolved_contained_path(root, Path(root) / relative_path)
        except (FileNotFoundError, ValueError):
            errors.append(f"{relative_path}: file not found")
            continue
        if not target.is_file():
            errors.append(f"{relative_path}: file not found")
            continue
        actual = hash_file(target)
        if actual.lower() != digest.lower():
            errors.append(f"{relative_path}: hash mismatch")

    if parsed_paths != sorted(parsed_paths):
        errors.insert(0, "manifest: entries are not sorted")
    return errors


__all__ = [
    "build_evidence_record",
    "hash_file",
    "migrate_repository_references",
    "repo_relative",
    "resolve_repository_path",
    "validate_sha256_manifest",
    "write_sha256_manifest",
]
