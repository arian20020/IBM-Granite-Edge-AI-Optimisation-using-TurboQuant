"""Source-level reconciliation for WB-02 fields."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


ALLOWED_STATUSES = {"Measured", "N/A", "Unsupported", "Blocked", "Passed", "Failed"}
PLACEHOLDERS = {"", "TBD", "TODO", "PENDING", "NOT RUN"}


@dataclass(frozen=True)
class ReconciliationReport:
    valid: bool
    checked_fields: int
    errors: tuple[str, ...]


def reconcile_workbook(template: Path, results: dict) -> ReconciliationReport:
    if not template.is_file():
        return ReconciliationReport(False, 0, (f"template-not-found:{template}",))
    required = results.get("required_fields", [])
    field_map = results.get("field_map", {})
    errors: list[str] = []
    for field in required:
        record = field_map.get(field)
        if not isinstance(record, dict):
            errors.append(f"missing-field:{field}")
            continue
        status = record.get("status")
        value = record.get("value")
        if status not in ALLOWED_STATUSES:
            errors.append(f"invalid-status:{field}")
        if value is None or str(value).strip().upper() in PLACEHOLDERS:
            errors.append(f"blank-or-placeholder:{field}")
        if not record.get("source_path") or not record.get("source_key"):
            errors.append(f"missing-source:{field}")
        if status in {"N/A", "Unsupported", "Blocked"} and not record.get("reason"):
            errors.append(f"missing-reason:{field}")
    extras = set(field_map) - set(required)
    if extras:
        errors.append(f"unexpected-fields:{','.join(sorted(extras))}")
    return ReconciliationReport(not errors, len(required), tuple(errors))
