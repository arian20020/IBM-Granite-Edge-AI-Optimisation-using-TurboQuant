"""Normalize the final OpenVINO campaigns into canonical route records."""

from __future__ import annotations

import csv
import hashlib
import json
import shutil
import statistics
from collections import Counter, defaultdict
from pathlib import Path, PureWindowsPath
from typing import Iterable, Mapping, Sequence

from .csvio import write_csv, write_json
from .evidence import (
    build_evidence_record,
    hash_file,
    validate_sha256_manifest,
    write_sha256_manifest,
)
from .models import (
    AttemptRecord,
    EvidenceRecord,
    FailureRecord,
    MeasurementRecord,
    QualityRecord,
    RouteBundle,
    Status,
    SummaryRecord,
)


_EXPERIMENTAL_ROUTE_ID = "openvino-experimental-fork"
_EXPERIMENTAL_CAMPAIGN_ID = "fv6-2026-08-30"
_EXPERIMENTAL_QUALITY_SCHEMA = "experimental-openvino-objective-quality/v2"
_FV6_RELATIVE = Path(
    "experiments/raw-results/openvino-experimental-fork/2026-08-30/fv6"
)
_EXPERIMENTAL_WORKBOOK_RELATIVE = Path(
    "outputs/openvino-experimental-fork-results/"
    "Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx"
)
_EXPERIMENTAL_ROUTE_RELATIVE = Path(
    "docs/testing/final-results/04-openvino-experimental-fork"
)

_OFFICIAL_ROUTE_ID = "openvino-official-upstream"
_OFFICIAL_CAMPAIGN_ID = "fv2-2026-08-30"
_OFFICIAL_QUALITY_SCHEMA = "experimental-openvino-objective-quality/v2"
_OFFICIAL_ROOT_RELATIVE = Path(
    "experiments/raw-results/openvino-official-upstream/2026-08-30"
)
_OFFICIAL_FV1_RELATIVE = _OFFICIAL_ROOT_RELATIVE / "fv1"
_OFFICIAL_FV2_RELATIVE = (
    _OFFICIAL_ROOT_RELATIVE / "fv2-missing-model-attempts"
)
_OFFICIAL_V1_WORKBOOK_RELATIVE = Path(
    "outputs/openvino-official-upstream-results/"
    "Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30.xlsx"
)
_OFFICIAL_V2_WORKBOOK_RELATIVE = Path(
    "outputs/openvino-official-upstream-results/"
    "Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx"
)
_OFFICIAL_ROUTE_RELATIVE = Path(
    "docs/testing/final-results/05-openvino-official-upstream"
)
_OFFICIAL_PREFLIGHT_INPUTS = (
    _OFFICIAL_FV1_RELATIVE
    / "preflight/inputs/314ad142957febe390cc7223b4deb1d1b21c187f84f6e7257a23fe46c27fcae3.txt",
    _OFFICIAL_FV1_RELATIVE
    / "preflight/inputs/ca101275d196803be37cb8fae1b81f1a7b2db733b7c1629293aae61465b2b3a0.txt",
)
_OFFICIAL_BENCHMARK_INPUT = (
    _OFFICIAL_FV1_RELATIVE
    / "inputs/f2b9e8f0f10053f3a04e1532ecd1e66d026ba1f37e7cde636bc2b5e2748c301c.txt"
)
_OFFICIAL_SOURCE_MODEL_ALIASES = (
    _OFFICIAL_FV2_RELATIVE / "guarded-retry-001/source-models.json",
    _OFFICIAL_FV2_RELATIVE / "guarded-retry-002/source-models.json",
    _OFFICIAL_FV2_RELATIVE / "source-models.json",
)
_OFFICIAL_CONVERSION_LOG_RELATIVE = (
    _OFFICIAL_FV2_RELATIVE
    / "guarded-retry-002/attempts/granite-3b__fp16/conversion.log"
)

_DETAILED_NAME = "experimental-openvino-detailed-results.csv"
_COMPARISON_NAME = "experimental-openvino-comparison.csv"
_COVERAGE_NAME = "experimental-openvino-coverage.csv"
_QUALITY_NAME = "experimental-openvino-quality-details.csv"
_ROWS_NAME = "rows.json"

_ATTEMPT_FIELDS = tuple(AttemptRecord.__dataclass_fields__)
_MEASUREMENT_FIELDS = tuple(MeasurementRecord.__dataclass_fields__)
_SUMMARY_FIELDS = tuple(SummaryRecord.__dataclass_fields__)
_QUALITY_FIELDS = tuple(QualityRecord.__dataclass_fields__)
_FAILURE_FIELDS = tuple(FailureRecord.__dataclass_fields__)
_EVIDENCE_FIELDS = tuple(EvidenceRecord.__dataclass_fields__)

_SUMMARY_CONTRACTS = {
    "generation_tokens_per_second": (
        "decode-tps-median",
        "tokens_per_second",
        "median over three executed benchmark repetitions",
    ),
    "time_to_first_token": (
        "ttft-selected-decode-median",
        "milliseconds",
        "value from the repetition selected by median decode throughput",
    ),
    "peak_working_set_bytes": (
        "peak-working-set-worst-observed",
        "bytes",
        "maximum over three executed benchmark repetitions",
    ),
}


def _attempt_id(case_id: str) -> str:
    return f"{case_id}--attempt-001"


def _measurement_identity(case_id: str, repetition: int) -> tuple[str, str, str]:
    repetition_id = f"{repetition:03d}"
    return (
        f"{case_id}--benchmark-repetition-{repetition_id}",
        f"{case_id}--benchmark",
        repetition_id,
    )


def _summary_id(case_id: str, metric_name: str) -> str:
    return f"{case_id}--{_SUMMARY_CONTRACTS[metric_name][0]}"


def _quality_id(
    case_id: str, prompt_id: str, category: str, criterion_id: str
) -> str:
    return f"{case_id}--{prompt_id}--{category}--{criterion_id}"


def _failure_id(case_id: str) -> str:
    return f"{case_id}--artifact-unavailable"


def _output_id(case_id: str, prompt_id: str) -> str:
    return f"{case_id}--{prompt_id}--output"


def _next_evidence_id(digest: str, used_ids: set[str]) -> str:
    return _next_evidence_id_for_route(
        _EXPERIMENTAL_ROUTE_ID, digest, used_ids
    )


def _next_evidence_id_for_route(
    route_id: str, digest: str, used_ids: set[str]
) -> str:
    compact = f"{route_id}-{digest[:12]}"
    if compact not in used_ids:
        return compact
    expanded = f"{route_id}-{digest}"
    if expanded in used_ids:
        raise ValueError(f"duplicate frozen evidence identity: {expanded}")
    return expanded


def _read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def _read_json(path: Path) -> object:
    return json.loads(path.read_text(encoding="utf-8"))


def _portable_source_path(value: str) -> str:
    return value.replace("\\", "/")


def _bool(value: str) -> bool:
    normalized = value.strip().lower()
    if normalized not in {"true", "false"}:
        raise ValueError(f"invalid boolean source value: {value!r}")
    return normalized == "true"


def _optional_int(value: object) -> int | None:
    return None if value is None or value == "" else int(value)


def _working_set_bytes(value_mb: object) -> int | None:
    if value_mb is None or value_mb == "":
        return None
    return round(float(value_mb) * 1_000_000)


def _record_evidence(
    repo_root: Path,
    path: Path,
    role: str,
    source_label: str,
    records: list[EvidenceRecord],
) -> EvidenceRecord:
    record = build_evidence_record(
        repo_root=repo_root,
        path=path,
        route_id=_EXPERIMENTAL_ROUTE_ID,
        campaign_id=_EXPERIMENTAL_CAMPAIGN_ID,
        role=role,
        source_label=source_label,
        existing_evidence_ids={item.evidence_id for item in records},
    )
    records.append(record)
    return record


def _ensure_uniform(rows: Sequence[Mapping[str, str]], field: str) -> str:
    values = {row[field] for row in rows}
    if len(values) != 1:
        raise ValueError(f"fv6 field {field!r} is not uniform: {sorted(values)!r}")
    return values.pop()


def _source_value(value: str, *, numeric: bool = False, boolean: bool = False) -> object:
    if value == "":
        return None
    if numeric:
        return float(value)
    if boolean:
        return _bool(value)
    return value


def _validate_fv6_sources(
    detailed: list[dict[str, str]],
    comparison: list[dict[str, str]],
    coverage: list[dict[str, str]],
    quality: list[dict[str, str]],
    rows_payload: object,
) -> None:
    if len(detailed) != 81:
        raise ValueError(f"fv6 detailed row count is {len(detailed)}, expected 81")
    case_ids = [row["case_id"] for row in detailed]
    if len(set(case_ids)) != 81:
        raise ValueError("fv6 detailed case IDs are not unique")
    status_counts = Counter((row["status"], _bool(row["executed"])) for row in detailed)
    if status_counts != Counter(
        {("passed", True): 27, ("model_artifact_unavailable", False): 54}
    ):
        raise ValueError(f"fv6 status accounting differs from the frozen campaign: {status_counts}")

    if not isinstance(rows_payload, dict) or not isinstance(rows_payload.get("rows"), list):
        raise ValueError("fv6 rows.json does not contain a rows array")
    row_objects = rows_payload["rows"]
    if len(row_objects) != 81 or {row.get("case_id") for row in row_objects} != set(case_ids):
        raise ValueError("fv6 rows.json does not preserve the detailed 81-case matrix")
    source_by_case = {row["case_id"]: row for row in detailed}
    for row in row_objects:
        source = source_by_case[row["case_id"]]
        for field in (
            "model",
            "weight_precision",
            "cache_codec",
            "status",
            "failure_stage",
            "failure_reason",
        ):
            expected = source[field] or None
            if row.get(field) != expected:
                raise ValueError(f"fv6 rows.json differs for {row['case_id']} field {field}")
        if bool(row.get("executed")) != _bool(source["executed"]):
            raise ValueError(f"fv6 rows.json execution flag differs for {row['case_id']}")

    comparison_by_key = {
        (row["model"], row["weight_precision"], row["cache_codec"]): row
        for row in comparison
    }
    comparison_keys = set(comparison_by_key)
    detailed_keys = {
        (row["model"], row["weight_precision"], row["cache_codec"])
        for row in detailed
    }
    if len(comparison) != 81 or len(comparison_by_key) != 81 or comparison_keys != detailed_keys:
        raise ValueError("fv6 comparison table does not cover the detailed matrix")
    numeric_comparison_fields = {
        "decode_tps",
        "ttft_ms",
        "peak_working_set_mb",
        "kv_mb",
        "quality_score",
    }
    for source in detailed:
        key = (source["model"], source["weight_precision"], source["cache_codec"])
        expected_case_id = "__".join(key)
        if source["case_id"] != expected_case_id:
            raise ValueError(f"fv6 detailed identifier mismatch: {source['case_id']}")
        if source["key_cache_codec"] != source["cache_codec"] or source[
            "value_cache_codec"
        ] != source["cache_codec"]:
            raise ValueError(f"fv6 detailed cache identifiers differ for {source['case_id']}")
        compared = comparison_by_key[key]
        for field in (
            "model",
            "weight_precision",
            "cache_codec",
            "status",
            "executed",
            "decode_tps",
            "ttft_ms",
            "peak_working_set_mb",
            "kv_mb",
            "quality_score",
            "failure_reason",
        ):
            detailed_value = _source_value(
                source[field],
                numeric=field in numeric_comparison_fields,
                boolean=field == "executed",
            )
            comparison_value = _source_value(
                compared[field],
                numeric=field in numeric_comparison_fields,
                boolean=field == "executed",
            )
            if detailed_value != comparison_value:
                raise ValueError(
                    f"fv6 comparison conflict for {source['case_id']} field {field}: "
                    f"{comparison_value!r} != {detailed_value!r}"
                )

    expected_coverage: dict[str, tuple[int, int, int]] = defaultdict(lambda: (0, 0, 0))
    for row in detailed:
        planned, executed, terminal = expected_coverage[row["model"]]
        expected_coverage[row["model"]] = (
            planned + 1,
            executed + int(_bool(row["executed"])),
            terminal + int(not _bool(row["executed"])),
        )
    observed_coverage = {
        row["model"]: (
            int(row["planned_cases"]),
            int(row["executed_cases"]),
            int(row["terminal_cases"]),
        )
        for row in coverage
    }
    if len(coverage) != 3 or observed_coverage != dict(expected_coverage):
        raise ValueError("fv6 coverage table does not reconcile with detailed rows")

    quality_counts = Counter((row["case_id"], row["prompt_id"]) for row in quality)
    passed_cases = {row["case_id"] for row in detailed if _bool(row["executed"])}
    if len(quality) != 3888 or {case for case, _ in quality_counts} != passed_cases:
        raise ValueError("fv6 quality detail does not cover every passed case")
    if set(quality_counts.values()) != {3}:
        raise ValueError("fv6 quality detail must have three criteria per prompt")
    prompts_per_case = Counter(case for case, _ in quality_counts)
    if set(prompts_per_case.values()) != {48}:
        raise ValueError("fv6 quality detail must have 48 prompts per passed case")


def _case_measurements(
    case_id: str,
    attempt_id: str,
    raw: Mapping[str, object],
    raw_evidence_id: str,
    *,
    route_id: str = _EXPERIMENTAL_ROUTE_ID,
    campaign_id: str = _EXPERIMENTAL_CAMPAIGN_ID,
) -> tuple[list[MeasurementRecord], list[SummaryRecord]]:
    raw_runs = raw.get("benchmark_runs")
    if not isinstance(raw_runs, list) or len(raw_runs) != 3:
        raise ValueError(f"{case_id}: expected three benchmark repetitions")
    selected_raw = raw.get("benchmark")
    if not isinstance(selected_raw, dict):
        raise ValueError(f"{case_id}: missing selected benchmark")
    selected_indexes = [
        index for index, raw_run in enumerate(raw_runs) if raw_run == selected_raw
    ]
    if len(selected_indexes) != 1:
        raise ValueError(
            f"{case_id}: selected benchmark must exactly match one repetition"
        )

    measurements: list[MeasurementRecord] = []
    results: list[Mapping[str, object]] = []
    for index, raw_run in enumerate(raw_runs, start=1):
        if not isinstance(raw_run, dict) or not isinstance(raw_run.get("result"), dict):
            raise ValueError(f"{case_id}: malformed benchmark repetition {index}")
        result = raw_run["result"]
        if result.get("status") != "passed" or result.get("executed") is not True:
            raise ValueError(f"{case_id}: non-passed benchmark repetition {index}")
        try:
            stdout_result = json.loads(str(raw_run["stdout"]))
        except (KeyError, TypeError, json.JSONDecodeError) as error:
            raise ValueError(f"{case_id}: malformed benchmark repetition stdout {index}") from error
        if stdout_result != result:
            raise ValueError(f"{case_id}: benchmark repetition {index} stdout/result conflict")
        results.append(result)
        measurement_id, run_id, repetition_id = _measurement_identity(case_id, index)
        measurements.append(
            MeasurementRecord(
                route_id=route_id,
                campaign_id=campaign_id,
                test_case_id=case_id,
                attempt_id=attempt_id,
                measurement_id=measurement_id,
                run_id=run_id,
                repetition_id=repetition_id,
                source_evidence_id=raw_evidence_id,
                latency_ms=float(result["ttft_ms"]),
                generation_tokens_per_second=float(result["decode_tps"]),
                peak_working_set_bytes=_working_set_bytes(raw_run.get("peak_working_set_mb")),
                input_tokens=_optional_int(result.get("input_tokens")),
                output_tokens=_optional_int(result.get("generated_tokens")),
            )
        )

    measurement_ids = tuple(item.measurement_id for item in measurements)
    decode_values = [item.generation_tokens_per_second for item in measurements]
    if any(value is None for value in decode_values):
        raise ValueError(f"{case_id}: missing repetition decode throughput")
    median_decode = statistics.median(float(value) for value in decode_values if value is not None)
    selected = next(
        item for item in measurements if item.generation_tokens_per_second == median_decode
    )
    if selected_indexes != [int(selected.repetition_id) - 1]:
        raise ValueError(
            f"{case_id}: selected benchmark is not the median-decode repetition"
        )
    peaks = [item.peak_working_set_bytes for item in measurements]
    if any(value is None for value in peaks):
        raise ValueError(f"{case_id}: missing repetition peak working set")
    worst_peak = max(int(value) for value in peaks if value is not None)
    summaries = [
        SummaryRecord(
            route_id=route_id,
            campaign_id=campaign_id,
            test_case_id=case_id,
            summary_id=_summary_id(case_id, "generation_tokens_per_second"),
            metric_name="generation_tokens_per_second",
            value=median_decode,
            unit="tokens_per_second",
            aggregation="median over three executed benchmark repetitions",
            source_measurement_ids=measurement_ids,
        ),
        SummaryRecord(
            route_id=route_id,
            campaign_id=campaign_id,
            test_case_id=case_id,
            summary_id=_summary_id(case_id, "time_to_first_token"),
            metric_name="time_to_first_token",
            value=selected.latency_ms,
            unit="milliseconds",
            aggregation="value from the repetition selected by median decode throughput",
            source_measurement_ids=measurement_ids,
        ),
        SummaryRecord(
            route_id=route_id,
            campaign_id=campaign_id,
            test_case_id=case_id,
            summary_id=_summary_id(case_id, "peak_working_set_bytes"),
            metric_name="peak_working_set_bytes",
            value=worst_peak,
            unit="bytes",
            aggregation="maximum over three executed benchmark repetitions",
            source_measurement_ids=measurement_ids,
        ),
    ]
    return measurements, summaries


def _json_source_cell(
    case_id: str, prompt_id: str, row: Mapping[str, str], field: str
) -> object:
    try:
        return json.loads(row[field])
    except (KeyError, json.JSONDecodeError) as error:
        raise ValueError(
            f"{case_id}/{prompt_id}: malformed quality criterion {field}"
        ) from error


def _validate_raw_quality_case(
    case_id: str,
    raw_payload: Mapping[str, object],
    source_by_prompt: Mapping[str, list[dict[str, str]]],
    detailed_row: Mapping[str, str],
) -> None:
    raw_runs = raw_payload.get("quality_runs")
    if not isinstance(raw_runs, list) or len(raw_runs) != 48:
        raise ValueError(f"{case_id}: expected 48 raw quality runs")
    if len({run.get("prompt_id") for run in raw_runs if isinstance(run, dict)}) != 48:
        raise ValueError(f"{case_id}: raw quality prompt identities are not unique")
    if set(source_by_prompt) != {
        run.get("prompt_id") for run in raw_runs if isinstance(run, dict)
    }:
        raise ValueError(f"{case_id}: raw quality prompt IDs differ from quality details")

    prompt_scores: list[float] = []
    for run in raw_runs:
        if not isinstance(run, dict):
            raise ValueError(f"{case_id}: malformed raw quality prompt")
        prompt_id = str(run["prompt_id"])
        source_rows = source_by_prompt[prompt_id]
        if len(source_rows) != 3:
            raise ValueError(f"{case_id}/{prompt_id}: expected three quality criterion rows")
        source_keys = [
            (row["category"], row["criterion_id"]) for row in source_rows
        ]
        if len(set(source_keys)) != 3:
            raise ValueError(
                f"{case_id}/{prompt_id}: quality criterion identities are not distinct"
            )
        quality = run.get("quality")
        result = run.get("result")
        if not isinstance(quality, dict) or not isinstance(result, dict):
            raise ValueError(f"{case_id}/{prompt_id}: malformed quality prompt evidence")

        prompt_fields = {
            "prompt_set_id": raw_payload.get("quality_prompt_set_id"),
            "domain": run.get("domain"),
            "prompt_length": run.get("prompt_length"),
            "prompt_score": quality.get("score"),
            "valid_output": quality.get("valid_output"),
            "critical_failure": quality.get("critical_failure"),
        }
        if quality.get("prompt_id") != prompt_id:
            raise ValueError(f"{case_id}/{prompt_id}: quality prompt identity conflict")
        if quality.get("domain") != run.get("domain") or quality.get(
            "prompt_length"
        ) != run.get("prompt_length"):
            raise ValueError(f"{case_id}/{prompt_id}: quality prompt metadata conflict")
        for field, raw_value in prompt_fields.items():
            for source in source_rows:
                if field in {"prompt_score"}:
                    source_value: object = float(source[field])
                elif field in {"valid_output", "critical_failure"}:
                    source_value = _bool(source[field])
                else:
                    source_value = source[field]
                if source_value != raw_value:
                    raise ValueError(
                        f"{case_id}/{prompt_id}: quality prompt {field} conflict"
                    )
        prompt_scores.append(float(quality["score"]))

        answer = result.get("text")
        if not isinstance(answer, str) or any(
            source["raw_answer"] != answer for source in source_rows
        ):
            raise ValueError(f"{case_id}/{prompt_id}: quality output text conflict")
        try:
            stdout_result = json.loads(str(run["stdout"]))
        except (KeyError, TypeError, json.JSONDecodeError) as error:
            raise ValueError(f"{case_id}/{prompt_id}: malformed quality output stdout") from error
        if stdout_result != result:
            raise ValueError(f"{case_id}/{prompt_id}: quality output stdout/result conflict")
        if result.get("status") != "passed" or result.get("executed") is not True:
            raise ValueError(f"{case_id}/{prompt_id}: quality output was not executed and passed")

        raw_criteria = quality.get("criteria")
        if not isinstance(raw_criteria, list) or len(raw_criteria) != 3:
            raise ValueError(f"{case_id}/{prompt_id}: expected three raw quality criteria")
        raw_by_key = {
            (criterion.get("category"), criterion.get("id")): criterion
            for criterion in raw_criteria
            if isinstance(criterion, dict)
        }
        if len(raw_by_key) != 3 or set(raw_by_key) != set(source_keys):
            raise ValueError(f"{case_id}/{prompt_id}: quality criterion identity conflict")
        category_scores = quality.get("category_scores")
        if not isinstance(category_scores, dict):
            raise ValueError(f"{case_id}/{prompt_id}: missing quality category scores")
        for source in source_rows:
            key = (source["category"], source["criterion_id"])
            criterion = raw_by_key[key]
            comparisons = {
                "category": (source["category"], criterion.get("category")),
                "criterion_id": (source["criterion_id"], criterion.get("id")),
                "kind": (source["kind"], criterion.get("kind")),
                "weight": (float(source["weight"]), criterion.get("weight")),
                "critical": (_bool(source["critical"]), bool(criterion.get("critical", False))),
                "passed": (_bool(source["passed"]), criterion.get("passed")),
                "points_awarded": (
                    float(source["points_awarded"]),
                    criterion.get("points_awarded"),
                ),
                "expected": (
                    _json_source_cell(case_id, prompt_id, source, "expected_json"),
                    criterion.get("expected"),
                ),
                "observed": (
                    _json_source_cell(case_id, prompt_id, source, "observed_json"),
                    criterion.get("observed"),
                ),
                "reason": (source["reason"], criterion.get("reason")),
                "category_score": (
                    float(source["category_score"]),
                    category_scores.get(source["category"]),
                ),
                "health_checks": (
                    _json_source_cell(case_id, prompt_id, source, "health_checks_json"),
                    quality.get("health_checks"),
                ),
            }
            for field, (source_value, raw_value) in comparisons.items():
                if source_value != raw_value:
                    raise ValueError(
                        f"{case_id}/{prompt_id}: quality criterion {field} conflict"
                    )

    expected_prompt_count = int(detailed_row["quality_prompt_count"])
    if expected_prompt_count != len(prompt_scores):
        raise ValueError(f"{case_id}: quality prompt count conflicts with detailed results")
    case_score = round(statistics.mean(prompt_scores), 4)
    if float(detailed_row["quality_score"]) != case_score:
        raise ValueError(f"{case_id}: quality case score conflicts with prompt evidence")


def build_experimental_bundle(repo_root: Path) -> RouteBundle:
    """Build the canonical fv6 bundle from only its frozen source evidence."""
    root = Path(repo_root).resolve(strict=True)
    fv6 = root / _FV6_RELATIVE
    workbook = root / _EXPERIMENTAL_WORKBOOK_RELATIVE
    detailed_path = fv6 / _DETAILED_NAME
    comparison_path = fv6 / _COMPARISON_NAME
    coverage_path = fv6 / _COVERAGE_NAME
    quality_path = fv6 / _QUALITY_NAME
    rows_path = fv6 / _ROWS_NAME

    detailed = _read_csv(detailed_path)
    comparison = _read_csv(comparison_path)
    coverage = _read_csv(coverage_path)
    quality_rows = _read_csv(quality_path)
    rows_payload = _read_json(rows_path)
    _validate_fv6_sources(detailed, comparison, coverage, quality_rows, rows_payload)

    evidence: list[EvidenceRecord] = []
    source_evidence = {
        "detailed": _record_evidence(
            root, detailed_path, "source-results", "fv6 detailed results", evidence
        ),
        "comparison": _record_evidence(
            root, comparison_path, "source-results", "fv6 comparison results", evidence
        ),
        "coverage": _record_evidence(
            root, coverage_path, "source-coverage", "fv6 coverage results", evidence
        ),
        "quality": _record_evidence(
            root, quality_path, "source-quality", "fv6 quality details", evidence
        ),
        "rows": _record_evidence(
            root, rows_path, "source-ledger", "fv6 normalized source rows", evidence
        ),
        "workbook": _record_evidence(
            root, workbook, "source-workbook", "fv6 final interactive workbook", evidence
        ),
    }

    attempts: list[AttemptRecord] = []
    failures: list[FailureRecord] = []
    measurements: list[MeasurementRecord] = []
    summaries: list[SummaryRecord] = []
    raw_by_case: dict[str, Mapping[str, object]] = {}
    raw_evidence_by_case: dict[str, EvidenceRecord] = {}
    input_evidence_by_path: dict[str, EvidenceRecord] = {}
    runtime_versions: set[str] = set()
    runtime_properties: set[tuple[tuple[str, str], ...]] = set()

    for row in detailed:
        case_id = row["case_id"]
        attempt_id = _attempt_id(case_id)
        executed = _bool(row["executed"])
        status = Status.from_source(row["status"])
        attempt_evidence_ids = [
            source_evidence["detailed"].evidence_id,
            source_evidence["rows"].evidence_id,
        ]
        if executed:
            raw_relative = _portable_source_path(row["raw_result_path"])
            raw_path = root / Path(raw_relative)
            if hash_file(raw_path) != row["raw_result_sha256"]:
                raise ValueError(f"{case_id}: raw-result hash differs from detailed source")
            raw_record = _record_evidence(
                root, raw_path, "raw-case-result", f"fv6 raw result {case_id}", evidence
            )
            raw_payload = _read_json(raw_path)
            if not isinstance(raw_payload, dict) or raw_payload.get("case_id") != case_id:
                raise ValueError(f"{case_id}: raw-result identity mismatch")
            raw_by_case[case_id] = raw_payload
            raw_evidence_by_case[case_id] = raw_record
            attempt_evidence_ids.append(raw_record.evidence_id)
            case_measurements, case_summaries = _case_measurements(
                case_id, attempt_id, raw_payload, raw_record.evidence_id
            )
            measurements.extend(case_measurements)
            summaries.extend(case_summaries)
            benchmark = raw_payload.get("benchmark")
            if not isinstance(benchmark, dict) or not isinstance(benchmark.get("result"), dict):
                raise ValueError(f"{case_id}: missing selected benchmark result")
            selected = benchmark["result"]
            if raw_payload.get("benchmark_selection") != (
                "median decode_tps among executed repetitions"
            ):
                raise ValueError(f"{case_id}: unsupported benchmark selection rule")
            expected_summary = {
                item.metric_name: item.value for item in case_summaries
            }
            if float(row["decode_tps"]) != expected_summary["generation_tokens_per_second"]:
                raise ValueError(f"{case_id}: detailed decode value is not the repetition median")
            if float(row["ttft_ms"]) != expected_summary["time_to_first_token"]:
                raise ValueError(f"{case_id}: detailed TTFT is not from the selected repetition")
            if _working_set_bytes(row["peak_working_set_mb"]) != expected_summary["peak_working_set_bytes"]:
                raise ValueError(f"{case_id}: detailed peak memory is not worst-observed")
            if float(selected["decode_tps"]) != float(row["decode_tps"]):
                raise ValueError(f"{case_id}: selected raw result differs from detailed result")
            for source_field, raw_field in (
                ("load_ms", "load_ms"),
                ("ttft_ms", "ttft_ms"),
                ("tpot_ms", "tpot_ms"),
                ("decode_tps", "decode_tps"),
                ("generation_duration_ms", "generation_duration_ms"),
                ("context_tokens", "input_tokens"),
                ("max_new_tokens", "max_new_tokens"),
            ):
                if float(row[source_field]) != float(selected[raw_field]):
                    raise ValueError(
                        f"{case_id}: selected benchmark {source_field} conflicts with detailed results"
                    )
            benchmark_runs = raw_payload["benchmark_runs"]
            envelope_checks = {
                "peak_working_set_mb": max(
                    float(run["peak_working_set_mb"]) for run in benchmark_runs
                ),
                "peak_private_mb": max(
                    float(run["peak_private_mb"]) for run in benchmark_runs
                ),
                "available_ram_min_mb": min(
                    float(run["available_ram_min_mb"]) for run in benchmark_runs
                ),
            }
            for source_field, expected in envelope_checks.items():
                if float(row[source_field]) != expected:
                    raise ValueError(
                        f"{case_id}: benchmark repetition {source_field} aggregate conflict"
                    )
            if raw_payload.get("model_size_bytes") != int(row["model_size_bytes"]):
                raise ValueError(f"{case_id}: raw model size conflicts with detailed results")
            fork = raw_payload.get("fork")
            if not isinstance(fork, dict) or fork != {
                "url": row["fork_url"],
                "branch": row["fork_branch"],
                "commit": row["fork_commit"],
            }:
                raise ValueError(f"{case_id}: raw repository identity conflicts with detailed results")
            version = selected.get("openvino_version")
            if isinstance(version, str):
                runtime_versions.add(version)
            properties = raw_payload.get("runtime_properties")
            if isinstance(properties, dict):
                runtime_properties.add(tuple(sorted((str(k), str(v)) for k, v in properties.items())))
        else:
            if any(
                row[field] != ""
                for field in (
                    "load_ms",
                    "ttft_ms",
                    "prompt_tps",
                    "tpot_ms",
                    "decode_tps",
                    "generation_duration_ms",
                    "peak_working_set_mb",
                    "peak_private_mb",
                    "available_ram_min_mb",
                    "kv_mb",
                    "quality_score",
                    "quality_prompt_count",
                    "raw_result_path",
                    "raw_result_sha256",
                )
            ):
                raise ValueError(f"{case_id}: unavailable row contains fabricated observations")
            failures.append(
                FailureRecord(
                    route_id=_EXPERIMENTAL_ROUTE_ID,
                    campaign_id=_EXPERIMENTAL_CAMPAIGN_ID,
                    test_case_id=case_id,
                    attempt_id=attempt_id,
                    failure_id=_failure_id(case_id),
                    status=status,
                    stage=row["failure_stage"],
                    reason=row["failure_reason"],
                    source_status=row["status"],
                    evidence_ids=tuple(attempt_evidence_ids),
                )
            )
        attempts.append(
            AttemptRecord(
                route_id=_EXPERIMENTAL_ROUTE_ID,
                campaign_id=_EXPERIMENTAL_CAMPAIGN_ID,
                test_case_id=case_id,
                attempt_id=attempt_id,
                status=status,
                executed=executed,
                reason=row["failure_reason"],
                model_id=row["model"],
                weight_format_id=row["weight_precision"],
                cache_format_id=row["cache_codec"],
                source_status=row["status"],
                failure_kind=row["failure_stage"] or None,
                evidence_ids=tuple(attempt_evidence_ids),
            )
        )

    quality_schemas = {
        run.get("quality", {}).get("schema")
        for payload in raw_by_case.values()
        for run in payload.get("quality_runs", [])
        if isinstance(run, dict) and isinstance(run.get("quality"), dict)
    }
    if quality_schemas != {_EXPERIMENTAL_QUALITY_SCHEMA}:
        raise ValueError(
            "fv6 quality scoring schema is not uniform and expected: "
            f"{sorted(str(value) for value in quality_schemas)}"
        )
    quality_scoring_version = next(iter(quality_schemas))

    quality: list[QualityRecord] = []
    quality_by_case_prompt: dict[tuple[str, str], list[dict[str, str]]] = defaultdict(list)
    for row in quality_rows:
        quality_by_case_prompt[(row["case_id"], row["prompt_id"])].append(row)
        raw_record = raw_evidence_by_case[row["case_id"]]
        quality.append(
            QualityRecord(
                route_id=_EXPERIMENTAL_ROUTE_ID,
                campaign_id=_EXPERIMENTAL_CAMPAIGN_ID,
                test_case_id=row["case_id"],
                quality_id=_quality_id(
                    row["case_id"],
                    row["prompt_id"],
                    row["category"],
                    row["criterion_id"],
                ),
                prompt_id=row["prompt_id"],
                criterion_id=f"{row['category']}:{row['criterion_id']}",
                score=float(row["points_awarded"]),
                maximum_score=float(row["weight"]),
                prompt_suite_id=row["prompt_set_id"],
                rubric_id="objective-quality-weighted-5-3-2-output-health-gate",
                scoring_version=quality_scoring_version,
                source_evidence_id=raw_record.evidence_id,
            )
        )

    detailed_by_case = {row["case_id"]: row for row in detailed}
    for case_id, raw_payload in raw_by_case.items():
        source_by_prompt = {
            prompt_id: rows
            for (quality_case_id, prompt_id), rows in quality_by_case_prompt.items()
            if quality_case_id == case_id
        }
        _validate_raw_quality_case(
            case_id,
            raw_payload,
            source_by_prompt,
            detailed_by_case[case_id],
        )
        raw_quality_runs = raw_payload.get("quality_runs")
        if not isinstance(raw_quality_runs, list) or len(raw_quality_runs) != 48:
            raise ValueError(f"{case_id}: expected 48 raw quality runs")
        if {run.get("prompt_id") for run in raw_quality_runs if isinstance(run, dict)} != {
            prompt for case, prompt in quality_by_case_prompt if case == case_id
        }:
            raise ValueError(f"{case_id}: raw quality prompt IDs differ from quality details")
        for run in raw_quality_runs:
            if not isinstance(run, dict):
                raise ValueError(f"{case_id}: malformed quality run")
            prompt_relative = _portable_source_path(str(run["prompt_path"]))
            prompt_path = root / Path(prompt_relative)
            if hash_file(prompt_path) != run["prompt_sha256"]:
                raise ValueError(f"{case_id}/{run['prompt_id']}: prompt hash mismatch")
            if prompt_relative not in input_evidence_by_path:
                input_evidence_by_path[prompt_relative] = _record_evidence(
                    root,
                    prompt_path,
                    "quality-prompt-input",
                    f"fv6 prompt input {run['prompt_id']}",
                    evidence,
                )

    repository = {
        "url": _ensure_uniform(detailed, "fork_url"),
        "branch": _ensure_uniform(detailed, "fork_branch"),
        "commit": _ensure_uniform(detailed, "fork_commit"),
        "source_campaign": "fv6",
        "source_date": "2026-08-30",
    }
    hardware = {
        "status": "not_collected",
        "reason": "fv6 source evidence records process memory but no portable host manifest",
    }
    software = {
        "openvino_versions": sorted(runtime_versions),
        "runtime_properties": [dict(items) for items in sorted(runtime_properties)],
    }
    return RouteBundle(
        route_id=_EXPERIMENTAL_ROUTE_ID,
        campaign_id=_EXPERIMENTAL_CAMPAIGN_ID,
        attempts=tuple(attempts),
        measurements=tuple(measurements),
        summaries=tuple(summaries),
        quality=tuple(quality),
        failures=tuple(failures),
        evidence=tuple(evidence),
        repository=repository,
        hardware=hardware,
        software=software,
    )


def _record_official_evidence(
    repo_root: Path,
    path: Path,
    role: str,
    source_label: str,
    records: list[EvidenceRecord],
    *,
    input_evidence_ids: Sequence[str] = (),
) -> EvidenceRecord:
    record = build_evidence_record(
        repo_root=repo_root,
        path=path,
        route_id=_OFFICIAL_ROUTE_ID,
        campaign_id=_OFFICIAL_CAMPAIGN_ID,
        role=role,
        source_label=source_label,
        input_evidence_ids=input_evidence_ids,
        existing_evidence_ids={item.evidence_id for item in records},
    )
    records.append(record)
    return record


def _official_terminal_manifest_relative(row: Mapping[str, str]) -> Path:
    model_weight = f"{row['model']}__{row['weight_precision']}"
    if model_weight == "granite-3b__fp16":
        return _OFFICIAL_FV2_RELATIVE / (
            "guarded-retry-002/attempts/granite-3b__fp16/manifest.json"
        )
    return _OFFICIAL_FV2_RELATIVE / "attempts" / model_weight / "manifest.json"


def _official_cache_semantics(cache_codec: str) -> tuple[str, str, dict[str, str]]:
    semantics = {
        "tbq3": (
            "TURBO",
            "u3",
            {
                "ATTENTION_BACKEND": "SDPA",
                "KEY_CACHE_PRECISION": "u3",
                "KEY_CACHE_QUANT_ALG": "TURBO",
                "VALUE_CACHE_PRECISION": "u3",
                "VALUE_CACHE_QUANT_ALG": "TURBO",
            },
        ),
        "tbq4": (
            "TURBO",
            "u4",
            {
                "ATTENTION_BACKEND": "SDPA",
                "KEY_CACHE_PRECISION": "u4",
                "KEY_CACHE_QUANT_ALG": "TURBO",
                "VALUE_CACHE_PRECISION": "u4",
                "VALUE_CACHE_QUANT_ALG": "TURBO",
            },
        ),
        "u4": (
            "SCALAR",
            "u4",
            {
                "ATTENTION_BACKEND": "SDPA",
                "KEY_CACHE_PRECISION": "u4",
                "KEY_CACHE_QUANT_ALG": "SCALAR",
                "VALUE_CACHE_PRECISION": "u4",
                "VALUE_CACHE_QUANT_ALG": "SCALAR",
            },
        ),
        "u8": (
            "SCALAR",
            "u8",
            {
                "ATTENTION_BACKEND": "SDPA",
                "KEY_CACHE_PRECISION": "u8",
                "KEY_CACHE_QUANT_ALG": "SCALAR",
                "VALUE_CACHE_PRECISION": "u8",
                "VALUE_CACHE_QUANT_ALG": "SCALAR",
            },
        ),
        "f16": ("", "f16", {"ATTENTION_BACKEND": "SDPA"}),
    }
    try:
        return semantics[cache_codec]
    except KeyError as error:
        raise ValueError(f"unsupported official cache codec: {cache_codec}") from error


def _validate_official_cache_source_row(row: Mapping[str, str], source: str) -> None:
    algorithm, precision, _ = _official_cache_semantics(row["cache_codec"])
    actual = (
        row["key_cache_algorithm"],
        row["value_cache_algorithm"],
        row["key_cache_precision"],
        row["value_cache_precision"],
    )
    expected = (algorithm, algorithm, precision, precision)
    if actual != expected:
        raise ValueError(
            f"{row['case_id']}: {source} cache activation semantics conflict: "
            f"{actual!r} != {expected!r}"
        )


def _validate_official_raw_cache_semantics(
    case_id: str,
    cache_codec: str,
    raw_payload: Mapping[str, object],
    repo_root: Path,
) -> None:
    _, _, expected = _official_cache_semantics(cache_codec)
    if raw_payload.get("runtime_properties") != expected:
        raise ValueError(f"{case_id}: raw cache activation semantics conflict")
    runs = list(raw_payload.get("benchmark_runs", [])) + list(
        raw_payload.get("quality_runs", [])
    )
    for run in runs:
        result = run.get("result") if isinstance(run, dict) else None
        if not isinstance(result, dict) or result.get("runtime_properties") != expected:
            raise ValueError(f"{case_id}: run cache activation semantics conflict")
    benchmark_runs = list(raw_payload.get("benchmark_runs", []))
    selected = raw_payload.get("benchmark")
    if isinstance(selected, dict):
        benchmark_runs.append(selected)
    expected_path = _OFFICIAL_BENCHMARK_INPUT.as_posix()
    expected_sha = hash_file(repo_root / _OFFICIAL_BENCHMARK_INPUT)
    for run in benchmark_runs:
        if not isinstance(run, dict):
            raise ValueError(f"{case_id}: malformed benchmark input evidence")
        actual_path = _portable_source_path(str(run.get("prompt_path", "")))
        if actual_path != expected_path:
            raise ValueError(f"{case_id}: benchmark input path conflict")
        if run.get("prompt_sha256") != expected_sha:
            raise ValueError(f"{case_id}: benchmark input hash conflict")


def _validate_official_preflight_inputs(repo_root: Path) -> tuple[Path, ...]:
    receipt_path = repo_root / _OFFICIAL_FV1_RELATIVE / "preflight/preflight-receipt.json"
    payload = _read_json(receipt_path)
    captures = payload.get("captures") if isinstance(payload, dict) else None
    if not isinstance(captures, dict) or not captures:
        raise ValueError("official preflight receipt lacks capture input evidence")
    expected = {relative.as_posix(): relative for relative in _OFFICIAL_PREFLIGHT_INPUTS}
    referenced: set[str] = set()
    for capture_id, capture in captures.items():
        if not isinstance(capture, dict):
            raise ValueError(f"official preflight capture {capture_id} is malformed")
        relative_path = _portable_source_path(str(capture.get("prompt_path", "")))
        relative = expected.get(relative_path)
        if relative is None:
            raise ValueError(
                f"official preflight input path conflict for capture {capture_id}"
            )
        actual_sha = hash_file(repo_root / relative)
        if capture.get("prompt_sha256") != actual_sha:
            raise ValueError(
                f"official preflight input hash conflict for capture {capture_id}"
            )
        referenced.add(relative_path)
    if referenced != set(expected):
        raise ValueError("official preflight receipt does not reference both input files")
    return tuple(relative for relative in _OFFICIAL_PREFLIGHT_INPUTS)


def _validate_official_source_model_aliases(repo_root: Path) -> None:
    identities = {
        (hash_file(repo_root / relative), (repo_root / relative).stat().st_size)
        for relative in _OFFICIAL_SOURCE_MODEL_ALIASES
    }
    if len(identities) != 1:
        raise ValueError("official source-model alias content conflict")


def _validate_official_tables(
    fv2_detailed: list[dict[str, str]],
    fv2_comparison: list[dict[str, str]],
    fv2_coverage: list[dict[str, str]],
    fv2_rows_payload: object,
    fv1_detailed: list[dict[str, str]],
    fv1_comparison: list[dict[str, str]],
    fv1_coverage: list[dict[str, str]],
    fv1_quality: list[dict[str, str]],
    repo_root: Path,
) -> tuple[Path, ...]:
    required_evidence = (
        *_OFFICIAL_PREFLIGHT_INPUTS,
        _OFFICIAL_BENCHMARK_INPUT,
        *_OFFICIAL_SOURCE_MODEL_ALIASES,
        _OFFICIAL_CONVERSION_LOG_RELATIVE,
    )
    for relative in required_evidence:
        if not (repo_root / relative).is_file():
            raise FileNotFoundError(f"required official evidence is missing: {relative.as_posix()}")
    preflight_inputs = _validate_official_preflight_inputs(repo_root)
    _validate_official_source_model_aliases(repo_root)
    if len(fv2_detailed) != 45 or len({row["case_id"] for row in fv2_detailed}) != 45:
        raise ValueError("official fv2 detailed results must contain 45 unique cases")
    expected_statuses = Counter(
        {
            ("passed", True): 15,
            ("conversion_failed", False): 5,
            ("hardware_preflight_blocked", False): 25,
        }
    )
    status_counts = Counter(
        (row["status"], _bool(row["executed"])) for row in fv2_detailed
    )
    if status_counts != expected_statuses:
        raise ValueError(
            "official fv2 final status accounting differs from 15 passed, "
            f"5 conversion_failed, 25 hardware_preflight_blocked: {status_counts}"
        )
    if {row["cache_codec"] for row in fv2_detailed} != {
        "tbq3",
        "u4",
        "tbq4",
        "u8",
        "f16",
    }:
        raise ValueError("official fv2 cache-format matrix differs from the frozen campaign")
    if any(
        "polar" in row["cache_codec"].lower() or "qjl" in row["cache_codec"].lower()
        for row in fv2_detailed
    ):
        raise ValueError("official fv2 unexpectedly contains PolarQuant or QJL")
    for row in fv2_detailed:
        _validate_official_cache_source_row(row, "fv2")

    if not isinstance(fv2_rows_payload, dict) or not isinstance(
        fv2_rows_payload.get("rows"), list
    ):
        raise ValueError("official fv2 rows.json does not contain a rows array")
    fv2_json_rows = fv2_rows_payload["rows"]
    by_case = {row["case_id"]: row for row in fv2_detailed}
    if len(fv2_json_rows) != 45 or {row.get("case_id") for row in fv2_json_rows} != set(by_case):
        raise ValueError("official fv2 rows.json does not preserve the 45-case matrix")
    row_fields = (
        "model",
        "weight_precision",
        "cache_codec",
        "status",
        "failure_stage",
        "failure_reason",
        "openvino_url",
        "openvino_commit",
        "openvino_genai_url",
        "openvino_genai_commit",
        "turboquant_merge_commit",
    )
    for payload_row in fv2_json_rows:
        source = by_case[str(payload_row["case_id"])]
        for field in row_fields:
            if payload_row.get(field) != (source[field] or None):
                raise ValueError(
                    f"official fv2 rows.json conflict for {source['case_id']} field {field}"
                )
        if bool(payload_row.get("executed")) != _bool(source["executed"]):
            raise ValueError(
                f"official fv2 rows.json execution conflict for {source['case_id']}"
            )

    numeric_fields = {
        "decode_tps",
        "ttft_ms",
        "peak_working_set_mb",
        "kv_mb",
        "quality_score",
    }
    comparison_by_key = {
        (row["model"], row["weight_precision"], row["cache_codec"]): row
        for row in fv2_comparison
    }
    if len(fv2_comparison) != 45 or len(comparison_by_key) != 45:
        raise ValueError("official fv2 comparison must contain 45 unique configurations")
    for source in fv2_detailed:
        key = (source["model"], source["weight_precision"], source["cache_codec"])
        if source["case_id"] != "__".join(key):
            raise ValueError(f"official fv2 case identity mismatch: {source['case_id']}")
        compared = comparison_by_key.get(key)
        if compared is None:
            raise ValueError(f"official fv2 comparison is missing {source['case_id']}")
        for field in (
            "model",
            "weight_precision",
            "cache_codec",
            "status",
            "executed",
            "decode_tps",
            "ttft_ms",
            "peak_working_set_mb",
            "kv_mb",
            "quality_score",
            "failure_reason",
        ):
            source_value = _source_value(
                source[field],
                numeric=field in numeric_fields,
                boolean=field == "executed",
            )
            compared_value = _source_value(
                compared[field],
                numeric=field in numeric_fields,
                boolean=field == "executed",
            )
            if source_value != compared_value:
                raise ValueError(
                    f"official fv2 comparison conflict for {source['case_id']} field {field}"
                )

    terminal_observation_fields = (
        "model_path",
        "model_sha256",
        "model_size_bytes",
        "context_tokens",
        "max_new_tokens",
        "load_ms",
        "ttft_ms",
        "prompt_tps",
        "tpot_ms",
        "decode_tps",
        "generation_duration_ms",
        "peak_working_set_mb",
        "peak_private_mb",
        "available_ram_min_mb",
        "kv_mb",
        "quality_score",
        "quality_prompt_count",
        "raw_result_path",
        "raw_result_sha256",
    )
    for row in fv2_detailed:
        if not _bool(row["executed"]):
            if any(row[field] != "" for field in terminal_observation_fields):
                raise ValueError(
                    f"{row['case_id']}: non-passed fv2 row contains published observations"
                )
            manifest_path = repo_root / _official_terminal_manifest_relative(row)
            if not manifest_path.is_file():
                raise FileNotFoundError(
                    f"missing final missing-model manifest: {manifest_path}"
                )
            manifest = _read_json(manifest_path)
            if not isinstance(manifest, dict):
                raise ValueError(f"{row['case_id']}: malformed final manifest")
            expected_model_weight = f"{row['model']}__{row['weight_precision']}"
            if manifest.get("case_id") != expected_model_weight:
                raise ValueError(f"{row['case_id']}: manifest identity conflict")
            if manifest.get("status") != row["status"]:
                raise ValueError(f"{row['case_id']}: manifest final status conflict")
            if manifest.get("failure_stage") != row["failure_stage"]:
                raise ValueError(f"{row['case_id']}: manifest failure stage conflict")
            if manifest.get("failure_reason") != row["failure_reason"]:
                raise ValueError(f"{row['case_id']}: manifest failure reason conflict")

    expected_coverage: dict[str, tuple[int, int, int, int]] = defaultdict(
        lambda: (0, 0, 0, 0)
    )
    for row in fv2_detailed:
        planned, recorded, executed, terminal = expected_coverage[row["model"]]
        did_execute = int(_bool(row["executed"]))
        expected_coverage[row["model"]] = (
            planned + 1,
            recorded + 1,
            executed + did_execute,
            terminal + (1 - did_execute),
        )
    observed_coverage = {
        row["model"]: (
            int(row["planned_cases"]),
            int(row["recorded_cases"]),
            int(row["executed_cases"]),
            int(row["terminal_cases"]),
        )
        for row in fv2_coverage
    }
    if len(fv2_coverage) != 3 or observed_coverage != dict(expected_coverage):
        raise ValueError("official fv2 coverage does not reconcile with detailed results")
    if any(int(row["missing_cases"]) != 0 for row in fv2_coverage):
        raise ValueError("official fv2 coverage reports missing cases")

    if len(fv1_detailed) != 45 or len({row["case_id"] for row in fv1_detailed}) != 45:
        raise ValueError("official fv1 detailed results must contain 45 unique cases")
    for row in fv1_detailed:
        _validate_official_cache_source_row(row, "fv1")
    fv1_by_case = {row["case_id"]: row for row in fv1_detailed}
    if set(fv1_by_case) != set(by_case):
        raise ValueError("official fv1/fv2 case matrices differ")
    fv1_passed = {
        row["case_id"]
        for row in fv1_detailed
        if row["status"] == "passed" and _bool(row["executed"])
    }
    fv2_passed = {
        row["case_id"]
        for row in fv2_detailed
        if row["status"] == "passed" and _bool(row["executed"])
    }
    if fv1_passed != fv2_passed or len(fv1_passed) != 15:
        raise ValueError("official passed fv2 cases do not exactly match fv1 passed evidence")
    for case_id in sorted(fv2_passed):
        row = fv1_by_case[case_id]
        if not row["raw_result_path"] or not row["raw_result_sha256"]:
            raise ValueError(f"{case_id}: passed fv2 case lacks fv1 raw evidence")
        fv2_row = by_case[case_id]
        for field in fv2_row:
            if field in {"status", "executed", "failure_stage", "failure_reason"}:
                continue
            if row[field] != fv2_row[field]:
                raise ValueError(
                    f"{case_id}: passed fv2 value differs from fv1 field {field}"
                )

    fv1_comparison_by_key = {
        (row["model"], row["weight_precision"], row["cache_codec"]): row
        for row in fv1_comparison
    }
    if len(fv1_comparison) != 45 or len(fv1_comparison_by_key) != 45:
        raise ValueError("official fv1 comparison must contain 45 unique configurations")
    for source in fv1_detailed:
        key = (source["model"], source["weight_precision"], source["cache_codec"])
        compared = fv1_comparison_by_key.get(key)
        if compared is None:
            raise ValueError(f"official fv1 comparison is missing {source['case_id']}")
        for field in (
            "model",
            "weight_precision",
            "cache_codec",
            "status",
            "executed",
            "decode_tps",
            "ttft_ms",
            "peak_working_set_mb",
            "kv_mb",
            "quality_score",
            "failure_reason",
        ):
            source_value = _source_value(
                source[field], numeric=field in numeric_fields, boolean=field == "executed"
            )
            compared_value = _source_value(
                compared[field], numeric=field in numeric_fields, boolean=field == "executed"
            )
            if source_value != compared_value:
                raise ValueError(
                    f"official fv1 comparison conflict for {source['case_id']} field {field}"
                )
    if _read_csv(repo_root / _OFFICIAL_FV1_RELATIVE / "official-openvino-coverage.csv") != fv1_coverage:
        raise ValueError("official fv1 coverage read is not stable")
    if fv1_coverage != fv2_coverage:
        raise ValueError("official fv1/fv2 coverage differs")
    quality_counts = Counter((row["case_id"], row["prompt_id"]) for row in fv1_quality)
    if len(fv1_quality) != 2_160 or {case for case, _ in quality_counts} != fv1_passed:
        raise ValueError("official fv1 quality evidence does not cover the 15 passed cases")
    if set(quality_counts.values()) != {3}:
        raise ValueError("official fv1 quality evidence must have three criteria per prompt")
    if set(Counter(case for case, _ in quality_counts).values()) != {48}:
        raise ValueError("official fv1 quality evidence must have 48 prompts per passed case")
    return preflight_inputs


def build_official_bundle(repo_root: Path) -> RouteBundle:
    """Join fv2 final status authority to fv1 evidence for fv2-passed cases."""
    root = Path(repo_root).resolve(strict=True)
    fv1 = root / _OFFICIAL_FV1_RELATIVE
    consolidated = root / _OFFICIAL_FV2_RELATIVE / "consolidated"
    fv2_detailed_path = consolidated / "official-openvino-detailed-results.csv"
    fv2_comparison_path = consolidated / "official-openvino-comparison.csv"
    fv2_coverage_path = consolidated / "official-openvino-coverage.csv"
    fv2_rows_path = consolidated / "rows.json"
    fv1_detailed_path = fv1 / "official-openvino-detailed-results.csv"
    fv1_comparison_path = fv1 / "official-openvino-comparison.csv"
    fv1_coverage_path = fv1 / "official-openvino-coverage.csv"
    fv1_quality_path = fv1 / "experimental-openvino-quality-details.csv"
    fv1_rows_path = fv1 / "rows.json"

    fv2_detailed = _read_csv(fv2_detailed_path)
    fv2_comparison = _read_csv(fv2_comparison_path)
    fv2_coverage = _read_csv(fv2_coverage_path)
    fv2_rows_payload = _read_json(fv2_rows_path)
    fv1_detailed = _read_csv(fv1_detailed_path)
    fv1_comparison = _read_csv(fv1_comparison_path)
    fv1_coverage = _read_csv(fv1_coverage_path)
    fv1_quality = _read_csv(fv1_quality_path)
    preflight_input_relatives = _validate_official_tables(
        fv2_detailed,
        fv2_comparison,
        fv2_coverage,
        fv2_rows_payload,
        fv1_detailed,
        fv1_comparison,
        fv1_coverage,
        fv1_quality,
        root,
    )

    evidence: list[EvidenceRecord] = []
    preflight_input_records = [
        _record_official_evidence(
            root,
            root / relative,
            "preflight-input",
            f"fv1 preflight input {relative.name}",
            evidence,
        )
        for relative in preflight_input_relatives
    ]
    benchmark_input_record = _record_official_evidence(
        root,
        root / _OFFICIAL_BENCHMARK_INPUT,
        "benchmark-prompt-input",
        "fv1 shared benchmark prompt input",
        evidence,
    )
    core_sources: dict[str, EvidenceRecord] = {}
    for key, path, role, label in (
        ("fv2-detailed", fv2_detailed_path, "source-results", "fv2 final detailed results"),
        ("fv2-comparison", fv2_comparison_path, "source-results", "fv2 final comparison results"),
        ("fv2-coverage", fv2_coverage_path, "source-coverage", "fv2 final coverage"),
        ("fv2-rows", fv2_rows_path, "source-ledger", "fv2 final source rows"),
        ("fv1-detailed", fv1_detailed_path, "prior-source-results", "fv1 passed measurement index"),
        ("fv1-comparison", fv1_comparison_path, "prior-source-results", "fv1 comparison index"),
        ("fv1-coverage", fv1_coverage_path, "prior-source-coverage", "fv1 coverage index"),
        ("fv1-quality", fv1_quality_path, "source-quality", "fv1 quality details"),
        ("fv1-rows", fv1_rows_path, "prior-source-ledger", "fv1 source rows"),
        ("fv1-audit", fv1 / "audit-report.json", "source-validation", "fv1 audit report"),
        ("fv1-preflight", fv1 / "preflight/preflight-receipt.json", "source-preflight", "fv1 TurboQuant preflight"),
        ("fv2-workbook", root / _OFFICIAL_V2_WORKBOOK_RELATIVE, "source-workbook", "fv2 revised final workbook"),
        ("fv1-workbook", root / _OFFICIAL_V1_WORKBOOK_RELATIVE, "indexed-prior-workbook", "fv1 original workbook"),
    ):
        input_ids = (
            tuple(item.evidence_id for item in preflight_input_records)
            if key == "fv1-preflight"
            else ()
        )
        core_sources[key] = _record_official_evidence(
            root,
            path,
            role,
            label,
            evidence,
            input_evidence_ids=input_ids,
        )

    manifest_evidence_by_path: dict[str, EvidenceRecord] = {}
    indexed_digests = {item.sha256 for item in evidence}
    fv2_root = root / _OFFICIAL_FV2_RELATIVE
    for path in sorted(fv2_root.rglob("*.json")):
        relative = path.relative_to(root).as_posix()
        if relative in {item.relative_path for item in evidence}:
            continue
        digest = hash_file(path)
        if digest in indexed_digests:
            continue
        if path.name == "manifest.json":
            role = "missing-model-attempt-manifest"
        elif path.name == "attempt-summary.json":
            role = "missing-model-attempt-summary"
        elif path.name == "source-models.json":
            role = "missing-model-source-inventory"
        else:
            role = "missing-model-evidence"
        record = _record_official_evidence(
            root,
            path,
            role,
            f"fv2 missing-model evidence {path.relative_to(fv2_root).as_posix()}",
            evidence,
        )
        manifest_evidence_by_path[relative] = record
        indexed_digests.add(record.sha256)

    conversion_log_record = _record_official_evidence(
        root,
        root / _OFFICIAL_CONVERSION_LOG_RELATIVE,
        "conversion-log",
        "fv2 guarded-retry-002 conversion log",
        evidence,
    )

    fv1_by_case = {row["case_id"]: row for row in fv1_detailed}
    quality_by_case_prompt: dict[tuple[str, str], list[dict[str, str]]] = defaultdict(list)
    for row in fv1_quality:
        quality_by_case_prompt[(row["case_id"], row["prompt_id"])].append(row)

    attempts: list[AttemptRecord] = []
    failures: list[FailureRecord] = []
    measurements: list[MeasurementRecord] = []
    summaries: list[SummaryRecord] = []
    raw_by_case: dict[str, Mapping[str, object]] = {}
    raw_evidence_by_case: dict[str, EvidenceRecord] = {}
    input_evidence_by_path: dict[str, EvidenceRecord] = {}
    runtime_versions: set[str] = set()
    runtime_properties: set[tuple[tuple[str, str], ...]] = set()

    for row in fv2_detailed:
        case_id = row["case_id"]
        attempt_id = _attempt_id(case_id)
        executed = _bool(row["executed"])
        status = Status.from_source(row["status"])
        attempt_evidence_ids = [
            core_sources["fv2-detailed"].evidence_id,
            core_sources["fv2-rows"].evidence_id,
        ]
        if executed:
            fv1_row = fv1_by_case[case_id]
            raw_relative = _portable_source_path(fv1_row["raw_result_path"])
            if not raw_relative:
                raise ValueError(f"{case_id}: passed fv2 case lacks fv1 raw evidence")
            raw_path = root / Path(raw_relative)
            if not raw_path.is_file():
                raise ValueError(f"{case_id}: passed fv2 raw evidence file is missing")
            if hash_file(raw_path) != fv1_row["raw_result_sha256"]:
                raise ValueError(f"{case_id}: fv1 raw-result hash conflict")
            raw_record = _record_official_evidence(
                root,
                raw_path,
                "raw-case-result",
                f"fv1 raw result {case_id}",
                evidence,
                input_evidence_ids=(benchmark_input_record.evidence_id,),
            )
            raw_payload = _read_json(raw_path)
            if not isinstance(raw_payload, dict) or raw_payload.get("case_id") != case_id:
                raise ValueError(f"{case_id}: fv1 raw-result identity mismatch")
            _validate_official_raw_cache_semantics(
                case_id, row["cache_codec"], raw_payload, root
            )
            raw_by_case[case_id] = raw_payload
            raw_evidence_by_case[case_id] = raw_record
            attempt_evidence_ids.extend(
                [core_sources["fv1-detailed"].evidence_id, raw_record.evidence_id]
            )
            case_measurements, case_summaries = _case_measurements(
                case_id,
                attempt_id,
                raw_payload,
                raw_record.evidence_id,
                route_id=_OFFICIAL_ROUTE_ID,
                campaign_id=_OFFICIAL_CAMPAIGN_ID,
            )
            measurements.extend(case_measurements)
            summaries.extend(case_summaries)
            selected = raw_payload.get("benchmark")
            if not isinstance(selected, dict) or not isinstance(selected.get("result"), dict):
                raise ValueError(f"{case_id}: missing selected fv1 benchmark")
            if raw_payload.get("benchmark_selection") != (
                "median decode_tps among executed repetitions"
            ):
                raise ValueError(f"{case_id}: unsupported fv1 benchmark selection rule")
            selected_result = selected["result"]
            expected_summary = {item.metric_name: item.value for item in case_summaries}
            if float(fv1_row["decode_tps"]) != expected_summary["generation_tokens_per_second"]:
                raise ValueError(f"{case_id}: fv1 decode is not the repetition median")
            if float(fv1_row["ttft_ms"]) != expected_summary["time_to_first_token"]:
                raise ValueError(f"{case_id}: fv1 TTFT is not from the selected repetition")
            if _working_set_bytes(fv1_row["peak_working_set_mb"]) != expected_summary["peak_working_set_bytes"]:
                raise ValueError(f"{case_id}: fv1 peak memory is not worst-observed")
            for source_field, raw_field in (
                ("load_ms", "load_ms"),
                ("ttft_ms", "ttft_ms"),
                ("tpot_ms", "tpot_ms"),
                ("decode_tps", "decode_tps"),
                ("generation_duration_ms", "generation_duration_ms"),
                ("context_tokens", "input_tokens"),
                ("max_new_tokens", "max_new_tokens"),
            ):
                if float(fv1_row[source_field]) != float(selected_result[raw_field]):
                    raise ValueError(
                        f"{case_id}: fv1 selected benchmark {source_field} conflict"
                    )
            raw_runs = raw_payload["benchmark_runs"]
            for source_field, expected in (
                ("peak_working_set_mb", max(float(run["peak_working_set_mb"]) for run in raw_runs)),
                ("peak_private_mb", max(float(run["peak_private_mb"]) for run in raw_runs)),
                ("available_ram_min_mb", min(float(run["available_ram_min_mb"]) for run in raw_runs)),
            ):
                if float(fv1_row[source_field]) != expected:
                    raise ValueError(f"{case_id}: fv1 repetition {source_field} conflict")
            if raw_payload.get("model_size_bytes") != int(fv1_row["model_size_bytes"]):
                raise ValueError(f"{case_id}: fv1 model size conflict")
            official_sources = raw_payload.get("official_sources")
            expected_sources = {
                "openvino": {
                    "url": fv1_row["openvino_url"],
                    "commit": fv1_row["openvino_commit"],
                },
                "openvino_genai": {
                    "url": fv1_row["openvino_genai_url"],
                    "commit": fv1_row["openvino_genai_commit"],
                },
                "turboquant_merge_commit": fv1_row["turboquant_merge_commit"],
            }
            if official_sources != expected_sources:
                raise ValueError(f"{case_id}: fv1 official repository identity conflict")
            version = selected_result.get("openvino_version")
            if isinstance(version, str):
                runtime_versions.add(version)
            properties = raw_payload.get("runtime_properties")
            if isinstance(properties, dict):
                runtime_properties.add(
                    tuple(sorted((str(key), str(value)) for key, value in properties.items()))
                )
        else:
            terminal_relative = _official_terminal_manifest_relative(row).as_posix()
            manifest_record = manifest_evidence_by_path.get(terminal_relative)
            if manifest_record is None:
                raise ValueError(f"{case_id}: final manifest evidence was not indexed")
            attempt_evidence_ids.append(manifest_record.evidence_id)
            if row["model"] == "granite-3b" and row["weight_precision"] == "fp16":
                attempt_evidence_ids.append(conversion_log_record.evidence_id)
            failures.append(
                FailureRecord(
                    route_id=_OFFICIAL_ROUTE_ID,
                    campaign_id=_OFFICIAL_CAMPAIGN_ID,
                    test_case_id=case_id,
                    attempt_id=attempt_id,
                    failure_id=f"{case_id}--{row['status']}",
                    status=status,
                    stage=row["failure_stage"],
                    reason=row["failure_reason"],
                    source_status=row["status"],
                    evidence_ids=tuple(attempt_evidence_ids),
                )
            )
        attempts.append(
            AttemptRecord(
                route_id=_OFFICIAL_ROUTE_ID,
                campaign_id=_OFFICIAL_CAMPAIGN_ID,
                test_case_id=case_id,
                attempt_id=attempt_id,
                status=status,
                executed=executed,
                reason=row["failure_reason"],
                model_id=row["model"],
                weight_format_id=row["weight_precision"],
                cache_format_id=row["cache_codec"],
                source_status=row["status"],
                failure_kind=row["failure_stage"] or None,
                evidence_ids=tuple(attempt_evidence_ids),
            )
        )

    quality_schemas = {
        run.get("quality", {}).get("schema")
        for payload in raw_by_case.values()
        for run in payload.get("quality_runs", [])
        if isinstance(run, dict) and isinstance(run.get("quality"), dict)
    }
    if quality_schemas != {_OFFICIAL_QUALITY_SCHEMA}:
        raise ValueError(
            "official fv1 quality scoring schema is not uniform and expected: "
            f"{sorted(str(value) for value in quality_schemas)}"
        )
    quality: list[QualityRecord] = []
    for row in fv1_quality:
        raw_record = raw_evidence_by_case[row["case_id"]]
        quality.append(
            QualityRecord(
                route_id=_OFFICIAL_ROUTE_ID,
                campaign_id=_OFFICIAL_CAMPAIGN_ID,
                test_case_id=row["case_id"],
                quality_id=_quality_id(
                    row["case_id"], row["prompt_id"], row["category"], row["criterion_id"]
                ),
                prompt_id=row["prompt_id"],
                criterion_id=f"{row['category']}:{row['criterion_id']}",
                score=float(row["points_awarded"]),
                maximum_score=float(row["weight"]),
                prompt_suite_id=row["prompt_set_id"],
                rubric_id="objective-quality-weighted-5-3-2-output-health-gate",
                scoring_version=_OFFICIAL_QUALITY_SCHEMA,
                source_evidence_id=raw_record.evidence_id,
            )
        )
    fv1_by_case = {row["case_id"]: row for row in fv1_detailed}
    for case_id, raw_payload in raw_by_case.items():
        source_by_prompt = {
            prompt_id: rows
            for (quality_case, prompt_id), rows in quality_by_case_prompt.items()
            if quality_case == case_id
        }
        _validate_raw_quality_case(case_id, raw_payload, source_by_prompt, fv1_by_case[case_id])
        for run in raw_payload["quality_runs"]:
            prompt_relative = _portable_source_path(str(run["prompt_path"]))
            prompt_path = root / Path(prompt_relative)
            if hash_file(prompt_path) != run["prompt_sha256"]:
                raise ValueError(f"{case_id}/{run['prompt_id']}: fv1 prompt hash conflict")
            if prompt_relative not in input_evidence_by_path:
                input_evidence_by_path[prompt_relative] = _record_official_evidence(
                    root,
                    prompt_path,
                    "quality-prompt-input",
                    f"fv1 prompt input {run['prompt_id']}",
                    evidence,
                )

    repositories = {
        "openvino": {
            "url": _ensure_uniform(fv2_detailed, "openvino_url"),
            "commit": _ensure_uniform(fv2_detailed, "openvino_commit"),
        },
        "openvino_genai": {
            "url": _ensure_uniform(fv2_detailed, "openvino_genai_url"),
            "commit": _ensure_uniform(fv2_detailed, "openvino_genai_commit"),
        },
        "turboquant_merge_commit": _ensure_uniform(
            fv2_detailed, "turboquant_merge_commit"
        ),
        "source_campaign": "fv2 status joined to fv1 passed evidence",
        "source_date": "2026-08-30",
    }
    terminal_manifests = [
        _read_json(root / _official_terminal_manifest_relative(row))
        for row in fv2_detailed
        if not _bool(row["executed"])
    ]
    unique_preflights = {
        str(item["case_id"]): item["preflight"]
        for item in terminal_manifests
        if isinstance(item, dict) and isinstance(item.get("preflight"), dict)
    }
    hardware = {
        "status": "source-recorded",
        "emergency_ram_floor_bytes": 2_147_483_648,
        "conversion_preflight_by_model_weight": unique_preflights,
    }
    tool_versions = {
        key: value
        for payload in terminal_manifests
        if isinstance(payload, dict)
        for key, value in dict(payload.get("tool_versions", {})).items()
    }
    software = {
        "openvino_versions": sorted(runtime_versions),
        "runtime_properties": [dict(items) for items in sorted(runtime_properties)],
        "missing_model_attempt_tool_versions": tool_versions,
    }
    return RouteBundle(
        route_id=_OFFICIAL_ROUTE_ID,
        campaign_id=_OFFICIAL_CAMPAIGN_ID,
        attempts=tuple(attempts),
        measurements=tuple(measurements),
        summaries=tuple(summaries),
        quality=tuple(quality),
        failures=tuple(failures),
        evidence=tuple(evidence),
        repository=repositories,
        hardware=hardware,
        software=software,
    )


def _csv_rows(records: Iterable[object]) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    for record in records:
        row = record.to_row()
        rows.append(
            {
                key: json.dumps(value, ensure_ascii=False, separators=(",", ":"))
                if isinstance(value, (list, dict))
                else value
                for key, value in row.items()
            }
        )
    return rows


def _copy_exact(source: Path, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    if hash_file(source) != hash_file(destination):
        raise OSError(f"byte-for-byte copy validation failed: {destination}")


def _prompt_and_output_rows(
    repo_root: Path,
    bundle: RouteBundle,
) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    raw_evidence = {
        record.source_label.removeprefix("fv6 raw result "): record
        for record in bundle.evidence
        if record.role == "raw-case-result" and record.source_label is not None
    }
    input_evidence = {
        record.relative_path: record
        for record in bundle.evidence
        if record.role == "quality-prompt-input"
    }
    prompts: dict[str, dict[str, object]] = {}
    outputs: list[dict[str, object]] = []
    for case_id, evidence_record in sorted(raw_evidence.items()):
        payload = _read_json(repo_root / Path(evidence_record.relative_path))
        if not isinstance(payload, dict):
            raise ValueError(f"{case_id}: raw quality evidence is not an object")
        for run in payload["quality_runs"]:
            prompt_path = _portable_source_path(str(run["prompt_path"]))
            prompt_record = input_evidence[prompt_path]
            prompt_id = str(run["prompt_id"])
            candidate = {
                "prompt_suite_id": str(payload["quality_prompt_set_id"]),
                "prompt_id": prompt_id,
                "domain": str(run["domain"]),
                "prompt_length": str(run["prompt_length"]),
                "input_evidence_id": prompt_record.evidence_id,
                "relative_path": prompt_record.relative_path,
                "sha256": str(run["prompt_sha256"]),
            }
            if prompt_id in prompts and prompts[prompt_id] != candidate:
                raise ValueError(f"prompt definition differs between cases: {prompt_id}")
            prompts[prompt_id] = candidate
            quality = run["quality"]
            result = run["result"]
            answer = str(result.get("text", ""))
            outputs.append(
                {
                    "output_id": _output_id(case_id, prompt_id),
                    "test_case_id": case_id,
                    "prompt_id": prompt_id,
                    "domain": str(run["domain"]),
                    "prompt_length": str(run["prompt_length"]),
                    "status": str(result["status"]),
                    "valid_output": bool(quality["valid_output"]),
                    "critical_failure": bool(quality["critical_failure"]),
                    "prompt_score": float(quality["score"]),
                    "output_sha256": hashlib.sha256(answer.encode("utf-8")).hexdigest(),
                    "source_evidence_id": evidence_record.evidence_id,
                }
            )
    return [prompts[key] for key in sorted(prompts)], outputs


def _official_prompt_and_output_rows(
    repo_root: Path,
    bundle: RouteBundle,
) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    raw_evidence = {
        record.source_label.removeprefix("fv1 raw result "): record
        for record in bundle.evidence
        if record.role == "raw-case-result" and record.source_label is not None
    }
    input_evidence = {
        record.relative_path: record
        for record in bundle.evidence
        if record.role == "quality-prompt-input"
    }
    prompts: dict[str, dict[str, object]] = {}
    outputs: list[dict[str, object]] = []
    for case_id, evidence_record in sorted(raw_evidence.items()):
        payload = _read_json(repo_root / Path(evidence_record.relative_path))
        if not isinstance(payload, dict):
            raise ValueError(f"{case_id}: official raw quality evidence is not an object")
        for run in payload["quality_runs"]:
            prompt_path = _portable_source_path(str(run["prompt_path"]))
            prompt_record = input_evidence[prompt_path]
            prompt_id = str(run["prompt_id"])
            candidate = {
                "prompt_suite_id": str(payload["quality_prompt_set_id"]),
                "prompt_id": prompt_id,
                "domain": str(run["domain"]),
                "prompt_length": str(run["prompt_length"]),
                "input_evidence_id": prompt_record.evidence_id,
                "relative_path": prompt_record.relative_path,
                "sha256": str(run["prompt_sha256"]),
            }
            if prompt_id in prompts and prompts[prompt_id] != candidate:
                raise ValueError(f"official prompt definition differs between cases: {prompt_id}")
            prompts[prompt_id] = candidate
            quality = run["quality"]
            result = run["result"]
            answer = str(result.get("text", ""))
            outputs.append(
                {
                    "output_id": _output_id(case_id, prompt_id),
                    "test_case_id": case_id,
                    "prompt_id": prompt_id,
                    "domain": str(run["domain"]),
                    "prompt_length": str(run["prompt_length"]),
                    "status": str(result["status"]),
                    "valid_output": bool(quality["valid_output"]),
                    "critical_failure": bool(quality["critical_failure"]),
                    "prompt_score": float(quality["score"]),
                    "output_sha256": hashlib.sha256(answer.encode("utf-8")).hexdigest(),
                    "source_evidence_id": evidence_record.evidence_id,
                }
            )
    return [prompts[key] for key in sorted(prompts)], outputs


def _model_artifact_rows(repo_root: Path) -> list[dict[str, object]]:
    detailed = _read_csv(repo_root / _FV6_RELATIVE / _DETAILED_NAME)
    grouped: dict[tuple[str, str], list[dict[str, str]]] = defaultdict(list)
    for row in detailed:
        grouped[(row["model"], row["weight_precision"])].append(row)
    rows: list[dict[str, object]] = []
    for (model_id, weight_id), source_rows in sorted(grouped.items()):
        executed = [row for row in source_rows if _bool(row["executed"])]
        hashes = {row["model_sha256"] for row in executed}
        sizes = {row["model_size_bytes"] for row in executed}
        model_names = {
            PureWindowsPath(row["model_path"]).name for row in executed if row["model_path"]
        }
        if len(hashes) > 1 or len(sizes) > 1 or len(model_names) > 1:
            raise ValueError(f"model artifact identity differs across {model_id}/{weight_id}")
        rows.append(
            {
                "model_id": model_id,
                "weight_format_id": weight_id,
                "status": "available" if executed else "artifact_unavailable",
                "executed_case_count": len(executed),
                "artifact_label": next(iter(model_names), ""),
                "sha256": next(iter(hashes), ""),
                "size_bytes": int(next(iter(sizes))) if sizes else "",
                "reason": "" if executed else source_rows[0]["failure_reason"],
            }
        )
    return rows


def _official_model_artifact_rows(repo_root: Path) -> list[dict[str, object]]:
    fv2 = _read_csv(
        repo_root
        / _OFFICIAL_FV2_RELATIVE
        / "consolidated/official-openvino-detailed-results.csv"
    )
    fv1_by_case = {
        row["case_id"]: row
        for row in _read_csv(
            repo_root / _OFFICIAL_FV1_RELATIVE / "official-openvino-detailed-results.csv"
        )
    }
    grouped: dict[tuple[str, str], list[dict[str, str]]] = defaultdict(list)
    for row in fv2:
        grouped[(row["model"], row["weight_precision"])].append(row)
    rows: list[dict[str, object]] = []
    for (model_id, weight_id), source_rows in sorted(grouped.items()):
        executed = [row for row in source_rows if _bool(row["executed"])]
        statuses = {row["status"] for row in source_rows}
        reasons = {row["failure_reason"] for row in source_rows}
        if len(statuses) != 1 or len(reasons) != 1:
            raise ValueError(
                f"official model artifact outcome differs across {model_id}/{weight_id}"
            )
        fv1_rows = [fv1_by_case[row["case_id"]] for row in executed]
        hashes = {row["model_sha256"] for row in fv1_rows}
        sizes = {row["model_size_bytes"] for row in fv1_rows}
        model_names = {
            PureWindowsPath(row["model_path"]).name
            for row in fv1_rows
            if row["model_path"]
        }
        if len(hashes) > 1 or len(sizes) > 1 or len(model_names) > 1:
            raise ValueError(f"official model identity differs across {model_id}/{weight_id}")
        source_status = next(iter(statuses))
        rows.append(
            {
                "model_id": model_id,
                "weight_format_id": weight_id,
                "status": "available" if executed else Status.from_source(source_status).value,
                "executed_case_count": len(executed),
                "artifact_label": next(iter(model_names), ""),
                "sha256": next(iter(hashes), ""),
                "size_bytes": int(next(iter(sizes))) if sizes else "",
                "reason": "" if executed else next(iter(reasons)),
            }
        )
    return rows


def _official_source_location_rows(
    repo_root: Path, bundle: RouteBundle
) -> list[dict[str, object]]:
    rows = [
        {
            "evidence_id": item.evidence_id,
            "role": item.role,
            "relative_path": item.relative_path,
            "sha256": item.sha256,
            "size_bytes": item.size_bytes,
            "source_label": item.source_label,
        }
        for item in bundle.evidence
        if item.role != "missing-model-source-inventory"
    ]
    content = next(
        item
        for item in bundle.evidence
        if item.role == "missing-model-source-inventory"
    )
    for relative in _OFFICIAL_SOURCE_MODEL_ALIASES:
        path = repo_root / relative
        digest = hash_file(path)
        size = path.stat().st_size
        if digest != content.sha256 or size != content.size_bytes:
            raise ValueError(
                f"source-model alias content conflict: {relative.as_posix()}"
            )
        rows.append(
            {
                "evidence_id": content.evidence_id,
                "role": content.role,
                "relative_path": relative.as_posix(),
                "sha256": digest,
                "size_bytes": size,
                "source_label": (
                    "fv2 source-model inventory alias "
                    f"{relative.relative_to(_OFFICIAL_FV2_RELATIVE).as_posix()}"
                ),
            }
        )
    return sorted(rows, key=lambda row: (str(row["relative_path"]), str(row["evidence_id"])))


def _official_source_location_consistency_errors(
    repo_root: Path,
    bundle: RouteBundle,
    rows: Sequence[Mapping[str, object]],
) -> list[str]:
    errors: list[str] = []
    evidence_by_id = {item.evidence_id: item for item in bundle.evidence}
    alias_paths = {relative.as_posix() for relative in _OFFICIAL_SOURCE_MODEL_ALIASES}
    allowed_paths = {item.relative_path for item in bundle.evidence} | alias_paths
    seen_paths: set[str] = set()
    observed_aliases: list[tuple[str, str, str, int]] = []
    for row in rows:
        relative = str(row.get("relative_path", ""))
        evidence_id = str(row.get("evidence_id", ""))
        role = str(row.get("role", ""))
        digest = str(row.get("sha256", ""))
        try:
            size = int(row.get("size_bytes", 0))
        except (TypeError, ValueError):
            errors.append(f"{relative}: invalid source-location size")
            continue
        if relative in seen_paths:
            errors.append(f"{relative}: duplicate source-location path")
        seen_paths.add(relative)
        record = evidence_by_id.get(evidence_id)
        if record is None:
            errors.append(f"{relative}: source-location evidence ID is not indexed")
            continue
        if relative not in allowed_paths:
            errors.append(f"{relative}: source-location path is not indexed")
            continue
        if role != record.role or digest != record.sha256 or size != record.size_bytes:
            errors.append(f"{relative}: source-location differs from evidence record")
        is_alias = relative in alias_paths
        if is_alias:
            observed_aliases.append((relative, evidence_id, digest, size))
            if role != "missing-model-source-inventory":
                errors.append(f"{relative}: source-model alias role conflict")
        elif relative != record.relative_path:
            errors.append(f"{relative}: source-location path differs from evidence record")
        physical = repo_root / Path(relative)
        if not physical.is_file():
            errors.append(f"{relative}: physical source-location file is missing")
            continue
        if hash_file(physical) != digest or physical.stat().st_size != size:
            errors.append(f"{relative}: physical source-location content conflict")
    if {item[0] for item in observed_aliases} != alias_paths:
        errors.append("source-model aliases do not preserve all three physical paths")
    if len({(item[1], item[2], item[3]) for item in observed_aliases}) != 1:
        errors.append("source-model aliases do not share one content evidence identity")
    return errors


def _availability_rows(bundle: RouteBundle) -> list[dict[str, object]]:
    return [
        {
            "test_case_id": item.test_case_id,
            "model_id": item.model_id,
            "weight_format_id": item.weight_format_id,
            "cache_format_id": item.cache_format_id,
            "status": item.status.value,
            "executed": item.executed,
            "reason": item.reason,
        }
        for item in bundle.attempts
    ]


def _receipt_check(actual: object, expected: object) -> dict[str, object]:
    return {"actual": actual, "expected": expected, "passed": actual == expected}


def _published_bool(value: object) -> bool:
    return value if isinstance(value, bool) else _bool(str(value))


def _published_prompt_entity(row: Mapping[str, object]) -> dict[str, str]:
    return {
        key: str(row.get(key, ""))
        for key in (
            "prompt_suite_id",
            "prompt_id",
            "domain",
            "prompt_length",
            "input_evidence_id",
            "relative_path",
            "sha256",
        )
    }


def _published_output_entity(row: Mapping[str, object]) -> dict[str, object]:
    return {
        "output_id": str(row.get("output_id", "")),
        "test_case_id": str(row.get("test_case_id", "")),
        "prompt_id": str(row.get("prompt_id", "")),
        "domain": str(row.get("domain", "")),
        "prompt_length": str(row.get("prompt_length", "")),
        "status": str(row.get("status", "")),
        "valid_output": _published_bool(row.get("valid_output", "")),
        "critical_failure": _published_bool(row.get("critical_failure", "")),
        "prompt_score": float(row.get("prompt_score", "")),
        "output_sha256": str(row.get("output_sha256", "")),
        "source_evidence_id": str(row.get("source_evidence_id", "")),
    }


def _published_availability_entity(row: Mapping[str, object]) -> dict[str, object]:
    return {
        "test_case_id": str(row.get("test_case_id", "")),
        "model_id": str(row.get("model_id", "")),
        "weight_format_id": str(row.get("weight_format_id", "")),
        "cache_format_id": str(row.get("cache_format_id", "")),
        "status": str(row.get("status", "")),
        "executed": _published_bool(row.get("executed", "")),
        "reason": str(row.get("reason", "")),
    }


def _published_model_artifact_entity(
    row: Mapping[str, object],
) -> dict[str, object]:
    size_value = row.get("size_bytes")
    return {
        "model_id": str(row.get("model_id", "")),
        "weight_format_id": str(row.get("weight_format_id", "")),
        "status": str(row.get("status", "")),
        "executed_case_count": int(row.get("executed_case_count", 0)),
        "artifact_label": str(row.get("artifact_label", "")),
        "sha256": str(row.get("sha256", "")),
        "size_bytes": None if size_value in (None, "") else int(size_value),
        "reason": str(row.get("reason", "")),
    }


def _entity_set_diagnostics(
    actual_rows: Sequence[Mapping[str, object]],
    expected_rows: Sequence[Mapping[str, object]],
    key_fields: tuple[str, ...],
) -> list[str]:
    def key(row: Mapping[str, object]) -> tuple[object, ...]:
        return tuple(row.get(field) for field in key_fields)

    actual_counts = Counter(key(row) for row in actual_rows)
    expected_counts = Counter(key(row) for row in expected_rows)
    diagnostics: list[str] = []
    for entity_key, count in sorted(actual_counts.items(), key=lambda item: repr(item[0])):
        if count != 1:
            diagnostics.append(f"actual duplicate {entity_key!r} count={count}")
    for entity_key, count in sorted(expected_counts.items(), key=lambda item: repr(item[0])):
        if count != 1:
            diagnostics.append(f"expected duplicate {entity_key!r} count={count}")
    for entity_key in sorted(
        set(expected_counts) - set(actual_counts), key=repr
    ):
        diagnostics.append(f"missing {entity_key!r}")
    for entity_key in sorted(
        set(actual_counts) - set(expected_counts), key=repr
    ):
        diagnostics.append(f"unexpected {entity_key!r}")

    actual_by_key = {
        key(row): row for row in actual_rows if actual_counts[key(row)] == 1
    }
    expected_by_key = {
        key(row): row for row in expected_rows if expected_counts[key(row)] == 1
    }
    for entity_key in sorted(set(actual_by_key) & set(expected_by_key), key=repr):
        actual = actual_by_key[entity_key]
        expected = expected_by_key[entity_key]
        differing_fields = sorted(
            field
            for field in set(actual) | set(expected)
            if actual.get(field) != expected.get(field)
        )
        if differing_fields:
            diagnostics.append(
                f"mismatch {entity_key!r} fields={','.join(differing_fields)}"
            )
    return diagnostics


def _frozen_validation_expectations(repo_root: Path) -> dict[str, object]:
    detailed = _read_csv(repo_root / _FV6_RELATIVE / _DETAILED_NAME)
    quality_rows = _read_csv(repo_root / _FV6_RELATIVE / _QUALITY_NAME)
    evidence_entities: dict[str, dict[str, object]] = {}
    used_evidence_ids: set[str] = set()

    def add_evidence(relative_path: Path | str, role: str, source_label: str) -> str:
        portable_path = Path(relative_path).as_posix()
        if portable_path in evidence_entities:
            return str(evidence_entities[portable_path]["evidence_id"])
        source_path = repo_root / Path(portable_path)
        digest = hash_file(source_path)
        evidence_id = _next_evidence_id(digest, used_evidence_ids)
        used_evidence_ids.add(evidence_id)
        evidence_entities[portable_path] = {
            "route_id": _EXPERIMENTAL_ROUTE_ID,
            "campaign_id": _EXPERIMENTAL_CAMPAIGN_ID,
            "evidence_id": evidence_id,
            "role": role,
            "relative_path": portable_path,
            "sha256": digest,
            "size_bytes": source_path.stat().st_size,
            "source_label": source_label,
            "derived": False,
            "input_evidence_ids": [],
        }
        return evidence_id

    source_evidence_ids = {
        "detailed": add_evidence(
            _FV6_RELATIVE / _DETAILED_NAME, "source-results", "fv6 detailed results"
        ),
        "comparison": add_evidence(
            _FV6_RELATIVE / _COMPARISON_NAME,
            "source-results",
            "fv6 comparison results",
        ),
        "coverage": add_evidence(
            _FV6_RELATIVE / _COVERAGE_NAME,
            "source-coverage",
            "fv6 coverage results",
        ),
        "quality": add_evidence(
            _FV6_RELATIVE / _QUALITY_NAME,
            "source-quality",
            "fv6 quality details",
        ),
        "rows": add_evidence(
            _FV6_RELATIVE / _ROWS_NAME,
            "source-ledger",
            "fv6 normalized source rows",
        ),
        "workbook": add_evidence(
            _EXPERIMENTAL_WORKBOOK_RELATIVE,
            "source-workbook",
            "fv6 final interactive workbook",
        ),
    }

    attempt_entities: dict[str, dict[str, object]] = {}
    failure_entities: dict[str, dict[str, object]] = {}
    measurement_entities: dict[str, dict[str, object]] = {}
    summary_entities: dict[str, dict[str, object]] = {}
    prompt_entities: dict[str, dict[str, object]] = {}
    output_entities: dict[tuple[str, str], dict[str, object]] = {}
    quality_entities: dict[tuple[str, str, str], dict[str, object]] = {}
    availability_entities: list[dict[str, object]] = []
    raw_payloads: dict[str, Mapping[str, object]] = {}
    raw_evidence_by_case: dict[str, str] = {}

    for row in detailed:
        case_id = row["case_id"]
        executed = _bool(row["executed"])
        status = Status.from_source(row["status"])
        attempt_evidence_ids = [
            source_evidence_ids["detailed"],
            source_evidence_ids["rows"],
        ]
        attempt_entities[case_id] = {
            "route_id": _EXPERIMENTAL_ROUTE_ID,
            "campaign_id": _EXPERIMENTAL_CAMPAIGN_ID,
            "test_case_id": case_id,
            "attempt_id": _attempt_id(case_id),
            "status": status.value,
            "executed": executed,
            "reason": row["failure_reason"],
            "model_id": row["model"],
            "weight_format_id": row["weight_precision"],
            "cache_format_id": row["cache_codec"],
            "backend_id": None,
            "source_status": row["status"],
            "failure_kind": row["failure_stage"] or None,
            "evidence_ids": attempt_evidence_ids,
        }
        availability_entities.append(
            {
                "test_case_id": case_id,
                "model_id": row["model"],
                "weight_format_id": row["weight_precision"],
                "cache_format_id": row["cache_codec"],
                "status": status.value,
                "executed": executed,
                "reason": row["failure_reason"],
            }
        )
        if not executed:
            failure_entities[case_id] = {
                "route_id": _EXPERIMENTAL_ROUTE_ID,
                "campaign_id": _EXPERIMENTAL_CAMPAIGN_ID,
                "test_case_id": case_id,
                "attempt_id": _attempt_id(case_id),
                "failure_id": _failure_id(case_id),
                "status": status.value,
                "stage": row["failure_stage"],
                "reason": row["failure_reason"],
                "source_status": row["status"],
                "evidence_ids": list(attempt_evidence_ids),
            }
            continue
        raw_relative = _portable_source_path(row["raw_result_path"])
        raw_evidence_id = add_evidence(
            raw_relative, "raw-case-result", f"fv6 raw result {case_id}"
        )
        raw_evidence_by_case[case_id] = raw_evidence_id
        attempt_evidence_ids.append(raw_evidence_id)
        attempt_entities[case_id]["evidence_ids"] = attempt_evidence_ids
        raw_payload = _read_json(repo_root / Path(raw_relative))
        if not isinstance(raw_payload, dict):
            raise ValueError(f"{case_id}: frozen raw result is not an object")
        raw_payloads[case_id] = raw_payload
        expected_measurement_ids: list[str] = []
        decode_values: list[float] = []
        latency_values: list[float] = []
        peak_values: list[int] = []
        for repetition, raw_run in enumerate(raw_payload["benchmark_runs"], start=1):
            measurement_id, run_id, repetition_id = _measurement_identity(
                case_id, repetition
            )
            result = raw_run["result"]
            stdout_result = json.loads(str(raw_run["stdout"]))
            if stdout_result != result:
                raise ValueError(
                    f"{case_id}: frozen repetition {repetition} stdout/result conflict"
                )
            result = stdout_result
            peak = _working_set_bytes(raw_run.get("peak_working_set_mb"))
            measurement_entities[measurement_id] = {
                "route_id": _EXPERIMENTAL_ROUTE_ID,
                "campaign_id": _EXPERIMENTAL_CAMPAIGN_ID,
                "test_case_id": case_id,
                "attempt_id": _attempt_id(case_id),
                "measurement_id": measurement_id,
                "run_id": run_id,
                "repetition_id": repetition_id,
                "source_evidence_id": raw_evidence_id,
                "latency_ms": float(result["ttft_ms"]),
                "prompt_tokens_per_second": None,
                "generation_tokens_per_second": float(result["decode_tps"]),
                "peak_working_set_bytes": peak,
                "input_tokens": _optional_int(result.get("input_tokens")),
                "output_tokens": _optional_int(result.get("generated_tokens")),
            }
            expected_measurement_ids.append(measurement_id)
            decode_values.append(float(result["decode_tps"]))
            latency_values.append(float(result["ttft_ms"]))
            if peak is None:
                raise ValueError(f"{case_id}: frozen repetition lacks peak memory")
            peak_values.append(peak)
        median_decode = statistics.median(decode_values)
        selected_index = decode_values.index(median_decode)
        summary_values = {
            "generation_tokens_per_second": median_decode,
            "time_to_first_token": latency_values[selected_index],
            "peak_working_set_bytes": max(peak_values),
        }
        for metric_name, value in summary_values.items():
            summary_id = _summary_id(case_id, metric_name)
            _, unit, aggregation = _SUMMARY_CONTRACTS[metric_name]
            summary_entities[summary_id] = {
                "route_id": _EXPERIMENTAL_ROUTE_ID,
                "campaign_id": _EXPERIMENTAL_CAMPAIGN_ID,
                "test_case_id": case_id,
                "summary_id": summary_id,
                "metric_name": metric_name,
                "value": value,
                "unit": unit,
                "aggregation": aggregation,
                "source_measurement_ids": list(expected_measurement_ids),
            }

    for case_id, raw_payload in raw_payloads.items():
        raw_evidence_id = raw_evidence_by_case[case_id]
        prompt_suite_id = str(raw_payload["quality_prompt_set_id"])
        for run in raw_payload["quality_runs"]:
            prompt_id = str(run["prompt_id"])
            prompt_relative = _portable_source_path(str(run["prompt_path"]))
            prompt_evidence_id = add_evidence(
                prompt_relative,
                "quality-prompt-input",
                f"fv6 prompt input {prompt_id}",
            )
            prompt_entity = {
                "prompt_suite_id": prompt_suite_id,
                "prompt_id": prompt_id,
                "domain": str(run["domain"]),
                "prompt_length": str(run["prompt_length"]),
                "input_evidence_id": prompt_evidence_id,
                "relative_path": prompt_relative,
                "sha256": str(run["prompt_sha256"]),
            }
            previous = prompt_entities.get(prompt_id)
            if previous is not None and previous != prompt_entity:
                raise ValueError(f"frozen prompt definition differs: {prompt_id}")
            prompt_entities[prompt_id] = prompt_entity
            quality = run["quality"]
            result = run["result"]
            answer = str(result.get("text", ""))
            output_entities[(case_id, prompt_id)] = {
                "output_id": _output_id(case_id, prompt_id),
                "test_case_id": case_id,
                "prompt_id": prompt_id,
                "domain": str(run["domain"]),
                "prompt_length": str(run["prompt_length"]),
                "status": str(result["status"]),
                "valid_output": bool(quality["valid_output"]),
                "critical_failure": bool(quality["critical_failure"]),
                "prompt_score": float(quality["score"]),
                "output_sha256": hashlib.sha256(answer.encode("utf-8")).hexdigest(),
                "source_evidence_id": raw_evidence_id,
            }
            for criterion in quality["criteria"]:
                category = str(criterion["category"])
                criterion_id = str(criterion["id"])
                criterion_identity = f"{category}:{criterion_id}"
                quality_entities[(case_id, prompt_id, criterion_identity)] = {
                    "route_id": _EXPERIMENTAL_ROUTE_ID,
                    "campaign_id": _EXPERIMENTAL_CAMPAIGN_ID,
                    "test_case_id": case_id,
                    "quality_id": _quality_id(
                        case_id, prompt_id, category, criterion_id
                    ),
                    "prompt_id": prompt_id,
                    "criterion_id": criterion_identity,
                    "score": float(criterion["points_awarded"]),
                    "maximum_score": float(criterion["weight"]),
                    "prompt_suite_id": prompt_suite_id,
                    "rubric_id": "objective-quality-weighted-5-3-2-output-health-gate",
                    "scoring_version": str(quality["schema"]),
                    "source_evidence_id": raw_evidence_id,
                }

    quality_projection = {
        (row["case_id"], row["prompt_id"], f"{row['category']}:{row['criterion_id']}"):
        (
            float(row["points_awarded"]),
            float(row["weight"]),
            row["prompt_set_id"],
        )
        for row in quality_rows
    }
    expected_projection = {
        key: (
            entity["score"],
            entity["maximum_score"],
            entity["prompt_suite_id"],
        )
        for key, entity in quality_entities.items()
    }
    if quality_projection != expected_projection:
        raise ValueError("frozen raw quality entities differ from quality details")

    grouped_artifacts: dict[tuple[str, str], list[dict[str, str]]] = defaultdict(list)
    for row in detailed:
        grouped_artifacts[(row["model"], row["weight_precision"])].append(row)
    model_artifact_entities: list[dict[str, object]] = []
    for (model_id, weight_id), source_rows in sorted(grouped_artifacts.items()):
        executed_rows = [row for row in source_rows if _bool(row["executed"])]
        model_names = {
            PureWindowsPath(row["model_path"]).name
            for row in executed_rows
            if row["model_path"]
        }
        hashes = {row["model_sha256"] for row in executed_rows}
        sizes = {int(row["model_size_bytes"]) for row in executed_rows}
        model_artifact_entities.append(
            {
                "model_id": model_id,
                "weight_format_id": weight_id,
                "status": "available" if executed_rows else "artifact_unavailable",
                "executed_case_count": len(executed_rows),
                "artifact_label": next(iter(model_names), ""),
                "sha256": next(iter(hashes), ""),
                "size_bytes": next(iter(sizes), None),
                "reason": "" if executed_rows else source_rows[0]["failure_reason"],
            }
        )

    return {
        "artifact_unavailable_cases": sorted(
            row["case_id"]
            for row in detailed
            if row["status"] == "model_artifact_unavailable"
            and not _bool(row["executed"])
        ),
        "cache_formats": sorted({row["cache_codec"] for row in detailed}),
        "executed_model_weight_artifacts": sorted(
            {
                f"{row['model']}|{row['weight_precision']}"
                for row in detailed
                if row["status"] == "passed" and _bool(row["executed"])
            }
        ),
        "attempt_semantics": {
            row["case_id"]: {
                "status": Status.from_source(row["status"]),
                "executed": _bool(row["executed"]),
                "source_status": row["status"],
            }
            for row in detailed
        },
        "attempt_entities": attempt_entities,
        "availability_entities": availability_entities,
        "evidence_entities": evidence_entities,
        "failure_entities": failure_entities,
        "measurement_entities": measurement_entities,
        "model_artifact_entities": model_artifact_entities,
        "output_entities": output_entities,
        "prompt_entities": prompt_entities,
        "quality_entities": quality_entities,
        "summary_entities": summary_entities,
    }


def _evidence_case(record: EvidenceRecord) -> str | None:
    prefix = "fv6 raw result "
    if record.role == "raw-case-result" and record.source_label is not None:
        if record.source_label.startswith(prefix):
            return record.source_label.removeprefix(prefix)
    return None


def _experimental_validation_receipts(
    bundle: RouteBundle,
    prompt_rows: Sequence[Mapping[str, object]],
    output_rows: Sequence[Mapping[str, object]],
    availability_rows: Sequence[Mapping[str, object]],
    model_artifact_rows: Sequence[Mapping[str, object]],
    expectations: Mapping[str, object],
) -> tuple[dict[str, object], dict[str, object]]:
    attempt_counts = Counter(item.attempt_id for item in bundle.attempts)
    measurement_counts = Counter(item.measurement_id for item in bundle.measurements)
    summary_counts = Counter(item.summary_id for item in bundle.summaries)
    quality_counts = Counter(item.quality_id for item in bundle.quality)
    failure_counts = Counter(item.failure_id for item in bundle.failures)
    evidence_counts = Counter(item.evidence_id for item in bundle.evidence)
    output_counts = Counter(str(row.get("output_id", "")) for row in output_rows)
    prompt_counts = Counter(
        (str(row.get("prompt_suite_id", "")), str(row.get("prompt_id", "")))
        for row in prompt_rows
    )

    attempt_by_id = {
        item.attempt_id: item
        for item in bundle.attempts
        if attempt_counts[item.attempt_id] == 1
    }
    measurement_by_id = {
        item.measurement_id: item
        for item in bundle.measurements
        if measurement_counts[item.measurement_id] == 1
    }
    evidence_by_id = {
        item.evidence_id: item
        for item in bundle.evidence
        if evidence_counts[item.evidence_id] == 1
    }
    passed_attempts = {
        item.test_case_id: item
        for item in bundle.attempts
        if item.status is Status.PASSED and item.executed
    }
    artifact_unavailable_attempts = {
        item.test_case_id: item
        for item in bundle.attempts
        if item.status is Status.ARTIFACT_UNAVAILABLE and not item.executed
    }
    expected_unavailable = list(expectations["artifact_unavailable_cases"])
    expected_cache_formats = list(expectations["cache_formats"])
    expected_artifacts = list(expectations["executed_model_weight_artifacts"])
    expected_semantics = expectations["attempt_semantics"]
    expected_attempt_entities = expectations["attempt_entities"]
    expected_availability_entities = expectations["availability_entities"]
    expected_evidence_entities = expectations["evidence_entities"]
    expected_failure_entities = expectations["failure_entities"]
    expected_measurement_entities = expectations["measurement_entities"]
    expected_model_artifact_entities = expectations["model_artifact_entities"]
    expected_output_entities = expectations["output_entities"]
    expected_prompt_entities = expectations["prompt_entities"]
    expected_quality_entities = expectations["quality_entities"]
    expected_summary_entities = expectations["summary_entities"]
    prompt_row_id_counts = Counter(
        str(row.get("prompt_id", "")) for row in prompt_rows
    )
    published_prompts_by_id = {
        str(row.get("prompt_id", "")): row
        for row in prompt_rows
        if prompt_row_id_counts[str(row.get("prompt_id", ""))] == 1
    }

    attempt_entity_errors = _entity_set_diagnostics(
        [item.to_row() for item in bundle.attempts],
        list(expected_attempt_entities.values()),
        ("attempt_id",),
    )
    measurement_entity_errors = _entity_set_diagnostics(
        [item.to_row() for item in bundle.measurements],
        list(expected_measurement_entities.values()),
        ("measurement_id",),
    )
    summary_entity_errors = _entity_set_diagnostics(
        [item.to_row() for item in bundle.summaries],
        list(expected_summary_entities.values()),
        ("summary_id",),
    )
    quality_entity_errors = _entity_set_diagnostics(
        [item.to_row() for item in bundle.quality],
        list(expected_quality_entities.values()),
        ("quality_id",),
    )
    failure_entity_errors = _entity_set_diagnostics(
        [item.to_row() for item in bundle.failures],
        list(expected_failure_entities.values()),
        ("failure_id",),
    )
    evidence_entity_errors = _entity_set_diagnostics(
        [item.to_row() for item in bundle.evidence],
        list(expected_evidence_entities.values()),
        ("evidence_id",),
    )
    prompt_entity_errors = _entity_set_diagnostics(
        [_published_prompt_entity(row) for row in prompt_rows],
        list(expected_prompt_entities.values()),
        ("prompt_suite_id", "prompt_id"),
    )
    output_entity_errors = _entity_set_diagnostics(
        [_published_output_entity(row) for row in output_rows],
        list(expected_output_entities.values()),
        ("output_id",),
    )
    availability_entity_errors = _entity_set_diagnostics(
        [_published_availability_entity(row) for row in availability_rows],
        list(expected_availability_entities),
        ("test_case_id",),
    )
    model_artifact_entity_errors = _entity_set_diagnostics(
        [_published_model_artifact_entity(row) for row in model_artifact_rows],
        list(expected_model_artifact_entities),
        ("model_id", "weight_format_id"),
    )

    prompts_per_case = Counter(
        (item.test_case_id, item.prompt_id) for item in bundle.quality
    )
    prompt_count_values = Counter(case_id for case_id, _ in prompts_per_case)
    prompt_count_actual: object = (
        next(iter(set(prompt_count_values.values())))
        if len(set(prompt_count_values.values())) == 1
        else sorted(set(prompt_count_values.values()))
    )
    coverage_checks = {
        "artifact_unavailable_count": _receipt_check(
            len(artifact_unavailable_attempts), len(expected_unavailable)
        ),
        "artifact_unavailable_cases": _receipt_check(
            sorted(artifact_unavailable_attempts), expected_unavailable
        ),
        "cache_format_count": _receipt_check(
            len({item.cache_format_id for item in bundle.attempts}),
            len(expected_cache_formats),
        ),
        "cache_format_set": _receipt_check(
            sorted({str(item.cache_format_id) for item in bundle.attempts}),
            expected_cache_formats,
        ),
        "executed_count": _receipt_check(len(passed_attempts), 27),
        "executed_model_weight_artifact_count": _receipt_check(
            len(
                {
                    (item.model_id, item.weight_format_id)
                    for item in bundle.attempts
                    if item.executed
                }
            ),
            len(expected_artifacts),
        ),
        "executed_model_weight_artifact_set": _receipt_check(
            sorted(
                {
                    f"{item.model_id}|{item.weight_format_id}"
                    for item in bundle.attempts
                    if item.executed
                }
            ),
            expected_artifacts,
        ),
        "passed_count": _receipt_check(
            sum(item.status is Status.PASSED for item in bundle.attempts), 27
        ),
        "planned_count": _receipt_check(len(bundle.attempts), 81),
        "quality_prompts_per_passed_case": _receipt_check(prompt_count_actual, 48),
    }
    coverage = {
        "planned": len(bundle.attempts),
        "executed": len(passed_attempts),
        "passed": sum(item.status is Status.PASSED for item in bundle.attempts),
        "artifact_unavailable": len(artifact_unavailable_attempts),
        "cache_format_count": len({item.cache_format_id for item in bundle.attempts}),
        "model_weight_artifact_count": len(
            {
                (item.model_id, item.weight_format_id)
                for item in bundle.attempts
                if item.executed
            }
        ),
        "quality_prompts_per_passed_case": prompt_count_actual,
        "checks": coverage_checks,
        "valid": all(bool(check["passed"]) for check in coverage_checks.values()),
    }

    attempt_identifier_errors: list[str] = []
    for item in bundle.attempts:
        expected = expected_attempt_entities.get(item.test_case_id)
        if (
            expected is None
            or item.attempt_id != _attempt_id(item.test_case_id)
            or item.attempt_id != expected["attempt_id"]
            or item.model_id != expected["model_id"]
            or item.weight_format_id != expected["weight_format_id"]
            or item.cache_format_id != expected["cache_format_id"]
        ):
            attempt_identifier_errors.append(item.attempt_id)

    measurement_identifier_errors: list[str] = []
    for item in bundle.measurements:
        try:
            repetition = int(str(item.repetition_id))
            canonical_id, canonical_run_id, canonical_repetition_id = (
                _measurement_identity(item.test_case_id, repetition)
            )
        except (TypeError, ValueError):
            measurement_identifier_errors.append(item.measurement_id)
            continue
        expected = expected_measurement_entities.get(canonical_id)
        if (
            expected is None
            or item.measurement_id != canonical_id
            or item.run_id != canonical_run_id
            or item.repetition_id != canonical_repetition_id
            or item.attempt_id != expected["attempt_id"]
            or item.source_evidence_id != expected["source_evidence_id"]
        ):
            measurement_identifier_errors.append(item.measurement_id)

    summary_identifier_errors: list[str] = []
    summary_lineage_errors: list[str] = []
    for item in bundle.summaries:
        try:
            canonical_id = _summary_id(item.test_case_id, item.metric_name)
        except KeyError:
            summary_identifier_errors.append(item.summary_id)
            summary_lineage_errors.append(item.summary_id)
            continue
        expected = expected_summary_entities.get(canonical_id)
        if expected is None or item.summary_id != canonical_id:
            summary_identifier_errors.append(item.summary_id)
        if (
            expected is None
            or item.test_case_id != expected["test_case_id"]
            or item.metric_name != expected["metric_name"]
            or item.value != expected["value"]
            or item.unit != expected["unit"]
            or item.aggregation != expected["aggregation"]
            or list(item.source_measurement_ids) != expected["source_measurement_ids"]
        ):
            summary_lineage_errors.append(item.summary_id)

    quality_identifier_errors: list[str] = []
    quality_prompt_binding_errors: list[str] = []
    for item in bundle.quality:
        expected_prompt = expected_prompt_entities.get(item.prompt_id)
        published_prompt = published_prompts_by_id.get(str(item.prompt_id))
        expected_quality = expected_quality_entities.get(
            (item.test_case_id, item.prompt_id, item.criterion_id)
        )
        if expected_quality is None or item.quality_id != expected_quality["quality_id"]:
            quality_identifier_errors.append(item.quality_id)
        expected_output = expected_output_entities.get(
            (item.test_case_id, item.prompt_id)
        )
        if (
            expected_prompt is None
            or published_prompt is None
            or _published_prompt_entity(published_prompt) != expected_prompt
            or item.prompt_suite_id != expected_prompt["prompt_suite_id"]
            or expected_output is None
            or item.source_evidence_id != expected_output["source_evidence_id"]
        ):
            quality_prompt_binding_errors.append(item.quality_id)

    failure_identifier_errors: list[str] = []
    for item in bundle.failures:
        expected = expected_failure_entities.get(item.test_case_id)
        if (
            expected is None
            or item.failure_id != _failure_id(item.test_case_id)
            or item.failure_id != expected["failure_id"]
            or item.attempt_id != expected["attempt_id"]
        ):
            failure_identifier_errors.append(item.failure_id)

    evidence_by_path = {
        item.relative_path: item
        for item in bundle.evidence
        if sum(
            candidate.relative_path == item.relative_path
            for candidate in bundle.evidence
        )
        == 1
    }
    evidence_identifier_errors: list[str] = []
    for relative_path in sorted(
        set(evidence_by_path) | set(expected_evidence_entities)
    ):
        item = evidence_by_path.get(relative_path)
        expected = expected_evidence_entities.get(relative_path)
        if (
            item is None
            or expected is None
            or item.route_id != expected["route_id"]
            or item.campaign_id != expected["campaign_id"]
            or item.evidence_id != expected["evidence_id"]
            or item.role != expected["role"]
            or item.sha256 != expected["sha256"]
            or item.size_bytes != expected["size_bytes"]
            or item.source_label != expected["source_label"]
            or item.derived != expected["derived"]
            or list(item.input_evidence_ids) != expected["input_evidence_ids"]
        ):
            evidence_identifier_errors.append(relative_path)

    attempt_semantic_errors: list[str] = []
    attempt_reference_errors: list[str] = []
    for item in bundle.attempts:
        expected = expected_semantics.get(item.test_case_id)
        if (
            expected is None
            or item.status is not expected["status"]
            or item.executed is not expected["executed"]
            or item.source_status != expected["source_status"]
        ):
            attempt_semantic_errors.append(item.attempt_id)
        linked = [evidence_by_id.get(evidence_id) for evidence_id in item.evidence_ids]
        if any(record is None for record in linked):
            attempt_reference_errors.append(item.attempt_id)
            continue
        roles = Counter(record.role for record in linked if record is not None)
        expected_roles = (
            Counter({"source-results": 1, "source-ledger": 1, "raw-case-result": 1})
            if item.status is Status.PASSED and item.executed
            else Counter({"source-results": 1, "source-ledger": 1})
        )
        raw_cases = {
            _evidence_case(record)
            for record in linked
            if record is not None and record.role == "raw-case-result"
        }
        if roles != expected_roles or raw_cases not in (set(), {item.test_case_id}):
            attempt_reference_errors.append(item.attempt_id)

    measurement_reference_errors: list[str] = []
    for item in bundle.measurements:
        attempt = attempt_by_id.get(item.attempt_id)
        evidence = evidence_by_id.get(str(item.source_evidence_id))
        if (
            attempt is None
            or attempt.test_case_id != item.test_case_id
            or attempt.status is not Status.PASSED
            or not attempt.executed
            or evidence is None
            or evidence.role != "raw-case-result"
            or _evidence_case(evidence) != item.test_case_id
        ):
            measurement_reference_errors.append(item.measurement_id)

    summary_reference_errors: list[str] = []
    for item in bundle.summaries:
        sources = [measurement_by_id.get(source_id) for source_id in item.source_measurement_ids]
        metric_field = {
            "generation_tokens_per_second": "generation_tokens_per_second",
            "time_to_first_token": "latency_ms",
            "peak_working_set_bytes": "peak_working_set_bytes",
        }.get(item.metric_name)
        if (
            item.test_case_id not in passed_attempts
            or not sources
            or any(source is None or source.test_case_id != item.test_case_id for source in sources)
            or metric_field is None
            or any(getattr(source, metric_field) is None for source in sources if source is not None)
        ):
            summary_reference_errors.append(item.summary_id)

    quality_reference_errors: list[str] = []
    for item in bundle.quality:
        evidence = evidence_by_id.get(str(item.source_evidence_id))
        if (
            item.test_case_id not in passed_attempts
            or evidence is None
            or evidence.role != "raw-case-result"
            or _evidence_case(evidence) != item.test_case_id
            or item.prompt_id is None
            or item.criterion_id is None
            or item.scoring_version != _EXPERIMENTAL_QUALITY_SCHEMA
        ):
            quality_reference_errors.append(item.quality_id)

    failure_reference_errors: list[str] = []
    for item in bundle.failures:
        attempt = attempt_by_id.get(item.attempt_id)
        linked = [evidence_by_id.get(evidence_id) for evidence_id in item.evidence_ids]
        roles = Counter(record.role for record in linked if record is not None)
        if (
            attempt is None
            or attempt.test_case_id != item.test_case_id
            or attempt.status is not Status.ARTIFACT_UNAVAILABLE
            or attempt.executed
            or item.status is not Status.ARTIFACT_UNAVAILABLE
            or any(record is None for record in linked)
            or roles != Counter({"source-results": 1, "source-ledger": 1})
        ):
            failure_reference_errors.append(item.failure_id)

    prompt_identifier_errors = sorted(
        set(published_prompts_by_id) ^ set(expected_prompt_entities)
    )
    prompt_source_binding_errors: list[str] = []
    for prompt_id in sorted(set(published_prompts_by_id) | set(expected_prompt_entities)):
        row = published_prompts_by_id.get(prompt_id)
        expected = expected_prompt_entities.get(prompt_id)
        if row is None or expected is None:
            prompt_source_binding_errors.append(prompt_id)
            continue
        actual = _published_prompt_entity(row)
        if actual != expected:
            prompt_identifier_errors.append(prompt_id)
            prompt_source_binding_errors.append(prompt_id)

    prompt_reference_errors: list[str] = []
    for row in prompt_rows:
        evidence = evidence_by_id.get(str(row.get("input_evidence_id", "")))
        if (
            evidence is None
            or evidence.role != "quality-prompt-input"
            or evidence.relative_path != str(row.get("relative_path", ""))
            or evidence.sha256 != str(row.get("sha256", ""))
        ):
            prompt_reference_errors.append(str(row.get("prompt_id", "")))

    prompt_ids = {prompt_id for _, prompt_id in prompt_counts}
    output_identifier_errors: list[str] = []
    output_source_binding_errors: list[str] = []
    output_reference_errors: list[str] = []
    for row in output_rows:
        case_id = str(row.get("test_case_id", ""))
        prompt_id = str(row.get("prompt_id", ""))
        output_id = str(row.get("output_id", ""))
        evidence = evidence_by_id.get(str(row.get("source_evidence_id", "")))
        expected = expected_output_entities.get((case_id, prompt_id))
        if expected is None or output_id != _output_id(case_id, prompt_id):
            output_identifier_errors.append(output_id or f"{case_id}/{prompt_id}")
        try:
            actual_output = {
                "output_id": output_id,
                "test_case_id": case_id,
                "prompt_id": prompt_id,
                "domain": str(row.get("domain", "")),
                "prompt_length": str(row.get("prompt_length", "")),
                "status": str(row.get("status", "")),
                "valid_output": _published_bool(row.get("valid_output", "")),
                "critical_failure": _published_bool(
                    row.get("critical_failure", "")
                ),
                "prompt_score": float(row.get("prompt_score", "")),
                "output_sha256": str(row.get("output_sha256", "")),
                "source_evidence_id": str(row.get("source_evidence_id", "")),
            }
        except (TypeError, ValueError):
            actual_output = None
        if expected is None or actual_output != expected:
            output_identifier_errors.append(output_id or f"{case_id}/{prompt_id}")
            output_source_binding_errors.append(output_id or f"{case_id}/{prompt_id}")
        if (
            output_id != _output_id(case_id, prompt_id)
            or case_id not in passed_attempts
            or prompt_id not in prompt_ids
            or evidence is None
            or evidence.role != "raw-case-result"
            or _evidence_case(evidence) != case_id
        ):
            output_reference_errors.append(output_id or f"{case_id}/{prompt_id}")

    known_source_roles = {
        "source-results",
        "source-coverage",
        "source-quality",
        "source-ledger",
        "source-workbook",
        "raw-case-result",
        "quality-prompt-input",
    }
    evidence_reference_errors = sorted(
        item.evidence_id
        for item in bundle.evidence
        if item.role not in known_source_roles
        or item.derived
        or bool(item.input_evidence_ids)
        or any(source_id not in evidence_by_id for source_id in item.input_evidence_ids)
    )

    criteria_by_prompt: dict[tuple[str, str | None], set[str | None]] = defaultdict(set)
    for item in bundle.quality:
        criteria_by_prompt[(item.test_case_id, item.prompt_id)].add(item.criterion_id)
    distinct_criteria_actual = sorted(
        set(len(criteria) for criteria in criteria_by_prompt.values())
    )
    unavailable_exclusions = {
        "measurements": sorted(
            {item.test_case_id for item in bundle.measurements} & set(expected_unavailable)
        ),
        "outputs": sorted(
            {str(row["test_case_id"]) for row in output_rows} & set(expected_unavailable)
        ),
        "quality": sorted(
            {item.test_case_id for item in bundle.quality} & set(expected_unavailable)
        ),
        "summaries": sorted(
            {item.test_case_id for item in bundle.summaries} & set(expected_unavailable)
        ),
    }
    empty_exclusions = {
        "measurements": [],
        "outputs": [],
        "quality": [],
        "summaries": [],
    }
    data_checks = {
        "availability_entity_set_equality": _receipt_check(
            availability_entity_errors, []
        ),
        "attempt_entity_set_equality": _receipt_check(attempt_entity_errors, []),
        "attempt_identifier_uniqueness": _receipt_check(
            len({item.attempt_id for item in bundle.attempts}), len(bundle.attempts)
        ),
        "attempt_identifier_bindings": _receipt_check(
            sorted(attempt_identifier_errors), []
        ),
        "attempt_source_evidence_references": _receipt_check(
            sorted(attempt_reference_errors), []
        ),
        "artifact_unavailable_semantics": _receipt_check(
            sorted(attempt_semantic_errors), []
        ),
        "evidence_identifier_uniqueness": _receipt_check(
            len(evidence_counts), len(bundle.evidence)
        ),
        "evidence_entity_set_equality": _receipt_check(evidence_entity_errors, []),
        "evidence_identifier_bindings": _receipt_check(
            sorted(evidence_identifier_errors), []
        ),
        "evidence_references": _receipt_check(evidence_reference_errors, []),
        "exactly_three_distinct_criteria_per_prompt": _receipt_check(
            distinct_criteria_actual, [3]
        ),
        "failure_count": _receipt_check(len(bundle.failures), 54),
        "failure_entity_set_equality": _receipt_check(failure_entity_errors, []),
        "failure_identifier_uniqueness": _receipt_check(
            len(failure_counts), len(bundle.failures)
        ),
        "failure_identifier_bindings": _receipt_check(
            sorted(failure_identifier_errors), []
        ),
        "failure_references": _receipt_check(sorted(failure_reference_errors), []),
        "measurement_count": _receipt_check(len(bundle.measurements), 81),
        "measurement_entity_set_equality": _receipt_check(
            measurement_entity_errors, []
        ),
        "measurement_identifier_uniqueness": _receipt_check(
            len(measurement_counts), len(bundle.measurements)
        ),
        "measurement_identifier_bindings": _receipt_check(
            sorted(measurement_identifier_errors), []
        ),
        "measurement_references": _receipt_check(
            sorted(measurement_reference_errors), []
        ),
        "output_count": _receipt_check(len(output_rows), 1296),
        "output_entity_set_equality": _receipt_check(output_entity_errors, []),
        "output_identifier_uniqueness": _receipt_check(
            len(output_counts), len(output_rows)
        ),
        "output_identifier_bindings": _receipt_check(
            sorted(output_identifier_errors), []
        ),
        "output_references": _receipt_check(sorted(output_reference_errors), []),
        "output_source_bindings": _receipt_check(
            sorted(output_source_binding_errors), []
        ),
        "prompt_count": _receipt_check(len(prompt_rows), 48),
        "prompt_entity_set_equality": _receipt_check(prompt_entity_errors, []),
        "prompt_identifier_uniqueness": _receipt_check(
            len(prompt_counts), len(prompt_rows)
        ),
        "prompt_identifier_bindings": _receipt_check(
            sorted(set(prompt_identifier_errors)), []
        ),
        "prompt_references": _receipt_check(prompt_reference_errors, []),
        "prompt_source_bindings": _receipt_check(
            sorted(prompt_source_binding_errors), []
        ),
        "quality_criterion_count": _receipt_check(len(bundle.quality), 3888),
        "quality_entity_set_equality": _receipt_check(quality_entity_errors, []),
        "quality_identifier_uniqueness": _receipt_check(
            len(quality_counts), len(bundle.quality)
        ),
        "quality_identifier_bindings": _receipt_check(
            sorted(quality_identifier_errors), []
        ),
        "quality_prompt_bindings": _receipt_check(
            sorted(quality_prompt_binding_errors), []
        ),
        "quality_references": _receipt_check(sorted(quality_reference_errors), []),
        "summary_count": _receipt_check(len(bundle.summaries), 81),
        "summary_entity_set_equality": _receipt_check(summary_entity_errors, []),
        "summary_identifier_uniqueness": _receipt_check(
            len(summary_counts), len(bundle.summaries)
        ),
        "summary_identifier_bindings": _receipt_check(
            sorted(summary_identifier_errors), []
        ),
        "summary_lineage_and_values": _receipt_check(
            sorted(summary_lineage_errors), []
        ),
        "summary_references": _receipt_check(sorted(summary_reference_errors), []),
        "unavailable_exclusions": _receipt_check(
            unavailable_exclusions, empty_exclusions
        ),
        "model_artifact_entity_set_equality": _receipt_check(
            model_artifact_entity_errors, []
        ),
    }
    data = {
        "failure_count": len(bundle.failures),
        "measurement_count": len(bundle.measurements),
        "quality_criterion_count": len(bundle.quality),
        "summary_count": len(bundle.summaries),
        "unavailable_cases_with_measurements": unavailable_exclusions["measurements"],
        "checks": data_checks,
        "valid": all(bool(check["passed"]) for check in data_checks.values()),
    }
    return coverage, data


def build_experimental_validation_receipts(
    repo_root: Path,
    bundle: RouteBundle,
    *,
    prompt_rows: Sequence[Mapping[str, object]] | None = None,
    output_rows: Sequence[Mapping[str, object]] | None = None,
    availability_rows: Sequence[Mapping[str, object]] | None = None,
    model_artifact_rows: Sequence[Mapping[str, object]] | None = None,
) -> tuple[dict[str, object], dict[str, object]]:
    """Recompute named fv6 coverage and referential-integrity checks."""
    root = Path(repo_root).resolve(strict=True)
    if prompt_rows is None or output_rows is None:
        generated_prompts, generated_outputs = _prompt_and_output_rows(root, bundle)
        if prompt_rows is None:
            prompt_rows = generated_prompts
        if output_rows is None:
            output_rows = generated_outputs
    if availability_rows is None:
        availability_rows = _availability_rows(bundle)
    if model_artifact_rows is None:
        model_artifact_rows = _model_artifact_rows(root)
    expectations = _frozen_validation_expectations(root)
    return _experimental_validation_receipts(
        bundle,
        prompt_rows,
        output_rows,
        availability_rows,
        model_artifact_rows,
        expectations,
    )


def write_experimental_route(repo_root: Path) -> RouteBundle:
    """Write deterministic canonical fv6 route files and return their bundle."""
    root = Path(repo_root).resolve(strict=True)
    route = root / _EXPERIMENTAL_ROUTE_RELATIVE
    bundle = build_experimental_bundle(root)

    write_json(route / "route-manifest.json", bundle.to_row())
    intended_rows = [
        {
            "test_case_id": item.test_case_id,
            "model_id": item.model_id,
            "weight_format_id": item.weight_format_id,
            "cache_format_id": item.cache_format_id,
            "intended": True,
        }
        for item in bundle.attempts
    ]
    write_csv(
        route / "protocol/intended-test-matrix.csv",
        intended_rows,
        ("test_case_id", "model_id", "weight_format_id", "cache_format_id", "intended"),
    )
    write_json(route / "system/repository.json", bundle.repository)
    write_json(route / "system/hardware.json", bundle.hardware)
    write_json(route / "system/software.json", bundle.software)
    model_artifacts = _model_artifact_rows(root)
    write_csv(
        route / "system/model-artifacts.csv",
        model_artifacts,
        (
            "model_id",
            "weight_format_id",
            "status",
            "executed_case_count",
            "artifact_label",
            "sha256",
            "size_bytes",
            "reason",
        ),
    )

    write_csv(route / "results/attempts.csv", _csv_rows(bundle.attempts), _ATTEMPT_FIELDS)
    write_csv(
        route / "results/measurements.csv",
        _csv_rows(bundle.measurements),
        _MEASUREMENT_FIELDS,
    )
    write_csv(
        route / "results/summary-results.csv",
        _csv_rows(bundle.summaries),
        _SUMMARY_FIELDS,
    )
    availability_rows = _availability_rows(bundle)
    write_csv(
        route / "results/availability-matrix.csv",
        availability_rows,
        (
            "test_case_id",
            "model_id",
            "weight_format_id",
            "cache_format_id",
            "status",
            "executed",
            "reason",
        ),
    )
    source_workbook = root / _EXPERIMENTAL_WORKBOOK_RELATIVE
    copied_workbook = route / "results/source" / source_workbook.name
    _copy_exact(source_workbook, copied_workbook)

    prompt_rows, output_rows = _prompt_and_output_rows(root, bundle)
    write_csv(
        route / "quality/prompt-suite.csv",
        prompt_rows,
        (
            "prompt_suite_id",
            "prompt_id",
            "domain",
            "prompt_length",
            "input_evidence_id",
            "relative_path",
            "sha256",
        ),
    )
    write_csv(route / "quality/scores.csv", _csv_rows(bundle.quality), _QUALITY_FIELDS)
    write_csv(
        route / "quality/outputs-index.csv",
        output_rows,
        (
            "output_id",
            "test_case_id",
            "prompt_id",
            "domain",
            "prompt_length",
            "status",
            "valid_output",
            "critical_failure",
            "prompt_score",
            "output_sha256",
            "source_evidence_id",
        ),
    )
    write_csv(
        route / "failures/failure-register.csv",
        _csv_rows(bundle.failures),
        _FAILURE_FIELDS,
    )
    write_csv(
        route / "evidence/evidence-index.csv",
        _csv_rows(bundle.evidence),
        _EVIDENCE_FIELDS,
    )
    source_location_rows = [
        {
            "evidence_id": item.evidence_id,
            "role": item.role,
            "relative_path": item.relative_path,
            "sha256": item.sha256,
            "size_bytes": item.size_bytes,
            "source_label": item.source_label,
        }
        for item in bundle.evidence
    ]
    write_csv(
        route / "evidence/source-locations.csv",
        source_location_rows,
        ("evidence_id", "role", "relative_path", "sha256", "size_bytes", "source_label"),
    )
    evidence_by_role: dict[str, list[str]] = defaultdict(list)
    for item in bundle.evidence:
        evidence_by_role[item.role].append(item.evidence_id)
    claim_rows = [
        {
            "claim_id": "fv6-complete-attempt-accounting",
            "claim": "fv6 records all 81 intended cases: 27 passed and 54 artifact unavailable",
            "evidence_ids": json.dumps(
                sorted(
                    evidence_by_role["source-results"]
                    + evidence_by_role["source-coverage"]
                    + evidence_by_role["source-ledger"]
                ),
                separators=(",", ":"),
            ),
        },
        {
            "claim_id": "fv6-performance-repetitions",
            "claim": "each passed case has three recorded benchmark repetitions",
            "evidence_ids": json.dumps(
                sorted(evidence_by_role["raw-case-result"]), separators=(",", ":")
            ),
        },
        {
            "claim_id": "fv6-quality-coverage",
            "claim": "each passed case has 48 prompts and three weighted criteria per prompt",
            "evidence_ids": json.dumps(
                sorted(evidence_by_role["source-quality"] + evidence_by_role["raw-case-result"]),
                separators=(",", ":"),
            ),
        },
    ]
    write_csv(
        route / "evidence/claim-evidence-map.csv",
        claim_rows,
        ("claim_id", "claim", "evidence_ids"),
    )
    detailed_evidence = next(
        item for item in bundle.evidence if item.source_label == "fv6 detailed results"
    )
    quality_evidence = next(
        item for item in bundle.evidence if item.source_label == "fv6 quality details"
    )
    workbook_evidence = next(
        item
        for item in bundle.evidence
        if item.source_label == "fv6 final interactive workbook"
    )
    reproduction = route / "reproduction/README.md"
    reproduction.parent.mkdir(parents=True, exist_ok=True)
    reproduction.write_text(
        "# Reproducing the experimental OpenVINO fv6 normalization\n\n"
        "Run from the repository root with the pinned portable test interpreter:\n\n"
        "```powershell\n"
        "& '.tools/python311-portable/python.exe' -c \"from pathlib import Path; "
        "from scripts.testing.final_results.openvino_adapter import "
        "write_experimental_route; write_experimental_route(Path.cwd())\"\n"
        "```\n\n"
        "## Frozen inputs\n\n"
        f"- `{detailed_evidence.relative_path}` — SHA-256 `{detailed_evidence.sha256}`\n"
        f"- `{quality_evidence.relative_path}` — SHA-256 `{quality_evidence.sha256}`\n"
        f"- `{workbook_evidence.relative_path}` — SHA-256 `{workbook_evidence.sha256}`\n"
        "- The companion comparison, coverage, `rows.json`, 27 raw-result JSON files, "
        "and 48 hash-named prompt inputs under "
        "`experiments/raw-results/openvino-experimental-fork/2026-08-30/fv6/` "
        "are individually listed and hashed in `../evidence/evidence-index.csv`.\n\n"
        "The adapter only normalizes existing evidence; it does not rerun inference. "
        "Do not replace unavailable observations with zero, infer missing hardware, "
        "or copy values from another campaign. A source conflict stops generation.\n\n"
        "The source XLSX is retained byte-identically under `../results/source/` as "
        "evidence-only/nonportable. The primary handoff is "
        "`../workbook/generated/openvino-experimental-fork-portable-results.xlsx`; "
        "its adjacent provenance receipt records the 55 absolute-path replacements "
        "and source/output hashes. The 275 formulas have no cached results and may "
        "appear blank in non-calculating readers until Excel recalculation.\n\n"
        "## Manifest update boundary\n\n"
        "`../evidence/manifest-sha256.txt` is an exact receipt for the complete route "
        "file set, including the portable XLSX and its provenance receipt. Regenerate "
        "it only after the final file set is known; the non-destructive manifest API "
        "intentionally refuses to overwrite a different existing receipt during a "
        "routine adapter rerun.\n",
        encoding="utf-8",
        newline="\n",
    )

    coverage_validation, data_validation = build_experimental_validation_receipts(
        root,
        bundle,
        prompt_rows=prompt_rows,
        output_rows=output_rows,
        availability_rows=availability_rows,
        model_artifact_rows=model_artifacts,
    )
    write_json(route / "validation/coverage-validation.json", coverage_validation)
    write_json(route / "validation/data-validation.json", data_validation)
    if not coverage_validation["valid"] or not data_validation["valid"]:
        raise ValueError("generated fv6 validation receipts contain failed checks")

    manifest = route / "evidence/manifest-sha256.txt"
    write_json(
        route / "validation/integrity-validation.json",
        {
            "manifest": manifest.relative_to(root).as_posix(),
            "status": "valid",
            "workbook_copy_sha256": hash_file(copied_workbook),
        },
    )
    files_before_manifest = sorted(
        path for path in route.rglob("*") if path.is_file() and path != manifest
    )
    write_sha256_manifest(root, files_before_manifest, manifest)
    errors = validate_sha256_manifest(root, manifest)
    if errors:
        raise ValueError(f"generated fv6 integrity manifest is invalid: {errors}")
    return bundle


def _official_frozen_validation_expectations(repo_root: Path) -> dict[str, object]:
    """Independently reconstruct official published entities from frozen evidence."""
    fv1 = repo_root / _OFFICIAL_FV1_RELATIVE
    consolidated = repo_root / _OFFICIAL_FV2_RELATIVE / "consolidated"
    paths = {
        "fv2-detailed": consolidated / "official-openvino-detailed-results.csv",
        "fv2-comparison": consolidated / "official-openvino-comparison.csv",
        "fv2-coverage": consolidated / "official-openvino-coverage.csv",
        "fv2-rows": consolidated / "rows.json",
        "fv1-detailed": fv1 / "official-openvino-detailed-results.csv",
        "fv1-comparison": fv1 / "official-openvino-comparison.csv",
        "fv1-coverage": fv1 / "official-openvino-coverage.csv",
        "fv1-quality": fv1 / "experimental-openvino-quality-details.csv",
        "fv1-rows": fv1 / "rows.json",
        "fv1-audit": fv1 / "audit-report.json",
        "fv1-preflight": fv1 / "preflight/preflight-receipt.json",
        "fv2-workbook": repo_root / _OFFICIAL_V2_WORKBOOK_RELATIVE,
        "fv1-workbook": repo_root / _OFFICIAL_V1_WORKBOOK_RELATIVE,
    }
    fv2_detailed = _read_csv(paths["fv2-detailed"])
    fv2_comparison = _read_csv(paths["fv2-comparison"])
    fv2_coverage = _read_csv(paths["fv2-coverage"])
    fv2_rows = _read_json(paths["fv2-rows"])
    fv1_detailed = _read_csv(paths["fv1-detailed"])
    fv1_comparison = _read_csv(paths["fv1-comparison"])
    fv1_coverage = _read_csv(paths["fv1-coverage"])
    fv1_quality = _read_csv(paths["fv1-quality"])
    preflight_input_relatives = _validate_official_tables(
        fv2_detailed,
        fv2_comparison,
        fv2_coverage,
        fv2_rows,
        fv1_detailed,
        fv1_comparison,
        fv1_coverage,
        fv1_quality,
        repo_root,
    )

    evidence_entities: dict[str, dict[str, object]] = {}
    evidence_by_path: dict[str, str] = {}
    evidence_by_digest: dict[str, str] = {}
    used_evidence_ids: set[str] = set()

    def add_evidence(
        path: Path,
        role: str,
        source_label: str,
        *,
        input_evidence_ids: Sequence[str] = (),
        deduplicate_digest: bool = False,
    ) -> str:
        relative = path.relative_to(repo_root).as_posix()
        if relative in evidence_by_path:
            return evidence_by_path[relative]
        digest = hash_file(path)
        if deduplicate_digest and digest in evidence_by_digest:
            evidence_by_path[relative] = evidence_by_digest[digest]
            return evidence_by_digest[digest]
        evidence_id = _next_evidence_id_for_route(
            _OFFICIAL_ROUTE_ID, digest, used_evidence_ids
        )
        used_evidence_ids.add(evidence_id)
        entity = {
            "route_id": _OFFICIAL_ROUTE_ID,
            "campaign_id": _OFFICIAL_CAMPAIGN_ID,
            "evidence_id": evidence_id,
            "role": role,
            "relative_path": relative,
            "sha256": digest,
            "size_bytes": path.stat().st_size,
            "source_label": source_label,
            "derived": False,
            "input_evidence_ids": list(input_evidence_ids),
        }
        evidence_entities[evidence_id] = entity
        evidence_by_path[relative] = evidence_id
        evidence_by_digest.setdefault(digest, evidence_id)
        return evidence_id

    preflight_input_ids = [
        add_evidence(
            repo_root / relative,
            "preflight-input",
            f"fv1 preflight input {relative.name}",
        )
        for relative in preflight_input_relatives
    ]
    benchmark_input_id = add_evidence(
        repo_root / _OFFICIAL_BENCHMARK_INPUT,
        "benchmark-prompt-input",
        "fv1 shared benchmark prompt input",
    )
    core_definitions = (
        ("fv2-detailed", "source-results", "fv2 final detailed results"),
        ("fv2-comparison", "source-results", "fv2 final comparison results"),
        ("fv2-coverage", "source-coverage", "fv2 final coverage"),
        ("fv2-rows", "source-ledger", "fv2 final source rows"),
        ("fv1-detailed", "prior-source-results", "fv1 passed measurement index"),
        ("fv1-comparison", "prior-source-results", "fv1 comparison index"),
        ("fv1-coverage", "prior-source-coverage", "fv1 coverage index"),
        ("fv1-quality", "source-quality", "fv1 quality details"),
        ("fv1-rows", "prior-source-ledger", "fv1 source rows"),
        ("fv1-audit", "source-validation", "fv1 audit report"),
        ("fv1-preflight", "source-preflight", "fv1 TurboQuant preflight"),
        ("fv2-workbook", "source-workbook", "fv2 revised final workbook"),
        ("fv1-workbook", "indexed-prior-workbook", "fv1 original workbook"),
    )
    core_ids: dict[str, str] = {}
    for key, role, label in core_definitions:
        core_ids[key] = add_evidence(
            paths[key],
            role,
            label,
            input_evidence_ids=(
                preflight_input_ids if key == "fv1-preflight" else ()
            ),
        )

    fv2_root = repo_root / _OFFICIAL_FV2_RELATIVE
    manifest_ids: dict[str, str] = {}
    for path in sorted(fv2_root.rglob("*.json")):
        relative = path.relative_to(repo_root).as_posix()
        if relative in evidence_by_path:
            continue
        if path.name == "manifest.json":
            role = "missing-model-attempt-manifest"
        elif path.name == "attempt-summary.json":
            role = "missing-model-attempt-summary"
        elif path.name == "source-models.json":
            role = "missing-model-source-inventory"
        else:
            role = "missing-model-evidence"
        evidence_id = add_evidence(
            path,
            role,
            f"fv2 missing-model evidence {path.relative_to(fv2_root).as_posix()}",
            deduplicate_digest=True,
        )
        if path.name == "manifest.json":
            manifest_ids[relative] = evidence_id
    conversion_log_id = add_evidence(
        repo_root / _OFFICIAL_CONVERSION_LOG_RELATIVE,
        "conversion-log",
        "fv2 guarded-retry-002 conversion log",
    )

    fv1_by_case = {row["case_id"]: row for row in fv1_detailed}
    attempt_entities: dict[str, dict[str, object]] = {}
    failure_entities: dict[str, dict[str, object]] = {}
    measurement_entities: dict[str, dict[str, object]] = {}
    summary_entities: dict[str, dict[str, object]] = {}
    quality_entities: dict[str, dict[str, object]] = {}
    prompt_entities: dict[str, dict[str, object]] = {}
    output_entities: dict[str, dict[str, object]] = {}
    availability_entities: list[dict[str, object]] = []
    raw_payloads: dict[str, Mapping[str, object]] = {}
    raw_evidence_ids: dict[str, str] = {}
    runtime_versions: set[str] = set()
    runtime_properties: set[tuple[tuple[str, str], ...]] = set()

    for row in fv2_detailed:
        case_id = row["case_id"]
        status = Status.from_source(row["status"])
        executed = _bool(row["executed"])
        attempt_id = _attempt_id(case_id)
        linked_evidence = [core_ids["fv2-detailed"], core_ids["fv2-rows"]]
        if executed:
            fv1_row = fv1_by_case[case_id]
            raw_relative = _portable_source_path(fv1_row["raw_result_path"])
            raw_path = repo_root / Path(raw_relative)
            raw_evidence_id = add_evidence(
                raw_path,
                "raw-case-result",
                f"fv1 raw result {case_id}",
                input_evidence_ids=(benchmark_input_id,),
            )
            raw_evidence_ids[case_id] = raw_evidence_id
            linked_evidence.extend([core_ids["fv1-detailed"], raw_evidence_id])
            raw_payload = _read_json(raw_path)
            if not isinstance(raw_payload, dict):
                raise ValueError(f"{case_id}: frozen raw result is not an object")
            _validate_official_raw_cache_semantics(
                case_id, row["cache_codec"], raw_payload, repo_root
            )
            raw_payloads[case_id] = raw_payload
            measurement_ids: list[str] = []
            decode_values: list[float] = []
            latency_values: list[float] = []
            peak_values: list[int] = []
            for repetition, raw_run in enumerate(raw_payload["benchmark_runs"], start=1):
                result = json.loads(str(raw_run["stdout"]))
                if result != raw_run["result"]:
                    raise ValueError(
                        f"{case_id}: frozen repetition {repetition} stdout/result conflict"
                    )
                measurement_id, run_id, repetition_id = _measurement_identity(
                    case_id, repetition
                )
                peak = _working_set_bytes(raw_run.get("peak_working_set_mb"))
                if peak is None:
                    raise ValueError(f"{case_id}: frozen repetition lacks peak memory")
                measurement_entities[measurement_id] = {
                    "route_id": _OFFICIAL_ROUTE_ID,
                    "campaign_id": _OFFICIAL_CAMPAIGN_ID,
                    "test_case_id": case_id,
                    "attempt_id": attempt_id,
                    "measurement_id": measurement_id,
                    "run_id": run_id,
                    "repetition_id": repetition_id,
                    "source_evidence_id": raw_evidence_id,
                    "latency_ms": float(result["ttft_ms"]),
                    "prompt_tokens_per_second": None,
                    "generation_tokens_per_second": float(result["decode_tps"]),
                    "peak_working_set_bytes": peak,
                    "input_tokens": _optional_int(result.get("input_tokens")),
                    "output_tokens": _optional_int(result.get("generated_tokens")),
                }
                measurement_ids.append(measurement_id)
                decode_values.append(float(result["decode_tps"]))
                latency_values.append(float(result["ttft_ms"]))
                peak_values.append(peak)
            median_decode = statistics.median(decode_values)
            selected_index = decode_values.index(median_decode)
            values = {
                "generation_tokens_per_second": median_decode,
                "time_to_first_token": latency_values[selected_index],
                "peak_working_set_bytes": max(peak_values),
            }
            for metric_name, value in values.items():
                summary_id = _summary_id(case_id, metric_name)
                _, unit, aggregation = _SUMMARY_CONTRACTS[metric_name]
                summary_entities[summary_id] = {
                    "route_id": _OFFICIAL_ROUTE_ID,
                    "campaign_id": _OFFICIAL_CAMPAIGN_ID,
                    "test_case_id": case_id,
                    "summary_id": summary_id,
                    "metric_name": metric_name,
                    "value": value,
                    "unit": unit,
                    "aggregation": aggregation,
                    "source_measurement_ids": list(measurement_ids),
                }
            selected = raw_payload["benchmark"]["result"]
            runtime_versions.add(str(selected["openvino_version"]))
            runtime_properties.add(
                tuple(
                    sorted(
                        (str(key), str(value))
                        for key, value in raw_payload["runtime_properties"].items()
                    )
                )
            )
        else:
            manifest_relative = _official_terminal_manifest_relative(row).as_posix()
            linked_evidence.append(manifest_ids[manifest_relative])
            if row["model"] == "granite-3b" and row["weight_precision"] == "fp16":
                linked_evidence.append(conversion_log_id)
            failure_id = f"{case_id}--{row['status']}"
            failure_entities[failure_id] = {
                "route_id": _OFFICIAL_ROUTE_ID,
                "campaign_id": _OFFICIAL_CAMPAIGN_ID,
                "test_case_id": case_id,
                "attempt_id": attempt_id,
                "failure_id": failure_id,
                "status": status.value,
                "stage": row["failure_stage"],
                "reason": row["failure_reason"],
                "source_status": row["status"],
                "evidence_ids": list(linked_evidence),
            }
        attempt_entities[attempt_id] = {
            "route_id": _OFFICIAL_ROUTE_ID,
            "campaign_id": _OFFICIAL_CAMPAIGN_ID,
            "test_case_id": case_id,
            "attempt_id": attempt_id,
            "status": status.value,
            "executed": executed,
            "reason": row["failure_reason"],
            "model_id": row["model"],
            "weight_format_id": row["weight_precision"],
            "cache_format_id": row["cache_codec"],
            "backend_id": None,
            "source_status": row["status"],
            "failure_kind": row["failure_stage"] or None,
            "evidence_ids": list(linked_evidence),
        }
        availability_entities.append(
            {
                "test_case_id": case_id,
                "model_id": row["model"],
                "weight_format_id": row["weight_precision"],
                "cache_format_id": row["cache_codec"],
                "status": status.value,
                "executed": executed,
                "reason": row["failure_reason"],
            }
        )

    for case_id, raw_payload in raw_payloads.items():
        raw_evidence_id = raw_evidence_ids[case_id]
        prompt_suite_id = str(raw_payload["quality_prompt_set_id"])
        for run in raw_payload["quality_runs"]:
            prompt_id = str(run["prompt_id"])
            prompt_relative = _portable_source_path(str(run["prompt_path"]))
            prompt_evidence_id = add_evidence(
                repo_root / Path(prompt_relative),
                "quality-prompt-input",
                f"fv1 prompt input {prompt_id}",
            )
            prompt = {
                "prompt_suite_id": prompt_suite_id,
                "prompt_id": prompt_id,
                "domain": str(run["domain"]),
                "prompt_length": str(run["prompt_length"]),
                "input_evidence_id": prompt_evidence_id,
                "relative_path": prompt_relative,
                "sha256": str(run["prompt_sha256"]),
            }
            if prompt_id in prompt_entities and prompt_entities[prompt_id] != prompt:
                raise ValueError(f"frozen official prompt definition differs: {prompt_id}")
            prompt_entities[prompt_id] = prompt
            quality = run["quality"]
            result = json.loads(str(run["stdout"]))
            if result != run["result"]:
                raise ValueError(f"{case_id}/{prompt_id}: frozen output stdout conflict")
            answer = str(result.get("text", ""))
            output_id = _output_id(case_id, prompt_id)
            output_entities[output_id] = {
                "output_id": output_id,
                "test_case_id": case_id,
                "prompt_id": prompt_id,
                "domain": str(run["domain"]),
                "prompt_length": str(run["prompt_length"]),
                "status": str(result["status"]),
                "valid_output": bool(quality["valid_output"]),
                "critical_failure": bool(quality["critical_failure"]),
                "prompt_score": float(quality["score"]),
                "output_sha256": hashlib.sha256(answer.encode("utf-8")).hexdigest(),
                "source_evidence_id": raw_evidence_id,
            }
            for criterion in quality["criteria"]:
                category = str(criterion["category"])
                criterion_id = str(criterion["id"])
                quality_id = _quality_id(case_id, prompt_id, category, criterion_id)
                quality_entities[quality_id] = {
                    "route_id": _OFFICIAL_ROUTE_ID,
                    "campaign_id": _OFFICIAL_CAMPAIGN_ID,
                    "test_case_id": case_id,
                    "quality_id": quality_id,
                    "prompt_id": prompt_id,
                    "criterion_id": f"{category}:{criterion_id}",
                    "score": float(criterion["points_awarded"]),
                    "maximum_score": float(criterion["weight"]),
                    "prompt_suite_id": prompt_suite_id,
                    "rubric_id": "objective-quality-weighted-5-3-2-output-health-gate",
                    "scoring_version": str(quality["schema"]),
                    "source_evidence_id": raw_evidence_id,
                }

    quality_projection = {
        _quality_id(row["case_id"], row["prompt_id"], row["category"], row["criterion_id"]): (
            float(row["points_awarded"]),
            float(row["weight"]),
            row["prompt_set_id"],
        )
        for row in fv1_quality
    }
    expected_projection = {
        identity: (
            entity["score"],
            entity["maximum_score"],
            entity["prompt_suite_id"],
        )
        for identity, entity in quality_entities.items()
    }
    if quality_projection != expected_projection:
        raise ValueError("official frozen raw quality differs from fv1 quality details")

    grouped: dict[tuple[str, str], list[dict[str, str]]] = defaultdict(list)
    for row in fv2_detailed:
        grouped[(row["model"], row["weight_precision"])].append(row)
    model_artifact_entities: list[dict[str, object]] = []
    for (model_id, weight_id), source_rows in sorted(grouped.items()):
        executed = [row for row in source_rows if _bool(row["executed"])]
        fv1_rows = [fv1_by_case[row["case_id"]] for row in executed]
        names = {
            PureWindowsPath(row["model_path"]).name
            for row in fv1_rows
            if row["model_path"]
        }
        hashes = {row["model_sha256"] for row in fv1_rows}
        sizes = {int(row["model_size_bytes"]) for row in fv1_rows}
        source_status = source_rows[0]["status"]
        model_artifact_entities.append(
            {
                "model_id": model_id,
                "weight_format_id": weight_id,
                "status": "available" if executed else Status.from_source(source_status).value,
                "executed_case_count": len(executed),
                "artifact_label": next(iter(names), ""),
                "sha256": next(iter(hashes), ""),
                "size_bytes": next(iter(sizes), None),
                "reason": "" if executed else source_rows[0]["failure_reason"],
            }
        )

    source_location_entities = [
        {
            "evidence_id": entity["evidence_id"],
            "role": entity["role"],
            "relative_path": entity["relative_path"],
            "sha256": entity["sha256"],
            "size_bytes": entity["size_bytes"],
            "source_label": entity["source_label"],
        }
        for entity in evidence_entities.values()
        if entity["role"] != "missing-model-source-inventory"
    ]
    inventory_entity = next(
        entity
        for entity in evidence_entities.values()
        if entity["role"] == "missing-model-source-inventory"
    )
    for relative in _OFFICIAL_SOURCE_MODEL_ALIASES:
        path = repo_root / relative
        source_location_entities.append(
            {
                "evidence_id": inventory_entity["evidence_id"],
                "role": "missing-model-source-inventory",
                "relative_path": relative.as_posix(),
                "sha256": hash_file(path),
                "size_bytes": path.stat().st_size,
                "source_label": (
                    "fv2 source-model inventory alias "
                    f"{relative.relative_to(_OFFICIAL_FV2_RELATIVE).as_posix()}"
                ),
            }
        )
    source_location_entities.sort(
        key=lambda row: (str(row["relative_path"]), str(row["evidence_id"]))
    )

    terminal_manifests = [
        _read_json(repo_root / _official_terminal_manifest_relative(row))
        for row in fv2_detailed
        if not _bool(row["executed"])
    ]
    hardware = {
        "status": "source-recorded",
        "emergency_ram_floor_bytes": 2_147_483_648,
        "conversion_preflight_by_model_weight": {
            str(item["case_id"]): item["preflight"]
            for item in terminal_manifests
            if isinstance(item, dict) and isinstance(item.get("preflight"), dict)
        },
    }
    software = {
        "openvino_versions": sorted(runtime_versions),
        "runtime_properties": [dict(items) for items in sorted(runtime_properties)],
        "missing_model_attempt_tool_versions": {
            key: value
            for payload in terminal_manifests
            if isinstance(payload, dict)
            for key, value in dict(payload.get("tool_versions", {})).items()
        },
    }
    repository = {
        "openvino": {
            "url": _ensure_uniform(fv2_detailed, "openvino_url"),
            "commit": _ensure_uniform(fv2_detailed, "openvino_commit"),
        },
        "openvino_genai": {
            "url": _ensure_uniform(fv2_detailed, "openvino_genai_url"),
            "commit": _ensure_uniform(fv2_detailed, "openvino_genai_commit"),
        },
        "turboquant_merge_commit": _ensure_uniform(
            fv2_detailed, "turboquant_merge_commit"
        ),
        "source_campaign": "fv2 status joined to fv1 passed evidence",
        "source_date": "2026-08-30",
    }
    return {
        "attempt_entities": list(attempt_entities.values()),
        "measurement_entities": list(measurement_entities.values()),
        "summary_entities": list(summary_entities.values()),
        "quality_entities": list(quality_entities.values()),
        "failure_entities": list(failure_entities.values()),
        "evidence_entities": list(evidence_entities.values()),
        "prompt_entities": list(prompt_entities.values()),
        "output_entities": list(output_entities.values()),
        "availability_entities": availability_entities,
        "model_artifact_entities": model_artifact_entities,
        "source_location_entities": source_location_entities,
        "repository": repository,
        "hardware": hardware,
        "software": software,
        "route_manifest": {
            "route_id": _OFFICIAL_ROUTE_ID,
            "campaign_id": _OFFICIAL_CAMPAIGN_ID,
            "attempt_count": len(attempt_entities),
            "measurement_count": len(measurement_entities),
            "summary_count": len(summary_entities),
            "quality_count": len(quality_entities),
            "failure_count": len(failure_entities),
            "evidence_count": len(evidence_entities),
            "repository": repository,
            "hardware": hardware,
            "software": software,
        },
        "executed_ids": sorted(
            row["case_id"] for row in fv2_detailed if _bool(row["executed"])
        ),
    }


def build_official_validation_receipts(
    repo_root: Path,
    bundle: RouteBundle,
    *,
    prompt_rows: Sequence[Mapping[str, object]] | None = None,
    output_rows: Sequence[Mapping[str, object]] | None = None,
    availability_rows: Sequence[Mapping[str, object]] | None = None,
    model_artifact_rows: Sequence[Mapping[str, object]] | None = None,
    source_location_rows: Sequence[Mapping[str, object]] | None = None,
) -> tuple[dict[str, object], dict[str, object]]:
    """Rebuild official expectations from frozen sources and compare every entity."""
    root = Path(repo_root).resolve(strict=True)
    expected = _official_frozen_validation_expectations(root)
    if prompt_rows is None or output_rows is None:
        generated_prompts, generated_outputs = _official_prompt_and_output_rows(root, bundle)
        if prompt_rows is None:
            prompt_rows = generated_prompts
        if output_rows is None:
            output_rows = generated_outputs
    if availability_rows is None:
        availability_rows = _availability_rows(bundle)
    if model_artifact_rows is None:
        model_artifact_rows = _official_model_artifact_rows(root)
    if source_location_rows is None:
        source_location_rows = _official_source_location_rows(root, bundle)

    status_counts = Counter(item.status.value for item in bundle.attempts)
    executed_ids = sorted(item.test_case_id for item in bundle.attempts if item.executed)
    expected_executed_ids = expected["executed_ids"]
    cache_ids = sorted({str(item.cache_format_id) for item in bundle.attempts})
    terminal_manifest_links = []
    conversion_log_links = []
    evidence_by_id = {item.evidence_id: item for item in bundle.evidence}
    for failure in bundle.failures:
        linked = [evidence_by_id.get(item) for item in failure.evidence_ids]
        terminal_manifest_links.append(
            any(
                record is not None
                and record.role == "missing-model-attempt-manifest"
                for record in linked
            )
        )
        if failure.test_case_id.startswith("granite-3b__fp16__"):
            conversion_log_links.append(
                any(
                    record is not None and record.role == "conversion-log"
                    for record in linked
                )
            )
    coverage_checks = {
        "attempt_count": _receipt_check(len(bundle.attempts), 45),
        "status_counts": _receipt_check(
            dict(sorted(status_counts.items())),
            {"blocked": 25, "failed": 5, "passed": 15},
        ),
        "executed_case_identities": _receipt_check(
            executed_ids, expected_executed_ids
        ),
        "executed_count": _receipt_check(len(executed_ids), 15),
        "cache_format_identities": _receipt_check(
            cache_ids, ["f16", "tbq3", "tbq4", "u4", "u8"]
        ),
        "turboquant_format_identities": _receipt_check(
            sorted(value for value in cache_ids if value.startswith("tbq")),
            ["tbq3", "tbq4"],
        ),
        "terminal_manifest_coverage": _receipt_check(
            terminal_manifest_links, [True] * 30
        ),
        "conversion_log_coverage": _receipt_check(
            conversion_log_links, [True] * 5
        ),
    }

    def entity_check(
        actual_records: Iterable[object],
        expected_rows: Sequence[Mapping[str, object]],
        key_fields: tuple[str, ...],
    ) -> dict[str, object]:
        actual_rows = [item.to_row() for item in actual_records]
        diagnostics = _entity_set_diagnostics(actual_rows, expected_rows, key_fields)
        return {
            "actual": diagnostics,
            "expected": [],
            "passed": not diagnostics,
        }

    prompt_actual = [_published_prompt_entity(row) for row in prompt_rows]
    prompt_expected = [
        _published_prompt_entity(row) for row in expected["prompt_entities"]
    ]
    output_actual = [_published_output_entity(row) for row in output_rows]
    output_expected = [
        _published_output_entity(row) for row in expected["output_entities"]
    ]
    availability_actual = [_published_availability_entity(row) for row in availability_rows]
    availability_expected = [
        _published_availability_entity(row)
        for row in expected["availability_entities"]
    ]
    model_actual = [
        _published_model_artifact_entity(row) for row in model_artifact_rows
    ]
    model_expected = [
        _published_model_artifact_entity(row)
        for row in expected["model_artifact_entities"]
    ]
    source_location_actual = [
        {
            "evidence_id": str(row.get("evidence_id", "")),
            "role": str(row.get("role", "")),
            "relative_path": str(row.get("relative_path", "")),
            "sha256": str(row.get("sha256", "")),
            "size_bytes": int(row.get("size_bytes", 0)),
            "source_label": str(row.get("source_label", "")),
        }
        for row in source_location_rows
    ]
    source_location_expected = [
        {
            "evidence_id": str(row["evidence_id"]),
            "role": str(row["role"]),
            "relative_path": str(row["relative_path"]),
            "sha256": str(row["sha256"]),
            "size_bytes": int(row["size_bytes"]),
            "source_label": str(row["source_label"]),
        }
        for row in expected["source_location_entities"]
    ]
    source_location_consistency_errors = _official_source_location_consistency_errors(
        root, bundle, source_location_rows
    )

    attempts_by_id = {item.attempt_id: item for item in bundle.attempts}
    evidence_by_id = {item.evidence_id: item for item in bundle.evidence}
    measurement_by_id = {item.measurement_id: item for item in bundle.measurements}
    same_case_typed_references = True
    for item in bundle.measurements:
        attempt = attempts_by_id.get(item.attempt_id)
        source = evidence_by_id.get(str(item.source_evidence_id))
        same_case_typed_references &= bool(
            attempt is not None
            and attempt.status is Status.PASSED
            and attempt.test_case_id == item.test_case_id
            and source is not None
            and source.role == "raw-case-result"
            and source.source_label == f"fv1 raw result {item.test_case_id}"
        )
    for item in bundle.summaries:
        linked = [measurement_by_id.get(identity) for identity in item.source_measurement_ids]
        same_case_typed_references &= bool(
            len(linked) == 3
            and all(
                measurement is not None
                and measurement.test_case_id == item.test_case_id
                for measurement in linked
            )
        )
    for item in bundle.quality:
        source = evidence_by_id.get(str(item.source_evidence_id))
        same_case_typed_references &= bool(
            source is not None
            and source.role == "raw-case-result"
            and source.source_label == f"fv1 raw result {item.test_case_id}"
        )
    for item in bundle.failures:
        attempt = attempts_by_id.get(item.attempt_id)
        same_case_typed_references &= bool(
            attempt is not None
            and attempt.test_case_id == item.test_case_id
            and attempt.status == item.status
            and not attempt.executed
        )

    passed_ids = {
        item.test_case_id for item in bundle.attempts if item.status is Status.PASSED
    }
    observed_ids = (
        {item.test_case_id for item in bundle.measurements}
        | {item.test_case_id for item in bundle.summaries}
        | {item.test_case_id for item in bundle.quality}
    )
    data_checks = {
        "attempt_entities": entity_check(
            bundle.attempts, expected["attempt_entities"], ("attempt_id",)
        ),
        "measurement_entities": entity_check(
            bundle.measurements, expected["measurement_entities"], ("measurement_id",)
        ),
        "summary_entities": entity_check(
            bundle.summaries, expected["summary_entities"], ("summary_id",)
        ),
        "quality_entities": entity_check(
            bundle.quality, expected["quality_entities"], ("quality_id",)
        ),
        "failure_entities": entity_check(
            bundle.failures, expected["failure_entities"], ("failure_id",)
        ),
        "evidence_entities": entity_check(
            bundle.evidence, expected["evidence_entities"], ("evidence_id",)
        ),
        "prompt_entities": {
            "actual": _entity_set_diagnostics(
                prompt_actual, prompt_expected, ("prompt_id",)
            ),
            "expected": [],
            "passed": not _entity_set_diagnostics(
                prompt_actual, prompt_expected, ("prompt_id",)
            ),
        },
        "output_entities": {
            "actual": _entity_set_diagnostics(
                output_actual, output_expected, ("output_id",)
            ),
            "expected": [],
            "passed": not _entity_set_diagnostics(
                output_actual, output_expected, ("output_id",)
            ),
        },
        "availability_entities": {
            "actual": _entity_set_diagnostics(
                availability_actual, availability_expected, ("test_case_id",)
            ),
            "expected": [],
            "passed": not _entity_set_diagnostics(
                availability_actual, availability_expected, ("test_case_id",)
            ),
        },
        "model_artifact_entities": {
            "actual": _entity_set_diagnostics(
                model_actual,
                model_expected,
                ("model_id", "weight_format_id"),
            ),
            "expected": [],
            "passed": not _entity_set_diagnostics(
                model_actual,
                model_expected,
                ("model_id", "weight_format_id"),
            ),
        },
        "source_location_entities": {
            "actual": _entity_set_diagnostics(
                source_location_actual,
                source_location_expected,
                ("relative_path",),
            ),
            "expected": [],
            "passed": not _entity_set_diagnostics(
                source_location_actual,
                source_location_expected,
                ("relative_path",),
            ),
        },
        "source_location_evidence_consistency": _receipt_check(
            source_location_consistency_errors, []
        ),
        "route_metadata": _receipt_check(bundle.to_row(), expected["route_manifest"]),
        "same_case_typed_references": _receipt_check(
            same_case_typed_references, True
        ),
        "nonpassed_observation_exclusion": _receipt_check(
            sorted(observed_ids), sorted(passed_ids)
        ),
        "measurement_count": _receipt_check(len(bundle.measurements), 45),
        "summary_count": _receipt_check(len(bundle.summaries), 45),
        "quality_count": _receipt_check(len(bundle.quality), 2_160),
        "failure_count": _receipt_check(len(bundle.failures), 30),
        "prompt_count": _receipt_check(len(prompt_rows), 48),
        "output_count": _receipt_check(len(output_rows), 720),
    }
    coverage = {
        "route_id": _OFFICIAL_ROUTE_ID,
        "campaign_id": _OFFICIAL_CAMPAIGN_ID,
        "checks": coverage_checks,
        "valid": all(check["passed"] for check in coverage_checks.values()),
    }
    data = {
        "route_id": _OFFICIAL_ROUTE_ID,
        "campaign_id": _OFFICIAL_CAMPAIGN_ID,
        "checks": data_checks,
        "valid": all(check["passed"] for check in data_checks.values()),
    }
    return coverage, data


def write_official_route(repo_root: Path) -> RouteBundle:
    """Write deterministic official fv2/fv1 canonical route files."""
    root = Path(repo_root).resolve(strict=True)
    route = root / _OFFICIAL_ROUTE_RELATIVE
    bundle = build_official_bundle(root)

    write_json(route / "route-manifest.json", bundle.to_row())
    intended_rows = [
        {
            "test_case_id": item.test_case_id,
            "model_id": item.model_id,
            "weight_format_id": item.weight_format_id,
            "cache_format_id": item.cache_format_id,
            "intended": True,
        }
        for item in bundle.attempts
    ]
    write_csv(
        route / "protocol/intended-test-matrix.csv",
        intended_rows,
        ("test_case_id", "model_id", "weight_format_id", "cache_format_id", "intended"),
    )
    write_json(route / "system/repository.json", bundle.repository)
    write_json(route / "system/hardware.json", bundle.hardware)
    write_json(route / "system/software.json", bundle.software)
    model_artifacts = _official_model_artifact_rows(root)
    write_csv(
        route / "system/model-artifacts.csv",
        model_artifacts,
        (
            "model_id",
            "weight_format_id",
            "status",
            "executed_case_count",
            "artifact_label",
            "sha256",
            "size_bytes",
            "reason",
        ),
    )
    write_csv(route / "results/attempts.csv", _csv_rows(bundle.attempts), _ATTEMPT_FIELDS)
    write_csv(
        route / "results/measurements.csv",
        _csv_rows(bundle.measurements),
        _MEASUREMENT_FIELDS,
    )
    write_csv(
        route / "results/summary-results.csv",
        _csv_rows(bundle.summaries),
        _SUMMARY_FIELDS,
    )
    availability_rows = _availability_rows(bundle)
    write_csv(
        route / "results/availability-matrix.csv",
        availability_rows,
        (
            "test_case_id",
            "model_id",
            "weight_format_id",
            "cache_format_id",
            "status",
            "executed",
            "reason",
        ),
    )
    source_workbook = root / _OFFICIAL_V2_WORKBOOK_RELATIVE
    copied_workbook = route / "results/source" / source_workbook.name
    _copy_exact(source_workbook, copied_workbook)

    prompt_rows, output_rows = _official_prompt_and_output_rows(root, bundle)
    write_csv(
        route / "quality/prompt-suite.csv",
        prompt_rows,
        (
            "prompt_suite_id",
            "prompt_id",
            "domain",
            "prompt_length",
            "input_evidence_id",
            "relative_path",
            "sha256",
        ),
    )
    write_csv(route / "quality/scores.csv", _csv_rows(bundle.quality), _QUALITY_FIELDS)
    write_csv(
        route / "quality/outputs-index.csv",
        output_rows,
        (
            "output_id",
            "test_case_id",
            "prompt_id",
            "domain",
            "prompt_length",
            "status",
            "valid_output",
            "critical_failure",
            "prompt_score",
            "output_sha256",
            "source_evidence_id",
        ),
    )
    write_csv(
        route / "failures/failure-register.csv",
        _csv_rows(bundle.failures),
        _FAILURE_FIELDS,
    )
    write_csv(
        route / "evidence/evidence-index.csv",
        _csv_rows(bundle.evidence),
        _EVIDENCE_FIELDS,
    )
    source_location_rows = _official_source_location_rows(root, bundle)
    write_csv(
        route / "evidence/source-locations.csv",
        source_location_rows,
        ("evidence_id", "role", "relative_path", "sha256", "size_bytes", "source_label"),
    )
    evidence_by_role: dict[str, list[str]] = defaultdict(list)
    for item in bundle.evidence:
        evidence_by_role[item.role].append(item.evidence_id)
    claims = [
        {
            "claim_id": "official-complete-attempt-accounting",
            "claim": "fv2 records 45 attempts: 15 passed, 5 conversion failed, and 25 hardware-preflight blocked",
            "evidence_ids": json.dumps(
                sorted(
                    evidence_by_role["source-results"]
                    + evidence_by_role["source-coverage"]
                    + evidence_by_role["source-ledger"]
                    + evidence_by_role["missing-model-attempt-manifest"]
                ),
                separators=(",", ":"),
            ),
        },
        {
            "claim_id": "official-passed-performance-evidence",
            "claim": "fv1 supplies three benchmark repetitions for each of the 15 fv2-passed cases",
            "evidence_ids": json.dumps(
                sorted(evidence_by_role["raw-case-result"]), separators=(",", ":")
            ),
        },
        {
            "claim_id": "official-passed-quality-evidence",
            "claim": "fv1 supplies 48 prompts and three weighted criteria for each fv2-passed case",
            "evidence_ids": json.dumps(
                sorted(
                    evidence_by_role["source-quality"]
                    + evidence_by_role["raw-case-result"]
                ),
                separators=(",", ":"),
            ),
        },
    ]
    write_csv(
        route / "evidence/claim-evidence-map.csv",
        claims,
        ("claim_id", "claim", "evidence_ids"),
    )

    primary = next(item for item in bundle.evidence if item.role == "source-workbook")
    prior = next(item for item in bundle.evidence if item.role == "indexed-prior-workbook")
    detailed = next(
        item for item in bundle.evidence if item.source_label == "fv2 final detailed results"
    )
    quality = next(
        item for item in bundle.evidence if item.source_label == "fv1 quality details"
    )
    reproduction = route / "reproduction/README.md"
    reproduction.parent.mkdir(parents=True, exist_ok=True)
    reproduction.write_text(
        "# Reproducing the official OpenVINO normalization\n\n"
        "Run from the repository root with the pinned portable interpreter:\n\n"
        "```powershell\n"
        "& '.tools/python311-portable/python.exe' -c \"from pathlib import Path; "
        "from scripts.testing.final_results.openvino_adapter import write_official_route; "
        "write_official_route(Path.cwd())\"\n"
        "```\n\n"
        "## Authority boundary\n\n"
        f"- `{detailed.relative_path}` ({detailed.sha256}) is authoritative for all 45 final statuses.\n"
        f"- `{quality.relative_path}` ({quality.sha256}) and the indexed fv1 raw results supply observations only for the 15 fv2-passed cases.\n"
        f"- `{primary.relative_path}` ({primary.sha256}) is the revised workbook copied byte-identically into `../results/source/` as evidence-only/nonportable.\n"
        f"- `{prior.relative_path}` ({prior.sha256}) is indexed as prior evidence and is not duplicated.\n\n"
        "The primary handoff is `../workbook/generated/"
        "openvino-official-upstream-portable-results.xlsx`; its adjacent provenance "
        "receipt records the 15 absolute-path replacements and source/output hashes. "
        "The adapter does not rerun inference. It rejects missing passed evidence, any "
        "published metric on a non-passed fv2 row, source conflicts, and missing final "
        "failure manifests. Regenerate the non-destructive checksum manifest only after "
        "the complete report, portable workbook, and receipt set is present.\n",
        encoding="utf-8",
        newline="\n",
    )

    coverage, data = build_official_validation_receipts(
        root,
        bundle,
        prompt_rows=prompt_rows,
        output_rows=output_rows,
        availability_rows=availability_rows,
        model_artifact_rows=model_artifacts,
        source_location_rows=source_location_rows,
    )
    write_json(route / "validation/coverage-validation.json", coverage)
    write_json(route / "validation/data-validation.json", data)
    if not coverage["valid"] or not data["valid"]:
        raise ValueError("generated official validation receipts contain failed checks")
    manifest = route / "evidence/manifest-sha256.txt"
    write_json(
        route / "validation/integrity-validation.json",
        {
            "manifest": manifest.relative_to(root).as_posix(),
            "status": "valid",
            "workbook_copy_sha256": hash_file(copied_workbook),
            "indexed_prior_workbook_sha256": prior.sha256,
        },
    )
    files_before_manifest = sorted(
        path for path in route.rglob("*") if path.is_file() and path != manifest
    )
    write_sha256_manifest(root, files_before_manifest, manifest)
    errors = validate_sha256_manifest(root, manifest)
    if errors:
        raise ValueError(f"generated official integrity manifest is invalid: {errors}")
    return bundle


__all__ = [
    "build_experimental_bundle",
    "build_experimental_validation_receipts",
    "build_official_bundle",
    "build_official_validation_receipts",
    "write_experimental_route",
    "write_official_route",
]
