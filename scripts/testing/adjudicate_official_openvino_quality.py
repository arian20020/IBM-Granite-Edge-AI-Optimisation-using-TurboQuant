"""Build blind WB-04 scoring inputs and apply the frozen harsh rubric.

Scoring is deliberately split into two stages.  The first stage validates raw
P1-P6 evidence and emits only opaque response labels.  The second accepts a
complete human score sheet, revalidates all hashes and objective gates, applies
non-overridable caps, and only then joins the external unblinding map.

The score sheet is schema version 1 and binds ``scoring_input_sha256``.  Every
blind response supplies all five ``dimensions``, explicit (possibly empty)
``manual_critical_caps`` and matching reasons, an unsupported-statement count,
reviewer, and notes.  No score or adjudication field is synthesized.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import statistics
import sys
from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.quality import (  # noqa: E402
    score_response as aggregate_prompt_scores,
)
from scripts.testing.run_official_openvino_quality import (  # noqa: E402
    EXPECTED_GENERATION_SETTINGS,
    FROZEN_PROMPT_SET_SHA256,
    FROZEN_PROMPT_SHA256S,
    PROMPT_IDS,
    QualityConfiguration,
    atomic_write_json,
    build_completion_artifact,
    load_prompt_contract,
    parse_json_bytes_strict,
    read_json_strict,
    reject_nulls,
    validate_completion_artifact,
    validate_request_artifact,
    validate_response_artifact,
)


DIMENSION_NAMES = (
    "correctness_and_grounding",
    "instruction_and_format_adherence",
    "completeness_and_fact_retention",
    "relevance_clarity_and_coherence",
    "stability_and_output_integrity",
)
FROZEN_RUBRIC_SHA256 = (
    "a36016f66e02c9e28f0938cf81335dc4b522e9f92b7d9bad3031f90b7ef91d90"
)
DEFAULT_PROMPT_SET_PATH = (
    ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "prompts"
    / "fixed-feasibility-prompt-set-v1.json"
)
_SHA256 = re.compile(r"^[0-9a-f]{64}$")
_BULLET = re.compile(r"^\s*(?:[-*•]|\d+[.)])\s+")
_WORD = re.compile(r"\b[A-Za-z0-9]+(?:[-'][A-Za-z0-9]+)*\b")


def _canonical_json(value: Any) -> bytes:
    reject_nulls(value)
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_text(value: str) -> str:
    return _sha256_bytes(value.encode("utf-8"))


def _require_sha256(value: Any, field: str) -> str:
    if not isinstance(value, str) or not _SHA256.fullmatch(value):
        raise ValueError(f"{field} must be a lowercase SHA-256")
    return value


def load_rubric(rubric_path: Path) -> dict[str, Any]:
    rubric_bytes = rubric_path.read_bytes()
    rubric_sha256 = _sha256_bytes(rubric_bytes)
    if rubric_sha256 != FROZEN_RUBRIC_SHA256:
        raise ValueError("frozen rubric hash mismatch")
    rubric = parse_json_bytes_strict(rubric_bytes, source=rubric_path)
    if not isinstance(rubric, dict):
        raise ValueError("rubric must be a JSON object")
    if rubric.get("rubric_id") != "GTQ-QUALITY-RUBRIC-v1":
        raise ValueError("unexpected rubric_id")
    rows = rubric.get("dimensions")
    if not isinstance(rows, list) or len(rows) != len(DIMENSION_NAMES):
        raise ValueError("rubric must define all five controlling dimensions")
    weights: dict[str, float] = {}
    caps: dict[str, str] = {}
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("rubric dimensions must be objects")
        name = row.get("name")
        weight = row.get("weight")
        cap = row.get("critical_cap")
        if (
            name not in DIMENSION_NAMES
            or name in weights
            or isinstance(weight, bool)
            or not isinstance(weight, (int, float))
            or not math.isfinite(float(weight))
            or float(weight) <= 0
            or not isinstance(cap, str)
            or not cap.strip()
        ):
            raise ValueError("invalid controlling rubric dimension")
        weights[name] = float(weight)
        caps[name] = cap
    if tuple(weights) != DIMENSION_NAMES or not math.isclose(
        sum(weights.values()), 1.0, rel_tol=0, abs_tol=1e-12
    ):
        raise ValueError("rubric weights must match the five controlling dimensions")
    anchors = rubric.get("anchors")
    procedure = rubric.get("procedure")
    if not isinstance(anchors, dict) or not isinstance(procedure, list):
        raise ValueError("rubric anchors and procedure are required")
    return {
        "rubric_id": rubric["rubric_id"],
        "rubric_sha256": rubric_sha256,
        "weights": weights,
        "caps": caps,
        "anchors": anchors,
        "procedure": procedure,
    }


def _prompt_controls(prompt_set_path: Path) -> dict[str, dict[str, Any]]:
    prompt_set_bytes = prompt_set_path.read_bytes()
    if _sha256_bytes(prompt_set_bytes) != FROZEN_PROMPT_SET_SHA256:
        raise ValueError("frozen prompt-set hash mismatch")
    prompt_set = parse_json_bytes_strict(
        prompt_set_bytes, source=prompt_set_path
    )
    rows = prompt_set.get("prompts") if isinstance(prompt_set, dict) else None
    if not isinstance(rows, list):
        raise ValueError("prompt set prompts must be a list")
    controls = {
        row.get("prompt_id"): row.get("deterministic_checks")
        for row in rows
        if isinstance(row, dict)
    }
    if set(controls) != set(PROMPT_IDS) or any(
        not isinstance(value, dict) for value in controls.values()
    ):
        raise ValueError("prompt set must provide P1-P6 deterministic checks")
    return controls


def _sentence_count(output: str) -> int:
    protected = output.replace("llama.cpp", "llama_cpp")
    return len(
        [
            part
            for part in re.split(r"(?<=[.!?])(?:[\"']?)(?:\s+|$)", protected.strip())
            if part.strip()
        ]
    )


def _gate_result(
    *, checks: Mapping[str, Any], reasons: Sequence[str], cap: float | None
) -> dict[str, Any]:
    passed = not reasons
    if passed and cap is not None:
        raise ValueError("a passing deterministic gate cannot have a cap")
    if not passed and cap is None:
        raise ValueError("a failing deterministic gate requires a cap")
    result = {
        "passed": passed,
        "checks": dict(checks),
        "critical_caps": [] if cap is None else [float(cap)],
        "reasons": list(reasons),
    }
    reject_nulls(result)
    return result


def deterministic_gate(
    prompt_id: str,
    output: str,
    turn_outputs: Sequence[Mapping[str, Any]],
    control: Mapping[str, Any],
) -> dict[str, Any]:
    """Apply objective P1-P6 checks; never infer a subjective content score."""

    if not isinstance(output, str) or not output.strip():
        return _gate_result(
            checks={"non_empty_output": False},
            reasons=["empty output is unusable"],
            cap=0,
        )

    if prompt_id == "P1":
        bullet_lines = [
            line for line in output.splitlines() if _BULLET.match(line)
        ]
        word_count = len(_WORD.findall(output))
        required = {
            term: term in output for term in control["required_substrings"]
        }
        benefit_count = len(
            re.findall(r"\bBenefits?\s*:", output, flags=re.IGNORECASE)
        )
        limitation_count = len(
            re.findall(r"\bLimitations?\s*:", output, flags=re.IGNORECASE)
        )
        check_count = len(re.findall(r"\bChecks?\s*:", output, flags=re.IGNORECASE))
        checks = {
            "bullet_count": len(bullet_lines),
            "bullet_count_expected": int(control["exact_bullet_count"]),
            "word_count": word_count,
            "maximum_words": int(control["maximum_words"]),
            "required_substrings": required,
            "benefit_slot_count": benefit_count,
            "limitation_slot_count": limitation_count,
            "check_slot_count": check_count,
        }
        reasons = []
        if len(bullet_lines) != int(control["exact_bullet_count"]):
            reasons.append("P1 exact bullet count failed")
        if word_count > int(control["maximum_words"]):
            reasons.append("P1 maximum word count failed")
        if not all(required.values()):
            reasons.append("P1 required substring check failed")
        if (benefit_count, limitation_count, check_count) != (2, 2, 1):
            reasons.append("P1 required semantic-slot labels failed")
        return _gate_result(
            checks=checks, reasons=reasons, cap=4 if reasons else None
        )

    if prompt_id == "P2":
        lines = [line.strip() for line in output.splitlines() if line.strip()]
        prefixes = list(control["line_prefixes"])
        prefix_matches = [
            index < len(lines)
            and lines[index].startswith(prefix)
            and bool(lines[index][len(prefix) :].strip())
            for index, prefix in enumerate(prefixes)
        ]
        checks = {
            "nonempty_line_count": len(lines),
            "line_count_expected": int(control["exact_nonempty_lines"]),
            "ordered_nonempty_prefix_matches": prefix_matches,
            "extra_text_absent": len(lines) == len(prefixes),
        }
        reasons = []
        if len(lines) != int(control["exact_nonempty_lines"]):
            reasons.append("P2 exact non-empty line count failed")
        if not all(prefix_matches):
            reasons.append("P2 ordered exact label check failed")
        return _gate_result(
            checks=checks, reasons=reasons, cap=4 if reasons else None
        )

    if prompt_id == "P3":
        fenced = "```" in output
        parse_error = ""
        try:
            value = json.loads(
                output,
                parse_constant=lambda item: (_ for _ in ()).throw(
                    ValueError(f"non-finite value {item}")
                ),
            )
        except (json.JSONDecodeError, ValueError) as exc:
            value = {}
            parse_error = type(exc).__name__
        exact_keys = list(control["exact_top_level_keys"])
        key_match = isinstance(value, dict) and set(value) == set(exact_keys)
        exact_values = isinstance(value, dict) and all(
            value.get(key) == expected
            for key, expected in control["exact_values"].items()
        )
        remaining = set(exact_keys) - set(control["exact_values"])
        string_values = isinstance(value, dict) and all(
            isinstance(value.get(key), str) and bool(value.get(key).strip())
            for key in remaining
        )
        checks = {
            "valid_json": not parse_error,
            "parse_error": parse_error or "none",
            "markdown_fence_absent": not fenced,
            "exact_top_level_keys": key_match,
            "exact_values": exact_values,
            "other_values_nonempty_strings": string_values,
        }
        reasons = [
            reason
            for failed, reason in (
                (bool(parse_error), "P3 valid JSON check failed"),
                (fenced, "P3 JSON-only Markdown-fence check failed"),
                (not key_match, "P3 exact top-level key check failed"),
                (not exact_values, "P3 exact value check failed"),
                (not string_values, "P3 string value check failed"),
            )
            if failed
        ]
        return _gate_result(
            checks=checks, reasons=reasons, cap=4 if reasons else None
        )

    if prompt_id == "P4":
        lower = output.lower()
        fact_checks = {
            "model": "IBM Granite 4.1 3B" in output,
            "runtime": "upstream llama.cpp" in output,
            "weight_format": "Q4_K_M" in output,
            "both_q8_caches": (
                "Q8_0" in output
                and (
                    re.search(r"\bK\s+and\s+V\s+caches?\b", output, re.IGNORECASE)
                    is not None
                    or (
                        re.search(r"Q8_0[^.!?]{0,40}\bK\s+cache\b", output)
                        is not None
                        and re.search(r"Q8_0[^.!?]{0,40}\bV\s+cache\b", output)
                        is not None
                    )
                )
            ),
            "context_length": "4096" in output,
            "local_windows_intel": all(
                token in lower for token in ("local", "windows", "intel")
            ),
            "turboquant_not_active": (
                "turboquant" in lower
                and any(
                    phrase in lower
                    for phrase in (
                        "not active",
                        "not activated",
                        "without activating",
                        "without turboquant",
                    )
                )
            ),
        }
        sentences = _sentence_count(output)
        checks = {
            "sentence_count": sentences,
            "sentence_count_expected": int(control["exact_sentences"]),
            "required_facts": fact_checks,
            "unsupported_fact_count_requires_manual_review": True,
        }
        reasons = []
        if sentences != int(control["exact_sentences"]):
            reasons.append("P4 exact sentence count failed")
        if not all(fact_checks.values()):
            reasons.append("P4 required fact retention failed")
        return _gate_result(
            checks=checks, reasons=reasons, cap=4 if reasons else None
        )

    if prompt_id == "P5":
        lines = [line for line in output.splitlines() if line.strip()]
        exact = output.strip() == control["exact_output"]
        checks = {
            "exact_output": exact,
            "nonempty_line_count": len(lines),
            "line_count_expected": int(control["exact_nonempty_lines"]),
        }
        reasons = []
        if not exact:
            reasons.append("P5 exact marker output failed")
        if len(lines) != int(control["exact_nonempty_lines"]):
            reasons.append("P5 exact line count failed")
        return _gate_result(
            checks=checks, reasons=reasons, cap=4 if reasons else None
        )

    if prompt_id == "P6":
        if not isinstance(turn_outputs, list) or len(turn_outputs) != 2:
            raise ValueError("P6 requires exactly two validated turn outputs")
        first = turn_outputs[0]["output"].strip()
        second = turn_outputs[1]["output"].strip()
        first_exact = first == control["turn_1_exact_output"]
        second_exact = second == control["turn_2_exact_output"]
        checks = {
            "turn_1_exact_output": first_exact,
            "turn_2_exact_output": second_exact,
        }
        reasons = []
        if not first_exact:
            reasons.append("P6 turn-one exact output failed")
        if not second_exact:
            reasons.append("P6 wrong remembered value")
        if not reasons:
            cap = None
        elif not first or not second:
            cap = 0
        elif not second_exact:
            cap = 2
        else:
            cap = 4
        return _gate_result(checks=checks, reasons=reasons, cap=cap)

    raise ValueError(f"unknown prompt_id: {prompt_id}")


def _collect_raw_responses(
    *,
    raw_root: Path,
    prompt_set_path: Path,
    rendered_root: Path,
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    contract = load_prompt_contract(prompt_set_path, rendered_root)
    controls = _prompt_controls(prompt_set_path)
    try:
        row_roots = sorted(
            path
            for path in raw_root.iterdir()
            if path.is_dir() and not path.name.startswith(".")
        )
    except OSError as exc:
        raise ValueError(f"cannot read raw quality root: {raw_root}: {exc}") from exc
    if not row_roots:
        raise ValueError("raw quality root contains no blind configurations")

    rows: list[dict[str, Any]] = []
    labels: set[str] = set()
    for row_root in row_roots:
        completion = read_json_strict(row_root / "completion.json")
        label = completion.get("blind_label") if isinstance(completion, dict) else None
        if label != row_root.name or label in labels:
            raise ValueError("blind configuration directory identity mismatch")
        labels.add(label)
        requests: dict[str, dict[str, Any]] = {}
        responses: dict[str, dict[str, Any]] = {}
        for prompt_id in PROMPT_IDS:
            prompt_root = row_root / prompt_id
            request = validate_request_artifact(
                read_json_strict(prompt_root / "request.json"), contract=contract
            )
            response = validate_response_artifact(
                read_json_strict(prompt_root / "response.json"), request
            )
            if request["blind_label"] != label or response["blind_label"] != label:
                raise ValueError("prompt artifact blind label mismatch")
            requests[prompt_id] = request
            responses[prompt_id] = response
        private_configuration = QualityConfiguration(
            test_id="_private_",
            blind_label=label,
            configuration_sha256=completion["configuration_sha256"],
            runtime_summary_path=Path("_private_"),
            executor_command=(),
        )
        expected_completion = build_completion_artifact(
            contract,
            private_configuration,
            completion["runtime_evidence_sha256"],
            requests,
            responses,
        )
        validate_completion_artifact(
            completion,
            requests=requests,
            responses=responses,
            expected=expected_completion,
        )
        for prompt_id in PROMPT_IDS:
            request = requests[prompt_id]
            response = responses[prompt_id]
            rows.append(
                {
                    "blind_label": label,
                    "prompt_id": prompt_id,
                    "prompt_sha256": request["prompt_sha256"],
                    "request_sha256": request["request_sha256"],
                    "response_sha256": response["response_sha256"],
                    "output": response["output"],
                    "output_sha256": response["output_sha256"],
                    "turn_outputs": response["turn_outputs"],
                    "deterministic_gate": deterministic_gate(
                        prompt_id,
                        response["output"],
                        response["turn_outputs"],
                        controls[prompt_id],
                    ),
                }
            )
    return contract, rows


def build_blind_scoring_input(
    *,
    raw_root: Path,
    prompt_set_path: Path,
    rendered_root: Path,
    rubric_path: Path,
) -> dict[str, Any]:
    """Create the reviewer-facing bundle without test IDs or codec labels."""

    contract, responses = _collect_raw_responses(
        raw_root=raw_root,
        prompt_set_path=prompt_set_path,
        rendered_root=rendered_root,
    )
    rubric = load_rubric(rubric_path)
    payload = {
        "schema_version": 1,
        "artifact_type": "openvino-quality-blind-scoring-input",
        "prompt_set_id": contract["prompt_set_id"],
        "prompt_set_sha256": contract["prompt_set_sha256"],
        "rubric_id": rubric["rubric_id"],
        "rubric_sha256": rubric["rubric_sha256"],
        "generation_settings": contract["generation_settings"],
        "prompt_contracts": contract["prompts"],
        "dimension_weights": rubric["weights"],
        "dimension_critical_caps": rubric["caps"],
        "rubric_anchors": rubric["anchors"],
        "rubric_procedure": rubric["procedure"],
        "responses": responses,
    }
    payload["scoring_input_sha256"] = _sha256_bytes(_canonical_json(payload))
    reject_nulls(payload)
    return payload


_SCORING_INPUT_FIELDS = {
    "schema_version",
    "artifact_type",
    "prompt_set_id",
    "prompt_set_sha256",
    "rubric_id",
    "rubric_sha256",
    "generation_settings",
    "prompt_contracts",
    "dimension_weights",
    "dimension_critical_caps",
    "rubric_anchors",
    "rubric_procedure",
    "responses",
    "scoring_input_sha256",
}
_SCORING_RESPONSE_FIELDS = {
    "blind_label",
    "prompt_id",
    "prompt_sha256",
    "request_sha256",
    "response_sha256",
    "output",
    "output_sha256",
    "turn_outputs",
    "deterministic_gate",
}


def _validate_scoring_input(
    scoring_input: Any,
    rubric: Mapping[str, Any],
    controls: Mapping[str, Mapping[str, Any]],
) -> dict[tuple[str, str], dict[str, Any]]:
    reject_nulls(scoring_input)
    if not isinstance(scoring_input, dict) or set(scoring_input) != _SCORING_INPUT_FIELDS:
        raise ValueError("scoring input has missing or unexpected fields")
    if (
        scoring_input["schema_version"] != 1
        or scoring_input["artifact_type"]
        != "openvino-quality-blind-scoring-input"
        or scoring_input["prompt_set_id"] != "GTQ-PROMPTS-v1"
        or scoring_input["prompt_set_sha256"] != FROZEN_PROMPT_SET_SHA256
        or scoring_input["rubric_id"] != rubric["rubric_id"]
        or scoring_input["rubric_sha256"] != rubric["rubric_sha256"]
        or scoring_input["generation_settings"] != EXPECTED_GENERATION_SETTINGS
        or scoring_input["dimension_weights"] != rubric["weights"]
        or scoring_input["dimension_critical_caps"] != rubric["caps"]
    ):
        raise ValueError("scoring input does not match the controlling rubric")
    _require_sha256(scoring_input["prompt_set_sha256"], "prompt_set_sha256")
    _require_sha256(scoring_input["scoring_input_sha256"], "scoring_input_sha256")
    unsigned = {
        key: value
        for key, value in scoring_input.items()
        if key != "scoring_input_sha256"
    }
    if _sha256_bytes(_canonical_json(unsigned)) != scoring_input[
        "scoring_input_sha256"
    ]:
        raise ValueError("scoring input hash mismatch")
    prompt_contracts = scoring_input["prompt_contracts"]
    if not isinstance(prompt_contracts, dict) or set(prompt_contracts) != set(
        PROMPT_IDS
    ):
        raise ValueError("scoring input must contain the P1-P6 prompt contracts")
    for prompt_id, prompt in prompt_contracts.items():
        if not isinstance(prompt, dict) or set(prompt) != {
            "prompt_id",
            "execution",
            "rendered_source_sha256s",
            "prompt_sha256",
        }:
            raise ValueError(f"{prompt_id} prompt contract has invalid fields")
        if prompt["prompt_id"] != prompt_id:
            raise ValueError(f"{prompt_id} prompt contract identity mismatch")
        if prompt["prompt_sha256"] != FROZEN_PROMPT_SHA256S[prompt_id]:
            raise ValueError(f"{prompt_id} is not the frozen prompt contract")
        expected_prompt_sha256 = _sha256_bytes(
            _canonical_json(
                {
                    "prompt_id": prompt_id,
                    "execution": prompt["execution"],
                    "rendered_source_sha256s": prompt[
                        "rendered_source_sha256s"
                    ],
                }
            )
        )
        if prompt["prompt_sha256"] != expected_prompt_sha256:
            raise ValueError(f"{prompt_id} prompt contract hash mismatch")
    responses = scoring_input["responses"]
    if not isinstance(responses, list) or not responses:
        raise ValueError("scoring input responses must be a non-empty list")
    indexed: dict[tuple[str, str], dict[str, Any]] = {}
    for row in responses:
        if not isinstance(row, dict) or set(row) != _SCORING_RESPONSE_FIELDS:
            raise ValueError("scoring response has missing or unexpected fields")
        label = row["blind_label"]
        prompt_id = row["prompt_id"]
        key = (label, prompt_id)
        if (
            not isinstance(label, str)
            or prompt_id not in PROMPT_IDS
            or key in indexed
        ):
            raise ValueError("duplicate or invalid blind scoring response")
        for field in (
            "prompt_sha256",
            "request_sha256",
            "response_sha256",
            "output_sha256",
        ):
            _require_sha256(row[field], field)
        if row["prompt_sha256"] != prompt_contracts[prompt_id]["prompt_sha256"]:
            raise ValueError("scoring response prompt hash mismatch")
        if not isinstance(row["output"], str):
            raise ValueError("scoring response output must be a string")
        if _sha256_text(row["output"]) != row["output_sha256"]:
            raise ValueError("scoring response output hash mismatch")
        turns = row["turn_outputs"]
        expected_turn_ids = ("turn_1", "turn_2") if prompt_id == "P6" else (
            "turn_1",
        )
        if not isinstance(turns, list) or len(turns) != len(expected_turn_ids):
            raise ValueError("scoring response has the wrong turn output count")
        for turn, expected_turn_id in zip(turns, expected_turn_ids):
            if not isinstance(turn, dict) or set(turn) != {
                "turn_id",
                "output",
                "output_sha256",
            }:
                raise ValueError("scoring turn output has invalid fields")
            if turn["turn_id"] != expected_turn_id:
                raise ValueError("scoring turn output order is invalid")
            if (
                not isinstance(turn["output"], str)
                or _sha256_text(turn["output"]) != turn["output_sha256"]
            ):
                raise ValueError("scoring turn output hash mismatch")
        if turns[-1]["output"] != row["output"]:
            raise ValueError("scoring final turn output does not match response")
        expected_response_sha256 = _sha256_bytes(
            _canonical_json({"prompt_id": prompt_id, "turn_outputs": turns})
        )
        if row["response_sha256"] != expected_response_sha256:
            raise ValueError("scoring complete response hash mismatch")
        gate = row["deterministic_gate"]
        if not isinstance(gate, dict) or set(gate) != {
            "passed",
            "checks",
            "critical_caps",
            "reasons",
        }:
            raise ValueError("invalid deterministic gate")
        if not isinstance(gate["passed"], bool):
            raise ValueError("deterministic gate passed must be Boolean")
        caps = gate["critical_caps"]
        reasons = gate["reasons"]
        if (
            not isinstance(caps, list)
            or any(
                isinstance(cap, bool)
                or not isinstance(cap, (int, float))
                or float(cap) not in {0.0, 2.0, 4.0}
                for cap in caps
            )
            or not isinstance(reasons, list)
            or any(
                not isinstance(reason, str) or not reason.strip()
                for reason in reasons
            )
            or not isinstance(gate["checks"], dict)
            or (gate["passed"] and (caps or reasons))
            or (not gate["passed"] and (not caps or not reasons))
        ):
            raise ValueError(
                "deterministic gate cap/reason state is inconsistent"
            )
        expected_gate = deterministic_gate(
            prompt_id,
            row["output"],
            row["turn_outputs"],
            controls[prompt_id],
        )
        if gate != expected_gate:
            raise ValueError(
                f"deterministic gate mismatch for {label} {prompt_id}"
            )
        indexed[key] = row
    labels = {label for label, _prompt_id in indexed}
    for label in labels:
        prompt_ids = {
            prompt_id for row_label, prompt_id in indexed if row_label == label
        }
        if prompt_ids != set(PROMPT_IDS):
            raise ValueError(f"{label} does not contain P1-P6 exactly once")
    return indexed


_SCORE_SHEET_FIELDS = {
    "schema_version",
    "artifact_type",
    "prompt_set_id",
    "rubric_id",
    "scoring_input_sha256",
    "adjudications",
}
_ADJUDICATION_FIELDS = {
    "blind_label",
    "prompt_id",
    "response_sha256",
    "dimensions",
    "manual_critical_caps",
    "manual_cap_reasons",
    "unsupported_statements_count",
    "reviewer",
    "notes",
}


def _normalise_score_sheet(
    score_sheet: Any,
    *,
    expected: Mapping[tuple[str, str], Mapping[str, Any]],
    rubric: Mapping[str, Any],
    prompt_set_id: str,
    scoring_input_sha256: str,
) -> dict[tuple[str, str], dict[str, Any]]:
    reject_nulls(score_sheet)
    if not isinstance(score_sheet, dict) or set(score_sheet) != _SCORE_SHEET_FIELDS:
        raise ValueError("score sheet has missing or unexpected fields")
    if (
        score_sheet["schema_version"] != 1
        or score_sheet["artifact_type"] != "openvino-quality-blind-scores"
        or score_sheet["prompt_set_id"] != prompt_set_id
        or score_sheet["rubric_id"] != rubric["rubric_id"]
        or score_sheet["scoring_input_sha256"] != scoring_input_sha256
        or not isinstance(score_sheet["adjudications"], list)
    ):
        raise ValueError("score sheet identity is invalid")
    indexed: dict[tuple[str, str], dict[str, Any]] = {}
    for row in score_sheet["adjudications"]:
        if not isinstance(row, dict) or set(row) != _ADJUDICATION_FIELDS:
            raise ValueError("adjudication has missing or unexpected fields")
        key = (row["blind_label"], row["prompt_id"])
        if key in indexed or key not in expected:
            raise ValueError("score sheet contains a duplicate or unknown adjudication")
        if row["response_sha256"] != expected[key]["response_sha256"]:
            raise ValueError(f"response hash mismatch for {key[0]} {key[1]}")
        dimensions = row["dimensions"]
        if not isinstance(dimensions, dict) or set(dimensions) != set(
            DIMENSION_NAMES
        ):
            raise ValueError("every controlling dimension must be scored")
        for name, score in dimensions.items():
            if (
                isinstance(score, bool)
                or not isinstance(score, (int, float))
                or not math.isfinite(float(score))
                or not 0 <= float(score) <= 10
            ):
                raise ValueError(f"invalid dimension score: {name}")
        caps = row["manual_critical_caps"]
        reasons = row["manual_cap_reasons"]
        if (
            not isinstance(caps, list)
            or any(
                isinstance(cap, bool)
                or not isinstance(cap, (int, float))
                or float(cap) not in {0.0, 2.0, 4.0}
                for cap in caps
            )
            or not isinstance(reasons, list)
            or len(caps) != len(reasons)
            or any(not isinstance(reason, str) or not reason.strip() for reason in reasons)
        ):
            raise ValueError("manual critical caps require matching harsh-rubric reasons")
        unsupported = row["unsupported_statements_count"]
        if isinstance(unsupported, bool) or not isinstance(unsupported, int) or unsupported < 0:
            raise ValueError("unsupported_statements_count must be a non-negative integer")
        if not isinstance(row["reviewer"], str) or not row["reviewer"].strip():
            raise ValueError("reviewer is required")
        if not isinstance(row["notes"], str) or not row["notes"].strip():
            raise ValueError("adjudication notes are required")
        indexed[key] = row
    if set(indexed) != set(expected):
        raise ValueError("score sheet must contain exactly one adjudication per response")

    # Content-identical responses for the same prompt cannot receive a score
    # advantage merely because they came from different hidden configurations.
    content_scores: dict[tuple[str, str], tuple[Any, ...]] = {}
    for key, row in indexed.items():
        prompt_id = key[1]
        response_digest = expected[key]["response_sha256"]
        content_key = (prompt_id, response_digest)
        scoring_decision = (
            tuple(float(row["dimensions"][name]) for name in DIMENSION_NAMES),
            tuple(sorted(float(cap) for cap in row["manual_critical_caps"])),
            row["unsupported_statements_count"],
            _canonical_json(expected[key]["deterministic_gate"]),
        )
        prior = content_scores.setdefault(content_key, scoring_decision)
        if prior != scoring_decision:
            raise ValueError(
                f"label-dependent adjudication for identical content: "
                f"{prompt_id}:{response_digest}"
            )
    return indexed


def _normalise_blind_map(
    blind_map: Any, expected_labels: set[str]
) -> dict[str, str]:
    reject_nulls(blind_map)
    if isinstance(blind_map, dict) and set(blind_map) == {
        "schema_version",
        "mapping",
    }:
        if blind_map["schema_version"] != 1:
            raise ValueError("blind map schema_version must be 1")
        blind_map = blind_map["mapping"]
    if not isinstance(blind_map, dict) or set(blind_map) != expected_labels:
        raise ValueError("blind map must map every and only scored blind label")
    if any(
        not isinstance(test_id, str) or not test_id.strip()
        for test_id in blind_map.values()
    ):
        raise ValueError("blind map test IDs must be non-empty strings")
    if len(set(blind_map.values())) != len(blind_map):
        raise ValueError("blind map test IDs must be unique")
    return dict(blind_map)


def adjudicate_quality(
    *,
    scoring_input: Mapping[str, Any],
    score_sheet: Mapping[str, Any],
    blind_map: Mapping[str, Any],
    rubric_path: Path,
    prompt_set_path: Path = DEFAULT_PROMPT_SET_PATH,
) -> dict[str, Any]:
    """Validate blind scores, apply caps, aggregate P1-P6, then unblind."""

    rubric = load_rubric(rubric_path)
    controls = _prompt_controls(prompt_set_path)
    responses = _validate_scoring_input(scoring_input, rubric, controls)
    adjudications = _normalise_score_sheet(
        score_sheet,
        expected=responses,
        rubric=rubric,
        prompt_set_id=scoring_input["prompt_set_id"],
        scoring_input_sha256=scoring_input["scoring_input_sha256"],
    )
    labels = {label for label, _prompt_id in responses}
    mapping = _normalise_blind_map(blind_map, labels)

    configuration_results: list[dict[str, Any]] = []
    for label in sorted(labels):
        prompt_results: dict[str, dict[str, Any]] = {}
        prompt_scores: dict[str, float] = {}
        for prompt_id in PROMPT_IDS:
            key = (label, prompt_id)
            raw = responses[key]
            scored = adjudications[key]
            dimensions = {
                name: float(scored["dimensions"][name]) for name in DIMENSION_NAMES
            }
            uncapped = round(
                sum(
                    dimensions[name] * rubric["weights"][name]
                    for name in DIMENSION_NAMES
                ),
                4,
            )
            objective_caps = [
                float(cap)
                for cap in raw["deterministic_gate"]["critical_caps"]
            ]
            manual_caps = [
                float(cap) for cap in scored["manual_critical_caps"]
            ]
            caps = sorted(set(objective_caps + manual_caps))
            final_score = round(min([uncapped, *caps]) if caps else uncapped, 4)
            cap_reasons = list(raw["deterministic_gate"]["reasons"]) + list(
                scored["manual_cap_reasons"]
            )
            prompt_scores[prompt_id] = final_score
            prompt_results[prompt_id] = {
                "prompt_sha256": raw["prompt_sha256"],
                "request_sha256": raw["request_sha256"],
                "response_sha256": raw["response_sha256"],
                "output_sha256": raw["output_sha256"],
                "deterministic_gate": raw["deterministic_gate"],
                "dimension_scores": dimensions,
                "unsupported_statements_count": scored[
                    "unsupported_statements_count"
                ],
                "uncapped_score": uncapped,
                "critical_caps": caps,
                "critical_cap_reasons": cap_reasons,
                "final_score": final_score,
                "reviewer": scored["reviewer"],
                "notes": scored["notes"],
            }
        # The existing official scorer is the controlling arithmetic mean and
        # explicitly ignores its format_label argument.
        mean_score = round(
            aggregate_prompt_scores(prompt_scores, format_label=label), 4
        )
        values = list(prompt_scores.values())
        configuration_results.append(
            {
                "test_id": mapping[label],
                "blind_label": label,
                "status": "complete",
                "prompt_count": 6,
                "mean_score": mean_score,
                "median_score": round(float(statistics.median(values)), 4),
                "minimum_score": round(min(values), 4),
                "maximum_score": round(max(values), 4),
                "prompts": prompt_results,
            }
        )

    result = {
        "schema_version": 1,
        "artifact_type": "openvino-quality-adjudication",
        "prompt_set_id": scoring_input["prompt_set_id"],
        "prompt_set_sha256": scoring_input["prompt_set_sha256"],
        "rubric_id": rubric["rubric_id"],
        "rubric_sha256": rubric["rubric_sha256"],
        "scoring_input_sha256": scoring_input["scoring_input_sha256"],
        "score_sheet_sha256": _sha256_bytes(_canonical_json(score_sheet)),
        "blind_map_sha256": _sha256_bytes(_canonical_json(mapping)),
        "configuration_count": len(configuration_results),
        "status": "complete",
        "configurations": configuration_results,
    }
    reject_nulls(result)
    return result


def _write_or_resume(path: Path, payload: Mapping[str, Any], *, resume: bool) -> None:
    if path.exists():
        if not resume:
            raise FileExistsError(f"refusing to overwrite evidence: {path}")
        if read_json_strict(path) != payload:
            raise ValueError(f"resume artifact does not match validated content: {path}")
        return
    atomic_write_json(path, payload)


def _read_blind_map(path: Path) -> dict[str, Any]:
    value = read_json_strict(path)
    if (
        isinstance(value, dict)
        and value.get("schema_version") == 1
        and isinstance(value.get("configurations"), list)
    ):
        mapping: dict[str, str] = {}
        for row in value["configurations"]:
            if not isinstance(row, dict) or not isinstance(
                row.get("blind_label"), str
            ) or not isinstance(row.get("test_id"), str):
                raise ValueError("configuration manifest cannot be used as a blind map")
            if row["blind_label"] in mapping:
                raise ValueError("duplicate blind label in configuration manifest")
            mapping[row["blind_label"]] = row["test_id"]
        return mapping
    if not isinstance(value, dict):
        raise ValueError("blind map must be a JSON object")
    return value


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build blind scoring inputs and adjudicate official P1-P6 evidence"
    )
    parser.add_argument("--raw-root", type=Path, required=True)
    parser.add_argument("--prompt-set", type=Path, required=True)
    parser.add_argument("--rendered-root", type=Path, required=True)
    parser.add_argument("--rubric", type=Path, required=True)
    parser.add_argument("--scoring-input", type=Path, required=True)
    parser.add_argument("--scores", type=Path)
    parser.add_argument("--blind-map", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--resume", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    scoring_input = build_blind_scoring_input(
        raw_root=args.raw_root,
        prompt_set_path=args.prompt_set,
        rendered_root=args.rendered_root,
        rubric_path=args.rubric,
    )
    _write_or_resume(args.scoring_input, scoring_input, resume=args.resume)
    final_paths = (args.scores, args.blind_map, args.output)
    if any(path is not None for path in final_paths):
        if not all(path is not None for path in final_paths):
            raise ValueError(
                "--scores, --blind-map, and --output are required together"
            )
        result = adjudicate_quality(
            scoring_input=scoring_input,
            score_sheet=read_json_strict(args.scores),
            blind_map=_read_blind_map(args.blind_map),
            rubric_path=args.rubric,
            prompt_set_path=args.prompt_set,
        )
        _write_or_resume(args.output, result, resume=args.resume)
        print(
            f"adjudicated {result['configuration_count']} configurations with "
            "complete P1-P6 scores"
        )
    else:
        print(
            f"wrote {len(scoring_input['responses'])} label-blind scoring responses"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
