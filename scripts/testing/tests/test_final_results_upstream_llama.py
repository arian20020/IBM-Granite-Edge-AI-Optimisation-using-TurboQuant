import csv
import dataclasses
import json
import sys
from pathlib import Path

import pytest


sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.final_results.models import Status


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
EXPECTED_IDS = {f"UL-{number:02d}" for number in range(1, 14)}
EXPECTED_SECTIONS = [
    "1. Title and document control",
    "2. Technical summary",
    "3. Key findings and decision-relevant evidence",
    "4. Repository, branch, commit, build, hardware, and software identity",
    "5. Objectives, scope, test matrix, and execution sequence",
    "6. Model, weight, cache-format, and backend availability",
    "7. Complete attempt accounting",
    "8. Performance results and repetition detail",
    "9. Quality methodology and results",
    "10. Device/backend use and fallback verification",
    "11. Failures, blocks, deviations, and recovery attempts",
    "12. Limitations, uncertainty, robustness checks, and claim boundaries",
    "13. Reproduction guidance",
    "14. Evidence index and hashes",
    "15. Revision history",
]


def _module():
    from scripts.testing.final_results import llama_adapter

    return llama_adapter


def _text(report) -> str:
    values = [report.title, report.route_id, report.revision, *report.evidence_ids]
    for section in report.sections:
        values.append(section.title)
        for block in section.blocks:
            if hasattr(block, "text"):
                values.append(block.text)
            else:
                values.extend((block.table_id, block.title, *block.columns))
                values.extend(cell for row in block.rows for cell in row)
                values.extend(block.footnotes)
    return "\n".join(values)


def test_bundle_covers_exact_matrix_completed_workloads_and_narrow_claims():
    bundle = _module().build_upstream_llama_bundle(REPOSITORY_ROOT)

    assert {attempt.test_case_id for attempt in bundle.attempts} == EXPECTED_IDS
    assert len(bundle.attempts) == 13
    assert all(attempt.executed and attempt.status is Status.PASSED for attempt in bundle.attempts)
    assert bundle.repository["best_observed_cpu_test_id"] == "UL-08"
    assert bundle.repository["best_observed_intel_gpu_test_id"] == "UL-10"
    assert bundle.repository["recorded_fallback_test_id"] == "UL-05"
    assert "only" in str(bundle.repository["recommendation_scope"]).casefold()


def test_repetitions_expand_from_source_evidence_and_missing_values_are_not_zero():
    bundle = _module().build_upstream_llama_bundle(REPOSITORY_ROOT)

    assert len(bundle.measurements) == 80
    bench = [item for item in bundle.measurements if item.repetition_id.startswith("bench-")]
    server = [item for item in bundle.measurements if item.repetition_id.startswith("server-")]
    assert len(bench) == 41
    assert len(server) == 39
    assert all(item.generation_tokens_per_second is not None for item in bench)
    assert all(item.prompt_tokens_per_second is not None for item in bench)
    assert all(item.latency_ms is None and item.peak_working_set_bytes is None for item in bench)
    assert all(item.latency_ms is not None and item.peak_working_set_bytes is not None for item in server)
    assert all(item.generation_tokens_per_second is None for item in server)
    assert all(item.prompt_tokens_per_second is None for item in server)
    assert all(measurement.input_tokens is None for measurement in bundle.measurements)
    assert all(measurement.output_tokens is None for measurement in bundle.measurements)
    assert bundle.repository["historical_missing_metric_display"] == "Not collected"


def test_evidence_index_paths_resolve_and_hashes_match_every_admitted_log():
    module = _module()
    bundle = module.build_upstream_llama_bundle(REPOSITORY_ROOT)

    indexed_logs = [
        row
        for row in csv.DictReader(
            (REPOSITORY_ROOT / "docs/testing/Evidence-Index.csv").open(
                encoding="utf-8-sig", newline=""
            )
        )
        if row["Route"] == "upstream-llama-cpp"
        and row["Test_ID"] in EXPECTED_IDS
        and row["Repository_Path"].startswith(
            "experiments/granite_turboquant_intel/logs/upstream-llama-cpp/"
        )
    ]
    admitted = {record.relative_path: record for record in bundle.evidence}

    assert indexed_logs
    assert set(row["Repository_Path"] for row in indexed_logs) <= set(admitted)
    assert all((REPOSITORY_ROOT / record.relative_path).is_file() for record in bundle.evidence)
    assert all(module.hash_file(REPOSITORY_ROOT / record.relative_path) == record.sha256 for record in bundle.evidence)


def test_absent_raw_results_are_documented_not_fabricated():
    bundle = _module().build_upstream_llama_bundle(REPOSITORY_ROOT)

    assert all(
        not record.relative_path.startswith("experiments/raw-results/upstream-llama-cpp/")
        or record.relative_path.endswith("README.md")
        for record in bundle.evidence
    )
    assert bundle.repository["raw_results_status"] == "Not collected"
    assert bundle.repository["raw_results_reason"] == "README placeholder only; no raw result observations"


def test_original_quality_method_and_comparison_boundary_are_preserved():
    module = _module()
    bundle = module.build_upstream_llama_bundle(REPOSITORY_ROOT)
    report = module.build_upstream_llama_report(bundle)
    text = _text(report).casefold()

    assert len(bundle.quality) == 54
    assert {record.prompt_id for record in bundle.quality} == {"P1", "P2", "P3", "P4", "P5", "P6"}
    assert {record.rubric_id for record in bundle.quality} == {"upstream-llama-quality-2026-07-15"}
    assert "format caps" in text
    assert "conservative" in text
    assert "not directly comparable" in text
    assert "openvino" in text


def test_report_uses_approved_sections_and_derives_complete_accounting():
    module = _module()
    bundle = module.build_upstream_llama_bundle(REPOSITORY_ROOT)
    report = module.build_upstream_llama_report(bundle)
    text = _text(report)

    assert [section.title for section in report.sections] == EXPECTED_SECTIONS
    assert "UL-08" in text and "best observed CPU" in text
    assert "UL-10" in text and "best observed Intel GPU" in text
    assert "UL-05" in text and "recorded fallback" in text
    assert "UL-04" not in bundle.repository["recommendation_scope"]
    assert len(report.evidence_ids) > 0


def test_source_mutation_is_rejected_instead_of_silently_reinterpreted(tmp_path):
    module = _module()
    source = REPOSITORY_ROOT / "docs/testing/workbooks/text-templates/01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.md"
    mutated = tmp_path / source.name
    mutated.write_text(
        source.read_text(encoding="utf-8").replace("| UL-13 |", "| UL-99 |", 1),
        encoding="utf-8",
    )

    with pytest.raises(ValueError, match="UL-01 through UL-13"):
        module._parse_workbook_matrix(mutated)


def test_bundle_mutation_changes_report_values_proving_source_derived_rendering():
    module = _module()
    bundle = module.build_upstream_llama_bundle(REPOSITORY_ROOT)
    original = module.build_upstream_llama_report(bundle)
    summary = next(
        item
        for item in bundle.summaries
        if item.test_case_id == "UL-08" and item.metric_name == "generation_tokens_per_second"
    )
    changed = dataclasses.replace(summary, value=float(summary.value) + 1.0)
    changed_bundle = dataclasses.replace(
        bundle,
        summaries=tuple(changed if item.summary_id == summary.summary_id else item for item in bundle.summaries),
    )

    assert _text(module.build_upstream_llama_report(changed_bundle)) != _text(original)


def test_canonical_records_validate_and_references_reconcile():
    from scripts.testing.final_results.csvio import validate_json

    bundle = _module().build_upstream_llama_bundle(REPOSITORY_ROOT)
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
        assert [
            (record, errors)
            for record in records
            if (errors := validate_json(record.to_row(), schemas / schema))
        ] == []
    assert validate_json(bundle.to_row(), schemas / "route-manifest.schema.json") == []

    attempt_ids = {item.attempt_id for item in bundle.attempts}
    evidence_ids = {item.evidence_id for item in bundle.evidence}
    measurement_ids = {item.measurement_id for item in bundle.measurements}
    assert all(item.attempt_id in attempt_ids for item in bundle.measurements)
    assert all(item.source_evidence_id in evidence_ids for item in bundle.measurements)
    assert all(item.source_evidence_id in evidence_ids for item in bundle.quality)
    assert all(set(item.source_measurement_ids) <= measurement_ids for item in bundle.summaries)


def test_generated_route_inventory_receipts_and_semantic_parity_exist():
    route = REPOSITORY_ROOT / "docs/testing/final-results/01-upstream-llama-cpp"
    expected = {
        "README.md",
        "route-manifest.json",
        "protocol/intended-test-matrix.csv",
        "results/attempts.csv",
        "results/measurements.csv",
        "results/summary-results.csv",
        "quality/scores.csv",
        "failures/failure-register.csv",
        "evidence/evidence-index.csv",
        "evidence/manifest-sha256.txt",
        "workbook/source/upstream-llama-cpp-final-report.md",
        "workbook/generated/upstream-llama-cpp-final-report.docx",
        "workbook/generated/upstream-llama-cpp-final-report.pdf",
        "validation/coverage-validation.json",
        "validation/data-validation.json",
        "validation/workbook-parity.json",
        "validation/integrity-validation.json",
        "validation/visual-validation.json",
    }

    assert expected <= {
        path.relative_to(route).as_posix() for path in route.rglob("*") if path.is_file()
    }
    assert json.loads((route / "validation/workbook-parity.json").read_text(encoding="utf-8"))["matches"] is True
    assert json.loads((route / "validation/visual-validation.json").read_text(encoding="utf-8"))["valid"] is True
