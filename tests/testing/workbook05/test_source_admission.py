from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.source_admission import evaluate_source_admission


ROOT = Path(__file__).resolve().parents[3]
CAMPAIGN_ROOT = ROOT / "experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1"


class SourceAdmissionTests(unittest.TestCase):
    def test_route_b_is_candidate_while_the_open_blocker_exists(self) -> None:
        record = json.loads((CAMPAIGN_ROOT / "route-b-source-admission.json").read_text(encoding="utf-8"))
        decision = evaluate_source_admission(record)

        self.assertFalse(decision.permitted)
        self.assertEqual("Candidate", decision.calculated_status)
        self.assertTrue(any("RB-SRC-001" in reason for reason in decision.reasons))

    def test_admitted_status_requires_every_required_proof(self) -> None:
        record = json.loads((CAMPAIGN_ROOT / "route-a-source-admission.json").read_text(encoding="utf-8"))
        record["admission_status"] = "Admitted"
        decision = evaluate_source_admission(record)

        self.assertFalse(decision.permitted)
        self.assertTrue(any("required proof" in reason for reason in decision.reasons))

    def test_record_can_be_admitted_after_proofs_and_blockers_pass(self) -> None:
        record = json.loads((CAMPAIGN_ROOT / "route-b-source-admission.json").read_text(encoding="utf-8"))
        for proof in record["proofs"]:
            if proof["required"]:
                proof["status"] = "Passed"
                proof["evidence_path"] = "evidence/proof.json"
        for blocker in record["known_blockers"]:
            blocker["status"] = "Resolved"
        record["admission_status"] = "Admitted"

        decision = evaluate_source_admission(record)
        self.assertTrue(decision.permitted)
        self.assertEqual("Admitted", decision.calculated_status)
        self.assertEqual((), decision.reasons)


if __name__ == "__main__":
    unittest.main()
