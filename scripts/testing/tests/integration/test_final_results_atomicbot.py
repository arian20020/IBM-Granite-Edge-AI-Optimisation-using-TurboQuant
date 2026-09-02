from __future__ import annotations

import csv
import json
import shutil
import sys
from collections import Counter
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[4]))

from scripts.testing.reporting import llama_adapter
from scripts.testing.reporting.llama_adapter import (
    ATOMICBOT_EXPECTED_IDS,
    _atomicbot_relationship_receipt,
    audit_atomicbot_sources,
    build_atomicbot_bundle,
    finalize_atomicbot_route,
    write_atomicbot_route,
)
from scripts.testing.reporting.models import Status
from scripts.testing.reporting.openvino_report import SECTION_ORDER
from scripts.testing.reporting.csvio import validate_json


REPOSITORY_ROOT = Path(__file__).resolve().parents[4]
ROUTE = REPOSITORY_ROOT / "docs/testing/final-results/02-atomicbot-turboquant"


@pytest.fixture
def isolated_route(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    route = REPOSITORY_ROOT / ".pytest_cache" / "atomicbot" / tmp_path.name
    if route.exists():
        raise FileExistsError(route)
    shutil.copytree(ROUTE, route)
    monkeypatch.setattr(
        llama_adapter,
        "ATOMICBOT_ROUTE_RELATIVE",
        route.relative_to(REPOSITORY_ROOT),
    )
    try:
        yield route
    finally:
        shutil.rmtree(route)


def _copy_sources(tmp_path: Path) -> Path:
    root = tmp_path / "repo"
    for relative in (
        "docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md",
        "docs/testing/Workbook-Revision-Register.csv",
        "docs/testing/Test-Run-Register.csv",
        "docs/testing/Performance-Measurement-Register.csv",
        "docs/testing/Quality-Evaluation-Register.csv",
        "docs/testing/Failure-Register.csv",
        "docs/testing/Evidence-Index.csv",
        "docs/testing/cleanup/PATH-MIGRATION.csv",
        "experiments/raw-results/atomicbot-turboquant",
        "experiments/raw-results/retained/atomicbot-turboquant",
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
    assert all(record.rubric_id == "GTQ-QUALITY-RUBRIC-v1" for record in bundle.quality)
    assert all(record.prompt_suite_id == "GTQ-PROMPTS-v1" for record in bundle.quality)
    assert len({record.source_evidence_id for record in bundle.quality}) == 114


def test_quality_summary_prompt_hash_relationship_is_recomputed(tmp_path: Path):
    root = _copy_sources(tmp_path)
    prompt = root / "experiments/raw-results/atomicbot-turboquant/2026-07-17/quality-all-rows/AB-01/P1.json"
    payload = json.loads(prompt.read_text(encoding="utf-8"))
    payload["output_sha256"] = "0" * 64
    prompt.write_text(json.dumps(payload), encoding="utf-8")
    with pytest.raises(ValueError, match="quality prompt hash conflict"):
        build_atomicbot_bundle(root)


def test_quality_dimensions_caps_contract_register_and_means_reconcile(
    tmp_path: Path,
):
    root = _copy_sources(tmp_path)
    relative = Path("2026-07-17/quality-all-rows/AB-01/P1.json")
    legacy = root / "experiments/raw-results/atomicbot-turboquant" / relative
    retained = (
        root / "experiments/raw-results/retained/atomicbot-turboquant" / relative
    )
    legacy.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(retained, legacy)

    bundle = build_atomicbot_bundle(root)
    audit = bundle.repository["source_reconciliation"]
    contract = bundle.repository["quality_contract"]

    assert audit["quality_evaluation_rows_reconciled"] == 114
    assert audit["quality_adjudication_bindings_reconciled"] == 114
    assert audit["quality_weighted_scores_recomputed"] == 114
    assert audit["quality_means_recomputed"] == 19
    assert audit["quality_authority_hashes_authenticated"] == 3
    assert all(
        row["relative_path"].startswith(
            "experiments/raw-results/retained/atomicbot-turboquant/"
        )
        for row in bundle.repository["quality_adjudications"]
    )
    assert contract["prompt_set_id"] == "GTQ-PROMPTS-v1"
    assert contract["rubric_id"] == "GTQ-QUALITY-RUBRIC-v1"
    assert contract["dimension_weights"] == {
        "correctness_and_grounding": 0.30,
        "instruction_and_format_adherence": 0.25,
        "completeness_and_fact_retention": 0.20,
        "relevance_clarity_and_coherence": 0.15,
        "stability_and_output_integrity": 0.10,
    }
    assert len(contract["prompts"]) == 6
    assert all(row["task"] and row["deterministic_checks"] for row in contract["prompts"])
    assert contract["generation_settings"] == {
        "temperature": 0.0, "top_p": 1.0, "seed": 42, "max_output_tokens": 256
    }


@pytest.mark.parametrize(
    ("source", "mutator", "message"),
    (
        ("quality-summary", lambda d: d["rows"][0]["prompts"]["P1"].update(score=8.0), "quality score conflict"),
        ("adjudications", lambda d: d[next(iter(d))]["dimensions"].update(correctness_and_grounding=8), "weighted quality score conflict"),
        ("adjudications", lambda d: d[next(iter(d))].update(critical_caps=[4]), "critical cap conflict"),
        ("prompt-contract", lambda d: d["prompts"][0]["deterministic_checks"].update(maximum_words=88), "prompt contract hash conflict"),
        ("rubric-contract", lambda d: d["dimensions"][0].update(weight=0.29), "quality rubric hash conflict"),
    ),
)
def test_quality_source_mutations_are_rejected(tmp_path: Path, source, mutator, message):
    root = _copy_sources(tmp_path)
    paths = {
        "quality-summary": root / "experiments/raw-results/atomicbot-turboquant/2026-07-17/quality-all-rows/quality-summary.json",
        "adjudications": root / "experiments/raw-results/atomicbot-turboquant/2026-07-17/quality-all-rows/quality-adjudications.json",
        "prompt-contract": root / "experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json",
        "rubric-contract": root / "experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json",
    }
    path = paths[source]
    payload = json.loads(path.read_text(encoding="utf-8")); mutator(payload)
    path.write_text(json.dumps(payload), encoding="utf-8")
    with pytest.raises(ValueError, match=message):
        build_atomicbot_bundle(root)


def test_quality_register_mutation_is_rejected(tmp_path: Path):
    root = _copy_sources(tmp_path)
    path = root / "docs/testing/Quality-Evaluation-Register.csv"
    rows = list(csv.DictReader(path.open(encoding="utf-8-sig", newline="")))
    row = next(item for item in rows if item["Route"] == "atomicbot-turboquant")
    row["Weighted_Score_0_to_10"] = "8.00"
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=rows[0].keys(), lineterminator="\n")
        writer.writeheader(); writer.writerows(rows)
    with pytest.raises(ValueError, match="quality register score conflict"):
        build_atomicbot_bundle(root)


def test_synchronized_quality_score_and_mean_mutation_is_still_rejected(tmp_path: Path):
    root = _copy_sources(tmp_path)
    path = root / "experiments/raw-results/atomicbot-turboquant/2026-07-17/quality-all-rows/quality-summary.json"
    payload = json.loads(path.read_text(encoding="utf-8"))
    payload["rows"][0]["prompts"]["P1"]["score"] = 8.0
    payload["rows"][0]["quality_mean"] = float(payload["rows"][0]["quality_mean"]) - (1 / 6)
    path.write_text(json.dumps(payload), encoding="utf-8")
    with pytest.raises(ValueError, match="quality score conflict"):
        build_atomicbot_bundle(root)


def test_coordinated_quality_authority_tampering_is_rejected_independently(tmp_path: Path):
    root = _copy_sources(tmp_path)
    quality_path = root / "experiments/raw-results/atomicbot-turboquant/2026-07-17/quality-all-rows/quality-summary.json"
    quality = json.loads(quality_path.read_text(encoding="utf-8"))
    response_hash = quality["rows"][0]["prompts"]["P1"]["response_sha256"]
    affected_tests = set()
    for summary_row in quality["rows"]:
        prompt = summary_row["prompts"]["P1"]
        if prompt["response_sha256"] == response_hash:
            old_score = float(prompt["score"])
            prompt["score"] = 8.0
            summary_row["quality_mean"] = float(summary_row["quality_mean"]) + ((8.0 - old_score) / 6)
            affected_tests.add(summary_row["test_id"])
    quality_path.write_text(json.dumps(quality), encoding="utf-8")

    adjudication_path = root / "experiments/raw-results/atomicbot-turboquant/2026-07-17/quality-all-rows/quality-adjudications.json"
    adjudications = json.loads(adjudication_path.read_text(encoding="utf-8"))
    adjudications[f"P1:{response_hash}"]["dimensions"] = {
        "correctness_and_grounding": 8,
        "instruction_and_format_adherence": 8,
        "completeness_and_fact_retention": 8,
        "relevance_clarity_and_coherence": 8,
        "stability_and_output_integrity": 8,
    }
    adjudication_path.write_text(json.dumps(adjudications), encoding="utf-8")

    register_path = root / "docs/testing/Quality-Evaluation-Register.csv"
    rows = list(csv.DictReader(register_path.open(encoding="utf-8-sig", newline="")))
    for row in rows:
        if row["Route"] != "atomicbot-turboquant" or row["Test_ID"] not in affected_tests or row["Prompt_ID"] != "P1":
            continue
        for field in (
            "Correctness_and_Grounding_0_to_10", "Instruction_and_Format_0_to_10",
            "Completeness_and_Fact_Retention_0_to_10", "Relevance_Clarity_Coherence_0_to_10",
            "Stability_and_Integrity_0_to_10",
        ):
            row[field] = "8"
        row["Weighted_Score_0_to_10"] = "8.00"
    with register_path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=rows[0].keys(), lineterminator="\n")
        writer.writeheader(); writer.writerows(rows)

    with pytest.raises(ValueError, match="quality (summary|adjudication|register) authority hash conflict"):
        build_atomicbot_bundle(root)


@pytest.mark.parametrize(
    ("source", "mutator", "message"),
    (
        ("register", lambda rows: rows[0].update(TTFT_ms="999.0"), "performance register field conflict"),
        ("measurement", lambda data: data.update(ttft_ms=999.0), "performance register field conflict"),
        ("server-summary", lambda data: data["aggregate"]["ttft_ms"].update(median=999.0), "current server summary aggregate conflict"),
        ("formal-summary", lambda data: data.update(median_tokens_per_second=999.0), "formal throughput aggregate conflict"),
        ("server-identity", lambda data: data.update(test_id="AB-UNKNOWN"), "current server summary identity conflict"),
    ),
)
def test_performance_field_source_identity_and_aggregate_mutations_are_rejected(tmp_path: Path, source, mutator, message):
    root = _copy_sources(tmp_path)
    register_path = root / "docs/testing/Performance-Measurement-Register.csv"
    rows = list(csv.DictReader(register_path.open(encoding="utf-8-sig", newline="")))
    route_rows = [row for row in rows if row["Route"] == "atomicbot-turboquant"]
    if source == "register":
        mutator(route_rows)
        with register_path.open("w", encoding="utf-8-sig", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=rows[0].keys(), lineterminator="\n")
            writer.writeheader(); writer.writerows(rows)
    else:
        paths = {
            "measurement": Path(route_rows[0]["Raw_Metrics_Path"]).parent / "measurement.json",
            "server-summary": Path(route_rows[0]["Processed_Result_Path"]),
            "server-identity": Path(route_rows[0]["Processed_Result_Path"]),
            "formal-summary": Path("experiments/raw-results/atomicbot-turboquant/2026-07-16/acquisition/metrics/AB-01/AB-01-formal-summary.json"),
        }
        path = root / paths[source]
        payload = json.loads(path.read_text(encoding="utf-8")); mutator(payload)
        path.write_text(json.dumps(payload), encoding="utf-8")
    with pytest.raises(ValueError, match=message):
        build_atomicbot_bundle(root)


def test_performance_sources_and_field_evidence_reconcile():
    bundle = build_atomicbot_bundle(REPOSITORY_ROOT)
    audit = bundle.repository["source_reconciliation"]
    mappings = bundle.repository["measurement_field_evidence"]

    assert len(bundle.measurements) == 76
    assert audit["performance_register_rows_reconciled"] == 57
    assert audit["current_server_summaries_reconciled"] == 19
    assert audit["formal_throughput_sources_reconciled"] == 19
    assert audit["master_summary_rows_reconciled"] == 19
    assert len(mappings) == 76
    assert all(row["evidence_ids"] and row["supported_fields"] for row in mappings)
    resource = [row for row in mappings if row["measurement_kind"] == "resource-utilization"]
    throughput = [row for row in mappings if row["measurement_kind"] == "formal-throughput"]
    assert len(resource) == 57 and len(throughput) == 19
    assert all("generation_tokens_per_second" not in row["supported_fields"] for row in resource)
    assert all(row["supported_fields"] == ["generation_tokens_per_second"] for row in throughput)


def test_workbook_overwritten_quality_status_pattern_is_explicit_and_excluded():
    bundle = build_atomicbot_bundle(REPOSITORY_ROOT)
    audit = bundle.repository["source_reconciliation"]
    assert audit["workbook_overwritten_result_rows"] == 19
    assert audit["workbook_duplicate_pairs_value_reconciled"] == 19
    assert audit["workbook_excluded_columns"] == ["Quality /10", "Status"]
    assert audit["workbook_precedence"] == (
        "Test-Run register for runtime status; Performance register/current summaries for performance and utilization; "
        "Quality-Evaluation register/current quality artifacts for quality"
    )


def test_workbook_corruption_pattern_mutation_is_rejected(tmp_path: Path):
    root = _copy_sources(tmp_path)
    path = root / "docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md"
    text = path.read_text(encoding="utf-8")
    path.write_text(text.replace("| 64.51 / 64.09 / 66.41 | 0.00 / 0.00 / 0.00 | 64.51 / 64.09 / 66.41 | 0.00 / 0.00 / 0.00 |", "| 64.51 / 64.09 / 66.41 | 0.00 / 0.00 / 0.00 | 6.22 | Pass |", 1), encoding="utf-8")
    with pytest.raises(ValueError, match="overwritten Quality/Status pattern"):
        build_atomicbot_bundle(root)


def test_coherent_fabricated_workbook_duplicate_values_are_rejected(tmp_path: Path):
    root = _copy_sources(tmp_path)
    path = root / "docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md"
    text = path.read_text(encoding="utf-8")
    original = "| 64.51 / 64.09 / 66.41 | 0.00 / 0.00 / 0.00 | 64.51 / 64.09 / 66.41 | 0.00 / 0.00 / 0.00 |"
    fabricated = "| 99.99 / 99.99 / 99.99 | 88.88 / 88.88 / 88.88 | 99.99 / 99.99 / 99.99 | 88.88 / 88.88 / 88.88 |"
    assert original in text
    path.write_text(text.replace(original, fabricated, 1), encoding="utf-8")
    with pytest.raises(ValueError, match="overwritten Quality/Status value conflict"):
        build_atomicbot_bundle(root)


def test_all_five_deviations_have_valid_scopes_and_evidence():
    bundle = build_atomicbot_bundle(REPOSITORY_ROOT)
    rows = bundle.repository["deviation_rows"]
    assert len(rows) == 5
    assert {row["scope_type"] for row in rows} == {"setup", "test", "multi-test", "mixed"}
    assert all(row["nonterminal"] is True and row["scope_ids"] and row["evidence_ids"] for row in rows)
    evidence = {item.evidence_id for item in bundle.evidence}
    assert all(set(row["evidence_ids"]) <= evidence for row in rows)


def test_deviation_scope_mutation_is_rejected(tmp_path: Path):
    root = _copy_sources(tmp_path)
    path = root / "docs/testing/Failure-Register.csv"
    rows = list(csv.DictReader(path.open(encoding="utf-8-sig", newline="")))
    row = next(item for item in rows if item["Failure_ID"] == "FAIL-AB-8B-SAFETY")
    row["Test_ID"] = "AB-KV8-F16-4K"
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=rows[0].keys(), lineterminator="\n")
        writer.writeheader(); writer.writerows(rows)
    with pytest.raises(ValueError, match="deviation scope conflict"):
        build_atomicbot_bundle(root)


def test_stale_index_inventory_and_duplicate_mutations_are_rejected(tmp_path: Path):
    root = _copy_sources(tmp_path)
    path = root / "docs/testing/Evidence-Index.csv"
    rows = list(csv.DictReader(path.open(encoding="utf-8-sig", newline="")))
    route_rows = [row for row in rows if row["Route"] == "atomicbot-turboquant"]
    missing = next(row for row in route_rows if not (root / row["Repository_Path"].replace("\\", "/")).is_file())
    rows.remove(missing)
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=rows[0].keys(), lineterminator="\n")
        writer.writeheader(); writer.writerows(rows)
    with pytest.raises(ValueError, match="stale index inventory conflict"):
        build_atomicbot_bundle(root)

    root = _copy_sources(tmp_path / "duplicate")
    path = root / "docs/testing/Evidence-Index.csv"
    rows = list(csv.DictReader(path.open(encoding="utf-8-sig", newline="")))
    duplicate = next(row for row in rows if row["Evidence_ID"] == "EVID-e3b0c44298fc1c149afb")
    duplicate["Evidence_ID"] = "EVID-mutated-duplicate"
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=rows[0].keys(), lineterminator="\n")
        writer.writeheader(); writer.writerows(rows)
    with pytest.raises(ValueError, match="duplicate Evidence_ID conflict"):
        build_atomicbot_bundle(root)


def test_stale_index_audit_is_exact_and_portable_outputs_contain_no_absolute_paths(
    isolated_route: Path,
):
    bundle = write_atomicbot_route(REPOSITORY_ROOT)
    audit = bundle.repository["source_reconciliation"]
    assert audit["index_row_count"] == 828
    assert audit["stale_index_missing_paths"] == 38
    assert audit["stale_index_hash_conflicts"] == 263
    assert audit["duplicate_evidence_id"] == "EVID-e3b0c44298fc1c149afb"
    assert audit["duplicate_evidence_id_paths"] == 5
    assert audit["duplicate_evidence_id_extra_rows"] == 4

    forbidden = ("C:\\Users\\", "C:/Users/", "\\\\?\\", str(REPOSITORY_ROOT))
    for path in isolated_route.rglob("*"):
        if path.is_file() and path.suffix.lower() not in {".docx", ".pdf"}:
            text = path.read_text(encoding="utf-8", errors="ignore")
            assert not any(value in text for value in forbidden), path


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


def test_route_generation_has_common_structure_schema_parity_and_honest_caveats(
    isolated_route: Path,
):
    bundle = write_atomicbot_route(REPOSITORY_ROOT)
    markdown = isolated_route / "reports/atomicbot-turboquant-report.md"
    text = markdown.read_text(encoding="utf-8")

    assert len(bundle.attempts) == 19
    assert all(heading in text for heading in SECTION_ORDER)
    assert "limited/provisional" in text
    assert "not directly comparable with OpenVINO" in text
    assert "Not collected" in text
    validation = json.loads(
        (isolated_route / "validation/validation.json").read_text()
    )
    assert validation["checks"]["workbook_parity"]["matches"] is True
    assert validation["checks"]["relationship"]["valid"] is True
    relationship = validation["checks"]["relationship"]
    assert relationship["deviation_count"] == 5
    assert relationship["deviation_relationships_valid"] is True
    assert set(relationship["deviation_ids"]) == {
        "FAIL-AB-UI-ASSET", "FAIL-AB-DEVICE-GUARD", "FAIL-AB-08Q-MEMORY",
        "FAIL-AB-8B-SAFETY", "FAIL-AB-P5-TIMEOUT",
    }
    assert validation["checks"]["coverage"]["valid"] is True
    assert validation["checks"]["data"]["valid"] is True


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


@pytest.mark.parametrize(
    ("mutator", "expected_error"),
    (
        (lambda row: row.update(deviation_id="FAIL-FABRICATED"), "deviation ID set"),
        (lambda row: row.update(scope_type="test"), "deviation scope"),
        (lambda row: row.update(scope_ids=["AB-01"]), "deviation scope"),
        (lambda row: row.update(nonterminal=False), "deviation terminal flag"),
        (lambda row: row.update(evidence_ids=["EVID-MISSING"]), "deviation evidence"),
    ),
)
def test_relationship_receipt_rejects_deviation_mutations(mutator, expected_error):
    bundle = build_atomicbot_bundle(REPOSITORY_ROOT)
    mutator(bundle.repository["deviation_rows"][0])
    receipt = _atomicbot_relationship_receipt(bundle)
    assert receipt["valid"] is False
    assert any(expected_error in error for error in receipt["errors"])


def test_generated_inventory_manifest_and_reproduction_contract():
    expected = (
        "README.md", "data/route.json", "reproduction/protocol/intended-test-matrix.csv",
        "reproduction/system/repository.json", "data/attempts.csv", "data/measurements.csv",
        "data/summaries.csv", "data/quality.csv", "reproduction/quality/rubric.md",
        "data/failures.csv", "evidence/evidence-index.csv",
        "evidence/claim-evidence-map.csv", "reproduction/commands.md",
        "validation/validation.json", "validation/validation.md",
        "reports/atomicbot-turboquant-report.md",
        "reports/atomicbot-turboquant-report.docx",
    )
    for relative in expected:
        assert (ROUTE / relative).is_file(), relative
    commands = (ROUTE / "reproduction/commands.md").read_text(encoding="utf-8")
    assert "python -m scripts.testing.cli.validate_results --route atomicbot" in commands
    assert "do not rerun" in commands.casefold()
    assert "do not modify evidence" in commands.casefold()
    assert "scripts.testing.reporting" not in commands
    assert "export_report.ps1" not in commands
    assert "powershell.exe" not in commands
    manifest_lines = (ROUTE / "evidence/manifest-sha256.txt").read_text(encoding="utf-8").splitlines()
    files = [path for path in ROUTE.rglob("*") if path.is_file()]
    assert len(manifest_lines) == len(files) - 1


def test_pdf_finalizer_uses_actual_structural_page_count(isolated_route: Path):
    receipt = finalize_atomicbot_route(REPOSITORY_ROOT)
    assert receipt["valid"] is True
    assert receipt["checks"]["page_count"] >= 10
    assert receipt["inspected_pages"] == list(range(1, receipt["checks"]["page_count"] + 1))
