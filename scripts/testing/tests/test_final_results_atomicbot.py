from __future__ import annotations

import csv
import json
import shutil
import sys
from collections import Counter
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.final_results.llama_adapter import (
    ATOMICBOT_EXPECTED_IDS,
    audit_atomicbot_sources,
    build_atomicbot_bundle,
    finalize_atomicbot_route,
    write_atomicbot_route,
)
from scripts.testing.final_results.models import Status
from scripts.testing.final_results.openvino_report import SECTION_ORDER
from scripts.testing.final_results.csvio import validate_json


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
ROUTE = REPOSITORY_ROOT / "docs/testing/final-results/02-atomicbot-turboquant"


def _copy_sources(tmp_path: Path) -> Path:
    root = tmp_path / "repo"
    for relative in (
        "docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md",
        "docs/testing/Workbook-Revision-Register.csv",
        "docs/testing/Test-Run-Register.csv",
        "docs/testing/Performance-Measurement-Register.csv",
        "docs/testing/Failure-Register.csv",
        "docs/testing/Evidence-Index.csv",
        "experiments/raw-results/atomicbot-turboquant/2026-07-16/acquisition/results",
        "experiments/raw-results/atomicbot-turboquant/2026-07-16/acquisition/metrics",
        "experiments/raw-results/atomicbot-turboquant/2026-07-17/all-row-utilization-v1",
        "experiments/raw-results/atomicbot-turboquant/2026-07-17/quality-all-rows",
    ):
        source = REPOSITORY_ROOT / relative
        target = root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if source.is_dir():
            shutil.copytree(source, target)
        else:
            shutil.copy2(source, target)
    return root


def test_bundle_represents_all_runtime_configs_and_current_formal_authority():
    bundle = build_atomicbot_bundle(REPOSITORY_ROOT)

    assert tuple(attempt.test_case_id for attempt in bundle.attempts) == ATOMICBOT_EXPECTED_IDS
    assert len(bundle.attempts) == 19
    assert Counter(attempt.status for attempt in bundle.attempts) == {Status.PASSED: 19}
    assert bundle.repository["workbook_revision"] == "1.7"
    assert bundle.repository["runtime_status_counts"] == {"passed": 19, "blocked": 0}
    assert bundle.repository["setup_status_counts"]["blocked"] == 1


def test_observed_utilization_remains_attached_to_each_run():
    bundle = build_atomicbot_bundle(REPOSITORY_ROOT)
    rows = bundle.repository["utilization_observations"]

    assert len(rows) == 57
    assert Counter(row["test_case_id"] for row in rows) == Counter(
        {test_id: 3 for test_id in ATOMICBOT_EXPECTED_IDS}
    )
    assert all(row[metric] is not None for row in rows for metric in (
        "cpu_mean_percent", "cpu_median_percent", "cpu_peak_percent",
        "gpu_mean_percent", "gpu_median_percent", "gpu_peak_percent",
    ))
    assert all(row["run_id"] and row["measurement_id"] and row["source_evidence_id"] for row in rows)


def test_quality_is_limited_provisional_and_not_openvino_comparable():
    bundle = build_atomicbot_bundle(REPOSITORY_ROOT)

    assert len(bundle.quality) == 114
    assert {record.prompt_id for record in bundle.quality} == {f"P{i}" for i in range(1, 7)}
    assert bundle.repository["quality_authority"] == "limited/provisional historical screen"
    assert bundle.repository["quality_direct_openvino_comparison_permitted"] is False
    assert bundle.repository["quality_calibration"] == "Not collected"
    assert all(record.rubric_id == "ATOMICBOT-QUALITY-VERIFIER-v2.0-LIMITED" for record in bundle.quality)
    assert len({record.source_evidence_id for record in bundle.quality}) == 114


def test_quality_summary_prompt_hash_relationship_is_recomputed(tmp_path: Path):
    root = _copy_sources(tmp_path)
    prompt = root / "experiments/raw-results/atomicbot-turboquant/2026-07-17/quality-all-rows/AB-01/P1.json"
    payload = json.loads(prompt.read_text(encoding="utf-8"))
    payload["output_sha256"] = "0" * 64
    prompt.write_text(json.dumps(payload), encoding="utf-8")
    with pytest.raises(ValueError, match="quality prompt hash conflict"):
        build_atomicbot_bundle(root)


def test_joined_evidence_uses_exact_id_path_hash_and_conflicts_are_rejected(tmp_path: Path):
    root = _copy_sources(tmp_path)
    audit = audit_atomicbot_sources(root)
    assert audit["joined_evidence_count"] >= 57
    assert audit["joined_evidence_hash_conflicts"] == 0

    index = root / "docs/testing/Evidence-Index.csv"
    rows = list(csv.DictReader(index.open(encoding="utf-8-sig", newline="")))
    performance = next(item for item in csv.DictReader(
        (root / "docs/testing/Performance-Measurement-Register.csv").open(encoding="utf-8-sig", newline="")
    ) if item["Route"] == "atomicbot-turboquant")
    joined_path = performance["Raw_Metrics_Path"].replace("\\", "/")
    row = next(item for item in rows if item["Repository_Path"].replace("\\", "/") == joined_path)
    row["SHA256"] = "0" * 64
    with index.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=rows[0].keys(), lineterminator="\n")
        writer.writeheader(); writer.writerows(rows)
    with pytest.raises(ValueError, match="hash conflict"):
        audit_atomicbot_sources(root)


def test_conflicting_register_identity_and_silent_filtering_are_rejected(tmp_path: Path):
    root = _copy_sources(tmp_path)
    register = root / "docs/testing/Performance-Measurement-Register.csv"
    rows = list(csv.DictReader(register.open(encoding="utf-8-sig", newline="")))
    row = next(item for item in rows if item["Route"] == "atomicbot-turboquant")
    row["Test_ID"] = "AB-UNKNOWN"
    with register.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=rows[0].keys(), lineterminator="\n")
        writer.writeheader(); writer.writerows(rows)
    with pytest.raises(ValueError, match="complete 57-row set"):
        build_atomicbot_bundle(root)


def test_route_generation_has_common_structure_schema_parity_and_honest_caveats():
    bundle = write_atomicbot_route(REPOSITORY_ROOT)
    markdown = ROUTE / "workbook/source/atomicbot-turboquant-final-report.md"
    text = markdown.read_text(encoding="utf-8")

    assert len(bundle.attempts) == 19
    assert all(heading in text for heading in SECTION_ORDER)
    assert "limited/provisional" in text
    assert "not directly comparable with OpenVINO" in text
    assert "Not collected" in text
    assert json.loads((ROUTE / "validation/workbook-parity.json").read_text())["matches"] is True
    assert json.loads((ROUTE / "validation/relationship-validation.json").read_text())["valid"] is True
    assert json.loads((ROUTE / "validation/coverage-validation.json").read_text())["valid"] is True
    assert json.loads((ROUTE / "validation/data-validation.json").read_text())["valid"] is True


def test_all_canonical_records_validate_against_shared_schemas_and_references():
    bundle = build_atomicbot_bundle(REPOSITORY_ROOT)
    schemas = REPOSITORY_ROOT / "docs/testing/final-results/standards/schemas"
    collections = (
        (bundle.attempts, "attempts.schema.json"),
        (bundle.measurements, "measurements.schema.json"),
        (bundle.summaries, "results.schema.json"),
        (bundle.quality, "quality.schema.json"),
        (bundle.failures, "failures.schema.json"),
        (bundle.evidence, "evidence.schema.json"),
    )
    for records, schema in collections:
        assert [(record, errors) for record in records if (
            errors := validate_json(record.to_row(), schemas / schema)
        )] == []
    assert validate_json(bundle.to_row(), schemas / "route-manifest.schema.json") == []

    attempts = {item.attempt_id for item in bundle.attempts}
    measurements = {item.measurement_id for item in bundle.measurements}
    evidence = {item.evidence_id for item in bundle.evidence}
    assert all(item.attempt_id in attempts and item.source_evidence_id in evidence for item in bundle.measurements)
    assert all(set(item.source_measurement_ids) <= measurements for item in bundle.summaries)
    assert all(item.source_evidence_id in evidence for item in bundle.quality)


def test_generated_inventory_manifest_and_reproduction_contract():
    expected = (
        "README.md", "route-manifest.json", "protocol/intended-test-matrix.csv",
        "system/repository.json", "results/attempts.csv", "results/measurements.csv",
        "results/summary-results.csv", "quality/scores.csv", "quality/rubric.md",
        "failures/failure-register.csv", "evidence/evidence-index.csv",
        "evidence/claim-evidence-map.csv", "reproduction/commands.md",
        "validation/workbook-parity.json", "workbook/source/atomicbot-turboquant-final-report.md",
        "workbook/generated/atomicbot-turboquant-final-report.docx",
    )
    for relative in expected:
        assert (ROUTE / relative).is_file(), relative
    commands = (ROUTE / "reproduction/commands.md").read_text(encoding="utf-8")
    assert "write_atomicbot_route" in commands
    assert "finalize_atomicbot_route" in commands
    assert "-TimeoutSeconds 180" in commands
    manifest_lines = (ROUTE / "evidence/manifest-sha256.txt").read_text(encoding="utf-8").splitlines()
    files = [path for path in ROUTE.rglob("*") if path.is_file()]
    assert len(manifest_lines) == len(files) - 1


def test_pdf_finalizer_uses_actual_structural_page_count():
    receipt = finalize_atomicbot_route(REPOSITORY_ROOT)
    assert receipt["valid"] is True
    assert receipt["checks"]["page_count"] >= 10
    assert receipt["inspected_pages"] == list(range(1, receipt["checks"]["page_count"] + 1))
