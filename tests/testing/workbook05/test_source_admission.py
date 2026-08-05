from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.source_admission import evaluate_source_admission


ROOT = Path(__file__).resolve().parents[3]
CAMPAIGN_ROOT = ROOT / "experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1"
SAFE_EVIDENCE_PATH = "evidence/proof.json"


def _mark_required_proofs_passed(record: dict[str, object]) -> None:
    """Populate every required proof with a safe evidence reference."""

    for proof in record["proofs"]:
        if proof["required"]:
            proof["status"] = "Passed"
            proof["evidence_path"] = SAFE_EVIDENCE_PATH


class SourceAdmissionTests(unittest.TestCase):
    def test_route_b_is_blocked_while_the_open_blocker_exists(self) -> None:
        record = json.loads(
            (CAMPAIGN_ROOT / "route-b-source-admission.json").read_text(
                encoding="utf-8"
            )
        )
        decision = evaluate_source_admission(record)

        self.assertFalse(decision.permitted)
        self.assertEqual("Blocked", decision.calculated_status)
        self.assertTrue(any("RB-SRC-001" in reason for reason in decision.reasons))

    def test_admitted_status_requires_every_required_proof(self) -> None:
        record = json.loads(
            (CAMPAIGN_ROOT / "route-a-source-admission.json").read_text(
                encoding="utf-8"
            )
        )
        record["admission_status"] = "Admitted"
        decision = evaluate_source_admission(record)

        self.assertFalse(decision.permitted)
        self.assertEqual("Candidate", decision.calculated_status)
        self.assertTrue(any("required proof" in reason for reason in decision.reasons))

    def test_accepted_risk_blocker_prevents_admission(self) -> None:
        record = json.loads(
            (CAMPAIGN_ROOT / "route-b-source-admission.json").read_text(
                encoding="utf-8"
            )
        )
        _mark_required_proofs_passed(record)
        record["known_blockers"][0]["status"] = "Accepted risk"
        record["admission_status"] = "Admitted"

        decision = evaluate_source_admission(record)

        self.assertFalse(decision.permitted)
        self.assertEqual("Blocked", decision.calculated_status)
        self.assertTrue(any("Accepted risk" in reason for reason in decision.reasons))

    def test_unsafe_passed_proof_path_prevents_admission(self) -> None:
        record = json.loads(
            (CAMPAIGN_ROOT / "route-a-source-admission.json").read_text(
                encoding="utf-8"
            )
        )
        _mark_required_proofs_passed(record)
        record["proofs"][0]["evidence_path"] = "../escape.json"
        record["admission_status"] = "Admitted"

        decision = evaluate_source_admission(record)

        self.assertFalse(decision.permitted)
        self.assertEqual("Blocked", decision.calculated_status)
        self.assertTrue(any("unsafe evidence_path" in reason for reason in decision.reasons))

    def test_blank_decision_reason_prevents_admission(self) -> None:
        record = json.loads(
            (CAMPAIGN_ROOT / "route-a-source-admission.json").read_text(
                encoding="utf-8"
            )
        )
        _mark_required_proofs_passed(record)
        record["admission_status"] = "Admitted"
        record["decision_reason"] = ""

        decision = evaluate_source_admission(record)

        self.assertFalse(decision.permitted)
        self.assertEqual("Blocked", decision.calculated_status)
        self.assertTrue(any("decision_reason" in reason for reason in decision.reasons))

    def test_record_can_be_admitted_after_proofs_and_blockers_pass(self) -> None:
        record = json.loads(
            (CAMPAIGN_ROOT / "route-b-source-admission.json").read_text(
                encoding="utf-8"
            )
        )
        _mark_required_proofs_passed(record)
        for blocker in record["known_blockers"]:
            blocker["status"] = "Resolved"
        record["admission_status"] = "Admitted"
        record["decision_reason"] = "All required source-admission proofs passed."

        decision = evaluate_source_admission(record)
        self.assertTrue(decision.permitted)
        self.assertEqual("Admitted", decision.calculated_status)
        self.assertEqual((), decision.reasons)


if __name__ == "__main__":
    unittest.main()
