"""Evidence-only harsh quality scoring and terminal quality records."""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any


def score_response(scores: Mapping[str, float], *, format_label: str) -> float:
    del format_label
    required = {f"P{i}" for i in range(1, 7)}
    if set(scores) != required:
        raise ValueError("P1-P6 scores are required")
    values = [float(scores[key]) for key in sorted(required)]
    if any(value < 0 or value > 10 for value in values):
        raise ValueError("quality scores must be within 0-10")
    return sum(values) / len(values)


def terminal_quality_record(test_id: str, reason: str, source: str) -> dict[str, Any]:
    status = f"not-scored: {reason}"
    return {"test_id": test_id, "status": status, "mean_score": None,
            "prompts": {f"P{i}": {"score": None, "status": status, "source": source}
                        for i in range(1, 7)}, "source": source}
