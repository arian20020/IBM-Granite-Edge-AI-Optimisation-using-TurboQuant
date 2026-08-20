"""Deterministic classification and retry tests for Workbook 05 C2."""

from __future__ import annotations

import unittest

from scripts.testing.workbook05.phase3.process_policy import (
    AttemptDecision,
    AttemptObservation,
    classify_attempt,
    retry_allowed,
)


class Phase3ProcessPolicyTests(unittest.TestCase):
    """Freeze classification precedence before process orchestration exists."""

    def test_integrity_failure_has_highest_precedence(self) -> None:
        observation = AttemptObservation(
            exit_code=1,
            integrity_errors=("HASH_MISMATCH",),
            infrastructure_interrupted=True,
            safety_stop_reason="LOW_AVAILABLE_MEMORY",
        )

        decision = classify_attempt(observation)

        self.assertEqual("IntegrityFailure", decision.classification)
        self.assertIn("HASH_MISMATCH", decision.failure_ids)

    def test_resource_safety_stop_precedes_timeout(self) -> None:
        observation = AttemptObservation(
            exit_code=1,
            safety_stop_reason="HIGH_COMMIT_PERCENT",
            timeout_triggered=True,
        )

        self.assertEqual(
            "ResourceSafetyStop",
            classify_attempt(observation).classification,
        )

    def test_timeout_precedes_infrastructure_interruption(self) -> None:
        observation = AttemptObservation(
            exit_code=1,
            timeout_triggered=True,
            infrastructure_interrupted=True,
        )

        self.assertEqual("Timeout", classify_attempt(observation).classification)

    def test_malformed_output_is_not_an_infrastructure_retry(self) -> None:
        observation = AttemptObservation(
            exit_code=0,
            output_integrity_errors=("EVENT_ORDER_INVALID",),
        )
        decision = classify_attempt(observation)

        self.assertEqual("OutputIntegrityFailure", decision.classification)
        self.assertFalse(retry_allowed(decision, prior_attempt_count=0))

    def test_nonzero_model_exit_is_model_compatibility_failure(self) -> None:
        observation = AttemptObservation(
            exit_code=9,
            model_compatibility_errors=("MODEL_LOAD_REJECTED",),
        )

        self.assertEqual(
            "ModelCompatibilityFailure",
            classify_attempt(observation).classification,
        )

    def test_clean_zero_exit_is_passed(self) -> None:
        decision = classify_attempt(AttemptObservation(exit_code=0))

        self.assertEqual("Passed", decision.classification)
        self.assertEqual((), decision.failure_ids)

    def test_only_one_infrastructure_retry_is_allowed(self) -> None:
        decision = AttemptDecision(
            classification="InfrastructureInterrupted",
            failure_ids=("RUNNER_DISCONNECTED",),
            next_action="Retry once after cooldown.",
        )

        self.assertTrue(retry_allowed(decision, prior_attempt_count=0))
        self.assertFalse(retry_allowed(decision, prior_attempt_count=1))

    def test_later_c3_classes_keep_the_same_precedence_function(self) -> None:
        observation = AttemptObservation(
            exit_code=0,
            activation_unproven=True,
            storage_mismatch=True,
        )

        self.assertEqual(
            "ActivationUnproven",
            classify_attempt(observation).classification,
        )


if __name__ == "__main__":
    unittest.main()
