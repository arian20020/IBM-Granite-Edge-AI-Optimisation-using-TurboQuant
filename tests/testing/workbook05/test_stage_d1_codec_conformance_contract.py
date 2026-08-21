"""Opening RED contracts for Workbook 05 Stage D1 codec conformance.

These tests intentionally precede the D1 schemas/templates. They freeze the rule
that a codec cannot be called conformant from a request or generated text alone.
"""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.contracts import validate_phase3_record


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = REPOSITORY_ROOT / "experiments/granite_turboquant_intel/manifests/templates/workbook05"


class StageD1CodecConformanceContractTests(unittest.TestCase):
    def _load(self, name: str) -> dict[str, object]:
        return json.loads((TEMPLATE_ROOT / name).read_text(encoding="utf-8"))

    def test_d1_templates_validate(self) -> None:
        for record_type, filename in {
            "codec-conformance-result": "codec-conformance-result-template.json",
            "codec-storage-reconciliation": "codec-storage-reconciliation-template.json",
        }.items():
            with self.subTest(record_type=record_type):
                self.assertEqual([], validate_phase3_record(record_type, self._load(filename), REPOSITORY_ROOT))

    def test_passed_conformance_requires_dispatch_no_fallback_and_storage(self) -> None:
        payload = self._load("codec-conformance-result-template.json")
        payload["status"] = "Passed"
        payload["dispatch_proven"] = False
        payload["fallback_observed"] = True
        payload["storage_status"] = "Unavailable"
        self.assertNotEqual([], validate_phase3_record("codec-conformance-result", payload, REPOSITORY_ROOT))

    def test_route_b_label_cannot_be_relabelled_as_route_a_turbo(self) -> None:
        payload = self._load("codec-conformance-result-template.json")
        payload["route"] = "route-a-merged-openvino"
        payload["requested_codec"] = "PolarQuant3"
        payload["verified_codec"] = "TURBO/u3"
        self.assertNotEqual([], validate_phase3_record("codec-conformance-result", payload, REPOSITORY_ROOT))


if __name__ == "__main__":
    unittest.main()
