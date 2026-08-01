"""Blind, two-order adjudication for adaptive OpenVINO quality captures."""

from __future__ import annotations

import argparse
import math
import secrets
import statistics
import sys
from collections.abc import Callable, Mapping, Sequence
from itertools import combinations
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))


from scripts.testing.adjudicate_official_openvino_quality import (
    _canonical_json,
    _prompt_controls,
    _require_sha256,
    _sha256_bytes,
    _sha256_text,
    deterministic_gate,
    load_rubric,
)
from scripts.testing.official_openvino.adaptive_quality import CAPTURE_SCHEMA
from scripts.testing.official_openvino.quality_campaign import (
    _canonical_identity_bytes,
    _strict_object,
)
from scripts.testing.official_openvino.quality_worker import (
    PROMPT_RESULT_SCHEMA,
    PROMPT_SPEC_SCHEMA,
    _canonical_json as _task5_canonical_json,
    _validate_prompt_worker_spec,
)
from scripts.testing.run_official_openvino_quality import (
    FROZEN_PROMPT_SHA256S,
    FROZEN_PROMPT_SET_SHA256,
    PROMPT_IDS,
    _validate_blind_label,
    atomic_write_json,
    load_prompt_contract,
    read_json_strict,
    reject_nulls,
)


_SUMMARY_FIELDS = {
    "schema",
    "status",
    "completed_prompt_ids",
    "prompt_receipt_count",
    "prompt_receipts",
    "capture_summary_path",
    "previous_capture_summary_path",
    "previous_capture_summary_sha256",
    "campaign_identity_sha256",
    "runtime_summary_sha256",
    "matrix_sha256",
    "artifact_manifest_sha256",
    "prompt_set_sha256",
    "rubric_sha256",
    "runtime_property_sha256",
    "capture_summary_sha256",
}
_RECEIPT_FIELDS = {
    "prompt_id",
    "status",
    "prompt_root",
    "worker_spec_path",
    "worker_spec_sha256",
    "worker_result_path",
    "worker_result_sha256",
    "worker_log_path",
    "worker_log_sha256",
    "guard_evidence_path",
    "guard_evidence_sha256",
    "cleanup_process_count",
    "process_identity",
}
_OUTCOME_FIELDS = {
    "turn_id",
    "raw_prompt",
    "raw_prompt_sha256",
    "status",
    "raw_output",
    "raw_output_sha256",
    "failure_type",
    "failure_message",
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
_SCORE_SHEET_FIELDS = {
    "schema_version",
    "artifact_type",
    "judge_id",
    "prompt_set_id",
    "rubric_id",
    "scoring_input_sha256",
    "adjudications",
    "score_sheet_sha256",
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
_PAIR_FIELDS = {"pair_id", "blind_labels"}
_PAIRWISE_REVIEW_FIELDS = {
    "pair_id",
    "order",
    "left_blind_label",
    "right_blind_label",
    "winner",
    "reviewer",
    "reason",
    "review_sha256",
}
_MANUAL_PROMPT_FIELDS = {
    "record_type",
    "blind_label",
    "prompt_id",
    "response_sha256",
    "flags",
    "evidence",
    "rubric_basis",
    "final_score",
    "final_reason",
    "adjudicator",
    "record_sha256",
}
_MANUAL_PAIR_FIELDS = {
    "record_type",
    "pair_id",
    "flags",
    "evidence",
    "selected_winner",
    "rubric_basis",
    "final_reason",
    "adjudicator",
    "record_sha256",
}
_PRIVATE_IDENTITY_FIELDS = {
    "test_id",
    "context_tokens",
    "capture_summary_path",
    "capture_summary_file_sha256",
    "capture_summary_sha256",
    "campaign_identity_sha256",
    "runtime_summary_sha256",
    "matrix_sha256",
    "artifact_manifest_sha256",
    "prompt_set_sha256",
    "rubric_sha256",
    "runtime_property_sha256",
    "model_path",
    "device",
    "properties",
    "build_identity",
    "runtime_property",
}


def _read_task5_object(path: Path, *, label: str) -> tuple[dict[str, Any], bytes]:
    source = Path(path).resolve()
    if not source.is_file() or source.is_symlink():
        raise ValueError(f"{label} is missing or aliased")
    raw = source.read_bytes()
    value = _strict_object(raw, source=source)
    if raw != _task5_canonical_json(value):
        raise ValueError(f"{label} bytes are not canonical")
    return value, raw


def _read_guard_object(path: Path) -> tuple[dict[str, Any], bytes]:
    source = Path(path).resolve()
    if not source.is_file() or source.is_symlink():
        raise ValueError("quality prompt guard evidence is missing or aliased")
    raw = source.read_bytes()
    value = _strict_object(raw, source=source)
    if raw != _canonical_identity_bytes(value):
        raise ValueError("quality prompt guard evidence bytes are not canonical")
    return value, raw


def _require_exact_file(value: Any, *, expected: Path, field: str) -> Path:
    if not isinstance(value, str):
        raise ValueError(f"{field} must be an absolute path")
    supplied = Path(value)
    if not supplied.is_absolute() or supplied.resolve() != expected.resolve():
        raise ValueError(f"{field} is not the expected evidence path")
    if not supplied.is_file() or supplied.is_symlink():
        raise ValueError(f"{field} is missing or aliased")
    return supplied.resolve()


def _expected_spec_turns(
    prompt_id: str,
    contract: Mapping[str, Any],
) -> list[dict[str, Any]]:
    execution = contract["prompts"][prompt_id]["execution"]
    if prompt_id == "P6":
        return [
            {"turn_id": "P6-turn-1", "prompt": execution["turn_1_prompt"]},
            {
                "turn_id": "P6-turn-2",
                "prompt": execution["turn_2_prompt"],
                "history_source_turn_id": "P6-turn-1",
            },
        ]
    return [{"turn_id": prompt_id, "prompt": execution["prompt"]}]


def _validate_task5_result(
    result: Mapping[str, Any],
    *,
    spec: Mapping[str, Any],
    worker_spec_sha256: str,
) -> list[dict[str, str]]:
    if set(result) != {
        "schema",
        "prompt_id",
        "worker_spec_sha256",
        "outcomes",
        "worker_result_sha256",
    }:
        raise ValueError("quality prompt worker result fields are invalid")
    prompt_id = spec["prompt_id"]
    if (
        result.get("schema") != PROMPT_RESULT_SCHEMA
        or result.get("prompt_id") != prompt_id
        or result.get("worker_spec_sha256") != worker_spec_sha256
    ):
        raise ValueError("quality prompt worker result identity is invalid")
    _require_sha256(result.get("worker_result_sha256"), "worker_result_sha256")
    unsigned = {
        key: item for key, item in result.items() if key != "worker_result_sha256"
    }
    if result["worker_result_sha256"] != _sha256_bytes(
        _task5_canonical_json(unsigned)
    ):
        raise ValueError("quality prompt worker result hash is invalid")

    outcomes = result.get("outcomes")
    turns = spec["turns"]
    if not isinstance(outcomes, list) or len(outcomes) != len(turns):
        raise ValueError("quality prompt worker outcomes are invalid")
    projected: list[dict[str, str]] = []
    first_output = ""
    for index, (outcome, turn) in enumerate(zip(outcomes, turns, strict=True)):
        expected_fields = set(_OUTCOME_FIELDS)
        if prompt_id == "P6" and index == 1:
            expected_fields.add("history_source_sha256")
        if (
            not isinstance(outcome, dict)
            or set(outcome) != expected_fields
            or outcome.get("turn_id") != turn["turn_id"]
            or outcome.get("status") != "complete"
            or outcome.get("failure_type") is not None
            or outcome.get("failure_message") is not None
        ):
            raise ValueError("quality prompt worker outcome is not complete")
        raw_prompt = outcome.get("raw_prompt")
        raw_output = outcome.get("raw_output")
        if (
            not isinstance(raw_prompt, str)
            or not isinstance(raw_output, str)
            or outcome.get("raw_prompt_sha256") != _sha256_text(raw_prompt)
            or outcome.get("raw_output_sha256") != _sha256_text(raw_output)
        ):
            raise ValueError("quality prompt worker outcome hash is invalid")
        expected_prompt = turn["prompt"]
        if prompt_id == "P6" and index == 1:
            expected_prompt = (
                f"User: {turns[0]['prompt'].strip()}\n"
                f"Assistant: {first_output}\n"
                f"User: {turn['prompt'].strip()}"
            )
            if outcome.get("history_source_sha256") != _sha256_text(first_output):
                raise ValueError("quality prompt worker P6 history hash is invalid")
        if raw_prompt != expected_prompt:
            raise ValueError("quality prompt worker executed a different prompt")
        projected.append(
            {
                "turn_id": outcome["turn_id"],
                "output": raw_output,
                "output_sha256": outcome["raw_output_sha256"],
            }
        )
        if index == 0:
            first_output = raw_output
    return projected


def _validate_capture(
    summary_path: Path,
    *,
    contract: Mapping[str, Any],
    controls: Mapping[str, Mapping[str, Any]],
    rubric_sha256: str,
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    path = Path(summary_path).resolve()
    summary, summary_raw = _read_task5_object(
        path, label="adaptive quality capture summary"
    )
    receipts = summary.get("prompt_receipts")
    if (
        set(summary) != _SUMMARY_FIELDS
        or summary.get("schema") != CAPTURE_SCHEMA
        or summary.get("status") != "passed"
        or summary.get("completed_prompt_ids") != list(PROMPT_IDS)
        or summary.get("prompt_receipt_count") != 6
        or not isinstance(receipts, list)
        or len(receipts) != 6
        or [
            receipt.get("prompt_id")
            for receipt in receipts
            if isinstance(receipt, Mapping)
        ]
        != list(PROMPT_IDS)
    ):
        raise ValueError("quality capture must contain exactly P1 through P6")
    if summary.get("capture_summary_path") != str(path):
        raise ValueError("adaptive quality capture summary path mismatch")
    for field in (
        "campaign_identity_sha256",
        "runtime_summary_sha256",
        "matrix_sha256",
        "artifact_manifest_sha256",
        "prompt_set_sha256",
        "rubric_sha256",
        "runtime_property_sha256",
        "capture_summary_sha256",
    ):
        _require_sha256(summary.get(field), field)
    if (
        summary["prompt_set_sha256"] != FROZEN_PROMPT_SET_SHA256
        or summary["prompt_set_sha256"] != contract["prompt_set_sha256"]
        or summary["rubric_sha256"] != rubric_sha256
    ):
        raise ValueError("quality capture prompt-set or rubric identity mismatch")
    unsigned_summary = {
        key: item for key, item in summary.items() if key != "capture_summary_sha256"
    }
    if summary["capture_summary_sha256"] != _sha256_bytes(
        _task5_canonical_json(unsigned_summary)
    ):
        raise ValueError("adaptive quality capture summary hash is invalid")
    previous_path = summary["previous_capture_summary_path"]
    previous_hash = summary["previous_capture_summary_sha256"]
    if (previous_path is None) != (previous_hash is None):
        raise ValueError("adaptive quality capture summary history is invalid")
    if previous_path is not None:
        previous = Path(previous_path)
        _require_sha256(previous_hash, "previous_capture_summary_sha256")
        if (
            not previous.is_absolute()
            or not previous.is_file()
            or previous.resolve().parent != path.parent
            or _sha256_bytes(previous.read_bytes()) != previous_hash
        ):
            raise ValueError("adaptive quality previous capture summary mismatch")

    identity: dict[str, Any] | None = None
    projected: list[dict[str, Any]] = []
    process_identities: set[str] = set()
    root = path.parent
    for receipt, prompt_id in zip(receipts, PROMPT_IDS, strict=True):
        if (
            not isinstance(receipt, dict)
            or set(receipt) != _RECEIPT_FIELDS
            or receipt.get("prompt_id") != prompt_id
            or receipt.get("status") != "passed"
            or type(receipt.get("cleanup_process_count")) is not int
            or receipt["cleanup_process_count"] != 0
            or not isinstance(receipt.get("process_identity"), str)
            or not receipt["process_identity"]
            or receipt["process_identity"] in process_identities
        ):
            raise ValueError("quality capture must contain exactly P1 through P6")
        process_identities.add(receipt["process_identity"])
        prompt_root = Path(str(receipt["prompt_root"]))
        if (
            not prompt_root.is_absolute()
            or prompt_root.resolve().parent != root
            or prompt_root.resolve().name
            not in {prompt_id, f"{prompt_id}-recovery-001"}
            or prompt_root.is_symlink()
            or not prompt_root.is_dir()
        ):
            raise ValueError("quality prompt root is invalid")
        prompt_root = prompt_root.resolve()
        expected_children = {
            "worker-spec.json",
            "worker-result.json",
            "worker.log",
            "guard-evidence.json",
        }
        if {child.name for child in prompt_root.iterdir()} != expected_children:
            raise ValueError("quality prompt evidence is incomplete or unexpected")

        spec_path = _require_exact_file(
            receipt["worker_spec_path"],
            expected=prompt_root / "worker-spec.json",
            field="worker_spec_path",
        )
        result_path = _require_exact_file(
            receipt["worker_result_path"],
            expected=prompt_root / "worker-result.json",
            field="worker_result_path",
        )
        log_path = _require_exact_file(
            receipt["worker_log_path"],
            expected=prompt_root / "worker.log",
            field="worker_log_path",
        )
        guard_path = _require_exact_file(
            receipt["guard_evidence_path"],
            expected=prompt_root / "guard-evidence.json",
            field="guard_evidence_path",
        )
        for field, evidence_path in (
            ("worker_spec_sha256", spec_path),
            ("worker_result_sha256", result_path),
            ("worker_log_sha256", log_path),
            ("guard_evidence_sha256", guard_path),
        ):
            _require_sha256(receipt[field], field)
            if _sha256_bytes(evidence_path.read_bytes()) != receipt[field]:
                raise ValueError(f"quality prompt {field} mismatch")

        spec, spec_raw = _read_task5_object(
            spec_path, label="quality prompt worker spec"
        )
        if (
            spec.get("schema") != PROMPT_SPEC_SCHEMA
            or spec.get("prompt_id") != prompt_id
            or spec.get("turns") != _expected_spec_turns(prompt_id, contract)
        ):
            raise ValueError("quality prompt worker spec identity is invalid")
        _validate_prompt_worker_spec(spec)
        worker_spec_sha256 = _sha256_bytes(spec_raw)
        result, _result_raw = _read_task5_object(
            result_path, label="quality prompt worker result"
        )
        turns = _validate_task5_result(
            result,
            spec=spec,
            worker_spec_sha256=worker_spec_sha256,
        )
        guard, _guard_raw = _read_guard_object(guard_path)
        if (
            guard.get("run_id") != receipt["process_identity"]
            or guard.get("valid") is not True
            or guard.get("timed_out") is not False
            or guard.get("low_memory_stop") is not False
            or guard.get("cleanup_process_count") != 0
            or guard.get("exit_code") != 0
            or guard.get("log_sha256") != receipt["worker_log_sha256"]
        ):
            raise ValueError("quality prompt guard evidence is not passing")

        controller = spec["private_controller"]
        bindings = spec["bindings"]
        current_identity = {
            "test_id": controller["test_id"],
            "context_tokens": controller["context_tokens"],
            "capture_summary_path": str(path),
            "capture_summary_file_sha256": _sha256_bytes(summary_raw),
            "capture_summary_sha256": summary["capture_summary_sha256"],
            "campaign_identity_sha256": summary["campaign_identity_sha256"],
            "runtime_summary_sha256": summary["runtime_summary_sha256"],
            "matrix_sha256": summary["matrix_sha256"],
            "artifact_manifest_sha256": summary["artifact_manifest_sha256"],
            "prompt_set_sha256": summary["prompt_set_sha256"],
            "rubric_sha256": summary["rubric_sha256"],
            "runtime_property_sha256": summary["runtime_property_sha256"],
            "model_path": spec["model_path"],
            "device": spec["device"],
            "properties": spec["properties"],
            "build_identity": bindings["build_identity"],
            "runtime_property": bindings["runtime_property"],
        }
        if identity is None:
            identity = current_identity
        elif current_identity != identity:
            raise ValueError("quality capture prompt identities do not match")

        prompt_sha256 = FROZEN_PROMPT_SHA256S[prompt_id]
        request_sha256 = _sha256_bytes(
            _canonical_json(
                {"prompt_id": prompt_id, "prompt_sha256": prompt_sha256}
            )
        )
        response_sha256 = _sha256_bytes(
            _canonical_json({"prompt_id": prompt_id, "turn_outputs": turns})
        )
        output = turns[-1]["output"]
        projected.append(
            {
                "prompt_id": prompt_id,
                "prompt_sha256": prompt_sha256,
                "request_sha256": request_sha256,
                "response_sha256": response_sha256,
                "output": output,
                "output_sha256": turns[-1]["output_sha256"],
                "turn_outputs": turns,
                "deterministic_gate": deterministic_gate(
                    prompt_id, output, turns, controls[prompt_id]
                ),
            }
        )
    if identity is None:
        raise ValueError("quality capture must contain exactly P1 through P6")
    return identity, projected


def _new_blind_label(used: set[str]) -> str:
    alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
    while True:
        candidate = "response-" + "".join(secrets.choice(alphabet) for _ in range(12))
        if candidate not in used:
            used.add(candidate)
            return _validate_blind_label(candidate)


def build_adaptive_blind_bundle(
    capture_summaries: Sequence[Path],
    *,
    prompt_set_path: Path,
    rubric_path: Path,
) -> tuple[dict[str, Any], dict[str, Any]]:
    if isinstance(capture_summaries, (str, bytes)) or not isinstance(
        capture_summaries, Sequence
    ):
        raise TypeError("capture_summaries must be a sequence of paths")
    paths = [Path(path).resolve() for path in capture_summaries]
    if not paths or len(paths) != len(set(paths)):
        raise ValueError("capture summaries must be non-empty and unique")
    rubric = load_rubric(Path(rubric_path))
    controls = _prompt_controls(Path(prompt_set_path))
    rendered_root = Path(prompt_set_path).resolve().parent / "rendered"
    contract = load_prompt_contract(Path(prompt_set_path), rendered_root)
    validated = [
        _validate_capture(
            path,
            contract=contract,
            controls=controls,
            rubric_sha256=rubric["rubric_sha256"],
        )
        for path in paths
    ]
    configuration_keys = [
        (identity["test_id"], identity["context_tokens"])
        for identity, _responses in validated
    ]
    if len(configuration_keys) != len(set(configuration_keys)):
        raise ValueError("duplicate quality capture configuration")

    used_labels: set[str] = set()
    mapping: dict[str, dict[str, Any]] = {}
    responses: list[dict[str, Any]] = []
    for identity, projected in validated:
        label = _new_blind_label(used_labels)
        mapping[label] = identity
        responses.extend({"blind_label": label, **row} for row in projected)

    context_groups: dict[int, list[str]] = {}
    for label, identity in mapping.items():
        context_groups.setdefault(identity["context_tokens"], []).append(label)
    pairwise_pairs: list[dict[str, Any]] = []
    for context_tokens in sorted(context_groups):
        labels = sorted(context_groups[context_tokens])
        for left, right in combinations(labels, 2):
            blind_labels = [left, right]
            pairwise_pairs.append(
                {
                    "pair_id": _sha256_bytes(
                        _canonical_json({"blind_labels": blind_labels})
                    ),
                    "blind_labels": blind_labels,
                }
            )

    public_unsigned = {
        "schema_version": 1,
        "artifact_type": "openvino-adaptive-quality-blind-scoring-input",
        "prompt_set_id": contract["prompt_set_id"],
        "prompt_set_sha256": contract["prompt_set_sha256"],
        "rubric_id": rubric["rubric_id"],
        "rubric_sha256": rubric["rubric_sha256"],
        "dimension_weights": rubric["weights"],
        "dimension_critical_caps": rubric["caps"],
        "rubric_anchors": rubric["anchors"],
        "rubric_procedure": rubric["procedure"],
        "responses": responses,
        "pairwise_pairs": pairwise_pairs,
    }
    public = {
        **public_unsigned,
        "scoring_input_sha256": _sha256_bytes(_canonical_json(public_unsigned)),
    }
    private_unsigned = {
        "schema_version": 1,
        "artifact_type": "openvino-adaptive-quality-private-blind-map",
        "scoring_input_sha256": public["scoring_input_sha256"],
        "mapping": mapping,
    }
    private = {
        **private_unsigned,
        "private_map_sha256": _sha256_bytes(_canonical_json(private_unsigned)),
    }
    return public, private


def _finite_score(value: Any, field: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{field} must be numeric")
    score = float(value)
    if not math.isfinite(score) or not 0 <= score <= 10:
        raise ValueError(f"{field} must be within 0-10")
    return score


def _validate_scoring_input(
    scoring_input: Mapping[str, Any],
    *,
    rubric: Mapping[str, Any],
    controls: Mapping[str, Mapping[str, Any]],
) -> dict[tuple[str, str], dict[str, Any]]:
    reject_nulls(scoring_input)
    expected_fields = {
        "schema_version",
        "artifact_type",
        "prompt_set_id",
        "prompt_set_sha256",
        "rubric_id",
        "rubric_sha256",
        "dimension_weights",
        "dimension_critical_caps",
        "rubric_anchors",
        "rubric_procedure",
        "responses",
        "pairwise_pairs",
        "scoring_input_sha256",
    }
    if not isinstance(scoring_input, Mapping) or set(scoring_input) != expected_fields:
        raise ValueError("scoring input has missing or unexpected fields")
    if (
        scoring_input.get("schema_version") != 1
        or scoring_input.get("artifact_type")
        != "openvino-adaptive-quality-blind-scoring-input"
        or scoring_input.get("prompt_set_id") != "GTQ-PROMPTS-v1"
        or scoring_input.get("prompt_set_sha256") != FROZEN_PROMPT_SET_SHA256
        or scoring_input.get("rubric_id") != rubric["rubric_id"]
        or scoring_input.get("rubric_sha256") != rubric["rubric_sha256"]
        or scoring_input.get("dimension_weights") != rubric["weights"]
        or scoring_input.get("dimension_critical_caps") != rubric["caps"]
        or scoring_input.get("rubric_anchors") != rubric["anchors"]
        or scoring_input.get("rubric_procedure") != rubric["procedure"]
    ):
        raise ValueError("scoring input does not match the controlling rubric")
    _require_sha256(scoring_input.get("scoring_input_sha256"), "scoring_input_sha256")
    unsigned = {
        key: item
        for key, item in scoring_input.items()
        if key != "scoring_input_sha256"
    }
    if scoring_input["scoring_input_sha256"] != _sha256_bytes(
        _canonical_json(unsigned)
    ):
        raise ValueError("scoring input hash mismatch")
    responses = scoring_input.get("responses")
    if not isinstance(responses, list) or not responses:
        raise ValueError("scoring input must contain responses")
    indexed: dict[tuple[str, str], dict[str, Any]] = {}
    for row in responses:
        if not isinstance(row, dict) or set(row) != _SCORING_RESPONSE_FIELDS:
            raise ValueError("scoring response has missing or unexpected fields")
        label = _validate_blind_label(row.get("blind_label"))
        prompt_id = row.get("prompt_id")
        key = (label, prompt_id)
        if prompt_id not in PROMPT_IDS or key in indexed:
            raise ValueError("duplicate or invalid blind scoring response")
        for field in (
            "prompt_sha256",
            "request_sha256",
            "response_sha256",
            "output_sha256",
        ):
            _require_sha256(row.get(field), field)
        if row["prompt_sha256"] != FROZEN_PROMPT_SHA256S[prompt_id]:
            raise ValueError("scoring response prompt hash mismatch")
        expected_request = _sha256_bytes(
            _canonical_json(
                {"prompt_id": prompt_id, "prompt_sha256": row["prompt_sha256"]}
            )
        )
        if row["request_sha256"] != expected_request:
            raise ValueError("scoring response request hash mismatch")
        if (
            not isinstance(row.get("output"), str)
            or row["output_sha256"] != _sha256_text(row["output"])
        ):
            raise ValueError("scoring response output hash mismatch")
        turns = row.get("turn_outputs")
        expected_turn_ids = (
            ("P6-turn-1", "P6-turn-2") if prompt_id == "P6" else (prompt_id,)
        )
        if not isinstance(turns, list) or len(turns) != len(expected_turn_ids):
            raise ValueError("scoring response turn outputs are invalid")
        for turn, turn_id in zip(turns, expected_turn_ids, strict=True):
            if (
                not isinstance(turn, dict)
                or set(turn) != {"turn_id", "output", "output_sha256"}
                or turn.get("turn_id") != turn_id
                or not isinstance(turn.get("output"), str)
                or turn.get("output_sha256") != _sha256_text(turn["output"])
            ):
                raise ValueError("scoring response turn output hash mismatch")
        if turns[-1]["output"] != row["output"]:
            raise ValueError("scoring final turn output does not match response")
        expected_response = _sha256_bytes(
            _canonical_json({"prompt_id": prompt_id, "turn_outputs": turns})
        )
        if row["response_sha256"] != expected_response:
            raise ValueError("scoring complete response hash mismatch")
        expected_gate = deterministic_gate(
            prompt_id, row["output"], turns, controls[prompt_id]
        )
        if row.get("deterministic_gate") != expected_gate:
            raise ValueError("deterministic gate mismatch")
        indexed[key] = dict(row)
    labels = {label for label, _prompt_id in indexed}
    for label in labels:
        if {prompt_id for row_label, prompt_id in indexed if row_label == label} != set(
            PROMPT_IDS
        ):
            raise ValueError("scoring input requires exactly P1 through P6")
    pairs = scoring_input.get("pairwise_pairs")
    if not isinstance(pairs, list):
        raise ValueError("scoring input pairwise schedule is invalid")
    seen_pair_ids: set[str] = set()
    seen_label_pairs: set[tuple[str, str]] = set()
    for pair in pairs:
        if not isinstance(pair, dict) or set(pair) != _PAIR_FIELDS:
            raise ValueError("scoring input pairwise schedule is invalid")
        pair_id = pair.get("pair_id")
        blind_labels = pair.get("blind_labels")
        _require_sha256(pair_id, "pair_id")
        if (
            not isinstance(blind_labels, list)
            or len(blind_labels) != 2
            or any(label not in labels for label in blind_labels)
            or blind_labels != sorted(set(blind_labels))
            or pair_id
            != _sha256_bytes(_canonical_json({"blind_labels": blind_labels}))
            or pair_id in seen_pair_ids
            or tuple(blind_labels) in seen_label_pairs
        ):
            raise ValueError("scoring input pairwise schedule is invalid")
        seen_pair_ids.add(pair_id)
        seen_label_pairs.add(tuple(blind_labels))
    return indexed


def _normalise_score_sheet(
    sheet: Mapping[str, Any],
    *,
    expected: Mapping[tuple[str, str], Mapping[str, Any]],
    scoring_input: Mapping[str, Any],
    rubric: Mapping[str, Any],
) -> tuple[str, dict[tuple[str, str], dict[str, Any]]]:
    reject_nulls(sheet)
    if not isinstance(sheet, Mapping) or set(sheet) != _SCORE_SHEET_FIELDS:
        raise ValueError("score sheet has missing or unexpected fields")
    judge_id = sheet.get("judge_id")
    if (
        sheet.get("schema_version") != 1
        or sheet.get("artifact_type") != "openvino-adaptive-quality-blind-scores"
        or not isinstance(judge_id, str)
        or not judge_id.strip()
        or sheet.get("prompt_set_id") != scoring_input["prompt_set_id"]
        or sheet.get("rubric_id") != rubric["rubric_id"]
        or sheet.get("scoring_input_sha256")
        != scoring_input["scoring_input_sha256"]
        or not isinstance(sheet.get("adjudications"), list)
    ):
        raise ValueError("score sheet identity is invalid")
    _require_sha256(sheet.get("score_sheet_sha256"), "score_sheet_sha256")
    unsigned = {
        key: item for key, item in sheet.items() if key != "score_sheet_sha256"
    }
    if sheet["score_sheet_sha256"] != _sha256_bytes(_canonical_json(unsigned)):
        raise ValueError("score sheet hash mismatch")
    indexed: dict[tuple[str, str], dict[str, Any]] = {}
    for row in sheet["adjudications"]:
        if not isinstance(row, dict) or set(row) != _ADJUDICATION_FIELDS:
            raise ValueError("score adjudication has missing or unexpected fields")
        key = (row.get("blind_label"), row.get("prompt_id"))
        if key in indexed or key not in expected:
            raise ValueError("score sheet contains a duplicate or unknown adjudication")
        if row.get("response_sha256") != expected[key]["response_sha256"]:
            raise ValueError("score sheet response hash mismatch")
        dimensions = row.get("dimensions")
        if not isinstance(dimensions, dict) or set(dimensions) != set(
            rubric["weights"]
        ):
            raise ValueError("every controlling dimension must be scored")
        normalized_dimensions = {
            name: _finite_score(dimensions[name], f"dimension {name}")
            for name in rubric["weights"]
        }
        caps = row.get("manual_critical_caps")
        reasons = row.get("manual_cap_reasons")
        if (
            not isinstance(caps, list)
            or any(_finite_score(cap, "manual critical cap") not in {0.0, 2.0, 4.0} for cap in caps)
            or not isinstance(reasons, list)
            or len(caps) != len(reasons)
            or any(not isinstance(reason, str) or not reason.strip() for reason in reasons)
        ):
            raise ValueError("manual critical caps require matching reasons")
        unsupported = row.get("unsupported_statements_count")
        if isinstance(unsupported, bool) or not isinstance(unsupported, int) or unsupported < 0:
            raise ValueError("unsupported statements count is invalid")
        if row.get("reviewer") != judge_id:
            raise ValueError("score sheet reviewer does not match judge identity")
        if not isinstance(row.get("notes"), str) or not row["notes"].strip():
            raise ValueError("score sheet notes are required")
        indexed[key] = {
            **row,
            "dimensions": normalized_dimensions,
            "manual_critical_caps": [float(cap) for cap in caps],
        }
    if set(indexed) != set(expected):
        raise ValueError("score sheet must contain exactly one adjudication per response")

    identical: dict[tuple[str, str], tuple[Any, ...]] = {}
    for key, row in indexed.items():
        content_key = (key[1], expected[key]["response_sha256"])
        decision = (
            tuple(row["dimensions"][name] for name in rubric["weights"]),
            tuple(sorted(row["manual_critical_caps"])),
            row["unsupported_statements_count"],
        )
        prior = identical.setdefault(content_key, decision)
        if prior != decision:
            raise ValueError("label-dependent adjudication for identical content")
    return judge_id, indexed


def _normalise_pairwise_reviews(
    value: Mapping[str, Any],
    *,
    scoring_input: Mapping[str, Any],
) -> list[dict[str, Any]]:
    reject_nulls(value)
    if not isinstance(value, Mapping) or set(value) != {
        "schema",
        "scoring_input_sha256",
        "reviews",
        "pairwise_reviews_sha256",
    }:
        raise ValueError("pairwise reviews are invalid")
    if (
        value.get("schema") != "official-openvino-adaptive-pairwise-reviews-v1"
        or value.get("scoring_input_sha256")
        != scoring_input["scoring_input_sha256"]
        or not isinstance(value.get("reviews"), list)
    ):
        raise ValueError("pairwise reviews are invalid")
    _require_sha256(
        value.get("pairwise_reviews_sha256"), "pairwise_reviews_sha256"
    )
    unsigned_wrapper = {
        key: item for key, item in value.items() if key != "pairwise_reviews_sha256"
    }
    if value["pairwise_reviews_sha256"] != _sha256_bytes(
        _canonical_json(unsigned_wrapper)
    ):
        raise ValueError("pairwise reviews hash mismatch")

    scheduled = {
        pair["pair_id"]: tuple(pair["blind_labels"])
        for pair in scoring_input["pairwise_pairs"]
    }
    indexed: dict[tuple[str, str], dict[str, Any]] = {}
    for review in value["reviews"]:
        if not isinstance(review, dict) or set(review) != _PAIRWISE_REVIEW_FIELDS:
            raise ValueError("pairwise review has missing or unexpected fields")
        pair_id = review.get("pair_id")
        order = review.get("order")
        key = (pair_id, order)
        if pair_id not in scheduled or order not in {"AB", "BA"} or key in indexed:
            raise ValueError("pairwise reviews require exactly one AB and BA review")
        first, second = scheduled[pair_id]
        expected_left, expected_right = (
            (first, second) if order == "AB" else (second, first)
        )
        if (
            review.get("left_blind_label") != expected_left
            or review.get("right_blind_label") != expected_right
            or review.get("winner") not in {"A", "B", "tie"}
            or not isinstance(review.get("reviewer"), str)
            or not review["reviewer"].strip()
            or not isinstance(review.get("reason"), str)
            or not review["reason"].strip()
        ):
            raise ValueError("pairwise review identity or decision is invalid")
        _require_sha256(review.get("review_sha256"), "review_sha256")
        unsigned_review = {
            field: item for field, item in review.items() if field != "review_sha256"
        }
        if review["review_sha256"] != _sha256_bytes(
            _canonical_json(unsigned_review)
        ):
            raise ValueError("pairwise review hash mismatch")
        indexed[key] = dict(review)
    expected_keys = {
        (pair_id, order)
        for pair_id in scheduled
        for order in ("AB", "BA")
    }
    if set(indexed) != expected_keys:
        raise ValueError("pairwise reviews require exactly one AB and BA review")

    normalized: list[dict[str, Any]] = []
    for pair in scoring_input["pairwise_pairs"]:
        pair_id = pair["pair_id"]
        winners: list[str] = []
        for order in ("AB", "BA"):
            review = indexed[(pair_id, order)]
            if review["winner"] == "tie":
                winners.append("tie")
            elif review["winner"] == "A":
                winners.append(review["left_blind_label"])
            else:
                winners.append(review["right_blind_label"])
        normalized.append(
            {
                "pair_id": pair_id,
                "orders": ["AB", "BA"],
                "normalized_winners": winners,
            }
        )
    return normalized


def _normalise_manual_adjudications(
    value: Mapping[str, Any],
    *,
    scoring_input: Mapping[str, Any],
    responses: Mapping[tuple[str, str], Mapping[str, Any]],
    rubric: Mapping[str, Any],
    score_sheet_sha256s: list[str],
    pairwise_reviews: Mapping[str, Any],
    required_prompt_flags: Mapping[tuple[str, str], list[str]],
    required_pair_ids: set[str],
) -> tuple[dict[tuple[str, str], float], dict[str, str]]:
    reject_nulls(value)
    if not isinstance(value, Mapping) or set(value) != {
        "schema",
        "scoring_input_sha256",
        "records",
        "manual_adjudications_sha256",
    }:
        raise ValueError("manual adjudication artifact is invalid")
    if (
        value.get("schema") != "official-openvino-manual-adjudications-v1"
        or value.get("scoring_input_sha256")
        != scoring_input["scoring_input_sha256"]
        or not isinstance(value.get("records"), list)
    ):
        raise ValueError("manual adjudication artifact identity is invalid")
    _require_sha256(
        value.get("manual_adjudications_sha256"),
        "manual_adjudications_sha256",
    )
    unsigned_wrapper = {
        key: item
        for key, item in value.items()
        if key != "manual_adjudications_sha256"
    }
    if value["manual_adjudications_sha256"] != _sha256_bytes(
        _canonical_json(unsigned_wrapper)
    ):
        raise ValueError("manual adjudication artifact hash mismatch")

    scheduled_pairs = {
        pair["pair_id"]: tuple(pair["blind_labels"])
        for pair in scoring_input["pairwise_pairs"]
    }
    review_index = {
        (review["pair_id"], review["order"]): review
        for review in pairwise_reviews["reviews"]
    }
    prompt_scores: dict[tuple[str, str], float] = {}
    pair_resolutions: dict[str, str] = {}
    content_decisions: dict[tuple[str, str], float] = {}
    for record in value["records"]:
        if not isinstance(record, dict):
            raise ValueError("manual adjudication record is invalid")
        record_type = record.get("record_type")
        expected_fields = (
            _MANUAL_PROMPT_FIELDS
            if record_type == "prompt"
            else _MANUAL_PAIR_FIELDS
            if record_type == "pair"
            else set()
        )
        if not expected_fields or set(record) != expected_fields:
            raise ValueError("manual adjudication record has unexpected fields")
        _require_sha256(record.get("record_sha256"), "record_sha256")
        unsigned_record = {
            key: item for key, item in record.items() if key != "record_sha256"
        }
        if record["record_sha256"] != _sha256_bytes(
            _canonical_json(unsigned_record)
        ):
            raise ValueError("manual adjudication record hash mismatch")
        if (
            not isinstance(record.get("final_reason"), str)
            or not record["final_reason"].strip()
            or not isinstance(record.get("adjudicator"), str)
            or not record["adjudicator"].strip()
        ):
            raise ValueError("manual adjudication requires a final reason")

        if record_type == "prompt":
            key = (record.get("blind_label"), record.get("prompt_id"))
            if key not in required_prompt_flags:
                raise ValueError("unexpected manual adjudication prompt record")
            if key in prompt_scores:
                raise ValueError("duplicate manual adjudication prompt record")
            raw = responses[key]
            expected_evidence = {
                "deterministic_gate_sha256": _sha256_bytes(
                    _canonical_json(raw["deterministic_gate"])
                ),
                "judge_score_sheet_sha256s": score_sheet_sha256s,
            }
            if (
                record.get("response_sha256") != raw["response_sha256"]
                or record.get("flags") != required_prompt_flags[key]
                or record.get("evidence") != expected_evidence
            ):
                raise ValueError("manual adjudication prompt evidence is stale")
            basis = record.get("rubric_basis")
            if not isinstance(basis, dict) or set(basis) != set(rubric["weights"]):
                raise ValueError("manual adjudication rubric basis is invalid")
            normalized_basis = {
                name: _finite_score(basis[name], f"manual rubric basis {name}")
                for name in rubric["weights"]
            }
            uncapped = sum(
                normalized_basis[name] * float(weight)
                for name, weight in rubric["weights"].items()
            )
            caps = [
                float(cap)
                for cap in raw["deterministic_gate"]["critical_caps"]
            ]
            expected_score = round(
                min([uncapped, *caps]) if caps else uncapped,
                4,
            )
            final_score = _finite_score(
                record.get("final_score"), "manual adjudication final score"
            )
            if final_score != expected_score:
                raise ValueError(
                    "manual adjudication final score is not rubric-grounded and capped"
                )
            prompt_scores[key] = final_score
            content_key = (key[1], raw["response_sha256"])
            prior = content_decisions.setdefault(content_key, final_score)
            if prior != final_score:
                raise ValueError("label-dependent manual adjudication for identical content")
        else:
            pair_id = record.get("pair_id")
            if pair_id not in required_pair_ids:
                raise ValueError("unexpected manual adjudication pair record")
            if pair_id in pair_resolutions:
                raise ValueError("duplicate manual adjudication pair record")
            expected_evidence = {
                "AB_review_sha256": review_index[(pair_id, "AB")][
                    "review_sha256"
                ],
                "BA_review_sha256": review_index[(pair_id, "BA")][
                    "review_sha256"
                ],
            }
            if (
                record.get("flags") != ["ranking-reversal"]
                or record.get("evidence") != expected_evidence
                or record.get("selected_winner") not in scheduled_pairs[pair_id]
            ):
                raise ValueError("manual adjudication pair evidence is stale")
            if (
                not isinstance(record.get("rubric_basis"), str)
                or not record["rubric_basis"].strip()
            ):
                raise ValueError("manual adjudication rubric basis is invalid")
            pair_resolutions[pair_id] = record["selected_winner"]

    if set(prompt_scores) != set(required_prompt_flags) or set(pair_resolutions) != set(
        required_pair_ids
    ):
        raise ValueError("manual adjudication is required for every flagged prompt or pair")
    return prompt_scores, pair_resolutions


def _normalise_private_map(
    value: Mapping[str, Any],
    *,
    labels: set[str],
    scoring_input_sha256: str,
) -> dict[str, Mapping[str, Any]]:
    reject_nulls(value)
    if not isinstance(value, Mapping) or set(value) != {
        "schema_version",
        "artifact_type",
        "scoring_input_sha256",
        "mapping",
        "private_map_sha256",
    }:
        raise ValueError("private blind map is invalid")
    if (
        value.get("schema_version") != 1
        or value.get("artifact_type")
        != "openvino-adaptive-quality-private-blind-map"
        or value.get("scoring_input_sha256") != scoring_input_sha256
        or not isinstance(value.get("mapping"), Mapping)
        or set(value["mapping"]) != labels
    ):
        raise ValueError("private blind map identity is invalid")
    _require_sha256(value.get("private_map_sha256"), "private_map_sha256")
    unsigned = {
        key: item for key, item in value.items() if key != "private_map_sha256"
    }
    if value["private_map_sha256"] != _sha256_bytes(_canonical_json(unsigned)):
        raise ValueError("private blind map hash mismatch")
    mapping: dict[str, Mapping[str, Any]] = {}
    test_ids: set[str] = set()
    for label, identity in value["mapping"].items():
        if (
            not isinstance(identity, Mapping)
            or set(identity) != _PRIVATE_IDENTITY_FIELDS
            or not isinstance(identity.get("test_id"), str)
            or not identity["test_id"].strip()
            or identity["test_id"] in test_ids
            or isinstance(identity.get("context_tokens"), bool)
            or not isinstance(identity.get("context_tokens"), int)
            or identity["context_tokens"] <= 0
            or not isinstance(identity.get("capture_summary_path"), str)
            or not Path(identity["capture_summary_path"]).is_absolute()
            or not isinstance(identity.get("model_path"), str)
            or not identity["model_path"].strip()
            or not isinstance(identity.get("device"), str)
            or not identity["device"].strip()
            or not isinstance(identity.get("properties"), Mapping)
            or not isinstance(identity.get("build_identity"), Mapping)
            or not isinstance(identity.get("runtime_property"), Mapping)
        ):
            raise ValueError("private blind map configuration identity is invalid")
        for field in (
            "capture_summary_file_sha256",
            "capture_summary_sha256",
            "campaign_identity_sha256",
            "runtime_summary_sha256",
            "matrix_sha256",
            "artifact_manifest_sha256",
            "prompt_set_sha256",
            "rubric_sha256",
            "runtime_property_sha256",
        ):
            _require_sha256(identity.get(field), field)
        test_ids.add(identity["test_id"])
        mapping[label] = identity
    return mapping


def adjudicate_adaptive_quality(
    *,
    scoring_input: Mapping[str, Any],
    judge_score_sheets: Sequence[Mapping[str, Any]],
    pairwise_reviews: Mapping[str, Any],
    manual_adjudications: Mapping[str, Any],
    blind_map_reader: Callable[[], Mapping[str, Any]],
    rubric_path: Path,
    prompt_set_path: Path,
) -> dict[str, Any]:
    rubric = load_rubric(Path(rubric_path))
    controls = _prompt_controls(Path(prompt_set_path))
    responses = _validate_scoring_input(
        scoring_input,
        rubric=rubric,
        controls=controls,
    )
    if isinstance(judge_score_sheets, (str, bytes)) or not isinstance(
        judge_score_sheets, Sequence
    ) or len(judge_score_sheets) != 2:
        raise ValueError("exactly two judge score sheets are required")
    normalized_sheets = [
        _normalise_score_sheet(
            sheet,
            expected=responses,
            scoring_input=scoring_input,
            rubric=rubric,
        )
        for sheet in judge_score_sheets
    ]
    judge_ids = [judge_id for judge_id, _rows in normalized_sheets]
    sheet_hashes = [sheet["score_sheet_sha256"] for sheet in judge_score_sheets]
    if len(set(judge_ids)) != 2 or len(set(sheet_hashes)) != 2:
        raise ValueError("two independent judge score sheets are required")

    normalized_pairwise = _normalise_pairwise_reviews(
        pairwise_reviews,
        scoring_input=scoring_input,
    )
    per_judge_scores: list[dict[tuple[str, str], float]] = []
    for _judge_id, adjudications in normalized_sheets:
        scores: dict[tuple[str, str], float] = {}
        for key, scored in adjudications.items():
            raw = responses[key]
            uncapped = sum(
                scored["dimensions"][name] * float(weight)
                for name, weight in rubric["weights"].items()
            )
            caps = [
                float(cap)
                for cap in raw["deterministic_gate"]["critical_caps"]
            ] + list(scored["manual_critical_caps"])
            scores[key] = round(min([uncapped, *caps]) if caps else uncapped, 4)
        per_judge_scores.append(scores)

    required_prompt_flags: dict[tuple[str, str], list[str]] = {}
    for key, raw in responses.items():
        flags: list[str] = []
        if raw["deterministic_gate"]["critical_caps"]:
            flags.append("critical-gate")
        if abs(per_judge_scores[0][key] - per_judge_scores[1][key]) > 1.0:
            flags.append("judge-disagreement-over-one")
        if flags:
            required_prompt_flags[key] = flags
    required_pair_ids = {
        pair["pair_id"]
        for pair in normalized_pairwise
        if pair["normalized_winners"][0] != pair["normalized_winners"][1]
    }
    manual_prompt_scores, pair_resolutions = _normalise_manual_adjudications(
        manual_adjudications,
        scoring_input=scoring_input,
        responses=responses,
        rubric=rubric,
        score_sheet_sha256s=sheet_hashes,
        pairwise_reviews=pairwise_reviews,
        required_prompt_flags=required_prompt_flags,
        required_pair_ids=required_pair_ids,
    )
    for pair in normalized_pairwise:
        if pair["pair_id"] in pair_resolutions:
            pair["manual_resolution"] = pair_resolutions[pair["pair_id"]]

    if not callable(blind_map_reader):
        raise TypeError("blind_map_reader must be callable")
    labels = {label for label, _prompt_id in responses}
    private_map = blind_map_reader()
    mapping = _normalise_private_map(
        private_map,
        labels=labels,
        scoring_input_sha256=scoring_input["scoring_input_sha256"],
    )
    private_context_groups: dict[int, list[str]] = {}
    for label, identity in mapping.items():
        private_context_groups.setdefault(identity["context_tokens"], []).append(label)
    expected_label_pairs = {
        tuple(pair)
        for group_labels in private_context_groups.values()
        for pair in combinations(sorted(group_labels), 2)
    }
    scheduled_label_pairs = {
        tuple(pair["blind_labels"]) for pair in scoring_input["pairwise_pairs"]
    }
    if scheduled_label_pairs != expected_label_pairs:
        raise ValueError("private blind map pairwise schedule does not match contexts")

    configurations: list[dict[str, Any]] = []
    for label in sorted(labels):
        prompt_scores = {
            prompt_id: manual_prompt_scores.get(
                (label, prompt_id),
                round(
                    statistics.mean(
                        sheet_scores[(label, prompt_id)]
                        for sheet_scores in per_judge_scores
                    ),
                    4,
                ),
            )
            for prompt_id in PROMPT_IDS
        }
        values = list(prompt_scores.values())
        identity = mapping[label]
        configurations.append(
            {
                "test_id": identity["test_id"],
                "context_tokens": identity.get("context_tokens"),
                "blind_label": label,
                "status": "complete",
                "prompt_scores": prompt_scores,
                "aggregates": {
                    "mean": round(float(statistics.mean(values)), 4),
                    "median": round(float(statistics.median(values)), 4),
                    "minimum": round(min(values), 4),
                    "maximum": round(max(values), 4),
                },
            }
        )

    result_unsigned = {
        "schema_version": 1,
        "artifact_type": "openvino-adaptive-quality-adjudication",
        "prompt_set_id": scoring_input["prompt_set_id"],
        "prompt_set_sha256": scoring_input["prompt_set_sha256"],
        "rubric_id": scoring_input["rubric_id"],
        "rubric_sha256": scoring_input["rubric_sha256"],
        "scoring_input_sha256": scoring_input["scoring_input_sha256"],
        "private_map_sha256": private_map["private_map_sha256"],
        "judge_score_sheet_sha256s": sheet_hashes,
        "pairwise_reviews_sha256": pairwise_reviews["pairwise_reviews_sha256"],
        "manual_adjudications_sha256": manual_adjudications[
            "manual_adjudications_sha256"
        ],
        "configuration_count": len(configurations),
        "status": "complete",
        "pairwise_reviews": normalized_pairwise,
        "configurations": configurations,
    }
    result = {
        **result_unsigned,
        "adjudication_sha256": _sha256_bytes(_canonical_json(result_unsigned)),
    }
    reject_nulls(result)
    return result


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build or adjudicate blind adaptive OpenVINO quality evidence"
    )
    commands = parser.add_subparsers(dest="command", required=True)

    build = commands.add_parser("build-bundle")
    build.add_argument("--capture-index", type=Path, required=True)
    build.add_argument("--prompt-set", type=Path, required=True)
    build.add_argument("--rubric", type=Path, required=True)
    build.add_argument("--public-output", type=Path, required=True)
    build.add_argument("--private-map-output", type=Path, required=True)

    adjudicate = commands.add_parser("adjudicate")
    adjudicate.add_argument("--scoring-input", type=Path, required=True)
    adjudicate.add_argument(
        "--judge-score-sheet", type=Path, action="append", required=True
    )
    adjudicate.add_argument("--pairwise-reviews", type=Path, required=True)
    adjudicate.add_argument("--manual-adjudications", type=Path, required=True)
    adjudicate.add_argument("--private-map", type=Path, required=True)
    adjudicate.add_argument("--prompt-set", type=Path, required=True)
    adjudicate.add_argument("--rubric", type=Path, required=True)
    adjudicate.add_argument("--output", type=Path, required=True)

    args = parser.parse_args(argv)
    if args.command == "adjudicate" and len(args.judge_score_sheet) != 2:
        parser.error("--judge-score-sheet must be supplied exactly twice")
    return args


def _read_capture_index(path: Path) -> list[Path]:
    source = Path(path).resolve()
    if not source.is_file() or source.is_symlink():
        raise ValueError("capture index is missing or aliased")
    value = read_json_strict(source)
    if not isinstance(value, dict) or set(value) != {
        "schema",
        "capture_summaries",
        "capture_index_sha256",
    }:
        raise ValueError("capture index has missing or unexpected fields")
    paths = value.get("capture_summaries")
    if (
        value.get("schema")
        != "official-openvino-adaptive-quality-capture-index-v1"
        or not isinstance(paths, list)
        or not paths
        or any(not isinstance(item, str) for item in paths)
    ):
        raise ValueError("capture index identity is invalid")
    _require_sha256(value.get("capture_index_sha256"), "capture_index_sha256")
    unsigned = {
        key: item for key, item in value.items() if key != "capture_index_sha256"
    }
    if value["capture_index_sha256"] != _sha256_bytes(_canonical_json(unsigned)):
        raise ValueError("capture index hash mismatch")
    resolved = [Path(item).resolve() for item in paths]
    if (
        any(not Path(item).is_absolute() for item in paths)
        or len(resolved) != len(set(resolved))
    ):
        raise ValueError("capture index paths must be absolute and unique")
    return resolved


def _require_fresh_outputs(paths: Sequence[Path]) -> list[Path]:
    resolved = [Path(path).resolve() for path in paths]
    if len(resolved) != len(set(resolved)):
        raise ValueError("output paths must be distinct")
    for path in resolved:
        if path.exists() or path.is_symlink():
            raise FileExistsError(f"refusing to overwrite evidence: {path}")
    return resolved


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    if args.command == "build-bundle":
        public_path, private_path = _require_fresh_outputs(
            [args.public_output, args.private_map_output]
        )
        capture_summaries = _read_capture_index(args.capture_index)
        public, private = build_adaptive_blind_bundle(
            capture_summaries,
            prompt_set_path=args.prompt_set,
            rubric_path=args.rubric,
        )
        atomic_write_json(public_path, public)
        atomic_write_json(private_path, private)
        metadata = {
            "private_map_output": str(private_path),
            "private_map_sha256": private["private_map_sha256"],
            "public_output": str(public_path),
            "scoring_input_sha256": public["scoring_input_sha256"],
        }
        print(_canonical_json(metadata).decode("utf-8"))
        return 0

    output_path = _require_fresh_outputs([args.output])[0]
    scoring_input = read_json_strict(Path(args.scoring_input).resolve())
    judge_score_sheets = [
        read_json_strict(Path(path).resolve()) for path in args.judge_score_sheet
    ]
    pairwise_reviews = read_json_strict(Path(args.pairwise_reviews).resolve())
    manual_adjudications = read_json_strict(
        Path(args.manual_adjudications).resolve()
    )
    private_map_path = Path(args.private_map).resolve()
    result = adjudicate_adaptive_quality(
        scoring_input=scoring_input,
        judge_score_sheets=judge_score_sheets,
        pairwise_reviews=pairwise_reviews,
        manual_adjudications=manual_adjudications,
        blind_map_reader=lambda: read_json_strict(private_map_path),
        rubric_path=args.rubric,
        prompt_set_path=args.prompt_set,
    )
    atomic_write_json(output_path, result)
    metadata = {
        "adjudication_sha256": result["adjudication_sha256"],
        "output": str(output_path),
    }
    print(_canonical_json(metadata).decode("utf-8"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
