"""Hash-bound, label-blind quality scoring for Official OpenVINO WB-04."""

from __future__ import annotations

import hashlib
import json
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
_GATE_FAILURE_CAPS = {
    "status_incomplete": 0.0,
    "p1_exact_bullet_count": 4.0,
    "p1_maximum_words": 4.0,
    "p1_required_substrings": 4.0,
    "p1_duplicate_semantic_evidence": 4.0,
    "p2_exact_line_count": 4.0,
    "p2_ordered_prefixes": 4.0,
    "p3_duplicate_json_key": 4.0,
    "p3_valid_json": 4.0,
    "p3_exact_key_set": 4.0,
    "p3_exact_value": 4.0,
    "p3_string_values": 4.0,
    "p3_markdown_fence": 4.0,
    "p4_exact_sentence_count": 4.0,
    "p4_reused_or_overlapping_fact_span": 4.0,
    "p4_reused_fact_text": 4.0,
    "p4_required_fact_text": 4.0,
    "p4_fact_not_bound_to_supported_sentence": 4.0,
    "p4_unsupported_statements": 4.0,
    "p5_exact_output": 4.0,
    "p5_exact_line_count": 4.0,
    "p6_turn_1_exact": 4.0,
    "p6_turn_2_exact": 2.0,
}
_PROMPT_FAILURE_CODES = {
    prompt_id: {
        "status_incomplete",
        *{
            code
            for code in _GATE_FAILURE_CAPS
            if code.startswith(prompt_id.casefold() + "_")
        },
    }
    for prompt_id in PROMPT_IDS
}


def _canonical_sha256(value: Any) -> str:
    try:
        payload = json.dumps(
            value,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    except (TypeError, ValueError) as exc:
        raise ValueError("quality evidence must be canonical JSON") from exc
    return hashlib.sha256(payload).hexdigest()


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


def _sentence_spans(output: str) -> list[tuple[int, int, str]]:
    protected = output.replace("llama.cpp", "llama_cpp")
    protected = re.sub(r"(?<=\d)\.(?=\d)", "_", protected)
    spans: list[tuple[int, int, str]] = []
    start = 0
    for match in re.finditer(r"[.!?](?=\s+|$)", protected):
        end = match.end()
        while start < end and output[start].isspace():
            start += 1
        if output[start:end].strip():
            spans.append((start, end, output[start:end]))
        start = end
    while start < len(output) and output[start].isspace():
        start += 1
    if output[start:].strip():
        spans.append((start, len(output), output[start:]))
    return spans


def _sentence_count(output: str) -> int:
    return len(_sentence_spans(output))


def _validate_content_evidence(
    content_evidence: Mapping[str, Any] | None,
    *,
    output_sha256: str,
    prompt_definition_sha256: str,
) -> str:
    if not isinstance(content_evidence, Mapping):
        raise ValueError("auditable content evidence is required")
    if content_evidence.get("schema_version") != 1:
        raise ValueError("content evidence schema version must be 1")
    if content_evidence.get("output_sha256") != output_sha256:
        raise ValueError("content evidence output SHA256 mismatch")
    if content_evidence.get("prompt_definition_sha256") != prompt_definition_sha256:
        raise ValueError("content evidence prompt definition SHA256 mismatch")
    _require_nonblank(content_evidence.get("reviewer_id"), "content evidence reviewer id")
    _require_nonblank(
        content_evidence.get("review_source"), "content evidence review source"
    )
    return _canonical_sha256(content_evidence)


def _validate_review_span(
    entry: Any,
    *,
    output: str,
    field: str,
    expected_verdict: str,
) -> tuple[int, int, str]:
    if not isinstance(entry, Mapping):
        raise ValueError(f"{field} must be an auditable span record")
    required = {"start", "end", "text", "verdict", "rationale"}
    if set(entry) != required:
        raise ValueError(f"{field} must contain exactly {sorted(required)}")
    start = entry["start"]
    end = entry["end"]
    if (
        isinstance(start, bool)
        or not isinstance(start, int)
        or isinstance(end, bool)
        or not isinstance(end, int)
        or start < 0
        or end <= start
        or end > len(output)
    ):
        raise ValueError(f"{field} offsets are invalid")
    text = entry["text"]
    if not isinstance(text, str) or not text.strip() or output[start:end] != text:
        raise ValueError(f"{field} text is not bound to the exact output span")
    if entry["verdict"] != expected_verdict:
        raise ValueError(f"{field} reviewed verdict mismatch")
    _require_nonblank(entry["rationale"], f"{field} rationale")
    return start, end, text


def _finalize_gate(result: dict[str, Any]) -> dict[str, Any]:
    result["gate_sha256"] = _canonical_sha256(result)
    return result


def evaluate_deterministic_gate(
    response: Mapping[str, Any],
    prompt_definition: Mapping[str, Any],
    *,
    content_evidence: Mapping[str, Any] | None = None,
    expected_prompt_set_sha256: str,
    expected_prompt_sha256: str,
    expected_prompt_definition_sha256: str,
) -> dict[str, Any]:
    """Apply the frozen prompt gate without inferring semantic facts from labels.

    P1 semantic roles and P4 fact-retention spans need explicit content review.
    The evidence is itself hashed so the eventual adjudication can be audited.
    """

    for field, expected in (
        ("prompt_set_sha256", expected_prompt_set_sha256),
        ("prompt_sha256", expected_prompt_sha256),
    ):
        _require_sha256(expected, f"expected_{field}")
        _require_sha256(response.get(field), field)
        if response[field] != expected:
            raise ValueError(f"{field.replace('_', ' ')} mismatch")
    _require_sha256(
        expected_prompt_definition_sha256,
        "expected_prompt_definition_sha256",
    )
    prompt_definition_sha256 = _canonical_sha256(prompt_definition)
    if prompt_definition_sha256 != expected_prompt_definition_sha256:
        raise ValueError("prompt definition SHA256 mismatch")

    prompt_id = response.get("prompt_id")
    if prompt_id not in PROMPT_IDS or prompt_definition.get("prompt_id") != prompt_id:
        raise ValueError("response and prompt definition must identify the same P1-P6 prompt")
    checks = prompt_definition.get("deterministic_checks")
    if not isinstance(checks, Mapping):
        raise ValueError("prompt deterministic checks are required")
    output = response.get("output")
    if not isinstance(output, str):
        raise ValueError("response output must be text")
    output_sha256 = hashlib.sha256(output.encode("utf-8")).hexdigest()
    _require_sha256(response.get("output_sha256"), "output_sha256")
    if response["output_sha256"] != output_sha256:
        raise ValueError("output SHA256 mismatch")
    if response.get("status") != "complete":
        return _finalize_gate({
            "schema_version": 1,
            "prompt_id": prompt_id,
            "passed": False,
            "failures": ["response status is not complete"],
            "failure_codes": ["status_incomplete"],
            "required_cap": 0.0,
            "checks": {"status_complete": False},
            "output_sha256": output_sha256,
            "prompt_set_sha256": response["prompt_set_sha256"],
            "prompt_sha256": response["prompt_sha256"],
            "prompt_definition_sha256": prompt_definition_sha256,
            "content_evidence_sha256": None,
        })

    failures: list[str] = []
    failure_codes: list[str] = []
    failure_caps: list[float] = []
    observed: dict[str, Any] = {"status_complete": True}
    evidence_hash: str | None = None

    def fail(code: str, message: str, cap: float) -> None:
        failure_codes.append(code)
        failures.append(message)
        failure_caps.append(cap)

    if prompt_id == "P1":
        bullet_matches = list(
            re.finditer(r"(?m)^[ \t]*(?:[-*]|\u2022)[ \t]+\S.*$", output)
        )
        bullets = [match.group(0) for match in bullet_matches]
        words = re.findall(r"\b[\w'-]+\b", output, flags=re.UNICODE)
        required = list(checks.get("required_substrings", ()))
        observed.update({
            "bullet_count": len(bullets),
            "word_count": len(words),
            "required_substrings": {
                term: term in output for term in required
            },
        })
        if len(bullets) != checks.get("exact_bullet_count"):
            fail("p1_exact_bullet_count", "exact bullet count failed", 4.0)
        if len(words) > checks.get("maximum_words"):
            fail("p1_maximum_words", "maximum word count failed", 4.0)
        if not all(observed["required_substrings"].values()):
            fail(
                "p1_required_substrings",
                "required substring retention failed",
                4.0,
            )
        evidence_hash = _validate_content_evidence(
            content_evidence,
            output_sha256=output_sha256,
            prompt_definition_sha256=prompt_definition_sha256,
        )
        slots = content_evidence.get("semantic_slots")
        if not isinstance(slots, Mapping):
            raise ValueError("P1 semantic slot evidence is required")
        expected_slot_keys = {"benefits", "limitations", "check"}
        if set(slots) != expected_slot_keys:
            raise ValueError("P1 semantic slot evidence has the wrong roles")
        benefits = slots["benefits"]
        limitations = slots["limitations"]
        check = slots["check"]
        if (
            not isinstance(benefits, list)
            or not isinstance(limitations, list)
            or not isinstance(check, list)
            or len(benefits) != 2
            or len(limitations) != 2
            or len(check) != 1
        ):
            raise ValueError(
                "P1 semantic slot evidence must assign each bullet once "
                "to two benefits, two limitations, and one check"
            )
        reviewed: list[tuple[int, int, str, int, str]] = []
        for role, singular in (
            ("benefits", "benefit"),
            ("limitations", "limitation"),
            ("check", "check"),
        ):
            for slot_index, entry in enumerate(slots[role]):
                if not isinstance(entry, Mapping):
                    raise ValueError("P1 semantic slot entries must be review records")
                bullet_index = entry.get("bullet_index")
                if (
                    isinstance(bullet_index, bool)
                    or not isinstance(bullet_index, int)
                    or bullet_index < 1
                    or bullet_index > len(bullet_matches)
                ):
                    raise ValueError("P1 semantic slot bullet index is invalid")
                span_entry = {key: value for key, value in entry.items()
                              if key != "bullet_index"}
                start, end, text = _validate_review_span(
                    span_entry,
                    output=output,
                    field=f"P1 {role}[{slot_index}]",
                    expected_verdict=singular,
                )
                match = bullet_matches[bullet_index - 1]
                if start != match.start() or end != match.end() or text != match.group(0):
                    raise ValueError("P1 semantic evidence does not match its bullet")
                reviewed.append((start, end, text, bullet_index, role))
        indices = [item[3] for item in reviewed]
        normalized_text = []
        for _, _, text, _, _ in reviewed:
            semantic_text = re.sub(
                r"^\s*(?:[-*]|\u2022)\s+",
                "",
                text,
            )
            semantic_text = re.sub(
                r"[^\w]+",
                " ",
                semantic_text,
                flags=re.UNICODE,
            ).casefold().strip()
            normalized_text.append(semantic_text)
        if sorted(indices) != list(range(1, len(bullet_matches) + 1)):
            raise ValueError("P1 semantic evidence must review each bullet exactly once")
        if len(set(normalized_text)) != len(normalized_text):
            fail(
                "p1_duplicate_semantic_evidence",
                "semantic evidence reuses duplicate bullet text",
                4.0,
            )
        observed["semantic_slots"] = dict(slots)

    elif prompt_id == "P2":
        lines = [line for line in output.splitlines() if line.strip()]
        prefixes = list(checks.get("line_prefixes", ()))
        prefix_match = (
            len(lines) == len(prefixes)
            and all(lines[index].startswith(prefix)
                    for index, prefix in enumerate(prefixes))
        )
        observed.update({
            "nonempty_line_count": len(lines),
            "line_prefixes_match": prefix_match,
        })
        if len(lines) != checks.get("exact_nonempty_lines"):
            fail("p2_exact_line_count", "exact non-empty line count failed", 4.0)
        if not prefix_match:
            fail(
                "p2_ordered_prefixes",
                "required ordered line prefixes failed",
                4.0,
            )

    elif prompt_id == "P3":
        fenced = "```" in output
        duplicate_key = False

        def reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
            nonlocal duplicate_key
            parsed_object: dict[str, Any] = {}
            for key, value in pairs:
                if key in parsed_object:
                    duplicate_key = True
                    raise ValueError(f"duplicate JSON key: {key}")
                parsed_object[key] = value
            return parsed_object

        try:
            parsed = json.loads(output, object_pairs_hook=reject_duplicate_keys)
        except (json.JSONDecodeError, ValueError):
            parsed = None
        exact_keys = list(checks.get("exact_top_level_keys", ()))
        exact_values = dict(checks.get("exact_values", {}))
        valid_object = isinstance(parsed, dict)
        key_match = valid_object and set(parsed) == set(exact_keys)
        value_match = valid_object and all(
            parsed.get(key) == value for key, value in exact_values.items()
        )
        other_string_values = valid_object and all(
            isinstance(parsed.get(key), str)
            for key in exact_keys
            if key not in exact_values
        )
        observed.update({
            "valid_json": parsed is not None,
            "exact_top_level_keys": key_match,
            "exact_values": value_match,
            "all_other_values_string": other_string_values,
            "markdown_fence": fenced,
            "duplicate_json_key": duplicate_key,
        })
        if duplicate_key:
            fail("p3_duplicate_json_key", "duplicate JSON key failed", 4.0)
        if parsed is None:
            fail("p3_valid_json", "valid JSON failed", 4.0)
        if not key_match:
            fail("p3_exact_key_set", "exact JSON key set failed", 4.0)
        if not value_match:
            fail("p3_exact_value", "exact JSON value failed", 4.0)
        if not other_string_values:
            fail("p3_string_values", "JSON string value type failed", 4.0)
        if fenced:
            fail("p3_markdown_fence", "prohibited Markdown fence present", 4.0)

    elif prompt_id == "P4":
        sentence_spans = _sentence_spans(output)
        sentence_count = len(sentence_spans)
        observed["sentence_count"] = sentence_count
        if sentence_count != checks.get("exact_sentences"):
            fail("p4_exact_sentence_count", "exact sentence count failed", 4.0)
        evidence_hash = _validate_content_evidence(
            content_evidence,
            output_sha256=output_sha256,
            prompt_definition_sha256=prompt_definition_sha256,
        )
        spans = content_evidence.get("required_fact_spans")
        required_facts = list(checks.get("required_facts", ()))
        if not isinstance(spans, Mapping) or set(spans) != set(required_facts):
            raise ValueError("P4 required fact span evidence is incomplete")
        fact_intervals: dict[str, tuple[int, int, str]] = {}
        for fact in required_facts:
            fact_intervals[fact] = _validate_review_span(
                spans[fact],
                output=output,
                field=f"P4 fact {fact}",
                expected_verdict="retained",
            )
            if fact not in fact_intervals[fact][2]:
                fail(
                    "p4_required_fact_text",
                    f"required fact text is absent from reviewed span: {fact}",
                    4.0,
                )
        observed["required_fact_spans"] = dict(spans)
        ordered_fact_intervals = sorted(
            (start, end, fact, text)
            for fact, (start, end, text) in fact_intervals.items()
        )
        for previous, current in zip(
            ordered_fact_intervals, ordered_fact_intervals[1:]
        ):
            if current[0] < previous[1]:
                fail(
                    "p4_reused_or_overlapping_fact_span",
                    "required fact evidence reuses or overlaps an output span",
                    4.0,
                )
                break
        normalized_fact_text = {
            re.sub(r"\s+", " ", text).casefold()
            for _, _, _, text in ordered_fact_intervals
        }
        if len(normalized_fact_text) != len(ordered_fact_intervals):
            fail(
                "p4_reused_fact_text",
                "required fact evidence reuses the same output text",
                4.0,
            )

        sentence_reviews = content_evidence.get("sentence_reviews")
        if (
            not isinstance(sentence_reviews, list)
            or len(sentence_reviews) != len(sentence_spans)
        ):
            raise ValueError("P4 sentence review evidence is incomplete")
        unsupported = 0
        reviewed_sentence_sources: list[set[str]] = []
        for index, (review, expected_span) in enumerate(
            zip(sentence_reviews, sentence_spans)
        ):
            if not isinstance(review, Mapping):
                raise ValueError("P4 sentence review must be a record")
            required_sentence_fields = {
                "start",
                "end",
                "text",
                "verdict",
                "rationale",
                "source_facts",
            }
            if set(review) != required_sentence_fields:
                raise ValueError("P4 sentence review fields mismatch")
            verdict = review["verdict"]
            if verdict not in {"supported", "unsupported"}:
                raise ValueError("P4 sentence verdict is invalid")
            span_entry = {
                key: review[key]
                for key in ("start", "end", "text", "verdict", "rationale")
            }
            actual_span = _validate_review_span(
                span_entry,
                output=output,
                field=f"P4 sentence {index + 1}",
                expected_verdict=verdict,
            )
            if actual_span != expected_span:
                raise ValueError("P4 sentence review is not bound to exact sentence")
            source_facts = review["source_facts"]
            if (
                not isinstance(source_facts, list)
                or any(fact not in required_facts for fact in source_facts)
                or (verdict == "supported" and not source_facts)
            ):
                raise ValueError("P4 sentence source facts are invalid")
            reviewed_sentence_sources.append(set(source_facts))
            if verdict == "unsupported":
                unsupported += 1
        for fact, (start, end, _) in fact_intervals.items():
            containing = [
                index
                for index, (sentence_start, sentence_end, _) in enumerate(sentence_spans)
                if sentence_start <= start and end <= sentence_end
            ]
            if (
                len(containing) != 1
                or fact not in reviewed_sentence_sources[containing[0]]
            ):
                fail(
                    "p4_fact_not_bound_to_supported_sentence",
                    f"required fact is not bound to a reviewed sentence: {fact}",
                    4.0,
                )
        observed["unsupported_statements_count"] = unsupported
        if unsupported != checks.get("unsupported_fact_count_expected"):
            fail(
                "p4_unsupported_statements",
                "unsupported statements were found",
                4.0,
            )

    elif prompt_id == "P5":
        nonempty_lines = [line for line in output.splitlines() if line.strip()]
        observed.update({
            "exact_output": output == checks.get("exact_output"),
            "nonempty_line_count": len(nonempty_lines),
        })
        if output != checks.get("exact_output"):
            fail("p5_exact_output", "exact output failed", 4.0)
        if len(nonempty_lines) != checks.get("exact_nonempty_lines"):
            fail("p5_exact_line_count", "exact non-empty line count failed", 4.0)

    elif prompt_id == "P6":
        turn_1 = response.get("turn_1")
        if not isinstance(turn_1, str):
            raise ValueError("P6 turn 1 output is required")
        turn_1_sha256 = hashlib.sha256(turn_1.encode("utf-8")).hexdigest()
        _require_sha256(response.get("turn_1_sha256"), "turn_1_sha256")
        if response["turn_1_sha256"] != turn_1_sha256:
            raise ValueError("turn 1 SHA256 mismatch")
        observed.update({
            "turn_1_exact": turn_1 == checks.get("turn_1_exact_output"),
            "turn_2_exact": output == checks.get("turn_2_exact_output"),
        })
        if not observed["turn_1_exact"]:
            fail("p6_turn_1_exact", "turn 1 exact output failed", 4.0)
        if not observed["turn_2_exact"]:
            fail("p6_turn_2_exact", "turn 2 exact output failed", 2.0)

    result = {
        "schema_version": 1,
        "prompt_id": prompt_id,
        "passed": not failures,
        "failures": failures,
        "failure_codes": failure_codes,
        "required_cap": min(failure_caps) if failure_caps else None,
        "checks": observed,
        "output_sha256": output_sha256,
        "prompt_set_sha256": response["prompt_set_sha256"],
        "prompt_sha256": response["prompt_sha256"],
        "prompt_definition_sha256": prompt_definition_sha256,
        "content_evidence_sha256": evidence_hash,
    }
    if prompt_id == "P6":
        result["turn_1_sha256"] = turn_1_sha256
    return _finalize_gate(result)


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
    deterministic_gate: Mapping[str, Any],
    response: Mapping[str, Any],
    prompt_definition: Mapping[str, Any],
    content_evidence: Mapping[str, Any] | None,
    expected_prompt_set_sha256: str,
    expected_prompt_sha256: str,
    expected_prompt_definition_sha256: str,
) -> dict[str, Any]:
    """Recompute a rubric score without allowing configuration-label bonuses."""

    del format_label
    if prompt_id not in PROMPT_IDS:
        raise ValueError("prompt id must be P1-P6")
    _require_sha256(output_sha256, "output_SHA256")
    if not isinstance(deterministic_gate, Mapping):
        raise ValueError("deterministic gate record is required")
    gate_required = {
        "schema_version",
        "prompt_id",
        "passed",
        "failures",
        "failure_codes",
        "required_cap",
        "checks",
        "output_sha256",
        "prompt_set_sha256",
        "prompt_sha256",
        "prompt_definition_sha256",
        "content_evidence_sha256",
        "gate_sha256",
    }
    missing_gate = sorted(gate_required - set(deterministic_gate))
    if missing_gate:
        raise ValueError(f"deterministic gate fields missing: {missing_gate}")
    if deterministic_gate["schema_version"] != 1:
        raise ValueError("deterministic gate schema version must be 1")
    if prompt_id == "P6":
        _require_sha256(
            deterministic_gate.get("turn_1_sha256"),
            "deterministic_gate_turn_1_sha256",
        )
    _require_sha256(deterministic_gate["gate_sha256"], "gate_sha256")
    gate_payload = {
        key: value
        for key, value in deterministic_gate.items()
        if key != "gate_sha256"
    }
    if deterministic_gate["gate_sha256"] != _canonical_sha256(gate_payload):
        raise ValueError("deterministic gate SHA256 mismatch")
    for field, expected in (
        ("prompt_set_sha256", expected_prompt_set_sha256),
        ("prompt_sha256", expected_prompt_sha256),
        ("prompt_definition_sha256", expected_prompt_definition_sha256),
    ):
        _require_sha256(expected, f"expected_{field}")
        _require_sha256(deterministic_gate[field], field)
        if deterministic_gate[field] != expected:
            raise ValueError(f"deterministic gate {field.replace('_', ' ')} mismatch")
    if deterministic_gate["prompt_id"] != prompt_id:
        raise ValueError("deterministic gate prompt id mismatch")
    if deterministic_gate["output_sha256"] != output_sha256:
        raise ValueError("deterministic gate output SHA256 mismatch")
    content_evidence_sha256 = deterministic_gate["content_evidence_sha256"]
    if prompt_id in {"P1", "P4"}:
        _require_sha256(content_evidence_sha256, "content_evidence_SHA256")
    elif content_evidence_sha256 is not None:
        _require_sha256(content_evidence_sha256, "content_evidence_SHA256")
    gate_passed = deterministic_gate["passed"]
    if not isinstance(gate_passed, bool):
        raise ValueError("deterministic gate passed flag must be boolean")
    failure_codes = deterministic_gate["failure_codes"]
    failures = deterministic_gate["failures"]
    if (
        not isinstance(failure_codes, list)
        or not all(isinstance(code, str) and code for code in failure_codes)
        or not isinstance(failures, list)
        or not all(isinstance(failure, str) and failure for failure in failures)
    ):
        raise ValueError("deterministic gate failures are invalid")
    gate_cap = deterministic_gate["required_cap"]
    if gate_passed:
        if failure_codes or failures or gate_cap is not None:
            raise ValueError("passing deterministic gate contains failure state")
    else:
        if not failure_codes or not failures:
            raise ValueError("failed deterministic gate must identify failures")
        gate_cap = _finite_score(gate_cap, "deterministic gate required cap")
        wrong_prompt_codes = [
            code
            for code in failure_codes
            if code not in _PROMPT_FAILURE_CODES[prompt_id]
        ]
        if wrong_prompt_codes:
            raise ValueError(
                f"deterministic gate failure code does not belong to {prompt_id}: "
                f"{sorted(set(wrong_prompt_codes))}"
            )
        unknown_codes = sorted(set(failure_codes) - set(_GATE_FAILURE_CAPS))
        if unknown_codes:
            raise ValueError(
                f"deterministic gate has unknown failure codes: {unknown_codes}"
            )
        failure_specific_cap = min(
            _GATE_FAILURE_CAPS[code] for code in failure_codes
        )
        if gate_cap != failure_specific_cap:
            raise ValueError("deterministic gate failure-specific cap mismatch")

    recomputed_gate = evaluate_deterministic_gate(
        response,
        prompt_definition,
        content_evidence=content_evidence,
        expected_prompt_set_sha256=expected_prompt_set_sha256,
        expected_prompt_sha256=expected_prompt_sha256,
        expected_prompt_definition_sha256=expected_prompt_definition_sha256,
    )
    if dict(deterministic_gate) != recomputed_gate:
        raise ValueError("recomputed deterministic gate does not match supplied gate")

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
    if deterministic_pass != gate_passed:
        raise ValueError("adjudication deterministic pass contradicts the gate")

    raw_caps = adjudication["critical_caps"]
    if isinstance(raw_caps, (str, bytes)) or not isinstance(raw_caps, Sequence):
        raise ValueError("critical caps must be a sequence")
    caps = tuple(_finite_score(cap, "critical cap") for cap in raw_caps)
    effective_caps = (*caps, gate_cap) if gate_cap is not None else caps

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
    score = min((uncapped, *effective_caps)) if effective_caps else uncapped
    return {
        "prompt_id": prompt_id,
        "output_sha256": output_sha256,
        "deterministic_gate_sha256": deterministic_gate["gate_sha256"],
        "prompt_set_sha256": deterministic_gate["prompt_set_sha256"],
        "prompt_sha256": deterministic_gate["prompt_sha256"],
        "prompt_definition_sha256": deterministic_gate[
            "prompt_definition_sha256"
        ],
        "dimensions": normalized_dimensions,
        "deterministic_pass": deterministic_pass,
        "critical_caps": list(caps),
        "deterministic_gate_cap": gate_cap,
        "effective_caps": list(effective_caps),
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
