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

from scripts.testing.final_results.csvio import write_csv, write_json
from scripts.testing.final_results.docx_renderer import render_docx
from scripts.testing.final_results.evidence import hash_file
from scripts.testing.final_results.markdown_renderer import render_markdown
from scripts.testing.final_results.report_model import (
    Report,
    ReportParagraph,
    ReportSection,
)
from scripts.testing.final_results.validate import (
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
        "attempt_id": f"{route_id}-attempt-1",
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
            "quality_count": 0,
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
    write_csv(route / "quality/scores.csv", (), (
        "route_id", "campaign_id", "test_case_id", "quality_id", "prompt_id",
        "criterion_id", "score", "maximum_score", "prompt_suite_id", "rubric_id",
        "scoring_version", "source_evidence_id",
    ))
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


def _write_cross_route(collection: Path) -> Path:
    route = collection / "06-cross-route-comparison"
    write_json(
        route / "route-manifest.json",
        {
            "route_id": "cross-route-comparison",
            "revision": "R1",
            "generated_date": "2026-08-30",
            "source_route_ids": ["alpha", "beta"],
            "source_campaign_ids": ["alpha-campaign", "beta-campaign"],
            "attempt_count": 2,
            "comparability_decision_count": 2,
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
    return route


def _collection(tmp_path: Path) -> tuple[Path, Path, Path, Path]:
    collection = tmp_path / "final-results"
    alpha = _write_route(collection, "01-alpha", "alpha")
    beta = _write_route(collection, "02-beta", "beta")
    cross = _write_cross_route(collection)
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
    rows.append({"test_case_id": "alpha-case-2", "intended": "true"})
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
                "route_id": "alpha",
                "campaign_id": "alpha-campaign",
                "test_case_id": "alpha-case-1",
                "attempt_id": "alpha-attempt-1",
                "failure_id": "alpha-failure-1",
                "status": "blocked",
                "stage": "preflight",
                "reason": "controlled block",
                "source_status": "blocked",
                "evidence_ids": json.dumps(["alpha-evidence"]),
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
            "attempt_id": "alpha-attempt-rejected",
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
                "route_id": "alpha",
                "campaign_id": "alpha-campaign",
                "test_case_id": "alpha-case-1",
                "attempt_id": "alpha-attempt-rejected",
                "failure_id": "alpha-rejected-failure",
                "status": "failed",
                "stage": "evidence-review",
                "reason": "retained rejected evidence",
                "source_status": "rejected",
                "evidence_ids": json.dumps(["alpha-evidence"]),
            },
        ),
        FAILURE_FIELDS,
    )

    report = validate_route(alpha)

    assert report.gate("availability").valid is True


def test_validation_catches_missing_evidence(tmp_path):
    collection, alpha, _, _ = _collection(tmp_path)
    (collection / "sources/alpha.txt").unlink()

    report = validate_route(alpha)

    assert "missing_evidence_path" in _codes(report, "paths_hashes")


def test_validation_catches_evidence_hash_mismatch(tmp_path):
    collection, alpha, _, _ = _collection(tmp_path)
    (collection / "sources/alpha.txt").write_text("changed\n", encoding="utf-8")

    report = validate_route(alpha)

    assert "evidence_hash_mismatch" in _codes(report, "paths_hashes")


def test_checkout_crlf_manifest_is_a_nonblocking_integrity_limitation(tmp_path):
    collection, alpha, _, _ = _collection(tmp_path)
    source = collection / "sources/alpha.txt"
    manifest = alpha / "evidence/manifest-sha256.txt"
    manifest.write_bytes(
        f"{hash_file(source)}  sources/alpha.txt\r\n".encode("utf-8")
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
