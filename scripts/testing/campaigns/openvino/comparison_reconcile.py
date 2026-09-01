"""Independent, hash-bound reconciliation for adaptive comparison evidence.

This module deliberately reopens the concrete evidence named by a release
input.  It never searches evidence directories, launches a runtime, or edits
historical evidence.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import re
import statistics
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from scripts.testing.campaigns.openvino.adaptive_campaign import (
    AdaptiveCampaignConfig,
    CANDIDATE_ORDER,
    CONTEXTS,
    MAX_GUARDED_ATTEMPTS,
    RUNTIME_FLOOR_MIB,
    START_RESERVE_MIB,
    _directory_sha256,
    _validate_state_receipts,
    _validate_state,
)
from scripts.testing.campaigns.openvino.adaptive_metrics import (
    build_adaptive_runtime_sample,
    summarize_adaptive_runtime_samples,
)
from scripts.testing.campaigns.openvino.adaptive_quality import (
    _input_evidence_paths as quality_capture_evidence_paths,
    _validate_summary as validate_quality_capture_summary,
    _validate_summary_transition as validate_quality_capture_transition,
    quality_campaign_input_from_recovery,
)
from scripts.testing.campaigns.openvino.matrix import load_adaptive_comparison_matrix
from scripts.testing.campaigns.openvino.quality_campaign import (
    AcceptedQualityCampaign,
    _canonical_identity_bytes,
    _load_frozen_rubric,
    _validated_lexical_output_root,
    build_worker_environment,
    load_prompt_contract,
)
from scripts.testing.measure_official_openvino import (
    RECEIPT_SCHEMA,
    SEQUENCE_ROLES,
    SEQUENCE_SCHEMA,
    _role_spec,
    _runtime_record_matches_spec,
    _sequence_spec,
    _sha256_json as task_three_sha256_json,
    build_campaign_identity,
    validate_runtime_record_against_matrix_case,
    validate_worker_spec_against_matrix_case,
)
from scripts.testing.campaigns.openvino.metrics import summarize_samples
from scripts.testing.campaigns.openvino.runtime_process import measurement_sample
from scripts.testing.adjudicate_official_openvino_adaptive_quality import (
    adjudicate_adaptive_quality,
)


RELEASE_INPUT_SCHEMA = "official-openvino-comparison-release-input-v1"
CAPTURE_INDEX_SCHEMA = "official-openvino-quality-capture-index-v1"
_SHA256 = set("0123456789abcdef")
_PROMPTS = ("P1", "P2", "P3", "P4", "P5", "P6")


@dataclass(frozen=True, order=True)
class ComparisonKey:
    test_id: str
    context_tokens: int


@dataclass(frozen=True)
class ComparisonRuntimeOutcome:
    key: ComparisonKey
    samples: tuple[Mapping[str, Any], Mapping[str, Any], Mapping[str, Any]]
    timing: Mapping[str, Mapping[str, float | int | tuple[float, ...]]]
    memory: Mapping[str, Mapping[str, float | int | tuple[float, ...]]]
    utilisation: Mapping[str, Mapping[str, float | int | tuple[float, ...]]]
    activation: Mapping[str, Any]
    identity_hashes: Mapping[str, str]
    evidence_path: Path
    evidence_sha256: str


@dataclass(frozen=True)
class ComparisonQualityOutcome:
    key: ComparisonKey
    status: str
    prompt_scores: Mapping[str, float] | None
    aggregates: Mapping[str, float] | None
    evidence_path: Path
    evidence_sha256: str
    terminal_stage: str | None = None
    principal_reason: str | None = None


@dataclass(frozen=True)
class BoundaryOutcome:
    test_id: str
    highest_runtime_context: int | None
    highest_fully_comparable_context: int | None
    first_confirmed_blocked_context: int | None
    terminal_stage: str | None
    terminal_evidence_sha256: str | None


@dataclass(frozen=True)
class ComparisonRelease:
    runtime: Mapping[ComparisonKey, ComparisonRuntimeOutcome]
    quality: Mapping[ComparisonKey, ComparisonQualityOutcome]
    terminals: Mapping[ComparisonKey, Mapping[str, Any]]
    shared_cache_contexts: tuple[int, ...]
    shared_standard_contexts: tuple[int, ...]
    boundaries: Mapping[str, BoundaryOutcome]


@dataclass(frozen=True)
class _LoadedReleaseInput:
    base: Path
    matrix_path: Path
    matrix_sha256: str
    campaign_state_path: Path | None
    campaign_state_sha256: str | None
    campaign_state: Mapping[str, Any]
    steps: tuple[Mapping[str, Any], ...]


def _canonical_bytes(value: Any, *, newline: bool = True) -> bytes:
    text = json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
        allow_nan=False,
    )
    return (text + ("\n" if newline else "")).encode("utf-8")


def _canonical_sha256(value: Any) -> str:
    return hashlib.sha256(_canonical_bytes(value, newline=False)).hexdigest()


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _strict_object(path: Path, label: str) -> dict[str, Any]:
    source = Path(path).resolve()
    if not source.is_file():
        raise ValueError(f"{label} is missing: {source}")

    def no_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"{label} has a duplicate JSON key: {key}")
            result[key] = value
        return result

    def reject_constant(value: str) -> None:
        raise ValueError(f"{label} has a non-finite JSON number: {value}")

    try:
        value = json.loads(
            source.read_text(encoding="utf-8-sig"),
            object_pairs_hook=no_duplicates,
            parse_constant=reject_constant,
        )
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
        raise ValueError(f"{label} is not strict JSON: {source}") from error
    if not isinstance(value, dict):
        raise ValueError(f"{label} must be a JSON object")
    return value


def _require_sha256(value: object, field: str) -> str:
    if (
        not isinstance(value, str)
        or len(value) != 64
        or any(character not in _SHA256 for character in value)
    ):
        raise ValueError(f"{field} must be a lowercase SHA-256 hash")
    return value


def _self_hash_matches(value: Mapping[str, Any], field: str) -> bool:
    supplied = value.get(field)
    if not isinstance(supplied, str):
        return False
    unsigned = {key: item for key, item in value.items() if key != field}
    return supplied in {
        hashlib.sha256(_canonical_bytes(unsigned, newline=False)).hexdigest(),
        hashlib.sha256(_canonical_bytes(unsigned, newline=True)).hexdigest(),
    }


def _resolve_reference(
    value: object,
    *,
    base: Path,
    label: str,
) -> tuple[Path, str]:
    if not isinstance(value, Mapping) or set(value) not in (
        {"path", "sha256"},
        {"path", "sha256", "authority"},
    ):
        raise ValueError(f"{label} must contain exactly path and sha256")
    raw_path = value.get("path")
    if not isinstance(raw_path, str) or not raw_path:
        raise ValueError(f"{label} path is invalid")
    if "\\" in raw_path:
        raise ValueError(f"{label} path must be normalized and relative")
    lexical = Path(raw_path)
    if lexical.is_absolute():
        raise ValueError(f"{label} path must be relative")
    if (
        lexical.as_posix() != raw_path
        or any(part in {"", ".", ".."} for part in lexical.parts)
    ):
        raise ValueError(f"{label} path must be normalized and relative")
    authority = value.get("authority")
    if authority is None:
        base_root = Path(base).resolve()
    else:
        if (
            not isinstance(authority, Mapping)
            or set(authority) != {"kind", "root"}
            or not isinstance(authority.get("kind"), str)
            or not authority["kind"].strip()
            or not isinstance(authority.get("root"), str)
            or not authority["root"].strip()
        ):
            raise ValueError(f"{label} external authority is invalid")
        authority_root = Path(authority["root"])
        if not authority_root.is_absolute():
            raise ValueError(f"{label} external authority root must be absolute")
        base_root = authority_root.resolve()
        if not base_root.is_dir():
            raise ValueError(f"{label} external authority root is missing")
    resolved = (base_root / lexical).resolve()
    if base_root != resolved and base_root not in resolved.parents:
        raise ValueError(f"{label} path escapes its release authority root")
    expected = _require_sha256(value.get("sha256"), f"{label} sha256")
    if not resolved.is_file():
        raise ValueError(f"{label} path is missing: {resolved}")
    if _sha256_file(resolved) != expected:
        raise ValueError(f"{label} hash mismatch")
    return resolved, expected


def _reference_for_output(
    path: Path,
    output_path: Path,
    *,
    external_authority: str = "external-evidence-root",
) -> dict[str, Any]:
    source = Path(path).resolve()
    base = Path(output_path).resolve().parent
    if not source.is_file():
        raise ValueError(f"release-input evidence is missing: {source}")
    try:
        relative = source.relative_to(base)
    except ValueError:
        root = source.parent
        return {
            "path": source.name,
            "sha256": _sha256_file(source),
            "authority": {
                "kind": external_authority,
                "root": str(root),
            },
        }
    return {"path": relative.as_posix(), "sha256": _sha256_file(source)}


def file_reference(path: Path, *, output_path: Path) -> dict[str, Any]:
    """Return one exact relative, hash-bound evidence reference."""

    return _reference_for_output(Path(path), Path(output_path))


def write_canonical_self_hashed_json(path: Path, payload: Mapping[str, Any]) -> dict[str, Any]:
    """Publish a deterministic JSON object with a schema-specific self hash."""

    value = dict(payload)
    schema = value.get("schema")
    if schema == RELEASE_INPUT_SCHEMA:
        field = "release_input_sha256"
    elif schema == CAPTURE_INDEX_SCHEMA:
        field = "quality_capture_index_sha256"
    else:
        field = "sha256"
    if field in value:
        raise ValueError(f"self-hashed payload already has {field}")
    value[field] = _canonical_sha256(value)
    destination = Path(path).resolve()
    destination.parent.mkdir(parents=True, exist_ok=True)
    data = _canonical_bytes(value)
    if destination.exists():
        if destination.read_bytes() != data:
            raise ValueError("existing self-hashed JSON is non-identical")
        return value
    destination.write_bytes(data)
    return value


def _state_child(root: Path, value: object, label: str) -> Path:
    if not isinstance(value, str) or not value:
        raise ValueError(f"{label} path is invalid")
    lexical = Path(value)
    if lexical.is_absolute() or ".." in lexical.parts:
        raise ValueError(f"{label} path must be a normalized relative path")
    resolved = (root / lexical).resolve()
    if root != resolved and root not in resolved.parents:
        raise ValueError(f"{label} path escapes campaign state")
    return resolved


def _validate_controller_attempts(
    step: Mapping[str, Any],
    *,
    campaign_root: Path,
    test_id: str,
    context: int,
) -> None:
    attempts = step.get("attempts")
    if not isinstance(attempts, list) or len(attempts) != step.get("attempt_count"):
        raise ValueError("controller receipt chain has an invalid attempt count")
    if len(attempts) > MAX_GUARDED_ATTEMPTS:
        raise ValueError("controller receipt chain exceeds guarded attempt limit")
    for number, entry in enumerate(attempts, start=1):
        if not isinstance(entry, Mapping) or set(entry) != {
            "attempt_number", "status", "receipt_path", "receipt_sha256",
            "evidence_path", "evidence_sha256", "failure_fingerprint",
            "residual_owned_process_count",
        }:
            raise ValueError("controller receipt entry is invalid")
        receipt_path = _state_child(campaign_root, entry.get("receipt_path"), "controller receipt")
        if not receipt_path.is_file() or _sha256_file(receipt_path) != _require_sha256(
            entry.get("receipt_sha256"), "controller receipt sha256"
        ):
            raise ValueError("controller receipt hash drift")
        receipt = _strict_object(receipt_path, "controller receipt")
        expected = {
            "schema": "official-openvino-adaptive-controller-receipt/v1",
            "test_id": test_id,
            "context_tokens": context,
            "attempt_number": number,
            "status": entry.get("status"),
            "evidence_path": entry.get("evidence_path"),
            "evidence_sha256": entry.get("evidence_sha256"),
            "failure_fingerprint": entry.get("failure_fingerprint"),
            "residual_owned_process_count": entry.get("residual_owned_process_count"),
            "launch_reserve_mib": START_RESERVE_MIB,
            "emergency_floor_mib": RUNTIME_FLOOR_MIB,
        }
        if receipt != expected or receipt.get("status") not in {
            "passed", "retryable-failure", "safety-boundary"
        }:
            raise ValueError("controller receipt identity drift")
        evidence_value = entry.get("evidence_path")
        if not isinstance(evidence_value, str):
            raise ValueError("controller receipt evidence path is invalid")
        evidence = Path(evidence_value).resolve()
        if not evidence.is_file() or _sha256_file(evidence) != _require_sha256(
            entry.get("evidence_sha256"), "controller receipt evidence sha256"
        ):
            raise ValueError("controller receipt evidence hash drift")
    if attempts:
        final = attempts[-1]
        if (
            final["evidence_path"] != step.get("evidence_path")
            or final["evidence_sha256"] != step.get("evidence_sha256")
        ):
            raise ValueError("controller final evidence does not match state")


def _validate_state_authority(state: Mapping[str, Any], state_path: Path) -> None:
    """Validate explicit controller and terminal receipts named by a state.

    This operates only on state-listed files.  It intentionally does not scan
    receipt directories, which keeps reconstruction reproducible and prevents
    a later attempt from becoming authority by discovery.
    """

    campaign_root = Path(state_path).parent.resolve()
    bindings = state["bindings"]
    reference_path = bindings.get("reference_boundary_index_path")
    reference_hash = bindings.get("reference_boundary_index_sha256")
    for key, raw_step in state["steps"].items():
        assert isinstance(key, str) and isinstance(raw_step, Mapping)
        test_id, text_context = key.split(":", 1)
        context = int(text_context)
        if raw_step.get("test_id") != test_id or raw_step.get("context_tokens") != context:
            raise ValueError("adaptive campaign state step identity drift")
        status = raw_step.get("runtime_status")
        evidence_value = raw_step.get("evidence_path")
        evidence_hash = _require_sha256(raw_step.get("evidence_sha256"), "state evidence sha256")
        if not isinstance(evidence_value, str):
            raise ValueError("adaptive campaign state evidence path is invalid")
        evidence = Path(evidence_value).resolve()
        if not evidence.is_file() or _sha256_file(evidence) != evidence_hash:
            raise ValueError("adaptive campaign state evidence hash drift")
        if status == "artifact-preparation-terminal":
            if test_id != "OV-13" or context != CONTEXTS[0] or raw_step.get("attempt_count") != 0 or raw_step.get("attempts") != []:
                raise ValueError("artifact-preparation terminal identity is invalid")
            receipt_path = _state_child(campaign_root, raw_step.get("terminal_receipt_path"), "artifact terminal controller receipt")
            if not receipt_path.is_file() or _sha256_file(receipt_path) != _require_sha256(
                raw_step.get("terminal_receipt_sha256"), "artifact terminal controller receipt sha256"
            ):
                raise ValueError("artifact terminal controller receipt hash drift")
            receipt = _strict_object(receipt_path, "artifact terminal controller receipt")
            if receipt != {
                "schema": "official-openvino-adaptive-controller-terminal-receipt/v1",
                "test_id": "OV-13",
                "context_tokens": CONTEXTS[0],
                "status": "artifact-preparation-terminal",
                "stage": "artifact-preparation",
                "source_receipt_path": str(evidence),
                "source_receipt_sha256": evidence_hash,
                "matrix_sha256": bindings["matrix_sha256"],
                "spec_index_sha256": bindings["spec_index_sha256"],
            }:
                raise ValueError("artifact terminal controller receipt identity drift")
        elif raw_step.get("boundary_source") == "explicit-reference-index":
            if status != "boundary-confirmed" or reference_path is None or (
                str(evidence) != str(Path(str(reference_path)).resolve())
                or evidence_hash != reference_hash
            ):
                raise ValueError("reference boundary state authority drift")
            attempts = raw_step.get("attempts")
            if not isinstance(attempts, list) or len(attempts) != raw_step.get("attempt_count"):
                raise ValueError("reference boundary receipt count is invalid")
            for attempt in attempts:
                if not isinstance(attempt, Mapping):
                    raise ValueError("reference boundary receipt is invalid")
                source = Path(str(attempt.get("path"))).resolve()
                if not source.is_file() or _sha256_file(source) != _require_sha256(
                    attempt.get("sha256"), "reference boundary attempt sha256"
                ):
                    raise ValueError("reference boundary receipt hash drift")
        else:
            _validate_controller_attempts(
                raw_step,
                campaign_root=campaign_root,
                test_id=test_id,
                context=context,
            )
            attempts = raw_step["attempts"]
            if not attempts:
                raise ValueError("state step has no governing controller receipt")
            final_status = attempts[-1]["status"]
            if status == "passed" and final_status != "passed":
                raise ValueError("passed state step lacks a passed controller receipt")
            if status == "boundary-confirmed" and final_status not in {"retryable-failure", "safety-boundary"}:
                raise ValueError("confirmed boundary lacks a failure controller receipt")
            if status in {"inconclusive-safety-boundary", "safety-boundary"} and final_status != "safety-boundary":
                raise ValueError("safety terminal lacks a safety controller receipt")
        quality_terminal = raw_step.get("quality_terminal")
        if quality_terminal is not None:
            required_terminal = {
                "stage", "principal_reason", "evidence_path", "evidence_sha256",
                "controller_receipt_path", "controller_receipt_sha256",
            }
            if (
                status != "passed"
                or not isinstance(quality_terminal, Mapping)
                or set(quality_terminal) != required_terminal
                or quality_terminal.get("stage") not in {
                    "quality-worker", "quality-capture", "quality-adjudication"
                }
                or not isinstance(quality_terminal.get("principal_reason"), str)
                or not quality_terminal["principal_reason"].strip()
            ):
                raise ValueError("quality terminal is invalid")
            terminal_evidence = Path(str(quality_terminal["evidence_path"])).resolve()
            terminal_hash = _require_sha256(
                quality_terminal["evidence_sha256"], "quality terminal evidence sha256"
            )
            receipt_path = _state_child(
                campaign_root, quality_terminal["controller_receipt_path"], "quality terminal controller receipt"
            )
            if (
                not terminal_evidence.is_file()
                or _sha256_file(terminal_evidence) != terminal_hash
                or not receipt_path.is_file()
                or _sha256_file(receipt_path) != _require_sha256(
                    quality_terminal["controller_receipt_sha256"], "quality terminal controller receipt sha256"
                )
            ):
                raise ValueError("quality terminal evidence or receipt hash drift")
            receipt = _strict_object(receipt_path, "quality terminal controller receipt")
            if receipt != {
                "schema": "official-openvino-adaptive-quality-terminal-receipt/v1",
                "test_id": test_id,
                "context_tokens": context,
                "stage": quality_terminal["stage"],
                "principal_reason": quality_terminal["principal_reason"],
                "evidence_path": str(terminal_evidence),
                "evidence_sha256": terminal_hash,
                "runtime_evidence_path": str(evidence),
                "runtime_evidence_sha256": evidence_hash,
            }:
                raise ValueError("quality terminal controller receipt identity drift")


def _state_step_references(state: Mapping[str, Any], output_path: Path) -> list[dict[str, Any]]:
    raw_steps = state.get("steps")
    if not isinstance(raw_steps, Mapping):
        raise ValueError("adaptive campaign state steps are missing")
    steps: list[dict[str, Any]] = []
    for state_key, raw_step in sorted(raw_steps.items()):
        if not isinstance(state_key, str) or not isinstance(raw_step, Mapping):
            raise ValueError("adaptive campaign state step is invalid")
        try:
            test_id, text_context = state_key.split(":", 1)
            context = int(text_context)
        except (ValueError, TypeError) as error:
            raise ValueError("adaptive campaign state step key is invalid") from error
        if raw_step.get("test_id", test_id) != test_id or raw_step.get("context_tokens", context) != context:
            raise ValueError("adaptive campaign state step identity is invalid")
        status = raw_step.get("runtime_status")
        if status == "passed":
            evidence_path = Path(str(raw_step.get("evidence_path"))).resolve()
            expected = _require_sha256(raw_step.get("evidence_sha256"), "runtime evidence sha256")
            if not evidence_path.is_file() or _sha256_file(evidence_path) != expected:
                raise ValueError("adaptive campaign runtime evidence hash drift")
            step: dict[str, Any] = {
                "test_id": test_id,
                "context_tokens": context,
                "runtime": _reference_for_output(evidence_path, output_path),
            }
            result = raw_step.get("quality_result")
            if isinstance(result, Mapping):
                capture_path = result.get("capture_summary_path")
                if isinstance(capture_path, str) and capture_path:
                    step["quality_capture"] = _reference_for_output(
                        Path(capture_path), output_path
                    )
                bundle = result.get("quality_adjudication_bundle")
                if bundle is not None:
                    step["quality_adjudication"] = _task_six_bundle_references(
                        bundle, output_path=output_path
                    )
            quality_terminal = raw_step.get("quality_terminal")
            if quality_terminal is not None:
                if not isinstance(quality_terminal, Mapping) or set(quality_terminal) != {
                    "stage", "principal_reason", "evidence_path", "evidence_sha256",
                    "controller_receipt_path", "controller_receipt_sha256",
                }:
                    raise ValueError("quality terminal is not governed by a controller receipt")
                terminal_path = Path(str(quality_terminal["evidence_path"])).resolve()
                terminal_hash = _require_sha256(
                    quality_terminal["evidence_sha256"], "quality terminal evidence sha256"
                )
                if not terminal_path.is_file() or _sha256_file(terminal_path) != terminal_hash:
                    raise ValueError("quality terminal evidence hash drift")
                step["quality_terminal"] = {
                    "stage": quality_terminal["stage"],
                    "principal_reason": quality_terminal["principal_reason"],
                    "evidence": _reference_for_output(
                        terminal_path,
                        output_path,
                        external_authority="state-quality-terminal-root",
                    ),
                }
            steps.append(step)
            continue
        if status in {
            "boundary-confirmed",
            "inconclusive-safety-boundary",
            "safety-boundary",
            "artifact-preparation-terminal",
        }:
            evidence_path = Path(str(raw_step.get("evidence_path"))).resolve()
            expected = _require_sha256(raw_step.get("evidence_sha256"), "terminal evidence sha256")
            if not evidence_path.is_file() or _sha256_file(evidence_path) != expected:
                raise ValueError("adaptive campaign terminal evidence hash drift")
            steps.append(
                {
                    "test_id": test_id,
                    "context_tokens": context,
                    "terminal": {
                        "stage": str(raw_step.get("terminal_stage") or (
                            "artifact-preparation"
                            if status == "artifact-preparation-terminal"
                            else (
                                "runtime-boundary"
                                if status == "boundary-confirmed"
                                else status
                            )
                        )),
                        "principal_reason": str(raw_step.get("principal_reason") or status),
                        "evidence": _reference_for_output(
                            evidence_path,
                            output_path,
                            external_authority=(
                                "state-reference-boundary-root"
                                if raw_step.get("boundary_source")
                                == "explicit-reference-index"
                                else "state-artifact-terminal-root"
                                if status == "artifact-preparation-terminal"
                                else "state-runtime-terminal-root"
                            ),
                        ),
                    },
                }
            )
    return steps


def _task_six_bundle_references(
    bundle: object,
    *,
    output_path: Path,
) -> dict[str, Any]:
    """Project the complete Task 6 authority named by controller state only."""

    required = {
        "result", "scoring_input", "private_map", "judge_score_sheets",
        "pairwise_reviews", "manual_adjudications",
    }
    if not isinstance(bundle, Mapping) or set(bundle) != required:
        raise ValueError("state quality adjudication bundle is incomplete")

    def state_reference(value: object, label: str) -> dict[str, str]:
        if not isinstance(value, Mapping) or set(value) != {"path", "sha256"}:
            raise ValueError(f"state {label} reference is invalid")
        raw_path = value.get("path")
        if not isinstance(raw_path, str) or not raw_path:
            raise ValueError(f"state {label} path is invalid")
        source = Path(raw_path).resolve()
        expected = _require_sha256(value.get("sha256"), f"state {label} sha256")
        if not source.is_file() or _sha256_file(source) != expected:
            raise ValueError(f"state {label} hash drift")
        return _reference_for_output(source, output_path)

    projected: dict[str, Any] = {
        field: state_reference(bundle[field], f"quality {field}")
        for field in (
            "result", "scoring_input", "private_map", "pairwise_reviews",
            "manual_adjudications",
        )
    }
    sheets = bundle["judge_score_sheets"]
    if not isinstance(sheets, list) or len(sheets) != 2:
        raise ValueError("state quality adjudication bundle requires two judge sheets")
    projected["judge_score_sheets"] = [
        state_reference(sheet, "quality judge score sheet") for sheet in sheets
    ]
    return projected


def load_and_validate_campaign_state(
    campaign_state_path: Path,
    matrix_path: Path | None = None,
) -> dict[str, Any]:
    """Reopen the canonical controller checkpoint without discovering evidence.

    The checkpoint is the authority for every step named by a release input;
    its bindings deliberately retain absolute machine-local evidence paths.
    The *release* then names those immutable files relatively and by hash.
    """

    state_path = Path(campaign_state_path).resolve()
    raw = _strict_object(state_path, "adaptive campaign state")
    if raw.get("schema") != "official-openvino-adaptive-campaign-state/v1":
        raise ValueError("adaptive campaign state schema is invalid")
    expected_policy = {
        "contexts": list(CONTEXTS),
        "candidate_order": list(CANDIDATE_ORDER),
        "start_reserve_mib": START_RESERVE_MIB,
        "runtime_floor_mib": RUNTIME_FLOOR_MIB,
        "max_guarded_attempts": MAX_GUARDED_ATTEMPTS,
    }
    if raw.get("policy") != expected_policy:
        raise ValueError("adaptive campaign controller policy drift")
    state = _validate_state(raw)
    bindings = state.get("bindings")
    required_bindings = {
        "matrix_path", "matrix_sha256", "spec_index_path", "spec_index_sha256",
        "artifact_inventory_path", "artifact_inventory_sha256", "build_root",
        "build_root_sha256", "build_provenance_path", "build_provenance_sha256",
        "sampler_script_path", "sampler_script_sha256", "quality_prompt_set_path",
        "quality_prompt_set_sha256", "quality_rubric_path", "quality_rubric_sha256",
        "quality_output_root", "quality_timeout_seconds",
        "reference_boundary_index_path", "reference_boundary_index_sha256",
    }
    if not isinstance(bindings, Mapping) or set(bindings) != required_bindings:
        raise ValueError("adaptive campaign state bindings are incomplete")

    def bound_file(path_field: str, hash_field: str) -> Path:
        raw_path = bindings.get(path_field)
        if not isinstance(raw_path, str) or not raw_path:
            raise ValueError(f"adaptive campaign {path_field} is invalid")
        source = Path(raw_path).resolve()
        expected = _require_sha256(bindings.get(hash_field), f"adaptive campaign {hash_field}")
        if not source.is_file() or _sha256_file(source) != expected:
            raise ValueError(f"adaptive campaign {path_field} hash drift")
        return source

    declared = bound_file("matrix_path", "matrix_sha256")
    for path_field, hash_field in (
        ("spec_index_path", "spec_index_sha256"),
        ("artifact_inventory_path", "artifact_inventory_sha256"),
        ("build_provenance_path", "build_provenance_sha256"),
        ("sampler_script_path", "sampler_script_sha256"),
        ("quality_prompt_set_path", "quality_prompt_set_sha256"),
        ("quality_rubric_path", "quality_rubric_sha256"),
    ):
        bound_file(path_field, hash_field)
    build_root = bindings.get("build_root")
    if not isinstance(build_root, str) or not Path(build_root).resolve().is_dir():
        raise ValueError("adaptive campaign build root is invalid")
    if _directory_sha256(Path(build_root)) != _require_sha256(
        bindings.get("build_root_sha256"), "adaptive campaign build_root_sha256"
    ):
        raise ValueError("adaptive campaign build root hash drift")
    if (
        not isinstance(bindings.get("quality_output_root"), str)
        or Path(bindings["quality_output_root"]).resolve() != state_path.parent / "quality"
        or type(bindings.get("quality_timeout_seconds")) is not float
        or bindings["quality_timeout_seconds"] != 1800.0
    ):
        raise ValueError("adaptive campaign quality bindings are invalid")
    reference_path = bindings.get("reference_boundary_index_path")
    reference_sha = bindings.get("reference_boundary_index_sha256")
    if (reference_path is None) != (reference_sha is None):
        raise ValueError("adaptive campaign reference boundary binding is invalid")
    if reference_path is not None:
        if not isinstance(reference_path, str):
            raise ValueError("adaptive campaign reference boundary path is invalid")
        reference = Path(reference_path).resolve()
        if not reference.is_file() or _sha256_file(reference) != _require_sha256(
            reference_sha, "adaptive campaign reference boundary hash"
        ):
            raise ValueError("adaptive campaign reference boundary hash drift")
    if matrix_path is not None and declared != Path(matrix_path).resolve():
        raise ValueError("campaign state matrix does not match the requested matrix")
    load_adaptive_comparison_matrix(declared)
    _validate_committed_state_receipts(state, state_path)
    _validate_state_authority(state, state_path)
    return state


def _validate_committed_state_receipts(state: Mapping[str, Any], state_path: Path) -> None:
    """Delegate the checkpoint's receipt/recovery subtree to Task 4 unchanged."""

    bindings = state["bindings"]
    recoveries = [
        step.get("quality_recovery")
        for step in state["steps"].values()
        if isinstance(step, Mapping) and isinstance(step.get("quality_recovery"), Mapping)
    ]
    exemplar = recoveries[0] if recoveries else {}
    config = AdaptiveCampaignConfig(
        matrix_path=Path(str(bindings["matrix_path"])),
        spec_root=Path(str(bindings["spec_index_path"])).parent,
        campaign_root=Path(state_path).parent,
        build_root=Path(str(bindings["build_root"])),
        build_provenance_path=Path(str(bindings["build_provenance_path"])),
        python_executable=Path(str(exemplar.get("python_executable", state_path))),
        python_site_packages=Path(str(exemplar.get("python_site_packages", state_path.parent))),
        openvino_libraries=Path(str(exemplar.get("openvino_libraries", state_path.parent))),
        sampler_script=Path(str(bindings["sampler_script_path"])),
        reference_boundary_index=(
            Path(str(bindings["reference_boundary_index_path"]))
            if bindings["reference_boundary_index_path"] is not None
            else None
        ),
    )
    # ``quality_terminal`` is a Task 7 controller extension.  Task 4's exact
    # validator deliberately predates and rejects that additional key, so give
    # it an otherwise byte-for-byte equivalent state projection and validate
    # the extension separately in ``_validate_state_authority``.
    receipt_state = json.loads(json.dumps(state))
    for step in receipt_state["steps"].values():
        if isinstance(step, dict):
            step.pop("quality_terminal", None)
    _validate_state_receipts(config, receipt_state)
    for key, step in state["steps"].items():
        if step.get("runtime_status") == "passed" and not isinstance(
            step.get("quality_recovery"), Mapping
        ):
            raise ValueError(f"passed state row lacks mandatory quality recovery: {key}")


def explicit_references_from_state(
    state: Mapping[str, Any], *, output_path: Path | None = None
) -> list[dict[str, Any]]:
    """Project only concrete controller-declared evidence references."""

    if output_path is None:
        raise ValueError("output_path is required for relative evidence references")
    return _state_step_references(state, Path(output_path))


def explicit_complete_capture_references(
    state: Mapping[str, Any], *, output_path: Path | None = None
) -> list[dict[str, Any]]:
    if output_path is None:
        raise ValueError("output_path is required for relative evidence references")
    captures: list[dict[str, Any]] = []
    raw_steps = state.get("steps")
    if not isinstance(raw_steps, Mapping):
        raise ValueError("adaptive campaign state steps are missing")
    for key, step in sorted(raw_steps.items()):
        if not isinstance(step, Mapping) or step.get("runtime_status") != "passed":
            continue
        result = step.get("quality_result")
        if not isinstance(result, Mapping) or result.get("status") != "passed":
            continue
        capture_path = result.get("capture_summary_path")
        if not isinstance(capture_path, str) or not capture_path:
            raise ValueError(f"complete quality capture is missing for {key}")
        captures.append(
            {
                "test_id": step.get("test_id"),
                "context_tokens": step.get("context_tokens"),
                "capture": _reference_for_output(Path(capture_path), Path(output_path)),
            }
        )
    return captures


def build_comparison_release_input(
    matrix_path: Path,
    campaign_state_path: Path,
    output_path: Path,
) -> dict[str, Any]:
    state = load_and_validate_campaign_state(campaign_state_path, matrix_path)
    output = Path(output_path).resolve()
    payload = {
        "schema": RELEASE_INPUT_SCHEMA,
        "matrix": _reference_for_output(Path(matrix_path), output),
        "campaign_state": _reference_for_output(Path(campaign_state_path), output),
        "steps": explicit_references_from_state(state, output_path=output),
    }
    return write_canonical_self_hashed_json(output, payload)


def build_quality_capture_index(
    campaign_state_path: Path,
    output_path: Path,
) -> dict[str, Any]:
    state = load_and_validate_campaign_state(campaign_state_path)
    output = Path(output_path).resolve()
    return write_canonical_self_hashed_json(
        output,
        {
            "schema": CAPTURE_INDEX_SCHEMA,
            "captures": explicit_complete_capture_references(state, output_path=output),
        },
    )


def _same_reference(left: object, right: object, *, base: Path) -> bool:
    for value in (left, right):
        if isinstance(value, Mapping) and isinstance(value.get("path"), str) and Path(value["path"]).is_absolute():
            raise ValueError("release reference path must be relative")
    try:
        left_path, left_sha = _resolve_reference(left, base=base, label="campaign state projection")
        right_path, right_sha = _resolve_reference(right, base=base, label="release input projection")
    except ValueError:
        return False
    return left_path == right_path and left_sha == right_sha


def _assert_authoritative_projection(
    state: Mapping[str, Any],
    steps: Sequence[Mapping[str, Any]],
    *,
    base: Path,
) -> None:
    expected = _state_step_references(state, base / "release-input.json")
    if len(expected) != len(steps):
        raise ValueError("release steps do not exactly match campaign state")
    for source_step, state_step in zip(steps, expected, strict=True):
        _validate_release_step_references(source_step, base=base)
        source_terminal = source_step.get("terminal")
        if isinstance(source_terminal, Mapping):
            stage = source_terminal.get("stage")
            if stage not in {
                "runtime-boundary", "inconclusive-safety-boundary",
                "safety-boundary", "artifact-preparation",
            }:
                raise ValueError("terminal stage is not approved")
        if (
            source_step.get("test_id") != state_step.get("test_id")
            or source_step.get("context_tokens") != state_step.get("context_tokens")
            or set(source_step) != set(state_step)
        ):
            raise ValueError("release steps do not exactly match campaign state")
        if source_step != state_step:
            raise ValueError("release steps do not exactly match campaign state")


def _validate_release_step_references(step: Mapping[str, Any], *, base: Path) -> None:
    for field in ("runtime", "quality_capture"):
        if field in step:
            _resolve_reference(step[field], base=base, label=f"release {field}")
    for field in ("terminal", "quality_terminal"):
        value = step.get(field)
        if isinstance(value, Mapping) and "evidence" in value:
            _resolve_reference(
                value["evidence"], base=base, label=f"release {field} evidence"
            )
    bundle = step.get("quality_adjudication")
    if bundle is None:
        return
    required = {
        "result", "scoring_input", "private_map", "judge_score_sheets",
        "pairwise_reviews", "manual_adjudications",
    }
    if not isinstance(bundle, Mapping) or set(bundle) != required:
        raise ValueError("release quality adjudication bundle is invalid")
    for field in (
        "result", "scoring_input", "private_map", "pairwise_reviews",
        "manual_adjudications",
    ):
        _resolve_reference(bundle[field], base=base, label=f"release quality {field}")
    sheets = bundle["judge_score_sheets"]
    if not isinstance(sheets, list) or len(sheets) != 2:
        raise ValueError("release quality adjudication judge sheets are invalid")
    for sheet in sheets:
        _resolve_reference(sheet, base=base, label="release quality judge score sheet")


def load_comparison_release_input(
    release_input: Mapping[str, Any] | Path,
) -> _LoadedReleaseInput:
    if isinstance(release_input, Path):
        path = Path(release_input).resolve()
        value = _strict_object(path, "comparison release input")
        base = path.parent
    elif isinstance(release_input, Mapping):
        value = dict(release_input)
        mapping_base = value.pop("__base_path", None)
        if not isinstance(mapping_base, str) or not mapping_base:
            raise ValueError("mapping release input requires a private base path")
        base = Path(mapping_base).resolve()
    else:
        raise TypeError("release_input must be a mapping or Path")
    if value.get("schema") != RELEASE_INPUT_SCHEMA:
        raise ValueError("comparison release input schema is invalid")
    if "campaign_state" not in value:
        raise ValueError("comparison release input requires a campaign state")
    if set(value) != {
        "schema", "matrix", "campaign_state", "steps", "release_input_sha256"
    }:
        raise ValueError("comparison release input fields are incomplete or unexpected")
    if not _self_hash_matches(value, "release_input_sha256"):
        raise ValueError("comparison release input self-hash is invalid")
    matrix_path, matrix_sha256 = _resolve_reference(value.get("matrix"), base=base, label="comparison matrix")
    if value.get("campaign_state") is None:
        raise ValueError("comparison release input requires a campaign state")
    campaign_state_path, campaign_state_sha256 = _resolve_reference(
        value["campaign_state"], base=base, label="campaign state"
    )
    campaign_state = load_and_validate_campaign_state(campaign_state_path, matrix_path)
    raw_steps = value.get("steps")
    if not isinstance(raw_steps, list) or not all(isinstance(step, Mapping) for step in raw_steps):
        raise ValueError("comparison release input steps are invalid")
    _assert_authoritative_projection(campaign_state, raw_steps, base=base)
    return _LoadedReleaseInput(
        base=base,
        matrix_path=matrix_path,
        matrix_sha256=matrix_sha256,
        campaign_state_path=campaign_state_path,
        campaign_state_sha256=campaign_state_sha256,
        campaign_state=campaign_state,
        steps=tuple(dict(step) for step in raw_steps),
    )


def _quality_campaign_input_for_runtime(
    *,
    source: _LoadedReleaseInput,
    key: ComparisonKey,
    runtime_path: Path,
    runtime_sha256: str,
) -> Any:
    """Reopen only the Task 4 receipt which names this Task 3 sequence."""

    state_step = source.campaign_state["steps"].get(
        f"{key.test_id}:{key.context_tokens}"
    )
    if not isinstance(state_step, Mapping):
        raise ValueError("runtime has no authoritative state row")
    recovery = state_step.get("quality_recovery")
    if not isinstance(recovery, Mapping):
        raise ValueError("runtime has no accepted Task 4 recovery authority")
    if (
        recovery.get("runtime_evidence_path") != str(runtime_path)
        or recovery.get("runtime_evidence_sha256") != runtime_sha256
        or recovery.get("test_id") != key.test_id
        or recovery.get("context_tokens") != key.context_tokens
    ):
        raise ValueError("quality recovery does not bind the reconciled runtime")
    try:
        campaign_input = quality_campaign_input_from_recovery(recovery)
        if Path(campaign_input.attempt_sequence_path).resolve() != runtime_path:
            raise ValueError("quality recovery names a different attempt sequence")
        return campaign_input
    except (OSError, TypeError, ValueError) as error:
        raise ValueError(
            "runtime does not validate against the committed Task 4 recovery: "
            f"{error}"
        ) from error


def _campaign_child(root: Path, value: object, label: str) -> Path:
    if not isinstance(value, str) or not value or "\\" in value:
        raise ValueError(f"{label} path is invalid")
    lexical = Path(value)
    if (
        lexical.is_absolute()
        or lexical.as_posix() != value
        or any(part in {"", ".", ".."} for part in lexical.parts)
    ):
        raise ValueError(f"{label} path is not normalized")
    resolved = (root / lexical).resolve()
    if root != resolved and root not in resolved.parents:
        raise ValueError(f"{label} path escapes the Task 3 campaign")
    return resolved


def _explicit_runtime_receipt(
    *,
    root: Path,
    receipt: object,
    role: str,
    identity: Mapping[str, Any],
    template: Mapping[str, Any],
) -> tuple[dict[str, Any], Path]:
    """Validate one sequence-named accepted attempt without directory search."""

    if not isinstance(receipt, Mapping) or set(receipt) != {
        "schema", "role", "attempt_number", "campaign_identity_sha256",
        "spec_sha256", "spec_path", "spec_file_sha256",
        "runtime_record_path", "runtime_record_sha256", "accepted",
    }:
        raise ValueError(f"{role} accepted receipt schema is invalid")
    number = receipt.get("attempt_number")
    if type(number) is not int or not 1 <= number <= 999:
        raise ValueError(f"{role} accepted receipt attempt number is invalid")
    expected_spec = _role_spec(
        template,
        role,
        str(identity["campaign_identity_sha256"]),
    )
    attempt = root / "attempts" / role / f"attempt-{number:03d}"
    spec_path = attempt / "spec.json"
    record_path = attempt / "run" / "attempt.json"
    if (
        receipt.get("schema") != RECEIPT_SCHEMA
        or receipt.get("role") != role
        or receipt.get("accepted") is not True
        or receipt.get("campaign_identity_sha256")
        != identity["campaign_identity_sha256"]
        or receipt.get("spec_sha256") != task_three_sha256_json(expected_spec)
        or receipt.get("spec_path") != spec_path.relative_to(root).as_posix()
        or receipt.get("runtime_record_path")
        != record_path.relative_to(root).as_posix()
    ):
        raise ValueError(f"{role} accepted receipt does not bind its sequence")
    if (
        _campaign_child(root, receipt["spec_path"], f"{role} worker spec")
        != spec_path.resolve()
        or _campaign_child(root, receipt["runtime_record_path"], f"{role} record")
        != record_path.resolve()
    ):
        raise ValueError(f"{role} accepted receipt path is not exact")
    if (
        not spec_path.is_file()
        or not record_path.is_file()
        or _sha256_file(spec_path)
        != _require_sha256(receipt.get("spec_file_sha256"), f"{role} spec hash")
        or _sha256_file(record_path)
        != _require_sha256(
            receipt.get("runtime_record_sha256"), f"{role} record hash"
        )
    ):
        raise ValueError(f"{role} accepted receipt evidence hash drift")
    if _strict_object(attempt / "sequence-receipt.json", f"{role} receipt") != dict(receipt):
        raise ValueError(f"{role} accepted receipt file differs from sequence")
    spec = _strict_object(spec_path, f"{role} worker spec")
    if spec != expected_spec:
        raise ValueError(f"{role} worker spec differs from its receipt authority")
    validate_worker_spec_against_matrix_case(
        spec, identity["identity"]["matrix"]["case"]
    )
    record = _strict_object(record_path, f"{role} runtime record")
    if (
        record.get("role") != role
        or record.get("valid") is not True
        or type(record.get("cleanup_process_count")) is not int
        or record["cleanup_process_count"] != 0
        or not _runtime_record_matches_spec(record, expected_spec, role)
    ):
        raise ValueError(f"{role} runtime record is not an accepted attempt")
    validate_runtime_record_against_matrix_case(
        record, identity["identity"]["matrix"]["case"]
    )
    command = record.get("command")
    if (
        not isinstance(command, list)
        or not command
        or not all(isinstance(item, str) and item for item in command)
    ):
        raise ValueError(f"{role} runtime record command is invalid")
    expected_hashes = {
        "artifact_manifest_sha256": identity["identity"]["model"][
            "artifact_manifest_sha256"
        ],
        "prompt_sha256": identity["identity"]["prompt"]["sha256"],
        "matrix_sha256": task_three_sha256_json(identity["identity"]["matrix"]),
        "build_provenance_sha256": identity["identity"]["build"][
            "provenance_sha256"
        ],
        "command_sha256": task_three_sha256_json(command),
        "evidence_sha256": identity["campaign_identity_sha256"],
    }
    if (
        record.get("identity_hashes") != expected_hashes
        or record.get("runtime_property_sha256")
        != task_three_sha256_json(identity["identity"]["config"])
    ):
        raise ValueError(f"{role} runtime record identity authority drift")
    return record, record_path


def _explicit_accepted_quality_campaign(campaign_input: Any) -> AcceptedQualityCampaign:
    """Validate only receipt-referenced Task 3 evidence and reopen Task 5."""

    root = Path(campaign_input.campaign_root).resolve()
    sequence_path = Path(campaign_input.attempt_sequence_path).resolve()
    if sequence_path != root / "attempt-sequence.json":
        raise ValueError("Task 3 attempt sequence is outside its campaign root")
    identity = build_campaign_identity(
        spec_path=campaign_input.spec_path,
        matrix_path=campaign_input.matrix_path,
        artifact_manifest_path=campaign_input.artifact_manifest_path,
        build_provenance_path=campaign_input.build_provenance_path,
        build_root=campaign_input.build_root,
        repo_root=campaign_input.repo_root,
        python_executable=campaign_input.python_executable,
        python_site_packages=campaign_input.python_site_packages,
        openvino_libraries=campaign_input.openvino_libraries,
    )
    identity_path = root / "campaign-identity.json"
    if identity_path.read_bytes() != _canonical_identity_bytes(identity):
        raise ValueError("Task 3 campaign identity does not match its authorities")
    if _strict_object(identity_path, "Task 3 campaign identity") != identity:
        raise ValueError("Task 3 campaign identity is invalid")
    sequence = _strict_object(sequence_path, "Task 3 attempt sequence")
    template = _sequence_spec(campaign_input.spec_path)
    receipts = [
        _explicit_runtime_receipt(
            root=root,
            receipt=sequence.get("pilot"),
            role="pilot",
            identity=identity,
            template=template,
        ),
        _explicit_runtime_receipt(
            root=root,
            receipt=sequence.get("warmup"),
            role="warmup",
            identity=identity,
            template=template,
        ),
    ]
    samples = sequence.get("accepted_samples")
    if not isinstance(samples, list) or len(samples) != 3:
        raise ValueError("Task 3 sequence must name exactly three formal samples")
    for role, receipt in zip(SEQUENCE_ROLES[2:], samples, strict=True):
        receipts.append(
            _explicit_runtime_receipt(
                root=root,
                receipt=receipt,
                role=role,
                identity=identity,
                template=template,
            )
        )
    summary_path = root / "measurement-summary.json"
    summary = _strict_object(summary_path, "Task 3 measurement summary")
    expected_summary = summarize_samples(
        [measurement_sample(record, path) for record, path in receipts[2:]]
    )
    expected_summary.update(
        {
            "accepted": True,
            "cleanup_process_count": 0,
            "test_id": template["controlled_test_id"],
            "context_tokens": template["context"],
            "campaign_identity_sha256": identity["campaign_identity_sha256"],
            "runtime_config_sha256": task_three_sha256_json(
                identity["identity"]["config"]
            ),
        }
    )
    expected_summary = json.loads(json.dumps(expected_summary, allow_nan=False))
    expected_sequence = {
        "schema": SEQUENCE_SCHEMA,
        "campaign_identity_sha256": identity["campaign_identity_sha256"],
        "pilot_passed": True,
        "warmup_excluded": True,
        "pilot": sequence["pilot"],
        "warmup": sequence["warmup"],
        "accepted_samples": samples,
        "accepted_sample_count": 3,
        "cleanup_process_count": 0,
        "measurement_summary_path": "measurement-summary.json",
        "measurement_summary_sha256": _sha256_file(summary_path),
    }
    if summary != expected_summary or sequence != expected_sequence:
        raise ValueError("Task 3 sequence does not match its explicit receipts")
    prompt_contract = load_prompt_contract(
        campaign_input.prompt_set_path, campaign_input.rendered_root
    )
    _rubric, rubric_sha256 = _load_frozen_rubric(campaign_input.rubric_path)
    environment = build_worker_environment(
        build_root=campaign_input.build_root,
        repo_root=campaign_input.repo_root,
        python_site_packages=campaign_input.python_site_packages,
        openvino_libraries=campaign_input.openvino_libraries,
        base_environment={
            key: value
            for key, value in os.environ.items()
            if key.strip() and value.strip()
        },
    )
    return AcceptedQualityCampaign(
        identity=identity,
        measurement_summary=summary,
        measurement_summary_sha256=_sha256_file(summary_path),
        runtime_config_sha256=task_three_sha256_json(
            identity["identity"]["config"]
        ),
        campaign_identity_sha256=str(identity["campaign_identity_sha256"]),
        worker_environment=environment,
        prompt_contract=prompt_contract,
        rubric_sha256=rubric_sha256,
        output_root=_validated_lexical_output_root(campaign_input.output_root),
        **{
            field: Path(getattr(campaign_input, field)).resolve()
            for field in (
                "campaign_root", "spec_path", "matrix_path",
                "artifact_manifest_path", "build_provenance_path", "build_root",
                "repo_root", "python_executable", "python_site_packages",
                "openvino_libraries", "sampler_script", "prompt_set_path",
                "rendered_root", "rubric_path",
            )
        },
        timeout_seconds=float(campaign_input.timeout_seconds),
    )


def _accepted_runtime_campaign(
    *,
    source: _LoadedReleaseInput,
    key: ComparisonKey,
    runtime_path: Path,
    runtime_sha256: str,
) -> AcceptedQualityCampaign:
    try:
        campaign_input = _quality_campaign_input_for_runtime(
            source=source,
            key=key,
            runtime_path=runtime_path,
            runtime_sha256=runtime_sha256,
        )
        return _explicit_accepted_quality_campaign(campaign_input)
    except (OSError, TypeError, ValueError) as error:
        raise ValueError(
            "runtime does not validate against the committed Task 3 evidence: "
            f"{error}"
        ) from error


def _runtime_outcome(
    step: Mapping[str, Any],
    source: _LoadedReleaseInput,
) -> tuple[ComparisonKey, ComparisonRuntimeOutcome]:
    test_id = step.get("test_id")
    context = step.get("context_tokens")
    if not isinstance(test_id, str) or not isinstance(context, int) or isinstance(context, bool):
        raise ValueError("runtime step identity is invalid")
    key = ComparisonKey(test_id, context)
    sequence_path, sequence_sha = _resolve_reference(step.get("runtime"), base=source.base, label=f"{test_id}:{context} runtime")
    sequence = _strict_object(sequence_path, "runtime attempt sequence")
    if sequence.get("schema") != "official-openvino-wb04-attempt-sequence/v1":
        raise ValueError("runtime attempt sequence schema is invalid")
    if sequence.get("pilot_passed") is not True or sequence.get("warmup_excluded") is not True:
        raise ValueError("runtime sequence pilot/warmup binding is invalid")
    if sequence.get("accepted_sample_count") != 3 or sequence.get("cleanup_process_count") != 0:
        raise ValueError("runtime sequence formal sample count or cleanup is invalid")
    samples_raw = sequence.get("accepted_samples")
    if not isinstance(samples_raw, list) or len(samples_raw) != 3:
        raise ValueError("runtime sequence must contain exactly three formal samples")
    root = sequence_path.parent
    _accepted_runtime_campaign(
        source=source,
        key=key,
        runtime_path=sequence_path,
        runtime_sha256=sequence_sha,
    )

    samples: list[Mapping[str, Any]] = []
    for receipt in samples_raw:
        if not isinstance(receipt, Mapping) or not isinstance(
            receipt.get("runtime_record_path"), str
        ):
            raise ValueError("accepted runtime sample receipt is invalid")
        record_path = (root / receipt["runtime_record_path"]).resolve()
        if root != record_path and root not in record_path.parents:
            raise ValueError("accepted runtime sample record escapes attempt root")
        raw = _strict_object(record_path, "accepted runtime sample record")
        samples.append(build_adaptive_runtime_sample(
            raw,
            record_path,
        ))
    recomputed = summarize_adaptive_runtime_samples(samples)
    timing = recomputed["timing"]
    memory = recomputed["memory"]
    utilisation = recomputed["utilisation"]
    return key, ComparisonRuntimeOutcome(
        key=key,
        samples=(samples[0], samples[1], samples[2]),
        timing=timing,
        memory=memory,
        utilisation=utilisation,
        activation=recomputed["activation"],
        identity_hashes=recomputed["identity_hashes"],
        evidence_path=sequence_path,
        evidence_sha256=sequence_sha,
    )


def reconcile_all_runtime_steps(
    source: _LoadedReleaseInput,
    matrix: Sequence[Any],
) -> dict[ComparisonKey, ComparisonRuntimeOutcome]:
    allowed = {case.test_id for case in matrix}
    result: dict[ComparisonKey, ComparisonRuntimeOutcome] = {}
    for step in source.steps:
        if "runtime" not in step:
            continue
        key, outcome = _runtime_outcome(step, source)
        if key.test_id not in allowed or key.context_tokens not in CONTEXTS:
            raise ValueError("runtime evidence is outside the adaptive comparison matrix")
        if key in result:
            raise ValueError("duplicate runtime evidence")
        result[key] = outcome
    return result


def _explicit_quality_capture_history(
    campaign: AcceptedQualityCampaign,
    *,
    path: Path,
    evidence_paths: Mapping[str, Any] | None,
) -> tuple[dict[str, Any], dict[str, Any]]:
    """Walk the named capture's own backward receipt chain, never its directory."""

    root = Path(campaign.output_root).resolve()
    current = Path(path).resolve()
    primary = root / "capture-summary.json"
    backward: list[Path] = []
    visited: set[Path] = set()
    while True:
        if current in visited:
            raise ValueError("quality capture previous-summary chain contains a cycle")
        visited.add(current)
        if current.parent != root:
            raise ValueError("quality capture is outside its accepted campaign")
        raw = _strict_object(current, "quality capture summary")
        backward.append(current)
        if current == primary:
            if (
                raw.get("previous_capture_summary_path") is not None
                or raw.get("previous_capture_summary_sha256") is not None
            ):
                raise ValueError("quality primary capture summary is not anchored")
            break
        match = re.fullmatch(
            r"capture-summary-recovery-(\d{3})\.json", current.name
        )
        if match is None:
            raise ValueError("quality capture recovery summary path is invalid")
        number = int(match.group(1))
        previous = (
            primary
            if number == 1
            else root / f"capture-summary-recovery-{number - 1:03d}.json"
        )
        if (
            raw.get("previous_capture_summary_path") != str(previous)
            or raw.get("previous_capture_summary_sha256")
            != _sha256_file(previous)
        ):
            raise ValueError("quality capture previous-summary receipt is invalid")
        current = previous

    previous_path: Path | None = None
    previous_summary: Mapping[str, Any] | None = None
    latest_summary: dict[str, Any] | None = None
    latest_loaded: dict[str, Any] = {}
    process_roots: dict[str, Path] = {}
    for summary_path in reversed(backward):
        summary, loaded = validate_quality_capture_summary(
            campaign,
            summary_path,
            evidence_paths=evidence_paths,
            previous_summary_path=previous_path,
        )
        if previous_summary is not None:
            validate_quality_capture_transition(previous_summary, summary)
        for receipt in summary["prompt_receipts"]:
            identity = receipt.get("process_identity")
            if not isinstance(identity, str):
                continue
            prompt_root = Path(str(receipt["prompt_root"])).resolve()
            earlier = process_roots.setdefault(identity, prompt_root)
            if earlier != prompt_root:
                raise ValueError("quality capture history reused a worker process")
        previous_path = summary_path
        previous_summary = summary
        latest_summary = summary
        latest_loaded = loaded
    if latest_summary is None:
        raise ValueError("quality capture history is empty")
    return latest_summary, latest_loaded


def _quality_capture_complete(
    *,
    source: _LoadedReleaseInput,
    key: ComparisonKey,
    runtime_row: ComparisonRuntimeOutcome,
    path: Path,
) -> Any | None:
    """Use Task 4/5's capture validator; do not accept result-shaped stubs."""

    state_step = source.campaign_state["steps"].get(
        f"{key.test_id}:{key.context_tokens}"
    )
    if not isinstance(state_step, Mapping):  # projection already guards this
        raise ValueError("quality capture has no authoritative state row")
    recovery = state_step.get("quality_recovery")
    if not isinstance(recovery, Mapping):
        raise ValueError("quality capture has no accepted Task 4 recovery authority")
    if (
        recovery.get("runtime_evidence_path") != str(runtime_row.evidence_path)
        or recovery.get("runtime_evidence_sha256") != runtime_row.evidence_sha256
        or recovery.get("test_id") != key.test_id
        or recovery.get("context_tokens") != key.context_tokens
    ):
        raise ValueError("quality recovery does not bind the reconciled runtime")
    try:
        campaign_input = _quality_campaign_input_for_runtime(
            source=source,
            key=key,
            runtime_path=runtime_row.evidence_path,
            runtime_sha256=runtime_row.evidence_sha256,
        )
        campaign = _explicit_accepted_quality_campaign(campaign_input)
        expected_root = Path(campaign.output_root).resolve()
        if path.parent != expected_root:
            raise ValueError("quality capture is outside its accepted campaign")
        summary, loaded = _explicit_quality_capture_history(
            campaign,
            path=path,
            evidence_paths=quality_capture_evidence_paths(campaign_input),
        )
    except (OSError, TypeError, ValueError) as error:
        raise ValueError("quality capture does not validate against Task 5 evidence") from error
    if (
        summary.get("prompt_receipt_count") != len(_PROMPTS)
        or set(loaded) != set(_PROMPTS)
        or not all(item.status == "passed" for item in loaded.values())
    ):
        return None
    return campaign


def _quality_from_adjudication_bundle(
    *,
    bundle: object,
    source: _LoadedReleaseInput,
    key: ComparisonKey,
    campaign: Any,
) -> ComparisonQualityOutcome:
    """Recompute Task 6 from every explicitly named blind input."""

    required = {
        "result", "scoring_input", "private_map", "judge_score_sheets",
        "pairwise_reviews", "manual_adjudications",
    }
    if not isinstance(bundle, Mapping) or set(bundle) != required:
        raise ValueError("quality adjudication must name its complete Task 6 evidence bundle")
    result_path, result_sha = _resolve_reference(
        bundle["result"], base=source.base, label="quality adjudication result"
    )
    scoring_path, _ = _resolve_reference(
        bundle["scoring_input"], base=source.base, label="quality scoring input"
    )
    private_path, _ = _resolve_reference(
        bundle["private_map"], base=source.base, label="quality private blind map"
    )
    pairwise_path, _ = _resolve_reference(
        bundle["pairwise_reviews"], base=source.base, label="quality pairwise reviews"
    )
    manual_path, _ = _resolve_reference(
        bundle["manual_adjudications"], base=source.base, label="quality manual adjudications"
    )
    sheets = bundle["judge_score_sheets"]
    if not isinstance(sheets, list) or len(sheets) != 2:
        raise ValueError("quality adjudication requires exactly two judge sheets")
    sheet_values = [
        _strict_object(
            _resolve_reference(sheet, base=source.base, label="quality judge score sheet")[0],
            "quality judge score sheet",
        )
        for sheet in sheets
    ]
    observed = _strict_object(result_path, "quality adjudication result")
    scoring = _strict_object(scoring_path, "quality scoring input")
    private = _strict_object(private_path, "quality private blind map")
    pairwise = _strict_object(pairwise_path, "quality pairwise reviews")
    manual = _strict_object(manual_path, "quality manual adjudications")
    try:
        recomputed = adjudicate_adaptive_quality(
            scoring_input=scoring,
            judge_score_sheets=sheet_values,
            pairwise_reviews=pairwise,
            manual_adjudications=manual,
            blind_map_reader=lambda: private,
            rubric_path=Path(campaign.rubric_path),
            prompt_set_path=Path(campaign.prompt_set_path),
        )
    except (OSError, TypeError, ValueError) as error:
        raise ValueError("quality adjudication Task 6 evidence is invalid") from error
    if observed != recomputed:
        raise ValueError("quality adjudication result does not match recomputed Task 6 evidence")
    return _quality_from_adjudication(
        path=result_path, evidence_sha256=result_sha, key=key
    )


def _quality_from_adjudication(
    *,
    path: Path,
    evidence_sha256: str,
    key: ComparisonKey,
) -> ComparisonQualityOutcome:
    adjudication = _strict_object(path, "quality adjudication")
    if not _self_hash_matches(adjudication, "adjudication_sha256"):
        raise ValueError("quality adjudication self-hash is invalid")
    for field in (
        "scoring_input_sha256",
        "private_map_sha256",
        "prompt_set_sha256",
        "rubric_sha256",
    ):
        if field in adjudication:
            _require_sha256(adjudication[field], field)
    configurations = adjudication.get("configurations")
    if not isinstance(configurations, list):
        raise ValueError("quality adjudication configurations are invalid")
    matches = [
        item
        for item in configurations
        if isinstance(item, Mapping)
        and item.get("test_id") == key.test_id
        and item.get("context_tokens") == key.context_tokens
    ]
    if len(matches) != 1:
        raise ValueError("quality adjudication does not bind exactly one runtime row")
    row = matches[0]
    if row.get("status") != "complete":
        raise ValueError("quality adjudication is not complete")
    scores = row.get("prompt_scores")
    if not isinstance(scores, Mapping) or set(scores) != set(_PROMPTS):
        raise ValueError("quality adjudication must score exactly P1 through P6")
    normalized: dict[str, float] = {}
    for prompt_id in _PROMPTS:
        value = scores[prompt_id]
        if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(float(value)):
            raise ValueError("quality adjudication score is not numeric")
        normalized[prompt_id] = float(value)
    values = list(normalized.values())
    aggregates = {
        "mean": float(statistics.mean(values)),
        "median": float(statistics.median(values)),
        "minimum": float(min(values)),
        "maximum": float(max(values)),
    }
    declared = row.get("aggregates")
    if isinstance(declared, Mapping):
        for field, expected in aggregates.items():
            if field in declared and not math.isclose(float(declared[field]), expected, rel_tol=0.0, abs_tol=1e-9):
                raise ValueError("quality adjudication aggregate does not match prompt scores")
    return ComparisonQualityOutcome(
        key=key,
        status="quality-complete",
        prompt_scores=normalized,
        aggregates=aggregates,
        evidence_path=path,
        evidence_sha256=evidence_sha256,
    )


def reconcile_all_quality_steps(
    source: _LoadedReleaseInput,
    runtime: Mapping[ComparisonKey, ComparisonRuntimeOutcome],
) -> dict[ComparisonKey, ComparisonQualityOutcome]:
    steps = {
        ComparisonKey(str(step.get("test_id")), int(step.get("context_tokens"))): step
        for step in source.steps
        if "runtime" in step
        and isinstance(step.get("test_id"), str)
        and isinstance(step.get("context_tokens"), int)
    }
    result: dict[ComparisonKey, ComparisonQualityOutcome] = {}
    for key, runtime_row in runtime.items():
        step = steps[key]
        capture_ref = step.get("quality_capture")
        if capture_ref is None:
            quality_terminal = step.get("quality_terminal")
            if quality_terminal is not None:
                terminal_path, terminal_sha = _resolve_reference(
                    quality_terminal.get("evidence") if isinstance(quality_terminal, Mapping) else None,
                    base=source.base,
                    label="quality terminal evidence",
                )
                return_value = ComparisonQualityOutcome(
                    key,
                    "quality-terminal",
                    None,
                    None,
                    terminal_path,
                    terminal_sha,
                    terminal_stage=quality_terminal["stage"],
                    principal_reason=quality_terminal["principal_reason"],
                )
                result[key] = return_value
                continue
            result[key] = ComparisonQualityOutcome(key, "quality-blocked", None, None, runtime_row.evidence_path, runtime_row.evidence_sha256)
            continue
        capture_path, capture_sha = _resolve_reference(capture_ref, base=source.base, label="quality capture")
        campaign = _quality_capture_complete(
            source=source,
            key=key,
            runtime_row=runtime_row,
            path=capture_path,
        )
        if campaign is None:
            quality_terminal = step.get("quality_terminal")
            if quality_terminal is not None:
                terminal_path, terminal_sha = _resolve_reference(
                    quality_terminal.get("evidence") if isinstance(quality_terminal, Mapping) else None,
                    base=source.base,
                    label="quality terminal evidence",
                )
                result[key] = ComparisonQualityOutcome(
                    key,
                    "quality-terminal",
                    None,
                    None,
                    terminal_path,
                    terminal_sha,
                    terminal_stage=quality_terminal["stage"],
                    principal_reason=quality_terminal["principal_reason"],
                )
                continue
            result[key] = ComparisonQualityOutcome(key, "quality-blocked", None, None, capture_path, capture_sha)
            continue
        adjudication_bundle = step.get("quality_adjudication")
        quality_terminal = step.get("quality_terminal")
        if quality_terminal is not None and adjudication_bundle is None:
            terminal_path, terminal_sha = _resolve_reference(
                quality_terminal.get("evidence") if isinstance(quality_terminal, Mapping) else None,
                base=source.base,
                label="quality terminal evidence",
            )
            result[key] = ComparisonQualityOutcome(
                key,
                "quality-terminal",
                None,
                None,
                terminal_path,
                terminal_sha,
                terminal_stage=quality_terminal["stage"],
                principal_reason=quality_terminal["principal_reason"],
            )
            continue
        if adjudication_bundle is None:
            result[key] = ComparisonQualityOutcome(key, "capture-complete-awaiting-adjudication", None, None, capture_path, capture_sha)
            continue
        result[key] = _quality_from_adjudication_bundle(
            bundle=adjudication_bundle,
            source=source,
            key=key,
            campaign=campaign,
        )
    return result


def reconcile_all_terminal_steps(
    source: _LoadedReleaseInput,
    matrix: Sequence[Any],
) -> dict[ComparisonKey, Mapping[str, Any]]:
    allowed = {case.test_id for case in matrix}
    result: dict[ComparisonKey, Mapping[str, Any]] = {}
    for step in source.steps:
        terminal = step.get("terminal")
        if terminal is None:
            continue
        test_id = step.get("test_id")
        context = step.get("context_tokens")
        if not isinstance(test_id, str) or not isinstance(context, int) or isinstance(context, bool):
            raise ValueError("terminal identity is invalid")
        key = ComparisonKey(test_id, context)
        if test_id not in allowed or context not in CONTEXTS or key in result:
            raise ValueError("duplicate or out-of-matrix terminal evidence")
        if not isinstance(terminal, Mapping) or set(terminal) != {"stage", "principal_reason", "evidence"}:
            raise ValueError("terminal record has missing or fabricated fields")
        stage = terminal.get("stage")
        reason = terminal.get("principal_reason")
        if not isinstance(stage, str) or not stage or not isinstance(reason, str) or not reason:
            raise ValueError("terminal stage or principal reason is invalid")
        if stage == "artifact-preparation":
            if test_id != "OV-13" or context != CONTEXTS[0]:
                raise ValueError("artifact-preparation terminal is restricted to OV-13 at 512")
        elif stage not in {
            "runtime-boundary", "inconclusive-safety-boundary", "safety-boundary",
        }:
            raise ValueError("terminal stage is not approved")
        evidence_path, evidence_sha = _resolve_reference(terminal.get("evidence"), base=source.base, label="terminal evidence")
        result[key] = {
            "test_id": test_id,
            "context_tokens": context,
            "stage": stage,
            "principal_reason": reason,
            "evidence_path": evidence_path,
            "evidence_sha256": evidence_sha,
        }
    return result


def derive_shared_contexts(
    runtime: Mapping[ComparisonKey, ComparisonRuntimeOutcome],
    test_ids: Sequence[str],
) -> tuple[int, ...]:
    required = tuple(test_ids)
    contexts = sorted(
        {
            key.context_tokens
            for key in runtime
            if key.test_id == required[0]
        }
    ) if required else []
    shared = []
    require_u8 = set(required) == {"OV-12", "OV-TQ-21", "OV-TQ-22"}
    for context in contexts:
        rows = [runtime.get(ComparisonKey(test_id, context)) for test_id in required]
        if any(row is None for row in rows):
            continue
        if require_u8:
            artifacts = {
                row.identity_hashes.get("artifact_manifest_sha256")
                for row in rows
                if row is not None
            }
            if len(artifacts) != 1:
                raise ValueError("shared cache comparison requires identical U8 artifact")
        shared.append(context)
    return tuple(shared)


def derive_boundaries(
    matrix: Sequence[Any],
    runtime: Mapping[ComparisonKey, ComparisonRuntimeOutcome],
    quality: Mapping[ComparisonKey, ComparisonQualityOutcome],
    terminals: Mapping[ComparisonKey, Mapping[str, Any]],
) -> dict[str, BoundaryOutcome]:
    result: dict[str, BoundaryOutcome] = {}
    for case in matrix:
        test_id = case.test_id
        runtime_contexts = [context for context in CONTEXTS if ComparisonKey(test_id, context) in runtime]
        comparable = [
            context
            for context in runtime_contexts
            if quality[ComparisonKey(test_id, context)].status == "quality-complete"
        ]
        terminal_rows = [
            (context, terminals[ComparisonKey(test_id, context)])
            for context in CONTEXTS
            if ComparisonKey(test_id, context) in terminals
            and terminals[ComparisonKey(test_id, context)].get("stage") == "runtime-boundary"
        ]
        first_terminal = min(terminal_rows, default=None, key=lambda item: item[0])
        result[test_id] = BoundaryOutcome(
            test_id=test_id,
            highest_runtime_context=max(runtime_contexts) if runtime_contexts else None,
            highest_fully_comparable_context=max(comparable) if comparable else None,
            first_confirmed_blocked_context=first_terminal[0] if first_terminal else None,
            terminal_stage=str(first_terminal[1]["stage"]) if first_terminal else None,
            terminal_evidence_sha256=str(first_terminal[1]["evidence_sha256"]) if first_terminal else None,
        )
    return result


def reconcile_comparison_release(
    release_input: Mapping[str, Any] | Path,
) -> ComparisonRelease:
    source = load_comparison_release_input(release_input)
    matrix = load_adaptive_comparison_matrix(source.matrix_path)
    runtime = reconcile_all_runtime_steps(source, matrix)
    quality = reconcile_all_quality_steps(source, runtime)
    terminals = reconcile_all_terminal_steps(source, matrix)
    return ComparisonRelease(
        runtime=runtime,
        quality=quality,
        terminals=terminals,
        shared_cache_contexts=derive_shared_contexts(runtime, ("OV-12", "OV-TQ-21", "OV-TQ-22")),
        shared_standard_contexts=derive_shared_contexts(runtime, ("OV-11", "OV-12", "OV-13")),
        boundaries=derive_boundaries(matrix, runtime, quality, terminals),
    )


def reconcile_terminal(release_input: Mapping[str, Any] | Path) -> Mapping[str, Any]:
    source = load_comparison_release_input(release_input)
    terminals = reconcile_all_terminal_steps(source, load_adaptive_comparison_matrix(source.matrix_path))
    if len(terminals) != 1:
        raise ValueError("reconcile_terminal requires exactly one terminal")
    return next(iter(terminals.values()))


def validate_closed_campaign(release: ComparisonRelease) -> None:
    for test_id in CANDIDATE_ORDER:
        pass_contexts = [
            context for context in CONTEXTS if ComparisonKey(test_id, context) in release.runtime
        ]
        terminal_contexts = [
            context for context in CONTEXTS if ComparisonKey(test_id, context) in release.terminals
        ]
        expected_passes: list[int] = []
        for context in CONTEXTS:
            if context in pass_contexts:
                expected_passes.append(context)
                continue
            break
        if pass_contexts != expected_passes:
            raise ValueError(f"{test_id} has an unattempted gap or skipped context")
        terminal = terminal_contexts[0] if terminal_contexts else None
        if len(terminal_contexts) > 1:
            raise ValueError(f"{test_id} terminal is not the next ladder context")
        if terminal is not None:
            next_index = len(expected_passes)
            if next_index >= len(CONTEXTS) or terminal != CONTEXTS[next_index]:
                raise ValueError(f"{test_id} terminal is not the next ladder context")
            if (
                release.terminals[ComparisonKey(test_id, terminal)]["stage"]
                == "artifact-preparation"
                and (test_id != "OV-13" or terminal != CONTEXTS[0])
            ):
                raise ValueError("OV-13 artifact-preparation terminal is restricted to 512")
        if test_id == "OV-13" and terminal is not None and release.terminals[ComparisonKey(test_id, terminal)]["stage"] == "artifact-preparation":
            pass
        elif expected_passes and expected_passes[-1] == 8192 and terminal is None:
            pass
        elif terminal is not None and release.terminals[ComparisonKey(test_id, terminal)]["stage"] == "runtime-boundary":
            pass
        elif terminal is not None:
            raise ValueError(f"{test_id} terminal is not a confirmed runtime boundary")
        else:
            raise ValueError(f"{test_id} ladder is not closed")
        for context in pass_contexts:
            status = release.quality[ComparisonKey(test_id, context)].status
            if status not in {"quality-complete", "capture-complete-awaiting-adjudication", "quality-terminal"}:
                raise ValueError(f"{test_id}:{context} runtime pass lacks complete quality capture or terminal")


def validate_complete_release(release: ComparisonRelease) -> None:
    validate_closed_campaign(release)
    awaiting = [
        outcome.key
        for outcome in release.quality.values()
        if outcome.status == "capture-complete-awaiting-adjudication"
    ]
    if awaiting:
        raise ValueError("complete release requires numeric adjudication for every complete capture")
    missing_scores = [
        outcome.key
        for outcome in release.quality.values()
        if outcome.status != "quality-terminal"
        and (
            outcome.prompt_scores is None
            or set(outcome.prompt_scores) != set(_PROMPTS)
            or any(
                isinstance(score, bool)
                or not isinstance(score, (int, float))
                or not math.isfinite(float(score))
                for score in outcome.prompt_scores.values()
            )
        )
    ]
    if missing_scores:
        raise ValueError("complete release requires exactly six numeric prompt scores")


__all__ = [
    "BoundaryOutcome",
    "ComparisonKey",
    "ComparisonQualityOutcome",
    "ComparisonRelease",
    "ComparisonRuntimeOutcome",
    "build_comparison_release_input",
    "build_quality_capture_index",
    "derive_boundaries",
    "derive_shared_contexts",
    "explicit_complete_capture_references",
    "explicit_references_from_state",
    "file_reference",
    "load_and_validate_campaign_state",
    "load_comparison_release_input",
    "reconcile_all_quality_steps",
    "reconcile_all_runtime_steps",
    "reconcile_all_terminal_steps",
    "reconcile_comparison_release",
    "reconcile_terminal",
    "validate_closed_campaign",
    "validate_complete_release",
    "write_canonical_self_hashed_json",
]
