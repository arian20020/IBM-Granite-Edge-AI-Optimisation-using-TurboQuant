"""Terminal-safe runtime reconciliation for WB-04."""

from __future__ import annotations

from collections.abc import Iterable, Mapping, Set
from typing import Any

from .matrix import FORMAL_METRICS


def terminal_runtime_record(test_id: str, reason: str, source: str) -> dict[str, Any]:
    status = f"not-measured: {reason}"
    return {"test_id": test_id, "terminal": True, "reason": reason,
            "metrics": {name: {"value": None, "status": status, "source": source}
                        for name in sorted(FORMAL_METRICS)},
            "status": status, "source": source, "cleanup_process_count": 0}


def validate_runtime_records(records: Iterable[Mapping[str, Any]], expected: Set[str]) -> dict[str, Any]:
    indexed = {str(row.get("test_id")): row for row in records}
    missing = sorted(expected - indexed.keys())
    if missing:
        raise ValueError(f"missing runtime: {', '.join(missing)}")
    for test_id, row in indexed.items():
        if row.get("cleanup_process_count") != 0:
            raise ValueError(f"nonzero cleanup process count: {test_id}")
        if set(row.get("metrics", {})) != FORMAL_METRICS:
            raise ValueError(f"incomplete metrics: {test_id}")
        for value in row["metrics"].values():
            if not value.get("status") or not value.get("source"):
                raise ValueError(f"unsourced metric: {test_id}")
    return {"accepted": True, "count": len(indexed), "failed_count": 0}
