import csv
import dataclasses
import json
import sys
from pathlib import Path

import pytest


sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.reporting.models import (
    AttemptRecord,
    MeasurementRecord,
    QualityRecord,
    RouteBundle,
    Status,
    SummaryRecord,
)
from scripts.testing.reporting.openvino_adapter import (
    build_experimental_bundle,
    build_official_bundle,
)
from scripts.testing.reporting.report_model import (
    ReportNote,
    ReportParagraph,
    ReportSection,
    ReportTable,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


def _bundle(
    route_id: str,
    *,
    case_id: str = "case-1",
    model: str = "granite-3b",
    backend: str = "openvino-cpu",
    input_tokens: int = 24,
    output_tokens: int = 32,
    metric_name: str = "generation_tokens_per_second",
    unit: str = "tokens_per_second",
    aggregation: str = "median over three executed benchmark repetitions",
    prompt_ids: tuple[str, ...] = ("Q01", "Q02"),
    prompt_suite: str | None = "suite-v3",
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
        test_case_id=case_id,
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
                test_case_id=case_id,
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
                test_case_id=case_id,
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
                test_case_id=case_id,
                quality_id=f"{route_id}-{prompt_id}",
                prompt_id=prompt_id,
                criterion_id="criterion",
                score=8.0,
                maximum_score=maximum_score,
                prompt_suite_id=prompt_suite,
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
    from scripts.testing.reporting.comparison import classify_comparability

    left = _bundle("left")
    right = _bundle("right")
    result = classify_comparability(left, right, "generation_tokens_per_second")
    assert result.classification == "direct"
    assert result.reasons == ("all_required_dimensions_match",)
    assert result.matched_case_ids == ("case-1",)
    assert result.matched_case_count == 1
    assert result.left_only_case_ids == ()
    assert result.right_only_case_ids == ()
    assert result.signature_mismatches == ()

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
    expected_fields = {
        "model_mismatch": "model_identity",
        "input_length_mismatch": "input_length",
        "output_length_mismatch": "output_length",
        "backend_class_mismatch": "backend_class",
        "repetition_treatment_mismatch": "repetition_treatment",
        "metric_definition_mismatch": "metric_definition",
    }
    for reason, changed in mutations.items():
        result = classify_comparability(left, changed, "generation_tokens_per_second")
        assert result.classification == expected_classifications[reason], reason
        assert reason in result.reasons
        mismatch = next(row for row in result.signature_mismatches if row["field"] == expected_fields[reason])
        assert mismatch["case_id"] == "case-1"
        assert mismatch["left"] != mismatch["right"]


def test_keyed_throughput_comparison_rejects_relationship_preserving_set_swap():
    from scripts.testing.reporting.comparison import classify_comparability

    def combine(route_id: str, first: RouteBundle, second: RouteBundle) -> RouteBundle:
        return RouteBundle(
            route_id=route_id,
            campaign_id=f"{route_id}-campaign",
            attempts=first.attempts + second.attempts,
            measurements=first.measurements + second.measurements,
            summaries=first.summaries + second.summaries,
            quality=first.quality + second.quality,
            repository=first.repository,
        )

    left = combine("left", _bundle("left", case_id="case-a", input_tokens=24), _bundle("left", case_id="case-b", input_tokens=48))
    right = combine("right", _bundle("right", case_id="case-a", input_tokens=48), _bundle("right", case_id="case-b", input_tokens=24))
    result = classify_comparability(left, right, "generation_tokens_per_second")

    assert result.classification == "normalized_with_caveat"
    assert result.matched_case_ids == ("case-a", "case-b")
    assert [(row["case_id"], row["field"]) for row in result.signature_mismatches] == [
        ("case-a", "input_length"),
        ("case-b", "input_length"),
    ]


def test_throughput_preserves_repetition_keyed_token_relationships():
    from scripts.testing.reporting.comparison import classify_comparability

    left = _bundle("left")
    right = _bundle("right")
    left = dataclasses.replace(
        left,
        measurements=tuple(
            dataclasses.replace(row, input_tokens=value)
            for row, value in zip(left.measurements, (24, 48, 24), strict=True)
        ),
    )
    right = dataclasses.replace(
        right,
        measurements=tuple(
            dataclasses.replace(row, input_tokens=value)
            for row, value in zip(right.measurements, (48, 24, 24), strict=True)
        ),
    )

    result = classify_comparability(left, right, "generation_tokens_per_second")

    assert result.classification == "normalized_with_caveat"
    assert "input_length_mismatch" in result.reasons
    mismatch = next(row for row in result.signature_mismatches if row["field"] == "input_length")
    assert mismatch["left"] == {
        "summary-001/source-001/repetition-1": 24,
        "summary-001/source-002/repetition-2": 48,
        "summary-001/source-003/repetition-3": 24,
    }
    assert mismatch["right"] == {
        "summary-001/source-001/repetition-1": 48,
        "summary-001/source-002/repetition-2": 24,
        "summary-001/source-003/repetition-3": 24,
    }


def test_throughput_rejects_one_missing_required_repetition_token_value():
    from scripts.testing.reporting.comparison import classify_comparability

    left = _bundle("left")
    right = _bundle("right")
    right = dataclasses.replace(
        right,
        measurements=tuple(
            dataclasses.replace(row, input_tokens=None) if row.repetition_id == "2" else row
            for row in right.measurements
        ),
    )

    result = classify_comparability(left, right, "generation_tokens_per_second")

    assert result.classification == "not_comparable"
    assert "input_length_missing" in result.reasons
    mismatch = next(row for row in result.signature_mismatches if row["field"] == "input_length")
    assert mismatch["right"]["summary-001/source-002/repetition-2"] == {"status": "missing"}


def test_quality_ranking_requires_identical_method_and_denominator():
    from scripts.testing.reporting.comparison import classify_comparability

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


def test_quality_comparison_preserves_prompt_row_relationships_and_suite_identity():
    from scripts.testing.reporting.comparison import classify_comparability

    left = _bundle("left")
    left = dataclasses.replace(
        left,
        quality=(
            dataclasses.replace(left.quality[0], scoring_version="score-a"),
            dataclasses.replace(left.quality[1], scoring_version="score-b"),
        ),
    )
    right = _bundle("right")
    right = dataclasses.replace(
        right,
        quality=(
            dataclasses.replace(right.quality[0], scoring_version="score-b"),
            dataclasses.replace(right.quality[1], scoring_version="score-a"),
        ),
    )

    swapped = classify_comparability(left, right, "quality")
    assert swapped.classification == "descriptive_only"
    assert [(row["row_id"], row["field"]) for row in swapped.signature_mismatches] == [
        ("Q01::criterion", "scoring_version"),
        ("Q02::criterion", "scoring_version"),
    ]

    changed_suite = classify_comparability(_bundle("left"), _bundle("right", prompt_suite="suite-v4"), "quality")
    assert changed_suite.classification == "descriptive_only"
    assert "prompt_suite_mismatch" in changed_suite.reasons


def test_missing_required_quality_metadata_on_both_sides_is_not_direct():
    from scripts.testing.reporting.comparison import classify_comparability

    left = _bundle("left", prompt_suite=None)
    right = _bundle("right", prompt_suite=None)
    result = classify_comparability(left, right, "quality")

    assert result.classification == "not_comparable"
    assert "prompt_suite_missing" in result.reasons
    missing = next(row for row in result.signature_mismatches if row["field"] == "prompt_suite")
    assert missing["left"]["status"] == "missing"
    assert missing["right"]["status"] == "missing"


def test_missing_required_quality_metadata_without_shared_cases_is_not_comparable():
    from scripts.testing.reporting.comparison import classify_comparability

    left = _bundle("left", case_id="case-left", prompt_suite=None)
    right = _bundle("right", case_id="case-right", prompt_suite=None)
    result = classify_comparability(left, right, "quality")

    assert result.classification == "not_comparable"
    assert "prompt_suite_missing" in result.reasons
    missing = next(row for row in result.signature_mismatches if row["field"] == "prompt_suite")
    assert missing["left"]["status"] == "missing"
    assert missing["right"]["status"] == "missing"


def test_disjoint_quality_routes_reject_missing_identity_and_row_dimensions():
    from scripts.testing.reporting.comparison import classify_comparability

    def remove_required_identity(bundle: RouteBundle) -> RouteBundle:
        return dataclasses.replace(
            bundle,
            attempts=(dataclasses.replace(bundle.attempts[0], model_id=None, backend_id=None),),
            quality=tuple(dataclasses.replace(row, prompt_id=None, criterion_id=None) for row in bundle.quality),
        )

    result = classify_comparability(
        remove_required_identity(_bundle("left", case_id="case-left")),
        remove_required_identity(_bundle("right", case_id="case-right")),
        "quality",
    )

    assert result.classification == "not_comparable"
    assert {
        "model_identity_missing", "backend_class_missing", "prompt_set_missing", "criterion_id_missing"
    }.issubset(result.reasons)


def test_openvino_v3_and_legacy_llama_quality_are_descriptive_only():
    from scripts.testing.reporting.comparison import classify_comparability
    from scripts.testing.reporting.llama_adapter import build_upstream_llama_bundle

    openvino = build_experimental_bundle(REPOSITORY_ROOT)
    llama = build_upstream_llama_bundle(REPOSITORY_ROOT)
    result = classify_comparability(openvino, llama, "quality")

    assert result.classification == "descriptive_only"
    assert {"prompt_set_mismatch", "rubric_mismatch", "scoring_version_mismatch", "denominator_mismatch"}.issubset(result.reasons)


def test_real_openvino_scope_is_fifteen_matched_and_twelve_experimental_only():
    from scripts.testing.reporting.comparison import classify_comparability

    experimental = build_experimental_bundle(REPOSITORY_ROOT)
    official = build_official_bundle(REPOSITORY_ROOT)
    for metric in ("generation_tokens_per_second", "quality"):
        result = classify_comparability(experimental, official, metric)
        assert result.classification == "direct"
        assert result.matched_case_count == 15
        assert result.left_only_case_count == 12
        assert result.right_only_case_count == 0
        assert result.signature_mismatches == ()


def test_absent_metric_evidence_is_not_comparable_with_explicit_reason():
    from scripts.testing.reporting.comparison import classify_comparability

    result = classify_comparability(_bundle("left"), _bundle("blocked", status=Status.BLOCKED), "quality")
    assert result.classification == "not_comparable"
    assert result.reasons == ("right_metric_evidence_missing",)
    assert result.reason_details["right_metric_evidence_missing"] == {
        "left": {"status": "present"},
        "right": {"status": "missing"},
    }

    both = classify_comparability(_bundle("left", status=Status.BLOCKED), _bundle("right", status=Status.BLOCKED), "quality")
    assert both.reasons == ("left_metric_evidence_missing", "right_metric_evidence_missing")
    assert both.reason_details == {
        "left_metric_evidence_missing": {"left": {"status": "missing"}, "right": {"status": "missing"}},
        "right_metric_evidence_missing": {"left": {"status": "missing"}, "right": {"status": "missing"}},
    }


def test_report_preserves_statuses_and_forbids_universal_or_incompatible_ranking():
    from scripts.testing.reporting.comparison import build_cross_route_report
    from scripts.testing.reporting.llama_adapter import (
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
    assert openvino_quality[4:7] == ("15", "12", "0")
    assert len(tables["CP-01"].columns) == 8
    assert max(len(row[7]) for row in tables["CP-01"].rows) <= 72
    assert any("machine-readable" in note.casefold() for note in tables["CP-01"].footnotes)
    assert "SC-01" not in tables
    legacy_quality_rows = [
        row for row in tables["CP-01"].rows
        if row[2] == "quality" and "openvino" in row[0] + row[1] and "llama" in row[0] + row[1]
    ]
    assert legacy_quality_rows
    assert {row[3] for row in legacy_quality_rows} == {"descriptive_only"}


def test_catalog_rows_keep_every_attempt_and_machine_readable_comparison_reasons():
    from scripts.testing.reporting.comparison import build_catalogs

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
    assert all(int(row["matched_case_count"]) == len(json.loads(row["matched_case_ids_json"])) for row in comparison)
    assert all(int(row["left_only_case_count"]) == len(json.loads(row["left_only_case_ids_json"])) for row in comparison)
    assert all(int(row["right_only_case_count"]) == len(json.loads(row["right_only_case_ids_json"])) for row in comparison)
    assert all(isinstance(json.loads(row["signature_mismatches_json"]), list) for row in comparison)


def test_cross_route_validation_is_derived_from_report_and_catalog_content():
    from scripts.testing.reporting.comparison import build_cross_route_report, build_cross_route_validation, build_catalogs

    bundles = (_bundle("left"), _bundle("right"))
    report = build_cross_route_report(bundles)
    catalogs = build_catalogs(bundles)
    valid = build_cross_route_validation(report, catalogs, bundles)
    assert valid["valid"] is True
    assert valid["universal_ranking_present"] is False
    assert valid["incompatible_quality_ranking_present"] is False
    assert valid["complete_attempt_accounting"] is True

    ranking = ReportTable(
        table_id="BAD-RANK",
        title="Universal repository ranking",
        columns=("Rank", "Repository", "Score"),
        rows=(("1", "left", "100"),),
    )
    changed_sections = report.sections + (ReportSection("Unsupported appendix", (ranking,)),)
    changed_report = dataclasses.replace(report, sections=changed_sections)
    invalid = build_cross_route_validation(changed_report, catalogs, bundles)
    assert invalid["valid"] is False
    assert invalid["universal_ranking_present"] is True

    dropped = {name: list(rows) for name, rows in catalogs.items()}
    dropped["campaign-summary.csv"] = dropped["campaign-summary.csv"][:-1]
    incomplete = build_cross_route_validation(report, dropped, bundles)
    assert incomplete["valid"] is False
    assert incomplete["complete_attempt_accounting"] is False

    changed_sections = tuple(
        dataclasses.replace(
            section,
            blocks=tuple(
                dataclasses.replace(block, rows=block.rows[:-1])
                if isinstance(block, ReportTable) and block.table_id == "CP-01"
                else block
                for block in section.blocks
            ),
        )
        for section in report.sections
    )
    incomplete_report = build_cross_route_validation(dataclasses.replace(report, sections=changed_sections), catalogs, bundles)
    assert incomplete_report["valid"] is False
    assert incomplete_report["report_comparison_matrix_matches_catalog"] is False

    invented = {name: list(rows) for name, rows in catalogs.items()}
    invented_row = dict(invented["comparability-matrix.csv"][0])
    invented_row["matched_case_ids_json"] = json.dumps(["invented-case"])
    invented_row["matched_case_count"] = 1
    invented["comparability-matrix.csv"][0] = invented_row
    invented_report_sections = tuple(
        dataclasses.replace(
            section,
            blocks=tuple(
                dataclasses.replace(
                    block,
                    rows=(
                        (
                            invented_row["left_route_id"], invented_row["right_route_id"],
                            invented_row["metric"], invented_row["classification"],
                            str(invented_row["matched_case_count"]), str(invented_row["left_only_case_count"]),
                            str(invented_row["right_only_case_count"]), block.rows[0][7],
                        ),
                        *block.rows[1:],
                    ),
                )
                if isinstance(block, ReportTable) and block.table_id == "CP-01"
                else block
                for block in section.blocks
            ),
        )
        for section in report.sections
    )
    synchronized_false = build_cross_route_validation(
        dataclasses.replace(report, sections=invented_report_sections), invented, bundles
    )
    assert synchronized_false["valid"] is False
    assert synchronized_false["comparison_catalog_matches_bundles"] is False

    leaderboard = ReportTable(
        table_id="QUALITY-LEADERBOARD",
        title="Quality leaderboard",
        columns=("Position", "Route", "Quality"),
        rows=(("1", "left", "10"),),
    )
    incompatible_bundles = (_bundle("left"), _bundle("right", prompt_suite="different-suite"))
    incompatible_report = build_cross_route_report(incompatible_bundles)
    incompatible_catalogs = build_catalogs(incompatible_bundles)
    leaderboard_report = dataclasses.replace(
        incompatible_report,
        sections=incompatible_report.sections + (ReportSection("Unsupported leaderboard", (leaderboard,)),),
    )
    leaderboard_validation = build_cross_route_validation(
        leaderboard_report, incompatible_catalogs, incompatible_bundles
    )
    assert leaderboard_validation["valid"] is False
    assert leaderboard_validation["universal_ranking_present"] is True
    assert leaderboard_validation["incompatible_quality_ranking_present"] is True

    prose_ranking = ReportParagraph("Repository ranking: 1. left, 2. right, using incompatible quality scores.")
    prose_report = dataclasses.replace(
        incompatible_report,
        sections=incompatible_report.sections + (ReportSection("Unsupported prose", (prose_ranking,)),),
    )
    prose_validation = build_cross_route_validation(prose_report, incompatible_catalogs, incompatible_bundles)
    assert prose_validation["valid"] is False
    assert prose_validation["universal_ranking_present"] is True
    assert prose_validation["incompatible_quality_ranking_present"] is True

    ranking_phrasings = (
        "No universal ranking is supported. Repository ranking: 1. left, 2. right by incompatible quality score.",
        "The left route is the best repository by incompatible quality score; the right route is second.",
        "Although a universal leaderboard is unsupported, left ranks first and right ranks second on incompatible quality.",
        "By incompatible quality score, the left route places ahead of the right route.",
        "Left has higher quality than right.",
        "Left achieved the highest quality score.",
        "Left is better than right by incompatible quality score.",
        "Left is superior to right on incompatible quality.",
        "No universal ranking is supported, although left is better than right by incompatible quality score.",
        "The evidence does not support a universal ranking, although left has higher quality than right.",
        "The evidence does not support deployment, and left is better than right by incompatible quality score.",
        "The evidence does not support deployment and left has higher quality than right.",
        "Left takes first place and right takes second place.",
        "Left is number one for quality.",
        "No universal ranking is supported;left ranks first.",
        "No universal ranking is supported.Repository ranking: left first.",
        "Left takes 1st place; right takes 2nd place.",
        "Left is number 1 for quality.",
        "Left ranks number one and right ranks number two.",
    )
    for wording in ranking_phrasings:
        changed = dataclasses.replace(
            incompatible_report,
            sections=incompatible_report.sections + (
                ReportSection("Unsupported prose variant", (ReportParagraph(wording),)),
            ),
        )
        validation = build_cross_route_validation(changed, incompatible_catalogs, incompatible_bundles)
        assert validation["valid"] is False, wording
        assert validation["universal_ranking_present"] is True, wording
        assert validation["incompatible_quality_ranking_present"] is True, wording

    nonranking_disclaimers = (
        "The evidence does not support any conclusion that the left route, based on current incompatible quality evidence, is the best repository.",
        "Neither route can be described, from these incompatible quality scores, as better or superior to the other.",
        "The evidence does not support ranking and does not establish that left is better than right.",
        "The evidence does not establish whether left is better than right or right is better than left.",
        "The evidence does not establish that left is better than right and right is worse than left.",
        "Left isn't better than right, and right isn't worse than left.",
        "Left isn’t better than right, and right isn’t worse than left.",
        "Left is neither better nor worse than right.",
        "Neither left nor right is number one for quality.",
    )
    for wording in nonranking_disclaimers:
        changed = dataclasses.replace(
            incompatible_report,
            sections=incompatible_report.sections + (
                ReportSection("Permitted disclaimer variant", (ReportParagraph(wording),)),
            ),
        )
        validation = build_cross_route_validation(changed, incompatible_catalogs, incompatible_bundles)
        assert validation["valid"] is True, wording
        assert validation["universal_ranking_present"] is False, wording
        assert validation["incompatible_quality_ranking_present"] is False, wording


@pytest.mark.parametrize(
    "wording",
    (
        "Left takes first place and right takes second place.",
        "Left is number one for quality.",
        "No universal ranking is supported;left ranks first.",
        "No universal ranking is supported.Repository ranking: left first.",
    ),
)
def test_ranking_prose_gate_rejects_controller_probes(wording):
    from scripts.testing.reporting.comparison import (
        build_catalogs,
        build_cross_route_report,
        build_cross_route_validation,
    )

    bundles = (_bundle("left"), _bundle("right", prompt_suite="different-suite"))
    report = build_cross_route_report(bundles)
    changed = dataclasses.replace(
        report,
        sections=report.sections + (ReportSection("Unsupported prose", (ReportParagraph(wording),)),),
    )

    validation = build_cross_route_validation(changed, build_catalogs(bundles), bundles)

    assert validation["valid"] is False
    assert validation["universal_ranking_present"] is True
    assert validation["incompatible_quality_ranking_present"] is True


@pytest.mark.parametrize(
    "wording",
    (
        "Left ranks 1st and right ranks 2nd.",
        "Left placed 1st and right placed 2nd.",
        "Left is in first place for quality.",
        "Left is number-one for quality.",
        "Left ranked 3rd and right ranked 4th.",
        "Left placed 3rd and right placed 4th.",
        "Left ranks 4th.",
        "Left is in 1st place for quality.",
        "Left is in 2nd place for quality.",
        "Left is in 3rd place for quality.",
        "Left is in 4th place for quality.",
        "Left is in third place for quality.",
        "Left is number one for quality.",
    ),
)
def test_ranking_prose_gate_rejects_ordinal_controller_probes(wording):
    from scripts.testing.reporting.comparison import (
        build_catalogs,
        build_cross_route_report,
        build_cross_route_validation,
    )

    bundles = (_bundle("left"), _bundle("right", prompt_suite="different-suite"))
    report = build_cross_route_report(bundles)
    changed = dataclasses.replace(
        report,
        sections=report.sections + (ReportSection("Unsupported prose", (ReportParagraph(wording),)),),
    )

    validation = build_cross_route_validation(changed, build_catalogs(bundles), bundles)

    assert validation["valid"] is False
    assert validation["universal_ranking_present"] is True
    assert validation["incompatible_quality_ranking_present"] is True


@pytest.mark.parametrize("entity", ("route", "repository", "model", "configuration"))
@pytest.mark.parametrize("copula", ("is", "was"))
@pytest.mark.parametrize("ordinal", ("first", "second", "third", "fourth"))
def test_ranking_prose_gate_rejects_copular_entity_ordinals(entity, copula, ordinal):
    from scripts.testing.reporting.comparison import (
        build_catalogs,
        build_cross_route_report,
        build_cross_route_validation,
    )

    bundles = (_bundle("left"), _bundle("right", prompt_suite="different-suite"))
    report = build_cross_route_report(bundles)
    changed = dataclasses.replace(
        report,
        sections=report.sections + (
            ReportSection(
                "Unsupported prose",
                (ReportParagraph(f"The {entity} {copula} {ordinal} for quality."),),
            ),
        ),
    )

    validation = build_cross_route_validation(changed, build_catalogs(bundles), bundles)

    assert validation["valid"] is False
    assert validation["universal_ranking_present"] is True
    assert validation["incompatible_quality_ranking_present"] is True


@pytest.mark.parametrize(
    "ordinal",
    ("first", "second", "third", "fourth", "1st", "2nd", "3rd", "4th"),
)
def test_ranking_prose_gate_rejects_placed_in_ordinal_place(ordinal):
    from scripts.testing.reporting.comparison import (
        build_catalogs,
        build_cross_route_report,
        build_cross_route_validation,
    )

    bundles = (_bundle("left"), _bundle("right", prompt_suite="different-suite"))
    report = build_cross_route_report(bundles)
    changed = dataclasses.replace(
        report,
        sections=report.sections + (
            ReportSection(
                "Unsupported prose",
                (ReportParagraph(f"The route placed in {ordinal} place for quality."),),
            ),
        ),
    )

    validation = build_cross_route_validation(changed, build_catalogs(bundles), bundles)

    assert validation["valid"] is False
    assert validation["universal_ranking_present"] is True
    assert validation["incompatible_quality_ranking_present"] is True


@pytest.mark.parametrize(
    "wording",
    (
        "Left isn't better than right, and right isn't worse than left.",
        "Left is neither better nor worse than right.",
        "The route is not fourth for quality.",
        "The repository wasn't fourth for quality.",
        "The configuration was not placed in first place for quality.",
        "The model wasn't placed in 1st place for quality.",
    ),
)
def test_ranking_prose_gate_accepts_controller_negations(wording):
    from scripts.testing.reporting.comparison import (
        build_catalogs,
        build_cross_route_report,
        build_cross_route_validation,
    )

    bundles = (_bundle("left"), _bundle("right", prompt_suite="different-suite"))
    report = build_cross_route_report(bundles)
    changed = dataclasses.replace(
        report,
        sections=report.sections + (ReportSection("Permitted prose", (ReportParagraph(wording),)),),
    )

    validation = build_cross_route_validation(changed, build_catalogs(bundles), bundles)

    assert validation["valid"] is True
    assert validation["universal_ranking_present"] is False
    assert validation["incompatible_quality_ranking_present"] is False


def test_package_writer_creates_portable_parity_valid_artifacts_and_catalogs(tmp_path):
    from scripts.testing.reporting.comparison import write_cross_route_package

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


def test_pdf_finalizer_reports_only_automated_checks_and_manual_qa_is_separate(tmp_path):
    import pytest
    import shutil

    from scripts.testing.reporting.comparison import (
        finalize_cross_route_package,
        record_cross_route_manual_visual_qa,
    )
    output = tmp_path / "docs/testing/final-results"
    route = output / "06-cross-route-comparison"
    pdf_path = route / "workbook/generated/cross-route-comparison-final-report.pdf"
    pdf_path.parent.mkdir(parents=True)
    shutil.copyfile(
        REPOSITORY_ROOT / "docs/testing/final-results/06-cross-route-comparison/workbook/generated/cross-route-comparison-final-report.pdf",
        pdf_path,
    )

    automated = finalize_cross_route_package(tmp_path, output_root=output)
    assert automated["valid"] is True
    page_count = automated["automated_rendering"]["rendered_page_count"]
    assert page_count >= 4
    assert automated["manual_visual_qa_performed"] is False
    assert "inspected_pages" not in automated
    assert "inspected" not in json.dumps(automated).casefold()
    report = (route / "validation/validation-report.md").read_text(encoding="utf-8")
    assert "Automated PDF rendering and structural checks: Passed" in report
    assert "Manual full-page visual QA: Pending" in report

    with pytest.raises(ValueError, match="all PDF pages"):
        record_cross_route_manual_visual_qa(tmp_path, tuple(range(1, page_count)), "Incomplete", output_root=output)

    manual = record_cross_route_manual_visual_qa(
        tmp_path,
        tuple(range(1, page_count + 1)),
        "No blank, clipped, overlapping, corrupt, or truncated content observed.",
        output_root=output,
    )
    assert manual["valid"] is True
    assert manual["inspected_pages"] == list(range(1, page_count + 1))
    assert "Manual full-page visual QA: Passed" in (route / "validation/validation-report.md").read_text(encoding="utf-8")
