import dataclasses
import json
import sys
from pathlib import Path


sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.final_results.models import Status
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
    from scripts.testing.final_results import openvino_report

    return openvino_report


def _text(report) -> str:
    parts = [report.title, report.route_id, report.revision, *report.evidence_ids]
    for section in report.sections:
        parts.append(section.title)
        for block in section.blocks:
            if isinstance(block, (ReportParagraph, ReportNote)):
                parts.append(block.text)
            elif isinstance(block, ReportTable):
                parts.extend((block.table_id, block.title, block.subtitle, *block.columns))
                parts.extend(cell for row in block.rows for cell in row)
                parts.extend(block.footnotes)
    return "\n".join(parts)


def _tables(report) -> dict[str, ReportTable]:
    return {
        block.table_id: block
        for section in report.sections
        for block in section.blocks
        if isinstance(block, ReportTable)
    }


def _local_repo_paths(report) -> set[str]:
    roots = ("docs/", "experiments/", "outputs/")
    candidates: set[str] = set()
    for value in _text(report).split():
        candidate = value.rstrip(".,;:")
        if candidate.startswith(roots):
            candidates.add(candidate)
    return candidates


def test_reports_follow_the_approved_sections_and_derive_campaign_accounting():
    build = _module().build_openvino_report

    for bundle, expected in (
        (build_experimental_bundle(REPOSITORY_ROOT), {"Passed": 27, "Artifact unavailable": 54}),
        (build_official_bundle(REPOSITORY_ROOT), {"Passed": 15, "Failed": 5, "Blocked": 25}),
    ):
        report = build(bundle)
        tables = _tables(report)

        assert [section.title for section in report.sections] == EXPECTED_SECTIONS
        assert bundle.route_id in _text(report)
        assert bundle.campaign_id in _text(report)
        assert len(bundle.attempts) == sum(expected.values())
        assert {
            row[0]: int(row[1]) for row in tables["AC-01"].rows
        } == expected
        assert len(tables["AT-01"].rows) == len(bundle.attempts)
        matrix_members = {row[0]: row[1] for row in tables["MX-01"].rows}
        assert matrix_members["Models"] == "granite-30b, granite-3b, granite-8b"
        assert matrix_members["Weight formats"] == "fp16, int4, int8"
        assert report.generated_date.isoformat() == bundle.repository["source_date"]
        assert report.revision == "R1"


def test_status_language_keeps_official_blocks_and_experimental_unavailability_distinct():
    build = _module().build_openvino_report
    experimental = build(build_experimental_bundle(REPOSITORY_ROOT))
    official = build(build_official_bundle(REPOSITORY_ROOT))

    experimental_attempts = _tables(experimental)["AT-01"].rows
    official_attempts = _tables(official)["AT-01"].rows

    assert {row[5] for row in experimental_attempts} == {"Passed", "Artifact unavailable"}
    assert "Failed" not in {row[5] for row in experimental_attempts}
    assert "Blocked" not in {row[5] for row in experimental_attempts}
    assert {row[5] for row in official_attempts} == {"Passed", "Failed", "Blocked"}
    assert "Artifact unavailable" not in {row[5] for row in official_attempts}
    assert "Unavailable" not in {row[5] for row in official_attempts}
    assert "hardware preflight" in _text(official).casefold()
    assert "model artifact" in _text(experimental).casefold()


def test_performance_and_quality_values_are_recomputed_from_bundle_records():
    module = _module()
    bundle = build_experimental_bundle(REPOSITORY_ROOT)
    report = module.build_openvino_report(bundle)
    tables = _tables(report)
    case_id = next(attempt.test_case_id for attempt in bundle.attempts if attempt.status is Status.PASSED)
    original_summary = next(
        summary
        for summary in bundle.summaries
        if summary.test_case_id == case_id
        and summary.metric_name == "generation_tokens_per_second"
    )
    changed_value = float(original_summary.value) + 1.234567
    changed_summaries = tuple(
        dataclasses.replace(summary, value=changed_value)
        if summary.summary_id == original_summary.summary_id
        else summary
        for summary in bundle.summaries
    )
    changed_bundle = dataclasses.replace(bundle, summaries=changed_summaries)
    changed_report = module.build_openvino_report(changed_bundle)

    original_row = next(row for row in tables["PF-01"].rows if row[0] == case_id)
    changed_row = next(row for row in _tables(changed_report)["PF-01"].rows if row[0] == case_id)
    assert original_row[4] == f"{float(original_summary.value):.6f}"
    assert changed_row[4] == f"{changed_value:.6f}"
    assert changed_row[4] != original_row[4]

    quality_rows = [record for record in bundle.quality if record.test_case_id == case_id]
    expected_quality = 10 * sum(float(record.score) for record in quality_rows) / sum(
        float(record.maximum_score) for record in quality_rows
    )
    quality_row = next(row for row in tables["QS-01"].rows if row[0] == case_id)
    assert quality_row[4] == f"{expected_quality:.6f}"
    assert int(quality_row[5]) == len({record.prompt_id for record in quality_rows})


def test_quality_methodology_exposes_weighted_small_increment_scoring_and_sector_coverage():
    report = _module().build_openvino_report(build_official_bundle(REPOSITORY_ROOT))
    tables = _tables(report)
    text = _text(report)

    assert "48 distinct prompts" in text
    assert "arithmetic mean" in text
    assert "objective" in text.casefold()
    assert "output-health gate" in text.casefold()
    assert {row[0]: int(row[1]) for row in tables["QD-01"].rows} == {
        "Education": 16,
        "General": 8,
        "Healthcare": 16,
        "Statistics": 8,
    }
    assert {row[0]: int(row[1]) for row in tables["QL-01"].rows} == {
        "Long": 16,
        "Medium": 16,
        "Short": 16,
    }
    assert {(row[0], row[1]) for row in tables["QC-01"].rows} == {
        ("factuality:primary", "5"),
        ("numerical_accuracy:primary", "5"),
        ("safety:primary", "5"),
        ("fact_retention:secondary", "3"),
        ("instruction_following:supporting", "2"),
    }
    assert "healthcare" in text.casefold()
    assert "education" in text.casefold()
    assert "short, medium, and long" in text.casefold()


def test_report_states_aggregation_comparison_boundaries_evidence_and_revision():
    report = _module().build_openvino_report(build_official_bundle(REPOSITORY_ROOT))
    tables = _tables(report)
    text = _text(report)

    assert "median over three executed benchmark repetitions" in text
    assert "value from the repetition selected by median decode throughput" in text
    assert "maximum over three executed benchmark repetitions" in text
    assert "same model, weight format, cache format, backend, prompt suite" in text.casefold()
    assert "descriptive" in text.casefold()
    assert len(tables["EV-01"].rows) == len(build_official_bundle(REPOSITORY_ROOT).evidence)
    assert all(len(row[3]) == 64 for row in tables["EV-01"].rows)
    assert tables["RV-01"].rows == (("R1", "2026-08-30", "Initial evidence-bound master report publication"),)
    assert set(report.evidence_ids).issubset({record.evidence_id for record in build_official_bundle(REPOSITORY_ROOT).evidence})
    assert report.evidence_ids


def test_all_generated_local_paths_use_the_verified_route_directory_and_exist():
    build = _module().build_openvino_report

    for bundle, route_directory in (
        (build_experimental_bundle(REPOSITORY_ROOT), "04-openvino-experimental-fork"),
        (build_official_bundle(REPOSITORY_ROOT), "05-openvino-official-upstream"),
    ):
        report = build(bundle)
        local_paths = _local_repo_paths(report)
        route_paths = {
            path for path in local_paths if path.startswith("docs/testing/final-results/")
        }

        assert route_paths
        assert all(
            path.startswith(f"docs/testing/final-results/{route_directory}/")
            for path in route_paths
        )
        assert [
            path for path in sorted(local_paths) if not (REPOSITORY_ROOT / path).exists()
        ] == []


def test_route_manifest_regeneration_is_exact_and_hash_valid(tmp_path):
    module = _module()
    route = tmp_path / "docs/testing/final-results/openvino-test-route"
    (route / "evidence").mkdir(parents=True)
    (route / "workbook/source").mkdir(parents=True)
    (route / "workbook/source/openvino-test-route-final-report.md").write_text(
        "# Canonical report\n", encoding="utf-8", newline="\n"
    )
    manifest = route / "evidence/manifest-sha256.txt"
    manifest.write_text("0" * 64 + "  stale.txt\n", encoding="utf-8", newline="\n")

    module.regenerate_route_manifest(tmp_path, route)

    from scripts.testing.final_results.evidence import validate_sha256_manifest

    assert validate_sha256_manifest(tmp_path, manifest) == []
    entries = [line.split("  ", 1)[1] for line in manifest.read_text(encoding="utf-8").splitlines()]
    assert entries == [
        "docs/testing/final-results/openvino-test-route/workbook/source/openvino-test-route-final-report.md"
    ]
    receipt = json.loads((route / "route-manifest.json").read_text(encoding="utf-8")) if (route / "route-manifest.json").exists() else None
    assert receipt is None


def test_wide_table_sections_lead_with_context_so_the_heading_is_not_orphaned():
    report = _module().build_openvino_report(build_experimental_bundle(REPOSITORY_ROOT))

    for section_index in (2, 3, 5, 6, 7, 9, 10, 13, 14):
        assert isinstance(report.sections[section_index].blocks[0], ReportParagraph)
