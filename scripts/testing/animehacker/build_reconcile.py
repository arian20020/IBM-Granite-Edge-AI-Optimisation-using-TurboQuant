"""Strict reconciliation helpers for animehacker build evidence."""

from __future__ import annotations

import hashlib
import re
from pathlib import Path


def parse_ctest_summary(text: str) -> dict[str, int]:
    match = re.search(r"(\d+)% tests passed, (\d+) tests failed out of (\d+)", text)
    if not match:
        compact = re.search(r"100% tests passed out of (\d+)", text)
        if compact:
            total = int(compact.group(1))
            return {"total": total, "failed": 0, "passed": total}
        raise ValueError("ctest summary not found")
    failed = int(match.group(2))
    total = int(match.group(3))
    if failed:
        raise ValueError(f"ctest reported {failed} failed tests")
    return {"total": total, "failed": failed, "passed": total - failed}


def inventory_binaries(root: Path, required: tuple[str, ...]) -> list[dict[str, str | int]]:
    records = []
    for name in required:
        matches = list(root.rglob(name))
        if not matches:
            raise FileNotFoundError(name)
        path = matches[0]
        records.append({
            "name": name,
            "path": str(path.resolve()),
            "bytes": path.stat().st_size,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        })
    return records
