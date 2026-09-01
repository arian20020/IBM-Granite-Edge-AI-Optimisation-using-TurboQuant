"""Strict reconciliation for official OpenVINO diagnostic evidence."""

from __future__ import annotations

from collections.abc import Iterable, Mapping, Set
from typing import Any


def classify_diagnostic(record: Mapping[str, Any]) -> str:
    if record.get("exit_code") != 0:
        return "official-test-failed"
    if not record.get("evidence"):
        return "missing-evidence"
    terminal = record.get("terminal_classification")
    if terminal == "unsupported-by-source":
        if record.get("activated") or record.get("accepted"):
            return "contradictory-terminal-classification"
        return terminal
    if record.get("fallback") or record.get("actual_device") != record.get("requested_device"):
        return "fallback-not-activated"
    if not record.get("accepted"):
        return "configuration-rejected"
    if not record.get("activated"):
        return "activation-unproven"
    expected = record.get("expected_bytes")
    actual = record.get("actual_bytes")
    if record.get("proof_kind") == "capability":
        return "passed" if expected == 0 and actual == 0 else "unexpected-allocation-claim"
    if not isinstance(expected, int) or not isinstance(actual, int) or expected <= 0:
        return "allocation-unproven"
    if actual != expected:
        return "allocation-mismatch"
    return "passed"


def reconcile_diagnostics(
    records: Iterable[Mapping[str, Any]], expected_ids: Set[str]
) -> dict[str, Any]:
    indexed: dict[str, Mapping[str, Any]] = {}
    for record in records:
        test_id = record.get("test_id")
        if not isinstance(test_id, str):
            raise ValueError("diagnostic test id is missing")
        if test_id in indexed:
            raise ValueError(f"duplicate diagnostic: {test_id}")
        indexed[test_id] = record
    missing = sorted(expected_ids - indexed.keys())
    if missing:
        raise ValueError(f"missing diagnostic: {', '.join(missing)}")
    unexpected = sorted(indexed.keys() - expected_ids)
    if unexpected:
        raise ValueError(f"unexpected diagnostic: {', '.join(unexpected)}")
    accepted_statuses = {"passed", "unsupported-by-source"}
    statuses = {test_id: classify_diagnostic(record) for test_id, record in indexed.items()}
    failed = {test_id: status for test_id, status in statuses.items()
              if status not in accepted_statuses}
    if failed:
        detail = ", ".join(f"{test_id}={status}" for test_id, status in sorted(failed.items()))
        raise ValueError(f"failed diagnostic: {detail}")
    return {
        "accepted": True,
        "test_count": len(indexed),
        "failed_count": 0,
        "terminal_count": sum(status == "unsupported-by-source"
                              for status in statuses.values()),
    }
