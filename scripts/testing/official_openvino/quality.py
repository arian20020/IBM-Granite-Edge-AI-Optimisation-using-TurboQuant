"""Hash-bound, label-blind quality scoring for Official OpenVINO WB-04."""

from __future__ import annotations

import hashlib
import math
import re
import statistics
from collections.abc import Iterable, Mapping, Sequence
from typing import Any


PROMPT_IDS = frozenset(f"P{i}" for i in range(1, 7))
WEIGHTS = {
    "correctness_and_grounding": 0.30,
    "instruction_and_format_adherence": 0.25,
    "completeness_and_fact_retention": 0.20,
    "relevance_clarity_and_coherence": 0.15,
    "stability_and_output_integrity": 0.10,
}
GENERATION_SETTINGS = {
    "temperature": 0.0,
    "top_p": 1.0,
    "seed": 42,
    "max_output_tokens": 256,
}
ADJUDICATION_FIELDS = frozenset({
    "dimensions",
    "deterministic_pass",
    "critical_caps",
    "critical_cap_reason",
    "format_valid",
    "required_facts_retained",
    "unsupported_statements_count",
    "integrity_issue",
    "manual_result",
    "notes",
})
_SHA256 = re.compile(r"^[0-9a-f]{64}$")


def _require_sha256(value: Any, field: str) -> str:
    if not isinstance(value, str) or _SHA256.fullmatch(value) is None:
        raise ValueError(f"{field.replace('_', ' ')} must be a lowercase SHA256")
    return value


def _finite_score(value: Any, field: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{field} must be numeric")
    score = float(value)
    if not math.isfinite(score) or not 0 <= score <= 10:
        raise ValueError(f"{field} must be within 0-10")
    return score


def _require_nonblank(value: Any, field: str) -> None:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field} must be a non-blank string")


def validate_response_record(
    record: Mapping[str, Any],
    *,
    expected_runtime: Mapping[str, Any],
    expected_prompt_set_sha256: str,
    expected_prompt_sha256: str,
) -> dict[str, Any]:
    """Validate one raw response against its exact accepted runtime configuration.

    The prompt hash is the hash of the rendered prompt actually sent to the
    model. This matters for route-specific P4 facts and prevents a response from
    being silently reused under a different configuration.
    """

    required = {
        "schema_version",
        "status",
        "test_id",
        "context_tokens",
        "prompt_id",
        "prompt_set_id",
        "prompt_set_sha256",
        "prompt_sha256",
        "rubric_id",
        "runtime_summary_sha256",
        "runtime_config_sha256",
        "generation_settings",
        "output",
        "output_sha256",
    }
    missing = sorted(required - set(record))
    if missing:
        raise ValueError(f"response record fields missing: {missing}")
    if record["schema_version"] != 1:
        raise ValueError("response schema version must be 1")
    if record["status"] != "complete":
        raise ValueError("response status must be complete")
    if record["prompt_id"] not in PROMPT_IDS:
        raise ValueError("prompt id must be P1-P6")
    if record["prompt_set_id"] != "GTQ-PROMPTS-v1":
        raise ValueError("prompt set id mismatch")
    if record["rubric_id"] != "GTQ-QUALITY-RUBRIC-v1":
        raise ValueError("rubric id mismatch")

    for field in (
        "prompt_set_sha256",
        "prompt_sha256",
        "runtime_summary_sha256",
        "runtime_config_sha256",
        "output_sha256",
    ):
        _require_sha256(record[field], field)
    _require_sha256(expected_prompt_set_sha256, "expected_prompt_set_sha256")
    _require_sha256(expected_prompt_sha256, "expected_prompt_sha256")
    if record["prompt_set_sha256"] != expected_prompt_set_sha256:
        raise ValueError("prompt set sha256 mismatch")
    if record["prompt_sha256"] != expected_prompt_sha256:
        raise ValueError("prompt sha256 mismatch")

    runtime_fields = {
        "test_id",
        "context_tokens",
        "runtime_summary_sha256",
        "runtime_config_sha256",
    }
    missing_runtime = sorted(runtime_fields - set(expected_runtime))
    if missing_runtime:
        raise ValueError(f"expected runtime identity fields missing: {missing_runtime}")
    for field in sorted(runtime_fields):
        if record[field] != expected_runtime[field]:
            raise ValueError(f"{field.replace('_', ' ')} mismatch")

    settings = record["generation_settings"]
    if not isinstance(settings, Mapping) or dict(settings) != GENERATION_SETTINGS:
        raise ValueError("generation settings mismatch")
    output = record["output"]
    if not isinstance(output, str):
        raise ValueError("output must be text")
    actual_output_sha256 = hashlib.sha256(output.encode("utf-8")).hexdigest()
    if record["output_sha256"] != actual_output_sha256:
        raise ValueError("output sha256 mismatch")

    if record["prompt_id"] == "P6":
        if not isinstance(record.get("turn_1"), str):
            raise ValueError("P6 turn 1 output is required")
        _require_sha256(record.get("turn_1_sha256"), "turn_1_sha256")
        turn_1_sha256 = hashlib.sha256(record["turn_1"].encode("utf-8")).hexdigest()
        if record["turn_1_sha256"] != turn_1_sha256:
            raise ValueError("turn 1 sha256 mismatch")
    return dict(record)


def score_adjudication(
    prompt_id: str,
    output_sha256: str,
    adjudication: Mapping[str, Any],
    *,
    format_label: str,
) -> dict[str, Any]:
    """Recompute a rubric score without allowing configuration-label bonuses."""

    del format_label
    if prompt_id not in PROMPT_IDS:
        raise ValueError("prompt id must be P1-P6")
    _require_sha256(output_sha256, "output_SHA256")
    missing = sorted(ADJUDICATION_FIELDS - set(adjudication))
    if missing:
        raise ValueError(f"adjudication fields missing: {missing}")

    dimensions = adjudication["dimensions"]
    if not isinstance(dimensions, Mapping) or set(dimensions) != set(WEIGHTS):
        raise ValueError("adjudication must score every controlling dimension")
    normalized_dimensions = {
        name: _finite_score(dimensions[name], f"dimension {name}")
        for name in WEIGHTS
    }
    deterministic_pass = adjudication["deterministic_pass"]
    if not isinstance(deterministic_pass, bool):
        raise ValueError("deterministic pass must be boolean")

    raw_caps = adjudication["critical_caps"]
    if isinstance(raw_caps, (str, bytes)) or not isinstance(raw_caps, Sequence):
        raise ValueError("critical caps must be a sequence")
    caps = tuple(_finite_score(cap, "critical cap") for cap in raw_caps)
    if not deterministic_pass and not caps:
        raise ValueError("a deterministic gate failure requires an explicit critical cap")

    for field in (
        "critical_cap_reason",
        "format_valid",
        "required_facts_retained",
        "integrity_issue",
        "manual_result",
        "notes",
    ):
        _require_nonblank(adjudication[field], field)
    unsupported = adjudication["unsupported_statements_count"]
    if isinstance(unsupported, bool) or not isinstance(unsupported, int) or unsupported < 0:
        raise ValueError("unsupported statements count must be a non-negative integer")

    uncapped = sum(
        normalized_dimensions[name] * weight
        for name, weight in WEIGHTS.items()
    )
    score = min((uncapped, *caps)) if caps else uncapped
    return {
        "prompt_id": prompt_id,
        "output_sha256": output_sha256,
        "dimensions": normalized_dimensions,
        "deterministic_pass": deterministic_pass,
        "critical_caps": list(caps),
        "critical_cap_reason": adjudication["critical_cap_reason"],
        "format_valid": adjudication["format_valid"],
        "required_facts_retained": adjudication["required_facts_retained"],
        "unsupported_statements_count": unsupported,
        "integrity_issue": adjudication["integrity_issue"],
        "manual_result": adjudication["manual_result"],
        "notes": adjudication["notes"],
        "uncapped_score": uncapped,
        "score": score,
    }


def summarize_quality(
    test_id: str,
    prompt_records: Iterable[Mapping[str, Any]],
) -> dict[str, Any]:
    """Require exactly P1-P6 and recompute every aggregate from prompt scores."""

    records = list(prompt_records)
    by_prompt: dict[str, dict[str, Any]] = {}
    for record in records:
        prompt_id = record.get("prompt_id")
        if prompt_id in by_prompt:
            raise ValueError(f"duplicate quality record for {prompt_id}")
        if prompt_id not in PROMPT_IDS:
            raise ValueError("quality summary requires exactly P1-P6")
        _require_sha256(record.get("output_sha256"), "output_SHA256")
        score = _finite_score(record.get("score"), f"{prompt_id} score")
        normalized = dict(record)
        normalized["score"] = score
        by_prompt[prompt_id] = normalized
    if set(by_prompt) != PROMPT_IDS:
        raise ValueError("quality summary requires exactly P1-P6")

    ordered = [by_prompt[f"P{i}"] for i in range(1, 7)]
    scores = [record["score"] for record in ordered]
    return {
        "schema_version": 1,
        "test_id": test_id,
        "status": "scored",
        "prompt_count": len(scores),
        "mean_score": sum(scores) / len(scores),
        "median_score": statistics.median(scores),
        "min_score": min(scores),
        "max_score": max(scores),
        "prompts": {record["prompt_id"]: record for record in ordered},
    }


def score_response(scores: Mapping[str, float], *, format_label: str) -> float:
    """Compatibility helper for callers that already hold six final scores."""

    del format_label
    if set(scores) != PROMPT_IDS:
        raise ValueError("P1-P6 scores are required")
    values = [_finite_score(scores[f"P{i}"], f"P{i} quality score") for i in range(1, 7)]
    return sum(values) / len(values)


def terminal_quality_record(
    test_id: str,
    reason: str,
    source: str,
    *,
    evidence_sha256: str | None = None,
) -> dict[str, Any]:
    """Create a sourced non-score for a declared non-generating terminal row."""

    _require_nonblank(test_id, "test id")
    _require_nonblank(reason, "reason")
    _require_nonblank(source, "source")
    if evidence_sha256 is not None:
        _require_sha256(evidence_sha256, "evidence_sha256")
    if "expected" in reason.lower() and evidence_sha256 is None:
        raise ValueError("expected-rejection quality records require evidence SHA256")
    status = f"not-scored: {reason}"
    prompts = {
        f"P{i}": {
            "score": None,
            "status": status,
            "source": source,
            "evidence_sha256": evidence_sha256,
        }
        for i in range(1, 7)
    }
    return {
        "schema_version": 1,
        "test_id": test_id,
        "status": status,
        "prompt_count": 6,
        "mean_score": None,
        "median_score": None,
        "min_score": None,
        "max_score": None,
        "prompts": prompts,
        "source": source,
        "evidence_sha256": evidence_sha256,
    }
