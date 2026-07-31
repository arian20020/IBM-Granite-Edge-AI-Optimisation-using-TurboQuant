"""Fail-closed reconciliation and Markdown finalization for controlled WB-04.

The release input selects evidence paths explicitly.  This module never scans
multiple campaign roots and never chooses a duplicate measurement implicitly.
Measured values are recomputed from the three hash-bound source attempts;
terminal and expected-rejection rows use non-numeric outcome literals.

This file deliberately lives outside ``scripts/testing/official_openvino``.
That package is part of the frozen runtime campaign identity and must not be
changed merely to render the workbook.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import statistics
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.reconcile import validate_workbook_text
from scripts.testing.official_openvino.metrics import summarize_samples
from scripts.testing.official_openvino.expected_rejections import (
    validate_expected_rejection_evidence,
)
from scripts.testing.official_openvino.matrix import load_matrix
from scripts.testing.official_openvino.runtime_process import measurement_sample
from scripts.testing.official_openvino.scalar_semantic_rejections import (
    validate_scalar_semantic_rejection_evidence,
)
from scripts.testing.measure_official_openvino import (
    validate_runtime_record_against_matrix_case,
    validate_worker_spec_against_matrix_case,
)
from scripts.testing.adjudicate_official_openvino_quality import (
    adjudicate_quality,
    build_blind_scoring_input,
)
from scripts.testing.run_official_openvino_quality import load_prompt_contract


WORKBOOK = (
    ROOT
    / "docs"
    / "testing"
    / "workbooks"
    / "text-templates"
    / "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md"
)
CANONICAL_MATRIX = (
    ROOT / "experiments" / "manifests" / "official-openvino" / "retest-matrix.json"
)
CANONICAL_MATRIX_SHA256 = (
    "7db2636b403d285aa3886c9f23560a9e48bd43645e9aca35e0d1fc4f16eaea42"
)
DEFAULT_RELEASE_INPUT = (
    ROOT
    / "experiments"
    / "raw-results"
    / "openvino-turboquant"
    / "2026-07-30"
    / "reconciliation-input.json"
)
RELEASE_INPUT_EXAMPLE = (
    ROOT
    / "scripts"
    / "testing"
    / "examples"
    / "official-openvino-wb04-reconciliation-input.example.json"
)
CONTROLLED_INVENTORY_ROOTS = (
    ROOT
    / "experiments"
    / "raw-results"
    / "openvino-turboquant"
    / "2026-07-30"
    / "artifacts",
    ROOT
    / "experiments"
    / "raw-results"
    / "openvino-turboquant"
    / "2026-07-30"
    / "campaign-specs-dbbb784",
)
OV06_DEVICE_ATTEMPTS = (
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "diagnostics/ov-06-gpu-calibration-attempt-001/run/attempt.json",
        "dfa6f7ff6fc937a05a95b397755e9d818cb1762e2ee4af4df211a9ddbca65541",
    ),
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "diagnostics/ov-06-gpu-calibration-attempt-002/run/attempt.json",
        "84c77a2f7eae277bc62d5731cdb2e320697d328c3ebdccbf40a8389f25f51677",
    ),
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "diagnostics/ov-06-gpu-calibration-attempt-003/run/attempt.json",
        "73404a70f1dece6e8b0e93b145d6381791ef4f861b8a88c96c15bc01b75f9e49",
    ),
)
OV06_INITIAL_WORKER_SPEC_SHA256 = (
    "1eb0aa9bf7366c7fdbe83183a65fd8228ce072caa81806230ebd13a6de232455"
)
OV06_CORRECTED_WORKER_SPEC_SHA256 = (
    "f745ba4b4bfffb16eb68165adfe8c6cf19f0f58088770e28c49681edb108e5ee"
)
QUALITY_PROMPT_SET = (
    ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "prompts"
    / "fixed-feasibility-prompt-set-v1.json"
)
QUALITY_RENDERED_PROMPTS = QUALITY_PROMPT_SET.parent / "rendered"

MEASUREMENT_SCHEMA = "official-openvino-wb04-measurement-summary/v1"
RELEASE_INPUT_SCHEMA = "official-openvino-wb04-release-input/v1"
TARGET_WORKBOOK_VERSION = "1.7"
TARGET_REVISION_ID = "WR-035"
RESOURCE_ENVELOPE_SCHEMA = (
    "official-openvino-wb04-resource-envelope-decision/v1"
)
TERMINAL_CLASSIFICATION_SCHEMA = (
    "official-openvino-wb04-terminal-classification/v1"
)
EXPECTED_REJECTION_CLASSIFICATION_SCHEMA = (
    "official-openvino-wb04-expected-rejection-classification/v1"
)
QUALITY_TERMINAL_CLASSIFICATION_SCHEMA = (
    "official-openvino-wb04-quality-terminal-classification/v1"
)
QUALITY_SCORE_EVIDENCE_SCHEMA = (
    "official-openvino-wb04-quality-score-evidence/v1"
)
STATIC_SECTION_EVIDENCE_SCHEMA = (
    "official-openvino-wb04-static-section-evidence/v1"
)
CONTROLLED_INVENTORY_SCHEMA = (
    "official-openvino-wb04-artifact-spec-inventory/v1"
)
EXPECTED_REJECTION_LITERAL = "not-produced-by-expected-rejection"
RESOURCE_BLOCKED_LITERAL = "not-produced-by-resource-blocked"
RESOURCE_ENVELOPE_LITERAL = "not-produced-by-resource-envelope"
QUALITY_RESOURCE_BLOCKED_LITERAL = (
    "not-produced-by-quality-resource-blocked"
)
QUALITY_RESOURCE_ENVELOPE_LITERAL = (
    "not-produced-by-quality-resource-envelope"
)

PERFORMANCE_METRICS = (
    "load_ms",
    "ttft_ms",
    "prompt_tps",
    "tpot_ms",
    "decode_tps",
    "generation_duration_ms",
)
MEMORY_METRICS = (
    "peak_working_set_mb",
    "peak_private_mb",
    "available_ram_min_mb",
    "kv_mb",
    "gpu_memory_peak_mb",
)
REQUIRED_SCALAR_METRICS = PERFORMANCE_METRICS + MEMORY_METRICS
UTILISATION_METRICS = ("cpu_percent", "gpu_percent")
PROMPT_IDS = ("P1", "P2", "P3", "P4", "P5", "P6")
_CACHE_PRECISION_BITS = {
    "u3": 3,
    "u4": 4,
    "u8": 8,
    "f16": 16,
    "bf16": 16,
}

_SHA256 = re.compile(r"^[0-9a-f]{64}$")
_PLACEHOLDERS = frozenset({"N/A", "NA", "TBD", "TODO", "TBC", "-", "—"})
_TERMINAL_OUTCOME_BY_STATUS = {
    "host-resource-blocked": RESOURCE_BLOCKED_LITERAL,
    "device-resource-blocked": RESOURCE_BLOCKED_LITERAL,
    "host-resource-blocked-not-launched": RESOURCE_ENVELOPE_LITERAL,
    "suitable-host-required-not-launched": (
        "not-produced-by-suitable-host-requirement"
    ),
    "controlled-terminal-not-launched": "not-produced-by-controlled-terminal",
}
_QUALITY_TERMINAL_OUTCOME_BY_STATUS = {
    **_TERMINAL_OUTCOME_BY_STATUS,
    "quality-host-resource-blocked": QUALITY_RESOURCE_BLOCKED_LITERAL,
    "quality-host-resource-blocked-not-launched": (
        QUALITY_RESOURCE_ENVELOPE_LITERAL
    ),
}
_TERMINAL_FIELDS = frozenset(
    {
        "test_id",
        "context_tokens",
        "accepted",
        "status",
        "reason",
        "sample_count",
        "cleanup_process_count",
        "metric_outcome",
        "evidence_path",
        "evidence_sha256",
    }
)
_EXPECTED_REJECTION_FIELDS = _TERMINAL_FIELDS | {"generation_not_launched"}
_QUALITY_TERMINAL_FIELDS = frozenset(
    {
        "test_id",
        "context_tokens",
        "status",
        "reason",
        "prompt_count",
        "score_outcome",
        "evidence_path",
        "evidence_sha256",
    }
)
_QUALITY_RECORD_FIELDS = frozenset(
    {
        "test_id",
        "context_tokens",
        "status",
        "prompt_count",
        "campaign_identity_sha256",
        "runtime_summary_sha256",
        "runtime_config_sha256",
        "prompts",
        "mean_score",
        "critical_failure_or_cap",
        "evidence_path",
        "evidence_sha256",
    }
)


@dataclass(frozen=True, order=True)
class RuntimeKey:
    test_id: str
    context_tokens: int


@dataclass(frozen=True, order=True)
class SampleKey:
    test_id: str
    context_tokens: int
    sample_id: str


@dataclass(frozen=True)
class SampleMeasurement:
    key: SampleKey
    metrics: Mapping[str, float]
    cpu_percent: Mapping[str, Any]
    gpu_percent: Mapping[str, Any]
    activation: Mapping[str, Any]
    output_sha256: str
    evidence_path: Path
    evidence_sha256: str
    canonical: Mapping[str, Any]


@dataclass(frozen=True)
class RuntimeOutcome:
    key: RuntimeKey
    status: str
    configuration_id: str
    accepted: bool
    sample_count: int
    cleanup_process_count: int
    metric_outcome: str
    reason: str
    evidence_path: Path
    evidence_sha256: str
    summary_path: Path | None
    summary_sha256: str
    campaign_identity_sha256: str
    runtime_config_sha256: str
    metrics: Mapping[str, Any]
    activation: Mapping[str, Any]
    samples: Mapping[SampleKey, SampleMeasurement]


@dataclass(frozen=True)
class QualityOutcome:
    key: RuntimeKey
    status: str
    prompt_scores: Mapping[str, float | str]
    mean_score: float | str
    critical_failure_or_cap: str
    evidence_path: Path
    evidence_sha256: str


def _read_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ValueError(f"cannot read JSON evidence {path}: {exc}") from exc
    if type(value) is not dict:
        raise ValueError(f"JSON evidence must be an object: {path}")
    return value


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as handle:
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
    except OSError as exc:
        raise ValueError(f"cannot hash evidence {path}: {exc}") from exc
    return digest.hexdigest()


def _canonical_sha256(value: Mapping[str, Any]) -> str:
    encoded = (
        json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        )
        + "\n"
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _runtime_json_sha256(value: Any) -> str:
    """Match the frozen runtime campaign's canonical JSON digest."""

    encoded = json.dumps(
        value,
        ensure_ascii=True,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _require_sha256(value: Any, field: str) -> str:
    if type(value) is not str or _SHA256.fullmatch(value) is None:
        raise ValueError(f"{field} must be a lowercase SHA-256")
    return value


def _resolve_path(value: Any, field: str) -> Path:
    if type(value) is not str or not value.strip():
        raise ValueError(f"{field} must be a non-empty evidence path")
    path = Path(value)
    resolved = (path if path.is_absolute() else ROOT / path).resolve()
    try:
        resolved.relative_to(ROOT.resolve())
    except ValueError as exc:
        raise ValueError(f"{field} must remain inside the repository") from exc
    return resolved


def _verify_evidence(path_value: Any, sha_value: Any, label: str) -> tuple[Path, str]:
    path = _resolve_path(path_value, f"{label} path")
    expected = _require_sha256(sha_value, f"{label} SHA-256")
    actual = _sha256_file(path)
    if actual != expected:
        raise ValueError(f"{label} SHA-256 mismatch")
    return path, actual


def _validate_classification_evidence(
    path: Path,
    *,
    schema: str,
    binding: Mapping[str, Any],
    exact_fields: set[str],
    label: str,
) -> dict[str, Any]:
    evidence = _read_json(path)
    if set(evidence) != exact_fields or evidence.get("schema") != schema:
        raise ValueError(f"{label} does not use the controlled classification schema")
    if any(evidence.get(field) != value for field, value in binding.items()):
        raise ValueError(f"{label} does not bind the classified test/context")
    return evidence


def _validate_source_evidence_list(value: Any, label: str) -> None:
    if not isinstance(value, list) or not value:
        raise ValueError(f"{label} must cite at least one source artifact")
    for index, item in enumerate(value, 1):
        if type(item) is not dict or set(item) != {"path", "sha256"}:
            raise ValueError(f"{label} source reference {index} is invalid")
        _verify_evidence(
            item["path"], item["sha256"], f"{label} source {index}"
        )


def _nonempty_text(value: Any, field: str) -> str:
    if type(value) is not str or not value.strip():
        raise ValueError(f"{field} must be a non-empty string")
    if value.strip().upper() in _PLACEHOLDERS:
        raise ValueError(f"{field} cannot be a placeholder")
    return value.strip()


def _finite_number(value: Any, field: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{field} must be numeric")
    result = float(value)
    if not math.isfinite(result):
        raise ValueError(f"{field} must be finite")
    return result


def _same_number(actual: Any, expected: float, field: str) -> None:
    numeric = _finite_number(actual, field)
    if not math.isclose(numeric, expected, rel_tol=1e-9, abs_tol=1e-9):
        raise ValueError(
            f"{field} aggregate mismatch: {numeric!r} != {expected!r}"
        )


def _case_map(cases: Sequence[Mapping[str, Any]]) -> dict[str, Mapping[str, Any]]:
    result: dict[str, Mapping[str, Any]] = {}
    for case in cases:
        if not isinstance(case, Mapping):
            raise ValueError("matrix cases must be objects")
        test_id = _nonempty_text(case.get("test_id"), "matrix test_id")
        if test_id in result:
            raise ValueError(f"duplicate matrix test_id: {test_id}")
        result[test_id] = case
    return result


def _runtime_case_map(
    cases: Sequence[Mapping[str, Any]],
) -> dict[RuntimeKey, Mapping[str, Any]]:
    result: dict[RuntimeKey, Mapping[str, Any]] = {}
    for case in cases:
        contexts = case.get("contexts")
        if case.get("phase") not in {"baseline", "formal"}:
            continue
        if not isinstance(contexts, list) or not contexts:
            raise ValueError(f"{case.get('test_id')} has no runtime contexts")
        for context in contexts:
            if type(context) is not int or context <= 0:
                raise ValueError(f"{case.get('test_id')} has invalid context")
            key = RuntimeKey(str(case["test_id"]), context)
            if key in result:
                raise ValueError(f"duplicate matrix runtime key: {key}")
            result[key] = case
    return result


def _aggregate(values: Sequence[float]) -> dict[str, float | int]:
    return {
        "count": len(values),
        "max": max(values),
        "mean": sum(values) / len(values),
        "median": float(statistics.median(values)),
        "min": min(values),
    }


def _validate_scalar_aggregate(
    value: Any, expected_values: Sequence[float], field: str
) -> dict[str, Any]:
    if type(value) is not dict:
        raise ValueError(f"{field} must be an aggregate object")
    required = {"count", "min", "max", "mean", "median"}
    if not required.issubset(value):
        raise ValueError(f"{field} is missing aggregate statistics")
    expected = _aggregate(expected_values)
    if value["count"] != expected["count"]:
        raise ValueError(f"{field} count does not match source samples")
    for statistic in ("min", "max", "mean", "median"):
        _same_number(
            value[statistic],
            float(expected[statistic]),
            f"{field}.{statistic}",
        )
    return dict(value)


def _validate_utilisation_aggregate(
    value: Any, expected_values: Sequence[float], field: str
) -> dict[str, Any]:
    if type(value) is not dict:
        raise ValueError(f"{field} must be an aggregate object")
    required = {"count", "mean", "median", "peak", "query_succeeded", "values"}
    if not required.issubset(value):
        raise ValueError(f"{field} is missing utilization statistics")
    if value["query_succeeded"] is not True:
        raise ValueError(f"{field} query did not succeed")
    if not isinstance(value["values"], list) or len(value["values"]) != len(
        expected_values
    ):
        raise ValueError(f"{field} raw values do not match source samples")
    observed_values = [
        _finite_number(item, f"{field}.values") for item in value["values"]
    ]
    for observed, expected in zip(observed_values, expected_values):
        if not math.isclose(observed, expected, rel_tol=0, abs_tol=1e-9):
            raise ValueError(f"{field} raw values do not match source samples")
    expected = {
        "count": len(expected_values),
        "mean": sum(expected_values) / len(expected_values),
        "median": float(statistics.median(expected_values)),
        "peak": max(expected_values),
    }
    if value["count"] != expected["count"]:
        raise ValueError(f"{field} count does not match source samples")
    for statistic in ("mean", "median", "peak"):
        _same_number(
            value[statistic],
            float(expected[statistic]),
            f"{field}.{statistic}",
        )
    return dict(value)


def _validate_attempt_activation(
    activation: Any, case: Mapping[str, Any], sample_id: str
) -> dict[str, Any]:
    if type(activation) is not dict:
        raise ValueError(f"{sample_id} has no runtime activation proof")
    expected = {
        "requested_key_algorithm": case.get("runtime_key_algorithm"),
        "requested_value_algorithm": case.get("runtime_value_algorithm"),
        "activated_key_algorithm": case.get("runtime_key_algorithm"),
        "activated_value_algorithm": case.get("runtime_value_algorithm"),
        "requested_key_cache_precision": case.get("key_cache_precision"),
        "requested_value_cache_precision": case.get("value_cache_precision"),
        "activated_key_cache_precision": case.get("key_cache_precision"),
        "activated_value_cache_precision": case.get("value_cache_precision"),
        "actual_device": case.get("requested_device"),
        "norm_correction": case.get("norm_correction"),
    }
    if any(activation.get(field) != value for field, value in expected.items()):
        raise ValueError(
            f"{sample_id} runtime-proven cache precision/activation mismatch"
        )
    if activation.get("status") != "activated" or activation.get("fallback") is not False:
        raise ValueError(f"{sample_id} activation or fallback proof is invalid")
    actual_bytes = activation.get("actual_bytes")
    if type(actual_bytes) is not int or actual_bytes <= 0:
        raise ValueError(f"{sample_id} actual KV byte proof is invalid")
    return dict(activation)


def _load_source_sample(
    source: Any,
    *,
    key: RuntimeKey,
    case: Mapping[str, Any],
) -> SampleMeasurement:
    if type(source) is not dict or set(source) != {"sample_id", "path", "sha256"}:
        raise ValueError(f"{key} source record fields are invalid")
    sample_id = source.get("sample_id")
    if sample_id not in {"sample-1", "sample-2", "sample-3"}:
        raise ValueError(f"{key} has an invalid sample ID")
    path, sha256 = _verify_evidence(
        source.get("path"), source.get("sha256"), f"{key}/{sample_id}"
    )
    attempt = _read_json(path)
    if (
        attempt.get("schema") != "official-openvino-wb04-governed-run/v1"
        or attempt.get("role") != sample_id
        or attempt.get("valid") is not True
        or attempt.get("validation_errors") != []
        or attempt.get("exit_code") != 0
        or attempt.get("sampler_exit_code") != 0
        or attempt.get("timed_out") is not False
        or attempt.get("low_memory_stop") is not False
        or attempt.get("cleanup_process_count") != 0
        or type(attempt.get("memory_sample_count")) is not int
        or attempt["memory_sample_count"] <= 0
    ):
        raise ValueError(f"{key}/{sample_id} is not an accepted source attempt")
    worker = attempt.get("worker")
    if type(worker) is not dict:
        raise ValueError(f"{key}/{sample_id} worker result is missing")
    if (
        worker.get("controlled_test_id") != key.test_id
        or worker.get("context") != key.context_tokens
        or worker.get("device") != case.get("requested_device")
    ):
        raise ValueError(f"{key}/{sample_id} worker identity mismatch")
    if worker.get("output_valid") is not True:
        raise ValueError(f"{key}/{sample_id} output validity was not proved")
    output = worker.get("output")
    if type(output) is not str or not output:
        raise ValueError(f"{key}/{sample_id} output is missing")
    output_sha256 = _require_sha256(
        attempt.get("output_sha256"), f"{key}/{sample_id}.output_sha256"
    )
    if hashlib.sha256(output.encode("utf-8")).hexdigest() != output_sha256:
        raise ValueError(f"{key}/{sample_id} output SHA-256 mismatch")
    activation = attempt.get("activation")
    telemetry_sha256 = _require_sha256(
        attempt.get("telemetry_sha256"),
        f"{key}/{sample_id}.telemetry_sha256",
    )
    if _runtime_json_sha256(activation) != telemetry_sha256:
        raise ValueError(f"{key}/{sample_id} telemetry SHA-256 mismatch")
    for filename, field in (
        ("stdout.txt", "stdout_sha256"),
        ("stderr.txt", "stderr_sha256"),
    ):
        adjacent = path.parent / filename
        expected = _require_sha256(
            attempt.get(field), f"{key}/{sample_id}.{field}"
        )
        if _sha256_file(adjacent) != expected:
            raise ValueError(f"{key}/{sample_id} {filename} SHA-256 mismatch")
    try:
        validate_runtime_record_against_matrix_case(attempt, case)
        canonical = measurement_sample(dict(attempt), path)
    except (KeyError, TypeError, ValueError) as exc:
        raise ValueError(
            f"{key}/{sample_id} canonical runtime evidence is invalid: {exc}"
        ) from exc
    if (
        canonical.get("sample_id") != sample_id
        or canonical.get("source_sha256") != sha256
        or Path(canonical.get("source", "")).resolve() != path.resolve()
    ):
        raise ValueError(f"{key}/{sample_id} canonical source identity mismatch")
    metrics = {
        metric: _finite_number(
            canonical.get(metric), f"{key}/{sample_id}.{metric}"
        )
        for metric in REQUIRED_SCALAR_METRICS
    }
    sample_key = SampleKey(key.test_id, key.context_tokens, sample_id)
    return SampleMeasurement(
        key=sample_key,
        metrics=metrics,
        cpu_percent=dict(canonical["cpu_percent"]),
        gpu_percent=dict(canonical["gpu_percent"]),
        activation=dict(canonical["activation"]),
        output_sha256=output_sha256,
        evidence_path=path,
        evidence_sha256=sha256,
        canonical=canonical,
    )


def _resolve_campaign_path(root: Path, value: Any, field: str) -> Path:
    if type(value) is not str or not value.strip():
        raise ValueError(f"{field} must be a non-empty path")
    candidate = Path(value)
    resolved = (candidate if candidate.is_absolute() else root / candidate).resolve()
    try:
        resolved.relative_to(root.resolve())
        resolved.relative_to(ROOT.resolve())
    except ValueError as exc:
        raise ValueError(f"{field} escapes the governed campaign root") from exc
    return resolved


def _validate_campaign_chain(
    *,
    summary_path: Path,
    summary: Mapping[str, Any],
    summary_sha256: str,
    key: RuntimeKey,
    case: Mapping[str, Any],
    matrix_sha256: str | None,
) -> None:
    campaign_root = summary_path.parent.resolve()
    identity_path = campaign_root / "campaign-identity.json"
    identity_record = _read_json(identity_path)
    if (
        set(identity_record)
        != {"schema", "identity", "campaign_identity_sha256"}
        or identity_record.get("schema")
        != "official-openvino-wb04-campaign-identity/v1"
        or type(identity_record.get("identity")) is not dict
    ):
        raise ValueError(f"{key} campaign identity schema is invalid")
    identity = identity_record["identity"]
    campaign_sha256 = _require_sha256(
        identity_record.get("campaign_identity_sha256"),
        f"{key}.campaign_identity_sha256",
    )
    if (
        _runtime_json_sha256(identity) != campaign_sha256
        or summary.get("campaign_identity_sha256") != campaign_sha256
        or identity.get("context") != key.context_tokens
    ):
        raise ValueError(f"{key} campaign identity does not bind the summary")
    matrix_identity = identity.get("matrix")
    if (
        type(matrix_identity) is not dict
        or matrix_identity.get("case") != dict(case)
    ):
        raise ValueError(f"{key} campaign matrix case does not match")
    if (
        matrix_sha256 is not None
        and matrix_identity.get("file_sha256") != matrix_sha256
    ):
        raise ValueError(f"{key} campaign matrix SHA-256 does not match")
    config = identity.get("config")
    config_fields = {
        "device",
        "max_new_tokens",
        "expected_input_tokens",
        "ignore_eos",
        "seed",
        "apply_chat_template",
        "properties",
    }
    if (
        type(config) is not dict
        or set(config) != config_fields
        or summary.get("runtime_config_sha256")
        != _runtime_json_sha256(config)
    ):
        raise ValueError(f"{key} runtime config identity does not match")

    sequence_path = campaign_root / "attempt-sequence.json"
    sequence = _read_json(sequence_path)
    sequence_fields = {
        "schema",
        "campaign_identity_sha256",
        "pilot_passed",
        "warmup_excluded",
        "pilot",
        "warmup",
        "accepted_samples",
        "accepted_sample_count",
        "cleanup_process_count",
        "measurement_summary_path",
        "measurement_summary_sha256",
    }
    if (
        set(sequence) != sequence_fields
        or sequence.get("schema")
        != "official-openvino-wb04-attempt-sequence/v1"
        or sequence.get("campaign_identity_sha256") != campaign_sha256
        or sequence.get("pilot_passed") is not True
        or sequence.get("warmup_excluded") is not True
        or sequence.get("accepted_sample_count") != 3
        or sequence.get("cleanup_process_count") != 0
        or sequence.get("measurement_summary_sha256") != summary_sha256
        or _resolve_campaign_path(
            campaign_root,
            sequence.get("measurement_summary_path"),
            f"{key} sequence summary path",
        )
        != summary_path.resolve()
    ):
        raise ValueError(f"{key} attempt sequence does not bind the summary")
    accepted = sequence.get("accepted_samples")
    if not isinstance(accepted, list) or len(accepted) != 3:
        raise ValueError(f"{key} attempt sequence has no three accepted samples")
    receipts = [
        ("pilot", sequence.get("pilot")),
        ("warmup", sequence.get("warmup")),
        *[
            (f"sample-{number}", receipt)
            for number, receipt in enumerate(accepted, 1)
        ],
    ]
    receipt_fields = {
        "schema",
        "role",
        "attempt_number",
        "campaign_identity_sha256",
        "spec_sha256",
        "spec_path",
        "spec_file_sha256",
        "runtime_record_path",
        "runtime_record_sha256",
        "accepted",
    }
    summary_sources = {
        source["sample_id"]: source
        for source in summary.get("sources", [])
        if type(source) is dict and "sample_id" in source
    }
    for expected_role, receipt in receipts:
        if (
            type(receipt) is not dict
            or set(receipt) != receipt_fields
            or receipt.get("schema")
            != "official-openvino-wb04-sequence-receipt/v1"
            or receipt.get("role") != expected_role
            or receipt.get("accepted") is not True
            or receipt.get("campaign_identity_sha256") != campaign_sha256
            or type(receipt.get("attempt_number")) is not int
            or receipt["attempt_number"] <= 0
        ):
            raise ValueError(f"{key}/{expected_role} sequence receipt is invalid")
        spec_path = _resolve_campaign_path(
            campaign_root,
            receipt.get("spec_path"),
            f"{key}/{expected_role} spec path",
        )
        spec_file_sha256 = _require_sha256(
            receipt.get("spec_file_sha256"),
            f"{key}/{expected_role} spec file SHA-256",
        )
        spec_sha256 = _require_sha256(
            receipt.get("spec_sha256"), f"{key}/{expected_role} spec SHA-256"
        )
        if _sha256_file(spec_path) != spec_file_sha256:
            raise ValueError(f"{key}/{expected_role} spec file SHA-256 mismatch")
        spec = _read_json(spec_path)
        spec_config = {
            field: spec.get(field)
            for field in config_fields
        }
        prompt_identity = identity.get("prompt")
        model_identity = identity.get("model")
        if (
            _runtime_json_sha256(spec) != spec_sha256
            or spec.get("schema") != "official-openvino-wb04-worker-spec/v1"
            or spec.get("controlled_test_id") != key.test_id
            or spec.get("context") != key.context_tokens
            or spec.get("role") != expected_role
            or spec.get("campaign_identity_sha256") != campaign_sha256
            or spec_config != config
            or type(prompt_identity) is not dict
            or prompt_identity.get("sha256")
            != hashlib.sha256(
                str(spec.get("prompt", "")).encode("utf-8")
            ).hexdigest()
            or prompt_identity.get("utf8_bytes")
            != len(str(spec.get("prompt", "")).encode("utf-8"))
            or type(model_identity) is not dict
            or type(model_identity.get("validated_artifact")) is not dict
            or Path(str(spec.get("model_path", ""))).resolve()
            != Path(
                str(model_identity["validated_artifact"].get("artifact_root", ""))
            ).resolve()
        ):
            raise ValueError(f"{key}/{expected_role} spec does not bind the campaign")
        try:
            validate_worker_spec_against_matrix_case(spec, case)
        except ValueError as exc:
            raise ValueError(
                f"{key}/{expected_role} spec contradicts matrix: {exc}"
            ) from exc
        runtime_path = _resolve_campaign_path(
            campaign_root,
            receipt.get("runtime_record_path"),
            f"{key}/{expected_role} runtime record path",
        )
        runtime_sha256 = _require_sha256(
            receipt.get("runtime_record_sha256"),
            f"{key}/{expected_role} runtime record SHA-256",
        )
        if _sha256_file(runtime_path) != runtime_sha256:
            raise ValueError(
                f"{key}/{expected_role} runtime record SHA-256 mismatch"
            )
        attempt = _read_json(runtime_path)
        if (
            attempt.get("schema") != "official-openvino-wb04-governed-run/v1"
            or attempt.get("role") != expected_role
            or attempt.get("valid") is not True
            or attempt.get("validation_errors") != []
            or attempt.get("exit_code") != 0
            or attempt.get("sampler_exit_code") != 0
            or attempt.get("timed_out") is not False
            or attempt.get("low_memory_stop") is not False
            or attempt.get("cleanup_process_count") != 0
        ):
            raise ValueError(
                f"{key}/{expected_role} governed runtime record is invalid"
            )
        try:
            validate_runtime_record_against_matrix_case(attempt, case)
            measurement_sample(attempt, runtime_path)
        except (KeyError, TypeError, ValueError) as exc:
            raise ValueError(
                f"{key}/{expected_role} runtime contradicts matrix: {exc}"
            ) from exc
        worker = attempt.get("worker")
        if (
            type(worker) is not dict
            or worker.get("output_valid") is not True
            or type(worker.get("output")) is not str
            or hashlib.sha256(worker["output"].encode("utf-8")).hexdigest()
            != attempt.get("output_sha256")
            or _runtime_json_sha256(attempt.get("activation"))
            != attempt.get("telemetry_sha256")
        ):
            raise ValueError(
                f"{key}/{expected_role} runtime output/telemetry is invalid"
            )
        for filename, hash_field in (
            ("stdout.txt", "stdout_sha256"),
            ("stderr.txt", "stderr_sha256"),
        ):
            if _sha256_file(runtime_path.parent / filename) != attempt.get(
                hash_field
            ):
                raise ValueError(
                    f"{key}/{expected_role} {filename} SHA-256 mismatch"
                )
        if expected_role.startswith("sample-"):
            source = summary_sources.get(expected_role)
            if (
                source is None
                or _resolve_path(
                    source.get("path"), f"{key}/{expected_role} summary source"
                )
                != runtime_path
                or source.get("sha256") != runtime_sha256
            ):
                raise ValueError(
                    f"{key}/{expected_role} receipt does not bind summary source"
                )


def index_measurement_summaries(
    paths: Iterable[Path],
    cases: Sequence[Mapping[str, Any]],
    *,
    matrix_sha256: str | None = None,
) -> dict[RuntimeKey, RuntimeOutcome]:
    """Validate selected measurement summaries and index by test/context.

    ``paths`` is an explicit selection.  Passing two files for the same
    composite key is an error; there is no implicit root or timestamp
    precedence.
    """

    case_by_id = _case_map(cases)
    rows: dict[RuntimeKey, RuntimeOutcome] = {}
    for raw_path in paths:
        path = Path(raw_path)
        summary = _read_json(path)
        key = RuntimeKey(
            _nonempty_text(summary.get("test_id"), "summary test_id"),
            summary.get("context_tokens"),
        )
        if type(key.context_tokens) is not int or key.context_tokens <= 0:
            raise ValueError("summary context_tokens must be a positive integer")
        if key in rows:
            raise ValueError(f"duplicate runtime key: {key}")
        case = case_by_id.get(key.test_id)
        if case is None or key.context_tokens not in case.get("contexts", []):
            raise ValueError(f"measurement summary is outside the matrix: {key}")
        if case.get("expected_outcome") != "pass":
            raise ValueError(f"expected-rejection row cannot be measured: {key}")
        if (
            summary.get("schema") != MEASUREMENT_SCHEMA
            or summary.get("schema_version") != 1
            or summary.get("accepted") is not True
            or summary.get("status") != "measured"
        ):
            raise ValueError(f"{key} is not an accepted measurement summary")
        if summary.get("sample_count") != 3:
            raise ValueError(f"{key} must have exactly three accepted samples")
        if summary.get("cleanup_process_count") != 0:
            raise ValueError(f"{key} has nonzero cleanup survivors")
        campaign_identity = _require_sha256(
            summary.get("campaign_identity_sha256"),
            f"{key}.campaign_identity_sha256",
        )
        runtime_config = _require_sha256(
            summary.get("runtime_config_sha256"),
            f"{key}.runtime_config_sha256",
        )
        sources = summary.get("sources")
        if not isinstance(sources, list) or len(sources) != 3:
            raise ValueError(f"{key} must have exactly three source records")
        samples = {
            sample.key: sample
            for sample in (
                _load_source_sample(source, key=key, case=case)
                for source in sources
            )
        }
        expected_sample_keys = {
            SampleKey(key.test_id, key.context_tokens, f"sample-{number}")
            for number in (1, 2, 3)
        }
        if set(samples) != expected_sample_keys:
            raise ValueError(f"{key} source samples are not unique sample-1..3")
        ordered = [samples[item] for item in sorted(samples)]
        for metric in REQUIRED_SCALAR_METRICS:
            _validate_scalar_aggregate(
                summary.get(metric),
                [sample.metrics[metric] for sample in ordered],
                f"{key}.{metric}",
            )
        for field in UTILISATION_METRICS:
            all_values = [
                _finite_number(item, f"{key}.{field}.source")
                for sample in ordered
                for item in getattr(sample, field)["values"]
            ]
            _validate_utilisation_aggregate(
                summary.get(field), all_values, f"{key}.{field}"
            )
        try:
            recomputed = summarize_samples(
                [sample.canonical for sample in ordered]
            )
        except ValueError as exc:
            raise ValueError(
                f"{key} canonical sample reconciliation failed: {exc}"
            ) from exc
        recomputed.update(
            {
                "accepted": True,
                "cleanup_process_count": 0,
                "test_id": key.test_id,
                "context_tokens": key.context_tokens,
                "campaign_identity_sha256": campaign_identity,
                "runtime_config_sha256": runtime_config,
            }
        )
        recomputed = json.loads(
            json.dumps(recomputed, allow_nan=False)
        )
        if summary != recomputed:
            raise ValueError(
                f"{key} measurement summary does not exactly match "
                "the canonical source-sample recomputation"
            )
        metrics = {
            metric: dict(recomputed[metric])
            for metric in REQUIRED_SCALAR_METRICS + UTILISATION_METRICS
        }
        activation = recomputed["activation"]
        summary_sha256 = _sha256_file(path)
        _validate_campaign_chain(
            summary_path=path,
            summary=summary,
            summary_sha256=summary_sha256,
            key=key,
            case=case,
            matrix_sha256=matrix_sha256,
        )
        rows[key] = RuntimeOutcome(
            key=key,
            status="measured",
            configuration_id=(
                f"{key.test_id}-ctx{key.context_tokens}-{runtime_config[:12]}"
            ),
            accepted=True,
            sample_count=3,
            cleanup_process_count=0,
            metric_outcome="measured",
            reason="three accepted governed samples",
            evidence_path=path,
            evidence_sha256=summary_sha256,
            summary_path=path,
            summary_sha256=summary_sha256,
            campaign_identity_sha256=campaign_identity,
            runtime_config_sha256=runtime_config,
            metrics=metrics,
            activation=dict(activation),
            samples=samples,
        )
    return rows


def _validate_direct_host_resource_terminal(
    classification: Mapping[str, Any],
    case: Mapping[str, Any],
    key: RuntimeKey,
) -> None:
    """Prove a directly executed row stopped at the fixed host-RAM floor."""

    sources = classification.get("source_evidence")
    if not isinstance(sources, list):
        raise ValueError(
            "direct host-resource provenance requires source evidence"
        )
    source_bindings = {
        (
            _resolve_path(
                source.get("path"),
                "direct host-resource provenance source",
            ),
            _require_sha256(
                source.get("sha256"),
                "direct host-resource provenance source SHA-256",
            ),
        )
        for source in sources
        if type(source) is dict
        and set(source) == {"path", "sha256"}
    }
    attempt_sources = [
        source
        for source in sources
        if type(source) is dict
        and set(source) == {"path", "sha256"}
        and Path(str(source.get("path"))).name == "attempt.json"
    ]
    if len(attempt_sources) != 3:
        raise ValueError(
            "direct host-resource terminal requires exactly three governed attempts"
        )
    if len(sources) != 10 or len(source_bindings) != 10:
        raise ValueError(
            "direct host-resource provenance requires three attempt, three "
            "spec, three receipt, and one campaign source"
        )
    attempt_paths: set[Path] = set()
    attempt_hashes: set[str] = set()
    attempt_numbers: set[int] = set()
    campaign_roots: set[Path] = set()
    campaign_hashes: set[str] = set()
    required_provenance: set[tuple[Path, str]] = set()
    for source_number, source in enumerate(attempt_sources, 1):
        if type(source) is not dict or set(source) != {"path", "sha256"}:
            raise ValueError(
                "direct host-resource source reference is invalid"
            )
        attempt_path, attempt_sha256 = _verify_evidence(
            source["path"],
            source["sha256"],
            f"direct host-resource attempt {source_number}",
        )
        if (
            attempt_path.name != "attempt.json"
            or attempt_path.parent.name != "run"
            or attempt_path.parent.parent.parent.name != "pilot"
            or attempt_path.parent.parent.parent.parent.name != "attempts"
        ):
            raise ValueError(
                "direct host-resource source is not a governed pilot attempt"
            )
        if attempt_path in attempt_paths or attempt_sha256 in attempt_hashes:
            raise ValueError(
                "direct host-resource attempts must be path/hash distinct"
            )
        attempt_paths.add(attempt_path)
        attempt_hashes.add(attempt_sha256)
        required_provenance.add((attempt_path, attempt_sha256))
        attempt = _read_json(attempt_path)
        available_ram = attempt.get("available_ram_bytes")
        floor = attempt.get("minimum_available_ram_bytes")
        minimum = (
            available_ram.get("minimum")
            if type(available_ram) is dict
            else None
        )
        sampler_job = attempt.get("sampler_job")
        workload_job = attempt.get("workload_job")
        if (
            attempt.get("schema")
            != "official-openvino-wb04-governed-run/v1"
            or attempt.get("role") != "pilot"
            or attempt.get("valid") is not False
            or not isinstance(attempt.get("validation_errors"), list)
            or not attempt["validation_errors"]
            or attempt.get("exit_code") != 137
            or attempt.get("sampler_exit_code") != 0
            or attempt.get("timed_out") is not False
            or attempt.get("low_memory_stop") is not True
            or attempt.get("cleanup_process_count") != 0
            or type(attempt.get("memory_sample_count")) is not int
            or attempt["memory_sample_count"] <= 0
            or floor != 2048 * 1024 * 1024
            or type(minimum) is not int
            or minimum < 0
            or minimum > floor
            or attempt.get("activation") is not None
            or attempt.get("worker") is not None
            or attempt.get("output_sha256") is not None
            or attempt.get("telemetry_sha256") is not None
            or type(sampler_job) is not dict
            or sampler_job.get("setup_ok") is not True
            or sampler_job.get("query_ok") is not True
            or sampler_job.get("survivor_pids_after_cleanup") != []
            or type(workload_job) is not dict
            or workload_job.get("setup_ok") is not True
            or workload_job.get("query_ok") is not True
            or workload_job.get("survivor_pids_after_cleanup") != []
        ):
            raise ValueError(
                "direct host-resource attempt does not prove a clean "
                "fixed-floor stop"
            )

        attempt_root = attempt_path.parent.parent
        campaign_root = attempt_path.parents[4]
        campaign_roots.add(campaign_root)
        spec_path = attempt_root / "spec.json"
        command = attempt.get("command")
        if not isinstance(command, list) or "--spec" not in command:
            raise ValueError(
                "direct host-resource spec command is invalid"
            )
        spec_index = command.index("--spec") + 1
        if (
            spec_index >= len(command)
            or _resolve_path(
                command[spec_index],
                "direct host-resource command spec path",
            )
            != spec_path.resolve()
        ):
            raise ValueError(
                "direct host-resource spec command is unbound"
            )
        spec = _read_json(spec_path)
        required_provenance.add((spec_path.resolve(), _sha256_file(spec_path)))
        campaign_sha256 = _require_sha256(
            spec.get("campaign_identity_sha256"),
            f"direct host-resource attempt {source_number} campaign identity",
        )
        if (
            spec.get("schema") != "official-openvino-wb04-worker-spec/v1"
            or spec.get("controlled_test_id") != key.test_id
            or spec.get("context") != key.context_tokens
            or spec.get("role") != "pilot"
        ):
            raise ValueError(
                "direct host-resource spec does not bind test/context"
            )
        try:
            validate_worker_spec_against_matrix_case(spec, case)
        except ValueError as exc:
            raise ValueError(
                f"direct host-resource spec contradicts matrix: {exc}"
            ) from exc
        campaign_hashes.add(campaign_sha256)

        receipt_path = attempt_root / "sequence-receipt.json"
        receipt = _read_json(receipt_path)
        required_provenance.add(
            (receipt_path.resolve(), _sha256_file(receipt_path))
        )
        receipt_fields = {
            "schema",
            "role",
            "attempt_number",
            "campaign_identity_sha256",
            "spec_sha256",
            "spec_path",
            "spec_file_sha256",
            "runtime_record_path",
            "runtime_record_sha256",
            "accepted",
            "controller_error",
        }
        attempt_number = receipt.get("attempt_number")
        if (
            set(receipt) != receipt_fields
            or receipt.get("schema")
            != "official-openvino-wb04-sequence-receipt/v1"
            or receipt.get("role") != "pilot"
            or receipt.get("accepted") is not False
            or type(receipt.get("controller_error")) is not str
            or not receipt["controller_error"].strip()
            or type(attempt_number) is not int
            or attempt_number <= 0
            or receipt.get("campaign_identity_sha256") != campaign_sha256
            or receipt.get("runtime_record_sha256") != attempt_sha256
            or receipt.get("spec_file_sha256") != _sha256_file(spec_path)
            or receipt.get("spec_sha256") != _runtime_json_sha256(spec)
            or _resolve_campaign_path(
                campaign_root,
                receipt.get("runtime_record_path"),
                "direct host-resource receipt runtime path",
            )
            != attempt_path
            or _resolve_campaign_path(
                campaign_root,
                receipt.get("spec_path"),
                "direct host-resource receipt spec path",
            )
            != spec_path
        ):
            raise ValueError(
                "direct host-resource sequence receipt is invalid"
            )
        attempt_numbers.add(attempt_number)

    if (
        attempt_numbers != {1, 2, 3}
        or len(campaign_roots) != 1
        or len(campaign_hashes) != 1
    ):
        raise ValueError(
            "direct host-resource attempts must be one campaign sequence 1..3"
        )
    campaign_root = next(iter(campaign_roots))
    campaign_sha256 = next(iter(campaign_hashes))
    identity_path = campaign_root / "campaign-identity.json"
    identity_record = _read_json(identity_path)
    required_provenance.add(
        (identity_path.resolve(), _sha256_file(identity_path))
    )
    if source_bindings != required_provenance:
        raise ValueError(
            "direct host-resource provenance does not bind the exact spec, "
            "receipt, and campaign identity bytes"
        )
    if (
        set(identity_record)
        != {"schema", "identity", "campaign_identity_sha256"}
        or identity_record.get("schema")
        != "official-openvino-wb04-campaign-identity/v1"
        or type(identity_record.get("identity")) is not dict
        or identity_record.get("campaign_identity_sha256") != campaign_sha256
        or _runtime_json_sha256(identity_record["identity"])
        != campaign_sha256
    ):
        raise ValueError(
            "direct host-resource campaign identity is invalid"
        )
    identity = identity_record["identity"]
    matrix_identity = identity.get("matrix")
    config = identity.get("config")
    config_fields = {
        "device",
        "max_new_tokens",
        "expected_input_tokens",
        "ignore_eos",
        "seed",
        "apply_chat_template",
        "properties",
    }
    if (
        identity.get("context") != key.context_tokens
        or type(matrix_identity) is not dict
        or matrix_identity.get("case") != dict(case)
        or matrix_identity.get("file_sha256") != CANONICAL_MATRIX_SHA256
        or type(config) is not dict
        or set(config) != config_fields
        or config.get("expected_input_tokens") != key.context_tokens
    ):
        raise ValueError(
            "direct host-resource campaign context token config does not bind "
            "the frozen matrix"
        )
    prompt_identity = identity.get("prompt")
    model_identity = identity.get("model")
    for attempt_path in attempt_paths:
        spec = _read_json(attempt_path.parent.parent / "spec.json")
        spec_config = {field: spec.get(field) for field in config_fields}
        if (
            spec_config != config
            or type(prompt_identity) is not dict
            or prompt_identity.get("sha256")
            != hashlib.sha256(
                str(spec.get("prompt", "")).encode("utf-8")
            ).hexdigest()
            or prompt_identity.get("utf8_bytes")
            != len(str(spec.get("prompt", "")).encode("utf-8"))
            or type(model_identity) is not dict
            or type(model_identity.get("validated_artifact")) is not dict
            or Path(str(spec.get("model_path", ""))).resolve()
            != Path(
                str(model_identity["validated_artifact"].get(
                    "artifact_root", ""
                ))
            ).resolve()
        ):
            raise ValueError(
                "direct host-resource spec does not bind campaign inputs"
            )


def _repo_relative(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT.resolve()).as_posix()
    except ValueError as exc:
        raise ValueError("inventory evidence escapes the repository") from exc


def _sealed_inventory_entries(
    roots: Sequence[Path],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    if len(roots) != 2:
        raise ValueError("controlled inventory must have two sealed roots")
    artifacts_root, specs_root = (Path(root).resolve() for root in roots)
    if not artifacts_root.is_dir() or not specs_root.is_dir():
        raise ValueError("controlled inventory sealed roots are missing")
    artifact_entries: list[dict[str, Any]] = []
    for path in sorted(artifacts_root.rglob("artifact-manifest.json")):
        manifest = _read_json(path)
        model = manifest.get("model")
        if (
            manifest.get("schema_version") != 1
            or manifest.get("status") not in {"load-proven", "validated"}
            or type(model) is not dict
            or model.get("family") != "granite-4.1"
            or model.get("parameter_scale") != "3b"
            or model.get("precision") not in {"u4", "u8", "f16"}
        ):
            raise ValueError(
                "controlled inventory artifact manifest is invalid"
            )
        artifact_entries.append(
            {
                "path": _repo_relative(path),
                "sha256": _sha256_file(path),
                "model": "granite-3b",
                "weight_precision": model["precision"],
            }
        )
    spec_entries: list[dict[str, Any]] = []
    for path in sorted(specs_root.rglob("spec.json")):
        spec = _read_json(path)
        test_id = spec.get("controlled_test_id")
        context = spec.get("context")
        if (
            spec.get("schema") != "official-openvino-wb04-worker-spec/v1"
            or type(test_id) is not str
            or not test_id.strip()
            or type(context) is not int
            or context <= 0
        ):
            raise ValueError("controlled inventory worker spec is invalid")
        spec_entries.append(
            {
                "path": _repo_relative(path),
                "sha256": _sha256_file(path),
                "test_id": test_id,
                "context_tokens": context,
            }
        )
    return artifact_entries, spec_entries


def _validate_controlled_terminal_inventory(
    classification: Mapping[str, Any],
    case: Mapping[str, Any],
    key: RuntimeKey,
) -> None:
    sources = classification.get("source_evidence")
    if not isinstance(sources, list) or len(sources) != 1:
        raise ValueError(
            "controlled terminal must cite one sealed artifact/spec inventory"
        )
    source = sources[0]
    inventory_path, _ = _verify_evidence(
        source.get("path"),
        source.get("sha256"),
        "controlled terminal inventory",
    )
    inventory = _read_json(inventory_path)
    fields = {
        "schema",
        "matrix_sha256",
        "inventory_roots",
        "artifact_entries",
        "spec_entries",
        "conclusions",
        "inventory_sha256",
    }
    if (
        set(inventory) != fields
        or inventory.get("schema") != CONTROLLED_INVENTORY_SCHEMA
        or inventory.get("matrix_sha256") != CANONICAL_MATRIX_SHA256
        or inventory.get("inventory_sha256")
        != _canonical_sha256(
            {
                field: value
                for field, value in inventory.items()
                if field != "inventory_sha256"
            }
        )
    ):
        raise ValueError("controlled terminal inventory is invalid")
    expected_roots = [
        _repo_relative(root) for root in CONTROLLED_INVENTORY_ROOTS
    ]
    if inventory.get("inventory_roots") != expected_roots:
        raise ValueError("controlled terminal inventory roots are invalid")
    artifacts, specs = _sealed_inventory_entries(
        CONTROLLED_INVENTORY_ROOTS
    )
    if (
        inventory.get("artifact_entries") != artifacts
        or inventory.get("spec_entries") != specs
    ):
        raise ValueError(
            "controlled terminal inventory does not match sealed roots"
        )
    controlled = {
        ("OV-01", 1024): (
            "diagnostic",
            "dynamic",
            "diagnostic-spec-absent",
        ),
        ("OV-02", 2048): (
            "granite-3b",
            "f16",
            "fp16-artifact-and-spec-absent",
        ),
    }
    conclusions = inventory.get("conclusions")
    if not isinstance(conclusions, list) or len(conclusions) != 2:
        raise ValueError("controlled terminal inventory conclusions are invalid")
    expected_conclusions = []
    for (test_id, context), (
        model,
        precision,
        reason_code,
    ) in sorted(controlled.items()):
        matching_artifacts = [
            entry["path"]
            for entry in artifacts
            if entry["model"] == model
            and entry["weight_precision"] == precision
        ]
        matching_specs = [
            entry["path"]
            for entry in specs
            if entry["test_id"] == test_id
            and entry["context_tokens"] == context
        ]
        expected_conclusions.append(
            {
                "test_id": test_id,
                "context_tokens": context,
                "status": "controlled-terminal-not-launched",
                "reason_code": reason_code,
                "matching_artifact_paths": matching_artifacts,
                "matching_spec_paths": matching_specs,
            }
        )
    if inventory["conclusions"] != expected_conclusions:
        raise ValueError(
            "controlled terminal inventory conclusions do not match evidence"
        )
    target = controlled.get((key.test_id, key.context_tokens))
    if (
        target is None
        or case.get("model") != target[0]
        or case.get("weight_precision") != target[1]
        or expected_conclusions[
            0 if key.test_id == "OV-01" else 1
        ]["matching_artifact_paths"]
        or expected_conclusions[
            0 if key.test_id == "OV-01" else 1
        ]["matching_spec_paths"]
    ):
        raise ValueError(
            "controlled terminal is not supported by the sealed inventory"
        )


def _validate_direct_device_resource_terminal(
    classification: Mapping[str, Any],
    case: Mapping[str, Any],
    key: RuntimeKey,
) -> None:
    if (
        key != RuntimeKey("OV-06", 4096)
        or case.get("model") != "granite-3b"
        or case.get("weight_precision") != "u8"
        or case.get("requested_device") != "GPU"
        or case.get("execution_route") != "device-standard"
        or case.get("expected_outcome") != "pass"
    ):
        raise ValueError(
            "device-resource terminal is not the frozen OV-06 GPU row"
        )
    sources = classification.get("source_evidence")
    if not isinstance(sources, list) or len(sources) != 3:
        raise ValueError(
            "OV-06 device-resource terminal requires three calibration attempts"
        )
    expected = set(OV06_DEVICE_ATTEMPTS)
    observed = {
        (
            _repo_relative(
                _resolve_path(
                    source.get("path"), "OV-06 calibration attempt path"
                )
            ),
            source.get("sha256"),
        )
        for source in sources
        if type(source) is dict
    }
    if observed != expected:
        raise ValueError(
            "OV-06 device-resource terminal does not cite the exact corrected "
            "GPU attempts"
        )
    property_failures = 0
    low_memory_stops = 0
    for number, source in enumerate(sources, 1):
        attempt_path, attempt_sha256 = _verify_evidence(
            source["path"],
            source["sha256"],
            f"OV-06 calibration attempt {number}",
        )
        attempt = _read_json(attempt_path)
        available = attempt.get("available_ram_bytes")
        minimum = (
            available.get("minimum") if type(available) is dict else None
        )
        floor = attempt.get("minimum_available_ram_bytes")
        sampler_job = attempt.get("sampler_job")
        workload_job = attempt.get("workload_job")
        if (
            attempt_sha256 != source["sha256"]
            or attempt.get("schema")
            != "official-openvino-wb04-governed-run/v1"
            or attempt.get("role") != "pilot"
            or attempt.get("valid") is not False
            or not isinstance(attempt.get("validation_errors"), list)
            or not attempt["validation_errors"]
            or attempt.get("sampler_exit_code") != 0
            or attempt.get("timed_out") is not False
            or attempt.get("cleanup_process_count") != 0
            or type(attempt.get("memory_sample_count")) is not int
            or attempt["memory_sample_count"] <= 0
            or floor != 2048 * 1024 * 1024
            or type(minimum) is not int
            or minimum < 0
            or attempt.get("activation") is not None
            or attempt.get("worker") is not None
            or attempt.get("output_sha256") is not None
            or attempt.get("telemetry_sha256") is not None
            or type(sampler_job) is not dict
            or sampler_job.get("setup_ok") is not True
            or sampler_job.get("query_ok") is not True
            or sampler_job.get("survivor_pids_after_cleanup") != []
            or type(workload_job) is not dict
            or workload_job.get("setup_ok") is not True
            or workload_job.get("query_ok") is not True
            or workload_job.get("survivor_pids_after_cleanup") != []
        ):
            raise ValueError(
                "OV-06 calibration attempt is not a clean governed stop"
            )
        command = attempt.get("command")
        if not isinstance(command, list) or "--spec" not in command:
            raise ValueError("OV-06 calibration spec command is invalid")
        spec_index = command.index("--spec") + 1
        if spec_index >= len(command):
            raise ValueError("OV-06 calibration spec path is missing")
        spec_path = _resolve_path(
            command[spec_index], "OV-06 calibration worker spec"
        )
        expected_spec_sha256 = (
            OV06_CORRECTED_WORKER_SPEC_SHA256
            if attempt.get("low_memory_stop") is True
            else OV06_INITIAL_WORKER_SPEC_SHA256
        )
        if _sha256_file(spec_path) != expected_spec_sha256:
            raise ValueError(
                "OV-06 calibration worker spec SHA-256 is not immutable"
            )
        spec = _read_json(spec_path)
        try:
            validate_worker_spec_against_matrix_case(spec, case)
        except ValueError as exc:
            raise ValueError(
                f"OV-06 calibration spec contradicts matrix: {exc}"
            ) from exc
        for filename, hash_field in (
            ("stdout.txt", "stdout_sha256"),
            ("stderr.txt", "stderr_sha256"),
        ):
            if _sha256_file(attempt_path.parent / filename) != attempt.get(
                hash_field
            ):
                raise ValueError(
                    f"OV-06 calibration {filename} hash mismatch"
                )
        if attempt.get("low_memory_stop") is True:
            if attempt.get("exit_code") != 137 or minimum > floor:
                raise ValueError(
                    "OV-06 low-memory calibration stop is invalid"
                )
            low_memory_stops += 1
        else:
            stderr = (attempt_path.parent / "stderr.txt").read_text(
                encoding="utf-8", errors="replace"
            )
            if (
                attempt.get("exit_code") != 1
                or minimum <= floor
                or "Invalid value: 1 for property: NUM_STREAMS"
                not in stderr
            ):
                raise ValueError(
                    "OV-06 GPU property calibration failure is invalid"
                )
            property_failures += 1
    if property_failures != 1 or low_memory_stops != 2:
        raise ValueError(
            "OV-06 requires one property failure and two RAM-floor stops"
        )


def validate_terminal_record(
    record: Mapping[str, Any],
    case: Mapping[str, Any],
    *,
    allowed_statuses: set[str] | None = None,
) -> RuntimeOutcome:
    if type(record) is not dict or set(record) != _TERMINAL_FIELDS:
        raise ValueError("terminal record fields are invalid")
    test_id = _nonempty_text(record.get("test_id"), "terminal test_id")
    context = record.get("context_tokens")
    if (
        test_id != case.get("test_id")
        or type(context) is not int
        or context not in case.get("contexts", [])
    ):
        raise ValueError("terminal record matrix identity mismatch")
    status = record.get("status")
    expected_outcome = _TERMINAL_OUTCOME_BY_STATUS.get(status)
    if expected_outcome is None:
        raise ValueError("terminal status is not an allowed explicit classification")
    direct_host_resource = (
        allowed_statuses is None
        and status == "host-resource-blocked"
        and case.get("suitable_host_required") is False
    )
    direct_controlled_terminal = (
        allowed_statuses is None
        and status == "controlled-terminal-not-launched"
        and case.get("suitable_host_required") is False
    )
    direct_device_resource = (
        allowed_statuses is None
        and status == "device-resource-blocked"
        and case.get("suitable_host_required") is False
    )
    if allowed_statuses is None:
        allowed_statuses = (
            {"suitable-host-required-not-launched"}
            if case.get("suitable_host_required") is True
            else {
                "host-resource-blocked",
                "device-resource-blocked",
                "controlled-terminal-not-launched",
            }
        )
    if status not in allowed_statuses:
        raise ValueError(
            "terminal status is not permitted directly for this matrix row"
        )
    if record.get("accepted") is not False:
        raise ValueError("terminal record cannot be accepted as execution")
    if record.get("sample_count") != 0:
        raise ValueError("terminal record must contain zero samples")
    if record.get("cleanup_process_count") != 0:
        raise ValueError("terminal record cleanup process count must be zero")
    if record.get("metric_outcome") != expected_outcome:
        raise ValueError(
            f"terminal metric outcome must be {expected_outcome}"
        )
    reason = _nonempty_text(record.get("reason"), "terminal reason")
    evidence_path, evidence_sha256 = _verify_evidence(
        record.get("evidence_path"),
        record.get("evidence_sha256"),
        "terminal evidence",
    )
    classification = _validate_classification_evidence(
        evidence_path,
        schema=TERMINAL_CLASSIFICATION_SCHEMA,
        binding={
            "test_id": test_id,
            "context_tokens": context,
            "status": status,
            "reason": reason,
            "sample_count": 0,
            "cleanup_process_count": 0,
            "metric_outcome": expected_outcome,
        },
        exact_fields={
            "schema",
            "test_id",
            "context_tokens",
            "status",
            "reason",
            "sample_count",
            "cleanup_process_count",
            "metric_outcome",
            "source_evidence",
        },
        label="terminal evidence",
    )
    _validate_source_evidence_list(
        classification["source_evidence"], "terminal evidence"
    )
    key = RuntimeKey(test_id, context)
    if direct_host_resource:
        _validate_direct_host_resource_terminal(
            classification,
            case,
            key,
        )
    if direct_controlled_terminal:
        _validate_controlled_terminal_inventory(
            classification,
            case,
            key,
        )
    if direct_device_resource:
        _validate_direct_device_resource_terminal(
            classification,
            case,
            key,
        )
    return RuntimeOutcome(
        key=key,
        status=status,
        configuration_id=f"{test_id}-ctx{context}-{status}",
        accepted=False,
        sample_count=0,
        cleanup_process_count=0,
        metric_outcome=expected_outcome,
        reason=reason,
        evidence_path=evidence_path,
        evidence_sha256=evidence_sha256,
        summary_path=None,
        summary_sha256="",
        campaign_identity_sha256="",
        runtime_config_sha256="",
        metrics={},
        activation={},
        samples={},
    )


def validate_expected_rejection_record(
    record: Mapping[str, Any],
    case: Mapping[str, Any],
    *,
    matrix_path: Path = CANONICAL_MATRIX,
) -> RuntimeOutcome:
    if type(record) is not dict or set(record) != _EXPECTED_REJECTION_FIELDS:
        raise ValueError("expected-rejection record fields are invalid")
    if case.get("expected_outcome") != "expected-rejection":
        raise ValueError("expected-rejection record is not declared by the matrix")
    test_id = _nonempty_text(
        record.get("test_id"), "expected-rejection test_id"
    )
    context = record.get("context_tokens")
    if (
        test_id != case.get("test_id")
        or type(context) is not int
        or context not in case.get("contexts", [])
    ):
        raise ValueError("expected-rejection matrix identity mismatch")
    if (
        record.get("accepted") is not False
        or record.get("status") != "passed: expected-rejection"
        or record.get("sample_count") != 0
        or record.get("cleanup_process_count") != 0
    ):
        raise ValueError(
            "expected rejection is a boundary pass with zero numeric samples"
        )
    if record.get("metric_outcome") != EXPECTED_REJECTION_LITERAL:
        raise ValueError(
            f"expected-rejection metric must be {EXPECTED_REJECTION_LITERAL}"
        )
    if type(record.get("generation_not_launched")) is not bool:
        raise ValueError("expected-rejection launch classification must be boolean")
    reason = _nonempty_text(record.get("reason"), "expected-rejection reason")
    evidence_path, evidence_sha256 = _verify_evidence(
        record.get("evidence_path"),
        record.get("evidence_sha256"),
        "expected-rejection evidence",
    )
    classification = _validate_classification_evidence(
        evidence_path,
        schema=EXPECTED_REJECTION_CLASSIFICATION_SCHEMA,
        binding={
            "test_id": test_id,
            "context_tokens": context,
            "status": "passed: expected-rejection",
            "metric_outcome": EXPECTED_REJECTION_LITERAL,
            "generation_not_launched": record["generation_not_launched"],
            "cleanup_process_count": 0,
        },
        exact_fields={
            "schema",
            "test_id",
            "context_tokens",
            "status",
            "metric_outcome",
            "generation_not_launched",
            "cleanup_process_count",
            "source_aggregate_path",
            "source_aggregate_sha256",
            "probe_sha256",
        },
        label="expected-rejection evidence",
    )
    aggregate_path, _ = _verify_evidence(
        classification["source_aggregate_path"],
        classification["source_aggregate_sha256"],
        "expected-rejection source aggregate",
    )
    aggregate = _read_json(aggregate_path)
    if aggregate.get("schema") not in {
        "official-openvino-wb04-expected-rejection-evidence/v1",
        "official-openvino-wb04-scalar-semantic-rejection-evidence/v1",
    }:
        raise ValueError("expected-rejection source aggregate schema is invalid")
    if aggregate["schema"] == "official-openvino-wb04-expected-rejection-evidence/v1":
        validate_expected_rejection_evidence(aggregate, matrix_path)
    else:
        scalar_paths: dict[str, Path] = {}
        for name in ("u8_spec", "u8_attempt", "u4_spec", "u4_attempt"):
            aggregate_value = aggregate.get(f"{name}_path")
            scalar_paths[name] = _resolve_path(
                aggregate_value,
                f"scalar expected-rejection {name} path",
            )
        validate_scalar_semantic_rejection_evidence(
            aggregate,
            matrix_path,
            u8_spec_path=scalar_paths["u8_spec"],
            u8_attempt_path=scalar_paths["u8_attempt"],
            u4_spec_path=scalar_paths["u4_spec"],
            u4_attempt_path=scalar_paths["u4_attempt"],
        )
    if aggregate.get("cleanup_process_count") != 0:
        raise ValueError("expected-rejection source aggregate cleanup is nonzero")
    probe_sha256 = _require_sha256(
        classification["probe_sha256"], "expected-rejection probe SHA-256"
    )
    probes = aggregate.get("probes")
    if not isinstance(probes, list):
        raise ValueError("expected-rejection source aggregate has no probes")
    matches = [
        probe
        for probe in probes
        if isinstance(probe, dict)
        and probe.get("probe_sha256") == probe_sha256
        and probe.get("controlled_test_id") == test_id
    ]
    if len(matches) != 1:
        raise ValueError("expected-rejection source aggregate does not bind probe")
    probe = matches[0]
    unsigned_probe = {
        field: value for field, value in probe.items() if field != "probe_sha256"
    }
    if _canonical_sha256(unsigned_probe) != probe_sha256:
        raise ValueError("expected-rejection probe SHA-256 mismatch")
    if (
        probe.get("status") != "passed: expected-rejection"
        or probe.get("expected_outcome") != "expected-rejection"
        or probe.get("cleanup_process_count") != 0
    ):
        raise ValueError("expected-rejection source probe did not pass")
    if record["generation_not_launched"]:
        if (
            aggregate.get("generation_not_launched") is not True
            or probe.get("generation_not_launched") is not True
        ):
            raise ValueError("expected-rejection source probe launched generation")
    elif (
        probe.get("generation_launched") is not True
        or probe.get("numeric_generation_metrics_accepted") is not False
        or probe.get("metric_outcome") != EXPECTED_REJECTION_LITERAL
    ):
        raise ValueError("scalar expected-rejection source probe is invalid")
    key = RuntimeKey(test_id, context)
    return RuntimeOutcome(
        key=key,
        status="passed: expected-rejection",
        configuration_id=(
            f"{test_id}-ctx{context}-expected-rejection-{evidence_sha256[:12]}"
        ),
        accepted=False,
        sample_count=0,
        cleanup_process_count=0,
        metric_outcome=EXPECTED_REJECTION_LITERAL,
        reason=reason,
        evidence_path=evidence_path,
        evidence_sha256=evidence_sha256,
        summary_path=None,
        summary_sha256="",
        campaign_identity_sha256="",
        runtime_config_sha256="",
        metrics={},
        activation={},
        samples={},
    )


def _cache_demand_units(
    case: Mapping[str, Any], context_tokens: int
) -> int:
    if type(context_tokens) is not int or context_tokens <= 0:
        raise ValueError("resource-envelope context is invalid")
    try:
        key_bits = _CACHE_PRECISION_BITS[str(case["key_cache_precision"])]
        value_bits = _CACHE_PRECISION_BITS[
            str(case["value_cache_precision"])
        ]
    except KeyError as exc:
        raise ValueError(
            "resource-envelope cache precision has no governed bit width"
        ) from exc
    return context_tokens * (key_bits + value_bits)


def validate_resource_envelope_decision(
    decision: Mapping[str, Any],
    cases: Sequence[Mapping[str, Any]],
    measured: Mapping[RuntimeKey, RuntimeOutcome],
    *,
    matrix_sha256: str | None = None,
) -> dict[RuntimeKey, RuntimeOutcome]:
    required = {
        "schema",
        "status",
        "matrix_sha256",
        "minimum_available_ram_mib",
        "accepted_anchor",
        "blocked_anchor",
        "classified_rows",
        "reason",
        "decision_sha256",
    }
    if type(decision) is not dict or set(decision) != required:
        raise ValueError("resource-envelope decision fields are invalid")
    if (
        decision.get("schema") != RESOURCE_ENVELOPE_SCHEMA
        or decision.get("status") != "accepted"
        or decision.get("minimum_available_ram_mib") != 2048
    ):
        raise ValueError("resource-envelope decision controls are invalid")
    decision_matrix_sha256 = _require_sha256(
        decision.get("matrix_sha256"), "resource-envelope matrix SHA-256"
    )
    if matrix_sha256 is not None and decision_matrix_sha256 != matrix_sha256:
        raise ValueError("resource-envelope matrix SHA-256 mismatch")
    decision_sha256 = _require_sha256(
        decision.get("decision_sha256"), "resource-envelope decision SHA-256"
    )
    unsigned = {
        key: value for key, value in decision.items() if key != "decision_sha256"
    }
    if _canonical_sha256(unsigned) != decision_sha256:
        raise ValueError("resource-envelope decision SHA-256 mismatch")
    _nonempty_text(decision.get("reason"), "resource-envelope reason")
    case_by_id = _case_map(cases)
    accepted = decision.get("accepted_anchor")
    accepted_fields = {
        "test_id",
        "context_tokens",
        "minimum_available_ram_mib",
        "measurement_summary_path",
        "measurement_summary_sha256",
    }
    if type(accepted) is not dict or set(accepted) != accepted_fields:
        raise ValueError("resource-envelope accepted anchor fields are invalid")
    accepted_key = RuntimeKey(
        accepted.get("test_id"), accepted.get("context_tokens")
    )
    accepted_row = measured.get(accepted_key)
    accepted_case = case_by_id.get(accepted_key.test_id)
    if accepted_row is None or accepted_case is None:
        raise ValueError("resource-envelope accepted anchor is not measured")
    accepted_path, accepted_sha = _verify_evidence(
        accepted.get("measurement_summary_path"),
        accepted.get("measurement_summary_sha256"),
        "resource-envelope accepted anchor",
    )
    if (
        accepted_row.summary_path is None
        or accepted_path.resolve() != accepted_row.summary_path.resolve()
        or accepted_sha != accepted_row.summary_sha256
    ):
        raise ValueError("resource-envelope accepted anchor summary mismatch")
    accepted_minimum = accepted_row.metrics["available_ram_min_mb"]["min"]
    _same_number(
        accepted.get("minimum_available_ram_mib"),
        float(accepted_minimum),
        "resource-envelope accepted minimum",
    )
    if float(accepted_minimum) <= 2048:
        raise ValueError("resource-envelope accepted anchor crossed the floor")

    blocked = decision.get("blocked_anchor")
    blocked_fields = {
        "test_id",
        "context_tokens",
        "terminal_record",
        "attempt_count",
        "attempts",
    }
    if type(blocked) is not dict or set(blocked) != blocked_fields:
        raise ValueError("resource-envelope blocked anchor fields are invalid")
    blocked_case = case_by_id.get(blocked.get("test_id"))
    if blocked_case is None:
        raise ValueError("resource-envelope blocked anchor is outside matrix")
    blocked_row = validate_terminal_record(
        blocked.get("terminal_record"),
        blocked_case,
        allowed_statuses={"host-resource-blocked"},
    )
    if (
        blocked_row.key.context_tokens != blocked.get("context_tokens")
        or blocked_row.status != "host-resource-blocked"
        or blocked_row.key in measured
        or blocked.get("attempt_count") != 3
        or not isinstance(blocked.get("attempts"), list)
        or len(blocked["attempts"]) != 3
    ):
        raise ValueError("resource-envelope blocked anchor is invalid")
    anchor_comparable_fields = (
        "phase",
        "model",
        "weight_precision",
        "device",
        "requested_device",
        "attention_path",
        "execution_route",
        "norm_correction",
    )
    accepted_demand = _cache_demand_units(
        accepted_case, accepted_key.context_tokens
    )
    blocked_demand = _cache_demand_units(
        blocked_case, blocked_row.key.context_tokens
    )
    if (
        blocked_row.key.context_tokens != accepted_key.context_tokens
        or any(
            blocked_case.get(field) != accepted_case.get(field)
            for field in anchor_comparable_fields
        )
    ):
        raise ValueError("resource-envelope anchors are not comparable")
    if accepted_demand >= blocked_demand:
        raise ValueError(
            "resource-envelope accepted anchor demand is not lower than "
            "the blocked anchor"
        )
    terminal_classification = _read_json(blocked_row.evidence_path)
    _validate_direct_host_resource_terminal(
        terminal_classification,
        blocked_case,
        blocked_row.key,
    )
    attempt_paths: set[Path] = set()
    attempt_hashes: set[str] = set()
    attempt_numbers: set[int] = set()
    attempt_references: set[tuple[str, str]] = set()
    campaign_identities: set[str] = set()
    for number, attempt_ref in enumerate(blocked["attempts"], 1):
        if type(attempt_ref) is not dict or set(attempt_ref) != {"path", "sha256"}:
            raise ValueError("resource-envelope attempt reference is invalid")
        attempt_path, attempt_sha256 = _verify_evidence(
            attempt_ref["path"],
            attempt_ref["sha256"],
            f"resource-envelope blocked attempt {number}",
        )
        if attempt_path in attempt_paths:
            raise ValueError("resource-envelope blocked attempts must be distinct")
        attempt_paths.add(attempt_path)
        if attempt_sha256 in attempt_hashes:
            raise ValueError(
                "resource-envelope blocked executions must have distinct hashes"
            )
        attempt_hashes.add(attempt_sha256)
        attempt_references.add((str(attempt_path), attempt_sha256))
        attempt = _read_json(attempt_path)
        available_ram = attempt.get("available_ram_bytes")
        raw_minimum = (
            available_ram.get("minimum")
            if type(available_ram) is dict
            else None
        )
        minimum = (
            raw_minimum / (1024 * 1024)
            if type(raw_minimum) is int and raw_minimum >= 0
            else None
        )
        if (
            attempt.get("schema") != "official-openvino-wb04-governed-run/v1"
            or attempt.get("role") != "pilot"
            or attempt.get("valid") is not False
            or attempt.get("low_memory_stop") is not True
            or attempt.get("timed_out") is not False
            or attempt.get("cleanup_process_count") != 0
            or _finite_number(
                minimum, f"resource-envelope attempt {number} minimum"
            )
            > 2048
        ):
            raise ValueError(
                "resource-envelope blocked attempt did not cross the fixed floor"
            )
        spec_path = attempt_path.parent.parent / "spec.json"
        spec = _read_json(spec_path)
        campaign_identity = _require_sha256(
            spec.get("campaign_identity_sha256"),
            f"resource-envelope blocked attempt {number} campaign identity",
        )
        if (
            spec.get("schema") != "official-openvino-wb04-worker-spec/v1"
            or spec.get("controlled_test_id") != blocked_row.key.test_id
            or spec.get("context_tokens", spec.get("context"))
            != blocked_row.key.context_tokens
            or spec.get("role") != "pilot"
        ):
            raise ValueError(
                "resource-envelope blocked attempt spec does not bind test/context"
            )
        try:
            validate_worker_spec_against_matrix_case(spec, blocked_case)
        except ValueError as exc:
            raise ValueError(
                f"resource-envelope blocked attempt spec contradicts matrix: {exc}"
            ) from exc
        campaign_identities.add(campaign_identity)
        receipt_path = attempt_path.parent.parent / "sequence-receipt.json"
        receipt = _read_json(receipt_path)
        receipt_fields = {
            "schema",
            "role",
            "attempt_number",
            "campaign_identity_sha256",
            "spec_sha256",
            "spec_path",
            "spec_file_sha256",
            "runtime_record_path",
            "runtime_record_sha256",
            "accepted",
            "controller_error",
        }
        attempt_number = receipt.get("attempt_number")
        campaign_root = attempt_path.parents[4]
        if (
            set(receipt) != receipt_fields
            or receipt.get("schema")
            != "official-openvino-wb04-sequence-receipt/v1"
            or receipt.get("role") != "pilot"
            or receipt.get("accepted") is not False
            or type(attempt_number) is not int
            or attempt_number <= 0
            or receipt.get("campaign_identity_sha256") != campaign_identity
            or receipt.get("runtime_record_sha256") != attempt_sha256
            or _resolve_campaign_path(
                campaign_root,
                receipt.get("runtime_record_path"),
                "resource-envelope receipt runtime path",
            )
            != attempt_path
            or _resolve_campaign_path(
                campaign_root,
                receipt.get("spec_path"),
                "resource-envelope receipt spec path",
            )
            != spec_path
            or receipt.get("spec_file_sha256") != _sha256_file(spec_path)
            or receipt.get("spec_sha256") != _runtime_json_sha256(spec)
            or type(receipt.get("controller_error")) is not str
            or not receipt["controller_error"].strip()
        ):
            raise ValueError(
                "resource-envelope blocked sequence receipt is invalid"
            )
        if attempt_number in attempt_numbers:
            raise ValueError(
                "resource-envelope blocked attempt numbers must be distinct"
            )
        attempt_numbers.add(attempt_number)
    if len(campaign_identities) != 1:
        raise ValueError(
            "resource-envelope blocked attempts have different campaign identities"
        )
    terminal_sources = {
        (
            str(_resolve_path(item.get("path"), "terminal resource source")),
            item.get("sha256"),
        )
        for item in terminal_classification.get("source_evidence", [])
        if type(item) is dict
    }
    if not attempt_references.issubset(terminal_sources):
        raise ValueError(
            "resource-envelope terminal classification omits blocked attempts"
        )

    classified = decision.get("classified_rows")
    if not isinstance(classified, list):
        raise ValueError("resource-envelope classified_rows must be a list")
    result: dict[RuntimeKey, RuntimeOutcome] = {
        blocked_row.key: blocked_row
    }
    blocked_demand = _cache_demand_units(
        blocked_case, blocked_row.key.context_tokens
    )
    comparable_fields = (
        "phase",
        "model",
        "weight_precision",
        "device",
        "requested_device",
        "attention_path",
        "execution_route",
    )
    for record in classified:
        target_case = case_by_id.get(record.get("test_id")) if isinstance(record, dict) else None
        if target_case is None:
            raise ValueError("resource-envelope target is outside matrix")
        row = validate_terminal_record(
            record,
            target_case,
            allowed_statuses={"host-resource-blocked-not-launched"},
        )
        if row.status != "host-resource-blocked-not-launched":
            raise ValueError("resource-envelope target status is invalid")
        if row.key in result or row.key in measured:
            raise ValueError("duplicate resource-envelope target")
        target_demand = _cache_demand_units(
            target_case, row.key.context_tokens
        )
        if (
            row.key.context_tokens <= blocked_row.key.context_tokens
            or any(
                target_case.get(field) != blocked_case.get(field)
                for field in comparable_fields
            )
            or target_demand < blocked_demand
            or (
                target_case.get("norm_correction") is False
                and blocked_case.get("norm_correction") is True
                and target_demand == blocked_demand
            )
            or target_case.get("expected_outcome") != "pass"
            or target_case.get("numeric_generation_metrics_expected") is not True
            or target_case.get("suitable_host_required") is not False
        ):
            raise ValueError("resource-envelope target is not comparable")
        target_classification = _read_json(row.evidence_path)
        target_sources = {
            (
                _resolve_path(
                    item.get("path"),
                    "resource-envelope target classification source",
                ),
                item.get("sha256"),
            )
            for item in target_classification.get("source_evidence", [])
            if type(item) is dict
        }
        if (
            blocked_row.evidence_path.resolve(),
            blocked_row.evidence_sha256,
        ) not in target_sources:
            raise ValueError(
                "resource-envelope target omits blocked-anchor classification"
            )
        result[row.key] = row
    return result


def _validate_quality_terminal(
    value: Mapping[str, Any], key: RuntimeKey
) -> QualityOutcome:
    if type(value) is not dict or set(value) != _QUALITY_TERMINAL_FIELDS:
        raise ValueError(f"{key} quality terminal fields are invalid")
    if (
        value.get("test_id") != key.test_id
        or value.get("context_tokens") != key.context_tokens
        or value.get("status") not in _QUALITY_TERMINAL_OUTCOME_BY_STATUS
        or value.get("prompt_count") != 0
        or value.get("score_outcome")
        != _QUALITY_TERMINAL_OUTCOME_BY_STATUS[value.get("status")]
    ):
        raise ValueError(f"{key} quality terminal classification is invalid")
    reason = _nonempty_text(value.get("reason"), f"{key} quality terminal reason")
    evidence_path, evidence_sha256 = _verify_evidence(
        value.get("evidence_path"),
        value.get("evidence_sha256"),
        f"{key} quality terminal evidence",
    )
    classification = _validate_classification_evidence(
        evidence_path,
        schema=QUALITY_TERMINAL_CLASSIFICATION_SCHEMA,
        binding={
            "test_id": key.test_id,
            "context_tokens": key.context_tokens,
            "status": value["status"],
            "reason": reason,
            "prompt_count": 0,
            "score_outcome": value["score_outcome"],
            "cleanup_process_count": 0,
        },
        exact_fields={
            "schema",
            "test_id",
            "context_tokens",
            "status",
            "reason",
            "prompt_count",
            "score_outcome",
            "cleanup_process_count",
            "source_evidence",
        },
        label=f"{key} quality terminal evidence",
    )
    _validate_source_evidence_list(
        classification["source_evidence"], f"{key} quality terminal evidence"
    )
    outcome = str(value["score_outcome"])
    return QualityOutcome(
        key=key,
        status=str(value["status"]),
        prompt_scores={prompt_id: outcome for prompt_id in PROMPT_IDS},
        mean_score=outcome,
        critical_failure_or_cap=f"score not computed: {reason}",
        evidence_path=evidence_path,
        evidence_sha256=evidence_sha256,
    )


def _quality_runtime_source(
    classification: Mapping[str, Any],
    runtime: RuntimeOutcome,
    key: RuntimeKey,
) -> list[tuple[Path, str]]:
    sources = [
        (
            _resolve_path(
                item.get("path"), f"{key} quality blocker source"
            ),
            _require_sha256(
                item.get("sha256"), f"{key} quality blocker source SHA-256"
            ),
        )
        for item in classification.get("source_evidence", [])
        if type(item) is dict
    ]
    binding = (runtime.evidence_path.resolve(), runtime.evidence_sha256)
    if binding not in sources:
        raise ValueError(
            f"{key} quality blocker omits measured runtime summary"
        )
    return [source for source in sources if source != binding]


def _governed_quality_prompts() -> list[dict[str, str]]:
    contract = load_prompt_contract(
        QUALITY_PROMPT_SET,
        QUALITY_RENDERED_PROMPTS,
    )
    prompts = contract["prompts"]
    return [
        {
            "prompt_id": prompt_id,
            "turn_id": "turn_1",
            "prompt": prompts[prompt_id]["execution"]["prompt"],
        }
        for prompt_id in ("P1", "P2", "P3", "P4", "P5")
    ] + [
        {
            "prompt_id": "P6",
            "turn_id": "turn_1",
            "prompt": prompts["P6"]["execution"]["turn_1_prompt"],
        },
        {
            "prompt_id": "P6",
            "turn_id": "turn_2",
            "prompt": prompts["P6"]["execution"]["turn_2_prompt"],
        },
    ]


def _quality_spec_matches_case(
    spec: Mapping[str, Any],
    case: Mapping[str, Any],
    *,
    expected_model_path: Path,
    expected_device: str,
    expected_properties: Mapping[str, Any],
    expected_prompts: Sequence[Mapping[str, str]],
) -> bool:
    properties = spec.get("properties")
    generation = spec.get("generation_settings")
    prompts = spec.get("prompts")
    if (
        set(spec)
        != {
            "schema",
            "model_path",
            "device",
            "properties",
            "generation_settings",
            "prompts",
        }
        or spec.get("schema")
        != "official-openvino-wb04-quality-worker-spec/v1"
        or spec.get("device") != expected_device
        or expected_device != case.get("requested_device")
        or Path(str(spec.get("model_path", ""))).resolve()
        != expected_model_path.resolve()
        or type(properties) is not dict
        or properties != dict(expected_properties)
        or generation
        != {
            "apply_chat_template": False,
            "do_sample": False,
            "max_new_tokens": 256,
            "rng_seed": 42,
        }
        or prompts != [dict(prompt) for prompt in expected_prompts]
    ):
        return False
    prompt_ids = [
        prompt.get("prompt_id")
        for prompt in prompts
        if type(prompt) is dict
        and type(prompt.get("prompt")) is str
        and prompt["prompt"].strip()
        and type(prompt.get("turn_id")) is str
        and prompt["turn_id"].strip()
    ]
    if (
        len(prompt_ids) != 7
        or set(prompt_ids) != set(PROMPT_IDS)
        or prompt_ids.count("P6") != 2
        or any(prompt_ids.count(prompt_id) != 1 for prompt_id in PROMPT_IDS[:-1])
    ):
        return False
    expected_key = case.get("runtime_key_algorithm")
    expected_value = case.get("runtime_value_algorithm")
    if expected_key == "STANDARD":
        if properties.get("KEY_CACHE_PRECISION") != case.get(
            "key_cache_precision"
        ):
            return False
    elif properties.get("TURBOQUANT_KEY_ALGORITHM") != expected_key:
        return False
    if expected_value == "STANDARD":
        if properties.get("VALUE_CACHE_PRECISION") != case.get(
            "value_cache_precision"
        ):
            return False
    elif properties.get("TURBOQUANT_VALUE_ALGORITHM") != expected_value:
        return False
    if (
        expected_key != "STANDARD" or expected_value != "STANDARD"
    ) and properties.get("TURBOQUANT_NORM_CORRECTION") is not case.get(
        "norm_correction"
    ):
        return False
    return True


def _validate_direct_quality_resource_blocker(
    classification: Mapping[str, Any],
    *,
    runtime: RuntimeOutcome,
    case: Mapping[str, Any],
    key: RuntimeKey,
) -> None:
    provenance_sources = _quality_runtime_source(
        classification, runtime, key
    )
    guard_sources = [
        source
        for source in provenance_sources
        if source[0].name == "guard-evidence.json"
    ]
    spec_sources = {
        source
        for source in provenance_sources
        if source[0].name == "worker-spec.json"
    }
    if len(guard_sources) != 2 or len(spec_sources) != 2:
        raise ValueError(
            f"{key} quality blocker requires two governed quality attempts "
            "with guard-bound worker-spec provenance"
        )
    campaign_identity = _read_json(
        runtime.evidence_path.parent / "campaign-identity.json"
    )
    identity = campaign_identity.get("identity")
    matrix_identity = identity.get("matrix") if type(identity) is dict else None
    config = identity.get("config") if type(identity) is dict else None
    config_fields = {
        "device",
        "max_new_tokens",
        "expected_input_tokens",
        "ignore_eos",
        "seed",
        "apply_chat_template",
        "properties",
    }
    if (
        set(campaign_identity)
        != {"schema", "identity", "campaign_identity_sha256"}
        or campaign_identity.get("schema")
        != "official-openvino-wb04-campaign-identity/v1"
        or type(identity) is not dict
        or campaign_identity.get("campaign_identity_sha256")
        != runtime.campaign_identity_sha256
        or _runtime_json_sha256(identity)
        != runtime.campaign_identity_sha256
        or identity.get("context") != key.context_tokens
        or type(matrix_identity) is not dict
        or matrix_identity.get("case") != dict(case)
        or matrix_identity.get("file_sha256") != CANONICAL_MATRIX_SHA256
        or type(config) is not dict
        or set(config) != config_fields
        or config.get("expected_input_tokens") != key.context_tokens
        or config.get("device") != case.get("requested_device")
        or _runtime_json_sha256(config) != runtime.runtime_config_sha256
    ):
        raise ValueError(
            f"{key} quality blocker runtime campaign is unbound"
        )
    model = identity.get("model") if type(identity) is dict else None
    artifact = (
        model.get("validated_artifact") if type(model) is dict else None
    )
    if type(artifact) is not dict:
        raise ValueError(f"{key} quality blocker runtime model is unbound")
    model_path = Path(str(artifact.get("artifact_root", "")))
    expected_properties = config["properties"]
    if type(expected_properties) is not dict:
        raise ValueError(
            f"{key} quality blocker runtime properties are unbound"
        )
    expected_prompts = _governed_quality_prompts()
    guard_paths: set[Path] = set()
    guard_hashes: set[str] = set()
    run_ids: set[str] = set()
    for number, (guard_path, guard_sha256) in enumerate(guard_sources, 1):
        if guard_path in guard_paths or guard_sha256 in guard_hashes:
            raise ValueError(
                f"{key} quality blocker attempts must be distinct"
            )
        guard_paths.add(guard_path)
        guard_hashes.add(guard_sha256)
        if (
            guard_path.parent.name != "governed"
            or guard_path.parents[1].name
            != f"context-{key.context_tokens}"
            or guard_path.parents[2].name != key.test_id
        ):
            raise ValueError(
                f"{key} quality blocker path does not bind test/context"
            )
        guard = _read_json(guard_path)
        observed = guard.get("observed_available_ram_bytes")
        floor = guard.get("configured_minimum_available_ram_bytes")
        minimum = (
            observed.get("minimum") if type(observed) is dict else None
        )
        job = guard.get("job_object")
        run_id = _require_sha256(
            guard.get("run_id"), f"{key} quality blocker run ID"
        )
        command = guard.get("command")
        if (
            guard.get("schema")
            != "official-openvino-owned-process-guard/v1"
            or guard.get("valid") is not False
            or not isinstance(guard.get("validation_errors"), list)
            or not guard["validation_errors"]
            or guard.get("exit_code") == 0
            or guard.get("timed_out") is not False
            or guard.get("low_memory_stop") is not True
            or guard.get("cleanup_process_count") != 0
            or floor != 2048 * 1024 * 1024
            or type(minimum) is not int
            or minimum < 0
            or minimum > floor
            or guard.get("termination_reason")
            != "minimum_available_ram"
            or type(guard.get("memory_sample_count")) is not int
            or guard["memory_sample_count"] <= 0
            or type(job) is not dict
            or job.get("setup_ok") is not True
            or job.get("query_ok") is not True
            or job.get("survivor_pids_after_cleanup") != []
            or not isinstance(command, list)
            or "--spec" not in command
        ):
            raise ValueError(
                f"{key} quality blocker attempt {number} is not a "
                "clean fixed-floor stop"
            )
        run_ids.add(run_id)
        spec_index = command.index("--spec") + 1
        if spec_index >= len(command):
            raise ValueError(f"{key} quality blocker spec command is invalid")
        spec_path = _resolve_path(
            command[spec_index], f"{key} quality blocker spec path"
        )
        bound_inputs = guard.get("bound_inputs")
        expected_bound_input = {
            "name": "quality_worker_spec",
            "path": str(spec_path),
            "sha256": _sha256_file(spec_path),
        }
        if (
            spec_path != (guard_path.parent / "worker-spec.json").resolve()
            or bound_inputs != [expected_bound_input]
            or (spec_path, expected_bound_input["sha256"])
            not in spec_sources
            or not _quality_spec_matches_case(
                _read_json(spec_path),
                case,
                expected_model_path=model_path,
                expected_device=config["device"],
                expected_properties=expected_properties,
                expected_prompts=expected_prompts,
            )
        ):
            raise ValueError(
                f"{key} quality blocker worker-spec provenance or "
                "matrix/runtime binding is invalid"
            )
    if len(run_ids) != 2:
        raise ValueError(f"{key} quality blocker run IDs are not distinct")


def _validate_quality_resource_envelope(
    classification: Mapping[str, Any],
    *,
    runtime: RuntimeOutcome,
    case: Mapping[str, Any],
    key: RuntimeKey,
    runtime_outcomes: Mapping[RuntimeKey, RuntimeOutcome],
    runtime_cases: Mapping[RuntimeKey, Mapping[str, Any]],
    quality_terminals: Mapping[RuntimeKey, Mapping[str, Any]],
) -> None:
    sources = _quality_runtime_source(classification, runtime, key)
    if len(sources) != 1:
        raise ValueError(
            f"{key} quality envelope must cite one direct blocker"
        )
    anchor_path, anchor_sha256 = sources[0]
    anchor = _read_json(anchor_path)
    anchor_fields = {
        "schema",
        "test_id",
        "context_tokens",
        "status",
        "reason",
        "prompt_count",
        "score_outcome",
        "cleanup_process_count",
        "source_evidence",
    }
    if (
        set(anchor) != anchor_fields
        or anchor.get("schema") != QUALITY_TERMINAL_CLASSIFICATION_SCHEMA
        or anchor.get("status") != "quality-host-resource-blocked"
        or anchor.get("prompt_count") != 0
        or anchor.get("score_outcome") != QUALITY_RESOURCE_BLOCKED_LITERAL
        or anchor.get("cleanup_process_count") != 0
    ):
        raise ValueError(f"{key} quality envelope anchor is invalid")
    anchor_key = RuntimeKey(
        _nonempty_text(
            anchor.get("test_id"), f"{key} quality envelope anchor test ID"
        ),
        anchor.get("context_tokens"),
    )
    selected_anchor = quality_terminals.get(anchor_key)
    if selected_anchor is None:
        raise ValueError(
            f"{key} quality envelope omits its selected quality terminal"
        )
    selected_outcome = _validate_quality_terminal(
        selected_anchor,
        anchor_key,
    )
    if (
        selected_outcome.status != "quality-host-resource-blocked"
        or selected_outcome.evidence_path.resolve() != anchor_path.resolve()
        or selected_outcome.evidence_sha256 != anchor_sha256
    ):
        raise ValueError(
            f"{key} quality envelope anchor is not the selected quality terminal"
        )
    anchor_runtime = runtime_outcomes.get(anchor_key)
    anchor_case = runtime_cases.get(anchor_key)
    if (
        anchor_runtime is None
        or anchor_runtime.status != "measured"
        or anchor_case is None
    ):
        raise ValueError(f"{key} quality envelope anchor is not measured")
    _validate_source_evidence_list(
        anchor["source_evidence"], f"{key} quality envelope anchor evidence"
    )
    _validate_direct_quality_resource_blocker(
        anchor,
        runtime=anchor_runtime,
        case=anchor_case,
        key=anchor_key,
    )
    comparable_fields = (
        "phase",
        "model",
        "weight_precision",
        "device",
        "requested_device",
        "attention_path",
        "execution_route",
    )
    target_demand = _cache_demand_units(case, key.context_tokens)
    anchor_demand = _cache_demand_units(
        anchor_case, anchor_key.context_tokens
    )
    if (
        any(
            case.get(field) != anchor_case.get(field)
            for field in comparable_fields
        )
        or target_demand < anchor_demand
        or (
            case.get("norm_correction") is False
            and anchor_case.get("norm_correction") is True
            and target_demand == anchor_demand
        )
        or case.get("expected_outcome") != "pass"
        or case.get("numeric_generation_metrics_expected") is not True
        or case.get("suitable_host_required") is not False
    ):
        raise ValueError(f"{key} quality envelope target is not comparable")


def _quality_cap_summary(prompts: Mapping[str, Any]) -> str:
    summaries: list[str] = []
    for prompt_id in PROMPT_IDS:
        prompt = prompts[prompt_id]
        caps = prompt.get("critical_caps")
        reasons = prompt.get("critical_cap_reasons")
        if not isinstance(caps, list) or not isinstance(reasons, list):
            raise ValueError(
                f"{prompt_id} adjudication lacks critical-cap evidence"
            )
        if caps:
            numeric_caps = [
                _finite_number(cap, f"{prompt_id}.critical_caps") for cap in caps
            ]
            if any(not isinstance(reason, str) or not reason.strip() for reason in reasons):
                raise ValueError(
                    f"{prompt_id} adjudication cap reasons are incomplete"
                )
            summaries.append(
                f"{prompt_id}: cap {min(numeric_caps):g} "
                f"({' / '.join(reason.strip() for reason in reasons)})"
            )
    return "; ".join(summaries) if summaries else "none"


def _validate_quality_score_evidence(
    path: Path,
    *,
    key: RuntimeKey,
    runtime: RuntimeOutcome,
) -> tuple[dict[str, Any], str]:
    evidence = _read_json(path)
    artifact_names = (
        "adjudication",
        "scoring_input",
        "score_sheet",
        "blind_map",
        "rubric",
        "prompt_set",
    )
    exact_fields = {
        "schema",
        "test_id",
        "context_tokens",
        "blind_label",
        "raw_root_path",
        "rendered_root_path",
        "campaign_identity_sha256",
        "runtime_summary_path",
        "runtime_summary_sha256",
        "runtime_config_sha256",
        *{
            f"{name}_{suffix}"
            for name in artifact_names
            for suffix in ("path", "sha256")
        },
    }
    if (
        set(evidence) != exact_fields
        or evidence.get("schema") != QUALITY_SCORE_EVIDENCE_SCHEMA
        or evidence.get("test_id") != key.test_id
        or evidence.get("context_tokens") != key.context_tokens
        or evidence.get("campaign_identity_sha256")
        != runtime.campaign_identity_sha256
        or evidence.get("runtime_summary_sha256") != runtime.summary_sha256
        or evidence.get("runtime_config_sha256")
        != runtime.runtime_config_sha256
    ):
        raise ValueError(f"{key} quality score evidence does not bind runtime")
    blind_label = _nonempty_text(
        evidence.get("blind_label"), f"{key} quality blind label"
    )
    raw_root = _resolve_path(
        evidence.get("raw_root_path"), f"{key} quality raw root"
    )
    rendered_root = _resolve_path(
        evidence.get("rendered_root_path"), f"{key} quality rendered root"
    )
    if not raw_root.is_dir() or not rendered_root.is_dir():
        raise ValueError(f"{key} quality governed input roots are missing")
    runtime_summary_path = _resolve_path(
        evidence.get("runtime_summary_path"),
        f"{key} quality runtime summary path",
    )
    if (
        runtime.summary_path is None
        or runtime_summary_path != runtime.summary_path.resolve()
        or _sha256_file(runtime_summary_path) != runtime.summary_sha256
    ):
        raise ValueError(f"{key} quality runtime summary evidence mismatch")
    artifacts: dict[str, tuple[Path, dict[str, Any]]] = {}
    for name in artifact_names:
        artifact_path, _ = _verify_evidence(
            evidence.get(f"{name}_path"),
            evidence.get(f"{name}_sha256"),
            f"{key} quality {name}",
        )
        artifacts[name] = (artifact_path, _read_json(artifact_path))
    try:
        recomputed_scoring_input = build_blind_scoring_input(
            raw_root=raw_root,
            prompt_set_path=artifacts["prompt_set"][0],
            rendered_root=rendered_root,
            rubric_path=artifacts["rubric"][0],
        )
        if recomputed_scoring_input != artifacts["scoring_input"][1]:
            raise ValueError(
                "blind scoring input does not match governed capture root"
            )
        capture_summary = _read_json(
            raw_root / blind_label / "capture-summary.json"
        )
        if (
            capture_summary.get("test_id") != key.test_id
            or capture_summary.get("context_tokens") != key.context_tokens
            or capture_summary.get("campaign_identity_sha256")
            != runtime.campaign_identity_sha256
            or capture_summary.get("runtime_summary_sha256")
            != runtime.summary_sha256
            or capture_summary.get("runtime_config_sha256")
            != runtime.runtime_config_sha256
        ):
            raise ValueError(
                "governed capture summary does not bind runtime/context"
            )
        recomputed = adjudicate_quality(
            scoring_input=artifacts["scoring_input"][1],
            score_sheet=artifacts["score_sheet"][1],
            blind_map=artifacts["blind_map"][1],
            rubric_path=artifacts["rubric"][0],
            prompt_set_path=artifacts["prompt_set"][0],
        )
    except ValueError as exc:
        raise ValueError(
            f"{key} governed quality adjudication is invalid: {exc}"
        ) from exc
    if recomputed != artifacts["adjudication"][1]:
        raise ValueError(
            f"{key} quality adjudication does not match governed inputs"
        )
    configurations = recomputed.get("configurations")
    matches = [
        configuration
        for configuration in configurations
        if isinstance(configuration, dict)
        and configuration.get("test_id") == key.test_id
        and configuration.get("blind_label") == blind_label
    ] if isinstance(configurations, list) else []
    if len(matches) != 1:
        raise ValueError(
            f"{key} quality adjudication does not contain one configuration"
        )
    configuration = matches[0]
    if (
        configuration.get("status") != "complete"
        or configuration.get("prompt_count") != 6
        or type(configuration.get("prompts")) is not dict
        or set(configuration["prompts"]) != set(PROMPT_IDS)
    ):
        raise ValueError(f"{key} quality adjudication is incomplete")
    return configuration, _quality_cap_summary(configuration["prompts"])


def _validate_quality_record(
    value: Mapping[str, Any],
    key: RuntimeKey,
    runtime: RuntimeOutcome,
) -> QualityOutcome:
    if type(value) is not dict or set(value) != _QUALITY_RECORD_FIELDS:
        raise ValueError(f"{key} quality record fields are invalid")
    if (
        value.get("test_id") != key.test_id
        or value.get("context_tokens") != key.context_tokens
        or value.get("status") != "complete"
        or value.get("prompt_count") != 6
        or value.get("campaign_identity_sha256")
        != runtime.campaign_identity_sha256
        or value.get("runtime_summary_sha256") != runtime.summary_sha256
        or value.get("runtime_config_sha256") != runtime.runtime_config_sha256
    ):
        raise ValueError(f"{key} quality/runtime identity mismatch")
    prompts = value.get("prompts")
    if type(prompts) is not dict or set(prompts) != set(PROMPT_IDS):
        raise ValueError(f"{key} quality must contain exactly P1-P6")
    prompt_scores: dict[str, float] = {}
    for prompt_id in PROMPT_IDS:
        prompt = prompts[prompt_id]
        if type(prompt) is not dict or "final_score" not in prompt:
            raise ValueError(f"{key}/{prompt_id} quality score is missing")
        score = _finite_number(
            prompt["final_score"], f"{key}/{prompt_id}.final_score"
        )
        if not 0 <= score <= 10:
            raise ValueError(f"{key}/{prompt_id} quality score is out of range")
        prompt_scores[prompt_id] = score
    mean = round(sum(prompt_scores.values()) / 6, 4)
    _same_number(value.get("mean_score"), mean, f"{key}.mean_score")
    critical = _nonempty_text(
        value.get("critical_failure_or_cap"),
        f"{key}.critical_failure_or_cap",
    )
    evidence_path, evidence_sha256 = _verify_evidence(
        value.get("evidence_path"),
        value.get("evidence_sha256"),
        f"{key} quality evidence",
    )
    configuration, computed_critical = _validate_quality_score_evidence(
        evidence_path,
        key=key,
        runtime=runtime,
    )
    if (
        configuration.get("prompts") != prompts
        or not math.isclose(
            _finite_number(configuration.get("mean_score"), f"{key}.adjudicated mean"),
            mean,
            rel_tol=0,
            abs_tol=1e-9,
        )
        or critical != computed_critical
    ):
        raise ValueError(f"{key} quality record does not bind adjudicated scores")
    return QualityOutcome(
        key=key,
        status="complete",
        prompt_scores=prompt_scores,
        mean_score=mean,
        critical_failure_or_cap=critical,
        evidence_path=evidence_path,
        evidence_sha256=evidence_sha256,
    )


def reconcile_quality_rows(
    cases: Sequence[Mapping[str, Any]],
    runtime_outcomes: Mapping[RuntimeKey, RuntimeOutcome],
    expected_rejections: Mapping[RuntimeKey, RuntimeOutcome],
    quality_records: Mapping[RuntimeKey, Mapping[str, Any]],
    quality_terminals: Mapping[RuntimeKey, Mapping[str, Any]],
) -> dict[RuntimeKey, QualityOutcome]:
    runtime_cases = _runtime_case_map(cases)
    result: dict[RuntimeKey, QualityOutcome] = {}
    required = {
        key
        for key, case in runtime_cases.items()
        if case.get("model") in {"granite-3b", "granite-8b"}
    }
    extras = (set(quality_records) | set(quality_terminals)) - required
    if extras:
        raise ValueError(f"quality outcomes outside required scope: {sorted(extras)}")
    for key in sorted(required):
        case = runtime_cases[key]
        if case.get("expected_outcome") == "expected-rejection":
            rejection = expected_rejections.get(key)
            if rejection is None:
                raise ValueError(f"missing quality outcome for {key}")
            result[key] = QualityOutcome(
                key=key,
                status=rejection.status,
                prompt_scores={
                    prompt_id: EXPECTED_REJECTION_LITERAL
                    for prompt_id in PROMPT_IDS
                },
                mean_score=EXPECTED_REJECTION_LITERAL,
                critical_failure_or_cap=(
                    "score not computed: declared expected-rejection boundary"
                ),
                evidence_path=rejection.evidence_path,
                evidence_sha256=rejection.evidence_sha256,
            )
            continue
        numeric = quality_records.get(key)
        terminal = quality_terminals.get(key)
        runtime = runtime_outcomes.get(key)
        if runtime is None:
            raise ValueError(f"missing runtime outcome for quality row {key}")
        if runtime.status == "measured":
            if numeric is not None and terminal is None:
                result[key] = _validate_quality_record(numeric, key, runtime)
                continue
            if numeric is not None or terminal is None:
                raise ValueError(
                    f"measured runtime requires numeric P1-P6 quality for {key}"
                )
            validated_terminal = _validate_quality_terminal(terminal, key)
            quality_classification = _read_json(
                validated_terminal.evidence_path
            )
            if (
                validated_terminal.status
                == "quality-host-resource-blocked"
            ):
                _validate_direct_quality_resource_blocker(
                    quality_classification,
                    runtime=runtime,
                    case=case,
                    key=key,
                )
            elif (
                validated_terminal.status
                == "quality-host-resource-blocked-not-launched"
            ):
                _validate_quality_resource_envelope(
                    quality_classification,
                    runtime=runtime,
                    case=case,
                    key=key,
                    runtime_outcomes=runtime_outcomes,
                    runtime_cases=runtime_cases,
                    quality_terminals=quality_terminals,
                )
            else:
                raise ValueError(
                    f"{key} measured quality terminal status is invalid"
                )
            result[key] = validated_terminal
            continue
        if numeric is not None or terminal is None:
            raise ValueError(f"missing quality outcome for {key}")
        validated_terminal = _validate_quality_terminal(terminal, key)
        if (
            validated_terminal.status != runtime.status
            or set(validated_terminal.prompt_scores.values())
            != {runtime.metric_outcome}
        ):
            raise ValueError(
                f"{key} quality terminal does not match runtime classification"
            )
        quality_classification = _read_json(validated_terminal.evidence_path)
        source_bindings = {
            (
                _resolve_path(item.get("path"), f"{key} quality terminal source"),
                item.get("sha256"),
            )
            for item in quality_classification.get("source_evidence", [])
            if type(item) is dict
        }
        if (runtime.evidence_path.resolve(), runtime.evidence_sha256) not in (
            source_bindings
        ):
            raise ValueError(
                f"{key} quality terminal omits runtime terminal evidence"
            )
        result[key] = validated_terminal
    return result


def reconcile_runtime_rows(
    cases: Sequence[Mapping[str, Any]],
    measured: Mapping[RuntimeKey, RuntimeOutcome],
    terminals: Mapping[RuntimeKey, RuntimeOutcome],
    expected_rejections: Mapping[RuntimeKey, RuntimeOutcome],
) -> dict[RuntimeKey, RuntimeOutcome]:
    matrix = _runtime_case_map(cases)
    supplied = set(measured) | set(terminals) | set(expected_rejections)
    extra = supplied - set(matrix)
    if extra:
        raise ValueError(f"runtime outcomes outside matrix: {sorted(extra)}")
    result: dict[RuntimeKey, RuntimeOutcome] = {}
    for key, case in matrix.items():
        candidates = [
            source[key]
            for source in (measured, terminals, expected_rejections)
            if key in source
        ]
        if len(candidates) != 1:
            raise ValueError(
                f"{key} requires exactly one explicit runtime outcome; "
                f"found {len(candidates)}"
            )
        row = candidates[0]
        if case.get("expected_outcome") == "expected-rejection":
            if row.status != "passed: expected-rejection":
                raise ValueError(f"{key} must use expected-rejection evidence")
        elif row.status == "passed: expected-rejection":
            raise ValueError(f"{key} is not an expected-rejection matrix row")
        result[key] = row
    return result


def _cell(value: Any) -> str:
    if value is None:
        raise ValueError("blank table cell")
    if isinstance(value, float):
        if not math.isfinite(value):
            raise ValueError("non-finite table cell")
        rendered = f"{value:.6f}".rstrip("0").rstrip(".")
    else:
        rendered = str(value).strip()
    if not rendered:
        raise ValueError("blank table cell")
    if rendered.upper() in _PLACEHOLDERS:
        raise ValueError(f"placeholder table cell: {rendered}")
    return rendered.replace("|", "/")


def _table(headers: Sequence[Any], rows: Sequence[Sequence[Any]]) -> str:
    width = len(headers)
    rendered = [
        "| " + " | ".join(_cell(item) for item in headers) + " |",
        "| " + " | ".join("---" for _ in headers) + " |",
    ]
    for row in rows:
        if len(row) != width:
            raise ValueError("table row width mismatch")
        rendered.append("| " + " | ".join(_cell(item) for item in row) + " |")
    return "\n".join(rendered)


def _evidence_cell(row: RuntimeOutcome | SampleMeasurement | QualityOutcome) -> str:
    try:
        display = row.evidence_path.resolve().relative_to(ROOT.resolve()).as_posix()
    except ValueError:
        display = row.evidence_path.as_posix()
    return f"{display}#sha256={row.evidence_sha256}"


def _aggregate_metric_cell(row: RuntimeOutcome, metric: str) -> Any:
    if row.status == "measured":
        return row.metrics[metric]["median"]
    return row.metric_outcome


def _aggregate_utilisation_cell(
    row: RuntimeOutcome, field: str, statistic: str
) -> Any:
    if row.status == "measured":
        return row.metrics[field][statistic]
    return row.metric_outcome


PRESENTATION_MEASURED_KEYS = (
    RuntimeKey("OV-TQ-13", 512),
    RuntimeKey("OV-TQ-14", 512),
    RuntimeKey("OV-TQ-14", 2048),
)

TIMING_HEADERS = (
    "Test ID", "Context", "Load ms", "TTFT ms", "Prompt tok/s",
    "TPOT ms", "Decode tok/s", "Generation ms",
)
MEMORY_HEADERS = (
    "Test ID", "Context", "Peak WS MiB", "Peak private MiB",
    "Available RAM min MiB", "KV MiB", "Cleanup",
)
UTILISATION_HEADERS = (
    "Test ID", "Context", "CPU mean/median/peak %",
    "GPU mean/median/peak %", "CPU samples", "GPU samples",
    "Accepted runs", "Fallback count", "Evidence ref",
)

PRESENTATION_INCOMPLETE_GROUPS = (
    ("Diagnostic only; no formal benchmark", ("OV-01",)),
    ("Missing validated FP16 artifact", ("OV-C01", "OV-02")),
    ("RAM safety floor reached", (
        "OV-B04", "OV-03", "OV-06", "OV-TQ-03", "OV-TQ-04", "OV-TQ-05",
        "OV-TQ-06", "OV-TQ-07", "OV-TQ-08", "OV-TQ-09", "OV-TQ-10",
        "OV-TQ-11", "OV-TQ-12", "OV-TQ-13", "OV-TQ-14", "OV-TQ-15",
    )),
    ("Larger host required", (
        "OV-C04", "OV-C05", "OV-C06", "OV-07", "OV-08", "OV-09",
        "OV-10", "OV-TQ-16", "OV-TQ-17",
    )),
    ("Strict activation proof incomplete", (
        "OV-B08", "OV-B09", "OV-B10", "OV-B12", "OV-TQS-01",
        "OV-TQS-02", "OV-TQS-03", "OV-TQS-04",
    )),
    ("Governed quality campaign stopped at the RAM floor", (
        "OV-TQ-13/512", "OV-TQ-14/512", "OV-TQ-14/2048",
    )),
)

PROHIBITED_PRESENTATION_CLAIMS = (
    "all tests passed",
    "quality-qualified pass",
    "quality winner",
)

PRESENTATION_NON_SUCCESS_RUNTIME_KEYS = (
    RuntimeKey("OV-01", 1024),
    RuntimeKey("OV-02", 2048),
    RuntimeKey("OV-03", 4096),
    RuntimeKey("OV-06", 4096),
    RuntimeKey("OV-07", 2048),
    RuntimeKey("OV-08", 4096),
    RuntimeKey("OV-09", 4096),
    RuntimeKey("OV-10", 4096),
    RuntimeKey("OV-TQ-03", 4096),
    RuntimeKey("OV-TQ-04", 4096),
    RuntimeKey("OV-TQ-05", 4096),
    RuntimeKey("OV-TQ-06", 4096),
    RuntimeKey("OV-TQ-07", 4096),
    RuntimeKey("OV-TQ-08", 4096),
    RuntimeKey("OV-TQ-09", 4096),
    RuntimeKey("OV-TQ-10", 4096),
    RuntimeKey("OV-TQ-11", 4096),
    RuntimeKey("OV-TQ-12", 4096),
    RuntimeKey("OV-TQ-13", 2048),
    RuntimeKey("OV-TQ-13", 4096),
    RuntimeKey("OV-TQ-13", 8192),
    RuntimeKey("OV-TQ-14", 4096),
    RuntimeKey("OV-TQ-14", 8192),
    RuntimeKey("OV-TQ-15", 4096),
    RuntimeKey("OV-TQ-16", 4096),
    RuntimeKey("OV-TQ-17", 4096),
)

PRESENTATION_NEGATIVE_CONTROL_IDS = frozenset(
    {
        "OV-04", "OV-05", "OV-TQ-01", "OV-TQ-02", "OV-TQ-18",
        "OV-TQ-19", "OV-TQ-20",
    }
)
PRESENTATION_INCOMPLETE_IDS = frozenset(
    identifier.split("/", 1)[0]
    for _, identifiers in PRESENTATION_INCOMPLETE_GROUPS
    for identifier in identifiers
)
PRESENTATION_MIXED_RUNTIME_IDS = frozenset({"OV-TQ-13", "OV-TQ-14"})


def select_presentation_measurements(
    rows: Mapping[RuntimeKey, RuntimeOutcome],
) -> tuple[RuntimeOutcome, ...]:
    measured_keys = {key for key, row in rows.items() if row.status == "measured"}
    if measured_keys != set(PRESENTATION_MEASURED_KEYS):
        raise ValueError("v1.8 presentation measured-key set is invalid")
    selected = tuple(rows[key] for key in PRESENTATION_MEASURED_KEYS)
    for row in selected:
        if not row.accepted or row.sample_count != 3 or row.cleanup_process_count != 0:
            raise ValueError(f"{row.key} is not an accepted three-sample measurement")
        if row.activation.get("fallback") is not False:
            raise ValueError(f"{row.key} does not prove fallback=false")
        required = set(REQUIRED_SCALAR_METRICS) | set(UTILISATION_METRICS)
        if not required.issubset(row.metrics):
            raise ValueError(f"{row.key} aggregate metrics are incomplete")
    return selected


def _presentation_display(value: Any) -> str:
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        numeric = float(value)
        if not math.isfinite(numeric):
            raise ValueError("non-finite presentation cell")
        return f"{numeric:.3f}".rstrip("0").rstrip(".")
    return _cell(value)


def _presentation_utilisation(row: RuntimeOutcome, field: str) -> str:
    return " / ".join(
        _presentation_display(row.metrics[field][statistic])
        for statistic in ("mean", "median", "peak")
    )


def render_section_5(rows: Mapping[RuntimeKey, RuntimeOutcome]) -> str:
    """Render the three approved measured configurations as compact aggregates."""

    selected = select_presentation_measurements(rows)
    timing_rows = []
    memory_rows = []
    utilisation_rows = []
    for index, row in enumerate(selected, 1):
        identity = [row.key.test_id, row.key.context_tokens]
        timing_rows.append(
            identity
            + [
                _presentation_display(row.metrics[metric]["median"])
                for metric in PERFORMANCE_METRICS
            ]
        )
        memory_rows.append(
            identity
            + [
                _presentation_display(row.metrics[metric]["median"])
                for metric in MEMORY_METRICS[:4]
            ]
            + [_presentation_display(row.cleanup_process_count)]
        )
        utilisation_rows.append(
            identity
            + [
                _presentation_utilisation(row, "cpu_percent"),
                _presentation_utilisation(row, "gpu_percent"),
                _presentation_display(row.metrics["cpu_percent"]["count"]),
                _presentation_display(row.metrics["gpu_percent"]["count"]),
                _presentation_display(row.sample_count),
                _presentation_display(int(row.activation["fallback"])),
                f"E{index}",
            ]
        )
    section = "\n".join(
        [
            "**Timing metrics — aggregate medians**",
            "",
            _table(TIMING_HEADERS, timing_rows),
            "",
            "**Memory metrics — aggregate medians**",
            "",
            _table(MEMORY_HEADERS, memory_rows),
            "",
            "**CPU/GPU utilisation — aggregate summary**",
            "",
            _table(UTILISATION_HEADERS, utilisation_rows),
        ]
    ).rstrip() + "\n"
    validate_section_5(section)
    return section


def validate_section_5(text: str) -> dict[str, int]:
    blocks = _table_blocks(text)
    expected_headers = [
        list(TIMING_HEADERS),
        list(MEMORY_HEADERS),
        list(UTILISATION_HEADERS),
    ]
    if len(blocks) != 3:
        raise ValueError(
            f"section 5 must contain exactly three metric tables; found {len(blocks)}"
        )
    expected_keys = {
        (key.test_id, str(key.context_tokens))
        for key in PRESENTATION_MEASURED_KEYS
    }
    evidence_by_key = {
        (key.test_id, str(key.context_tokens)): f"E{index}"
        for index, key in enumerate(PRESENTATION_MEASURED_KEYS, 1)
    }
    table_keys = []
    for table_index, (block, headers) in enumerate(
        zip(blocks, expected_headers), 1
    ):
        if len(block) != 5 or block[0] != headers:
            raise ValueError(f"section 5 table {table_index} structure is invalid")
        separator = block[1]
        if len(separator) != len(headers) or any(
            re.fullmatch(r":?-{3,}:?", cell) is None for cell in separator
        ):
            raise ValueError(f"section 5 table {table_index} separator is invalid")
        keys: set[tuple[str, str]] = set()
        labels: set[str] = set()
        for row in block[2:]:
            if len(row) != len(headers):
                raise ValueError(f"section 5 table {table_index} row width mismatch")
            for cell in row:
                _cell(cell)
            key = (row[0], row[1])
            if key in keys:
                raise ValueError(f"section 5 table {table_index} duplicate key {key}")
            keys.add(key)
            if headers == list(UTILISATION_HEADERS):
                label = row[-1]
                if evidence_by_key.get(key) != label:
                    raise ValueError(
                        f"section 5 evidence label is invalid for {key}"
                    )
                labels.add(label)
        if keys != expected_keys:
            raise ValueError(f"section 5 table {table_index} measured keys are invalid")
        if headers == list(UTILISATION_HEADERS) and labels != {"E1", "E2", "E3"}:
            raise ValueError("section 5 evidence labels must be E1, E2, E3")
        table_keys.append(keys)
    if len({frozenset(keys) for keys in table_keys}) != 1:
        raise ValueError("section 5 measured keys differ between tables")
    return {"table_count": 3, "measured_row_count": 3}


def render_section_6(
    runtime_rows: Mapping[RuntimeKey, RuntimeOutcome],
    expected_ids: set[str],
) -> str:
    """Render the governed non-success outcomes as six compact reason bullets."""

    if not runtime_rows:
        raise ValueError("section 6 requires reconciled runtime outcomes")
    select_presentation_measurements(runtime_rows)
    if not PRESENTATION_INCOMPLETE_IDS.issubset(expected_ids):
        raise ValueError("section 6 identifiers are outside the canonical matrix")
    non_success = {
        key
        for key, row in runtime_rows.items()
        if not (
            (row.status == "measured" and row.accepted)
            or row.status == "passed: expected-rejection"
        )
    }
    if non_success != set(PRESENTATION_NON_SUCCESS_RUNTIME_KEYS):
        raise ValueError("section 6 non-success runtime key set is invalid")
    all_contexts: dict[str, set[int]] = {}
    failed_contexts: dict[str, set[int]] = {}
    for key in runtime_rows:
        all_contexts.setdefault(key.test_id, set()).add(key.context_tokens)
    for key in non_success:
        failed_contexts.setdefault(key.test_id, set()).add(key.context_tokens)

    def display_identifier(identifier: str) -> list[str]:
        test_id, separator, context = identifier.partition("/")
        if separator:
            return [identifier]
        failed = failed_contexts.get(test_id, set())
        if failed and failed != all_contexts.get(test_id, set()):
            return [f"{test_id}/{value}" for value in sorted(failed)]
        return [test_id]

    bullets = []
    for reason, identifiers in PRESENTATION_INCOMPLETE_GROUPS:
        displayed = [
            value
            for identifier in identifiers
            for value in display_identifier(identifier)
        ]
        suffix = " (larger host required)" if reason == "Larger host required" else ""
        bullets.append(f"- **{reason}:** {', '.join(displayed)}{suffix}")
    return "\n".join(bullets) + "\n"


def render_section_7(quality_rows: Mapping[RuntimeKey, QualityOutcome]) -> str:
    """Render the governed quality boundary without score or winner claims."""

    for quality in quality_rows.values():
        if isinstance(quality.mean_score, (int, float)):
            raise ValueError("section 7 cannot present a numeric quality score")
    return (
        "No governed P1-P6 quality campaign completed. The governed evidence "
        "therefore provides no numeric quality score and no winner.\n"
    )


def render_section_8(
    runtime_rows: Mapping[RuntimeKey, RuntimeOutcome],
    release_input_path: Path,
) -> str:
    """Render the decision and its four hash-bound sources."""

    selected = select_presentation_measurements(runtime_rows)
    release_path = Path(release_input_path)
    release_sha256 = _sha256_file(release_path)
    evidence_rows = [
        (f"E{index}", _evidence_cell(row))
        for index, row in enumerate(selected, 1)
    ]
    evidence_rows.append(
        (
            "Reconciliation input",
            f"{release_path.as_posix()}#sha256={release_sha256}",
        )
    )
    return "\n".join(
        [
            "**Final decision**",
            "",
            _table(
                ("Decision", "Controlled conclusion"),
                [
                    (
                        "Runtime presentation",
                        "Only E1-E3 are accepted formal runtime measurements.",
                    ),
                    (
                        "Quality boundary",
                        "No numeric quality score exists and there is no winner.",
                    ),
                ],
            ),
            "",
            "**Evidence index**",
            "",
            _table(("Reference", "Hash-bound source"), evidence_rows),
            "",
        ]
    )


def validate_presentation_text(
    text: str, expected_ids: set[str]
) -> dict[str, int]:
    """Reject prohibited claims and require every canonical ID to be visible."""

    canonical_ids = frozenset(case.test_id for case in load_matrix(CANONICAL_MATRIX))
    if len(canonical_ids) != 60 or set(expected_ids) != canonical_ids:
        raise ValueError("presentation requires the canonical 60-ID inventory")
    headings = list(re.finditer(r"(?m)^# ([1-8])\.", text))
    if [match.group(1) for match in headings] != [str(number) for number in range(1, 9)]:
        raise ValueError("presentation requires exactly one ordered heading for sections 1-8")
    sections = {
        int(match.group(1)): text[
            match.end() : headings[index + 1].start()
            if index + 1 < len(headings)
            else len(text)
        ]
        for index, match in enumerate(headings)
    }

    def contains(body: str, test_id: str) -> bool:
        return re.search(
            rf"(?<![A-Z0-9-]){re.escape(test_id)}(?![A-Z0-9-])", body
        ) is not None

    success_ids = canonical_ids - PRESENTATION_NEGATIVE_CONTROL_IDS - PRESENTATION_INCOMPLETE_IDS
    placements = (
        ("success sections 1-3 or 5", success_ids, "".join(sections[number] for number in (1, 2, 3, 5))),
        ("section 4", PRESENTATION_NEGATIVE_CONTROL_IDS, sections[4]),
        ("section 6", PRESENTATION_INCOMPLETE_IDS, sections[6]),
    )
    for location, identifiers, body in placements:
        missing = sorted(test_id for test_id in identifiers if not contains(body, test_id))
        if missing:
            raise ValueError(
                f"presentation IDs missing from {location}: {', '.join(missing)}"
            )
    success_body = "".join(sections[number] for number in (1, 2, 3, 5))
    control_body = sections[4]
    incomplete_body = sections[6]
    for test_id in success_ids:
        if contains(control_body, test_id) or contains(incomplete_body, test_id):
            raise ValueError(f"successful ID is misplaced: {test_id}")
    for test_id in PRESENTATION_NEGATIVE_CONTROL_IDS:
        if contains(success_body, test_id) or contains(incomplete_body, test_id):
            raise ValueError(f"negative-control ID is misplaced: {test_id}")
    for test_id in PRESENTATION_INCOMPLETE_IDS - PRESENTATION_MIXED_RUNTIME_IDS:
        if contains(success_body, test_id) or contains(control_body, test_id):
            raise ValueError(f"incomplete ID is misplaced: {test_id}")

    lowered = text.casefold()
    for claim in PROHIBITED_PRESENTATION_CLAIMS:
        if claim in lowered:
            raise ValueError(f"prohibited presentation claim: {claim}")
    missing = sorted(test_id for test_id in canonical_ids if not contains(text, test_id))
    if missing:
        raise ValueError(f"missing canonical test IDs: {', '.join(missing)}")
    return {"controlled_id_count": len(expected_ids)}


def render_section_11(rows: Mapping[RuntimeKey, RuntimeOutcome]) -> str:
    """Render six source-separated aggregate/per-sample metric tables."""

    ordered = [rows[key] for key in sorted(rows)]
    performance_aggregate = []
    memory_aggregate = []
    utilisation_aggregate = []
    performance_samples = []
    memory_samples = []
    utilisation_samples = []
    for row in ordered:
        identity = [
            row.key.test_id,
            row.key.context_tokens,
            row.configuration_id,
        ]
        performance_aggregate.append(
            identity
            + [_aggregate_metric_cell(row, metric) for metric in PERFORMANCE_METRICS]
            + [row.status, _evidence_cell(row)]
        )
        memory_aggregate.append(
            identity
            + [_aggregate_metric_cell(row, metric) for metric in MEMORY_METRICS]
            + [row.status, _evidence_cell(row)]
        )
        utilisation_aggregate.append(
            identity
            + [
                _aggregate_utilisation_cell(row, "cpu_percent", statistic)
                for statistic in ("mean", "median", "peak", "count")
            ]
            + [
                _aggregate_utilisation_cell(row, "gpu_percent", statistic)
                for statistic in ("mean", "median", "peak", "count")
            ]
            + [row.cleanup_process_count, row.status, _evidence_cell(row)]
        )
        for sample_key in sorted(row.samples):
            sample = row.samples[sample_key]
            sample_identity = identity + [sample_key.sample_id]
            performance_samples.append(
                sample_identity
                + [sample.metrics[metric] for metric in PERFORMANCE_METRICS]
                + ["measured", _evidence_cell(sample)]
            )
            memory_samples.append(
                sample_identity
                + [sample.metrics[metric] for metric in MEMORY_METRICS]
                + ["measured", _evidence_cell(sample)]
            )
            utilisation_samples.append(
                sample_identity
                + [
                    sample.cpu_percent[statistic]
                    for statistic in ("mean", "median", "peak", "count")
                ]
                + [
                    sample.gpu_percent[statistic]
                    for statistic in ("mean", "median", "peak", "count")
                ]
                + [0, "measured", _evidence_cell(sample)]
            )

    common = ["Test ID", "Context tokens", "Configuration ID"]
    sample_common = common + ["Sample ID"]
    performance_headers = common + [
        "Load ms",
        "TTFT ms",
        "Prompt tok/s",
        "TPOT ms",
        "Decode tok/s",
        "Generation duration ms",
        "Status",
        "Evidence",
    ]
    performance_sample_headers = sample_common + performance_headers[3:]
    memory_headers = common + [
        "Peak working set MB",
        "Peak private MB",
        "Available RAM min MB",
        "KV MB",
        "GPU memory peak MB",
        "Status",
        "Evidence",
    ]
    memory_sample_headers = sample_common + memory_headers[3:]
    utilisation_headers = common + [
        "CPU mean %",
        "CPU median %",
        "CPU peak %",
        "CPU sample count",
        "GPU mean %",
        "GPU median %",
        "GPU peak %",
        "GPU sample count",
        "Cleanup process count",
        "Status",
        "Evidence",
    ]
    utilisation_sample_headers = sample_common + utilisation_headers[3:]
    parts = [
        "Use one pilot, one excluded warm-up and exactly three measured "
        "repetitions per accepted configuration. Terminal and expected-"
        "rejection rows contain no numeric sample.",
        "",
        "**Performance and timing metrics — aggregate**",
        "",
        _table(performance_headers, performance_aggregate),
        "",
        "**Performance and timing metrics — per sample**",
        "",
        _table(performance_sample_headers, performance_samples),
        "",
        "**Memory metrics — aggregate**",
        "",
        _table(memory_headers, memory_aggregate),
        "",
        "**Memory metrics — per sample**",
        "",
        _table(memory_sample_headers, memory_samples),
        "",
        "**CPU/GPU utilization metrics — aggregate**",
        "",
        _table(utilisation_headers, utilisation_aggregate),
        "",
        "**CPU/GPU utilization metrics — per sample**",
        "",
        _table(utilisation_sample_headers, utilisation_samples),
    ]
    section = "\n".join(parts).rstrip() + "\n"
    validate_section_11(section)
    return section


def _table_blocks(text: str) -> list[list[list[str]]]:
    lines = text.splitlines()
    blocks: list[list[list[str]]] = []
    index = 0
    while index < len(lines):
        if not lines[index].lstrip().startswith("|"):
            index += 1
            continue
        block: list[list[str]] = []
        while index < len(lines) and lines[index].lstrip().startswith("|"):
            cells = [
                cell.strip()
                for cell in lines[index].strip().strip("|").split("|")
            ]
            block.append(cells)
            index += 1
        blocks.append(block)
    return blocks


def validate_section_11(text: str) -> dict[str, int]:
    blocks = _table_blocks(text)
    if len(blocks) != 6:
        raise ValueError(
            f"section 11 must contain exactly six metric tables; found {len(blocks)}"
        )
    expected_headers = [
        [
            "Test ID",
            "Context tokens",
            "Configuration ID",
            "Load ms",
            "TTFT ms",
            "Prompt tok/s",
            "TPOT ms",
            "Decode tok/s",
            "Generation duration ms",
            "Status",
            "Evidence",
        ],
        [
            "Test ID",
            "Context tokens",
            "Configuration ID",
            "Sample ID",
            "Load ms",
            "TTFT ms",
            "Prompt tok/s",
            "TPOT ms",
            "Decode tok/s",
            "Generation duration ms",
            "Status",
            "Evidence",
        ],
        [
            "Test ID",
            "Context tokens",
            "Configuration ID",
            "Peak working set MB",
            "Peak private MB",
            "Available RAM min MB",
            "KV MB",
            "GPU memory peak MB",
            "Status",
            "Evidence",
        ],
        [
            "Test ID",
            "Context tokens",
            "Configuration ID",
            "Sample ID",
            "Peak working set MB",
            "Peak private MB",
            "Available RAM min MB",
            "KV MB",
            "GPU memory peak MB",
            "Status",
            "Evidence",
        ],
        [
            "Test ID",
            "Context tokens",
            "Configuration ID",
            "CPU mean %",
            "CPU median %",
            "CPU peak %",
            "CPU sample count",
            "GPU mean %",
            "GPU median %",
            "GPU peak %",
            "GPU sample count",
            "Cleanup process count",
            "Status",
            "Evidence",
        ],
        [
            "Test ID",
            "Context tokens",
            "Configuration ID",
            "Sample ID",
            "CPU mean %",
            "CPU median %",
            "CPU peak %",
            "CPU sample count",
            "GPU mean %",
            "GPU median %",
            "GPU peak %",
            "GPU sample count",
            "Cleanup process count",
            "Status",
            "Evidence",
        ],
    ]
    aggregate_count = 0
    sample_count = 0
    for table_index, (block, headers) in enumerate(
        zip(blocks, expected_headers)
    ):
        if len(block) < 2 or block[0] != headers:
            raise ValueError(f"section 11 table {table_index + 1} header mismatch")
        separator = block[1]
        if len(separator) != len(headers) or any(
            re.fullmatch(r":?-{3,}:?", cell) is None for cell in separator
        ):
            raise ValueError(
                f"section 11 table {table_index + 1} separator is invalid"
            )
        keys: set[tuple[str, ...]] = set()
        for row in block[2:]:
            if len(row) != len(headers):
                raise ValueError(
                    f"section 11 table {table_index + 1} row width mismatch"
                )
            for cell in row:
                _cell(cell)
            key_width = 4 if "Sample ID" in headers else 2
            key = (
                (row[0], row[1], row[3])
                if key_width == 4
                else (row[0], row[1])
            )
            if key in keys:
                raise ValueError(
                    f"section 11 table {table_index + 1} duplicate key {key}"
                )
            keys.add(key)
        if "Sample ID" in headers:
            sample_count += len(block) - 2
        else:
            aggregate_count += len(block) - 2
    if "TERMINAL-R001" in text:
        raise ValueError("section 11 contains a synthetic terminal run ID")
    return {
        "table_count": 6,
        "aggregate_row_count": aggregate_count,
        "sample_row_count": sample_count,
    }


def _heading_bounds(text: str, section_number: int) -> tuple[int, int]:
    lines = text.splitlines()
    pattern = re.compile(rf"^# {section_number}\.")
    starts = [index for index, line in enumerate(lines) if pattern.match(line)]
    if len(starts) != 1:
        raise ValueError(
            f"workbook must contain exactly one section {section_number} heading"
        )
    start = starts[0]
    next_pattern = re.compile(rf"^# {section_number + 1}\.")
    ends = [
        index
        for index in range(start + 1, len(lines))
        if next_pattern.match(lines[index])
    ]
    if section_number == 15 and not ends:
        return start, len(lines)
    if len(ends) != 1:
        raise ValueError(
            f"workbook must contain exactly one section {section_number + 1} heading"
        )
    return start, ends[0]


def _replace_section_body(text: str, section_number: int, body: str) -> str:
    _nonempty_text(body, f"section {section_number} body")
    lines = text.splitlines()
    start, end = _heading_bounds(text, section_number)
    replacement = [lines[start], "", *body.strip().splitlines(), ""]
    updated = lines[:start] + replacement + lines[end:]
    return "\n".join(updated).rstrip() + "\n"


def apply_release_identity(
    text: str,
    *,
    workbook_version: str,
    revision_id: str,
) -> str:
    """Apply and verify the release-controlled visible workbook identity."""

    if (
        workbook_version != TARGET_WORKBOOK_VERSION
        or revision_id != TARGET_REVISION_ID
    ):
        raise ValueError(
            "release identity must be workbook v1.7 with revision WR-035"
        )
    title_pattern = re.compile(
        r"(?m)^# 04 Official OpenVINO Controlled Retest Workbook "
        r"v[0-9]+\.[0-9]+$"
    )
    intro_pattern = re.compile(
        r"(?m)^Controlled retest revision [0-9]+\.[0-9]+"
        r"(?: \(WR-[0-9]+\))?\."
    )
    title = (
        "# 04 Official OpenVINO Controlled Retest Workbook "
        f"v{workbook_version}"
    )
    intro = f"Controlled retest revision {workbook_version} ({revision_id})."
    rendered, title_count = title_pattern.subn(title, text)
    rendered, intro_count = intro_pattern.subn(intro, rendered)
    if title_count != 1 or intro_count != 1:
        raise ValueError("release identity source fields are missing or ambiguous")
    if rendered.count(title) != 1 or rendered.count(intro) != 1:
        raise ValueError("release identity render validation failed")
    return rendered


def replace_section_11(text: str, section: str) -> str:
    validate_section_11(section)
    updated = _replace_section_body(text, 11, section)
    start, end = _heading_bounds(updated, 11)
    validate_section_11("\n".join(updated.splitlines()[start + 1 : end]))
    return updated


def render_section_12(
    runtime_rows: Mapping[RuntimeKey, RuntimeOutcome],
    quality_rows: Mapping[RuntimeKey, QualityOutcome],
) -> str:
    rows = []
    for key in sorted(quality_rows):
        quality = quality_rows[key]
        runtime = runtime_rows[key]
        rows.append(
            [
                key.test_id,
                key.context_tokens,
                runtime.configuration_id,
                *(quality.prompt_scores[prompt_id] for prompt_id in PROMPT_IDS),
                quality.mean_score,
                quality.critical_failure_or_cap,
                quality.status,
                _evidence_cell(quality),
            ]
        )
    return _table(
        [
            "Test ID",
            "Context tokens",
            "Configuration ID",
            *PROMPT_IDS,
            "Mean /10",
            "Critical failure/cap",
            "Status",
            "Evidence",
        ],
        rows,
    )


def validate_final_workbook_text(
    text: str, expected_ids: set[str]
) -> dict[str, Any]:
    report = validate_workbook_text(text, expected_ids)
    if "TERMINAL-R001" in text:
        raise ValueError("synthetic terminal run IDs are forbidden")
    for block in _table_blocks(text):
        for row in block:
            if row and all(re.fullmatch(r":?-{3,}:?", cell) for cell in row):
                continue
            for value in row:
                _cell(value)
            if any(
                value.strip().casefold() == "failed"
                or value.strip().casefold().startswith("failed:")
                for value in row
            ):
                raise ValueError("workbook contains a failed required status")
    start, end = _heading_bounds(text, 11)
    section_report = validate_section_11(
        "\n".join(text.splitlines()[start + 1 : end])
    )
    return {**report, "section_11": section_report}


def write_validated_workbook(
    destination: Path, text: str, *, expected_ids: set[str]
) -> dict[str, Any]:
    """Validate fully before atomically replacing the workbook."""

    report = validate_final_workbook_text(text, expected_ids)
    destination = Path(destination)
    destination.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        dir=destination.parent,
        prefix=f".{destination.name}.",
        suffix=".tmp",
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(text)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, destination)
    except BaseException:
        temporary.unlink(missing_ok=True)
        raise
    return report


def _normalised_keyed_records(
    values: Any, field: str
) -> dict[RuntimeKey, Mapping[str, Any]]:
    if not isinstance(values, list):
        raise ValueError(f"{field} must be a list")
    result: dict[RuntimeKey, Mapping[str, Any]] = {}
    for value in values:
        if not isinstance(value, Mapping):
            raise ValueError(f"{field} entries must be objects")
        key = RuntimeKey(value.get("test_id"), value.get("context_tokens"))
        if type(key.test_id) is not str or type(key.context_tokens) is not int:
            raise ValueError(f"{field} entry has invalid composite key")
        if key in result:
            raise ValueError(f"{field} has duplicate composite key {key}")
        result[key] = value
    return result


def validate_static_section_body(
    section_number: int,
    record: Mapping[str, Any],
) -> str:
    if (
        type(section_number) is not int
        or section_number not in (set(range(1, 11)) | {13, 14, 15})
        or type(record) is not dict
        or set(record) != {"body", "evidence"}
    ):
        raise ValueError("static section record is invalid")
    body = _nonempty_text(record.get("body"), f"section {section_number} body")
    reference = record.get("evidence")
    if type(reference) is not dict or set(reference) != {"path", "sha256"}:
        raise ValueError(f"section {section_number} evidence reference is invalid")
    evidence_path, _ = _verify_evidence(
        reference["path"],
        reference["sha256"],
        f"section {section_number} evidence",
    )
    evidence = _read_json(evidence_path)
    if (
        set(evidence)
        != {"schema", "section_number", "body", "source_evidence"}
        or evidence.get("schema") != STATIC_SECTION_EVIDENCE_SCHEMA
        or evidence.get("section_number") != section_number
        or evidence.get("body") != body
    ):
        raise ValueError(
            f"section {section_number} evidence does not bind body content"
        )
    _validate_source_evidence_list(
        evidence["source_evidence"], f"section {section_number} evidence"
    )
    validate_workbook_text(body, set())
    return body


def finalize_release(
    *,
    release_input_path: Path,
    destination: Path,
    check_only: bool,
) -> dict[str, Any]:
    release_path = Path(release_input_path)
    release = _read_json(release_path)
    required = {
        "schema",
        "campaign_date",
        "workbook_version",
        "revision_id",
        "matrix_path",
        "matrix_sha256",
        "selected_measurement_summaries",
        "terminal_records",
        "resource_envelope_decisions",
        "expected_rejection_records",
        "quality_records",
        "quality_terminal_records",
        "static_section_bodies",
    }
    if set(release) != required:
        raise ValueError("release-input fields are invalid")
    if (
        release.get("schema") != RELEASE_INPUT_SCHEMA
        or release.get("campaign_date") != "2026-07-30"
        or release.get("workbook_version") != TARGET_WORKBOOK_VERSION
        or release.get("revision_id") != TARGET_REVISION_ID
    ):
        raise ValueError("release-input identity is invalid")
    matrix_path, verified_matrix_sha256 = _verify_evidence(
        release.get("matrix_path"),
        release.get("matrix_sha256"),
        "release matrix",
    )
    if (
        matrix_path.resolve() != CANONICAL_MATRIX.resolve()
        or verified_matrix_sha256 != CANONICAL_MATRIX_SHA256
    ):
        raise ValueError("release matrix is not the frozen canonical WB-04 matrix")
    typed_cases = load_matrix(matrix_path)
    if len(typed_cases) != 60:
        raise ValueError("canonical WB-04 matrix must contain exactly 60 IDs")
    matrix = _read_json(matrix_path)
    cases = matrix.get("cases")
    if not isinstance(cases, list):
        raise ValueError("release matrix has no cases")
    if {case.test_id for case in typed_cases} != {
        case.get("test_id") for case in cases if type(case) is dict
    }:
        raise ValueError("release matrix typed/raw case identity mismatch")

    selected = release.get("selected_measurement_summaries")
    if not isinstance(selected, list) or any(
        type(value) is not str or not value.strip() for value in selected
    ):
        raise ValueError(
            "selected_measurement_summaries must explicitly name every chosen file"
        )
    measured = index_measurement_summaries(
        [_resolve_path(value, "selected measurement summary") for value in selected],
        cases,
        matrix_sha256=release["matrix_sha256"],
    )
    case_by_id = _case_map(cases)
    terminals: dict[RuntimeKey, RuntimeOutcome] = {}
    for key, record in _normalised_keyed_records(
        release.get("terminal_records"), "terminal_records"
    ).items():
        case = case_by_id.get(key.test_id)
        if case is None:
            raise ValueError(f"terminal record outside matrix: {key}")
        terminals[key] = validate_terminal_record(record, case)
    decisions = release.get("resource_envelope_decisions")
    if not isinstance(decisions, list):
        raise ValueError("resource_envelope_decisions must be a list")
    for decision in decisions:
        envelope_rows = validate_resource_envelope_decision(
            decision,
            cases,
            measured,
            matrix_sha256=release["matrix_sha256"],
        )
        overlap = set(terminals) & set(envelope_rows)
        if overlap:
            raise ValueError(f"duplicate terminal classification: {sorted(overlap)}")
        terminals.update(envelope_rows)
    expected: dict[RuntimeKey, RuntimeOutcome] = {}
    for key, record in _normalised_keyed_records(
        release.get("expected_rejection_records"),
        "expected_rejection_records",
    ).items():
        case = case_by_id.get(key.test_id)
        if case is None:
            raise ValueError(f"expected rejection outside matrix: {key}")
        expected[key] = validate_expected_rejection_record(
            record, case, matrix_path=matrix_path
        )
    runtime = reconcile_runtime_rows(cases, measured, terminals, expected)
    quality_records = _normalised_keyed_records(
        release.get("quality_records"), "quality_records"
    )
    quality_terminals = _normalised_keyed_records(
        release.get("quality_terminal_records"), "quality_terminal_records"
    )
    quality = reconcile_quality_rows(
        cases,
        runtime,
        expected,
        quality_records,
        quality_terminals,
    )

    template = apply_release_identity(
        WORKBOOK.read_text(encoding="utf-8-sig"),
        workbook_version=release["workbook_version"],
        revision_id=release["revision_id"],
    )
    bodies = release.get("static_section_bodies")
    static_sections = set(range(1, 11)) | {13, 14, 15}
    if type(bodies) is not dict or set(bodies) != {
        str(number) for number in static_sections
    }:
        raise ValueError("static_section_bodies must provide sections 1-10 and 13-15")
    text = template
    for number in sorted(static_sections):
        body = validate_static_section_body(number, bodies[str(number)])
        text = _replace_section_body(text, number, body)
    text = replace_section_11(text, render_section_11(runtime))
    text = _replace_section_body(text, 12, render_section_12(runtime, quality))
    expected_ids = {str(case["test_id"]) for case in cases}
    report = validate_final_workbook_text(text, expected_ids)
    if not check_only:
        report = write_validated_workbook(
            destination, text, expected_ids=expected_ids
        )
    return {
        **report,
        "campaign_date": "2026-07-30",
        "workbook_version": TARGET_WORKBOOK_VERSION,
        "revision_id": TARGET_REVISION_ID,
        "runtime_row_count": len(runtime),
        "measured_row_count": len(measured),
        "terminal_row_count": len(terminals),
        "expected_rejection_row_count": len(expected),
        "quality_row_count": len(quality),
        "check_only": check_only,
    }


def _parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Fail-closed WB-04 reconciler; selected evidence paths are explicit "
            "and no workbook is written until every validation passes."
        )
    )
    parser.add_argument(
        "--release-input",
        type=Path,
        default=DEFAULT_RELEASE_INPUT,
        help=(
            "strict 2026-07-30 release-input JSON; contract example: "
            f"{RELEASE_INPUT_EXAMPLE.name}"
        ),
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=WORKBOOK,
        help="Markdown workbook destination",
    )
    parser.add_argument(
        "--check-only",
        action="store_true",
        help="validate and render in memory without changing the workbook",
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv)
    try:
        report = finalize_release(
            release_input_path=args.release_input,
            destination=args.output,
            check_only=args.check_only,
        )
    except (OSError, ValueError) as exc:
        print(f"WB-04 finalization refused: {exc}", file=sys.stderr)
        return 1
    print(json.dumps(report, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
