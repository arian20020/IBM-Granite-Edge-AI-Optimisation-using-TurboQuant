"""Read-only validation gates for unified final-results route collections."""

from __future__ import annotations

import csv
import hashlib
import json
import math
import re
import statistics
import subprocess
from dataclasses import dataclass
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Iterable, Mapping, Sequence

from jsonschema import Draft202012Validator
from docx import Document
from pypdf import PdfReader

from .csvio import write_json
from .evidence import hash_file, validate_sha256_manifest
from .parity import compare_markdown_docx


GATE_ORDER = (
    "schema",
    "ids",
    "coverage",
    "derivation",
    "status_failure_consistency",
    "availability",
    "paths_hashes",
    "claim_coverage",
    "workbook_parity",
    "pdf_structure",
    "comparability",
    "release_metadata",
    "release_readiness",
)

_SCHEMA_FILES = {
    "manifest": "route-manifest.schema.json",
    "attempts": "attempts.schema.json",
    "measurements": "measurements.schema.json",
    "summaries": "results.schema.json",
    "quality": "quality.schema.json",
    "failures": "failures.schema.json",
    "evidence": "evidence.schema.json",
}
_CSV_FILES = {
    "attempts": "results/attempts.csv",
    "measurements": "results/measurements.csv",
    "summaries": "results/summary-results.csv",
    "quality": "quality/scores.csv",
    "failures": "failures/failure-register.csv",
    "evidence": "evidence/evidence-index.csv",
}
_ID_FIELDS = {
    "attempts": "attempt_id",
    "measurements": "measurement_id",
    "summaries": "summary_id",
    "quality": "quality_id",
    "failures": "failure_id",
    "evidence": "evidence_id",
}
_METRIC_FIELDS = {
    "time_to_first_token": "latency_ms",
    "latency_ms": "latency_ms",
    "prompt_tokens_per_second": "prompt_tokens_per_second",
    "generation_tokens_per_second": "generation_tokens_per_second",
    "peak_working_set_bytes": "peak_working_set_bytes",
}
_DERIVATION_LIMITATION_ALLOWLIST = (
    {
        (
            "upstream-llama-cpp",
            f"{case_id}--kv_cache_allocated_bytes",
            "kv_cache_allocated_bytes",
            "median of exactly three deduplicated runtime KV allocations",
            3,
        ): "KV allocation is retained only in route-specific source evidence, not a canonical measurement column"
        for case_id in (
            "UL-01", "UL-02", "UL-03", "UL-04", "UL-05", "UL-06", "UL-07",
            "UL-08", "UL-09", "UL-10", "UL-11", "UL-12", "UL-13",
        )
    }
    | {
        (
            "atomicbot-turboquant",
            f"{case_id}--kv_cache_allocated_bytes",
            "kv_cache_allocated_bytes",
            "median of three validated repetitions",
            3,
        ): "KV allocation is retained only in route-specific source evidence, not a canonical measurement column"
        for case_id in (
            "AB-01", "AB-02", "AB-03", "AB-04", "AB-05", "AB-06", "AB-07",
            "AB-08F", "AB-08Q", "AB-09", "AB-10", "AB-11", "AB-12", "AB-13",
            "AB-14", "AB-15", "AB-15M", "AB-KV3-F16-4K", "AB-KV8-F16-4K",
        )
    }
    | {
        (
            "atomicbot-turboquant",
            f"{case_id}--{metric}",
            metric,
            "arithmetic mean of three validated repetition means",
            3,
        ): f"{label} utilization means are retained only in route-specific source evidence, not a canonical measurement column"
        for case_id in (
            "AB-01", "AB-02", "AB-03", "AB-04", "AB-05", "AB-06", "AB-07",
            "AB-08F", "AB-08Q", "AB-09", "AB-10", "AB-11", "AB-12", "AB-13",
            "AB-14", "AB-15", "AB-15M", "AB-KV3-F16-4K", "AB-KV8-F16-4K",
        )
        for metric, label in (("cpu_mean_percent", "CPU"), ("gpu_mean_percent", "GPU"))
    }
    | {
        (
            "animehacker-tq3-0",
            f"{case_id}--kv_cache_allocated_bytes",
            "kv_cache_allocated_bytes",
            "median of exactly 3 explicitly included formal samples",
            3,
        ): "KV allocation is retained only in route-specific source evidence, not a canonical measurement column"
        for case_id in ("AH-01", "AH-02", "AH-03", "AH-04", "AH-05", "AH-08", "AH-09")
    }
)
_EXPECTED_ROUTE_DIRECTORIES = {
    "01-upstream-llama-cpp": "upstream-llama-cpp",
    "02-atomicbot-turboquant": "atomicbot-turboquant",
    "03-animehacker-tq3-0": "animehacker-tq3-0",
    "04-openvino-experimental-fork": "openvino-experimental-fork",
    "05-openvino-official-upstream": "openvino-official-upstream",
    "06-cross-route-comparison": "cross-route-comparison",
}
_CATALOG_NUMERIC_FIELDS = {
    "attempt_count",
    "measurement_count",
    "summary_count",
    "quality_count",
    "failure_count",
    "evidence_count",
    "value",
    "prompt_count",
    "record_count",
    "awarded_points",
    "denominator",
    "normalized_score_10",
    "size_bytes",
    "matched_case_count",
    "left_only_case_count",
    "right_only_case_count",
}
_TASK14_METADATA = (
    "README.md",
    "CHANGELOG.md",
    "REPRODUCING.md",
    "LICENSES.md",
    "ro-crate-metadata.json",
    "manifest-sha256.txt",
    "validation/release-readiness.json",
    "validation/validation-summary.md",
)
_RELEASE_ROUTES = tuple(_EXPECTED_ROUTE_DIRECTORIES)
_REPORT_STEMS = {
    "01-upstream-llama-cpp": "upstream-llama-cpp-final-report",
    "02-atomicbot-turboquant": "atomicbot-turboquant-final-report",
    "03-animehacker-tq3-0": "animehacker-tq3-0-final-report",
    "04-openvino-experimental-fork": "openvino-experimental-fork-final-report",
    "05-openvino-official-upstream": "openvino-official-upstream-final-report",
    "06-cross-route-comparison": "cross-route-comparison-final-report",
}
_RELEASE_REPORT_PATHS = tuple(
    f"{route}/workbook/{folder}/{stem}.{suffix}"
    for route, stem in _REPORT_STEMS.items()
    for folder, suffix in (
        ("source", "md"),
        ("generated", "docx"),
        ("generated", "pdf"),
    )
)
_OPENVINO_SOURCE_WORKBOOK_PATHS = (
    "04-openvino-experimental-fork/results/source/"
    "Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx",
    "05-openvino-official-upstream/results/source/"
    "Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx",
)
_OPENVINO_PORTABLE_WORKBOOK_PATHS = (
    "04-openvino-experimental-fork/workbook/generated/"
    "openvino-experimental-fork-portable-results.xlsx",
    "05-openvino-official-upstream/workbook/generated/"
    "openvino-official-upstream-portable-results.xlsx",
)
_OPENVINO_WORKBOOK_RECEIPT_PATHS = (
    "04-openvino-experimental-fork/workbook/generated/portable-workbook-provenance.json",
    "05-openvino-official-upstream/workbook/generated/portable-workbook-provenance.json",
)
_OPENVINO_WORKBOOK_PATHS = (
    *_OPENVINO_SOURCE_WORKBOOK_PATHS,
    *_OPENVINO_PORTABLE_WORKBOOK_PATHS,
)
_EXPECTED_PDF_PAGE_COUNTS = {
    f"{route}/workbook/generated/{stem}.pdf": page_count
    for (route, stem), page_count in zip(
        _REPORT_STEMS.items(),
        (47, 22, 7, 54, 52, 20),
        strict=True,
    )
}
_EXPECTED_WORKBOOK_QA = {
    _OPENVINO_SOURCE_WORKBOOK_PATHS[0]: {
        "sheets": (
            "Dashboard",
            "Format Comparison",
            "Sector Summary",
            "Detailed Results",
            "Quality Details",
            "Availability Matrix",
            "Methodology",
            "Source Data",
        ),
        "sheet_dimensions": {
            "Dashboard": "A1:N28",
            "Format Comparison": "A1:M17",
            "Sector Summary": "A1:N32",
            "Detailed Results": "A1:AF82",
            "Quality Details": "A1:U3889",
            "Availability Matrix": "A1:J20",
            "Methodology": "A1:H37",
            "Source Data": "A1:BB83",
        },
        "formula_count": 275,
        "cached_formula_value_count": 0,
        "machine_absolute_path_count": 55,
        "role": "immutable_evidence_only_nonportable",
    },
    _OPENVINO_SOURCE_WORKBOOK_PATHS[1]: {
        "sheets": (
            "Executive Summary",
            "Format Comparison",
            "Sector Summary",
            "Detailed Results",
            "Quality Details",
            "Availability Matrix",
            "Missing Attempts",
            "Methodology",
            "Source Data",
        ),
        "sheet_dimensions": {
            "Executive Summary": "A1:L29",
            "Format Comparison": "A1:M14",
            "Sector Summary": "A1:I185",
            "Detailed Results": "A1:AJ50",
            "Quality Details": "A1:U2165",
            "Availability Matrix": "A1:G14",
            "Missing Attempts": "A1:K11",
            "Methodology": "A1:H31",
            "Source Data": "A1:E16",
        },
        "formula_count": 0,
        "cached_formula_value_count": 0,
        "machine_absolute_path_count": 15,
        "role": "immutable_evidence_only_nonportable",
    },
}
_EXPECTED_WORKBOOK_QA[_OPENVINO_PORTABLE_WORKBOOK_PATHS[0]] = {
    **_EXPECTED_WORKBOOK_QA[_OPENVINO_SOURCE_WORKBOOK_PATHS[0]],
    "machine_absolute_path_count": 0,
    "role": "primary_portable_derivative",
}
_EXPECTED_WORKBOOK_QA[_OPENVINO_PORTABLE_WORKBOOK_PATHS[1]] = {
    **_EXPECTED_WORKBOOK_QA[_OPENVINO_SOURCE_WORKBOOK_PATHS[1]],
    "machine_absolute_path_count": 0,
    "role": "primary_portable_derivative",
}
_RELEASE_CATALOG_PATHS = (
    "catalog/route-register.csv",
    "catalog/campaign-summary.csv",
    "catalog/performance-summary.csv",
    "catalog/quality-summary.csv",
    "catalog/failure-summary.csv",
    "catalog/evidence-manifest.csv",
    "catalog/claim-evidence-map.csv",
    "catalog/comparability-matrix.csv",
)


@dataclass(frozen=True, slots=True)
class ValidationIssue:
    """One stable, machine-readable validation finding."""

    code: str
    message: str
    path: str = ""
    blocking: bool = True

    def to_dict(self) -> dict[str, object]:
        return {
            "code": self.code,
            "message": self.message,
            "path": self.path,
            "blocking": self.blocking,
        }


@dataclass(frozen=True, slots=True)
class GateResult:
    """Result for one gate in the fixed release order."""

    name: str
    issues: tuple[ValidationIssue, ...] = ()
    limitations: tuple[str, ...] = ()

    @property
    def valid(self) -> bool:
        return not any(issue.blocking for issue in self.issues)

    def to_dict(self) -> dict[str, object]:
        return {
            "name": self.name,
            "valid": self.valid,
            "issues": [issue.to_dict() for issue in self.issues],
            "limitations": list(self.limitations),
        }


@dataclass(frozen=True, slots=True)
class ValidationReport:
    """Complete ordered validation result for one route or collection."""

    scope: str
    root: Path
    gates: tuple[GateResult, ...]

    @property
    def valid(self) -> bool:
        return all(gate.valid for gate in self.gates)

    def gate(self, name: str) -> GateResult:
        for gate in self.gates:
            if gate.name == name:
                return gate
        raise KeyError(name)

    def to_dict(self) -> dict[str, object]:
        return {
            "scope": self.scope,
            "root": str(self.root),
            "valid": self.valid,
            "gate_order": list(GATE_ORDER),
            "gates": [gate.to_dict() for gate in self.gates],
        }


@dataclass(slots=True)
class _RouteData:
    root: Path
    manifest: dict[str, object]
    rows: dict[str, list[dict[str, object]]]
    raw_rows: dict[str, list[dict[str, str]]]
    load_issues: list[ValidationIssue]


def _schema_root() -> Path:
    return Path(__file__).resolve().parents[3] / "docs/testing/final-results/standards/schemas"


def _relative(root: Path, path: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return str(path)


def _issue(code: str, message: str, path: Path | str = "") -> ValidationIssue:
    return ValidationIssue(code, message, str(path))


def _read_schema(name: str) -> dict[str, object]:
    return json.loads((_schema_root() / _SCHEMA_FILES[name]).read_text(encoding="utf-8"))


def _coerce_csv_row(
    raw: Mapping[str, str], schema: Mapping[str, object]
) -> dict[str, object]:
    properties = schema.get("properties", {})
    converted: dict[str, object] = {}
    for key, raw_value in raw.items():
        definition = properties.get(key, {}) if isinstance(properties, Mapping) else {}
        declared = definition.get("type") if isinstance(definition, Mapping) else None
        allowed = set(declared if isinstance(declared, list) else (declared,))
        if raw_value == "" and "null" in allowed:
            converted[key] = None
        elif "array" in allowed:
            converted[key] = json.loads(raw_value or "[]")
        elif "boolean" in allowed:
            normalized = raw_value.casefold()
            if normalized not in {"true", "false"}:
                converted[key] = raw_value
            else:
                converted[key] = normalized == "true"
        elif "integer" in allowed:
            try:
                converted[key] = int(raw_value)
            except ValueError:
                converted[key] = raw_value
        elif "number" in allowed:
            try:
                converted[key] = float(raw_value)
            except ValueError:
                converted[key] = raw_value
        else:
            converted[key] = raw_value
    return converted


def _load_route(route_root: Path) -> _RouteData:
    route = Path(route_root).resolve()
    issues: list[ValidationIssue] = []
    manifest: dict[str, object] = {}
    manifest_path = route / "route-manifest.json"
    try:
        value = json.loads(manifest_path.read_text(encoding="utf-8"))
        if isinstance(value, dict):
            manifest = value
        else:
            issues.append(_issue("invalid_json_shape", "route manifest must be an object", manifest_path))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        issues.append(_issue("invalid_or_missing_json", str(error), manifest_path))

    rows: dict[str, list[dict[str, object]]] = {}
    raw_rows: dict[str, list[dict[str, str]]] = {}
    for name, relative_path in _CSV_FILES.items():
        path = route / relative_path
        schema = _read_schema(name)
        parsed_raw: list[dict[str, str]] = []
        parsed: list[dict[str, object]] = []
        try:
            with path.open(encoding="utf-8", newline="") as handle:
                reader = csv.DictReader(handle)
                if reader.fieldnames is None:
                    raise ValueError("missing CSV header")
                for row_number, raw in enumerate(reader, start=2):
                    normalized = {str(key): value or "" for key, value in raw.items() if key is not None}
                    parsed_raw.append(normalized)
                    try:
                        parsed.append(_coerce_csv_row(normalized, schema))
                    except (TypeError, ValueError, json.JSONDecodeError) as error:
                        issues.append(
                            _issue(
                                "invalid_csv_value",
                                f"{name} row {row_number}: {error}",
                                path,
                            )
                        )
        except (OSError, UnicodeError, csv.Error, ValueError) as error:
            issues.append(_issue("invalid_or_missing_csv", f"{name}: {error}", path))
        rows[name] = parsed
        raw_rows[name] = parsed_raw
    return _RouteData(route, manifest, rows, raw_rows, issues)


def _gate_schema(data: _RouteData) -> GateResult:
    issues = list(data.load_issues)
    is_cross = data.root.name.startswith("06-")
    if is_cross:
        required = {
            "route_id",
            "revision",
            "generated_date",
            "source_route_ids",
            "source_campaign_ids",
            "universal_ranking_permitted",
        }
        missing = sorted(required - set(data.manifest))
        if missing:
            issues.append(
                _issue("cross_route_manifest_missing_fields", f"missing fields: {missing}", data.root / "route-manifest.json")
            )
        if data.manifest.get("universal_ranking_permitted") is not False:
            issues.append(
                _issue("universal_ranking_permitted", "cross-route manifest must prohibit universal ranking", data.root / "route-manifest.json")
            )
        # Cross-route packages deliberately do not duplicate canonical route tables.
        issues = [issue for issue in issues if issue.code not in {"invalid_or_missing_csv"}]
        return GateResult("schema", tuple(issues))

    validators = {name: Draft202012Validator(_read_schema(name)) for name in _SCHEMA_FILES}
    for error in validators["manifest"].iter_errors(data.manifest):
        pointer = "/" + "/".join(str(part) for part in error.absolute_path)
        issues.append(_issue("schema_validation_error", f"manifest{pointer}: {error.message}", data.root / "route-manifest.json"))
    for name, records in data.rows.items():
        validator = validators[name]
        for index, record in enumerate(records, start=2):
            for error in validator.iter_errors(record):
                pointer = "/" + "/".join(str(part) for part in error.absolute_path)
                issues.append(
                    _issue(
                        "schema_validation_error",
                        f"{name} row {index}{pointer}: {error.message}",
                        data.root / _CSV_FILES[name],
                    )
                )
    count_fields = {
        "attempts": "attempt_count",
        "measurements": "measurement_count",
        "summaries": "summary_count",
        "quality": "quality_count",
        "failures": "failure_count",
        "evidence": "evidence_count",
    }
    for name, manifest_field in count_fields.items():
        if data.manifest.get(manifest_field) != len(data.rows[name]):
            issues.append(
                _issue(
                    "manifest_count_mismatch",
                    f"{manifest_field} does not match {name} rows",
                    data.root / "route-manifest.json",
                )
            )
    return GateResult("schema", tuple(issues))


def _duplicates(values: Iterable[object]) -> set[str]:
    seen: set[str] = set()
    duplicates: set[str] = set()
    for value in values:
        normalized = str(value)
        if normalized in seen:
            duplicates.add(normalized)
        seen.add(normalized)
    return duplicates


def _gate_ids(data: _RouteData) -> GateResult:
    if data.root.name.startswith("06-"):
        return GateResult("ids")
    issues: list[ValidationIssue] = []
    for name, field in _ID_FIELDS.items():
        for duplicate in sorted(_duplicates(row.get(field) for row in data.rows[name])):
            issues.append(
                _issue(f"duplicate_{field}", f"duplicate {field}: {duplicate}", data.root / _CSV_FILES[name])
            )
    route_id = str(data.manifest.get("route_id", ""))
    campaign_id = str(data.manifest.get("campaign_id", ""))
    attempts = {str(row.get("attempt_id")): row for row in data.rows["attempts"]}
    test_ids = {str(row.get("test_case_id")) for row in data.rows["attempts"]}
    measurement_ids = {str(row.get("measurement_id")) for row in data.rows["measurements"]}
    evidence_ids = {str(row.get("evidence_id")) for row in data.rows["evidence"]}
    for name, rows in data.rows.items():
        for row in rows:
            record_id = str(row.get(_ID_FIELDS[name], ""))
            if str(row.get("route_id", "")) != route_id:
                issues.append(
                    _issue(
                        "row_route_mismatch",
                        f"{name} {record_id} route_id does not match route manifest",
                        data.root / _CSV_FILES[name],
                    )
                )
            if str(row.get("campaign_id", "")) != campaign_id:
                issues.append(
                    _issue(
                        "row_campaign_mismatch",
                        f"{name} {record_id} campaign_id does not match route manifest",
                        data.root / _CSV_FILES[name],
                    )
                )

    def reference_ids(value: object) -> tuple[str, ...]:
        return tuple(str(item) for item in value) if isinstance(value, (list, tuple)) else ()

    for attempt in data.rows["attempts"]:
        for evidence_id in reference_ids(attempt.get("evidence_ids")):
            if evidence_id not in evidence_ids:
                issues.append(
                    _issue(
                        "unknown_attempt_evidence_reference",
                        f"attempt {attempt.get('attempt_id')} references unknown evidence {evidence_id}",
                        data.root / _CSV_FILES["attempts"],
                    )
                )
    for name in ("measurements", "failures"):
        for row in data.rows[name]:
            attempt = attempts.get(str(row.get("attempt_id")))
            if attempt is None:
                issues.append(_issue("unknown_attempt_reference", f"{name} references unknown attempt {row.get('attempt_id')}", data.root / _CSV_FILES[name]))
            if str(row.get("test_case_id")) not in test_ids:
                issues.append(_issue("unknown_test_case_reference", f"{name} references unknown test case {row.get('test_case_id')}", data.root / _CSV_FILES[name]))
            if attempt is not None and str(row.get("test_case_id")) != str(attempt.get("test_case_id")):
                issues.append(
                    _issue(
                        "attempt_test_case_mismatch",
                        f"{name} record {row.get(_ID_FIELDS[name])} test case differs from attempt {row.get('attempt_id')}",
                        data.root / _CSV_FILES[name],
                    )
                )
    for name in ("measurements", "quality"):
        for row in data.rows[name]:
            evidence_id = str(row.get("source_evidence_id") or "")
            record_id = str(row.get(_ID_FIELDS[name], ""))
            if not evidence_id or evidence_id not in evidence_ids:
                issues.append(
                    _issue(
                        "unknown_source_evidence_reference",
                        f"{name} {record_id} references unknown source evidence {evidence_id or '<missing>'}",
                        data.root / _CSV_FILES[name],
                    )
                )
    for row in data.rows["quality"]:
        if str(row.get("test_case_id")) not in test_ids:
            issues.append(
                _issue(
                    "unknown_test_case_reference",
                    f"quality references unknown test case {row.get('test_case_id')}",
                    data.root / _CSV_FILES["quality"],
                )
            )
    for row in data.rows["summaries"]:
        for measurement_id in row.get("source_measurement_ids", ()) or ():
            if str(measurement_id) not in measurement_ids:
                issues.append(_issue("unknown_measurement_reference", f"summary references unknown measurement {measurement_id}", data.root / _CSV_FILES["summaries"]))
                continue
            measurement = next(
                item
                for item in data.rows["measurements"]
                if str(item.get("measurement_id")) == str(measurement_id)
            )
            if str(measurement.get("test_case_id")) != str(row.get("test_case_id")):
                issues.append(
                    _issue(
                        "summary_measurement_case_mismatch",
                        f"summary {row.get('summary_id')} references measurement {measurement_id} from another test case",
                        data.root / _CSV_FILES["summaries"],
                    )
                )
        if str(row.get("test_case_id")) not in test_ids:
            issues.append(
                _issue(
                    "unknown_test_case_reference",
                    f"summary references unknown test case {row.get('test_case_id')}",
                    data.root / _CSV_FILES["summaries"],
                )
            )
    for failure in data.rows["failures"]:
        for evidence_id in reference_ids(failure.get("evidence_ids")):
            if evidence_id not in evidence_ids:
                issues.append(
                    _issue(
                        "unknown_failure_evidence_reference",
                        f"failure {failure.get('failure_id')} references unknown evidence {evidence_id}",
                        data.root / _CSV_FILES["failures"],
                    )
                )
    for row in data.rows["evidence"]:
        for evidence_id in row.get("input_evidence_ids", ()) or ():
            if str(evidence_id) not in evidence_ids:
                issues.append(_issue("unknown_evidence_reference", f"derived evidence references unknown evidence {evidence_id}", data.root / _CSV_FILES["evidence"]))
    return GateResult("ids", tuple(issues))


def _matrix_ids(path: Path) -> tuple[set[str], list[ValidationIssue]]:
    issues: list[ValidationIssue] = []
    intended: set[str] = set()
    try:
        with path.open(encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            fields = reader.fieldnames or []
            id_field = next((name for name in ("test_case_id", "test_id", "ID", "id") if name in fields), None)
            if id_field is None:
                raise ValueError("no stable test-case ID column")
            for row in reader:
                intended_value = str(row.get("intended", "true")).strip().casefold()
                if intended_value not in {"false", "0", "no"}:
                    intended.add(str(row.get(id_field, "")).strip())
    except (OSError, UnicodeError, csv.Error, ValueError) as error:
        issues.append(_issue("invalid_intended_matrix", str(error), path))
    return intended, issues


def _gate_coverage(data: _RouteData) -> GateResult:
    if data.root.name.startswith("06-"):
        return GateResult("coverage")
    intended, issues = _matrix_ids(data.root / "protocol/intended-test-matrix.csv")
    attempts = {str(row.get("test_case_id")) for row in data.rows["attempts"]}
    for test_case_id in sorted(intended - attempts):
        issues.append(_issue("missing_intended_attempt", f"no attempt for intended test case {test_case_id}", data.root / "results/attempts.csv"))
    for test_case_id in sorted(attempts - intended):
        issues.append(_issue("unexpected_attempt", f"attempt is not in intended matrix: {test_case_id}", data.root / "results/attempts.csv"))
    return GateResult("coverage", tuple(issues))


def _expected_summary_value(
    summary: Mapping[str, object], measurements: Mapping[str, Mapping[str, object]]
) -> float | int | None:
    source_ids = tuple(str(value) for value in summary.get("source_measurement_ids", ()) or ())
    source_rows = [measurements[value] for value in source_ids if value in measurements]
    if len(source_rows) != len(source_ids) or not source_rows:
        return None
    metric = str(summary.get("metric_name"))
    aggregation = str(summary.get("aggregation", "")).casefold()
    if "selected by median decode throughput" in aggregation:
        ordered = sorted(
            source_rows,
            key=lambda row: float(row["generation_tokens_per_second"]),
        )
        return ordered[len(ordered) // 2].get("latency_ms")
    field = _METRIC_FIELDS.get(metric)
    if field is None:
        return None
    values = [row.get(field) for row in source_rows]
    if any(value is None or isinstance(value, bool) for value in values):
        return None
    numeric = [float(value) for value in values]
    if "median" in aggregation:
        return statistics.median(numeric)
    if "maximum" in aggregation or "worst" in aggregation or "peak" in aggregation:
        return max(numeric)
    if "minimum" in aggregation:
        return min(numeric)
    if "mean" in aggregation or "average" in aggregation:
        return statistics.fmean(numeric)
    return None


def _gate_derivation(data: _RouteData) -> GateResult:
    if data.root.name.startswith("06-"):
        return GateResult("derivation")
    issues: list[ValidationIssue] = []
    limitations: list[str] = []
    measurements = {str(row.get("measurement_id")): row for row in data.rows["measurements"]}
    for summary in data.rows["summaries"]:
        expected = _expected_summary_value(summary, measurements)
        if expected is None:
            source_ids = tuple(summary.get("source_measurement_ids", ()) or ())
            limitation_key = (
                str(data.manifest.get("route_id", "")),
                str(summary.get("summary_id", "")),
                str(summary.get("metric_name", "")),
                str(summary.get("aggregation", "")),
                len(source_ids),
            )
            rationale = _DERIVATION_LIMITATION_ALLOWLIST.get(limitation_key)
            if rationale is None:
                issues.append(
                    _issue(
                        "unsupported_summary_derivation",
                        f"{summary.get('summary_id')}: derivation is not recomputable and is not an approved legacy limitation",
                        data.root / _CSV_FILES["summaries"],
                    )
                )
            else:
                limitations.append(
                    f"{summary.get('summary_id')}: {rationale}; exact allowlist key={limitation_key!r}"
                )
            continue
        actual = summary.get("value")
        if not isinstance(actual, (int, float)) or isinstance(actual, bool) or not math.isclose(float(actual), float(expected), rel_tol=1e-9, abs_tol=1e-9):
            issues.append(
                _issue(
                    "derived_value_mismatch",
                    f"{summary.get('summary_id')}: expected {expected!r}, found {actual!r}",
                    data.root / _CSV_FILES["summaries"],
                )
            )
    return GateResult("derivation", tuple(issues), tuple(sorted(set(limitations))))


def _gate_status_failure(data: _RouteData) -> GateResult:
    if data.root.name.startswith("06-"):
        return GateResult("status_failure_consistency")
    issues: list[ValidationIssue] = []
    attempts = {str(row.get("attempt_id")): row for row in data.rows["attempts"]}
    failures_by_attempt: dict[str, list[Mapping[str, object]]] = {}
    for failure in data.rows["failures"]:
        failures_by_attempt.setdefault(str(failure.get("attempt_id")), []).append(failure)
    for measurement in data.rows["measurements"]:
        attempt = attempts.get(str(measurement.get("attempt_id")))
        if attempt is not None and attempt.get("status") != "passed":
            issues.append(
                _issue(
                    "measurement_for_non_passed_attempt",
                    f"measurement {measurement.get('measurement_id')} belongs to {attempt.get('status')} attempt",
                    data.root / _CSV_FILES["measurements"],
                )
            )
    for attempt_id, attempt in attempts.items():
        if attempt.get("status") == "passed":
            continue
        failures = failures_by_attempt.get(attempt_id, [])
        if not failures:
            issues.append(_issue("missing_failure_record", f"non-passed attempt has no failure record: {attempt_id}", data.root / _CSV_FILES["failures"]))
        elif not any(failure.get("status") == attempt.get("status") for failure in failures):
            issues.append(_issue("failure_status_mismatch", f"failure status does not match attempt {attempt_id}", data.root / _CSV_FILES["failures"]))
    return GateResult("status_failure_consistency", tuple(issues))


def _gate_availability(data: _RouteData) -> GateResult:
    if data.root.name.startswith("06-"):
        return GateResult("availability")
    issues: list[ValidationIssue] = []
    path = data.root / "results/availability-matrix.csv"
    try:
        with path.open(encoding="utf-8", newline="") as handle:
            availability = list(csv.DictReader(handle))
    except (OSError, UnicodeError, csv.Error) as error:
        return GateResult("availability", (_issue("invalid_availability_matrix", str(error), path),))
    attempts_by_test: dict[str, list[Mapping[str, object]]] = {}
    for attempt in data.rows["attempts"]:
        attempts_by_test.setdefault(str(attempt.get("test_case_id")), []).append(attempt)
    availability_by_test = {str(row.get("test_case_id")): row for row in availability}
    if len(availability_by_test) != len(availability):
        issues.append(_issue("duplicate_availability_test_case", "availability contains duplicate test-case rows", path))
    if set(attempts_by_test) != set(availability_by_test):
        issues.append(_issue("availability_case_set_mismatch", "availability test-case set differs from attempts", path))
    for test_case_id in sorted(set(attempts_by_test) & set(availability_by_test)):
        retained = attempts_by_test[test_case_id]
        terminal = [
            attempt
            for attempt in retained
            if str(attempt.get("attempt_id", "")).casefold().endswith("--terminal")
        ]
        if len(terminal) == 1:
            authoritative = terminal[0]
        elif len(retained) == 1:
            authoritative = retained[0]
        else:
            issues.append(
                _issue(
                    "ambiguous_terminal_attempt",
                    f"no unique authoritative terminal attempt for {test_case_id}",
                    data.root / _CSV_FILES["attempts"],
                )
            )
            continue
        published_row = availability_by_test[test_case_id]
        published = str(published_row.get("status", "")).casefold()
        expected = str(authoritative.get("status", "")).casefold()
        if published != expected:
            issues.append(
                _issue(
                    "availability_terminal_status_mismatch",
                    f"availability status {published!r} differs from terminal status {expected!r} for {test_case_id}",
                    path,
                )
            )
        for field in ("model_id", "weight_format_id", "cache_format_id", "backend_id"):
            published_value = str(published_row.get(field) or "")
            expected_value = str(authoritative.get(field) or "")
            if published_value != expected_value:
                issues.append(
                    _issue(
                        "availability_terminal_identity_mismatch",
                        f"availability {field} differs from terminal attempt for {test_case_id}",
                        path,
                    )
                )
    return GateResult("availability", tuple(issues))


def _repository_root(route: Path) -> Path:
    for candidate in (route, *route.parents):
        if (candidate / "scripts/testing/final_results").is_dir():
            return candidate
    return route.parent


def _gate_paths_hashes(data: _RouteData) -> GateResult:
    issues: list[ValidationIssue] = []
    limitations: list[str] = []
    repository_root = _repository_root(data.root).resolve()
    evidence_rows = () if data.root.name.startswith("06-") else data.rows["evidence"]
    for row in evidence_rows:
        relative_path = str(row.get("relative_path", ""))
        candidate = (repository_root / Path(relative_path)).resolve()
        try:
            candidate.relative_to(repository_root)
        except ValueError:
            issues.append(_issue("evidence_path_escape", f"evidence path escapes repository: {relative_path}", data.root / _CSV_FILES["evidence"]))
            continue
        if not candidate.is_file():
            issues.append(_issue("missing_evidence_path", f"missing evidence: {relative_path}", candidate))
            continue
        if hash_file(candidate).casefold() != str(row.get("sha256", "")).casefold():
            issues.append(_issue("evidence_hash_mismatch", f"SHA-256 mismatch: {relative_path}", candidate))
        if row.get("size_bytes") != candidate.stat().st_size:
            issues.append(_issue("evidence_size_mismatch", f"size mismatch: {relative_path}", candidate))
    manifest = data.root / "evidence/manifest-sha256.txt"
    if manifest.is_file():
        for error in validate_sha256_manifest(repository_root, manifest):
            if error in {
                "manifest: CRLF line endings are not canonical",
                "manifest: carriage-return line endings are not canonical",
            }:
                limitations.append(
                    f"{manifest.name}: {error}; entries and hashes were still validated"
                )
            else:
                issues.append(_issue("manifest_validation_error", error, manifest))
    return GateResult("paths_hashes", tuple(issues), tuple(limitations))


def _json_list(raw: object) -> list[str]:
    if isinstance(raw, list):
        return [str(value) for value in raw]
    if raw in (None, ""):
        return []
    value = json.loads(str(raw))
    if not isinstance(value, list):
        raise ValueError("expected JSON array")
    return [str(item) for item in value]


def _gate_claim_coverage(data: _RouteData) -> GateResult:
    if data.root.name.startswith("06-"):
        return GateResult("claim_coverage")
    issues: list[ValidationIssue] = []
    evidence_ids = {str(row.get("evidence_id")) for row in data.rows["evidence"]}
    path = data.root / "evidence/claim-evidence-map.csv"
    try:
        with path.open(encoding="utf-8", newline="") as handle:
            claims = list(csv.DictReader(handle))
    except (OSError, UnicodeError, csv.Error) as error:
        return GateResult("claim_coverage", (_issue("invalid_claim_map", str(error), path),))
    if not claims:
        issues.append(_issue("empty_claim_map", "claim map contains no material claims", path))
    for row_number, claim in enumerate(claims, start=2):
        raw = claim.get("evidence_ids", claim.get("evidence_ids_json", ""))
        try:
            referenced = _json_list(raw)
        except (TypeError, ValueError, json.JSONDecodeError) as error:
            issues.append(_issue("invalid_claim_evidence_ids", f"row {row_number}: {error}", path))
            continue
        if not referenced:
            issues.append(_issue("claim_without_evidence", f"row {row_number} has no evidence", path))
        for evidence_id in referenced:
            if evidence_id not in evidence_ids:
                issues.append(_issue("unknown_claim_evidence", f"row {row_number} references {evidence_id}", path))
    return GateResult("claim_coverage", tuple(issues))


def _single_file(directory: Path, suffix: str) -> Path | None:
    values = sorted(path for path in directory.glob(f"*{suffix}") if path.is_file())
    return values[0] if len(values) == 1 else None


def _gate_workbook_parity(data: _RouteData) -> GateResult:
    markdown = _single_file(data.root / "workbook/source", ".md")
    docx = _single_file(data.root / "workbook/generated", ".docx")
    if markdown is None or docx is None:
        return GateResult("workbook_parity", (_issue("missing_or_ambiguous_workbook", "exactly one Markdown and DOCX report are required", data.root / "workbook"),))
    try:
        comparison = compare_markdown_docx(markdown, docx)
        markdown_text = markdown.read_text(encoding="utf-8")
        document = Document(docx)
        narrative_paragraphs = [
            paragraph.text
            for paragraph in document.paragraphs
            if paragraph.text.strip()
            and paragraph.style is not None
            and paragraph.style.name != "Title"
            and not paragraph.style.name.startswith("Heading ")
        ]
        narrative_matches = all(text in markdown_text for text in narrative_paragraphs)
    except (OSError, UnicodeError, ValueError) as error:
        return GateResult("workbook_parity", (_issue("workbook_parity_error", str(error), data.root / "workbook"),))
    issues = () if comparison.get("matches") and narrative_matches else (
        _issue("workbook_parity_mismatch", "canonical Markdown and generated DOCX semantics differ", data.root / "workbook"),
    )
    return GateResult("workbook_parity", issues)


def _gate_pdf_structure(data: _RouteData) -> GateResult:
    pdf = _single_file(data.root / "workbook/generated", ".pdf")
    if pdf is None:
        return GateResult("pdf_structure", (_issue("missing_or_ambiguous_pdf", "exactly one generated PDF is required", data.root / "workbook/generated"),))
    issues: list[ValidationIssue] = []
    try:
        if not pdf.read_bytes().startswith(b"%PDF-"):
            issues.append(_issue("invalid_pdf_signature", "PDF signature is missing", pdf))
        reader = PdfReader(pdf)
        texts = [(page.extract_text() or "").strip() for page in reader.pages]
        if not texts or not all(texts):
            issues.append(_issue("blank_or_unsearchable_pdf_page", "every PDF page must contain searchable text", pdf))
    except (OSError, ValueError) as error:
        issues.append(_issue("invalid_pdf", str(error), pdf))
    return GateResult("pdf_structure", tuple(issues))


def _affirmative_ranking(text: str) -> bool:
    normalized = " ".join(text.casefold().replace("’", "'").split())
    patterns = (
        r"\b(?:route|repository|model|configuration)\s+[\w.-]+\s+(?:is|was)\s+(?:the\s+)?(?:best|worst|first|second|third|fourth|top)\b",
        r"\b(?:route|repository|model|configuration)\s+[\w.-]+\s+(?:ranks?|ranked|placed)\s+(?:the\s+)?(?:first|second|third|fourth|1st|2nd|3rd|4th|above|below)\b",
        r"\b(?:outperforms?|beats?)\b",
    )
    for sentence in re.split(r"(?<=[.!?;])\s+", normalized):
        for pattern in patterns:
            match = re.search(pattern, sentence)
            if match is None:
                continue
            prefix = sentence[: match.start()]
            if re.search(r"\b(?:no|not|never|cannot|can't|without|neither)\b", prefix):
                continue
            return True
    return False


def _gate_comparability(data: _RouteData) -> GateResult:
    if not data.root.name.startswith("06-"):
        return GateResult("comparability")
    issues: list[ValidationIssue] = []
    receipt_path = data.root / "validation/cross-route-validation.json"
    try:
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
        if receipt.get("valid") is not True or receipt.get("universal_ranking_present") is not False:
            issues.append(_issue("cross_route_receipt_failed", "cross-route validation receipt is not passing", receipt_path))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        issues.append(_issue("invalid_cross_route_receipt", str(error), receipt_path))
    markdown = _single_file(data.root / "workbook/source", ".md")
    if markdown is not None:
        try:
            if _affirmative_ranking(markdown.read_text(encoding="utf-8")):
                issues.append(_issue("unsupported_cross_route_ranking", "cross-route report contains an affirmative universal ranking", markdown))
        except (OSError, UnicodeError) as error:
            issues.append(_issue("invalid_cross_route_markdown", str(error), markdown))
    return GateResult("comparability", tuple(issues))


def _route_gates(data: _RouteData) -> list[GateResult]:
    gates = [
        _gate_schema(data),
        _gate_ids(data),
        _gate_coverage(data),
        _gate_derivation(data),
        _gate_status_failure(data),
        _gate_availability(data),
        _gate_paths_hashes(data),
        _gate_claim_coverage(data),
        _gate_workbook_parity(data),
        _gate_pdf_structure(data),
        _gate_comparability(data),
        GateResult("release_metadata"),
    ]
    blocking = [gate.name for gate in gates if not gate.valid]
    release_issues = () if not blocking else (
        _issue("release_blocked", f"blocking gates: {', '.join(blocking)}", data.root),
    )
    gates.append(GateResult("release_readiness", release_issues))
    return gates


def validate_route(route_root: Path) -> ValidationReport:
    """Validate one route without writing or repairing any artifact."""
    data = _load_route(Path(route_root))
    return ValidationReport("route", data.root, tuple(_route_gates(data)))


def _route_directories(root: Path) -> list[Path]:
    return sorted(
        path
        for path in Path(root).iterdir()
        if path.is_dir()
        and (
            re.match(r"^\d{2}-", path.name)
            or (path / "route-manifest.json").is_file()
        )
    )


def _bundle_from_route_data(data: _RouteData):
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

    def status(value: object) -> Status:
        return Status(str(value))

    attempts = tuple(
        AttemptRecord(
            **{
                **row,
                "status": status(row.get("status")),
                "evidence_ids": tuple(row.get("evidence_ids", ()) or ()),
            }
        )
        for row in data.rows["attempts"]
    )
    measurements = tuple(MeasurementRecord(**row) for row in data.rows["measurements"])
    summaries = tuple(
        SummaryRecord(
            **{
                **row,
                "source_measurement_ids": tuple(
                    row.get("source_measurement_ids", ()) or ()
                ),
            }
        )
        for row in data.rows["summaries"]
    )
    quality = tuple(QualityRecord(**row) for row in data.rows["quality"])
    failures = tuple(
        FailureRecord(
            **{
                **row,
                "status": status(row.get("status")),
                "evidence_ids": tuple(row.get("evidence_ids", ()) or ()),
            }
        )
        for row in data.rows["failures"]
    )
    evidence = tuple(
        EvidenceRecord(
            **{
                **row,
                "input_evidence_ids": tuple(
                    row.get("input_evidence_ids", ()) or ()
                ),
            }
        )
        for row in data.rows["evidence"]
    )
    return RouteBundle(
        route_id=str(data.manifest["route_id"]),
        campaign_id=str(data.manifest["campaign_id"]),
        attempts=attempts,
        measurements=measurements,
        summaries=summaries,
        quality=quality,
        failures=failures,
        evidence=evidence,
        repository=dict(data.manifest.get("repository", {}) or {}),
        hardware=dict(data.manifest.get("hardware", {}) or {}),
        software=dict(data.manifest.get("software", {}) or {}),
    )


def _expected_catalogs(
    route_data: Sequence[_RouteData],
) -> dict[str, list[dict[str, object]]]:
    from .comparison import build_catalogs

    return build_catalogs(tuple(_bundle_from_route_data(data) for data in route_data))


def _csv_scalar(value: object) -> str:
    if value is None:
        return ""
    if isinstance(value, bool):
        return str(value).lower()
    return str(value)


def _expected_catalog_table(
    name: str, rows: Sequence[Mapping[str, object]]
) -> tuple[tuple[str, ...], list[dict[str, str]]]:
    fallbacks = {
        "failure-summary.csv": (
            "route_id",
            "campaign_id",
            "failure_id",
            "status",
            "reason",
        ),
        "evidence-manifest.csv": (
            "route_id",
            "campaign_id",
            "evidence_id",
            "relative_path",
            "sha256",
        ),
    }
    fields = tuple(rows[0]) if rows else fallbacks.get(name, ("route_id",))
    return fields, [
        {field: _csv_scalar(row.get(field)) for field in fields} for row in rows
    ]


def _read_csv_table(path: Path) -> tuple[tuple[str, ...], list[dict[str, str]]]:
    with path.open(encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None:
            raise ValueError("missing CSV header")
        fields = tuple(reader.fieldnames)
        rows: list[dict[str, str]] = []
        for row_number, row in enumerate(reader, start=2):
            surplus = row.get(None)
            missing = sum(value is None for key, value in row.items() if key is not None)
            if surplus is not None or missing:
                actual_width = len(fields) + len(surplus or ()) - missing
                raise ValueError(
                    f"row width mismatch at row {row_number}: "
                    f"expected {len(fields)}, found {actual_width}"
                )
            rows.append({str(key): str(value or "") for key, value in row.items()})
        return fields, rows


def _catalog_tables_match(
    expected_fields: tuple[str, ...],
    expected: Sequence[Mapping[str, str]],
    actual_fields: tuple[str, ...],
    actual: Sequence[Mapping[str, str]],
) -> bool:
    if expected_fields != actual_fields or len(expected) != len(actual):
        return False
    for expected_row, actual_row in zip(expected, actual):
        for field in expected_fields:
            expected_value = expected_row.get(field, "")
            actual_value = actual_row.get(field, "")
            if field not in _CATALOG_NUMERIC_FIELDS:
                if actual_value != expected_value:
                    return False
                continue
            try:
                if Decimal(actual_value) != Decimal(expected_value):
                    return False
            except InvalidOperation:
                if actual_value != expected_value:
                    return False
    return True


def _collection_reconciliation_issues(
    collection: Path, route_data_by_name: Mapping[str, _RouteData]
) -> list[ValidationIssue]:
    issues: list[ValidationIssue] = []
    required_standard = tuple(_EXPECTED_ROUTE_DIRECTORIES)[:5]
    if any(name not in route_data_by_name for name in required_standard):
        return issues
    source_data = [route_data_by_name[name] for name in required_standard]
    try:
        catalogs = _expected_catalogs(source_data)
    except (KeyError, TypeError, ValueError) as error:
        return [
            _issue(
                "catalog_recomputation_failed",
                f"canonical route rows could not be converted into catalogs: {error}",
                collection / "catalog",
            )
        ]

    for name, expected_rows in catalogs.items():
        path = collection / "catalog" / name
        expected_fields, expected_table = _expected_catalog_table(name, expected_rows)
        try:
            actual_fields, actual_table = _read_csv_table(path)
        except (OSError, UnicodeError, csv.Error, ValueError) as error:
            issues.append(_issue("catalog_content_mismatch", f"{name}: {error}", path))
            continue
        if not _catalog_tables_match(
            expected_fields, expected_table, actual_fields, actual_table
        ):
            issues.append(
                _issue(
                    "catalog_content_mismatch",
                    f"{name} does not exactly match catalogs recomputed from canonical route rows",
                    path,
                )
            )

    cross = route_data_by_name.get("06-cross-route-comparison")
    if cross is None:
        return issues
    expected_route_ids = sorted(str(data.manifest.get("route_id", "")) for data in source_data)
    expected_campaign_ids = sorted(str(data.manifest.get("campaign_id", "")) for data in source_data)
    expected_attempt_count = sum(len(data.rows["attempts"]) for data in source_data)
    expected_comparison_count = len(catalogs["comparability-matrix.csv"])
    manifest = cross.manifest
    if sorted(str(value) for value in manifest.get("source_route_ids", ()) or ()) != expected_route_ids:
        issues.append(
            _issue(
                "cross_route_source_routes_mismatch",
                "cross-route source_route_ids differ from the five canonical route manifests",
                cross.root / "route-manifest.json",
            )
        )
    if sorted(str(value) for value in manifest.get("source_campaign_ids", ()) or ()) != expected_campaign_ids:
        issues.append(
            _issue(
                "cross_route_source_campaigns_mismatch",
                "cross-route source_campaign_ids differ from the five canonical route manifests",
                cross.root / "route-manifest.json",
            )
        )
    if manifest.get("attempt_count") != expected_attempt_count:
        issues.append(
            _issue(
                "cross_route_attempt_count_mismatch",
                f"cross-route attempt_count must be {expected_attempt_count}",
                cross.root / "route-manifest.json",
            )
        )
    if manifest.get("comparability_decision_count") != expected_comparison_count:
        issues.append(
            _issue(
                "cross_route_comparison_count_mismatch",
                f"cross-route comparability_decision_count must be {expected_comparison_count}",
                cross.root / "route-manifest.json",
            )
        )

    mirrored = {
        "campaign-summary.csv": cross.root / "results/route-status-summary.csv",
        "comparability-matrix.csv": cross.root / "results/comparability-matrix.csv",
    }
    for catalog_name, path in mirrored.items():
        expected_fields, expected_table = _expected_catalog_table(
            catalog_name, catalogs[catalog_name]
        )
        try:
            actual_fields, actual_table = _read_csv_table(path)
        except (OSError, UnicodeError, csv.Error, ValueError) as error:
            issues.append(
                _issue(
                    "cross_route_result_mismatch",
                    f"{path.name}: {error}",
                    path,
                )
            )
            continue
        if not _catalog_tables_match(
            expected_fields, expected_table, actual_fields, actual_table
        ):
            issues.append(
                _issue(
                    "cross_route_result_mismatch",
                    f"{path.name} does not match canonical route rows",
                    path,
                )
            )
    return issues


def _release_markdown_targets(path: Path) -> set[str]:
    text = path.read_text(encoding="utf-8")
    return {
        target.split("#", 1)[0]
        for target in re.findall(r"\[[^\]]+\]\(([^)]+)\)", text)
        if target
        and not re.match(r"^[A-Za-z][A-Za-z0-9+.-]*:", target)
        and not target.startswith("//")
    }


def _entity_types(entity: Mapping[str, object]) -> set[str]:
    value = entity.get("@type", ())
    if isinstance(value, str):
        return {value}
    if isinstance(value, Sequence):
        return {str(item) for item in value}
    return set()


def _entity_references(value: object) -> set[str]:
    values = value if isinstance(value, list) else [value]
    return {
        str(item.get("@id"))
        for item in values
        if isinstance(item, Mapping) and item.get("@id")
    }


def _is_relative_data_entity_id(entity_id: str) -> bool:
    return bool(entity_id) and not (
        entity_id.startswith(("/", "\\"))
        or "\\" in entity_id
        or re.match(r"^[A-Za-z][A-Za-z0-9+.-]*:", entity_id)
        or re.match(r"^[A-Za-z]:", entity_id)
        or ".." in entity_id.split("/")
    )


def write_git_index_release_manifest(
    repository_root: Path, collection_root: Path
) -> Path:
    """Write the top manifest from canonical stage-0 Git blobs, never working bytes."""
    repository = Path(repository_root).resolve()
    collection = Path(collection_root).resolve()
    prefix = collection.relative_to(repository).as_posix()
    listing = subprocess.run(
        ["git", "ls-files", "--stage", "-z", "--", prefix],
        cwd=repository,
        check=True,
        capture_output=True,
    ).stdout
    entries: dict[str, str] = {}
    tracked: set[str] = set()
    for raw in listing.split(b"\0"):
        if not raw:
            continue
        metadata, raw_path = raw.split(b"\t", 1)
        _mode, object_id, stage = metadata.decode("ascii").split()
        if stage != "0":
            raise ValueError(f"unmerged release entry: {raw_path!r}")
        repository_relative = raw_path.decode("utf-8")
        relative = Path(repository_relative).relative_to(prefix).as_posix()
        tracked.add(relative)
        if relative == "manifest-sha256.txt":
            continue
        payload = subprocess.run(
            ["git", "cat-file", "blob", object_id],
            cwd=repository,
            check=True,
            capture_output=True,
        ).stdout
        entries[relative] = hashlib.sha256(payload).hexdigest()

    actual = {
        path.relative_to(collection).as_posix()
        for path in collection.rglob("*")
        if path.is_file()
    }
    if actual != tracked:
        raise ValueError(
            "release working file set differs from the Git index; "
            f"missing={sorted(tracked - actual)}, untracked={sorted(actual - tracked)}"
        )
    manifest = collection / "manifest-sha256.txt"
    manifest.write_text(
        "".join(f"{entries[path]}  {path}\n" for path in sorted(entries)),
        encoding="utf-8",
        newline="\n",
    )
    return manifest


def _release_manifest_issues(collection: Path) -> list[ValidationIssue]:
    manifest = collection / "manifest-sha256.txt"
    errors = validate_sha256_manifest(collection, manifest)
    issues = [
        _issue("release_manifest_invalid", error, manifest) for error in errors
    ]
    if errors:
        return issues
    try:
        lines = manifest.read_text(encoding="utf-8").splitlines()
        published = {
            line.split("  ", 1)[1]
            for line in lines
            if re.fullmatch(r"[0-9a-fA-F]{64}  .+", line)
        }
        expected = {
            path.relative_to(collection).as_posix()
            for path in collection.rglob("*")
            if path.is_file() and path != manifest
        }
    except (OSError, UnicodeError, IndexError) as error:
        return [_issue("release_manifest_invalid", str(error), manifest)]
    if "manifest-sha256.txt" in published:
        issues.append(
            _issue(
                "release_manifest_self_included",
                "the top-level checksum manifest must exclude itself",
                manifest,
            )
        )
    missing = sorted(expected - published)
    surplus = sorted(published - expected)
    if missing or surplus:
        issues.append(
            _issue(
                "release_manifest_file_set_mismatch",
                f"top-level manifest file set mismatch; missing={missing}, surplus={surplus}",
                manifest,
            )
        )
    return issues


def _release_ro_crate_issues(collection: Path) -> list[ValidationIssue]:
    path = collection / "ro-crate-metadata.json"
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
        context = payload.get("@context", ())
        contexts = [context] if isinstance(context, str) else list(context)
        graph = payload.get("@graph")
        if not isinstance(graph, list):
            raise ValueError("@graph must be an array")
        entities = {
            str(entity["@id"]): entity
            for entity in graph
            if isinstance(entity, Mapping) and entity.get("@id")
        }
    except (OSError, UnicodeError, json.JSONDecodeError, TypeError, ValueError) as error:
        return [_issue("invalid_ro_crate", str(error), path)]

    issues: list[ValidationIssue] = []
    if not contexts or contexts[0] != "https://w3id.org/ro/crate/1.3/context":
        issues.append(
            _issue("ro_crate_version_mismatch", "RO-Crate 1.3 context is required", path)
        )
    descriptor = entities.get("ro-crate-metadata.json", {})
    if (
        descriptor.get("conformsTo") != {"@id": "https://w3id.org/ro/crate/1.3"}
        or descriptor.get("about") != {"@id": "./"}
    ):
        issues.append(
            _issue(
                "invalid_ro_crate_descriptor",
                "metadata descriptor must identify RO-Crate 1.3 and the root dataset",
                path,
            )
        )
    if "Dataset" not in _entity_types(entities.get("./", {})):
        issues.append(_issue("missing_ro_crate_root", "root Dataset entity is missing", path))

    for entity_id, entity in entities.items():
        if _entity_types(entity) & {"File", "Dataset"} and not _is_relative_data_entity_id(entity_id):
            issues.append(
                _issue(
                    "absolute_ro_crate_data_entity",
                    f"packaged data entity must use a relative URI: {entity_id}",
                    path,
                )
            )

    expected_files = {
        *_RELEASE_REPORT_PATHS,
        *_OPENVINO_WORKBOOK_PATHS,
        *_OPENVINO_WORKBOOK_RECEIPT_PATHS,
        *_RELEASE_CATALOG_PATHS,
        *(
            f"{route}/{relative}"
            for route in _RELEASE_ROUTES[:5]
            for relative in (
                "results/attempts.csv",
                "results/measurements.csv",
                "results/summary-results.csv",
            )
        ),
    }
    missing_entities = sorted(expected_files - entities.keys())
    if missing_entities:
        issues.append(
            _issue(
                "ro_crate_coverage_gap",
                f"required reports or canonical tables are absent: {missing_entities}",
                path,
            )
        )
    for route in _RELEASE_ROUTES[:5]:
        missing_context = [
            entity_id
            for entity_id in (
                f"#{route}-repository",
                f"#{route}-hardware",
                f"#{route}-software",
            )
            if entity_id not in entities
        ]
        if missing_context:
            issues.append(
                _issue(
                    "ro_crate_context_gap",
                    f"route repository/hardware/software entities are absent: {missing_context}",
                    path,
                )
            )

    expected_generated = {
        path
        for path in _RELEASE_REPORT_PATHS
        if path.endswith((".docx", ".pdf"))
    } | {
        "manifest-sha256.txt",
        *_OPENVINO_PORTABLE_WORKBOOK_PATHS,
        *_OPENVINO_WORKBOOK_RECEIPT_PATHS,
    }
    for entity_id in sorted(expected_generated):
        entity = entities.get(entity_id, {})
        if "#generated-artifact" not in _entity_references(entity.get("additionalType")):
            issues.append(
                _issue(
                    "generated_artifact_marker_missing",
                    f"expected generated artifact must retain its marker: {entity_id}",
                    path,
                )
            )
        activities = _entity_references(entity.get("wasGeneratedBy"))
        if len(activities) != 1:
            issues.append(
                _issue(
                    "generated_artifact_activity_missing",
                    f"generated artifact must identify one source activity: {entity_id}",
                    path,
                )
            )
            continue
        activity_id = next(iter(activities))
        activity = entities.get(activity_id, {})
        if (
            "CreateAction" not in _entity_types(activity)
            or entity_id not in _entity_references(activity.get("result"))
            or not _entity_references(activity.get("object"))
        ):
            issues.append(
                _issue(
                    "generated_artifact_activity_invalid",
                    f"source activity does not bind inputs and result for {entity_id}",
                    path,
                )
            )
    for entity_id in _OPENVINO_SOURCE_WORKBOOK_PATHS:
        if "#nonportable-source-evidence" not in _entity_references(
            entities.get(entity_id, {}).get("additionalType")
        ):
            issues.append(
                _issue(
                    "nonportable_source_marker_missing",
                    f"source workbook must be labelled evidence-only/nonportable: {entity_id}",
                    path,
                )
            )
    for entity_id in _OPENVINO_PORTABLE_WORKBOOK_PATHS:
        if "#portable-workbook" not in _entity_references(
            entities.get(entity_id, {}).get("additionalType")
        ):
            issues.append(
                _issue(
                    "portable_workbook_marker_missing",
                    f"portable workbook must retain its portability marker: {entity_id}",
                    path,
                )
            )
    return issues


def validate_release_metadata(root: Path) -> GateResult:
    """Validate the Task 14 portal, metadata, provenance, and release manifest."""
    collection = Path(root).resolve()
    present = [name for name in _TASK14_METADATA if (collection / name).is_file()]
    if not present:
        return GateResult(
            "release_metadata",
            limitations=(
                "Task 14 collection metadata is pending: " + ", ".join(_TASK14_METADATA),
            ),
        )
    missing = [name for name in _TASK14_METADATA if not (collection / name).is_file()]
    if missing:
        return GateResult(
            "release_metadata",
            (
                _issue(
                    "missing_release_metadata",
                    "required release metadata is missing: " + ", ".join(missing),
                    collection,
                ),
            ),
        )

    issues: list[ValidationIssue] = []
    limitations: list[str] = []
    try:
        portal_targets = _release_markdown_targets(collection / "README.md")
        expected_targets = {
            "CHANGELOG.md",
            "REPRODUCING.md",
            "LICENSES.md",
            "ro-crate-metadata.json",
            "manifest-sha256.txt",
            "validation/release-readiness.json",
            *_RELEASE_CATALOG_PATHS,
            *_RELEASE_REPORT_PATHS,
            *_OPENVINO_WORKBOOK_PATHS,
            *_OPENVINO_WORKBOOK_RECEIPT_PATHS,
            *(f"{route}/route-manifest.json" for route in _RELEASE_ROUTES),
        }
        unlinked = sorted(expected_targets - portal_targets)
        if unlinked:
            issues.append(
                _issue(
                    "portal_link_gap",
                    f"release portal does not link required routes/reports: {unlinked}",
                    collection / "README.md",
                )
            )
    except (OSError, UnicodeError) as error:
        issues.append(_issue("invalid_release_portal", str(error), collection / "README.md"))

    issues.extend(_release_ro_crate_issues(collection))
    issues.extend(_release_manifest_issues(collection))

    licenses_path = collection / "LICENSES.md"
    try:
        licenses = licenses_path.read_text(encoding="utf-8").casefold()
        required_phrases = (
            "no repository-level license file",
            "no license grant",
            "not verified",
            "verified author metadata",
        )
        if any(phrase not in licenses for phrase in required_phrases) or any(
            route not in licenses for route in _RELEASE_ROUTES[:5]
        ):
            issues.append(
                _issue(
                    "licensing_gap_not_explicit",
                    "LICENSES.md must disclose the repository, route, and citation metadata gaps",
                    licenses_path,
                )
            )
    except (OSError, UnicodeError) as error:
        issues.append(_issue("invalid_licenses_metadata", str(error), licenses_path))

    readiness_path = collection / "validation/release-readiness.json"
    try:
        readiness = json.loads(readiness_path.read_text(encoding="utf-8"))
        if (
            readiness.get("valid") is not True
            or readiness.get("status") != "ready_with_documented_limitations"
            or readiness.get("release_version")
            != "unified-final-results-2026-09-01-v2"
            or readiness.get("basis", {}).get("all_ordered_validation_gates_passed")
            is not True
            or readiness.get("basis", {}).get("raw_git_archive_self_contained")
            is not True
            or readiness.get("basis", {}).get("external_hydration_required")
            is not False
            or readiness.get("basis", {}).get("published_evidence_source_count")
            != 1846
        ):
            issues.append(
                _issue(
                    "release_readiness_invalid",
                    "release readiness must record the exact passed version, self-contained archive status, and gate result",
                    readiness_path,
                )
            )
        citation = readiness.get("citation", {})
        verified_authors = citation.get("verified_author_metadata_available")
        citation_exists = (collection / "CITATION.cff").is_file()
        if verified_authors is not False or citation_exists:
            issues.append(
                _issue(
                    "citation_metadata_policy_violation",
                    "CITATION.cff must be omitted while verified author metadata is unavailable",
                    collection / "CITATION.cff",
                )
            )
        qa = readiness.get("qa", {})
        pdf_qa = qa.get("pdf_reports", {})
        expected_pdfs = set(_EXPECTED_PDF_PAGE_COUNTS)
        if set(pdf_qa) != expected_pdfs:
            issues.append(
                _issue(
                    "pdf_qa_coverage_gap",
                    "release readiness must record all six report PDFs",
                    readiness_path,
                )
            )
        for relative, expected_pages in _EXPECTED_PDF_PAGE_COUNTS.items():
            receipt = pdf_qa.get(relative, {})
            if (
                receipt.get("page_count") != expected_pages
                or receipt.get("inspected_pages") != list(range(1, expected_pages + 1))
                or receipt.get("visual_review") != "passed"
                or receipt.get("searchable_text_review") != "passed"
                or receipt.get("blank_page_count") != 0
                or receipt.get("clipped_text_block_count") != 0
            ):
                issues.append(
                    _issue(
                        "pdf_qa_invalid",
                        f"PDF QA is incomplete or disagrees with inspected evidence: {relative}",
                        readiness_path,
                    )
                )
        workbook_qa = qa.get("openvino_workbooks", {})
        if set(workbook_qa) != set(_OPENVINO_WORKBOOK_PATHS):
            issues.append(
                _issue(
                    "workbook_qa_coverage_gap",
                    "release readiness must record both OpenVINO workbooks",
                    readiness_path,
                )
            )
        for relative, expected in _EXPECTED_WORKBOOK_QA.items():
            receipt = workbook_qa.get(relative, {})
            if (
                receipt.get("result") != "passed"
                or receipt.get("read_only") is not True
                or receipt.get("data_only") is not False
                or receipt.get("keep_links") is not True
                or tuple(receipt.get("sheets", ())) != expected["sheets"]
                or receipt.get("sheet_dimensions") != expected["sheet_dimensions"]
                or receipt.get("formula_count") != expected["formula_count"]
                or receipt.get("cached_formula_value_count")
                != expected["cached_formula_value_count"]
                or receipt.get("machine_absolute_path_count")
                != expected["machine_absolute_path_count"]
                or receipt.get("role") != expected["role"]
                or receipt.get("missing_sheet_reference_count") != 0
                or receipt.get("external_formula_reference_count") != 0
                or receipt.get("formula_error_count") != 0
            ):
                issues.append(
                    _issue(
                        "workbook_qa_invalid",
                        f"workbook QA is incomplete or disagrees with inspected evidence: {relative}",
                        readiness_path,
                    )
                )
        limitations.extend(str(item) for item in readiness.get("limitations", ()))
        if not any(
            "non-calculating" in limitation.casefold()
            and "recalculation" in limitation.casefold()
            for limitation in limitations
        ):
            issues.append(
                _issue(
                    "formula_recalculation_disclosure_missing",
                    "readiness must disclose blank experimental formulas in non-calculating readers until recalculation",
                    readiness_path,
                )
            )
    except (OSError, UnicodeError, json.JSONDecodeError, AttributeError) as error:
        issues.append(_issue("invalid_release_readiness", str(error), readiness_path))

    summary_path = collection / "validation/validation-summary.md"
    try:
        summary = summary_path.read_text(encoding="utf-8")
        expected_overall = (
            "Overall result for release package `unified-final-results-2026-09-01-v2`: "
            "**Passed — ready with documented limitations**."
        )
        gate_rows = {
            match.group(1): (
                match.group(2),
                int(match.group(3)),
                int(match.group(4)),
            )
            for match in re.finditer(
                r"^\| `([^`]+)` \| (Passed|Failed) \| (\d+) \| (\d+) \|$",
                summary,
                re.MULTILINE,
            )
        }
        expected_gate_rows = {
            name: ("Passed", 0, 0) for name in GATE_ORDER
        }
        expected_gate_rows["derivation"] = ("Passed", 0, 77)
        expected_gate_rows["release_metadata"] = (
            "Passed",
            0,
            len(readiness.get("limitations", ())),
        )
        expected_gate_rows["release_readiness"] = expected_gate_rows[
            "release_metadata"
        ]
        if (
            expected_overall not in summary
            or gate_rows != expected_gate_rows
        ):
            issues.append(
                _issue(
                    "validation_summary_mismatch",
                    "human-readable summary must agree with the passed structured readiness record",
                    summary_path,
                )
            )
    except (OSError, UnicodeError) as error:
        issues.append(_issue("validation_summary_mismatch", str(error), summary_path))
    return GateResult("release_metadata", tuple(issues), tuple(limitations))


def validate_collection(root: Path) -> ValidationReport:
    """Validate every discovered route and collection release boundary read-only."""
    collection = Path(root).resolve()
    reports: list[ValidationReport] = []
    discovery_issues: list[ValidationIssue] = []
    route_data_by_name: dict[str, _RouteData] = {}
    try:
        routes = _route_directories(collection)
    except OSError as error:
        routes = []
        discovery_issues.append(_issue("invalid_collection_root", str(error), collection))
    if not routes and not discovery_issues:
        discovery_issues.append(_issue("no_routes", "collection contains no numbered route directories", collection))
    actual_names = {route.name for route in routes}
    expected_names = set(_EXPECTED_ROUTE_DIRECTORIES)
    if actual_names != expected_names:
        missing = sorted(expected_names - actual_names)
        unexpected = sorted(actual_names - expected_names)
        discovery_issues.append(
            _issue(
                "collection_route_set_mismatch",
                f"expected exactly six route directories; missing={missing}, unexpected={unexpected}",
                collection,
            )
        )
    for route in routes:
        data = _load_route(route)
        route_data_by_name[route.name] = data
        expected_route_id = _EXPECTED_ROUTE_DIRECTORIES.get(route.name)
        if expected_route_id is not None and data.manifest.get("route_id") != expected_route_id:
            discovery_issues.append(
                _issue(
                    "collection_route_identity_mismatch",
                    f"{route.name} must publish route_id {expected_route_id!r}",
                    route / "route-manifest.json",
                )
            )
        reports.append(ValidationReport("route", data.root, tuple(_route_gates(data))))

    reconciliation_issues = _collection_reconciliation_issues(
        collection, route_data_by_name
    )

    gates: list[GateResult] = []
    for gate_name in GATE_ORDER[:-1]:
        if gate_name == "release_metadata":
            gates.append(validate_release_metadata(collection))
            continue
        issues: list[ValidationIssue] = []
        limitations: list[str] = []
        if gate_name == "schema":
            issues.extend(discovery_issues)
        if gate_name == "comparability":
            issues.extend(reconciliation_issues)
        for report in reports:
            gate = report.gate(gate_name)
            prefix = report.root.name
            issues.extend(
                ValidationIssue(
                    issue.code,
                    f"{prefix}: {issue.message}",
                    issue.path,
                    issue.blocking,
                )
                for issue in gate.issues
            )
            limitations.extend(f"{prefix}: {item}" for item in gate.limitations)
        gates.append(GateResult(gate_name, tuple(issues), tuple(limitations)))

    blocking = [gate.name for gate in gates if not gate.valid]
    release_issues = () if not blocking else (
        _issue("release_blocked", f"blocking gates: {', '.join(blocking)}", collection),
    )
    metadata_gate = next(gate for gate in gates if gate.name == "release_metadata")
    limitations = metadata_gate.limitations
    gates.append(GateResult("release_readiness", release_issues, limitations))
    return ValidationReport("collection", collection, tuple(gates))


def _receipt_payload(report: ValidationReport, names: Sequence[str]) -> dict[str, object]:
    selected = [report.gate(name) for name in names]
    return {
        "valid": all(gate.valid for gate in selected),
        "scope": report.scope,
        "root": ".",
        "gates": [gate.to_dict() for gate in selected],
    }


def write_validation_receipts(root: Path, report: ValidationReport) -> tuple[Path, ...]:
    """Write deterministic collection receipts for an already-computed report."""
    collection = Path(root).resolve()
    if report.root != collection or report.scope != "collection":
        raise ValueError("receipt report must describe the requested collection root")
    validation = collection / "validation"
    validation.mkdir(parents=True, exist_ok=True)
    payloads = {
        "schema-validation.json": _receipt_payload(
            report,
            (
                "schema",
                "ids",
                "coverage",
                "derivation",
                "status_failure_consistency",
                "availability",
            ),
        ),
        "integrity-validation.json": _receipt_payload(
            report,
            ("paths_hashes", "claim_coverage", "workbook_parity", "pdf_structure"),
        ),
        "cross-route-validation.json": _receipt_payload(report, ("comparability",)),
        "release-readiness.json": {
            **_receipt_payload(report, ("release_readiness",)),
            "valid": report.valid,
        },
    }
    outputs: list[Path] = []
    for name, payload in payloads.items():
        path = validation / name
        write_json(path, payload)
        outputs.append(path)

    lines = [
        "# Unified final-results validation summary",
        "",
        f"Overall result: **{'Passed' if report.valid else 'Failed'}**",
        "",
        "| Gate | Result | Blocking findings | Limitations |",
        "| --- | --- | ---: | ---: |",
    ]
    for gate in report.gates:
        lines.append(
            f"| `{gate.name}` | {'Passed' if gate.valid else 'Failed'} | "
            f"{sum(issue.blocking for issue in gate.issues)} | {len(gate.limitations)} |"
        )
    lines.append("")
    for gate in report.gates:
        if not gate.issues and not gate.limitations:
            continue
        lines.extend((f"## {gate.name}", ""))
        lines.extend(f"- `{issue.code}`: {issue.message}" for issue in gate.issues)
        lines.extend(f"- Limitation: {limitation}" for limitation in gate.limitations)
        lines.append("")
    summary = validation / "validation-summary.md"
    summary.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    outputs.append(summary)
    readme = validation / "README.md"
    readme.write_text(
        "# Validation receipts\n\n"
        "These receipts were generated by the Task 13 ordered validation gates. "
        "Validation itself is read-only; receipt publication is an explicit separate step.\n",
        encoding="utf-8",
        newline="\n",
    )
    outputs.append(readme)
    return tuple(sorted(outputs))


__all__ = [
    "GATE_ORDER",
    "GateResult",
    "ValidationIssue",
    "ValidationReport",
    "validate_collection",
    "validate_release_metadata",
    "validate_route",
    "write_git_index_release_manifest",
    "write_validation_receipts",
]
