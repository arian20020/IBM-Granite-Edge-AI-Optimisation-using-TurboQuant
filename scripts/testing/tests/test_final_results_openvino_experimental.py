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
        "cache_format_count",
        "executed_count",
        "executed_model_weight_artifact_count",
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
        "attempt_source_evidence_references",
        "exactly_three_distinct_criteria_per_prompt",
        "failure_count",
        "failure_references",
        "measurement_count",
        "measurement_references",
        "output_count",
        "output_references",
        "prompt_count",
        "prompt_references",
        "quality_criterion_count",
        "quality_references",
        "summary_count",
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
