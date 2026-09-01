from __future__ import annotations

import csv
import json
import subprocess
import sys
from datetime import date
from pathlib import Path

import fitz
import pytest


sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.reporting.csvio import write_csv, write_json
from scripts.testing.reporting.docx_renderer import render_docx
from scripts.testing.reporting.evidence import hash_file
from scripts.testing.reporting.markdown_renderer import render_markdown
from scripts.testing.reporting.comparison import build_catalogs
from scripts.testing.reporting.models import (
    AttemptRecord,
    EvidenceRecord,
    MeasurementRecord,
    QualityRecord,
    RouteBundle,
    Status,
    SummaryRecord,
)
from scripts.testing.reporting.report_model import (
    Report,
    ReportParagraph,
    ReportSection,
)
from scripts.testing.reporting.validate import (
    GATE_ORDER,
    validate_collection,
    validate_route,
    write_validation_receipts,
)
from scripts.testing.build_final_results import build_final_results


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
BUILD_SCRIPT = REPOSITORY_ROOT / "scripts/testing/build_final_results.py"


ATTEMPT_FIELDS = (
    "route_id",
    "campaign_id",
    "test_case_id",
    "attempt_id",
    "status",
    "executed",
    "reason",
    "model_id",
    "weight_format_id",
    "cache_format_id",
    "backend_id",
    "source_status",
    "failure_kind",
    "evidence_ids",
)
MEASUREMENT_FIELDS = (
    "route_id",
    "campaign_id",
    "test_case_id",
    "attempt_id",
    "measurement_id",
    "run_id",
    "repetition_id",
    "source_evidence_id",
    "latency_ms",
    "prompt_tokens_per_second",
    "generation_tokens_per_second",
    "peak_working_set_bytes",
    "input_tokens",
    "output_tokens",
)
SUMMARY_FIELDS = (
    "route_id",
    "campaign_id",
    "test_case_id",
    "summary_id",
    "metric_name",
    "value",
    "unit",
    "aggregation",
    "source_measurement_ids",
)
FAILURE_FIELDS = (
    "route_id",
    "campaign_id",
    "test_case_id",
    "attempt_id",
    "failure_id",
    "status",
    "stage",
    "reason",
    "source_status",
    "evidence_ids",
)
EVIDENCE_FIELDS = (
    "route_id",
    "campaign_id",
    "evidence_id",
    "role",
    "relative_path",
    "sha256",
    "size_bytes",
    "source_label",
    "derived",
    "input_evidence_ids",
)
QUALITY_FIELDS = (
    "route_id",
    "campaign_id",
    "test_case_id",
    "quality_id",
    "prompt_id",
    "criterion_id",
    "score",
    "maximum_score",
    "prompt_suite_id",
    "rubric_id",
    "scoring_version",
    "source_evidence_id",
)
ROUTES = (
    ("01-upstream-llama-cpp", "upstream-llama-cpp"),
    ("02-atomicbot-turboquant", "atomicbot-turboquant"),
    ("03-animehacker-tq3-0", "animehacker-tq3-0"),
    ("04-openvino-experimental-fork", "openvino-experimental-fork"),
    ("05-openvino-official-upstream", "openvino-official-upstream"),
)
COLLECTION_CSV_TABLES = (
    ("catalog/route-register.csv", "catalog_content_mismatch"),
    ("catalog/campaign-summary.csv", "catalog_content_mismatch"),
    ("catalog/performance-summary.csv", "catalog_content_mismatch"),
    ("catalog/quality-summary.csv", "catalog_content_mismatch"),
    ("catalog/failure-summary.csv", "catalog_content_mismatch"),
    ("catalog/evidence-manifest.csv", "catalog_content_mismatch"),
    ("catalog/claim-evidence-map.csv", "catalog_content_mismatch"),
    ("catalog/comparability-matrix.csv", "catalog_content_mismatch"),
    (
        "06-cross-route-comparison/results/route-status-summary.csv",
        "cross_route_result_mismatch",
    ),
    (
        "06-cross-route-comparison/results/comparability-matrix.csv",
        "cross_route_result_mismatch",
    ),
)


def _write_pdf(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    document = fitz.open()
    page = document.new_page()
    page.insert_text((72, 72), text)
    document.save(path)
    document.close()


def _write_route(collection: Path, directory: str, route_id: str) -> Path:
    route = collection / directory
    campaign_id = f"{route_id}-campaign"
    evidence_id = f"{route_id}-evidence"
    source = collection / "sources" / f"{route_id}.txt"
    source.parent.mkdir(parents=True, exist_ok=True)
    source.write_text(f"source for {route_id}\n", encoding="utf-8", newline="\n")

    attempt = {
        "route_id": route_id,
        "campaign_id": campaign_id,
        "test_case_id": f"{route_id}-case-1",
        "attempt_id": f"{route_id}--terminal",
        "status": "passed",
        "executed": True,
        "reason": "",
        "model_id": "model-1",
        "weight_format_id": "q4",
        "cache_format_id": "f16",
        "backend_id": "cpu",
        "source_status": "passed",
        "failure_kind": None,
        "evidence_ids": json.dumps([evidence_id]),
    }
    measurements = [
        {
            "route_id": route_id,
            "campaign_id": campaign_id,
            "test_case_id": attempt["test_case_id"],
            "attempt_id": attempt["attempt_id"],
            "measurement_id": f"{route_id}-measurement-{index}",
            "run_id": f"{route_id}-run-{index}",
            "repetition_id": str(index),
            "source_evidence_id": evidence_id,
            "latency_ms": value,
            "prompt_tokens_per_second": None,
            "generation_tokens_per_second": None,
            "peak_working_set_bytes": None,
            "input_tokens": 8,
            "output_tokens": 8,
        }
        for index, value in enumerate((10.0, 20.0, 30.0), start=1)
    ]
    summary = {
        "route_id": route_id,
        "campaign_id": campaign_id,
        "test_case_id": attempt["test_case_id"],
        "summary_id": f"{route_id}-latency-median",
        "metric_name": "time_to_first_token",
        "value": 20.0,
        "unit": "milliseconds",
        "aggregation": "median of exactly three measurements",
        "source_measurement_ids": json.dumps(
            [row["measurement_id"] for row in measurements]
        ),
    }
    evidence = {
        "route_id": route_id,
        "campaign_id": campaign_id,
        "evidence_id": evidence_id,
        "role": "source",
        "relative_path": source.relative_to(collection).as_posix(),
        "sha256": hash_file(source),
        "size_bytes": source.stat().st_size,
        "source_label": f"{route_id} source",
        "derived": False,
        "input_evidence_ids": "[]",
    }
    write_json(
        route / "route-manifest.json",
        {
            "route_id": route_id,
            "campaign_id": campaign_id,
            "attempt_count": 1,
            "measurement_count": 3,
            "summary_count": 1,
            "quality_count": 1,
            "failure_count": 0,
            "evidence_count": 1,
            "repository": {},
            "hardware": {},
            "software": {},
        },
    )
    write_csv(
        route / "protocol/intended-test-matrix.csv",
        ({"test_case_id": attempt["test_case_id"], "intended": True},),
        ("test_case_id", "intended"),
    )
    write_csv(route / "results/attempts.csv", (attempt,), ATTEMPT_FIELDS)
    write_csv(route / "results/measurements.csv", measurements, MEASUREMENT_FIELDS)
    write_csv(route / "results/summary-results.csv", (summary,), SUMMARY_FIELDS)
    write_csv(
        route / "results/availability-matrix.csv",
        (
            {
                "test_case_id": attempt["test_case_id"],
                "model_id": "model-1",
                "weight_format_id": "q4",
                "cache_format_id": "f16",
                "backend_id": "cpu",
                "status": "passed",
            },
        ),
        (
            "test_case_id",
            "model_id",
            "weight_format_id",
            "cache_format_id",
            "backend_id",
            "status",
        ),
    )
    write_csv(
        route / "quality/scores.csv",
        (
            {
                "route_id": route_id,
                "campaign_id": campaign_id,
                "test_case_id": attempt["test_case_id"],
                "quality_id": f"{route_id}-quality-1",
                "prompt_id": "prompt-1",
                "criterion_id": "criterion-1",
                "score": 1.0,
                "maximum_score": 1.0,
                "prompt_suite_id": "suite-1",
                "rubric_id": "rubric-1",
                "scoring_version": "v1",
                "source_evidence_id": evidence_id,
            },
        ),
        QUALITY_FIELDS,
    )
    write_csv(route / "failures/failure-register.csv", (), FAILURE_FIELDS)
    write_csv(route / "evidence/evidence-index.csv", (evidence,), EVIDENCE_FIELDS)
    write_csv(
        route / "evidence/claim-evidence-map.csv",
        (
            {
                "claim_id": f"{route_id}-claim",
                "claim": "The miniature route has one passed case.",
                "evidence_ids": json.dumps([evidence_id]),
            },
        ),
        ("claim_id", "claim", "evidence_ids"),
    )
    report = Report(
        title=f"{route_id} report",
        route_id=route_id,
        revision="R1",
        generated_date=date(2026, 8, 30),
        sections=(
            ReportSection(
                title="Result",
                blocks=(ReportParagraph("One passed miniature case."),),
            ),
        ),
    )
    render_markdown(report, route / "workbook/source/final-report.md")
    render_docx(report, route / "workbook/generated/final-report.docx")
    _write_pdf(route / "workbook/generated/final-report.pdf", f"{route_id} report")
    write_json(route / "validation/workbook-parity.json", {"matches": True})
    write_json(route / "validation/visual-validation.json", {"valid": True})
    write_json(route / "validation/integrity-validation.json", {"valid": True})
    return route


def _fixture_bundle(route: Path) -> RouteBundle:
    manifest = json.loads((route / "route-manifest.json").read_text(encoding="utf-8"))
    attempts, _ = _rows(route / "results/attempts.csv")
    measurements, _ = _rows(route / "results/measurements.csv")
    summaries, _ = _rows(route / "results/summary-results.csv")
    quality, _ = _rows(route / "quality/scores.csv")
    evidence, _ = _rows(route / "evidence/evidence-index.csv")
    return RouteBundle(
        route_id=manifest["route_id"],
        campaign_id=manifest["campaign_id"],
        attempts=tuple(
            AttemptRecord(
                route_id=row["route_id"],
                campaign_id=row["campaign_id"],
                test_case_id=row["test_case_id"],
                attempt_id=row["attempt_id"],
                status=Status(row["status"]),
                executed=row["executed"] == "true",
                reason=row["reason"],
                model_id=row["model_id"] or None,
                weight_format_id=row["weight_format_id"] or None,
                cache_format_id=row["cache_format_id"] or None,
                backend_id=row["backend_id"] or None,
                source_status=row["source_status"] or None,
                failure_kind=row["failure_kind"] or None,
                evidence_ids=tuple(json.loads(row["evidence_ids"])),
            )
            for row in attempts
        ),
        measurements=tuple(
            MeasurementRecord(
                route_id=row["route_id"],
                campaign_id=row["campaign_id"],
                test_case_id=row["test_case_id"],
                attempt_id=row["attempt_id"],
                measurement_id=row["measurement_id"],
                run_id=row["run_id"] or None,
                repetition_id=row["repetition_id"] or None,
                source_evidence_id=row["source_evidence_id"] or None,
                latency_ms=float(row["latency_ms"]) if row["latency_ms"] else None,
                input_tokens=int(row["input_tokens"]) if row["input_tokens"] else None,
                output_tokens=int(row["output_tokens"]) if row["output_tokens"] else None,
            )
            for row in measurements
        ),
        summaries=tuple(
            SummaryRecord(
                route_id=row["route_id"],
                campaign_id=row["campaign_id"],
                test_case_id=row["test_case_id"],
                summary_id=row["summary_id"],
                metric_name=row["metric_name"],
                value=float(row["value"]) if row["value"] else None,
                unit=row["unit"],
                aggregation=row["aggregation"],
                source_measurement_ids=tuple(json.loads(row["source_measurement_ids"])),
            )
            for row in summaries
        ),
        quality=tuple(
            QualityRecord(
                route_id=row["route_id"],
                campaign_id=row["campaign_id"],
                test_case_id=row["test_case_id"],
                quality_id=row["quality_id"],
                prompt_id=row["prompt_id"] or None,
                criterion_id=row["criterion_id"] or None,
                score=float(row["score"]) if row["score"] else None,
                maximum_score=float(row["maximum_score"]) if row["maximum_score"] else None,
                prompt_suite_id=row["prompt_suite_id"] or None,
                rubric_id=row["rubric_id"] or None,
                scoring_version=row["scoring_version"] or None,
                source_evidence_id=row["source_evidence_id"] or None,
            )
            for row in quality
        ),
        evidence=tuple(
            EvidenceRecord(
                route_id=row["route_id"],
                campaign_id=row["campaign_id"],
                evidence_id=row["evidence_id"],
                role=row["role"],
                relative_path=row["relative_path"],
                sha256=row["sha256"],
                size_bytes=int(row["size_bytes"]),
                source_label=row["source_label"] or None,
                derived=row["derived"] == "true",
                input_evidence_ids=tuple(json.loads(row["input_evidence_ids"])),
            )
            for row in evidence
        ),
    )


def _write_cross_route(
    collection: Path,
    bundles: tuple[RouteBundle, ...],
    catalogs: dict[str, list[dict[str, object]]],
) -> Path:
    route = collection / "06-cross-route-comparison"
    write_json(
        route / "route-manifest.json",
        {
            "route_id": "cross-route-comparison",
            "revision": "R1",
            "generated_date": "2026-08-30",
            "source_route_ids": sorted(bundle.route_id for bundle in bundles),
            "source_campaign_ids": sorted(bundle.campaign_id for bundle in bundles),
            "attempt_count": 5,
            "comparability_decision_count": 20,
            "universal_ranking_permitted": False,
        },
    )
    report = Report(
        title="Cross-route report",
        route_id="cross-route-comparison",
        revision="R1",
        generated_date=date(2026, 8, 30),
        sections=(
            ReportSection(
                title="Comparison boundary",
                blocks=(ReportParagraph("No universal ranking is supported."),),
            ),
        ),
    )
    render_markdown(report, route / "workbook/source/final-report.md")
    render_docx(report, route / "workbook/generated/final-report.docx")
    _write_pdf(route / "workbook/generated/final-report.pdf", "Cross-route report")
    write_json(route / "validation/workbook-parity.json", {"matches": True})
    write_json(route / "validation/visual-validation.json", {"valid": True})
    write_json(route / "validation/integrity-validation.json", {"valid": True})
    write_json(
        route / "validation/cross-route-validation.json",
        {"valid": True, "universal_ranking_present": False},
    )
    comparison = catalogs["comparability-matrix.csv"]
    campaigns = catalogs["campaign-summary.csv"]
    write_csv(
        route / "results/comparability-matrix.csv",
        comparison,
        tuple(comparison[0]),
    )
    write_csv(
        route / "results/route-status-summary.csv",
        campaigns,
        tuple(campaigns[0]),
    )
    return route


def _collection(tmp_path: Path) -> tuple[Path, Path, Path, Path]:
    collection = tmp_path / "final-results"
    routes = tuple(_write_route(collection, directory, route_id) for directory, route_id in ROUTES)
    bundles = tuple(_fixture_bundle(route) for route in routes)
    catalogs = build_catalogs(bundles)
    for name, rows in catalogs.items():
        fallbacks = {
            "failure-summary.csv": ("route_id", "campaign_id", "failure_id", "status", "reason"),
            "evidence-manifest.csv": ("route_id", "campaign_id", "evidence_id", "relative_path", "sha256"),
        }
        write_csv(
            collection / "catalog" / name,
            rows,
            tuple(rows[0]) if rows else fallbacks.get(name, ("route_id",)),
        )
    cross = _write_cross_route(collection, bundles, catalogs)
    alpha, beta = routes[:2]
    return collection, alpha, beta, cross


def _rows(path: Path) -> tuple[list[dict[str, str]], tuple[str, ...]]:
    with path.open(encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        return list(reader), tuple(reader.fieldnames or ())


def _codes(report, gate_name: str) -> set[str]:
    return {issue.code for issue in report.gate(gate_name).issues}


def test_valid_miniature_collection_passes_gates_in_release_order(tmp_path):
    collection, alpha, _, _ = _collection(tmp_path)

    route_report = validate_route(alpha)
    collection_report = validate_collection(collection)

    assert route_report.valid is True
    assert collection_report.valid is True
    assert tuple(gate.name for gate in collection_report.gates) == GATE_ORDER
    assert collection_report.gate("release_readiness").limitations


def test_validation_catches_duplicate_ids(tmp_path):
    _, alpha, _, _ = _collection(tmp_path)
    path = alpha / "results/measurements.csv"
    rows, fields = _rows(path)
    rows[1]["measurement_id"] = rows[0]["measurement_id"]
    write_csv(path, rows, fields)

    report = validate_route(alpha)

    assert "duplicate_measurement_id" in _codes(report, "ids")


def test_validation_catches_missing_intended_attempt(tmp_path):
    _, alpha, _, _ = _collection(tmp_path)
    path = alpha / "protocol/intended-test-matrix.csv"
    rows, fields = _rows(path)
    rows.append({"test_case_id": "upstream-llama-cpp-case-2", "intended": "true"})
    write_csv(path, rows, fields)

    report = validate_route(alpha)

    assert "missing_intended_attempt" in _codes(report, "coverage")


def test_validation_catches_incorrect_median(tmp_path):
    _, alpha, _, _ = _collection(tmp_path)
    path = alpha / "results/summary-results.csv"
    rows, fields = _rows(path)
    rows[0]["value"] = "21"
    write_csv(path, rows, fields)

    report = validate_route(alpha)

    assert "derived_value_mismatch" in _codes(report, "derivation")


def test_validation_fails_closed_for_renamed_supported_aggregation(tmp_path):
    _, alpha, _, _ = _collection(tmp_path)
    path = alpha / "results/summary-results.csv"
    rows, fields = _rows(path)
    rows[0]["value"] = "999"
    rows[0]["aggregation"] = "renamed latency reduction"
    write_csv(path, rows, fields)

    report = validate_route(alpha)

    assert report.gate("derivation").valid is False
    assert "unsupported_summary_derivation" in _codes(report, "derivation")


def test_derivation_allowlist_is_closed_to_exact_approved_summary_ids(tmp_path):
    _, alpha, _, _ = _collection(tmp_path)
    path = alpha / "results/summary-results.csv"
    rows, fields = _rows(path)
    rows[0].update(
        {
            "summary_id": "UL-01--kv_cache_allocated_bytes",
            "metric_name": "kv_cache_allocated_bytes",
            "value": "123",
            "unit": "bytes",
            "aggregation": "median of exactly three deduplicated runtime KV allocations",
        }
    )
    write_csv(path, rows, fields)
    approved = validate_route(alpha)
    assert approved.gate("derivation").valid is True
    assert len(approved.gate("derivation").limitations) == 1

    rows[0]["summary_id"] = "UL-INVENTED--kv_cache_allocated_bytes"
    write_csv(path, rows, fields)
    invented = validate_route(alpha)

    assert "unsupported_summary_derivation" in _codes(invented, "derivation")


@pytest.mark.parametrize(
    ("relative_path", "id_field"),
    (
        ("results/measurements.csv", "measurement_id"),
        ("quality/scores.csv", "quality_id"),
    ),
)
def test_validation_catches_nonexistent_source_evidence_id(
    tmp_path, relative_path, id_field
):
    _, alpha, _, _ = _collection(tmp_path)
    path = alpha / relative_path
    rows, fields = _rows(path)
    rows[0]["source_evidence_id"] = "does-not-exist"
    mutated_id = rows[0][id_field]
    write_csv(path, rows, fields)

    report = validate_route(alpha)

    issues = report.gate("ids").issues
    assert any(
        issue.code == "unknown_source_evidence_reference"
        and mutated_id in issue.message
        for issue in issues
    )


def test_validation_catches_attempt_evidence_reference(tmp_path):
    _, alpha, _, _ = _collection(tmp_path)
    path = alpha / "results/attempts.csv"
    rows, fields = _rows(path)
    rows[0]["evidence_ids"] = json.dumps(["does-not-exist"])
    write_csv(path, rows, fields)

    report = validate_route(alpha)

    assert "unknown_attempt_evidence_reference" in _codes(report, "ids")


def test_validation_catches_failure_evidence_and_same_case_relationships(tmp_path):
    _, alpha, _, _ = _collection(tmp_path)
    attempts, _ = _rows(alpha / "results/attempts.csv")
    attempt = attempts[0]
    write_csv(
        alpha / "failures/failure-register.csv",
        (
            {
                "route_id": attempt["route_id"],
                "campaign_id": attempt["campaign_id"],
                "test_case_id": "wrong-case",
                "attempt_id": attempt["attempt_id"],
                "failure_id": "bad-failure",
                "status": "failed",
                "stage": "test",
                "reason": "retained historical failure",
                "source_status": "failed",
                "evidence_ids": json.dumps(["does-not-exist"]),
            },
        ),
        FAILURE_FIELDS,
    )

    report = validate_route(alpha)

    codes = _codes(report, "ids")
    assert "unknown_failure_evidence_reference" in codes
    assert "attempt_test_case_mismatch" in codes


def test_validation_catches_row_route_campaign_and_summary_case_relationships(tmp_path):
    _, alpha, beta, _ = _collection(tmp_path)
    beta_attempts, _ = _rows(beta / "results/attempts.csv")
    measurement_path = alpha / "results/measurements.csv"
    measurements, measurement_fields = _rows(measurement_path)
    measurements[0]["route_id"] = "wrong-route"
    measurements[0]["campaign_id"] = "wrong-campaign"
    measurements[0]["attempt_id"] = beta_attempts[0]["attempt_id"]
    write_csv(measurement_path, measurements, measurement_fields)
    summary_path = alpha / "results/summary-results.csv"
    summaries, summary_fields = _rows(summary_path)
    summaries[0]["test_case_id"] = "wrong-case"
    write_csv(summary_path, summaries, summary_fields)

    report = validate_route(alpha)

    codes = _codes(report, "ids")
    assert "row_route_mismatch" in codes
    assert "row_campaign_mismatch" in codes
    assert "unknown_attempt_reference" in codes
    assert "summary_measurement_case_mismatch" in codes


def test_validation_catches_measurement_for_non_passed_attempt(tmp_path):
    _, alpha, _, _ = _collection(tmp_path)
    attempt_path = alpha / "results/attempts.csv"
    attempts, attempt_fields = _rows(attempt_path)
    attempts[0]["status"] = "blocked"
    attempts[0]["reason"] = "controlled block"
    write_csv(attempt_path, attempts, attempt_fields)
    write_csv(
        alpha / "failures/failure-register.csv",
        (
            {
                "route_id": attempts[0]["route_id"],
                "campaign_id": attempts[0]["campaign_id"],
                "test_case_id": attempts[0]["test_case_id"],
                "attempt_id": attempts[0]["attempt_id"],
                "failure_id": "upstream-llama-cpp-failure-1",
                "status": "blocked",
                "stage": "preflight",
                "reason": "controlled block",
                "source_status": "blocked",
                "evidence_ids": attempts[0]["evidence_ids"],
            },
        ),
        FAILURE_FIELDS,
    )

    report = validate_route(alpha)

    assert "measurement_for_non_passed_attempt" in _codes(
        report, "status_failure_consistency"
    )


def test_availability_accepts_passed_terminal_attempt_with_retained_rejection(tmp_path):
    _, alpha, _, _ = _collection(tmp_path)
    attempt_path = alpha / "results/attempts.csv"
    attempts, attempt_fields = _rows(attempt_path)
    rejected = dict(attempts[0])
    rejected.update(
        {
            "attempt_id": "upstream-llama-cpp--historical-rejected",
            "status": "failed",
            "reason": "retained rejected evidence",
            "source_status": "rejected",
            "failure_kind": "invalid-runtime-evidence",
        }
    )
    attempts.append(rejected)
    write_csv(attempt_path, attempts, attempt_fields)
    manifest_path = alpha / "route-manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["attempt_count"] = 2
    manifest["failure_count"] = 1
    write_json(manifest_path, manifest)
    write_csv(
        alpha / "failures/failure-register.csv",
        (
            {
                "route_id": rejected["route_id"],
                "campaign_id": rejected["campaign_id"],
                "test_case_id": rejected["test_case_id"],
                "attempt_id": rejected["attempt_id"],
                "failure_id": "upstream-llama-cpp-rejected-failure",
                "status": "failed",
                "stage": "evidence-review",
                "reason": "retained rejected evidence",
                "source_status": "rejected",
                "evidence_ids": rejected["evidence_ids"],
            },
        ),
        FAILURE_FIELDS,
    )

    report = validate_route(alpha)

    assert report.gate("availability").valid is True


def test_availability_rejects_stale_failed_status_beside_terminal_pass(tmp_path):
    _, alpha, _, _ = _collection(tmp_path)
    attempt_path = alpha / "results/attempts.csv"
    attempts, attempt_fields = _rows(attempt_path)
    historical = dict(attempts[0])
    historical.update(
        {
            "attempt_id": "upstream-llama-cpp--historical-failed",
            "status": "failed",
            "reason": "retained historical failure",
            "source_status": "rejected",
            "failure_kind": "invalid-runtime-evidence",
        }
    )
    attempts.append(historical)
    write_csv(attempt_path, attempts, attempt_fields)
    availability_path = alpha / "results/availability-matrix.csv"
    availability, availability_fields = _rows(availability_path)
    availability[0]["status"] = "failed"
    write_csv(availability_path, availability, availability_fields)

    report = validate_route(alpha)

    assert "availability_terminal_status_mismatch" in _codes(report, "availability")


def test_validation_catches_missing_evidence(tmp_path):
    collection, alpha, _, _ = _collection(tmp_path)
    (collection / "sources/upstream-llama-cpp.txt").unlink()

    report = validate_route(alpha)

    assert "missing_evidence_path" in _codes(report, "paths_hashes")


def test_validation_catches_evidence_hash_mismatch(tmp_path):
    collection, alpha, _, _ = _collection(tmp_path)
    (collection / "sources/upstream-llama-cpp.txt").write_text("changed\n", encoding="utf-8")

    report = validate_route(alpha)

    assert "evidence_hash_mismatch" in _codes(report, "paths_hashes")


def test_checkout_crlf_manifest_is_a_nonblocking_integrity_limitation(tmp_path):
    collection, alpha, _, _ = _collection(tmp_path)
    source = collection / "sources/upstream-llama-cpp.txt"
    manifest = alpha / "evidence/manifest-sha256.txt"
    manifest.write_bytes(
        f"{hash_file(source)}  sources/upstream-llama-cpp.txt\r\n".encode("utf-8")
    )

    report = validate_route(alpha)

    gate = report.gate("paths_hashes")
    assert gate.valid is True
    assert any("CRLF" in limitation for limitation in gate.limitations)


def test_validation_recomputes_markdown_docx_parity(tmp_path):
    _, alpha, _, _ = _collection(tmp_path)
    markdown = alpha / "workbook/source/final-report.md"
    markdown.write_text(
        markdown.read_text(encoding="utf-8").replace(
            "One passed miniature case.", "A changed canonical conclusion."
        ),
        encoding="utf-8",
        newline="\n",
    )

    report = validate_route(alpha)

    assert "workbook_parity_mismatch" in _codes(report, "workbook_parity")


def test_validation_catches_unsupported_cross_route_ranking(tmp_path):
    collection, _, _, cross = _collection(tmp_path)
    markdown = cross / "workbook/source/final-report.md"
    markdown.write_text(
        markdown.read_text(encoding="utf-8").replace(
            "No universal ranking is supported.",
            "Route alpha is the best repository overall.",
        ),
        encoding="utf-8",
        newline="\n",
    )

    report = validate_collection(collection)

    assert "unsupported_cross_route_ranking" in _codes(report, "comparability")


def test_validation_checks_cross_route_checksum_manifest(tmp_path):
    collection, _, _, cross = _collection(tmp_path)
    pdf = cross / "workbook/generated/final-report.pdf"
    manifest = cross / "evidence/manifest-sha256.txt"
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text(
        f"{hash_file(pdf)}  06-cross-route-comparison/workbook/generated/final-report.pdf\n",
        encoding="utf-8",
        newline="\n",
    )
    pdf.write_bytes(pdf.read_bytes() + b"changed")

    report = validate_collection(collection)

    assert "manifest_validation_error" in _codes(report, "paths_hashes")


def test_collection_requires_exact_complete_six_route_directories(tmp_path):
    collection, _, _, _ = _collection(tmp_path)
    missing = collection / "05-openvino-official-upstream"
    missing.rename(collection / "05-wrong-route")

    report = validate_collection(collection)

    assert report.gate("release_readiness").valid is False
    assert "collection_route_set_mismatch" in _codes(report, "schema")


@pytest.mark.parametrize(
    ("directory_name", "write_manifest"),
    (
        ("07-rogue-route", True),
        ("07-route-shaped-only", False),
        ("manifest-only-rogue", True),
    ),
)
def test_collection_rejects_rogue_route_directory(
    tmp_path, directory_name, write_manifest
):
    collection, _, _, _ = _collection(tmp_path)
    rogue = collection / directory_name
    rogue.mkdir()
    if write_manifest:
        write_json(rogue / "route-manifest.json", {"route_id": "rogue-route"})

    report = validate_collection(collection)

    assert report.gate("schema").valid is False
    assert report.gate("release_readiness").valid is False
    assert "collection_route_set_mismatch" in _codes(report, "schema")


def test_collection_ignores_non_route_support_directories(tmp_path):
    collection, _, _, _ = _collection(tmp_path)
    for name in ("validation", "standards", "sources"):
        support = collection / name
        support.mkdir(exist_ok=True)
        (support / "README.md").write_text("support files\n", encoding="utf-8")

    report = validate_collection(collection)

    assert report.valid is True


def test_collection_reconciles_cross_route_source_identities_and_counts(tmp_path):
    collection, _, _, cross = _collection(tmp_path)
    manifest_path = cross / "route-manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["source_route_ids"] = manifest["source_route_ids"][:-1]
    manifest["source_campaign_ids"] = ["stale-campaign"]
    manifest["attempt_count"] = 999
    manifest["comparability_decision_count"] = 999
    write_json(manifest_path, manifest)

    report = validate_collection(collection)

    assert report.gate("release_readiness").valid is False
    codes = _codes(report, "comparability")
    assert "cross_route_source_routes_mismatch" in codes
    assert "cross_route_source_campaigns_mismatch" in codes
    assert "cross_route_attempt_count_mismatch" in codes
    assert "cross_route_comparison_count_mismatch" in codes


@pytest.mark.parametrize(
    "catalog_name",
    (
        "route-register.csv",
        "campaign-summary.csv",
        "performance-summary.csv",
        "quality-summary.csv",
        "failure-summary.csv",
        "evidence-manifest.csv",
        "claim-evidence-map.csv",
        "comparability-matrix.csv",
    ),
)
def test_collection_independently_recomputes_every_catalog(tmp_path, catalog_name):
    collection, _, _, _ = _collection(tmp_path)
    (collection / "catalog" / catalog_name).write_text(
        "route_id,attempt_count\nwrong,999\n",
        encoding="utf-8",
        newline="\n",
    )

    report = validate_collection(collection)

    assert report.gate("release_readiness").valid is False
    assert "catalog_content_mismatch" in _codes(report, "comparability")


def test_catalog_numeric_cells_allow_equivalent_integer_float_spelling(tmp_path):
    collection, _, _, _ = _collection(tmp_path)
    path = collection / "catalog/performance-summary.csv"
    rows, fields = _rows(path)
    assert rows[0]["value"] == "20.0"
    rows[0]["value"] = "20"
    write_csv(path, rows, fields)

    report = validate_collection(collection)

    assert report.valid is True


@pytest.mark.parametrize(("relative_path", "expected_code"), COLLECTION_CSV_TABLES)
@pytest.mark.parametrize("width_mutation", ("long", "short", "header-long"))
def test_collection_csv_tables_reject_row_width_mismatch(
    tmp_path, relative_path, expected_code, width_mutation
):
    collection, _, _, _ = _collection(tmp_path)
    path = collection / relative_path
    with path.open(encoding="utf-8", newline="") as handle:
        table = list(csv.reader(handle))
    row_index = 1
    if len(table) == 1:
        table.append(["wrong"] * len(table[0]))
    if width_mutation == "header-long":
        table[0].append("rogue-header")
    elif width_mutation == "long":
        table[row_index].append("rogue-cell")
    else:
        table[row_index].pop()
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerows(table)

    report = validate_collection(collection)

    assert report.gate("release_readiness").valid is False
    assert any(
        issue.code == expected_code and "row width mismatch" in issue.message
        for issue in report.gate("comparability").issues
    ), [(issue.code, issue.message) for issue in report.gate("comparability").issues]


def test_receipt_writer_emits_required_json_and_markdown(tmp_path):
    collection, _, _, _ = _collection(tmp_path)
    report = validate_collection(collection)

    outputs = write_validation_receipts(collection, report)

    assert {path.name for path in outputs} == {
        "README.md",
        "validation-summary.md",
        "schema-validation.json",
        "integrity-validation.json",
        "cross-route-validation.json",
        "release-readiness.json",
    }
    readiness = json.loads(
        (collection / "validation/release-readiness.json").read_text(encoding="utf-8")
    )
    assert readiness["valid"] is True
    assert readiness["root"] == "."
    assert str(tmp_path) not in json.dumps(readiness)


def test_validate_only_cli_is_read_only_and_uses_exit_0_or_1(tmp_path):
    collection, alpha, _, _ = _collection(tmp_path)
    valid = subprocess.run(
        [
            sys.executable,
            str(BUILD_SCRIPT),
            "--route",
            "all",
            "--output-root",
            str(collection),
            "--validate-only",
        ],
        cwd=REPOSITORY_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    assert valid.returncode == 0, valid.stdout + valid.stderr
    assert not (collection / "validation").exists()

    path = alpha / "results/summary-results.csv"
    rows, fields = _rows(path)
    rows[0]["value"] = "999"
    write_csv(path, rows, fields)
    invalid = subprocess.run(
        [
            sys.executable,
            str(BUILD_SCRIPT),
            "--route",
            "all",
            "--output-root",
            str(collection),
            "--validate-only",
        ],
        cwd=REPOSITORY_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    assert invalid.returncode == 1
    assert not (collection / "validation").exists()


def test_invalid_cli_use_exits_2(tmp_path):
    result = subprocess.run(
        [
            sys.executable,
            str(BUILD_SCRIPT),
            "--route",
            "not-a-route",
            "--output-root",
            str(tmp_path),
            "--validate-only",
        ],
        cwd=REPOSITORY_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 2
    assert "usage:" in result.stderr.casefold()
    assert "invalid choice" in result.stderr.casefold()


def test_build_refuses_protected_external_and_nonempty_output_roots(tmp_path):
    protected = REPOSITORY_ROOT / "docs/testing/final-results"
    with pytest.raises(ValueError, match="protected repository output root"):
        build_final_results("all", protected)

    with pytest.raises(ValueError, match="inside the repository"):
        build_final_results("all", tmp_path / "outside-repository")

    nonempty = REPOSITORY_ROOT / "tmp/task13-nonempty-output"
    nonempty.mkdir(parents=True, exist_ok=True)
    marker = nonempty / "preserve.txt"
    marker.write_text("preserve\n", encoding="utf-8")
    try:
        with pytest.raises(ValueError, match="must be empty"):
            build_final_results("all", nonempty)
        assert marker.read_text(encoding="utf-8") == "preserve\n"
    finally:
        marker.unlink()
        nonempty.rmdir()
