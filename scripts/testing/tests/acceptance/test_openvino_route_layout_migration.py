from __future__ import annotations

import csv
import hashlib
import json
import re
from collections import Counter
from pathlib import Path

import openpyxl
import pytest
from pypdf import PdfReader


ROOT = Path(__file__).resolve().parents[4]
COLLECTION = ROOT / "docs/testing/final-results"
BASELINE = json.loads(
    (ROOT / "docs/testing/cleanup/baseline-semantic-snapshot.json").read_text(
        encoding="utf-8"
    )
)
WINDOWS_ABSOLUTE = re.compile(r"^[A-Za-z]:[\\/]")
OPENVINO_ROUTES = (
    {
        "directory": "04-openvino-experimental-fork",
        "route_id": "openvino-experimental-fork",
        "builder": "build_experimental_bundle",
        "statuses": Counter({"passed": 27, "artifact_unavailable": 54}),
        "source_name": "Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx",
        "source_sha256": "09820a5b19edb11efd7640f1d9a13802b82fb464ab6e14622998d2565c5aca83",
        "pdf_sha256": "3f63cb7e4f6560c3234aa9023befe0d4bd3d3848580ee69166267f58ebcf9f64",
        "pdf_pages": 54,
    },
    {
        "directory": "05-openvino-official-upstream",
        "route_id": "openvino-official-upstream",
        "builder": "build_official_bundle",
        "statuses": Counter({"passed": 15, "failed": 5, "blocked": 25}),
        "source_name": "Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx",
        "source_sha256": "1d5fc2893e0c7f412140b3fa1a26c4a0c18e3c65ecfa356e80549dc4cd10aff7",
        "pdf_sha256": "5f7dde8af8a36ccbd62326cb881346b79eaa94f9a507fe1b91dd472abd362a55",
        "pdf_pages": 52,
    },
)
TABLES = {
    "attempts": "data/attempts.csv",
    "measurements": "data/measurements.csv",
    "summaries": "data/summaries.csv",
    "quality": "data/quality.csv",
    "failures": "data/failures.csv",
}


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _rows(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return sorted(
            (dict(row) for row in csv.DictReader(handle)),
            key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")),
        )


def _migrated_relationships(rows: list[dict[str, str]]) -> list[dict[str, str]]:
    migrations = {
        row["old_path"]: row["new_path"]
        for row in _rows(ROOT / "docs/testing/cleanup/PATH-MIGRATION.csv")
    }
    return [
        {
            **row,
            "relative_path": migrations.get(row["relative_path"], row["relative_path"]),
        }
        for row in rows
    ]


def _baseline_rows(table: str, route_id: str) -> list[dict[str, str]]:
    return sorted(
        (
            row
            for row in BASELINE["scientific_tables"][table]
            if row.get("route_id") == route_id
        ),
        key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")),
    )


def _assert_portable_workbook_has_no_absolute_paths(path: Path) -> None:
    workbook = openpyxl.load_workbook(path, data_only=False, keep_links=True)
    try:
        machine_paths = []
        for sheet in workbook.worksheets:
            for row in sheet.iter_rows():
                for cell in row:
                    if isinstance(cell.value, str) and WINDOWS_ABSOLUTE.match(cell.value):
                        machine_paths.append(f"{sheet.title}!{cell.coordinate}")
                    if (
                        cell.hyperlink
                        and isinstance(cell.hyperlink.target, str)
                        and WINDOWS_ABSOLUTE.match(cell.hyperlink.target)
                    ):
                        machine_paths.append(
                            f"{sheet.title}!{cell.coordinate}:hyperlink"
                        )
        assert machine_paths == []
    finally:
        workbook.close()


@pytest.mark.parametrize("case", OPENVINO_ROUTES, ids=lambda case: case["route_id"])
def test_openvino_routes_preserve_semantics_evidence_and_portable_derivatives(
    case: dict[str, object],
) -> None:
    from scripts.testing.reporting import openvino_adapter
    from scripts.testing.reporting.layout import validate_layout
    from scripts.testing.reporting.parity import compare_markdown_docx

    route_id = str(case["route_id"])
    route = COLLECTION / str(case["directory"])
    bundle = getattr(openvino_adapter, str(case["builder"]))(ROOT)

    assert validate_layout(route) == ()
    validation = json.loads(
        (route / "validation/validation.json").read_text(encoding="utf-8")
    )
    assert validation["valid"] is True
    assert all(receipt["valid"] is True for receipt in validation["checks"].values())
    assert json.loads((route / "data/route.json").read_text(encoding="utf-8")) == json.loads(
        json.dumps(bundle.to_row())
    )

    for table, relative in TABLES.items():
        assert _rows(route / relative) == _baseline_rows(table, route_id)

    attempts = _rows(route / "data/attempts.csv")
    assert Counter(row["status"] for row in attempts) == case["statuses"]
    quality = _rows(route / "data/quality.csv")
    baseline_quality = _baseline_rows("quality", route_id)
    quality_boundary = {
        (
            row["rubric_id"],
            row["prompt_suite_id"],
            row["scoring_version"],
            row["criterion_id"],
        )
        for row in quality
    }
    assert quality_boundary == {
        (
            row["rubric_id"],
            row["prompt_suite_id"],
            row["scoring_version"],
            row["criterion_id"],
        )
        for row in baseline_quality
    }
    denominators = Counter()
    for row in quality:
        denominators[row["test_case_id"]] += float(row["maximum_score"])
    assert set(denominators.values()) == {480.0}

    source = route / "evidence/source" / str(case["source_name"])
    portable = route / "reports" / f"{route_id}-results.xlsx"
    provenance = json.loads(
        (route / "reports" / f"{route_id}-results-provenance.json").read_text(
            encoding="utf-8"
        )
    )
    assert _sha256(source) == case["source_sha256"]
    assert provenance["source_sha256"] == case["source_sha256"]
    assert provenance["source_role"] == "immutable evidence-only nonportable workbook"
    assert provenance["machine_absolute_path_count"] == 0
    assert sorted(path.relative_to(route).as_posix() for path in route.rglob("*.xlsx")) == [
        f"evidence/source/{case['source_name']}",
        f"reports/{route_id}-results.xlsx",
    ]
    _assert_portable_workbook_has_no_absolute_paths(portable)

    evidence_rows = _rows(route / "evidence/evidence-index.csv")
    evidence_ids = {row["evidence_id"] for row in evidence_rows}
    expected_relationships = sorted(
        _migrated_relationships(
            [
            row
            for row in BASELINE["evidence"]["relationships"]
            if row["evidence_id"] in evidence_ids
            ]
        ),
        key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")),
    )
    actual_relationships = sorted(
        (
            {
                "evidence_id": row["evidence_id"],
                "relative_path": row["relative_path"],
                "sha256": row["sha256"],
                "size_bytes": row["size_bytes"],
            }
            for row in evidence_rows
        ),
        key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")),
    )
    assert actual_relationships == expected_relationships

    markdown = route / "reports" / f"{route_id}-report.md"
    docx = route / "reports" / f"{route_id}-report.docx"
    pdf = route / "reports" / f"{route_id}-report.pdf"
    assert compare_markdown_docx(markdown, docx)["matches"] is True
    report_text = markdown.read_text(encoding="utf-8")
    assert "workbook/source/" not in report_text
    assert f"docs/testing/final-results/{case['directory']}/results/" not in report_text
    assert f"reports/{route_id}-report.md" in report_text
    assert f"docs/testing/final-results/{case['directory']}/data/" in report_text
    assert _sha256(pdf) == case["pdf_sha256"]
    pages = [(page.extract_text() or "").strip() for page in PdfReader(pdf).pages]
    assert len(pages) == case["pdf_pages"]
    assert all(pages)


def test_cross_route_migration_preserves_task_one_comparison_decisions() -> None:
    from scripts.testing.reporting.layout import validate_layout
    from scripts.testing.reporting.parity import compare_markdown_docx

    route = COLLECTION / "06-cross-route-comparison"
    assert validate_layout(route) == ()
    validation = json.loads(
        (route / "validation/validation.json").read_text(encoding="utf-8")
    )
    assert validation["valid"] is True
    assert all(receipt["valid"] is True for receipt in validation["checks"].values())

    comparisons = _rows(route / "data/comparability-matrix.csv")
    expected = sorted(
        BASELINE["comparability"]["rows"],
        key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")),
    )
    assert comparisons == expected
    direct = [row for row in comparisons if row["classification"] == "direct"]
    assert len(direct) == 2
    assert {row["metric"] for row in direct} == {
        "generation_tokens_per_second",
        "quality",
    }
    assert {row["matched_case_count"] for row in direct} == {"15"}
    assert len({tuple(json.loads(row["matched_case_ids_json"])) for row in direct}) == 1

    markdown = route / "reports/cross-route-comparison-report.md"
    docx = route / "reports/cross-route-comparison-report.docx"
    pdf = route / "reports/cross-route-comparison-report.pdf"
    assert compare_markdown_docx(markdown, docx)["matches"] is True
    report_text = markdown.read_text(encoding="utf-8")
    assert "workbook/source/" not in report_text
    assert "06-cross-route-comparison/results/" not in report_text
    assert "reports/cross-route-comparison-report.md" in report_text
    assert "06-cross-route-comparison/data/comparability-matrix.csv" in report_text
    assert _sha256(pdf) == "49b47568a244aed83929db2e5336996f7cbf279ff3cc4597aa16a9ab692b01d3"
    pages = [(page.extract_text() or "").strip() for page in PdfReader(pdf).pages]
    assert len(pages) == 20
    assert all(pages)
