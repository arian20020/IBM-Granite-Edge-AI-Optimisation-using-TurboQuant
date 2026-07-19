"""Validation helpers for the fully populated WB-04 Markdown workbook."""

from __future__ import annotations

import re
from collections.abc import Set
from typing import Any


def validate_workbook_text(text: str, expected_ids: Set[str]) -> dict[str, Any]:
    table_rows = [line for line in text.splitlines() if line.strip().startswith("|")]
    for row_number, line in enumerate(table_rows, 1):
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if cells and all(re.fullmatch(r":?-{3,}:?", cell) for cell in cells):
            continue
        if any(cell == "" for cell in cells):
            raise ValueError(f"blank table cell at table row {row_number}")
        if any(cell.upper() == "N/A" for cell in cells):
            raise ValueError(f"bare N/A at table row {row_number}")
    missing = sorted(test_id for test_id in expected_ids
                     if not re.search(rf"(?<![A-Z0-9-]){re.escape(test_id)}(?![A-Z0-9-])", text))
    if missing:
        raise ValueError(f"missing controlled ID: {', '.join(missing)}")
    return {"accepted": True, "table_rows": len(table_rows),
            "controlled_id_count": len(expected_ids), "blank_table_cells": 0}
