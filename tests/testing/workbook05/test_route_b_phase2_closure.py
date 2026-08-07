from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.build_contracts import validate_build_record


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
DECISION_PATH = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/manifests/campaigns/"
    "GTQ-WB05-MF-v1/route-b-phase-2-build-decision.json"
)
CLOSURE_PATH = (
    REPOSITORY_ROOT
    / "docs/testing/workbook05/2026-08-06-route-b-phase-2-closure.md"
)


class RouteBPhase2ClosureTests(unittest.TestCase):
    def test_decision_is_schema_valid_and_blocked(self) -> None:
        payload = json.loads(DECISION_PATH.read_text(encoding="utf-8"))
        self.assertEqual([], validate_build_record("decision", payload, REPOSITORY_ROOT))
        self.assertEqual("Blocked", payload["status"])
        self.assertEqual("route-b-experimental-qjl-polar", payload["route_id"])
        self.assertEqual("route", payload["component"])
        self.assertTrue(all(payload[name] is False for name in (
            "granite_model_test_authorised",
            "activation_claim_authorised",
            "packed_storage_claim_authorised",
            "performance_claim_authorised",
            "quality_claim_authorised",
        )))

    def test_closure_records_the_exact_incomplete_artifact_boundary(self) -> None:
        text = CLOSURE_PATH.read_text(encoding="utf-8")
        required = (
            "31088162027",
            "workbook-05-route-b-repair-31088162027-4",
            "8974029140",
            "981bbaf75af4a4bd254d81cb2fe2c496f4971bfdd8423ed1ce65a5a1988213a8",
            "target-membership.json",
            "no `decision.json`",
            "no `manifest.sha256`",
            "no compiled-target result",
            "no six-case test result",
            "Blocked",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, text)


if __name__ == "__main__":
    unittest.main()
