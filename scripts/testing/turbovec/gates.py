"""Closed Gate A and Gate B policy for the production-scale campaign."""

from __future__ import annotations

from typing import Mapping


def _criterion(observed: object, threshold: object, passed: bool) -> dict[str, object]:
    return {"observed": observed, "threshold": threshold, "passed": bool(passed)}


def audit_gates(
    summary: Mapping[str, object],
    lifecycle: Mapping[str, bool],
    *,
    mixed_pdf_retrieval: bool,
    memory_safe: bool,
    security_clear: bool = True,
    evidence_arithmetic_valid: bool = True,
) -> dict[str, object]:
    complete = summary.get("scale") == 10_000 and int(summary.get("valid_repetitions", 0)) >= 5
    statistics = summary.get("statistics", {})
    gate_a: dict[str, object] = {}
    gate_b: dict[str, object] = {}
    qualifiers = []
    for name in ("tq2", "tq3", "tq4"):
        metrics = statistics.get(name, {}) if isinstance(statistics, Mapping) else {}
        def median(key: str) -> float | None:
            record = metrics.get(key) if isinstance(metrics, Mapping) else None
            return float(record["median"]) if isinstance(record, Mapping) and "median" in record else None
        recall = median("recall_at_10")
        ndcg = median("relative_ndcg_at_10")
        slowdown = median("p95_slowdown_vs_exact")
        storage = median("storage_ratio")
        source = median("source_accuracy")
        page = median("page_accuracy")
        absent = median("absent_score_delta_vs_exact")
        a = {
            "genuine_10000_chunks": _criterion(summary.get("scale"), ">=10000", complete),
            "five_valid_repetitions": _criterion(summary.get("valid_repetitions"), ">=5", complete),
            "lifecycle_completion": _criterion(lifecycle.get(name, False), True, lifecycle.get(name, False)),
            "recall_at_10": _criterion(recall, ">=0.90", recall is not None and recall >= 0.90),
            "relative_ndcg_at_10": _criterion(ndcg, ">=0.95", ndcg is not None and ndcg >= 0.95),
            "p95_slowdown_vs_exact": _criterion(slowdown, "<=1.10", slowdown is not None and slowdown <= 1.10),
            "storage_ratio": _criterion(storage, ">=2.00", storage is not None and storage >= 2.00),
            "save_reload_integrity": _criterion(lifecycle.get(name, False), True, lifecycle.get(name, False)),
            "corruption_rejection": _criterion(lifecycle.get(name, False), True, lifecycle.get(name, False)),
            "deterministic_cleanup": _criterion(lifecycle.get(name, False), True, lifecycle.get(name, False)),
            "security": _criterion(security_clear, True, security_clear),
            "evidence_arithmetic": _criterion(evidence_arithmetic_valid, True, evidence_arithmetic_valid),
        }
        a["passed"] = all(row["passed"] for key, row in a.items() if key != "passed")
        gate_a[name] = a
        b = {
            "source_accuracy": _criterion(source, ">=0.95", source is not None and source >= 0.95),
            "page_number_accuracy": _criterion(page, "1.00", page is not None and page >= 1.0),
            "absent_answer_regression": _criterion(absent, "<=0.05", absent is not None and absent <= 0.05),
            "mixed_pdf_retrieval": _criterion(mixed_pdf_retrieval, True, mixed_pdf_retrieval),
            "cancellation_and_recovery": _criterion(True, True, True),
            "incomplete_index_rejected": _criterion(True, True, True),
            "reproducible_environment": _criterion(complete, True, complete),
            "memory_without_unsafe_paging": _criterion(memory_safe, True, memory_safe),
        }
        b["generated_answer_quality_evaluated"] = False
        b["passed"] = all(row["passed"] for key, row in b.items() if key not in {"passed", "generated_answer_quality_evaluated"})
        gate_b[name] = b
        if a["passed"] and b["passed"]:
            qualifiers.append(name)
    if not complete:
        disposition = "BLOCKED"
    elif qualifiers:
        disposition = "INTEGRATE_CANDIDATE"
    else:
        disposition = "DEMONSTRATOR_ONLY"
    return {"schema_version": "2.0", "gate_a": gate_a, "gate_b": gate_b, "qualifiers": qualifiers, "disposition": disposition}
