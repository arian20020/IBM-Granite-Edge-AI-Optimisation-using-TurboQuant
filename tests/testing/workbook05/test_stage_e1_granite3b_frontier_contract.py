"""Opening RED contracts for Workbook 05 Stage E1 Granite 3B frontier/formal work."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.contracts import validate_phase3_record


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = REPOSITORY_ROOT / "experiments/granite_turboquant_intel/manifests/templates/workbook05"


class StageE1Granite3bFrontierContractTests(unittest.TestCase):
    def _load(self, name: str) -> dict[str, object]:
        return json.loads((TEMPLATE_ROOT / name).read_text(encoding="utf-8"))

    def test_e1_templates_validate(self) -> None:
        for record_type, filename in {
            "frontier-point": "frontier-point-template.json",
            "frontier-summary": "frontier-summary-template.json",
            "formal-comparison-summary": "formal-comparison-summary-template.json",
        }.items():
            with self.subTest(record_type=record_type):
                self.assertEqual([], validate_phase3_record(record_type, self._load(filename), REPOSITORY_ROOT))

    def test_frontier_requires_repeated_failure_before_skipping_higher_contexts(self) -> None:
        payload = self._load("frontier-summary-template.json")
        payload["higher_points_skipped"] = True
        payload["first_failure_repeated"] = False
        self.assertNotEqual([], validate_phase3_record("frontier-summary", payload, REPOSITORY_ROOT))

    def test_formal_comparison_requires_matched_baseline_identity(self) -> None:
        payload = self._load("formal-comparison-summary-template.json")
        payload["status"] = "Passed"
        payload["matched_baseline_sha256"] = None
        self.assertNotEqual([], validate_phase3_record("formal-comparison-summary", payload, REPOSITORY_ROOT))


if __name__ == "__main__":
    unittest.main()
