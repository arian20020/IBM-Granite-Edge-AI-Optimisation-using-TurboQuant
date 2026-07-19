"""Structural and control audit for the generated WB-04 DOCX."""

from __future__ import annotations

import csv
import hashlib
import zipfile
from pathlib import Path

from docx import Document


def audit_docx(docx_path: Path, manifest_path: Path, required_ids: set[str]) -> dict:
    with zipfile.ZipFile(docx_path) as archive:
        bad_zip_member = archive.testzip()
    if bad_zip_member is not None:
        raise ValueError(f"corrupt DOCX member: {bad_zip_member}")

    document = Document(docx_path)
    text_parts = [paragraph.text for paragraph in document.paragraphs]
    blank_cells = []
    for table_index, table in enumerate(document.tables):
        for row_index, row in enumerate(table.rows):
            for cell_index, cell in enumerate(row.cells):
                text_parts.append(cell.text)
                if not cell.text.strip():
                    blank_cells.append([table_index, row_index, cell_index])
    visible_text = "\n".join(text_parts)
    missing_ids = sorted(test_id for test_id in required_ids if test_id not in visible_text)
    if blank_cells:
        raise ValueError(f"blank DOCX table cells: {blank_cells[:5]}")
    if missing_ids:
        raise ValueError(f"missing controlled IDs in DOCX: {missing_ids}")
    if "v1.4" not in visible_text or "revision history" not in visible_text.lower():
        raise ValueError("visible v1.4 title or revision history is missing")

    with manifest_path.open(newline="", encoding="utf-8-sig") as handle:
        manifest_row = next(row for row in csv.DictReader(handle)
                            if row["Workbook_ID"] == "WB-04")
    actual_hash = hashlib.sha256(docx_path.read_bytes()).hexdigest()
    if actual_hash.lower() != manifest_row["Last_Validated_DOCX_SHA256"].lower():
        raise ValueError("WB-04 generated hash differs from controlled manifest")
    return {
        "accepted": True,
        "zip_integrity": "passed",
        "controlled_id_count": len(required_ids),
        "blank_table_cells": 0,
        "visible_revision": "1.4",
        "revision_history": "present",
        "generated_sha256": actual_hash,
        "table_count": len(document.tables),
    }
