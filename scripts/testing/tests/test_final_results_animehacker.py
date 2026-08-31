from __future__ import annotations

import json
import re
import shutil
import sys
from collections import Counter
from dataclasses import replace
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.final_results.csvio import validate_json
from scripts.testing.final_results.llama_adapter import (
    ANIMEHACKER_EXPECTED_IDS,
    _animehacker_relationship_receipt,
    audit_animehacker_sources,
    build_animehacker_bundle,
    build_animehacker_report,
    finalize_animehacker_route,
    write_animehacker_route,
)
from scripts.testing.final_results.models import Status
from scripts.testing.final_results.openvino_report import SECTION_ORDER
from scripts.testing.final_results.report_model import ReportParagraph, ReportTable


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
ROUTE = REPOSITORY_ROOT / "docs/testing/final-results/03-animehacker-tq3-0"
RAW = Path("experiments/raw-results/animehacker-tq3-0/2026-07-18")


def _copy_sources(tmp_path: Path) -> Path:
    root = tmp_path / "repo"
    for relative in (
        "docs/testing/workbooks/text-templates/03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.md",
        "docs/testing/Workbook-Revision-Register.csv",
        "docs/testing/Quality-Evaluation-Register.csv",
        "experiments/manifests/animehacker-tq3-0/retest-matrix.json",
        "experiments/raw-results/animehacker-tq3-0/2026-07-18",
        "experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json",
        "experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json",
    ):
        source = REPOSITORY_ROOT / relative
        target = root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if source.is_dir():
            shutil.copytree(source, target)
        else:
            shutil.copy2(source, target)
    return root


def test_final_status_authority_has_seven_completed_and_three_safety_rows():
    bundle = build_animehacker_bundle(REPOSITORY_ROOT)
    terminal = [row for row in bundle.attempts if row.attempt_id.endswith("--terminal")]

    assert tuple(row.test_case_id for row in terminal) == ANIMEHACKER_EXPECTED_IDS
    assert Counter(row.status for row in terminal) == {
        Status.PASSED: 7,
        Status.BLOCKED: 3,
    }
    assert all(row.executed for row in terminal if row.status is Status.PASSED)
    assert {row.test_case_id for row in terminal if row.status is Status.BLOCKED} == {
        "AH-06", "AH-07", "AH-10",
    }
    assert bundle.repository["workbook_revision"] == "1.5"
    assert bundle.repository["unresolved_failure_count"] == 0


def test_runtime_summaries_are_measurement_authority_and_rejected_rows_are_excluded():
    bundle = build_animehacker_bundle(REPOSITORY_ROOT)
    formal = [row for row in bundle.measurements if row.repetition_id]

    assert len(formal) == 21
    assert Counter(row.test_case_id for row in formal) == Counter({
        "AH-01": 3, "AH-02": 3, "AH-03": 3, "AH-04": 3,
        "AH-05": 3, "AH-08": 3, "AH-09": 3,
    })
    assert all("rejected" not in (row.source_evidence_id or "") for row in formal)
    assert bundle.repository["formal_runtime_summary_count"] == 7
    assert bundle.repository["rejected_runtime_summary_count"] >= 2
    assert not any(
        row.value == 1_000_000.0
        for row in bundle.summaries
        if row.metric_name == "generation_tokens_per_second"
    )


def test_rejected_runtime_is_retained_as_historical_attempt_and_failure():
    bundle = build_animehacker_bundle(REPOSITORY_ROOT)
    rejected = [row for row in bundle.attempts if row.source_status == "rejected"]

    assert any(row.test_case_id == "AH-09" for row in rejected)
    assert bundle.repository["historical_failure_attempt_count"] > 0
    assert len(bundle.failures) > 0
    assert any(row.test_case_id == "AH-09" and "rejected" in row.reason.lower() for row in bundle.failures)
    assert bundle.repository["unresolved_failure_count"] == 0


def test_missing_os_metrics_remain_literal_not_collected():
    bundle = build_animehacker_bundle(REPOSITORY_ROOT)
    by_test = {row["test_case_id"]: row for row in bundle.repository["resource_observations"]}

    for test_id in ("AH-06", "AH-07", "AH-10"):
        assert by_test[test_id]["cpu_mean_percent"] == "Not collected"
        assert by_test[test_id]["gpu_mean_percent"] == "Not collected"
        assert by_test[test_id]["time_to_first_token_ms"] == "Not collected"
        assert by_test[test_id]["generation_tokens_per_second"] == "Not collected"
    assert not any(value == 0 for row in by_test.values() for value in row.values() if value == "Not collected")


def test_explicit_reconciliation_inclusion_is_required(tmp_path: Path):
    root = _copy_sources(tmp_path)
    state_path = root / RAW / "runtime/state.json"
    state = json.loads(state_path.read_text(encoding="utf-8"))
    state["attempts"]["AH-01"].pop("reconciled")
    state_path.write_text(json.dumps(state), encoding="utf-8")

    with pytest.raises(ValueError, match="explicit reconciliation inclusion"):
        build_animehacker_bundle(root)


def test_summary_not_selected_by_reconciliation_cannot_enter_formal_stats(tmp_path: Path):
    root = _copy_sources(tmp_path)
    reconciliation_path = root / RAW / "reconciliation.json"
    reconciliation = json.loads(reconciliation_path.read_text(encoding="utf-8"))
    reconciliation["recovery"]["AH-09"]["runtime"] = (
        "experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/"
        "AH-09-rejected-flash-env-only/summary.json"
    )
    reconciliation_path.write_text(json.dumps(reconciliation), encoding="utf-8")

    with pytest.raises(ValueError, match="rejected runtime summary"):
        build_animehacker_bundle(root)


def test_formal_summary_sample_must_equal_its_raw_measurement(tmp_path: Path):
    root = _copy_sources(tmp_path)
    path = root / RAW / "runtime/AH-01/summary.json"
    payload = json.loads(path.read_text(encoding="utf-8"))
    payload["samples"][0]["ttft_ms"] = 999.0
    values = [row["ttft_ms"] for row in payload["samples"]]
    payload["aggregate"]["ttft_ms"] = {
        "min": min(values), "max": max(values),
        "mean": sum(values) / 3, "median": sorted(values)[1],
    }
    path.write_text(json.dumps(payload), encoding="utf-8")

    with pytest.raises(ValueError, match="raw measurement relationship conflict"):
        build_animehacker_bundle(root)


def test_matrix_complete_entity_is_authenticated(tmp_path: Path):
    root = _copy_sources(tmp_path)
    path = root / "experiments/manifests/animehacker-tq3-0/retest-matrix.json"
    payload = json.loads(path.read_text(encoding="utf-8"))
    runtime = next(row for row in payload["cases"] if row.get("test_id") == "AH-01")
    runtime["model_id"] = "fabricated-model"
    path.write_text(json.dumps(payload), encoding="utf-8")

    with pytest.raises(ValueError, match="intended-matrix authority hash conflict"):
        build_animehacker_bundle(root)


def test_coordinated_summary_and_measurement_edit_cannot_redefine_authority(tmp_path: Path):
    root = _copy_sources(tmp_path)
    summary_path = root / RAW / "runtime/AH-01/summary.json"
    measurement_path = root / RAW / "runtime/AH-01/sample-1/measurement.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    measurement = json.loads(measurement_path.read_text(encoding="utf-8"))
    summary["samples"][0]["ttft_ms"] = 999.0
    measurement["ttft_ms"] = 999.0
    values = [row["ttft_ms"] for row in summary["samples"]]
    summary["aggregate"]["ttft_ms"] = {
        "min": min(values), "max": max(values),
        "mean": sum(values) / 3, "median": sorted(values)[1],
    }
    summary_path.write_text(json.dumps(summary), encoding="utf-8")
    measurement_path.write_text(json.dumps(measurement), encoding="utf-8")

    with pytest.raises(ValueError, match="formal-runtime-summary authority hash conflict"):
        build_animehacker_bundle(root)


def test_historical_failure_complete_entity_is_authenticated(tmp_path: Path):
    root = _copy_sources(tmp_path)
    path = root / "docs/testing/workbooks/text-templates/03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.md"
    text = path.read_text(encoding="utf-8")
    path.write_text(text.replace("| AH-F01 | AH-B02 | DEP |", "| AH-F01 | AH-B02 | FAKE |", 1), encoding="utf-8")

    with pytest.raises(ValueError, match="controlled-workbook-markdown authority hash conflict"):
        build_animehacker_bundle(root)


def test_relationship_receipt_reauthenticates_authority_evidence():
    bundle = build_animehacker_bundle(REPOSITORY_ROOT)
    authority = next(row for row in bundle.evidence if row.role == "intended-matrix")
    forged = replace(authority, sha256="0" * 64)
    mutated = replace(bundle, evidence=tuple(
        forged if row.evidence_id == authority.evidence_id else row for row in bundle.evidence
    ))

    receipt = _animehacker_relationship_receipt(mutated)

    assert receipt["valid"] is False
    assert "authority evidence" in " ".join(receipt["errors"])
    assert receipt["authenticated_authority_count"] >= 40


def test_report_uses_bounded_key_evidence_and_reader_formatted_metrics():
    bundle = build_animehacker_bundle(REPOSITORY_ROOT)
    report = build_animehacker_report(bundle)
    evidence_section = report.sections[13]
    evidence_table = next(block for block in evidence_section.blocks if isinstance(block, ReportTable))
    performance_table = next(
        block for section in report.sections for block in section.blocks
        if isinstance(block, ReportTable) and block.table_id == "PF-01"
    )

    assert 8 <= len(report.evidence_ids) <= 16
    assert len(evidence_table.rows) == len(report.evidence_ids)
    assert len(evidence_table.rows) < len(bundle.evidence)
    assert any(
        isinstance(block, ReportParagraph) and "evidence-index.csv" in block.text
        for block in evidence_section.blocks
    )
    assert not any(re.search(r"\d+\.\d{7,}", cell) for row in performance_table.rows for cell in row)


def test_wide_report_tables_have_context_before_their_heading_and_table():
    report = build_animehacker_report(build_animehacker_bundle(REPOSITORY_ROOT))

    def is_wide(table: ReportTable) -> bool:
        characters = sum(
            max([len(table.columns[index]), *(len(row[index]) for row in table.rows)])
            for index in range(len(table.columns))
        )
        return len(table.columns) > 6 or characters > 95

    for section in report.sections:
        first_wide = next(
            (index for index, block in enumerate(section.blocks) if isinstance(block, ReportTable) and is_wide(block)),
            None,
        )
        if first_wide is not None:
            assert any(isinstance(block, ReportParagraph) for block in section.blocks[:first_wide]), section.title


def test_build_reconciliations_and_quality_adjudications_are_bound():
    bundle = build_animehacker_bundle(REPOSITORY_ROOT)
    audit = bundle.repository["source_reconciliation"]

    assert audit["cpu_repository_tests"] == {"passed": 40, "failed": 0, "total": 40}
    assert audit["sycl_repository_tests"] == {"passed": 40, "failed": 0, "total": 40}
    assert audit["vulkan_tq3_runtime_classification"] == "not proven by source audit; not a controlled TQ3 route"
    assert len(bundle.quality) == 42
    assert Counter(row.test_case_id for row in bundle.quality) == Counter({
        "AH-01": 6, "AH-02": 6, "AH-03": 6, "AH-04": 6,
        "AH-05": 6, "AH-08": 6, "AH-09": 6,
    })
    assert bundle.repository["quality_calibration"] == "Not collected"
    assert bundle.repository["quality_direct_openvino_comparison_permitted"] is False


def test_reconciliation_status_mutation_is_rejected(tmp_path: Path):
    root = _copy_sources(tmp_path)
    path = root / RAW / "reconciliation.json"
    payload = json.loads(path.read_text(encoding="utf-8"))
    payload["recovery"]["AH-10"]["status"] = "complete"
    path.write_text(json.dumps(payload), encoding="utf-8")

    with pytest.raises(ValueError, match="status authority conflict"):
        audit_animehacker_sources(root)


def test_generated_route_has_common_structure_parity_integrity_and_portability():
    bundle = write_animehacker_route(REPOSITORY_ROOT)
    markdown = ROUTE / "workbook/source/animehacker-tq3-0-final-report.md"
    text = markdown.read_text(encoding="utf-8")

    assert all(heading in text for heading in SECTION_ORDER)
    assert "seven" in text.lower() and "three" in text.lower()
    assert "historical failure" in text.lower()
    assert "Not collected" in text
    assert json.loads((ROUTE / "validation/workbook-parity.json").read_text())["matches"] is True
    assert json.loads((ROUTE / "validation/relationship-validation.json").read_text())["valid"] is True
    assert json.loads((ROUTE / "validation/coverage-validation.json").read_text())["valid"] is True
    assert len(bundle.attempts) > 10

    forbidden = ("C:\\Users\\", "C:/Users/", "\\\\?\\", str(REPOSITORY_ROOT))
    for path in ROUTE.rglob("*"):
        if path.is_file() and path.suffix.lower() not in {".docx", ".pdf"}:
            rendered = path.read_text(encoding="utf-8", errors="ignore")
            assert not any(value in rendered for value in forbidden), path


def test_canonical_records_validate_and_relationships_reconcile():
    bundle = build_animehacker_bundle(REPOSITORY_ROOT)
    schemas = REPOSITORY_ROOT / "docs/testing/final-results/standards/schemas"
    for records, schema in (
        (bundle.attempts, "attempts.schema.json"),
        (bundle.measurements, "measurements.schema.json"),
        (bundle.summaries, "results.schema.json"),
        (bundle.quality, "quality.schema.json"),
        (bundle.failures, "failures.schema.json"),
        (bundle.evidence, "evidence.schema.json"),
    ):
        assert [(record, errors) for record in records if (
            errors := validate_json(record.to_row(), schemas / schema)
        )] == []
    assert validate_json(bundle.to_row(), schemas / "route-manifest.schema.json") == []
    receipt = _animehacker_relationship_receipt(bundle)
    assert receipt["valid"] is True
    assert receipt["terminal_attempt_count"] == 10
    assert receipt["formal_measurement_count"] == 21
    assert receipt["historical_failure_count"] > 0


def test_generated_inventory_manifest_and_reproduction_contract():
    expected = (
        "README.md", "route-manifest.json", "protocol/intended-test-matrix.csv",
        "system/repository.json", "results/attempts.csv", "results/measurements.csv",
        "results/summary-results.csv", "results/resource-observations.csv",
        "quality/scores.csv", "quality/rubric.md", "failures/failure-register.csv",
        "evidence/evidence-index.csv", "evidence/claim-evidence-map.csv",
        "reproduction/commands.md", "validation/workbook-parity.json",
        "workbook/source/animehacker-tq3-0-final-report.md",
        "workbook/generated/animehacker-tq3-0-final-report.docx",
    )
    for relative in expected:
        assert (ROUTE / relative).is_file(), relative
    commands = (ROUTE / "reproduction/commands.md").read_text(encoding="utf-8")
    assert "write_animehacker_route" in commands
    assert "finalize_animehacker_route" in commands
    assert "-TimeoutSeconds 180" in commands
    lines = (ROUTE / "evidence/manifest-sha256.txt").read_text(encoding="utf-8").splitlines()
    files = [path for path in ROUTE.rglob("*") if path.is_file()]
    assert len(lines) == len(files) - 1


def test_pdf_finalizer_uses_actual_structural_page_count():
    receipt = finalize_animehacker_route(REPOSITORY_ROOT)
    assert receipt["valid"] is True
    assert receipt["checks"]["page_count"] >= 7
    assert receipt["inspected_pages"] == list(range(1, receipt["checks"]["page_count"] + 1))
    expected_ranges = [
        f"{start + 1}-{min(start + 9, receipt['checks']['page_count'])}"
        for start in range(0, receipt["checks"]["page_count"], 9)
    ]
    assert receipt["visual_findings"]["inspection_page_ranges"] == expected_ranges
    assert receipt["visual_findings"]["inspection_method"] == (
        "Rendered every PDF page with PyMuPDF and inspected all full-page images"
    )
    assert receipt["visual_findings"]["evidence_table_pages"] == "7"
    assert receipt["visual_findings"]["revision_history_pages"] == "7"
