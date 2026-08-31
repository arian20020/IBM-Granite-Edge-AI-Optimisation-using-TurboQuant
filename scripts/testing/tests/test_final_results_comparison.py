import csv
import dataclasses
import json
import sys
from pathlib import Path


sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.final_results.models import (
    AttemptRecord,
    MeasurementRecord,
    QualityRecord,
    RouteBundle,
    Status,
    SummaryRecord,
)
from scripts.testing.final_results.openvino_adapter import (
    build_experimental_bundle,
    build_official_bundle,
)
from scripts.testing.final_results.report_model import (
    ReportNote,
    ReportParagraph,
    ReportTable,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


def _bundle(
    route_id: str,
    *,
    model: str = "granite-3b",
    backend: str = "openvino-cpu",
    input_tokens: int = 24,
    output_tokens: int = 32,
    metric_name: str = "generation_tokens_per_second",
    unit: str = "tokens_per_second",
    aggregation: str = "median over three executed benchmark repetitions",
    prompt_ids: tuple[str, ...] = ("Q01", "Q02"),
    rubric: str = "objective-v3",
    scoring_version: str = "v3",
    maximum_score: float = 10.0,
    quality_aggregation: str = "10 * awarded points / possible points",
    status: Status = Status.PASSED,
) -> RouteBundle:
    reason = "" if status is Status.PASSED else f"synthetic {status.value} outcome"
    executed = status is Status.PASSED
    attempt = AttemptRecord(
        route_id=route_id,
        campaign_id=f"{route_id}-campaign",
        test_case_id=f"{route_id}-case",
        attempt_id=f"{route_id}-attempt",
        status=status,
        executed=executed,
        reason=reason,
        model_id=model,
        weight_format_id="int4",
        cache_format_id="tbq3",
        backend_id=backend,
    )
    measurements = ()
    summaries = ()
    quality = ()
    if executed:
        measurements = tuple(
            MeasurementRecord(
                route_id=route_id,
                campaign_id=f"{route_id}-campaign",
                test_case_id=f"{route_id}-case",
                attempt_id=f"{route_id}-attempt",
                measurement_id=f"{route_id}-measurement-{number}",
                repetition_id=str(number),
                generation_tokens_per_second=10.0 + number,
                input_tokens=input_tokens,
                output_tokens=output_tokens,
            )
            for number in (1, 2, 3)
        )
        summaries = (
            SummaryRecord(
                route_id=route_id,
                campaign_id=f"{route_id}-campaign",
                test_case_id=f"{route_id}-case",
                summary_id=f"{route_id}-summary",
                metric_name=metric_name,
                value=12.0,
                unit=unit,
                aggregation=aggregation,
                source_measurement_ids=tuple(row.measurement_id for row in measurements),
            ),
        )
        quality = tuple(
            QualityRecord(
                route_id=route_id,
                campaign_id=f"{route_id}-campaign",
                test_case_id=f"{route_id}-case",
                quality_id=f"{route_id}-{prompt_id}",
                prompt_id=prompt_id,
                criterion_id="criterion",
                score=8.0,
                maximum_score=maximum_score,
                prompt_suite_id="suite-v3",
                rubric_id=rubric,
                scoring_version=scoring_version,
            )
            for prompt_id in prompt_ids
        )
    return RouteBundle(
        route_id=route_id,
        campaign_id=f"{route_id}-campaign",
        attempts=(attempt,),
        measurements=measurements,
        summaries=summaries,
        quality=quality,
        repository={"quality_aggregation": quality_aggregation, "source_date": "2026-08-30"},
    )


def _text(report) -> str:
    values = [report.title]
    for section in report.sections:
        values.append(section.title)
        for block in section.blocks:
            if isinstance(block, (ReportParagraph, ReportNote)):
                values.append(block.text)
            elif isinstance(block, ReportTable):
                values.extend((block.table_id, block.title, block.subtitle, *block.columns))
                values.extend(cell for row in block.rows for cell in row)
                values.extend(block.footnotes)
    return "\n".join(values)


def _tables(report) -> dict[str, ReportTable]:
    return {
        block.table_id: block
        for section in report.sections
        for block in section.blocks
        if isinstance(block, ReportTable)
    }


def test_throughput_direct_comparison_requires_every_protocol_dimension():
    from scripts.testing.final_results.comparison import classify_comparability

    left = _bundle("left")
    right = _bundle("right")
    result = classify_comparability(left, right, "generation_tokens_per_second")
    assert result.classification == "direct"
    assert result.reasons == ("all_required_dimensions_match",)

    mutations = {
        "model_mismatch": dataclasses.replace(right, attempts=(dataclasses.replace(right.attempts[0], model_id="granite-8b"),)),
        "input_length_mismatch": dataclasses.replace(right, measurements=tuple(dataclasses.replace(row, input_tokens=48) for row in right.measurements)),
        "output_length_mismatch": dataclasses.replace(right, measurements=tuple(dataclasses.replace(row, output_tokens=64) for row in right.measurements)),
        "backend_class_mismatch": dataclasses.replace(right, attempts=(dataclasses.replace(right.attempts[0], backend_id="llama-vulkan"),)),
        "repetition_treatment_mismatch": dataclasses.replace(right, summaries=(dataclasses.replace(right.summaries[0], aggregation="mean over three repetitions"),)),
        "metric_definition_mismatch": dataclasses.replace(right, summaries=(dataclasses.replace(right.summaries[0], unit="milliseconds"),)),
    }
    expected_classifications = {
        "model_mismatch": "not_comparable",
        "input_length_mismatch": "normalized_with_caveat",
        "output_length_mismatch": "normalized_with_caveat",
        "backend_class_mismatch": "not_comparable",
        "repetition_treatment_mismatch": "normalized_with_caveat",
        "metric_definition_mismatch": "not_comparable",
    }
    for reason, changed in mutations.items():
        result = classify_comparability(left, changed, "generation_tokens_per_second")
        assert result.classification == expected_classifications[reason], reason
        assert reason in result.reasons
        assert result.reason_details[reason]["left"] != result.reason_details[reason]["right"]


def test_quality_ranking_requires_identical_method_and_denominator():
    from scripts.testing.final_results.comparison import classify_comparability

    left = _bundle("left")
    right = _bundle("right")
    assert classify_comparability(left, right, "quality").classification == "direct"

    mutations = {
        "prompt_set_mismatch": _bundle("right", prompt_ids=("Q01", "Q03")),
        "rubric_mismatch": _bundle("right", rubric="legacy-rubric"),
        "scoring_version_mismatch": _bundle("right", scoring_version="v2"),
        "denominator_mismatch": _bundle("right", maximum_score=5.0),
        "aggregation_mismatch": _bundle("right", quality_aggregation="arithmetic mean of prompt scores"),
    }
    for reason, changed in mutations.items():
        result = classify_comparability(left, changed, "quality")
        assert result.classification == "descriptive_only", reason
        assert reason in result.reasons


def test_openvino_v3_and_legacy_llama_quality_are_descriptive_only():
    from scripts.testing.final_results.comparison import classify_comparability
    from scripts.testing.final_results.llama_adapter import build_upstream_llama_bundle

    openvino = build_experimental_bundle(REPOSITORY_ROOT)
    llama = build_upstream_llama_bundle(REPOSITORY_ROOT)
    result = classify_comparability(openvino, llama, "quality")

    assert result.classification == "descriptive_only"
    assert {"prompt_set_mismatch", "rubric_mismatch", "scoring_version_mismatch", "denominator_mismatch"}.issubset(result.reasons)


def test_absent_metric_evidence_is_not_comparable_with_explicit_reason():
    from scripts.testing.final_results.comparison import classify_comparability

    result = classify_comparability(_bundle("left"), _bundle("blocked", status=Status.BLOCKED), "quality")
    assert result.classification == "not_comparable"
    assert result.reasons == ("right_metric_evidence_missing",)


def test_report_preserves_statuses_and_forbids_universal_or_incompatible_ranking():
    from scripts.testing.final_results.comparison import build_cross_route_report
    from scripts.testing.final_results.llama_adapter import (
        build_animehacker_bundle,
        build_atomicbot_bundle,
        build_upstream_llama_bundle,
    )

    bundles = (
        build_upstream_llama_bundle(REPOSITORY_ROOT),
        build_atomicbot_bundle(REPOSITORY_ROOT),
        build_animehacker_bundle(REPOSITORY_ROOT),
        build_experimental_bundle(REPOSITORY_ROOT),
        build_official_bundle(REPOSITORY_ROOT),
    )
    report = build_cross_route_report(bundles)
    tables = _tables(report)
    text = _text(report).casefold()

    assert not any("ranking" in table.title.casefold() for table in tables.values())
    assert "best repository:" not in text
    assert "no universal ranking" in text
    assert "incompatible quality scores are not ranked" in text
    assert len(tables["ST-01"].rows) == sum(len(bundle.attempts) for bundle in bundles)
    assert {row[4] for row in tables["ST-01"].rows}.issuperset({"Passed", "Failed", "Blocked", "Artifact unavailable"})
    openvino_quality = next(
        row for row in tables["CP-01"].rows
        if {row[0], row[1]} == {"openvino-experimental-fork", "openvino-official-upstream"}
        and row[2] == "quality"
    )
    assert openvino_quality[3] == "direct"
    legacy_quality_rows = [
        row for row in tables["CP-01"].rows
        if row[2] == "quality" and "openvino" in row[0] + row[1] and "llama" in row[0] + row[1]
    ]
    assert legacy_quality_rows
    assert {row[3] for row in legacy_quality_rows} == {"descriptive_only"}


def test_catalog_rows_keep_every_attempt_and_machine_readable_comparison_reasons():
    from scripts.testing.final_results.comparison import build_catalogs

    bundles = (
        _bundle("passed"),
        _bundle("failed", status=Status.FAILED),
        _bundle("blocked", status=Status.BLOCKED),
    )
    catalogs = build_catalogs(bundles)

    assert {row["status"] for row in catalogs["campaign-summary.csv"]} == {"passed", "failed", "blocked"}
    assert len(catalogs["campaign-summary.csv"]) == 3
    comparison = catalogs["comparability-matrix.csv"]
    assert comparison
    assert all(json.loads(row["reason_codes_json"]) for row in comparison)
    assert all(json.loads(row["reason_details_json"]) is not None for row in comparison)


def test_package_writer_creates_portable_parity_valid_artifacts_and_catalogs(tmp_path):
    from scripts.testing.final_results.comparison import write_cross_route_package

    output = tmp_path / "docs/testing/final-results"
    bundles = (_bundle("left"), _bundle("right"), _bundle("failed", status=Status.FAILED))
    report = write_cross_route_package(tmp_path, bundles, output_root=output)
    route = output / "06-cross-route-comparison"

    assert report.route_id == "cross-route-comparison"
    expected = (
        route / "route-manifest.json",
        route / "results/comparability-matrix.csv",
        route / "results/route-status-summary.csv",
        route / "workbook/source/cross-route-comparison-final-report.md",
        route / "workbook/generated/cross-route-comparison-final-report.docx",
        route / "validation/workbook-parity.json",
        route / "validation/integrity-validation.json",
        route / "evidence/manifest-sha256.txt",
        output / "catalog/route-register.csv",
        output / "catalog/campaign-summary.csv",
        output / "catalog/performance-summary.csv",
        output / "catalog/quality-summary.csv",
        output / "catalog/failure-summary.csv",
        output / "catalog/evidence-manifest.csv",
        output / "catalog/claim-evidence-map.csv",
        output / "catalog/comparability-matrix.csv",
    )
    assert all(path.is_file() for path in expected)
    assert json.loads((route / "validation/workbook-parity.json").read_text())["matches"] is True
    manifest_lines = (route / "evidence/manifest-sha256.txt").read_text(encoding="utf-8").splitlines()
    assert len(manifest_lines) == len([path for path in route.rglob("*") if path.is_file()]) - 1
    with (output / "catalog/campaign-summary.csv").open(encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle))
    assert {row["status"] for row in rows} == {"passed", "failed"}
    forbidden = ("C:\\Users\\", "C:/Users/", str(REPOSITORY_ROOT))
    for path in output.rglob("*"):
        if path.is_file() and path.suffix.lower() not in {".docx", ".pdf"}:
            content = path.read_text(encoding="utf-8", errors="ignore")
            assert not any(value in content for value in forbidden), path
