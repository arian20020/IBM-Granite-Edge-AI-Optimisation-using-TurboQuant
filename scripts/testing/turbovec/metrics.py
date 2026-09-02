"""Pure retrieval and latency metrics."""

from __future__ import annotations

import math
from statistics import median
from typing import Mapping, Sequence


def recall_at_k(ranked: Sequence[int], expected: set[int], k: int) -> float:
    if not expected or k <= 0: raise ValueError("positive relevance and k are required")
    return len(set(ranked[:k]) & expected) / len(expected)


def ndcg_at_k(ranked: Sequence[int], grades: Mapping[int, int], k: int) -> float:
    if not grades or k <= 0 or any(value not in (1, 3) for value in grades.values()): raise ValueError("graded relevance is invalid")
    dcg = sum(grades.get(identifier, 0) / math.log2(rank + 2) for rank, identifier in enumerate(ranked[:k]))
    ideal = sum(grade / math.log2(rank + 2) for rank, grade in enumerate(sorted(grades.values(), reverse=True)[:k]))
    return dcg / ideal


def nearest_rank_percentile(values: Sequence[float], percentile: float) -> float:
    if not values or not 0 < percentile <= 1 or any(not math.isfinite(v) or v < 0 for v in values): raise ValueError("invalid percentile samples")
    ordered = sorted(values); return ordered[max(0, math.ceil(percentile * len(ordered)) - 1)]


def aggregate_latency(measured: Sequence[float]) -> dict[str, float]:
    if not measured or any(not math.isfinite(value) or value < 0 for value in measured): raise ValueError("measured latencies must be finite and non-negative")
    return {"p50": float(median(measured)), "p95": float(nearest_rank_percentile(measured, .95))}
