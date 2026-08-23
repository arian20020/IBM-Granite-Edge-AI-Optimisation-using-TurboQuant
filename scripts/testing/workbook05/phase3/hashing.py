"""Deterministic SHA-256 helpers for Workbook 05 Phase 3 evidence."""

from __future__ import annotations

import hashlib
import os
from pathlib import Path
from typing import Sequence

from scripts.testing.workbook05.phase3.paths import (
    _assert_no_link_or_reparse_chain,
    _is_link_or_reparse,
    _reject_unsafe_path_text,
)


_CHUNK_SIZE = 1024 * 1024


def sha256_file(path: Path) -> str:
    """Hash one existing normal local file without following a link."""

    _reject_unsafe_path_text(path)
    if not path.is_absolute():
        raise ValueError(f"The file path must be absolute: {path}")
    if _is_link_or_reparse(path):
        raise ValueError(f"The file must not be a link or reparse point: {path}")
    if not path.is_file():
        raise ValueError(f"The path is not a regular file: {path}")

    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(_CHUNK_SIZE), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _normal_tree_root(root: Path) -> Path:
    _reject_unsafe_path_text(root)
    if not root.is_absolute():
        raise ValueError(f"The tree root must be absolute: {root}")
    if _is_link_or_reparse(root):
        raise ValueError(f"The tree root must not be a link or reparse point: {root}")
    try:
        resolved = root.resolve(strict=True)
    except OSError as error:
        raise ValueError(f"The tree root does not exist: {root}") from error
    if not resolved.is_dir():
        raise ValueError(f"The tree root is not a directory: {root}")
    return resolved


def _is_within(root: Path, candidate: Path) -> bool:
    try:
        common = Path(os.path.commonpath([str(root), str(candidate)]))
    except ValueError:
        return False
    return os.path.normcase(str(common)) == os.path.normcase(str(root))


def sha256_tree(root: Path, files: Sequence[Path]) -> str:
    """Hash sorted relative path, byte size, and file digest records."""

    resolved_root = _normal_tree_root(root)
    canonical: list[tuple[str, int, str]] = []
    seen: set[str] = set()

    for requested in files:
        _reject_unsafe_path_text(requested)
        if not requested.is_absolute():
            raise ValueError(f"Tree file paths must be absolute: {requested}")
        _assert_no_link_or_reparse_chain(resolved_root, requested)
        if _is_link_or_reparse(requested):
            raise ValueError(
                f"Tree files must not be a link or reparse point: {requested}"
            )
        try:
            resolved = requested.resolve(strict=True)
        except OSError as error:
            raise ValueError(f"Tree file does not exist: {requested}") from error
        if not resolved.is_file():
            raise ValueError(f"Tree input is not a regular file: {requested}")
        if not _is_within(resolved_root, resolved):
            raise ValueError(
                f"Tree file is outside the tree root {resolved_root}: {resolved}"
            )

        relative_path = resolved.relative_to(resolved_root).as_posix()
        duplicate_key = relative_path.casefold()
        if duplicate_key in seen:
            raise ValueError(f"Duplicate canonical relative path: {relative_path}")
        seen.add(duplicate_key)
        canonical.append((relative_path, resolved.stat().st_size, sha256_file(resolved)))

    aggregate = hashlib.sha256()
    for relative_path, size, file_digest in sorted(
        canonical,
        key=lambda row: (row[0].casefold(), row[0]),
    ):
        line = f"{relative_path}\0{size}\0{file_digest}\n".encode("utf-8")
        aggregate.update(line)
    return aggregate.hexdigest()
