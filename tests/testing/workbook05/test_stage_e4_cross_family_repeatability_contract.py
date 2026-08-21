"""Opening RED contracts for Workbook 05 Stage E4 cross-family/repeatability work."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.contracts import validate_phase3_record


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = REPOSITORY_ROOT / "experiments/granite_turboquant_intel/manifests/templates/workbook05"


class StageE4CrossFamilyRepeatabilityContractTests(unittest.TestCase):
    def _load(self, name: str) -> dict[str, object]:
        return json.loads((TEMPLATE_ROOT / name).read_text(encoding="utf-8"))

    def test_e4_templates_validate(self) -> None:
        for record_type, filename in {
            "cross-family-result": "cross-family-result-template.json",
            "repeatability-result": "repeatability-result-template.json",
        }.items():
            with self.subTest(record_type=record_type):
                self.assertEqual([], validate_phase3_record(record_type, self._load(filename), REPOSITORY_ROOT))

    def test_cross_family_pass_requires_both_constituent_codecs_admitted(self) -> None:
        payload = self._load("cross-family-result-template.json")
        payload["status"] = "Passed"
        payload["key_codec_status"] = "Blocked"
        payload["value_codec_status"] = "Passed"
        self.assertNotEqual([], validate_phase3_record("cross-family-result", payload, REPOSITORY_ROOT))

    def test_repeatability_requires_identical_execution_identity(self) -> None:
        payload = self._load("repeatability-result-template.json")
        payload["status"] = "Passed"
        payload["identity_consistent"] = False
        self.assertNotEqual([], validate_phase3_record("repeatability-result", payload, REPOSITORY_ROOT))


if __name__ == "__main__":
    unittest.main()
