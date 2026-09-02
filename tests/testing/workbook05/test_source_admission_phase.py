from __future__ import annotations

import unittest

from scripts.testing.workbook05.source_admission_phase import (
    assemble_phase_outputs,
    calculate_phase_decision,
)


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


def _route_evidence_path(route_id: str) -> str:
    """Return one route-specific, safe, repository-relative evidence path."""

    return (
        "routes/route-a/source-tree-runtime.json"
        if route_id == "route-a-merged-openvino"
        else "routes/route-b/source-tree-runtime.json"
    )


def _route_record(
    route_id: str,
    admission_status: str,
    *,
    proof_status: str = "Passed",
    evidence_path: str | None = None,
    blockers: list[dict[str, str]] | None = None,
    decision_reason: str = "The source-admission evidence supports this route state.",
) -> dict[str, object]:
    """Create one phase-specific route record with source-admission proofs only."""

    return {
        "route_id": route_id,
        "admission_status": admission_status,
        "decision_reason": decision_reason,
        "proofs": [
            {
                "proof_id": f"{route_id}-source-proof",
                "required": True,
                "status": proof_status,
                "evidence_path": evidence_path or _route_evidence_path(route_id),
            }
        ],
        "known_blockers": blockers or [],
    }


def _checkpoint_template() -> dict[str, object]:
    """Return a deterministic campaign checkpoint candidate for assembly tests."""

    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "generation": 0,
        "phase_order": ["phase-0-preflight", "phase-1-source-admission"],
        "steps": [
            {
                "step_id": "phase-0-preflight",
                "status": "Passed",
                "evidence_sha256": "1" * 64,
            },
            {
                "step_id": "phase-1-source-admission",
                "status": "Not started",
                "evidence_sha256": "",
            },
        ],
        "updated_at_utc": "2026-08-05T00:00:00Z",
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
                    "severity": "Blocker",
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

    def test_failed_required_route_a_proof_blocks_phase(self) -> None:
        route_a = _route_record(
            "route-a-merged-openvino",
            "Admitted",
            proof_status="Failed",
        )
        route_b = _route_record(
            "route-b-experimental-qjl-polar",
            "Blocked",
        )

        decision = calculate_phase_decision(
            route_a,
            route_b,
            _measurement_report(),
        )

        self.assertEqual("Blocked", decision.route_a_status)
        self.assertEqual("Blocked", decision.checkpoint_status)
        self.assertTrue(any("required proof" in reason for reason in decision.reasons))

    def test_invalid_measurement_controls_block_phase_with_issue_code(self) -> None:
        measurement_report = _measurement_report()
        measurement_report["raw_output_required"] = False

        decision = calculate_phase_decision(
            _route_record("route-a-merged-openvino", "Admitted"),
            _route_record("route-b-experimental-qjl-polar", "Blocked"),
            measurement_report,
        )

        self.assertEqual("Blocked", decision.checkpoint_status)
        self.assertTrue(
            any("CONTROL_FLAG_DISABLED" in reason for reason in decision.reasons)
        )

    def test_unsafe_passed_proof_path_blocks_route_a(self) -> None:
        route_a = _route_record(
            "route-a-merged-openvino",
            "Admitted",
            evidence_path="../escape.json",
        )

        decision = calculate_phase_decision(
            route_a,
            _route_record("route-b-experimental-qjl-polar", "Blocked"),
            _measurement_report(),
        )

        self.assertEqual("Blocked", decision.route_a_status)
        self.assertEqual("Blocked", decision.checkpoint_status)
        self.assertTrue(any("unsafe evidence_path" in reason for reason in decision.reasons))

    def test_phase_outputs_include_summary_markdown_checkpoint_and_hash_inputs(self) -> None:
        outputs = assemble_phase_outputs(
            _route_record("route-a-merged-openvino", "Admitted"),
            _route_record("route-b-experimental-qjl-polar", "Blocked"),
            _measurement_report(),
            checkpoint_template=_checkpoint_template(),
            measurement_controls_path="measurement/measurement-controls.json",
            measurement_controls_sha256="2" * 64,
            summary_json_path="summary/source-admission-summary.json",
            summary_markdown_path="summary/source-admission-summary.md",
            checkpoint_path="checkpoint/checkpoint.json",
            hash_manifest_path="hash-manifest.sha256",
        )

        self.assertEqual("Passed", outputs.decision.checkpoint_status)
        self.assertEqual("Passed", outputs.summary["checkpoint_status"])
        self.assertEqual(
            "Admitted",
            outputs.summary["route_decisions"]["route-a-merged-openvino"]["status"],
        )
        self.assertEqual(
            "Blocked",
            outputs.summary["route_decisions"]["route-b-experimental-qjl-polar"]["status"],
        )
        phase_step = next(
            step
            for step in outputs.checkpoint_candidate["steps"]
            if step["step_id"] == "phase-1-source-admission"
        )
        self.assertEqual("Passed", phase_step["status"])
        self.assertEqual("", phase_step["evidence_sha256"])
        self.assertIn("Route A", outputs.summary_markdown)
        self.assertIn("Route B", outputs.summary_markdown)
        self.assertEqual(
            (
                "checkpoint/checkpoint.json",
                "measurement/measurement-controls.json",
                "routes/route-a/source-tree-runtime.json",
                "routes/route-b/source-tree-runtime.json",
                "summary/source-admission-summary.json",
                "summary/source-admission-summary.md",
            ),
            outputs.hash_manifest_inputs,
        )


if __name__ == "__main__":
    unittest.main()
