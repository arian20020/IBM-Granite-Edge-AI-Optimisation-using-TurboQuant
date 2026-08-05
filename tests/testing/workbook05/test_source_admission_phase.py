from __future__ import annotations

import unittest

from scripts.testing.workbook05.source_admission_phase import calculate_phase_decision


SAFE_EVIDENCE_PATH = "routes/route-a/source-tree-runtime.json"
VALID_SHA256 = "0" * 64


def _measurement_report() -> dict[str, object]:
    """Return the smallest complete measurement-control report for phase tests."""

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
    *,
    blockers: list[dict[str, str]] | None = None,
) -> dict[str, object]:
    """Create one phase-specific route record with source-admission proofs only."""

    return {
        "route_id": route_id,
        "admission_status": admission_status,
        "decision_reason": "The source-admission evidence supports this route state.",
        "proofs": [
            {
                "proof_id": f"{route_id}-source-proof",
                "required": True,
                "status": "Passed",
                "evidence_path": SAFE_EVIDENCE_PATH,
            }
        ],
        "known_blockers": blockers or [],
    }


class SourceAdmissionPhaseTests(unittest.TestCase):
    def test_route_a_admitted_and_route_b_blocked_allows_phase_to_pass(self) -> None:
        route_a = _route_record("route-a-merged-openvino", "Admitted")
        route_b = _route_record(
            "route-b-experimental-qjl-polar",
            "Blocked",
            blockers=[
                {
                    "blocker_id": "RB-SRC-001",
                    "status": "Open",
                    "description": "Required functional-test sources may be omitted.",
                }
            ],
        )

        decision = calculate_phase_decision(
            route_a,
            route_b,
            _measurement_report(),
        )

        self.assertEqual("Admitted", decision.route_a_status)
        self.assertEqual("Blocked", decision.route_b_status)
        self.assertEqual("Passed", decision.checkpoint_status)
        self.assertTrue(any("Route A" in reason for reason in decision.reasons))
        self.assertTrue(any("Route B" in reason for reason in decision.reasons))


if __name__ == "__main__":
    unittest.main()
