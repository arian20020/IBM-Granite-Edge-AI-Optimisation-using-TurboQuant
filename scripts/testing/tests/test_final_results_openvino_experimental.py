import csv
import hashlib
import json
import os
import shutil
import sys
from collections import Counter, defaultdict
from dataclasses import replace
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPO_ROOT))

from scripts.testing.final_results.csvio import validate_json
from scripts.testing.final_results.evidence import validate_sha256_manifest
from scripts.testing.final_results.models import Status
from scripts.testing.final_results.openvino_adapter import (
    build_experimental_bundle,
    build_experimental_validation_receipts,
    write_experimental_route,
)


FV6 = REPO_ROOT / "experiments/raw-results/openvino-experimental-fork/2026-08-30/fv6"
ROUTE = REPO_ROOT / "docs/testing/final-results/04-openvino-experimental-fork"
SOURCE_WORKBOOK = (
    REPO_ROOT
    / "outputs/openvino-experimental-fork-results"
    / "Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx"
)


def _rows(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _write_rows(path: Path, rows: list[dict[str, str]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def _isolated_fv6_repo(tmp_path: Path) -> Path:
    repo = tmp_path / "repo"
    destination = repo / FV6.relative_to(REPO_ROOT)
    shutil.copytree(FV6, destination)
    workbook = repo / SOURCE_WORKBOOK.relative_to(REPO_ROOT)
    workbook.parent.mkdir(parents=True)
    shutil.copyfile(SOURCE_WORKBOOK, workbook)
    return repo


def _rewrite_raw(repo: Path, case_id: str, mutation) -> None:
    fv6 = repo / FV6.relative_to(REPO_ROOT)
    raw_path = fv6 / "raw" / f"{case_id}.json"
    payload = json.loads(raw_path.read_text(encoding="utf-8"))
    mutation(payload)
    raw_path.write_text(
        json.dumps(payload, ensure_ascii=False, separators=(",", ":")),
        encoding="utf-8",
    )
    digest = _sha256(raw_path)

    detailed_path = fv6 / "experimental-openvino-detailed-results.csv"
    detailed = _rows(detailed_path)
    next(row for row in detailed if row["case_id"] == case_id)["raw_result_sha256"] = digest
    _write_rows(detailed_path, detailed)

    rows_path = fv6 / "rows.json"
    rows_payload = json.loads(rows_path.read_text(encoding="utf-8"))
    next(row for row in rows_payload["rows"] if row["case_id"] == case_id)[
        "raw_result_sha256"
    ] = digest
    rows_path.write_text(
        json.dumps(rows_payload, ensure_ascii=False, separators=(",", ":")),
        encoding="utf-8",
    )


def test_fv6_normalization_preserves_complete_campaign_without_fabricating_unavailable_results():
    # A wrong source version, incomplete matrix, collapsed status, or invented
    # unavailable metric must break this end-to-end route contract.
    source_rows = _rows(FV6 / "experimental-openvino-detailed-results.csv")
    assert _sha256(FV6 / "experimental-openvino-detailed-results.csv") == (
        "2f57390980664a79cdcc2272152fd316cd315360afd5ab3b715bba9fd70ebc9f"
    )
    assert _sha256(SOURCE_WORKBOOK) == (
        "09820a5b19edb11efd7640f1d9a13802b82fb464ab6e14622998d2565c5aca83"
    )

    bundle = build_experimental_bundle(REPO_ROOT)

    assert bundle.route_id == "openvino-experimental-fork"
    assert bundle.campaign_id == "fv6-2026-08-30"
    assert len(bundle.attempts) == 81
    assert Counter((attempt.status, attempt.executed) for attempt in bundle.attempts) == {
        (Status.PASSED, True): 27,
        (Status.ARTIFACT_UNAVAILABLE, False): 54,
    }
    assert {attempt.cache_format_id for attempt in bundle.attempts} == {
        "f16",
        "polar3",
        "polar4",
        "tbq3",
        "tbq3_qjl",
        "tbq4",
        "tbq4_qjl",
        "u4",
        "u8",
    }
    assert {
        (attempt.model_id, attempt.weight_format_id)
        for attempt in bundle.attempts
        if attempt.executed
    } == {
        ("granite-3b", "int4"),
        ("granite-3b", "int8"),
        ("granite-8b", "int4"),
    }

    attempts_by_case = {attempt.test_case_id: attempt for attempt in bundle.attempts}
    assert set(attempts_by_case) == {row["case_id"] for row in source_rows}
    for row in source_rows:
        attempt = attempts_by_case[row["case_id"]]
        assert attempt.source_status == row["status"]
        assert attempt.executed is (row["executed"] == "true")
        assert attempt.reason == row["failure_reason"]
        assert attempt.model_id == row["model"]
        assert attempt.weight_format_id == row["weight_precision"]
        assert attempt.cache_format_id == row["cache_codec"]

    passed_cases = {
        attempt.test_case_id for attempt in bundle.attempts if attempt.executed
    }
    unavailable_cases = set(attempts_by_case) - passed_cases
    assert len(bundle.measurements) == 27 * 3
    assert {item.test_case_id for item in bundle.measurements} == passed_cases
    assert {item.test_case_id for item in bundle.summaries} == passed_cases
    assert all(len(item.source_measurement_ids) == 3 for item in bundle.summaries)
    assert {item.test_case_id for item in bundle.quality} == passed_cases
    assert len(bundle.failures) == 54
    assert {item.test_case_id for item in bundle.failures} == unavailable_cases
    assert all(item.status is Status.ARTIFACT_UNAVAILABLE for item in bundle.failures)

    prompt_ids_by_case: dict[str, set[str | None]] = defaultdict(set)
    criteria_by_prompt: Counter[tuple[str, str | None]] = Counter()
    for score in bundle.quality:
        prompt_ids_by_case[score.test_case_id].add(score.prompt_id)
        criteria_by_prompt[(score.test_case_id, score.prompt_id)] += 1
    assert len(bundle.quality) == 27 * 48 * 3
    assert all(len(prompt_ids) == 48 for prompt_ids in prompt_ids_by_case.values())
    assert set(criteria_by_prompt.values()) == {3}

    evidence_by_path = {record.relative_path: record for record in bundle.evidence}
    for row in source_rows:
        if row["executed"] != "true":
            assert row["raw_result_path"] == ""
            assert row["raw_result_sha256"] == ""
            continue
        raw_relative = row["raw_result_path"].replace("\\", "/")
        raw_record = evidence_by_path[raw_relative]
        assert raw_record.sha256 == row["raw_result_sha256"]
        assert raw_record.sha256 == _sha256(REPO_ROOT / raw_relative)
        assert raw_record.evidence_id in attempts_by_case[row["case_id"]].evidence_ids

    output_bundle = write_experimental_route(REPO_ROOT)
    assert output_bundle == bundle
    required_files = {
        "route-manifest.json",
        "protocol/intended-test-matrix.csv",
        "system/repository.json",
        "system/hardware.json",
        "system/software.json",
        "system/model-artifacts.csv",
        "results/attempts.csv",
        "results/measurements.csv",
        "results/summary-results.csv",
        "results/availability-matrix.csv",
        "results/source/Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx",
        "quality/prompt-suite.csv",
        "quality/scores.csv",
        "quality/outputs-index.csv",
        "reproduction/README.md",
        "failures/failure-register.csv",
        "evidence/evidence-index.csv",
        "evidence/source-locations.csv",
        "evidence/claim-evidence-map.csv",
        "evidence/manifest-sha256.txt",
        "validation/coverage-validation.json",
        "validation/data-validation.json",
        "validation/integrity-validation.json",
    }
    assert required_files <= {
        path.relative_to(ROUTE).as_posix()
        for path in ROUTE.rglob("*")
        if path.is_file()
    }

    copied_workbook = (
        ROUTE
        / "results/source"
        / "Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx"
    )
    assert copied_workbook.read_bytes() == SOURCE_WORKBOOK.read_bytes()
    assert validate_sha256_manifest(
        REPO_ROOT, ROUTE / "evidence/manifest-sha256.txt"
    ) == []

    schemas = REPO_ROOT / "docs/testing/final-results/standards/schemas"
    canonical_outputs = (
        ("results/attempts.csv", "attempts.schema.json"),
        ("results/measurements.csv", "measurements.schema.json"),
        ("results/summary-results.csv", "results.schema.json"),
        ("quality/scores.csv", "quality.schema.json"),
        ("failures/failure-register.csv", "failures.schema.json"),
        ("evidence/evidence-index.csv", "evidence.schema.json"),
    )
    for relative_path, schema_name in canonical_outputs:
        for index, row in enumerate(_rows(ROUTE / relative_path), start=2):
            # CSV encodes lists and nullable scalars portably; validation uses
            # the corresponding typed canonical records below.
            assert row, (relative_path, index)
    assert validate_json(bundle.to_row(), schemas / "route-manifest.schema.json") == []
    for records, schema_name in (
        (bundle.attempts, "attempts.schema.json"),
        (bundle.measurements, "measurements.schema.json"),
        (bundle.summaries, "results.schema.json"),
        (bundle.quality, "quality.schema.json"),
        (bundle.failures, "failures.schema.json"),
        (bundle.evidence, "evidence.schema.json"),
    ):
        assert all(validate_json(record.to_row(), schemas / schema_name) == [] for record in records)

    generated_attempts = _rows(ROUTE / "results/attempts.csv")
    generated_measurements = _rows(ROUTE / "results/measurements.csv")
    generated_scores = _rows(ROUTE / "quality/scores.csv")
    generated_outputs = _rows(ROUTE / "quality/outputs-index.csv")
    generated_evidence = _rows(ROUTE / "evidence/evidence-index.csv")
    generated_availability = _rows(ROUTE / "results/availability-matrix.csv")
    unavailable_output_rows = [
        row for row in generated_availability if row["test_case_id"] in unavailable_cases
    ]
    assert len(generated_attempts) == 81
    assert len(unavailable_output_rows) == 54
    assert all(row["status"] == "artifact_unavailable" for row in unavailable_output_rows)
    forbidden_metric_columns = {
        "decode_tps",
        "ttft_ms",
        "quality_score",
        "peak_working_set_bytes",
    }
    assert forbidden_metric_columns.isdisjoint(generated_availability[0])

    assert all(isinstance(json.loads(row["evidence_ids"]), list) for row in generated_attempts)
    assert all(
        isinstance(json.loads(row["input_evidence_ids"]), list)
        for row in generated_evidence
    )
    measurement_by_id = {row["measurement_id"]: row for row in generated_measurements}
    output_by_key = {
        (row["test_case_id"], row["prompt_id"]): row for row in generated_outputs
    }
    assert len({row["output_id"] for row in generated_outputs}) == 27 * 48
    score_by_id = {row["quality_id"]: row for row in generated_scores}
    for source in source_rows:
        if source["executed"] != "true":
            continue
        raw = json.loads((REPO_ROOT / source["raw_result_path"].replace("\\", "/")).read_text())
        for repetition, run in enumerate(raw["benchmark_runs"], start=1):
            measurement = measurement_by_id[
                f"{source['case_id']}--benchmark-repetition-{repetition:03d}"
            ]
            assert float(measurement["latency_ms"]) == float(run["result"]["ttft_ms"])
            assert float(measurement["generation_tokens_per_second"]) == float(
                run["result"]["decode_tps"]
            )
            assert int(measurement["peak_working_set_bytes"]) == round(
                float(run["peak_working_set_mb"]) * 1_000_000
            )
            assert int(measurement["input_tokens"]) == int(run["result"]["input_tokens"])
            assert int(measurement["output_tokens"]) == int(run["result"]["generated_tokens"])
        for run in raw["quality_runs"]:
            output = output_by_key[(source["case_id"], run["prompt_id"])]
            assert output["output_sha256"] == hashlib.sha256(
                run["result"]["text"].encode("utf-8")
            ).hexdigest()
            assert float(output["prompt_score"]) == float(run["quality"]["score"])
            for criterion in run["quality"]["criteria"]:
                quality_id = (
                    f"{source['case_id']}--{run['prompt_id']}--"
                    f"{criterion['category']}--{criterion['id']}"
                )
                score = score_by_id[quality_id]
                assert float(score["score"]) == float(criterion["points_awarded"])
                assert float(score["maximum_score"]) == float(criterion["weight"])

    coverage = json.loads((ROUTE / "validation/coverage-validation.json").read_text(encoding="utf-8"))
    assert coverage["valid"] is True
    assert set(coverage["checks"]) == {
        "artifact_unavailable_count",
        "artifact_unavailable_cases",
        "cache_format_count",
        "cache_format_set",
        "executed_count",
        "executed_model_weight_artifact_count",
        "executed_model_weight_artifact_set",
        "passed_count",
        "planned_count",
        "quality_prompts_per_passed_case",
    }
    assert all(check["passed"] is True for check in coverage["checks"].values())
    assert coverage["checks"]["planned_count"] == {
        "actual": 81,
        "expected": 81,
        "passed": True,
    }

    data_validation = json.loads(
        (ROUTE / "validation/data-validation.json").read_text(encoding="utf-8")
    )
    assert data_validation["valid"] is True
    assert set(data_validation["checks"]) == {
        "attempt_identifier_uniqueness",
        "attempt_identifier_bindings",
        "attempt_source_evidence_references",
        "artifact_unavailable_semantics",
        "evidence_identifier_bindings",
        "evidence_identifier_uniqueness",
        "evidence_references",
        "exactly_three_distinct_criteria_per_prompt",
        "failure_count",
        "failure_identifier_bindings",
        "failure_identifier_uniqueness",
        "failure_references",
        "measurement_count",
        "measurement_identifier_bindings",
        "measurement_identifier_uniqueness",
        "measurement_references",
        "output_count",
        "output_identifier_bindings",
        "output_identifier_uniqueness",
        "output_references",
        "output_source_bindings",
        "prompt_count",
        "prompt_identifier_bindings",
        "prompt_identifier_uniqueness",
        "prompt_references",
        "prompt_source_bindings",
        "quality_criterion_count",
        "quality_identifier_bindings",
        "quality_identifier_uniqueness",
        "quality_prompt_bindings",
        "quality_references",
        "summary_count",
        "summary_identifier_bindings",
        "summary_identifier_uniqueness",
        "summary_lineage_and_values",
        "summary_references",
        "unavailable_exclusions",
    }
    assert all(check["passed"] is True for check in data_validation["checks"].values())
    reproduction = (ROUTE / "reproduction/README.md").read_text(encoding="utf-8")
    assert "write_experimental_route" in reproduction
    assert "2026-08-30/fv6" in reproduction
    assert "Do not" in reproduction and "zero" in reproduction


def test_validation_receipts_fail_when_expected_measurements_are_missing():
    bundle = build_experimental_bundle(REPO_ROOT)
    incomplete = replace(bundle, measurements=())

    coverage, data = build_experimental_validation_receipts(REPO_ROOT, incomplete)

    assert coverage["valid"] is True
    assert data["valid"] is False
    assert data["checks"]["measurement_count"] == {
        "actual": 0,
        "expected": 81,
        "passed": False,
    }


def test_validation_receipts_require_exact_unavailable_status_and_execution_flag():
    bundle = build_experimental_bundle(REPO_ROOT)
    attempts = list(bundle.attempts)
    index = next(
        index
        for index, attempt in enumerate(attempts)
        if attempt.status is Status.ARTIFACT_UNAVAILABLE
    )
    attempts[index] = replace(attempts[index], status=Status.NOT_EXECUTED)

    coverage, data = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, attempts=tuple(attempts))
    )

    assert coverage["valid"] is False
    assert coverage["checks"]["artifact_unavailable_cases"]["passed"] is False
    assert data["checks"]["artifact_unavailable_semantics"]["passed"] is False


def test_validation_receipts_reject_count_preserving_unsupported_cache_format():
    bundle = build_experimental_bundle(REPO_ROOT)
    attempts = tuple(
        replace(attempt, cache_format_id="unsupported-format")
        if attempt.cache_format_id == "f16"
        else attempt
        for attempt in bundle.attempts
    )

    coverage, _ = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, attempts=attempts)
    )

    assert coverage["checks"]["cache_format_count"]["passed"] is True
    assert coverage["checks"]["cache_format_set"]["passed"] is False
    assert coverage["valid"] is False


def test_validation_receipts_reject_count_preserving_wrong_executed_artifact():
    bundle = build_experimental_bundle(REPO_ROOT)
    attempts = tuple(
        replace(attempt, model_id="unsupported-model")
        if attempt.executed
        and (attempt.model_id, attempt.weight_format_id) == ("granite-8b", "int4")
        else attempt
        for attempt in bundle.attempts
    )

    coverage, _ = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, attempts=attempts)
    )

    assert coverage["checks"]["executed_model_weight_artifact_count"]["passed"] is True
    assert coverage["checks"]["executed_model_weight_artifact_set"]["passed"] is False
    assert coverage["valid"] is False


@pytest.mark.parametrize(
    ("collection_name", "identifier_field", "check_name"),
    (
        ("attempts", "attempt_id", "attempt_identifier_uniqueness"),
        ("measurements", "measurement_id", "measurement_identifier_uniqueness"),
        ("summaries", "summary_id", "summary_identifier_uniqueness"),
        ("quality", "quality_id", "quality_identifier_uniqueness"),
        ("failures", "failure_id", "failure_identifier_uniqueness"),
        ("evidence", "evidence_id", "evidence_identifier_uniqueness"),
    ),
)
def test_validation_receipts_reject_duplicate_stable_record_ids(
    collection_name, identifier_field, check_name
):
    bundle = build_experimental_bundle(REPO_ROOT)
    records = list(getattr(bundle, collection_name))
    records[1] = replace(
        records[1], **{identifier_field: getattr(records[0], identifier_field)}
    )

    _, data = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, **{collection_name: tuple(records)})
    )

    assert data["checks"][check_name]["passed"] is False
    assert data["valid"] is False


def test_validation_receipts_reject_duplicate_output_ids():
    bundle = build_experimental_bundle(REPO_ROOT)
    prompts = _rows(ROUTE / "quality/prompt-suite.csv")
    outputs = _rows(ROUTE / "quality/outputs-index.csv")
    outputs[1]["output_id"] = outputs[0]["output_id"]

    _, data = build_experimental_validation_receipts(
        REPO_ROOT,
        bundle,
        prompt_rows=prompts,
        output_rows=outputs,
    )

    assert data["checks"]["output_identifier_uniqueness"]["passed"] is False
    assert data["valid"] is False


@pytest.mark.parametrize(
    ("collection_name", "field", "replacement", "check_name"),
    (
        ("attempts", "attempt_id", "wrong-attempt", "attempt_identifier_bindings"),
        (
            "measurements",
            "measurement_id",
            "wrong-measurement",
            "measurement_identifier_bindings",
        ),
        (
            "measurements",
            "repetition_id",
            "999",
            "measurement_identifier_bindings",
        ),
        ("summaries", "summary_id", "wrong-summary", "summary_identifier_bindings"),
        ("quality", "quality_id", "wrong-quality", "quality_identifier_bindings"),
        ("failures", "failure_id", "wrong-failure", "failure_identifier_bindings"),
        ("evidence", "evidence_id", "wrong-evidence", "evidence_identifier_bindings"),
    ),
)
def test_validation_receipts_reject_noncanonical_record_identity_bindings(
    collection_name, field, replacement, check_name
):
    bundle = build_experimental_bundle(REPO_ROOT)
    records = list(getattr(bundle, collection_name))
    records[0] = replace(records[0], **{field: replacement})

    _, data = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, **{collection_name: tuple(records)})
    )

    assert data["checks"][check_name]["passed"] is False
    assert data["valid"] is False


def test_attempt_identifier_uniqueness_is_independent_of_planned_count():
    bundle = build_experimental_bundle(REPO_ROOT)

    coverage, data = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, attempts=bundle.attempts[:-1])
    )

    assert coverage["checks"]["planned_count"]["passed"] is False
    assert data["checks"]["attempt_identifier_uniqueness"] == {
        "actual": 80,
        "expected": 80,
        "passed": True,
    }


def test_validation_receipts_require_quality_prompt_to_exist_in_prompt_suite():
    bundle = build_experimental_bundle(REPO_ROOT)
    quality = list(bundle.quality)
    quality[0] = replace(quality[0], prompt_id="Q99")

    _, data = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, quality=tuple(quality))
    )

    assert data["checks"]["quality_prompt_bindings"]["passed"] is False
    assert data["valid"] is False


def test_validation_receipts_reject_count_preserving_published_prompt_identity_swap():
    bundle = build_experimental_bundle(REPO_ROOT)
    prompts = _rows(ROUTE / "quality/prompt-suite.csv")
    outputs = _rows(ROUTE / "quality/outputs-index.csv")
    first_id, second_id = prompts[0]["prompt_id"], prompts[1]["prompt_id"]
    prompts[0]["prompt_id"], prompts[1]["prompt_id"] = second_id, first_id
    for row in outputs:
        if row["prompt_id"] not in {first_id, second_id}:
            continue
        row["prompt_id"] = second_id if row["prompt_id"] == first_id else first_id
        row["output_id"] = f"{row['test_case_id']}--{row['prompt_id']}--output"

    _, data = build_experimental_validation_receipts(
        REPO_ROOT,
        bundle,
        prompt_rows=prompts,
        output_rows=outputs,
    )

    assert data["checks"]["prompt_identifier_uniqueness"]["passed"] is True
    assert data["checks"]["output_identifier_uniqueness"]["passed"] is True
    assert data["checks"]["prompt_identifier_bindings"]["passed"] is False
    assert data["checks"]["output_identifier_bindings"]["passed"] is False
    assert data["checks"]["quality_prompt_bindings"]["passed"] is False
    assert data["checks"]["prompt_source_bindings"]["passed"] is False
    assert data["checks"]["output_source_bindings"]["passed"] is False
    assert data["valid"] is False


def test_validation_receipts_reject_noncanonical_output_identity():
    bundle = build_experimental_bundle(REPO_ROOT)
    prompts = _rows(ROUTE / "quality/prompt-suite.csv")
    outputs = _rows(ROUTE / "quality/outputs-index.csv")
    outputs[0]["output_id"] = "wrong-output"

    _, data = build_experimental_validation_receipts(
        REPO_ROOT,
        bundle,
        prompt_rows=prompts,
        output_rows=outputs,
    )

    assert data["checks"]["output_identifier_bindings"]["passed"] is False


@pytest.mark.parametrize(
    ("mutation",),
    (
        (lambda summary: replace(summary, source_measurement_ids=(summary.source_measurement_ids[1], summary.source_measurement_ids[0], summary.source_measurement_ids[2])),),
        (lambda summary: replace(summary, source_measurement_ids=summary.source_measurement_ids[:1]),),
        (lambda summary: replace(summary, aggregation="incorrect aggregation"),),
        (lambda summary: replace(summary, value=float(summary.value) + 1.0),),
    ),
)
def test_validation_receipts_reject_wrong_summary_lineage_aggregation_or_value(mutation):
    bundle = build_experimental_bundle(REPO_ROOT)
    summaries = list(bundle.summaries)
    summaries[0] = mutation(summaries[0])

    _, data = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, summaries=tuple(summaries))
    )

    assert data["checks"]["summary_lineage_and_values"]["passed"] is False
    assert data["valid"] is False


def test_validation_receipts_reject_cross_case_measurement_attempt():
    bundle = build_experimental_bundle(REPO_ROOT)
    measurements = list(bundle.measurements)
    wrong_attempt = next(
        attempt
        for attempt in bundle.attempts
        if attempt.executed and attempt.test_case_id != measurements[0].test_case_id
    )
    measurements[0] = replace(measurements[0], attempt_id=wrong_attempt.attempt_id)

    _, data = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, measurements=tuple(measurements))
    )

    assert data["checks"]["measurement_references"]["passed"] is False


def test_validation_receipts_reject_cross_case_summary_measurement():
    bundle = build_experimental_bundle(REPO_ROOT)
    summaries = list(bundle.summaries)
    wrong_measurement = next(
        measurement
        for measurement in bundle.measurements
        if measurement.test_case_id != summaries[0].test_case_id
    )
    summaries[0] = replace(
        summaries[0], source_measurement_ids=(wrong_measurement.measurement_id,)
    )

    _, data = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, summaries=tuple(summaries))
    )

    assert data["checks"]["summary_references"]["passed"] is False


def test_validation_receipts_reject_cross_case_quality_raw_evidence():
    bundle = build_experimental_bundle(REPO_ROOT)
    quality = list(bundle.quality)
    wrong_raw = next(
        evidence
        for evidence in bundle.evidence
        if evidence.role == "raw-case-result"
        and quality[0].test_case_id not in str(evidence.source_label)
    )
    quality[0] = replace(quality[0], source_evidence_id=wrong_raw.evidence_id)

    _, data = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, quality=tuple(quality))
    )

    assert data["checks"]["quality_references"]["passed"] is False


def test_validation_receipts_reject_attempt_raw_evidence_from_another_case():
    bundle = build_experimental_bundle(REPO_ROOT)
    attempts = list(bundle.attempts)
    index = next(index for index, attempt in enumerate(attempts) if attempt.executed)
    attempt = attempts[index]
    evidence_by_id = {item.evidence_id: item for item in bundle.evidence}
    wrong_raw = next(
        evidence
        for evidence in bundle.evidence
        if evidence.role == "raw-case-result"
        and attempt.test_case_id not in str(evidence.source_label)
    )
    attempts[index] = replace(
        attempt,
        evidence_ids=tuple(
            wrong_raw.evidence_id
            if evidence_by_id[evidence_id].role == "raw-case-result"
            else evidence_id
            for evidence_id in attempt.evidence_ids
        ),
    )

    _, data = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, attempts=tuple(attempts))
    )

    assert data["checks"]["attempt_source_evidence_references"]["passed"] is False


def test_validation_receipts_reject_failure_linked_to_another_case_attempt():
    bundle = build_experimental_bundle(REPO_ROOT)
    failures = list(bundle.failures)
    wrong_attempt = next(
        attempt
        for attempt in bundle.attempts
        if not attempt.executed and attempt.test_case_id != failures[0].test_case_id
    )
    failures[0] = replace(failures[0], attempt_id=wrong_attempt.attempt_id)

    _, data = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, failures=tuple(failures))
    )

    assert data["checks"]["failure_references"]["passed"] is False


def test_validation_receipts_reject_output_with_prompt_input_evidence_role():
    bundle = build_experimental_bundle(REPO_ROOT)
    prompts = _rows(ROUTE / "quality/prompt-suite.csv")
    outputs = _rows(ROUTE / "quality/outputs-index.csv")
    prompt_evidence = next(
        evidence for evidence in bundle.evidence if evidence.role == "quality-prompt-input"
    )
    outputs[0]["source_evidence_id"] = prompt_evidence.evidence_id

    _, data = build_experimental_validation_receipts(
        REPO_ROOT,
        bundle,
        prompt_rows=prompts,
        output_rows=outputs,
    )

    assert data["checks"]["output_references"]["passed"] is False


def test_validation_receipts_reject_source_evidence_with_derived_links():
    bundle = build_experimental_bundle(REPO_ROOT)
    evidence = list(bundle.evidence)
    evidence[0] = replace(
        evidence[0],
        derived=True,
        input_evidence_ids=(evidence[1].evidence_id,),
    )

    _, data = build_experimental_validation_receipts(
        REPO_ROOT, replace(bundle, evidence=tuple(evidence))
    )

    assert data["checks"]["evidence_references"]["passed"] is False


def test_fv6_rejects_a_conflicting_comparison_value(tmp_path):
    repo = _isolated_fv6_repo(tmp_path)
    comparison_path = (
        repo / FV6.relative_to(REPO_ROOT) / "experimental-openvino-comparison.csv"
    )
    rows = _rows(comparison_path)
    target = next(
        row
        for row in rows
        if (row["model"], row["weight_precision"], row["cache_codec"])
        == ("granite-3b", "int4", "tbq3")
    )
    target["decode_tps"] = str(float(target["decode_tps"]) + 1.0)
    _write_rows(comparison_path, rows)

    with pytest.raises(ValueError, match="comparison"):
        build_experimental_bundle(repo)


def test_fv6_rejects_a_selected_repetition_metric_conflict(tmp_path):
    repo = _isolated_fv6_repo(tmp_path)
    _rewrite_raw(
        repo,
        "granite-3b__int4__tbq3",
        lambda payload: payload["benchmark_runs"][0]["result"].__setitem__(
            "input_tokens", payload["benchmark_runs"][0]["result"]["input_tokens"] + 1
        ),
    )

    with pytest.raises(ValueError, match="selected benchmark"):
        build_experimental_bundle(repo)


def test_fv6_rejects_nonselected_repetition_stdout_result_conflict(tmp_path):
    repo = _isolated_fv6_repo(tmp_path)
    _rewrite_raw(
        repo,
        "granite-3b__int4__tbq3",
        lambda payload: payload["benchmark_runs"][1]["result"].__setitem__(
            "input_tokens", payload["benchmark_runs"][1]["result"]["input_tokens"] + 1
        ),
    )

    with pytest.raises(ValueError, match="stdout/result conflict"):
        build_experimental_bundle(repo)


def test_fv6_rejects_complete_internally_consistent_nonmedian_selection(tmp_path):
    repo = _isolated_fv6_repo(tmp_path)

    def mutate(payload):
        payload["benchmark"] = json.loads(
            json.dumps(payload["benchmark_runs"][1])
        )

    _rewrite_raw(repo, "granite-3b__int4__tbq3", mutate)

    with pytest.raises(ValueError, match="not the median-decode repetition"):
        build_experimental_bundle(repo)


@pytest.mark.parametrize(
    ("field", "replacement"),
    (
        ("id", "wrong-criterion"),
        ("category", "wrong-category"),
        ("kind", "contains_none"),
        ("weight", 4.5),
        ("points_awarded", 4.5),
        ("critical", False),
        ("passed", False),
        ("expected", ["wrong expected value"]),
        ("observed", {"missing": ["wrong observed value"]}),
        ("reason", "conflicting reason"),
    ),
)
def test_fv6_rejects_raw_quality_criterion_conflicts(tmp_path, field, replacement):
    repo = _isolated_fv6_repo(tmp_path)

    def mutate(payload):
        payload["quality_runs"][0]["quality"]["criteria"][0][field] = replacement

    _rewrite_raw(repo, "granite-3b__int4__tbq3", mutate)

    with pytest.raises(ValueError, match="quality criterion"):
        build_experimental_bundle(repo)


@pytest.mark.parametrize(
    ("field", "replacement"),
    (
        ("valid_output", False),
        ("critical_failure", True),
        ("score", 4.25),
        ("domain", "education"),
        ("prompt_length", "long"),
    ),
)
def test_fv6_rejects_raw_quality_prompt_metadata_or_score_conflicts(
    tmp_path, field, replacement
):
    repo = _isolated_fv6_repo(tmp_path)

    def mutate(payload):
        target = payload["quality_runs"][0]
        if field in {"domain", "prompt_length"}:
            target[field] = replacement
        else:
            target["quality"][field] = replacement

    _rewrite_raw(repo, "granite-3b__int4__tbq3", mutate)

    with pytest.raises(ValueError, match="quality prompt"):
        build_experimental_bundle(repo)


def test_fv6_rejects_raw_quality_output_text_conflict(tmp_path):
    repo = _isolated_fv6_repo(tmp_path)

    def mutate(payload):
        payload["quality_runs"][0]["result"]["text"] += " altered"

    _rewrite_raw(repo, "granite-3b__int4__tbq3", mutate)

    with pytest.raises(ValueError, match="quality output"):
        build_experimental_bundle(repo)


@pytest.mark.parametrize(
    ("field", "replacement"),
    (("domain", "education"), ("prompt_length", "long")),
)
def test_fv6_rejects_raw_and_embedded_prompt_metadata_conflicting_with_csv(
    tmp_path, field, replacement
):
    repo = _isolated_fv6_repo(tmp_path)

    def mutate(payload):
        run = payload["quality_runs"][0]
        run[field] = replacement
        run["quality"][field] = replacement

    _rewrite_raw(repo, "granite-3b__int4__tbq3", mutate)

    with pytest.raises(ValueError, match=f"quality prompt {field} conflict"):
        build_experimental_bundle(repo)


def test_fv6_rejects_nonuniform_quality_scoring_schema(tmp_path):
    repo = _isolated_fv6_repo(tmp_path)

    def mutate(payload):
        payload["quality_runs"][0]["quality"]["schema"] = "unexpected-quality/v9"

    _rewrite_raw(repo, "granite-3b__int4__tbq3", mutate)

    with pytest.raises(ValueError, match="quality scoring schema"):
        build_experimental_bundle(repo)


@pytest.mark.parametrize(
    ("field", "replacement"),
    (
        ("category_scores", {"safety": 9.5}),
        ("health_checks", {}),
    ),
)
def test_fv6_rejects_raw_quality_category_or_health_conflicts(
    tmp_path, field, replacement
):
    repo = _isolated_fv6_repo(tmp_path)

    def mutate(payload):
        payload["quality_runs"][0]["quality"][field] = replacement

    _rewrite_raw(repo, "granite-3b__int4__tbq3", mutate)

    with pytest.raises(ValueError, match="quality criterion"):
        build_experimental_bundle(repo)


def test_fv6_rejects_a_case_score_that_disagrees_with_prompt_scores(tmp_path):
    repo = _isolated_fv6_repo(tmp_path)
    fv6 = repo / FV6.relative_to(REPO_ROOT)
    case_id = "granite-3b__int4__tbq3"
    detailed_path = fv6 / "experimental-openvino-detailed-results.csv"
    detailed = _rows(detailed_path)
    next(row for row in detailed if row["case_id"] == case_id)["quality_score"] = "9.9999"
    _write_rows(detailed_path, detailed)
    comparison_path = fv6 / "experimental-openvino-comparison.csv"
    comparison = _rows(comparison_path)
    next(
        row
        for row in comparison
        if (row["model"], row["weight_precision"], row["cache_codec"])
        == ("granite-3b", "int4", "tbq3")
    )["quality_score"] = "9.9999"
    _write_rows(comparison_path, comparison)

    with pytest.raises(ValueError, match="quality case score"):
        build_experimental_bundle(repo)


def test_experimental_route_generation_preserves_an_identical_existing_manifest():
    # Reintroducing an unconditional manifest delete/rewrite must break this
    # idempotence contract and could overwrite reviewer-controlled evidence.
    write_experimental_route(REPO_ROOT)
    manifest = ROUTE / "evidence/manifest-sha256.txt"
    preserved_timestamp_ns = 1_700_000_000_000_000_000
    os.utime(manifest, ns=(preserved_timestamp_ns, preserved_timestamp_ns))

    write_experimental_route(REPO_ROOT)

    assert manifest.stat().st_mtime_ns == preserved_timestamp_ns
