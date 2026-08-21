"""Opening RED contracts for Workbook 05 Stage F evidence/workbook closure."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.contracts import validate_phase3_record


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = REPOSITORY_ROOT / "experiments/granite_turboquant_intel/manifests/templates/workbook05"


class StageFWorkbookClosureContractTests(unittest.TestCase):
    def _load(self, name: str) -> dict[str, object]:
        return json.loads((TEMPLATE_ROOT / name).read_text(encoding="utf-8"))

    def test_stage_f_template_validates(self) -> None:
        payload = self._load("workbook-closure-decision-template.json")
        self.assertEqual([], validate_phase3_record("workbook-closure-decision", payload, REPOSITORY_ROOT))

    def test_closed_workbook_rejects_unvalidated_formal_rows(self) -> None:
        payload = self._load("workbook-closure-decision-template.json")
        payload["status"] = "Closed"
        payload["all_formal_rows_independently_validated"] = False
        self.assertNotEqual([], validate_phase3_record("workbook-closure-decision", payload, REPOSITORY_ROOT))

    def test_closed_workbook_rejects_unresolved_controlled_fields(self) -> None:
        payload = self._load("workbook-closure-decision-template.json")
        payload["status"] = "Closed"
        payload["unresolved_controlled_field_count"] = 1
        self.assertNotEqual([], validate_phase3_record("workbook-closure-decision", payload, REPOSITORY_ROOT))

    def test_historical_legacy_evidence_cannot_be_promoted_to_active_result(self) -> None:
        payload = self._load("workbook-closure-decision-template.json")
        payload["legacy_evidence_promoted_to_active_results"] = True
        self.assertNotEqual([], validate_phase3_record("workbook-closure-decision", payload, REPOSITORY_ROOT))


if __name__ == "__main__":
    unittest.main()
