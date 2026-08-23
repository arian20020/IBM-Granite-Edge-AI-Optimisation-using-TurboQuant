from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.build_contracts import validate_build_record


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
DECISION_PATH = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/manifests/campaigns/"
    "GTQ-WB05-MF-v1/route-a-phase-2-build-decision.json"
)
CLOSURE_PATH = (
    REPOSITORY_ROOT
    / "docs/testing/workbook05/2026-08-13-route-a-phase-2-closure.md"
)


class RouteAPhase2ClosureTests(unittest.TestCase):
    def test_decision_is_schema_valid_build_candidate(self) -> None:
        payload = json.loads(DECISION_PATH.read_text(encoding="utf-8"))

        self.assertEqual(
            [],
            validate_build_record("decision", payload, REPOSITORY_ROOT),
        )
        self.assertEqual("BuildCandidate", payload["status"])
        self.assertEqual("route-a-merged-openvino", payload["route_id"])
        self.assertEqual("route", payload["component"])
        self.assertEqual(
            [
                ("runtime", "Passed"),
                ("genai", "Passed"),
            ],
            [
                (item["component"], item["status"])
                for item in payload["required_components"]
            ],
        )
        self.assertTrue(
            all(
                payload[name] is False
                for name in (
                    "granite_model_test_authorised",
                    "activation_claim_authorised",
                    "packed_storage_claim_authorised",
                    "performance_claim_authorised",
                    "quality_claim_authorised",
                )
            )
        )

    def test_closure_records_exact_runtime_and_genai_evidence(self) -> None:
        text = CLOSURE_PATH.read_text(encoding="utf-8")
        required = (
            "31656417607",
            "workbook-05-build-route-a-runtime-resume-31656417607-1",
            "9165704574",
            "6fb86648780faf7e142cee2260369a567a228ed0ae1147d2034c2fd3f32e5fca",
            "5dae00b38edb9a20f2d82a99dcf4cd1e2d4e7aeaaf6984873ccc55abccf3ae38",
            "31661571860",
            "workbook-05-build-route-a-genai-31661571860-1",
            "9167835183",
            "a70492bdabc6ce5a9334b9a43eb51309193f65ee4452b823329c6c9b573ae023",
            "0f273e1f345e1512e8e159af9b83f9ad521a26aa5e0ae813f2599161a5cb4a79",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            "bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0",
            "C:\\w5a\\phase2-31391119557-4\\i-ov",
            "C:\\w5a\\phase2-31661571860-1\\i-genai",
            "18,589 warnings",
            "BuildCandidate",
            "Model execution authorised: no",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, text)


if __name__ == "__main__":
    unittest.main()
