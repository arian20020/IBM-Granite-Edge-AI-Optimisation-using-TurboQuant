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
    parser.add_argument("--expect-release-baseline", action="store_true")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    snapshot = write_semantic_snapshot(args.repo_root, args.output)
    if args.expect_release_baseline:
        validate_release_baseline(snapshot)
    print(
        f"Frozen {snapshot['outcomes']['total']} outcomes and "
        f"{snapshot['evidence']['unique_path_count']} cited evidence paths."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
