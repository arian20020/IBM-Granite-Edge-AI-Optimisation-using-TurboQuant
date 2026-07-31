"""Structural and control audit for the generated WB-04 DOCX."""

from __future__ import annotations

import csv
import hashlib
import re
import zipfile
from pathlib import Path

from docx import Document


PRESENTATION_MEASURED_PAIRS = (
    ("OV-TQ-13", 512),
    ("OV-TQ-14", 512),
    ("OV-TQ-14", 2048),
)
PRESENTATION_LIMITATION_BULLETS = (
    "Diagnostic only; no formal benchmark",
    "Missing validated FP16 artifact",
    "RAM safety floor reached",
    "Larger host required",
    "Strict activation proof incomplete",
    "Governed quality campaign stopped at the RAM floor",
)
NO_SCORE_NO_WINNER_DISCLOSURE = (
    "No numeric quality score exists and there is no winner."
)
PROHIBITED_PRESENTATION_CLAIMS = (
    "all tests passed",
    "quality-qualified pass",
    "quality winner",
)


def _presentation_pair_is_visible(
    visible_text: str, test_id: str, context_tokens: int
) -> bool:
    return re.search(
        rf"(?<![A-Z0-9-]){re.escape(test_id)}(?![A-Z0-9-])"
        rf"\s*(?:\|\s*)?{context_tokens}\b",
        visible_text,
    ) is not None


def audit_presentation(visible_text: str) -> dict[str, int]:
    """Validate the compact v1.8 presentation without relaxing control checks."""
    missing_pairs = [
        f"{test_id}/{context_tokens}"
        for test_id, context_tokens in PRESENTATION_MEASURED_PAIRS
        if not _presentation_pair_is_visible(visible_text, test_id, context_tokens)
    ]
    if missing_pairs:
        raise ValueError(
            "missing measured presentation pairs: " + ", ".join(missing_pairs)
        )
    missing_limitations = [
        bullet
        for bullet in PRESENTATION_LIMITATION_BULLETS
        if bullet not in visible_text
    ]
    if missing_limitations:
        raise ValueError(
            "missing compact limitation bullets: " + ", ".join(missing_limitations)
        )
    if NO_SCORE_NO_WINNER_DISCLOSURE not in visible_text:
        raise ValueError("exact no-score/no-winner disclosure is missing")

    lowered = visible_text.casefold()
    for claim in PROHIBITED_PRESENTATION_CLAIMS:
        if claim in lowered:
            raise ValueError(f"prohibited presentation claim: {claim}")
    if re.search(
        r"\bquality\s+score\b[^\n|]{0,40}"
        r"(?<![a-z0-9])[0-9]+(?:\.[0-9]+)?(?:\s*/\s*10)?\b",
        visible_text,
        re.IGNORECASE,
    ):
        raise ValueError("prohibited numeric quality score")
    without_no_winner = re.sub(
        r"\b(?:there\s+is\s+)?no(?:\s+[a-z-]+){0,3}\s+winner\b",
        "",
        visible_text,
        flags=re.IGNORECASE,
    )
    if re.search(r"\bwinner\b", without_no_winner, re.IGNORECASE):
        raise ValueError("prohibited winner claim")
    return {
        "presentation_measured_row_count": len(PRESENTATION_MEASURED_PAIRS),
        "presentation_limitation_bullet_count": len(PRESENTATION_LIMITATION_BULLETS),
    }


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
    presentation = audit_presentation(visible_text)
    with manifest_path.open(newline="", encoding="utf-8-sig") as handle:
        manifest_row = next(row for row in csv.DictReader(handle)
                            if row["Workbook_ID"] == "WB-04")
    manifest_revision = manifest_row["Revision"].strip()
    if not manifest_revision:
        raise ValueError("WB-04 manifest revision is missing")
    visible_revision = f"v{manifest_revision}"
    if visible_revision not in visible_text or "revision history" not in visible_text.lower():
        raise ValueError(
            f"visible {visible_revision} title or revision history is missing"
        )

    actual_hash = hashlib.sha256(docx_path.read_bytes()).hexdigest()
    if actual_hash.lower() != manifest_row["Last_Validated_DOCX_SHA256"].lower():
        raise ValueError("WB-04 generated hash differs from controlled manifest")
    return {
        "accepted": True,
        "zip_integrity": "passed",
        "controlled_id_count": len(required_ids),
        "blank_table_cells": 0,
        "visible_revision": manifest_revision,
        "revision_history": "present",
        "generated_sha256": actual_hash,
        "table_count": len(document.tables),
        **presentation,
    }
