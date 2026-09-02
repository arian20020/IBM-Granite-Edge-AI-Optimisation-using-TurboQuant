#!/usr/bin/env python3
"""Freeze a deterministic semantic snapshot of the published result library."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
from collections import Counter
from pathlib import Path
from typing import Iterable, Sequence


ROUTE_GLOB = "0[1-5]-*"


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _rows(path: Path) -> list[dict[str, str]]:
    if not path.is_file():
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        rows = [dict(row) for row in csv.DictReader(handle)]
    return sorted(rows, key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")))


def _table_record(root: Path, path: Path) -> dict[str, object]:
    return {
        "path": path.relative_to(root).as_posix(),
        "sha256": _sha256(path),
        "rows": _rows(path),
    }


def _canonical_hash(value: object) -> str:
    encoded = (json.dumps(value, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def build_semantic_snapshot(repo_root: Path) -> dict[str, object]:
    """Return scientific values and publication invariants, independent of layout."""

    root = Path(repo_root).resolve()
    collection = root / "docs/testing/final-results"
    routes = sorted(path for path in collection.glob(ROUTE_GLOB) if path.is_dir())

    attempts: list[dict[str, str]] = []
    evidence_rows: list[dict[str, str]] = []
    tables: dict[str, list[dict[str, object]]] = {
        "attempts": [],
        "measurements": [],
        "summaries": [],
        "quality": [],
        "failures": [],
        "deviations": [],
    }
    table_locations = {
        "attempts": "results/attempts.csv",
        "measurements": "results/measurements.csv",
        "summaries": "results/summary-results.csv",
        "quality": "quality/scores.csv",
        "failures": "failures/failure-register.csv",
        "deviations": "protocol/deviations.csv",
    }
    for route in routes:
        for name, relative in table_locations.items():
            path = route / relative
            if path.is_file():
                record = _table_record(root, path)
                tables[name].extend(record["rows"])
                if name == "attempts":
                    attempts.extend(record["rows"])
        evidence_rows.extend(_rows(route / "evidence/evidence-index.csv"))

    for rows in tables.values():
        rows.sort(key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")))
    statuses = Counter(
        row.get("status", "").replace("_", "-") for row in attempts if row.get("status")
    )
    evidence_paths = sorted(
        {row.get("relative_path", "") for row in evidence_rows if row.get("relative_path")}
    )
    evidence_ids = sorted(
        {row.get("evidence_id", "") for row in evidence_rows if row.get("evidence_id")}
    )

    report_pairs = []
    for route in sorted(path for path in collection.glob("0[1-6]-*") if path.is_dir()):
        markdown = sorted((route / "workbook/source").glob("*.md"))
        docx = sorted((route / "workbook/generated").glob("*.docx"))
        if len(markdown) == 1 and len(docx) == 1:
            report_pairs.append(
                {
                    "route": route.name,
                    "markdown_path": markdown[0].relative_to(root).as_posix(),
                    "markdown_sha256": _sha256(markdown[0]),
                    "docx_path": docx[0].relative_to(root).as_posix(),
                    "docx_sha256": _sha256(docx[0]),
                }
            )

    pdf_records = []
    page_total = 0
    try:
        from pypdf import PdfReader
    except ImportError:  # pragma: no cover - dependency is required by the project suite
        PdfReader = None
    for pdf in sorted(collection.glob("0[1-6]-*/workbook/generated/*.pdf")):
        pages = []
        if PdfReader is not None:
            pages = [(page.extract_text() or "").strip() for page in PdfReader(pdf).pages]
        page_total += len(pages)
        pdf_records.append(
            {
                "path": pdf.relative_to(root).as_posix(),
                "sha256": _sha256(pdf),
                "page_count": len(pages),
                "all_pages_searchable_nonblank": bool(pages) and all(pages),
            }
        )

    matrix_path = collection / "06-cross-route-comparison/results/comparability-matrix.csv"
    comparison_rows = _rows(matrix_path)
    payload: dict[str, object] = {
        "schema": "testing-cleanup-semantic-snapshot/v1",
        "outcomes": {
            "total": len(attempts),
            "status_counts": dict(sorted(statuses.items())),
            "stable_route_ids": sorted({row.get("route_id", "") for row in attempts}),
            "stable_campaign_ids": sorted({row.get("campaign_id", "") for row in attempts}),
            "stable_test_case_ids": sorted({row.get("test_case_id", "") for row in attempts}),
            "stable_attempt_ids": sorted({row.get("attempt_id", "") for row in attempts}),
        },
        "evidence": {
            "record_count": len(evidence_rows),
            "relationship_count": len(evidence_paths),
            "unique_path_count": len(evidence_paths),
            "unique_evidence_id_count": len(evidence_ids),
            "relationships": sorted(
                (
                    {
                        "evidence_id": row.get("evidence_id", ""),
                        "relative_path": row.get("relative_path", ""),
                        "sha256": row.get("sha256", ""),
                        "size_bytes": row.get("size_bytes", ""),
                    }
                    for row in evidence_rows
                ),
                key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")),
            ),
        },
        "scientific_tables": tables,
        "reports": {"pair_count": len(report_pairs), "pairs": report_pairs},
        "pdfs": {"file_count": len(pdf_records), "page_count": page_total, "files": pdf_records},
        "comparability": {
            "path": matrix_path.relative_to(root).as_posix() if matrix_path.is_file() else "",
            "sha256": _sha256(matrix_path) if matrix_path.is_file() else "",
            "rows": comparison_rows,
        },
    }
    payload["snapshot_sha256"] = _canonical_hash(payload)
    return payload


def _path_migrations(path: Path) -> dict[str, str]:
    rows = _rows(Path(path))
    migrations: dict[str, str] = {}
    for row in rows:
        old_path = row.get("old_path", "")
        new_path = row.get("new_path", "")
        if not old_path or not new_path:
            raise ValueError("path migration rows require old_path and new_path")
        if old_path in migrations:
            raise ValueError(f"duplicate path migration source: {old_path}")
        migrations[old_path] = new_path
    return migrations


def _migrated_path(path: str, migrations: dict[str, str]) -> str:
    current = path
    visited: set[str] = set()
    while current in migrations:
        if current in visited:
            raise ValueError(f"path migration cycle: {path}")
        visited.add(current)
        current = migrations[current]
    return current


def _assert_equal(label: str, actual: object, expected: object) -> None:
    if actual != expected:
        raise ValueError(f"final semantic snapshot drift: {label}")


def build_final_semantic_snapshot(
    repo_root: Path,
    *,
    baseline_path: Path,
    path_migration: Path,
) -> dict[str, object]:
    """Reconcile the compact library to the frozen, layout-neutral Task 1 snapshot.

    The returned object is exactly the frozen snapshot.  It is returned only after
    every scientific table, stable outcome identity, migrated evidence relationship,
    comparison decision, report parity relationship, and unchanged PDF identity has
    been re-derived from the compact tree and matched to that snapshot.
    """

    root = Path(repo_root).resolve()
    baseline = json.loads(Path(baseline_path).read_text(encoding="utf-8"))
    baseline_without_hash = dict(baseline)
    snapshot_sha256 = baseline_without_hash.pop("snapshot_sha256", None)
    _assert_equal(
        "baseline self hash",
        snapshot_sha256,
        _canonical_hash(baseline_without_hash),
    )
    validate_release_baseline(baseline)

    migrations = _path_migrations(path_migration)
    collection = root / "docs/testing/final-results"
    routes = sorted(path for path in collection.glob(ROUTE_GLOB) if path.is_dir())
    table_locations = {
        "attempts": "data/attempts.csv",
        "measurements": "data/measurements.csv",
        "summaries": "data/summaries.csv",
        "quality": "data/quality.csv",
        "failures": "data/failures.csv",
        "deviations": "data/deviations.csv",
    }
    tables: dict[str, list[dict[str, str]]] = {
        name: [] for name in table_locations
    }
    for route in routes:
        for name, relative in table_locations.items():
            tables[name].extend(_rows(route / relative))
    for rows in tables.values():
        rows.sort(key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")))
    _assert_equal("scientific tables", tables, baseline["scientific_tables"])

    attempts = tables["attempts"]
    outcomes = {
        "total": len(attempts),
        "status_counts": dict(
            sorted(
                Counter(
                    row.get("status", "").replace("_", "-")
                    for row in attempts
                    if row.get("status")
                ).items()
            )
        ),
        "stable_route_ids": sorted({row.get("route_id", "") for row in attempts}),
        "stable_campaign_ids": sorted(
            {row.get("campaign_id", "") for row in attempts}
        ),
        "stable_test_case_ids": sorted(
            {row.get("test_case_id", "") for row in attempts}
        ),
        "stable_attempt_ids": sorted(
            {row.get("attempt_id", "") for row in attempts}
        ),
    }
    _assert_equal("outcomes and stable IDs", outcomes, baseline["outcomes"])

    evidence_rows = [
        row
        for route in routes
        for row in _rows(route / "evidence/evidence-index.csv")
    ]
    actual_relationships = sorted(
        (
            {
                "evidence_id": row.get("evidence_id", ""),
                "relative_path": row.get("relative_path", ""),
                "sha256": row.get("sha256", ""),
                "size_bytes": row.get("size_bytes", ""),
            }
            for row in evidence_rows
        ),
        key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")),
    )
    expected_relationships = sorted(
        (
            {
                **row,
                "relative_path": _migrated_path(row["relative_path"], migrations),
            }
            for row in baseline["evidence"]["relationships"]
        ),
        key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")),
    )
    _assert_equal(
        "migrated evidence relationships", actual_relationships, expected_relationships
    )
    evidence_ids = {row["evidence_id"] for row in actual_relationships}
    evidence_paths = {row["relative_path"] for row in actual_relationships}
    _assert_equal("evidence record count", len(actual_relationships), baseline["evidence"]["record_count"])
    _assert_equal("evidence relationship count", len(evidence_paths), baseline["evidence"]["relationship_count"])
    _assert_equal("evidence unique path count", len(evidence_paths), baseline["evidence"]["unique_path_count"])
    _assert_equal("evidence unique ID count", len(evidence_ids), baseline["evidence"]["unique_evidence_id_count"])

    from scripts.testing.reporting.parity import compare_markdown_docx

    expected_markdown: set[str] = set()
    expected_docx: set[str] = set()
    for pair in baseline["reports"]["pairs"]:
        markdown_relative = _migrated_path(pair["markdown_path"], migrations)
        docx_relative = _migrated_path(pair["docx_path"], migrations)
        expected_markdown.add(markdown_relative)
        expected_docx.add(docx_relative)
        parity = compare_markdown_docx(root / markdown_relative, root / docx_relative)
        if parity["matches"] is not True:
            raise ValueError(f"final semantic snapshot drift: report parity {pair['route']}")
    actual_markdown = {
        path.relative_to(root).as_posix()
        for path in collection.glob("0[1-6]-*/reports/*-report.md")
    }
    actual_docx = {
        path.relative_to(root).as_posix()
        for path in collection.glob("0[1-6]-*/reports/*-report.docx")
    }
    _assert_equal("Markdown report set", actual_markdown, expected_markdown)
    _assert_equal("DOCX report set", actual_docx, expected_docx)

    from pypdf import PdfReader

    expected_pdfs: set[str] = set()
    for record in baseline["pdfs"]["files"]:
        relative = _migrated_path(record["path"], migrations)
        expected_pdfs.add(relative)
        pdf = root / relative
        pages = [(page.extract_text() or "").strip() for page in PdfReader(pdf).pages]
        _assert_equal(f"PDF hash {relative}", _sha256(pdf), record["sha256"])
        _assert_equal(f"PDF page count {relative}", len(pages), record["page_count"])
        _assert_equal(
            f"PDF searchable pages {relative}",
            bool(pages) and all(pages),
            record["all_pages_searchable_nonblank"],
        )
    actual_pdfs = {
        path.relative_to(root).as_posix()
        for path in collection.glob("0[1-6]-*/reports/*-report.pdf")
    }
    _assert_equal("PDF report set", actual_pdfs, expected_pdfs)

    comparison = baseline["comparability"]
    comparison_relative = _migrated_path(comparison["path"], migrations)
    comparison_path = root / comparison_relative
    _assert_equal("comparability rows", _rows(comparison_path), comparison["rows"])
    _assert_equal("comparability hash", _sha256(comparison_path), comparison["sha256"])

    return baseline


def validate_release_baseline(snapshot: dict[str, object]) -> None:
    outcomes = snapshot["outcomes"]
    evidence = snapshot["evidence"]
    reports = snapshot["reports"]
    pdfs = snapshot["pdfs"]
    expected_statuses = {
        "artifact-unavailable": 54,
        "blocked": 28,
        "failed": 6,
        "passed": 81,
    }
    checks = {
        "outcome total": outcomes["total"] == 169,
        "status split": outcomes["status_counts"] == expected_statuses,
        "cited evidence paths": evidence["unique_path_count"] == 1846,
        "report pairs": reports["pair_count"] == 6,
        "PDF files": pdfs["file_count"] == 6,
        "PDF pages": pdfs["page_count"] == 202,
        "searchable PDFs": all(item["all_pages_searchable_nonblank"] for item in pdfs["files"]),
    }
    failed = [name for name, valid in checks.items() if not valid]
    if failed:
        raise ValueError("semantic release baseline drift: " + ", ".join(failed))


def write_semantic_snapshot(repo_root: Path, output: Path) -> dict[str, object]:
    snapshot = build_semantic_snapshot(repo_root)
    Path(output).parent.mkdir(parents=True, exist_ok=True)
    Path(output).write_text(
        json.dumps(snapshot, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    return snapshot


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--baseline", type=Path)
    parser.add_argument("--path-migration", type=Path)
    parser.add_argument("--expect-release-baseline", action="store_true")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)
    if (args.baseline is None) != (args.path_migration is None):
        parser.error("--baseline and --path-migration must be supplied together")
    if args.baseline is None:
        snapshot = write_semantic_snapshot(args.repo_root, args.output)
    else:
        snapshot = build_final_semantic_snapshot(
            args.repo_root,
            baseline_path=args.baseline,
            path_migration=args.path_migration,
        )
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(
            json.dumps(snapshot, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
            newline="\n",
        )
    if args.expect_release_baseline:
        validate_release_baseline(snapshot)
    print(
        f"Frozen {snapshot['outcomes']['total']} outcomes and "
        f"{snapshot['evidence']['unique_path_count']} cited evidence paths."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
