"""Opening RED contracts for Workbook 05 C5 standard-cache smoke evidence.

The Lenovo is intentionally not involved at this checkpoint.  These tests freeze the
repository-only smoke records and non-claim boundary before any live Granite smoke
workflow can be implemented or dispatched.
"""

from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.contracts import validate_phase3_record


# Keep this test independent of machine-local C:\\w5* state.
REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/manifests/templates/workbook05"
)


class Phase3SmokeContractTests(unittest.TestCase):
    """Require strict smoke records without weakening the formal measured-run schema."""

    def _load(self, filename: str) -> dict[str, object]:
        """Load one future C5 template as repository-controlled JSON."""

        path = TEMPLATE_ROOT / filename
        self.assertTrue(path.is_file(), f"Missing C5 template: {path}")
        return json.loads(path.read_text(encoding="utf-8"))

    def test_smoke_templates_validate(self) -> None:
        """C5 must own dedicated smoke records rather than fabricating formal runs."""

        bindings = {
            "smoke-repetition": "smoke-repetition-template.json",
            "smoke-summary": "smoke-summary-template.json",
        }

        for record_type, filename in bindings.items():
            with self.subTest(record_type=record_type):
                payload = self._load(filename)
                self.assertEqual(
                    [],
                    validate_phase3_record(record_type, payload, REPOSITORY_ROOT),
                )

    def test_passed_summary_requires_exact_five_role_order(self) -> None:
        """Pilot, warm-up, and three measured rows are a fixed smoke sequence."""

        payload = copy.deepcopy(self._load("smoke-summary-template.json"))
        payload["status"] = "Passed"
        payload["repetitions"] = payload["repetitions"][:-1]

        self.assertNotEqual(
            [],
            validate_phase3_record("smoke-summary", payload, REPOSITORY_ROOT),
        )

    def test_every_smoke_row_is_excluded_from_formal_statistics(self) -> None:
        """A smoke run must never be silently promoted into the formal benchmark set."""

        payload = copy.deepcopy(self._load("smoke-repetition-template.json"))
        payload["included_in_formal_statistics"] = True

        self.assertNotEqual(
            [],
            validate_phase3_record("smoke-repetition", payload, REPOSITORY_ROOT),
        )

    def test_scalar_control_cannot_claim_turboquant(self) -> None:
        """The standard-cache baseline must keep optimisation claims false."""

        payload = copy.deepcopy(self._load("smoke-repetition-template.json"))
        payload["configuration_id"] = "RA-SCALAR-U8-SYM"
        payload["optimisation_requested"] = True
        payload["optimisation_activated"] = True

        self.assertNotEqual(
            [],
            validate_phase3_record("smoke-repetition", payload, REPOSITORY_ROOT),
        )

    def test_smoke_requires_cpu_actual_device(self) -> None:
        """The first C5 baseline is the reviewed CPU route only."""

        payload = copy.deepcopy(self._load("smoke-repetition-template.json"))
        payload["actual_device"] = "GPU"

        self.assertNotEqual(
            [],
            validate_phase3_record("smoke-repetition", payload, REPOSITORY_ROOT),
        )

    def test_summary_cannot_claim_formal_quality_or_maximum_context(self) -> None:
        """C5 proves harness stability only, not later scientific conclusions."""

        payload = copy.deepcopy(self._load("smoke-summary-template.json"))
        payload["claims"]["formal_quality_proven"] = True
        payload["claims"]["maximum_context_proven"] = True

        self.assertNotEqual(
            [],
            validate_phase3_record("smoke-summary", payload, REPOSITORY_ROOT),
        )


if __name__ == "__main__":
    unittest.main()
