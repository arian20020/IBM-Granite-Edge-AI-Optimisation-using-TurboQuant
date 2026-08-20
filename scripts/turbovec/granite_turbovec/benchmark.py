"""Deterministic, privacy-safe retrieval benchmark metrics and evidence."""

from __future__ import annotations

import json
import math
import re
import statistics
from dataclasses import dataclass
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any, Iterable, Mapping, Sequence

from .contracts import ResearchError


_QUERY_ID = re.compile(r"q[0-9]{2}\Z")
_SAFE_IDENTITY = re.compile(r"[A-Za-z0-9][A-Za-z0-9._+-]{0,63}\Z")
_MAX_QUERIES = 99
_MAX_QUERY_LENGTH = 500
_GATE_SPECS = (
    ("recall_at_10", 0.85, ">="),
    ("mrr_ratio", 0.90, ">="),
    ("hit_at_5_delta", -0.05, ">="),
    ("size_ratio", 0.25, "<="),
    ("latency_ratio", 1.0, "<="),
)
_GATE_TOLERANCE = 1e-12
# Only Hit@5 uses this absolute threshold tolerance, to absorb rate-subtraction
# noise such as 17/20 - 18/20 producing -0.050000000000000044.


@dataclass(frozen=True)
class RankingMetrics:
    recall_at_k: float
    mrr: float
    hit_at_k: float

    def __post_init__(self) -> None:
        if not all(_finite_unit(value) for value in (self.recall_at_k, self.mrr, self.hit_at_k)):
            raise ResearchError("benchmark-evidence-invalid")

    def to_dict(self) -> dict[str, float]:
        return {"hit_at_k": self.hit_at_k, "mrr": self.mrr, "recall_at_k": self.recall_at_k}


@dataclass(frozen=True)
class MatchedRetrievalMetrics:
    exact_route: str
    candidate_route: str
    query_count: int
    top_k: int
    exact: RankingMetrics
    candidate: RankingMetrics
    mrr_ratio: float
    hit_delta: float

    def __post_init__(self) -> None:
        if (
            not _safe_identity(self.exact_route)
            or not _safe_identity(self.candidate_route)
            or self.exact_route == self.candidate_route
            or type(self.query_count) is not int
            or self.query_count <= 0
            or type(self.top_k) is not int
            or self.top_k <= 0
            or not isinstance(self.exact, RankingMetrics)
            or not isinstance(self.candidate, RankingMetrics)
            or not _finite_nonnegative(self.mrr_ratio)
            or not math.isclose(self.mrr_ratio, _safe_ratio(self.candidate.mrr, self.exact.mrr), rel_tol=1e-12, abs_tol=1e-15)
            or not _finite_between(self.hit_delta, -1, 1)
            or not math.isclose(self.hit_delta, self.candidate.hit_at_k - self.exact.hit_at_k, rel_tol=0, abs_tol=1e-15)
        ):
            raise ResearchError("benchmark-evidence-invalid")

    def to_dict(self) -> dict[str, Any]:
        return {
            "candidate": self.candidate.to_dict(),
            "candidate_route": self.candidate_route,
            "exact": self.exact.to_dict(),
            "exact_route": self.exact_route,
            "hit_delta": self.hit_delta,
            "mrr_ratio": self.mrr_ratio,
            "query_count": self.query_count,
            "top_k": self.top_k,
        }


@dataclass(frozen=True)
class TimingSummary:
    count: int
    median_seconds: float
    p95_seconds: float
    min_seconds: float
    max_seconds: float

    def __post_init__(self) -> None:
        values = (self.min_seconds, self.median_seconds, self.p95_seconds, self.max_seconds)
        if (
            type(self.count) is not int
            or self.count <= 0
            or not all(_finite_nonnegative(value) for value in values)
            or not self.min_seconds <= self.median_seconds <= self.p95_seconds <= self.max_seconds
        ):
            raise ResearchError("benchmark-evidence-invalid")

    def to_dict(self) -> dict[str, int | float]:
        return {
            "count": self.count,
            "max_seconds": self.max_seconds,
            "median_seconds": self.median_seconds,
            "min_seconds": self.min_seconds,
            "p95_seconds": self.p95_seconds,
        }


@dataclass(frozen=True)
class ColdWarmTimings:
    cold: TimingSummary
    warm: TimingSummary

    def __post_init__(self) -> None:
        if (
            not isinstance(self.cold, TimingSummary)
            or not isinstance(self.warm, TimingSummary)
            or self.cold.count < 1
            or self.warm.count < 5
        ):
            raise ResearchError("benchmark-evidence-invalid")

    def to_dict(self) -> dict[str, Any]:
        return {"cold": self.cold.to_dict(), "warm": self.warm.to_dict()}


@dataclass(frozen=True)
class GateMetrics:
    recall_at_10: float
    mrr_ratio: float
    hit_at_5_delta: float
    size_ratio: float
    latency_ratio: float

    def to_dict(self) -> dict[str, float]:
        return {
            "hit_at_5_delta": self.hit_at_5_delta,
            "latency_ratio": self.latency_ratio,
            "mrr_ratio": self.mrr_ratio,
            "recall_at_10": self.recall_at_10,
            "size_ratio": self.size_ratio,
        }


@dataclass(frozen=True)
class GateCriterion:
    name: str
    value: float
    threshold: float
    comparison: str
    passed: bool

    def __post_init__(self) -> None:
        expected = next((item for item in _GATE_SPECS if item[0] == self.name), None)
        if (
            expected is None
            or not _finite_number(self.value)
            or not _finite_number(self.threshold)
            or self.threshold != expected[1]
            or self.comparison != expected[2]
            or type(self.passed) is not bool
            or self.passed != _criterion_passes(self.name, self.value, self.threshold, self.comparison)
        ):
            raise ResearchError("benchmark-evidence-invalid")

    def to_dict(self) -> dict[str, Any]:
        return {
            "comparison": self.comparison,
            "name": self.name,
            "passed": self.passed,
            "threshold": self.threshold,
            "value": self.value,
        }


@dataclass(frozen=True)
class GateResult:
    passed: bool
    criteria: tuple[GateCriterion, ...]

    def __post_init__(self) -> None:
        if (
            type(self.criteria) is not tuple
            or not all(isinstance(item, GateCriterion) for item in self.criteria)
            or tuple(item.name for item in self.criteria) != tuple(item[0] for item in _GATE_SPECS)
            or type(self.passed) is not bool
            or self.passed != all(item.passed for item in self.criteria)
        ):
            raise ResearchError("benchmark-evidence-invalid")

    def to_dict(self) -> dict[str, Any]:
        return {"criteria": [item.to_dict() for item in self.criteria], "passed": self.passed}


@dataclass(frozen=True)
class RouteEvidence:
    route: str
    requested_backend: str
    actual_backend: str
    requested_provider: str
    actual_provider: str
    query_count: int
    top_k: int
    hit_at_5: float
    metrics: RankingMetrics
    timings: ColdWarmTimings

    def __post_init__(self) -> None:
        if (
            self.route not in {"float32", "turbovec-4bit"}
            or not all(
                _safe_identity(identity)
                for identity in (self.requested_backend, self.actual_backend, self.requested_provider, self.actual_provider)
            )
            or type(self.query_count) is not int
            or self.query_count <= 0
            or type(self.top_k) is not int
            or self.top_k <= 0
            or not _finite_unit(self.hit_at_5)
            or not isinstance(self.metrics, RankingMetrics)
            or not isinstance(self.timings, ColdWarmTimings)
        ):
            raise ResearchError("benchmark-evidence-invalid")

    def to_dict(self) -> dict[str, Any]:
        return {
            "actual_backend": self.actual_backend,
            "actual_provider": self.actual_provider,
            "metrics": self.metrics.to_dict(),
            "hit_at_5": self.hit_at_5,
            "query_count": self.query_count,
            "requested_backend": self.requested_backend,
            "requested_provider": self.requested_provider,
            "route": self.route,
            "top_k": self.top_k,
            "timings": self.timings.to_dict(),
        }


@dataclass(frozen=True)
class StorageEvidence:
    float32_vector_bytes: int
    candidate_persisted_bytes: int
    size_ratio: float

    def __post_init__(self) -> None:
        if (
            type(self.float32_vector_bytes) is not int
            or self.float32_vector_bytes <= 0
            or type(self.candidate_persisted_bytes) is not int
            or self.candidate_persisted_bytes <= 0
            or not _finite_nonnegative(self.size_ratio)
            or not math.isclose(
                self.size_ratio,
                self.candidate_persisted_bytes / self.float32_vector_bytes,
                rel_tol=1e-12,
                abs_tol=1e-15,
            )
        ):
            raise ResearchError("benchmark-evidence-invalid")

    def to_dict(self) -> dict[str, int | float]:
        return {
            "candidate_persisted_bytes": self.candidate_persisted_bytes,
            "float32_vector_bytes": self.float32_vector_bytes,
            "size_ratio": self.size_ratio,
        }


@dataclass(frozen=True)
class BenchmarkEvidence:
    schema_version: int
    query_count: int
    top_k: int
    baseline: RouteEvidence
    candidate: RouteEvidence
    storage: StorageEvidence
    gate: GateResult

    def __post_init__(self) -> None:
        if (
            not isinstance(self.baseline, RouteEvidence)
            or not isinstance(self.candidate, RouteEvidence)
            or not isinstance(self.storage, StorageEvidence)
            or not isinstance(self.gate, GateResult)
        ):
            raise ResearchError("benchmark-evidence-invalid")
        try:
            represented = GateMetrics(
                self.candidate.metrics.recall_at_k,
                _safe_ratio(self.candidate.metrics.mrr, self.baseline.metrics.mrr),
                self.candidate.hit_at_5 - self.baseline.hit_at_5,
                self.storage.size_ratio,
                _safe_ratio(
                    self.candidate.timings.warm.median_seconds,
                    self.baseline.timings.warm.median_seconds,
                ),
            )
            recomputed = evaluate_gate(represented)
        except ResearchError:
            raise ResearchError("benchmark-evidence-invalid") from None
        if (
            type(self.schema_version) is not int
            or self.schema_version != 1
            or type(self.query_count) is not int
            or self.query_count <= 0
            or type(self.top_k) is not int
            or self.top_k != 10
            or self.baseline.route != "float32"
            or self.candidate.route != "turbovec-4bit"
            or self.baseline.route == self.candidate.route
            or self.baseline.query_count != self.query_count
            or self.candidate.query_count != self.query_count
            or self.baseline.top_k != self.top_k
            or self.candidate.top_k != self.top_k
            or self.gate != recomputed
        ):
            raise ResearchError("benchmark-evidence-invalid")

    def to_dict(self) -> dict[str, Any]:
        return {
            "baseline": self.baseline.to_dict(),
            "candidate": self.candidate.to_dict(),
            "gate": self.gate.to_dict(),
            "query_count": self.query_count,
            "schema_version": self.schema_version,
            "storage": self.storage.to_dict(),
            "top_k": self.top_k,
        }


@dataclass(frozen=True)
class EvaluationQuery:
    id: str
    text: str
    relevant_sources: tuple[str, ...]

    def to_dict(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "relevant_sources": list(self.relevant_sources),
            "text": self.text,
        }


@dataclass(frozen=True)
class EvaluationFixture:
    schema_version: int
    top_k: int
    queries: tuple[EvaluationQuery, ...]

    def to_dict(self) -> dict[str, Any]:
        return {
            "queries": [query.to_dict() for query in self.queries],
            "schema_version": self.schema_version,
            "top_k": self.top_k,
        }


def evaluate_rankings(
    exact: Sequence[Sequence[int | str]],
    candidate: Sequence[Sequence[int | str]],
    relevant: Sequence[set[int | str] | frozenset[int | str]],
    k: int,
) -> RankingMetrics:
    identity_type = _validate_matched_rankings(exact, candidate, relevant, k)
    del identity_type
    recall_sum = 0.0
    reciprocal_sum = 0.0
    hit_sum = 0
    for baseline_row, candidate_row, judged in zip(exact, candidate, relevant):
        recall_sum += len(set(baseline_row[:k]) & set(candidate_row[:k])) / k
        first = next((rank for rank, item in enumerate(candidate_row[:k], 1) if item in judged), None)
        if first is not None:
            reciprocal_sum += 1.0 / first
            hit_sum += 1
    count = len(exact)
    return RankingMetrics(recall_sum / count, reciprocal_sum / count, hit_sum / count)


def compare_matched_routes(
    exact: Sequence[Sequence[int | str]],
    candidate: Sequence[Sequence[int | str]],
    relevant: Sequence[set[int | str] | frozenset[int | str]],
    k: int,
    *,
    exact_route: str = "float32",
    candidate_route: str = "candidate",
) -> MatchedRetrievalMetrics:
    candidate_metrics = evaluate_rankings(exact, candidate, relevant, k)
    exact_metrics = evaluate_rankings(exact, exact, relevant, k)
    mrr_ratio = _safe_ratio(candidate_metrics.mrr, exact_metrics.mrr)
    return MatchedRetrievalMetrics(
        exact_route,
        candidate_route,
        len(exact),
        k,
        exact_metrics,
        candidate_metrics,
        mrr_ratio,
        candidate_metrics.hit_at_k - exact_metrics.hit_at_k,
    )


def aggregate_timings(samples: Sequence[float], *, require_release_evidence: bool = False) -> TimingSummary:
    """Summarize seconds with conventional median and nearest-rank p95.

    For sorted ``N`` samples, p95 is ``samples[ceil(.95 * N) - 1]``. An even
    sample count uses the arithmetic mean of the two middle values for median.
    """
    if not isinstance(samples, Sequence) or isinstance(samples, (str, bytes)) or not samples:
        raise ResearchError("timing-samples-invalid")
    if require_release_evidence and len(samples) < 5:
        raise ResearchError("timing-sample-count-insufficient")
    values: list[float] = []
    for sample in samples:
        if isinstance(sample, bool) or not isinstance(sample, (int, float)):
            raise ResearchError("timing-samples-invalid")
        value = float(sample)
        if not math.isfinite(value) or value < 0:
            raise ResearchError("timing-samples-invalid")
        values.append(value)
    values.sort()
    return TimingSummary(
        len(values),
        statistics.median(values),
        _nearest_rank(values, 0.95),
        values[0],
        values[-1],
    )


def summarize_cold_warm(cold: Sequence[float], warm: Sequence[float], *, require_release_evidence: bool = True) -> ColdWarmTimings:
    if require_release_evidence and len(warm) < 5:
        raise ResearchError("timing-warm-count-insufficient")
    return ColdWarmTimings(
        aggregate_timings(cold),
        aggregate_timings(warm, require_release_evidence=require_release_evidence),
    )


def evaluate_gate(metrics: GateMetrics) -> GateResult:
    _validate_gate_metrics(metrics)
    criteria = (
        GateCriterion("recall_at_10", metrics.recall_at_10, 0.85, ">=", _criterion_passes("recall_at_10", metrics.recall_at_10, 0.85, ">=")),
        GateCriterion("mrr_ratio", metrics.mrr_ratio, 0.90, ">=", _criterion_passes("mrr_ratio", metrics.mrr_ratio, 0.90, ">=")),
        GateCriterion("hit_at_5_delta", metrics.hit_at_5_delta, -0.05, ">=", _criterion_passes("hit_at_5_delta", metrics.hit_at_5_delta, -0.05, ">=")),
        GateCriterion("size_ratio", metrics.size_ratio, 0.25, "<=", _criterion_passes("size_ratio", metrics.size_ratio, 0.25, "<=")),
        GateCriterion("latency_ratio", metrics.latency_ratio, 1.0, "<=", _criterion_passes("latency_ratio", metrics.latency_ratio, 1.0, "<=")),
    )
    return GateResult(all(item.passed for item in criteria), criteria)


def build_matched_evidence(
    exact: Sequence[Sequence[int | str]],
    candidate: Sequence[Sequence[int | str]],
    relevant: Sequence[set[int | str] | frozenset[int | str]],
    *,
    baseline_timings: ColdWarmTimings,
    candidate_timings: ColdWarmTimings,
    float32_vector_bytes: int,
    candidate_persisted_bytes: int,
    requested_backend: str,
    actual_backend: str,
    requested_provider: str,
    actual_provider: str,
    baseline_route: str = "float32",
    candidate_route: str = "turbovec-4bit",
) -> BenchmarkEvidence:
    """Build evidence and all gate inputs from one matched set of rankings.

    Recall and MRR are measured at 10. Hit delta is measured at 5 from the
    same query rows, preventing callers from combining unrelated runs.
    """
    at_ten = compare_matched_routes(
        exact, candidate, relevant, 10, exact_route=baseline_route, candidate_route=candidate_route
    )
    at_five = compare_matched_routes(
        exact, candidate, relevant, 5, exact_route=baseline_route, candidate_route=candidate_route
    )
    if type(float32_vector_bytes) is not int or float32_vector_bytes <= 0:
        raise ResearchError("storage-bytes-invalid")
    if type(candidate_persisted_bytes) is not int or candidate_persisted_bytes <= 0:
        raise ResearchError("storage-bytes-invalid")
    size_ratio = _safe_ratio(candidate_persisted_bytes, float32_vector_bytes)
    latency_ratio = _safe_ratio(
        candidate_timings.warm.median_seconds,
        baseline_timings.warm.median_seconds,
    )
    gate = evaluate_gate(
        GateMetrics(
            at_ten.candidate.recall_at_k,
            at_ten.mrr_ratio,
            at_five.hit_delta,
            size_ratio,
            latency_ratio,
        )
    )
    evidence = BenchmarkEvidence(
        1,
        at_ten.query_count,
        10,
        RouteEvidence(
            baseline_route,
            "float32",
            "float32",
            requested_provider,
            actual_provider,
            at_ten.query_count,
            10,
            at_five.exact.hit_at_k,
            at_ten.exact,
            baseline_timings,
        ),
        RouteEvidence(
            candidate_route,
            requested_backend,
            actual_backend,
            requested_provider,
            actual_provider,
            at_ten.query_count,
            10,
            at_five.candidate.hit_at_k,
            at_ten.candidate,
            candidate_timings,
        ),
        StorageEvidence(float32_vector_bytes, candidate_persisted_bytes, size_ratio),
        gate,
    )
    # Validate identities, storage consistency, and finite serialization now.
    evidence.to_dict()
    return evidence


def load_evaluation_fixture(path: str | Path) -> EvaluationFixture:
    try:
        text = Path(path).read_text(encoding="utf-8")
        payload = json.loads(text, object_pairs_hook=_reject_duplicate_keys)
    except (OSError, UnicodeError, json.JSONDecodeError, ResearchError):
        raise ResearchError("evaluation-fixture-invalid") from None
    try:
        if type(payload) is not dict or set(payload) != {"schema_version", "top_k", "queries"}:
            raise ValueError
        if type(payload["schema_version"]) is not int or payload["schema_version"] != 1:
            raise ValueError
        top_k = payload["top_k"]
        queries = payload["queries"]
        if type(top_k) is not int or top_k != 10:
            raise ValueError
        if type(queries) is not list or not 1 <= len(queries) <= _MAX_QUERIES:
            raise ValueError
        parsed: list[EvaluationQuery] = []
        seen: set[str] = set()
        for item in queries:
            if type(item) is not dict or set(item) != {"id", "text", "relevant_sources"}:
                raise ValueError
            query_id = item["id"]
            question = item["text"]
            sources = item["relevant_sources"]
            if type(query_id) is not str or not _QUERY_ID.fullmatch(query_id) or query_id in seen:
                raise ValueError
            if type(question) is not str or not question.strip() or len(question) > _MAX_QUERY_LENGTH:
                raise ValueError
            if (
                type(sources) is not list
                or len(sources) > 32
                or any(type(source) is not str for source in sources)
                or len(sources) != len({source.casefold() for source in sources})
            ):
                raise ValueError
            if any(not _safe_relative_source(source) for source in sources):
                raise ValueError
            seen.add(query_id)
            parsed.append(EvaluationQuery(query_id, question, tuple(sources)))
        return EvaluationFixture(1, top_k, tuple(parsed))
    except (KeyError, TypeError, ValueError):
        raise ResearchError("evaluation-fixture-invalid") from None


def canonical_json(value: BenchmarkEvidence | EvaluationFixture | Mapping[str, Any]) -> str:
    payload = value.to_dict() if hasattr(value, "to_dict") else dict(value)
    _ensure_json_numbers_finite(payload)
    return json.dumps(payload, ensure_ascii=False, allow_nan=False, separators=(",", ":"), sort_keys=True)


def render_markdown(evidence: BenchmarkEvidence) -> str:
    payload = evidence.to_dict()
    _ensure_json_numbers_finite(payload)
    criteria = payload["gate"]["criteria"]
    lines = [
        "# TurboVec retrieval benchmark",
        "",
        f"- Gate: {'PASS' if payload['gate']['passed'] else 'FAIL'}",
        f"- Queries: {payload['query_count']}",
        f"- Top-k: {payload['top_k']}",
        f"- Baseline route: {payload['baseline']['route']}",
        f"- Candidate route: {payload['candidate']['route']}",
        "",
        "| Criterion | Value | Threshold | Pass |",
        "|---|---:|---:|:---:|",
    ]
    for item in criteria:
        lines.append(f"| {item['name']} | {item['value']:.6g} | {item['comparison']} {item['threshold']:.6g} | {'yes' if item['passed'] else 'no'} |")
    return "\n".join(lines) + "\n"


def calculate_ratio(numerator: float, denominator: float) -> float:
    """Return a finite nonnegative ratio, failing closed on a zero baseline."""
    return _safe_ratio(numerator, denominator)


def _validate_matched_rankings(exact: Any, candidate: Any, relevant: Any, k: Any) -> type:
    if type(k) is not int or k <= 0:
        raise ResearchError("rankings-k-invalid")
    for collection in (exact, candidate, relevant):
        if not isinstance(collection, Sequence) or isinstance(collection, (str, bytes)):
            raise ResearchError("rankings-type-invalid")
    if not exact:
        raise ResearchError("rankings-empty")
    if len(exact) != len(candidate):
        raise ResearchError("rankings-query-count-mismatch")
    if len(exact) != len(relevant):
        raise ResearchError("rankings-relevance-count-mismatch")
    identity_type: type | None = None
    for exact_row, candidate_row, judged in zip(exact, candidate, relevant):
        for row in (exact_row, candidate_row):
            if not isinstance(row, Sequence) or isinstance(row, (str, bytes)):
                raise ResearchError("ranking-row-invalid")
            if len(row) < k:
                raise ResearchError("rankings-k-out-of-bounds")
            for item in row:
                identity_type = _validate_identity(item, identity_type, "ranking-id-invalid")
            if len(row) != len(set(row)):
                raise ResearchError("ranking-id-duplicate")
        if not isinstance(judged, (set, frozenset)):
            raise ResearchError("relevance-set-invalid")
        for item in judged:
            identity_type = _validate_identity(item, identity_type, "relevance-id-invalid")
    return identity_type or int


def _validate_identity(item: Any, identity_type: type | None, code: str) -> type:
    if isinstance(item, bool) or type(item) not in (int, str) or (type(item) is str and not item):
        raise ResearchError(code)
    if identity_type is not None and type(item) is not identity_type:
        raise ResearchError(code)
    return type(item)


def _nearest_rank(values: Sequence[float], quantile: float) -> float:
    # P95 uses nearest-rank: sorted[ceil(.95*N)-1]. Median is conventional.
    return values[max(0, math.ceil(quantile * len(values)) - 1)]


def _safe_ratio(numerator: Any, denominator: Any) -> float:
    for value in (numerator, denominator):
        if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(float(value)) or value < 0:
            raise ResearchError("metric-ratio-invalid")
    if denominator == 0:
        raise ResearchError("metric-baseline-zero")
    result = float(numerator) / float(denominator)
    if not math.isfinite(result):
        raise ResearchError("metric-ratio-invalid")
    return result


def _validate_gate_metrics(metrics: GateMetrics) -> None:
    values = (metrics.recall_at_10, metrics.mrr_ratio, metrics.hit_at_5_delta, metrics.size_ratio, metrics.latency_ratio)
    if any(isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(float(value)) for value in values):
        raise ResearchError("gate-metrics-invalid")
    if not 0 <= metrics.recall_at_10 <= 1 or metrics.mrr_ratio < 0 or not -1 <= metrics.hit_at_5_delta <= 1 or metrics.size_ratio < 0 or metrics.latency_ratio < 0:
        raise ResearchError("gate-metrics-invalid")


def _finite_nonnegative(value: Any) -> bool:
    return (
        not isinstance(value, bool)
        and isinstance(value, (int, float))
        and math.isfinite(float(value))
        and value >= 0
    )


def _finite_number(value: Any) -> bool:
    return not isinstance(value, bool) and isinstance(value, (int, float)) and math.isfinite(float(value))


def _finite_unit(value: Any) -> bool:
    return _finite_between(value, 0, 1)


def _finite_between(value: Any, minimum: float, maximum: float) -> bool:
    return _finite_number(value) and minimum <= value <= maximum


def _safe_identity(value: Any) -> bool:
    return type(value) is str and _SAFE_IDENTITY.fullmatch(value) is not None


def _criterion_passes(name: str, value: float, threshold: float, comparison: str) -> bool:
    if comparison == ">=":
        tolerance = _GATE_TOLERANCE if name == "hit_at_5_delta" else 0.0
        return value + tolerance >= threshold
    if comparison == "<=":
        return value <= threshold
    return False


def _reject_duplicate_keys(pairs: Iterable[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ResearchError("evaluation-fixture-invalid")
        result[key] = value
    return result


def _safe_relative_source(value: str) -> bool:
    if (
        not value
        or value.startswith("./")
        or value.endswith("/")
        or "//" in value
        or "\\" in value
        or ":" in value
        or any(ord(character) < 32 or ord(character) == 127 for character in value)
    ):
        return False
    raw_segments = value.split("/")
    if any(segment in {"", ".", ".."} for segment in raw_segments):
        return False
    posix = PurePosixPath(value)
    windows = PureWindowsPath(value)
    if posix.is_absolute() or windows.is_absolute() or windows.drive or any(part in {"", ".", ".."} for part in posix.parts):
        return False
    if any(part.endswith((".", " ")) or _is_dos_device(part) for part in posix.parts):
        return False
    return posix.suffix.casefold() in {".txt", ".md"}


def _is_dos_device(segment: str) -> bool:
    base = segment.split(".", 1)[0].rstrip(" .").casefold()
    return base in {"con", "prn", "aux", "nul"} or re.fullmatch(r"(?:com|lpt)[1-9]", base) is not None


def _ensure_json_numbers_finite(value: Any) -> None:
    if isinstance(value, Mapping):
        for item in value.values():
            _ensure_json_numbers_finite(item)
    elif isinstance(value, (list, tuple)):
        for item in value:
            _ensure_json_numbers_finite(item)
    elif isinstance(value, float) and not math.isfinite(value):
        raise ResearchError("benchmark-evidence-invalid")
