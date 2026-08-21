"""Opening RED contracts for Workbook 05 C3 activation and storage conformance.

C3 is repository-only at this checkpoint.  These tests deliberately describe the
missing evidence contracts before any probe, trace build, model execution, or live
self-hosted workflow is implemented.
"""

from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.contracts import validate_phase3_record


# Resolve all paths from the checked-out repository rather than from machine state.
REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/manifests/templates/workbook05"
)


class Phase3ActivationContractTests(unittest.TestCase):
    """Freeze the fail-closed C3 record vocabulary before production code exists."""

    def _load(self, filename: str) -> dict[str, object]:
        """Load one repository-controlled template as UTF-8 JSON."""

        path = TEMPLATE_ROOT / filename
        self.assertTrue(path.is_file(), f"Missing C3 template: {path}")
        return json.loads(path.read_text(encoding="utf-8"))

    def test_c3_templates_validate(self) -> None:
        """All four C3 records must exist and be registered in the shared validator."""

        bindings = {
            "probe-run-request": "probe-run-request-template.json",
            "activation-proof": "activation-proof-template.json",
            "storage-proof": "storage-proof-template.json",
            "conformance-result": "conformance-result-template.json",
        }

        for record_type, filename in bindings.items():
            with self.subTest(record_type=record_type):
                payload = self._load(filename)
                self.assertEqual(
                    [],
                    validate_phase3_record(record_type, payload, REPOSITORY_ROOT),
                )

    def test_proven_activation_requires_dispatch_and_no_fallback(self) -> None:
        """Successful generation alone must never be enough to call TurboQuant proven."""

        payload = copy.deepcopy(self._load("activation-proof-template.json"))
        payload["confidence"] = "Proven"
        payload["dispatch"]["evidence_path"] = ""
        payload["fallback"]["observed"] = True

        self.assertNotEqual(
            [],
            validate_phase3_record("activation-proof", payload, REPOSITORY_ROOT),
        )

    def test_k_and_v_selection_must_remain_independent(self) -> None:
        """A combined KV precision field must not replace independent K and V evidence."""

        payload = copy.deepcopy(self._load("probe-run-request-template.json"))
        payload["kv_precision"] = "u3"
        payload.pop("key_cache", None)
        payload.pop("value_cache", None)

        self.assertNotEqual(
            [],
            validate_phase3_record("probe-run-request", payload, REPOSITORY_ROOT),
        )

    def test_route_b_labels_are_not_translated_into_route_a(self) -> None:
        """QJL and PolarQuant labels must remain unsupported on the Route A contract."""

        for forbidden_label in ("QJL3", "QJL4", "PolarQuant3", "PolarQuant4"):
            with self.subTest(label=forbidden_label):
                payload = copy.deepcopy(self._load("probe-run-request-template.json"))
                payload["key_cache"]["algorithm"] = forbidden_label
                payload["value_cache"]["algorithm"] = forbidden_label
                self.assertNotEqual(
                    [],
                    validate_phase3_record(
                        "probe-run-request",
                        payload,
                        REPOSITORY_ROOT,
                    ),
                )

    def test_passed_compressed_result_requires_reconciled_storage(self) -> None:
        """A compressed candidate cannot pass when physical K/V storage is unavailable."""

        payload = copy.deepcopy(self._load("conformance-result-template.json"))
        payload["status"] = "Passed"
        payload["key_storage_status"] = "Unavailable"
        payload["value_storage_status"] = "Unavailable"

        self.assertNotEqual(
            [],
            validate_phase3_record("conformance-result", payload, REPOSITORY_ROOT),
        )


if __name__ == "__main__":
    unittest.main()
