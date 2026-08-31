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
        measurements.append(
            MeasurementRecord(
                route_id=_EXPERIMENTAL_ROUTE_ID,
                campaign_id=_EXPERIMENTAL_CAMPAIGN_ID,
                test_case_id=case_id,
                attempt_id=attempt_id,
                measurement_id=f"{case_id}--benchmark-repetition-{index:03d}",
                run_id=f"{case_id}--benchmark",
                repetition_id=f"{index:03d}",
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
    worst_peak_ids = tuple(
        item.measurement_id
        for item in measurements
        if item.peak_working_set_bytes == worst_peak
    )

    summaries = [
        SummaryRecord(
            route_id=_EXPERIMENTAL_ROUTE_ID,
            campaign_id=_EXPERIMENTAL_CAMPAIGN_ID,
            test_case_id=case_id,
            summary_id=f"{case_id}--decode-tps-median",
            metric_name="generation_tokens_per_second",
            value=median_decode,
            unit="tokens_per_second",
            aggregation="median over three executed benchmark repetitions",
            source_measurement_ids=measurement_ids,
        ),
        SummaryRecord(
            route_id=_EXPERIMENTAL_ROUTE_ID,
            campaign_id=_EXPERIMENTAL_CAMPAIGN_ID,
            test_case_id=case_id,
            summary_id=f"{case_id}--ttft-selected-decode-median",
            metric_name="time_to_first_token",
            value=selected.latency_ms,
            unit="milliseconds",
            aggregation="value from the repetition selected by median decode throughput",
            source_measurement_ids=(selected.measurement_id,),
        ),
        SummaryRecord(
            route_id=_EXPERIMENTAL_ROUTE_ID,
            campaign_id=_EXPERIMENTAL_CAMPAIGN_ID,
            test_case_id=case_id,
            summary_id=f"{case_id}--peak-working-set-worst-observed",
            metric_name="peak_working_set_bytes",
            value=worst_peak,
            unit="bytes",
            aggregation="maximum over three executed benchmark repetitions",
            source_measurement_ids=worst_peak_ids,
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
        attempt_id = f"{case_id}--attempt-001"
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
                    failure_id=f"{case_id}--artifact-unavailable",
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
                quality_id=(
                    f"{row['case_id']}--{row['prompt_id']}--"
                    f"{row['category']}--{row['criterion_id']}"
                ),
                prompt_id=row["prompt_id"],
                criterion_id=f"{row['category']}:{row['criterion_id']}",
                score=float(row["points_awarded"]),
                maximum_score=float(row["weight"]),
                prompt_suite_id=row["prompt_set_id"],
                rubric_id="objective-quality-weighted-5-3-2-output-health-gate",
                scoring_version="experimental-openvino-objective-quality/v2",
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


def _receipt_check(actual: object, expected: object) -> dict[str, object]:
    return {"actual": actual, "expected": expected, "passed": actual == expected}


def _experimental_validation_receipts(
    bundle: RouteBundle,
    prompt_rows: Sequence[Mapping[str, object]],
    output_rows: Sequence[Mapping[str, object]],
) -> tuple[dict[str, object], dict[str, object]]:
    passed_attempts = {item.test_case_id: item for item in bundle.attempts if item.executed}
    unavailable_cases = {
        item.test_case_id for item in bundle.attempts if not item.executed
    }
    evidence_ids = {item.evidence_id for item in bundle.evidence}
    attempt_ids = {item.attempt_id for item in bundle.attempts}
    measurement_ids = {item.measurement_id for item in bundle.measurements}
    prompt_keys = {
        (str(row["prompt_suite_id"]), str(row["prompt_id"])) for row in prompt_rows
    }

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
        "artifact_unavailable_count": _receipt_check(len(unavailable_cases), 54),
        "cache_format_count": _receipt_check(
            len({item.cache_format_id for item in bundle.attempts}), 9
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
            3,
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
        "artifact_unavailable": len(unavailable_cases),
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

    attempt_reference_errors = sorted(
        item.attempt_id
        for item in bundle.attempts
        if any(evidence_id not in evidence_ids for evidence_id in item.evidence_ids)
    )
    measurement_reference_errors = sorted(
        item.measurement_id
        for item in bundle.measurements
        if item.attempt_id not in attempt_ids
        or item.test_case_id not in passed_attempts
        or item.source_evidence_id not in evidence_ids
    )
    summary_reference_errors = sorted(
        item.summary_id
        for item in bundle.summaries
        if item.test_case_id not in passed_attempts
        or not item.source_measurement_ids
        or any(source_id not in measurement_ids for source_id in item.source_measurement_ids)
    )
    quality_reference_errors = sorted(
        item.quality_id
        for item in bundle.quality
        if item.test_case_id not in passed_attempts
        or item.source_evidence_id not in evidence_ids
        or item.prompt_id is None
        or item.criterion_id is None
    )
    failure_reference_errors = sorted(
        item.failure_id
        for item in bundle.failures
        if item.attempt_id not in attempt_ids
        or item.test_case_id not in unavailable_cases
        or any(evidence_id not in evidence_ids for evidence_id in item.evidence_ids)
    )
    prompt_reference_errors = sorted(
        str(row["prompt_id"])
        for row in prompt_rows
        if str(row["input_evidence_id"]) not in evidence_ids
    )
    output_keys = [
        (str(row["test_case_id"]), str(row["prompt_id"])) for row in output_rows
    ]
    output_reference_errors = sorted(
        f"{row['test_case_id']}/{row['prompt_id']}"
        for row in output_rows
        if str(row["source_evidence_id"]) not in evidence_ids
        or str(row["test_case_id"]) not in passed_attempts
        or not any(prompt_id == str(row["prompt_id"]) for _, prompt_id in prompt_keys)
    )
    duplicate_output_keys = sorted(
        f"{case_id}/{prompt_id}"
        for (case_id, prompt_id), count in Counter(output_keys).items()
        if count != 1
    )
    output_reference_errors.extend(duplicate_output_keys)

    criteria_by_prompt: dict[tuple[str, str | None], set[str | None]] = defaultdict(set)
    for item in bundle.quality:
        criteria_by_prompt[(item.test_case_id, item.prompt_id)].add(item.criterion_id)
    distinct_criteria_actual = sorted(
        set(len(criteria) for criteria in criteria_by_prompt.values())
    )
    unavailable_exclusions = {
        "measurements": sorted(
            {item.test_case_id for item in bundle.measurements} & unavailable_cases
        ),
        "outputs": sorted(
            {str(row["test_case_id"]) for row in output_rows} & unavailable_cases
        ),
        "quality": sorted({item.test_case_id for item in bundle.quality} & unavailable_cases),
        "summaries": sorted(
            {item.test_case_id for item in bundle.summaries} & unavailable_cases
        ),
    }
    empty_exclusions = {
        "measurements": [],
        "outputs": [],
        "quality": [],
        "summaries": [],
    }
    data_checks = {
        "attempt_identifier_uniqueness": _receipt_check(
            len({item.attempt_id for item in bundle.attempts}), 81
        ),
        "attempt_source_evidence_references": _receipt_check(
            attempt_reference_errors, []
        ),
        "exactly_three_distinct_criteria_per_prompt": _receipt_check(
            distinct_criteria_actual, [3]
        ),
        "failure_count": _receipt_check(len(bundle.failures), 54),
        "failure_references": _receipt_check(failure_reference_errors, []),
        "measurement_count": _receipt_check(len(bundle.measurements), 81),
        "measurement_references": _receipt_check(measurement_reference_errors, []),
        "output_count": _receipt_check(len(output_rows), 1296),
        "output_references": _receipt_check(output_reference_errors, []),
        "prompt_count": _receipt_check(len(prompt_rows), 48),
        "prompt_references": _receipt_check(prompt_reference_errors, []),
        "quality_criterion_count": _receipt_check(len(bundle.quality), 3888),
        "quality_references": _receipt_check(quality_reference_errors, []),
        "summary_count": _receipt_check(len(bundle.summaries), 81),
        "summary_references": _receipt_check(summary_reference_errors, []),
        "unavailable_exclusions": _receipt_check(
            unavailable_exclusions, empty_exclusions
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
    repo_root: Path, bundle: RouteBundle
) -> tuple[dict[str, object], dict[str, object]]:
    """Recompute named fv6 coverage and referential-integrity checks."""
    root = Path(repo_root).resolve(strict=True)
    prompt_rows, output_rows = _prompt_and_output_rows(root, bundle)
    return _experimental_validation_receipts(bundle, prompt_rows, output_rows)


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
    availability_rows = [
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
        "## Manifest update boundary\n\n"
        "`../evidence/manifest-sha256.txt` is an exact receipt for the current planned "
        "route file set. Task 8 will add generated report and workbook artifacts. After "
        "that planned set is complete, regenerate the manifest deliberately and validate "
        "it. The non-destructive manifest API intentionally refuses to overwrite a "
        "different existing receipt during a routine adapter rerun.\n",
        encoding="utf-8",
        newline="\n",
    )

    coverage_validation, data_validation = _experimental_validation_receipts(
        bundle, prompt_rows, output_rows
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


__all__ = [
    "build_experimental_bundle",
    "build_experimental_validation_receipts",
    "write_experimental_route",
]
