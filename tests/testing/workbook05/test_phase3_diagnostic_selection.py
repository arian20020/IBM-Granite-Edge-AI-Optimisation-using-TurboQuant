from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.diagnostic_selection import (
    APPROVED_CANDIDATE_ORDER,
    EXPECTED_BACKEND,
    EXPECTED_RUNTIME_SOURCE_COMMIT,
    DiagnosticCandidate,
    DiagnosticEvidence,
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
    def _candidate(
        self,
        candidate_id: str = "project-generated-stateful-ir",
    ) -> DiagnosticCandidate:
        return DiagnosticCandidate(
            candidate_id=candidate_id,
            repository="project/local-diagnostic",
            resolved_revision="a" * 40,
            local_asset_id="MODEL-WB05-DIAGNOSTIC",
        )

    def _evidence(
        self,
        *,
        runtime_source_commit: str = EXPECTED_RUNTIME_SOURCE_COMMIT,
        backend: str = EXPECTED_BACKEND,
        stateful: bool = True,
        sdpa: bool = True,
        kv_cache: bool = True,
        fallback_absent: bool = True,
        trace: bool = True,
    ) -> DiagnosticEvidence:
        return DiagnosticEvidence(
            text_generation_completed=True,
            runtime_source_commit=runtime_source_commit,
            backend=backend,
            stateful_execution_observed=stateful,
            sdpa_path_observed=sdpa,
            kv_cache_observed=kv_cache,
            fallback_absent=fallback_absent,
            trace_evidence_path=(
                "traces/stateful-sdpa.json" if trace else None
            ),
            trace_evidence_sha256=("b" * 64 if trace else None),
        )

    def test_complete_digest_bound_path_evidence_is_path_equivalent(self) -> None:
        decision = evaluate_diagnostic_candidate(
            self._candidate(),
            self._evidence(),
        )

        self.assertEqual("PathEquivalent", decision.status)
        self.assertTrue(decision.path_equivalence_authorised)
        self.assertTrue(decision.process_harness_use_authorised)
        self.assertFalse(decision.codec_activation_claim_authorised)
        self.assertFalse(decision.performance_claim_authorised)

    def test_generation_without_complete_path_trace_is_harness_only(self) -> None:
        decision = evaluate_diagnostic_candidate(
            self._candidate(),
            self._evidence(
                stateful=False,
                sdpa=False,
                kv_cache=False,
                trace=False,
            ),
        )

        self.assertEqual("HarnessOnly", decision.status)
        self.assertFalse(decision.path_equivalence_authorised)
        self.assertTrue(decision.process_harness_use_authorised)
        self.assertIn("not available", decision.reasons[0].casefold())

    def test_wrong_runtime_or_observed_fallback_is_rejected(self) -> None:
        cases = (
            self._evidence(runtime_source_commit="0" * 40),
            self._evidence(fallback_absent=False),
        )
        for evidence in cases:
            with self.subTest(evidence=evidence):
                decision = evaluate_diagnostic_candidate(
                    self._candidate(),
                    evidence,
                )
                self.assertEqual("Rejected", decision.status)
                self.assertFalse(decision.path_equivalence_authorised)

    def test_unapproved_candidate_is_rejected_without_weaker_admission(self) -> None:
        decision = evaluate_diagnostic_candidate(
            self._candidate("arbitrary-model"),
            self._evidence(
                stateful=False,
                sdpa=False,
                kv_cache=False,
                trace=False,
            ),
        )

        self.assertEqual("Rejected", decision.status)
        self.assertFalse(decision.process_harness_use_authorised)
        self.assertIn("approved candidate order", decision.reasons[0].casefold())

    def test_configuration_freezes_order_runtime_and_backend(self) -> None:
        settings = json.loads(SETTINGS_PATH.read_text(encoding="utf-8"))

        self.assertEqual(
            list(APPROVED_CANDIDATE_ORDER),
            settings["candidate_order"],
        )
        self.assertEqual(
            EXPECTED_RUNTIME_SOURCE_COMMIT,
            settings["required_runtime_source_commit"],
        )
        self.assertEqual(EXPECTED_BACKEND, settings["required_backend"])


if __name__ == "__main__":
    unittest.main()
