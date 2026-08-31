import csv
import hashlib
import json
import os
import sys
from collections import Counter, defaultdict
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPO_ROOT))

from scripts.testing.final_results.csvio import validate_json
from scripts.testing.final_results.evidence import validate_sha256_manifest
from scripts.testing.final_results.models import Status
from scripts.testing.final_results.openvino_adapter import (
    build_experimental_bundle,
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

    coverage = json.loads((ROUTE / "validation/coverage-validation.json").read_text(encoding="utf-8"))
    assert coverage == {
        "artifact_unavailable": 54,
        "cache_format_count": 9,
        "executed": 27,
        "model_weight_artifact_count": 3,
        "passed": 27,
        "planned": 81,
        "quality_prompts_per_passed_case": 48,
        "valid": True,
    }


def test_experimental_route_generation_preserves_an_identical_existing_manifest():
    # Reintroducing an unconditional manifest delete/rewrite must break this
    # idempotence contract and could overwrite reviewer-controlled evidence.
    write_experimental_route(REPO_ROOT)
    manifest = ROUTE / "evidence/manifest-sha256.txt"
    preserved_timestamp_ns = 1_700_000_000_000_000_000
    os.utime(manifest, ns=(preserved_timestamp_ns, preserved_timestamp_ns))

    write_experimental_route(REPO_ROOT)

    assert manifest.stat().st_mtime_ns == preserved_timestamp_ns
