"""Pure Gate A four-outcome policy."""

from __future__ import annotations

from dataclasses import dataclass
import math
from typing import Sequence

from .contracts import Decision, Thresholds


@dataclass(frozen=True)
class ConfigurationResult:
    name: str
    bit_width: int
    recall_at_10: float | None
    relative_ndcg_at_10: float | None
    query_p95_ms: float | None
    serving_bytes: int | None
    lifecycle_passed: bool


@dataclass(frozen=True)
class DecisionResult:
    outcome: Decision
    selected_configuration: str | None
    thresholds: dict[str, dict[str, dict[str, object]]]
    reasons: tuple[str, ...]


def decide(configurations: Sequence[ConfigurationResult], *, exact_p95: float, exact_bytes: int, prerequisites_complete: bool, policy: Thresholds = Thresholds(), external_blockers: Sequence[str] = ()) -> DecisionResult:
    if not prerequisites_complete:
        return DecisionResult(Decision.BLOCKED, None, {}, tuple(external_blockers) or ("required prerequisites unavailable before a fair measured run",))
    if not math.isfinite(exact_p95) or exact_p95 <= 0 or exact_bytes <= 0: raise ValueError("exact baseline is invalid")
    if not configurations: raise ValueError("candidate configurations are required")
    if any(not item.lifecycle_passed for item in configurations):
        return DecisionResult(Decision.EXCLUDE, None, {}, ("candidate correctness or lifecycle failure",))
    thresholds: dict[str, dict[str, dict[str, object]]] = {}; qualifiers: list[ConfigurationResult] = []
    for item in configurations:
        values = (item.recall_at_10, item.relative_ndcg_at_10, item.query_p95_ms, item.serving_bytes)
        if any(value is None for value in values): raise ValueError("incomplete candidate metrics")
        recall=float(item.recall_at_10); ndcg=float(item.relative_ndcg_at_10); p95=float(item.query_p95_ms); size=int(item.serving_bytes)
        if not all(math.isfinite(value) for value in (recall,ndcg,p95)) or size<=0: raise ValueError("invalid candidate metrics")
        ratio=exact_bytes/size; slowdown=p95/exact_p95
        rows={
            "recall_at_10":{"observed":recall,"threshold":policy.recall_at_10,"passed":recall>=policy.recall_at_10},
            "relative_ndcg_at_10":{"observed":ndcg,"threshold":policy.relative_ndcg_at_10,"passed":ndcg>=policy.relative_ndcg_at_10},
            "storage_ratio":{"observed":ratio,"threshold":policy.minimum_storage_ratio,"passed":ratio>=policy.minimum_storage_ratio},
            "p95_slowdown":{"observed":slowdown,"threshold":policy.maximum_p95_slowdown,"passed":slowdown<=policy.maximum_p95_slowdown},
            "lifecycle":{"observed":True,"threshold":True,"passed":True},
        }
        thresholds[item.name]=rows
        if all(row["passed"] for row in rows.values()): qualifiers.append(item)
    if not qualifiers: return DecisionResult(Decision.DEMONSTRATOR_ONLY,None,thresholds,("no configuration passed every Gate A threshold",))
    qualifiers.sort(key=lambda item:(-float(item.recall_at_10),float(item.query_p95_ms),int(item.serving_bytes),-item.bit_width))
    return DecisionResult(Decision.INTEGRATE,qualifiers[0].name,thresholds,())
