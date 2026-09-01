import csv
import dataclasses
import json
import subprocess
import sys
from pathlib import Path

import pytest


sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.reporting.models import Status


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
    from scripts.testing.reporting import llama_adapter

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
    assert {record.rubric_id for record in bundle.quality} == {"GTQ-QUALITY-RUBRIC-v1"}
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
    from scripts.testing.reporting.csvio import validate_json

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


def test_independent_authorities_reconcile_and_known_divergences_are_explicit():
    module = _module()
    audit = module.audit_upstream_llama_sources(REPOSITORY_ROOT)

    assert audit["exact_test_run_register_rows"] == 0
    assert audit["exact_performance_register_rows"] == 0
    assert audit["matrix_ids"] == list(module.EXPECTED_IDS)
    assert audit["formal_ids"] == list(module.EXPECTED_IDS)
    assert audit["evidence_binding_count"] == 642
    assert audit["resource_rows_reconciled"] == 13
    assert audit["quality_rows_reconciled"] == 13
    assert audit["known_divergences"] == {
        "UL-13-performance": {
            "workbook_prompt_tokens_per_second": 40.789,
            "computed_prompt_tokens_per_second": 40.844,
            "workbook_decode_tokens_per_second": 7.211,
            "computed_decode_tokens_per_second": 7.212,
            "precedence": "indexed repetition logs",
        },
        "UL-05-quality": {
            "workbook_displayed_mean": 5.9,
            "arithmetic_prompt_mean": 5.75,
            "precedence": "prompt-level scores",
        },
        "decision-labels": {
            "workbook_cpu": "UL-04 for speed; UL-03 when quality is primary",
            "workbook_gpu": "UL-09",
            "workbook_fallback": "UL-04 or UL-03",
            "approved_cpu": "UL-08 only",
            "approved_gpu": "UL-10 only",
            "approved_fallback": "UL-05 only",
            "precedence": "approved bounded publication labels",
        },
    }


def test_register_mutation_is_rejected(tmp_path):
    module = _module()
    register = tmp_path / "register.csv"
    register.write_text("Test_ID,Run_ID\nUL-01,mutated\n", encoding="utf-8")
    with pytest.raises(ValueError, match="unexpected exact UL-01 through UL-13 row"):
        module._assert_no_exact_register_rows(register, "mutated register")


@pytest.mark.parametrize(
    ("field", "mutated_value", "message"),
    (
        ("Test_ID", "UL-99", "Evidence-Index binding conflict"),
        ("Run_ID", "UL-99-R003", "Evidence-Index binding conflict"),
        ("Repository_Path", "experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-99/UL-01-R003/bench-stderr.jsonl", "expected evidence tuple set mismatch"),
        ("SHA256", "0" * 64, "Evidence-Index hash conflict"),
    ),
)
def test_each_evidence_tuple_mutation_is_rejected_without_reducing_admission(
    tmp_path, field, mutated_value, message
):
    module = _module()
    source = REPOSITORY_ROOT / "docs/testing/Evidence-Index.csv"
    mutated = tmp_path / "Evidence-Index.csv"
    with source.open(encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
        fields = tuple(rows[0])
    target = next(
        row
        for row in rows
        if row["Repository_Path"].endswith("/UL-01/UL-01-R003/bench-stderr.jsonl")
    )
    target[field] = mutated_value
    with mutated.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)

    with pytest.raises(ValueError, match=message):
        module._evidence_records(REPOSITORY_ROOT, evidence_index_path=mutated)


@pytest.mark.parametrize(
    ("source_name", "old", "new", "message"),
    (
        ("workbook", "6.353 decode; 28.676 prompt", "6.999 decode; 28.676 prompt", "formal performance mismatch"),
        ("quality", "| UL-02 | 9.5 | 9.0 | 7.0 | 10.0 | 8.9 |", "| UL-02 | 9.5 | 9.0 | 7.0 | 10.0 | 8.0 |", "quality aggregate mismatch"),
        ("resource", '"UL-02": {"peak_ram_mb": 6712.36', '"UL-02": {"peak_ram_mb": 6719.36', "processed resource mismatch"),
    ),
)
def test_metric_and_quality_authority_mutations_are_rejected(
    tmp_path, source_name, old, new, message
):
    module = _module()
    paths = {
        "workbook": REPOSITORY_ROOT / module.WORKBOOK_RELATIVE,
        "quality": REPOSITORY_ROOT / module.QUALITY_RELATIVE,
        "resource": REPOSITORY_ROOT / module.RESOURCE_RELATIVE,
    }
    overrides = {
        "workbook_path": REPOSITORY_ROOT / module.WORKBOOK_RELATIVE,
        "quality_path": REPOSITORY_ROOT / module.QUALITY_RELATIVE,
        "resource_path": REPOSITORY_ROOT / module.RESOURCE_RELATIVE,
    }
    mutated = tmp_path / paths[source_name].name
    payload = paths[source_name].read_text(encoding="utf-8")
    assert old in payload
    mutated.write_text(payload.replace(old, new, 1), encoding="utf-8")
    overrides[f"{source_name}_path"] = mutated

    with pytest.raises(ValueError, match=message):
        module.audit_upstream_llama_sources(REPOSITORY_ROOT, **overrides)


def test_historical_deviation_scopes_and_canonical_failure_relationships_are_stable():
    module = _module()
    bundle = module.build_upstream_llama_bundle(REPOSITORY_ROOT)
    deviations = module.build_upstream_deviation_rows(REPOSITORY_ROOT, bundle)
    historical = [row for row in deviations if row["source_failure_id"].startswith("UL-F")]
    relationships = module.validate_upstream_relationships(bundle, deviations)

    assert {row["source_failure_id"] for row in historical} == {
        "UL-F01", "UL-F02", "UL-F03", "UL-F04", "UL-F05", "UL-F06", "UL-F07"
    }
    assert all(row["nonterminal"] is True for row in historical)
    assert bundle.repository["setup_scope_ids"] == [
        "UL-B01", "UL-B02", "UL-B03", "UL-B04", "UL-B05", "UL-B06", "UL-B07"
    ]
    assert next(row for row in historical if row["source_failure_id"] == "UL-F01")["scope_type"] == "setup"
    assert next(row for row in historical if row["source_failure_id"] == "UL-F07")["scope_test_ids"] == list(module.EXPECTED_IDS)
    assert len(bundle.failures) == 5
    assert {failure.test_case_id for failure in bundle.failures} <= EXPECTED_IDS
    assert all(failure.attempt_id in {attempt.attempt_id for attempt in bundle.attempts} for failure in bundle.failures)
    assert all("UL-B" not in failure.test_case_id and "All formal" not in failure.test_case_id for failure in bundle.failures)
    assert relationships["valid"] is True
    assert relationships["errors"] == []


def test_relationship_validation_detects_attempt_scope_and_evidence_mutations():
    module = _module()
    bundle = module.build_upstream_llama_bundle(REPOSITORY_ROOT)
    deviations = list(module.build_upstream_deviation_rows(REPOSITORY_ROOT, bundle))
    broken_failure = dataclasses.replace(bundle.failures[0], attempt_id="UL-B06--attempt-001")
    broken_bundle = dataclasses.replace(bundle, failures=(broken_failure, *bundle.failures[1:]))
    deviations[0] = {**deviations[0], "evidence_ids": ["missing-evidence-id"]}

    receipt = module.validate_upstream_relationships(broken_bundle, deviations)

    assert receipt["valid"] is False
    assert any("unknown attempt" in error for error in receipt["errors"])
    assert any("unknown evidence" in error for error in receipt["errors"])


@pytest.mark.parametrize(
    ("deviation_id", "changes", "message"),
    (
        ("UL-DEV-HIST-01", {"scope_test_ids": ["UL-B99"]}, "unknown setup scope"),
        ("UL-DEV-HIST-04", {"scope_test_ids": []}, "scope must not be empty"),
        ("UL-DEV-HIST-04", {"scope_type": "setup"}, "setup scope must contain only"),
        ("UL-DEV-HIST-06", {"scope_test_ids": ["UL-10"]}, "multi-test scope requires at least two"),
        ("UL-DEV-HIST-04", {"evidence_ids": []}, "evidence must not be empty"),
        ("UL-DEV-HIST-04", {"evidence_ids": ["missing-evidence-id"]}, "unknown evidence"),
    ),
)
def test_scope_model_mutations_fail_computed_relationship_validation(
    deviation_id, changes, message
):
    module = _module()
    bundle = module.build_upstream_llama_bundle(REPOSITORY_ROOT)
    deviations = list(module.build_upstream_deviation_rows(REPOSITORY_ROOT, bundle))
    index = next(i for i, row in enumerate(deviations) if row["deviation_id"] == deviation_id)
    deviations[index] = {**deviations[index], **changes}

    receipt = module.validate_upstream_relationships(bundle, deviations)

    assert receipt["valid"] is False
    assert any(message in error for error in receipt["errors"])


def test_scope_evidence_must_support_the_declared_test_or_setup_ids():
    module = _module()
    bundle = module.build_upstream_llama_bundle(REPOSITORY_ROOT)
    deviations = list(module.build_upstream_deviation_rows(REPOSITORY_ROOT, bundle))
    index = next(i for i, row in enumerate(deviations) if row["deviation_id"] == "UL-DEV-HIST-06")
    unrelated = next(
        evidence.evidence_id
        for evidence in bundle.evidence
        if "/UL-01/" in evidence.relative_path
    )
    deviations[index] = {**deviations[index], "evidence_ids": [unrelated]}

    receipt = module.validate_upstream_relationships(bundle, deviations)

    assert receipt["valid"] is False
    assert any("does not support scope UL-10" in error for error in receipt["errors"])
    assert any("does not support scope UL-12" in error for error in receipt["errors"])


def test_precedence_evidence_must_support_its_declared_test_scope():
    module = _module()
    bundle = module.build_upstream_llama_bundle(REPOSITORY_ROOT)
    deviations = list(module.build_upstream_deviation_rows(REPOSITORY_ROOT, bundle))
    index = next(i for i, row in enumerate(deviations) if row["deviation_id"] == "UL-DEV-UL13-PERFORMANCE")
    unrelated = next(
        evidence.evidence_id
        for evidence in bundle.evidence
        if "/UL-01/" in evidence.relative_path
    )
    deviations[index] = {**deviations[index], "evidence_ids": [unrelated]}

    receipt = module.validate_upstream_relationships(bundle, deviations)

    assert receipt["valid"] is False
    assert any("does not support scope UL-13" in error for error in receipt["errors"])


def test_quality_contract_is_hash_bound_and_fully_preserved():
    module = _module()
    bundle = module.build_upstream_llama_bundle(REPOSITORY_ROOT)
    evidence_by_role = {item.role: item for item in bundle.evidence}
    rubric = json.loads((REPOSITORY_ROOT / module.QUALITY_RUBRIC_RELATIVE).read_text(encoding="utf-8"))
    prompts = json.loads((REPOSITORY_ROOT / module.QUALITY_PROMPTS_RELATIVE).read_text(encoding="utf-8"))

    assert {record.rubric_id for record in bundle.quality} == {"GTQ-QUALITY-RUBRIC-v1"}
    assert {record.prompt_suite_id for record in bundle.quality} == {"GTQ-PROMPTS-v1"}
    assert rubric["rubric_id"] == "GTQ-QUALITY-RUBRIC-v1"
    assert prompts["prompt_set_id"] == "GTQ-PROMPTS-v1"
    assert [item["weight"] for item in rubric["dimensions"]] == [0.3, 0.25, 0.2, 0.15, 0.1]
    assert all(item["critical_cap"] for item in rubric["dimensions"])
    assert set(rubric["anchors"]) == {"0", "2", "4", "6", "8", "10"}
    assert len(rubric["procedure"]) == 6
    assert {item["prompt_id"] for item in prompts["prompts"]} == {"P1", "P2", "P3", "P4", "P5", "P6"}
    assert all(item["task"] and item["deterministic_checks"] for item in prompts["prompts"])
    assert prompts["generation_defaults"] == {
        "temperature": 0.0, "top_p": 1.0, "seed": 42, "max_output_tokens": 256
    }
    for role, relative in (
        ("quality-rubric", module.QUALITY_RUBRIC_RELATIVE),
        ("quality-prompts", module.QUALITY_PROMPTS_RELATIVE),
    ):
        assert evidence_by_role[role].relative_path == relative.as_posix()
        assert module.hash_file(REPOSITORY_ROOT / relative) == evidence_by_role[role].sha256


def test_comparator_claims_bind_all_eligible_rows_and_reproduction_is_executable():
    route = REPOSITORY_ROOT / "docs/testing/final-results/01-upstream-llama-cpp"
    claims = list(csv.DictReader((route / "evidence/claim-evidence-map.csv").open(encoding="utf-8-sig", newline="")))
    cpu = next(row for row in claims if row["claim_id"] == "UL-CLAIM-CPU")
    gpu = next(row for row in claims if row["claim_id"] == "UL-CLAIM-GPU")
    assert json.loads(cpu["comparator_test_ids"]) == [f"UL-{number:02d}" for number in range(1, 9)]
    assert json.loads(gpu["comparator_test_ids"]) == [f"UL-{number:02d}" for number in range(9, 14)]
    assert len(json.loads(cpu["evidence_ids"])) > 4
    assert len(json.loads(gpu["evidence_ids"])) > 4

    commands = (route / "reproduction/commands.md").read_text(encoding="utf-8")
    expected_order = (
        "1. Normalize and render",
        "2. Export the owned Word PDF",
        "3. Finalize and validate the PDF",
        "4. Validate the checksum manifest",
        "5. Run the focused validation suite",
    )
    assert all(label in commands for label in expected_order)
    assert [commands.index(label) for label in expected_order] == sorted(commands.index(label) for label in expected_order)
    assert commands.count("sys.path.insert(0,str(root))") == 3
    for path in (
        ".tools/python311-portable/python.exe",
        "scripts/testing/requirements.txt",
        "scripts/testing/cli/export_report.ps1",
        "scripts/testing/reporting/llama_adapter.py",
        "scripts/testing/tests/test_final_results_upstream_llama.py",
    ):
        assert path in commands or path in (route / "reproduction/dependencies.md").read_text(encoding="utf-8")
        if path != ".tools/python311-portable/python.exe":
            assert (REPOSITORY_ROOT / path).exists(), path

    rubric_text = (route / "quality/rubric.md").read_text(encoding="utf-8")
    calibration = (route / "quality/calibration.md").read_text(encoding="utf-8")
    prompt_rows = list(csv.DictReader((route / "quality/prompt-suite.csv").open(encoding="utf-8-sig", newline="")))
    assert "GTQ-QUALITY-RUBRIC-v1" in rubric_text
    assert all(name in rubric_text for name in (
        "correctness_and_grounding", "instruction_and_format_adherence",
        "completeness_and_fact_retention", "relevance_clarity_and_coherence",
        "stability_and_output_integrity",
    ))
    assert "Calibration: Not collected" in calibration
    assert "Score increments: Not collected" in calibration
    assert all(row["task_type"] and row["deterministic_checks_json"] and row["generation_settings_json"] for row in prompt_rows)


def test_documented_portable_python_is_available_when_installed():
    executable = REPOSITORY_ROOT / ".tools/python311-portable/python.exe"
    if not executable.is_file():
        pytest.skip("ignored portable Python dependency is not installed in this worktree")
    completed = subprocess.run(
        [str(executable), "--version"],
        capture_output=True,
        text=True,
        check=False,
    )
    assert completed.returncode == 0
    assert "Python 3.11.9" in completed.stdout + completed.stderr


def test_pdf_finalizer_uses_the_current_structurally_valid_page_count():
    receipt = _module().finalize_upstream_llama_route(REPOSITORY_ROOT)

    assert receipt["valid"] is True
    assert receipt["checks"]["page_count"] == 47
    assert receipt["inspected_pages"] == list(range(1, 48))
    assert receipt["visual_findings"]["dense_evidence_table"] == (
        "Evidence section begins on page 18; the evidence table spans pages 19-46 and is readable at page zoom"
    )
