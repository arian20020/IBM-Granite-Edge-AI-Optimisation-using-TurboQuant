"""Byte-accurate provenance helpers."""

from __future__ import annotations

import hashlib
from pathlib import Path


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def file_identity(path: Path) -> dict[str, object]:
    resolved = Path(path).resolve(strict=True)
    if not resolved.is_file() or resolved.is_symlink():
        raise ValueError("asset must be a regular non-link file")
    return {"bytes": resolved.stat().st_size, "sha256": sha256_file(resolved)}
