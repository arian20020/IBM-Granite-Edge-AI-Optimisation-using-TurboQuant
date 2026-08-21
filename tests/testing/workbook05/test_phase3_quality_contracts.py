"""Opening RED contracts for Workbook 05 C4 deterministic quality evidence.

C4 remains model-free at this checkpoint.  These tests freeze the records and
scientific guardrails that must exist before any quality evaluator implementation
is allowed to claim a scored result.
"""

from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.contracts import validate_phase3_record


# All test evidence is repository-controlled and independent of the Lenovo runner.
REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/manifests/templates/workbook05"
)


class Phase3QualityContractTests(unittest.TestCase):
    """Require closed C4 records before evaluator code is added."""

    def _load(self, filename: str) -> dict[str, object]:
        """Read one quality template without normalising its bytes."""

        path = TEMPLATE_ROOT / filename
        self.assertTrue(path.is_file(), f"Missing C4 template: {path}")
        return json.loads(path.read_text(encoding="utf-8"))

    def test_quality_templates_validate(self) -> None:
        """The deterministic, scoring, and adjudication records must all be registered."""

        bindings = {
            "deterministic-check-result": "deterministic-check-result-template.json",
            "quality-result": "quality-result-template.json",
            "adjudication-record": "adjudication-record-template.json",
        }

        for record_type, filename in bindings.items():
            with self.subTest(record_type=record_type):
                payload = self._load(filename)
                self.assertEqual(
                    [],
                    validate_phase3_record(record_type, payload, REPOSITORY_ROOT),
                )

    def test_passed_quality_requires_all_five_dimension_scores(self) -> None:
        """A nominal pass must not hide a missing rubric dimension."""

        payload = copy.deepcopy(self._load("quality-result-template.json"))
        payload["status"] = "Passed"
        payload["dimension_scores"]["stability_and_output_integrity"] = None

        self.assertNotEqual(
            [],
            validate_phase3_record("quality-result", payload, REPOSITORY_ROOT),
        )

    def test_raw_output_hash_is_mandatory(self) -> None:
        """Evaluation must remain bound to the exact raw bytes seen by the evaluator."""

        payload = copy.deepcopy(self._load("quality-result-template.json"))
        payload["raw_output_sha256"] = None

        self.assertNotEqual(
            [],
            validate_phase3_record("quality-result", payload, REPOSITORY_ROOT),
        )

    def test_pairwise_scoring_requires_hidden_labels(self) -> None:
        """Comparative evaluation cannot pass when the candidate identity is exposed."""

        payload = copy.deepcopy(self._load("quality-result-template.json"))
        payload["judge_label_hidden"] = False
        payload["status"] = "Passed"

        self.assertNotEqual(
            [],
            validate_phase3_record("quality-result", payload, REPOSITORY_ROOT),
        )

    def test_deterministic_failure_cannot_be_overridden_by_subjective_score(self) -> None:
        """A critical objective failure must survive even when the numeric score is high."""

        payload = copy.deepcopy(self._load("quality-result-template.json"))
        payload["deterministic_failures"] = ["WRONG_EXACT_ANSWER"]
        payload["critical_caps"] = []
        payload["score_0_to_10"] = 10.0
        payload["status"] = "Passed"

        self.assertNotEqual(
            [],
            validate_phase3_record("quality-result", payload, REPOSITORY_ROOT),
        )

    def test_adjudication_requires_named_evidence(self) -> None:
        """A disagreement cannot be closed by status text alone."""

        payload = copy.deepcopy(self._load("adjudication-record-template.json"))
        payload["status"] = "Adjudicated"
        payload["evidence_paths"] = []

        self.assertNotEqual(
            [],
            validate_phase3_record("adjudication-record", payload, REPOSITORY_ROOT),
        )


if __name__ == "__main__":
    unittest.main()
