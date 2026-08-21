"""Opening RED contracts for Workbook 05 Stage E2 Granite 8B feasibility."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.contracts import validate_phase3_record


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = REPOSITORY_ROOT / "experiments/granite_turboquant_intel/manifests/templates/workbook05"


class StageE2Granite8bFeasibilityContractTests(unittest.TestCase):
    def _load(self, name: str) -> dict[str, object]:
        return json.loads((TEMPLATE_ROOT / name).read_text(encoding="utf-8"))

    def test_e2_template_validates(self) -> None:
        payload = self._load("model-feasibility-decision-template.json")
        self.assertEqual([], validate_phase3_record("model-feasibility-decision", payload, REPOSITORY_ROOT))

    def test_eight_b_gate_requires_exact_model_and_watchdog_evidence(self) -> None:
        payload = self._load("model-feasibility-decision-template.json")
        payload["model_repository"] = "ibm-granite/granite-4.1-8b"
        payload["decision"] = "Feasible"
        payload["watchdog_evidence_sha256"] = None
        self.assertNotEqual([], validate_phase3_record("model-feasibility-decision", payload, REPOSITORY_ROOT))

    def test_discovery_only_result_cannot_claim_formal_quality(self) -> None:
        payload = self._load("model-feasibility-decision-template.json")
        payload["evaluation_scope"] = "DiscoveryOnly"
        payload["formal_quality_authorised"] = True
        self.assertNotEqual([], validate_phase3_record("model-feasibility-decision", payload, REPOSITORY_ROOT))


if __name__ == "__main__":
    unittest.main()
