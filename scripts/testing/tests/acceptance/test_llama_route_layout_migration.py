import csv
import hashlib
import json
from collections import Counter
from pathlib import Path

import pytest
from pypdf import PdfReader


ROOT = Path(__file__).resolve().parents[4]
BASELINE = json.loads(
    (ROOT / "docs/testing/cleanup/baseline-semantic-snapshot.json").read_text(
        encoding="utf-8"
    )
)
ROUTES = (
    (
        "01-upstream-llama-cpp",
        "upstream-llama-cpp",
        "build_upstream_llama_bundle",
        47,
    ),
    (
        "02-atomicbot-turboquant",
        "atomicbot-turboquant",
        "build_atomicbot_bundle",
        22,
    ),
    (
        "03-animehacker-tq3-0",
        "animehacker-tq3-0",
        "build_animehacker_bundle",
        7,
    ),
)
BASELINE_REPORT_HASHES = {
    "upstream-llama-cpp": {
        "md": "701f788c20d5b8443af675ae12382e83f05c824480ad3b8d429b27514e168ab8",
        "docx": "2acdf72a33415dec05f8184fb2b03ce329bb991c79b981c857608bef59c23bb1",
        "pdf": "e9fd0e7358acca1ddfc063049010cce617ec7ba6fd27a26e5a95cc9b56a77c8c",
    },
    "atomicbot-turboquant": {
        "md": "1d04373c2ed4e2035fe6c5329763b1853cab383a78fe245cd9af093e5ad33b94",
        "docx": "cf47c80781f9268b5706552cf4523c301c5474da0310bfa87a341cfdba021858",
        "pdf": "a0b8dd912956857b9836f6a606fc9817101b2c1b612fd926e9d1b0be2139c433",
    },
    "animehacker-tq3-0": {
        "md": "c24dd436c9c04c6c6bdcd6ee7318562cdc93c163169b57951ee863f5930c26a9",
        "docx": "f5b8b6a32baff1c78085e8ded0aba05d950c991c790c3e895b35ad7fe3634e99",
        "pdf": "6d7e86d5f90511ee139aa50cc5b439b9746898edb551a9ea2204ab40daed4643",
    },
}
BASELINE_UNMOVED_HASHES = {
    "upstream-llama-cpp": {
        "data/deviations.csv": "29212edb52ea9c4972696bc242d9a82c93879474ee00bb0f4a09fd6fbc41db02",
        "data/failures.csv": "900ae72b7755f20cf92475f59aa2a6043c4e1e47b43fc2873a045bba5a6f464f",
        "evidence/claim-evidence-map.csv": "1437a370bf61806587b6c6b764efb7c1bc6a77ccf7f6042d4c6c355cbf13b1f7",
    },
    "atomicbot-turboquant": {
        "data/deviations.csv": "de17dee26680c59fcc20375a49586fb6d126ebaf88e72e8aeac3f8d3593db346",
        "data/failures.csv": "89c20697e7f38584c8c00c59b8a157c1a9603e36f8269dabbbc3b90025369362",
        "evidence/claim-evidence-map.csv": "87a486488e9aa1806aaf52451a2c7051a150c2dc2b60fd17616a3a46b64d8a64",
    },
    "animehacker-tq3-0": {
        "data/deviations.csv": "5c0c9b7d88001cebbf47ab3de80c942380c4ea88476a87bc14d784c1698fb9b7",
        "data/failures.csv": "061df8e192742547f073d5263b238289933a385586e0432960c4996841bc387e",
        "evidence/claim-evidence-map.csv": "e42e9bfb17253e5400a2832e6959808a01f79d1bc7e86cb5e72a10e84994d4a1",
    },
}
TABLES = {
    "attempts": "data/attempts.csv",
    "measurements": "data/measurements.csv",
    "summaries": "data/summaries.csv",
    "quality": "data/quality.csv",
    "failures": "data/failures.csv",
}


def _rows(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return sorted(
            (dict(row) for row in csv.DictReader(handle)),
            key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")),
        )


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _migrated_relationships(rows: list[dict[str, str]]) -> list[dict[str, str]]:
    migrations = {
        row["old_path"]: row["new_path"]
        for row in _rows(ROOT / "docs/testing/cleanup/PATH-MIGRATION.csv")
    }
    migrated = []
    for row in rows:
        migrated.append(
            {
                **row,
                "relative_path": migrations.get(row["relative_path"], row["relative_path"]),
            }
        )
    return migrated


def _baseline_rows(table: str, route_id: str) -> list[dict[str, str]]:
    return sorted(
        (
            row
            for row in BASELINE["scientific_tables"][table]
            if row.get("route_id") == route_id
        ),
        key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")),
    )


@pytest.mark.parametrize(
    ("route_name", "route_id", "builder_name", "page_count"), ROUTES
)
def test_llama_route_migration_preserves_frozen_semantics_and_report_evidence(
    route_name: str, route_id: str, builder_name: str, page_count: int
) -> None:
    from scripts.testing.reporting import llama_adapter
    from scripts.testing.reporting.layout import validate_layout
    from scripts.testing.reporting.parity import compare_markdown_docx

    route = ROOT / "docs/testing/final-results" / route_name
    bundle = getattr(llama_adapter, builder_name)(ROOT)

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
    assert Counter(row["status"] for row in attempts) == Counter(
        row["status"] for row in _baseline_rows("attempts", route_id)
    )
    quality = _rows(route / "data/quality.csv")
    baseline_quality = _baseline_rows("quality", route_id)
    assert {
        (row["rubric_id"], row["prompt_suite_id"], row["scoring_version"])
        for row in quality
    } == {
        (row["rubric_id"], row["prompt_suite_id"], row["scoring_version"])
        for row in baseline_quality
    }
    assert (min(float(row["score"]) for row in quality), max(float(row["score"]) for row in quality)) == (
        min(float(row["score"]) for row in baseline_quality),
        max(float(row["score"]) for row in baseline_quality),
    )

    for relative, expected_hash in BASELINE_UNMOVED_HASHES[route_id].items():
        assert _sha256(route / relative) == expected_hash

    evidence_rows = _rows(route / "evidence/evidence-index.csv")
    evidence_ids = {row["evidence_id"] for row in evidence_rows}
    baseline_relationships = sorted(
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
    assert actual_relationships == baseline_relationships
    assert all(
        (ROOT / row["relative_path"]).is_file()
        and _sha256(ROOT / row["relative_path"]) == row["sha256"]
        for row in evidence_rows
    )

    markdown = route / "reports" / f"{route_id}-report.md"
    docx = route / "reports" / f"{route_id}-report.docx"
    pdf = route / "reports" / f"{route_id}-report.pdf"
    assert compare_markdown_docx(markdown, docx)["matches"] is True
    if route_id == "upstream-llama-cpp":
        report_text = markdown.read_text(encoding="utf-8")
        assert "workbook/source/" not in report_text
        assert "docs/testing/final-results/01-upstream-llama-cpp/results/" not in report_text
        assert "reports/upstream-llama-cpp-report.md" in report_text
        assert "docs/testing/final-results/01-upstream-llama-cpp/data/" in report_text

    assert _sha256(pdf) == BASELINE_REPORT_HASHES[route_id]["pdf"]
    reader = PdfReader(pdf)
    page_text = [(page.extract_text() or "").strip() for page in reader.pages]
    assert len(page_text) == page_count
    assert all(page_text)
