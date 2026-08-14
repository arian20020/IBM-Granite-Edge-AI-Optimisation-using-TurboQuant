from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.diagnostic_selection import (
    APPROVED_CANDIDATE_ORDER,
    EXPECTED_RUNTIME_SOURCE_COMMIT,
    PATH_EQUIVALENT_BACKEND,
    DiagnosticCandidate,
    DiagnosticEvidence,
    DiagnosticStatus,
    evaluate_diagnostic_candidate,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SETTINGS_PATH = (
    REPOSITORY_ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "configurations"
    / "workbook05"
    / "phase3-diagnostic-candidates.json"
)


class Phase3DiagnosticSelectionTests(unittest.TestCase):
    def _candidate(self) -> DiagnosticCandidate:
        return DiagnosticCandidate("project-generated-stateful-ir")

    def test_complete_direct_path_evidence_is_path_equivalent(self) -> None:
        decision = evaluate_diagnostic_candidate(
            self._candidate(),
            DiagnosticEvidence(
                text_generation_succeeded=True,
                runtime_source_commit=EXPECTED_RUNTIME_SOURCE_COMMIT,
                backend=PATH_EQUIVALENT_BACKEND,
                stateful_execution_observed=True,
                sdpa_observed=True,
                kv_cache_observed=True,
                fallback_absent=True,
            ),
        )

        self.assertEqual(DiagnosticStatus.PATH_EQUIVALENT, decision.status)
        self.assertFalse(decision.activation_claim_authorised)
        self.assertFalse(decision.performance_claim_authorised)

    def test_text_generation_without_path_proof_is_harness_only(self) -> None:
        decision = evaluate_diagnostic_candidate(
            self._candidate(),
            DiagnosticEvidence(
                text_generation_succeeded=True,
                runtime_source_commit=None,
                backend=None,
                stateful_execution_observed=False,
                sdpa_observed=False,
                kv_cache_observed=False,
                fallback_absent=True,
            ),
        )

        self.assertEqual(DiagnosticStatus.HARNESS_ONLY, decision.status)
        self.assertIn("incomplete", decision.reasons[0].casefold())

    def test_wrong_runtime_source_or_observed_fallback_is_rejected(self) -> None:
        cases = (
            DiagnosticEvidence(
                text_generation_succeeded=True,
                runtime_source_commit="0" * 40,
                backend=PATH_EQUIVALENT_BACKEND,
                stateful_execution_observed=True,
                sdpa_observed=True,
                kv_cache_observed=True,
                fallback_absent=True,
            ),
            DiagnosticEvidence(
                text_generation_succeeded=True,
                runtime_source_commit=EXPECTED_RUNTIME_SOURCE_COMMIT,
                backend=PATH_EQUIVALENT_BACKEND,
                stateful_execution_observed=True,
                sdpa_observed=True,
                kv_cache_observed=True,
                fallback_absent=False,
            ),
        )
        for evidence in cases:
            with self.subTest(evidence=evidence):
                decision = evaluate_diagnostic_candidate(self._candidate(), evidence)
                self.assertEqual(DiagnosticStatus.REJECTED, decision.status)

    def test_unapproved_candidate_is_rejected_before_classification(self) -> None:
        with self.assertRaisesRegex(ValueError, "approved order"):
            evaluate_diagnostic_candidate(
                DiagnosticCandidate("arbitrary-model"),
                DiagnosticEvidence(
                    text_generation_succeeded=False,
                    runtime_source_commit=None,
                    backend=None,
                    stateful_execution_observed=False,
                    sdpa_observed=False,
                    kv_cache_observed=False,
                    fallback_absent=True,
                ),
            )

    def test_configuration_freezes_order_runtime_and_backend(self) -> None:
        settings = json.loads(SETTINGS_PATH.read_text(encoding="utf-8"))

        self.assertEqual(list(APPROVED_CANDIDATE_ORDER), settings["candidate_order"])
        self.assertEqual(
            EXPECTED_RUNTIME_SOURCE_COMMIT,
            settings["required_runtime_source_commit"],
        )
        self.assertEqual(PATH_EQUIVALENT_BACKEND, settings["required_backend"])
        self.assertFalse(settings["generation_alone_proves_path_equivalence"])


if __name__ == "__main__":
    unittest.main()
