from __future__ import annotations

import unittest

from scripts.testing.workbook05.source_admission_phase import calculate_phase_decision


VALID_SHA256 = "0" * 64


def _measurement_report() -> dict[str, object]:
    """Return valid controls so only route-record integrity affects the result."""

    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "prompt_set_id": "GTQ-PROMPTS-v1",
        "rubric_id": "GTQ-QUALITY-RUBRIC-v1",
        "prompt_count": 6,
        "prompt_ids": ["P1", "P2", "P3", "P4", "P5", "P6"],
        "rubric_weight_total": 1.0,
        "required_metric_names": ["TTFT"],
        "controls": [
            {"path": f"controls/control-{index}.json", "sha256": VALID_SHA256}
            for index in range(6)
        ],
        "raw_output_required": True,
        "activation_proof_required": True,
        "fallback_result_required": True,
        "separate_k_v_allocation_required": True,
        "weight_and_kv_axes_separate": True,
    }


def _route_record(
    route_id: str,
    admission_status: str,
    evidence_path: str,
) -> dict[str, object]:
    """Create one route record with a single required Passed proof."""

    return {
        "route_id": route_id,
        "admission_status": admission_status,
        "decision_reason": "The recorded evidence explains this route state.",
        "proofs": [
            {
                "proof_id": f"{route_id}-proof",
                "required": True,
                "status": "Passed",
                "evidence_path": evidence_path,
            }
        ],
        "known_blockers": [],
    }


class SourceAdmissionPhaseIntegrityTests(unittest.TestCase):
    def test_corrupt_route_b_evidence_cannot_be_treated_as_truthful_blocking(self) -> None:
        route_a = _route_record(
            "route-a-merged-openvino",
            "Admitted",
            "routes/route-a/source-tree-runtime.json",
        )
        route_b = _route_record(
            "route-b-experimental-qjl-polar",
            "Blocked",
            "../escape.json",
        )

        decision = calculate_phase_decision(
            route_a,
            route_b,
            _measurement_report(),
        )

        self.assertEqual("Blocked", decision.route_b_status)
        self.assertEqual("Blocked", decision.checkpoint_status)
        self.assertTrue(any("unsafe evidence_path" in reason for reason in decision.reasons))


if __name__ == "__main__":
    unittest.main()
