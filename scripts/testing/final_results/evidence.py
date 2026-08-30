"""Portable evidence provenance and checksum-manifest helpers."""

from __future__ import annotations

import hashlib
import re
from collections.abc import Collection, Sequence
from pathlib import Path

from .models import EvidenceRecord


_HASH_BLOCK_SIZE = 1024 * 1024
_SHA256_RE = re.compile(r"[0-9a-fA-F]{64}\Z")
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
    entries: list[tuple[str, str]] = []
    seen_paths: set[str] = set()
    for path in paths:
        relative_path = repo_relative(root, path)
        if relative_path in seen_paths:
            raise ValueError(f"duplicate manifest path: {relative_path}")
        seen_paths.add(relative_path)
        entries.append((relative_path, hash_file(Path(path))))

    entries.sort(key=lambda entry: entry[0])
    content = "".join(f"{digest}  {relative_path}\n" for relative_path, digest in entries)
    output_path = Path(output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(content, encoding="utf-8", newline="\n")


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
        lines = manifest_path.read_text(encoding="utf-8").splitlines()
    except (OSError, UnicodeError) as error:
        return [f"manifest: {error}"]

    for line_number, line in enumerate(lines, start=1):
        if "  " not in line:
            errors.append(_manifest_error(line_number, "malformed checksum entry"))
            continue
        digest, relative_path = line.split("  ", 1)
        if not _SHA256_RE.fullmatch(digest):
            errors.append(_manifest_error(line_number, "malformed checksum entry"))
            continue
        try:
            relative_path = _portable_relative(relative_path)
        except ValueError:
            errors.append(_manifest_error(line_number, "non-portable path"))
            continue
        if relative_path in seen_paths:
            errors.append(_manifest_error(line_number, "duplicate path"))
            continue
        seen_paths.add(relative_path)

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

    return errors


__all__ = [
    "build_evidence_record",
    "hash_file",
    "repo_relative",
    "validate_sha256_manifest",
    "write_sha256_manifest",
]
