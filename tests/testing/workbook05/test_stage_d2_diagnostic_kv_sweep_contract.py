"""Opening RED contracts for Workbook 05 Stage D2 diagnostic K/V sweeps."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.contracts import validate_phase3_record


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = REPOSITORY_ROOT / "experiments/granite_turboquant_intel/manifests/templates/workbook05"


class StageD2DiagnosticKvSweepContractTests(unittest.TestCase):
    def _load(self, name: str) -> dict[str, object]:
        return json.loads((TEMPLATE_ROOT / name).read_text(encoding="utf-8"))

    def test_d2_templates_validate(self) -> None:
        for record_type, filename in {
            "diagnostic-kv-sweep-row": "diagnostic-kv-sweep-row-template.json",
            "diagnostic-kv-sweep-summary": "diagnostic-kv-sweep-summary-template.json",
        }.items():
            with self.subTest(record_type=record_type):
                self.assertEqual([], validate_phase3_record(record_type, self._load(filename), REPOSITORY_ROOT))

    def test_sweep_summary_rejects_high_memory_before_lower_memory_candidate(self) -> None:
        payload = self._load("diagnostic-kv-sweep-summary-template.json")
        payload["execution_order"] = ["higher-memory", "lower-memory"]
        payload["ordering_verified_low_to_high"] = False
        self.assertNotEqual([], validate_phase3_record("diagnostic-kv-sweep-summary", payload, REPOSITORY_ROOT))

    def test_blocked_codec_keeps_dependent_row_visible(self) -> None:
        payload = self._load("diagnostic-kv-sweep-row-template.json")
        payload["codec_status"] = "Blocked"
        payload["row_status"] = "Passed"
        payload["blocker_id"] = None
        self.assertNotEqual([], validate_phase3_record("diagnostic-kv-sweep-row", payload, REPOSITORY_ROOT))


if __name__ == "__main__":
    unittest.main()
