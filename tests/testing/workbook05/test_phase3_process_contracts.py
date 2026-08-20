"""Contracts for Workbook 05 C2 process evidence.

These tests are intentionally written before the C2 schemas and templates.  They
freeze the record vocabulary and the fail-closed relationships needed by later
process, activation, storage, performance, and quality packages.
"""

from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.contracts import validate_phase3_record


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/manifests/templates/workbook05"
)


class Phase3ProcessContractTests(unittest.TestCase):
    """Require closed schemas for every process-harness record."""

    def _load(self, name: str) -> dict[str, object]:
        return json.loads((TEMPLATE_ROOT / name).read_text(encoding="utf-8"))

    def test_templates_validate(self) -> None:
        for record_type, filename in {
            "process-attempt": "process-attempt-template.json",
            "resource-summary": "resource-summary-template.json",
            "phase3-checkpoint": "phase3-checkpoint-template.json",
        }.items():
            with self.subTest(record_type=record_type):
                payload = self._load(filename)
                self.assertEqual(
                    [],
                    validate_phase3_record(record_type, payload, REPOSITORY_ROOT),
                )

    def test_passed_attempt_cannot_report_a_safety_stop(self) -> None:
        payload = self._load("process-attempt-template.json")
        payload["classification"] = "Passed"
        payload["watchdog"]["safety_stop_triggered"] = True
        payload["watchdog"]["safety_stop_reason"] = "LOW_AVAILABLE_MEMORY"

        self.assertNotEqual(
            [],
            validate_phase3_record("process-attempt", payload, REPOSITORY_ROOT),
        )

    def test_passed_attempt_requires_zero_exit_code(self) -> None:
        payload = self._load("process-attempt-template.json")
        payload["classification"] = "Passed"
        payload["execution"]["exit_code"] = 7

        self.assertNotEqual(
            [],
            validate_phase3_record("process-attempt", payload, REPOSITORY_ROOT),
        )

    def test_retry_attempt_requires_prior_attempt_identity(self) -> None:
        payload = self._load("process-attempt-template.json")
        payload["attempt_number"] = 2
        payload["retry_of_attempt_id"] = None

        self.assertNotEqual(
            [],
            validate_phase3_record("process-attempt", payload, REPOSITORY_ROOT),
        )

    def test_checkpoint_passed_step_requires_evidence_digest(self) -> None:
        payload = self._load("phase3-checkpoint-template.json")
        payload["steps"][0]["status"] = "Passed"
        payload["steps"][0]["evidence_sha256"] = None

        self.assertNotEqual(
            [],
            validate_phase3_record("phase3-checkpoint", payload, REPOSITORY_ROOT),
        )

    def test_evidence_paths_are_repository_relative(self) -> None:
        payload = self._load("process-attempt-template.json")
        payload["evidence"]["stdout_path"] = r"C:\outside\stdout.txt"

        self.assertNotEqual(
            [],
            validate_phase3_record("process-attempt", payload, REPOSITORY_ROOT),
        )

    def test_closed_schema_rejects_unreviewed_fields(self) -> None:
        payload = copy.deepcopy(self._load("resource-summary-template.json"))
        payload["invented_metric"] = 1

        self.assertNotEqual(
            [],
            validate_phase3_record("resource-summary", payload, REPOSITORY_ROOT),
        )


if __name__ == "__main__":
    unittest.main()
