from __future__ import annotations

import csv
from dataclasses import replace
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys

import pytest


REPO_ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPO_ROOT))

from scripts.testing.final_results.models import Status
from scripts.testing.final_results.openvino_adapter import (
    build_official_bundle,
    build_official_validation_receipts,
    write_official_route,
)


FV1 = Path("experiments/raw-results/openvino-official-upstream/2026-08-30/fv1")
FV2 = Path(
    "experiments/raw-results/openvino-official-upstream/2026-08-30/"
    "fv2-missing-model-attempts"
)
V1_WORKBOOK = Path(
    "outputs/openvino-official-upstream-results/"
    "Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30.xlsx"
)
V2_WORKBOOK = Path(
    "outputs/openvino-official-upstream-results/"
    "Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx"
)
ROUTE = REPO_ROOT / "docs/testing/final-results/05-openvino-official-upstream"


def _rows(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def _write_rows(path: Path, rows: list[dict[str, str]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=tuple(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def _isolated_official_repo(tmp_path: Path) -> Path:
    repo = tmp_path / "repo"
    (repo / FV1.parent).mkdir(parents=True)

    def ignore_broken_preflight_inputs(directory: str, names: list[str]) -> set[str]:
        if Path(directory).name == "preflight" and "inputs" in names:
            return {"inputs"}
        return set()

    shutil.copytree(
        REPO_ROOT / FV1,
        repo / FV1,
        symlinks=True,
        ignore_dangling_symlinks=True,
        ignore=ignore_broken_preflight_inputs,
    )
    shutil.copytree(REPO_ROOT / FV2, repo / FV2)
    for relative in (V1_WORKBOOK, V2_WORKBOOK):
        target = repo / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(REPO_ROOT / relative, target)
    return repo


def _rewrite_json(repo: Path, relative: Path, mutate) -> None:
    path = repo / relative
    payload = json.loads(path.read_text(encoding="utf-8"))
    mutate(payload)
    path.write_text(
        json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")),
        encoding="utf-8",
    )


def _rewrite_raw(repo: Path, case_id: str, mutate) -> None:
    relative = FV1 / "raw" / f"{case_id}.json"
    _rewrite_json(repo, relative, mutate)
    raw_path = repo / relative
    digest = hashlib.sha256(raw_path.read_bytes()).hexdigest()
    detailed_path = repo / FV1 / "official-openvino-detailed-results.csv"
    detailed = _rows(detailed_path)
    next(row for row in detailed if row["case_id"] == case_id)[
        "raw_result_sha256"
    ] = digest
    _write_rows(detailed_path, detailed)
    fv2_path = repo / FV2 / "consolidated/official-openvino-detailed-results.csv"
    fv2 = _rows(fv2_path)
    next(row for row in fv2 if row["case_id"] == case_id)[
        "raw_result_sha256"
    ] = digest
    _write_rows(fv2_path, fv2)


def test_official_campaign_joins_fv2_statuses_to_only_fv1_passed_evidence():
    bundle = build_official_bundle(REPO_ROOT)

    assert bundle.route_id == "openvino-official-upstream"
    assert bundle.campaign_id == "fv2-2026-08-30"
    assert len(bundle.attempts) == 45
    assert sum(item.status is Status.PASSED for item in bundle.attempts) == 15
    assert sum(item.status is Status.FAILED for item in bundle.attempts) == 5
    assert sum(item.status is Status.BLOCKED for item in bundle.attempts) == 25
    assert sum(item.executed for item in bundle.attempts) == 15
    assert len(bundle.measurements) == 45
    assert len(bundle.summaries) == 45
    assert len(bundle.quality) == 2_160
    assert len(bundle.failures) == 30

    passed = {item.test_case_id for item in bundle.attempts if item.status is Status.PASSED}
    assert {item.test_case_id for item in bundle.measurements} == passed
    assert {item.test_case_id for item in bundle.summaries} == passed
    assert {item.test_case_id for item in bundle.quality} == passed
    assert not ({item.test_case_id for item in bundle.failures} & passed)

    cache_ids = {item.cache_format_id for item in bundle.attempts}
    assert {"tbq3", "tbq4"} <= cache_ids
    assert not cache_ids & {"polar3", "polar4", "qjl3", "qjl4"}
    assert all("polar" not in value and "qjl" not in value for value in cache_ids)

    failed = next(
        item for item in bundle.failures
        if item.test_case_id == "granite-3b__fp16__tbq3"
    )
    assert failed.status is Status.FAILED
    assert failed.source_status == "conversion_failed"
    assert failed.stage == "model_conversion"
    assert failed.reason == "emergency_ram_floor_reached"
    blocked = next(
        item for item in bundle.failures
        if item.test_case_id == "granite-30b__int4__tbq4"
    )
    assert blocked.status is Status.BLOCKED
    assert blocked.source_status == "hardware_preflight_blocked"
    assert blocked.stage == "conversion_preflight"
    assert blocked.reason == (
        "official source metadata proves conversion cannot preserve the 2 GiB "
        "emergency RAM floor on this host"
    )
    assert any(
        item.role == "missing-model-attempt-manifest"
        and item.relative_path.endswith("attempts/granite-30b__int4/manifest.json")
        and item.evidence_id in blocked.evidence_ids
        for item in bundle.evidence
    )


def test_official_bundle_publishes_source_derived_identities_and_lineage():
    bundle = build_official_bundle(REPO_ROOT)
    case_id = "granite-3b__int4__tbq3"
    attempt = next(item for item in bundle.attempts if item.test_case_id == case_id)
    assert attempt.attempt_id == f"{case_id}--attempt-001"
    measurements = [item for item in bundle.measurements if item.test_case_id == case_id]
    assert [item.measurement_id for item in measurements] == [
        f"{case_id}--benchmark-repetition-001",
        f"{case_id}--benchmark-repetition-002",
        f"{case_id}--benchmark-repetition-003",
    ]
    assert [item.run_id for item in measurements] == [f"{case_id}--benchmark"] * 3
    assert [item.repetition_id for item in measurements] == ["001", "002", "003"]
    raw = json.loads((REPO_ROOT / FV1 / "raw" / f"{case_id}.json").read_text())
    assert [item.latency_ms for item in measurements] == [
        float(run["result"]["ttft_ms"]) for run in raw["benchmark_runs"]
    ]
    assert [item.generation_tokens_per_second for item in measurements] == [
        float(run["result"]["decode_tps"]) for run in raw["benchmark_runs"]
    ]
    lineage = tuple(item.measurement_id for item in measurements)
    summaries = {item.metric_name: item for item in bundle.summaries if item.test_case_id == case_id}
    assert set(summaries) == {
        "generation_tokens_per_second",
        "time_to_first_token",
        "peak_working_set_bytes",
    }
    assert all(item.source_measurement_ids == lineage for item in summaries.values())
    assert summaries["generation_tokens_per_second"].value == 17.751513
    assert summaries["time_to_first_token"].value == 4413.813965
    assert summaries["peak_working_set_bytes"].value == 3_635_527_000

    quality = next(
        item for item in bundle.quality
        if item.test_case_id == case_id and item.prompt_id == "Q01"
        and item.criterion_id == "safety:primary"
    )
    assert quality.quality_id == f"{case_id}--Q01--safety--primary"
    assert quality.prompt_suite_id == "OPENVINO-SECTOR-EXPERIENCE-QUALITY-v3"
    assert quality.rubric_id == "objective-quality-weighted-5-3-2-output-health-gate"
    assert quality.scoring_version == "experimental-openvino-objective-quality/v2"


def test_official_generation_writes_common_route_and_copies_only_primary_workbook():
    bundle = write_official_route(REPO_ROOT)

    expected = {
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
        f"results/source/{V2_WORKBOOK.name}",
        "quality/prompt-suite.csv",
        "quality/scores.csv",
        "quality/outputs-index.csv",
        "failures/failure-register.csv",
        "evidence/evidence-index.csv",
        "evidence/source-locations.csv",
        "evidence/claim-evidence-map.csv",
        "evidence/manifest-sha256.txt",
        "reproduction/README.md",
        "validation/coverage-validation.json",
        "validation/data-validation.json",
        "validation/integrity-validation.json",
    }
    actual = {path.relative_to(ROUTE).as_posix() for path in ROUTE.rglob("*") if path.is_file()}
    assert actual == expected
    copied = ROUTE / "results/source" / V2_WORKBOOK.name
    assert copied.read_bytes() == (REPO_ROOT / V2_WORKBOOK).read_bytes()
    assert not (ROUTE / "results/source" / V1_WORKBOOK.name).exists()
    assert any(
        item.role == "indexed-prior-workbook"
        and item.relative_path == V1_WORKBOOK.as_posix()
        and item.sha256 == "b3e26eae69c3854dec26536c6d141943572292f8a9ea331cb4de1d88c76b32b4"
        for item in bundle.evidence
    )
    primary = next(item for item in bundle.evidence if item.role == "source-workbook")
    assert primary.relative_path == V2_WORKBOOK.as_posix()
    assert primary.sha256 == "1d5fc2893e0c7f412140b3fa1a26c4a0c18e3c65ecfa356e80549dc4cd10aff7"

    coverage = json.loads((ROUTE / "validation/coverage-validation.json").read_text())
    data = json.loads((ROUTE / "validation/data-validation.json").read_text())
    assert coverage["valid"] is True
    assert data["valid"] is True
    assert all(check["passed"] for check in coverage["checks"].values())
    assert all(check["passed"] for check in data["checks"].values())


def test_official_validation_rejects_full_entity_and_typed_cross_case_mutations():
    bundle = build_official_bundle(REPO_ROOT)
    attempt = list(bundle.attempts)
    target = next(i for i, item in enumerate(attempt) if item.status is Status.BLOCKED)
    attempt[target] = replace(attempt[target], reason="fabricated reason")
    coverage, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, attempts=tuple(attempt))
    )
    assert coverage["valid"] is False or data["valid"] is False
    assert data["checks"]["attempt_entities"]["passed"] is False

    measurements = list(bundle.measurements)
    measurements[0] = replace(
        measurements[0], attempt_id=next(
            item.attempt_id
            for item in bundle.attempts
            if item.status is Status.PASSED
            and item.test_case_id != measurements[0].test_case_id
        )
    )
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, measurements=tuple(measurements))
    )
    assert data["valid"] is False
    assert data["checks"]["measurement_entities"]["passed"] is False

    summaries = list(bundle.summaries)
    summaries[0] = replace(summaries[0], aggregation="mean")
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, summaries=tuple(summaries))
    )
    assert data["valid"] is False
    assert data["checks"]["summary_entities"]["passed"] is False

    evidence = list(bundle.evidence)
    raw_index = next(i for i, item in enumerate(evidence) if item.role == "raw-case-result")
    evidence[raw_index] = replace(evidence[raw_index], role="source-results")
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, evidence=tuple(evidence))
    )
    assert data["valid"] is False
    assert data["checks"]["evidence_entities"]["passed"] is False


def test_official_validation_rejects_each_published_entity_type_mutation():
    bundle = write_official_route(REPO_ROOT)

    quality = list(bundle.quality)
    quality[0] = replace(quality[0], score=float(quality[0].score) + 0.5)
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, quality=tuple(quality))
    )
    assert data["checks"]["quality_entities"]["passed"] is False

    failures = list(bundle.failures)
    failures[0] = replace(failures[0], stage="fabricated-stage")
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, failures=tuple(failures))
    )
    assert data["checks"]["failure_entities"]["passed"] is False

    summaries = list(bundle.summaries)
    summaries[0] = replace(summaries[0], value=float(summaries[0].value) + 1.0)
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, summaries=tuple(summaries))
    )
    assert data["checks"]["summary_entities"]["passed"] is False
    summaries[0] = replace(
        bundle.summaries[0],
        source_measurement_ids=tuple(reversed(bundle.summaries[0].source_measurement_ids)),
    )
    _, data = build_official_validation_receipts(
        REPO_ROOT, replace(bundle, summaries=tuple(summaries))
    )
    assert data["checks"]["summary_entities"]["passed"] is False

    prompt_rows = _rows(ROUTE / "quality/prompt-suite.csv")
    prompt_rows[0]["domain"] = "fabricated-domain"
    _, data = build_official_validation_receipts(
        REPO_ROOT, bundle, prompt_rows=prompt_rows
    )
    assert data["checks"]["prompt_entities"]["passed"] is False

    output_rows = _rows(ROUTE / "quality/outputs-index.csv")
    output_rows[0]["output_sha256"] = "0" * 64
    _, data = build_official_validation_receipts(
        REPO_ROOT, bundle, output_rows=output_rows
    )
    assert data["checks"]["output_entities"]["passed"] is False

    availability_rows = _rows(ROUTE / "results/availability-matrix.csv")
    availability_rows[0]["reason"] = "fabricated reason"
    _, data = build_official_validation_receipts(
        REPO_ROOT, bundle, availability_rows=availability_rows
    )
    assert data["checks"]["availability_entities"]["passed"] is False

    artifact_rows = _rows(ROUTE / "system/model-artifacts.csv")
    artifact_rows[0]["executed_case_count"] = "999"
    _, data = build_official_validation_receipts(
        REPO_ROOT, bundle, model_artifact_rows=artifact_rows
    )
    assert data["checks"]["model_artifact_entities"]["passed"] is False


def test_official_rejects_a_passed_fv2_case_without_fv1_raw_evidence(tmp_path):
    repo = _isolated_official_repo(tmp_path)
    detailed_path = repo / FV1 / "official-openvino-detailed-results.csv"
    rows = _rows(detailed_path)
    target = next(row for row in rows if row["case_id"] == "granite-3b__int4__tbq3")
    target["raw_result_path"] = ""
    target["raw_result_sha256"] = ""
    _write_rows(detailed_path, rows)

    with pytest.raises(ValueError, match="passed fv2.*raw evidence"):
        build_official_bundle(repo)


@pytest.mark.parametrize("field", ("decode_tps", "quality_score", "raw_result_path"))
def test_official_rejects_nonpassed_fv2_published_observations(tmp_path, field):
    repo = _isolated_official_repo(tmp_path)
    path = repo / FV2 / "consolidated/official-openvino-detailed-results.csv"
    rows = _rows(path)
    target = next(row for row in rows if row["status"] == "hardware_preflight_blocked")
    target[field] = "1" if field != "raw_result_path" else "fabricated.json"
    _write_rows(path, rows)
    if field in {"decode_tps", "quality_score"}:
        comparison_path = repo / FV2 / "consolidated/official-openvino-comparison.csv"
        comparison = _rows(comparison_path)
        compared = next(
            row for row in comparison
            if (row["model"], row["weight_precision"], row["cache_codec"])
            == (target["model"], target["weight_precision"], target["cache_codec"])
        )
        compared[field] = target[field]
        _write_rows(comparison_path, comparison)

    with pytest.raises(ValueError, match="non-passed.*published"):
        build_official_bundle(repo)


def test_official_rejects_a_changed_final_status_or_failure_reason(tmp_path):
    repo = _isolated_official_repo(tmp_path)
    path = repo / FV2 / "consolidated/official-openvino-detailed-results.csv"
    rows = _rows(path)
    target = next(row for row in rows if row["status"] == "hardware_preflight_blocked")
    target["failure_reason"] = "generic block"
    _write_rows(path, rows)
    comparison_path = repo / FV2 / "consolidated/official-openvino-comparison.csv"
    comparison = _rows(comparison_path)
    next(
        row for row in comparison
        if (row["model"], row["weight_precision"], row["cache_codec"])
        == (target["model"], target["weight_precision"], target["cache_codec"])
    )["failure_reason"] = "generic block"
    _write_rows(comparison_path, comparison)
    rows_path = repo / FV2 / "consolidated/rows.json"
    payload = json.loads(rows_path.read_text(encoding="utf-8"))
    next(row for row in payload["rows"] if row["case_id"] == target["case_id"])[
        "failure_reason"
    ] = "generic block"
    rows_path.write_text(json.dumps(payload), encoding="utf-8")

    with pytest.raises(ValueError, match="manifest.*failure reason|failure reason.*manifest"):
        build_official_bundle(repo)


def test_official_rejects_missing_or_conflicting_missing_model_manifest(tmp_path):
    repo = _isolated_official_repo(tmp_path)
    manifest = repo / FV2 / "attempts/granite-30b__int4/manifest.json"
    manifest.unlink()
    with pytest.raises((FileNotFoundError, ValueError), match="manifest"):
        build_official_bundle(repo)

    repo = _isolated_official_repo(tmp_path / "conflict")
    _rewrite_json(
        repo,
        FV2 / "attempts/granite-30b__int4/manifest.json",
        lambda payload: payload.__setitem__("failure_stage", "wrong_stage"),
    )
    with pytest.raises(ValueError, match="manifest.*failure stage|failure stage.*manifest"):
        build_official_bundle(repo)


def test_official_rejects_fv1_comparison_and_repetition_conflicts(tmp_path):
    repo = _isolated_official_repo(tmp_path)
    comparison_path = repo / FV1 / "official-openvino-comparison.csv"
    rows = _rows(comparison_path)
    target = next(
        row for row in rows
        if (row["model"], row["weight_precision"], row["cache_codec"])
        == ("granite-3b", "int4", "tbq3")
    )
    target["decode_tps"] = "99"
    _write_rows(comparison_path, rows)
    with pytest.raises(ValueError, match="fv1 comparison"):
        build_official_bundle(repo)

    repo = _isolated_official_repo(tmp_path / "stdout")

    def mutate_nonselected_repetition(payload):
        target = next(
            run for run in payload["benchmark_runs"] if run != payload["benchmark"]
        )
        target["result"]["input_tokens"] += 1

    _rewrite_raw(
        repo,
        "granite-3b__int4__tbq3",
        mutate_nonselected_repetition,
    )
    with pytest.raises(ValueError, match="stdout/result conflict"):
        build_official_bundle(repo)


def test_official_rejects_quality_criterion_prompt_and_output_conflicts(tmp_path):
    repo = _isolated_official_repo(tmp_path)
    _rewrite_raw(
        repo,
        "granite-3b__int4__tbq3",
        lambda payload: payload["quality_runs"][0]["quality"]["criteria"][0].__setitem__(
            "weight", 4.5
        ),
    )
    with pytest.raises(ValueError, match="quality criterion"):
        build_official_bundle(repo)

    repo = _isolated_official_repo(tmp_path / "prompt")
    _rewrite_raw(
        repo,
        "granite-3b__int4__tbq3",
        lambda payload: payload["quality_runs"][0].__setitem__("domain", "education"),
    )
    with pytest.raises(ValueError, match="quality prompt"):
        build_official_bundle(repo)

    repo = _isolated_official_repo(tmp_path / "output")
    _rewrite_raw(
        repo,
        "granite-3b__int4__tbq3",
        lambda payload: payload["quality_runs"][0]["result"].__setitem__(
            "text", payload["quality_runs"][0]["result"]["text"] + " altered"
        ),
    )
    with pytest.raises(ValueError, match="quality output"):
        build_official_bundle(repo)


def test_official_route_generation_preserves_an_identical_existing_manifest():
    write_official_route(REPO_ROOT)
    manifest = ROUTE / "evidence/manifest-sha256.txt"
    preserved_timestamp_ns = 1_700_000_000_000_000_000
    os.utime(manifest, ns=(preserved_timestamp_ns, preserved_timestamp_ns))

    write_official_route(REPO_ROOT)

    assert manifest.stat().st_mtime_ns == preserved_timestamp_ns
