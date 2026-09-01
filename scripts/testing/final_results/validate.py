"""Read-only validation gates for unified final-results route collections."""

from __future__ import annotations

import csv
import json
import math
import re
import statistics
from dataclasses import dataclass
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
_TASK14_METADATA = (
    "README.md",
    "CHANGELOG.md",
    "REPRODUCING.md",
    "LICENSES.md",
    "ro-crate-metadata.json",
    "manifest-sha256.txt",
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
    attempts = {str(row.get("attempt_id")): row for row in data.rows["attempts"]}
    test_ids = {str(row.get("test_case_id")) for row in data.rows["attempts"]}
    measurement_ids = {str(row.get("measurement_id")) for row in data.rows["measurements"]}
    evidence_ids = {str(row.get("evidence_id")) for row in data.rows["evidence"]}
    for name in ("measurements", "failures"):
        for row in data.rows[name]:
            if str(row.get("attempt_id")) not in attempts:
                issues.append(_issue("unknown_attempt_reference", f"{name} references unknown attempt {row.get('attempt_id')}", data.root / _CSV_FILES[name]))
            if str(row.get("test_case_id")) not in test_ids:
                issues.append(_issue("unknown_test_case_reference", f"{name} references unknown test case {row.get('test_case_id')}", data.root / _CSV_FILES[name]))
    for row in data.rows["summaries"]:
        for measurement_id in row.get("source_measurement_ids", ()) or ():
            if str(measurement_id) not in measurement_ids:
                issues.append(_issue("unknown_measurement_reference", f"summary references unknown measurement {measurement_id}", data.root / _CSV_FILES["summaries"]))
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
            limitations.append(
                f"{summary.get('summary_id')}: aggregation cannot be independently recomputed from canonical measurement columns"
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
    if set(attempts_by_test) != set(availability_by_test):
        issues.append(_issue("availability_case_set_mismatch", "availability test-case set differs from attempts", path))
    for test_case_id in sorted(set(attempts_by_test) & set(availability_by_test)):
        published = str(availability_by_test[test_case_id].get("status", "")).casefold()
        retained_statuses = {
            str(attempt.get("status", "")).casefold()
            for attempt in attempts_by_test[test_case_id]
        }
        if published not in retained_statuses:
            issues.append(_issue("availability_status_mismatch", f"availability status differs for {test_case_id}", path))
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
        if path.is_dir() and re.match(r"^0[1-6]-", path.name)
    )


def validate_collection(root: Path) -> ValidationReport:
    """Validate every discovered route and collection release boundary read-only."""
    collection = Path(root).resolve()
    reports: list[ValidationReport] = []
    discovery_issue: ValidationIssue | None = None
    try:
        routes = _route_directories(collection)
    except OSError as error:
        routes = []
        discovery_issue = _issue("invalid_collection_root", str(error), collection)
    if not routes and discovery_issue is None:
        discovery_issue = _issue("no_routes", "collection contains no numbered route directories", collection)
    for route in routes:
        reports.append(validate_route(route))

    gates: list[GateResult] = []
    for gate_name in GATE_ORDER[:-1]:
        issues: list[ValidationIssue] = []
        limitations: list[str] = []
        if gate_name == "schema" and discovery_issue is not None:
            issues.append(discovery_issue)
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
    missing_metadata = [name for name in _TASK14_METADATA if not (collection / name).is_file()]
    limitations = () if not missing_metadata else (
        "Task 14 collection metadata is pending: " + ", ".join(missing_metadata),
    )
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
    "validate_route",
    "write_validation_receipts",
]
