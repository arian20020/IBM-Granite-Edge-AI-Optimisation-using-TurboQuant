from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.dependency_acceptance import (
    ACCEPTED_DEPENDENCY_ARTIFACT_SHA256,
    ACCEPTED_DEPENDENCY_DECISION_SHA256,
    ACCEPTED_DEPENDENCY_RUN_ATTEMPT,
    ACCEPTED_DEPENDENCY_RUN_ID,
    ACCEPTED_DEPENDENCY_WORKSPACE,
    validate_dependency_acceptance_record,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
ACCEPTANCE_PATH = (
    REPOSITORY_ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "manifests"
    / "campaigns"
    / "GTQ-WB05-MF-v1"
    / "phase3"
    / "accepted-dependency-preflight.json"
)


class Phase3DependencyAcceptanceBindingTests(unittest.TestCase):
    def setUp(self) -> None:
        self.record = json.loads(ACCEPTANCE_PATH.read_text(encoding="utf-8"))

    def test_committed_record_binds_the_independently_accepted_attempt(self) -> None:
        self.assertEqual("32211117536", ACCEPTED_DEPENDENCY_RUN_ID)
        self.assertEqual(1, ACCEPTED_DEPENDENCY_RUN_ATTEMPT)
        self.assertEqual(
            "b68a4f8af8c57a9f5d71347d2485855796f4d0dce0291c50b596512c309c0c21",
            ACCEPTED_DEPENDENCY_ARTIFACT_SHA256,
        )
        self.assertEqual(
            "429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49",
            ACCEPTED_DEPENDENCY_DECISION_SHA256,
        )
        self.assertEqual(
            r"C:\w5c\dependency-preflight-32211117536-1",
            ACCEPTED_DEPENDENCY_WORKSPACE,
        )
        self.assertEqual([], validate_dependency_acceptance_record(self.record))

    def test_record_rejects_a_different_decision_digest(self) -> None:
        changed = copy.deepcopy(self.record)
        changed["decision_sha256"] = "0" * 64
        issues = validate_dependency_acceptance_record(changed)
        self.assertTrue(any(issue.code == "DECISION_SHA256_MISMATCH" for issue in issues))

    def test_record_rejects_disagreement_between_artifact_digests(self) -> None:
        changed = copy.deepcopy(self.record)
        changed["independent_artifact_sha256"] = "1" * 64
        issues = validate_dependency_acceptance_record(changed)
        self.assertTrue(any(issue.code == "ARTIFACT_SHA256_MISMATCH" for issue in issues))

    def test_record_rejects_run_attempt_workspace_disagreement(self) -> None:
        changed = copy.deepcopy(self.record)
        changed["run_attempt"] = 2
        issues = validate_dependency_acceptance_record(changed)
        self.assertTrue(any(issue.code == "WORKSPACE_IDENTITY_MISMATCH" for issue in issues))

    def test_record_keeps_model_and_scientific_authorisations_false(self) -> None:
        for key in (
            "model_download_authorised",
            "granite_model_test_authorised",
            "activation_claim_authorised",
            "packed_storage_claim_authorised",
            "performance_claim_authorised",
            "quality_claim_authorised",
        ):
            with self.subTest(key=key):
                self.assertIs(False, self.record[key])


if __name__ == "__main__":
    unittest.main()
