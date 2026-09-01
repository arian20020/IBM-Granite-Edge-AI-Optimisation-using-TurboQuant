"""Terminal-safe runtime reconciliation for WB-04."""

from __future__ import annotations

import math
import re
from collections.abc import Iterable, Mapping, Set
from typing import Any

from .matrix import FORMAL_METRICS


EXPECTED_REJECTION_METRIC_VALUE = "not-produced-by-expected-rejection"


def terminal_runtime_record(test_id: str, reason: str, source: str) -> dict[str, Any]:
    status = f"not-measured: {reason}"
    return {"test_id": test_id, "terminal": True, "reason": reason,
            "expected_outcome": "pass",
            "metrics": {name: {"value": None, "status": status, "source": source}
                        for name in sorted(FORMAL_METRICS)},
            "status": status, "source": source, "cleanup_process_count": 0,
            "pilot_passed": False, "warmup_excluded": False,
            "accepted_sample_count": 0}


def expected_rejection_runtime_record(
    test_id: str,
    reason: str,
    source: str,
    rejection_evidence_sha256: str,
) -> dict[str, Any]:
    """Record a controlled fail-closed boundary without inventing metrics."""

    status = f"passed: expected-rejection: {reason}"
    metric_status = f"{EXPECTED_REJECTION_METRIC_VALUE}: {reason}"
    return {
        "test_id": test_id,
        "terminal": False,
        "reason": reason,
        "expected_outcome": "expected-rejection",
        "metrics": {
            name: {
                "value": EXPECTED_REJECTION_METRIC_VALUE,
                "status": metric_status,
                "source": source,
            }
            for name in sorted(FORMAL_METRICS)
        },
        "status": status,
        "source": source,
        "rejection_evidence_sha256": rejection_evidence_sha256,
        "cleanup_process_count": 0,
        "pilot_passed": False,
        "warmup_excluded": False,
        "accepted_sample_count": 0,
    }


def _is_finite_number(value: Any) -> bool:
    return (
        isinstance(value, (int, float))
        and not isinstance(value, bool)
        and math.isfinite(float(value))
    )


def _valid_utilization_aggregate(value: Any) -> bool:
    if not isinstance(value, Mapping):
        return False
    if value.get("query_succeeded") is not True:
        return False
    count = value.get("count")
    if not isinstance(count, int) or isinstance(count, bool) or count <= 0:
        return False
    numbers = [value.get(key) for key in ("mean", "median", "peak")]
    return (
        all(_is_finite_number(number) and 0.0 <= float(number) <= 100.0
            for number in numbers)
        and float(value["peak"]) >= float(value["mean"])
        and float(value["peak"]) >= float(value["median"])
    )


def _numeric_runtime_pass(row: Mapping[str, Any]) -> bool:
    if (
        row.get("terminal") is not False
        or row.get("expected_outcome") != "pass"
        or row.get("status") != "passed"
        or row.get("pilot_passed") is not True
        or row.get("warmup_excluded") is not True
        or row.get("accepted_sample_count") != 3
    ):
        return False
    for name, metric in row["metrics"].items():
        value = metric.get("value")
        if name in {"cpu_percent", "gpu_percent"}:
            if not _valid_utilization_aggregate(value):
                return False
        elif not _is_finite_number(value) or float(value) < 0.0:
            return False
    return True


def _expected_rejection_pass(row: Mapping[str, Any]) -> bool:
    evidence_hash = row.get("rejection_evidence_sha256")
    return (
        row.get("terminal") is False
        and row.get("expected_outcome") == "expected-rejection"
        and isinstance(row.get("status"), str)
        and row["status"].startswith("passed: expected-rejection:")
        and isinstance(evidence_hash, str)
        and re.fullmatch(r"[0-9a-f]{64}", evidence_hash) is not None
        and row.get("pilot_passed") is False
        and row.get("warmup_excluded") is False
        and row.get("accepted_sample_count") == 0
        and all(
            metric.get("value") == EXPECTED_REJECTION_METRIC_VALUE
            and isinstance(metric.get("status"), str)
            and metric["status"].startswith(EXPECTED_REJECTION_METRIC_VALUE + ":")
            for metric in row["metrics"].values()
        )
    )


def validate_runtime_records(
    records: Iterable[Mapping[str, Any]],
    expected: Set[str],
    *,
    expected_rejections: Set[str] = frozenset(),
) -> dict[str, Any]:
    rows = list(records)
    ids = [str(row.get("test_id")) for row in rows]
    if len(ids) != len(set(ids)):
        raise ValueError("duplicate runtime test id")
    indexed = dict(zip(ids, rows))
    unexpected = sorted(indexed.keys() - expected)
    if unexpected:
        raise ValueError(f"unexpected runtime: {', '.join(unexpected)}")
    missing = sorted(expected - indexed.keys())
    if missing:
        raise ValueError(f"missing runtime: {', '.join(missing)}")
    if not expected_rejections <= expected:
        raise ValueError("expected rejection id is not in the runtime set")

    failed_count = 0
    accepted_count = 0
    rejection_pass_count = 0
    for test_id, row in indexed.items():
        if row.get("cleanup_process_count") != 0:
            raise ValueError(f"nonzero cleanup process count: {test_id}")
        if set(row.get("metrics", {})) != FORMAL_METRICS:
            raise ValueError(f"incomplete metrics: {test_id}")
        for value in row["metrics"].values():
            if (
                not isinstance(value, Mapping)
                or not value.get("status")
                or not value.get("source")
            ):
                raise ValueError(f"unsourced metric: {test_id}")
        if test_id in expected_rejections:
            passed = _expected_rejection_pass(row)
            rejection_pass_count += int(passed)
        else:
            passed = _numeric_runtime_pass(row)
        accepted_count += int(passed)
        failed_count += int(not passed)
    return {
        "accepted": failed_count == 0,
        "count": len(indexed),
        "accepted_count": accepted_count,
        "failed_count": failed_count,
        "expected_rejection_pass_count": rejection_pass_count,
    }
