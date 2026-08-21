"""Opening RED contracts for Workbook 05 Stage E3 Granite 30B bounded feasibility."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.contracts import validate_phase3_record


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = REPOSITORY_ROOT / "experiments/granite_turboquant_intel/manifests/templates/workbook05"


class StageE3Granite30bFeasibilityContractTests(unittest.TestCase):
    def _load(self, name: str) -> dict[str, object]:
        return json.loads((TEMPLATE_ROOT / name).read_text(encoding="utf-8"))

    def test_e3_template_validates(self) -> None:
        payload = self._load("bounded-large-model-feasibility-template.json")
        self.assertEqual([], validate_phase3_record("bounded-large-model-feasibility", payload, REPOSITORY_ROOT))

    def test_thirty_b_gate_is_bound_to_official_granite_repository(self) -> None:
        payload = self._load("bounded-large-model-feasibility-template.json")
        payload["model_repository"] = "some-other/model"
        self.assertNotEqual([], validate_phase3_record("bounded-large-model-feasibility", payload, REPOSITORY_ROOT))

    def test_thirty_b_candidates_must_run_lowest_weight_first(self) -> None:
        payload = self._load("bounded-large-model-feasibility-template.json")
        payload["model_repository"] = "ibm-granite/granite-4.1-30b"
        payload["lowest_weight_first_verified"] = False
        payload["decision"] = "Feasible"
        self.assertNotEqual([], validate_phase3_record("bounded-large-model-feasibility", payload, REPOSITORY_ROOT))

    def test_feasibility_cannot_authorise_formal_performance_claims(self) -> None:
        payload = self._load("bounded-large-model-feasibility-template.json")
        payload["formal_performance_authorised"] = True
        self.assertNotEqual([], validate_phase3_record("bounded-large-model-feasibility", payload, REPOSITORY_ROOT))


if __name__ == "__main__":
    unittest.main()
